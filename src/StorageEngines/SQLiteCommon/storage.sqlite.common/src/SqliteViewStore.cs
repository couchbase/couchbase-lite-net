//
//  SqliteViewStore.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Couchbase.Lite.Internal;
using Couchbase.Lite.Revisions;
using Couchbase.Lite.Storage.Internal;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;
using Couchbase.Lite.Views;
using SQLitePCL;

#if SQLITE
namespace Couchbase.Lite.Storage.SystemSQLite
#else
namespace Couchbase.Lite.Storage.SQLCipher
#endif
{
    internal sealed class SqliteViewStore : IViewStore, IQueryRowStore
    {

        #region Constants

        private static readonly string Tag = typeof(SqliteViewStore).Name;
        private string _emitSql;

        #endregion

        #region Variables

        private SqliteCouchStore _dbStorage;
        private int _viewId;
        private ViewCollation _collation;
        private bool _initializedRTreeSchema;

        //TODO: Full text
        //private bool _initializedFullTextSchema;

        #endregion

        #region Properties

        public string Name { get; private set; }

        public IViewStoreDelegate Delegate { get; set; }

        public int TotalRows
        {
            get {
                var db = _dbStorage;
                var totalRows = db.QueryOrDefault<int>(c => c.GetInt(0), false, 0, "SELECT total_docs FROM views WHERE name=?", Name);
                if (totalRows == -1) { //means unknown
                    CreateIndex();
                    totalRows = db.QueryOrDefault<int>(c => c.GetInt(0), false, 0, QueryString("SELECT COUNT(*) FROM 'maps_#'"));
                    var args = new ContentValues();
                    args["total_docs"] = totalRows;
                    db.StorageEngine.Update("views", args, "view_id=?", ViewID.ToString());
                }

                Debug.Assert(totalRows >= 0);
                return totalRows;
            }
        }

        public long LastSequenceIndexed
        {
            get {
                return _dbStorage.QueryOrDefault<long>(c => c.GetLong(0), true, 0, "SELECT lastsequence FROM views WHERE name=?", Name);
            }
        }

        public long LastSequenceChangedAt
        {
            get {
                return LastSequenceIndexed;
                //FIXME: Should store this properly; it helps optimize CBLLiveQuery
            }
        }

        private int ViewID
        {
            get {
                if (_viewId < 0) {
                    _viewId = _dbStorage.QueryOrDefault<int>(c => c.GetInt(0), false, 0, "SELECT view_id FROM views WHERE name=?", Name);
                }

                return _viewId;
            }
        }

        private string MapTableName
        {
            get {
                if(_mapTableName == null) {
                    _mapTableName = ViewID.ToString();
                }

                return _mapTableName;
            }
        }
        private string _mapTableName;

        #endregion

        #region Constructors

        public static SqliteViewStore MakeViewStore(SqliteCouchStore store, string name, bool create)
        {
            var retVal = new SqliteViewStore();
            retVal._dbStorage = store;
            retVal.Name = name;
            retVal._viewId = -1;
            retVal._collation = ViewCollation.Unicode;

            if (!create && retVal.ViewID <= 0) {
                return null;
            }

            return retVal;
        }

        #endregion

        #region Private Methods

        private static string ViewNames(IEnumerable<SqliteViewStore> inputViews)
        {
            var names = inputViews.Select(x => x.Name);
            return String.Join(", ", names.ToStringArray());
        }

        private void RunStatements(string sqlStatements)
        {
            var db = _dbStorage;
            db.RunInTransaction(() =>
            {
                try {
                    _dbStorage.RunStatements(QueryString(sqlStatements));
                } catch(CouchbaseLiteException) {
                    Log.To.Database.E(Tag, "Failed to run statments ({0}), rethrowing...", 
                        new SecureLogString(sqlStatements, LogMessageSensitivity.PotentiallyInsecure));
                    throw;
                } catch(Exception e) {
                    throw Misc.CreateExceptionAndLog(Log.To.View, e, Tag, 
                        "Exception running sql statements ({0}), ",
                        new SecureLogString(sqlStatements, LogMessageSensitivity.PotentiallyInsecure));
                }

                return true;
            });
        }

        private string QueryString(string statement)
        {
            return statement.Replace("#", MapTableName);
        }

        private void CreateIndex()
        {
            const string sql = 
                "CREATE TABLE IF NOT EXISTS 'maps_#' (" +
                    "sequence INTEGER NOT NULL REFERENCES revs(sequence) ON DELETE CASCADE," +
                    "key TEXT NOT NULL COLLATE JSON," +
                    "value TEXT," +
                    "fulltext_id INTEGER, " +
                    "bbox_id INTEGER, " +
                    "geokey BLOB)";

            try {
                RunStatements(sql);
            } catch(CouchbaseLiteException) {
                Log.To.Database.E(Tag, "Couldn't create view index `{0}`, rethrowing...", Name);
                throw;
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.View, e, Tag,
                    "Couldn't create view index `{0}`", Name);
            }
        }

        private StatusCode Emit(object key, object value, bool valueIsDoc, long sequence)
        {
            var db = _dbStorage;
            string valueJSON;
            if (valueIsDoc) {
                valueJSON = "*";
            } else {
                valueJSON = Manager.GetObjectMapper().WriteValueAsString(value);
            }

            string keyJSON;
            //IEnumerable<byte> geoKey = null;
            if (false) {
                //TODO: bbox, geo, fulltext
            } else {
                keyJSON = Manager.GetObjectMapper().WriteValueAsString(key);
                Log.To.Query.V(Tag, "    emit({0}, {1}", 
                        new SecureLogString(keyJSON, LogMessageSensitivity.PotentiallyInsecure),
                        new SecureLogString(valueJSON, LogMessageSensitivity.PotentiallyInsecure));
            }

            if (keyJSON == null) {
                keyJSON = "null";
            }

            if (_emitSql == null) {
                _emitSql = QueryString("INSERT INTO 'maps_#' (sequence, key, value, " +
                "fulltext_id, bbox_id, geokey) VALUES (?, ?, ?, ?, ?, ?)");
            }

            //TODO: bbox, geo, fulltext
            try {
                db.StorageEngine.ExecSQL(_emitSql, sequence, keyJSON, valueJSON, null, null, null);
            } catch(Exception) {
                return StatusCode.DbError;
            }

            return StatusCode.Ok;
        }
            
        private void FinishCreatingIndex()
        {
            const string sql = "CREATE INDEX IF NOT EXISTS 'maps_#_keys' on 'maps_#'(key COLLATE JSON);" +
                               "CREATE INDEX IF NOT EXISTS 'maps_#_sequence' ON 'maps_#'(sequence)";

            try {
                RunStatements(sql);
            } catch(CouchbaseLiteException) {
                Log.To.View.E(Tag, "Couldn't create view SQL index `{0}`, rethrowing...", Name);
                throw;
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.View, e, Tag,
                    "Couldn't create view SQL index `{0}`", Name);
            }
        }
            
        private bool CreateRTreeSchema()
        {
            if (_initializedRTreeSchema) {
                return true;
            }

            if (raw.sqlite3_compileoption_used("SQLITE_ENABLE_RTREE") == 0) {
                Log.To.Query.W(Tag, "Can't geo-query: SQLite isn't built with the Rtree module");
                return false;
            }

            const string sql = "CREATE VIRTUAL TABLE IF NOT EXISTS bboxes USING rtree(rowid, x0, x1, y0, y1);" +
            "CREATE TRIGGER IF NOT EXISTS 'del_maps_#_bbox' " +
            "DELETE ON 'maps_#' WHEN old.bbox_id not null BEGIN " +
            "DELETE FROM bboxes WHERE rowid=old.bbox_id| END";

            try {
                RunStatements(sql);
            } catch(CouchbaseLiteException) {
                Log.To.View.E(Tag, "Error initializing rtree schema for `{0}`, rethrowing...", Name);
                throw;
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.View, e, Tag, "Error initializing rtree schema");
            }

            _initializedRTreeSchema = true;
            return true;
        }

        private static bool GroupTogether(byte[] key1, byte[] key2, int groupLevel)
        {
            if (key1 == null || key2 == null) {
                return false;
            }

            if (groupLevel == 0) {
                groupLevel = Int32.MaxValue;
            }

            return JsonCollator.Compare(JsonCollationMode.Unicode, Encoding.UTF8.GetString(key1), Encoding.UTF8.GetString(key2), groupLevel) == 0;
        }

        private static object GroupKey(byte[] keyJson, int groupLevel)
        {
            var key = FromJSON(keyJson);
            var keyList = key.AsList<object>();
            if (groupLevel > 0 && keyList != null && keyList.Count > groupLevel) {
                return new Couchbase.Lite.Util.ArraySegment<object>(keyList.ToArray(), 0, groupLevel);
            }

            return key;
        }

        private static object CallReduce(ReduceDelegate reduce, List<object> keysToReduce, List<object> valuesToReduce)
        {
            if (reduce == null) {
                return null;
            }

            var lazyKeys = new LazyJsonArray(keysToReduce);
            var lazyVals = new LazyJsonArray(valuesToReduce);

            try {
                object result = reduce(lazyKeys, lazyVals, false);
                if(result != null) {
                    return result;
                }
            } catch(Exception e) {
                Log.To.Query.E(Tag, String.Format("Failed to reduce query (keys={0} vals={1}), returning null...", 
                    new SecureLogJsonString(keysToReduce, LogMessageSensitivity.PotentiallyInsecure),
                    new SecureLogJsonString(valuesToReduce, LogMessageSensitivity.PotentiallyInsecure)), e);
            }

            return null;
        }

        private static string ToJSONString(object obj)
        {
            if (obj == null)
                return null;

            string result = null;
            try {
                result = Manager.GetObjectMapper().WriteValueAsString(obj);
            } catch (Exception e)  {
                Log.To.View.W(Tag, String.Format("Exception serializing object to json: {0}, returning null...", 
                    new SecureLogJsonString(obj, LogMessageSensitivity.PotentiallyInsecure)), e);
            }

            return result;
        }

        private static object FromJSON(IEnumerable<byte> json)
        {
            if (json == null) {
                return null;
            }

            object result = null;
            try  {
                result = Manager.GetObjectMapper().ReadValue<object>(json);
            } catch (Exception e) {
                Log.To.View.W(Tag, String.Format("Exception parsing json ({0}), returning null...",
                    new SecureLogString(json.ToArray(), LogMessageSensitivity.PotentiallyInsecure)), e);
            }

            return result;
        }

        private Status RunQuery(QueryOptions options, Func<Lazy<byte[]>, Lazy<byte[]>, string, Cursor, Status> action)
        {
            if (options == null) {
                options = new QueryOptions();
            }

            string collationStr = "";
            if (_collation == ViewCollation.ASCII) {
                collationStr = " COLLATE JSON_ASCII ";
            } else if (_collation == ViewCollation.Raw) {
                collationStr = " COLLATE JSON_RAW ";
            }

            var sql = new StringBuilder("SELECT key, value, docid, revs.sequence");
            if (options.IncludeDocs) {
                sql.Append(", revid, json");
            }

            /*if (false) {
                //TODO: bbox
                if (!CreateRTreeSchema()) {
                    return new Status(StatusCode.NotImplemented);
                }

                sql.AppendFormat(", bboxes.x0, bboxes.y0, bboxes.x1, bboxes.y1, maps_{0}.geokey", MapTableName);
            }*/

            sql.AppendFormat(" FROM 'maps_{0}', revs, docs", MapTableName);
            /*if (false) {
                //TODO: bbox
                sql.Append(", bboxes");
            }*/

            sql.Append(" WHERE 1 ");
            var args = new List<object>();
            if (options.Keys != null) {
                sql.Append(" AND key IN (");
                var item = "?";
                foreach (var key in options.Keys) {
                    sql.Append(item);
                    item = ",?";
                    args.Add(ToJSONString(key));
                }
                sql.Append(")");
            }

            var minKey = options.StartKey;
            var maxKey = options.EndKey;
            var minKeyDocId = options.StartKeyDocId;
            var maxKeyDocId = options.EndKeyDocId;
            bool inclusiveMin = options.InclusiveStart;
            bool inclusiveMax = options.InclusiveEnd;
            if (options.Descending) {
                minKey = options.EndKey;
                maxKey = options.StartKey;
                inclusiveMin = options.InclusiveEnd;
                inclusiveMax = options.InclusiveStart;
                minKeyDocId = options.EndKeyDocId;
                maxKeyDocId = options.StartKeyDocId;
            }

            if (minKey != null) {
                var minKeyData = ToJSONString(minKey);
                sql.Append(inclusiveMin ? " AND key >= ?" : " AND key > ?");
                sql.Append(collationStr);
                args.Add(minKeyData);
                if (minKeyDocId != null && inclusiveMin) {
                    sql.AppendFormat(" AND (key > ? {0} OR docid >= ?)", collationStr);
                    args.Add(minKeyData);
                    args.Add(minKeyDocId);
                }
            }

            if (maxKey != null) {
                maxKey = Misc.KeyForPrefixMatch(maxKey, options.PrefixMatchLevel);
                var maxKeyData = ToJSONString(maxKey);
                sql.Append(inclusiveMax ? " AND key <= ?" : " AND key < ?");
                sql.Append(collationStr);
                args.Add(maxKeyData);
                if (maxKeyDocId != null && inclusiveMax) {
                    sql.AppendFormat(" AND (key < ? {0} OR docid <= ?)", collationStr);
                    args.Add(maxKeyData);
                    args.Add(maxKeyDocId);
                }
            }

            /*if (false) {
                //TODO: bbox
                sql.AppendFormat(" AND (bboxes.x1 > ? AND bboxes.x0 < ?)" +
                    " AND (bboxes.y1 > ? AND bboxes.y0 < ?)" +
                    " AND bboxes.rowid = 'maps_{0}'.bbox_id", MapTableName);
                
            }*/

            sql.AppendFormat(" AND revs.sequence = 'maps_{0}'.sequence AND docs.doc_id = revs.doc_id " +
                "ORDER BY", MapTableName);
            /*if (false) {
                //TODO: bbox
                sql.Append(" bboxes.y0, bboxes.x0");
            } else {*/
                sql.Append(" key");
            //}

            sql.Append(collationStr);
            if (options.Descending) {
                sql.Append(" DESC");
            }

            sql.Append(options.Descending ? ", docid DESC" : ", docid");
            sql.Append(" LIMIT ? OFFSET ?");
            int limit = options.Limit != QueryOptions.DefaultLimit ? options.Limit : -1;
            args.Add(limit);
            args.Add(options.Skip);

            Log.To.Query.I(Tag, "Query {0}: {1}\n\tArguments: {2}", Name, sql, new SecureLogJsonString(args, LogMessageSensitivity.PotentiallyInsecure));

            var dbStorage = _dbStorage;
            var status = new Status();
            dbStorage.TryQuery(c => 
            {
                var docId = c.GetString(2);
                status = action(new Lazy<byte[]>(() => c.GetBlob(0)), new Lazy<byte[]>(() => c.GetBlob(1)), docId, c);
                if(status.IsError) {
                    return false;
                } else if((int)status.Code <= 0) {
                    status.Code = StatusCode.Ok;
                }

                return true;
            }, true, sql.ToString(), args.ToArray());

            return status;
        }

        #endregion

        #region IViewStore

        public void Close()
        {
            _dbStorage = null;
            _viewId = -1;
        }

        public void DeleteIndex()
        {
            if (ViewID <= 0) {
                return;
            }

            const string sql = "DROP TABLE IF EXISTS 'maps_#';UPDATE views SET lastSequence=0, total_docs=0 WHERE view_id=#";

            try {
                RunStatements(sql);
            } catch(CouchbaseLiteException) {
                Log.To.Database.E(Tag, "Couldn't delete view index `{0}`, rethrowing...", Name);
                throw;
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.View, e, Tag, "Couldn't delete view index `{0}`", Name);
            }
        }

        public void DeleteView()
        {
            var db = _dbStorage;
            db.RunInTransaction(() =>
            {
                DeleteIndex();
                try {
                    db.StorageEngine.Delete("views", "name=?", Name);
                } catch(CouchbaseLiteException) {
                    Log.To.Database.E(Tag, "Failed to delete view `{0}`, rethrowing...", Name);
                    throw;
                } catch(Exception e) {
                    throw Misc.CreateExceptionAndLog(Log.To.View, e, Tag, "Error deleting view {0}", Name);
                }

                return true;
            });

            _viewId = 0;
        }

        public bool SetVersion(string version)
        {
            // Update the version column in the db. This is a little weird looking because we want to
            // avoid modifying the db if the version didn't change, and because the row might not exist yet.
            var db = _dbStorage;
            var args = new ContentValues();
            args["name"] = Name;
            args["version"] = version;
            args["total_docs"] = 0;

            long changes = 0;
            try {
                changes = db.StorageEngine.InsertWithOnConflict("views", null, args, ConflictResolutionStrategy.Ignore);
            } catch(Exception) {
                return false;
            }

            if (changes > 0) {
                CreateIndex();
                return true; //created new view
            }

            try {
                args = new ContentValues();
                args["version"] = version;
                args["lastSequence"] = 0;
                args["total_docs"] = 0;
                db.StorageEngine.Update("views", args, "name=? AND version!=?", Name, version);
            } catch(Exception) {
                return false;
            }

            return true;
        }

        public bool UpdateIndexes(IEnumerable<IViewStore> inputViews)
        {
            Log.To.View.I(Tag, "Checking indexes of ({0}) for {1}", ViewNames(inputViews.Cast<SqliteViewStore>()), Name);
            var db = _dbStorage;

            var status = false;
                status = db.RunInTransaction(() =>
                {
                    long dbMaxSequence = db.LastSequence;
                    long forViewLastSequence = LastSequenceIndexed;

                    // Check whether we need to update at all,
                    // and remove obsolete emitted results from the 'maps' table:
                    long minLastSequence = dbMaxSequence;
                    long[] viewLastSequence = new long[inputViews.Count()];
                    int deletedCount = 0;
                    int i = 0;
                    HashSet<string> docTypes = new HashSet<string>();
                    IDictionary<string, string> viewDocTypes = null;
                    bool allDocTypes = false;
                    IDictionary<int, int> viewTotalRows = new Dictionary<int, int>();
                    List<SqliteViewStore> views = new List<SqliteViewStore>(inputViews.Count());
                    List<MapDelegate> mapBlocks = new List<MapDelegate>();
                    foreach (var view in inputViews.Cast<SqliteViewStore>()) {
                        var viewDelegate = view.Delegate;
                        var mapBlock = viewDelegate == null ? null : viewDelegate.Map;
                        if (mapBlock == null) {
                            Debug.Assert(view != this, String.Format("Cannot index view {0}: no map block registered", view.Name));
                            Log.To.View.V(Tag, "    {0} has no map block; skipping it", view.Name);
                            continue;
                        }

                        long last = view == this ? forViewLastSequence : view.LastSequenceIndexed;
                        if(last >= dbMaxSequence) {
                            Log.To.View.V(Tag, "{0} is already up to date, skipping...", view.Name);
                            continue;
                        }

                        views.Add(view);
                        mapBlocks.Add(mapBlock);

                        int viewId = view.ViewID;
                        Debug.Assert(viewId > 0, String.Format("View '{0}' not found in database", view.Name));

                        int totalRows = view.TotalRows;
                        viewTotalRows[viewId] = totalRows;


                        viewLastSequence[i++] = last;
                        if (last < 0) {
                            throw Misc.CreateExceptionAndLog(Log.To.View, StatusCode.DbError, Tag,
                                "Invalid last sequence indexed ({0}) received from {1}", last, view);
                        }

                        if (last < dbMaxSequence) {
                            if (last == 0) {
                                CreateIndex();
                            }

                            minLastSequence = Math.Min(minLastSequence, last);
                            Log.To.View.V(Tag, "    {0} last indexed at #{1}", view.Name, last);

                            string docType = viewDelegate.DocumentType;
                            if (docType != null) {
                                docTypes.Add(docType);
                                if (viewDocTypes == null) {
                                    viewDocTypes = new Dictionary<string, string>();
                                }

                                viewDocTypes[view.Name] = docType;
                            } else {
                                // can't filter by doc_type
                                allDocTypes = true; 
                            }

                            bool ok = true;
                            int changes = 0;
                            if (last == 0) {
                                try {
                                    // If the lastSequence has been reset to 0, make sure to remove all map results:
                                    changes = db.StorageEngine.ExecSQL(view.QueryString("DELETE FROM 'maps_#'"));
                                } catch (Exception) {
                                    ok = false;
                                }
                            } else {
                                db.OptimizeSQLIndexes(); // ensures query will use the right indexes
                                // Delete all obsolete map results (ones from since-replaced revisions):
                                try {
                                    changes = db.StorageEngine.ExecSQL(view.QueryString("DELETE FROM 'maps_#' WHERE sequence IN (" +
                                    "SELECT parent FROM revs WHERE sequence>?" +
                                    "AND +parent>0 AND +parent<=?)"), last, last);
                                } catch (Exception) {
                                    ok = false;
                                }
                            }

                            if (!ok) {
                                throw Misc.CreateExceptionAndLog(Log.To.View, StatusCode.DbError, Tag,
                                    "Error deleting obsolete map results before index update");
                            }

                            // Update #deleted rows
                            deletedCount += changes;

                            // Only count these deletes as changes if this isn't a view reset to 0
                            if (last != 0) {
                                viewTotalRows[viewId] -= changes;
                            }
                        }
                    }

                    if (minLastSequence == dbMaxSequence) {
                        return true;
                    }

                    Log.To.View.I(Tag, "Updating indexes of ({0}) from #{1} to #{2} ...",
                        ViewNames(views), minLastSequence, dbMaxSequence);

                    // This is the emit() block, which gets called from within the user-defined map() block
                    // that's called down below.
                    SqliteViewStore currentView = null;
                    IDictionary<string, object> currentDoc = null;
                    long sequence = minLastSequence;
                    Status emitStatus = new Status(StatusCode.Ok);
                    int insertedCount = 0;
                    EmitDelegate emit = (key, value) =>
                    {
                        if(key == null) {
                            Log.To.View.W(Tag, "Emit function called with a null key; ignoring");
                            return;
                        }

                        StatusCode s = currentView.Emit(key, value, value == currentDoc, sequence);
                        if (s != StatusCode.Ok) {
                            emitStatus.Code = s;
                        } else {
                            viewTotalRows[currentView.ViewID] += 1;
                            insertedCount++;
                        }
                    };

                    // Now scan every revision added since the last time the views were indexed:
                    bool checkDocTypes = docTypes.Count > 1 || (allDocTypes && docTypes.Count > 0);
                    var sql = new StringBuilder("SELECT revs.doc_id, sequence, docid, revid, json, deleted ");
                    if (checkDocTypes) {
                        sql.Append(", doc_type ");
                    }

                    sql.Append("FROM revs, docs WHERE sequence>? AND sequence <=? AND current!=0 ");
                    if (minLastSequence == 0) {
                        sql.Append("AND deleted=0 ");
                    }

                    if (!allDocTypes && docTypes.Count > 0) {
                    sql.AppendFormat("AND doc_type IN ({0}) ", Utility.JoinQuoted(docTypes));
                    }

                    sql.Append("AND revs.doc_id = docs.doc_id " +
                    "ORDER BY revs.doc_id, deleted, revid DESC");

                    Cursor c = null;
                    Cursor c2 = null;
                    try {
                        c = db.StorageEngine.IntransactionRawQuery(sql.ToString(), minLastSequence, dbMaxSequence);
                        bool keepGoing = c.MoveToNext();
                        while (keepGoing) {
                            // Get row values now, before the code below advances 'c':
                            long doc_id = c.GetLong(0);
                            sequence = c.GetLong(1);
                            string docId = c.GetString(2);
                            if (docId.StartsWith("_design/")) { // design documents don't get indexed
                                keepGoing = c.MoveToNext();
                                continue;
                            }

                            string revId = c.GetString(3);
                            var json = c.GetBlob(4);
                            bool deleted = c.GetInt(5) != 0;
                            string docType = checkDocTypes ? c.GetString(6) : null;

                            // Skip rows with the same doc_id -- these are losing conflicts.
                            var conflicts = default(List<string>);
                            while ((keepGoing = c.MoveToNext()) && c.GetLong(0) == doc_id) {
                                if(conflicts == null) {
                                    conflicts = new List<string>();
                                }
                                
                                conflicts.Add(c.GetString(3));
                            }

                            long realSequence = sequence; // because sequence may be changed, below
                            if (minLastSequence > 0) {
                                // Find conflicts with documents from previous indexings.
                                using (c2 = db.StorageEngine.IntransactionRawQuery("SELECT revid, sequence FROM revs " +
                                  "WHERE doc_id=? AND sequence<=? AND current!=0 AND deleted=0 " +
                                  "ORDER BY revID DESC ", doc_id, minLastSequence)) {

                                    if (c2.MoveToNext()) {
                                        string oldRevId = c2.GetString(0);
                                        // This is the revision that used to be the 'winner'.
                                        // Remove its emitted rows:
                                        long oldSequence = c2.GetLong(1);
                                        foreach (var view in views) {
                                            int changes = db.StorageEngine.ExecSQL(QueryString("DELETE FROM 'maps_#' WHERE sequence=?"), oldSequence);
                                            deletedCount += changes;
                                            viewTotalRows[view.ViewID] -= changes;
                                        }

                                        if (deleted || RevisionID.CBLCompareRevIDs(oldRevId, revId) > 0) {
                                            // It still 'wins' the conflict, so it's the one that
                                            // should be mapped [again], not the current revision!
                                            revId = oldRevId;
                                            deleted = false;
                                            sequence = oldSequence;
                                            json = db.QueryOrDefault<byte[]>(x => x.GetBlob(0), true, null, "SELECT json FROM revs WHERE sequence=?", sequence);
                                        }

                                        if (!deleted) {
                                            // Conflict revisions:
                                            if (conflicts == null) {
                                                conflicts = new List<string>();
                                            }

                                            conflicts.Add(oldRevId);
                                            while (c2.MoveToNext()) {
                                                conflicts.Add(c2.GetString(0));
                                            }
                                        }
                                    }
                                }
                            }

                            if (deleted) {
                                continue;
                            }

                            // Get the document properties, to pass to the map function:
                            currentDoc = db.GetDocumentProperties(json, docId, revId, deleted, sequence);
                            if (currentDoc == null) {
                                Log.To.View.W(Tag, "Failed to parse JSON of doc {0} rev {1}, skipping...",
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure), revId);
                                continue;
                            }

                            currentDoc["_local_seq"] = sequence;
                            if(conflicts != null) {
                                currentDoc["_conflicts"] = conflicts;
                            }

                            // Call the user-defined map() to emit new key/value pairs from this revision:
                            int viewIndex = -1;
                            var e = views.GetEnumerator();
                            while (e.MoveNext()) {
                                currentView = e.Current;
                                ++viewIndex;
                                if (viewLastSequence[viewIndex] < realSequence) {
                                    if (checkDocTypes) {
                                        var viewDocType = viewDocTypes[currentView.Name];
                                        if (viewDocType != null && viewDocType != docType) {
                                            // skip; view's documentType doesn't match this doc
                                            continue;
                                        }
                                    }

                                        Log.To.View.V(Tag, "    #{0}: map \"{1}\" for view {2}...",
                                        sequence, docId, e.Current.Name);
                                    try {
                                        mapBlocks[viewIndex](currentDoc, emit);
                                    } catch (Exception x) {
                                        Log.To.View.E(Tag, String.Format("Exception in map() block for view {0}, cancelling update...", currentView.Name), x);
                                        emitStatus.Code = StatusCode.Exception;
                                    }

                                    if (emitStatus.IsError) {
                                        c.Dispose();
                                        return false;
                                    }
                                }
                            }

                            currentView = null;
                        }
                    } catch(CouchbaseLiteException) {
                        Log.To.View.E(Tag, "Failed to update index for {0}, rethrowing...", currentView.Name);
                        throw;
                    } catch (Exception e) {
                        throw Misc.CreateExceptionAndLog(Log.To.View, e, Tag, "Error updating index for {0}", currentView.Name);
                    } finally {
                        if (c != null) {
                            c.Dispose();
                        }
                    }

                    // Finally, record the last revision sequence number that was indexed and update #rows:
                    foreach (var view in views) {
                        view.FinishCreatingIndex();
                        int newTotalRows = viewTotalRows[view.ViewID];
                        Debug.Assert(newTotalRows >= 0);

                        var args = new ContentValues();
                        args["lastSequence"] = dbMaxSequence;
                        args["total_docs"] = newTotalRows;
                        try {
                            db.StorageEngine.Update("views", args, "view_id=?", view.ViewID.ToString());
                        } catch (CouchbaseLiteException) {
                            Log.To.View.E(Tag, "Failed to update view {0}, rethrowing...", view.Name);
                            throw;
                        } catch(Exception e) {
                            throw Misc.CreateExceptionAndLog(Log.To.View, e, Tag, "Error updating view {0}", view.Name);
                        }
                    }

                    Log.To.View.I(Tag, "...Finished re-indexing ({0}) to #{1} (deleted {2}, added {3})",
                        ViewNames(views), dbMaxSequence, deletedCount, insertedCount);
                    return true;
                });

            if(!status) {
                Log.To.View.W(Tag, "Failed to rebuild views ({0}): {1}", ViewNames(inputViews.Cast<SqliteViewStore>()), status);
            }

            return status;
        }

        public UpdateJob CreateUpdateJob(IEnumerable<IViewStore> viewsToUpdate)
        {
            var cast = viewsToUpdate.Cast<SqliteViewStore>();
            return new UpdateJob(UpdateIndexes, viewsToUpdate, from store in cast
                                                         select store._dbStorage.LastSequence);
        }

        public IEnumerable<QueryRow> RegularQuery(QueryOptions options)
        {
            var db = _dbStorage;
            var filter = options.Filter;
            int limit = int.MaxValue;
            int skip = 0;
            if (filter != null) {
                // Custom post-filter means skip/limit apply to the filtered rows, not to the
                // underlying query, so handle them specially:
                limit = options.Limit;
                skip = options.Skip;
                options.Limit = QueryOptions.DefaultLimit;
                options.Skip = 0;
            }

            var rows = new List<QueryRow>();
            RunQuery(options, (keyData, valueData, docId, cursor) =>
            {
                long sequence = cursor.GetLong(3);
                RevisionInternal docRevision = null;
                if(options.IncludeDocs) {
                    IDictionary<string, object> value = null;
                    if(valueData != null && !RowValueIsEntireDoc(valueData.Value)) {
                        value = valueData.Value.AsDictionary<string, object>();
                    }

                    string linkedId = value == null ? null : value.GetCast<string>("_id");
                    if(linkedId != null) {
                        // Linked document: http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
                        string linkedRev = value == null ? null : value.GetCast<string>("_rev"); //usually null
                        docRevision = db.GetDocument(linkedId, linkedRev, true);
                        sequence = docRevision == null ? 0 : docRevision.Sequence;
                    } else {
                        docRevision = db.GetRevision(docId, cursor.GetString(4), false, sequence, cursor.GetBlob(5));
                    }
                }

                Log.To.Query.V(Tag, "Query {0}: Found row with key={1}, value={2}, id={3}",
                    Name, new SecureLogString(keyData.Value, LogMessageSensitivity.PotentiallyInsecure),
                    new SecureLogString(valueData.Value, LogMessageSensitivity.PotentiallyInsecure),
                    new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));

                QueryRow row = null;
                if(false) {
                    //TODO: bbox
                } else {
                    row = new QueryRow(docId, sequence, keyData.Value, valueData.Value, docRevision, this);
                }

                if(filter != null) {
                    if(!filter(row)) {
                        return new Status(StatusCode.Ok);
                    }

                    if(skip > 0) {
                        --skip;
                        return new Status(StatusCode.Ok);
                    }
                }

                rows.Add(row);
                if(limit-- == 0) {
                    return new Status(StatusCode.Reserved);
                }

                return new Status(StatusCode.Ok);
            });

            // If given keys, sort the output into that order, and add entries for missing keys:
            if (options.Keys != null) {
                // Group rows by key:
                var rowsByKey = new Dictionary<string, List<QueryRow>>();
                foreach (var row in rows) {
                    var key = ToJSONString(row.Key);
                    var dictRows = rowsByKey.Get(key);
                    if (dictRows == null) {
                        dictRows = rowsByKey[key] = new List<QueryRow>();
                    }

                    dictRows.Add(row);
                }

                // Now concatenate them in the order the keys are given in options:
                var sortedRows = new List<QueryRow>();
                foreach (var key in options.Keys.Select(x => ToJSONString(x))) {
                    if (key == null) {
                        continue;
                    }

                    var dictRows = rowsByKey.Get(key);
                    if (dictRows != null) {
                        sortedRows.AddRange(dictRows);
                    }
                }

                rows = sortedRows;
            }

            return rows;
        }

        public IEnumerable<QueryRow> ReducedQuery(QueryOptions options)
        {
            var db = _dbStorage;
            var groupLevel = options.GroupLevel;
            bool group = options.Group || groupLevel > 0;
            var reduce = Delegate.Reduce;
            if (options.ReduceSpecified) {
                if (options.Reduce && reduce == null) {
                    Log.To.Query.W(Tag, "Cannot use reduce option in view {0} which has no reduce block defined, " +
                        "returning null", Name);
                    return null;
                }
            }

            List<object> keysToReduce = null, valuesToReduce = null;
            if (reduce != null) {
                keysToReduce = new List<object>(100);
                valuesToReduce = new List<object>(100);
            }

            Lazy<byte[]> lastKeyData = null;
            List<QueryRow> rows = new List<QueryRow>();
            RunQuery(options, (keyData, valueData, docID, c) =>
            {
                var lastKeyValue = lastKeyData != null ? lastKeyData.Value : null;
                if(group && !GroupTogether(keyData.Value, lastKeyValue, groupLevel)) {
                    if(lastKeyData != null && lastKeyData.Value != null) {
                        // This pair starts a new group, so reduce & record the last one:
                        var key = GroupKey(lastKeyData.Value, groupLevel);
                        var reduced = CallReduce(reduce, keysToReduce, valuesToReduce);
                        var row = new QueryRow(null, 0, key, reduced, null, this);
                        if(options.Filter == null || options.Filter(row)) {
                            rows.Add(row);
                        }

                        keysToReduce.Clear();
                        valuesToReduce.Clear();
                    }
                    lastKeyData = keyData;
                }

                Log.To.Query.V(Tag, "Query {0}: Will reduce row with key={1}, value={2}", Name, 
                    new SecureLogString(keyData.Value, LogMessageSensitivity.PotentiallyInsecure),
                    new SecureLogString(valueData.Value, LogMessageSensitivity.PotentiallyInsecure));

                object valueOrData = FromJSON(valueData.Value);
                if(valuesToReduce != null && RowValueIsEntireDoc(valueData.Value)) {
                    // map fn emitted 'doc' as value, which was stored as a "*" placeholder; expand now:
                    try {
                        var rev = db.GetDocument(docID, c.GetLong(1));
                        valueOrData = rev.GetProperties();
                    } catch(CouchbaseLiteException) {
                        Log.To.Query.E(Tag, "Couldn't load doc for row value, rethrowing...");
                        throw;
                    }   
                }

                keysToReduce.Add(keyData.Value);
                valuesToReduce.Add(valueOrData);
                return new Status(StatusCode.Ok);
            });

            if((keysToReduce != null && keysToReduce.Count > 0) || lastKeyData != null) {
                // Finish the last group (or the entire list, if no grouping):
                var key = group ? GroupKey(lastKeyData.Value, groupLevel) : null;
                var reduced = CallReduce(reduce, keysToReduce, valuesToReduce);
                Log.To.Query.V(Tag, "Query {0}: Reduced to key={1}, value={2}", Name,
                    new SecureLogJsonString(key, LogMessageSensitivity.PotentiallyInsecure),
                    new SecureLogJsonString(reduced, LogMessageSensitivity.PotentiallyInsecure));

                var row = new QueryRow(null, 0, key, reduced, null, this);
                if (options.Filter == null || options.Filter(row)) {
                    rows.Add(row);
                }
            }

            return rows;
        }

        public IQueryRowStore StorageForQueryRow(QueryRow row)
        {
            return this;
        }

        public IEnumerable<IDictionary<string, object>> Dump()
        {
            if (ViewID <= 0) {
                return null;
            }

            List<IDictionary<string, object>> retVal = new List<IDictionary<string, object>>();
            _dbStorage.TryQuery(c =>
            {
                retVal.Add(new Dictionary<string, object>() {
                    { "seq", c.GetLong(0) },
                    { "key", c.GetString(1) },
                    { "val", c.GetString(2) }
                });

                return true;
            }, false, QueryString("SELECT sequence, key, value FROM 'maps_#' ORDER BY key"));

            return retVal;
        }

        #endregion

        #region IQueryRowStore

        public bool RowValueIsEntireDoc(object valueData)
        {
            var valueString = valueData as IEnumerable<byte>;
            if (valueString == null) {
                return false;
            }

            bool first = true;
            foreach (var character in valueString) {
                if (!first) {
                    return false;
                }

                if (character != (byte)'*') {
                    return false;
                }

                first = false;
            }

            return true;
        }

        public T ParseRowValue<T>(IEnumerable<byte> valueData)
        {
            return Manager.GetObjectMapper().ReadValue<T>(valueData);
        }

        public IDictionary<string, object> DocumentProperties(string docId, long sequenceNumber)
        {
            return _dbStorage.GetDocument(docId, sequenceNumber).GetProperties();
        }

        #endregion
    }
}
