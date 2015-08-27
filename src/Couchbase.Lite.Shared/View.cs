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
using Couchbase.Lite.Store;
using Sharpen;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using Couchbase.Lite.Internal;
using System.Diagnostics;
using Couchbase.Lite.Storage;
using System.Collections;
using System.Text;
using Couchbase.Lite.Views;

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

        #region Constants

        internal const string TAG = "View";
        private const int REDUCE_BATCH_SIZE = 100;

        #endregion

        #region Variables

        private TypedEventHandler<View, EventArgs> _changed;
        internal event TypedEventHandler<View, EventArgs> Changed
        {
            add { _changed = (TypedEventHandler<View, EventArgs>)Delegate.Combine(_changed, value); }
            remove { _changed = (TypedEventHandler<View, EventArgs>)Delegate.Remove(_changed, value); }
        }

        private ConcurrentQueue<UpdateJob> _updateQueue = new ConcurrentQueue<UpdateJob>();
        private string _emitSql;
        private ViewCollation _collation;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets an object that can compile source code into map and reduce delegates.
        /// </summary>
        public static IViewCompiler Compiler { get; set; }

        /// <summary>
        /// Get the <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s name.
        /// </summary>
        public string Name { get; private set; }


        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.View"/>'s indices are currently out of date.
        /// </summary>
        /// <value><c>true</c> if this instance is stale; otherwise, <c>false</c>.</value>
        public bool IsStale { get { return (LastSequenceIndexed < Database.LastSequenceNumber); } }

        /// <summary>
        /// Gets the last sequence number indexed so far.
        /// </summary>
        public long LastSequenceIndexed { 
            get {
                return Database.QueryOrDefault<long>(c => c.GetLong(0), true, 0, "SELECT lastsequence FROM views WHERE name=?", Name);
            }
        }

        /// <summary>
        /// Gets the last sequence that there was a change in the view
        /// </summary>
        public long LastSequenceChangedAt
        {
            get {
                return LastSequenceIndexed;
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
                    totalRows = db.QueryOrDefault<int>(c => c.GetInt(0), false, 0, QueryString("SELECT COUNT(*) FROM 'maps_#'"));
                    var args = new ContentValues();
                    args["total_docs"] = totalRows;
                    db.StorageEngine.Update("views", args, "view_id=?", ViewID.ToString());
                }

                Debug.Assert(totalRows >= 0);
                return totalRows;
            }
        }

        internal ViewCollation Collation { get; set; }

        private int ViewID
        {
            get {
                if (_viewId < 0) {
                    _viewId = Database.QueryOrDefault<int>(c => c.GetInt(0), false, 0, "SELECT view_id FROM views WHERE name=?", Name);
                }

                return _viewId;
            }
        }
        private int _viewId;

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

        internal static View MakeView(Database database, string name, bool create)
        {
            var view = new View();
            view.Database = database;
            view.Name = name;
            view._viewId = -1;
            if (!create && view.ViewID <= 0) {
                return null;
            }

            // means 'unknown'
            view.Collation = ViewCollation.Unicode;
            return view;
        }

        #endregion

        #region Public Methods

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
        public Boolean SetMap(MapDelegate mapDelegate, string version) {
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
        public bool SetMapReduce(MapDelegate map, ReduceDelegate reduce, string version) { 
            System.Diagnostics.Debug.Assert(map != null);
            System.Diagnostics.Debug.Assert(version != null); // String.Empty is valid.

            var changed = version != MapVersion;
            var shared = Database.Shared;
            shared.SetValue("map", Name, Database.Name, map);
            shared.SetValue("mapVersion", Name, Database.Name, version);
            shared.SetValue("reduce", Name, Database.Name, reduce);

            if (changed) {
                SetVersion(version);
                if (_changed != null) {
                    _changed(this, null);
                }
            }

            return changed;
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>'s persistent index. 
        /// The index is regenerated on the next <see cref="Couchbase.Lite.Query"/> execution.
        /// </summary>
        public void DeleteIndex()
        {
            if (ViewID <= 0) {
                return;
            }

            const string sql = "DROP TABLE IF EXISTS 'maps_#';UPDATE views SET lastSequence=0, total_docs=0 WHERE view_id=#";
            if (!RunStatements(sql)) {
                Log.W(TAG, "Couldn't delete view index `{0}`", Name);
            }
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        public void Delete()
        { 
            DeleteView();
            Database.ForgetView(Name);
            Close();
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Query"/> for this view.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Query"/> for this view.</returns>
        public Query CreateQuery() {
            return new Query(Database, this);
        }

        #endregion

        #region Internal Methods

        internal Status UpdateIndexes(IEnumerable<View> inputViews)
        {
            Log.D(TAG, "Checking indexes of ({0}) for {1}", ViewNames(inputViews), Name);
            var db = Database;

            Status status = null;
            var success = db.RunInTransaction(() =>
            {
                // If the view the update is for doesn't need any update, don't do anything:
                long dbMaxSequence = db.LastSequenceNumber;
                long forViewLastSequence = LastSequenceIndexed;
                if (forViewLastSequence >= dbMaxSequence) {
                    status = new Status(StatusCode.NotModified);
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
                bool allDocTypes = false;
                IDictionary<int, int> viewTotalRows = new Dictionary<int, int>();
                List<View> views = new List<View>(inputViews.Count());
                List<MapDelegate> mapBlocks = new List<MapDelegate>();
                foreach (var view in inputViews) {
                    var mapBlock = Map;
                    if (mapBlock == null) {
                        Debug.Assert(view != this, String.Format("Cannot index view {0}: no map block registered", view.Name));
                        Log.V(TAG, "    {0} has no map block; skipping it", view.Name);
                        continue;
                    }

                    views.Add(view);
                    mapBlocks.Add(mapBlock);

                    int viewId = view.ViewID;
                    Debug.Assert(viewId > 0, String.Format("View '{0}' not found in database", view.Name));

                    int totalRows = view.TotalRows;
                    viewTotalRows[viewId] = totalRows;

                    long last = view == this ? forViewLastSequence : view.LastSequenceIndexed;
                    viewLastSequence[i++] = last;
                    if (last < 0) {
                        status = new Status(StatusCode.DbError);
                        return false;
                    }

                    if (last < dbMaxSequence) {
                        if (last == 0) {
                            CreateIndex();
                        }

                        minLastSequence = Math.Min(minLastSequence, last);
                        Log.V(TAG, "    {0} last indexed at #{1}", view.Name, last);

                        string docType = DocumentType;
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
                            status = new Status(StatusCode.DbError);
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
                    status = new Status(StatusCode.NotModified);
                    return true;
                }

                Log.D(TAG, "Updating indexes of ({0}) from #{1} to #{2} ...",
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
                    sql.AppendFormat("AND doc_type IN ({0}) ", Database.JoinQuoted(docTypes));
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
                                        viewTotalRows[view.ViewID] -= changes;
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
                            Log.W(TAG, "Failed to parse JSON of doc {0} rev {1}", docId, revId);
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

                                Log.V(TAG, "    #{0}: map \"{1}\" for view {2}...",
                                    sequence, docId, e.Current.Name);
                                try {
                                    mapBlocks[viewIndex](currentDoc, emit);
                                } catch (Exception x) {
                                    Log.E(TAG, String.Format("Exception in map() block for view {0}", currentView.Name), x);
                                    emitStatus.Code = StatusCode.Exception;
                                }

                                if (emitStatus.IsError) {
                                    c.Dispose();
                                    status = emitStatus;
                                    return false;
                                }
                            }
                        }

                        currentView = null;
                    }
                } catch (Exception) {
                    status = new Status(StatusCode.DbError);
                    return false;
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
                    } catch (Exception) {
                        status = new Status(StatusCode.DbError);
                        return false;
                    }
                }

                Log.D(TAG, "...Finished re-indexing ({0}) to #{1} (deleted {2}, added {3})",
                    ViewNames(views), dbMaxSequence, deletedCount, insertedCount);
                status = new Status(StatusCode.Ok);
                return true;
            });

            if(status.Code >= StatusCode.BadRequest) {
                Log.W(TAG, "CouchbaseLite: Failed to rebuild views ({0}): {1}", ViewNames(inputViews), status);
            }

            return status;
        }

        internal UpdateJob CreateUpdateJob(IEnumerable<View> viewsToUpdate)
        {
            return new UpdateJob(UpdateIndexes, viewsToUpdate, from store in viewsToUpdate
                select store.LastSequenceIndexed);
        }

        internal void DeleteView()
        {
            var db = Database;
            db.RunInTransaction(() =>
            {
                DeleteIndex();
                try {
                    db.StorageEngine.Delete("views", "name=?", Name);
                } catch(Exception) {
                    return false;
                }

                return true;
            });

            _viewId = 0;
        }

        internal bool SetVersion(string version)
        {
            // Update the version column in the db. This is a little weird looking because we want to
            // avoid modifying the db if the version didn't change, and because the row might not exist yet.
            var db = Database;
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


        internal void Close()
        {
            Database = null;
        }

        internal Status UpdateIndex()
        {
            //TODO: View grouping
            var viewsToUpdate = new List<View> { this };

            UpdateJob proposedJob = CreateUpdateJob(viewsToUpdate);
            UpdateJob nextJob = null;
            if (_updateQueue.TryPeek(out nextJob)) {
                if (!nextJob.LastSequences.SequenceEqual(proposedJob.LastSequences)) {
                    QueueUpdate(proposedJob);
                    nextJob = proposedJob;
                } 
            } else {
                QueueUpdate(proposedJob);
                nextJob = proposedJob;
            }

            nextJob.Wait();
            return nextJob.Result;
        }

        /// <summary>Queries the view.</summary>
        /// <remarks>Queries the view. Does NOT first update the index.</remarks>
        /// <param name="options">The options to use.</param>
        /// <returns>An array of QueryRow objects.</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IEnumerable<QueryRow> QueryWithOptions(QueryOptions options)
        {
            if (options == null) {
                options = new QueryOptions();
            }

            IEnumerable<QueryRow> iterator = null;
            if (false) {
                //TODO: Full text
            } else if (GroupOrReduce(options)) {
                iterator = ReducedQuery(options);
            } else {
                iterator = RegularQuery(options);
            }

            if (iterator != null) {
                Log.D(TAG, "Query {0}: Returning iterator", Name);
            } else {
                Log.D(TAG, "Query {0}: Failed", Name);
            }

            return iterator;
        }

        internal static bool RowValueIsEntireDoc(object valueData)
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

        internal static T ParseRowValue<T>(IEnumerable<byte> valueData)
        {
            return Manager.GetObjectMapper().ReadValue<T>(valueData);
        }

        internal IDictionary<string, object> DocumentProperties(string docId, long sequenceNumber)
        {
            return Database.GetDocument(docId, sequenceNumber).GetProperties();
        }

        internal IEnumerable<QueryRow> RegularQuery(QueryOptions options)
        {
            var db = Database;
            var filter = options.Filter;
            int limit = int.MaxValue;
            int skip = 0;
            if (filter != null) {
                // Custom post-filter means skip/limit apply to the filtered rows, not to the
                // underlying query, so handle them specially:
                limit = options.Limit;
                skip = options.Skip;
                options.Limit = QueryOptions.DEFAULT_LIMIT;
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
                        sequence = docRevision == null ? 0 : docRevision.GetSequence();
                    } else {
                        docRevision = db.GetRevision(docId, cursor.GetString(4), false, sequence, cursor.GetBlob(5));
                    }
                }

                Log.V(TAG, "Query {0}: Found row with key={1}, value={2}, id={3}",
                    Name, keyData.Value, valueData.Value, docId);

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
                var rowsByKey = new Dictionary<object, List<QueryRow>>();
                foreach (var row in rows) {
                    var dictRows = rowsByKey.Get(row.Key);
                    if (dictRows == null) {
                        dictRows = rowsByKey[row.Key] = new List<QueryRow>();
                    }

                    dictRows.Add(row);
                }

                // Now concatenate them in the order the keys are given in options:
                var sortedRows = new List<QueryRow>();
                foreach (var key in options.Keys) {
                    var dictRows = rowsByKey.Get(key);
                    if (dictRows != null) {
                        sortedRows.AddRange(dictRows);
                    }
                }

                rows = sortedRows;
            }

            return rows;
        }

        internal IEnumerable<QueryRow> ReducedQuery(QueryOptions options)
        {
            var db = Database;
            var groupLevel = options.GroupLevel;
            bool group = options.Group || groupLevel > 0;
            var reduce = Reduce;
            if (options.ReduceSpecified) {
                if (options.Reduce && reduce == null) {
                    Log.W(TAG, "Cannot use reduce option in view {0} which has no reduce block defined", Name);
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

                Log.V(TAG, "    Query {0}: Will reduce row with key={1}, value={2}", Name, keyData.Value, valueData.Value);

                object valueOrData = FromJSON(valueData.Value);
                if(valuesToReduce != null && RowValueIsEntireDoc(valueData)) {
                    // map fn emitted 'doc' as value, which was stored as a "*" placeholder; expand now:
                    Status status = new Status();
                    var rev = db.GetDocument(docID, c.GetLong(1), status);
                    if(rev == null) {
                        Log.W(TAG, "Couldn't load doc for row value: status {0}", status.Code);
                    }

                    valueOrData = rev.GetProperties();
                }

                keysToReduce.Add(keyData.Value);
                valuesToReduce.Add(valueOrData);
                return new Status(StatusCode.Ok);
            });

            if((keysToReduce != null && keysToReduce.Count > 0) || lastKeyData != null) {
                // Finish the last group (or the entire list, if no grouping):
                var key = group ? GroupKey(lastKeyData.Value, groupLevel) : null;
                var reduced = CallReduce(reduce, keysToReduce, valuesToReduce);
                Log.V(TAG, "    Query {0}: Will reduce row with key={1}, value={2}", Name, Manager.GetObjectMapper().WriteValueAsString(key),
                    Manager.GetObjectMapper().WriteValueAsString(reduced));

                var row = new QueryRow(null, 0, key, reduced, null, this);
                if (options.Filter == null || options.Filter(row)) {
                    rows.Add(row);
                }
            }

            return rows;
        }

        /// <summary>Indexing</summary>
        internal static string ToJSONString(object obj)
        {
            if (obj == null)
                return null;

            string result = null;
            try
            {
                result = Manager.GetObjectMapper().WriteValueAsString(obj);
            }
            catch (Exception e)
            {
                Log.W(Database.TAG, "Exception serializing object to json: " + obj, e);
            }
            return result;
        }

        internal static object FromJSON(IEnumerable<byte> json)
        {
            if (json == null)
            {
                return null;
            }
            object result = null;
            try
            {
                result = Manager.GetObjectMapper().ReadValue<object>(json);
            }
            catch (Exception e)
            {
                Log.W(Database.TAG, "Exception parsing json", e);
            }
            return result;
        }

        internal IEnumerable<IDictionary<string, object>> Dump()
        {
            if (ViewID <= 0) {
                return null;
            }

            List<IDictionary<string, object>> retVal = new List<IDictionary<string, object>>();
            Database.TryQuery(c =>
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

        internal Status CompileFromDesignDoc()
        {
            MapDelegate map;
            if (Database.Shared.TryGetValue("map", Name, Database.Name, out map)) {
                return new Status(StatusCode.Ok);
            }

            string language = null;
            var viewProps = Database.GetDesignDocFunction(Name, "views", out language).AsDictionary<string, object>();
            if (viewProps == null) {
                return new Status(StatusCode.NotFound);
            }

            Log.D(TAG, "{0}: Attempting to compile {1} from design doc", Name, language);
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
                Log.W(TAG, "View {0} could not compile {1} map fn: {2}", Name, language, mapSource);
                return new Status(StatusCode.CallbackError);
            }

            string reduceSource = viewProps.Get("reduce") as string;
            ReduceDelegate reduceDelegate = null;
            if (reduceSource != null) {
                reduceDelegate = Compiler.CompileReduce(reduceSource, language);
                if (reduceDelegate == null) {
                    Log.W(TAG, "View {0} could not compile {1} reduce fn: {2}", Name, language, mapSource);
                    return new Status(StatusCode.CallbackError);
                }
            }

            string version = Misc.HexSHA1Digest(Manager.GetObjectMapper().WriteValueAsBytes(viewProps));
            SetMapReduce(mapDelegate, reduceDelegate, version);
            DocumentType = viewProps.GetCast<string>("documentType");

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

        #region Private Methods

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
                Log.E(TAG, "Exception in reduce block", e);
            }

            return null;
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

            Log.D(TAG, "Query {0}: {1}\n\tArguments: {2}", Name, sql, Manager.GetObjectMapper().WriteValueAsString(args));

            var dbStorage = Database;
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

        private static string ViewNames(IEnumerable<View> inputViews)
        {
            var names = inputViews.Select(x => x.Name);
            return String.Join(", ", names.ToStringArray());
        }

        private bool RunStatements(string sqlStatements)
        {
            var db = Database;
            return db.RunInTransaction(() =>
            {
                if(Database.RunStatements(QueryString(sqlStatements))) {
                    return true;
                }

                return false;
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

            if (!RunStatements(sql)) {
                Log.W(TAG, "Couldn't create view index `{0}`", Name);
            }
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
                Log.V(TAG, "    emit({0}, {1}", keyJSON, valueJSON);
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

            if (!RunStatements(sql)) {
                Log.W(TAG, "Couldn't create view SQL index `{0}`", Name);
            }
        }

        private UpdateJob QueueUpdate(UpdateJob job)
        {
            job.Finished += (sender, e) => {
                UpdateJob nextJob;
                _updateQueue.TryDequeue(out nextJob);
                if(_updateQueue.TryPeek(out nextJob)) {
                    nextJob.Run();
                }
            };

            _updateQueue.Enqueue(job);
            if (_updateQueue.Count == 1) {
                job.Run();
            }

            return job;
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

        #endregion

        #region IViewStoreDelegate
        #pragma warning disable 1591

        public MapDelegate Map
        {
            get
            {
                MapDelegate map;
                if (!Database.Shared.TryGetValue("map", Name, Database.Name, out map)) {
                    map = null;
                    if (CompileFromDesignDoc().IsSuccessful) {
                        map = Map;
                    }
                }

                return map;
            }
        }

        public ReduceDelegate Reduce
        {
            get {
                ReduceDelegate retVal;
                if (!Database.Shared.TryGetValue("reduce", Name, Database.Name, out retVal)) {
                    return null;
                }

                return retVal;
            }
        }

        public string MapVersion
        {
            get {
                string retVal;
                if (!Database.Shared.TryGetValue("mapVersion", Name, Database.Name, out retVal)) {
                    return null;
                }

                return retVal;
            }
        }

        public string DocumentType { 
            get {
                string retVal;
                if(!Database.Shared.TryGetValue("docType", Name, Database.Name, out retVal)) {
                    return null;
                }

                return retVal;
            }
            set { 
                Database.Shared.SetValue("docType", Name, Database.Name, value);
            }
        }

        #pragma warning restore 1591
        #endregion

    }

    /// <summary>
    /// An object that can be used to compile source code into map and reduce delegates.
    /// </summary>
    public interface IViewCompiler 
    {

        //Methods
        /// <summary>
        /// Compiles source code into a <see cref="Couchbase.Lite.MapDelegate"/>.
        /// </summary>
        /// <returns>A compiled <see cref="Couchbase.Lite.MapDelegate"/>.</returns>
        /// <param name="source">The source code to compile into a <see cref="Couchbase.Lite.MapDelegate"/>.</param>
        /// <param name="language">The language of the source.</param>
        MapDelegate CompileMap(string source, string language);

        /// <summary>
        /// Compiles source code into a <see cref="Couchbase.Lite.ReduceDelegate"/>.
        /// </summary>
        /// <returns>A compiled <see cref="Couchbase.Lite.ReduceDelegate"/>.</returns>
        /// <param name="source">The source code to compile into a <see cref="Couchbase.Lite.ReduceDelegate"/>.</param>
        /// <param name="language">The language of the source.</param>
        ReduceDelegate CompileReduce(string source, string language);

    }

    #region Global Delegates

    /// <summary>
    /// A delegate that is invoked when a <see cref="Couchbase.Lite.Document"/> 
    /// is being added to a <see cref="Couchbase.Lite.View"/>.
    /// </summary>
    /// <param name="document">The <see cref="Couchbase.Lite.Document"/> being mapped.</param>
    /// <param name="emit">The delegate to use to add key/values to the <see cref="Couchbase.Lite.View"/>.</param>
    public delegate void MapDelegate(IDictionary<string, object> document, EmitDelegate emit);

    /// <summary>
    /// A delegate that can be invoked to add key/values to a <see cref="Couchbase.Lite.View"/> 
    /// during a <see cref="Couchbase.Lite.MapDelegate"/> call.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public delegate void EmitDelegate(object key, object value);

    /// <summary>
    /// A delegate that can be invoked to summarize the results of a <see cref="Couchbase.Lite.View"/>.
    /// </summary>
    /// <param name="keys">A list of keys to be reduced, or null if this is a rereduce.</param>
    /// <param name="values">A parallel array of values to be reduced, corresponding 1-to-1 with the keys.</param>
    /// <param name="rereduce"><c>true</c> if the input values are the results of previous reductions, otherwise <c>false</c>.</param>
    public delegate object ReduceDelegate(IEnumerable<object> keys, IEnumerable<object> values, Boolean rereduce);

    #endregion
}
