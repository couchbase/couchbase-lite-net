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
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>Builder class to build a linked list (chain) of unique class instances.</summary>
	/// <remarks>
	/// Builder class to build a linked list (chain) of unique class instances. Each class can have
	/// only one instance in the list. Useful for building lists of protocol interceptors.
	/// </remarks>
	/// <seealso cref="ImmutableHttpProcessor">ImmutableHttpProcessor</seealso>
	/// <since>4.3</since>
	internal sealed class ChainBuilder<E>
	{
		private readonly List<E> list;

		private readonly IDictionary<Type, E> uniqueClasses;

		public ChainBuilder()
		{
			this.list = new List<E>();
			this.uniqueClasses = new Dictionary<Type, E>();
		}

		private void EnsureUnique(E e)
		{
			E previous = Sharpen.Collections.Remove(this.uniqueClasses, e.GetType());
			if (previous != null)
			{
				this.list.Remove(previous);
			}
			this.uniqueClasses.Put(e.GetType(), e);
		}

		public Org.Apache.Http.Protocol.ChainBuilder<E> AddFirst(E e)
		{
			if (e == null)
			{
				return this;
			}
			EnsureUnique(e);
			this.list.AddFirst(e);
			return this;
		}

		public Org.Apache.Http.Protocol.ChainBuilder<E> AddLast(E e)
		{
			if (e == null)
			{
				return this;
			}
			EnsureUnique(e);
			this.list.AddLast(e);
			return this;
		}

		public Org.Apache.Http.Protocol.ChainBuilder<E> AddAllFirst(ICollection<E> c)
		{
			if (c == null)
			{
				return this;
			}
			foreach (E e in c)
			{
				AddFirst(e);
			}
			return this;
		}

		public Org.Apache.Http.Protocol.ChainBuilder<E> AddAllFirst(params E[] c)
		{
			if (c == null)
			{
				return this;
			}
			foreach (E e in c)
			{
				AddFirst(e);
			}
			return this;
		}

		public Org.Apache.Http.Protocol.ChainBuilder<E> AddAllLast(ICollection<E> c)
		{
			if (c == null)
			{
				return this;
			}
			foreach (E e in c)
			{
				AddLast(e);
			}
			return this;
		}

		public Org.Apache.Http.Protocol.ChainBuilder<E> AddAllLast(params E[] c)
		{
			if (c == null)
			{
				return this;
			}
			foreach (E e in c)
			{
				AddLast(e);
			}
			return this;
		}

		public List<E> Build()
		{
			return new List<E>(this.list);
		}
	}
}
