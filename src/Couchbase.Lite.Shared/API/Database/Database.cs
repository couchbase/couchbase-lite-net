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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Interop;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

using Newtonsoft.Json;

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
    /// A Couchbase Lite database.  This class is responsible for CRUD operations revolving around
    /// <see cref="Document"/> instances.  It is portable between platforms if the file is retrieved,
    /// and can be seeded with prepopulated data if desired.
    /// </summary>
    public sealed unsafe class Database : IDisposable
    {
        #region Constants

        private static readonly C4DatabaseConfig DBConfig = new C4DatabaseConfig {
            flags = C4DatabaseFlags.Create | C4DatabaseFlags.AutoCompact | C4DatabaseFlags.SharedKeys,
            storageEngine = "SQLite",
            versioning = C4DocumentVersioning.RevisionTrees
        };

        private const string DBExtension = "cblite2";

        private const string Tag = nameof(Database);

        private static readonly C4DocumentObserverCallback _DocumentObserverCallback = DocObserverCallback;
        private static readonly C4DatabaseObserverCallback _DatabaseObserverCallback = DbObserverCallback;

        #endregion

        #region Variables

        private static readonly TimeSpan HousekeepingDelayAfterOpen = TimeSpan.FromSeconds(3);

        [NotNull]
        private readonly Dictionary<string, Tuple<IntPtr, GCHandle>> _docObs = new Dictionary<string, Tuple<IntPtr, GCHandle>>();

        [NotNull]
        private readonly FilteredEvent<string, DocumentChangedEventArgs> _documentChanged =
            new FilteredEvent<string, DocumentChangedEventArgs>();

        [NotNull]
        private readonly Event<DatabaseChangedEventArgs> _databaseChanged = 
            new Event<DatabaseChangedEventArgs>();
        
        [NotNull]
        private readonly HashSet<Document> _unsavedDocuments = new HashSet<Document>();

        [NotNull]
        private readonly TaskFactory _callbackFactory = new TaskFactory(new QueueTaskScheduler());

#if false
        private IJsonSerializer _jsonSerializer;
#endif

        private Timer _expirePurgeTimer;
        private C4DatabaseObserver* _obs;
        private GCHandle _obsContext;
        private C4Database* _c4db;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the configuration that was used to create the database.  The returned object
        /// is readonly; an <see cref="InvalidOperationException"/> will be thrown if the configuration
        /// object is modified.
        /// </summary>
        [NotNull]
        public DatabaseConfiguration Config { get; }

        /// <summary>
        /// Gets the number of documents in the database
        /// </summary>
        public ulong Count => ThreadSafety.DoLocked(() => Native.c4db_getDocumentCount(_c4db));

        /// <summary>
        /// Gets a <see cref="DocumentFragment"/> with the given document ID
        /// </summary>
        /// <param name="id">The ID of the <see cref="DocumentFragment"/> to retrieve</param>
        /// <returns>The <see cref="DocumentFragment"/> object</returns>
        [NotNull]
        public DocumentFragment this[string id] => new DocumentFragment(GetDocument(id));

        /// <summary>
        /// Gets the database's name
        /// </summary>
        [NotNull]
        public string Name { get; }

        /// <summary>
        /// Gets the database's path.  If the database is closed or deleted, a <c>null</c>
        /// value will be returned.
        /// </summary>
        [CanBeNull]
        public string Path
        {
            get {
                return ThreadSafety.DoLocked(() => _c4db != null ? Native.c4db_getPath(c4db) : null);
            }
        }

        [NotNull]
        [ItemNotNull]
        internal ICollection<XQuery> ActiveLiveQueries { get; } = new HashSet<XQuery>();

        [NotNull]
        [ItemNotNull]
        internal ICollection<Replicator> ActiveReplications { get; } = new HashSet<Replicator>();

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

        [NotNull]
        internal IDictionary<Uri, Replicator> Replications { get; } = new Dictionary<Uri, Replicator>();

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

        [NotNull]
        internal ThreadSafety ThreadSafety { get; } = new ThreadSafety();
        
        private bool InTransaction => ThreadSafety.DoLocked(() => _c4db != null && Native.c4db_isInTransaction(_c4db));

        #endregion

        #region Constructors

        static Database()
        {
            FLSliceExtensions.RegisterFLEncodeExtension(FLValueConverter.FLEncode);
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
        public Database([NotNull]string name, [CanBeNull]DatabaseConfiguration configuration = null)
        {
            Name = CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(name), name);
            Config = configuration?.Freeze() ?? new DatabaseConfiguration(true);
            Open();
        }

        internal Database([NotNull]Database other)
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
        /// Copies a canned database from the given path to a new database with the given name and
        /// the configuration.  The new database will be created at the directory specified in the
        /// configuration.  Without given the database configuration, the default configuration that
        /// is equivalent to setting all properties in the configuration to <c>null</c> wlil be used.
        /// </summary>
        /// <param name="path">The source database path (i.e. path to the cblite2 folder)</param>
        /// <param name="name">The name of the new database to be created</param>
        /// <param name="config">The database configuration for the new database</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> or <paramref name="name"/>
        /// are <c>null</c></exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        [ContractAnnotation("name:null => halt; path:null => halt")]
        public static void Copy([NotNull]string path, [NotNull]string name, [CanBeNull]DatabaseConfiguration config)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(path), path);
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(name), name);

            var destPath = DatabasePath(name, config?.Directory);
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

				return Native.c4db_copy(path, destPath, &nativeConfig, err);
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
        [ContractAnnotation("name:null => halt")]
        public static void Delete([NotNull]string name, [CanBeNull]string directory)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(name), name);

            var path = DatabasePath(name, directory);
            LiteCoreBridge.Check(err => Native.c4db_deleteAtPath(path, err) || err->code == 0);
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
        [ContractAnnotation("name:null => halt")]
        public static bool Exists([NotNull]string name, [CanBeNull]string directory)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(name), name);

            return Directory.Exists(DatabasePath(name, directory));
        }

		/// <summary>
		/// Sets the log level for the given domains(s)
		/// </summary>
		/// <param name="domains">The log domain(s)</param>
		/// <param name="level">The log level</param>
		public static void SetLogLevel(LogDomain domains, LogLevel level)
		{
			if(domains.HasFlag(LogDomain.Couchbase)) {
				Log.To.Couchbase.Level = level;
			    Log.To.LiteCore.Level = level;
			}

			if(domains.HasFlag(LogDomain.Database)) {
				Log.To.Database.Level = level;
			}

			if(domains.HasFlag(LogDomain.Query)) {
				Log.To.Query.Level = level;
			}

			if(domains.HasFlag(LogDomain.Replicator)) {
				Log.To.Sync.Level = level;
			    Native.c4log_setLevel(Log.LogDomainSyncBusy, (C4LogLevel)level);
			}

		    if (domains.HasFlag(LogDomain.Network)) {
		        Native.c4log_setLevel(Log.LogDomainBLIP, (C4LogLevel)level);
                Native.c4log_setLevel(Log.LogDomainWebSocket, (C4LogLevel)level);
		    }
		}

        internal static IReadOnlyDictionary<LogDomain, LogLevel> GetLogLevels(LogDomain domains)
        {
            var retVal = new Dictionary<LogDomain, LogLevel>();
            if(domains.HasFlag(LogDomain.Couchbase)) {
                retVal[LogDomain.Couchbase] = Log.To.Couchbase.Level;
            }

            if(domains.HasFlag(LogDomain.Database)) {
                retVal[LogDomain.Database] = Log.To.Database.Level;
            }

            if(domains.HasFlag(LogDomain.Query)) {
                retVal[LogDomain.Query] = Log.To.Query.Level;
            }

            if(domains.HasFlag(LogDomain.Replicator)) {
                retVal[LogDomain.Replicator] = Log.To.Sync.Level;
            }

            if (domains.HasFlag(LogDomain.Network)) {
                retVal[LogDomain.Network] = (LogLevel)Native.c4log_getLevel(Log.LogDomainBLIP);
            }

            return retVal;
        }
        
        /// <summary>
        /// Adds a change listener for the changes that occur in this database.  Signatures
        /// are the same as += style event handlers, but the callbacks will be called using the
        /// specified <see cref="TaskScheduler"/>.  If the scheduler is null, the default task
        /// scheduler will be used (scheduled via thread pool).
        /// </summary>
        /// <param name="scheduler">The scheduler to use when firing the change handler</param>
        /// <param name="handler">The handler to invoke</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [ContractAnnotation("handler:null => halt")]
        public ListenerToken AddChangeListener([CanBeNull]TaskScheduler scheduler,
            [NotNull]EventHandler<DatabaseChangedEventArgs> handler)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(handler), handler);

            return ThreadSafety.DoLocked(() =>
            {
                CheckOpen();

                var cbHandler = new CouchbaseEventHandler<DatabaseChangedEventArgs>(handler, scheduler);
                if (_databaseChanged.Add(cbHandler) == 0) {
                    _obsContext = GCHandle.Alloc(this);
                    _obs = Native.c4dbobs_create(_c4db, _DatabaseObserverCallback, GCHandle.ToIntPtr(_obsContext).ToPointer());
                }

                return new ListenerToken(cbHandler, "db");
            });
        }

        /// <summary>
        /// Adds a change listener for the changes that occur in this database.  Signatures
        /// are the same as += style event handlers.  The callback will be invoked on a thread pool
        /// thread.
        /// </summary>
        /// <param name="handler">The handler to invoke</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [ContractAnnotation("null => halt")]
        public ListenerToken AddChangeListener([NotNull]EventHandler<DatabaseChangedEventArgs> handler) => AddChangeListener(null, handler);

        /// <summary>
        /// Adds a document change listener for the document with the given ID and the <see cref="TaskScheduler"/>
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
        [ContractAnnotation("id:null => halt; handler:null => halt")]
        public ListenerToken AddDocumentChangeListener([NotNull]string id, [CanBeNull]TaskScheduler scheduler,
            [NotNull]EventHandler<DocumentChangedEventArgs> handler)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(id), id);
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(handler), handler);

            return ThreadSafety.DoLocked(() =>
            {
                CheckOpen();

                var cbHandler =
                    new CouchbaseEventHandler<string, DocumentChangedEventArgs>(handler, id, scheduler);
                var count = _documentChanged.Add(cbHandler);
                if (count == 0) {
                    var handle = GCHandle.Alloc(this);
                    var docObs = Native.c4docobs_create(_c4db, id, _DocumentObserverCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    _docObs[id] = Tuple.Create((IntPtr)docObs, handle);
                }
                
                return new ListenerToken(cbHandler, "doc");
            });
        }

        /// <summary>
        /// Adds a document change listener for the document with the given ID.  The callback will be
        /// invoked on a thread pool thread.
        /// </summary>
        /// <param name="id">The document ID</param>
        /// <param name="handler">The logic to handle the event</param>
        /// <returns>A <see cref="ListenerToken"/> that can be used to remove the listener later</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> or <paramref name="id"/>
        /// is <c>null</c></exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        [ContractAnnotation("id:null => halt; handler:null => halt")]
        public ListenerToken AddDocumentChangeListener([NotNull]string id, [NotNull]EventHandler<DocumentChangedEventArgs> handler) => AddDocumentChangeListener(id, null, handler);

        /// <summary>
        /// Closes the database
        /// </summary>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.Busy"/> if there are still active replicators
        /// or query listeners when the close call occurred</exception>
        public void Close() => Dispose();

        /// <summary>
        /// Compacts the database file by deleting unused attachment files and vacuuming
        /// the SQLite database
        /// </summary>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public void Compact()
        {
            ThreadSafety.DoLockedBridge(err =>
            {
                CheckOpen();
                return Native.c4db_compact(_c4db, err);
            });
        }

        /// <summary>
        /// Creates an index which could be a value index from <see cref="IndexBuilder.ValueIndex"/> or a full-text search index
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
        [ContractAnnotation("name:null => halt; index:null => halt")]
        public void CreateIndex([NotNull]string name, [NotNull]IIndex index)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(name), name);
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(index), index);

            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                var concreteIndex = Misc.TryCast<IIndex, QueryIndex>(index);
                var jsonObj = concreteIndex.ToJSON();
                var json = JsonConvert.SerializeObject(jsonObj);
                LiteCoreBridge.Check(err =>
                {
                    var internalOpts = concreteIndex.Options;

                    // For some reason a "using" statement here causes a compiler error
                    try {
                        return Native.c4db_createIndex(c4db, name, json, concreteIndex.IndexType, &internalOpts, err);
                    } finally {
                        internalOpts.Dispose();
                    }
                });
            });
        }

        /// <summary>
        /// Deletes a database
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
                ThrowIfActiveItems();
                LiteCoreBridge.Check(err => Native.c4db_delete(_c4db, err));
                Native.c4db_free(_c4db);
                _c4db = null;
                Native.c4dbobs_free(_obs);
                _obs = null;
                if (_obsContext.IsAllocated) {
                    _obsContext.Free();
                }
            });
        }

        /// <summary>
        /// Deletes a document from the database.  When write operations are executed
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
        [ContractAnnotation("null => halt")]
        public void Delete([NotNull]Document document) => Delete(document, ConcurrencyControl.LastWriteWins);

        /// <summary>
        /// Deletes the given <see cref="Document"/> from this database
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
        [ContractAnnotation("document:null => halt")]
        public bool Delete([NotNull]Document document, ConcurrencyControl concurrencyControl)
        {
            var doc = CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(document), document);
            return Save(doc, concurrencyControl, true);
        }

        /// <summary>
        /// Deletes the index with the given name
        /// </summary>
        /// <param name="name">The name of the index to delete</param>
        [ContractAnnotation("null => halt")]
        public void DeleteIndex(string name)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(name), name);

            ThreadSafety.DoLockedBridge(err =>
            {
                CheckOpen();
                return Native.c4db_deleteIndex(c4db, name, err);
            });
        }

        /// <summary>
        /// Gets the <see cref="Document"/> with the specified ID
        /// </summary>
        /// <param name="id">The ID to use when creating or getting the document</param>
        /// <returns>The instantiated document, or <c>null</c> if it does not exist</returns>
        [CanBeNull]
        [ContractAnnotation("null => halt")]
        public Document GetDocument(string id)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(id), id);
            return ThreadSafety.DoLocked(() => GetDocumentInternal(id));
        }

        /// <summary>
        /// Gets a list of index names that are present in the database
        /// </summary>
        /// <returns>The list of created index names</returns>
        [NotNull]
        [ItemNotNull]
        public IList<string> GetIndexes()
        {
            object retVal = null;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                var result = new FLSliceResult();
                LiteCoreBridge.Check(err =>
                {
                    result = NativeRaw.c4db_getIndexes(c4db, err);
                    return result.buf != null;
                });

                var val = NativeRaw.FLValue_FromData(new FLSlice(result.buf, result.size), FLTrust.Trusted);
                if (val == null) {
                    Native.FLSliceResult_Release(result);
                    throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError);
                }

                retVal = FLValueConverter.ToCouchbaseObject(val, this, true, typeof(string));
                Native.FLSliceResult_Release(result);
            });

            return retVal as IList<string> ?? new List<string>();
        }

        /// <summary>
        /// Runs the given batch of operations as an atomic unit
        /// </summary>
        /// <param name="action">The <see cref="Action"/> containing the operations. </param>
        [ContractAnnotation("null => halt")]
        public void InBatch(Action action)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(action), action);

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
        /// <exception cref="InvalidOperationException">Thrown when trying to purge a document from a database
        /// other than the one it was previously added to</exception>
        [ContractAnnotation("null => halt")]
        public void Purge(Document document)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(document), document);

            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                VerifyDB(document);

                if (!document.Exists) {
                    throw new CouchbaseLiteException(C4ErrorCode.NotFound);
                }

                InBatch(() =>
                {
                    var result = Native.c4doc_purgeRevision(document.c4Doc.RawDoc, null, null);
                    if (result >= 0) {
                        LiteCoreBridge.Check(err => Native.c4doc_save(document.c4Doc.RawDoc, 0, err));
                    }
                });

                document.ReplaceC4Doc(null);
            });
        }

        /// <summary>
        /// Purges the given document id of the <see cref="Document"/> 
        /// from the database.  This leaves no trace behind and will 
        /// not be replicated
        /// </summary>
        /// <param name="docId">The id of the document to purge</param>
        /// <exception cref="C4ErrorCode.NotFound">Throws NOT FOUND error if the document 
        /// of the docId doesn't exist.</exception>
        [ContractAnnotation("null => halt")]
        public void Purge(string docId)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(docId), docId);
            InBatch(() => PurgeDocById(docId));
        }

        /// <summary>
        /// Sets an expiration date on a document. After this time, the document
        /// will be purged from the database.
        /// </summary>
        /// <param name="docId"> The ID of the <see cref="Document"/> </param> 
        /// <param name="timestamp"> Nullable expiration timestamp as a 
        /// <see cref="DateTimeOffset"/>, set timestamp to <c>null</c> 
        /// to remove expiration date time from doc.</param>
        /// <returns>Whether succesfully sets an expiration date on the document</returns>
        /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
        /// doesn't exist</exception>
        public bool SetDocumentExpiration(string docId, DateTimeOffset? timestamp)
        {
            var succeed = false;
            ThreadSafety.DoLockedBridge(err =>
            {
                if (timestamp == null) {
                    succeed = Native.c4doc_setExpiration(_c4db, docId, 0, null);
                } else {
                    var millisSinceEpoch = timestamp.Value.ToUnixTimeMilliseconds();
                    succeed = Native.c4doc_setExpiration(_c4db, docId, millisSinceEpoch, err);
                }
                SchedulePurgeExpired(TimeSpan.Zero);
                return succeed;
            });
            return succeed;
        }

        /// <summary>
        /// Returns the expiration time of the document. <c>null</c> will be returned
        /// if there is no expiration time set
        /// </summary>
        /// <param name="docId"> The ID of the <see cref="Document"/> </param>
        /// <returns>Nullable expiration timestamp as a <see cref="DateTimeOffset"/> 
        /// of the document or <c>null</c> if time not set. </returns>
        /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
        /// doesn't exist</exception>
        public DateTimeOffset? GetDocumentExpiration(string docId)
        {
            if (LiteCoreBridge.Check(err => Native.c4doc_get(_c4db, docId, true, err)) == null) {
                throw new CouchbaseLiteException(C4ErrorCode.NotFound);
            }
            var res = (long)Native.c4doc_getExpiration(_c4db, docId);
            if (res == 0) {
                return null;
            }
            return DateTimeOffset.FromUnixTimeMilliseconds(res);
        }

        /// <summary>
        /// Removes a database changed listener by token
        /// </summary>
        /// <param name="token">The token received from <see cref="AddChangeListener(TaskScheduler, EventHandler{DatabaseChangedEventArgs})"/>
        /// and family</param>
        public void RemoveChangeListener(ListenerToken token)
        {
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();

                if (token.Type == "db") {
                    if (_databaseChanged.Remove(token) == 0) {
                        Native.c4dbobs_free(_obs);
                        _obs = null;
                        if (_obsContext.IsAllocated) {
                            _obsContext.Free();
                        }
                    }
                } else {
                    if (_documentChanged.Remove(token, out var docID) == 0) {
                        if (_docObs.TryGetValue(docID, out var observer)) {
                            _docObs.Remove(docID);
                            Native.c4docobs_free((C4DocumentObserver *)observer.Item1);
                            observer.Item2.Free();
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Saves the given <see cref="MutableDocument"/> into this database.  This call is equivalent to calling
        /// <see cref="Save(MutableDocument, ConcurrencyControl)" /> with a second argument of
        /// <see cref="ConcurrencyControl.LastWriteWins"/>
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        [ContractAnnotation("null => halt")]
        public void Save(MutableDocument document) => Save(document, ConcurrencyControl.LastWriteWins);

        /// <summary>
        /// Saves the given <see cref="MutableDocument"/> into this database
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="concurrencyControl">The rule to use when encountering a conflict in the database</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
        [ContractAnnotation("document:null => halt")]
        public bool Save(MutableDocument document, ConcurrencyControl concurrencyControl)
        {
            var doc = CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(document), document);
            return Save(doc, concurrencyControl, false);
        }

        #if CBL_LINQ
        public void Save(Couchbase.Lite.Linq.IDocumentModel model)
        {
            CBDebug.MustNotBeNull(Log.To.Database, Tag, nameof(model), model);

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

#if COUCHBASE_ENTERPRISE
		/// <summary>
		/// Sets the encryption key for the database.  If null, encryption is
		/// removed.
		/// </summary>
		/// <param name="key">The new key to encrypt the database with, or <c>null</c>
		/// to remove encryption</param>
		public void ChangeEncryptionKey([CanBeNull]EncryptionKey key)
		{
			ThreadSafety.DoLockedBridge(err =>
			{
				var newKey = new C4EncryptionKey
				{
					algorithm = key == null ? C4EncryptionAlgorithm.None : C4EncryptionAlgorithm.AES256
				};

			    if (key != null) {
			        var i = 0;
			        foreach (var b in key.KeyData) {
			            newKey.bytes[i++] = b;
			        }
			    }

			    return Native.c4db_rekey(c4db, &newKey, err);
			});
		}
#endif

        #endregion

        #region Internal Methods

        internal void ResolveConflict([NotNull]string docID)
        {
            Debug.Assert(docID != null);

            ThreadSafety.DoLocked(() =>
            {
                var success = true;
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(_c4db, err));
                try {
                    Document localDoc = null, remoteDoc = null;
                    try {
                        localDoc = new Document(this, docID);
                        if (!localDoc.Exists) {
                            throw new CouchbaseLiteException(C4ErrorCode.NotFound);
                        }

                        remoteDoc = new Document(this, docID);
                        if (!remoteDoc.SelectConflictingRevision()) {
                            Log.To.Sync.W(Tag, "Unable to select conflicting revision for '{0}', skipping...",
                                new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
                            return;
                        }

                        // Resolve conflict:
                        Log.To.Database.I(Tag, "Resolving doc '{0}' (mine={1} and theirs={2})",
                            new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure), localDoc.RevID,
                            remoteDoc.RevID);
                        var resolvedDoc = ResolveConflict(localDoc, remoteDoc);
                        SaveResolvedDocument(resolvedDoc, localDoc, remoteDoc);
                    } finally {
                        localDoc?.Dispose();
                        remoteDoc?.Dispose();
                    }
                } catch (Exception) {
                    success = false;
                    throw;
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(_c4db, success, err));
                }
            });
        }

        internal void SchedulePurgeExpired(TimeSpan delay)
        {
            var nextExpiration = Native.c4db_nextDocExpiration(_c4db);
            if (nextExpiration > 0) {
                var delta = (DateTimeOffset.FromUnixTimeMilliseconds((long)nextExpiration) - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));
                var expirationTimeSpan = delta > delay ? delta : delay;
                if (expirationTimeSpan.TotalMilliseconds >= UInt32.MaxValue) {
                    _expirePurgeTimer.Change(TimeSpan.FromMilliseconds(UInt32.MaxValue - 1), TimeSpan.FromMilliseconds(-1));
                    Log.To.Database.I(Tag, "{0:F3} seconds is too far in the future to schedule a document expiration," +
                                           " will run again at the maximum value of {0:F3} seconds", expirationTimeSpan.TotalSeconds, (UInt32.MaxValue - 1) / 1000);
                } else if (expirationTimeSpan.TotalSeconds <= Double.Epsilon) {
                    _expirePurgeTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    PurgeExpired(null);
                } else {
                    _expirePurgeTimer.Change(expirationTimeSpan, TimeSpan.FromMilliseconds(-1));
                    Log.To.Database.I(Tag, "Scheduling next doc expiration in {0:F3} seconds", expirationTimeSpan.TotalSeconds);
                }
            } else {
                Log.To.Database.I(Tag, "No pending doc expirations");
            }
        }

        #endregion

        #region Private Methods

        [NotNull]
        private static string DatabasePath(string name, string directory)
        {
            var directoryToUse = String.IsNullOrWhiteSpace(directory)
                ? Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory()
                : directory;

            if (String.IsNullOrWhiteSpace(directoryToUse)) {
                throw new RuntimeException(
                    "Failed to resolve a default directory!  If you have overriden the IDefaultDirectoryResolver interface, please check it.  Otherwise please file a bug report.");
            }

            if (String.IsNullOrWhiteSpace(name)) {
                return directoryToUse;
            }
            
            return System.IO.Path.Combine(directoryToUse, $"{name}.{DBExtension}") ?? throw new RuntimeException("Path.Combine failed to return a non-null value!");
        }

        private static void DbObserverCallback(C4DatabaseObserver* db, void* context)
        {
            var dbObj = GCHandle.FromIntPtr((IntPtr)context).Target as Database;
            dbObj?._callbackFactory.StartNew(() => {
              dbObj.PostDatabaseChanged();
            });
        }

        private static void DocObserverCallback(C4DocumentObserver* obs, FLSlice docId, ulong sequence, void* context)
        {
            if (docId.buf == null) {
                return;
            }

            var dbObj = GCHandle.FromIntPtr((IntPtr) context).Target as Database;
            dbObj?._callbackFactory.StartNew(() => {
                dbObj.PostDocChanged(docId.CreateString());
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

            if (disposing) {
                if (_obs != null) {
                    Native.c4dbobs_free(_obs);
                    _obsContext.Free();
                }

                foreach (var obs in _docObs) {
                    Native.c4docobs_free((C4DocumentObserver *)obs.Value.Item1);
                    obs.Value.Item2.Free();
                }

                _docObs.Clear();
                if (_unsavedDocuments.Count > 0) {
                    Log.To.Database.W(Tag,
                        $"Closing database with {_unsavedDocuments.Count} such as {_unsavedDocuments.Any()}");
                }

                _unsavedDocuments.Clear();
            }

            Log.To.Database.I(Tag, $"Closing database at path {Native.c4db_getPath(_c4db)}");
            LiteCoreBridge.Check(err => Native.c4db_close(_c4db, err));
            Native.c4db_free(_c4db);
            _c4db = null;
        }

        [CanBeNull]
        private Document GetDocumentInternal([NotNull]string docID)
        {
            CheckOpen();
            var doc = new Document(this, docID);

            if (!doc.Exists || doc.IsDeleted) {
                doc.Dispose();
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

            try {
                Directory.CreateDirectory(Config.Directory);
            } catch (Exception e) {
                throw new CouchbaseLiteException(C4ErrorCode.CantOpenFile, "Unable to create database directory", e);
            }

            var path = DatabasePath(Name, Config.Directory);
            var config = DBConfig;

            var encrypted = "";

            #if COUCHBASE_ENTERPRISE
            if(Config.EncryptionKey != null) {
                var key = Config.EncryptionKey;
                var i = 0;
                config.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
                foreach(var b in key.KeyData) {
                    config.encryptionKey.bytes[i++] = b;
                }

                encrypted = "encrypted ";
            }
            #endif

            Log.To.Database.I(Tag, $"Opening {encrypted}database at {path}");
            var localConfig1 = config;
            ThreadSafety.DoLocked(() =>
            {
                _c4db = (C4Database*) LiteCoreBridge.Check(err =>
                {
                    var localConfig2 = localConfig1;
                    return Native.c4db_open(path, &localConfig2, err);
                });
            });

            _expirePurgeTimer = new Timer(PurgeExpired, null, HousekeepingDelayAfterOpen, TimeSpan.FromMilliseconds(-1));
        }

        private void PostDatabaseChanged()
        {
			ThreadSafety.DoLocked(() =>
			{
				if (_obs == null || _c4db == null || InTransaction) {
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
					nChanges = Native.c4dbobs_getChanges(_obs, changes, maxChanges, &newExternal);
				    if (nChanges == 0 || external != newExternal || docIDs.Count > 1000) {
				        if (docIDs.Count > 0) {
                            // Only notify if there are actually changes to send
				            var args = new DatabaseChangedEventArgs(this, docIDs);
				            _databaseChanged.Fire(this, args);
				            docIDs = new List<string>();
				        }
				    }

				    external = newExternal;
				    for (var i = 0; i < nChanges; i++) {
				        docIDs.Add(changes[i].docID.CreateString());
				    }
                    Native.c4dbobs_releaseChanges(changes, nChanges);
				} while (nChanges > 0);
			});
        }

        private void PostDocChanged([NotNull]string documentID)
        {
            DocumentChangedEventArgs change = null;
            ThreadSafety.DoLocked(() =>
            {
                if (_c4db == null || !_docObs.ContainsKey(documentID) || Native.c4db_isInTransaction(_c4db)) {
                    return;
                }

                change = new DocumentChangedEventArgs(documentID, this);
            });

            _documentChanged.Fire(documentID, this, change);
        }

        [NotNull]
        private Document ResolveConflict([NotNull]Document localDoc, [NotNull]Document remoteDoc)
        {
            if (remoteDoc.IsDeleted) {
                return remoteDoc;
            }

            if (localDoc.IsDeleted) {
                return localDoc;
            }

            if (localDoc.Generation > remoteDoc.Generation) {
                return localDoc;
            }

            if (remoteDoc.Generation > localDoc.Generation) {
                return remoteDoc;
            }

            return String.CompareOrdinal(localDoc.RevID, remoteDoc.RevID) > 0 ? localDoc : remoteDoc;
        }
        
        private bool Save([NotNull]Document document, ConcurrencyControl concurrencyControl, bool deletion)
        {
            if (deletion && document.RevID == null) {
                throw new CouchbaseLiteException(C4ErrorCode.NotFound, "Cannot delete a document that has not yet been saved");
            }

            var success = true;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                VerifyDB(document);
                C4Document* curDoc = null;
                C4Document* newDoc = null;
                var committed = false;
                try {
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(_c4db, err));
                    Save(document, &newDoc, null, deletion);
                    if (newDoc == null) {
                        // Handle conflict:
                        if (concurrencyControl == ConcurrencyControl.FailOnConflict) {
                            success = false;
                            committed = true; // Weird, but if the next call fails I don't want to call it again in the catch block
                            LiteCoreBridge.Check(e => Native.c4db_endTransaction(_c4db, true, e));
                            return;
                        }

                        C4Error err;
                        curDoc = Native.c4doc_get(_c4db, document.Id, true, &err);

                        // If deletion and the current doc has already been deleted
                        // or doesn't exist:
                        if (deletion) {
                            if (curDoc == null) {
                                if (err.code == (int) C4ErrorCode.NotFound) {
                                    return;
                                }

                                throw CouchbaseException.Create(err);
                            } else if (curDoc->flags.HasFlag(C4DocumentFlags.DocDeleted)) {
                                document.ReplaceC4Doc(new C4DocumentWrapper(curDoc));
                                curDoc = null;
                                return;

                            }
                        }

                        // Save changes on the current branch:
                        if (curDoc == null) {
                            throw CouchbaseException.Create(err);
                        }

                        Save(document, &newDoc, curDoc, deletion);
                    }
                    
                    committed = true; // Weird, but if the next call fails I don't want to call it again in the catch block
                    LiteCoreBridge.Check(e => Native.c4db_endTransaction(_c4db, true, e));
                    document.ReplaceC4Doc(new C4DocumentWrapper(newDoc));
                    newDoc = null;
                } catch (Exception) {
                    if (!committed) {
                        LiteCoreBridge.Check(e => Native.c4db_endTransaction(_c4db, false, e));
                    }

                    throw;
                } finally {
                    Native.c4doc_free(curDoc);
                    Native.c4doc_free(newDoc);
                }
            });

            return success;
        }
        
        private void Save([NotNull]Document doc, C4Document** outDoc, C4Document* baseDoc, bool deletion)
        {
            var revFlags = (C4RevisionFlags) 0;
            if (deletion) {
                revFlags = C4RevisionFlags.Deleted;
            }

            byte[] body = null;
            if (!deletion && !doc.IsEmpty) {
                try {
                    body = doc.Encode();
                } catch (ObjectDisposedException) {
                    Log.To.Database.E(Tag, "Save of disposed document {0} attempted, skipping...", new SecureLogString(doc.Id, LogMessageSensitivity.PotentiallyInsecure));
                    return;
                }

                // https://github.com/couchbase/couchbase-lite-net/issues/997
                // body must not move while root / rootDict are being used
                fixed (byte* b = body) {
                    var root = Native.FLValue_FromData(body, FLTrust.Trusted);
                    if (root == null) {
                        Log.To.Database.E(Tag, "Failed to encode document body properly.  Aborting save of document!");
                        return;
                    }

                    var rootDict = Native.FLValue_AsDict(root);
                    if (rootDict == null) {
                        Log.To.Database.E(Tag, "Failed to encode document body properly.  Aborting save of document!");
                        return;
                    }

                    ThreadSafety.DoLocked(() =>
                    {
                        if (Native.c4doc_dictContainsBlobs(rootDict)) {
                            revFlags |= C4RevisionFlags.HasAttachments;
                        }
                    });
                }

            } else if (doc.IsEmpty) {
                FLEncoder* encoder = SharedEncoder;
                Native.FLEncoder_BeginDict(encoder, 0);
                Native.FLEncoder_EndDict(encoder);
                body = Native.FLEncoder_Finish(encoder, null);
                Native.FLEncoder_Reset(encoder);
            }

            var rawDoc = baseDoc != null ? baseDoc :
                doc.c4Doc?.HasValue == true ? doc.c4Doc.RawDoc : null;
            if (rawDoc != null) {
                doc.ThreadSafety.DoLocked(() =>
                {
                    ThreadSafety.DoLocked(() =>
                    {
                        *outDoc = (C4Document*)NativeHandler.Create()
                            .AllowError((int)C4ErrorCode.Conflict, C4ErrorDomain.LiteCoreDomain).Execute(
                                err => Native.c4doc_update(rawDoc, body, revFlags, err));
                    });
                });
            } else {
                ThreadSafety.DoLocked(() =>
                {
                    *outDoc = (C4Document*)NativeHandler.Create()
                        .AllowError((int)C4ErrorCode.Conflict, C4ErrorDomain.LiteCoreDomain).Execute(
                            err => Native.c4doc_create(_c4db, doc.Id, body, revFlags, err));
                });
            }
        }

        // Must be called in transaction
        private void SaveResolvedDocument([NotNull]Document resolved, [NotNull]Document localDoc, [NotNull]Document remoteDoc)
        {
            if (!ReferenceEquals(resolved, localDoc)) {
                resolved.Database = this;
            }

            // The remote branch has to win, so that the doc revision history matches the server's
            var winningRevID = remoteDoc.RevID;
            var losingRevID = localDoc.RevID;

            byte[] mergedBody = null;
            if (!ReferenceEquals(resolved, remoteDoc)) {
                // Unless the remote revision is being used as-is, we need a new revision:
                try {
                    mergedBody = resolved.Encode();
                } catch (ObjectDisposedException) {
                    Log.To.Sync.E(Tag, "Resolved document for {0} somehow got disposed!",
                        new SecureLogString(resolved.Id, LogMessageSensitivity.PotentiallyInsecure));
                    throw new RuntimeException(
                        "Resolved document was disposed before conflict resolution completed.  Please file a bug report at https://github.com/couchbase/couchbase-lite-net");
                }
            }

            // Tell LiteCore to do the resolution:
            C4Document* rawDoc = localDoc.c4Doc != null ? localDoc.c4Doc.RawDoc : null;
            var flags = resolved.c4Doc != null ? resolved.c4Doc.RawDoc->selectedRev.flags : 0;
            LiteCoreBridge.Check(
                err => Native.c4doc_resolveConflict(rawDoc, winningRevID, losingRevID, mergedBody, flags, err));
            LiteCoreBridge.Check(err => Native.c4doc_save(rawDoc, 0, err));

            Log.To.Database.I(Tag, "Conflict resolved as doc '{0}' rev {1}",
                new SecureLogString(localDoc.Id, LogMessageSensitivity.PotentiallyInsecure),
                rawDoc->revID.CreateString());
        }

        private void ThrowIfActiveItems()
        {
            if (ActiveReplications.Any()) {
                var c4err = Native.c4error_make(C4ErrorDomain.LiteCoreDomain, (int) C4ErrorCode.Busy,
                    "Cannot close the database. Please stop all of the replicators before closing the database.");
                throw new CouchbaseLiteException(c4err);
            }

            if (ActiveLiveQueries.Any()) {
                var c4err = Native.c4error_make(C4ErrorDomain.LiteCoreDomain, (int) C4ErrorCode.Busy,
                    "Cannot close the database. Please remove all of the query listeners before closing the database.");
                throw new CouchbaseLiteException(c4err);
            }
        }

        private void VerifyDB([NotNull]Document document)
        {
            if (document.Database == null) {
                document.Database = this;
            } else if (document.Database != this) {
                throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter, "Cannot operate on a document from another database");
            }
        }

        private void PurgeDocById(string id)
        {
            ThreadSafety.DoLockedBridge(err =>
            {
                return Native.c4db_purgeDoc(_c4db, id, err);
            });
        }

        private void PurgeExpired(object state)
        {
            var cnt = 0L;
            LiteCoreBridge.Check(err =>
            {
                CheckOpen();
                cnt = Native.c4db_purgeExpiredDocs(_c4db, err);
                Log.To.Database.I(Tag, "{0} purged {1} expired documents", this, cnt);
                return err;
            });
            SchedulePurgeExpired(TimeSpan.FromSeconds(1));
        }

        #endregion

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Path?.GetHashCode() ?? 0;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (!(obj is Database other)) {
                return false;
            }

            return String.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override string ToString() => $"DB[{Path}]";

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            ThreadSafety.DoLocked(() =>
            {
                ThrowIfActiveItems();
                Dispose(true);
            });
        }

        #endregion
    }
}

