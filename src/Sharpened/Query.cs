// 
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
//using System;
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
    /// <summary>Represents a query of a CouchbaseLite 'view', or of a view-like resource like _all_documents.
    ///     </summary>
    /// <remarks>Represents a query of a CouchbaseLite 'view', or of a view-like resource like _all_documents.
    ///     </remarks>
    public class Query
    {
        /// <summary>Determines whether or when the view index is updated.</summary>
        /// <remarks>
        /// Determines whether or when the view index is updated. By default, the index will be updated
        /// if necessary before the query runs -- this guarantees up-to-date results but can cause a delay.
        /// </remarks>
        public enum IndexUpdateMode
        {
            Before,
            Never,
            After
        }

        /// <summary>Changes the behavior of a query created by queryAllDocuments.</summary>
        /// <remarks>Changes the behavior of a query created by queryAllDocuments.</remarks>
        public enum AllDocsMode
        {
            AllDocs,
            IncludeDeleted,
            ShowConflicts,
            OnlyConflicts
        }

        /// <summary>The database that contains this view.</summary>
        /// <remarks>The database that contains this view.</remarks>
        private Database database;

        /// <summary>The view object associated with this query</summary>
        private View view;

        /// <summary>Is this query based on a temporary view?</summary>
        private bool temporaryView;

        /// <summary>The number of initial rows to skip.</summary>
        /// <remarks>
        /// The number of initial rows to skip. Default value is 0.
        /// Should only be used with small values. For efficient paging, use startKey and limit.
        /// </remarks>
        private int skip;

        /// <summary>The maximum number of rows to return.</summary>
        /// <remarks>The maximum number of rows to return. Default value is 0, meaning 'unlimited'.
        ///     </remarks>
        private int limit = int.MaxValue;

        /// <summary>If non-nil, the key value to start at.</summary>
        /// <remarks>If non-nil, the key value to start at.</remarks>
        private object startKey;

        /// <summary>If non-nil, the key value to end after.</summary>
        /// <remarks>If non-nil, the key value to end after.</remarks>
        private object endKey;

        /// <summary>If non-nil, the document ID to start at.</summary>
        /// <remarks>
        /// If non-nil, the document ID to start at.
        /// (Useful if the view contains multiple identical keys, making .startKey ambiguous.)
        /// </remarks>
        private string startKeyDocId;

        /// <summary>If non-nil, the document ID to end at.</summary>
        /// <remarks>
        /// If non-nil, the document ID to end at.
        /// (Useful if the view contains multiple identical keys, making .endKey ambiguous.)
        /// </remarks>
        private string endKeyDocId;

        /// <summary>If set, the view will not be updated for this query, even if the database has changed.
        ///     </summary>
        /// <remarks>
        /// If set, the view will not be updated for this query, even if the database has changed.
        /// This allows faster results at the expense of returning possibly out-of-date data.
        /// </remarks>
        private Query.IndexUpdateMode indexUpdateMode;

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
        private Query.AllDocsMode allDocsMode;

        /// <summary>Should the rows be returned in descending key order? Default value is NO.
        ///     </summary>
        /// <remarks>Should the rows be returned in descending key order? Default value is NO.
        ///     </remarks>
        private bool descending;

        /// <summary>If set to YES, the results will include the entire document contents of the associated rows.
        ///     </summary>
        /// <remarks>
        /// If set to YES, the results will include the entire document contents of the associated rows.
        /// These can be accessed via QueryRow's -documentProperties property.
        /// This slows down the query, but can be a good optimization if you know you'll need the entire
        /// contents of each document. (This property is equivalent to "include_docs" in the CouchDB API.)
        /// </remarks>
        private bool prefetch;

        /// <summary>If set to YES, disables use of the reduce function.</summary>
        /// <remarks>
        /// If set to YES, disables use of the reduce function.
        /// (Equivalent to setting "?reduce=false" in the REST API.)
        /// </remarks>
        private bool mapOnly;

        /// <summary>If set to YES, queries created by -createAllDocumentsQuery will include deleted documents.
        ///     </summary>
        /// <remarks>
        /// If set to YES, queries created by -createAllDocumentsQuery will include deleted documents.
        /// This property has no effect in other types of queries.
        /// </remarks>
        private bool includeDeleted;

        /// <summary>If non-nil, the query will fetch only the rows with the given keys.</summary>
        /// <remarks>If non-nil, the query will fetch only the rows with the given keys.</remarks>
        private IList<object> keys;

        /// <summary>If non-zero, enables grouping of results, in views that have reduce functions.
        ///     </summary>
        /// <remarks>If non-zero, enables grouping of results, in views that have reduce functions.
        ///     </remarks>
        private int groupLevel;

        private long lastSequence;

        /// <summary>Constructor</summary>
        [InterfaceAudience.Private]
        internal Query(Database database, View view)
        {
            // Always update index if needed before querying (default)
            // Don't update the index; results may be out of date
            // Update index _after_ querying (results may still be out of date)
            // (the default), the query simply returns all non-deleted documents.
            // in this mode it also returns deleted documents.
            // the .conflictingRevisions property of each row will return the conflicting revisions, if any, of that document.
            // _only_ documents in conflict will be returned. (This mode is especially useful for use with a CBLLiveQuery, so you can be notified of conflicts as they happen, i.e. when they're pulled in by a replication.)
            // null for _all_docs query
            this.database = database;
            this.view = view;
            limit = int.MaxValue;
            mapOnly = (view != null && view.GetReduce() == null);
            indexUpdateMode = Query.IndexUpdateMode.Before;
            allDocsMode = Query.AllDocsMode.AllDocs;
        }

        /// <summary>Constructor</summary>
        [InterfaceAudience.Private]
        internal Query(Database database, Mapper mapFunction) : this(database, database.MakeAnonymousView
            ())
        {
            temporaryView = true;
            view.SetMap(mapFunction, string.Empty);
        }

        /// <summary>Constructor</summary>
        [InterfaceAudience.Private]
        internal Query(Database database, Couchbase.Lite.Query query) : this(database, query
            .GetView())
        {
            limit = query.limit;
            skip = query.skip;
            startKey = query.startKey;
            endKey = query.endKey;
            descending = query.descending;
            prefetch = query.prefetch;
            keys = query.keys;
            groupLevel = query.groupLevel;
            mapOnly = query.mapOnly;
            startKeyDocId = query.startKeyDocId;
            endKeyDocId = query.endKeyDocId;
            indexUpdateMode = query.indexUpdateMode;
            allDocsMode = query.allDocsMode;
        }

        /// <summary>The database this query is associated with</summary>
        [InterfaceAudience.Public]
        public virtual Database GetDatabase()
        {
            return database;
        }

        [InterfaceAudience.Public]
        public virtual int GetLimit()
        {
            return limit;
        }

        [InterfaceAudience.Public]
        public virtual void SetLimit(int limit)
        {
            this.limit = limit;
        }

        [InterfaceAudience.Public]
        public virtual int GetSkip()
        {
            return skip;
        }

        [InterfaceAudience.Public]
        public virtual void SetSkip(int skip)
        {
            this.skip = skip;
        }

        [InterfaceAudience.Public]
        public virtual bool IsDescending()
        {
            return descending;
        }

        [InterfaceAudience.Public]
        public virtual void SetDescending(bool descending)
        {
            this.descending = descending;
        }

        [InterfaceAudience.Public]
        public virtual object GetStartKey()
        {
            return startKey;
        }

        [InterfaceAudience.Public]
        public virtual void SetStartKey(object startKey)
        {
            this.startKey = startKey;
        }

        [InterfaceAudience.Public]
        public virtual object GetEndKey()
        {
            return endKey;
        }

        [InterfaceAudience.Public]
        public virtual void SetEndKey(object endKey)
        {
            this.endKey = endKey;
        }

        [InterfaceAudience.Public]
        public virtual string GetStartKeyDocId()
        {
            return startKeyDocId;
        }

        [InterfaceAudience.Public]
        public virtual void SetStartKeyDocId(string startKeyDocId)
        {
            this.startKeyDocId = startKeyDocId;
        }

        [InterfaceAudience.Public]
        public virtual string GetEndKeyDocId()
        {
            return endKeyDocId;
        }

        [InterfaceAudience.Public]
        public virtual void SetEndKeyDocId(string endKeyDocId)
        {
            this.endKeyDocId = endKeyDocId;
        }

        [InterfaceAudience.Public]
        public virtual Query.IndexUpdateMode GetIndexUpdateMode()
        {
            return indexUpdateMode;
        }

        [InterfaceAudience.Public]
        public virtual void SetIndexUpdateMode(Query.IndexUpdateMode indexUpdateMode)
        {
            this.indexUpdateMode = indexUpdateMode;
        }

        [InterfaceAudience.Public]
        public virtual Query.AllDocsMode GetAllDocsMode()
        {
            return allDocsMode;
        }

        [InterfaceAudience.Public]
        public virtual void SetAllDocsMode(Query.AllDocsMode allDocsMode)
        {
            this.allDocsMode = allDocsMode;
        }

        [InterfaceAudience.Public]
        public virtual IList<object> GetKeys()
        {
            return keys;
        }

        [InterfaceAudience.Public]
        public virtual void SetKeys(IList<object> keys)
        {
            this.keys = keys;
        }

        [InterfaceAudience.Public]
        public virtual bool IsMapOnly()
        {
            return mapOnly;
        }

        [InterfaceAudience.Public]
        public virtual void SetMapOnly(bool mapOnly)
        {
            this.mapOnly = mapOnly;
        }

        [InterfaceAudience.Public]
        public virtual int GetGroupLevel()
        {
            return groupLevel;
        }

        [InterfaceAudience.Public]
        public virtual void SetGroupLevel(int groupLevel)
        {
            this.groupLevel = groupLevel;
        }

        [InterfaceAudience.Public]
        public virtual bool ShouldPrefetch()
        {
            return prefetch;
        }

        [InterfaceAudience.Public]
        public virtual void SetPrefetch(bool prefetch)
        {
            this.prefetch = prefetch;
        }

        [InterfaceAudience.Public]
        public virtual bool ShouldIncludeDeleted()
        {
            return allDocsMode == Query.AllDocsMode.IncludeDeleted;
        }

        [InterfaceAudience.Public]
        public virtual void SetIncludeDeleted(bool includeDeletedParam)
        {
            allDocsMode = (includeDeletedParam == true) ? Query.AllDocsMode.IncludeDeleted : 
                Query.AllDocsMode.AllDocs;
        }

        /// <summary>Sends the query to the server and returns an enumerator over the result rows (Synchronous).
        ///     </summary>
        /// <remarks>
        /// Sends the query to the server and returns an enumerator over the result rows (Synchronous).
        /// If the query fails, this method returns nil and sets the query's .error property.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [InterfaceAudience.Public]
        public virtual QueryEnumerator Run()
        {
            IList<long> outSequence = new AList<long>();
            string viewName = (view != null) ? view.GetName() : null;
            IList<QueryRow> rows = database.QueryViewNamed(viewName, GetQueryOptions(), outSequence
                );
            lastSequence = outSequence[0];
            return new QueryEnumerator(database, rows, lastSequence);
        }

        /// <summary>Returns a live query with the same parameters.</summary>
        /// <remarks>Returns a live query with the same parameters.</remarks>
        [InterfaceAudience.Public]
        public virtual LiveQuery ToLiveQuery()
        {
            if (view == null)
            {
                throw new InvalidOperationException("Cannot convert a Query to LiveQuery if the view is null"
                    );
            }
            return new LiveQuery(this);
        }

        /// <summary>Starts an asynchronous query.</summary>
        /// <remarks>
        /// Starts an asynchronous query. Returns immediately, then calls the onLiveQueryChanged block when the
        /// query completes, passing it the row enumerator. If the query fails, the block will receive
        /// a non-nil enumerator but its .error property will be set to a value reflecting the error.
        /// The originating Query's .error property will NOT change.
        /// </remarks>
        [InterfaceAudience.Public]
        public virtual Future RunAsync(Query.QueryCompleteListener onComplete)
        {
            return RunAsyncInternal(onComplete);
        }

        /// <summary>A delegate that can be called to signal the completion of a Query.</summary>
        /// <remarks>A delegate that can be called to signal the completion of a Query.</remarks>
        public interface QueryCompleteListener
        {
            void Completed(QueryEnumerator rows, Exception error);
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        internal virtual Future RunAsyncInternal(Query.QueryCompleteListener onComplete)
        {
            return database.GetManager().RunAsync(new _Runnable_383(this, onComplete));
        }

        private sealed class _Runnable_383 : Runnable
        {
            public _Runnable_383(Query _enclosing, Query.QueryCompleteListener onComplete)
            {
                this._enclosing = _enclosing;
                this.onComplete = onComplete;
            }

            public void Run()
            {
                try
                {
                    if (!this._enclosing.GetDatabase().IsOpen())
                    {
                        throw new InvalidOperationException("The database has been closed.");
                    }
                    string viewName = this._enclosing.view.GetName();
                    QueryOptions options = this._enclosing.GetQueryOptions();
                    IList<long> outSequence = new AList<long>();
                    IList<QueryRow> rows = this._enclosing.database.QueryViewNamed(viewName, options, 
                        outSequence);
                    long sequenceNumber = outSequence[0];
                    QueryEnumerator enumerator = new QueryEnumerator(this._enclosing.database, rows, 
                        sequenceNumber);
                    onComplete.Completed(enumerator, null);
                }
                catch (Exception t)
                {
                    Log.E(Log.TagQuery, "Exception caught in runAsyncInternal", t);
                    onComplete.Completed(null, t);
                }
            }

            private readonly Query _enclosing;

            private readonly Query.QueryCompleteListener onComplete;
        }

        /// <exclude></exclude>
        [InterfaceAudience.Private]
        public virtual View GetView()
        {
            return view;
        }

        [InterfaceAudience.Private]
        private QueryOptions GetQueryOptions()
        {
            QueryOptions queryOptions = new QueryOptions();
            queryOptions.SetStartKey(GetStartKey());
            queryOptions.SetEndKey(GetEndKey());
            queryOptions.SetStartKey(GetStartKey());
            queryOptions.SetKeys(GetKeys());
            queryOptions.SetSkip(GetSkip());
            queryOptions.SetLimit(GetLimit());
            queryOptions.SetReduce(!IsMapOnly());
            queryOptions.SetReduceSpecified(true);
            queryOptions.SetGroupLevel(GetGroupLevel());
            queryOptions.SetDescending(IsDescending());
            queryOptions.SetIncludeDocs(ShouldPrefetch());
            queryOptions.SetUpdateSeq(true);
            queryOptions.SetInclusiveEnd(true);
            queryOptions.SetStale(GetIndexUpdateMode());
            queryOptions.SetAllDocsMode(GetAllDocsMode());
            queryOptions.SetStartKeyDocId(GetStartKeyDocId());
            queryOptions.SetEndKeyDocId(GetEndKeyDocId());
            return queryOptions;
        }

        ~Query()
        {
            base.Finalize();
            if (temporaryView)
            {
                view.Delete();
            }
        }
    }
}
