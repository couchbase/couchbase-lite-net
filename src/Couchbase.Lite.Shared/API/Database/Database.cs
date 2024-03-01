// 
//  Database.cs
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Couchbase.Lite.Extensions;

namespace Couchbase.Lite
{

    /// <summary>
    /// Specifies the way that the library should behave when it encounters a situation
    /// when the database has been altered since the last read (e.g. a local operation read
    /// a document, modified it, and while it was being modified a replication committed a
    /// change to the document, and then the local document was saved after that)
    /// </summary>
    public enum ConcurrencyControl
    {
        /// <summary>
        /// Disregard the version that was received out of band and
        /// force this version to be current
        /// </summary>
        LastWriteWins,

        /// <summary>
        /// Throw an exception to indicate the situation so that the latest
        /// data can be read again from the local database
        /// </summary>
        FailOnConflict
    }

    /// <summary>
    /// Maintenance Type used when performing database maintenance .
    /// </summary>
    public enum MaintenanceType
    {
        /// <summary>
        /// Compact the database file and delete unused attachments.
        /// </summary>
        Compact,

        /// <summary>
        /// [VOLATILE] Rebuild the entire database's indexes.
        /// </summary>
        Reindex,

        /// <summary>
        /// [VOLATILE] Check for the database’s corruption. If found, an error will be returned.
        /// </summary>
        IntegrityCheck,

        /// <summary>
        /// Quickly update db statistics to help optimize queries
        /// </summary>
        Optimize,

        /// <summary>
        /// Full update of db statistics; takes longer than Optimize
        /// </summary>
        FullOptimize
    }

    /// <summary>
    /// A Couchbase Lite database.  This class is responsible for CRUD operations revolving around
    /// <see cref="Document"/> instances.  It is portable between platforms if the file is retrieved,
    /// and can be seeded with pre-populated data if desired.
    /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
    public sealed unsafe partial class Database : IChangeObservable<DatabaseChangedEventArgs>, IDocumentChangeObservable,
#pragma warning restore CS0618 // Type or member is obsolete
         IDisposable
    {
        #region Constants

        private static readonly C4DatabaseConfig2 DBConfig = new C4DatabaseConfig2 {
            flags = C4DatabaseFlags.Create | C4DatabaseFlags.AutoCompact,
        };

        private const string DBExtension = "cblite2";

        private const string Tag = nameof(Database);

        internal const string _defaultScopeName = "_default";
        internal const string _defaultCollectionName = "_default";

        #endregion

        #region Variables

#if false
        private IJsonSerializer _jsonSerializer;
#endif

        private C4Database* _c4db;

        private bool _isClosing;
        private ManualResetEventSlim _closeCondition = new ManualResetEventSlim(true);

        //Pre 3.1 Database's Collection
        private Collection? _defaultCollection;
        private Scope? _defaultScope;

        //3.1+ Database
        private ConcurrentDictionary<string, Scope> _scopes = new ConcurrentDictionary<string, Scope>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the configuration that was used to create the database.  The returned object
        /// is readonly; an <see cref="InvalidOperationException"/> will be thrown if the configuration
        /// object is modified.
        /// </summary>
        public DatabaseConfiguration Config { get; }

        /// <summary>
        /// [DEPRECATED] Gets the number of documents in the database
        /// </summary>
        [Obsolete("Count is deprecated, please use GetDefaultCollection().Count")]
        public ulong Count => GetDefaultCollection().Count;

        /// <summary>
        /// [DEPRECATED] Gets a <see cref="DocumentFragment"/> with the given document ID
        /// </summary>
        /// <param name="id">The ID of the <see cref="DocumentFragment"/> to retrieve</param>
        /// <returns>The <see cref="DocumentFragment"/> object</returns>
        [Obsolete("Document subscript in the Database class is deprecated, please use Document Script of the default collection.")]
        public DocumentFragment this[string id] => new DocumentFragment(GetDocument(id));

        /// <summary>
        /// Gets the object that stores the available logging methods
        /// for Couchbase Lite
        /// </summary>
        public static Log Log { get; } = new Log();

        /// <summary>
        /// Gets the database's name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the database's path.  If the database is closed or deleted, a <c>null</c>
        /// value will be returned.
        /// </summary>
        public string? Path => ThreadSafety.DoLocked(() => _c4db != null ? Native.c4db_getPath(c4db) : null);

        internal ConcurrentDictionary<IStoppable, int> ActiveStoppables { get; } = new ConcurrentDictionary<IStoppable, int>();

        internal FLSliceResult PublicUUID
        {
            get {
                var retVal = new FLSliceResult(null, 0UL);
                ThreadSafety.DoLocked(() =>
                {
                    CheckOpen();
                    var publicUUID = new C4UUID();
                    C4Error err;
                    var uuidSuccess = Native.c4db_getUUIDs(_c4db, &publicUUID, null, &err);
                    if (!uuidSuccess) {
                        throw CouchbaseException.Create(err);
                    }
                    
                    retVal = Native.FLSlice_Copy(new FLSlice(publicUUID.bytes, (ulong) C4UUID.Size));
                });

                return retVal;
            }
        }

        internal C4BlobStore* BlobStore
        {
            get {
                C4BlobStore* retVal = null;
                ThreadSafety.DoLocked(() =>
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
                C4Database* retVal = null;
                ThreadSafety.DoLocked(() => retVal = _c4db);
                return retVal;
            }
        }

        internal FLEncoder* SharedEncoder
        {
            get {
                FLEncoder* encoder = null;
                ThreadSafety.DoLocked(() =>
                {
                    CheckOpen();
                    encoder = Native.c4db_getSharedFleeceEncoder(_c4db);
                });

                return encoder;
            }
        }

        internal ThreadSafety ThreadSafety { get; } = new ThreadSafety();

        internal bool IsClosedLocked
        {
            get {
                return ThreadSafety.DoLocked(() =>
                {
                    return IsClosed;
                });
            }
        }

        private bool IsShell { get; } //this object is borrowing the C4Database from somewhere else, so don't free C4Database at the end if isshell

        // Must be called inside self lock
        private bool IsClosed
        {
            get {
                return _c4db == null;
            }
        }

        private bool IsReadyToClose
        {
            get {
                return ThreadSafety.DoLocked(() =>
                {
                    return ActiveStoppables.Count == 0;
                });
            }
        }

        #endregion

        #region Constructors

        static Database()
        {
            Native.c4log_enableFatalExceptionBacktrace();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.Contains("Couchbase"))) {
                Console.WriteLine($"Searching for extensions in {assembly.FullName}");
                var extensionType = assembly.GetTypes()
                    .FirstOrDefault(t => !t.IsInterface && t.GetInterface(typeof(ICouchbaseLiteExtension).FullName) != null);
                if(extensionType != null) {
                    try {
                        var impl = Activator.CreateInstance(extensionType) as ICouchbaseLiteExtension;
                        if (impl == null) {
                            Console.WriteLine("Unable to create found extension (Activator.CreateInstance returned null...)");
                            continue;
                        }

                        Console.WriteLine($"Extension {impl.Name} in {impl.Path} registered!");
                        Native.c4_setExtensionPath(impl.Path);
                        return; // For now, only accept the first one since that's all we can handle
                    } catch(Exception e) {
                        WriteLog.To.Database.E(Tag, $"Extension constructor failed", e);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a database with a given name and database configuration.  If the configuration
        /// is <c>null</c> then the default configuration will be used.  If the database does not yet
        /// exist, it will be created.
        /// </summary>
        /// <param name="name">The name of the database</param>
        /// <param name="configuration">The database configuration, or <c>null</c> for the default configuration</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is <c>null</c></exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="CouchbaseLiteError.CantOpenFile"/> if the
        /// directory indicated in <paramref name="configuration"/> could not be created</exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition was returned by LiteCore</exception>
        public Database(string name, DatabaseConfiguration? configuration = null)
        {
            Name = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(name), name);
            if(name == "") {
                var err = new C4Error(C4ErrorDomain.LiteCoreDomain, (int) CouchbaseLiteError.WrongFormat);
                throw new CouchbaseLiteException(err);
            }

            Config = configuration?.Freeze() ?? new DatabaseConfiguration(true);
            Run.Once(nameof(CheckFileLogger), CheckFileLogger);
            Open();
        }

        private void CheckFileLogger()
        {
            if (Log.File.Config == null) {
                WriteLog.To.Database.W("Logging", "Database.Log.File.Config is null, meaning file logging is disabled.  Log files required for product support are not being generated.");
            }
        }

        internal Database(Database other)
            : this(other.Name, other.Config)
        {

        }

        #if !COUCHBASE_ENTERPRISE
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        #endif
        // Used for predictive query callback
        internal Database(C4Database* c4db)
        {
            Name = "tmp";
            Config = new DatabaseConfiguration(true);
            _c4db = (C4Database*) Native.c4db_retain(c4db);
            IsShell = true;
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~Database()
        {
            try {
                Dispose(false);
            } catch (Exception e) {
                WriteLog.To.Database.E(Tag, "Error during finalizer, swallowing!", e);
            }
        }

        #endregion

        #region Public Methods - Scopes and Collections Management

        /// <summary>
        /// Get the default scope.  This is a cached object so there is no need to dispose it.
        /// </summary>
        /// <returns>default scope</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public Scope GetDefaultScope()
        {
            var retVal = ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                if (_defaultScope == null) {
                    _defaultScope = new Scope(this);
                    if (!_scopes.ContainsKey(_defaultScopeName))
                        _scopes.TryAdd(_defaultScopeName, _defaultScope);
                }

                return _defaultScope;
            });

            return retVal;
        }

        /// <summary>
        /// Get the default collection.  This is a cached object so there is no need to dispose it.  If you do,
        /// a new one will be created on the next call.
        /// </summary>
        /// <returns>default collection</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public Collection GetDefaultCollection()
        {
            return ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                if (_defaultCollection == null || !_defaultCollection.IsValid) {
                    var c4coll = (C4Collection*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_getDefaultCollection(c4db, err);
                    });

                    _defaultCollection = new Collection(this, _defaultCollectionName, GetDefaultScope(), c4coll);
                }

                return _defaultCollection;
            });
        }

        /// <summary>
        /// Get scope names that have at least one collection.
        /// The default scope is an exception as it will always be listed even it doesn't contain any collection.    
        /// </summary>
        /// <returns>
        /// scope names of all existing scopes, in the order in which they were created.
        /// </returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public IReadOnlyList<Scope> GetScopes()
        {
            GetScopesList();
            return _scopes.Values as IReadOnlyList<Scope>
                ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Invalid cast in GetScopes()");
        }

        /// <summary>
        /// Get a scope object by name. As the scope cannot exist by itself without having a collection, the null 
        /// value will be returned if there is no collection under the given scope’s name. 
        /// The default scope is an exception, it will always be returned.
        /// </summary>
        /// <param name="name">The name of the scope</param>
        /// <returns>scope object with the given scope name</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public Scope? GetScope(string? name = _defaultScopeName)
        {
            // TODO: Make name non-null in 4.0
            if (name == null) {
                name = _defaultScopeName;
            }

            Scope? scope = null;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                if (HasScopeFromLiteCore(name)) {
                    if (!_scopes.ContainsKey(name)) {
                        scope = new Scope(this, name);
                    } else {
                        scope = _scopes[name];
                    }
                }
            });

            return scope;
        }

        /// <summary>
        /// Get all collections of given Scope name.
        /// </summary>
        /// <param name="scope">The scope of the collections belong to</param>
        /// <returns>All collections with the given scope name</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public IReadOnlyList<Collection> GetCollections(string? scope = _defaultScopeName)
        {
            // TODO: Make scope non-null in 4.0
            if (scope == null) {
                scope = _defaultScopeName;
            }

            return ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                var s = scope == _defaultScopeName ? GetDefaultScope() : GetScope(scope);
                if (s != null) {
                    return s.GetCollections();
                }

                return new List<Collection>();
            });
        }

        /// <summary>
        /// Get a collection in the specified scope by name. 
        /// If the collection doesn't exist, null will be returned.
        /// Note that this will cache after the first retrieval
        /// and return the same instance until said instance is disposed.  Be careful if using multiple 
        /// instances because disposing one will invalidate them all.
        /// </summary>
        /// <param name="name">The name of the collection</param>
        /// <param name="scope">The scope of the collection</param>
        /// <returns>The collection with the given name and scope</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public Collection? GetCollection(string name, string? scope = _defaultScopeName)
        {
            // TODO: Make scope non-null in 4.0
            if (scope == null) {
                scope = _defaultScopeName;
            }

            return ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                Collection? coll = null;
                var s = scope == _defaultScopeName ? GetDefaultScope() : GetScope(scope);
                if (s != null) {
                    coll = s.GetCollection(name);
                }

                return coll?.IsValid == true ? coll : null;
            });
        }

        /// <summary>
        /// Create a named collection in the specified scope.
        /// If the collection already exists, the existing collection will be returned.
        /// </summary>
        /// <remarks>
        /// None default Collection and Scope Names are allowed to contain the following characters 
        /// A - Z, a - z, 0 - 9, and the symbols _, -, and % and to start with A-Z, a-z, 0-9, and -
        /// None default Collection and Scope Names start with _ and % are prohibited
        /// </remarks>
        /// <param name="name">The name of the new collection to be created</param>
        /// <param name="scope">The scope of the new collection to be created</param>
        /// <returns>New collection with the given name and scope</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public Collection CreateCollection(string name, string? scope = _defaultScopeName)
        {
            // TODO: Make scope non-null in 4.0
            if (scope == null) {
                scope = _defaultScopeName;
            }

            return ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                var s = scope == _defaultScopeName ? GetDefaultScope() : GetScope(scope);
                if (s == null) {
                    s = new Scope(this, scope);
                }

                var co = s.CreateCollection(name);
                if (!_scopes.ContainsKey(scope))
                    _scopes.TryAdd(scope, s);
                
                return co;
            });
        }

        /// <summary>
        /// Delete a collection by name  in the specified scope. If the collection doesn't exist, the operation
        /// will be no-ops. 
        /// </summary>
        /// <param name="name">The name of the collection to be deleted</param>
        /// <param name="scope">The scope of the collection to be deleted</param>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public void DeleteCollection(string name, string? scope = _defaultScopeName)
        {
            // TODO: Make scope non-null in 4.0
            if (scope == null) {
                scope = _defaultScopeName;
            }

            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                var s = scope == _defaultScopeName ? GetDefaultScope() : GetScope(scope);
                if (s != null) {
                    if (s.DeleteCollection(name, scope) && s.Count == 0 && s.Name != _defaultScopeName) {
                        if (_scopes.TryRemove(scope, out var sc)) {
                            sc?.Dispose();
                        }
                    }
                }
            });
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Copies a canned database from the given path to a new database with the given name and
        /// the configuration.  The new database will be created at the directory specified in the
        /// configuration.  Without given the database configuration, the default configuration that
        /// is equivalent to setting all properties in the configuration to <c>null</c> will be used.
        /// </summary>
        ///<remarks>
        /// Note: This method will copy the database without changing the encryption key of
        /// the original database:  the encryption key specified in the given config is the
        /// encryption key used for both the original and copied database.To change or add
        /// the encryption key for the copied database, call <see cref="Database.ChangeEncryptionKey(EncryptionKey)"/> for the copy.
        /// Furthermore, any <see cref="Database"/> object that is operating on the source database to be copied should
        /// be closed before this call to eliminate the possibility on some platforms of a "resource in use" error.
        ///</remarks>
        /// <param name="path">The source database path (i.e. path to the cblite2 folder)</param>
        /// <param name="name">The name of the new database to be created</param>
        /// <param name="config">The database configuration for the new database</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> or <paramref name="name"/>
        /// are <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public static void Copy(string path, string name, DatabaseConfiguration? config)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(path), path);
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(name), name);

            LiteCoreBridge.Check(err =>
            {
                var nativeConfig = DBConfig;

                #if COUCHBASE_ENTERPRISE
                if (config?.EncryptionKey != null) {
                    var key = config.EncryptionKey;
                    var i = 0;
                    nativeConfig.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
                    foreach (var b in key.KeyData) {
                        nativeConfig.encryptionKey.bytes[i++] = b;
                    }
                }
                #endif
                using (var parentDirectory = new C4String(config?.Directory)) {
                    nativeConfig.parentDirectory = parentDirectory.AsFLSlice();
                    return Native.c4db_copyNamed(path, name, &nativeConfig, err);
                }
            });

        }

        /// <summary>
        /// Deletes a database of the given name in the given directory.  If a <c>null</c> directory
        /// is passed then the default directory is searched.
        /// </summary>
        /// <param name="name">The database name</param>
        /// <param name="directory">The directory where the database is located, or <c>null</c> to check the default directory</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public static void Delete(string name, string? directory)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(name), name);

            var path = DatabasePath(directory);
            LiteCoreBridge.Check(err => Native.c4db_deleteNamed(name, path, err) || err->code == 0);
        }

        /// <summary>
        /// Checks whether a database of the given name exists in the given directory or not.  If a
        /// <c>null</c> directory is passed then the default directory is checked
        /// </summary>
        /// <param name="name">The database name</param>
        /// <param name="directory">The directory where the database is located</param>
        /// <returns><c>true</c> if the database exists in the directory, otherwise <c>false</c></returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public static bool Exists(string name, string? directory)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(name), name);

            return Directory.Exists(DatabasePath(name, directory));
        }

        /// <summary>
        /// Close database synchronously. Before closing the database, the active replicators, listeners and live queries will be stopped.
        /// </summary>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.Busy"/> if there are still active replicators
        /// or query listeners when the close call occurred</exception>
        public void Close() => Dispose();

        /// <summary>
        /// Performs database maintenance.
        /// </summary>
        /// <param name="type">Maintenance type</param>
        public void PerformMaintenance(MaintenanceType type)
        {
            ThreadSafety.DoLockedBridge(err =>
            {
                CheckOpen();
                return Native.c4db_maintenance(_c4db, (C4MaintenanceType) type, err);
            });
        }

        /// <summary>
        /// [DEPRECATED] Creates an index which could be a value index from <see cref="IndexBuilder.ValueIndex"/> or a full-text search index
        /// from <see cref="IndexBuilder.FullTextIndex"/> with the given name.
        /// The name can be used for deleting the index. Creating a new different index with an existing
        /// index name will replace the old index; creating the same index with the same name will be no-ops.
        /// </summary>
        /// <param name="name">The index name</param>
        /// <param name="index">The index</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> or <paramref name="index"/>
        /// is <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        /// <exception cref="NotSupportedException">Thrown if an implementation of <see cref="IIndex"/> other than one of the library
        /// provided ones is used</exception>
        [Obsolete("CreateIndex is deprecated, please use GetDefaultCollection().CreateIndex")]
        public void CreateIndex(string name, IIndex index)
        {
            GetDefaultCollection().CreateIndex(name, index);
        }

        /// <summary>
        /// [DEPRECATED] Creates a SQL++ query index which could be a value index from <see cref="ValueIndexConfiguration"/> or a full-text search index
        /// from <see cref="FullTextIndexConfiguration"/> with the given name.
        /// The name can be used for deleting the index. Creating a new different index with an existing
        /// index name will replace the old index; creating the same index with the same name will be no-ops.
        /// </summary>
        /// <param name="name">The index name</param>
        /// <param name="indexConfig">The index</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> or <paramref name="indexConfig"/>
        /// is <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        /// <exception cref="NotSupportedException">Thrown if an implementation of <see cref="IIndex"/> other than one of the library
        /// provided ones is used</exception>
        [Obsolete("CreateIndex is deprecated, please use GetDefaultCollection().CreateIndex")]
        public void CreateIndex(string name, IndexConfiguration indexConfig)
        {
            GetDefaultCollection().CreateIndex(name, indexConfig);
        }

        /// <summary>
        /// Creates a Query object from the given SQL++ string.
        /// </summary>
        /// <param name="queryExpression">SQL++ Expression</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queryExpression"/>
        /// is <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Throw if compiling <paramref name="queryExpression"/> returns an error</exception>
        public IQuery CreateQuery(string queryExpression)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(queryExpression), queryExpression);
            var query = new NQuery(queryExpression, this);
            return query;
        }

        /// <summary>
        /// Close and delete the database synchronously. Before closing the database, the active replicators, listeners and live queries will be stopped.
        /// </summary>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.Busy"/> if there are still active replicators
        /// or query listeners when the close call occurred</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public void Delete()
        {
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
            });

            Close();
            Delete(Name, Config.Directory);
        }

        /// <summary>
        /// [DEPRECATED] Deletes a document from the database.  When write operations are executed
        /// concurrently, the last writer will overwrite all other written values.
        /// Calling this method is the same as calling <see cref="Delete(Document, ConcurrencyControl)"/>
        /// with <see cref="ConcurrencyControl.LastWriteWins"/>
        /// </summary>
        /// <param name="document">The document</param>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.InvalidParameter"/>
        /// when trying to save a document into a database other than the one it was previously added to</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotFound"/>
        /// when trying to delete a document that hasn't been saved into a <see cref="Database"/> yet</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [Obsolete("Delete is deprecated, please use GetDefaultCollection().Delete")]
        public void Delete(Document document) => Delete(document, ConcurrencyControl.LastWriteWins);

        /// <summary>
        /// [DEPRECATED] Deletes the given <see cref="Document"/> from this database
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="concurrencyControl">The rule to use when encountering a conflict in the database</param>
        /// <returns><c>true</c> if the delete succeeded, <c>false</c> if there was a conflict</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.InvalidParameter"/>
        /// when trying to save a document into a database other than the one it was previously added to</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotFound"/>
        /// when trying to delete a document that hasn't been saved into a <see cref="Database"/> yet</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [Obsolete("Delete is deprecated, please use GetDefaultCollection().Delete")]
        public bool Delete(Document document, ConcurrencyControl concurrencyControl)
        {
            return GetDefaultCollection().Delete(document, concurrencyControl);
        }

        /// <summary>
        /// [DEPRECATED] Deletes the index with the given name
        /// </summary>
        /// <param name="name">The name of the index to delete</param>
        [Obsolete("DeleteIndex is deprecated, please use GetDefaultCollection().DeleteIndex")]
        public void DeleteIndex(string name)
        {
            GetDefaultCollection().DeleteIndex(name);
        }

        /// <summary>
        /// [DEPRECATED] Gets the <see cref="Document"/> with the specified ID
        /// </summary>
        /// <param name="id">The ID to use when creating or getting the document</param>
        /// <returns>The instantiated document, or <c>null</c> if it does not exist</returns>
        [Obsolete("GetDocument is deprecated, please use GetDefaultCollection().GetDocument")]
        public Document? GetDocument(string id)
        {
            return GetDefaultCollection().GetDocument(id);
        }

        /// <summary>
        /// [DEPRECATED] Gets a list of index names that are present in the database
        /// </summary>
        /// <returns>The list of created index names</returns>
        [Obsolete("GetIndexes is deprecated, please use GetDefaultCollection().GetIndexes")]
        public IList<string> GetIndexes()
        {
            return GetDefaultCollection().GetIndexes();
        }

        /// <summary>
        /// Runs the given batch of operations as an atomic unit
        /// </summary>
        /// <param name="action">The <see cref="Action"/> containing the operations. </param>
        public void InBatch(Action action)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(action), action);

            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                PerfTimer.StartEvent("InBatch_BeginTransaction");
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(_c4db, err));
                PerfTimer.StopEvent("InBatch_BeginTransaction");
                var success = true;
                try {
                    action();
                } catch (Exception e) {
                    WriteLog.To.Database.W(Tag, "Exception during InBatch, rolling back...", e);
                    success = false;
                    throw;
                } finally {
                    PerfTimer.StartEvent("InBatch_EndTransaction");
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(_c4db, success, err));
                    PerfTimer.StopEvent("InBatch_EndTransaction");
                }
            });
        }

        /// <summary>
        /// [DEPRECATED] Purges the given <see cref="Document"/> from the database.  This leaves
        /// no trace behind and will not be replicated
        /// </summary>
        /// <param name="document">The document to purge</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to purge a document from a database
        /// other than the one it was previously added to</exception>
        [Obsolete("Purge is deprecated, please use GetDefaultCollection().Purge")]
        public void Purge(Document document)
        {
            GetDefaultCollection().Purge(document);
        }

        /// <summary>
        /// [DEPRECATED] Purges the given document id of the <see cref="Document"/> 
        /// from the database.  This leaves no trace behind and will 
        /// not be replicated
        /// </summary>
        /// <param name="docId">The id of the document to purge</param>
        /// <exception cref="C4ErrorCode.NotFound">Throws NOT FOUND error if the document 
        /// of the docId doesn't exist.</exception>
        [Obsolete("Purge is deprecated, please use GetDefaultCollection().Purge")]
        public void Purge(string docId)
        {
            GetDefaultCollection().Purge(docId);
        }

        /// <summary>
        /// [DEPRECATED] Sets an expiration date on a document. After this time, the document
        /// will be purged from the database.
        /// </summary>
        /// <param name="docId"> The ID of the <see cref="Document"/> </param> 
        /// <param name="expiration"> Nullable expiration timestamp as a 
        /// <see cref="DateTimeOffset"/>, set timestamp to <c>null</c> 
        /// to remove expiration date time from doc.</param>
        /// <returns>Whether successfully sets an expiration date on the document</returns>
        /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
        /// doesn't exist</exception>
        [Obsolete("SetDocumentExpiration is deprecated, please use GetDefaultCollection().SetDocumentExpiration")]
        public bool SetDocumentExpiration(string docId, DateTimeOffset? expiration)
        {
            return GetDefaultCollection().SetDocumentExpiration(docId, expiration);
        }

        /// <summary>
        /// [DEPRECATED] Returns the expiration time of the document. <c>null</c> will be returned
        /// if there is no expiration time set
        /// </summary>
        /// <param name="docId"> The ID of the <see cref="Document"/> </param>
        /// <returns>Nullable expiration timestamp as a <see cref="DateTimeOffset"/> 
        /// of the document or <c>null</c> if time not set. </returns>
        /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
        /// doesn't exist</exception>
        [Obsolete("GetDocumentExpiration is deprecated, please use GetDefaultCollection().GetDocumentExpiration")]
        public DateTimeOffset? GetDocumentExpiration(string docId)
        {
            return GetDefaultCollection().GetDocumentExpiration(docId);
        }

        /// <summary>
        /// [DEPRECATED] Saves the given <see cref="MutableDocument"/> into this database.  This call is equivalent to calling
        /// <see cref="Save(MutableDocument, ConcurrencyControl)" /> with a second argument of
        /// <see cref="ConcurrencyControl.LastWriteWins"/>
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        [Obsolete("Save is deprecated, please use GetDefaultCollection().Save")]
        public void Save(MutableDocument document) => Save(document, ConcurrencyControl.LastWriteWins);

        /// <summary>
        /// [DEPRECATED] Saves the given <see cref="MutableDocument"/> into this database
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="concurrencyControl">The rule to use when encountering a conflict in the database</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
        [Obsolete("Save is deprecated, please use GetDefaultCollection().Save")]
        public bool Save(MutableDocument document, ConcurrencyControl concurrencyControl)
        {
            return GetDefaultCollection().Save(document, concurrencyControl);
        }

        /// <summary>
        /// [DEPRECATED] Saves a document to the database. When write operations are executed concurrently, 
        /// and if conflicts occur, conflict handler will be called. Use the handler to directly
        /// edit the document.Returning true, will save the document. Returning false, will cancel
        /// the save operation.
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="conflictHandler">The conflict handler block which can be used to resolve it.</param> 
        /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
        [Obsolete("Save is deprecated, please use GetDefaultCollection().Save")]
        public bool Save(MutableDocument document, Func<MutableDocument, Document?, bool> conflictHandler)
        {
            return GetDefaultCollection().Save(document, conflictHandler);
        }

        /// <summary>
        /// Save a blob object directly into the database without associating it with any documents.
        /// </summary>
        /// <remarks>The blobs that are not associated with any documents will be removed from the database when compacting the database.</remarks>
        /// <exception cref="CouchbaseLiteException">Thrown if an error occurs during the blob save operation.</exception>
        /// <param name="blob">The blob object will be saved into Database.</param>
        public void SaveBlob(Blob blob)
        {
            blob.Install(this);
        }

        /// <summary>
        /// Gets the <see cref="Blob"/> of a given blob dictionary.
        /// </summary>
        /// <remarks>The blobs that are not associated with any documents are/will be removed from the database after compacting the database.</remarks>
        /// <param name="blobDict"> 
        /// JSON Dictionary represents in the <see cref="Blob"/> and the value will be validated in <see cref="Blob.IsBlob(IDictionary{string, object})"/>
        /// </param>
        /// <exception cref="ArgumentException">Throw if the given blob dictionary is not valid.</exception>
        /// <returns>The contained value, or <c>null</c> if it's digest information doesn’t exist.</returns>
        public Blob? GetBlob(Dictionary<string, object?> blobDict)
        {
            if (!blobDict.ContainsKey(Blob.DigestKey) || blobDict[Blob.DigestKey] == null)
                return null;

            if (!Blob.IsBlob(blobDict)) {
                throw new ArgumentException(CouchbaseLiteErrorMessage.InvalidJSONDictionaryForBlob);
            }

            C4BlobKey expectedKey = new C4BlobKey();
            var keyFromStr = Native.c4blob_keyFromString((string?)blobDict[Blob.DigestKey], &expectedKey);
            if (!keyFromStr) {
                return null;
            }

            var size = Native.c4blob_getSize(BlobStore, expectedKey);
            if (size == -1) {
                return null;
            }

            return new Blob(this, blobDict);
        }

#if CBL_LINQ
        public void Save(Couchbase.Lite.Linq.IDocumentModel model)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(model), model);

            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                MutableDocument md = (model.Document as MutableDocument) ?? model.Document?.ToMutable() ?? new MutableDocument();
                md.SetFromModel(model);

                try {
                    var retVal = Save(md, false);
                    model.Document = retVal;
                } finally {
                    md.Dispose();
                }
            });
        }
#endif

        #endregion

        #region IChangeObservable

        /// <summary>
        /// [DEPRECATED] Adds a change listener for the changes that occur in the default collection
        /// of this database. Signatures are the same as += style event handlers, but the callbacks 
        /// will be called using the specified <see cref="TaskScheduler"/>.  If the scheduler is null, 
        /// the default task scheduler will be used (scheduled via thread pool).
        /// </summary>
        /// <param name="scheduler">The scheduler to use when firing the change handler</param>
        /// <param name="handler">The handler to invoke</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [Obsolete("AddChangeListener is deprecated, please use GetDefaultCollection().AddChangeListener")]
        public ListenerToken AddChangeListener(TaskScheduler? scheduler,
            EventHandler<DatabaseChangedEventArgs> handler)
        {
            EventHandler<CollectionChangedEventArgs> collectionChangeEventHandler =
                new EventHandler<CollectionChangedEventArgs>((sender, args) =>
                {
                    handler(sender, args);
                });
            return GetDefaultCollection().AddChangeListener(scheduler, collectionChangeEventHandler);
        }

        /// <summary>
        /// [DEPRECATED] Adds a change listener for the changes that occur in the default collection
        /// of this database. Signatures are the same as += style event handlers. The callback will 
        /// be invoked on a thread pool thread.
        /// </summary>
        /// <param name="handler">The handler to invoke</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [Obsolete("AddChangeListener is deprecated, please use GetDefaultCollection().AddChangeListener")]
        public ListenerToken AddChangeListener(EventHandler<DatabaseChangedEventArgs> handler) => AddChangeListener(null, handler);

        #endregion

        #region IDocumentChangeObservable

        /// <summary>
        /// [DEPRECATED] Adds a document change listener for the document with the given ID and the <see cref="TaskScheduler"/>
        /// that will be used to invoke the callback.  If the scheduler is not specified, then the default scheduler
        /// will be used (scheduled via thread pool)
        /// </summary>
        /// <param name="id">The document ID</param>
        /// <param name="scheduler">The scheduler to use when firing the event handler</param>
        /// <param name="handler">The logic to handle the event</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the listener later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> or <paramref name="id"/>
        /// is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [Obsolete("AddDocumentChangeListener is deprecated, please use GetDefaultCollection().AddDocumentChangeListener")]
        public ListenerToken AddDocumentChangeListener(string id, TaskScheduler? scheduler,
            EventHandler<DocumentChangedEventArgs> handler)
        {
            return GetDefaultCollection().AddDocumentChangeListener(id, scheduler, handler);
        }

        /// <summary>
        /// [DEPRECATED] Adds a document change listener for the document with the given ID.  The callback will be
        /// invoked on a thread pool thread.
        /// </summary>
        /// <param name="id">The document ID</param>
        /// <param name="handler">The logic to handle the event</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the listener later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> or <paramref name="id"/>
        /// is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [Obsolete("AddDocumentChangeListener is deprecated, please use GetDefaultCollection().AddDocumentChangeListener")]
        public ListenerToken AddDocumentChangeListener(string id, EventHandler<DocumentChangedEventArgs> handler) => AddDocumentChangeListener(id, null, handler);

        #endregion

        #region IChangeObservableRemovable

        /// <summary>
        /// [DEPRECATED] Removes a database changed listener by token
        /// </summary>
        /// <param name="token">The token received from <see cref="AddChangeListener(TaskScheduler, EventHandler{DatabaseChangedEventArgs})"/>
        /// and family</param>
        [Obsolete("RemoveChangeListener is deprecated, please use GetDefaultCollection().RemoveChangeListener")]
        public void RemoveChangeListener(ListenerToken token)
        {
            GetDefaultCollection().RemoveChangeListener(token);
        }

        #endregion

        #region Internal Methods

        internal void AddActiveStoppable(IStoppable stoppable)
        {
            ThreadSafety.DoLocked(() =>
            {
                CheckOpenAndNotClosing();
                if(ActiveStoppables.TryAdd(stoppable, 0)) {
                    _closeCondition.Reset();
                }
            });
        }

        internal void RemoveActiveStoppable(IStoppable stoppable)
        {
            ThreadSafety.DoLocked(() =>
            {
                if (IsClosed) {
                    return;
                }

                if(!ActiveStoppables.TryRemove(stoppable, out var dummy)) {
                    return;
                }

                if (ActiveStoppables.Count == 0) {
                    _closeCondition.Set();
                }
            });
        }

        internal string? GetCookies(Uri? uri)
        {
            string? cookies = null;
            ThreadSafety.DoLocked(() =>
            {
                if (uri == null) {
                    WriteLog.To.Sync.V(Tag, "The Uri used to get cookies is null.");
                } else {
                    var addr = new C4Address();
                    var scheme = new C4String();
                    var host = new C4String();
                    var path = new C4String();
                    var pathStr = String.Concat(uri.Segments.Take(uri.Segments.Length - 1));
                    scheme = new C4String(uri.Scheme);
                    host = new C4String(uri.Host);
                    path = new C4String(pathStr);
                    addr.scheme = scheme.AsFLSlice();
                    addr.hostname = host.AsFLSlice();
                    addr.port = (ushort) uri.Port;
                    addr.path = path.AsFLSlice();

                    C4Error err = new C4Error();
                    cookies = Native.c4db_getCookies(_c4db, addr, &err);
                    if (err.code > 0) {
                        WriteLog.To.Sync.W(Tag, $"{err.domain}/{err.code} Failed getting Cookie from address {addr}.");
                    }

                    if (String.IsNullOrEmpty(cookies) && err.code == 0) {
                        WriteLog.To.Sync.V(Tag, "There is no saved HTTP cookies.");
                    }
                }
            });

            return cookies;
        }

        internal bool SaveCookie(string cookie, Uri uri, bool acceptParentDomain)
        {
            bool cookieSaved = false;
            ThreadSafety.DoLocked(() =>
            {
                if (uri == null) {
                    WriteLog.To.Sync.V(Tag, "The Uri used to set cookie is null.");
                } else {
                    var pathStr = String.Concat(uri.Segments.Take(uri.Segments.Length - 1));
                    C4Error err = new C4Error();
                    cookieSaved = Native.c4db_setCookie(_c4db, cookie, uri.Host, pathStr, acceptParentDomain, &err);
                    if(err.code > 0) {
                        WriteLog.To.Sync.W(Tag, $"{err.domain}/{err.code} Failed saving Cookie {cookie}.");
                    }
                }
            });

            return cookieSaved;
        }

        internal void ResolveConflict(string docID, IConflictResolver? conflictResolver, Collection collection)
        {
            Debug.Assert(docID != null);

            var writeSuccess = false;
            while (!writeSuccess) {
                var readSuccess = false;
                Document? localDoc = null, remoteDoc = null, resolvedDoc = null;
                try {
                    InBatch(() =>
                    {
                        // Do this in a batch so that there are no changes to the document between
                        // localDoc read and remoteDoc read
                        localDoc = new Document(collection ?? GetDefaultCollection(), docID);
                        if (!localDoc.Exists) {
                            throw new CouchbaseLiteException(C4ErrorCode.NotFound);
                        }

                        remoteDoc = new Document(collection ?? GetDefaultCollection(), docID, C4DocContentLevel.DocGetAll);
                        if (!remoteDoc.Exists || !remoteDoc.SelectConflictingRevision()) {
                            WriteLog.To.Sync.W(Tag, "Unable to select conflicting revision for '{0}', the conflict may have been previously resolved...",
                                new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
                            return;
                        }

                        readSuccess = true;
                    });

                    if (!readSuccess) {
                        return;
                    }

                    // Local and remote doc are non null, but the compiler doesn't realize (inside InBatch)
                    if (localDoc!.IsDeleted && remoteDoc!.IsDeleted) {
                        resolvedDoc = localDoc; // No need go through resolver, because both remote and local docs are deleted.
                    } else {
                        // Resolve conflict:
                        WriteLog.To.Database.I(Tag, "Resolving doc '{0}' (mine={1} and theirs={2})",
                                new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure), localDoc.RevisionID,
                                remoteDoc!.RevisionID);

                        conflictResolver = conflictResolver ?? ConflictResolver.Default;
                        var conflict = new Conflict(docID, localDoc.IsDeleted ? null : localDoc, remoteDoc.IsDeleted ? null : remoteDoc);

                        resolvedDoc = conflictResolver.Resolve(conflict);
                    }

                    if (resolvedDoc != null) {
                        if (resolvedDoc.Id != docID) {
                            WriteLog.To.Sync.W(Tag, $"Resolved docID {resolvedDoc.Id} does not match docID {docID}",
                                new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
                            Misc.SafeSwap(ref resolvedDoc, new MutableDocument(docID, resolvedDoc.ToDictionary()));
                        }

                        // Compiler doesn't realize that this is swapped to another non-null just above
                        if (resolvedDoc!.Collection == null) {
                            resolvedDoc.Collection = collection ?? GetDefaultCollection();
                        } else if (resolvedDoc.Database != this) {
                            throw new InvalidOperationException(String.Format(CouchbaseLiteErrorMessage.ResolvedDocWrongDb,
                                resolvedDoc.Database?.Name, this.Name));
                        }
                    }

                    InBatch(() =>
                    {
                        // ReSharper disable once AccessToDisposedClosure
                        writeSuccess = SaveResolvedDocument(resolvedDoc, localDoc, remoteDoc);
                    });
                } finally {
                    resolvedDoc?.Dispose();
                    localDoc?.Dispose();
                    remoteDoc?.Dispose();
                }
            }
        }

        internal void CheckOpenLocked()
        {
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
            });
        }

        internal bool HasScopeFromLiteCore(string scope)
        {
            return ThreadSafety.DoLocked(() =>
            {
                //Returns true if the named scope exists.  Note that _default will always return true.
                return Native.c4db_hasScope(_c4db, scope);
            });
        }

        #endregion

        #region Private Methods

        private static string DatabasePath(string? directory)
        {
            var directoryToUse = String.IsNullOrWhiteSpace(directory)
                ? Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory()
                : directory;

            if (String.IsNullOrWhiteSpace(directoryToUse)) {
                throw new RuntimeException(
                    CouchbaseLiteErrorMessage.ResolveDefaultDirectoryFailed);
            }

            return directoryToUse;
        }

        private static string DatabasePath(string name, string? directory)
        {
            var directoryToUse = DatabasePath(directory);

            if (String.IsNullOrWhiteSpace(name)) {
                return directoryToUse;
            }

            return System.IO.Path.Combine(directoryToUse, $"{name}.{DBExtension}") ??
                throw new RuntimeException("Path.Combine failed to return a non-null value!");
        }

        // Must be called inside self lock
        private void CheckOpen()
        {
            if (IsClosed) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.DBClosed);
            }
        }

        private void Dispose(bool disposing)
        {
            if (IsClosed) {
                return;
            }

            WriteLog.To.Database.I(Tag, $"Closing database at path {Native.c4db_getPath(_c4db)}");
            if (!IsShell) {
                LiteCoreBridge.Check(err => Native.c4db_close(_c4db, err));
            }

            ClearScopesCollections();

            ReleaseC4Db();

            _closeCondition.Dispose();
        }

        private void Open()
        {
            if (_c4db != null) {
                return;
            }

            try {
                Directory.CreateDirectory(Config.Directory);
            } catch (Exception e) {
                throw new CouchbaseLiteException(C4ErrorCode.CantOpenFile, 
                    CouchbaseLiteErrorMessage.CreateDBDirectoryFailed, e);
            }

            var config = DBConfig;
            var encrypted = "";

            #if COUCHBASE_ENTERPRISE
            if (Config.EncryptionKey != null) {
                var key = Config.EncryptionKey;
                var i = 0;
                config.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
                foreach (var b in key.KeyData) {
                    config.encryptionKey.bytes[i++] = b;
                }

                encrypted = "encrypted ";
            }
            #endif

            WriteLog.To.Database.I(Tag, $"Opening {encrypted} database at { DatabasePath(Name, Config.Directory)}");
            var localConfig1 = config;
            ThreadSafety.DoLocked(() =>
            {
                _c4db = (C4Database*) LiteCoreBridge.Check(err =>
                {
                    var localConfig2 = localConfig1;
                    using (var parentDirectory = new C4String(Config.Directory)) {
                        localConfig2.parentDirectory = parentDirectory.AsFLSlice();
                        return Native.c4db_openNamed(Name, &localConfig2, err);
                    }
                });
            });
        }

        // Must be called in transaction
        private bool SaveResolvedDocument(Document? resolvedDoc, Document localDoc, Document remoteDoc)
        {
            if (resolvedDoc == null) {
                if (localDoc.IsDeleted)
                    resolvedDoc = localDoc;

                if (remoteDoc.IsDeleted)
                    resolvedDoc = remoteDoc;
            }

            if (resolvedDoc != null && !ReferenceEquals(resolvedDoc, localDoc)) {
                resolvedDoc.Collection = GetDefaultCollection();
            }

            // The remote branch has to win, so that the doc revision history matches the server's.
            var winningRevID = remoteDoc.RevisionID;
            var losingRevID = localDoc.RevisionID;

            // mergedBody:
            FLSliceResult mergedBody = (FLSliceResult) FLSlice.Null;
            if (!ReferenceEquals(resolvedDoc, remoteDoc)) {
                if (resolvedDoc != null) {
                    // Unless the remote revision is being used as-is, we need a new revision:
                    mergedBody = resolvedDoc.Encode();
                    if (mergedBody.Equals((FLSliceResult) FLSlice.Null))
                        throw new RuntimeException(CouchbaseLiteErrorMessage.ResolvedDocContainsNull);
                } else {
                    mergedBody = EmptyFLSliceResult();
                }
            }

            // mergedFlags:
            C4RevisionFlags mergedFlags = resolvedDoc?.c4Doc != null ? resolvedDoc.c4Doc.RawDoc->selectedRev.flags : 0;
            if (resolvedDoc == null || resolvedDoc.IsDeleted)
                mergedFlags |= C4RevisionFlags.Deleted;

            FLDoc* fleeceDoc = Native.FLDoc_FromResultData(mergedBody, FLTrust.Trusted,
                Native.c4db_getFLSharedKeys(_c4db), FLSlice.Null);
            if (Native.c4doc_dictContainsBlobs((FLDict*)Native.FLDoc_GetRoot(fleeceDoc))) {
                mergedFlags |= C4RevisionFlags.HasAttachments;
            }

            Native.FLDoc_Release(fleeceDoc);

            // Tell LiteCore to do the resolution:
            C4Document* rawDoc = localDoc.c4Doc != null ? localDoc.c4Doc.RawDoc : null;
            using (var winningRevID_ = new C4String(winningRevID))
            using (var losingRevID_ = new C4String(losingRevID)) {
                C4Error err;
                var retVal = NativeRaw.c4doc_resolveConflict(rawDoc, winningRevID_.AsFLSlice(),
                    losingRevID_.AsFLSlice(), (FLSlice) mergedBody, mergedFlags, &err)
                    && Native.c4doc_save(rawDoc, 0, &err);
                Native.FLSliceResult_Release(mergedBody);

                if (!retVal) {
                    if (err.code == (int) C4ErrorCode.Conflict) {
                        return false;
                    } else {
                        throw new CouchbaseLiteException((C4ErrorCode) err.code,
                            CouchbaseLiteErrorMessage.ResolvedDocFailedLiteCore);
                    }
                }
            }

            WriteLog.To.Database.I(Tag, "Conflict resolved as doc '{0}' rev {1}",
                new SecureLogString(localDoc.Id, LogMessageSensitivity.PotentiallyInsecure),
                rawDoc->revID.CreateString());

            return true;
        }

        private FLSliceResult EmptyFLSliceResult()
        {
            FLEncoder* encoder = SharedEncoder;
            Native.FLEncoder_BeginDict(encoder, 0);
            Native.FLEncoder_EndDict(encoder);
            var body = NativeRaw.FLEncoder_Finish(encoder, null);
            Native.FLEncoder_Reset(encoder);

            return body;
        }

        private void VerifyDB(Document document)
        {
            if (document.Collection == null) {
                document.Collection = GetDefaultCollection();
            } else if (document.Collection != GetDefaultCollection()) {
                throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter,
                    CouchbaseLiteErrorMessage.DocumentAnotherDatabase);
            }
        }

        private void PurgeDocById(string id)
        {
            GetDefaultCollection().Purge(id);
        }

        private void ReleaseC4Db()
        {
            Native.c4db_release(_c4db);
            _c4db = null;
        }

        private void CheckOpenAndNotClosing()
        {
            if (IsClosed || _isClosing) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.DBClosed);
            }
        }

        private void ClearScopesCollections()
        {
            _defaultCollection?.Dispose();
            _defaultScope?.Dispose();

            foreach (var s in _scopes) {
                s.Value?.Dispose();
            }

            _scopes.Clear();
        }

        private void GetScopesList()
        { 
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                C4Error error;
                var arrScopes = Native.c4db_scopeNames(_c4db, &error);
                if (error.code == 0) {
                    var scopesCnt = Native.FLArray_Count((FLArray*)arrScopes);
                    if (_scopes.Count > scopesCnt) 
                        _scopes.Clear();

                    for (uint i = 0; i < scopesCnt; i++) {
                        var scopeStr = (string?)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrScopes, i));
                        if (scopeStr != null && !_scopes.ContainsKey(scopeStr)) {
                            var s = new Scope(this, scopeStr);
                            var cnt = s.GetCollections().Count;
                            if(scopeStr == _defaultCollectionName || cnt > 0)
                                _scopes.TryAdd(scopeStr, s);
                        }
                    }
                }

                Native.FLValue_Release((FLValue*)arrScopes);
            });
        }

        #endregion

        #region object
        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Path?.GetHashCode() ?? 0;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj == null)
                return false;

            if (!(obj is Database other)) {
                return false;
            }

            return String.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override string ToString() => $"DB[{Path}]";
        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            // Do this here because otherwise if a purge job runs there will
            // be a deadlock while the purge job waits for the lock that is held
            // by the disposal which is waiting for timer callbacks to finish
            var isClosed = ThreadSafety.DoLocked(() =>
            {
                if (IsClosed) {
                    return true;
                }

                if (!_isClosing) {
                    _isClosing = true;
                }

                return false;
            });

            if(isClosed) {
                return;
            }

            foreach (var q in ActiveStoppables) {
                q.Key.Stop();
            }

            while (!_closeCondition.Wait(TimeSpan.FromSeconds(5))) {
                WriteLog.To.Database.W(Tag, "Taking a while for active items to stop...");
            }

            ThreadSafety.DoLocked(() =>
            {
                Dispose(true);
            });
        }

        #endregion
    }
}

