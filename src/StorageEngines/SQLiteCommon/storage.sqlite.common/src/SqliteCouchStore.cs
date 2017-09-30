//
//  SqliteCouchStore.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Text;
using System.Threading;

using Couchbase.Lite.Db;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Revisions;
using Couchbase.Lite.Storage.Internal;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;
using SQLitePCL;
using SQLitePCL.Ugly;

#if !NET_3_5
using StringEx = System.String;
#endif

#if SQLITE
namespace Couchbase.Lite.Storage.SystemSQLite
#elif CUSTOM_SQLITE
namespace Couchbase.Lite.Storage.CustomSQLite
#else
namespace Couchbase.Lite.Storage.SQLCipher
#endif
{
    /// <summary>
    /// This class will register this storage engine for use with Couchbase Lite
    /// </summary>
    public static class Plugin
    {

        /// <summary>
        /// Register this class for use as the storage engine for the SQLite storage type
        /// (be careful, once you set this you cannot change it)
        /// </summary>
        public static void Register()
        {
#if SQLITE && __ANDROID__
            var osVersion = global::Android.OS.Build.VERSION.SdkInt;
            if (global::Android.OS.Build.VERSION.SdkInt > global::Android.OS.BuildVersionCodes.M) {
                Log.To.Database.W("SqliteCouchStore",
                    $"SystemSQLite cannot be used on '{osVersion}' because of new Google restrictions in API 24+, if another " +
                    "storage engine is not registered this process will misbehave.");
            }
#endif
                Database.RegisterStorageEngine(StorageEngineTypes.SQLite, typeof(SqliteCouchStore));
        }
    }

    #if __IOS__
    [Foundation.Preserve(AllMembers = true)]
    #endif
    internal sealed class SqliteCouchStore : ICouchStore
    {
        #region Constants

        private const int SQLITE_MMAP_SIZE = 50 * 1024 * 1024;
        private const int DOC_ID_CACHE_SIZE = 1000;
        private const double SQLITE_BUSY_TIMEOUT = 5.0; //seconds

        private const string LOCAL_CHECKPOINT_DOC_ID = "CBL_LocalCheckpoint";
        private const string TAG = "SqliteCouchStore";
        private const string DB_FILENAME = "db.sqlite3";

        private const string SCHEMA = 
            // docs            
            "CREATE TABLE docs ( " +
            "        doc_id INTEGER PRIMARY KEY, " +
            "        docid TEXT UNIQUE NOT NULL); " +
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
            // views
            "    CREATE TABLE views ( " +
            "        view_id INTEGER PRIMARY KEY, " +
            "        name TEXT UNIQUE NOT NULL," +
            "        version TEXT, " +
            "        lastsequence INTEGER DEFAULT 0," +
            "        total_docs INTEGER DEFAULT -1); " +
            // info
            "    CREATE TABLE info (" +
            "        key TEXT PRIMARY KEY," +
            "        value TEXT);" +
            // version
            "    PRAGMA user_version = 17";

        #endregion

        #region Variables

        private static readonly int _SqliteVersion;

        private string _directory;
        private LruCache<string, object> _docIDs = new LruCache<string, object>(DOC_ID_CACHE_SIZE);
        private SymmetricKey _encryptionKey = null;
        private bool _readOnly;

        #endregion

        #region Properties

        internal ISQLiteStorageEngine StorageEngine { get; private set; }

        public bool IsOpen
        { 
            get
            {
                return StorageEngine != null && StorageEngine.IsOpen;
            }
        }

        public ICouchStoreDelegate Delegate { get; set; }
        public int MaxRevTreeDepth { get; set; }

        public bool AutoCompact { get; set; }

        public int DocumentCount { 
            get {
                return QueryOrDefault<int>(c => c.GetInt(0),
                     -1, "SELECT COUNT(DISTINCT doc_id) FROM revs WHERE current=1 AND deleted=0");
            }
        }

        public long LastSequence { 
            get {
                return QueryOrDefault<long>(c => c.GetLong(0),
                     0L, "SELECT seq FROM sqlite_sequence WHERE name='revs'");
            }
        }

        public bool InTransaction
        { 
            get {
                return StorageEngine.InTransaction;
            }
        }

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
                        Log.To.Database.I(TAG, "Other LastErrorCode {0}", StorageEngine.LastErrorCode);
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


        #endregion

        #region Constructors

        static SqliteCouchStore()
        {
            // Test the version of the actual SQLite implementation at runtime. Necessary because
            // the app might be linked with a custom version of SQLite (like SQLCipher) instead of the
            // system library, so the actual version/features may differ from what was declared in
            // sqlite3.h at compile time.
#if SQLCIPHER
#if __IOS__
            raw.SetProvider(new SQLite3Provider_internal());
#else
            raw.SetProvider(new SQLite3Provider_sqlcipher());
#endif
#elif CUSTOM_SQLITE
#if __IOS__
            raw.SetProvider(new SQLite3Provider_internal());
#else
            raw.SetProvider (new SQLite3Provider_cbsqlite());
#endif
#elif SQLITE && !__MOBILE__
            if(Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                raw.SetProvider(new SQLite3Provider_esqlite3());
            }
#endif
            Log.To.Database.I(TAG, "Initialized SQLite store (version {0} ({1}))", raw.sqlite3_libversion(), raw.sqlite3_sourceid());
            _SqliteVersion = raw.sqlite3_libversion_number();

            Debug.Assert(_SqliteVersion >= 3007000, String.Format("SQLite library is too old ({0}); needs to be at least 3.7", raw.sqlite3_libversion()));
        }

#endregion

#region Public Methods

        public void OptimizeSQLIndexes()
        {
            long currentSequence = LastSequence;
            if (currentSequence > 0) {
                long lastOptimized = long.Parse(GetInfo("last_optimized") ?? "0");
                if (lastOptimized <= currentSequence / 10) {
                    RunInTransaction(() =>
                    {
                        Log.To.Database.I(TAG, "Optimizing SQL indexes (curSeq={0}, last run at {1})", currentSequence, lastOptimized);
                        StorageEngine.ExecSQL("ANALYZE");
                        StorageEngine.ExecSQL("ANALYZE sqlite_master");
                        SetInfo("last_optimized", currentSequence.ToString());
                        return true;
                    });
                }
            }
        }

        public void RunStatements(string sqlStatements)
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
                    } catch(CouchbaseLiteException) {
                        Log.To.Database.E(TAG, "Error running statement '{0}', rethrowing", 
                            new SecureLogString(statement, LogMessageSensitivity.PotentiallyInsecure));
                        throw;
                    } catch(Exception e) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                            "Error running statement '{0}'", 
                            new SecureLogString(statement, LogMessageSensitivity.PotentiallyInsecure));
                    }

                }
            }
        }

        public IDictionary<string, object> GetDocumentProperties(IEnumerable<byte> json, string docId, RevisionID revId, bool deleted, long sequence)
        {
            var realizedJson = json.ToArray();
            IDictionary<string, object> docProperties;
            if (realizedJson.Length == 0 || (realizedJson.Length == 2 && Encoding.UTF8.GetString(realizedJson) == "{}")) {
                docProperties = new Dictionary<string, object>();
            } else {
                try {
                    docProperties = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(realizedJson);
                } catch(CouchbaseLiteException) {
                    Log.To.Database.W(TAG, "Unparseable JSON for doc={0}, rev={1}: {2}, returning skeleton set", 
                        new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure),
                        revId, 
                        new SecureLogString(realizedJson, LogMessageSensitivity.PotentiallyInsecure));
                    docProperties = new Dictionary<string, object>();
                }
            }

            docProperties.SetDocRevID(docId, revId);
            if (deleted) {
                docProperties["_deleted"] = true;
            }

            return docProperties;
        }

        public RevisionInternal GetDocument(string docId, long sequence)
        {
            RevisionInternal result = null;
            TryQuery(c =>
            {
                var revId = c.GetString(0).AsRevID();
                bool deleted = c.GetInt(1) != 0;
                result = new RevisionInternal(docId, revId, deleted);
                result.Sequence = sequence;
                result.SetBody(new Body(c.GetBlob(2)));

                return false;
            }, "SELECT revid, deleted, json FROM revs WHERE sequence=?", sequence);

            return result;
        }

        public RevisionInternal GetRevision(string docId, RevisionID revId, bool deleted, long sequence, IEnumerable<byte> json)
        {
            var rev = new RevisionInternal(docId, revId, deleted);
            rev.Sequence = sequence;
            if (json != null) {
                rev.SetBody(new Body(json));
            }

            return rev;
        }

        public Status TryQuery(Func<Cursor, bool> action, string sqlQuery, params object[] args)
        {
            Cursor c = null;
            try {
                c = StorageEngine.RawQuery (sqlQuery, args);

                var retVal = new Status(StatusCode.NotFound);
                while(c.MoveToNext()) {
                    retVal.Code = StatusCode.Ok;
                    if(!action(c)) {
                        break;
                    }
                }

                return retVal;
            } catch(Exception e) {
                Log.To.Database.E(TAG, "Error executing SQL query, returning DbError status", e);
            } finally {
                if (c != null) {
                    c.Dispose();
                }
            }

            return new Status(StatusCode.DbError);
        }

        public T QueryOrDefault<T>(Func<Cursor, T> action, T defaultVal, string sqlQuery, params object[] args)
        {
            T retVal = defaultVal;
            var success = TryQuery(c => {
                retVal = action(c);
                return false;
            }, sqlQuery, args);
            if(success.IsError) {
                return defaultVal;
            }

            return retVal;
        }

#endregion

#region Internal Methods

        internal IDictionary<string, object> GetRevisionHistoryDictStartingFromAnyAncestor(RevisionInternal rev, IList<RevisionID>ancestorRevIDs)
        {
            var history = GetRevisionHistory(rev, null); // This is in reverse order, newest ... oldest
            if (ancestorRevIDs != null && ancestorRevIDs.Any())
            {
                for (var i = 0; i < history.Count; i++)
                {
                    if (ancestorRevIDs.Contains(history[i]))
                    {
                        var newHistory = new List<RevisionID>();
                        for (var index = 0; index < i + 1; index++) 
                        {
                            newHistory.Add(history[index]);
                        }
                        history = newHistory;
                        break;
                    }
                }
            }

            return TreeRevisionID.MakeRevisionHistoryDict(history);
        }

#endregion

#region Private Methods

        private static string JoinQuotedObjects(IEnumerable<Object> objects)
        {
            var strings = new List<String>();
            foreach (var obj in objects)
            {
                strings.Add(obj != null ? obj.ToString() : null);
            }
            return Utility.JoinQuoted(strings);
        }

        private int PruneDocument(long docNumericID, int minGenToKeep)
        {
            const string sql = "DELETE FROM revs WHERE doc_id=? AND revid < ? AND current=0 AND" +
                "sequence NOT IN (SELECT parent FROM revs WHERE doc_id=? AND current=1)";
            var minGen = String.Format("{0}-", minGenToKeep);
            try {
                var retVal = StorageEngine?.ExecSQL(sql, docNumericID, minGen, docNumericID);
                return retVal.HasValue ? retVal.Value : 0;
            } catch(Exception) {
                Log.To.Database.W(TAG, "SQLite error {0} pruning generations < {1} of doc {2}", StorageEngine?.LastErrorCode, minGenToKeep, docNumericID);
            }

            return 0;
        }

        private void Open()
        {
            if (IsOpen) {
                return;
            }

            // Create the storage engine.
            StorageEngine = SQLiteStorageEngineFactory.CreateStorageEngine();

            // Try to open the storage engine and stop if we fail.
            if (!Directory.Exists(_directory)) {
                Directory.CreateDirectory(_directory);
            }

            var path = Path.Combine(_directory, DB_FILENAME);
            if (StorageEngine == null || !StorageEngine.Open(path, _readOnly, SCHEMA, _encryptionKey)) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                    "Unable to create a SQLite storage engine");
            }

            // Stuff we need to initialize every time the sqliteDb opens:
            try {
                RunStatements("PRAGMA foreign_keys = ON; PRAGMA journal_mode=WAL;");

                // Check the user_version number we last stored in the sqliteDb:
                var dbVersion = StorageEngine.GetVersion();
                bool isNew = dbVersion == 17;
                if (isNew) {
                    RunStatements("BEGIN TRANSACTION");
                }

                // Incompatible version changes increment the hundreds' place:
                if (dbVersion >= 200) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                        "Database version ({0}) is newer (>= 200) than I know how to work with", dbVersion);
                }

                if (dbVersion < 17) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                        "Database version ({0}) is older than I know how to work with", dbVersion);
                }

                if (dbVersion < 18) {
                    const string upgradeSql = "ALTER TABLE revs ADD COLUMN doc_type TEXT;" +
                        "PRAGMA user_version = 18";

                    RunStatements(upgradeSql);
                    dbVersion = 18;
                }

                if (dbVersion < 101) {
                    const string upgradeSql = "PRAGMA user_version = 101";
                    RunStatements(upgradeSql);
                    dbVersion = 101;
                }

                if(dbVersion < 102) {
                    const string upgradeSql = "ALTER TABLE docs ADD COLUMN expiry_timestamp INTEGER;" +
                        "CREATE INDEX IF NOT EXISTS docs_expiry ON docs(expiry_timestamp)" +
                        "WHERE expiry_timestamp not null;" +
                        "PRAGMA user_version = 102";
                    RunStatements(upgradeSql);
                    dbVersion = 102;
                }

                if (isNew) {
                    RunStatements("END TRANSACTION");
                    SetInfo("pruned", "true"); // See Compact for explanation
                }

                if (!isNew && !_readOnly) {
                    OptimizeSQLIndexes();
                }
            } catch(CouchbaseLiteException) {
                Log.To.Database.E(TAG, "Error initializing the SQLite storage engine, rethrowing...");
                StorageEngine.Close();
                throw;
            } catch(Exception e) {
                StorageEngine.Close();
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                    "Error initializing SQLite storage engine");
            }
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

                    var minGenRevId = cursor.GetString(1).AsRevID();
                    var maxGenRevId = cursor.GetString(2).AsRevID();

                    minGen = minGenRevId.Generation;
                    maxGen = maxGenRevId.Generation;

                    if ((maxGen - minGen + 1) > maxDepth) {
                        toPrune[docNumericID] = (maxGen - minGen);
                    }
                }

                if (toPrune.Count == 0) {
                    return 0;
                }

                RunInTransaction(() =>
                {
                    foreach (var pair in toPrune) {
                        outPruned += PruneDocument(pair.Key, pair.Value);
                    }

                    return true;
                });
            } catch (Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, "Error pruning database");
            } finally {
                if (cursor != null) {
                    cursor.Close();
                }
            }

            return outPruned;
        }

        internal long GetDocNumericID(string docId)
        {
            long docNumericId = 0L;
            var success = TryQuery(c =>
            {
                docNumericId = c.GetLong(0);
                return false;
            }, "SELECT doc_id FROM docs WHERE docid=?", docId);

            if (success.Code == StatusCode.DbError) {
                return -1L;
            }

            if (success.Code == StatusCode.NotFound) {
                return 0L;
            }

            return docNumericId;
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

        private bool SequenceHasAttachments(long sequence)
        {
            return QueryOrDefault<bool>(c => c.GetInt(0) != 0, false, 
                "SELECT no_attachments=0 FROM revs WHERE sequence=?", sequence);
        }

        private RevisionInternal RevisionWithDocID(string docId, RevisionID revId, bool deleted, long sequence, IEnumerable<byte> json)
        {
            var rev = new RevisionInternal(docId, revId, deleted);
            rev.Sequence = sequence;
            if (json != null) {
                rev.SetJson(json);
            }

            return rev;
        }

        private long GetSequenceOfDocument(long docNumericId, RevisionID revId, bool onlyCurrent)
        {
            var sql = String.Format("SELECT sequence FROM revs WHERE doc_id=? AND revid=? {0} LIMIT 1",
                          (onlyCurrent ? "AND current=1" : ""));

            return QueryOrDefault<long>(c => c.GetLong(0), 0L, sql, docNumericId, revId.ToString());
        }

        private bool DocumentExists(string docId, RevisionID revId)
        {
            return GetDocument(docId, revId, false) != null;
        }

        private long InsertRevision(RevisionInternal rev, long docNumericId, long parentSequence, bool current, bool hasAttachments,
            IEnumerable<byte> json, string docType)
        {
            var vals = new ContentValues();
            vals["doc_id"] = docNumericId;
            vals["revid"] = rev.RevID.ToString();
            if (parentSequence != 0) {
                vals["parent"] = parentSequence;
            }

            vals["current"] = current;
            vals["deleted"] = rev.Deleted;
            vals["no_attachments"] = !hasAttachments;
            if (json != null) {
                vals["json"] = json;
            }

            if (docType != null) {
                vals["doc_type"] = docType;
            }
                
            var row = StorageEngine.Insert("revs", null, vals);
            rev.Sequence = row;
            return row;
        }

        private RevisionID GetWinner(long docNumericId, RevisionID oldWinnerRevId, bool oldWinnerWasDeletion, RevisionInternal newRev)
        {
            var newRevID = newRev.RevID;
            if (oldWinnerRevId == null) {
                return newRevID;
            }

            if (!newRev.Deleted) {
                if (oldWinnerWasDeletion || newRevID.CompareTo(oldWinnerRevId) > 0) {
                    return newRevID; // this is now the winning live revision
                }
            } else if (oldWinnerWasDeletion) {
                if (newRevID.CompareTo(oldWinnerRevId) > 0) {
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

        private HashSet<long> LosingSequences(long since)
        {
            var retVal = new HashSet<long>();
            var sql = "SELECT sequence, revs.doc_id, docid, revid, deleted FROM revs, docs " +
                "WHERE sequence > ? AND current=1 " +
                "AND revs.doc_id = docs.doc_id " +
                "ORDER BY revs.doc_id, deleted, revid DESC";
            long lastDocId = 0L;
            using(var c = StorageEngine.RawQuery(sql, since)) {
                while(c.MoveToNext()) {
                    var docNumericId = c.GetLong(1);
                    if(docNumericId == lastDocId) {
                        retVal.Add(c.GetLong(0));
                    }

                    lastDocId = docNumericId;
                }
            }

            return retVal;  
        }

        internal RevisionID GetWinner(long docNumericId, ValueTypePtr<bool> outDeleted, ValueTypePtr<bool> outConflict)
        {
            Debug.Assert(docNumericId > 0);
            RevisionID revId = null;
            outDeleted.Value = false;
            outConflict.Value = false;
            TryQuery(c =>
            {
                revId = c.GetString(0).AsRevID();
                outDeleted.Value = c.GetInt(1) != 0;
                // The document is in conflict if there are two+ result rows that are not deletions.
                outConflict.Value = !outDeleted && c.MoveToNext() && c.GetInt(1) == 0;
                return false;
            }, "SELECT revid, deleted FROM revs WHERE doc_id=? and current=1 ORDER BY deleted asc, revid desc LIMIT ?",
                docNumericId, (!outConflict.IsNull ? 2 : 1));

            return revId;
        }

        private RevisionList GetAllDocumentRevisions(string docId, long docNumericId, bool onlyCurrent, bool includedDeleted)
        {
            StringBuilder sql = new StringBuilder("SELECT sequence, revid, deleted FROM revs WHERE doc_id=?");
            if (onlyCurrent) {
                sql.Append (" and current");
            }

            if (!includedDeleted) {
                sql.Append (" and deleted=0");
            }

            sql.Append (" ORDER BY sequence DESC");

            var revs = new RevisionList();
            var innerStatus = TryQuery(c =>
            {
                var rev = new RevisionInternal(docId, c.GetString(1).AsRevID(), c.GetInt(2) != 0);
                rev.Sequence = c.GetLong(0);
                revs.Add(rev);

                return true;
            }, sql.ToString(), docNumericId);
                
            if (innerStatus.IsError && innerStatus.Code != StatusCode.NotFound) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, innerStatus.Code, TAG,
                    "Error getting document revisions");
            }

            return revs;
        }

        internal IEnumerable<byte> EncodeDocumentJSON(RevisionInternal rev)
        {
            var originalProps = rev.GetProperties();
            if (originalProps == null) {
                return null;
            }

            var properties = Database.StripDocumentJSON(originalProps);

            // Create canonical JSON -- this is important, because the JSON data returned here will be used
            // to create the new revision ID, and we need to guarantee that equivalent revision bodies
            // result in equal revision IDs.
            return Manager.GetObjectMapper().WriteValueAsBytes(properties, true);
        }

        private RevisionInternal PutLocalRevisionNoMvcc(RevisionInternal rev)
        {
            RevisionInternal result = null;
            RunInTransaction(() =>
            {
                RevisionInternal prevRev = GetLocalDocument(rev.DocID, null);
                result = PutLocalRevision(rev, prevRev == null ? null : prevRev.RevID, true);

                return true;
            });

            return result;
        }

        private Status DeleteLocalRevision(string docId, RevisionID revId)
        {
            if (revId == null) {
                // Didn't specify a revision to delete: kCBLStatusNotFound or a kCBLStatusConflict, depending
                return GetLocalDocument(docId, null) != null ? new Status(StatusCode.Conflict) : new Status(StatusCode.NotFound);
            }

            var changes = 0;
            try {
                changes = StorageEngine.Delete("localdocs", "docid=? AND revid=?", docId, revId.ToString());
            } catch(Exception) {
                return new Status(StatusCode.DbError);
            }

            if (changes == 0) {
                return GetLocalDocument(docId, null) != null ? new Status(StatusCode.Conflict) : new Status(StatusCode.NotFound);
            }

            return new Status(StatusCode.Ok);
        }

        private void InvalidateDocNumericID(string docID)
        {
            _docIDs.Remove(docID);
        }

        private void NotifyPurgedDocument(string docID)
        {
            Delegate?.DatabaseStorageChanged(new DocumentChange(docID));
        }

#endregion

#region ICouchStore
#pragma warning disable 1591

        public IDatabaseUpgrader CreateUpgrader(Database upgradeTo, string upgradeFrom)
        {
            return DatabaseUpgraderFactory.CreateUpgrader(upgradeTo, upgradeFrom);
        }

        public bool DatabaseExistsIn(string directory)
        {
            return File.Exists(Path.Combine(directory, DB_FILENAME));
        }

        public void Open(string directory, Manager manager, bool readOnly)
        {
            _directory = directory;
            _readOnly = readOnly;
            Open();
        }

        public void Close()
        {
            if (StorageEngine != null && StorageEngine.IsOpen) {
                StorageEngine.Close();
                _docIDs.Dispose();
            }
        }

        public void SetInfo(string key, string info)
        {
            var vals = new ContentValues(2);
            vals["key"] = key;
            vals["value"] = info;
            try {
                StorageEngine.InsertWithOnConflict("info", null, vals, ConflictResolutionStrategy.Replace);
            } catch(CouchbaseLiteException) {
                Log.To.Database.E(TAG, "Failed to set info ({0} -> {1}), rethrowing...", 
                    new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure), 
                    new SecureLogString(info, LogMessageSensitivity.PotentiallyInsecure));
                throw;
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                    "Error setting info ({0} -> {1})", 
                    new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure), 
                    new SecureLogString(info, LogMessageSensitivity.PotentiallyInsecure));
            }
        }

        public string GetInfo(string key)
        {
            string retVal = null;
            var success = TryQuery(c => {
                retVal = c.GetString(0);
                return false;
            }, "SELECT value FROM info WHERE key=?", key);

            return success.IsError ? null : retVal;
        }

        public void Compact()
        {
            // Can't delete any rows because that would lose revision tree history.
            // But we can remove the JSON of non-current revisions, which is most of the space.
            try {
                // Bulk pruning is no longer needed, because revisions are pruned incrementally as new
                // ones are added. But databases from before this feature was added (1.3) may have documents
                // that need pruning. So we'll do a one-time bulk prune, then set a flag indicating that
                // it isn't needed anymore.
                if(GetInfo("pruned") == null) {
                    PruneRevsToMaxDepth(MaxRevTreeDepth);
                    SetInfo("pruned", "true");
                }

                Log.To.Database.I(TAG, "Deleting JSON of old revisions...");
                var args = new ContentValues();
                args["json"] = null;
                args["doc_type"] = null;
                args["no_attachments"] = 1;
                var result = StorageEngine.Update("revs", args, "current=0", null);
                Log.To.Database.I (TAG, "...Deleted {0} revisions", result);
            } catch(CouchbaseLiteException) {
                Log.To.Database.E(TAG, "Error compacting old JSON, rethrowing...");
                throw;
            } catch (Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, 
                    "Error compacting old JSON");
            }

            try {
                Log.To.Database.V(TAG, "Flushing SQLite WAL...");
                StorageEngine.ExecSQL("PRAGMA wal_checkpoint(RESTART)");
                Log.To.Database.V(TAG, "Vacuuming SQLite sqliteDb...");
                StorageEngine.ExecSQL("VACUUM");
            } catch(CouchbaseLiteException) {
                Log.To.Database.E(TAG, "Error vacuuming Sqlite DB, rethrowing...");
                throw;
            } catch (Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, 
                    "Error vacuuming Sqlite DB");
            }

            Log.To.Database.I(TAG, "...Finished database compaction.");
        }

        public bool RunInTransaction(RunInTransactionDelegate block)
        {
            var retVal = StorageEngine.RunInTransaction(block);
            if(!retVal) {
                _docIDs.Clear(); // State of DB unknown, cache is now invalid
            }

            if (!StorageEngine.InTransaction) {
                Delegate?.StorageExitedTransaction(retVal);
            }

            return retVal;
        }

        public void SetEncryptionKey(SymmetricKey key)
        {
#if !ENCRYPTION
            Log.To.Database.E(TAG, "This store does not support encryption, throwing...");
            throw new InvalidOperationException("This store does not support encryption");
#else
            _encryptionKey = key;
#endif
        }

        public AtomicAction ActionToChangeEncryptionKey(SymmetricKey newKey)
        {
#if !ENCRYPTION
            Log.To.Database.E(TAG, "This store does not support encryption, throwing...");
            throw new InvalidOperationException("This store does not support encryption");
#else
            // https://www.zetetic.net/sqlcipher/sqlcipher-api/index.html#sqlcipher_export

            var action = new AtomicAction();
            var dbWasClosed = false;
            var tempPath = default(string);

            // Make a path for a temporary database file:
            tempPath = Path.Combine(Path.GetTempPath(), Misc.CreateGUID());
            action.AddLogic(null, () => File.Delete(tempPath), null);

            // Create & attach a temporary database encrypted with the new key:
            action.AddLogic(() =>
            {
                string sql;
                if(newKey != null) {
                    sql = String.Format("ATTACH DATABASE ? AS rekeyed_db KEY \"x'{0}'\"", newKey.HexData);
                } else {
                    sql = "ATTACH DATABASE ? AS rekeyed_db KEY ''";
                }

                StorageEngine.ExecSQL(sql, tempPath);
            }, () =>
            {
                if(dbWasClosed) {
                    return;
                }

                StorageEngine.ExecSQL("DETACH DATABASE rekeyed_db");
            });

            // Export the current database's contents to the new one:
            action.AddLogic(() => 
            {
                StorageEngine.ExecSQL("SELECT sqlcipher_export('rekeyed_db')");
                var version = QueryOrDefault<int>(c => c.GetInt(0), 0, "PRAGMA user_version");
                StorageEngine.ExecSQL(String.Format("PRAGMA rekeyed_db.user_version = {0}", version));
            }, null, null);

            // Close the database (and re-open it on cleanup):
            action.AddLogic(() =>
            {
                StorageEngine.Close();
                dbWasClosed = true;
            }, () =>
            {
                Open();
            }, () =>
            {
                SetEncryptionKey(newKey);
                Open();
            });

            // Overwrite the old db file with the new one:
            action.AddLogic (AtomicAction.DeleteFile (Path.Combine (_directory, DB_FILENAME + "-wal")));
            action.AddLogic (AtomicAction.DeleteFile (Path.Combine (_directory, DB_FILENAME + "-shm")));
            action.AddLogic(AtomicAction.MoveFile(tempPath, Path.Combine(_directory, DB_FILENAME)));

            return action;
#endif
        }

        public RevisionInternal GetDocument(string docId, RevisionID revId, bool withBody, Status outStatus = null)
        {
            if (outStatus == null) {
                outStatus = new Status();
            }

            long docNumericId = GetDocNumericID(docId);
            if (docNumericId <= 0L) {
                outStatus.Code = StatusCode.NotFound;
                return null;
            }

            RevisionInternal result = null;
            var sb = new StringBuilder("SELECT revid, deleted, sequence");
            if (withBody) {
                sb.Append(", json");
            } else {
                sb.Append(", json is not null");
            }

            if (revId != null) {
                sb.Append(" FROM revs WHERE revs.doc_id=? AND revid=? LIMIT 1");
            } else {
                sb.Append(" FROM revs WHERE revs.doc_id=? and current=1 ORDER BY deleted ASC, revid DESC LIMIT 1");
            }
                
            var transactionStatus = TryQuery(c =>
            {
                var revIDToUse = revId;
                if(revIDToUse == null) {
                    revIDToUse = c.GetString(0).AsRevID();
                }

                bool deleted = c.GetInt(1) != 0;
                if (revId != null || !deleted) {
                    result = new RevisionInternal(docId, revIDToUse, deleted);
                    result.Sequence = c.GetLong(2);
                    if (withBody) {
                        result.SetJson(c.GetBlob(3));
                    } else {
                        result.Missing = c.GetInt(3) == 0;
                    }
                }

                return revId == null && deleted;
            }, sb.ToString(), docNumericId, revId?.ToString());

            if (transactionStatus.IsError) {
                if (transactionStatus.Code == StatusCode.NotFound) {
                    outStatus.Code = StatusCode.NotFound;
                    return null;
                } else {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, outStatus.Code, TAG,
                        "Error during transaction in GetDocument()");
                }
            }

            outStatus.Code = result == null || result.Deleted ? StatusCode.Deleted : StatusCode.Ok;

            return result;
        }

        public void LoadRevisionBody(RevisionInternal rev)
        {
            if (rev.GetBody() != null && rev.Sequence != 0) {
                // no-op
                return;
            }
                
            Debug.Assert(rev.DocID != null && rev.RevID != null);
            var docNumericId = GetDocNumericID(rev.DocID);
            if (docNumericId <= 0L) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotFound, TAG,
                    "Cannot load body of {0} because it doesn't exist", rev);
            }

            var status = TryQuery(c =>
            {
                var json = c.GetBlob(1);
                if(json != null) {
                    rev.Sequence = c.GetLong(0);
                    rev.SetJson(json);
                }

                return false;
            }, "SELECT sequence, json FROM revs WHERE doc_id=? AND revid=? LIMIT 1", docNumericId, rev.RevID.ToString());

            if (status.IsError) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, status.Code, TAG,
                    "Error during SQLite query");
            }
        }

        public long GetRevisionSequence(RevisionInternal rev)
        {
            var docNumericId = GetDocNumericID(rev.DocID);
            if (docNumericId <= 0L) {
                return 0L;
            }

            return QueryOrDefault<long>(c => c.GetLong(0), 0L, "SELECT sequence FROM revs WHERE doc_id=? AND revid=? LIMIT 1", docNumericId, rev.RevID.ToString());
        }

        public DateTime? NextDocumentExpiry()
        {
            var result = QueryOrDefault<long?>(c => c.GetLong(0), null, "SELECT expiry_timestamp FROM " +
                "docs WHERE expiry_timestamp IS NOT NULL ORDER BY expiry_timestamp ASC LIMIT 1");
            if(result == null) {
                return null;
            }

            return Misc.OffsetFromEpoch(TimeSpan.FromSeconds(result.Value));
        }

        public DateTime? GetDocumentExpiration(string documentId)
        {
            var docNumericId = GetDocNumericID(documentId);
            if(docNumericId <= 0L) {
                return null;
            }

            var result = QueryOrDefault<long?>(c => c.GetLong(0), null, "SELECT expiry_timestamp FROM docs WHERE doc_id=? AND expiry_timestamp IS NOT NULL", docNumericId);
            if(result == null) {
                return null;
            }

            return Misc.OffsetFromEpoch(TimeSpan.FromSeconds(result.Value));
        }

        public void SetDocumentExpiration(string documentId, DateTime? expiration)
        {
            var docNumericId = GetDocNumericID(documentId);
            if(docNumericId <= 0L) {
                var msg = String.Format("Unable to find document {0} in SetRevisionExpiration",
                    new SecureLogString(documentId, LogMessageSensitivity.PotentiallyInsecure));
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG, msg);
            }

            if(expiration.HasValue) {
                StorageEngine.ExecSQL("UPDATE docs SET expiry_timestamp=? WHERE doc_id=?", expiration.Value,
                    docNumericId);
            } else {
                StorageEngine.ExecSQL("UPDATE docs SET expiry_timestamp=null WHERE doc_id=?",
                    docNumericId);
            }
        }

        public RevisionInternal GetParentRevision(RevisionInternal rev)
        {
            // First get the parent's sequence:
            var seq = rev.Sequence;
            if (seq != 0) {
                seq = QueryOrDefault<long>(c => c.GetLong(0), 0L, "SELECT parent FROM revs WHERE sequence=?", seq);
            } else {
                var docNumericId = GetDocNumericID(rev.DocID);
                if (docNumericId == 0L) {
                    return null;
                }

                seq = QueryOrDefault<long>(c => c.GetLong(0), 0L, "SELECT parent FROM revs WHERE doc_id=? and revid=?", docNumericId, rev.RevID.ToString());
            }

            if (seq == 0) {
                return null;
            }

            // Now get its revID and deletion status:
            RevisionInternal result = null;
            TryQuery(c =>
            {
                result = new RevisionInternal(rev.DocID, c.GetString(0).AsRevID(), c.GetInt(1) != 0);
                result.Sequence = seq;
                return false;
            }, "SELECT revid, deleted FROM revs WHERE sequence=?", seq);

            return result;
        }

        public IList<RevisionID> GetRevisionHistory(RevisionInternal rev, ICollection<RevisionID> ancestorRevIds)
        {
            string docId = rev.DocID;
            var revId = rev.RevID;
            Debug.Assert(docId != null && revId != null);

            var docNumericId = GetDocNumericID(docId);
            if (docNumericId < 0) {
                return null;
            }

            if (docNumericId == 0) {
                return new List<RevisionID>(0);
            }

            var lastSequence = 0L;
            var history = new List<RevisionID>();
            var status = TryQuery(c =>
            {
                var sequence = c.GetLong(0);
                bool matches;
                if(lastSequence == 0) {
                    matches = revId == c.GetString(2).AsRevID();
                } else {
                    matches = lastSequence == sequence;
                }

                if(matches) {
                    var nextRevId = c.GetString(2).AsRevID();
                    history.Add(nextRevId);
                    lastSequence = c.GetLong(1);
                    if(lastSequence == 0) {
                        return false;
                    }

                    if(ancestorRevIds != null && ancestorRevIds.Contains(revId)) {
                        return false;
                    }
                }

                return true;
            }, "SELECT sequence, parent, revid" +
            " FROM revs WHERE doc_id=? ORDER BY sequence DESC", docNumericId);

            if (status.IsError) {
                return null;
            }

            return history;
        }

        public RevisionList GetAllDocumentRevisions(string docId, bool onlyCurrent, bool includeDeleted)
        {
            var docNumericId = GetDocNumericID(docId);
            if (docNumericId < 0) {
                return null;
            }

            if (docNumericId == 0) {
                return new RevisionList(); // no such document
            }

            return GetAllDocumentRevisions(docId, docNumericId, onlyCurrent, includeDeleted);
        }

        public IEnumerable<RevisionID> GetPossibleAncestors(RevisionInternal rev, int limit, ValueTypePtr<bool> haveBodies,
            bool withBodiesOnly)
        {
            int generation = rev.Generation;
            if (generation <= 1L) {
                return new List<RevisionID>();
            }

            long docNumericId = GetDocNumericID(rev.DocID);
            if (docNumericId <= 0L) {
                return new List<RevisionID>();
            }

            int sqlLimit = limit > 0 ? limit : -1;
            haveBodies.Value = true;
            string sql;
            if (withBodiesOnly) {
                sql = "SELECT revid, json is not null, json FROM revs " +
                      "WHERE doc_id=? AND current=? AND revid < ? " +
                      "ORDER BY revid DESC LIMIT ?";
            } else {
                sql = "SELECT revid, json is not null FROM revs " +
                                   "WHERE doc_id=? AND current=? AND revid < ? " +
                                   "ORDER BY revid DESC LIMIT ?";
            }

            // First look only for current revisions; if none match, go to non-current ones.
            var revIDs = new List<RevisionID>();
            for(int current = 1; current >= 0; current--) {
                var status = TryQuery(c =>
                {
                    if (c.GetInt(1) == 0) {
                        if (haveBodies) {
                            haveBodies.Value = false;
                        }

                        if (withBodiesOnly) {
                            return true;
                        }
                    } else if(withBodiesOnly) {
                        var body = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(c.GetBlob(2));
                        if (body.ContainsKey("_removed")) {
                            return true; // Skip _removed
                        }
                    }
                    revIDs.Add(c.GetString(0).AsRevID());
                    

                    return true;
                }, sql, docNumericId, current, $"{generation}-", sqlLimit);

                if(status.Code != StatusCode.NotFound && status.IsError) {
                    return null;
                }

                if (revIDs.Count > 0) {
                    return revIDs;
                }
            }

            return null;
        }

        public RevisionID FindCommonAncestor(RevisionInternal rev, IEnumerable<RevisionID> revIds)
        {
            if (revIds == null || !revIds.Any()) {
                return null;
            }

            var docNumericId = GetDocNumericID(rev.DocID);
            if (docNumericId <= 0) {
                return null;
            }

            var sql = String.Format("SELECT revid FROM revs " +
                "WHERE doc_id=? and revid in ({0}) and revid <= ? " +
                "ORDER BY revid DESC LIMIT 1", Utility.JoinQuoted(revIds.Select(x => x.ToString())));

            return QueryOrDefault(c => c.GetString(0), null, sql, docNumericId, rev.RevID.ToString()).AsRevID();
        }

        public int FindMissingRevisions(RevisionList revs)
        {
            if (!revs.Any()) {
                return 0;
            }

            var sql = String.Format("SELECT docid, revid FROM revs, docs " +
                      "WHERE revid in ({0}) AND docid IN ({1}) " +
                "AND revs.doc_id == docs.doc_id", Utility.JoinQuoted(revs.GetAllRevIds().Select(x => x.ToString())), Utility.JoinQuoted(revs.GetAllDocIds()));

            int count = 0;
            var status = TryQuery(c =>
            {
                var rev = revs.RevWithDocIdAndRevId(c.GetString(0), c.GetString(1));
                if(rev != null) {
                    while (revs.Contains (rev)) {
                        count++;
                        revs.Remove (rev);
                    }
                }

                return true;
            }, sql);

            return status.IsSuccessful ? count : 0;
        }

        public ICollection<BlobKey> FindAllAttachmentKeys()
        {
            var allKeys = new HashSet<BlobKey>();
            var status = TryQuery(c =>
            {
                var rev = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(c.GetBlob(0));
                foreach(var pair in rev["_attachments"].AsDictionary<string, object>()) {
                    var attachmentDict = pair.Value.AsDictionary<string, object>();
                    if(attachmentDict == null) {
                        Log.To.Database.W(TAG, "Invalid attachment found, not a dictionary, skipping...");
                        continue;
                    }

                    var digest = attachmentDict.GetCast<string>("digest");
                    if(digest == null) {
                        Log.To.Database.W(TAG, "Invalid attachment found, no digest, skipping...");
                        continue;
                    }

                    var blobKey = new BlobKey(digest);
                    allKeys.Add(blobKey);
                }

                return true;
            }, "SELECT json FROM revs WHERE no_attachments != 1");

            return status.IsError && status.Code != StatusCode.NotFound ? null : allKeys;
        }

        public IEnumerable<QueryRow> GetAllDocs(QueryOptions options)
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

                sql.AppendFormat(" revs.doc_id IN (SELECT doc_id FROM docs WHERE docid IN ({0})) AND", JoinQuotedObjects(options.Keys));
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
                args.Add(Misc.KeyForPrefixMatch(maxKey, options.PrefixMatchLevel));
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
                    var revId = c.GetString(2);
                    long sequence = c.GetLong(3);
                    bool deleted = includeDeletedDocs && c.GetInt(includeDocs ? 6 : 4) != 0;

                    RevisionInternal docRevision = null;
                    if(includeDocs) {
                        // Fill in the document contents:
                        docRevision = RevisionWithDocID(docId, revId.AsRevID(), deleted, sequence, c.GetBlob(4));
                        Debug.Assert(docRevision != null);
                    }

                    // Iterate over following rows with the same doc_id -- these are conflicts.
                    // Skip them, but collect their revIDs if the 'conflicts' option is set:
                    List<string> conflicts = null;
                    while((keepGoing = c.MoveToNext()) && c.GetLong(0) == docNumericId) {
                        if(options.AllDocsMode >= AllDocsMode.ShowConflicts) {
                            if(conflicts == null) {
                                conflicts = new List<string>();
                                conflicts.Add(revId.ToString());
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
            } catch(Exception e) {
                Log.To.Database.W(TAG, "Error in all docs query, returning null...", e);
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
                            var revId = GetWinner(docNumericId, deleted, ValueTypePtr<bool>.NULL);
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

        public RevisionList ChangesSince(long lastSequence, ChangesOptions options, RevisionFilter filter)
        {
            // http://wiki.apache.org/couchdb/HTTP_database_API#Changes

            bool includeDocs = options.IncludeDocs || filter != null;
            var sql = String.Format("SELECT sequence, revs.doc_id, docid, revid, deleted {0} FROM revs " +
                "JOIN docs ON docs.doc_id = revs.doc_id " +
                "WHERE sequence > ? AND +current=1 " +
                "ORDER BY +revs.doc_id, +deleted, revid DESC",
                (includeDocs ? @", json" : @""));

            var changes = new RevisionList();
            long lastDocId = 0L;

            var returned = 0;
            TryQuery(c =>
            {
                if(!options.IncludeConflicts) {
                    // Only count the first rev for a given doc (the rest will be losing conflicts):
                    var docNumericId = c.GetLong(1);
                    if(docNumericId == lastDocId) {
                        return true;
                    }

                    lastDocId = docNumericId;
                }

                string docId = c.GetString(2);
                var revId = c.GetString(3).AsRevID();
                bool deleted = c.GetInt(4) != 0;
                var rev = new RevisionInternal(docId, revId, deleted);
                rev.Sequence = c.GetLong(0);
                if(includeDocs) {
                    rev.SetJson(c.GetBlob(5));
                }

                if((filter == null || filter(rev)) && returned++ < options.Limit) {
                    changes.Add(rev);
                }

                if(returned >= options.Limit) {
                    return false;
                }

                return true;
            }, sql, lastSequence);

            if(options.SortBySequence) {
                changes.SortBySequence(!options.Descending);
            }

            return changes;
        }

        public IEnumerable<RevisionInternal> ChangesSinceStreaming(long lastSequence, ChangesOptions options, RevisionFilter filter)
        {
            bool includeDocs = options.IncludeDocs || filter != null;
            var orderby = options.SortBySequence ? options.Descending ? "sequence DESC" : "sequence" : "+revs.doc_id, +deleted, revid DESC";
            var sql = String.Format("SELECT sequence, revs.doc_id, docid, revid, deleted {0} FROM revs " +
                "JOIN docs ON docs.doc_id = revs.doc_id " +
                "WHERE sequence > ? AND +current=1 " +
                "ORDER BY {1} ",
                (includeDocs ? @", json" : @""), orderby);

            var losingSequences = default(HashSet<long>);
            if(options.SortBySequence && !options.IncludeConflicts) {
                losingSequences = LosingSequences(lastSequence);
            }

            var returned = 0;
            var lastDocId = 0L;
            using(var c = StorageEngine.RawQuery(sql, lastSequence)) {
                while(c.MoveToNext()) {
                    if(options.SortBySequence) {
                        if(losingSequences?.Contains(c.GetLong(0)) == true) {
                            continue;
                        }
                    } else if(!options.IncludeConflicts) {
                        // Only count the first rev for a given doc (the rest will be losing conflicts):
                        var docNumericId = c.GetLong(1);
                        if(docNumericId == lastDocId) {
                            continue;
                        }

                        lastDocId = docNumericId;
                    }

                    string docId = c.GetString(2);
                    var revId = c.GetString(3).AsRevID();
                    bool deleted = c.GetInt(4) != 0;
                    var rev = new RevisionInternal(docId, revId, deleted);
                    rev.Sequence = c.GetLong(0);
                    if(includeDocs) {
                        rev.SetJson(c.GetBlob(5));
                    }

                    if(filter == null || filter(rev) && returned++ < options.Limit) {
                        yield return rev;
                    }

                    if(returned >= options.Limit) {
                        break;
                    }
                }
            }
        }

        public RevisionInternal PutRevision(string inDocId, RevisionID inPrevRevId, IDictionary<string, object> properties,
            bool deleting, bool allowConflict, Uri source, StoreValidation validationBlock)
        {
            RevisionInternal newRev = null;
            RevisionID winningRevID = null;
            bool inConflict = false;

            RunInTransaction(() =>
            {
                // Remember, this block may be called multiple times if I have to retry the transaction.
                newRev = null;
                winningRevID = null;
                inConflict = false;
                var prevRevId = inPrevRevId;
                string docId = inDocId;

                //// PART I: In which are performed lookups and validations prior to the insert...

                // Get the doc's numeric ID (doc_id) and its current winning revision:
                bool isNewDoc = prevRevId == null;
                long docNumericId;
                if(docId != null) {
                    docNumericId = GetOrInsertDocNumericID(docId, ref isNewDoc);
                    if(docNumericId <= 0L) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                            "Unable to create sequence number for document");
                    }
                } else {
                    docNumericId = 0L;
                    isNewDoc = true;
                }

                ValueTypePtr<bool> oldWinnerWasDeletion = false;
                ValueTypePtr<bool> wasConflicted = false;
                RevisionID oldWinningRevId = null;
                if(!isNewDoc) {
                    // Look up which rev is the winner, before this insertion
                    oldWinningRevId = GetWinner(docNumericId, oldWinnerWasDeletion, wasConflicted);
                }

                long parentSequence = 0L;
                if(prevRevId != null) {
                    // Replacing: make sure given prevRevID is current & find its sequence number:
                    if(isNewDoc) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotFound, TAG,
                            "Previous revision specified for a new document");
                    }

                    parentSequence = GetSequenceOfDocument(docNumericId, prevRevId, !allowConflict);
                    if(parentSequence == 0L) {
                        // Not found: NotFound or a Conflict, depending on whether there is any current revision
                        if(!allowConflict && DocumentExists(docId, null)) {
                            throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                                "Conflict attempted in PutRevision without allowConflict == true");
                        }

                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotFound, TAG,
                            "Unable to find previous revision (ID={0} REV={1})",
                            new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure), prevRevId);
                    }
                } else {
                    // Inserting first revision.
                    if(deleting && docId != null) {
                        // Didn't specify a revision to delete: NotFound or a Conflict, depending
                        var status = DocumentExists(docId, null) ? StatusCode.Conflict : StatusCode.NotFound;
                        throw Misc.CreateExceptionAndLog(Log.To.Database, status, TAG,
                            "Delete operation attempted without specifying revision ID for {0}",
                            new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                    }

                    if(docId != null) {
                        // Inserting first revision, with docID given (PUT):
                        // Check whether current winning revision is deleted:
                        if(oldWinnerWasDeletion) {
                            prevRevId = oldWinningRevId;
                            parentSequence = GetSequenceOfDocument(docNumericId, prevRevId, false);
                        } else if(oldWinningRevId != null) {
                            // The current winning revision is not deleted, so this is a conflict
                            throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                                "Conflict attempted in PutRevision");
                        }
                    } else {
                        // Inserting first revision, with no docID given (POST): generate a unique docID:
                        docId = Misc.CreateGUID();
                        docNumericId = GetOrInsertDocNumericID(docId, ref isNewDoc);
                        if(docNumericId <= 0L) {
                            // This docId is log safe because it was generated by us
                            throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                                "Couldn't write new document {0} to database", docId);
                        }
                    }
                }

                // There may be a conflict if (a) the document was already in conflict, or
                // (b) a conflict is created by adding a non-deletion child of a non-winning rev.
                inConflict = wasConflicted || (!deleting && prevRevId != oldWinningRevId);

                //// PART II: In which we prepare for insertion...

                // https://github.com/couchbase/couchbase-lite-net/issues/749
                // Need to ensure revpos is correct for a revision inserted on top
                // of a deletion
                if(oldWinnerWasDeletion) {
                    var attachments = properties.CblAttachments();
                    if(attachments != null) {
                        foreach(var attach in attachments) {
                            var metadata = attach.Value.AsDictionary<string, object>();
                            if(metadata != null) {
                                metadata["revpos"] = prevRevId.Generation + 1;
                            }
                        }
                    }
                }

                IEnumerable<byte> json = null;
                if(properties != null) {
                    try {
                        json = Manager.GetObjectMapper().WriteValueAsBytes(Database.StripDocumentJSON(properties), true);
                    } catch(Exception e) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                            "Unable to serialize document properties in PutRevision");
                    }
                } else {
                    json = Encoding.UTF8.GetBytes("{}");
                }

                // Bump the revID and update the JSON:
                var newRevId = TreeRevisionID.RevIDForJSON(json, deleting, prevRevId);
                if(newRevId == null) {
                    // invalid previous revID (no numeric prefix)
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG,
                        "Invalid rev ID {0} for document {1}", prevRevId, 
                        new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
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
                        throw Misc.CreateExceptionAndLog(Log.To.Validation, validationStatus.Code, TAG,
                            "{0} failed validation", newRev);
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

                var sequence = 0L;
                try {
                    sequence = InsertRevision(newRev, docNumericId, parentSequence, true, hasAttachments, json, docType);
                } catch(Exception) {
                    var lastCode = StorageEngine.LastErrorCode;
                    if(lastCode != raw.SQLITE_CONSTRAINT) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, LastDbError.Code, TAG,
                            "Failed to insert revision {0} ({1})", newRev, lastCode);
                    }

                    Log.To.Database.I(TAG, "Duplicate rev insertion {0} / {1}", 
                        new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure), newRevId);
                    newRev.SetBody(null);
                }

                // Make replaced rev non-current:
                if(parentSequence > 0) {
                    var args = new ContentValues();
                    args["current"] = 0;
                    args["doc_type"] = null;
                    try {
                        StorageEngine.Update("revs", args, "sequence=?", parentSequence.ToString());
                    } catch(CouchbaseLiteException) {
                        Log.To.Database.E(TAG, "Failed to update document {0}, rethrowing...", 
                            new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                        throw;
                    } catch(Exception e) {
                        StorageEngine.Delete("revs", "sequence=?", sequence.ToString());
                        throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                            "Error updating document {0}", 
                            new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                    }
                }

                if(sequence == 0L) {
                    // duplicate rev; see above
                    return true;
                }

                // Delete the deepest revs in the tree to enforce the MaxRevTreeDepth:
                var minGenToKeep = newRev.Generation - MaxRevTreeDepth + 1;
                if(minGenToKeep > 1) {
                    var pruned = PruneDocument(docNumericId, minGenToKeep);
                    if(pruned > 0) {
                        Log.To.Database.V(TAG, "Pruned {0} old revisions of doc '{1}'", pruned,
                            new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                    }
                }

                // Figure out what the new winning rev ID is:
                winningRevID = GetWinner(docNumericId, oldWinningRevId, oldWinnerWasDeletion, newRev);

                // Success!
                return true;
            });

            //// EPILOGUE: A change notification is sent...
            Delegate.DatabaseStorageChanged(new DocumentChange(newRev, winningRevID, inConflict, source));

            return newRev;
        }

        public void ForceInsert(RevisionInternal inRev, IList<RevisionID> revHistory, StoreValidation validationBlock, Uri source)
        {
            var rev = new RevisionInternal(inRev);
            rev.Sequence = 0L;
            string docId = rev.DocID;

            RevisionID winningRevId = null;
            ValueTypePtr<bool> inConflict = false;
            bool created = false;
            RunInTransaction(() =>
            {
                // First look up the document's row-id and all locally-known revisions of it:
                var localRevs = new Dictionary<RevisionID, RevisionInternal>();
                RevisionID oldWinningRevId = null;
                ValueTypePtr<bool> oldWinnerWasDeletion = false;
                bool isNewDoc = revHistory.Count == 1;
                var docNumericId = GetOrInsertDocNumericID(docId, ref isNewDoc);
                if(docNumericId <= 0) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                        "Error inserting document {0}", new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                }

                var fullHistoryCount = revHistory.Count;
                var commonAncestorIndex = 0;
                var commonAncestor = default(RevisionInternal);
                if (isNewDoc) {
                    commonAncestorIndex = fullHistoryCount;
                } else {
                    var localRevsList = default(RevisionList);
                    try {
                        localRevsList = GetAllDocumentRevisions(docId, docNumericId, false, true);
                        localRevs = new Dictionary<RevisionID, RevisionInternal>(localRevsList.Count);
                        foreach(var localRev in localRevsList) {
                            localRevs[localRev.RevID] = localRev;
                        }

                        // What's the oldest revID in the history that appears to be in the local db?
                        commonAncestorIndex = 0;
                        foreach (var revID in revHistory) {
                            commonAncestor = localRevs.Get(revID);
                            if (commonAncestor != null) {
                                break;
                            }

                            commonAncestorIndex++;
                        }

                        if (commonAncestorIndex == 0) { // No-op: rev already exists
                            return true;
                        }

                        // Look up which rev is the winner, before this insertion
                        try {
                            oldWinningRevId = GetWinner(docNumericId, oldWinnerWasDeletion, inConflict);
                        } catch(CouchbaseLiteException) {
                            Log.To.Database.E(TAG, "Failed to look up winner for {0}, rethrowing...", 
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                            throw;
                        } catch(Exception e) {
                            throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, 
                                "Error looking up winner for {0}", 
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                        }
                    } catch(CouchbaseLiteException e) {
                        // Don't stop on a not found, because it is not critical.  This can happen
                        // when two pullers are pulling the same data at the same time.  One will
                        // insert JUST the document (not the revisions yet), and then yield to the
                        // other which will see the document and assume revisions are there which aren't.
                        // In that case, we'd like to continue and insert the missing revisions instead of
                        // erroring out.  Note that this needs to be changed to a better way.
                        if(e.Code != StatusCode.NotFound) {
                            Log.To.Database.E(TAG, "Error getting document revisions for {0}, rethrowing...", 
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                            throw;
                        }
                    }
                }

                // Trim down the history to what we need
                var history = revHistory;
                if (commonAncestorIndex < fullHistoryCount) {
                    // Trim history to new revisions
                    history = revHistory.Take(commonAncestorIndex).ToList();
                } else if (fullHistoryCount > MaxRevTreeDepth) {
                    // If no common ancestor, limit history to max depth
                    history = revHistory.Take(MaxRevTreeDepth).ToList();
                }

                // Validate against the latest common ancestor
                if(validationBlock != null) {
                    RevisionID parentRevId = (revHistory.Count > 1) ? revHistory[1] : null;
                    var validationStatus = validationBlock(rev, commonAncestor, parentRevId);
                    if(validationStatus.IsError) {
                        Log.To.Validation.I(TAG, "{0} failed validation, throwing CouchbaseLiteException", rev);
                        throw new CouchbaseLiteException($"{rev} failed validation", StatusCode.DbError);
                    }
                }

                // Walk through the remote history in chronological order, matching each revision ID to
                // a local revision. When the list diverges, start creating blank local revisions to fill
                // in the local history:
                long sequence = commonAncestor == null ? 0L : commonAncestor.Sequence;
                for(int i = history.Count - 1; i >= 0; --i) {
                    var revId = revHistory[i];
                    var newRev = default(RevisionInternal);
                    var json = default(IEnumerable<byte>);
                    var docType = default(string);
                    var current = false;
                    if (i == 0) {
                        // Hey, this is the leaf revision we're inserting:
                        newRev = rev;
                        json = EncodeDocumentJSON(rev);
                        docType = rev.GetPropertyForKey("type") as string;
                        current = true;
                    } else {
                        // It's an intermediate parent, so insert a stub:
                        newRev = new RevisionInternal(docId, revId, false);
                    }

                    // Insert it:
                    try {
                        sequence = InsertRevision(newRev, docNumericId, sequence, current, newRev.GetAttachments() != null, json, docType);
                    } catch (CouchbaseLiteException e) {
                        if (e.Code == StatusCode.DbError) {
                            var sqliteException = e.InnerException as ugly.sqlite3_exception;
                            if (sqliteException == null) {
                                // DbError without an inner sqlite3 exception? Weird...throw
                                throw;
                            }

                            if (sqliteException.errcode != raw.SQLITE_CONSTRAINT) {
                                // This is a genuine error inserting the revision
                                Log.To.Database.E(TAG, "Error inserting revision {0} ({1}), rethrowing...", newRev, sqliteException.errcode);
                                throw;
                            } else {
                                // This situation means that the revision already exists, so go get the existing
                                // sequence number
                                Log.To.Database.I(TAG, "Previous error due to duplicate revision, fetching existing one...");
                                sequence = GetSequenceOfDocument(docNumericId, newRev.RevID, false);
                            }
                        } else {
                            Log.To.Database.E(TAG, "Error inserting revision {0}, rethrowing...", newRev);
                            throw;
                        }
                    }
                }

                // Mark the latest local rev as no longer current:
                if(commonAncestor != null) {
                    var args = new ContentValues();
                    args["current"] = 0;
                    args["doc_type"] = null;
                    int changes;
                    try {
                        changes = StorageEngine.Update("revs", args, "sequence=? AND current > 0", commonAncestor.Sequence.ToString());
                    } catch(CouchbaseLiteException) {
                        Log.To.Database.E(TAG, "Failed to update {0}, rethrowing...", 
                            new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                        throw;
                    } catch(Exception e) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                            "Error updating {0}", new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                    }

                    if(changes == 0) {
                        // local parent wasn't a leaf, ergo we just created a branch
                        inConflict = true;
                    }
                }

                // Delete the deepest revs in the tree to enforce the MaxRevTreeDepth:
                if (localRevs != null && inRev.Generation > MaxRevTreeDepth) {
                    int minGen = rev.Generation, maxGen = rev.Generation;
                    foreach (var innerRev in localRevs.Values) {
                        var generation = innerRev.Generation;
                        minGen = Math.Min(minGen, generation);
                        maxGen = Math.Max(maxGen, generation);
                    }

                    var minGenToKeep = maxGen - MaxRevTreeDepth + 1;
                    if (minGen < minGenToKeep) {
                        var pruned = PruneDocument(docNumericId, minGenToKeep);
                        if (pruned > 0) {
                            Log.To.Database.V(TAG, "Pruned {0} old revisions of '{1}'", pruned,
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                        }
                    }
                }

                // Figure out what the new winning rev ID is:
                winningRevId = GetWinner(docNumericId, oldWinningRevId, oldWinnerWasDeletion, rev);
                created = true;
                return true;
            });

            if (created) {
                Delegate.DatabaseStorageChanged (new DocumentChange (rev, winningRevId, inConflict, source));
            }
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
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadParam, TAG,
                            "Illegal null revIds for {0}", new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                    } else if(revIDs.Count == 0) {
                        revsPurged = new List<string>();
                    } else if(revIDs.Contains("*")) {
                        // Delete all revisions if magic "*" revision ID is given.  Deleting the 'docs'
                        // row will delete all 'revs' rows due to cascading.
                        try {
                            StorageEngine.Delete("docs", "doc_id=?", docNumericId.ToString());
                        } catch(CouchbaseLiteException) {
                            Log.To.Database.E(TAG, "Failed to delete revisions of {0}, rethrowing...", 
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                            throw;
                        } catch(Exception e) {
                            throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                                "Error deleting revisions of {0}", new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                        }

                        InvalidateDocNumericID(docId);
                        NotifyPurgedDocument(docId);
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
                        }, sql, docNumericId);

                        seqsToPurge.ExceptWith(seqsToKeep);
                        Log.To.Database.I(TAG, "Purging doc '{0}' revs ({1}); asked for ({2})", 
                            new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure),
                            new LogJsonString(revsToPurge.ToStringArray()),
                            new LogJsonString(revIDs));
                        
                        if(seqsToPurge.Any()) {
                            // Now delete the sequences to be purged.
                            var deleteSql = String.Format("sequence in ({0})", String.Join(", ", seqsToPurge.ToStringArray()));
                            int count = 0;
                            try {
                                count = StorageEngine.Delete("revs", deleteSql);
                            } catch(CouchbaseLiteException) {
                                Log.To.Database.E(TAG, "Failed to delete revisions of {0}, rethrowing...", 
                                    new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                                throw;
                            } catch(Exception e) {
                                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                                    "Error deleting revisions of {0}", new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                            }

                            if(count != seqsToPurge.Count) {
                                Log.To.Database.W(TAG, "Only {0} revisions deleted of {1}", count, String.Join(", ", seqsToPurge.ToStringArray()));
                            }
                        }

                        revsPurged = revsToPurge;
                    }

                    result["docID"] = revIDs.Where(x => revsPurged.Contains(x));
                }

                return true;
            });

            return result;
        }

        public IList<string> PurgeExpired()
        {
            var result = new List<string>();
            RunInTransaction (() => {
                var sequences = new List<long>();
                var now = DateTime.UtcNow;
                TryQuery(c =>
                {
                    sequences.Add(c.GetLong(0));
                    result.Add(c.GetString(1));

                    return true;
                }, "SELECT * FROM docs WHERE expiry_timestamp <= ?", now);
                    
                if (result.Count > 0) {
                    var deleteSql = String.Format("sequence in ({0})", String.Join(", ", sequences.ToStringArray()));
                    var vals = new ContentValues(1);
                    vals["expiry_timestamp"] = null;

                        StorageEngine.Delete("revs", deleteSql);
                        StorageEngine.ExecSQL("UPDATE docs SET expiry_timestamp=null WHERE expiry_timestamp <= ?", now);
                        return true;
                }

                return true;
            });

            return result;
        }

        public IViewStore GetViewStorage(string name, bool create)
        {
            return SqliteViewStore.MakeViewStore(this, name, create);
        }

        public IEnumerable<string> GetAllViews()
        {
            var result = new List<string>();
            TryQuery(c =>
            {
                result.Add(c.GetString(0));
                return true;
            }, "SELECT name FROM views");

            return result;
        }

        public RevisionInternal GetLocalDocument(string docId, RevisionID revId)
        {
            RevisionInternal result = null;
            TryQuery(c =>
            {
                var gotRevId = c.GetString(0).AsRevID();
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

                properties.SetDocRevID(docId, gotRevId);
                result = new RevisionInternal(docId, gotRevId, false);
                result.SetProperties(properties);

                return false;
            }, "SELECT revid, json FROM localdocs WHERE docid=?", docId);

            return result;
        }

        public RevisionInternal PutLocalRevision(RevisionInternal revision, RevisionID prevRevId, bool obeyMVCC)
        {
            string docId = revision.DocID;
            if (!docId.StartsWith("_local/")) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG, "Local revision doesn't start with '_local/'");
            }

            if (!obeyMVCC) {
                return PutLocalRevisionNoMvcc(revision);
            }

            if (!revision.Deleted) {
                // PUT:
                var json = EncodeDocumentJSON(revision);
                if (json == null) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadJson, TAG, "Invalid JSON in local revision");
                }

                RevisionID newRevId;
                long changes = -1;
                if (prevRevId != null) {
                    int generation = prevRevId.Generation;
                    if (generation == 0) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG, "Invalid prevRevId in PutLocalRevision");
                    }

                    newRevId = String.Format("{0}-local", ++generation).AsRevID();
                    try {
                        var args = new ContentValues();
                        args["revid"] = newRevId.ToString();
                        args["json"] = json;
                        changes = StorageEngine.Update("localdocs", args, "docid=? AND revid=?", docId, prevRevId.ToString());
                    } catch (Exception e) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, "SQLite error writing local document");
                    }
                } else {
                    newRevId = "1-local".AsRevID();
                    // The docid column is unique so the insert will be a no-op if there is already
                    // a doc with this ID.
                    var args = new ContentValues();
                    args["docid"] = docId;
                    args["revid"] = newRevId.ToString();
                    args["json"] = json;
                    try {
                        changes = StorageEngine.InsertWithOnConflict("localdocs", null, args, ConflictResolutionStrategy.Ignore);
                    } catch (Exception e) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, "SQLite error creating local document");
                    }
                }

                if (changes == 0) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG, "Local revision conflict detected");
                }

                return revision.Copy(docId, newRevId);
            } else {
                // DELETE:
                var status = DeleteLocalRevision(docId, prevRevId);
                if (status.IsError) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, status.Code, TAG, "Error deleting local document");
                }

                return revision;
            }
        }

#pragma warning restore 1591
#endregion
    }
}