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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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

namespace Couchbase.Lite
{
    /// <summary>
    /// A container for storing and maintaining Couchbase Lite <see cref="Document"/>s
    /// </summary>
    public sealed unsafe class Database : IDisposable
    {
        #region Constants

        private static readonly DatabaseObserverCallback _DbObserverCallback;
        private static readonly DocumentObserverCallback _DocObserverCallback;

        private static readonly C4DatabaseConfig DBConfig = new C4DatabaseConfig {
            flags = C4DatabaseFlags.Create | C4DatabaseFlags.AutoCompact | C4DatabaseFlags.Bundled | C4DatabaseFlags.SharedKeys,
            storageEngine = "SQLite",
            versioning = C4DocumentVersioning.RevisionTrees
        };

        private const string DBExtension = "cblite2";

        private const string Tag = nameof(Database);

        #endregion

        #region Variables

        private readonly Dictionary<string, DocumentObserver> _docObs = new Dictionary<string, DocumentObserver>();

        private readonly FilteredEvent<string, DocumentChangedEventArgs> _documentChanged =
            new FilteredEvent<string, DocumentChangedEventArgs>();

        private readonly SharedStringCache _sharedStrings;

        private readonly ThreadSafety _threadSafety = new ThreadSafety();
        private readonly HashSet<Document> _unsavedDocuments = new HashSet<Document>();

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
        public ulong Count => _threadSafety.DoLocked(() => Native.c4db_getDocumentCount(_c4db));

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
                return _threadSafety.DoLocked(() => _c4db != null ? Native.c4db_getPath(c4db) : null);
            }
        }

        internal ICollection<Replicator> ActiveReplications { get; } = new HashSet<Replicator>();

        internal C4BlobStore* BlobStore
        {
            get {
                var retVal = default(C4BlobStore*);
                _threadSafety.DoLocked(() =>
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
                _threadSafety.DoLocked(() => retVal = _c4db);
                return retVal;
            }
        }

        internal IJsonSerializer JsonSerializer
        {
            get => _jsonSerializer ?? (_jsonSerializer = Serializer.CreateDefaultFor(this));
            set => _jsonSerializer = value;
        }

        internal IDictionary<Uri, Replicator> Replications { get; } = new Dictionary<Uri, Replicator>();

        internal SharedStringCache SharedStrings => _threadSafety.DoLocked(() => _sharedStrings);

        private C4Database *_c4db
        {
            get => (C4Database *)p_c4db;
            set => p_c4db = (long)value;
        }

        #endregion

        #region Constructors

        static Database()
        {
            _DbObserverCallback = DbObserverCallback;
            _DocObserverCallback = DocObserverCallback;
			Log.To.NoDomain.I("Startup", HTTPLogic.UserAgent);
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
            try {
                Dispose(false);
            } catch (Exception e) {
                Log.To.Database.E(Tag, "Error during finalizer, swallowing!", e);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Copies a database from the given path to be used as the database with
        /// the given name and configuration
        /// </summary>
        /// <param name="path">The path (of the .cblite2 folder) to copy</param>
        /// <param name="name">The name of the database to be used when opening</param>
        /// <param name="config">The config to use when copying (for specifying directory, etc)</param>
        public static void Copy(string path, string name, DatabaseConfiguration config)
        {
            var destPath = DatabasePath(name, config.Directory);
			LiteCoreBridge.Check(err =>
			{
				var nativeConfig = DBConfig;
				if (config.EncryptionKey != null) {
					var key = config.EncryptionKey;
					int i = 0;
					nativeConfig.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
					foreach (var b in key.KeyData) {
						nativeConfig.encryptionKey.bytes[i++] = b;
					}
				}

				return Native.c4db_copy(path, destPath, &nativeConfig, err);
			});

		}

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
                var localConfig = DBConfig;
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

            return Directory.Exists(DatabasePath(name, directory));
        }

        /// <summary>
        /// Adds a listener for changes on a certain document (by ID).  Similar to <see cref="Database.Changed"/>
        /// but requires a parameter to add so it is a method instead of an event.
        /// </summary>
        /// <param name="documentID">The ID to add the listener for</param>
        /// <param name="handler">The logic to handle the event</param>
        public void AddDocumentChangedListener(string documentID, EventHandler<DocumentChangedEventArgs> handler)
        {
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();

                var count = _documentChanged.Add(documentID, handler);
                if (count == 0) {
                    var docObs = new DocumentObserver(_c4db, documentID, _DocObserverCallback, this);
                    _docObs[documentID] = docObs;
                }
            });
        }

        /// <summary>
        /// Closes the database
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Performs a manual compaction of this database, removing old irrelevant data
        /// and decreasing the size of the database file on disk
        /// </summary>
        public void Compact()
        {
            _threadSafety.DoLocked(() => LiteCoreBridge.Check(err =>
            {
                CheckOpen();
                return Native.c4db_compact(_c4db, err);
            }));
        }

        /// <summary>
        /// Checks whether or not a given <see cref="Document"/> exists 
        /// in this database instance by searching for its ID.
        /// </summary>
        /// <param name="docID">The ID of the <see cref="Document"/> to search for</param>
        /// <returns><c>true</c> if the <see cref="Document"/> exists, <c>false</c> otherwise</returns>
        public bool Contains(string docID)
        {
            return _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                using (var doc = GetDocument(docID, true)) {
                    return doc != null;
                }
            });
        }

        /// <summary>
        /// Creates an index of the given type on the given path with the given configuration
        /// </summary>
        /// <param name="name">The name to give to the index (must be unique, or previous
        /// index with the same name will be overwritten)</param>
        /// <param name="index">The index to creaate</param>
        public void CreateIndex(string name, IIndex index)
        {
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                var concreteIndex = Misc.TryCast<IIndex, QueryIndex>(index);
                var jsonObj = concreteIndex.ToJSON();
                var json = JsonConvert.SerializeObject(jsonObj);
                LiteCoreBridge.Check(err =>
                {
                    var internalOpts = concreteIndex.Options;
                    return Native.c4db_createIndex(c4db, name, json, concreteIndex.IndexType, &internalOpts, err);
                });
            });
        }

        /// <summary>
        /// Deletes the database
        /// </summary>
        public void Delete()
        {
            _threadSafety.DoLocked(() =>
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
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                VerifyDB(document).Delete();
            });
        }

        /// <summary>
        /// Deletes the index with the given name
        /// </summary>
        /// <param name="name">The name of the index to delete</param>
        public void DeleteIndex(string name)
        {
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                LiteCoreBridge.Check(err => Native.c4db_deleteIndex(c4db, name, err));
            });
        }

        /// <summary>
        /// Gets the <see cref="Document"/> with the specified ID
        /// </summary>
        /// <param name="id">The ID to use when creating or getting the document</param>
        /// <returns>The instantiated document, or <c>null</c> if it does not exist</returns>
        public Document GetDocument(string id) => _threadSafety.DoLocked(() => GetDocument(id, true));

        /// <summary>
        /// Gets a list of index names that are present in the database
        /// </summary>
        /// <returns>The list of created index names</returns>
        public IList<string> GetIndexes()
        {
            FLArray* array = null;
            var retVal = new List<string>();
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                C4SliceResult result = new C4SliceResult();
                LiteCoreBridge.Check(err =>
                {
                    result = NativeRaw.c4db_getIndexes(c4db, err);
                    return result.buf != null;
                });

                var val = NativeRaw.FLValue_FromTrustedData(new FLSlice(result.buf, result.size));
                if (val == null) {
                    throw new LiteCoreException(new C4Error(C4ErrorCode.CorruptIndexData));
                }

                array = Native.FLValue_AsArray(val);
                if (array == null) {
                    throw new LiteCoreException(new C4Error(C4ErrorCode.CorruptIndexData));
                }
            });

            var flArray = new FleeceArray(array, null, null);
            foreach (var obj in flArray) {
                retVal.Add(obj as string);
            }

            return retVal;
        }

        /// <summary>
        /// Runs the given batch of operations as an atomic unit
        /// </summary>
        /// <param name="a">The <see cref="Action"/> containing the operations. </param>
        public void InBatch(Action a)
        {
            _threadSafety.DoLocked(() =>
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
            _threadSafety.DoLocked(() =>
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
            _threadSafety.DoLocked(() =>
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
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                VerifyDB(document).Save();
            });
        }

        #endregion

        #region Internal Methods

        internal void BeginTransaction()
        {
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(_c4db, err));
        }

        internal void ChangeEncryptionKey(IEncryptionKey key)
        {
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                LiteCoreBridge.Check(err =>
                {
                    var newKey = new C4EncryptionKey {
                        algorithm = C4EncryptionAlgorithm.AES256
                    };

                    var i = 0;
                    foreach (var b in key.KeyData) {
                        newKey.bytes[i++] = b;
                    }

                    return Native.c4db_rekey(c4db, &newKey, err);
                });
            });
        }

        internal void EndTransaction(bool commit)
        {
            LiteCoreBridge.Check(err => Native.c4db_endTransaction(_c4db, commit, err));
        }

        internal void ResolveConflict(string docID, IConflictResolver resolver)
        {
            InBatch(() =>
            {
                using (var doc = new ReadOnlyDocument(this, docID, true))
                using (var otherDoc = new ReadOnlyDocument(this, docID, true)) {
                    otherDoc.SelectConflictingRevision();
                    using (var tmp = new ReadOnlyDocument(this, docID, true)) {
                        var baseDoc = tmp;
                        if (!baseDoc.SelectCommonAncestor(doc, otherDoc)) {
                            baseDoc = null;
                        }

                        ReadOnlyDocument resolved;
                        var logDocID = new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure);
                        if (otherDoc.IsDeleted) {
                            resolved = doc;
                        } else if (doc.IsDeleted) {
                            resolved = otherDoc;
                        } else {
                            var effectiveResolver = resolver ?? doc.EffectiveConflictResolver;
                            var conflict = new Conflict(doc, otherDoc, baseDoc);
                            Log.To.Database.I(Tag,
                                $"Resolving doc '{logDocID}' with {effectiveResolver.GetType().Name} (mine={doc.RevID}, theirs={otherDoc.RevID}, base={baseDoc?.RevID}");
                            resolved = effectiveResolver.Resolve(conflict);
                            if (resolved == null) {
                                throw new LiteCoreException(new C4Error(C4ErrorCode.Conflict));
                            }
                        }

                        // Figure out what revision to delete and what if anything to add:
                        string winningRevID, losingRevID;
                        byte[] mergedBody = null;
                        if (resolved == otherDoc) {
                            winningRevID = otherDoc.RevID;
                            losingRevID = doc.RevID;
                        } else {
                            winningRevID = doc.RevID;
                            losingRevID = otherDoc.RevID;
                            if (resolved != doc) {
                                resolved.Database = this;
                                mergedBody = resolved.Encode();
                            }
                        }

                        // Tell LiteCore to do the resolution
                        var rawDoc = doc.c4Doc;
                        LiteCoreBridge.Check(
                            err => Native.c4doc_resolveConflict(rawDoc, winningRevID, losingRevID,
                                       mergedBody, err) && Native.c4doc_save(rawDoc, 0, err));
                        Log.To.Database.I(Tag,
                            $"Conflict resolved as doc '{logDocID}' rev {rawDoc->revID.CreateString()}");
                    }
                }
            });
        }

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

            var directoryToUse = String.IsNullOrWhiteSpace(directory)
                ? Service.Provider.TryGetRequiredService<IDefaultDirectoryResolver>().DefaultDirectory()
                : directory;
            return System.IO.Path.Combine(directoryToUse, $"{name}.{DBExtension}");
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
            
            Directory.CreateDirectory(Config.Directory);
            var path = DatabasePath(Name, Config.Directory);
            var config = DBConfig;

            var encrypted = "";
            if(Config.EncryptionKey != null) {
                var key = Config.EncryptionKey;
                int i = 0;
                config.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
                foreach(var b in key.KeyData) {
                    config.encryptionKey.bytes[i++] = b;
                }

                encrypted = "encrypted ";
            }

            Log.To.Database.I(Tag, $"Opening {encrypted}database at {path}");
            var localConfig1 = config;
            _threadSafety.DoLocked(() =>
            {
                _c4db = (C4Database*) NativeHandler.Create()
                    .AllowError((int) C4ErrorCode.NotADatabaseFile, C4ErrorDomain.LiteCoreDomain).Execute(err =>
                    {
                        var localConfig2 = localConfig1;
                        return Native.c4db_open(path, &localConfig2, err);
                    });

                if (_c4db == null) {
                    throw new CouchbaseLiteException(StatusCode.Unauthorized);
                }

                _obs = Native.c4dbobs_create(_c4db, _DbObserverCallback, this);
            });
        }

        private void PostDatabaseChanged()
        {
            var allChanges = new List<DatabaseChangedEventArgs>();
			_threadSafety.DoLocked(() =>
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
				            allChanges.Add(args);
				            docIDs = new List<string>();
				        }
				    }

				    external = newExternal;
				    for (int i = 0; i < nChanges; i++) {
				        docIDs.Add(changes[i].docID.CreateString());
				    }
				} while (nChanges > 0);
			});

            foreach (var args in allChanges) {
                Changed?.Invoke(this, args);
            }
        }

        private void PostDocChanged(string documentID)
        {
            _threadSafety.DoLocked(() =>
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
            _threadSafety.DoLocked(() => Dispose(true));
        }

        #endregion
    }
}
