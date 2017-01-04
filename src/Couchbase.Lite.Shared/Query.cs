//
// Query.cs
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
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Util;
using System.Diagnostics;

namespace Couchbase.Lite {

    /// <summary>
    /// Used to specify when a <see cref="Couchbase.Lite.View"/> index is updated 
    /// when running a <see cref="Couchbase.Lite.Query"/>.
    /// 
    /// <list type="table">
    /// <listheader>
    /// <term>Name</term>
    /// <description>Description</description>
    /// </listheader>
    /// <item>
    /// <term>Before</term>
    /// <description>
    /// If needed, update the index before running the <see cref="Couchbase.Lite.Query"/> (default). 
    /// This guarantees up-to-date results at the expense of a potential delay in receiving results.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Never</term>
    /// <description>
    /// Never update the index when running a <see cref="Couchbase.Lite.Query"/>. 
    /// This guarantees receiving results the fastest at the expense of potentially out-of-date results.
    /// </description>
    /// </item>
    /// <item>
    /// <term>After</term>
    /// <description>
    /// If needed, update the index asynchronously after running the <see cref="Couchbase.Lite.Query"/>. 
    /// This guarantees receiving results the fastest, at the expense of potentially out-of-date results, 
    /// and that subsequent Queries will return more accurate results.
    /// </description>
    /// </item>    
    /// </list>
    /// </summary>
    [Serializable]
    public enum IndexUpdateMode {
            /// <summary>
            /// If needed, update the index before running the <see cref="Couchbase.Lite.Query"/> (default). 
            /// This guarantees up-to-date results at the expense of a potential delay in receiving results.
            /// </summary>
            Before,
            /// <summary>
            /// Never update the index when running a <see cref="Couchbase.Lite.Query"/>. 
            /// This guarantees receiving results the fastest at the expense of potentially out-of-date results.
            /// </summary>
            Never,
            /// <summary>
            /// If needed, update the index asynchronously after running the <see cref="Couchbase.Lite.Query"/>. 
            /// This guarantees receiving results the fastest, at the expense of potentially out-of-date results, 
            /// and that subsequent Queries will return more accurate results.
            /// </summary>
            After
    }
            
    /// <summary>
    /// Options for specifying the mode that an all documents query should run in
    /// </summary>
    [Serializable]
    public enum AllDocsMode
    {
        /// <summary>
        /// Regular mode
        /// </summary>
        AllDocs,
        /// <summary>
        /// Include deleted documents in the results
        /// </summary>
        IncludeDeleted,
        /// <summary>
        /// Include conflicted revisions in the results
        /// </summary>
        ShowConflicts,
        /// <summary>
        /// Include *only* conflicted revisions in the results
        /// </summary>
        OnlyConflicts,

        /// <summary>
        /// Order by sequence number (i.e. chronologically)
        /// </summary>
        BySequence
    }

    /// <summary>
    /// A Couchbase Lite <see cref="Couchbase.Lite.View"/> <see cref="Couchbase.Lite.Query"/>.
    /// </summary>
    public class Query : IDisposable
    {

        #region Constants

        private const string TAG = "Query";

        #endregion

        #region Variables

        /// <summary>
        /// Event raised when a query has finished running.
        /// </summary>
        public event EventHandler<QueryCompletedEventArgs> Completed
        {
            add { _completed = (EventHandler<QueryCompletedEventArgs>)Delegate.Combine(_completed, value); }
            remove { _completed = (EventHandler<QueryCompletedEventArgs>)Delegate.Remove(_completed, value); }
        }
        private EventHandler<QueryCompletedEventArgs> _completed;

        /// <summary>
        /// The context to fire events on
        /// </summary>
        protected readonly TaskFactory _eventContext;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> that owns 
        /// the <see cref="Couchbase.Lite.Query"/>'s <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        /// <value>
        /// The <see cref="Couchbase.Lite.Database"/> that owns 
        /// the <see cref="Couchbase.Lite.Query"/>'s <see cref="Couchbase.Lite.View"/>.
        /// </value>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets or sets the maximum number of rows to return. 
        /// The default value is int.MaxValue, meaning 'unlimited'.
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets the number of initial rows to skip. Default value is 0.
        /// </summary>
        /// <value>
        /// The number of initial rows to skip. Default value is 0
        /// </value>
        public int Skip { get; set; }

        /// <summary>
        /// Gets or sets whether the rows be returned in descending key order. 
        /// Default value is <c>false</c>.
        /// </summary>
        /// <value><c>true</c> if descending; otherwise, <c>false</c>.</value>
        public bool Descending { get; set; }

        /// <summary>
        /// Gets or sets the key of the first value to return. 
        /// A null value has no effect.
        /// </summary>
        /// <value>The start key.</value>
        public object StartKey { get; set; }

        /// <summary>
        /// Gets or sets the key of the last value to return. 
        /// A null value has no effect.
        /// </summary>
        /// <value>The end key.</value>
        public object EndKey { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Couchbase.Lite.Document"/> id of the first value to return. 
        /// A null value has no effect. This is useful if the view contains 
        /// multiple identical keys, making startKey ambiguous.
        /// </summary>
        /// <value>The Document id of the first value to return.</value>
        public string StartKeyDocId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Couchbase.Lite.Document"/> id of the last value to return. 
        /// A null value has no effect. This is useful if the view contains 
        /// multiple identical keys, making endKey ambiguous.
        /// </summary>
        /// <value>The Document id of the last value to return.</value>
        public string EndKeyDocId { get; set; }

        /// <summary>
        /// If true the StartKey (or StartKeyDocID) comparison uses "&gt;=". Else it uses "&gt;"
        /// </summary>
        public bool InclusiveStart { get; set; }

        /// <summary>
        /// If true the EndKey (or EndKeyDocID) comparison uses "&lt;=". Else it uses "&lt;".
        /// Default value is <c>true</c>.
        /// </summary>
        /// <value><c>true</c> if InclusiveEnd; otherwise, <c>false</c>.</value>
        public bool InclusiveEnd { get; set; }

        /// <summary>
        /// Gets or sets when a <see cref="Couchbase.Lite.View"/> index is updated when running a Query.
        /// </summary>
        /// <value>The index update mode.</value>
        public IndexUpdateMode IndexUpdateMode { get; set; }

        /// <summary>Changes the behavior of a query created by -queryAllDocuments.</summary>
        /// <remarks>
        /// Changes the behavior of a query created by -queryAllDocuments.
        /// - In mode kCBLAllDocs (the default), the query simply returns all non-deleted documents.
        /// - In mode kCBLIncludeDeleted, it also returns deleted documents.
        /// - In mode kCBLShowConflicts, the .conflictingRevisions property of each row will return the
        /// conflicting revisions, if any, of that document.
        /// - In mode kCBLOnlyConflicts, _only_ documents in conflict will be returned.
        /// (This mode is especially useful for use with a CBLLiveQuery, so you can be notified of
        /// conflicts as they happen, i.e. when they're pulled in by a replication.)
        /// </remarks>
        public AllDocsMode AllDocsMode { get; set; }

        /// <summary>
        /// Gets or sets the keys of the values to return. 
        /// A null value has no effect.
        /// </summary>
        /// <value>The keys of the values to return.</value>
        public IEnumerable<Object> Keys { get; set; }

        /// <summary>
        /// Gets or sets whether to only use the map function without using the reduce function.
        /// </summary>
        /// <value><c>true</c> if map only; otherwise, <c>false</c>.</value>
        public bool MapOnly { get; set; }

        /// <summary>
        /// Gets or sets whether results will be grouped in <see cref="Couchbase.Lite.View"/>s that have reduce functions.
        /// </summary>
        /// <value>The group level.</value>
        public int GroupLevel { get; set; }

        /// <summary>
        /// Gets or sets whether to include the entire <see cref="Couchbase.Lite.Document"/> content with the results. 
        /// The <see cref="Couchbase.Lite.Document"/>s can be accessed via the <see cref="Couchbase.Lite.QueryRow"/>'s 
        /// documentProperties property.
        /// </summary>
        /// <value><c>true</c> if prefetch; otherwise, <c>false</c>.</value>
        public bool Prefetch { get; set; }

        /// <summary>
        /// Gets or sets whether Queries created via the <see cref="Couchbase.Lite.Database"/> createAllDocumentsQuery method 
        /// will include deleted <see cref="Couchbase.Lite.Document"/>s. 
        /// This property has no effect in other types of Queries.
        /// </summary>
        /// <value><c>true</c> if include deleted; otherwise, <c>false</c>.</value>
        public bool IncludeDeleted 
        {
            get { return AllDocsMode == AllDocsMode.IncludeDeleted; }
            set 
            {
                AllDocsMode = (value)
                    ? AllDocsMode.IncludeDeleted
                    : AllDocsMode.AllDocs;
            } 
        }

        /// <summary>
        /// Gets or sets an optional predicate that filters the resulting query rows.
        /// If present, it's called on every row returned from the query, and if it returnsfalseNO
        /// the row is skipped.
        /// </summary>
        public Func<QueryRow, bool> PostFilter { get; set; }

        /// <summary>
        /// If nonzero, enables prefix matching of string or array keys.
        /// * A value of 1 treats the endKey itself as a prefix: if it's a string, keys in the index that
        ///   come after the endKey, but begin with the same prefix, will be matched. (For example, if the
        ///   endKey is "foo" then the key "foolish" in the index will be matched, but not "fong".) Or if
        ///   the endKey is an array, any array beginning with those elements will be matched. (For
        ///   example, if the endKey is [1], then [1, "x"] will match, but not [2].) If the key is any
        ///   other type, there is no effect.
        /// * A value of 2 assumes the endKey is an array and treats its final item as a prefix, using the
        ///   rules above. (For example, an endKey of [1, "x"] will match [1, "xtc"] but not [1, "y"].)
        /// * A value of 3 assumes the key is an array of arrays, etc.
        ///   Note that if the .descending property is also set, the search order is reversed and the above
        ///   discussion applies to the startKey, _not_ the endKey.
        /// </summary>
        public int PrefixMatchLevel { get; set; }

        internal View View { get; private set; }

        private bool TemporaryView { get; set; }

        private long LastSequence { get; set; }

        private QueryOptions QueryOptions
        {
            get {
                var queryOptions = new QueryOptions();
                queryOptions.StartKey = StartKey;
                queryOptions.EndKey = EndKey;
                queryOptions.Keys = Keys;
                queryOptions.Skip = Skip;
                queryOptions.Limit = Limit;
                queryOptions.Reduce = !MapOnly;
                queryOptions.ReduceSpecified = true;
                queryOptions.GroupLevel = GroupLevel;
                queryOptions.Descending = Descending;
                queryOptions.IncludeDocs = Prefetch;
                queryOptions.UpdateSeq = true;
                queryOptions.InclusiveStart = InclusiveStart;
                queryOptions.InclusiveEnd = InclusiveEnd;
                queryOptions.IncludeDeletedDocs = IncludeDeleted;
                queryOptions.Stale = IndexUpdateMode;
                queryOptions.AllDocsMode = AllDocsMode;
                queryOptions.StartKeyDocId = StartKeyDocId;
                queryOptions.EndKeyDocId = EndKeyDocId;
                var postFilter = PostFilter;
                if (postFilter != null) {
                    var database = Database;
                    queryOptions.Filter = r =>
                    {
                        r.Database = database;
                        bool result = postFilter(r);
                        r.Database = null;
                        return result;
                    };
                }
                queryOptions.PrefixMatchLevel = PrefixMatchLevel;
                return queryOptions;
            }
        }

        #endregion

        #region Constructors

        // null view for _all_docs query
        internal Query(Database database, View view)
        {
            Debug.Assert(database != null);

            Database = database;
            _eventContext = database.Manager.CapturedContext;
            View = view;
            Limit = Int32.MaxValue;
            MapOnly = (view != null && view.Reduce == null);
            InclusiveEnd = true;
            InclusiveStart = true;
            IndexUpdateMode = IndexUpdateMode.Before;
            AllDocsMode = AllDocsMode.AllDocs;
        }

        /// <summary>Constructor</summary>
        internal Query(Database database, MapDelegate mapFunction)
        : this(database, database.MakeAnonymousView())
        {
            TemporaryView = true;
            View.SetMap(mapFunction, string.Empty);
        }

        #endregion
       

        #region Public Methods

        /// <summary>
        /// Runs the <see cref="Couchbase.Lite.Query"/> and returns an enumerator over the result rows.
        /// </summary>
        /// <exception cref="T:Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while executing the <see cref="Couchbase.Lite.Query"/>.
        /// </exception>
        public virtual QueryEnumerator Run() 
        {
            Log.To.Query.I(TAG, "{0} running...", this);
            Database.Open();

            ValueTypePtr<long> outSequence = 0;
            var viewName = (View != null) ? View.Name : null;
            var queryOptions = QueryOptions;

            IEnumerable<QueryRow> rows = null;
            var success = Database.RunInTransaction(()=>
            {
                rows = Database.QueryViewNamed (viewName, queryOptions, 0, outSequence);
                LastSequence = outSequence;
                return true;
            });

            if (!success) {
                throw Misc.CreateExceptionAndLog(Log.To.Query, StatusCode.DbError, TAG,
                    "Failed to query view named {0}", viewName);
            }

            return new QueryEnumerator(Database, rows, outSequence);
        }

        /// <summary>
        /// Runs <see cref="Couchbase.Lite.Query"/> function asynchronously and 
        /// will notified <see cref="Completed"/> event handlers on completion.
        /// </summary>
        /// <returns>The async task.</returns>
        /// <param name="run">Query's Run function</param>
        /// <param name="token">CancellationToken token.</param>
        public Task<QueryEnumerator> RunAsync(Func<QueryEnumerator> run, CancellationToken token) 
        {
            return Database.Manager.RunAsync(run, token)
                    .ContinueWith(runTask=> // Raise the query's Completed event.
                    {
                        Log.To.Query.V(TAG, "Manager.RunAsync finished, processing results...");
                        var error = runTask.Exception;
                        var completed = _completed;
                        if (completed != null) {
                            var args = new QueryCompletedEventArgs(runTask.Result, error);
                            completed(this, args);
                        }

                        if (error != null) {
                            Log.To.Query.E(TAG, String.Format("{0} exception in RunAsync", this), error);
                            throw error; // Rethrow innner exceptions.
                        }

                        return runTask.Result;
                    }, _eventContext.Scheduler);
        }

        /// <summary>
        /// Runs the Query asynchronously and 
        /// will notified <see cref="Completed"/> event handlers on completion.
        /// </summary>
        /// <returns>The async task.</returns>
        public Task<QueryEnumerator> RunAsync() 
        {
            return RunAsync(Run, CancellationToken.None);
        }

        /// <summary>
        /// Returns a new LiveQuery with identical properties to the the Query.
        /// </summary>
        /// <returns>The live query.</returns>
        public LiveQuery ToLiveQuery() 
        {
            return new LiveQuery(this);
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Disposes the resources of this object
        /// </summary>
        /// <param name="finalizing">If <c>true</c>, this is the finalizer method.  Otherwise,
        /// this is the IDisposable.Dispose() method calling.</param>
        protected virtual void Dispose(bool finalizing)
        {
            if(finalizing) {
                return;
            }

            if(TemporaryView)
                View.Delete();
        }

        #endregion

        #region Overrides
#pragma warning disable 1591

        public override string ToString()
        {
            return string.Format("[Query: Database={0}, Limit={1}, Skip={2}, Descending={3}, StartKey={4},{19}" +
                "EndKey={5}, StartKeyDocId={6}, EndKeyDocId={7}, InclusiveStart={8}, InclusiveEnd={9},{19}" +
                "IndexUpdateMode={10}, AllDocsMode={11}, Keys={12}, MapOnly={13}, GroupLevel={14}, Prefetch={15},{19}" +
                "IncludeDeleted={16}, PostFilter={17}, PrefixMatchLevel={18}]", Database, Limit, Skip, Descending, 
                new SecureLogJsonString(StartKey, LogMessageSensitivity.PotentiallyInsecure), 
                new SecureLogJsonString(EndKey, LogMessageSensitivity.PotentiallyInsecure), 
                new SecureLogString(StartKeyDocId, LogMessageSensitivity.PotentiallyInsecure), 
                new SecureLogString(EndKeyDocId, LogMessageSensitivity.PotentiallyInsecure),
                InclusiveStart, InclusiveEnd, IndexUpdateMode, AllDocsMode, Keys, MapOnly, GroupLevel, Prefetch, 
                IncludeDeleted, PostFilter, PrefixMatchLevel, Environment.NewLine);
        }

        #endregion

        #region IDisposable


        /// <summary>
        /// Releases all resource used by the <see cref="Couchbase.Lite.Query"/> object.
        /// </summary>
        /// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="Couchbase.Lite.Query"/>. The
        /// <see cref="Dispose()"/> method leaves the <see cref="Couchbase.Lite.Query"/> in an unusable state. After
        /// calling <see cref="Dispose()"/>, you must release all references to the <see cref="Couchbase.Lite.Query"/> so
        /// the garbage collector can reclaim the memory that the <see cref="Couchbase.Lite.Query"/> was occupying.</remarks>
        public void Dispose()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }
    
        #pragma warning restore 1591
        #endregion    
    }
}

