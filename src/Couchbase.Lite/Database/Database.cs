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
using System.Threading;

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

namespace Couchbase.Lite.Internal.DB
{
    internal sealed unsafe class Database : IDatabase
    {
        #region Constants

        private static readonly C4DatabaseConfig _DBConfig = new C4DatabaseConfig {
            flags = C4DatabaseFlags.Create | C4DatabaseFlags.AutoCompact | C4DatabaseFlags.Bundled | C4DatabaseFlags.SharedKeys,
            storageEngine = "SQLite",
            versioning = C4DocumentVersioning.RevisionTrees
        };

        private static readonly DatabaseObserverCallback _DbObserverCallback;

        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private static readonly C4LogCallback _LogCallback;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

        private const string Tag = nameof(Database);

        #endregion

        #region Variables

        private readonly ThreadSafety _threadSafety = new ThreadSafety();
        private readonly SharedStringCache _sharedStrings;
        private readonly HashSet<Document> _unsavedDocuments = new HashSet<Document>();

        public event EventHandler<DatabaseChangedEventArgs> Changed;
        public event EventHandler<DocumentChangedEventArgs> DocumentChanged;
        private IConflictResolver _conflictResolver;

        private IJsonSerializer _jsonSerializer;
        private DatabaseObserver _obs;
        private long p_c4db;

        #endregion

        #region Properties

        public DatabaseConfiguration Config { get; }

        public IConflictResolver ConflictResolver
        {
            get => _threadSafety.DoLocked(() => _conflictResolver);
            set => _threadSafety.DoLocked(() => _conflictResolver = value);
        }

        public IDocument this[string id] => GetDocument(id);

        public string Name { get; }

        public string Path
        {
            get {
                return _threadSafety.DoLocked(() =>
                {
                    CheckOpen();
                    return Native.c4db_getPath(c4db);
                });
            }
        }

        internal ICollection<IReplication> ActiveReplications { get; } = new HashSet<IReplication>();

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

        internal IDictionary<Uri, IReplication> Replications { get; } = new Dictionary<Uri, IReplication>();

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
            _LogCallback = LiteCoreLog;
            Native.c4log_register(C4LogLevel.Warning, _LogCallback);
            _DbObserverCallback = DbObserverCallback;
        }

        public Database(Database other) : this(other.Name, other.Config)
        {
            
        }

        public Database(string name) : this(name, new DatabaseConfiguration())
        {
            
        }

        public Database(string name, DatabaseConfiguration options) 
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Config = options;
            Open();
            _sharedStrings = new SharedStringCache(_c4db);
        }

        ~Database()
        {
            Dispose(false);
        }

        #endregion

        #region Public Methods

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

        public static bool Exists(string name, string directory)
        {
            if(name == null) {
                throw new ArgumentNullException(nameof(name));
            }

            return File.Exists(DatabasePath(name, directory));
        }

        public void ChangeEncryptionKey(object key)
        {
            throw new NotImplementedException();
        }

        public IDocument CreateDocument() => _threadSafety.DoLocked(() => GetDocument(Misc.CreateGuid(), false));

        public IReplication CreateReplication(Uri remoteUrl)
        {
            if (remoteUrl == null) {
                throw new ArgumentNullException(nameof(remoteUrl));
            }

            var repl = Replications.Get(remoteUrl);
            if (repl == null) {
                repl = new Replication(this, remoteUrl, null);
                Replications[remoteUrl] = repl;
            }

            return repl;
        }

        public IReplication CreateReplication(IDatabase otherDatabase)
        {
            if (otherDatabase == null) {
                throw new ArgumentNullException(nameof(otherDatabase));
            }

            if (otherDatabase == this) {
                throw new InvalidOperationException("Source and target database are the same");
            }

            var key = new Uri(otherDatabase.Path);
            var repl = Replications.Get(key);
            if (repl == null) {
                repl = new Replication(this, null, otherDatabase);
                Replications[key] = repl;
            }

            return repl;
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
            return System.IO.Path.Combine(Directory(directory), name);
        }

        private static void DbObserverCallback(C4DatabaseObserver* db, object context)
        {
            var dbObj = (Database)context;
            dbObj?.PostDatabaseChanged();
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
        private static void LiteCoreLog(C4LogDomain* domain, C4LogLevel level, C4Slice msg)
        {
            var name = Native.c4log_getDomainName(domain);
            switch(level) {
                case C4LogLevel.Error:
                    Log.To.DomainOrLiteCore(name).E(name, msg.CreateString());
                    break;
                case C4LogLevel.Warning:
                    Log.To.DomainOrLiteCore(name).W(name, msg.CreateString());
                    break;
                case C4LogLevel.Info:
                    Log.To.DomainOrLiteCore(name).I(name, msg.CreateString());
                    break;
                case C4LogLevel.Verbose:
                    Log.To.DomainOrLiteCore(name).V(name, msg.CreateString());
                    break;
                case C4LogLevel.Debug:
                    Log.To.DomainOrLiteCore(name).D(name, msg.CreateString());
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
            Debug.WriteLine("DISPOSE");
            if(disposing) {
                var obs = Interlocked.Exchange(ref _obs, null);
                obs?.Dispose();
                if(_unsavedDocuments.Count > 0) {
                    Log.To.Database.W(Tag,
                        $"Closing database with {_unsavedDocuments.Count} such as {_unsavedDocuments.Any()}");
                }
                _unsavedDocuments.Clear();
            }

            var old = (C4Database *)Interlocked.Exchange(ref p_c4db, 0);
            if(old != null) {
                Log.To.Database.I(Tag, $"Closing database at path {Native.c4db_getPath(old)}");
                LiteCoreBridge.Check(err => Native.c4db_close(old, err));
                Native.c4db_free(old);
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

            Debug.WriteLine("OPEN!");
            System.IO.Directory.CreateDirectory(Directory(Config.Directory));
            var path = DatabasePath(Name, Config.Directory);
            var config = _DBConfig;
            //if(Config.ReadOnly) {
            //    config.flags |= C4DatabaseFlags.ReadOnly;
            //}

            var encrypted = "";
            if(Config.EncryptionKey != null) {
#if true
                throw new NotImplementedException("Encryption is not yet supported");
#else
                var key = Options.EncryptionKey;
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
            if(_obs == null || _c4db == null || Native.c4db_isInTransaction(_c4db)) {
                return;
            }

            const uint maxChanges = 100u;
            uint nChanges;
            var changes = new C4DatabaseChange[maxChanges];
            do {
                // Read changes in batches of MaxChanges:
                bool newExternal;
                nChanges = Native.c4dbobs_getChanges(_obs.Observer, changes, maxChanges, &newExternal);
                for (int i = 0; i < nChanges; i++) {
                    var docID = changes[i].docID.CreateString();
                    using (var doc = new Document(this, docID, false)) {
                        var args = new DatabaseChangedEventArgs(this, doc);
                        Changed?.Invoke(this, args);
                    }
                }
                
            } while(nChanges > 0);
        }

        private Document VerifyDB(IDocument document)
        {
            var doc = document as Document ?? throw new InvalidOperationException("Custom IDocument not supported");
            if (doc.Database == null) {
                doc.Database = this;
            } else if (doc.Database != this) {
                throw new CouchbaseLiteException("Cannot delete a document from another database",
                    StatusCode.Forbidden);
            }

            return doc;
        }

        #endregion

        #region IDatabase

        public void Close()
        {
            Dispose();
        }

        public void CreateIndex(IList expressions, IndexType indexType, IndexOptions options)
        {
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                var jsonObj = QueryExpression.EncodeToJSON(expressions);
                var json = JsonConvert.SerializeObject(jsonObj);
                LiteCoreBridge.Check(err =>
                {
                    if (options == null) {
                        return Native.c4db_createIndex(c4db, json, (C4IndexType) indexType, null, err);
                    } else {
                        var localOpts = IndexOptions.Internal(options);
                        return Native.c4db_createIndex(c4db, json, (C4IndexType) indexType, &localOpts, err);
                    }
                });
            });
        }

        public void CreateIndex(IList<IExpression> expressions)
        {
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                CreateIndex(expressions as IList, IndexType.ValueIndex, null);
            });
        }

        public void Delete()
        {
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                var old = (C4Database*) Interlocked.Exchange(ref p_c4db, 0);
                if (old == null) {
                    throw new InvalidOperationException("Attempt to perform an operation on a closed database");
                }

                LiteCoreBridge.Check(err => Native.c4db_delete(old, err));
                Native.c4db_free(old);
                _obs?.Dispose();
                _obs = null;
            });
        }

        public void Delete(IDocument document)
        {
            _threadSafety.DoLocked(() => VerifyDB(document).Delete());
        }

        public void DeleteIndex(string propertyPath, IndexType type)
        {
            _threadSafety.DoLocked(() =>
            {
                CheckOpen();
                LiteCoreBridge.Check(err => Native.c4db_deleteIndex(c4db, propertyPath, (C4IndexType) type, err));
            });
        }

        public IDocument GetDocument(string id) => _threadSafety.DoLocked(() => GetDocument(id, false));

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

        public bool Purge(IDocument document)
        {
            return _threadSafety.DoLocked(() => VerifyDB(document).Purge());
        }

        public void Save(IDocument document)
        {
            _threadSafety.DoLocked(() => VerifyDB(document).Save());
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _threadSafety.DoLocked(() => Dispose(true));
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
