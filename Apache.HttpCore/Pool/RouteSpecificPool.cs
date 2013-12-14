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

using System.Collections.Generic;
using System.Text;
using Org.Apache.Http.Pool;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Pool
{
	internal abstract class RouteSpecificPool<T, C, E> where E:PoolEntry<T, C>
	{
		private readonly T route;

		private readonly ICollection<E> leased;

		private readonly List<E> available;

		private readonly List<PoolEntryFuture<E>> pending;

		internal RouteSpecificPool(T route) : base()
		{
			this.route = route;
			this.leased = new HashSet<E>();
			this.available = new List<E>();
			this.pending = new List<PoolEntryFuture<E>>();
		}

		protected internal abstract E CreateEntry(C conn);

		public T GetRoute()
		{
			return route;
		}

		public virtual int GetLeasedCount()
		{
			return this.leased.Count;
		}

		public virtual int GetPendingCount()
		{
			return this.pending.Count;
		}

		public virtual int GetAvailableCount()
		{
			return this.available.Count;
		}

		public virtual int GetAllocatedCount()
		{
			return this.available.Count + this.leased.Count;
		}

		public virtual E GetFree(object state)
		{
			if (!this.available.IsEmpty())
			{
				if (state != null)
				{
					IEnumerator<E> it = this.available.GetEnumerator();
					while (it.HasNext())
					{
						E entry = it.Next();
						if (state.Equals(entry.GetState()))
						{
							it.Remove();
							this.leased.AddItem(entry);
							return entry;
						}
					}
				}
				IEnumerator<E> it_1 = this.available.GetEnumerator();
				while (it_1.HasNext())
				{
					E entry = it_1.Next();
					if (entry.GetState() == null)
					{
						it_1.Remove();
						this.leased.AddItem(entry);
						return entry;
					}
				}
			}
			return null;
		}

		public virtual E GetLastUsed()
		{
			if (!this.available.IsEmpty())
			{
				return this.available.GetLast();
			}
			else
			{
				return null;
			}
		}

		public virtual bool Remove(E entry)
		{
			Args.NotNull(entry, "Pool entry");
			if (!this.available.Remove(entry))
			{
				if (!this.leased.Remove(entry))
				{
					return false;
				}
			}
			return true;
		}

		public virtual void Free(E entry, bool reusable)
		{
			Args.NotNull(entry, "Pool entry");
			bool found = this.leased.Remove(entry);
			Asserts.Check(found, "Entry %s has not been leased from this pool", entry);
			if (reusable)
			{
				this.available.AddFirst(entry);
			}
		}

		public virtual E Add(C conn)
		{
			E entry = CreateEntry(conn);
			this.leased.AddItem(entry);
			return entry;
		}

		public virtual void Queue(PoolEntryFuture<E> future)
		{
			if (future == null)
			{
				return;
			}
			this.pending.AddItem(future);
		}

		public virtual PoolEntryFuture<E> NextPending()
		{
			return this.pending.Poll();
		}

		public virtual void Unqueue(PoolEntryFuture<E> future)
		{
			if (future == null)
			{
				return;
			}
			this.pending.Remove(future);
		}

		public virtual void Shutdown()
		{
			foreach (PoolEntryFuture<E> future in this.pending)
			{
				future.Cancel(true);
			}
			this.pending.Clear();
			foreach (E entry in this.available)
			{
				entry.Close();
			}
			this.available.Clear();
			foreach (E entry_1 in this.leased)
			{
				entry_1.Close();
			}
			this.leased.Clear();
		}

		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append("[route: ");
			buffer.Append(this.route);
			buffer.Append("][leased: ");
			buffer.Append(this.leased.Count);
			buffer.Append("][available: ");
			buffer.Append(this.available.Count);
			buffer.Append("][pending: ");
			buffer.Append(this.pending.Count);
			buffer.Append("]");
			return buffer.ToString();
		}
	}
}
