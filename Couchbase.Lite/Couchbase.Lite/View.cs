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

namespace Couchbase.Lite {

    // TODO: Either remove or update the API defs to indicate the enum value changes, and global scope.
    public enum ViewCollation
    {
        Unicode,
        Raw,
        ASCII
    }

    public partial class View {

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

    #region Static Members
        /// <summary>
        /// Gets or sets an object that can compile source code into map and reduce delegates.
        /// </summary>
        /// <value>The compiler.</value>
        public static IViewCompiler Compiler { get; set; }

    #endregion
    
    #region Constants

        const Int32 ReduceBatchSize = 100;

    #endregion

    #region Non-public Members

        private Int32 _id;

        private ViewCollation Collation { get; set; }

        internal Int32 Id {
            get {
                if (_id < 0)
                {
                    string sql = "SELECT view_id FROM views WHERE name=@";
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
            Log.V(Database.Tag, "Re-indexing view " + Name + " ...");
            System.Diagnostics.Debug.Assert((Map != null));

            if (Id < 0)
            {
                var msg = string.Format("View.Id < 0");
                throw new CouchbaseLiteException(msg, new Status(StatusCode.NotFound));
            }

            Database.BeginTransaction();

            var result = new Status(StatusCode.InternalServerError);
            Cursor cursor = null;
            try
            {
                var lastSequence = LastSequenceIndexed;
                var dbMaxSequence = Database.LastSequenceNumber;

                if (lastSequence == dbMaxSequence)
                {
                    // nothing to do (eg,  kCBLStatusNotModified)
                    var msg = String.Format("lastSequence ({0}) == dbMaxSequence ({1}), nothing to do", lastSequence, dbMaxSequence);
                    Log.D(Database.Tag, msg);
                    result.SetCode(StatusCode.Ok);
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
                    var whereArgs = new string[] { Sharpen.Extensions.ToString(Id) };
                    Database.StorageEngine.Delete("maps", "view_id=@", whereArgs);
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

                    Database.StorageEngine.ExecSQL(
                        "DELETE FROM maps WHERE view_id=@ AND sequence IN ("
                        + "SELECT parent FROM revs WHERE sequence>@ " + "AND parent>0 AND parent<=@)", 
                            args);
                }
                var deleted = 0;
                cursor = Database.StorageEngine.RawQuery("SELECT changes()", null); // TODO: Convert to ADO params.
                cursor.MoveToNext();
                deleted = cursor.GetInt(0);
                cursor.Close();

                // find a better way to propagate this back
                // Now scan every revision added since the last time the view was
                // indexed:
                var selectArgs = new[] { Convert.ToString(lastSequence) };
                cursor = Database.StorageEngine.RawQuery("SELECT revs.doc_id, sequence, docid, revid, json FROM revs, docs "
                    + "WHERE sequence>@ AND current!=0 AND deleted=0 " + "AND revs.doc_id = docs.doc_id "
                    + "ORDER BY revs.doc_id, revid DESC", CommandBehavior.SequentialAccess, selectArgs);
                cursor.MoveToNext();

                var lastDocID = 0L;
                while (!cursor.IsAfterLast())
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
                            cursor.MoveToNext();
                            continue;
                        }
                        var revId = cursor.GetString(3);
                        var json = cursor.GetBlob(4);
                        var properties = Database.DocumentPropertiesFromJSON(
                            json, docId, revId, false, sequence, EnumSet.NoneOf<TDContentOptions>()
                        );
                        if (properties != null)
                        {
                            // Call the user-defined map() to emit new key/value
                            // pairs from this revision:
                            Log.V(Database.Tag, "  call map for sequence=" + System.Convert.ToString(sequence
                            ));
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
                                    Log.V(Database.Tag, String.Format("    emit({0}, {1})", keyJson, valueJson));

                                    var insertValues = new ContentValues();
                                    insertValues.Put("view_id", enclosingView.Id);
                                    insertValues["sequence"] = thisSequence;
                                    insertValues["key"] = keyJson;
                                    insertValues["value"] = valueJson;

                                    enclosingView.Database.StorageEngine.Insert("maps", null, insertValues);
                                }
                                catch (Exception e)
                                {
                                    Log.E(Database.Tag, "Error emitting", e);
                                }
                            };

                            map(properties, emitBlock);
                        }
                    }
                    cursor.MoveToNext();
                }
                // Finally, record the last revision sequence number that was
                // indexed:
                ContentValues updateValues = new ContentValues();
                updateValues["lastSequence"] = dbMaxSequence;
                var whereArgs_1 = new string[] { Sharpen.Extensions.ToString(Id) };
                Database.StorageEngine.Update("views", updateValues, "view_id=@", whereArgs_1);
                // FIXME actually count number added :)
                        Log.V(Database.Tag, "...Finished re-indexing view " + Name + " up to sequence " +
                    System.Convert.ToString(dbMaxSequence) + " (deleted " + deleted + " added " + "?" + ")");
                        result.SetCode(StatusCode.Ok);
            }
            catch (SQLException e)
            {
                throw new CouchbaseLiteException(e, new Status(StatusCode.DbError));
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
                if (!result.IsSuccessful())
                {
                    Log.W(Database.Tag, "Failed to rebuild view " + Name + ": " + result.GetCode());
                }
                if (Database != null)
                {
                    Database.EndTransaction(result.IsSuccessful());
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
            IList<QueryRow> rows = new AList<QueryRow>();
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
                        var keyData = FromJSON(cursor.GetBlob(0));
                        // TODO: delay parsing this for increased efficiency
                        var value = FromJSON(cursor.GetBlob(1));
                        // TODO: ditto
                        var docId = cursor.GetString(2);
                        var sequenceLong = cursor.GetLong(3);
                        var sequence = Convert.ToInt32(sequenceLong);

                        IDictionary<string, object> docContents = null;
                        if (options.IsIncludeDocs())
                        {
                            // http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
                            if (value is IDictionary<string,object> && ((IDictionary<string,object>)value).ContainsKey("_id"))
                            {
                                string linkedDocId = (string)((IDictionary<string,object>)value).Get("_id");
                                RevisionInternal linkedDoc = Database.GetDocumentWithIDAndRev(linkedDocId, null, 
                                    EnumSet.NoneOf<TDContentOptions>());
                                docContents = linkedDoc.GetProperties();
                            }
                            else
                            {
                                var revId = cursor.GetString(4);
                                docContents = Database.DocumentPropertiesFromJSON(cursor.GetBlob(5), docId, revId, false, sequenceLong, options.GetContentOptions());
                            }
                        }
                        var row = new QueryRow(docId, sequence, keyData, value, docContents);
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

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IList<QueryRow> ReducedQuery(Cursor cursor, Boolean group, Int32 groupLevel)
        {
            IList<object> keysToReduce = null;
            IList<object> valuesToReduce = null;
            object lastKey = null;

            var reduce = Reduce;
            // FIXME: If reduce is null, then so are keysToReduce and ValuesToReduce, which can throw an NRE below.
            if (reduce != null)
            {
                keysToReduce = new AList<Object>(ReduceBatchSize);
                valuesToReduce = new AList<Object>(ReduceBatchSize);
            }

            var rows = new AList<QueryRow>();
            cursor.MoveToNext();

            while (!cursor.IsAfterLast())
            {
                var keyData = FromJSON(cursor.GetBlob(0));
                var value = FromJSON(cursor.GetBlob(1));
                System.Diagnostics.Debug.Assert((keyData != null));
                if (group && !GroupTogether(keyData, lastKey, groupLevel))
                {
                    if (lastKey != null)
                    {
                        // This pair starts a new group, so reduce & record the last one:
                        var reduced = (reduce != null) ? reduce(keysToReduce, valuesToReduce, false) : null;

                        var key = GroupKey(lastKey, groupLevel);
                        var row = new QueryRow(null, 0, key, reduced, null);
                        row.Database = Database;
                        rows.AddItem(row); // NOTE.ZJG: Change to `yield return row` to convert to a generator.

                        keysToReduce.Clear();
                        valuesToReduce.Clear();
                    }
                    lastKey = keyData;
                }
                keysToReduce.AddItem(keyData);
                valuesToReduce.AddItem(value);
                cursor.MoveToNext();
            }
            // NOTE.ZJG: Need to handle grouping differently if switching this to a generator.
            if (keysToReduce.Count > 0)
            {
                // Finish the last group (or the entire list, if no grouping):
                var key = group ? GroupKey(lastKey, groupLevel) : null;
                var reduced = (reduce != null) ? reduce(keysToReduce, valuesToReduce, false) : null;
                var row = new QueryRow(null, 0, key, reduced, null);
                row.Database = Database;
                rows.AddItem(row);
            }
            return rows;
        }

        // Are key1 and key2 grouped together at this groupLevel?
        public static bool GroupTogether(object key1, object key2, int groupLevel)
        {
            if (groupLevel == 0 || !(key1 is IList<object>) || !(key2 is IList<object>))
            {
                return key1.Equals(key2);
            }
            var key1List = (IList<object>)key1;
            var key2List = (IList<object>)key2;
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
            if (groupLevel > 0 && (key is IList<object>) && (((IList<object>)key).Count > groupLevel))
            {
                return ((IList<object>)key).SubList(0, groupLevel);
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
            sql = sql + " FROM maps, revs, docs WHERE maps.view_id=@";
            var argsList = new AList<string>();
            argsList.AddItem(Sharpen.Extensions.ToString(Id));
            if (options.GetKeys() != null)
            {
                sql += " AND key in (";
                var item = "@";
                foreach (object key in options.GetKeys())
                {
                    sql += item;
                    item = ", @";
                    argsList.AddItem(ToJSONString(key));
                }
                sql += ")";
            }
            var minKey = options.GetStartKey();
            var maxKey = options.GetEndKey();
            var inclusiveMin = true;
            var inclusiveMax = options.IsInclusiveEnd();
            if (options.IsDescending())
            {
                minKey = maxKey;
                maxKey = options.GetStartKey();
                inclusiveMin = inclusiveMax;
                inclusiveMax = true;
            }
            if (minKey != null)
            {
                System.Diagnostics.Debug.Assert((minKey is string));
                sql += inclusiveMin ? " AND key >= @" : " AND key > @";
                sql += collationStr;
                argsList.AddItem(ToJSONString(minKey));
            }
            if (maxKey != null)
            {
                System.Diagnostics.Debug.Assert((maxKey is string));
                if (inclusiveMax)
                {
                    sql += " AND key <= @";
                }
                else
                {
                    sql += " AND key < @";
                }
                sql += collationStr;
                argsList.AddItem(ToJSONString(maxKey));
            }
            sql = sql + " AND revs.sequence = maps.sequence AND docs.doc_id = revs.doc_id ORDER BY key";
            sql += collationStr;
            if (options.IsDescending())
            {
                sql = sql + " DESC";
            }
            sql = sql + " LIMIT @ OFFSET @";
            argsList.AddItem(options.GetLimit().ToString());
            argsList.AddItem(options.GetSkip().ToString());
            Log.V(Database.Tag, "Query " + Name + ": " + sql);
            var cursor = Database.StorageEngine.RawQuery(sql, CommandBehavior.SequentialAccess, argsList.ToArray());
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

    #endregion

    #region Instance Members
        /// <summary>Get the <see cref="Couchbase.Lite.Database"/> that owns this <see cref="Couchbase.Lite.View"/>.</summary>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s name.
        /// </summary>
        /// <value>The name.</value>
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
        public ReduceDelegate Reduce { get; private set; }

        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.View"/>'s indices are currently out of date.
        /// </summary>
        /// <value><c>true</c> if this instance is stale; otherwise, <c>false</c>.</value>
        public Boolean IsStale { get { return (LastSequenceIndexed < Database.GetLastSequenceNumber()); } }

        /// <summary>
        /// Gets the last sequence number indexed so far.
        /// </summary>
        /// <value>The last sequence indexed.</value>
        public Int64 LastSequenceIndexed { 
            get {
                var sql = "SELECT lastSequence FROM views WHERE name=@";
                var args = new[] { Name };
                Cursor cursor = null;
                var result = -1L;
                try
                {
                    Log.D(Database.TagSql, Sharpen.Thread.CurrentThread().GetName() + " start running query: " + sql);
                    cursor = Database.StorageEngine.RawQuery(sql, args);
                    Log.D(Database.TagSql, Sharpen.Thread.CurrentThread().GetName() + " finish running query: " + sql);

                    if (cursor.MoveToNext())
                    {
                        result = cursor.GetLong(0);
                    }
                }
                catch (Exception)
                {
                    Log.E(Database.Tag, "Error getting last sequence indexed");
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

        /// <summary>Defines the <see cref="Couchbase.Lite.View"/>'s <see cref="Couchbase.Lite.MapDelegate"/> and sets 
        /// its <see cref="Couchbase.Lite.ReduceDelegate"/> to null.</summary>
        /// <returns>
        /// True if the <see cref="Couchbase.Lite.MapDelegate"/> was set, otherwise false.  If the values provided are 
        /// identical to the values that are already set, then the values will not be updated and false will be returned.  
        /// In addition, if true is returned, the index was deleted and will be rebuilt on the next 
        /// <see cref="Couchbase.Lite.Query"/> execution.
        /// </returns>
        public Boolean SetMap(MapDelegate mapDelegate, String version) {
            return SetMapReduce(mapDelegate, null, version);
        }

        /// <summary>Defines a view's functions.</summary>
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
        public Boolean SetMapReduce(MapDelegate map, ReduceDelegate reduce, String version) { 
            System.Diagnostics.Debug.Assert((map != null));
            System.Diagnostics.Debug.Assert(!String.IsNullOrWhiteSpace(version));

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
            var sql = "SELECT name, version FROM views WHERE name=@"; // TODO: Convert to ADO params.
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
                var rowsAffected = storageEngine.Update("views", updateValues, "name=@ AND version!=@", whereArgs);

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
        /// Deletes the <see cref="Couchbase.Lite.View"/>'s persistent index.  The index is regenerated on the next <see cref="Couchbase.Lite.Query"/> execution.
        /// </summary>
        public void DeleteIndex()
        {
            if (Id < 0)
                return;

            var success = false;

            try
            {
                Database.BeginTransaction();

                var whereArgs = new string[] { Sharpen.Extensions.ToString(Id) };
                Database.StorageEngine.Delete("maps", "view_id=@", whereArgs);

                var updateValues = new ContentValues();
                updateValues["lastSequence"] = 0;

                Database.StorageEngine.Update("views", updateValues, "view_id=@", whereArgs); // TODO: Convert to ADO params.

                success = true;
            }
            catch (SQLException e)
            {
                Log.E(Database.Tag, "Error removing index", e);
            }
            finally
            {
                Database.EndTransaction(success);
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
        /// <returns>The query.</returns>
        public Query CreateQuery() {
            return new Query(Database, this);
        }

    #endregion
    
    #region Delegates
        public delegate Object ReduceDelegate(IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce);

    #endregion
    
    }

    public partial interface IViewCompiler {

    #region Instance Members
        //Methods
        MapDelegate CompileMap(String source, String language);

        ReduceDelegate CompileReduce(String source, String language);

    #endregion
    
    }

    #region Global Delegates

    public delegate void MapDelegate(IDictionary<String, Object> document, EmitDelegate emit);
        
    public delegate void EmitDelegate(Object key, Object value);
        
    public delegate Object ReduceDelegate(IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce);

    #endregion
}

