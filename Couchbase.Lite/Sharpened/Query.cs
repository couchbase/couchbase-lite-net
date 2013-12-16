/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>Represents a query of a CouchbaseLite 'view', or of a view-like resource like _all_documents.
	/// 	</summary>
	/// <remarks>Represents a query of a CouchbaseLite 'view', or of a view-like resource like _all_documents.
	/// 	</remarks>
	public class Query
	{
		public enum IndexUpdateMode
		{
			Never,
			Before,
			After
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
		/// 	</remarks>
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
		/// 	</summary>
		/// <remarks>
		/// If set, the view will not be updated for this query, even if the database has changed.
		/// This allows faster results at the expense of returning possibly out-of-date data.
		/// </remarks>
		private Query.IndexUpdateMode indexUpdateMode;

		/// <summary>Should the rows be returned in descending key order? Default value is NO.
		/// 	</summary>
		/// <remarks>Should the rows be returned in descending key order? Default value is NO.
		/// 	</remarks>
		private bool descending;

		/// <summary>If set to YES, the results will include the entire document contents of the associated rows.
		/// 	</summary>
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
		/// 	</summary>
		/// <remarks>
		/// If set to YES, queries created by -createAllDocumentsQuery will include deleted documents.
		/// This property has no effect in other types of queries.
		/// </remarks>
		private bool includeDeleted;

		/// <summary>If non-nil, the query will fetch only the rows with the given keys.</summary>
		/// <remarks>If non-nil, the query will fetch only the rows with the given keys.</remarks>
		private IList<object> keys;

		/// <summary>If non-zero, enables grouping of results, in views that have reduce functions.
		/// 	</summary>
		/// <remarks>If non-zero, enables grouping of results, in views that have reduce functions.
		/// 	</remarks>
		private int groupLevel;

		/// <summary>
		/// If a query is running and the user calls stop() on this query, the future
		/// will be used in order to cancel the query in progress.
		/// </summary>
		/// <remarks>
		/// If a query is running and the user calls stop() on this query, the future
		/// will be used in order to cancel the query in progress.
		/// </remarks>
		protected internal Future updateQueryFuture;

		private long lastSequence;

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		internal Query(Database database, View view)
		{
			// null for _all_docs query
			this.database = database;
			this.view = view;
			limit = int.MaxValue;
			mapOnly = (view != null && view.GetReduce() == null);
			indexUpdateMode = Query.IndexUpdateMode.Never;
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
		internal Query(Database database, Couchbase.Lite.Query query) : this(database
			, query.GetView())
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
			return includeDeleted;
		}

		[InterfaceAudience.Public]
		public virtual void SetIncludeDeleted(bool includeDeleted)
		{
			this.includeDeleted = includeDeleted;
		}

		/// <summary>Sends the query to the server and returns an enumerator over the result rows (Synchronous).
		/// 	</summary>
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

		[InterfaceAudience.Private]
		internal virtual Future RunAsyncInternal(Query.QueryCompleteListener onComplete)
		{
			return database.GetManager().RunAsync(new _Runnable_334(this, onComplete));
		}

		private sealed class _Runnable_334 : Runnable
		{
			public _Runnable_334(Query _enclosing, Query.QueryCompleteListener onComplete)
			{
				this._enclosing = _enclosing;
				this.onComplete = onComplete;
			}

			public void Run()
			{
				try
				{
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
					onComplete.Completed(null, t);
				}
			}

			private readonly Query _enclosing;

			private readonly Query.QueryCompleteListener onComplete;
		}

		public virtual View GetView()
		{
			return view;
		}

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
			queryOptions.SetIncludeDeletedDocs(ShouldIncludeDeleted());
			queryOptions.SetStale(GetIndexUpdateMode());
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

		public interface QueryCompleteListener
		{
			void Completed(QueryEnumerator rows, Exception error);
		}
	}
}
