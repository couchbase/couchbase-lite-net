//
// Database.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Store;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using Couchbase.Lite.Storage;
using SQLitePCL;
using System.Threading;


#if !NET_3_5
using System.Net;
using StringEx = System.String;
#else
using System.Net.Couchbase;
#endif

namespace Couchbase.Lite 
{
    
    internal delegate Status StoreValidation(RevisionInternal rev, RevisionInternal prevRev, string parentRevId);

    /// <summary>
    /// A Couchbase Lite Database.
    /// </summary>
    public sealed class Database : IDisposable
    {
        #region Constants

        internal const string TAG = "Database";
        internal const string TAG_SQL = "CBLSQL";
        internal const int BIG_ATTACHMENT_LENGTH = 2 * 1024;

        private const bool AUTO_COMPACT = true;
        private const int NOTIFY_CHANGES_LIMIT = 5000;
        private const int MAX_DOC_CACHE_SIZE = 50;
        private const int DEFAULT_MAX_REVS = 20;
        private const int DOC_ID_CACHE_SIZE = 1000;
        private const double SQLITE_BUSY_TIMEOUT = 5.0; //seconds
        private const int TRANSACTION_MAX_RETRIES = 10;
        private const int TRANSACTION_MAX_RETRY_DELAY = 50; //milliseconds

        private static readonly int _SqliteVersion;

        private static readonly HashSet<string> SPECIAL_KEYS_TO_REMOVE = new HashSet<string> {
            "_id", "_rev", "_deleted", "_revisions", "_revs_info", "_conflicts", "_deleted_conflicts",
            "_local_seq"
        };

        private static readonly HashSet<string> SPECIAL_KEYS_TO_LEAVE = new HashSet<string> {
            "_removed", "_attachments"
        };

        private const string SCHEMA = 
            // docs            
            "CREATE TABLE docs ( " +
            "        doc_id INTEGER PRIMARY KEY, " +
            "        docid TEXT UNIQUE NOT NULL); " +
            "    CREATE INDEX docs_docid ON docs(docid); " +
            // revs
            "    CREATE TABLE revs ( " +
            "        sequence INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "        doc_id INTEGER NOT NULL REFERENCES docs(doc_id) ON DELETE CASCADE, " +
            "        revid TEXT NOT NULL COLLATE REVID, " +
            "        parent INTEGER REFERENCES revs(sequence) ON DELETE SET NULL, " +
            "        current BOOLEAN, " +
            "        deleted BOOLEAN DEFAULT 0, " +
            "        json BLOB, " +
            "        no_attachments BOOLEAN, " +
            "        UNIQUE (doc_id, revid)); " +
            "    CREATE INDEX revs_parent ON revs(parent); " +
            "    CREATE INDEX revs_by_docid_revid ON revs(doc_id, revid desc, current, deleted); " +
            "    CREATE INDEX revs_current ON revs(doc_id, current desc, deleted, revid desc); " +
            // localdocs
            "    CREATE TABLE localdocs ( " +
            "        docid TEXT UNIQUE NOT NULL, " +
            "        revid TEXT NOT NULL COLLATE REVID, " +
            "        json BLOB); " +
            "    CREATE INDEX localdocs_by_docid ON localdocs(docid); " +
            // views
            "    CREATE TABLE views ( " +
            "        view_id INTEGER PRIMARY KEY, " +
            "        name TEXT UNIQUE NOT NULL," +
            "        version TEXT, " +
            "        lastsequence INTEGER DEFAULT 0," +
            "        total_docs INTEGER DEFAULT -1); " +
            "    CREATE INDEX views_by_name ON views(name); " +
            // info
            "    CREATE TABLE info (" +
            "        key TEXT PRIMARY KEY," +
            "        value TEXT);" +
            // version
            "    PRAGMA user_version = 17";

        #endregion

        #region Variables

        /// <summary>
        /// Each database can have an associated PersistentCookieStore,
        /// where the persistent cookie store uses the database to store
        /// its cookies.
        /// </summary>
        /// <remarks>
        /// Each database can have an associated PersistentCookieStore,
        /// where the persistent cookie store uses the database to store
        /// its cookies.
        /// There are two reasons this has been made an instance variable
        /// of the Database, rather than of the Replication:
        /// - The PersistentCookieStore needs to span multiple replications.
        /// For example, if there is a "push" and a "pull" replication for
        /// the same DB, they should share a cookie store.
        /// - PersistentCookieStore lifecycle should be tied to the Database
        /// lifecycle, since it needs to cease to exist if the underlying
        /// Database ceases to exist.
        /// REF: https://github.com/couchbase/couchbase-lite-android/issues/269
        /// </remarks>
        private CookieStore                             _persistentCookieStore; // Not used yet.

        private bool                                    _isOpen;
        private IDictionary<string, BlobStoreWriter>    _pendingAttachmentsByDigest;
        private IDictionary<string, View>               _views;
        private IList<DocumentChange>                   _changesToNotify;
        private bool                                    _isPostingChangeNotifications;
        private object                                  _allReplicatorsLocker = new object();
        private int _transactionCount;
        private LruCache<string, object> _docIDs = new LruCache<string, object>(DOC_ID_CACHE_SIZE);

        #endregion

        #region Properties

        internal Status LastDbStatus
        {
            get
            {
                switch (StorageEngine.LastErrorCode) {
                    case raw.SQLITE_OK:
                    case raw.SQLITE_ROW:
                    case raw.SQLITE_DONE:
                        return new Status(StatusCode.Ok);
                    case raw.SQLITE_BUSY:
                    case raw.SQLITE_LOCKED:
                        return new Status(StatusCode.DbBusy);
                    case raw.SQLITE_CORRUPT:
                        return new Status(StatusCode.CorruptError);
                    case raw.SQLITE_NOTADB:
                        return new Status(StatusCode.Unauthorized);
                    default:
                        Log.I(TAG, "Other LastErrorCode {0}", StorageEngine.LastErrorCode);
                        return new Status(StatusCode.DbError);
                }
            }
        }

        internal Status LastDbError
        {
            get
            {
                var status = LastDbStatus;
                return (status.Code == StatusCode.Ok) ? new Status(StatusCode.DbError) : status;
            }
        }

        /// <summary>
        /// Gets or sets an object that can compile source code into <see cref="FilterDelegate"/>.
        /// </summary>
        /// <value>The filter compiler object.</value>
        public static IFilterCompiler FilterCompiler { get; set; }

        public bool InTransaction
        { 
            get {
                return _transactionCount > 0;
            }
        }

        /// <summary>
        /// Gets the container the holds cookie information received from the remote replicator
        /// </summary>
        public CookieContainer PersistentCookieStore
        {
            get {
                if (_persistentCookieStore == null) {
                    _persistentCookieStore = new CookieStore(System.IO.Path.GetDirectoryName(Path));
                }

                return _persistentCookieStore;
            }
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> name.
        /// </summary>
        /// <value>The database name.</value>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Manager" /> that owns this <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <value>The manager object.</value>
        public Manager Manager { get; private set; }

        /// <summary>
        /// Gets the number of <see cref="Couchbase.Lite.Document" /> in the <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <value>The document count.</value>
        /// TODO: Convert this to a standard method call.
        public int DocumentCount 
        {
            get {
                return QueryOrDefault<int>(c => c.GetInt(0),
                    false, -1, "SELECT COUNT(DISTINCT doc_id) FROM revs WHERE current=1 AND deleted=0");
            }
        }

        /// <summary>
        /// Gets the latest sequence number used by the <see cref="Couchbase.Lite.Database" />.  Every new <see cref="Couchbase.Lite.Revision" /> is assigned a new sequence 
        /// number, so this property increases monotonically as changes are made to the <see cref="Couchbase.Lite.Database" />. This can be used to 
        /// check whether the <see cref="Couchbase.Lite.Database" /> has changed between two points in time.
        /// </summary>
        /// <value>The last sequence number.</value>
        public long LastSequenceNumber 
        {
            get {
                return QueryOrDefault<long>(c => c.GetLong(0),
                    false, 0L, "SELECT seq FROM sqlite_sequence WHERE name='revs'");
            }
        }

        /// <summary>
        /// Gets the total size of the database on the filesystem.
        /// </summary>
        public long TotalDataSize {
            get {
                string dir = System.IO.Path.GetDirectoryName(Path);
                var info = new DirectoryInfo(dir);
                long size = 0;
                var sanitizedName = Name.Replace('/', '.');

                // Database files
                foreach (var fileInfo in info.EnumerateFiles(sanitizedName + "*", SearchOption.TopDirectoryOnly)) {
                    size += fileInfo.Length;
                }

                // Attachment files
                dir = AttachmentStorePath;
                info = new DirectoryInfo(dir);
                if (!info.Exists) {
                    return size;
                }

                foreach (var fileInfo in info.EnumerateFiles()) {
                    size += fileInfo.Length;
                }

                return size;
            }
        }

        /// <summary>
        /// Gets all the running <see cref="Couchbase.Lite.Replication" />s 
        /// for this <see cref="Couchbase.Lite.Database" />.  
        /// This includes all continuous <see cref="Couchbase.Lite.Replication" />s and 
        /// any non-continuous <see cref="Couchbase.Lite.Replication" />s that has been started 
        /// and are still running.
        /// </summary>
        /// <value>All replications.</value>
        public IEnumerable<Replication> AllReplications { get { return AllReplicators.ToList(); } }

        /// <summary>
        /// Maximum depth of a document's revision tree (or, max length of its revision history.)
        /// Revisions older than this limit will be deleted during a -compact: operation.
        /// </summary>
        /// <remarks>
        /// Maximum depth of a document's revision tree (or, max length of its revision history.)
        /// Revisions older than this limit will be deleted during a -compact: operation.
        /// Smaller values save space, at the expense of making document conflicts somewhat more likely.
        /// </remarks>
        public Int32 MaxRevTreeDepth 
        {
            get {
                return _maxRevTreeDepth;
            }
            set {
                if (value == 0) {
                    value = DEFAULT_MAX_REVS;
                }

                if (value != _maxRevTreeDepth) {
                    SetInfo("max_revs", value.ToString());
                }

                _maxRevTreeDepth = value;
            }
        }
        private int _maxRevTreeDepth;

        internal SharedState Shared { 
            get {
                return Manager.Shared;
            }
        }

        internal ISQLiteStorageEngine                   StorageEngine { get; set; }
        internal String                                 Path { get; private set; }
        internal IList<Replication>                     ActiveReplicators { get; set; }
        internal IList<Replication>                     AllReplicators { get; set; }
        internal LruCache<String, Document>             DocumentCache { get; set; }
        internal IDictionary<String, WeakReference>     UnsavedRevisionDocumentCache { get; set; }
        internal long StartTime { get; private set; }
        internal BlobStoreWriter AttachmentWriter { get { return new BlobStoreWriter(Attachments); } }

        internal BlobStore Attachments { get; set; }


        private IDictionary<String, FilterDelegate>     Filters { get; set; }
        private TaskFactory Scheduler { get; set; }

        private IDictionary<string, BlobStoreWriter> PendingAttachmentsByDigest
        {
            get {
                return _pendingAttachmentsByDigest ?? (_pendingAttachmentsByDigest = new Dictionary<string, BlobStoreWriter>());
            }
            set {
                _pendingAttachmentsByDigest = value;
            }
        }

        #endregion

        #region Constructors

        // "_local/*" is not a valid document ID. Local docs have their own API and shouldn't get here.
        internal static String GenerateDocumentId()
        {
            return Misc.CreateGUID();
        }

        internal Database(string path, string name, Manager manager)
        {
            Debug.Assert(System.IO.Path.IsPathRooted(path));

            //path must be absolute
            Path = path;
            Name = name ?? FileDirUtils.GetDatabaseNameFromPath(path);
            Manager = manager;
            DocumentCache = new LruCache<string, Document>(MAX_DOC_CACHE_SIZE);
            UnsavedRevisionDocumentCache = new Dictionary<string, WeakReference>();

            // FIXME: Not portable to WinRT/WP8.
            ActiveReplicators = new List<Replication>();
            AllReplicators = new List<Replication> ();

            _changesToNotify = new List<DocumentChange>();
            Scheduler = new TaskFactory(new SingleTaskThreadpoolScheduler());
            StartTime = DateTime.UtcNow.ToMillisecondsSinceEpoch ();
        }

        static Database()
        {
            // Test the version of the actual SQLite implementation at runtime. Necessary because
            // the app might be linked with a custom version of SQLite (like SQLCipher) instead of the
            // system library, so the actual version/features may differ from what was declared in
            // sqlite3.h at compile time.
            Log.I(TAG, "Couchbase Lite using SQLite version {0} ({1})", raw.sqlite3_libversion(), raw.sqlite3_sourceid());
            _SqliteVersion = raw.sqlite3_libversion_number();

            Debug.Assert(_SqliteVersion >= 3007000, String.Format("SQLite library is too old ({0}); needs to be at least 3.7", raw.sqlite3_libversion()));
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Compacts the <see cref="Couchbase.Lite.Database" /> file by purging non-current 
        /// <see cref="Couchbase.Lite.Revision" />s and deleting unused <see cref="Couchbase.Lite.Attachment" />s.
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">thrown if an issue occurs while 
        /// compacting the <see cref="Couchbase.Lite.Database" /></exception>
        public bool Compact()
        {
            try {
                // Can't delete any rows because that would lose revision tree history.
                // But we can remove the JSON of non-current revisions, which is most of the space.
                try {
                    Log.V(TAG, "Deleting JSON of old revisions...");
                    PruneRevsToMaxDepth(0);

                    var args = new ContentValues();
                    args["json"] = null;
                    args["doc_type"] = null;
                    args["no_attachments"] = 1;
                    StorageEngine.Update("revs", args, "current=0", null);
                } catch (SQLException e) {
                    Log.E(TAG, "Error compacting", e);
                    throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
                }

                Log.V(TAG, "Deleting old attachments...");

                try {
                    Log.V(TAG, "Flushing SQLite WAL...");
                    StorageEngine.ExecSQL("PRAGMA wal_checkpoint(RESTART)");
                    Log.V(TAG, "Vacuuming SQLite sqliteDb...");
                    StorageEngine.ExecSQL("VACUUM");
                } catch (SQLException e) {
                    Log.E(TAG, "Error vacuuming sqliteDb", e);
                    throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
                }
            } catch(CouchbaseLiteException) {
                return false;
            }

            return GarbageCollectAttachments();
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.Database" />.
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while deleting the <see cref="Couchbase.Lite.Database" /></exception>
        public void Delete()
        {
            if (_isOpen && !Close()) {
                throw new CouchbaseLiteException("The database was open, and could not be closed", StatusCode.InternalServerError);
            }

            Manager.ForgetDatabase(this);
            if (!Exists()) {
                return;
            }

            var file = new FilePath(Path);
            var fileJournal = new FilePath(Path + "-journal");
            var fileWal = new FilePath(Path + "-wal");
            var fileShm = new FilePath(Path + "-shm");

            var deleteStatus = file.Delete();

            if (fileJournal.Exists()){
                deleteStatus &= fileJournal.Delete();
            }
            if (fileWal.Exists()) {
                deleteStatus &= fileWal.Delete();
            }
            if (fileShm.Exists()) {
                deleteStatus &= fileShm.Delete();
            }

            //recursively delete attachments path
            var attachmentsFile = new FilePath(AttachmentStorePath);
            var deleteAttachmentStatus = FileDirUtils.DeleteRecursive(attachmentsFile);

            if (!deleteStatus) {
                Log.W(TAG, "Error deleting the SQLite database file at {0}", file.GetAbsolutePath());
                throw new CouchbaseLiteException("Was not able to delete the database file", StatusCode.InternalServerError);
            }

            if (!deleteAttachmentStatus) {
                Log.W(TAG, "Error deleting the attachment files file at {0}", attachmentsFile.GetAbsolutePath());
                throw new CouchbaseLiteException("Was not able to delete the attachments files", StatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Gets or creates the <see cref="Couchbase.Lite.Document" /> with the given id.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Document" />.</returns>
        /// <param name="id">The id of the Document to get or create.</param>
        public Document GetDocument(string id) 
        { 
            return GetDocument(id, false);
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Document" /> with the given id, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Document" /> with the given id, or null if it does not exist.</returns>
        /// <param name="id">The id of the Document to get.</param>
        public Document GetExistingDocument(String id) 
        { 
            return GetDocument(id, true);
        }

        /// <summary>
        /// Creates a <see cref="Couchbase.Lite.Document" /> with a unique id.
        /// </summary>
        /// <returns>A document with a unique id.</returns>
        public Document CreateDocument()
        { 
            return GetDocument(Misc.CreateGUID());
        }

        /// <summary>
        /// Gets the local document with the given id, or null if it does not exist.
        /// </summary>
        /// <returns>The existing local document.</returns>
        /// <param name="id">Identifier.</param>
        public IDictionary<String, Object> GetExistingLocalDocument(String id) 
        {
            var gotRev = GetLocalDocument(MakeLocalDocumentId(id), null);
            return gotRev != null ? gotRev.GetProperties() : null;
        }

        /// <summary>
        /// Sets the contents of the local <see cref="Couchbase.Lite.Document" /> with the given id.  If <param name="properties"/> is null, the 
        /// <see cref="Couchbase.Lite.Document" /> is deleted.
        /// </summary>
        /// <param name="id">The id of the local document whos contents to set.</param>
        /// <param name="properties">The contents to set for the local document.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if an issue occurs 
        /// while setting the contents of the local document.</exception>
        public bool PutLocalDocument(string id, IDictionary<string, object> properties) 
        { 
            id = MakeLocalDocumentId(id);
            var rev = new RevisionInternal(id, null, properties == null);
            if (properties != null) {
                rev.SetProperties(properties);
            }

            bool ok = PutLocalRevision(rev, null, false) != null;
            return ok;
        }

        /// <summary>
        /// Deletes the local <see cref="Couchbase.Lite.Document" /> with the given id.
        /// </summary>
        /// <returns><c>true</c>, if local <see cref="Couchbase.Lite.Document" /> was deleted, <c>false</c> otherwise.</returns>
        /// <param name="id">Identifier.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if there is an issue occurs while deleting the local document.</exception>
        public bool DeleteLocalDocument(string id) 
        {
            return PutLocalDocument(id, null);
        } 

        /// <summary>
        /// Creates a <see cref="Couchbase.Lite.Query" /> that matches all <see cref="Couchbase.Lite.Document" />s in the <see cref="Couchbase.Lite.Database" />.
        /// </summary>
        /// <returns>Returns a <see cref="Couchbase.Lite.Query" /> that matches all <see cref="Couchbase.Lite.Document" />s in the <see cref="Couchbase.Lite.Database" />s.</returns>
        public Query CreateAllDocumentsQuery() 
        {
            return new Query(this, (View)null);
        }

        /// <summary>
        /// Gets or creates the <see cref="Couchbase.Lite.View" /> with the given name.  
        /// New <see cref="Couchbase.Lite.View" />s won't be added to the <see cref="Couchbase.Lite.Database" /> 
        /// until a map function is assigned.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.View" /> with the given name.</returns>
        /// <param name="name">The name of the <see cref="Couchbase.Lite.View" /> to get or create.</param>
        public View GetView(String name) 
        {
            View view = null;

            if (_views != null) {
                view = _views.Get(name);
            }

            if (view != null) {
                return view;
            }

            return RegisterView(View.MakeView(this, name, true));
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View" /> with the given name, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.View" /> with the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the View to get.</param>
        public View GetExistingView(String name) 
        {
            View view = null;
            if (_views != null) {
                _views.TryGetValue(name, out view);
            }

            if (view != null) {
                return view;
            }
            view = View.MakeView(this, name, false);

            return RegisterView(view);
        }

        /// <summary>
        /// Gets the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.
        /// </summary>
        /// <returns>the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the validation delegate to get.</param>
        public ValidateDelegate GetValidation(String name) 
        {
            ValidateDelegate retVal = null;
            if (!Shared.TryGetValue<ValidateDelegate>("validation", name, Name, out retVal)) {
                return null;
            }

            return retVal;
        }

        /// <summary>
        /// Sets the validation delegate for the given name. If delegate is null, 
        /// the validation with the given name is deleted. Before any change 
        /// to the <see cref="Couchbase.Lite.Database"/> is committed, including incoming changes from a pull 
        /// <see cref="Couchbase.Lite.Replication"/>, all of its validation delegates are called and given 
        /// a chance to reject it.
        /// </summary>
        /// <param name="name">The name of the validation delegate to set.</param>
        /// <param name="validationDelegate">The validation delegate to set.</param>
        public void SetValidation(String name, ValidateDelegate validationDelegate)
        {
            Shared.SetValue("validation", name, Name, validationDelegate);
        }

        /// <summary>
        /// Returns the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the validation delegate to get.</param>
        /// <param name="status">The result of the operation</param>
        public FilterDelegate GetFilter(String name, Status status = null) 
        { 
            FilterDelegate result = null;
            if (!Shared.TryGetValue("filter", name, Name, out result)) {
                result = null;
            }

            if (result == null) {
                var filterCompiler = FilterCompiler;
                if (filterCompiler == null) {
                    return null;
                }

                string language = null;
                var sourceCode = GetDesignDocFunction(name, "filters", out language) as string;

                if (sourceCode == null) {
                    if (status != null) {
                        status.Code = StatusCode.NotFound;
                    }
                    return null;
                }

                var filter = filterCompiler.CompileFilter(sourceCode, language);
                if (filter == null) {
                    if (status != null) {
                        status.Code = StatusCode.CallbackError;
                    }
                    Log.W(TAG, string.Format("Filter {0} failed to compile", name));
                    return null;
                }

                SetFilter(name, filter);
                return filter;
            }

            return result;
        }

        /// <summary>
        /// Sets the <see cref="ValidateDelegate" /> for the given name. If delegate is null, the filter 
        /// with the given name is deleted. Before a <see cref="Couchbase.Lite.Revision" /> is replicated via a 
        /// push <see cref="Couchbase.Lite.Replication" />, its filter delegate is called and 
        /// given a chance to exclude it from the <see cref="Couchbase.Lite.Replication" />.
        /// </summary>
        /// <param name="name">The name of the filter delegate to set.</param>
        /// <param name="filterDelegate">The filter delegate to set.</param>
        public void SetFilter(String name, FilterDelegate filterDelegate) 
        { 
            Shared.SetValue("filter", name, Name, filterDelegate);
        }

        /// <summary>
        /// Runs the <see cref="Couchbase.Lite.RunAsyncDelegate"/> asynchronously.
        /// </summary>
        /// <returns>The async task.</returns>
        /// <param name="runAsyncDelegate">The delegate to run asynchronously.</param>
        public Task RunAsync(RunAsyncDelegate runAsyncDelegate) 
        {
            return Manager.RunAsync(runAsyncDelegate, this);
        }

        /// <summary>
        /// Runs the delegate within a transaction. If the delegate returns false, 
        /// the transaction is rolled back.
        /// </summary>
        /// <returns>True if the transaction was committed, otherwise false.</returns>
        /// <param name="transactionDelegate">The delegate to run within a transaction.</param>
        public bool RunInTransaction(RunInTransactionDelegate transactionDelegate)
        {
            return RunInTransaction(() => transactionDelegate() ? 
                new Status(StatusCode.Ok) : new Status(StatusCode.Reserved)).Code == StatusCode.Ok;
        }

        public IDictionary<string, object> PurgeRevisions(IDictionary<string, IList<string>> docsToRev)
        {
            // <http://wiki.apache.org/couchdb/Purge_Documents>
            IDictionary<string, object> result = new Dictionary<string, object>();
            if (docsToRev.Count == 0) {
                return result;
            }

            RunInTransaction(() =>
            {
                foreach(var docId in docsToRev.Keys) {
                    var docNumericId = GetDocNumericID(docId);
                    if(docNumericId == 0) {
                        // no such document; skip it
                        continue;
                    }

                    IEnumerable<string> revsPurged = null;
                    var revIDs = docsToRev[docId];
                    if(revIDs == null) {
                        return new Status(StatusCode.BadParam);
                    } else if(revIDs.Count == 0) {
                        revsPurged = new List<string>();
                    } else if(revIDs.Contains("*")) {
                        // Delete all revisions if magic "*" revision ID is given:
                        try {
                            StorageEngine.Delete("revs", "doc_id=?", docNumericId.ToString());
                        } catch(Exception) {
                            return new Status(StatusCode.DbError);
                        }

                        revsPurged = new List<string> { "*" };
                    } else {
                        // Iterate over all the revisions of the doc, in reverse sequence order.
                        // Keep track of all the sequences to delete, i.e. the given revs and ancestors,
                        // but not any non-given leaf revs or their ancestors.
                        const string sql = "SELECT revid, sequence, parent FROM revs WHERE doc_id=? ORDER BY sequence DESC";
                        HashSet<long> seqsToPurge = new HashSet<long>();
                        HashSet<long> seqsToKeep = new HashSet<long>();
                        HashSet<string> revsToPurge = new HashSet<string>();
                        TryQuery(c => 
                        {
                            string revId = c.GetString(0);
                            long sequence = c.GetLong(1);
                            long parent = c.GetLong(2);
                            if(seqsToPurge.Contains(sequence) || revIDs.Contains(revId) && !seqsToKeep.Contains(sequence)) {
                                // Purge it and maybe its parent:
                                seqsToPurge.Add(sequence);
                                revsToPurge.Add(revId);
                                if(parent > 0) {
                                    seqsToPurge.Add(parent);
                                }
                            } else {
                                // Keep it and its parent:
                                seqsToPurge.Remove(sequence);
                                revsToPurge.Remove(revId);
                                seqsToKeep.Add(parent);
                            }
                            return true;
                        }, true, sql, docNumericId);

                        seqsToPurge.ExceptWith(seqsToKeep);
                        Log.D(TAG, "Purging doc '{0}' revs ({1}); asked for ({2})", docId, String.Join(", ", revsToPurge.ToStringArray()), String.Join(", ", revIDs.ToStringArray()));
                        if(seqsToPurge.Any()) {
                            // Now delete the sequences to be purged.
                            var deleteSql = String.Format("sequence in ({0})", String.Join(", ", seqsToPurge.ToStringArray()));
                            int count = 0;
                            try {
                                count = StorageEngine.Delete("revs", deleteSql);
                            } catch(Exception) {
                                return new Status(StatusCode.DbError);
                            }

                            if(count != seqsToPurge.Count) {
                                Log.W(TAG, "Only {0} revisions deleted of {1}", count, String.Join(", ", seqsToPurge.ToStringArray()));
                            }
                        }

                        revsPurged = revsToPurge;
                    }

                    result["docID"] = revIDs.Where(x => revsPurged.Contains(x));
                }

                return new Status(StatusCode.Ok);
            });

            return result;
        }
        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Replication"/> that will push to the target <see cref="Couchbase.Lite.Database"/> at the given url.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Replication"/> that will push to the target <see cref="Couchbase.Lite.Database"/> at the given url.</returns>
        /// <param name="url">The url of the target Database.</param>
        public Replication CreatePushReplication(Uri url)
        {
            var scheduler = new SingleTaskThreadpoolScheduler();
            return new Pusher(this, url, false, new TaskFactory(scheduler));
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Replication"/> that will pull from the source <see cref="Couchbase.Lite.Database"/> at the given url.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Replication"/> that will pull from the source Database at the given url.</returns>
        /// <param name="url">The url of the source Database.</param>
        public Replication CreatePullReplication(Uri url)
        {
            var scheduler = new SingleTaskThreadpoolScheduler();
            return new Puller(this, url, false, new TaskFactory(scheduler));
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.Database"/>.</returns>
        public override string ToString()
        {
            return GetType().FullName + "[" + Path + "]";
        }

        /// <summary>
        /// Event handler delegate that will be called whenever a <see cref="Couchbase.Lite.Document"/> within the <see cref="Couchbase.Lite.Database"/> changes.
        /// </summary>
        public event EventHandler<DatabaseChangeEventArgs> Changed {
            add { _changed = (EventHandler<DatabaseChangeEventArgs>)Delegate.Combine(_changed, value); }
            remove { _changed = (EventHandler<DatabaseChangeEventArgs>)Delegate.Remove(_changed, value); }
        }
        private EventHandler<DatabaseChangeEventArgs> _changed;

        #endregion

        #region Internal Methods

        internal Status RunInTransaction(Func<Status> block)
        {
            Status status = new Status();
            int retries = 0;
            do {
                if(!BeginTransaction()) {
                    return new Status(StatusCode.DbError);
                }

                try {
                    status = block();
                } catch(Exception e) {
                    Log.E(TAG, "Exception in RunInTransaction", e);
                    status.Code = StatusCode.Exception;
                } finally {
                    EndTransaction(status.IsSuccessful);
                }

                if(status.Code == StatusCode.DbBusy) {
                    // retry if locked out
                    if(_transactionCount > 1) {
                        break;
                    }

                    if(++retries > TRANSACTION_MAX_RETRIES) {
                        Log.W(TAG, "Db busy, too many retries, giving up");
                        break;
                    }

                    Log.D(TAG, "Db busy, retrying transaction ({0})", retries);
                    Thread.Sleep(TRANSACTION_MAX_RETRY_DELAY);
                }
            } while(status.Code == StatusCode.DbBusy);

            return status;
        }

        internal string GetWinner(long docNumericId, ValueTypePtr<bool> outDeleted, ValueTypePtr<bool> outConflict)
        {
            Debug.Assert(docNumericId > 0);
            string revId = null;
            outDeleted.Value = false;
            outConflict.Value = false;
            TryQuery(c =>
            {
                revId = c.GetString(0);
                outDeleted.Value = c.GetInt(1) != 0;
                // The document is in conflict if there are two+ result rows that are not deletions.
                outConflict.Value = !outDeleted && c.MoveToNext() && c.GetInt(1) == 0;
                return false;
            }, true, "SELECT revid, deleted FROM revs WHERE doc_id=? and current=1 ORDER BY deleted asc, revid desc LIMIT ?",
                docNumericId, (!outConflict.IsNull ? 2 : 1));

            return revId;
        }

        internal long GetDocNumericID(string docId)
        {
            long docNumericId = 0L;
            var success = TryQuery(c =>
            {
                docNumericId = c.GetLong(0);
                return false;
            }, true, "SELECT doc_id FROM docs WHERE docid=?", docId);

            if (success.Code == StatusCode.DbError) {
                return -1L;
            }

            if (success.Code == StatusCode.NotFound) {
                return 0L;
            }

            return docNumericId;
        }

        internal int PruneRevsToMaxDepth(int maxDepth)
        {
            int outPruned = 0;
            IDictionary<long, int> toPrune = new Dictionary<long, int>();

            if (maxDepth == 0) {
                maxDepth = MaxRevTreeDepth;
            }

            // First find which docs need pruning, and by how much:
            Cursor cursor = null;
            const string sql = "SELECT doc_id, MIN(revid), MAX(revid) FROM revs GROUP BY doc_id";

            long docNumericID = -1;
            var minGen = 0;
            var maxGen = 0;

            try {
                cursor = StorageEngine.RawQuery(sql);

                while (cursor.MoveToNext()) {
                    docNumericID = cursor.GetLong(0);

                    var minGenRevId = cursor.GetString(1);
                    var maxGenRevId = cursor.GetString(2);

                    minGen = RevisionInternal.GenerationFromRevID(minGenRevId);
                    maxGen = RevisionInternal.GenerationFromRevID(maxGenRevId);

                    if ((maxGen - minGen + 1) > maxDepth) {
                        toPrune.Put(docNumericID, (maxGen - minGen));
                    }
                }

                if (toPrune.Count == 0) {
                    return 0;
                }

                RunInTransaction(() =>
                {
                    foreach (long id in toPrune.Keys) {
                        var minIDToKeep = String.Format("{0}-", (toPrune.Get(id) + 1));
                        var deleteArgs = new string[] { System.Convert.ToString(docNumericID), minIDToKeep };
                        var rowsDeleted = StorageEngine.Delete("revs", "doc_id=? AND revid < ? AND current=0", deleteArgs);
                        outPruned += rowsDeleted;
                    }

                    return true;
                });
            } catch (Exception e) {
                throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
            } finally {
                if (cursor != null) {
                    cursor.Close();
                }
            }

            return outPruned;
        }

        internal string FindCommonAncestor(RevisionInternal rev, IEnumerable<string> revIds)
        {
            if (revIds == null || !revIds.Any()) {
                return null;
            }

            var docNumericId = GetDocNumericID(rev.GetDocId());
            if (docNumericId <= 0) {
                return null;
            }

            var sql = String.Format("SELECT revid FROM revs " +
                "WHERE doc_id=? and revid in ({0}) and revid <= ? " +
                "ORDER BY revid DESC LIMIT 1", Database.JoinQuoted(revIds));

            return QueryOrDefault(c => c.GetString(0), false, null, sql, docNumericId, rev.GetRevId());
        }

        internal Status ForceInsert(RevisionInternal inRev, IList<string> revHistory, StoreValidation validationBlock, Uri source)
        {
            var rev = inRev.CopyWithDocID(inRev.GetDocId(), inRev.GetRevId());
            rev.SetSequence(0L);
            string docId = rev.GetDocId();

            string winningRevId = null;
            bool inConflict = false;
            var status = RunInTransaction(() =>
            {
                // First look up the document's row-id and all locally-known revisions of it:
                Dictionary<string, RevisionInternal> localRevs = null;
                string oldWinningRevId = null;
                bool oldWinnerWasDeletion = false;
                bool isNewDoc = revHistory.Count == 1;
                var docNumericId = GetOrInsertDocNumericID(docId, ref isNewDoc);
                if(docNumericId <= 0) {
                    return new Status(StatusCode.DbError);
                }

                if(!isNewDoc) {
                    var innerStatus = new Status();
                    RevisionList localRevsList = GetAllDocumentRevisions(docId, docNumericId, false, innerStatus);
                    if(localRevsList != null) {
                        localRevs = new Dictionary<string, RevisionInternal>(localRevsList.Count);
                        foreach(var localRev in localRevsList) {
                            localRevs[localRev.GetRevId()] = localRev;
                        }

                        // Look up which rev is the winner, before this insertion
                        try {
                            oldWinningRevId = GetWinner(docNumericId, oldWinnerWasDeletion, inConflict);
                        } catch(CouchbaseLiteException e) {
                            return e.CBLStatus;
                        }
                    } else if(innerStatus.Code != StatusCode.NotFound) {
                        // Don't stop on a not found, because it is not critical.  This can happen
                        // when two pullers are pulling the same data at the same time.  One will
                        // insert JUST the document (not the revisions yet), and then yield to the
                        // other which will see the document and assume revisions are there which aren't.
                        // In that case, we'd like to continue and insert the missing revisions instead of
                        // erroring out
                        return innerStatus;
                    }
                }

                if(validationBlock != null) {
                    RevisionInternal oldRev = null;
                    for(int i = 1; i < revHistory.Count; i++) {
                        oldRev = localRevs == null ? null : localRevs.Get(revHistory[i]);
                        if(oldRev != null) {
                            break;
                        }
                    }

                    string parentRevId = (revHistory.Count > 1) ? revHistory[1] : null;
                    var validationStatus = validationBlock(rev, oldRev, parentRevId);
                    if(validationStatus.IsError) {
                        return validationStatus;
                    }
                }

                // Walk through the remote history in chronological order, matching each revision ID to
                // a local revision. When the list diverges, start creating blank local revisions to fill
                // in the local history:
                long sequence = 0L;
                long localParentSequence = 0L;
                for(int i = revHistory.Count - 1; i >= 0; --i) {
                    var revId = revHistory[i];
                    var localRev = localRevs == null ? null : localRevs.Get(revId);
                    if(localRev != null) {
                        // This revision is known locally. Remember its sequence as the parent of the next one:
                        sequence = localRev.GetSequence();
                        Debug.Assert(sequence > 0);
                        localParentSequence = sequence;
                    } else {
                        // This revision isn't known, so add it:
                        RevisionInternal newRev = null;
                        IEnumerable<byte> json = null;
                        string docType = null;
                        bool current = false;
                        if(i == 0) {
                            // Hey, this is the leaf revision we're inserting:
                            newRev = rev;
                            json = EncodeDocumentJSON(rev);
                            if(json == null) {
                                return new Status(StatusCode.BadJson);
                            }

                            docType = rev.GetPropertyForKey("type") as string;
                            current = true;
                        } else {
                            // It's an intermediate parent, so insert a stub:
                            newRev = new RevisionInternal(docId, revId, false);
                        }

                        // Insert it:
                        sequence = InsertRevision(newRev, docNumericId, sequence, current, newRev.GetAttachments() != null, json, docType);
                        if(sequence == 0) {
                            if(StorageEngine.LastErrorCode != raw.SQLITE_CONSTRAINT) {
                                return new Status(StatusCode.DbError);
                            } else {
                                sequence = GetSequenceOfDocument(docNumericId, newRev.GetRevId(), false);
                            }
                        }
                    }
                }

                if(localParentSequence == sequence) {
                    // No-op: No new revisions were inserted.
                    return new Status(StatusCode.Ok);
                }

                // Mark the latest local rev as no longer current:
                if(localParentSequence > 0) {
                    var args = new ContentValues();
                    args["current"] = 0;
                    args["doc_type"] = null;
                    int changes;
                    try {
                        changes = StorageEngine.Update("revs", args, "sequence=?", localParentSequence.ToString());
                    } catch(Exception) {
                        return new Status(StatusCode.DbError);
                    }

                    if(changes == 0) {
                        // local parent wasn't a leaf, ergo we just created a branch
                        inConflict = true;
                    }
                }

                // Figure out what the new winning rev ID is:
                winningRevId = GetWinner(docNumericId, oldWinningRevId, oldWinnerWasDeletion, rev);
                return new Status(StatusCode.Created);
            });

            if (status.IsSuccessful) {
                DatabaseStorageChanged(new DocumentChange(rev, winningRevId, inConflict, source));
            }

            return status;
        }
        internal RevisionInternal GetParentRevision(RevisionInternal rev)
        {
            // First get the parent's sequence:
            var seq = rev.GetSequence();
            if (seq != 0) {
                seq = QueryOrDefault<long>(c => c.GetLong(0), false, 0L, "SELECT parent FROM revs WHERE sequence=?", seq);
            } else {
                var docNumericId = GetDocNumericID(rev.GetDocId());
                if (docNumericId == 0L) {
                    return null;
                }

                seq = QueryOrDefault<long>(c => c.GetLong(0), false, 0L, "SELECT parent FROM revs WHERE doc_id=? and revid=?", docNumericId, rev.GetRevId());
            }

            if (seq == 0) {
                return null;
            }

            // Now get its revID and deletion status:
            RevisionInternal result = null;
            TryQuery(c =>
            {
                result = new RevisionInternal(rev.GetDocId(), c.GetString(0), c.GetInt(1) != 0);
                result.SetSequence(seq);
                return false;
            }, false, "SELECT revid, deleted FROM revs WHERE sequence=?", seq);

            return result;
        }

        internal int FindMissingRevisions(RevisionList revs)
        {
            if (!revs.Any()) {
                return 0;
            }

            var sql = String.Format("SELECT docid, revid FROM revs, docs " +
                "WHERE revid in ({0}) AND docid IN ({1}) " +
                "AND revs.doc_id == docs.doc_id", Database.JoinQuoted(revs.GetAllRevIds()), Database.JoinQuoted(revs.GetAllDocIds()));

            int count = 0;
            var status = TryQuery(c =>
            {
                var rev = revs.RevWithDocIdAndRevId(c.GetString(0), c.GetString(1));
                if(rev != null) {
                    count++;
                    revs.Remove(rev);
                }

                return true;
            }, false, sql);

            return status.IsSuccessful ? count : 0;
        }

        internal IEnumerable<string> GetPossibleAncestors(RevisionInternal rev, int limit, bool onlyAttachments)
        {
            int generation = rev.GetGeneration();
            if (generation <= 1L) {
                return new List<string>();
            }

            long docNumericId = GetDocNumericID(rev.GetDocId());
            if (docNumericId <= 0L) {
                return new List<string>();
            }

            int sqlLimit = limit > 0 ? limit : -1;
            const string sql = "SELECT revid, sequence FROM revs WHERE doc_id=? and revid < ?" +
                " and deleted=0 and json not null" +
                " ORDER BY sequence DESC LIMIT ?";

            var revIDs = new List<string>();
            var status = TryQuery(c => 
            {
                if(onlyAttachments && !SequenceHasAttachments(c.GetLong(1))) {
                    return true;
                }

                revIDs.Add(c.GetString(0));
                return true;
            }, false, sql, docNumericId, String.Format("{0}-", generation), sqlLimit);

            return status.IsError ? null : revIDs;
        }

        internal bool RunStatements(string sqlStatements)
        {
            foreach (var quotedStatement in sqlStatements.Split(';')) {
                var statement = quotedStatement.Replace('|', ';');

                if (_SqliteVersion < 3008000) {
                    // No partial index support before SQLite 3.8
                    if (statement.Contains("CREATE INDEX")) {
                        var where = statement.IndexOf("WHERE");
                        if (where >= 0) {
                            statement = statement.Substring(0, where);
                        }
                    }
                }

                if (!StringEx.IsNullOrWhiteSpace(statement)) {
                    try {
                        StorageEngine.ExecSQL(statement);
                    } catch(Exception e) {
                        Log.E(TAG, String.Format("Error running statement {0}", statement), e);
                        return false;
                    }

                }
            }

            return true;
        }

        internal RevisionInternal GetRevision(string docId, string revId, bool deleted, long sequence, IEnumerable<byte> json)
        {
            var rev = new RevisionInternal(docId, revId, deleted);
            rev.SetSequence(sequence);
            if (json != null) {
                rev.SetBody(new Body(json));
            }

            return rev;
        }

        internal RevisionInternal GetDocument(string docId, long sequence, Status outStatus = null)
        {
            RevisionInternal result = null;
            var status = TryQuery(c =>
            {
                string revId = c.GetString(0);
                bool deleted = c.GetInt(1) != 0;
                result = new RevisionInternal(docId, revId, deleted);
                result.SetSequence(sequence);
                result.SetBody(new Body(c.GetBlob(2)));

                return false;
            }, false, "SELECT revid, deleted, json FROM revs WHERE sequence=?", sequence);

            if (outStatus != null) {
                outStatus.Code = status.Code;
            }

            return result;
        }

        internal IDictionary<string, object> GetDocumentProperties(IEnumerable<byte> json, string docId, string revId, bool deleted, long sequence)
        {
            var realizedJson = json.ToArray();
            IDictionary<string, object> docProperties;
            if (realizedJson.Length == 0 || (realizedJson.Length == 2 && Encoding.UTF8.GetString(realizedJson) == "{}")) {
                docProperties = new Dictionary<string, object>();
            } else {
                try {
                    docProperties = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(realizedJson);
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Unparseable JSON for doc={0}, rev={1}: {2}", docId, revId, Encoding.UTF8.GetString(realizedJson));
                    docProperties = new Dictionary<string, object>();
                }
            }

            docProperties["_id"] = docId;
            docProperties["_rev"] = revId;
            if (deleted) {
                docProperties["_deleted"] = true;
            }

            return docProperties;
        }

        internal void OptimizeSQLIndexes()
        {
            long currentSequence = LastSequenceNumber;
            if (currentSequence > 0) {
                long lastOptimized = long.Parse(GetInfo("last_optimized") ?? "0");
                if (lastOptimized <= currentSequence / 10) {
                    RunInTransaction(() =>
                    {
                        Log.D(TAG, "Optimizing SQL indexes (curSeq={0}, last run at {1})", currentSequence, lastOptimized);
                        StorageEngine.ExecSQL("ANALYZE");
                        StorageEngine.ExecSQL("ANALYZE sqlite_master");
                        SetInfo("last_optimized", currentSequence.ToString());
                        return true;
                    });
                }
            }
        }

        internal Status TryQuery(Func<Cursor, bool> action, bool readUncommit, string sqlQuery, params object[] args)
        {
            Cursor c = null;
            try {
                if (readUncommit) {
                    c = StorageEngine.IntransactionRawQuery(sqlQuery, args);
                } else {
                    c = StorageEngine.RawQuery(sqlQuery, args);
                }

                var retVal = new Status(StatusCode.NotFound);
                while(c.MoveToNext()) {
                    retVal.Code = StatusCode.Ok;
                    if(!action(c)) {
                        break;
                    }
                }

                return retVal;
            } catch(SQLException e) {
                Log.E(TAG, "Error executing SQL query", e);
            } finally {
                if (c != null) {
                    c.Dispose();
                }
            }

            return new Status(StatusCode.DbError);
        }

        internal T QueryOrDefault<T>(Func<Cursor, T> action, bool readUncommit, T defaultVal, string sqlQuery, params object[] args)
        {
            T retVal = defaultVal;
            var success = TryQuery(c => {
                retVal = action(c);
                return false;
            }, readUncommit, sqlQuery, args);
            if(success.IsError) {
                return defaultVal;
            }

            return retVal;
        }

        internal RevisionList GetAllDocumentRevisions(string docId, bool onlyCurrent)
        {
            var docNumericId = GetDocNumericID(docId);
            if (docNumericId < 0) {
                return null;
            }

            if (docNumericId == 0) {
                return new RevisionList(); // no such document
            }

            return GetAllDocumentRevisions(docId, docNumericId, onlyCurrent);
        }

        internal RevisionInternal PutRevision(string inDocId, string inPrevRevId, IDictionary<string, object> properties,
            bool deleting, bool allowConflict, StoreValidation validationBlock, Status outStatus = null)
        {
            if (outStatus == null) {
                outStatus = new Status();
            }

            IEnumerable<byte> json = null;
            if (properties != null) {
                try {
                    json = Manager.GetObjectMapper().WriteValueAsBytes(StripDocumentJSON(properties), true);
                } catch (Exception e) {
                    throw new CouchbaseLiteException(e, StatusCode.BadJson);
                }
            } else {
                json = Encoding.UTF8.GetBytes("{}");
            }

            RevisionInternal newRev = null;
            string winningRevID = null;
            bool inConflict = false;

            var status = RunInOuterTransaction(() =>
            {
                // Remember, this block may be called multiple times if I have to retry the transaction.
                newRev = null;
                winningRevID = null;
                inConflict = false;
                string prevRevId = inPrevRevId;
                string docId = inDocId;

                //// PART I: In which are performed lookups and validations prior to the insert...

                // Get the doc's numeric ID (doc_id) and its current winning revision:
                bool isNewDoc = prevRevId == null;
                long docNumericId;
                if(docId != null) {
                    docNumericId = GetOrInsertDocNumericID(docId, ref isNewDoc);
                    if(docNumericId <= 0L) {
                        throw new CouchbaseLiteException(StatusCode.DbError);
                    }
                } else {
                    docNumericId = 0L;
                    isNewDoc = true;
                }

                ValueTypePtr<bool> oldWinnerWasDeletion = false;
                ValueTypePtr<bool> wasConflicted = false;
                string oldWinningRevId = null;
                if(!isNewDoc) {
                    try {
                        // Look up which rev is the winner, before this insertion
                        oldWinningRevId = GetWinner(docNumericId, oldWinnerWasDeletion, wasConflicted);
                    } catch(CouchbaseLiteException e) {
                        return e.CBLStatus;
                    }
                }

                long parentSequence = 0L;
                if(prevRevId != null) {
                    // Replacing: make sure given prevRevID is current & find its sequence number:
                    if(isNewDoc) {
                        return new Status(StatusCode.NotFound);
                    }

                    parentSequence = GetSequenceOfDocument(docNumericId, prevRevId, !allowConflict);
                    if(parentSequence == 0L) {
                        // Not found: NotFound or a Conflict, depending on whether there is any current revision
                        if(!allowConflict && DocumentExists(docId, null)) {
                            return new Status(StatusCode.Conflict);
                        }

                        return new Status(StatusCode.NotFound);
                    }
                } else {
                    // Inserting first revision.
                    if(deleting && docId != null) {
                        // Didn't specify a revision to delete: NotFound or a Conflict, depending
                        return DocumentExists(docId, null) ? new Status(StatusCode.Conflict) : new Status(StatusCode.NotFound);
                    }

                    if(docId != null) {
                        // Inserting first revision, with docID given (PUT):
                        // Check whether current winning revision is deleted:
                        if(oldWinnerWasDeletion) {
                            prevRevId = oldWinningRevId;
                            parentSequence = GetSequenceOfDocument(docNumericId, prevRevId, false);
                        } else if(oldWinningRevId != null) {
                            // The current winning revision is not deleted, so this is a conflict
                            return new Status(StatusCode.Conflict);
                        }
                    } else {
                        // Inserting first revision, with no docID given (POST): generate a unique docID:
                        docId = Misc.CreateGUID();
                        docNumericId = GetOrInsertDocNumericID(docId, ref isNewDoc);
                        if(docNumericId <= 0L) {
                            return new Status(StatusCode.DbError);
                        }
                    }
                }

                // There may be a conflict if (a) the document was already in conflict, or
                // (b) a conflict is created by adding a non-deletion child of a non-winning rev.
                inConflict = wasConflicted || (!deleting && prevRevId != oldWinningRevId);

                //// PART II: In which we prepare for insertion...

                // Bump the revID and update the JSON:
                string newRevId = GenerateRevID(json, deleting, prevRevId);
                if(newRevId == null) {
                    // invalid previous revID (no numeric prefix)
                    return new Status(StatusCode.BadId);
                }

                Debug.Assert(docId != null);
                newRev = new RevisionInternal(docId, newRevId, deleting);
                if(properties != null) {
                    newRev.SetProperties(properties);
                }

                // Validate:
                if(validationBlock != null) {
                    // Fetch the previous revision and validate the new one against it:
                    RevisionInternal prevRev = null;
                    if(prevRevId != null) {
                        prevRev = new RevisionInternal(docId, prevRevId, false);
                    }

                    var validationStatus = validationBlock(newRev, prevRev, prevRevId);
                    if(validationStatus.IsError) {
                        return validationStatus;
                    }
                }

                // Don't store a SQL null in the 'json' column -- I reserve it to mean that the revision data
                // is missing due to compaction or replication.
                // Instead, store an empty zero-length blob.
                if(json == null) {
                    json = new byte[0];
                }

                //// PART III: In which the actual insertion finally takes place:

                bool hasAttachments = false;
                string docType = null;
                if(properties != null) {
                    hasAttachments = properties.Get("_attachments") != null;
                    docType = properties.GetCast<string>("type");
                }

                var sequence = InsertRevision(newRev, docNumericId, parentSequence, true, hasAttachments, json, docType);
                if(sequence == 0L) {
                    if(StorageEngine.LastErrorCode != raw.SQLITE_CONSTRAINT) {
                        return LastDbError;
                    }

                    Log.I(TAG, "Duplicate rev insertion {0} / {1}", docId, newRevId);
                    newRev.SetBody(null);
                }

                // Make replaced rev non-current:
                if(parentSequence > 0) {
                    var args = new ContentValues();
                    args["current"] = 0;
                    args["doc_type"] = null;
                    try {
                        StorageEngine.Update("revs", args, "sequence=?", parentSequence.ToString());
                    } catch(Exception) {
                        StorageEngine.Delete("revs", "sequence=?", sequence.ToString());
                        return new Status(StatusCode.DbError);
                    }
                }

                if(sequence == 0L) {
                    // duplicate rev; see above
                    return new Status(StatusCode.Ok);
                }

                // Figure out what the new winning rev ID is:
                winningRevID = GetWinner(docNumericId, oldWinningRevId, oldWinnerWasDeletion, newRev);

                // Success!
                return deleting ? new Status(StatusCode.Ok) : new Status(StatusCode.Created);
            });

            outStatus.Code = status.Code;
            if (outStatus.IsError) {
                return null;
            }

            //// EPILOGUE: A change notification is sent...
            DatabaseStorageChanged(new DocumentChange(newRev, winningRevID, inConflict, null));

            return newRev;
        }
        internal RevisionInternal GetLocalDocument(string docId, string revId)
        {
            RevisionInternal result = null;
            TryQuery(c =>
            {
                string gotRevId = c.GetString(0);
                if(revId != null && revId != gotRevId) {
                    return false;
                }

                var json = c.GetBlob(1);
                IDictionary<string, object> properties;
                if(json == null || !json.Any() || (json.Length == 2 && json[0] == (byte)'{' && json[1] == '}')) {
                    properties = new Dictionary<string, object>();
                } else {
                    try {
                        properties = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(json);
                    } catch(Exception) {
                        return false;
                    }
                }

                properties["_id"] = docId;
                properties["_rev"] = gotRevId;
                result = new RevisionInternal(docId, gotRevId, false);
                result.SetProperties(properties);

                return false;
            }, false, "SELECT revid, json FROM localdocs WHERE docid=?", docId);

            return result;
        }

        internal object GetDesignDocFunction(string fnName, string key, out string language)
        {
            language = null;
            var path = fnName.Split('/');
            if (path.Length != 2) {
                return null;
            }

            var docId = string.Format("_design/{0}", path[0]);
            var rev = GetDocumentWithIDAndRev(docId, null, true);
            if (rev == null) {
                return null;
            }

            var outLanguage = (string)rev.GetPropertyForKey("language");
            if (outLanguage != null) {
                language = outLanguage;
            }
            else {
                language = "javascript";
            }

            var container = rev.GetPropertyForKey(key).AsDictionary<string, object>();
            if (container == null) {
                return null;
            }

            return container.Get(path[1]);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">When attempting to add an invalid revision</exception>
        internal void ForceInsert(RevisionInternal inRev, IList<string> revHistory, Uri source)
        {
            if (revHistory == null) {
                revHistory = new List<string>(0);
            }

            var rev = inRev.CopyWithDocID(inRev.GetDocId(), inRev.GetRevId());
            rev.SetSequence(0);
            string revID = rev.GetRevId();
            if (!IsValidDocumentId(rev.GetDocId()) || revID == null) {
                throw new CouchbaseLiteException(StatusCode.BadId);
            }

            if (revHistory.Count == 0) {
                revHistory.Add(revID);
            } else if (revID != revHistory[0]) {
                throw new CouchbaseLiteException(StatusCode.BadId);
            }

            if (inRev.GetAttachments() != null) {
                var updatedRev = inRev.CopyWithDocID(inRev.GetDocId(), inRev.GetRevId());
                string prevRevID = revHistory.Count >= 2 ? revHistory[1] : null;
                Status status = new Status();
                if (!ProcessAttachmentsForRevision(updatedRev, prevRevID, status)) {
                    throw new CouchbaseLiteException(status.Code);
                }

                inRev = updatedRev;
            }

            StoreValidation validationBlock = null;
            if (Shared != null && Shared.HasValues("validation", Name)) {
                validationBlock = ValidateRevision;
            }

            var insertStatus = ForceInsert(inRev, revHistory, validationBlock, source);
            if(insertStatus.IsError) {
                throw new CouchbaseLiteException(insertStatus.Code);
            }
        }

        internal bool AddReplication(Replication replication)
        {
            lock (_allReplicatorsLocker) {
                if (AllReplications.All(x => x.RemoteCheckpointDocID() != replication.RemoteCheckpointDocID())) {
                    AllReplicators.Add(replication);
                    return true;
                }

                return false;

            }
        }

        internal void ForgetReplication(Replication replication)
        {
            lock (_allReplicatorsLocker) { AllReplicators.Remove(replication); }
        }

        internal bool AddActiveReplication(Replication replication)
        {
            if (ActiveReplicators == null) {
                Log.W(TAG, "ActiveReplicators is null, so replication will not be added");
                return false;
            }

            if (ActiveReplicators.All(x => x.RemoteCheckpointDocID() != replication.RemoteCheckpointDocID())) {
                ActiveReplicators.Add(replication);
            } else {
                return false;
            }

            replication.Changed += (sender, e) => {
                if (e.Source != null && !e.Source.IsRunning && ActiveReplicators != null)
                {
                    ActiveReplicators.Remove(e.Source);
                }
            };

            return true;
        }

        internal bool Exists()
        {
            return new FilePath(Path).Exists();
        }

        internal static string MakeLocalDocumentId(string documentId)
        {
            return string.Format("_local/{0}", documentId);
        }

        internal bool SetLastSequence(string lastSequence, string checkpointId, bool push)
        {
            return SetInfo(CheckpointInfoKey(checkpointId), lastSequence).IsSuccessful;
        }

        internal string LastSequenceWithCheckpointId(string checkpointId)
        {
            return GetInfo(CheckpointInfoKey(checkpointId));
        }

        internal IEnumerable<QueryRow> QueryViewNamed(String viewName, QueryOptions options, long ifChangedSince, ValueTypePtr<long> outLastSequence, Status outStatus = null)
        {
            if (outStatus == null) {
                outStatus = new Status();
            }

            IEnumerable<QueryRow> iterator = null;
            Status status = null;
            long lastIndexedSequence = 0, lastChangedSequence = 0;
            do {
                if(viewName != null) {
                    var view = GetView(viewName);
                    if(view == null) {
                        outStatus.Code = StatusCode.NotFound;
                        break;
                    }

                    lastIndexedSequence = view.LastSequenceIndexed;
                    if(options.Stale == IndexUpdateMode.Before || lastIndexedSequence <= 0) {
                        status = view.UpdateIndex();
                        if(status.IsError) {
                            Log.W(TAG, "Failed to update index: {0}", status.Code);
                            break;
                        }

                        lastIndexedSequence = view.LastSequenceIndexed;
                    } else if(options.Stale == IndexUpdateMode.After && lastIndexedSequence <= LastSequenceNumber) {
                        RunAsync(d => view.UpdateIndex());
                    }

                    lastChangedSequence = view.LastSequenceChangedAt;
                    iterator = view.QueryWithOptions(options);
                } else { // null view means query _all_docs
                    iterator = GetAllDocs(options);
                    lastIndexedSequence = lastChangedSequence = LastSequenceNumber;
                }

                if(lastChangedSequence <= ifChangedSince) {
                    status = new Status(StatusCode.NotModified);
                }
            } while(false); // just to allow 'break' within the block

            outLastSequence.Value = lastIndexedSequence;
            if (status != null) {
                outStatus.Code = status.Code;
            }

            return iterator;
        }

        internal RevisionList ChangesSince(long lastSeq, ChangesOptions options, FilterDelegate filter, IDictionary<string, object> filterParams)
        {
            RevisionFilter revFilter = null;
            if (filter != null) {
                revFilter = (rev => RunFilter(filter, filterParams, rev));
            }

            return ChangesSince(lastSeq, options, revFilter);
        }

        internal bool RunFilter(FilterDelegate filter, IDictionary<string, object> filterParams, RevisionInternal rev)
        {
            if (filter == null) {
                return true;
            }

            var publicRev = new SavedRevision(this, rev);
            return filter(publicRev, filterParams);
        }

        internal IEnumerable<QueryRow> GetAllDocs(QueryOptions options)
        {
            // For regular all-docs, let storage do it all:
            if (options == null || options.AllDocsMode != AllDocsMode.BySequence) {
                return GetAllDocsRegular(options);
            }

            if (options.Descending) {
                throw new CouchbaseLiteException("Descending all docs not implemented", StatusCode.NotImplemented);
            }

            ChangesOptions changesOpts = new ChangesOptions();
            changesOpts.SetLimit(options.Limit);
            changesOpts.SetIncludeDocs(options.IncludeDocs);
            changesOpts.SetIncludeConflicts(true);
            changesOpts.SetSortBySequence(true);

            long startSeq = KeyToSequence(options.StartKey, 1);
            long endSeq = KeyToSequence(options.EndKey, long.MaxValue);
            if (!options.InclusiveStart) {
                ++startSeq;
            }

            if (!options.InclusiveEnd) {
                --endSeq;
            }

            long minSeq = startSeq, maxSeq = endSeq;
            if (minSeq > maxSeq) {
                return null; // empty result
            }

            RevisionList revs = ChangesSince(minSeq - 1, changesOpts, null);
            if (revs == null) {
                return null;
            }

            var result = new List<QueryRow>();
            var revEnum = options.Descending ? revs.Reverse<RevisionInternal>() : revs;
            foreach (var rev in revEnum) {
                long seq = rev.GetSequence();
                if (seq < minSeq || seq > maxSeq) {
                    break;
                }

                var value = new NonNullDictionary<string, object> {
                    { "rev", rev.GetRevId() },
                    { "deleted", rev.IsDeleted() ? (object)true : null }
                };
                result.Add(new QueryRow(rev.GetDocId(), seq, rev.GetDocId(), value, rev, null));
            }

            return result;
        }

        internal static string JoinQuotedObjects(IEnumerable<Object> objects)
        {
            var strings = new List<String>();
            foreach (var obj in objects)
            {
                strings.AddItem(obj != null ? obj.ToString() : null);
            }
            return JoinQuoted(strings);
        }

        internal static string JoinQuoted(IEnumerable<string> strings)
        {
            if (!strings.Any()) {
                return String.Empty;
            }

            var result = "'";
            var first = true;

            foreach (string str in strings)
            {
                if (first)
                    first = false;
                else
                    result = result + "','";

                result = result + Quote(str);
            }

            result = result + "'";

            return result;
        }

        internal static string Quote(string str)
        {
            return str.Replace("'", "''");
        }

        internal View RegisterView(View view)
        {
            if (view == null) {
                return null;
            }

            if (_views == null) {
                _views = new Dictionary<string, View>();
            }

            _views.Put(view.Name, view);
            return view;
        }

        internal View MakeAnonymousView()
        {
            for (var i = 0; true; ++i)
            {
                var name = String.Format("anon{0}", i);
                var existing = GetExistingView(name);
                if (existing == null)
                {
                    // this name has not been used yet, so let's use it
                    return GetView(name);
                }
            }
        }

        internal IList<View> GetAllViews()
        {
            var viewNames = new List<string>();
            TryQuery(c =>
            {
                viewNames.Add(c.GetString(0));
                return true;
            }, false, "SELECT name FROM views");
                
            var enumerator = from viewName in viewNames
                select GetExistingView(viewName);

            return enumerator.ToList();
        }

        internal void ForgetView(string viewName) {
            _views.Remove(viewName);
        }

        /// <summary>
        /// Creates a one-shot query with the given map function. This is equivalent to creating an
        /// anonymous View and then deleting it immediately after querying it. It may be useful during
        /// development, but in general this is inefficient if this map will be used more than once,
        /// because the entire view has to be regenerated from scratch every time.
        /// </summary>
        /// <returns>The query.</returns>
        /// <param name="map">Map.</param>
        internal Query SlowQuery(MapDelegate map) 
        {
            return new Query(this, map);
        }

        internal void RemoveDocumentFromCache(Document document)
        {
            DocumentCache.Remove(document.Id);
            UnsavedRevisionDocumentCache.Remove(document.Id);
        }

        internal string PrivateUUID ()
        {
            return GetInfo("privateUUID");
        }

        internal string PublicUUID()
        {
            return GetInfo("publicUUID");
        }

        internal bool ReplaceUUIDs(string privUUID = null, string pubUUID = null)
        {
            if (privUUID == null) {
                privUUID = Misc.CreateGUID();
            }

            if (pubUUID == null) {
                pubUUID = Misc.CreateGUID();
            }

            var status = SetInfo("publicUUID", pubUUID);
            if (status.IsSuccessful) {
                status = SetInfo("privateUUID", privUUID);
            }

            return status.IsSuccessful;
        }

        internal void RememberAttachmentWritersForDigests(IDictionary<String, BlobStoreWriter> blobsByDigest)
        {
            PendingAttachmentsByDigest.PutAll(blobsByDigest);
        }

        internal void RememberAttachmentWriter (BlobStoreWriter writer)
        {
            var digest = writer.SHA1DigestString();
            PendingAttachmentsByDigest[digest] = writer;
        }

        internal RevisionInternal GetDocument(string docId, string revId, bool withBody, Status status = null)
        {
            if (status == null) {
                status = new Status();
            }

            long docNumericId = GetDocNumericID(docId);
            if (docNumericId <= 0L) {
                status.Code = StatusCode.NotFound;
                return null;
            }

            RevisionInternal result = null;
            var sb = new StringBuilder("SELECT revid, deleted, sequence");
            if (withBody) {
                sb.Append(", json");
            }

            if (revId != null) {
                sb.Append(" FROM revs WHERE revs.doc_id=? AND revid=? AND json notnull LIMIT 1");
            } else {
                sb.Append(" FROM revs WHERE revs.doc_id=? and current=1 and deleted=0 ORDER BY revid DESC LIMIT 1");
            }

            var transactionStatus = TryQuery(c =>
            {
                if(revId == null) {
                    revId = c.GetString(0);
                }

                bool deleted = c.GetInt(1) != 0;
                result = new RevisionInternal(docId, revId, deleted);
                result.SetSequence(c.GetLong(2));
                if(withBody) {
                    result.SetJson(c.GetBlob(3));
                }

                status.Code = StatusCode.Ok;
                return false;
            }, true, sb.ToString(), docNumericId, revId);

            if (transactionStatus.IsError) {
                if (transactionStatus.Code == StatusCode.NotFound && revId == null) {
                    status.Code = StatusCode.Deleted;
                } else {
                    status.Code = transactionStatus.Code;
                }

                return null;
            }

            return result;
        }

        internal MultipartWriter MultipartWriterForRev(RevisionInternal rev, string contentType)
        {
            var writer = new MultipartWriter(contentType, null);
            writer.SetNextPartHeaders(new Dictionary<string, string> { { "Content-Type", "application/json" } });
            writer.AddData(rev.GetBody().AsJson());
            var attachments = rev.GetAttachments();
            if (attachments == null) {
                return writer;
            }

            foreach (var entry in attachments) {
                var attachment = entry.Value.AsDictionary<string, object>();
                if (attachment != null && attachment.GetCast<bool>("follows", false)) {
                    var disposition = String.Format("attachment; filename={0}", Quote(entry.Key));
                    writer.SetNextPartHeaders(new Dictionary<string, string> { { "Content-Disposition", disposition } });

                    Status status = new Status();
                    var attachObj = AttachmentForDict(attachment, entry.Key, status);
                    if (attachObj == null) {
                        return null;
                    }

                    var fileURL = attachObj.ContentUrl;
                    if (fileURL != null) {
                        writer.AddFileUrl(fileURL);
                    } else {
                        writer.AddStream(attachObj.ContentStream);
                    }
                }
            }

            return writer;
        }

        internal static IDictionary<string, object> MakeRevisionHistoryDict(IList<RevisionInternal> history)
        {
            if (history == null)
                return null;

            // Try to extract descending numeric prefixes:
            var suffixes = new List<string>();
            var start = -1;
            var lastRevNo = -1;

            foreach (var rev in history) {
                var parsed = RevisionInternal.ParseRevId(rev.GetRevId());
                int revNo = parsed.Item1;
                string suffix = parsed.Item2;
                if (revNo > 0 && suffix.Length > 0) {
                    if (start < 0) {
                        start = revNo;
                    }
                    else {
                        if (revNo != lastRevNo - 1) {
                            start = -1;
                            break;
                        }
                    }
                    lastRevNo = revNo;
                    suffixes.AddItem(suffix);
                }
                else {
                    start = -1;
                    break;
                }
            }

            var result = new Dictionary<String, Object>();
            if (start == -1) {
                // we failed to build sequence, just stuff all the revs in list
                suffixes = new List<string>();
                foreach (RevisionInternal rev_1 in history) {
                    suffixes.AddItem(rev_1.GetRevId());
                }
            }
            else {
                result["start"] = start;
            }

            result["ids"] = suffixes;
            return result;
        }

        /// <summary>Parses the _revisions dict from a document into an array of revision ID strings.</summary>
        internal static IList<string> ParseCouchDBRevisionHistory(IDictionary<String, Object> docProperties)
        {
            var revisions = docProperties.Get ("_revisions").AsDictionary<string,object> ();
            if (revisions == null) {
                return new List<string>();
            }

            var ids = revisions ["ids"].AsList<string> ();
            if (ids == null || ids.Count == 0) {
                return new List<string>();
            }

            var revIDs = new List<string>(ids);
            var start = Convert.ToInt64(revisions.Get("start"));
            for (var i = 0; i < revIDs.Count; i++) {
                var revID = revIDs[i];
                revIDs.Set(i, Sharpen.Extensions.ToString(start--) + "-" + revID);
            }

            return revIDs;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal RevisionInternal PutRevision(RevisionInternal rev, String prevRevId, Status resultStatus)
        {
            return PutRevision(rev, prevRevId, false, resultStatus);
        }

        internal RevisionInternal PutDocument(string docId, IDictionary<string, object> properties, string prevRevId, bool allowConflict, Status resultStatus)
        {
            bool deleting = properties == null || properties.GetCast<bool>("_deleted");
            Log.D(TAG, "PUT _id={0}, _rev={1}, _deleted={2}, allowConflict={3}", docId, prevRevId, deleting, allowConflict);
            if ((prevRevId != null && docId == null) || (deleting && docId == null)) {
                if (resultStatus != null) {
                    resultStatus.Code = StatusCode.BadId;
                    return null;
                }
            }

            if (properties != null && properties.Get("_attachments").AsDictionary<string, object>() != null) {
                var tmpRev = new RevisionInternal(docId, prevRevId, deleting);
                tmpRev.SetProperties(properties);
                if (!ProcessAttachmentsForRevision(tmpRev, prevRevId, resultStatus)) {
                    return null;
                }

                properties = tmpRev.GetProperties();
            }

            StoreValidation validationBlock = null;
            if (Shared.HasValues("validation", Name)) {
                validationBlock = ValidateRevision;
            }

            var putRev = PutRevision(docId, prevRevId, properties, deleting, allowConflict, validationBlock, resultStatus);
            if (putRev != null) {
                Log.D(TAG, "--> created {0}", putRev);
                if (!string.IsNullOrEmpty(docId)) {
                    UnsavedRevisionDocumentCache.Remove(docId);
                }
            }

            return putRev;
        }

        /// <summary>Stores a new (or initial) revision of a document.</summary>
        /// <remarks>
        /// Stores a new (or initial) revision of a document.
        /// This is what's invoked by a PUT or POST. As with those, the previous revision ID must be supplied when necessary and the call will fail if it doesn't match.
        /// </remarks>
        /// <param name="oldRev">The revision to add. If the docID is null, a new UUID will be assigned. Its revID must be null. It must have a JSON body.
        ///     </param>
        /// <param name="prevRevId">The ID of the revision to replace (same as the "?rev=" parameter to a PUT), or null if this is a new document.
        ///     </param>
        /// <param name="allowConflict">If false, an error status 409 will be returned if the insertion would create a conflict, i.e. if the previous revision already has a child.
        ///     </param>
        /// <param name="resultStatus">On return, an HTTP status code indicating success or failure.
        ///     </param>
        /// <returns>A new RevisionInternal with the docID, revID and sequence filled in (but no body).
        ///     </returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal RevisionInternal PutRevision(RevisionInternal oldRev, string prevRevId, bool allowConflict, Status resultStatus = null)
        {
            return PutDocument(oldRev.GetDocId(), oldRev.GetProperties(), prevRevId, allowConflict, resultStatus);
        }

        internal bool PostChangeNotifications()
        {
            bool posted = false;

            // This is a 'while' instead of an 'if' because when we finish posting notifications, there
            // might be new ones that have arrived as a result of notification handlers making document
            // changes of their own (the replicator manager will do this.) So we need to check again.
            while (!InTransaction && _isOpen && !_isPostingChangeNotifications && _changesToNotify != null && _changesToNotify.Count > 0) {
                try {
                    _isPostingChangeNotifications = true;

                    IList<DocumentChange> outgoingChanges = new List<DocumentChange>();
                    foreach (var change in _changesToNotify) {
                        outgoingChanges.Add(change);
                    }
                    _changesToNotify.Clear();
                    // TODO: change this to match iOS and call cachedDocumentWithID
                    var isExternal = false;
                    foreach (var change in outgoingChanges) {
                        var document = GetDocument(change.DocumentId);
                        document.RevisionAdded(change, true);
                        if (change.SourceUrl != null) {
                            isExternal = true;
                        }
                    }

                    var args = new DatabaseChangeEventArgs { 
                        Changes = outgoingChanges,
                        IsExternal = isExternal,
                        Source = this
                    };

                    var changeEvent = _changed;
                    if (changeEvent != null)
                        changeEvent(this, args);

                    posted = true;
                } catch (Exception e) {
                    Log.E(TAG, "Got exception posting change notifications", e);
                } finally {
                    _isPostingChangeNotifications = false;
                }
            }

            return posted;
        }

        internal void NotifyChange(RevisionInternal rev, RevisionInternal winningRev, Uri source, bool inConflict)
        {
            var change = new DocumentChange(rev, winningRev.GetRevId(), inConflict, source);
            _changesToNotify.Add(change);

            if (!PostChangeNotifications())
            {
                // The notification wasn't posted yet, probably because a transaction is open.
                // But the Document, if any, needs to know right away so it can update its
                // currentRevision.
                var doc = DocumentCache.Get(change.DocumentId);
                if (doc != null) {
                    doc.RevisionAdded(change, false);
                }
            }
        }

        /// <summary>
        /// Given a newly-added revision, adds the necessary attachment rows to the sqliteDb and
        /// stores inline attachments into the blob store.
        /// </summary>
        /// <remarks>
        /// Given a newly-added revision, adds the necessary attachment rows to the sqliteDb and
        /// stores inline attachments into the blob store.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void ProcessAttachmentsForRevision(IDictionary<string, AttachmentInternal> attachments, RevisionInternal rev, long parentSequence)
        {
            Debug.Assert((rev != null));
            var newSequence = rev.GetSequence();
            Debug.Assert((newSequence > parentSequence));
            var generation = rev.GetGeneration();
            Debug.Assert((generation > 0));

            // If there are no attachments in the new rev, there's nothing to do:
            IDictionary<string, object> revAttachments = null;
            var properties = rev.GetProperties ();
            if (properties != null) {
                revAttachments = properties.Get("_attachments").AsDictionary<string, object>();
            }

            if (revAttachments == null || revAttachments.Count == 0 || rev.IsDeleted()) {
                return;
            }

            foreach (string name in revAttachments.Keys)
            {
                var attachment = attachments.Get(name);
                if (attachment != null)
                {
                    // Determine the revpos, i.e. generation # this was added in. Usually this is
                    // implicit, but a rev being pulled in replication will have it set already.
                    if (attachment.RevPos == 0)
                    {
                        attachment.RevPos = generation;
                    }
                    else
                    {
                        if (attachment.RevPos > generation)
                        {
                            Log.W(TAG, string.Format("Attachment {0} {1} has unexpected revpos {2}, setting to {3}", rev, name, attachment.RevPos, generation));
                            attachment.RevPos = generation;
                        }
                    }
                }
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void InstallAttachment(AttachmentInternal attachment)
        {
            var digest = attachment.Digest;
            if (digest == null) {
                throw new CouchbaseLiteException(StatusCode.BadAttachment);
            }

            if (PendingAttachmentsByDigest != null && PendingAttachmentsByDigest.ContainsKey(digest)) {
                var writer = PendingAttachmentsByDigest.Get(digest);
                try {
                    var blobStoreWriter = writer;
                    blobStoreWriter.Install();
                    attachment.BlobKey = (blobStoreWriter.GetBlobKey());
                    attachment.Length = blobStoreWriter.GetLength();
                }
                catch (Exception e) {
                    throw new CouchbaseLiteException(e, StatusCode.AttachmentError);
                }
            }
        }

        internal Uri FileForAttachmentDict(IDictionary<String, Object> attachmentDict)
        {
            var digest = (string)attachmentDict.Get("digest");
            if (digest == null)
            {
                return null;
            }
            string path = null;
            var pending = PendingAttachmentsByDigest.Get(digest);
            if (pending != null)
            {
                path = pending.FilePath;
            }
            else
            {
                // If it's an installed attachment, ask the blob-store for it:
                var key = new BlobKey(digest);
                path = Attachments.PathForKey(key);
            }
            Uri retval = null;
            try
            {
                retval = new FilePath(path).ToURI().ToURL();
            }
            catch (UriFormatException)
            {
            }
            //NOOP: retval will be null
            return retval;
        }

        internal IDictionary<string, object> GetRevisionHistoryDictStartingFromAnyAncestor(RevisionInternal rev, IList<string>ancestorRevIDs)
        {
            var history = GetRevisionHistory(rev, null); // This is in reverse order, newest ... oldest
            if (ancestorRevIDs != null && ancestorRevIDs.Any())
            {
                for (var i = 0; i < history.Count; i++)
                {
                    if (ancestorRevIDs.Contains(history[i].GetRevId()))
                    {
                        var newHistory = new List<RevisionInternal>();
                        for (var index = 0; index < i + 1; index++) 
                        {
                            newHistory.Add(history[index]);
                        }
                        history = newHistory;
                        break;
                    }
                }
            }

            return Database.MakeRevisionHistoryDict(history);
        }

        internal IList<RevisionInternal> GetRevisionHistory(RevisionInternal rev, IList<string> ancestorRevIds)
        {
            HashSet<string> ancestors = ancestorRevIds != null ? new HashSet<string>(ancestorRevIds) : null;
            string docId = rev.GetDocId();
            string revId = rev.GetRevId();
            Debug.Assert(docId != null && revId != null);

            var docNumericId = GetDocNumericID(docId);
            if (docNumericId < 0) {
                return null;
            }

            if (docNumericId == 0) {
                return new List<RevisionInternal>(0);
            }

            var lastSequence = 0L;
            var history = new List<RevisionInternal>();
            var status = TryQuery(c =>
            {
                var sequence = c.GetLong(0);
                bool matches;
                if(lastSequence == 0) {
                    matches = revId == c.GetString(2);
                } else {
                    matches = lastSequence == sequence;
                }

                if(matches) {
                    string nextRevId = c.GetString(2);
                    bool deleted = c.GetInt(3) != 0;
                    var nextRev = new RevisionInternal(docId, nextRevId, deleted);
                    nextRev.SetSequence(sequence);
                    nextRev.SetMissing(c.GetInt(4) != 0);
                    history.Add(nextRev);
                    lastSequence = c.GetLong(1);
                    if(lastSequence == 0) {
                        return false;
                    }

                    if(ancestors != null && ancestors.Contains(revId)) {
                        return false;
                    }
                }

                return true;
            }, false, "SELECT sequence, parent, revid, deleted, json isnull" +
                " FROM revs WHERE doc_id=? ORDER BY sequence DESC", docNumericId);

            if (status.IsError) {
                return null;
            }

            return history;
        }

        internal bool ExpandAttachments(RevisionInternal rev, int minRevPos, bool allowFollows, 
            bool decodeAttachments, Status outStatus)
        {
            outStatus.Code = StatusCode.Ok;
            rev.MutateAttachments((name, attachment) =>
            {
                var revPos = attachment.GetCast<long>("revpos");
                if(revPos < minRevPos && revPos != 0) {
                    //Stub:
                    return new Dictionary<string, object> { { "stub", true }, { "revpos", revPos } };
                }

                var expanded = new Dictionary<string, object>(attachment);
                expanded.Remove("stub");
                if(decodeAttachments) {
                    expanded.Remove("encoding");
                    expanded.Remove("encoded_length");
                }

                if(allowFollows && SmallestLength(expanded) >= Database.BIG_ATTACHMENT_LENGTH) {
                    //Data will follow (multipart):
                    expanded["follows"] = true;
                    expanded.Remove("data");
                } else {
                    //Put data inline:
                    expanded.Remove("follows");
                    Status status = new Status();
                    var attachObj = AttachmentForDict(attachment, name, status);
                    if(attachObj == null) {
                        Log.W(TAG, "Can't get attachment '{0}' of {1} (status {2})", name, rev, status);
                        outStatus.Code = status.Code;
                        return attachment;
                    }

                    var data = decodeAttachments ? attachObj.Content : attachObj.EncodedContent;
                    if(data == null) {
                        Log.W(TAG, "Can't get binary data of attachment '{0}' of {1}", name, rev);
                        outStatus.Code = StatusCode.NotFound;
                        return attachment;
                    }

                    expanded["data"] = Convert.ToBase64String(data.ToArray());
                }

                return expanded;
            });

            return outStatus.Code == StatusCode.Ok;
        }

        internal AttachmentInternal AttachmentForDict(IDictionary<string, object> info, string filename, Status status)
        {
            if (info == null) {
                if (status != null) {
                    status.Code = StatusCode.NotFound;
                }

                return null;
            }

            AttachmentInternal attachment;
            try {
                attachment = new AttachmentInternal(filename, info);
            } catch(CouchbaseLiteException e) {
                if (status != null) {
                    status.Code = e.CBLStatus.Code;
                }
                return null;
            }

            attachment.Database = this;
            return attachment;
        }

        internal static void StubOutAttachmentsInRevBeforeRevPos(RevisionInternal rev, long minRevPos, bool attachmentsFollow)
        {
            if (minRevPos <= 1 && !attachmentsFollow)
            {
                return;
            }

            rev.MutateAttachments((name, attachment) =>
            {
                var revPos = 0L;
                if (attachment.ContainsKey("revpos"))
                {
                    revPos = Convert.ToInt64(attachment["revpos"]);
                }

                var includeAttachment = (revPos == 0 || revPos >= minRevPos);
                var stubItOut = !includeAttachment && (!attachment.ContainsKey("stub") || (bool)attachment["stub"] == false);
                var addFollows = includeAttachment && attachmentsFollow && (!attachment.ContainsKey("follows") || (bool)attachment["follows"] == false);

                if (!stubItOut && !addFollows)
                {
                    return attachment; // no change
                }

                // Need to modify attachment entry
                var editedAttachment = new Dictionary<string, object>(attachment);
                editedAttachment.Remove("data");

                if (stubItOut)
                {
                    // ...then remove the 'data' and 'follows' key:
                    editedAttachment.Remove("follows");
                    editedAttachment["stub"] = true;
                    Log.V(TAG, String.Format("Stubbed out attachment {0}: revpos {1} < {2}", rev, revPos, minRevPos));
                }
                else if (addFollows)
                {
                    editedAttachment.Remove("stub");
                    editedAttachment["follows"] = true;
                    Log.V(TAG, String.Format("Added 'follows' for attachment {0}: revpos {1} >= {2}", rev, revPos, minRevPos));
                }

                return editedAttachment;
            });
        }

        // Replaces the "follows" key with the real attachment data in all attachments to 'doc'.
        internal bool InlineFollowingAttachmentsIn(RevisionInternal rev)
        {
            return rev.MutateAttachments((s, attachment)=>
            {
                if (!attachment.ContainsKey("follows"))
                {
                    return attachment;
                }

                var fileURL = FileForAttachmentDict(attachment);
                byte[] fileData = null;
                try
                {
                    var inputStream = fileURL.OpenConnection().GetInputStream();
                    var os = new MemoryStream();
                    inputStream.CopyTo(os);
                    fileData = os.ToArray();
                }
                catch (IOException e)
                {
                    Log.E(TAG, "could not retrieve attachment data: {0}".Fmt(fileURL.ToString()), e);
                    return null;
                }

                var editedAttachment = new Dictionary<string, object>(attachment);
                editedAttachment.Remove("follows");
                editedAttachment.Put("data", Convert.ToBase64String(fileData));

                return editedAttachment;
            });
        }

        internal bool ProcessAttachmentsForRevision(RevisionInternal rev, string prevRevId, Status status)
        {
            if (status == null) {
                status = new Status();
            }

            status.Code = StatusCode.Ok;
            var revAttachments = rev.GetAttachments();
            if (revAttachments == null) {
                return true; // no-op: no attachments
            }

            // Deletions can't have attachments:
            if (rev.IsDeleted() || revAttachments.Count == 0) {
                var body = rev.GetProperties();
                body.Remove("_attachments");
                rev.SetProperties(body);
                return true;
            }

            int generation = RevisionInternal.GenerationFromRevID(prevRevId) + 1;
            IDictionary<string, object> parentAttachments = null;
            return rev.MutateAttachments((name, attachInfo) =>
            {
                AttachmentInternal attachment = null;
                try {
                    attachment = new AttachmentInternal(name, attachInfo);
                } catch(CouchbaseLiteException) {
                    return null;
                }

                if(attachment.EncodedContent != null) {
                    // If there's inline attachment data, decode and store it:
                    BlobKey blobKey = new BlobKey();
                    if(!Attachments.StoreBlob(attachment.EncodedContent.ToArray(), blobKey)) {
                        status.Code = StatusCode.AttachmentError;
                        return null;
                    }

                    attachment.BlobKey = blobKey;
                } else if(attachInfo.GetCast<bool>("follows")) {
                    // "follows" means the uploader provided the attachment in a separate MIME part.
                    // This means it's already been registered in _pendingAttachmentsByDigest;
                    // I just need to look it up by its "digest" property and install it into the store:
                    InstallAttachment(attachment);
                } else if(attachInfo.GetCast<bool>("stub")) {
                    // "stub" on an incoming revision means the attachment is the same as in the parent.
                    if(parentAttachments == null && prevRevId != null) {
                        parentAttachments = GetAttachmentsFromDoc(rev.GetDocId(), prevRevId, status);
                        if(parentAttachments == null) {
                            if(status.Code == StatusCode.Ok || status.Code == StatusCode.NotFound) {
                                status.Code = StatusCode.BadAttachment;
                            }

                            return null;
                        }
                    }

                    var parentAttachment = parentAttachments == null ? null : parentAttachments.Get(name).AsDictionary<string, object>();
                    if(parentAttachment == null) {
                        status.Code = StatusCode.BadAttachment;
                        return null;
                    }

                    return parentAttachment;
                }


                // Set or validate the revpos:
                if(attachment.RevPos == 0) {
                    attachment.RevPos = generation;
                } else if(attachment.RevPos > generation) {
                    status.Code = StatusCode.BadAttachment;
                    return null;
                }

                Debug.Assert(attachment.IsValid);
                return attachment.AsStubDictionary();
            });
        }

        internal IDictionary<string, object> GetAttachmentsFromDoc(string docId, string revId, Status status)
        {
            var rev = new RevisionInternal(docId, revId, false);
            try {
                LoadRevisionBody(rev);
            } catch(CouchbaseLiteException e) {
                status.Code = e.CBLStatus.Code;
                return null;
            }

            return rev.GetAttachments();
        }

        /// <summary>
        /// Given a revision, read its _attachments dictionary (if any), convert each attachment to a
        /// AttachmentInternal object, and return a dictionary mapping names-&gt;CBL_Attachments.
        /// </summary>
        /// <remarks>
        /// Given a revision, read its _attachments dictionary (if any), convert each attachment to a
        /// AttachmentInternal object, and return a dictionary mapping names-&gt;CBL_Attachments.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IDictionary<String, AttachmentInternal> GetAttachmentsFromRevision(RevisionInternal rev)
        {
            var revAttachments = rev.GetPropertyForKey("_attachments").AsDictionary<string, object>();
            if (revAttachments == null || revAttachments.Count == 0 || rev.IsDeleted())
            {
                return new Dictionary<string, AttachmentInternal>();
            }

            var attachments = new Dictionary<string, AttachmentInternal>();
            foreach (var name in revAttachments.Keys)
            {
                var attachInfo = revAttachments.Get(name).AsDictionary<string, object>();
                var contentType = (string)attachInfo.Get("content_type");
                var attachment = new AttachmentInternal(name, contentType);
                var newContentBase64 = (string)attachInfo.Get("data");
                if (newContentBase64 != null)
                {
                    // If there's inline attachment data, decode and store it:
                    byte[] newContents;
                    try
                    {
                        newContents = StringUtils.ConvertFromUnpaddedBase64String (newContentBase64);
                    }
                    catch (IOException e)
                    {
                        throw new CouchbaseLiteException(e, StatusCode.BadEncoding);
                    }
                    attachment.Length = newContents.Length;
                    var outBlobKey = new BlobKey();
                    var storedBlob = Attachments.StoreBlob(newContents, outBlobKey);
                    attachment.BlobKey = outBlobKey;
                    if (!storedBlob)
                    {
                        throw new CouchbaseLiteException(StatusCode.AttachmentError);
                    }
                }
                else
                {
                    if (attachInfo.ContainsKey("follows") && ((bool)attachInfo.Get("follows")))
                    {
                        // "follows" means the uploader provided the attachment in a separate MIME part.
                        // This means it's already been registered in _pendingAttachmentsByDigest;
                        // I just need to look it up by its "digest" property and install it into the store:
                        InstallAttachment(attachment);
                    }
                    else
                    {
                        // This item is just a stub; validate and skip it
                        if (((bool)attachInfo.Get("stub")) == false)
                        {
                            throw new CouchbaseLiteException("Expected this attachment to be a stub", StatusCode.
                                BadAttachment);
                        }

                        var revPos = Convert.ToInt64(attachInfo.Get("revpos"));
                        if (revPos <= 0)
                        {
                            throw new CouchbaseLiteException("Invalid revpos: " + revPos, StatusCode.BadAttachment);
                        }

                        continue;
                    }
                }
                // Handle encoded attachment:
                string encodingStr = (string)attachInfo.Get("encoding");
                if (!string.IsNullOrEmpty(encodingStr)) {
                    if ("gzip".Equals(encodingStr, StringComparison.CurrentCultureIgnoreCase)) {
                        attachment.Encoding = AttachmentEncoding.GZIP;
                    }
                    else {
                        throw new CouchbaseLiteException("Unnkown encoding: " + encodingStr, StatusCode.BadEncoding
                        );
                    }
                    attachment.EncodedLength = attachment.Length;
                    if (attachInfo.ContainsKey("length")) {
                        attachment.Length = attachInfo.GetCast<long>("length");
                    }
                }
                if (attachInfo.ContainsKey("revpos"))
                {
                    var revpos = Convert.ToInt32(attachInfo.Get("revpos"));
                    attachment.RevPos = revpos;
                }
                attachments[name] = attachment;
            }
            return attachments;
        }

        internal AttachmentInternal GetAttachmentForRevision(RevisionInternal rev, string name, Status status = null)
        {
            Debug.Assert(name != null);
            var attachments = rev.GetAttachments();
            if (attachments == null) {
                try {
                    rev = LoadRevisionBody(rev);
                } catch(CouchbaseLiteException e) {
                    if (status != null) {
                        status.Code = e.CBLStatus.Code;
                    }

                    return null;
                }

                attachments = rev.GetAttachments();
                if (attachments == null) {
                    status.Code = StatusCode.NotFound;
                    return null;
                }
            }

            return AttachmentForDict(attachments.Get(name).AsDictionary<string, object>(), name, status);
        }

        internal RevisionInternal RevisionByLoadingBody(RevisionInternal rev, Status outStatus)
        {
            // First check for no-op -- if we just need the default properties and already have them:
            if (rev.GetSequence() != 0) {
                var props = rev.GetProperties();
                if (props != null && props.ContainsKey("_rev") && props.ContainsKey("_id")) {
                    if (outStatus != null) {
                        outStatus.Code = StatusCode.Ok;
                    }

                    return rev;
                }
            }

            RevisionInternal nuRev = rev.CopyWithDocID(rev.GetDocId(), rev.GetRevId());
            try {
                LoadRevisionBody(nuRev);
            } catch(CouchbaseLiteException e) {
                if (outStatus != null) {
                    outStatus.Code = e.CBLStatus.Code;
                }

                nuRev = null;
            }

            return nuRev;
        }

        //Doesn't handle CouchbaseLiteException
        internal RevisionInternal LoadRevisionBody(RevisionInternal rev)
        {
            if (rev.GetSequence() > 0) {
                var props = rev.GetProperties();
                if (props != null && props.GetCast<string>("_rev") != null && props.GetCast<string>("_id") != null) {
                    return rev;
                }
            }

            Debug.Assert(rev.GetDocId() != null && rev.GetRevId() != null);
            if (rev.GetBody() != null && rev.GetSequence() != 0) {
                // no-op
                return rev;
            }

            Debug.Assert(rev.GetDocId() != null && rev.GetRevId() != null);
            var docNumericId = GetDocNumericID(rev.GetDocId());
            if (docNumericId <= 0L) {
                throw new CouchbaseLiteException(StatusCode.NotFound);
            }

            var status = TryQuery(c =>
            {
                var json = c.GetBlob(1);
                if(json != null) {
                    rev.SetSequence(c.GetLong(0));
                    rev.SetJson(json);
                }

                return false;
            }, false, "SELECT sequence, json FROM revs WHERE doc_id=? AND revid=? LIMIT 1", docNumericId, rev.GetRevId());

            if (status.IsError) {
                throw new CouchbaseLiteException(status.Code);
            }

            return rev;
        }

        internal Boolean IsValidDocumentId(string id)
        {
            // http://wiki.apache.org/couchdb/HTTP_Document_API#Documents
            if (String.IsNullOrEmpty (id)) {
                return false;
            }

            return id [0] != '_' || id.StartsWith ("_design/", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>Updates or deletes an attachment, creating a new document revision in the process.
        ///     </summary>
        /// <remarks>
        /// Updates or deletes an attachment, creating a new document revision in the process.
        /// Used by the PUT / DELETE methods called on attachment URLs.
        /// </remarks>
        /// <exclude></exclude>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal RevisionInternal UpdateAttachment(string filename, BlobStoreWriter body, string contentType, AttachmentEncoding encoding, string docID, string oldRevID)
        {
            if(StringEx.IsNullOrWhiteSpace(filename) || (body != null && contentType == null) || 
                (oldRevID != null && docID == null) || (body != null && docID == null)) {
                throw new CouchbaseLiteException(StatusCode.BadAttachment);
            }

            var oldRev = new RevisionInternal(docID, oldRevID, false);
            if (oldRevID != null) {
                // Load existing revision if this is a replacement:
                try {
                    oldRev = LoadRevisionBody(oldRev);
                } catch (CouchbaseLiteException e) {
                    if (e.Code == StatusCode.NotFound && GetDocument(docID, null, false) != null) {
                        throw new CouchbaseLiteException(StatusCode.Conflict);
                    }

                    throw;
                }
            } else {
                // If this creates a new doc, it needs a body:
                oldRev.SetBody(new Body(new Dictionary<string, object>()));
            }

            // Update the _attachments dictionary:
            var attachments = oldRev.GetProperties().Get("_attachments").AsDictionary<string, object>();
            if (attachments == null) {
                attachments = new Dictionary<string, object>();
            }

            if (body != null) {
                var key = body.GetBlobKey();
                string digest = key.Base64Digest();
                RememberAttachmentWriter(body);
                string encodingName = (encoding == AttachmentEncoding.GZIP) ? "gzip" : null;
                attachments[filename] = new NonNullDictionary<string, object> {
                    { "digest", digest },
                    { "length", body.GetLength() },
                    { "follows", true },
                    { "content_type", contentType },
                    { "encoding", encodingName }
                };
            } else {
                if (oldRevID != null && attachments.Get(filename) == null) {
                    throw new CouchbaseLiteException(StatusCode.AttachmentNotFound);
                }

                attachments.Remove(filename);
            }

            var properties = oldRev.GetProperties();
            properties["_attachments"] = attachments;
            oldRev.SetProperties(properties);

            Status status = new Status();
            var newRev = PutRevision(oldRev, oldRevID, false, status);
            if (status.IsError) {
                throw new CouchbaseLiteException(status.Code);
            }

            return newRev;
        }

        /// <summary>VALIDATION</summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal Status ValidateRevision(RevisionInternal newRev, RevisionInternal oldRev, String parentRevId)
        {
            var validations = Shared.GetValues("validation", Name);
            if (validations == null || validations.Count == 0) {
                return new Status(StatusCode.Ok);
            }

            var publicRev = new SavedRevision(this, newRev, parentRevId);
            var context = new ValidationContext(this, oldRev, newRev);
            Status status = new Status(StatusCode.Ok);
            foreach (var validationName in validations.Keys)
            {
                var validation = GetValidation(validationName);
                try {
                    validation(publicRev, context);
                } catch(Exception e) {
                    Log.E(TAG, String.Format("Validation block '{0}'", validationName), e);
                    status.Code = StatusCode.Exception;
                    break;
                }

                if (context.RejectMessage != null) {
                    Log.D(TAG, "Failed update of {0}: {1}:{2} Old doc = {3}{2} New doc = {4}", oldRev, context.RejectMessage,
                        Environment.NewLine, oldRev == null ? null : oldRev.GetProperties(), newRev.GetProperties());
                    status.Code = StatusCode.Forbidden;
                    break;
                }
            }

            return status;
        }

        internal IEnumerable<byte> EncodeDocumentJSON(RevisionInternal rev)
        {
            var originalProps = rev.GetProperties();
            if (originalProps == null) {
                return null;
            }

            var properties = StripDocumentJSON(originalProps);

            // Create canonical JSON -- this is important, because the JSON data returned here will be used
            // to create the new revision ID, and we need to guarantee that equivalent revision bodies
            // result in equal revision IDs.
            return Manager.GetObjectMapper().WriteValueAsBytes(properties, true);
        }

        internal String AttachmentStorePath 
        {
            get 
            {
                return System.IO.Path.ChangeExtension(Path, null) + " attachments";
            }
        }

        internal bool Close()
        {
            var success = true;
            if (_isOpen) {
                Log.D("Closing database at {0}", Path);
                if (_views != null) {
                    foreach (var view in _views) {
                        view.Value.Close();
                    }
                }

                if (ActiveReplicators != null) {
                    var activeReplicatorCopy = new Replication[ActiveReplicators.Count];
                    ActiveReplicators.CopyTo(activeReplicatorCopy, 0);
                    foreach (var repl in activeReplicatorCopy) {
                        repl.DatabaseClosing();
                    }

                    ActiveReplicators = null;
                }

                try {
                    StorageEngine.Close();
                } catch(Exception) {
                    success = false;
                }

                _isOpen = false;
                UnsavedRevisionDocumentCache.Clear();
                DocumentCache = new LruCache<string, Document>(DocumentCache.MaxSize);
            }

            Manager.ForgetDatabase(this);
            return success;
        }

        internal bool Open()
        {
            if (_isOpen) {
                return true;
            }

            Log.D(TAG, "Opening {0}", Name);

            // Create the storage engine.
            StorageEngine = SQLiteStorageEngineFactory.CreateStorageEngine();

            // Try to open the storage engine and stop if we fail.
            if (StorageEngine == null || !StorageEngine.Open(Path)) {
                var msg = "Unable to create a storage engine, fatal error";
                Log.E(TAG, msg);
                throw new CouchbaseLiteException(msg);
            }

            // Stuff we need to initialize every time the sqliteDb opens:
            if (!RunStatements("PRAGMA foreign_keys = ON; PRAGMA journal_mode=WAL;")) {
                Log.E(TAG, "Error turning on foreign keys");
                return false;
            }

            // Check the user_version number we last stored in the sqliteDb:
            var dbVersion = StorageEngine.GetVersion();
            bool isNew = dbVersion == 0;
            if (isNew && !RunStatements("BEGIN TRANSACTION")) {
                StorageEngine.Close();
                return false;
            }

            // Incompatible version changes increment the hundreds' place:
            if (dbVersion >= 200)
            {
                Log.E(TAG, "Database: Database version (" + dbVersion + ") is newer than I know how to work with");
                StorageEngine.Close();
                return false;
            }

            if (dbVersion < 17) {
                if (!isNew) {
                    Log.W(TAG, "Database version ({0}) is older than I know how to work with",
                        dbVersion);
                    StorageEngine.Close();
                    return false;
                }
                    
                if (!RunStatements(SCHEMA)) {
                    StorageEngine.Close();
                    return false;
                }
                dbVersion = 17;
            }
            if (dbVersion < 18) {
                const string upgradeSql = "ALTER TABLE revs ADD COLUMN doc_type TEXT;" +
                    "PRAGMA user_version = 18";

                if (!RunStatements(upgradeSql)) {
                    StorageEngine.Close();
                    return false;
                }
                dbVersion = 18;
            }
            if (dbVersion < 101) {
                const string upgradeSql = "PRAGMA user_version = 101";
                if (!RunStatements(upgradeSql)) {
                    StorageEngine.Close();
                    return false;
                }
                dbVersion = 101;
            }

            if (isNew && !RunStatements("END TRANSACTION")) {
                StorageEngine.Close();
                return false;
            }

            if (!isNew) {
                OptimizeSQLIndexes();
            }

            // First-time setup:
            if (PrivateUUID() == null) {
                SetInfo("privateUUID", Misc.CreateGUID());
                SetInfo("publicUUID", Misc.CreateGUID());
            }

            var savedMaxRevDepth = _maxRevTreeDepth != 0 ? _maxRevTreeDepth.ToString() : GetInfo("max_revs");
            int maxRevTreeDepth = 0;
            if (savedMaxRevDepth != null && int.TryParse(savedMaxRevDepth, out maxRevTreeDepth)) {
                MaxRevTreeDepth = maxRevTreeDepth;
            } else {
                MaxRevTreeDepth = DEFAULT_MAX_REVS;
            }

            // Open attachment store:
            string attachmentsPath = AttachmentStorePath;

            try {
                Attachments = new BlobStore(attachmentsPath);
            } catch(Exception e) {
                Log.W(TAG, String.Format("Couldn't open attachment store at {0}", attachmentsPath), e);
                StorageEngine.Close();
                StorageEngine = null;
                return false;
            }

            _isOpen = true;
            return true;
        }

        internal RevisionInternal PutLocalRevision(RevisionInternal revision, string prevRevId, bool obeyMVCC)
        {
            string docId = revision.GetDocId();
            if (!docId.StartsWith("_local/")) {
                Log.E(TAG, "Local revision doesn't start with '_local/'");
                throw new CouchbaseLiteException(StatusCode.BadId);
            }

            if (!obeyMVCC) {
                return PutLocalRevisionNoMvcc(revision);
            }

            if (!revision.IsDeleted()) {
                // PUT:
                var json = EncodeDocumentJSON(revision);
                if (json == null) {
                    Log.E(TAG, "Invalid JSON in local revision");
                    throw new CouchbaseLiteException(StatusCode.BadJson);
                }

                string newRevId;
                long changes = -1;
                if (prevRevId != null) {
                    int generation = RevisionInternal.GenerationFromRevID(prevRevId);
                    if (generation == 0) {
                        Log.E(TAG, "Invalid prevRevId in PutLocalRevision");
                        throw new CouchbaseLiteException(StatusCode.BadId);
                    }

                    newRevId = String.Format("{0}-local", ++generation);
                    try {
                        var args = new ContentValues();
                        args["revid"] = newRevId;
                        args["json"] = json;
                        changes = StorageEngine.Update("localdocs", args, "docid=? AND revid=?", docId, prevRevId);
                    } catch (Exception e) {
                        throw new CouchbaseLiteException(e, StatusCode.DbError);
                    }
                } else {
                    newRevId = "1-local";
                    // The docid column is unique so the insert will be a no-op if there is already
                    // a doc with this ID.
                    var args = new ContentValues();
                    args["docid"] = docId;
                    args["revid"] = newRevId;
                    args["json"] = json;
                    try {
                        changes = StorageEngine.InsertWithOnConflict("localdocs", null, args, ConflictResolutionStrategy.Ignore);
                    } catch (Exception e) {
                        throw new CouchbaseLiteException(e, StatusCode.DbError);
                    }
                }

                if (changes == 0) {
                    Log.I(TAG, "Local revision conflict detected");
                    throw new CouchbaseLiteException(StatusCode.Conflict);
                }

                return revision.CopyWithDocID(docId, newRevId);
            } else {
                // DELETE:
                var status = DeleteLocalRevision(docId, prevRevId);
                if (status.IsError) {
                    throw new CouchbaseLiteException(status.Code);
                }

                return revision;
            }
        }


        #endregion

        #region Private Methods

        private string GetWinner(long docNumericId, string oldWinnerRevId, bool oldWinnerWasDeletion, RevisionInternal newRev)
        {
            var newRevID = newRev.GetRevId();
            if (oldWinnerRevId == null) {
                return newRevID;
            }

            if (!newRev.IsDeleted()) {
                if (oldWinnerWasDeletion || RevisionInternal.CBLCompareRevIDs(newRevID, oldWinnerRevId) > 0) {
                    return newRevID; // this is now the winning live revision
                }
            } else if (oldWinnerWasDeletion) {
                if (RevisionInternal.CBLCompareRevIDs(newRevID, oldWinnerRevId) > 0) {
                    return newRevID; // doc still deleted, but this beats previous deletion rev
                }
            } else {
                // Doc was alive. How does this deletion affect the winning rev ID?
                ValueTypePtr<bool> deleted = false;
                var winningRevId = GetWinner(docNumericId, deleted, ValueTypePtr<bool>.NULL);
                if (winningRevId != oldWinnerRevId) {
                    return winningRevId;
                }
            }

            return null; // no change
        }

        private bool SequenceHasAttachments(long sequence)
        {
            return QueryOrDefault<bool>(c => c.GetInt(0) != 0, false, false, 
                "SELECT no_attachments=0 FROM revs WHERE sequence=?", sequence);
        }

        private long GetSequenceOfDocument(long docNumericId, string revId, bool onlyCurrent)
        {
            var sql = String.Format("SELECT sequence FROM revs WHERE doc_id=? AND revid=? {0} LIMIT 1",
                (onlyCurrent ? "AND current=1" : ""));

            return QueryOrDefault<long>(c => c.GetLong(0), true, 0L, sql, docNumericId, revId);
        }

        private bool DocumentExists(string docId, string revId)
        {
            return GetDocument(docId, revId, false) != null;
        }

        private long InsertRevision(RevisionInternal rev, long docNumericId, long parentSequence, bool current, bool hasAttachments,
            IEnumerable<byte> json, string docType)
        {
            var vals = new ContentValues();
            vals["doc_id"] = docNumericId;
            vals["revid"] = rev.GetRevId();
            if (parentSequence != 0) {
                vals["parent"] = parentSequence;
            }

            vals["current"] = current;
            vals["deleted"] = rev.IsDeleted();
            vals["no_attachments"] = !hasAttachments;
            if (json != null) {
                vals["json"] = json;
            }

            if (docType != null) {
                vals["doc_type"] = docType;
            }

            try {
                var row = StorageEngine.Insert("revs", null, vals);
                rev.SetSequence(row);
                return row;
            } catch(Exception) {
                return 0L;
            }
        }

        private Status RunInOuterTransaction(Func<Status> action)
        {
            if (!InTransaction) {
                return RunInTransaction(action);
            }

            Status status = null;
            try {
                status = action();
            } catch(Exception e) {
                Log.E(TAG, "Exception in RunInOuterTransaction", e);
                status = new Status(StatusCode.Exception);
            }

            return status;
        }

        private RevisionList GetAllDocumentRevisions(string docId, long docNumericId, bool onlyCurrent, Status status = null)
        {
            if (status == null) {
                status = new Status();
            }

            string sql;
            if (onlyCurrent) {
                sql = "SELECT sequence, revid, deleted FROM revs " +
                    "WHERE doc_id=? AND current ORDER BY sequence DESC";
            } else {
                sql = "SELECT sequence, revid, deleted FROM revs " +
                    "WHERE doc_id=? ORDER BY sequence DESC";
            }

            var revs = new RevisionList();
            var innerStatus = TryQuery(c =>
            {
                var rev = new RevisionInternal(docId, c.GetString(1), c.GetInt(2) != 0);
                rev.SetSequence(c.GetLong(0));
                revs.Add(rev);

                return true;
            }, true, sql, docNumericId);

            status.Code = innerStatus.Code;
            if (status.IsError) {
                Log.W(TAG, "GetAllDocumentRevisions() failed {0}", status.Code);
            }

            return status.IsError ? null : revs;
        }

        private IDictionary<string, object> StripDocumentJSON(IDictionary<string, object> originalProps)
        {
            // Don't leave in any "_"-prefixed keys except for the ones in SPECIAL_KEYS_TO_LEAVE.
            // Keys in SPECIAL_KEYS_TO_REMOVE (_id, _rev, ...) are left out, any others trigger an error.
            var properties = new Dictionary<string, object>(originalProps.Count);
            foreach (var pair in originalProps) {
                if (!pair.Key.StartsWith("_") || SPECIAL_KEYS_TO_LEAVE.Contains(pair.Key)) {
                    properties[pair.Key] = pair.Value;
                } else if (!SPECIAL_KEYS_TO_REMOVE.Contains(pair.Key)) {
                    Log.W(TAG, "Invalid top-level key '{0}' in document to be inserted", pair.Key);
                    return null;
                }
            }

            return properties;
        }

        private Status DeleteLocalRevision(string docId, string revId)
        {
            if (revId == null) {
                // Didn't specify a revision to delete: kCBLStatusNotFound or a kCBLStatusConflict, depending
                return GetLocalDocument(docId, null) != null ? new Status(StatusCode.Conflict) : new Status(StatusCode.NotFound);
            }

            var changes = 0;
            try {
                changes = StorageEngine.Delete("localdocs", "docid=? AND revid=?", docId, revId);
            } catch(Exception) {
                return new Status(StatusCode.DbError);
            }

            if (changes == 0) {
                return GetLocalDocument(docId, null) != null ? new Status(StatusCode.Conflict) : new Status(StatusCode.NotFound);
            }

            return new Status(StatusCode.Ok);
        }

        private RevisionInternal PutLocalRevisionNoMvcc(RevisionInternal rev)
        {
            RevisionInternal result = null;
            RunInTransaction(() =>
            {
                RevisionInternal prevRev = GetLocalDocument(rev.GetDocId(), null);
                try {
                    result = PutLocalRevision(rev, prevRev == null ? null : prevRev.GetRevId(), true);
                } catch(CouchbaseLiteException e) {
                    return e.CBLStatus;
                }

                return new Status(StatusCode.Ok);
            });

            return result;
        }

        private RevisionList ChangesSince(long lastSequence, ChangesOptions options, RevisionFilter filter)
        {
            // http://wiki.apache.org/couchdb/HTTP_database_API#Changes
            if (options == null) {
                options = new ChangesOptions();
            }

            bool includeDocs = options.IsIncludeDocs() || filter != null;
            var sql = String.Format("SELECT sequence, revs.doc_id, docid, revid, deleted {0} FROM revs, docs " +
                "WHERE sequence > ? AND current=1 " +
                "AND revs.doc_id = docs.doc_id " +
                "ORDER BY revs.doc_id, revid DESC",
                (includeDocs ? @", json" : @""));

            var changes = new RevisionList();
            long lastDocId = 0L;
            TryQuery(c =>
            {
                if(!options.IsIncludeConflicts()) {
                    // Only count the first rev for a given doc (the rest will be losing conflicts):
                    var docNumericId = c.GetLong(1);
                    if(docNumericId == lastDocId) {
                        return true;
                    }

                    lastDocId = docNumericId;
                }

                string docId = c.GetString(2);
                string revId = c.GetString(3);
                bool deleted = c.GetInt(4) != 0;
                var rev = new RevisionInternal(docId, revId, deleted);
                rev.SetSequence(c.GetLong(0));
                if(includeDocs) {
                    rev.SetJson(c.GetBlob(5));
                }

                if(filter == null || filter(rev)) {
                    changes.Add(rev);
                }

                return true;
            }, false, sql, lastSequence);

            if (options.IsSortBySequence()) {
                changes.SortBySequence(!options.Descending);
                changes.Limit(options.GetLimit());
            }

            return changes;
        }

        private bool BeginTransaction()
        {
            try {
                _transactionCount = StorageEngine.BeginTransaction();
                Log.D(TAG, "Begin transaction (level " + _transactionCount + ")");
            } catch (SQLException e) {
                Log.E(TAG," Error calling beginTransaction()" , e);
                return false;
            }

            return true;
        }

        private bool EndTransaction(bool commit)
        {
            Debug.Assert((_transactionCount > 0));

            if (commit) {
                Log.V(TAG, "    Committing transaction (level " + _transactionCount + ")");
                StorageEngine.SetTransactionSuccessful();
            }
            else {
                Log.V(TAG, "    CANCEL transaction (level " + _transactionCount + ")");
            }

            try  {
                _transactionCount = StorageEngine.EndTransaction();
            } catch (SQLException e)  {
                Log.E(TAG, " Error calling EndTransaction()", e);
                return false;
            }

            StorageExitedTransaction(commit);

            return true;
        }

        private RevisionInternal RevisionWithDocID(string docId, string revId, bool deleted, long sequence, IEnumerable<byte> json)
        {
            var rev = new RevisionInternal(docId, revId, deleted);
            rev.SetSequence(sequence);
            if (json != null) {
                rev.SetJson(json);
            }

            return rev;
        }

        private long InsertDocNumericID(string docId)
        {
            var vals = new ContentValues();
            vals["docid"] = docId;
            try {
                var changed = StorageEngine.InsertWithOnConflict("docs", null, vals, ConflictResolutionStrategy.Ignore);
                if(changed == -1) {
                    return 0L;
                }

                return changed;
            } catch(Exception) {
                return -1L;
            }
        }

        private long GetOrInsertDocNumericID(string docId, ref bool isNewDoc)
        {
            var cached = _docIDs.Get(docId);
            if (cached != null) {
                isNewDoc = false;
                return (long)cached;
            }

            long row = isNewDoc ? InsertDocNumericID(docId) : GetDocNumericID(docId);
            if (row < 0) {
                return row;
            }

            if(row == 0) {
                isNewDoc = !isNewDoc;
                row = isNewDoc ? InsertDocNumericID(docId) : GetDocNumericID(docId);
            }

            if (row > 0) {
                _docIDs[docId] = row;
            }

            return row;
        }

        private IEnumerable<QueryRow> GetAllDocsRegular(QueryOptions options)
        {
            if (options == null) {
                options = new QueryOptions();
            }

            bool includeDocs = options.IncludeDocs || options.Filter != null;
            bool includeDeletedDocs = options.AllDocsMode == AllDocsMode.IncludeDeleted;

            // Generate the SELECT statement, based on the options:
            var sql = new StringBuilder("SELECT revs.doc_id, docid, revid, sequence");
            if (includeDocs) {
                sql.Append(", json, no_attachments");
            }

            if (includeDeletedDocs) {
                sql.Append(", deleted");
            }

            sql.Append(" FROM revs, docs WHERE");
            if (options.Keys != null) {
                if (!options.Keys.Any()) {
                    return null;
                }

                sql.AppendFormat(" revs.doc_id IN (SELECT doc_id FROM docs WHERE docid IN ({0})) AND", Database.JoinQuotedObjects(options.Keys));
            }

            sql.Append(" docs.doc_id = revs.doc_id AND current=1");
            if (!includeDeletedDocs) {
                sql.Append(" AND deleted=0");
            }

            var args = new List<object>();
            object minKey = options.StartKey;
            object maxKey = options.EndKey;
            bool inclusiveMin = true;
            bool inclusiveMax = options.InclusiveEnd;
            if (options.Descending) {
                minKey = maxKey;
                maxKey = options.StartKey;
                inclusiveMin = inclusiveMax;
                inclusiveMax = true;
            }

            if (minKey != null) {
                Debug.Assert(minKey is string);
                sql.Append(inclusiveMin ? " AND docid >= ?" : " AND docid > ?");
                args.Add(minKey);
            }

            if (maxKey != null) {
                Debug.Assert(maxKey is string);
                sql.Append(inclusiveMax ? " AND docid <= ?" : " AND docid < ?");
                args.Add(maxKey);
            }

            sql.AppendFormat(" ORDER BY docid {0}, {1} revid DESC LIMIT ? OFFSET ?",
                (options.Descending ? "DESC" : "ASC"),
                (includeDeletedDocs ? "deleted ASC," : string.Empty));

            args.Add(options.Limit);
            args.Add(options.Skip);

            // Now run the database query:
            Cursor c = null;
            var rows = new List<QueryRow>();
            var docs = new Dictionary<string, QueryRow>();
            try {
                c = StorageEngine.RawQuery(sql.ToString(), args.ToArray());
                bool keepGoing = c.MoveToNext();
                while(keepGoing) {
                    long docNumericId = c.GetLong(0);
                    string docId = c.GetString(1);
                    string revId = c.GetString(2);
                    long sequence = c.GetLong(3);
                    bool deleted = includeDeletedDocs && c.GetInt(includeDocs ? 6 : 4) != 0;

                    RevisionInternal docRevision = null;
                    if(includeDocs) {
                        // Fill in the document contents:
                        docRevision = RevisionWithDocID(docId, revId, deleted, sequence, c.GetBlob(4));
                        Debug.Assert(docRevision != null);
                    }

                    // Iterate over following rows with the same doc_id -- these are conflicts.
                    // Skip them, but collect their revIDs if the 'conflicts' option is set:
                    List<string> conflicts = null;
                    while((keepGoing = c.MoveToNext()) && c.GetLong(0) == docNumericId) {
                        if(options.AllDocsMode >= AllDocsMode.ShowConflicts) {
                            if(conflicts == null) {
                                conflicts = new List<string>();
                                conflicts.Add(revId);
                            }

                            conflicts.Add(c.GetString(2));
                        }
                    }

                    if(options.AllDocsMode == AllDocsMode.OnlyConflicts && conflicts == null) {
                        continue;
                    }

                    var value = new NonNullDictionary<string, object> {
                        { "rev", revId },
                        { "deleted", deleted ? (object)true : null },
                        { "_conflicts", conflicts } // (not found in CouchDB)
                    };

                    var row = new QueryRow(docId, sequence, docId, value, docRevision, null);
                    if(options.Keys != null) {
                        docs[docId] = row;
                    } else if(options.Filter == null || options.Filter(row)) {
                        rows.Add(row);
                    }
                }
            } catch(SQLException e) {
                Log.E(TAG, "Error in all docs query", e);
                return null;
            } finally {
                if(c != null) {
                    c.Close();
                }
            }

            // If given doc IDs, sort the output into that order, and add entries for missing docs:
            if (options.Keys != null) {
                foreach (var docId in options.Keys) {
                    var change = docs.Get(docId as string);
                    if (change == null) {
                        // create entry for missing or deleted doc:
                        IDictionary<string, object> value = null;
                        var docNumericId = GetDocNumericID(docId as string);
                        if (docNumericId > 0) {
                            ValueTypePtr<bool> deleted = false;
                            string revId = GetWinner(docNumericId, deleted, ValueTypePtr<bool>.NULL);
                            if (revId != null) {
                                value = new NonNullDictionary<string, object> {
                                    { "rev", revId },
                                    { "deleted", deleted ? (object)true : null }
                                };
                            }
                        }

                        change = new QueryRow(value != null ? docId as string : null, 0, docId, value, null, null);
                    }

                    if (options.Filter == null || options.Filter(change)) {
                        rows.Add(change);
                    }
                }
            }

            return rows;
        }

        private Status SetInfo(string key, string info)
        {
            var vals = new ContentValues(2);
            vals["key"] = key;
            vals["value"] = info;
            try {
                StorageEngine.InsertWithOnConflict("info", null, vals, ConflictResolutionStrategy.Replace);
            } catch(Exception) {
                return new Status(StatusCode.DbError);
            }

            return new Status(StatusCode.Ok);
        }

        private string GetInfo(string key)
        {
            string retVal = null;
            var success = TryQuery(c => {
                retVal = c.GetString(0);
                return false;
            }, false, "SELECT value FROM info WHERE key=?", key);

            return success.IsError ? null : retVal;
        }

        private static long SmallestLength(IDictionary<string, object> attachment)
        {
            long length = attachment.GetCast<long>("length");
            long encodedLength = attachment.GetCast<long>("encoded_length", -1);
            if (encodedLength != -1) {
                length = encodedLength;
            }

            return length;
        }

        private RevisionInternal GetDocumentWithIDAndRev(string docId, string revId, bool withBody)
        {
            return GetDocument(docId, revId, withBody);
        }

        private ICollection<BlobKey> FindAllAttachmentKeys()
        {
            var allKeys = new HashSet<BlobKey>();
            var status = TryQuery(c =>
            {
                var rev = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(c.GetBlob(0));
                foreach(var pair in rev["_attachments"].AsDictionary<string, object>()) {
                    var attachmentDict = pair.Value.AsDictionary<string, object>();
                    if(attachmentDict == null) {
                        Log.W(TAG, "Invalid attachment found, not a dictionary!");
                        continue;
                    }

                    var digest = attachmentDict.GetCast<string>("digest");
                    if(digest == null) {
                        Log.W(TAG, "Invalid attachment found, no digest!");
                        continue;
                    }

                    var blobKey = new BlobKey(digest);
                    allKeys.Add(blobKey);
                }

                return true;
            }, false, "SELECT json FROM revs WHERE no_attachments != 1");

            return status.IsError ? null : allKeys;
        }

        // Deletes obsolete attachments from the sqliteDb and blob store.
        private bool GarbageCollectAttachments()
        {
            Log.D(TAG, "Scanning database revisions for attachments...");
            var keys = FindAllAttachmentKeys();
            if (keys == null) {
                return false;
            }

            Log.D(TAG, "...found {0} attachments", keys.Count);
            var numDeleted = Attachments.DeleteBlobsExceptWithKeys(keys);
            Log.D(TAG, "    ... deleted {0} obsolete attachment files.", numDeleted);

            return numDeleted >= 0;
        }

        private string CheckpointInfoKey(string checkpointId) 
        {
            return "checkpoint/" + checkpointId;
        }

        private long KeyToSequence(object key, long defaultVal)
        {
            if (key == null) {
                return defaultVal;
            }

            try {
                return Convert.ToInt64(key);
            } catch(Exception) {
                return defaultVal;
            }
        }

        private Document GetDocument(string docId, bool mustExist)
        {
            if (StringEx.IsNullOrWhiteSpace (docId)) {
                return null;
            }

            var unsavedDoc = UnsavedRevisionDocumentCache.Get(docId);
            var doc = unsavedDoc != null 
                ? (Document)unsavedDoc.Target 
                : DocumentCache.Get(docId);

            if (doc != null) {
                if (mustExist && doc.CurrentRevision == null) {
                    return null;
                }

                return doc;
            }

            doc = new Document(this, docId);
            if (mustExist && doc.CurrentRevision == null) {
                return null;
            }

            if (DocumentCache == null) {
                DocumentCache = new LruCache<string, Document>(MAX_DOC_CACHE_SIZE);
            }

            DocumentCache[docId] = doc;
            UnsavedRevisionDocumentCache[docId] = new WeakReference(doc);
            return doc;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases all resource used by the <see cref="Couchbase.Lite.Database"/> object.
        /// </summary>
        /// <remarks>
        /// The database file may be used again if open is called
        /// </remarks>
        public void Dispose()
        {
            if (_isOpen && !Close()) {
                Log.E(TAG, "Error disposing database (possibly already disposed?)");
            }
        }

        #endregion

        #region ICouchStoreDelegate
        #pragma warning disable 1591

        public void StorageExitedTransaction(bool committed)
        {
            var changes = _changesToNotify;
            if (!committed && changes != null) {
                // I already told cached Documents about these new revisions. Back that out:
                foreach(var change in changes) {
                    var doc = DocumentCache.Get(change.DocumentId);
                    if (doc != null) {
                        doc.ForgetCurrentRevision();
                    }
                }

                _changesToNotify = null;
            }

            PostChangeNotifications();
        }

        public void DatabaseStorageChanged(DocumentChange change)
        {
            Log.D(TAG, "Added: {0}", change.AddedRevision);
            if (_changesToNotify == null) {
                _changesToNotify = new List<DocumentChange>();
            }

            _changesToNotify.Add(change);
            if (!PostChangeNotifications()) {
                // The notification wasn't posted yet, probably because a transaction is open.
                // But the Document, if any, needs to know right away so it can update its
                // currentRevision.
                var doc = DocumentCache.Get(change.DocumentId);
                if (doc != null) {
                    doc.RevisionAdded(change, false);
                }
            }

            // Squish the change objects if too many of them are piling up
            if (_changesToNotify.Count >= NOTIFY_CHANGES_LIMIT) {
                if (_changesToNotify.Count == NOTIFY_CHANGES_LIMIT) {
                    foreach (var c in _changesToNotify) {
                        c.ReduceMemoryUsage();
                    }
                } else {
                    change.ReduceMemoryUsage();
                }
            }
        }

        public string GenerateRevID(IEnumerable<byte> json, bool deleted, string previousRevisionId)
        {
            MessageDigest md5Digest;

            // Revision IDs have a generation count, a hyphen, and a UUID.
            int generation = 0;
            if (previousRevisionId != null) {
                generation = RevisionInternal.GenerationFromRevID(previousRevisionId);
                if (generation == 0) {
                    return null;
                }
            }

            // Generate a digest for this revision based on the previous revision ID, document JSON,
            // and attachment digests. This doesn't need to be secure; we just need to ensure that this
            // code consistently generates the same ID given equivalent revisions.
            try {
                md5Digest = MessageDigest.GetInstance("MD5");
            } catch (NoSuchAlgorithmException e) {
                throw new RuntimeException(e);
            }

            var length = 0;
            if (previousRevisionId != null) {
                var prevIDUTF8 = Encoding.UTF8.GetBytes(previousRevisionId);
                length = prevIDUTF8.Length;
            }

            if (length > unchecked((0xFF))) {
                return null;
            }

            var lengthByte = unchecked((byte)(length & unchecked((0xFF))));
            var lengthBytes = new[] { lengthByte };
            md5Digest.Update(lengthBytes);

            var isDeleted = deleted ? 1 : 0;
            var deletedByte = new[] { unchecked((byte)isDeleted) };
            md5Digest.Update(deletedByte);

            if (json != null)
            {
                md5Digest.Update(json != null ? json.ToArray() : null);
            }

            var md5DigestResult = md5Digest.Digest();
            var digestAsHex = BitConverter.ToString(md5DigestResult).Replace("-", String.Empty);
            int generationIncremented = generation + 1;
            return string.Format("{0}-{1}", generationIncremented, digestAsHex).ToLower();
        }

        public SymmetricKey EncryptionKey
        {
            get {
                throw new NotImplementedException();
            }
        }

        #pragma warning restore 1591
        #endregion
    }

    #region Global Delegates

    /// <summary>
    /// A delegate that can validate a key/value change.
    /// </summary>
    public delegate Boolean ValidateChangeDelegate(String key, Object oldValue, Object newValue);

    /// <summary>
    /// A delegate that can be run asynchronously on a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate void RunAsyncDelegate(Database database);

    /// <summary>
    /// A delegate that can be used to accept/reject new <see cref="Couchbase.Lite.Revision"/>s being added to a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate Boolean ValidateDelegate(Revision newRevision, IValidationContext context);

    /// <summary>
    /// A delegate that can be used to include/exclude <see cref="Couchbase.Lite.Revision"/>s during push <see cref="Couchbase.Lite.Replication"/>.
    /// </summary>
    public delegate Boolean FilterDelegate(SavedRevision revision, IDictionary<String, Object> filterParams);

    /// <summary>
    /// A delegate that can be run in a transaction on a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate Boolean RunInTransactionDelegate();

    ///
    /// <summary>The event raised when a <see cref="Couchbase.Lite.Database"/> changes</summary>
    ///
    public class DatabaseChangeEventArgs : EventArgs 
    {
        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> that raised the event.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Database"/> that raised the event.</value>
        public Database Source { get; internal set; }

        /// <summary>
        /// Returns true if the change was not made by a Document belonging to this Database 
        /// (e.g. it came from another process or from a pull Replication), otherwise false.
        /// </summary>
        /// <value>true if the change was not made by a Document belonging to this Database 
        /// (e.g. it came from another process or from a pull Replication), otherwise false</value>
        public Boolean IsExternal { get; internal set; }

        /// <summary>
        /// Gets the DocumentChange details for the Documents that caused the Database change.
        /// </summary>
        /// <value>The DocumentChange details for the Documents that caused the Database change.</value>
        public IEnumerable<DocumentChange> Changes { get; internal set; }
    }

    #endregion

    #region IFilterCompiler

    /// <summary>
    /// An interface for compiling filters on queries in languages other than C#
    /// </summary>
    public interface IFilterCompiler
    {
        /// <summary>
        /// Compiles the filter.
        /// </summary>
        /// <returns>The compiled filter.</returns>
        /// <param name="filterSource">The filter source code</param>
        /// <param name="language">The language that the source was written in</param>
        FilterDelegate CompileFilter(string filterSource, string language);
    }

    #endregion
}
