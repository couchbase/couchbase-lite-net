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
	public class LiveQuery : Query, Database.ChangeListener
	{
		private bool observing;

		private bool willUpdate;

		private QueryEnumerator rows;

		private IList<LiveQuery.ChangeListener> observers = new AList<LiveQuery.ChangeListener
			>();

		private Exception lastError;

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		internal LiveQuery(Query query) : base(query.GetDatabase(), query.GetView())
		{
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

		/// <summary>In LiveQuery the rows accessor is a non-blocking property.</summary>
		/// <remarks>
		/// In LiveQuery the rows accessor is a non-blocking property.
		/// Its value will be nil until the initial query finishes.
		/// </remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public override QueryEnumerator Run()
		{
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
		public virtual Exception GetLastError()
		{
			return lastError;
		}

		/// <summary>Starts observing database changes.</summary>
		/// <remarks>
		/// Starts observing database changes. The .rows property will now update automatically. (You
		/// usually don't need to call this yourself, since calling rows()
		/// call start for you.)
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual void Start()
		{
			if (!observing)
			{
				observing = true;
				GetDatabase().AddChangeListener(this);
			}
			Update();
		}

		/// <summary>Stops observing database changes.</summary>
		/// <remarks>Stops observing database changes. Calling start() or rows() will restart it.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual void Stop()
		{
			if (observing)
			{
				observing = false;
				GetDatabase().RemoveChangeListener(this);
			}
			if (willUpdate)
			{
				SetWillUpdate(false);
				updateQueryFuture.Cancel(true);
			}
		}

		/// <summary>Blocks until the intial async query finishes.</summary>
		/// <remarks>Blocks until the intial async query finishes. After this call either .rows or .error will be non-nil.
		/// 	</remarks>
		/// <exception cref="System.Exception"></exception>
		/// <exception cref="Sharpen.ExecutionException"></exception>
		[InterfaceAudience.Public]
		public virtual void WaitForRows()
		{
			Start();
			try
			{
				updateQueryFuture.Get();
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Got interrupted exception waiting for rows", e);
				throw;
			}
			catch (ExecutionException e)
			{
				Log.E(Database.Tag, "Got execution exception waiting for rows", e);
				throw;
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
		public virtual void AddChangeListener(LiveQuery.ChangeListener changeListener)
		{
			observers.AddItem(changeListener);
		}

		/// <summary>Remove previously added change listener</summary>
		[InterfaceAudience.Public]
		public virtual void RemoveChangeListener(LiveQuery.ChangeListener changeListener)
		{
			observers.Remove(changeListener);
		}

		internal virtual void Update()
		{
			if (GetView() == null)
			{
				throw new InvalidOperationException("Cannot start LiveQuery when view is null");
			}
			SetWillUpdate(false);
			updateQueryFuture = RunAsyncInternal(new _QueryCompleteListener_134(this));
		}

		private sealed class _QueryCompleteListener_134 : Query.QueryCompleteListener
		{
			public _QueryCompleteListener_134(LiveQuery _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Completed(QueryEnumerator rows, Exception error)
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
					if (rows != null && !rows.Equals(rows))
					{
						this._enclosing.SetRows(rows);
						foreach (LiveQuery.ChangeListener observer in this._enclosing.observers)
						{
							observer.Changed(new LiveQuery.ChangeEvent(this._enclosing, rows));
						}
					}
					this._enclosing.lastError = null;
				}
			}

			private readonly LiveQuery _enclosing;
		}

		public virtual void Changed(Database.ChangeEvent @event)
		{
			if (!willUpdate)
			{
				SetWillUpdate(true);
				Update();
			}
		}

		private void SetRows(QueryEnumerator queryEnumerator)
		{
			lock (this)
			{
				rows = queryEnumerator;
			}
		}

		private void SetWillUpdate(bool willUpdateParam)
		{
			lock (this)
			{
				willUpdate = willUpdateParam;
			}
		}

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

		public interface ChangeListener
		{
			void Changed(LiveQuery.ChangeEvent @event);
		}
	}
}
