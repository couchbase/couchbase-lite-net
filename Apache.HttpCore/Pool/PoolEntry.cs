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
using System.Text;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Pool
{
	/// <summary>Pool entry containing a pool connection object along with its route.</summary>
	/// <remarks>
	/// Pool entry containing a pool connection object along with its route.
	/// <p/>
	/// The connection contained by the pool entry may have an expiration time which
	/// can be either set upon construction time or updated with
	/// the
	/// <see cref="PoolEntry{T, C}.UpdateExpiry(long, Sharpen.TimeUnit)">PoolEntry&lt;T, C&gt;.UpdateExpiry(long, Sharpen.TimeUnit)
	/// 	</see>
	/// .
	/// <p/>
	/// Pool entry may also have an object associated with it that represents
	/// a connection state (usually a security principal or a unique token identifying
	/// the user whose credentials have been used while establishing the connection).
	/// </remarks>
	/// <?></?>
	/// <?></?>
	/// <since>4.2</since>
	public abstract class PoolEntry<T, C>
	{
		private readonly string id;

		private readonly T route;

		private readonly C conn;

		private readonly long created;

		private readonly long validUnit;

		private long updated;

		private long expiry;

		private volatile object state;

		/// <summary>Creates new <tt>PoolEntry</tt> instance.</summary>
		/// <remarks>Creates new <tt>PoolEntry</tt> instance.</remarks>
		/// <param name="id">unique identifier of the pool entry. May be <code>null</code>.</param>
		/// <param name="route">route to the opposite endpoint.</param>
		/// <param name="conn">the connection.</param>
		/// <param name="timeToLive">
		/// maximum time to live. May be zero if the connection
		/// does not have an expiry deadline.
		/// </param>
		/// <param name="tunit">time unit.</param>
		public PoolEntry(string id, T route, C conn, long timeToLive, TimeUnit tunit) : base
			()
		{
			Args.NotNull(route, "Route");
			Args.NotNull(conn, "Connection");
			Args.NotNull(tunit, "Time unit");
			this.id = id;
			this.route = route;
			this.conn = conn;
			this.created = Runtime.CurrentTimeMillis();
			if (timeToLive > 0)
			{
				this.validUnit = this.created + tunit.ToMillis(timeToLive);
			}
			else
			{
				this.validUnit = long.MaxValue;
			}
			this.expiry = this.validUnit;
		}

		/// <summary>Creates new <tt>PoolEntry</tt> instance without an expiry deadline.</summary>
		/// <remarks>Creates new <tt>PoolEntry</tt> instance without an expiry deadline.</remarks>
		/// <param name="id">unique identifier of the pool entry. May be <code>null</code>.</param>
		/// <param name="route">route to the opposite endpoint.</param>
		/// <param name="conn">the connection.</param>
		public PoolEntry(string id, T route, C conn) : this(id, route, conn, 0, TimeUnit.
			Milliseconds)
		{
		}

		public virtual string GetId()
		{
			return this.id;
		}

		public virtual T GetRoute()
		{
			return this.route;
		}

		public virtual C GetConnection()
		{
			return this.conn;
		}

		public virtual long GetCreated()
		{
			return this.created;
		}

		public virtual long GetValidUnit()
		{
			return this.validUnit;
		}

		public virtual object GetState()
		{
			return this.state;
		}

		public virtual void SetState(object state)
		{
			this.state = state;
		}

		public virtual long GetUpdated()
		{
			lock (this)
			{
				return this.updated;
			}
		}

		public virtual long GetExpiry()
		{
			lock (this)
			{
				return this.expiry;
			}
		}

		public virtual void UpdateExpiry(long time, TimeUnit tunit)
		{
			lock (this)
			{
				Args.NotNull(tunit, "Time unit");
				this.updated = Runtime.CurrentTimeMillis();
				long newExpiry;
				if (time > 0)
				{
					newExpiry = this.updated + tunit.ToMillis(time);
				}
				else
				{
					newExpiry = long.MaxValue;
				}
				this.expiry = Math.Min(newExpiry, this.validUnit);
			}
		}

		public virtual bool IsExpired(long now)
		{
			lock (this)
			{
				return now >= this.expiry;
			}
		}

		/// <summary>
		/// Invalidates the pool entry and closes the pooled connection associated
		/// with it.
		/// </summary>
		/// <remarks>
		/// Invalidates the pool entry and closes the pooled connection associated
		/// with it.
		/// </remarks>
		public abstract void Close();

		/// <summary>Returns <code>true</code> if the pool entry has been invalidated.</summary>
		/// <remarks>Returns <code>true</code> if the pool entry has been invalidated.</remarks>
		public abstract bool IsClosed();

		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append("[id:");
			buffer.Append(this.id);
			buffer.Append("][route:");
			buffer.Append(this.route);
			buffer.Append("][state:");
			buffer.Append(this.state);
			buffer.Append("]");
			return buffer.ToString();
		}
	}
}
