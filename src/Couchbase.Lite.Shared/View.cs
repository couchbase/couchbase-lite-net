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
using System.Data;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections;
using Couchbase.Lite.Portable;

namespace Couchbase.Lite 
{
    /// <summary>
    /// A Couchbase Lite <see cref="Couchbase.Lite.View"/>. 
    /// A <see cref="Couchbase.Lite.View"/> defines a persistent index managed by map/reduce.
    /// </summary>
    public sealed class View : Shared.DatabaseHolder, IView
    {

    #region Constructors

        internal View(Database database, String name):base()
        {
            Database = database;
            Name = name;
            _id = -1;
            // means 'unknown'
            Collation = ViewCollation.Unicode;
        }

    #endregion

    #region Static Members
        /// <summary>
        /// Gets or sets an object that can compile source code into map and reduce delegates.
        /// </summary>
        /// <value>The compiler object.</value>
        public static IViewCompiler Compiler { get; set; }

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
                        cursor = DatabaseInternal.StorageEngine.RawQuery(sql, args);
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
                        Log.E(Couchbase.Lite.Database.Tag, "Error getting view id", e);
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
            Log.V(Couchbase.Lite.Database.Tag, "Re-indexing view {0} ...", Name);
            System.Diagnostics.Debug.Assert((Map != null));

            if (Id <= 0)
            {
                var msg = string.Format("View.Id <= 0");
                throw new CouchbaseLiteException(msg, new Status(StatusCode.NotFound));
            }

            DatabaseInternal.BeginTransaction();

            var result = new Status(StatusCode.InternalServerError);
            Cursor cursor = null;
            try
            {
                var lastSequence = LastSequenceIndexed;
                var dbMaxSequence = Database.LastSequenceNumber;

                if (lastSequence == dbMaxSequence)
                {
                    // nothing to do (eg,  kCBLStatusNotModified)
                    Log.V(Couchbase.Lite.Database.Tag, "lastSequence ({0}) == dbMaxSequence ({1}), nothing to do", lastSequence, dbMaxSequence);
                    result.SetCode(StatusCode.NotModified);
                    return;
                }

                // First remove obsolete emitted results from the 'maps' table:
                var sequence = lastSequence;
                if (lastSequence < 0)
                {
                    var msg = string.Format("lastSequence < 0 ({0})", lastSequence);
                    throw new CouchbaseLiteException(msg, new Status(StatusCode.InternalServerError));
                }
                if (lastSequence == 0)
                {
                    // If the lastSequence has been reset to 0, make sure to remove
                    // any leftover rows:
                    var whereArgs = new string[] { Id.ToString() };
                    DatabaseInternal.StorageEngine.Delete("maps", "view_id=?", whereArgs);
                }
                else
                {
                    // Delete all obsolete map results (ones from since-replaced
                    // revisions):
                    var args = new [] {
                        Id.ToString(),
                        lastSequence.ToString(),
                        lastSequence.ToString()
                    };

                    DatabaseInternal.StorageEngine.ExecSQL(
                        "DELETE FROM maps WHERE view_id=? AND sequence IN ("
                        + "SELECT parent FROM revs WHERE sequence>? " + "AND parent>0 AND parent<=?)", 
                            args);
                }

                var deleted = 0;
                cursor = DatabaseInternal.StorageEngine.RawQuery("SELECT changes()");
                cursor.MoveToNext();
                deleted = cursor.GetInt(0);
                cursor.Close();

                // Find a better way to propagate this back
                // Now scan every revision added since the last time the view was indexed:
                var selectArgs = new[] { lastSequence.ToString() };
                cursor = DatabaseInternal.StorageEngine.RawQuery("SELECT revs.doc_id, sequence, docid, revid, json, no_attachments FROM revs, docs "
                    + "WHERE sequence>? AND current!=0 AND deleted=0 "
                    + "AND revs.doc_id = docs.doc_id "
                    + "ORDER BY revs.doc_id, revid DESC", selectArgs);

                var lastDocID = 0L;
                var keepGoing = cursor.MoveToNext();
                while (keepGoing)
                {
                    long docID = cursor.GetLong(0);
                    if (docID != lastDocID)
                    {
                        // Only look at the first-iterated revision of any document,
                        // because this is the
                        // one with the highest revid, hence the "winning" revision
                        // of a conflict.
                        lastDocID = docID;
                        // Reconstitute the document as a dictionary:
                        sequence = cursor.GetLong(1);
                        string docId = cursor.GetString(2);
                        if (docId.StartsWith("_design/", StringComparison.InvariantCultureIgnoreCase))
                        {
                            // design docs don't get indexed!
                            keepGoing = cursor.MoveToNext();
                            continue;
                        }
                        var revId = cursor.GetString(3);
                        var json = cursor.GetBlob(4);

                        var noAttachments = cursor.GetInt(5) > 0;

                        // Skip rows with the same doc_id -- these are losing conflicts.
                        while ((keepGoing = cursor.MoveToNext()) && cursor.GetLong(0) == docID) { }

                        if (lastSequence > 0)
                        {
                            // Find conflicts with documents from previous indexings.
                            var selectArgs2 = new[] { Convert.ToString(docID), Convert.ToString(lastSequence) };
                            var cursor2 = DatabaseInternal.StorageEngine.RawQuery("SELECT revid, sequence FROM revs "
                                + "WHERE doc_id=? AND sequence<=? AND current!=0 AND deleted=0 " + "ORDER BY revID DESC "
                                + "LIMIT 1", selectArgs2);
                            if (cursor2.MoveToNext())
                            {
                                var oldRevId = cursor2.GetString(0);

                                // This is the revision that used to be the 'winner'.
                                // Remove its emitted rows:
                                var oldSequence = cursor2.GetLong(1);
                                var args = new[] { Sharpen.Extensions.ToString(Id), Convert.ToString(oldSequence) };
                                DatabaseInternal.StorageEngine.ExecSQL("DELETE FROM maps WHERE view_id=? AND sequence=?", args);

                                if (RevisionInternal.CBLCompareRevIDs(oldRevId, revId) > 0)
                                {
                                    // It still 'wins' the conflict, so it's the one that
                                    // should be mapped [again], not the current revision!
                                    revId = oldRevId;
                                    sequence = oldSequence;
                                    var selectArgs3 = new[] { Convert.ToString(sequence) };
                                    json = Misc.ByteArrayResultForQuery(
                                        DatabaseInternal.StorageEngine, 
                                        "SELECT json FROM revs WHERE sequence=?", 
                                        selectArgs3
                                    );
                                }
                            }
                        }
                        // Get the document properties, to pass to the map function:
                        var contentOptions = DocumentContentOptions.None;
                        if (noAttachments)
                        {
                            contentOptions |= DocumentContentOptions.NoAttachments;
                        }

                        var properties = DatabaseInternal.DocumentPropertiesFromJSON(
                            json, docId, revId, false, sequence, DocumentContentOptions.None
                        );
                        if (properties != null)
                        {
                            // Call the user-defined map() to emit new key/value
                            // pairs from this revision:

                            // This is the emit() block, which gets called from within the
                            // user-defined map() block
                            // that's called down below.

                            var enclosingView = this;
                            var thisSequence = sequence;
                            var map = Map;

                            if (map == null)
                                throw new CouchbaseLiteException("Map function is missing.");

                            EmitDelegate emitBlock = (key, value) =>
                            {
                                // TODO: Do we need to do any null checks on key or value?
                                try
                                {
                                    var keyJson = Manager.GetObjectMapper().WriteValueAsString(key);
                                    var valueJson = value == null ? null : Manager.GetObjectMapper().WriteValueAsString(value) ;

                                    var insertValues = new ContentValues();
                                    insertValues.Put("view_id", enclosingView.Id);
                                    insertValues["sequence"] = thisSequence;
                                    insertValues["key"] = keyJson;
                                    insertValues["value"] = valueJson;

                                    enclosingView.DatabaseInternal.StorageEngine.Insert("maps", null, insertValues);

                                    //
                                    // According to the issue #81, it is possible that there will be another
                                    // thread inserting a new revision to the database at the same time that 
                                    // the UpdateIndex operation is running. This event should be guarded by
                                    // the database transaction that the code begun but apparently it was not.
                                    // As a result, it is possible that dbMaxSequence will be out of date at 
                                    // this point and could cause the last indexed sequence to be out of track 
                                    // from the obsolete map entry cleanup operation, which eventually results 
                                    // to duplicated documents in the indexed map.
                                    //
                                    // To prevent the issue above, as a workaroubd, we need to make sure that 
                                    // we have the current max sequence of the indexed documents updated. 
                                    // This diverts from the CBL's Android code which doesn't have the same issue 
                                    // as the Android doesn't allow multiple thread to interact with the database 
                                    // at the same time.
                                    if (thisSequence > dbMaxSequence)
                                    {
                                        dbMaxSequence = thisSequence;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.E(Couchbase.Lite.Database.Tag, "Error emitting", e);
                                }
                            };

                            map(properties, emitBlock);
                        }
                    }
                }

                // Finally, record the last revision sequence number that was 
                // indexed:
                var updateValues = new ContentValues();
                updateValues["lastSequence"] = dbMaxSequence;
                var whereArgs_1 = new string[] { Id.ToString() };
                DatabaseInternal.StorageEngine.Update("views", updateValues, "view_id=?", whereArgs_1);

                // FIXME actually count number added :)
                Log.V(Couchbase.Lite.Database.Tag, "...Finished re-indexing view {0} up to sequence {1} (deleted {2} added ?)", Name, Convert.ToString(dbMaxSequence), deleted);
                result.SetCode(StatusCode.Ok);
            }
            catch (Exception e)
            {
                throw new CouchbaseLiteException(e, new Status(StatusCode.DbError));
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
                if (!result.IsSuccessful)
                {
                    Log.W(Couchbase.Lite.Database.Tag, "Failed to rebuild view {0}:{1}", Name, result.GetCode());
                }
                if (Database != null)
                {
                    DatabaseInternal.EndTransaction(result.IsSuccessful);
                }
            }
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
                int groupLevel = options.GetGroupLevel();
                var group = options.IsGroup() || (groupLevel > 0);
                var reduce = options.IsReduce() || group;
                var reduceBlock = Reduce;

                if (reduce && (reduceBlock == null) && !group)
                {
                    var msg = "Cannot use reduce option in view " + Name + " which has no reduce block defined";
                    Log.W(Couchbase.Lite.Database.Tag, msg);
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
                        var lazyKey = new Lazy<object>(()=>FromJSON(cursor.GetBlob(0)));
                        var lazyValue = new Lazy<object>(()=>FromJSON(cursor.GetBlob(1)));
                        // TODO: ditto
                        var docId = cursor.GetString(2);
                        var sequenceLong = cursor.GetLong(3);
                        var sequence = Convert.ToInt32(sequenceLong);

                        IDictionary<string, object> docContents = null;
                        if (options.IsIncludeDocs())
                        {
                            // http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
                            if (lazyValue.Value is IDictionary<string,object> && ((IDictionary<string,object>)lazyValue.Value).ContainsKey("_id"))
                            {
                                var linkedDocId = (string)((IDictionary<string,object>)lazyValue.Value).Get("_id");
                                var linkedDoc = DatabaseInternal.GetDocumentWithIDAndRev(linkedDocId, null, DocumentContentOptions.None);
                                docContents = linkedDoc.GetProperties();
                            }
                            else
                            {
                                var revId = cursor.GetString(4);
                                docContents = DatabaseInternal.DocumentPropertiesFromJSON(cursor.GetBlob(5), docId, revId, false, sequenceLong, options.GetContentOptions());
                            }
                        }
                        var row = new QueryRow(docId, sequence, lazyKey.Value, lazyValue.Value, docContents);
                        row.Database = Database;
                        rows.AddItem<QueryRow>(row);  // NOTE.ZJG: Change to `yield return row` to convert to a generator.
                        cursor.MoveToNext();
                    }
                }
            }
            catch (SQLException e)
            {
                var errMsg = string.Format("Error querying view: {0}", this);
                Log.E(Couchbase.Lite.Database.Tag, errMsg, e);
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
                cursor = DatabaseInternal.StorageEngine.
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
                cursor = DatabaseInternal.StorageEngine.RawQuery(
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

        // Are key1 and key2 grouped together at this groupLevel?
        public static bool GroupTogether(Lazy<object> key1, Lazy<object> key2, int groupLevel)
        {
            if (groupLevel == 0 || !(key1 != null && key1.Value is IList) || !(key2 != null && key2.Value is IList))
            {
                var key2val = key2 != null 
                    ? key2.Value 
                    : null;
                return key1.Value.Equals(key2val);
            }
            var key1List = (IList)key1.Value;
            var key2List = (IList)key2.Value;

            var end = Math.Min(groupLevel, Math.Min(key1List.Count, key2List.Count));
            for (int i = 0; i < end; ++i)
            {
                if (!key1List[i].Equals(key2List[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // Returns the prefix of the key to use in the result row, at this groupLevel
        public static object GroupKey(object key, int groupLevel)
        {
            if (groupLevel > 0 && (key is IList) && (((IList)key).Count > groupLevel))
            {
                if (key is JArray) {
                    JArray subArray = new JArray();
                    for(var i = 0; i < groupLevel; i++)
                    {
                        subArray.Add(((JArray)key)[i]);
                    }
                    return subArray;
                } else {
                    return ((IList<object>)key).SubList(0, groupLevel);
                }
            }
            else
            {
                return key;
            }
        }

        internal Cursor ResultSetWithOptions(QueryOptions options)
        {
            if (options == null)
            {
                options = new QueryOptions();
            }
            // OPT: It would be faster to use separate tables for raw-or ascii-collated views so that
            // they could be indexed with the right Collation, instead of having to specify it here.
            var collationStr = string.Empty;
            if (Collation == ViewCollation.ASCII)
            {
                collationStr += " COLLATE JSON_ASCII";
            }
            else
            {
                if (Collation == ViewCollation.Raw)
                {
                    collationStr += " COLLATE JSON_RAW";
                }
            }
            var sql = "SELECT key, value, docid, revs.sequence";
            if (options.IsIncludeDocs())
            {
                sql = sql + ", revid, json";
            }
            sql = sql + " FROM maps, revs, docs WHERE maps.view_id=?";
            var argsList = new List<string>();
            argsList.AddItem(Sharpen.Extensions.ToString(Id));
            if (options.GetKeys() != null)
            {
                sql += " AND key in (";
                var item = "?";
                foreach (object key in options.GetKeys())
                {
                    sql += item;
                    item = ", ?";
                    argsList.AddItem(ToJSONString(key));
                }
                sql += ")";
            }
            var startKey = ToJSONString(options.GetStartKey());
            var endKey = ToJSONString(options.GetEndKey());
            var minKey = startKey;
            var maxKey = endKey;
            var minKeyDocId = options.GetStartKeyDocId();
            var maxKeyDocId = options.GetEndKeyDocId();
            var inclusiveMin = true;
            var inclusiveMax = options.IsInclusiveEnd();
            if (options.IsDescending())
            {
                var min = minKey;
                minKey = maxKey;
                maxKey = min;
                inclusiveMin = inclusiveMax;
                inclusiveMax = true;
                minKeyDocId = options.GetEndKeyDocId();
                maxKeyDocId = options.GetStartKeyDocId();
            }
            if (minKey != null)
            {
                sql += inclusiveMin 
                    ? " AND key >= ?" 
                    : " AND key > ?";
                sql += collationStr;
                argsList.AddItem(minKey);
                if (minKeyDocId != null && inclusiveMin)
                {
                    //OPT: This calls the JSON collator a 2nd time unnecessarily.
                    sql += " AND (key > ? {0} OR docid >= ?)".Fmt(collationStr);
                    argsList.AddItem(minKey);
                    argsList.AddItem(minKeyDocId);
                }
            }
            if (maxKey != null)
            {
                if (inclusiveMax)
                {
                    sql += " AND key <= ?";
                }
                else
                {
                    sql += " AND key < ?";
                }
                sql += collationStr;
                argsList.AddItem(maxKey);
                if (maxKeyDocId != null && inclusiveMax)
                {
                    sql += string.Format(" AND (key < ? {0} OR docid <= ?)", collationStr);
                    argsList.AddItem(maxKey);
                    argsList.AddItem(maxKeyDocId);
                }
            }
            sql = sql + " AND revs.sequence = maps.sequence AND docs.doc_id = revs.doc_id ORDER BY key";
            sql += collationStr;
            if (options.IsDescending())
            {
                sql = sql + " DESC";
            }
            sql = sql + " LIMIT ? OFFSET ?";
            argsList.AddItem(options.GetLimit().ToString());
            argsList.AddItem(options.GetSkip().ToString());
            Log.V(Couchbase.Lite.Database.Tag, "Query {0}:{1}", Name, sql);
            var cursor = DatabaseInternal.StorageEngine.RawQuery(sql, argsList.ToArray());
            return cursor;
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
                Log.W(Couchbase.Lite.Database.Tag, "Exception serializing object to json: " + obj, e);
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
                Log.W(Couchbase.Lite.Database.Tag, "Exception parsing json", e);
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
                    Log.E(Couchbase.Lite.Database.Tag, "Warning non-numeric value found in totalValues: " + o, e);
                }
            }
            return total;
        }



    #endregion

    #region Instance Members
        ///// <summary>
        ///// Get the <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.View"/>.
        ///// </summary>
        ///// <value>
        ///// The <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.View"/>.
        ///// </value>
        //public IDatabase Database { get; private set; }

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
        public Boolean IsStale { get { return (LastSequenceIndexed < DatabaseInternal.GetLastSequenceNumber()); } }

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
                try
                {
                    cursor = DatabaseInternal.StorageEngine.RawQuery(sql, args);
                    if (cursor.MoveToNext())
                    {
                        result = cursor.GetLong(0);
                    }
                }
                catch (Exception)
                {
                    Log.E(Couchbase.Lite.Database.Tag, "Error getting last sequence indexed");
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

            if (!DatabaseInternal.Open())
            {
                return false;
            }
            // Update the version column in the database. This is a little weird looking
            // because we want to
            // avoid modifying the database if the version didn't change, and because the
            // row might not exist yet.
            var storageEngine = this.DatabaseInternal.StorageEngine;

            // Older Android doesnt have reliable insert or ignore, will to 2 step
            // FIXME review need for change to execSQL, manual call to changes()
            var sql = "SELECT name, version FROM views WHERE name=?"; // TODO: Convert to ADO params.
            var args = new [] { Name };
            Cursor cursor = null;

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

                var updateValues = new ContentValues();
                updateValues["version"] = version;
                updateValues["lastSequence"] = 0;

                var whereArgs = new [] { Name, version };
                var rowsAffected = storageEngine.Update("views", updateValues, "name=? AND version!=?", whereArgs);

                return (rowsAffected > 0);
            }
            catch (SQLException e)
            {
                Log.E(Couchbase.Lite.Database.Tag, "Error setting map block", e);
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
            if (Id < 0)
                return;

            var success = false;

            try
            {
                DatabaseInternal.BeginTransaction();

                var whereArgs = new string[] { Sharpen.Extensions.ToString(Id) };
                DatabaseInternal.StorageEngine.Delete("maps", "view_id=?", whereArgs);

                var updateValues = new ContentValues();
                updateValues["lastSequence"] = 0;

                DatabaseInternal.StorageEngine.Update("views", updateValues, "view_id=?", whereArgs); // TODO: Convert to ADO params.

                success = true;
            }
            catch (SQLException e)
            {
                Log.E(Couchbase.Lite.Database.Tag, "Error removing index", e);
            }
            finally
            {
                DatabaseInternal.EndTransaction(success);
            }
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        public void Delete()
        { 
            DatabaseInternal.DeleteViewNamed(Name);
            _id = 0;
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Query"/> for this view.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Query"/> for this view.</returns>
        public IQuery CreateQuery() {
            return new Query(Database, this);
        }

    #endregion
    
    }
    
}