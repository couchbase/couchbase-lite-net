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
using System.Text;
using Org.Apache.Http.Concurrent;
using Org.Apache.Http.Pool;
using Org.Apache.Http.Util;
using Sharpen;
using Mono.CSharp;

namespace Org.Apache.Http.Pool
{
	/// <summary>Abstract synchronous (blocking) pool of connections.</summary>
	/// <remarks>
	/// Abstract synchronous (blocking) pool of connections.
	/// <p/>
	/// Please note that this class does not maintain its own pool of execution
	/// <see cref="Sharpen.Thread">Sharpen.Thread</see>
	/// s.
	/// Therefore, one <b>must</b> call
	/// <see cref="Sharpen.Future{V}.Get()">Sharpen.Future&lt;V&gt;.Get()</see>
	/// or
	/// <see cref="Sharpen.Future{V}.Get(long, Sharpen.TimeUnit)">Sharpen.Future&lt;V&gt;.Get(long, Sharpen.TimeUnit)
	/// 	</see>
	/// method on the
	/// <see cref="Sharpen.Future{V}">Sharpen.Future&lt;V&gt;</see>
	/// object returned by the
	/// <see cref="AbstractConnPool{T, C, E}.Lease(object, object, Org.Apache.Http.Concurrent.FutureCallback{T})
	/// 	">AbstractConnPool&lt;T, C, E&gt;.Lease(object, object, Org.Apache.Http.Concurrent.FutureCallback&lt;T&gt;)
	/// 	</see>
	/// method in order for the lease operation
	/// to complete.
	/// </remarks>
	/// <?></?>
	/// <?></?>
	/// <?></?>
	/// <since>4.2</since>
	public abstract class AbstractConnPool<T, C, E> : ConnPool<T, E>, ConnPoolControl
		<T> where E:PoolEntry<T, C>
	{
		private readonly Lock Lock;

		private readonly ConnFactory<T, C> connFactory;

		private readonly IDictionary<T, RouteSpecificPool<T, C, E>> routeToPool;

		private readonly ICollection<E> leased;

		private readonly List<E> available;

		private readonly List<PoolEntryFuture<E>> pending;

		private readonly IDictionary<T, int> maxPerRoute;

		private volatile bool isShutDown;

		private volatile int defaultMaxPerRoute;

		private volatile int maxTotal;

		public AbstractConnPool(ConnFactory<T, C> connFactory, int defaultMaxPerRoute, int
			 maxTotal) : base()
		{
			this.connFactory = Args.NotNull(connFactory, "Connection factory");
			this.defaultMaxPerRoute = Args.NotNegative(defaultMaxPerRoute, "Max per route value"
				);
			this.maxTotal = Args.NotNegative(maxTotal, "Max total value");
			this.Lock = new ReentrantLock();
			this.routeToPool = new Dictionary<T, RouteSpecificPool<T, C, E>>();
			this.leased = new HashSet<E>();
			this.available = new List<E>();
			this.pending = new List<PoolEntryFuture<E>>();
			this.maxPerRoute = new Dictionary<T, int>();
		}

		/// <summary>Creates a new entry for the given connection with the given route.</summary>
		/// <remarks>Creates a new entry for the given connection with the given route.</remarks>
		protected internal abstract E CreateEntry(T route, C conn);

		/// <since>4.3</since>
		protected internal virtual void OnLease(E entry)
		{
		}

		/// <since>4.3</since>
		protected internal virtual void OnRelease(E entry)
		{
		}

		public virtual bool IsShutdown()
		{
			return this.isShutDown;
		}

		/// <summary>Shuts down the pool.</summary>
		/// <remarks>Shuts down the pool.</remarks>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Shutdown()
		{
			if (this.isShutDown)
			{
				return;
			}
			this.isShutDown = true;
			this.Lock.Lock();
			try
			{
				foreach (E entry in this.available)
				{
					entry.Close();
				}
				foreach (E entry_1 in this.leased)
				{
					entry_1.Close();
				}
				foreach (RouteSpecificPool<T, C, E> pool in this.routeToPool.Values)
				{
					pool.Shutdown();
				}
				this.routeToPool.Clear();
				this.leased.Clear();
				this.available.Clear();
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		private RouteSpecificPool<T, C, E> GetPool(T route)
		{
			RouteSpecificPool<T, C, E> pool = this.routeToPool.Get(route);
			if (pool == null)
			{
				pool = new _RouteSpecificPool_146(this, route, route);
				this.routeToPool.Put(route, pool);
			}
			return pool;
		}

		private sealed class _RouteSpecificPool_146 : RouteSpecificPool<T, C, E>
		{
			public _RouteSpecificPool_146(AbstractConnPool<T, C, E> _enclosing, T route, T baseArg1
				) : base(baseArg1)
			{
				this._enclosing = _enclosing;
				this.route = route;
			}

			protected internal override E CreateEntry(C conn)
			{
				return this._enclosing._enclosing.CreateEntry(route, conn);
			}

			private readonly AbstractConnPool<T, C, E> _enclosing;

			private readonly T route;
		}

		/// <summary>
		/// <inheritDoc></inheritDoc>
		/// <p/>
		/// Please note that this class does not maintain its own pool of execution
		/// <see cref="Sharpen.Thread">Sharpen.Thread</see>
		/// s. Therefore, one <b>must</b> call
		/// <see cref="Sharpen.Future{V}.Get()">Sharpen.Future&lt;V&gt;.Get()</see>
		/// or
		/// <see cref="Sharpen.Future{V}.Get(long, Sharpen.TimeUnit)">Sharpen.Future&lt;V&gt;.Get(long, Sharpen.TimeUnit)
		/// 	</see>
		/// method on the
		/// <see cref="Sharpen.Future{V}">Sharpen.Future&lt;V&gt;</see>
		/// returned by this method in order for the lease operation to complete.
		/// </summary>
		public virtual Future<E> Lease(T route, object state, FutureCallback<E> callback)
		{
			Args.NotNull(route, "Route");
			Asserts.Check(!this.isShutDown, "Connection pool shut down");
			return new _PoolEntryFuture_170(this, route, state, this.Lock, callback);
		}

		private sealed class _PoolEntryFuture_170 : PoolEntryFuture<E>
		{
			public _PoolEntryFuture_170(AbstractConnPool<T, C, E> _enclosing, T route, object
				 state, Lock baseArg1, FutureCallback<E> baseArg2) : base(baseArg1, baseArg2)
			{
				this._enclosing = _enclosing;
				this.route = route;
				this.state = state;
			}

			/// <exception cref="System.Exception"></exception>
			/// <exception cref="Sharpen.TimeoutException"></exception>
			/// <exception cref="System.IO.IOException"></exception>
			protected internal override E GetPoolEntry(long timeout, TimeUnit tunit)
			{
				E entry = this._enclosing.GetPoolEntryBlocking(route, state, timeout, tunit, this
					);
				this._enclosing.OnLease(entry);
				return entry;
			}

			private readonly AbstractConnPool<T, C, E> _enclosing;

			private readonly T route;

			private readonly object state;
		}

		/// <summary>
		/// Attempts to lease a connection for the given route and with the given
		/// state from the pool.
		/// </summary>
		/// <remarks>
		/// Attempts to lease a connection for the given route and with the given
		/// state from the pool.
		/// <p/>
		/// Please note that this class does not maintain its own pool of execution
		/// <see cref="Sharpen.Thread">Sharpen.Thread</see>
		/// s. Therefore, one <b>must</b> call
		/// <see cref="Sharpen.Future{V}.Get()">Sharpen.Future&lt;V&gt;.Get()</see>
		/// or
		/// <see cref="Sharpen.Future{V}.Get(long, Sharpen.TimeUnit)">Sharpen.Future&lt;V&gt;.Get(long, Sharpen.TimeUnit)
		/// 	</see>
		/// method on the
		/// <see cref="Sharpen.Future{V}">Sharpen.Future&lt;V&gt;</see>
		/// returned by this method in order for the lease operation to complete.
		/// </remarks>
		/// <param name="route">route of the connection.</param>
		/// <param name="state">
		/// arbitrary object that represents a particular state
		/// (usually a security principal or a unique token identifying
		/// the user whose credentials have been used while establishing the connection).
		/// May be <code>null</code>.
		/// </param>
		/// <returns>future for a leased pool entry.</returns>
		public virtual Future<E> Lease(T route, object state)
		{
			return Lease(route, state, null);
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="System.Exception"></exception>
		/// <exception cref="Sharpen.TimeoutException"></exception>
		private E GetPoolEntryBlocking(T route, object state, long timeout, TimeUnit tunit
			, PoolEntryFuture<E> future)
		{
			DateTime deadline = null;
			if (timeout > 0)
			{
				deadline = Sharpen.Extensions.CreateDate(Runtime.CurrentTimeMillis() + tunit.ToMillis
					(timeout));
			}
			this.Lock.Lock();
			try
			{
				RouteSpecificPool<T, C, E> pool = GetPool(route);
				E entry = null;
				while (entry == null)
				{
					Asserts.Check(!this.isShutDown, "Connection pool shut down");
					for (; ; )
					{
						entry = pool.GetFree(state);
						if (entry == null)
						{
							break;
						}
						if (entry.IsClosed() || entry.IsExpired(Runtime.CurrentTimeMillis()))
						{
							entry.Close();
							this.available.Remove(entry);
							pool.Free(entry, false);
						}
						else
						{
							break;
						}
					}
					if (entry != null)
					{
						this.available.Remove(entry);
						this.leased.AddItem(entry);
						return entry;
					}
					// New connection is needed
					int maxPerRoute = GetMax(route);
					// Shrink the pool prior to allocating a new connection
					int excess = Math.Max(0, pool.GetAllocatedCount() + 1 - maxPerRoute);
					if (excess > 0)
					{
						for (int i = 0; i < excess; i++)
						{
							E lastUsed = pool.GetLastUsed();
							if (lastUsed == null)
							{
								break;
							}
							lastUsed.Close();
							this.available.Remove(lastUsed);
							pool.Remove(lastUsed);
						}
					}
					if (pool.GetAllocatedCount() < maxPerRoute)
					{
						int totalUsed = this.leased.Count;
						int freeCapacity = Math.Max(this.maxTotal - totalUsed, 0);
						if (freeCapacity > 0)
						{
							int totalAvailable = this.available.Count;
							if (totalAvailable > freeCapacity - 1)
							{
								if (!this.available.IsEmpty())
								{
									E lastUsed = this.available.RemoveLast();
									lastUsed.Close();
									RouteSpecificPool<T, C, E> otherpool = GetPool(lastUsed.GetRoute());
									otherpool.Remove(lastUsed);
								}
							}
							C conn = this.connFactory.Create(route);
							entry = pool.Add(conn);
							this.leased.AddItem(entry);
							return entry;
						}
					}
					bool success = false;
					try
					{
						pool.Queue(future);
						this.pending.AddItem(future);
						success = future.Await(deadline);
					}
					finally
					{
						// In case of 'success', we were woken up by the
						// connection pool and should now have a connection
						// waiting for us, or else we're shutting down.
						// Just continue in the loop, both cases are checked.
						pool.Unqueue(future);
						this.pending.Remove(future);
					}
					// check for spurious wakeup vs. timeout
					if (!success && (deadline != null) && (deadline.GetTime() <= Runtime.CurrentTimeMillis
						()))
					{
						break;
					}
				}
				throw new TimeoutException("Timeout waiting for connection");
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		public virtual void Release(E entry, bool reusable)
		{
			this.Lock.Lock();
			try
			{
				if (this.leased.Remove(entry))
				{
					RouteSpecificPool<T, C, E> pool = GetPool(entry.GetRoute());
					pool.Free(entry, reusable);
					if (reusable && !this.isShutDown)
					{
						this.available.AddFirst(entry);
						OnRelease(entry);
					}
					else
					{
						entry.Close();
					}
					PoolEntryFuture<E> future = pool.NextPending();
					if (future != null)
					{
						this.pending.Remove(future);
					}
					else
					{
						future = this.pending.Poll();
					}
					if (future != null)
					{
						future.Wakeup();
					}
				}
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		private int GetMax(T route)
		{
			int v = this.maxPerRoute.Get(route);
			if (v != null)
			{
				return v;
			}
			else
			{
				return this.defaultMaxPerRoute;
			}
		}

		public virtual void SetMaxTotal(int max)
		{
			Args.NotNegative(max, "Max value");
			this.Lock.Lock();
			try
			{
				this.maxTotal = max;
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		public virtual int GetMaxTotal()
		{
			this.Lock.Lock();
			try
			{
				return this.maxTotal;
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		public virtual void SetDefaultMaxPerRoute(int max)
		{
			Args.NotNegative(max, "Max per route value");
			this.Lock.Lock();
			try
			{
				this.defaultMaxPerRoute = max;
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		public virtual int GetDefaultMaxPerRoute()
		{
			this.Lock.Lock();
			try
			{
				return this.defaultMaxPerRoute;
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		public virtual void SetMaxPerRoute(T route, int max)
		{
			Args.NotNull(route, "Route");
			Args.NotNegative(max, "Max per route value");
			this.Lock.Lock();
			try
			{
				this.maxPerRoute.Put(route, Sharpen.Extensions.ValueOf(max));
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		public virtual int GetMaxPerRoute(T route)
		{
			Args.NotNull(route, "Route");
			this.Lock.Lock();
			try
			{
				return GetMax(route);
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		public virtual PoolStats GetTotalStats()
		{
			this.Lock.Lock();
			try
			{
				return new PoolStats(this.leased.Count, this.pending.Count, this.available.Count, 
					this.maxTotal);
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		public virtual PoolStats GetStats(T route)
		{
			Args.NotNull(route, "Route");
			this.Lock.Lock();
			try
			{
				RouteSpecificPool<T, C, E> pool = GetPool(route);
				return new PoolStats(pool.GetLeasedCount(), pool.GetPendingCount(), pool.GetAvailableCount
					(), GetMax(route));
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		/// <summary>Enumerates all available connections.</summary>
		/// <remarks>Enumerates all available connections.</remarks>
		/// <since>4.3</since>
		protected internal virtual void EnumAvailable(PoolEntryCallback<T, C> callback)
		{
			this.Lock.Lock();
			try
			{
				EnumEntries(this.available.GetEnumerator(), callback);
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		/// <summary>Enumerates all leased connections.</summary>
		/// <remarks>Enumerates all leased connections.</remarks>
		/// <since>4.3</since>
		protected internal virtual void EnumLeased(PoolEntryCallback<T, C> callback)
		{
			this.Lock.Lock();
			try
			{
				EnumEntries(this.leased.GetEnumerator(), callback);
			}
			finally
			{
				this.Lock.Unlock();
			}
		}

		private void EnumEntries(IEnumerator<E> it, PoolEntryCallback<T, C> callback)
		{
			while (it.HasNext())
			{
				E entry = it.Next();
				callback.Process(entry);
				if (entry.IsClosed())
				{
					RouteSpecificPool<T, C, E> pool = GetPool(entry.GetRoute());
					pool.Remove(entry);
					it.Remove();
				}
			}
		}

		private void PurgePoolMap()
		{
			IEnumerator<KeyValuePair<T, RouteSpecificPool<T, C, E>>> it = this.routeToPool.EntrySet
				().GetEnumerator();
			while (it.HasNext())
			{
				KeyValuePair<T, RouteSpecificPool<T, C, E>> entry = it.Next();
				RouteSpecificPool<T, C, E> pool = entry.Value;
				if (pool.GetPendingCount() + pool.GetAllocatedCount() == 0)
				{
					it.Remove();
				}
			}
		}

		/// <summary>
		/// Closes connections that have been idle longer than the given period
		/// of time and evicts them from the pool.
		/// </summary>
		/// <remarks>
		/// Closes connections that have been idle longer than the given period
		/// of time and evicts them from the pool.
		/// </remarks>
		/// <param name="idletime">maximum idle time.</param>
		/// <param name="tunit">time unit.</param>
		public virtual void CloseIdle(long idletime, TimeUnit tunit)
		{
			Args.NotNull(tunit, "Time unit");
			long time = tunit.ToMillis(idletime);
			if (time < 0)
			{
				time = 0;
			}
			long deadline = Runtime.CurrentTimeMillis() - time;
			EnumAvailable(new _PoolEntryCallback_491(deadline));
			PurgePoolMap();
		}

		private sealed class _PoolEntryCallback_491 : PoolEntryCallback<T, C>
		{
			public _PoolEntryCallback_491(long deadline)
			{
				this.deadline = deadline;
			}

			public void Process(PoolEntry<T, C> entry)
			{
				if (entry.GetUpdated() <= deadline)
				{
					entry.Close();
				}
			}

			private readonly long deadline;
		}

		/// <summary>Closes expired connections and evicts them from the pool.</summary>
		/// <remarks>Closes expired connections and evicts them from the pool.</remarks>
		public virtual void CloseExpired()
		{
			long now = Runtime.CurrentTimeMillis();
			EnumAvailable(new _PoolEntryCallback_508(now));
			PurgePoolMap();
		}

		private sealed class _PoolEntryCallback_508 : PoolEntryCallback<T, C>
		{
			public _PoolEntryCallback_508(long now)
			{
				this.now = now;
			}

			public void Process(PoolEntry<T, C> entry)
			{
				if (entry.IsExpired(now))
				{
					entry.Close();
				}
			}

			private readonly long now;
		}

		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append("[leased: ");
			buffer.Append(this.leased);
			buffer.Append("][available: ");
			buffer.Append(this.available);
			buffer.Append("][pending: ");
			buffer.Append(this.pending);
			buffer.Append("]");
			return buffer.ToString();
		}
	}
}
