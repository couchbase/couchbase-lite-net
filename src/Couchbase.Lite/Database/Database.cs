//
//  Database.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Serialization;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using ObjCRuntime;

namespace Couchbase.Lite.DB
{
    internal sealed unsafe class Database : ThreadSafe, IDatabase
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

        private readonly SharedStringCache _sharedStrings;

        public event EventHandler<DatabaseChangedEventArgs> Changed;
        private IConflictResolver _conflictResolver;

        private LruCache<string, Document> _documents = new LruCache<string, Document>(100);
        private IJsonSerializer _jsonSerializer;
        private DatabaseObserver _obs;
        private long p_c4db;

        #endregion

        #region Properties

        public IConflictResolver ConflictResolver
        {
            get {
                AssertSafety();
                return _conflictResolver;
            }
            set {
                AssertSafety();
                _conflictResolver = value;
            }
        }

        public IDocument this[string id]
        {
            get {
                return GetDocument(id);
            }
        }

        public string Name { get; }

        public DatabaseOptions Options { get; }

        public string Path
        {
            get {
                CheckOpen();
                return Native.c4db_getPath(c4db);
            }
        }

        internal C4BlobStore* BlobStore
        {
            get {
                AssertSafety();
                CheckOpen();
                return (C4BlobStore*)LiteCoreBridge.Check(err => Native.c4db_getBlobStore(c4db, err));
            }
        }

        internal C4Database* c4db
        {
            get {
                AssertSafety();
                return _c4db;
            }
        }

        internal IJsonSerializer JsonSerializer
        {
            get { return _jsonSerializer ?? (_jsonSerializer = Serializer.CreateDefaultFor(this)); }
            set { 
                _jsonSerializer = value;
            }
        }

        internal SharedStringCache SharedStrings
        {
            get {
                AssertSafety();
                return _sharedStrings;
            }
        }

        private C4Database *_c4db
        {
            get {
                return (C4Database *)p_c4db;
            }
            set {
                p_c4db = (long)value;
            }
        }

        #endregion

        #region Constructors

        static Database()
        {
            _LogCallback = LiteCoreLog;
            Native.c4log_register(C4LogLevel.Warning, _LogCallback);
            _DbObserverCallback = DbObserverCallback;
        }

        public Database(string name) : this(name, DatabaseOptions.Default)
        {
            
        }

        public Database(string name, DatabaseOptions options) 
        {
            if(name == null) {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            Options = options;
            Open();
            _sharedStrings = new SharedStringCache(Native.c4db_getFLSharedKeys(_c4db));
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
                return Native.c4db_deleteAtPath(path, &localConfig, err);
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

        #endregion

        #region Private Methods

        private static string DatabasePath(string name, string directory)
        {
            return System.IO.Path.Combine(Directory(directory), name);
        }

        private static void DbObserverCallback(C4DatabaseObserver* db, object context)
        {
            var dbObj = (Database)context;
            dbObj.ActionQueue.DispatchAsync(() =>
            {
                dbObj.PostDatabaseChanged();
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
        private static void LiteCoreLog(C4LogDomain domain, C4LogLevel level, C4Slice msg)
        {
            switch(level) {
                case C4LogLevel.Error:
                    Log.To.Database.E("LiteCore", msg.CreateString());
                    break;
                case C4LogLevel.Warning:
                    Log.To.Database.W("LiteCore", msg.CreateString());
                    break;
                case C4LogLevel.Info:
                    Log.To.Database.V("LiteCore", msg.CreateString()); // Noisy, so intentionally V
                    break;
                case C4LogLevel.Verbose:
                    Log.To.Database.V("LiteCore", msg.CreateString());
                    break;
                case C4LogLevel.Debug:
                    Log.To.Database.D("LiteCore", msg.CreateString());
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
            if(disposing) {
                var docs = Interlocked.Exchange(ref _documents, null);
                docs?.Dispose();
                var obs = Interlocked.Exchange(ref _obs, null);
                obs?.Dispose();
            }

            var old = (C4Database *)Interlocked.Exchange(ref p_c4db, 0);
            if(old != null) {
                LiteCoreBridge.Check(err => Native.c4db_close(old, err));
                Native.c4db_free(old);
            }
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException", Justification = "GetItem() never returns null")]
        private ModeledDocument<T> GetDocument<T>(string docID, bool mustExist) where T : class, new()
        {
            CheckOpen();
            PerfTimer.StartEvent("GetDocument<T>_Native.c4doc_get");
            var doc = (C4Document*)RetryHandler.RetryIfBusy()
                .AllowError((int)LiteCoreError.NotFound, C4ErrorDomain.LiteCoreDomain)
                .Execute(err => Native.c4doc_get(_c4db, docID, mustExist, err));
            PerfTimer.StopEvent("GetDocument<T>_Native.c4doc_get");

            if(doc == null) {
                return null;
            }

            PerfTimer.StartEvent("GetDocument<T>_NativeRaw.FLValue_FromTrustedData");
            var value = NativeRaw.FLValue_FromTrustedData((FLSlice)doc->selectedRev.body);
            PerfTimer.StopEvent("GetDocument<T>_NativeRaw.FLValue_FromTrustedData");
            PerfTimer.StartEvent("GetDocument<T>_CreateModeledDocument");
            var poolObject = ObjectPool.GetObjectPool<ModeledDocument<T>>().GetItem(() => new ModeledDocument<T>(this, doc)) as ModeledDocument<T>;
            poolObject.Reconstruct(this, doc);
            if(value != null) {
                poolObject.ActionQueue.DispatchSync(() => JsonSerializer.Populate(poolObject.Item, value));
            }

            PerfTimer.StopEvent("GetDocument<T>_CreateModeledDocument");
            return poolObject;
        }

        private Document GetDocument(string docID, bool mustExist)
        {
            CheckOpen();
            if(_documents == null) {
                Log.To.Database.W(Tag, "GetDocument called after Close(), returning null...");
                return null;
            }

            var doc = _documents[docID];
            if(doc == null) {
                doc = new Document(this, docID, mustExist);
                _documents[docID] = doc;
            } else {
                if(mustExist && !doc.Exists) {
                    Log.To.Database.V(Tag, "Requested existing document {0}, but it doesn't exist", 
                        new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
                    return null;
                }
            }

            return doc;
        }

        private void Open()
        {
            if(_c4db != null) {
                return;
            }

            System.IO.Directory.CreateDirectory(Directory(Options.Directory));
            var path = DatabasePath(Name, Options.Directory);
            var config = _DBConfig;
            if(Options.ReadOnly) {
                config.flags |= C4DatabaseFlags.ReadOnly;
            }

            var encrypted = "";
            if(Options.EncryptionKey != null) {
                var key = SymmetricKey.Create(Options.EncryptionKey);
                int i = 0;
                config.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
                foreach(var b in key.KeyData) {
                    config.encryptionKey.bytes[i++] = b;
                }

                encrypted = "encrypted ";
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
            var external = false;
            uint changes;
            var c4DocIDs = new string[maxChanges];
            var docIDs = new List<string>();
            do {
                // Read changes in batches of MaxChanges:
                bool newExternal;
                ulong lastSequence;
                changes = Native.c4dbobs_getChanges(_obs.Observer, c4DocIDs, &lastSequence, &newExternal);
                if(changes == 0 || external != newExternal || docIDs.Count > 1000) {
                    if(docIDs.Count > 0) {
                        // Only notify if there are actually changes to send
                        var args = new DatabaseChangedEventArgs(docIDs.ToArray(), lastSequence, external);
                        CallbackQueue.DispatchAsync(() =>
                        {
                            Changed?.Invoke(this, args);
                        });
                        docIDs.Clear();
                    }
                }

                external = newExternal;
                foreach(var docID in c4DocIDs.Take((int)changes)) {
                    docIDs.Add(docID);
                    if(external) {
                        var existingDoc = _documents[docID];
                        existingDoc?.ActionQueue.DispatchAsync(() => existingDoc.ChangedExternally());
                    }
                }
            } while(changes > 0);
        }

        #endregion

        #region IDatabase

        public void Close()
        {
            Dispose();
        }

        public IDocument CreateDocument()
        {
            return GetDocument(Misc.CreateGuid(), false);
        }

        public IModeledDocument<T> CreateDocument<T>() where T : class, new()
        {
            return GetDocument<T>(Misc.CreateGuid(), false);
        }

        public void CreateIndex(string propertyPath)
        {
            AssertSafety();
            CheckOpen();
            CreateIndex(propertyPath, IndexType.ValueIndex, null);
        }

        public void CreateIndex(string propertyPath, IndexType indexType, IndexOptions options)
        {
            AssertSafety();
            CheckOpen();
            LiteCoreBridge.Check(err =>
            {
                if(options == null) {
                    return Native.c4db_createIndex(c4db, propertyPath, (C4IndexType)indexType, null, err);
                } else {
                    var localOpts = IndexOptions.Internal(options);
                    return Native.c4db_createIndex(c4db, propertyPath, (C4IndexType)indexType, &localOpts, err);
                }
            });
        }

        public void Delete()
        {
            AssertSafety();
            CheckOpen();
            var old = (C4Database *)Interlocked.Exchange(ref p_c4db, 0);
            if(old == null) {
                throw new InvalidOperationException("Attempt to perform an operation on a closed database");
            }

            LiteCoreBridge.Check(err => Native.c4db_delete(old, err));
            Native.c4db_free(old);
            _obs?.Dispose();
            _obs = null;
        }

        public void DeleteIndex(string propertyPath, IndexType type)
        {
            AssertSafety();
            CheckOpen();
            LiteCoreBridge.Check(err => Native.c4db_deleteIndex(c4db, propertyPath, (C4IndexType)type, err));
        }

        public bool DocumentExists(string documentID)
        {
            AssertSafety();
            CheckOpen();
            if(documentID == null) {
                throw new ArgumentNullException(nameof(documentID));
            }

            var check = (C4Document*)RetryHandler.RetryIfBusy().AllowError((int)LiteCoreError.NotFound, C4ErrorDomain.LiteCoreDomain)
                .Execute(err => Native.c4doc_get(c4db, documentID, true, err));
            var exists = check != null;
            Native.c4doc_free(check);
            return exists;
        }

        public IDocument GetDocument(string id)
        {
            AssertSafety();
            return GetDocument(id, false);
        }

        public IModeledDocument<T> GetDocument<T>(string id) where T : class, new()
        {
            AssertSafety();
            return GetDocument<T>(id, false);
        }

        public bool InBatch(Func<bool> a)
        {
            AssertSafety();
            CheckOpen();
            PerfTimer.StartEvent("InBatch_BeginTransaction");
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(_c4db, err));
            PerfTimer.StopEvent("InBatch_BeginTransaction");
            var success = true;
            try {
                success = a();
            } catch(Exception e) {
                Log.To.Database.W(Tag, "Exception during InBatch, rolling back...", e);
                success = false;
                throw;
            } finally {
                PerfTimer.StartEvent("InBatch_EndTransaction");
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(_c4db, success, err));
                PerfTimer.StopEvent("InBatch_EndTransaction");
            }

            PostDatabaseChanged();
            return success;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            ActionQueue.DispatchSync(() =>
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            });
        }

        #endregion
    }
}
