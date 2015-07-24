//
// View.cs
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

using Couchbase.Lite.Util;
using Couchbase.Lite.Storage;
using Sharpen;
using Couchbase.Lite.Internal;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections;
using Couchbase.Lite.Views;
using System.Diagnostics;
using System.Text;

namespace Couchbase.Lite {

    // TODO: Either remove or update the API defs to indicate the enum value changes, and global scope.
    /// <summary>
    /// Indicates the collation to use for sorted items in the view
    /// </summary>
    [Serializable]
    public enum ViewCollation
    {
        /// <summary>
        /// Sort via the unicode standard
        /// </summary>
        Unicode,
        /// <summary>
        /// Raw binary sort
        /// </summary>
        Raw,
        /// <summary>
        /// Sort via ASCII comparison
        /// </summary>
        ASCII
    }

    /// <summary>
    /// A Couchbase Lite <see cref="Couchbase.Lite.View"/>. 
    /// A <see cref="Couchbase.Lite.View"/> defines a persistent index managed by map/reduce.
    /// </summary>
    public sealed class View {

    #region Constructors

        internal View(Database database, String name)
        {
            Database = database;
            Name = name;
            _id = -1;
            // means 'unknown'
            Collation = ViewCollation.Unicode;
        }

    #endregion

        #region Variables

        internal event TypedEventHandler<View, EventArgs> Changed;

        private readonly object _updateLock = new object();

        private string _emitSql;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets an object that can compile source code into map and reduce delegates.
        /// </summary>
        /// <value>The compiler object.</value>
        public static IViewCompiler Compiler { get; set; }

        private string MapTableName
        {
            get {
                if(_mapTableName == null) {
                    _mapTableName = Id.ToString();
                }

                return _mapTableName;
            }
        }
        private string _mapTableName;

    #endregion
    
    #region Constants

        internal const String Tag = "View";

        const Int32 ReduceBatchSize = 100;

    #endregion

    #region Non-public Members

        private Int32 _id;

        internal ViewCollation Collation { get; set; }

        internal Int32 Id {
            get {
                if (_id < 0)
                {
                    string sql = "SELECT view_id FROM views WHERE name=?";
                    var args = new [] { Name };
                    Cursor cursor = null;
                    try
                    {
                        cursor = Database.StorageEngine.RawQuery(sql, args);
                        if (cursor.MoveToNext())
                        {
                            _id = cursor.GetInt(0);
                        }
                        else
                        {
                            _id = 0;
                        }
                    }
                    catch (SQLException e)
                    {
                        Log.E(Database.Tag, "Error getting view id", e);
                        _id = 0;
                    }
                    finally
                    {
                        if (cursor != null)
                        {
                            cursor.Close();
                        }
                    }
                }
                return _id;
            }
        }

        internal void DatabaseClosing()
        {
            Database = null;
            _id = 0;
        }

        internal void UpdateIndex()
        {
            UpdateIndexes(new List<View> { this });
        }

        internal void UpdateIndexes(IEnumerable<View> inputViews)
        {
            Log.D(Tag, "Checking indexes of ({0}) for {1}", ViewNames(inputViews), Name);
            var db = Database;

            lock (_updateLock) {
                var success = db.RunInTransaction(() =>
                {
                    // If the view the update is for doesn't need any update, don't do anything:
                    long dbMaxSequence = db.LastSequenceNumber;
                    long forViewLastSequence = LastSequenceIndexed;
                    if (forViewLastSequence >= dbMaxSequence) {
                        return true;
                    }

                    // Check whether we need to update at all,
                    // and remove obsolete emitted results from the 'maps' table:
                    long minLastSequence = dbMaxSequence;
                    long[] viewLastSequence = new long[inputViews.Count()];
                    int deletedCount = 0;
                    int i = 0;
                    HashSet<string> docTypes = new HashSet<string>();
                    IDictionary<string, string> viewDocTypes = null;
                    bool allDocTypes = true;
                    IDictionary<int, int> viewTotalRows = new Dictionary<int, int>();
                    List<View> views = new List<View>(inputViews.Count());
                    List<MapDelegate> mapBlocks = new List<MapDelegate>();
                    foreach (var view in inputViews) {
                        var mapBlock = Map;
                        if (mapBlock == null) {
                            Debug.Assert(view != this, String.Format("Cannot index view {0}: no map block registered", view.Name));
                            Log.V(Tag, "    {0} has no map block; skipping it", view.Name);
                            continue;
                        }

                        views.Add(view);
                        mapBlocks.Add(mapBlock);

                        int viewId = view.Id;
                        Debug.Assert(viewId > 0, String.Format("View '{0}' not found in database", view.Name));

                        int totalRows = view.TotalRows;
                        viewTotalRows[viewId] = totalRows;

                        long last = view == this ? forViewLastSequence : view.LastSequenceIndexed;
                        viewLastSequence[i++] = last;
                        if (last < 0) {
                            return false;
                        }

                        if (last < dbMaxSequence) {
                            if (last == 0) {
                                CreateIndex();
                            }

                            minLastSequence = Math.Min(minLastSequence, last);
                            Log.V(Tag, "    {0} last indexed at #{1}", view.Name, last);

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
                                return false;
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

                    Log.D(Tag, "Updating indexes of ({0}) from #{1} to #{2} ...",
                        ViewNames(views), minLastSequence, dbMaxSequence);

                    // This is the emit() block, which gets called from within the user-defined map() block
                    // that's called down below.
                    View currentView = null;
                    IDictionary<string, object> currentDoc = null;
                    long sequence = minLastSequence;
                    Status emitStatus = new Status(StatusCode.Ok);
                    int insertedCount = 0;
                    EmitDelegate emit = (key, value) =>
                    {
                        StatusCode s = currentView.Emit(key, value, value == currentDoc, sequence);
                        if (s != StatusCode.Ok) {
                            emitStatus.Code = s;
                        } else {
                            viewTotalRows[currentView.Id] += 1;
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
                        sql.AppendFormat("AND doc_type IN ({0}) ", Database.JoinQuoted(docTypes.ToList()));
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
                            while ((keepGoing = c.MoveToNext()) && c.GetLong(0) == doc_id) {
                            }

                            long realSequence = sequence; // because sequence may be changed, below
                            if (minLastSequence > 0) {
                                // Find conflicts with documents from previous indexings.
                                using (c2 = db.StorageEngine.IntransactionRawQuery("SELECT revid, sequence FROM revs " +
                                    "WHERE doc_id=? AND sequence<=? AND current!=0 AND deleted=0 " +
                                    "ORDER BY revID DESC " +
                                    "LIMIT 1", doc_id, minLastSequence)) {

                                    if (c2.MoveToNext()) {
                                        string oldRevId = c2.GetString(0);
                                        // This is the revision that used to be the 'winner'.
                                        // Remove its emitted rows:
                                        long oldSequence = c2.GetLong(1);
                                        foreach (var view in views) {
                                            int changes = db.StorageEngine.ExecSQL(QueryString("DELETE FROM 'maps_#' WHERE sequence=?"), oldSequence);
                                            deletedCount += changes;
                                            viewTotalRows[view.Id] -= changes;
                                        }

                                        if (deleted || RevisionInternal.CBLCompareRevIDs(oldRevId, revId) > 0) {
                                            // It still 'wins' the conflict, so it's the one that
                                            // should be mapped [again], not the current revision!
                                            revId = oldRevId;
                                            deleted = false;
                                            sequence = oldSequence;
                                            json = db.QueryOrDefault<byte[]>(x => x.GetBlob(0), true, null, "SELECT json FROM revs WHERE sequence=?", sequence);

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
                                Log.W(Tag, "Failed to parse JSON of doc {0} rev {1}", docId, revId);
                                continue;
                            }

                            currentDoc["_local_seq"] = sequence;

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

                                    Log.V(Tag, "    #{0}: map \"{1}\" for view {2}...",
                                        sequence, docId, e.Current.Name);
                                    try {
                                        mapBlocks[viewIndex](currentDoc, emit);
                                    } catch (Exception x) {
                                        Log.E(Tag, String.Format("Exception in map() block for view {0}", currentView.Name), x);
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
                    } catch (Exception) {
                        return false;
                    } finally {
                        if (c != null) {
                            c.Dispose();
                        }
                    }

                    // Finally, record the last revision sequence number that was indexed and update #rows:
                    foreach (var view in views) {
                        view.FinishCreatingIndex();
                        int newTotalRows = viewTotalRows[view.Id];
                        Debug.Assert(newTotalRows >= 0);

                        var args = new ContentValues();
                        args["lastSequence"] = dbMaxSequence;
                        args["total_docs"] = newTotalRows;
                        try {
                            db.StorageEngine.Update("views", args, "view_id=?", view.Id.ToString());
                        } catch (Exception) {
                            return false;
                        }
                    }

                    Log.D(Tag, "...Finished re-indexing ({0}) to #{1} (deleted {2}, added {3})",
                        ViewNames(views), dbMaxSequence, deletedCount, insertedCount);
                    return true;
                });

                if(!success) {
                    Log.W(Tag, "CouchbaseLite: Failed to rebuild views ({0})", ViewNames(inputViews));
                }
            }
        }

        private StatusCode Emit(object key, object value, bool valueIsDoc, long sequence)
        {
            var db = Database;
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
                Log.V(Tag, "    emit({0}, {1}", keyJSON, valueJSON);
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

            if (!RunStatements(sql)) {
                Log.W(Tag, "Couldn't create view index `{0}`", Name);
            }
        }

        private void FinishCreatingIndex()
        {
            const string sql = "CREATE INDEX IF NOT EXISTS 'maps_#_keys' on 'maps_#'(key COLLATE JSON);" +
                "CREATE INDEX IF NOT EXISTS 'maps_#_sequence' ON 'maps_#'(sequence)";

            if (!RunStatements(sql)) {
                Log.W(Tag, "Couldn't create view SQL index `{0}`", Name);
            }
        }

        private bool RunStatements(string sqlStatements)
        {
            var db = Database;
            return db.RunInTransaction(() =>
            {
                if(db.RunStatements(QueryString(sqlStatements))) {
                    return true;
                }

                return false;
            });
        }

        private string QueryString(string statement)
        {
            return statement.Replace("#", MapTableName);
        }

        private static string ViewNames(IEnumerable<View> inputViews)
        {
            var names = inputViews.Select(x => x.Name);
            return String.Join(", ", names.ToStringArray());
        }

        private bool GroupOrReduce(QueryOptions options) {
            if (options.Group|| options.GroupLevel> 0) {
                return true;
            }

            if (options.ReduceSpecified) {
                return options.Reduce;
            }

            return Reduce != null;
        }

        /// <summary>Queries the view.</summary>
        /// <remarks>Queries the view. Does NOT first update the index.</remarks>
        /// <param name="options">The options to use.</param>
        /// <returns>An array of QueryRow objects.</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IEnumerable<QueryRow> QueryWithOptions(QueryOptions options)
        {
            if (options == null)
                options = new QueryOptions();

            Cursor cursor = null;
            IList<QueryRow> rows = new List<QueryRow>();
            try
            {
                cursor = ResultSetWithOptions(options);
                int groupLevel = options.GroupLevel;
                var group = options.Group || (groupLevel > 0);
                var reduceBlock = Reduce;
                var reduce = GroupOrReduce(options);

                if (reduce && (reduceBlock == null) && !group)
                {
                    var msg = "Cannot use reduce option in view " + Name + " which has no reduce block defined";
                    Log.W(Database.Tag, msg);
                    throw new CouchbaseLiteException(StatusCode.BadRequest);
                }

                if (reduce || group)
                {
                    // Reduced or grouped query:
                    rows = ReducedQuery(cursor, group, groupLevel);
                }
                else
                {
                    // regular query
                    cursor.MoveToNext();
                    while (!cursor.IsAfterLast())
                    {
                        var key = FromJSON(cursor.GetBlob(0));
                        var value = FromJSON(cursor.GetBlob(1));
                        var docId = cursor.GetString(2);
                        var sequenceLong = cursor.GetLong(3);
                        var sequence = Convert.ToInt32(sequenceLong);


                        IDictionary<string, object> docContents = null;
                        if (options.IncludeDocs)
                        {
                            // http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
                            if (value is IDictionary<string,object> && ((IDictionary<string,object>)value).ContainsKey("_id"))
                            {
                                var linkedDocId = (string)((IDictionary<string,object>)value).Get("_id");
                                var linkedDoc = Database.GetDocumentWithIDAndRev(linkedDocId, null, DocumentContentOptions.None);
                                docContents = linkedDoc.GetProperties();
                            }
                            else
                            {
                                var revId = cursor.GetString(4);
                                docContents = Database.DocumentPropertiesFromJSON(cursor.GetBlob(5), docId, revId, false, sequenceLong, options.ContentOptions);
                            }
                        }
                        var row = new QueryRow(docId, sequence, key, value, docContents);
                        row.Database = Database;
                        rows.AddItem<QueryRow>(row);  // NOTE.ZJG: Change to `yield return row` to convert to a generator.
                        cursor.MoveToNext();
                    }
                }
            }
            catch (SQLException e)
            {
                var errMsg = string.Format("Error querying view: {0}", this);
                Log.E(Database.Tag, errMsg, e);
                throw new CouchbaseLiteException(errMsg, e, new Status(StatusCode.DbError));
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return rows;
        }

        internal IList<IDictionary<string, object>> dump() 
        {
            if (Id < 0)
            {
                return null;
            }
                
            var selectArgs  = new[] { Id.ToString() };

            Cursor cursor = null;

            var result = new List<IDictionary<string, object>>();

            try
            {
                cursor = Database.StorageEngine.
                    RawQuery("SELECT sequence, key, value FROM map WHERE view_id=? ORDER BY key", selectArgs);

                while(cursor.MoveToNext())
                {
                    var row = new Dictionary<string, object>();
                    row["seq"] = cursor.GetInt(0);
                    row["key"] = cursor.GetString(1);
                    row["value"] = cursor.GetString(2);
                    result.Add(row);
                }
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error dumping view", e);
                result = null;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }

            return result;
        }

        internal IList<IDictionary<string, object>> Dump()
        {
            if (Id < 0)
            {
                return null;
            }

            var result = new List<IDictionary<string, object>>();

            var selectArgs = new string[] { Id.ToString() };

            Cursor cursor = null;
            try
            {
                cursor = Database.StorageEngine.RawQuery(
                    "SELECT sequence, key, value FROM maps WHERE view_id=? ORDER BY key", selectArgs);

                while (cursor.MoveToNext()) 
                {
                    var row = new Dictionary<string, object>();
                    row.Put("seq", cursor.GetInt(0));
                    row.Put("key", cursor.GetString(1));
                    row.Put("value", cursor.GetString(2));
                    result.AddItem(row);
                }

            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error dumping view", e);
                result = null;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }

            return result;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IList<QueryRow> ReducedQuery(Cursor cursor, Boolean group, Int32 groupLevel)
        {
            IList<object> keysToReduce = null;
            IList<object> valuesToReduce = null;
            Lazy<object> lastKey = null;

            var reduce = Reduce;
            // FIXME: If reduce is null, then so are keysToReduce and ValuesToReduce, which can throw an NRE below.
            if (reduce != null)
            {
                keysToReduce = new List<Object>(ReduceBatchSize);
                valuesToReduce = new List<Object>(ReduceBatchSize);
            }

            var rows = new List<QueryRow>();
            cursor.MoveToNext();

            while (!cursor.IsAfterLast())
            {
                var lazyKey = new Lazy<Object>(()=>FromJSON(cursor.GetBlob(0)));
                var lazyValue = new Lazy<Object>(()=>FromJSON(cursor.GetBlob(1)));

                System.Diagnostics.Debug.Assert((lazyKey != null));
                if (group && !GroupTogether(lazyKey, lastKey, groupLevel))
                {
                    if (lastKey != null && lastKey.Value != null)
                    {
                        // This pair starts a new group, so reduce & record the last one:
                        var reduced = (reduce != null) 
                            ? reduce(keysToReduce, valuesToReduce, false) 
                            : null;
                        var key = GroupKey(lastKey.Value, groupLevel);
                        var row = new QueryRow(null, 0, key, reduced, null)
                        {
                            Database = Database
                        };
                        rows.AddItem(row); // NOTE.ZJG: Change to `yield return row` to convert to a generator.

                        keysToReduce.Clear();
                        valuesToReduce.Clear();
                    }
                    lastKey = lazyKey;
                }
                keysToReduce.AddItem(lazyKey.Value);
                valuesToReduce.AddItem(lazyValue.Value);
                cursor.MoveToNext();
            }
            // NOTE.ZJG: Need to handle grouping differently if switching this to a generator.
            if (keysToReduce.Count > 0)
            {
                // Finish the last group (or the entire list, if no grouping):
                var key = group ? GroupKey(lastKey.Value, groupLevel) : null;
                var reduced = (reduce != null) ? reduce(keysToReduce, valuesToReduce, false) : null;
                var row = new QueryRow(null, 0, key, reduced, null);
                row.Database = Database;
                rows.AddItem(row);
            }
            return rows;
        }
            
        /// <summary>
        /// Checks if two keys belong in the same grouping level (i.e. they are equal at all
        /// levels up to and including groupLevel
        /// </summary>
        /// <returns><c>true</c>, if the two keys belong in the same grouping level,
        ///  <c>false</c> otherwise.</returns>
        /// <param name="key1">Key1.</param>
        /// <param name="key2">Key2.</param>
        /// <param name="groupLevel">Group level.</param>
        public static bool GroupTogether(Lazy<object> key1, Lazy<object> key2, int groupLevel)
        {
            var key1List = key1 == null ? null : key1.Value as IList;
            var key2List = key2 == null ? null : key2.Value as IList;
            if (groupLevel == 0 || key1List == null || key2List == null) {
                var key2val = key2 != null 
                    ? key2.Value 
                    : null;
                return key1.Value.Equals(key2val);
            }

            var end = Math.Min(groupLevel, Math.Min(key1List.Count, key2List.Count));
            for (int i = 0; i < end; ++i) {
                if (!key1List[i].Equals(key2List[i])) {
                    return false;
                }
            }

            return true;
        }
            
        /// <summary>
        /// Returns the prefix of the key to use in the result row, at this groupLevel
        /// </summary>
        /// <returns>The prefix of the key to use in the result row</returns>
        /// <param name="key">The key to check.</param>
        /// <param name="groupLevel">The group level to use.</param>
        public static object GroupKey(object key, int groupLevel)
        {
            
            if (groupLevel > 0) {
                var keyList = key.AsList<object>();
                if (keyList == null) {
                    return key;
                }

                return keyList.SubList(0, groupLevel);
            }
            else {
                return key;
            }
        }

        internal Cursor ResultSetWithOptions(QueryOptions options)
        {
            if (options == null) {
                options = new QueryOptions();
            }

            string collationStr = "";
            if (Collation == ViewCollation.ASCII) {
                collationStr = " COLLATE JSON_ASCII ";
            } else if (Collation == ViewCollation.Raw) {
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
                maxKey = KeyForPrefixMatch(maxKey, options.PrefixMatchLevel);
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
            int limit = options.Limit != QueryOptions.DEFAULT_LIMIT ? options.Limit : -1;
            args.Add(limit);
            args.Add(options.Skip);

            Log.D(Tag, "Query {0}: {1}\n\tArguments: {2}", Name, sql, Manager.GetObjectMapper().WriteValueAsString(args));

            var dbStorage = Database;
            var status = new Status();
            return dbStorage.StorageEngine.IntransactionRawQuery(sql.ToString(), args.ToArray());

        }

        private object KeyForPrefixMatch(object key, int depth)
        {
            if(depth < 1) {
                return key;
            }

            var keyStr = key as string;
            if (keyStr != null) {
                // Kludge: prefix match a string by appending max possible character value to it
                return keyStr + "\uffffffff";
            }

            var keyList = key as IList;
            if (keyList != null) {
                if (depth == 1) {
                    keyList.Add(new Dictionary<string, object>());
                } else {
                    var lastObject = KeyForPrefixMatch(keyList[keyList.Count - 1], depth - 1);
                    keyList[keyList.Count - 1] = lastObject;
                }

                return keyList;
            }

            return key;
        }

        /// <summary>Indexing</summary>
        internal string ToJSONString(Object obj)
        {
            if (obj == null)
                return null;

            String result = null;
            try
            {
                result = Manager.GetObjectMapper().WriteValueAsString(obj);
            }
            catch (Exception e)
            {
                Log.W(Database.Tag, "Exception serializing object to json: " + obj, e);
            }
            return result;
        }

        internal Object FromJSON(IEnumerable<Byte> json)
        {
            if (json == null)
            {
                return null;
            }
            object result = null;
            try
            {
                result = Manager.GetObjectMapper().ReadValue<Object>(json);
            }
            catch (Exception e)
            {
                Log.W(Database.Tag, "Exception parsing json", e);
            }
            return result;
        }

        internal static double TotalValues(IList<object> values)
        {
            double total = 0;
            foreach (object o in values)
            {
                try {
                    double number = Convert.ToDouble(o);
                    total += number;
                } 
                catch (Exception e)
                {
                    Log.E(Database.Tag, "Warning non-numeric value found in totalValues: " + o, e);
                }
            }
            return total;
        }

        internal Status CompileFromDesignDoc()
        {
            if (Map != null) {
                return new Status(StatusCode.Ok);
            }

            string language = null;
            var viewProps = Database.GetDesignDocFunction(Name, "views", out language).AsDictionary<string, object>();
            if (viewProps == null) {
                return new Status(StatusCode.NotFound);
            }

            Log.D(Tag, "{0}: Attempting to compile {1} from design doc", Name, language);
            if (Compiler == null) {
                return new Status(StatusCode.NotImplemented);
            }

            return Compile(viewProps, language);
        }

        internal Status Compile(IDictionary<string, object> viewProps, string language)
        {
            language = language ?? "javascript";
            string mapSource = viewProps.Get("map") as string;
            if (mapSource == null) {
                return new Status(StatusCode.NotFound);
            }

            MapDelegate mapDelegate = Compiler.CompileMap(mapSource, language);
            if (mapDelegate == null) {
                Log.W(Tag, "View {0} could not compile {1} map fn: {2}", Name, language, mapSource);
                return new Status(StatusCode.CallbackError);
            }

            string reduceSource = viewProps.Get("reduce") as string;
            ReduceDelegate reduceDelegate = null;
            if (reduceSource != null) {
                reduceDelegate = Compiler.CompileReduce(reduceSource, language);
                if (reduceDelegate == null) {
                    Log.W(Tag, "View {0} could not compile {1} reduce fn: {2}", Name, language, mapSource);
                    return new Status(StatusCode.CallbackError);
                }
            }
                
            string version = Misc.HexSHA1Digest(Manager.GetObjectMapper().WriteValueAsBytes(viewProps));
            SetMapReduce(mapDelegate, reduceDelegate, version);
            //TODO: DocumentType

            var options = viewProps.Get("options").AsDictionary<string, object>();
            Collation = ViewCollation.Unicode;
            if (options != null && options.ContainsKey("collation")) {
                string collation = options["collation"] as string;
                if (collation.ToLower().Equals("raw")) {
                    Collation = ViewCollation.Raw;
                }
            }

            return new Status(StatusCode.Ok);
        }

    #endregion

    #region Instance Members
        /// <summary>
        /// Get the <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        /// <value>
        /// The <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.View"/>.
        /// </value>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s name.
        /// </summary>
        /// <value>the <see cref="Couchbase.Lite.View"/>'s name.</value>
        public String Name { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s <see cref="Couchbase.Lite.MapDelegate"/>.
        /// </summary>
        /// <value>The map function.</value>
        public MapDelegate Map { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s <see cref="Couchbase.Lite.ReduceDelegate"/>.
        /// </summary>
        /// <value>The reduce function.</value>
        public ReduceDelegate Reduce { get; set; }

        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.View"/>'s indices are currently out of date.
        /// </summary>
        /// <value><c>true</c> if this instance is stale; otherwise, <c>false</c>.</value>
        public Boolean IsStale { get { return (LastSequenceIndexed < Database.GetLastSequenceNumber()); } }

        /// <summary>
        /// Gets the last sequence number indexed so far.
        /// </summary>
        /// <value>The last sequence number indexed.</value>
        public Int64 LastSequenceIndexed { 
            get {
                var sql = "SELECT lastSequence FROM views WHERE name=?";
                var args = new[] { Name };
                Cursor cursor = null;
                var result = -1L;
                try {
                    cursor = Database.StorageEngine.RawQuery(sql, args);
                    if (cursor.MoveToNext()) {
                        result = cursor.GetLong(0);
                    }
                } catch (SQLException) {
                    Log.E(Database.Tag, "Error getting last sequence indexed");
                } finally {
                    if (cursor != null) {
                        cursor.Dispose();
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the total number of rows present in the view
        /// </summary>
        public int TotalRows {
            get {
                var db = Database;
                var totalRows = db.QueryOrDefault<int>(c => c.GetInt(0), false, 0, "SELECT total_docs FROM views WHERE name=?", Name);
                if (totalRows == -1) { //means unknown
                    CreateIndex();
                    totalRows = db.QueryOrDefault<int>(c => c.GetInt(0), true, 0, QueryString("SELECT COUNT(*) FROM 'maps_#'"));
                    var args = new ContentValues();
                    args["total_docs"] = totalRows;
                    db.StorageEngine.Update("views", args, "view_id=?", Id.ToString());
                }

                Debug.Assert(totalRows >= 0);
                return totalRows;
            }
        }

        /// <summary>
        /// Defines the <see cref="Couchbase.Lite.View"/>'s <see cref="Couchbase.Lite.MapDelegate"/> and sets 
        /// its <see cref="Couchbase.Lite.ReduceDelegate"/> to null.
        /// </summary>
        /// <returns>
        /// True if the <see cref="Couchbase.Lite.MapDelegate"/> was set, otherwise false. If the values provided are 
        /// identical to the values that are already set, then the values will not be updated and false will be returned.  
        /// In addition, if true is returned, the index was deleted and will be rebuilt on the next 
        /// <see cref="Couchbase.Lite.Query"/> execution.
        /// </returns>
        /// <param name="mapDelegate">The <see cref="Couchbase.Lite.MapDelegate"/> to set</param>
        /// <param name="version">
        /// The key of the property value to return. The value of this parameter must change when 
        /// the <see cref="Couchbase.Lite.MapDelegate"/> is changed in a way that will cause it to 
        /// produce different results.
        /// </param>
        public Boolean SetMap(MapDelegate mapDelegate, String version) {
            return SetMapReduce(mapDelegate, null, version);
        }

        /// <summary>
        /// Defines the View's <see cref="Couchbase.Lite.MapDelegate"/> 
        /// and <see cref="Couchbase.Lite.ReduceDelegate"/>.
        /// </summary>
        /// <remarks>
        /// Defines a view's functions.
        /// The view's definition is given as a class that conforms to the Mapper or
        /// Reducer interface (or null to delete the view). The body of the block
        /// should call the 'emit' object (passed in as a paramter) for every key/value pair
        /// it wants to write to the view.
        /// Since the function itself is obviously not stored in the database (only a unique
        /// string idenfitying it), you must re-define the view on every launch of the app!
        /// If the database needs to rebuild the view but the function hasn't been defined yet,
        /// it will fail and the view will be empty, causing weird problems later on.
        /// It is very important that this block be a law-abiding map function! As in other
        /// languages, it must be a "pure" function, with no side effects, that always emits
        /// the same values given the same input document. That means that it should not access
        /// or change any external state; be careful, since callbacks make that so easy that you
        /// might do it inadvertently!  The callback may be called on any thread, or on
        /// multiple threads simultaneously. This won't be a problem if the code is "pure" as
        /// described above, since it will as a consequence also be thread-safe.
        /// </remarks>
        /// <returns>
        /// <c>true</c> if the <see cref="Couchbase.Lite.MapDelegate"/> 
        /// and <see cref="Couchbase.Lite.ReduceDelegate"/> were set, otherwise <c>false</c>.
        /// If the values provided are identical to the values that are already set, 
        /// then the values will not be updated and <c>false</c> will be returned. 
        /// In addition, if <c>true</c> is returned, the index was deleted and 
        /// will be rebuilt on the next <see cref="Couchbase.Lite.Query"/> execution.
        /// </returns>
        /// <param name="map">The <see cref="Couchbase.Lite.MapDelegate"/> to set.</param>
        /// <param name="reduce">The <see cref="Couchbase.Lite.ReduceDelegate"/> to set.</param>
        /// <param name="version">
        /// The key of the property value to return. The value of this parameter must change 
        /// when the <see cref="Couchbase.Lite.MapDelegate"/> and/or <see cref="Couchbase.Lite.ReduceDelegate"/> 
        /// are changed in a way that will cause them to produce different results.
        /// </param>
        public Boolean SetMapReduce(MapDelegate map, ReduceDelegate reduce, String version) { 
            System.Diagnostics.Debug.Assert(map != null);
            System.Diagnostics.Debug.Assert(version != null); // String.Empty is valid.

            Map = map;
            Reduce = reduce;

            if (!Database.Open())
            {
                return false;
            }
            // Update the version column in the database. This is a little weird looking
            // because we want to
            // avoid modifying the database if the version didn't change, and because the
            // row might not exist yet.
            var storageEngine = this.Database.StorageEngine;

            // Older Android doesnt have reliable insert or ignore, will to 2 step
            // FIXME review need for change to execSQL, manual call to changes()
            const string sql = "SELECT name, version FROM views WHERE name=?"; // TODO: Convert to ADO params.
            var args = new [] { Name };
            Cursor cursor = null;

            // NOTE: Probably needs to be a run in transaction call.
            try
            {
                cursor = storageEngine.RawQuery(sql, args);

                if (!cursor.MoveToNext())
                {
                    // no such record, so insert
                    var insertValues = new ContentValues();
                    insertValues["name"] = Name;
                    insertValues["version"] = version;
                    storageEngine.Insert("views", null, insertValues);
                    return true;
                }
                
                if (cursor != null)
                {
                    cursor.Close();
                    cursor = null;
                }

                var updateValues = new ContentValues();
                updateValues["version"] = version;
                updateValues["lastSequence"] = 0;

                var whereArgs = new [] { Name, version };
                var rowsAffected = storageEngine.Update("views", updateValues, "name=? AND version!=?", whereArgs);

                return (rowsAffected > 0);
            }
            catch (SQLException e)
            {
                Log.E(Database.Tag, "Error setting map block", e);
                return false;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>'s persistent index. 
        /// The index is regenerated on the next <see cref="Couchbase.Lite.Query"/> execution.
        /// </summary>
        public void DeleteIndex()
        {
            if (Id <= 0) {
                return;
            }

            const string sql = "DROP TABLE IF EXISTS 'maps_#';UPDATE views SET lastSequence=0, total_docs=0 WHERE view_id=#";
            if (!RunStatements(sql)) {
                Log.W(Tag, "Couldn't delete view index `{0}`", Name);
            }
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        public void Delete()
        { 
            Database.DeleteViewNamed(Name);
            _id = 0;
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Query"/> for this view.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Query"/> for this view.</returns>
        public Query CreateQuery() {
            return new Query(Database, this);
        }

    #endregion
    
    }

    /// <summary>
    /// An object that can be used to compile source code into map and reduce delegates.
    /// </summary>
    public interface IViewCompiler {

    #region Instance Members
        //Methods
        /// <summary>
        /// Compiles source code into a <see cref="Couchbase.Lite.MapDelegate"/>.
        /// </summary>
        /// <returns>A compiled <see cref="Couchbase.Lite.MapDelegate"/>.</returns>
        /// <param name="source">The source code to compile into a <see cref="Couchbase.Lite.MapDelegate"/>.</param>
        /// <param name="language">The language of the source.</param>
        MapDelegate CompileMap(String source, String language);

        /// <summary>
        /// Compiles source code into a <see cref="Couchbase.Lite.ReduceDelegate"/>.
        /// </summary>
        /// <returns>A compiled <see cref="Couchbase.Lite.ReduceDelegate"/>.</returns>
        /// <param name="source">The source code to compile into a <see cref="Couchbase.Lite.ReduceDelegate"/>.</param>
        /// <param name="language">The language of the source.</param>
        ReduceDelegate CompileReduce(String source, String language);

    #endregion
    
    }

    #region Global Delegates

    /// <summary>
    /// A delegate that is invoked when a <see cref="Couchbase.Lite.Document"/> 
    /// is being added to a <see cref="Couchbase.Lite.View"/>.
    /// </summary>
    /// <param name="document">The <see cref="Couchbase.Lite.Document"/> being mapped.</param>
    /// <param name="emit">The delegate to use to add key/values to the <see cref="Couchbase.Lite.View"/>.</param>
    public delegate void MapDelegate(IDictionary<String, Object> document, EmitDelegate emit);
        
    /// <summary>
    /// A delegate that can be invoked to add key/values to a <see cref="Couchbase.Lite.View"/> 
    /// during a <see cref="Couchbase.Lite.MapDelegate"/> call.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public delegate void EmitDelegate(Object key, Object value);
        
    /// <summary>
    /// A delegate that can be invoked to summarize the results of a <see cref="Couchbase.Lite.View"/>.
    /// </summary>
    /// <param name="keys">A list of keys to be reduced, or null if this is a rereduce.</param>
    /// <param name="values">A parallel array of values to be reduced, corresponding 1-to-1 with the keys.</param>
    /// <param name="rereduce"><c>true</c> if the input values are the results of previous reductions, otherwise <c>false</c>.</param>
    public delegate Object ReduceDelegate(IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce);

    #endregion
}

