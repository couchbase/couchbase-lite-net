//
// AbstractCollection.cs
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

namespace Sharpen
{
	using System;
	using System.Collections;
	using System.Collections.Generic;

	public abstract class AbstractCollection<T> : Iterable<T>, IEnumerable, ICollection<T>, IEnumerable<T>
	{
		protected AbstractCollection ()
		{
		}

		public virtual bool AddItem (T element)
		{
			throw new NotSupportedException ();
		}

		public virtual void Clear ()
		{
			Iterator iterator = Iterator ();
			while (iterator.HasNext ()) {
				iterator.Next ();
				iterator.Remove ();
			}
		}

		public virtual bool Contains (object item)
		{
			foreach (var t in this) {
				if (object.ReferenceEquals (t, item) || t.Equals (item))
					return true;
			}
			return false;
		}

		public virtual bool ContainsAll (ICollection<object> c)
		{
			foreach (var t in c) {
				if (!Contains (t))
					return false;
			}
			return true;
		}

		public bool ContainsAll (ICollection<T> c)
		{
			List<object> list = new List<object> (c.Count);
			foreach (var t in c)
				list.Add (t);
			return ContainsAll ((ICollection<object>)list);
		}

		public virtual bool IsEmpty ()
		{
			return (this.Count == 0);
		}

		public virtual bool Remove (object element)
		{
			Iterator iterator = Iterator ();
			while (iterator.HasNext ()) {
				if (iterator.Next ().Equals (element)) {
					iterator.Remove ();
					return true;
				}
			}
			return false;
		}

		void ICollection<T>.Add (T element)
		{
			AddItem (element);
		}

		bool ICollection<T>.Contains (T item)
		{
			return Contains (item);
		}

		void ICollection<T>.CopyTo (T[] array, int arrayIndex)
		{
			foreach (T t in this)
				array[arrayIndex++] = t;
		}

		bool ICollection<T>.Remove (T item)
		{
			return Remove (item);
		}

		public abstract int Count { get; }

		bool ICollection<T>.IsReadOnly {
			get { return false; }
		}
	}
}
