// 
// Database.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using Newtonsoft.Json;
using ObjCRuntime;

namespace Couchbase.Lite
{
    /// <summary>
    /// A container for storing and maintaining Couchbase Lite <see cref="Document"/>s
    /// </summary>
    public sealed unsafe class Database : IDisposable
    {
        #region Constants

        private const string DBExtension = "cblite2";

        private static readonly C4DatabaseConfig _DBConfig = new C4DatabaseConfig {
            flags = C4DatabaseFlags.Create | C4DatabaseFlags.AutoCompact | C4DatabaseFlags.Bundled | C4DatabaseFlags.SharedKeys,
            storageEngine = "SQLite",
            versioning = C4DocumentVersioning.RevisionTrees
        };

        private static readonly DatabaseObserverCallback _DbObserverCallback;
        private static readonly DocumentObserverCallback _DocObserverCallback;

        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private static readonly C4LogCallback _LogCallback;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

        private const string Tag = nameof(Database);

        #endregion

        #region Variables

        private readonly SharedStringCache _sharedStrings;

        private readonly ThreadSafety _threadSafety = new ThreadSafety(true);
        private readonly HashSet<Document> _unsavedDocuments = new HashSet<Document>();
        private readonly FilteredEvent<string, DocumentChangedEventArgs> _documentChanged =
            new FilteredEvent<string, DocumentChangedEventArgs>();
        private readonly Dictionary<string, DocumentObserver> _docObs = new Dictionary<string, DocumentObserver>();

        /// <summary>
        /// An event fired whenever the database changes
        /// </summary>
        public event EventHandler<DatabaseChangedEventArgs> Changed;

        private IJsonSerializer _jsonSerializer;
        private DatabaseObserver _obs;
        private long p_c4db;
        

        #endregion

        #region Properties

        /// <summary>
        /// Gets the configuration that were used to create the database
        /// </summary>
        public DatabaseConfiguration Config { get; }

        /// <summary>
        /// Gets the total number of documents in the database
        /// </summary>
        public ulong Count => _threadSafety.LockedForRead(() => Native.c4db_getDocumentCount(_c4db));

        /// <summary>
        /// Bracket operator for retrieving <see cref="DocumentFragment"/> objects
        /// </summary>
        /// <param name="id">The ID of the <see cref="DocumentFragment"/> to retrieve</param>
        /// <returns>The instantiated <see cref="DocumentFragment"/></returns>
        public DocumentFragment this[string id] => new DocumentFragment(GetDocument(id));

        /// <summary>
        /// Gets the name of the database
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the path on disk where the database exists
        /// </summary>
        public string Path
        {
            get {
                return _threadSafety.LockedForRead(() => _c4db != null ? Native.c4db_getPath(c4db) : null);
            }
        }

        internal ICollection<Replicator> ActiveReplications { get; } = new HashSet<Replicator>();

        internal C4BlobStore* BlobStore
        {
            get {
                var retVal = default(C4BlobStore*);
                _threadSafety.LockedForRead(() =>
                {
                    CheckOpen();
                    retVal = (C4BlobStore*) LiteCoreBridge.Check(err => Native.c4db_getBlobStore(c4db, err));
                });

                return retVal;
            }
        }

        internal C4Database* c4db
        {
            get {
                var retVal = default(C4Database*);
                _threadSafety.LockedForRead(() => retVal = _c4db);
                return retVal;
            }
        }

        internal IJsonSerializer JsonSerializer
        {
            get => _jsonSerializer ?? (_jsonSerializer = Serializer.CreateDefaultFor(this));
            set => _jsonSerializer = value;
        }

        internal IDictionary<Uri, Replicator> Replications { get; } = new Dictionary<Uri, Replicator>();

        internal SharedStringCache SharedStrings => _threadSafety.LockedForRead(() => _sharedStrings);

        private C4Database *_c4db
        {
            get => (C4Database *)p_c4db;
            set => p_c4db = (long)value;
        }

        #endregion

        #region Constructors

        static Database()
        {
            _LogCallback = LiteCoreLog;
            Native.c4log_writeToCallback(C4LogLevel.Warning, _LogCallback, true);
            _DbObserverCallback = DbObserverCallback;
            _DocObserverCallback = DocObserverCallback;
        }

        /// <summary>
        /// Creates a database instance with the given name.  Internally
        /// it may be operating on the same underlying data as another instance.
        /// </summary>
        /// <param name="name">The name of the database</param>
        /// <returns>The instantiated database object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>name</c> is <c>null</c></exception>
        /// <exception cref="ArgumentException"><c>name</c> contains invalid characters</exception> 
        /// <exception cref="LiteCoreException">An error occurred during LiteCore interop</exception>
        public Database(string name) : this(name, new DatabaseConfiguration())
        {
            
        }

        /// <summary>
        /// Creates a database given a name and some configuration
        /// </summary>
        /// <param name="name">The name of the database</param>
        /// <param name="configuration">The configuration to open it with</param>
        public Database(string name, DatabaseConfiguration configuration) 
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Config = configuration;
            Open();
            _sharedStrings = new SharedStringCache(_c4db);
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">The database to copy from</param>
        public Database(Database other)
            : this(other.Name, other.Config)
        {
            
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~Database()
        {
            Dispose(false);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Deletes the contents of a database with the given name in the
        /// given directory
        /// </summary>
        /// <param name="name">The name of the database to delete</param>
        /// <param name="directory">The directory to search in</param>
        public static void Delete(string name, string directory)
        {
            if(name == null) {
                throw new ArgumentNullException(nameof(name));
            }

            var path = DatabasePath(name, directory);
            LiteCoreBridge.Check(err =>
            {
                var localConfig = _DBConfig;
                return Native.c4db_deleteAtPath(path, &localConfig, err) || err->code == 0;
            });
        }

        /// <summary>
        /// Returns whether or not a database with the given name
        /// exists in the given directory
        /// </summary>
        /// <param name="name">The name of the database to search for</param>
        /// <param name="directory">The directory to search in</param>
        /// <returns><c>true</c> if the database exists in the directory, otherwise <c>false</c></returns>
        public static bool Exists(string name, string directory)
        {
            if(name == null) {
                throw new ArgumentNullException(nameof(name));
            }

            return System.IO.Directory.Exists(DatabasePath(name, directory));
        }

        /// <summary>
        /// Adds a listener for changes on a certain document (by ID).  Similar to <see cref="Database.Changed"/>
        /// but requires a parameter to add so it is a method instead of an event.
        /// </summary>
        /// <param name="documentID">The ID to add the listener for</param>
        /// <param name="handler">The logic to handle the event</param>
        public void AddDocumentChangedListener(string documentID, EventHandler<DocumentChangedEventArgs> handler)
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();

                var count = _documentChanged.Add(documentID, handler);
                if (count == 0) {
                    var docObs = new DocumentObserver(_c4db, documentID, _DocObserverCallback, this);
                    _docObs[documentID] = docObs;
                }
            });
        }

        internal void ChangeEncryptionKey(object key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Closes the database
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Checks whether or not a given <see cref="Document"/> exists 
        /// in this database instance by searching for its ID.
        /// </summary>
        /// <param name="docID">The ID of the <see cref="Document"/> to search for</param>
        /// <returns><c>true</c> if the <see cref="Document"/> exists, <c>false</c> otherwise</returns>
        public bool Contains(string docID)
        {
            return _threadSafety.LockedForRead(() =>
            {
                CheckOpen();
                using (var doc = GetDocument(docID, true)) {
                    return doc != null;
                }
            });
        }

        /// <summary>
        /// Performs a manual compaction of this database, removing old irrelevant data
        /// and decreasing the size of the database file on disk
        /// </summary>
        public void Compact()
        {
            _threadSafety.LockedForWrite(() => LiteCoreBridge.Check(err =>
            {
                CheckOpen();
                return Native.c4db_compact(_c4db, err);
            }));
        }

        /// <summary>
        /// Creates an index of the given type on the given path with the given configuration
        /// </summary>
        /// <param name="expressions">The expressions to create the index on (must be either string
        /// or IExpression)</param>
        /// <param name="indexType">The type of index to create</param>
        /// <param name="options">The configuration to apply to the index</param>
        public void CreateIndex(IList expressions, IndexType indexType, IndexOptions options)
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();
                var jsonObj = QueryExpression.EncodeToJSON(expressions);
                var json = JsonConvert.SerializeObject(jsonObj);
                LiteCoreBridge.Check(err =>
                {
                    if (options == null) {
                        return Native.c4db_createIndex(c4db, json, (C4IndexType) indexType, null, err);
                    } 

                    var localOpts = IndexOptions.Internal(options);
                    return Native.c4db_createIndex(c4db, json, (C4IndexType) indexType, &localOpts, err);
                });
            });
        }

        /// <summary>
        /// Creates an <see cref="IndexType.ValueIndex"/> index on the given path
        /// </summary>
        /// <param name="expressions">The expressions to create the index on</param>
        public void CreateIndex(IList<IExpression> expressions)
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();
                CreateIndex(expressions as IList, IndexType.ValueIndex, null);
            });
        }

        /// <summary>
        /// Deletes the database
        /// </summary>
        public void Delete()
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();
                LiteCoreBridge.Check(err => Native.c4db_delete(_c4db, err));
                Native.c4db_free(_c4db);
                _c4db = null;
                _obs?.Dispose();
                _obs = null;
            });
        }

        /// <summary>
        /// Deletes the given <see cref="Document"/> from the database
        /// </summary>
        /// <param name="document">The document to delete</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to delete a document from a database
        /// other than the one it was previously added to</exception>
        public void Delete(Document document)
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();
                VerifyDB(document).Delete();
            });
        }

        /// <summary>
        /// Deletes an index of the given <see cref="IndexType"/> on the given propertyPath
        /// </summary>
        /// <param name="propertyPath">The path of the index to delete</param>
        /// <param name="type">The type of the index to delete</param>
        public void DeleteIndex(string propertyPath, IndexType type)
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();
                LiteCoreBridge.Check(err => Native.c4db_deleteIndex(c4db, propertyPath, (C4IndexType) type, err));
            });
        }

        /// <summary>
        /// Checks whether a document with the given ID exists in the database
        /// </summary>
        /// <param name="docID">the ID to search for</param>
        /// <returns><c>true</c> if a document exists with that ID, <c>false</c> otherwise</returns>
        public bool DocumentExists(string docID)
        {
            return _threadSafety.LockedForRead(() =>
            {
                CheckOpen();
                using (var doc = GetDocument(docID, true)) {
                    return doc != null;
                }
            });
        }

        /// <summary>
        /// Gets the <see cref="Document"/> with the specified ID
        /// </summary>
        /// <param name="id">The ID to use when creating or getting the document</param>
        /// <returns>The instantiated document, or <c>null</c> if it does not exist</returns>
        public Document GetDocument(string id) => _threadSafety.LockedForRead(() => GetDocument(id, true));

        /// <summary>
        /// Runs the given batch of operations as an atomic unit
        /// </summary>
        /// <param name="a">The <see cref="Action"/> containing the operations. </param>
        public void InBatch(Action a)
        {
            _threadSafety.LockedForPossibleWrite(() =>
            {
                CheckOpen();
                PerfTimer.StartEvent("InBatch_BeginTransaction");
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(_c4db, err));
                PerfTimer.StopEvent("InBatch_BeginTransaction");
                var success = true;
                try {
                    a();
                } catch (Exception e) {
                    Log.To.Database.W(Tag, "Exception during InBatch, rolling back...", e);
                    success = false;
                    throw;
                } finally {
                    PerfTimer.StartEvent("InBatch_EndTransaction");
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(_c4db, success, err));
                    PerfTimer.StopEvent("InBatch_EndTransaction");
                }
            });

            PostDatabaseChanged();
        }

        /// <summary>
        /// Purges the given <see cref="Document"/> from the database.  This leaves
        /// no trace behind and will not be replicated
        /// </summary>
        /// <param name="document">The document to purge</param>
        /// <returns>Whether or not the document was actually purged.</returns>
        /// <exception cref="InvalidOperationException">Thrown when trying to purge a document from a database
        /// other than the one it was previously added to</exception>
        public void Purge(Document document)
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();
                VerifyDB(document).Purge();
            });
        }

        /// <summary>
        /// Removes a listener for changes on a certain document (by ID).  Similar to <see cref="Database.Changed"/>
        /// but requires a parameter to add so it is a method instead of an event.
        /// </summary>
        /// <param name="documentID">The ID to add the listener for</param>
        /// <param name="handler">The logic to handle the event</param>
        public void RemoveDocumentChangedListener(string documentID, EventHandler<DocumentChangedEventArgs> handler)
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();
                var count = _documentChanged.Remove(documentID, handler);
                if (count == 0) {
                    DocumentObserver obs;
                    if (_docObs.TryGetValue(documentID, out obs)) {
                        obs.Dispose();
                        _docObs.Remove(documentID);
                    }
                }
            });
        }

        /// <summary>
        /// Saves the given <see cref="Document"/> into this database
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        public void Save(Document document)
        {
            _threadSafety.LockedForWrite(() =>
            {
                CheckOpen();
                VerifyDB(document).Save();
            });
        }

        #endregion

        #region Internal Methods

        internal void SetHasUnsavedChanges(Document doc, bool hasChanges)
        {
            if (hasChanges) {
                _unsavedDocuments.Add(doc);
            } else {
                _unsavedDocuments.Remove(doc);
            }
        }

        #endregion

        #region Private Methods

        private static string DatabasePath(string name, string directory)
        {
            if (String.IsNullOrWhiteSpace(name)) {
                return directory;
            }

            return System.IO.Path.Combine(Directory(directory), $"{name}.{DBExtension}");
        }

        private static void DbObserverCallback(C4DatabaseObserver* db, object context)
        {
            Task.Factory.StartNew(() => {
              var dbObj = (Database)context;
              dbObj?.PostDatabaseChanged();
            });
        }

        private static void DocObserverCallback(C4DocumentObserver* obs, string docID, ulong sequence, object context)
        {
            Task.Factory.StartNew(() =>
            {
                var dbObj = (Database)context;
                dbObj?.PostDocChanged(docID);
            });
        }

        private static string DefaultDirectory()
        {
            return InjectableCollection.GetImplementation<IDefaultDirectoryResolver>().DefaultDirectory();
        }

        private static string Directory(string directory)
        {
            return directory ?? DefaultDirectory();
        }

        [MonoPInvokeCallback(typeof(C4LogCallback))]
        private static void LiteCoreLog(C4LogDomain* domain, C4LogLevel level, string message, IntPtr ignored)
        {
            var name = Native.c4log_getDomainName(domain);
            switch(level) {
                case C4LogLevel.Error:
                    Log.To.DomainOrLiteCore(name).E(name, message);
                    break;
                case C4LogLevel.Warning:
                    Log.To.DomainOrLiteCore(name).W(name, message);
                    break;
                case C4LogLevel.Info:
                    Log.To.DomainOrLiteCore(name).I(name, message);
                    break;
                case C4LogLevel.Verbose:
                    Log.To.DomainOrLiteCore(name).V(name, message);
                    break;
                case C4LogLevel.Debug:
                    Log.To.DomainOrLiteCore(name).D(name, message);
                    break;
            }
        }

        private void CheckOpen()
        {
            if(_c4db == null) {
                throw new InvalidOperationException("Attempt to perform an operation on a closed database");
            }
        }

        private void Dispose(bool disposing)
        {
            if (_c4db == null) {
                return;
            }

            Log.To.Database.I(Tag, $"Closing database at path {Native.c4db_getPath(_c4db)}");
            LiteCoreBridge.Check(err => Native.c4db_close(_c4db, err));
            Native.c4db_free(_c4db);
            _c4db = null;
            if (disposing) {
                var obs = Interlocked.Exchange(ref _obs, null);
                obs?.Dispose();
                if (_unsavedDocuments.Count > 0) {
                    Log.To.Database.W(Tag,
                        $"Closing database with {_unsavedDocuments.Count} such as {_unsavedDocuments.Any()}");
                }
                _unsavedDocuments.Clear();
            }
        }

        private Document GetDocument(string docID, bool mustExist)
        {
            CheckOpen();
            var doc = new Document(this, docID, mustExist);

            if (mustExist && !doc.Exists) {
                Log.To.Database.V(Tag, "Requested existing document {0}, but it doesn't exist", 
                    new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
                return null;
            }

            return doc;
        }

        private void Open()
        {
            if(_c4db != null) {
                return;
            }
            
            System.IO.Directory.CreateDirectory(Directory(Config.Directory));
            var path = DatabasePath(Name, Config.Directory);
            var config = _DBConfig;

            var encrypted = "";
            if(Config.EncryptionKey != null) {
#if true
                throw new NotImplementedException("Encryption is not yet supported");
#else
                var key = Config.EncryptionKey;
                int i = 0;
                config.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
                foreach(var b in key.KeyData) {
                    config.encryptionKey.bytes[i++] = b;
                }

                encrypted = "encrypted ";
#endif
            }

            Log.To.Database.I(Tag, $"Opening {encrypted}database at {path}");
            var localConfig1 = config;
            _c4db = (C4Database *)LiteCoreBridge.Check(err => {
                var localConfig2 = localConfig1;
                return Native.c4db_open(path, &localConfig2, err);
            });

            _obs = Native.c4dbobs_create(_c4db, _DbObserverCallback, this);
        }

        private void PostDatabaseChanged()
        {
			_threadSafety.LockedForRead(() =>
			{
				if (_obs == null || _c4db == null || Native.c4db_isInTransaction(_c4db)) {
					return;
				}

				const uint maxChanges = 100u;
                var external = false;
				uint nChanges;
				var changes = new C4DatabaseChange[maxChanges];
			    var docIDs = new List<string>();
				do {
					// Read changes in batches of MaxChanges:
					bool newExternal;
					nChanges = Native.c4dbobs_getChanges(_obs.Observer, changes, maxChanges, &newExternal);
				    if (nChanges == 0 || external != newExternal || docIDs.Count > 1000) {
				        if (docIDs.Count > 0) {
                            // Only notify if there are actually changes to send
				            var args = new DatabaseChangedEventArgs(this, docIDs, external);
				            Changed?.Invoke(this, args);
				            docIDs = new List<string>();
				        }
				    }

				    external = newExternal;
				    for (int i = 0; i < nChanges; i++) {
				        docIDs.Add(changes[i].docID.CreateString());
				    }
				} while (nChanges > 0);
			});
        }

        private void PostDocChanged(string documentID)
        {
            _threadSafety.LockedForRead(() =>
            {
                if (!_docObs.ContainsKey(documentID) || _c4db == null || Native.c4db_isInTransaction(_c4db)) {
                    return;
                }

                var change = new DocumentChangedEventArgs(documentID);
                _documentChanged.Fire(documentID, this, change);
            });
        }

        private Document VerifyDB(Document document)
        {
            if (document.Database == null) {
                document.Database = this;
            } else if (document.Database != this) {
                throw new CouchbaseLiteException(StatusCode.Forbidden, "Cannot operate on a document from another database");
            }

            return document;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _threadSafety.LockedForWrite(() => Dispose(true));
        }

        #endregion
    }
}
