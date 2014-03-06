/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>A Query subclass that automatically refreshes the result rows every time the database changes.
	/// 	</summary>
	/// <remarks>
	/// A Query subclass that automatically refreshes the result rows every time the database changes.
	/// All you need to do is use add a listener to observe changes.
	/// </remarks>
	public sealed class LiveQuery : Query, Database.ChangeListener
	{
		private bool observing;

		private QueryEnumerator rows;

		private IList<LiveQuery.ChangeListener> observers = new AList<LiveQuery.ChangeListener
			>();

		private Exception lastError;

		private AtomicBoolean runningState;

		/// <summary>
		/// If a query is running and the user calls stop() on this query, the future
		/// will be used in order to cancel the query in progress.
		/// </summary>
		/// <remarks>
		/// If a query is running and the user calls stop() on this query, the future
		/// will be used in order to cancel the query in progress.
		/// </remarks>
		protected internal Future queryFuture;

		/// <summary>
		/// If the update() method is called while a query is in progress, once it is
		/// finished it will be scheduled to re-run update().
		/// </summary>
		/// <remarks>
		/// If the update() method is called while a query is in progress, once it is
		/// finished it will be scheduled to re-run update().  This tracks the future
		/// related to that scheduled task.
		/// </remarks>
		protected internal Future rerunUpdateFuture;

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		internal LiveQuery(Query query) : base(query.GetDatabase(), query.GetView())
		{
			// true == running, false == stopped
			runningState = new AtomicBoolean(false);
			SetLimit(query.GetLimit());
			SetSkip(query.GetSkip());
			SetStartKey(query.GetStartKey());
			SetEndKey(query.GetEndKey());
			SetDescending(query.IsDescending());
			SetPrefetch(query.ShouldPrefetch());
			SetKeys(query.GetKeys());
			SetGroupLevel(query.GetGroupLevel());
			SetMapOnly(query.IsMapOnly());
			SetStartKeyDocId(query.GetStartKeyDocId());
			SetEndKeyDocId(query.GetEndKeyDocId());
			SetIndexUpdateMode(query.GetIndexUpdateMode());
		}

		/// <summary>Sends the query to the server and returns an enumerator over the result rows (Synchronous).
		/// 	</summary>
		/// <remarks>
		/// Sends the query to the server and returns an enumerator over the result rows (Synchronous).
		/// Note: In a LiveQuery you should consider adding a ChangeListener and calling start() instead.
		/// </remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public override QueryEnumerator Run()
		{
			while (true)
			{
				try
				{
					WaitForRows();
					break;
				}
				catch (Exception e)
				{
					if (e is CancellationException)
					{
						continue;
					}
					else
					{
						lastError = e;
						throw new CouchbaseLiteException(e, Status.InternalServerError);
					}
				}
			}
			if (rows == null)
			{
				return null;
			}
			else
			{
				// Have to return a copy because the enumeration has to start at item #0 every time
				return new QueryEnumerator(rows);
			}
		}

		/// <summary>Returns the last error, if any, that occured while executing the Query, otherwise null.
		/// 	</summary>
		/// <remarks>Returns the last error, if any, that occured while executing the Query, otherwise null.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public Exception GetLastError()
		{
			return lastError;
		}

		/// <summary>Starts observing database changes.</summary>
		/// <remarks>
		/// Starts observing database changes. The .rows property will now update automatically. (You
		/// usually don't need to call this yourself, since calling getRows() will start it for you
		/// </remarks>
		[InterfaceAudience.Public]
		public void Start()
		{
			if (runningState.Get() == true)
			{
				Log.D(Database.Tag, this + ": start() called, but runningState is already true.  Ignoring."
					);
				return;
			}
			else
			{
				Log.D(Database.Tag, this + ": start() called");
				runningState.Set(true);
			}
			if (!observing)
			{
				observing = true;
				GetDatabase().AddChangeListener(this);
				Log.D(Database.Tag, this + ": start() is calling update()");
				Update();
			}
		}

		/// <summary>Stops observing database changes.</summary>
		/// <remarks>Stops observing database changes. Calling start() or rows() will restart it.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public void Stop()
		{
			if (runningState.Get() == false)
			{
				Log.D(Database.Tag, this + ": stop() called, but runningState is already false.  Ignoring."
					);
				return;
			}
			else
			{
				Log.D(Database.Tag, this + ": stop() called");
				runningState.Set(false);
			}
			if (observing)
			{
				observing = false;
				GetDatabase().RemoveChangeListener(this);
			}
			// slight diversion from iOS version -- cancel the queryFuture
			// regardless of the willUpdate value, since there can be an update in flight
			// with willUpdate set to false.  was needed to make testLiveQueryStop() unit test pass.
			if (queryFuture != null)
			{
				bool cancelled = queryFuture.Cancel(true);
				Log.D(Database.Tag, this + ": cancelled queryFuture " + queryFuture + ", returned: "
					 + cancelled);
			}
			else
			{
				Log.D(Database.Tag, this + ": not cancelling queryFuture, since it is null");
			}
			if (rerunUpdateFuture != null)
			{
				bool cancelled = rerunUpdateFuture.Cancel(true);
				Log.D(Database.Tag, this + ": cancelled rerunUpdateFuture " + rerunUpdateFuture +
					 ", returned: " + cancelled);
			}
			else
			{
				Log.D(Database.Tag, this + ": not cancelling rerunUpdateFuture, since it is null"
					);
			}
		}

		/// <summary>Blocks until the intial async query finishes.</summary>
		/// <remarks>Blocks until the intial async query finishes. After this call either .rows or .error will be non-nil.
		/// 	</remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public void WaitForRows()
		{
			Start();
			while (true)
			{
				try
				{
					queryFuture.Get();
					break;
				}
				catch (Exception e)
				{
					if (e is CancellationException)
					{
						continue;
					}
					else
					{
						lastError = e;
						throw new CouchbaseLiteException(e, Status.InternalServerError);
					}
				}
			}
		}

		/// <summary>Gets the results of the Query.</summary>
		/// <remarks>Gets the results of the Query. The value will be null until the initial Query completes.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public QueryEnumerator GetRows()
		{
			Start();
			if (rows == null)
			{
				return null;
			}
			else
			{
				// Have to return a copy because the enumeration has to start at item #0 every time
				return new QueryEnumerator(rows);
			}
		}

		/// <summary>
		/// Add a change listener to be notified when the live query result
		/// set changes.
		/// </summary>
		/// <remarks>
		/// Add a change listener to be notified when the live query result
		/// set changes.
		/// </remarks>
		[InterfaceAudience.Public]
		public void AddChangeListener(LiveQuery.ChangeListener changeListener)
		{
			observers.AddItem(changeListener);
		}

		/// <summary>Remove previously added change listener</summary>
		[InterfaceAudience.Public]
		public void RemoveChangeListener(LiveQuery.ChangeListener changeListener)
		{
			observers.Remove(changeListener);
		}

		/// <summary>The type of event raised when a LiveQuery result set changes.</summary>
		/// <remarks>The type of event raised when a LiveQuery result set changes.</remarks>
		public class ChangeEvent
		{
			private LiveQuery source;

			private Exception error;

			private QueryEnumerator queryEnumerator;

			internal ChangeEvent()
			{
			}

			internal ChangeEvent(LiveQuery source, QueryEnumerator queryEnumerator)
			{
				this.source = source;
				this.queryEnumerator = queryEnumerator;
			}

			internal ChangeEvent(Exception error)
			{
				this.error = error;
			}

			public virtual LiveQuery GetSource()
			{
				return source;
			}

			public virtual Exception GetError()
			{
				return error;
			}

			public virtual QueryEnumerator GetRows()
			{
				return queryEnumerator;
			}
		}

		/// <summary>A delegate that can be used to listen for LiveQuery result set changes.</summary>
		/// <remarks>A delegate that can be used to listen for LiveQuery result set changes.</remarks>
		public interface ChangeListener
		{
			void Changed(LiveQuery.ChangeEvent @event);
		}

		[InterfaceAudience.Private]
		internal void Update()
		{
			Log.D(Database.Tag, this + ": update() called.");
			if (GetView() == null)
			{
				throw new InvalidOperationException("Cannot start LiveQuery when view is null");
			}
			if (runningState.Get() == false)
			{
				Log.D(Database.Tag, this + ": update() called, but running state == false.  Ignoring."
					);
				return;
			}
			if (queryFuture != null && !queryFuture.IsCancelled() && !queryFuture.IsDone())
			{
				// There is a already a query in flight, so leave it alone except to schedule something
				// to run update() again once it finishes.
				Log.D(Database.Tag, this + ": already a query in flight, scheduling call to update() once it's done"
					);
				if (rerunUpdateFuture != null && !rerunUpdateFuture.IsCancelled() && !rerunUpdateFuture
					.IsDone())
				{
					bool cancelResult = rerunUpdateFuture.Cancel(true);
					Log.D(Database.Tag, this + ": cancelled " + rerunUpdateFuture + " result: " + cancelResult
						);
				}
				rerunUpdateFuture = RerunUpdateAfterQueryFinishes();
				Log.D(Database.Tag, this + ": created new rerunUpdateFuture: " + rerunUpdateFuture
					);
				return;
			}
			// No query in flight, so kick one off
			queryFuture = RunAsyncInternal(new _QueryCompleteListener_285(this));
			Log.D(Database.Tag, this + ": update() created queryFuture: " + queryFuture);
		}

		private sealed class _QueryCompleteListener_285 : Query.QueryCompleteListener
		{
			public _QueryCompleteListener_285(LiveQuery _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Completed(QueryEnumerator rowsParam, Exception error)
			{
				if (error != null)
				{
					foreach (LiveQuery.ChangeListener observer in this._enclosing.observers)
					{
						observer.Changed(new LiveQuery.ChangeEvent(error));
					}
					this._enclosing.lastError = error;
				}
				else
				{
					if (rowsParam != null && !rowsParam.Equals(this._enclosing.rows))
					{
						this._enclosing.SetRows(rowsParam);
						foreach (LiveQuery.ChangeListener observer in this._enclosing.observers)
						{
							Log.D(Database.Tag, this._enclosing + ": update() calling back observer with rows"
								);
							observer.Changed(new LiveQuery.ChangeEvent(this._enclosing, this._enclosing.rows)
								);
						}
					}
					this._enclosing.lastError = null;
				}
			}

			private readonly LiveQuery _enclosing;
		}

		/// <summary>
		/// kick off async task that will wait until the query finishes, and after it
		/// does, it will run upate() again in case the current query in flight misses
		/// some of the recently added items.
		/// </summary>
		/// <remarks>
		/// kick off async task that will wait until the query finishes, and after it
		/// does, it will run upate() again in case the current query in flight misses
		/// some of the recently added items.
		/// </remarks>
		private Future RerunUpdateAfterQueryFinishes()
		{
			return GetDatabase().GetManager().RunAsync(new _Runnable_315(this));
		}

		private sealed class _Runnable_315 : Runnable
		{
			public _Runnable_315(LiveQuery _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Run()
			{
				if (this._enclosing.runningState.Get() == false)
				{
					Log.D(Database.Tag, this + ": rerunUpdateAfterQueryFinishes.run() fired, but running state == false.  Ignoring."
						);
					return;
				}
				if (this._enclosing.queryFuture != null)
				{
					try
					{
						this._enclosing.queryFuture.Get();
						this._enclosing.Update();
					}
					catch (Exception e)
					{
						if (e is CancellationException)
						{
						}
						else
						{
							// can safely ignore these
							Log.E(Database.Tag, "Got exception waiting for queryFuture to finish", e);
						}
					}
				}
			}

			private readonly LiveQuery _enclosing;
		}

		/// <exclude></exclude>
		[InterfaceAudience.Private]
		public void Changed(Database.ChangeEvent @event)
		{
			Log.D(Database.Tag, this + ": changed() called");
			Update();
		}

		[InterfaceAudience.Private]
		private void SetRows(QueryEnumerator queryEnumerator)
		{
			lock (this)
			{
				rows = queryEnumerator;
			}
		}
	}
}
