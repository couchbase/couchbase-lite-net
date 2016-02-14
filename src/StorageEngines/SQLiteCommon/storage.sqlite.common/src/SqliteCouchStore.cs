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

using Couchbase.Lite.Util;
using SQLitePCL;
using SQLitePCL.Ugly;
using Couchbase.Lite.Revisions;
using Sharpen;
using Couchbase.Lite.Db;
using Couchbase.Lite.Internal;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Store
{
    internal sealed class SqliteCouchStore : ICouchStore
    {
        #region Constants

        private const int SQLITE_MMAP_SIZE = 50 * 1024 * 1024;
        private const int DOC_ID_CACHE_SIZE = 1000;
        private const double SQLITE_BUSY_TIMEOUT = 5.0; //seconds
        private const int TRANSACTION_MAX_RETRIES = 10;
        private const int TRANSACTION_MAX_RETRY_DELAY = 50; //milliseconds

        private const string LOCAL_CHECKPOINT_DOC_ID = "CBL_LocalCheckpoint";
        private const string TAG = "SqliteCouchStore";
        private const string DB_FILENAME = "db.sqlite3";

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

        private static readonly int _SqliteVersion;

        private string _directory;
        private int _transactionCount;
        private LruCache<string, object> _docIDs = new LruCache<string, object>(DOC_ID_CACHE_SIZE);
        private SymmetricKey _encryptionKey;
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
                    false, -1, "SELECT COUNT(DISTINCT doc_id) FROM revs WHERE current=1 AND deleted=0");
            }
        }

        public long LastSequence { 
            get {
                return QueryOrDefault<long>(c => c.GetLong(0),
                    false, 0L, "SELECT seq FROM sqlite_sequence WHERE name='revs'");
            }
        }

        public bool InTransaction
        { 
            get {
                return _transactionCount > 0;
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


        #endregion

        #region Constructors

        static SqliteCouchStore()
        {
            // Test the version of the actual SQLite implementation at runtime. Necessary because
            // the app might be linked with a custom version of SQLite (like SQLCipher) instead of the
            // system library, so the actual version/features may differ from what was declared in
            // sqlite3.h at compile time.
            Log.I(TAG, "Initialized SQLite store (version {0} ({1}))", raw.sqlite3_libversion(), raw.sqlite3_sourceid());
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
                        Log.D(TAG, "Optimizing SQL indexes (curSeq={0}, last run at {1})", currentSequence, lastOptimized);
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
                        Log.E(TAG, "Error running statement '{0}'", statement);
                        throw;
                    } catch(Exception e) {
                        throw new CouchbaseLiteException(String.Format("Error running statement '{0}'", statement), e) { Code = StatusCode.DbError };
                    }

                }
            }
        }

        public IDictionary<string, object> GetDocumentProperties(IEnumerable<byte> json, string docId, string revId, bool deleted, long sequence)
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

        public RevisionInternal GetDocument(string docId, long sequence)
        {
            RevisionInternal result = null;
            TryQuery(c =>
            {
                string revId = c.GetString(0);
                bool deleted = c.GetInt(1) != 0;
                result = new RevisionInternal(docId, revId, deleted);
                result.Sequence = sequence;
                result.SetBody(new Body(c.GetBlob(2)));

                return false;
            }, false, "SELECT revid, deleted, json FROM revs WHERE sequence=?", sequence);

            return result;
        }

        public RevisionInternal GetRevision(string docId, string revId, bool deleted, long sequence, IEnumerable<byte> json)
        {
            var rev = new RevisionInternal(docId, revId, deleted);
            rev.Sequence = sequence;
            if (json != null) {
                rev.SetBody(new Body(json));
            }

            return rev;
        }

        public Status TryQuery(Func<Cursor, bool> action, bool readUncommit, string sqlQuery, params object[] args)
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
            } catch(Exception e) {
                Log.E(TAG, "Error executing SQL query", e);
            } finally {
                if (c != null) {
                    c.Dispose();
                }
            }

            return new Status(StatusCode.DbError);
        }

        public T QueryOrDefault<T>(Func<Cursor, T> action, bool readUncommit, T defaultVal, string sqlQuery, params object[] args)
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

        #endregion

        #region Internal Methods

        internal IDictionary<string, object> GetRevisionHistoryDictStartingFromAnyAncestor(RevisionInternal rev, IList<string>ancestorRevIDs)
        {
            var history = GetRevisionHistory(rev, null); // This is in reverse order, newest ... oldest
            if (ancestorRevIDs != null && ancestorRevIDs.Any())
            {
                for (var i = 0; i < history.Count; i++)
                {
                    if (ancestorRevIDs.Contains(history[i].RevID))
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

        #endregion

        #region Private Methods

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
                throw new CouchbaseLiteException("Unable to create a storage engine", StatusCode.DbError);
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
                    throw new CouchbaseLiteException("Database version (" + dbVersion + ") is newer than I know how to work with", StatusCode.DbError);
                }

                if (dbVersion < 17) {
                    throw new CouchbaseLiteException("Database version ({0}) is older " +
                        "than I know how to work with", dbVersion) { Code = StatusCode.DbError };
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

                if (isNew) {
                    RunStatements("END TRANSACTION");
                }

                if (!isNew && !_readOnly) {
                    OptimizeSQLIndexes();
                }
            } catch(CouchbaseLiteException) {
                Log.W(TAG, "Error initializing the SQLite storage engine");
                StorageEngine.Close();
                throw;
            } catch(Exception e) {
                StorageEngine.Close();
                throw new CouchbaseLiteException("Unknown error initializing SQLite storage engine", e) { Code = StatusCode.Exception };
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

                    var minGenRevId = cursor.GetString(1);
                    var maxGenRevId = cursor.GetString(2);

                    minGen = RevisionID.GetGeneration(minGenRevId);
                    maxGen = RevisionID.GetGeneration(maxGenRevId);

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

            if (Delegate != null) {
                Delegate.StorageExitedTransaction(commit);
            }

            return true;
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
            return QueryOrDefault<bool>(c => c.GetInt(0) != 0, false, false, 
                "SELECT no_attachments=0 FROM revs WHERE sequence=?", sequence);
        }

        private RevisionInternal RevisionWithDocID(string docId, string revId, bool deleted, long sequence, IEnumerable<byte> json)
        {
            var rev = new RevisionInternal(docId, revId, deleted);
            rev.Sequence = sequence;
            if (json != null) {
                rev.SetJson(json);
            }

            return rev;
        }

        private bool RunInOuterTransaction(RunInTransactionDelegate action)
        {
            if (!InTransaction) {
                return RunInTransaction(action);
            }

            var status = false;
            try {
                status = action();
            } catch(CouchbaseLiteException) {
                Log.W(TAG, "Failed in RunInOuterTransaction");
                status = false;
                throw;
            }

            return status;
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
            vals["revid"] = rev.RevID;
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

        private string GetWinner(long docNumericId, string oldWinnerRevId, bool oldWinnerWasDeletion, RevisionInternal newRev)
        {
            var newRevID = newRev.RevID;
            if (oldWinnerRevId == null) {
                return newRevID;
            }

            if (!newRev.Deleted) {
                if (oldWinnerWasDeletion || RevisionID.CBLCompareRevIDs(newRevID, oldWinnerRevId) > 0) {
                    return newRevID; // this is now the winning live revision
                }
            } else if (oldWinnerWasDeletion) {
                if (RevisionID.CBLCompareRevIDs(newRevID, oldWinnerRevId) > 0) {
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

        private RevisionList GetAllDocumentRevisions(string docId, long docNumericId, bool onlyCurrent)
        {
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
                rev.Sequence = c.GetLong(0);
                revs.Add(rev);

                return true;
            }, true, sql, docNumericId);
                
            if (innerStatus.IsError && innerStatus.Code != StatusCode.NotFound) {
                throw new CouchbaseLiteException("Error getting document revisions ({0})", innerStatus) { Code = StatusCode.DbError };
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
                Log.W(TAG, "Failed to set info ({0} -> {1})", key, info);
                throw;
            } catch(Exception e) {
                throw new CouchbaseLiteException(String.Format(
                    "Error setting info ({0} -> {1})", key, info), e) { Code = StatusCode.Exception }; 
            }
        }

        public string GetInfo(string key)
        {
            string retVal = null;
            var success = TryQuery(c => {
                retVal = c.GetString(0);
                return false;
            }, false, "SELECT value FROM info WHERE key=?", key);

            return success.IsError ? null : retVal;
        }

        public void Compact()
        {
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
            } catch(CouchbaseLiteException) {
                Log.W(TAG, "Error compacting old JSON");
                throw;
            } catch (Exception e) {
                throw new CouchbaseLiteException(e, StatusCode.DbError);
            }

            Log.V(TAG, "Deleting old attachments...");

            try {
                Log.V(TAG, "Flushing SQLite WAL...");
                StorageEngine.ExecSQL("PRAGMA wal_checkpoint(RESTART)");
                Log.V(TAG, "Vacuuming SQLite sqliteDb...");
                StorageEngine.ExecSQL("VACUUM");
            } catch(CouchbaseLiteException) {
                Log.W(TAG, "Error vacuuming Sqlite DB");
                throw;
            } catch (Exception e) {
                throw new CouchbaseLiteException("Error vacuuming Sqlite DB", e) { Code = StatusCode.DbError };
            }
        }

        public bool RunInTransaction(RunInTransactionDelegate block)
        {
            var status = false;
            var keepGoing = false;
            int retries = 0;
            do {
                keepGoing = false;
                if(!BeginTransaction()) {
                    throw new CouchbaseLiteException("Error beginning begin transaction", StatusCode.DbError);
                }

                try {
                    status = block();
                } catch(CouchbaseLiteException e) {
                    if(e.Code == StatusCode.DbBusy) {
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
                        keepGoing = true;
                    } else {
                        Log.W(TAG, "Failed to run transaction");
                        status = false;
                        throw;
                    }
                } catch(Exception e) {
                    status = false;
                    throw new CouchbaseLiteException("Error running transaction", e) { Code = StatusCode.Exception };
                } finally {
                    EndTransaction(status);
                }
            } while(keepGoing);

            return status;
        }

        public void SetEncryptionKey(SymmetricKey key)
        {
            #if !ENCRYPTION
            throw new InvalidOperationException("This store does not support encryption");
            #else
            _encryptionKey = key;
            #endif
        }

        public AtomicAction ActionToChangeEncryptionKey(SymmetricKey newKey)
        {
            #if !ENCRYPTION
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
                var keyStr = newKey != null ? newKey.HexData : String.Empty;
                var sql = String.Format("ATTACH DATABASE ? AS rekeyed_db KEY \"x'{0}'\"", keyStr);
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
                var version = QueryOrDefault<int>(c => c.GetInt(0), false, 0, "PRAGMA user_version");
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
            action.AddLogic(AtomicAction.MoveFile(tempPath, Path.Combine(_directory, DB_FILENAME)));

            return action;
            #endif
        }

        public RevisionInternal GetDocument(string docId, string revId, bool withBody, Status outStatus = null)
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
                result.Sequence = c.GetLong(2);
                if(withBody) {
                    result.SetJson(c.GetBlob(3));
                }
                    
                return false;
            }, true, sb.ToString(), docNumericId, revId);

            if (transactionStatus.IsError) {
                if (transactionStatus.Code == StatusCode.NotFound) {
                    outStatus.Code = revId == null ? StatusCode.Deleted : StatusCode.NotFound;
                    return null;
                } else {
                    throw new CouchbaseLiteException(transactionStatus.Code);
                }
            }

            outStatus.Code = StatusCode.Ok;
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
                throw new CouchbaseLiteException(StatusCode.NotFound);
            }

            var status = TryQuery(c =>
            {
                var json = c.GetBlob(1);
                if(json != null) {
                    rev.Sequence = c.GetLong(0);
                    rev.SetJson(json);
                }

                return false;
            }, false, "SELECT sequence, json FROM revs WHERE doc_id=? AND revid=? LIMIT 1", docNumericId, rev.RevID);

            if (status.IsError) {
                throw new CouchbaseLiteException(status.Code);
            }
        }

        public long GetRevisionSequence(RevisionInternal rev)
        {
            var docNumericId = GetDocNumericID(rev.DocID);
            if (docNumericId <= 0L) {
                return 0L;
            }

            return QueryOrDefault<long>(c => c.GetLong(0), false, 0L, "SELECT sequence FROM revs WHERE doc_id=? AND revid=? LIMIT 1", docNumericId, rev.RevID);
        }

        public RevisionInternal GetParentRevision(RevisionInternal rev)
        {
            // First get the parent's sequence:
            var seq = rev.Sequence;
            if (seq != 0) {
                seq = QueryOrDefault<long>(c => c.GetLong(0), false, 0L, "SELECT parent FROM revs WHERE sequence=?", seq);
            } else {
                var docNumericId = GetDocNumericID(rev.DocID);
                if (docNumericId == 0L) {
                    return null;
                }

                seq = QueryOrDefault<long>(c => c.GetLong(0), false, 0L, "SELECT parent FROM revs WHERE doc_id=? and revid=?", docNumericId, rev.RevID);
            }

            if (seq == 0) {
                return null;
            }

            // Now get its revID and deletion status:
            RevisionInternal result = null;
            TryQuery(c =>
            {
                result = new RevisionInternal(rev.DocID, c.GetString(0), c.GetInt(1) != 0);
                result.Sequence = seq;
                return false;
            }, false, "SELECT revid, deleted FROM revs WHERE sequence=?", seq);

            return result;
        }

        public IList<RevisionInternal> GetRevisionHistory(RevisionInternal rev, ICollection<string> ancestorRevIds)
        {
            string docId = rev.DocID;
            string revId = rev.RevID;
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
                    nextRev.Sequence = sequence;
                    nextRev.Missing = c.GetInt(4) != 0;
                    history.Add(nextRev);
                    lastSequence = c.GetLong(1);
                    if(lastSequence == 0) {
                        return false;
                    }

                    if(ancestorRevIds != null && ancestorRevIds.Contains(revId)) {
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

        public IDictionary<string, object> GetRevisionHistoryDict(RevisionInternal rev, IList<string> ancestorRevIds)
        {
            string docId = rev.DocID;
            string revId = rev.RevID;
            Debug.Assert(docId != null && revId != null);

            var docNumericId = GetDocNumericID(docId);
            if (docNumericId < 0) {
                return null;
            }

            var history = new List<RevisionInternal>();

            if (docNumericId > 0) {
                long lastSequence = 0L;
                var status = TryQuery(c =>
                {
                    var sequence = c.GetLong(0);
                    bool matches;
                    if(lastSequence == 0L) {
                        matches = revId == c.GetString(2);
                    } else {
                        matches = lastSequence == sequence;
                    }

                    if(matches) {
                        string nextRevId = c.GetString(2);
                        bool deleted = c.GetInt(3) != 0;
                        var nextRev = new RevisionInternal(docId, nextRevId, deleted);
                        nextRev.Sequence = sequence;
                        nextRev.Missing = c.GetInt(4) != 0;
                        history.Add(nextRev);
                        lastSequence = c.GetLong(1);
                        if((ancestorRevIds != null && ancestorRevIds.Contains(nextRevId)) || lastSequence == 0) {
                            return false;
                        }
                    }

                    return true;
                }, false, "SELECT sequence, parent, revid, deleted, json isnull " +
                "FROM revs WHERE doc_id=? ORDER BY sequence DESC", docNumericId);

                if (status.IsError) {
                    return null;
                }
            }

            // Try to extract descending numeric prefixes:
            var suffixes = new List<string>();
            object start = null;
            int lastRevNo = -1;
            foreach (var historyRev in history) {
                var parsed = RevisionID.ParseRevId(historyRev.RevID);
                if (parsed.Item1 > 0) {
                    int revNo = parsed.Item1;
                    string suffix = parsed.Item2;
                    if (start == null) {
                        start = revNo;
                    } else if (revNo != lastRevNo - 1) {
                        start = null;
                        break;
                    }

                    lastRevNo = revNo;
                    suffixes.Add(suffix);
                } else {
                    start = null;
                    break;
                }
            }

            IEnumerable<string> revIDs = start != null ? suffixes : history.Select(r => r.RevID);
            return new NonNullDictionary<string, object> {
                { "ids", revIDs },
                { "start", start }
            };
        }

        public RevisionList GetAllDocumentRevisions(string docId, bool onlyCurrent)
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

        public IEnumerable<string> GetPossibleAncestors(RevisionInternal rev, int limit, bool onlyAttachments)
        {
            int generation = rev.Generation;
            if (generation <= 1L) {
                return new List<string>();
            }

            long docNumericId = GetDocNumericID(rev.DocID);
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

        public string FindCommonAncestor(RevisionInternal rev, IEnumerable<string> revIds)
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
                "ORDER BY revid DESC LIMIT 1", Database.JoinQuoted(revIds));

            return QueryOrDefault(c => c.GetString(0), false, null, sql, docNumericId, rev.RevID);
        }

        public int FindMissingRevisions(RevisionList revs)
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

        public ICollection<BlobKey> FindAllAttachmentKeys()
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

        public RevisionList ChangesSince(long lastSequence, ChangesOptions options, RevisionFilter filter)
        {
            // http://wiki.apache.org/couchdb/HTTP_database_API#Changes

            bool includeDocs = options.IncludeDocs || filter != null;
            var sql = String.Format("SELECT sequence, revs.doc_id, docid, revid, deleted {0} FROM revs, docs " +
                "WHERE sequence > ? AND current=1 " +
                "AND revs.doc_id = docs.doc_id " +
                "ORDER BY revs.doc_id, revid DESC",
                (includeDocs ? @", json" : @""));

            var changes = new RevisionList();
            long lastDocId = 0L;
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
                string revId = c.GetString(3);
                bool deleted = c.GetInt(4) != 0;
                var rev = new RevisionInternal(docId, revId, deleted);
                rev.Sequence = c.GetLong(0);
                if(includeDocs) {
                    rev.SetJson(c.GetBlob(5));
                }

                if(filter == null || filter(rev)) {
                    changes.Add(rev);
                }

                return true;
            }, false, sql, lastSequence);

            if (options.SortBySequence) {
                changes.SortBySequence(!options.Descending);
                changes.Limit(options.Limit);
            }

            return changes;
        }

        public RevisionInternal PutRevision(string inDocId, string inPrevRevId, IDictionary<string, object> properties,
            bool deleting, bool allowConflict, Uri source, StoreValidation validationBlock)
        {
            IEnumerable<byte> json = null;
            if (properties != null) {
                try {
                    json = Manager.GetObjectMapper().WriteValueAsBytes(Database.StripDocumentJSON(properties), true);
                } catch (Exception e) {
                    throw new CouchbaseLiteException(e, StatusCode.BadJson);
                }
            } else {
                json = Encoding.UTF8.GetBytes("{}");
            }

            RevisionInternal newRev = null;
            string winningRevID = null;
            bool inConflict = false;

            RunInOuterTransaction(() =>
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
                    // Look up which rev is the winner, before this insertion
                    oldWinningRevId = GetWinner(docNumericId, oldWinnerWasDeletion, wasConflicted);
                }

                long parentSequence = 0L;
                if(prevRevId != null) {
                    // Replacing: make sure given prevRevID is current & find its sequence number:
                    if(isNewDoc) {
                        throw new CouchbaseLiteException("Previous revision not found", StatusCode.NotFound);
                    }

                    parentSequence = GetSequenceOfDocument(docNumericId, prevRevId, !allowConflict);
                    if(parentSequence == 0L) {
                        // Not found: NotFound or a Conflict, depending on whether there is any current revision
                        if(!allowConflict && DocumentExists(docId, null)) {
                            throw new CouchbaseLiteException(StatusCode.Conflict);
                        }

                        throw new CouchbaseLiteException("Previous revision not found", StatusCode.NotFound);
                    }
                } else {
                    // Inserting first revision.
                    if(deleting && docId != null) {
                        // Didn't specify a revision to delete: NotFound or a Conflict, depending
                        throw new CouchbaseLiteException(DocumentExists(docId, null) ? StatusCode.Conflict : StatusCode.NotFound);
                    }

                    if(docId != null) {
                        // Inserting first revision, with docID given (PUT):
                        // Check whether current winning revision is deleted:
                        if(oldWinnerWasDeletion) {
                            prevRevId = oldWinningRevId;
                            parentSequence = GetSequenceOfDocument(docNumericId, prevRevId, false);
                        } else if(oldWinningRevId != null) {
                            // The current winning revision is not deleted, so this is a conflict
                            throw new CouchbaseLiteException(StatusCode.Conflict);
                        }
                    } else {
                        // Inserting first revision, with no docID given (POST): generate a unique docID:
                        docId = Misc.CreateGUID();
                        docNumericId = GetOrInsertDocNumericID(docId, ref isNewDoc);
                        if(docNumericId <= 0L) {
                            throw new CouchbaseLiteException(String.Format(
                                "Couldn't write new document {0} to database", docId), StatusCode.DbError);
                        }
                    }
                }

                // There may be a conflict if (a) the document was already in conflict, or
                // (b) a conflict is created by adding a non-deletion child of a non-winning rev.
                inConflict = wasConflicted || (!deleting && prevRevId != oldWinningRevId);

                //// PART II: In which we prepare for insertion...

                // Bump the revID and update the JSON:
                string newRevId = Delegate.GenerateRevID(json, deleting, prevRevId);
                if(newRevId == null) {
                    // invalid previous revID (no numeric prefix)
                    throw new CouchbaseLiteException(String.Format(
                        "Invalid rev ID {0} for document {1}", prevRevId, docId), StatusCode.BadId);
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
                        throw new CouchbaseLiteException(String.Format("{0} failed validation", newRev), 
                            validationStatus.Code);
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
                    if(StorageEngine.LastErrorCode != raw.SQLITE_CONSTRAINT) {
                        throw new CouchbaseLiteException(String.Format("Failed to insert revision {0}", newRev),
                            LastDbError.Code);
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
                    } catch(CouchbaseLiteException) {
                        Log.W(TAG, "Failed to update document {0}", docId);
                        throw;
                    } catch(Exception e) {
                        StorageEngine.Delete("revs", "sequence=?", sequence.ToString());
                        throw new CouchbaseLiteException(String.Format(
                            "Error updating document {0}", docId), e) { Code = StatusCode.DbError };
                    }
                }

                if(sequence == 0L) {
                    // duplicate rev; see above
                    return true;
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

        public void ForceInsert(RevisionInternal inRev, IList<string> revHistory, StoreValidation validationBlock, Uri source)
        {
            var rev = new RevisionInternal(inRev);
            rev.Sequence = 0L;
            string docId = rev.DocID;

            string winningRevId = null;
            bool inConflict = false;
            RunInTransaction(() =>
            {
                // First look up the document's row-id and all locally-known revisions of it:
                Dictionary<string, RevisionInternal> localRevs = null;
                string oldWinningRevId = null;
                bool oldWinnerWasDeletion = false;
                bool isNewDoc = revHistory.Count == 1;
                var docNumericId = GetOrInsertDocNumericID(docId, ref isNewDoc);
                if(docNumericId <= 0) {
                    throw new CouchbaseLiteException(String.Format("Error inserting document {0}", docId),
                        StatusCode.DbError);
                }

                if(!isNewDoc) {
                    var localRevsList = default(RevisionList);
                    try {
                        localRevsList = GetAllDocumentRevisions(docId, docNumericId, false);
                        localRevs = new Dictionary<string, RevisionInternal>(localRevsList.Count);
                        foreach(var localRev in localRevsList) {
                            localRevs[localRev.RevID] = localRev;
                        }

                        // Look up which rev is the winner, before this insertion
                        try {
                            oldWinningRevId = GetWinner(docNumericId, oldWinnerWasDeletion, inConflict);
                        } catch(CouchbaseLiteException) {
                            Log.W(TAG, "Failed to look up winner for {0}", docId);
                            throw;
                        } catch(Exception e) {
                            throw new CouchbaseLiteException(String.Format(
                                "Error looking up winner for {0}", docId), e) { Code = StatusCode.Exception };
                        }
                    } catch(CouchbaseLiteException e) {
                        // Don't stop on a not found, because it is not critical.  This can happen
                        // when two pullers are pulling the same data at the same time.  One will
                        // insert JUST the document (not the revisions yet), and then yield to the
                        // other which will see the document and assume revisions are there which aren't.
                        // In that case, we'd like to continue and insert the missing revisions instead of
                        // erroring out.  Note that this needs to be changed to a better way.
                        if(e.Code != StatusCode.NotFound) {
                            Log.W(TAG, "Error getting document revisions for {0}", docId);
                            throw;
                        }
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
                        throw new CouchbaseLiteException(String.Format("{0} failed validation", rev),
                            StatusCode.DbError);
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
                        sequence = localRev.Sequence;
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

                            docType = rev.GetPropertyForKey("type") as string;
                            current = true;
                        } else {
                            // It's an intermediate parent, so insert a stub:
                            newRev = new RevisionInternal(docId, revId, false);
                        }

                        // Insert it:
                        try {
                            sequence = InsertRevision(newRev, docNumericId, sequence, current, newRev.GetAttachments() != null, json, docType);
                        } catch(CouchbaseLiteException e) {
                            if(e.Code == StatusCode.DbError) {
                                var sqliteException = e.InnerException as ugly.sqlite3_exception;
                                if(sqliteException == null) {
                                    // DbError without an inner sqlite3 exception? Weird...throw
                                    throw;
                                }

                                if(sqliteException.errcode != raw.SQLITE_CONSTRAINT) {
                                    // This is a genuine error inserting the revision
                                    Log.E(TAG, "Error inserting revision {0} ({1})", newRev, sqliteException.errcode);
                                    throw;
                                } else {
                                    // This situation means that the revision already exists, so go get the existing
                                    // sequence number
                                    sequence = GetSequenceOfDocument(docNumericId, newRev.RevID, false);
                                }
                            } else {
                                throw;
                            }
                        }
                    }
                }

                if(localParentSequence == sequence) {
                    // No-op: No new revisions were inserted.
                    return true;
                }

                // Mark the latest local rev as no longer current:
                if(localParentSequence > 0) {
                    var args = new ContentValues();
                    args["current"] = 0;
                    args["doc_type"] = null;
                    int changes;
                    try {
                        changes = StorageEngine.Update("revs", args, "sequence=?", localParentSequence.ToString());
                    } catch(CouchbaseLiteException) {
                        Log.W(TAG, "Failed to update {0}", docId);
                        throw;
                    } catch(Exception e) {
                        throw new CouchbaseLiteException(String.Format("Error updating {0}", docId),
                            e) { Code = StatusCode.Exception };
                    }

                    if(changes == 0) {
                        // local parent wasn't a leaf, ergo we just created a branch
                        inConflict = true;
                    }
                }

                // Figure out what the new winning rev ID is:
                winningRevId = GetWinner(docNumericId, oldWinningRevId, oldWinnerWasDeletion, rev);
                return true;
            });
                
            Delegate.DatabaseStorageChanged(new DocumentChange(rev, winningRevId, inConflict, source));
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
                        throw new CouchbaseLiteException(String.Format("Illegal null revIds for {0}", docId),
                            StatusCode.BadParam);
                    } else if(revIDs.Count == 0) {
                        revsPurged = new List<string>();
                    } else if(revIDs.Contains("*")) {
                        // Delete all revisions if magic "*" revision ID is given:
                        try {
                            StorageEngine.Delete("revs", "doc_id=?", docNumericId.ToString());
                        } catch(CouchbaseLiteException) {
                            Log.W(TAG, "Failed to delete revisions of {0}", docId);
                            throw;
                        } catch(Exception e) {
                            throw new CouchbaseLiteException(String.Format("Error deleting revisions of {0}", docId),
                                e) { Code = StatusCode.Exception };
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
                            } catch(CouchbaseLiteException) {
                                Log.W(TAG, "Failed to delete revisions of {0}", docId);
                                throw;
                            } catch(Exception e) {
                                throw new CouchbaseLiteException(String.Format("Error deleting revisions of {0}", docId),
                                    e) { Code = StatusCode.Exception };
                            }

                            if(count != seqsToPurge.Count) {
                                Log.W(TAG, "Only {0} revisions deleted of {1}", count, String.Join(", ", seqsToPurge.ToStringArray()));
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
            }, false, "SELECT name FROM views");

            return result;
        }

        public RevisionInternal GetLocalDocument(string docId, string revId)
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

        public RevisionInternal PutLocalRevision(RevisionInternal revision, string prevRevId, bool obeyMVCC)
        {
            string docId = revision.DocID;
            if (!docId.StartsWith("_local/")) {
                Log.E(TAG, "Local revision doesn't start with '_local/'");
                throw new CouchbaseLiteException(StatusCode.BadId);
            }

            if (!obeyMVCC) {
                return PutLocalRevisionNoMvcc(revision);
            }

            if (!revision.Deleted) {
                // PUT:
                var json = EncodeDocumentJSON(revision);
                if (json == null) {
                    Log.E(TAG, "Invalid JSON in local revision");
                    throw new CouchbaseLiteException(StatusCode.BadJson);
                }

                string newRevId;
                long changes = -1;
                if (prevRevId != null) {
                    int generation = RevisionID.GetGeneration(prevRevId);
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

                return revision.Copy(docId, newRevId);
            } else {
                // DELETE:
                var status = DeleteLocalRevision(docId, prevRevId);
                if (status.IsError) {
                    throw new CouchbaseLiteException(status.Code);
                }

                return revision;
            }
        }

        #pragma warning restore 1591
        #endregion
    }
}