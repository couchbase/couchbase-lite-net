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
using System.IO;
using System.Threading;

using Couchbase.Lite.Crypto;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Serialization;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    using IndexType = C4IndexType;

    public sealed class IndexOptions
    {
        public string Language { get; set; }

        public bool IgnoreDiacriticals { get; set; }

        public IndexOptions()
        {

        }

        public IndexOptions(string language, bool ignoreDiacriticals)
        {
            Language = language;
            IgnoreDiacriticals = ignoreDiacriticals;
        }

        public static explicit operator C4IndexOptions(IndexOptions options)
        {
            return new C4IndexOptions {
                language = options.Language,
                ignoreDiacritics = options.IgnoreDiacriticals
            };
        }
    }

    public abstract class ComponentChangedEventArgs<T> : EventArgs
    {
        public T Source { get; set; }

        public T Value { get; set; }

        public T OldValue { get; set; }
    }

    public struct DatabaseOptions
    {
        public static readonly DatabaseOptions Default = new DatabaseOptions();

        public string Directory { get; set; }

        public object EncryptionKey { get; set; }

        public bool ReadOnly { get; set; }
    }

    public sealed unsafe class Database : ThreadLockedObject, IDisposable
    {
        private static readonly C4DatabaseConfig DBConfig = new C4DatabaseConfig {
            flags = C4DatabaseFlags.Create | C4DatabaseFlags.AutoCompact | C4DatabaseFlags.Bundled | C4DatabaseFlags.SharedKeys,
            storageEngine = "SQLite",
            versioning = C4DocumentVersioning.RevisionTrees
        };

        private const string Tag = nameof(Database);

        private readonly DatabaseOptions _options;
        private LruCache<string, Document> _documents = new LruCache<string, Document>(100);

        public event EventHandler<DocumentChangedEventArgs> DocumentChanged;

        public string Name { get; }

        private long p_c4db;
        internal C4Database* c4db
        {
            get {
                return (C4Database*)p_c4db;
            }
            private set {
                p_c4db = (long)value;
            }
        }

        internal SharedStringCache SharedStrings { get; }

        internal string Path
        {
            get {
                return Native.c4db_getPath(c4db);
            }
        }

        private IJsonSerializer _jsonSerializer;
        internal IJsonSerializer JsonSerializer
        {
            get {
                if(_jsonSerializer == null) {
                    _jsonSerializer = Serializer.CreateDefaultFor(this);
                }

                return _jsonSerializer;
            }
            set {
                _jsonSerializer = value;
            }
        }

        public IConflictResolver ConflictResolver { get; set; }

        static Database()
        {
            Native.c4log_register(C4LogLevel.Warning, (level, msg) =>
            {
                switch(level) {
                    case C4LogLevel.Error:
                        Log.To.Database.E("LiteCore", msg.CreateString());
                        break;
                    case C4LogLevel.Warning:
                        Log.To.Database.W("LiteCore", msg.CreateString());
                        break;
                    case C4LogLevel.Info:
                        Log.To.Database.I("LiteCore", msg.CreateString());
                        break;
                    case C4LogLevel.Verbose:
                        Log.To.Database.V("LiteCore", msg.CreateString());
                        break;
                    case C4LogLevel.Debug:
                        Log.To.Database.D("LiteCore", msg.CreateString());
                        break;
                }
            });
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
            _options = options;
            Open();
            SharedStrings = new SharedStringCache(Native.c4db_getFLSharedKeys(c4db));
        }

        internal Database(Database other) : this(other.Name, other._options)
        {

        }

        ~Database()
        {
            Dispose(false);
        }

        public static void Delete(string name, string directory)
        {
            if(name == null) {
                throw new ArgumentNullException(nameof(name));
            }

            var path = DatabasePath(name, directory);
            LiteCoreBridge.Check(err =>
            {
                var localConfig = DBConfig;
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

        public void Close()
        {
            Dispose();
        }

        public void ChangeEncryptionKey(object key)
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            var db = (C4Database*)Interlocked.Exchange(ref p_c4db, 0);
            if(db != null) {
                Close();
                LiteCoreBridge.Check(err => Native.c4db_delete(db, err));
            }
        }

        public bool InBatch(Func<bool> a)
        {
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(c4db, err));
            var success = true;
            try {
                success = a();
            } catch(Exception e) {
                Log.To.Database.W(Tag, "Exception during InBatch, rolling back...", e);
                success = false;
            } finally {
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(c4db, success, err));
            }

            return success;
        }

        public Document GetDocument()
        {
            return GetDocument(Misc.CreateGUID());
        }

        public Document GetDocument(string id)
        {
            return GetDocument(id, false);
        }

        public ModeledDocument<T> GetDocument<T>() where T : class, new()
        {
            return GetDocument<T>(Misc.CreateGUID());
        }

        public ModeledDocument<T> GetDocument<T>(string id) where T : class, new()
        {
            return GetDocument<T>(id, false);
        }

        public bool Exists(string documentID)
        {
            if(documentID == null) {
                throw new ArgumentNullException(nameof(documentID));
            }

            var check = (C4Document*)RetryHandler.RetryIfBusy().AllowError((int)LiteCoreError.NotFound, C4ErrorDomain.LiteCoreDomain)
                .Execute(err => Native.c4doc_get(c4db, documentID, true, err));
            var exists = check != null;
            Native.c4doc_free(check);
            return exists;
        }

        public Document this[string id]
        {
            get {
                return GetDocument(id);
            }
        }

        public void CreateIndex(string propertyPath)
        {
            CreateIndex(propertyPath, IndexType.ValueIndex, null);
        }

        public void CreateIndex(string propertyPath, IndexType indexType, IndexOptions options)
        {
            LiteCoreBridge.Check(err =>
            {
                if(options == null) {
                    return Native.c4db_createIndex(c4db, propertyPath, indexType, null, err);
                } else {
                    var localOpts = (C4IndexOptions)options;
                    return Native.c4db_createIndex(c4db, propertyPath, indexType, &localOpts, err);
                }
            });
        }

        public void DeleteIndex(string propertyPath, IndexType type)
        {
            LiteCoreBridge.Check(err => Native.c4db_deleteIndex(c4db, propertyPath, type, err));
        }

        public Query CreateQuery()
        {
            return new Query(this);
        }

        private static string DefaultDirectory()
        {
            return InjectableCollection.GetImplementation<IDefaultDirectoryResolver>().DefaultDirectory();
        }

        private static string Directory(string directory)
        {
            return directory ?? DefaultDirectory();
        }

        private static string DatabasePath(string name, string directory)
        {
            return System.IO.Path.Combine(Directory(directory), name);
        }

        private ModeledDocument<T> GetDocument<T>(string docID, bool mustExist) where T : class, new()
        {
            var doc = (C4Document*)RetryHandler.RetryIfBusy()
                .AllowError((int)LiteCoreError.NotFound, C4ErrorDomain.LiteCoreDomain)
                .Execute(err => Native.c4doc_get(c4db, docID, mustExist, err));

            if(doc == null) {
                return null;
            }

            FLValue *value = NativeRaw.FLValue_FromTrustedData((FLSlice)doc->selectedRev.body);
            var retVal = JsonSerializer.Deserialize<T>(value);
            if(retVal == null) {
                retVal = Activator.CreateInstance<T>();
            }
            return new ModeledDocument<T>(retVal, this, doc);
        }

        private Document GetDocument(string docID, bool mustExist)
        {
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

        private void Dispose(bool disposing)
        {
            if(disposing) {
                var docs = Interlocked.Exchange(ref _documents, null);
                docs?.Dispose();
            }

            var db = (C4Database*)Interlocked.Exchange(ref p_c4db, 0);
            if(db != null) {
                LiteCoreBridge.Check(err => Native.c4db_close(db, err));
                Native.c4db_free(db);
            }
        }

        private void Open()
        {
            if(c4db != null) {
                return;
            }

            System.IO.Directory.CreateDirectory(Directory(_options.Directory));
            var path = DatabasePath(Name, _options.Directory);
            var config = DBConfig;
            if(_options.ReadOnly) {
                config.flags |= C4DatabaseFlags.ReadOnly;
            }

            if(_options.EncryptionKey != null) {
                var key = SymmetricKey.Create(_options.EncryptionKey);
                int i = 0;
                config.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
                foreach(var b in key.KeyData) {
                    config.encryptionKey.bytes[i++] = b;
                }
            }

            var localConfig1 = config;
            c4db = (C4Database *)LiteCoreBridge.Check(err => {
                var localConfig2 = localConfig1;
                return Native.c4db_open(path, &localConfig2, err);
            });
        }

        internal override object Copy()
        {
            return new Database(this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
