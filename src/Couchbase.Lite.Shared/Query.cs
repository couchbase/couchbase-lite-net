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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using System.Threading.Tasks;
using System.Threading;
using Couchbase.Lite.Util;

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
    public enum IndexUpdateMode {
            Before,
            Never,
            After
    }
                            
    public enum AllDocsMode
    {
        AllDocs,
        IncludeDeleted,
        ShowConflicts,
        OnlyConflicts
    }

    /// <summary>
    /// A Couchbase Lite <see cref="Couchbase.Lite.View"/> <see cref="Couchbase.Lite.Query"/>.
    /// </summary>
    public partial class Query : IDisposable
    {

    #region Constructors
        internal Query(Database database, View view)
        {
            // null for _all_docs query
            Database = database;
            View = view;
            Limit = Int32.MaxValue;
            MapOnly = (view != null && view.Reduce == null);
            InclusiveEnd = true;
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

        /// <summary>Constructor</summary>
        internal Query(Database database, Query query) 
        : this(database, query.View)
        {
            Limit = query.Limit;
            Skip = query.Skip;
            StartKey = query.StartKey;
            EndKey = query.EndKey;
            Descending = query.Descending;
            Prefetch = query.Prefetch;
            Keys = query.Keys;
            GroupLevel = query.GroupLevel;
            MapOnly = query.MapOnly;
            StartKeyDocId = query.StartKeyDocId;
            EndKeyDocId = query.EndKeyDocId;
            InclusiveEnd = query.InclusiveEnd;
            IndexUpdateMode = query.IndexUpdateMode;
            AllDocsMode = query.AllDocsMode;
        }


    #endregion
       
    #region Non-public Members

        const string Tag = "Query";

        internal View View { get; private set; }

        private  bool TemporaryView { get; set; }

        private Int64 LastSequence { get; set; }

        private QueryOptions QueryOptions
        {
            get {
                var queryOptions = new QueryOptions();
                queryOptions.SetStartKey(StartKey);
                queryOptions.SetEndKey(EndKey);
                queryOptions.SetKeys(Keys);
                queryOptions.SetSkip(Skip);
                queryOptions.SetLimit(Limit);
                queryOptions.SetReduce(!MapOnly);
                queryOptions.SetReduceSpecified(true);
                queryOptions.SetGroupLevel(GroupLevel);
                queryOptions.SetDescending(Descending);
                queryOptions.SetIncludeDocs(Prefetch);
                queryOptions.SetUpdateSeq(true);
                queryOptions.SetInclusiveEnd(InclusiveEnd);
                queryOptions.SetIncludeDeletedDocs(IncludeDeleted);
                queryOptions.SetStale(IndexUpdateMode);
                queryOptions.SetAllDocsMode(AllDocsMode);
                queryOptions.SetStartKeyDocId(StartKeyDocId);
                queryOptions.SetEndKeyDocId(EndKeyDocId);
                return queryOptions;
            }
        }


    #endregion

    #region Instance Members
        //Properties
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
        /// The default value is 0, meaning 'unlimited'.
        /// </summary>
        /// <value>
        /// The maximum number of rows to return. 
        /// The default value is 0, meaning 'unlimited'
        /// </value>
        public Int32 Limit { get; set; }

        /// <summary>
        /// Gets or sets the number of initial rows to skip. Default value is 0.
        /// </summary>
        /// <value>
        /// The number of initial rows to skip. Default value is 0
        /// </value>
        public Int32 Skip { get; set; }

        /// <summary>
        /// Gets or sets whether the rows be returned in descending key order. 
        /// Default value is <c>false</c>.
        /// </summary>
        /// <value><c>true</c> if descending; otherwise, <c>false</c>.</value>
        public Boolean Descending { get; set; }

        /// <summary>
        /// Gets or sets the key of the first value to return. 
        /// A null value has no effect.
        /// </summary>
        /// <value>The start key.</value>
        public Object StartKey { get; set; }

        /// <summary>
        /// Gets or sets the key of the last value to return. 
        /// A null value has no effect.
        /// </summary>
        /// <value>The end key.</value>
        public Object EndKey { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Couchbase.Lite.Document"/> id of the first value to return. 
        /// A null value has no effect. This is useful if the view contains 
        /// multiple identical keys, making startKey ambiguous.
        /// </summary>
        /// <value>The Document id of the first value to return.</value>
        public String StartKeyDocId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Couchbase.Lite.Document"/> id of the last value to return. 
        /// A null value has no effect. This is useful if the view contains 
        /// multiple identical keys, making endKey ambiguous.
        /// </summary>
        /// <value>The Document id of the last value to return.</value>
        public String EndKeyDocId { get; set; }

        /// <summary>
        /// If true the EndKey (or EndKeyDocID) comparison uses "<=". Else it uses "<".
        /// Default value is <c>true</c>.
        /// </summary>
        /// <value><c>true</c> if InclusiveEnd; otherwise, <c>false</c>.</value>
        public Boolean InclusiveEnd { get; set; }

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
        public Boolean MapOnly { get; set; }

        /// <summary>
        /// Gets or sets whether results will be grouped in <see cref="Couchbase.Lite.View"/>s that have reduce functions.
        /// </summary>
        /// <value>The group level.</value>
        public Int32 GroupLevel { get; set; }

        /// <summary>
        /// Gets or sets whether to include the entire <see cref="Couchbase.Lite.Document"/> content with the results. 
        /// The <see cref="Couchbase.Lite.Document"/>s can be accessed via the <see cref="Couchbase.Lite.QueryRow"/>'s 
        /// documentProperties property.
        /// </summary>
        /// <value><c>true</c> if prefetch; otherwise, <c>false</c>.</value>
        public Boolean Prefetch { get; set; }

        /// <summary>
        /// Gets or sets whether Queries created via the <see cref="Couchbase.Lite.Database"/> createAllDocumentsQuery method 
        /// will include deleted <see cref="Couchbase.Lite.Document"/>s. 
        /// This property has no effect in other types of Queries.
        /// </summary>
        /// <value><c>true</c> if include deleted; otherwise, <c>false</c>.</value>
        public Boolean IncludeDeleted 
        {
            get { return AllDocsMode == AllDocsMode.IncludeDeleted; }
            set 
            {
                   AllDocsMode = (value)
                                 ? AllDocsMode.IncludeDeleted
                                 : AllDocsMode.AllDocs;
            } 
        }

        public event EventHandler<QueryCompletedEventArgs> Completed;

        //Methods
        /// <summary>
        /// Runs the <see cref="Couchbase.Lite.Query"/> and returns an enumerator over the result rows.
        /// </summary>
        /// <exception cref="T:Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while executing the <see cref="Couchbase.Lite.Query"/>.
        /// </exception>
        public virtual QueryEnumerator Run() 
        {
            if (!Database.Open())
            {
                throw new CouchbaseLiteException("The database has been closed.");
            }

            var outSequence = new List<long>();
            var viewName = (View != null) ? View.Name : null;
            var queryOptions = QueryOptions;

            IEnumerable<QueryRow> rows = null;
            var success = Database.RunInTransaction(()=>
            {
                rows = Database.QueryViewNamed (viewName, queryOptions, outSequence);
                return true;
            });

            if (!success)
            {
                throw new CouchbaseLiteException("Failed to query view named " + viewName, StatusCode.DbError);
            }

            LastSequence = outSequence[0]; // potential concurrency issue?

            return new QueryEnumerator(Database, rows, outSequence[0]);
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
                        var error = runTask.Exception;

                        var completed = Completed;
                        if (completed != null)
                        {
                            var args = new QueryCompletedEventArgs(runTask.Result, error);
                            completed(this, args);
                        }

                        if (error != null) {
                            Log.E(Tag, "Exception caught in runAsyncInternal", error);
                            throw error; // Rethrow innner exceptions.
                        }
                        return runTask.Result; // Give additional continuation functions access to the results task.
                    }, Database.Manager.CapturedContext.Scheduler);
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
            if (View == null)
            {
                throw new CouchbaseLiteException("Cannot convert a Query to LiveQuery if the view is null");
            }
            return new LiveQuery(this);
        }

        public void Dispose()
        {
            if (TemporaryView)
                View.Delete();
        }
    #endregion    
    }
}

