//
// Query.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using System.Threading.Tasks;
using System.Threading;

namespace Couchbase.Lite {

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
            IndexUpdateMode = query.IndexUpdateMode;
            AllDocsMode = query.AllDocsMode;
        }


    #endregion
       
    #region Non-public Members

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
                queryOptions.SetInclusiveEnd(true);
                queryOptions.SetIncludeDeletedDocs(IncludeDeleted);
                queryOptions.SetStale(IndexUpdateMode);
                queryOptions.SetAllDocsMode(AllDocsMode);
                return queryOptions;
            }
        }


    #endregion

    #region Instance Members
        //Properties
        public Database Database { get; private set; }

        public Int32 Limit { get; set; }

        public Int32 Skip { get; set; }

        public Boolean Descending { get; set; }

        public Object StartKey { get; set; }

        public Object EndKey { get; set; }

        public String StartKeyDocId { get; set; }

        public String EndKeyDocId { get; set; }

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

        public IEnumerable<Object> Keys { get; set; }

        public Boolean MapOnly { get; set; }

        public Int32 GroupLevel { get; set; }

        public Boolean Prefetch { get; set; }

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
        public virtual QueryEnumerator Run() 
        {
            if (!Database.Open())
            {
                throw new CouchbaseLiteException("The database has been closed.");
            }

            var outSequence = new AList<long>();
            var viewName = (View != null) ? View.Name : null;
            var queryOptions = QueryOptions;

            var rows = Database.QueryViewNamed (viewName, queryOptions, outSequence);

            LastSequence = outSequence[0]; // potential concurrency issue?

            return new QueryEnumerator(Database, rows, outSequence[0]);
        }

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

                            if (runTask.Status != TaskStatus.RanToCompletion)
                                throw runTask.Exception; // Rethrow innner exceptions.

                            return runTask.Result; // Give additional continuation functions access to the results task.
                    });
        }

        public Task<QueryEnumerator> RunAsync() 
        {
            return RunAsync(Run, CancellationToken.None);
        }

//        internal Task<QueryEnumerator> RunAsync(Func<QueryEnumerator> action) 
//        {
//            return Database.Manager.RunAsync(action, CancellationToken.None)
//                .ContinueWith(runTask=> // Raise the query's Completed event.
//                    {
//                        var error = runTask.Exception;
//
//                        var completed = Completed;
//                        if (completed != null)
//                        {
//                            var args = new QueryCompletedEventArgs(runTask.Result, error);
//                            completed(this, args);
//                        }
//
//                        if (runTask.Status != TaskStatus.RanToCompletion)
//                            throw runTask.Exception; // Rethrow innner exceptions.
//
//                        return runTask.Result; // Give additional continuation functions access to the results task.
//                    });
//        }

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

    #region Delegates

    public delegate void QueryCompleteDelegate(QueryEnumerator rows, Exception error);

    #endregion
        
}

