//
// AbstractList.cs
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
namespace Sharpen
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Reflection;

	internal abstract class AbstractList<T> : AbstractCollection<T>, IEnumerable, ICollection<T>, IEnumerable<T>, IList<T>
	{
		protected AbstractList ()
		{
		}

		public override bool AddItem (T element)
		{
			Add (Count, element);
			return true;
		}

		public virtual void Add (int index, T element)
		{
			throw new NotSupportedException ();
		}

		public virtual bool AddAll<Q>(ICollection<Q> c) where Q:T
		{
			foreach (var q in c)
				AddItem (q);
			return true;
		}

		public override void Clear ()
		{
			RemoveRange (0, Count);
		}

		public abstract T Get (int index);
		
		public override Iterator<T> Iterator ()
		{
			return new SimpleIterator (this);
		}

		public virtual T Remove (int index)
		{
			if (index < 0) {
				throw new IndexOutOfRangeException ();
			}
			int num = 0;
			object item = null;
			Sharpen.Iterator iterator = this.Iterator ();
			while (num <= index) {
				if (!iterator.HasNext ()) {
					throw new IndexOutOfRangeException ();
				}
				item = iterator.Next ();
				num++;
			}
			iterator.Remove ();
			return (T)item;
		}

		public virtual void RemoveRange (int index, int toIndex)
		{
			int num = 0;
			Sharpen.Iterator iterator = this.Iterator ();
			while (num <= index) {
				if (!iterator.HasNext ()) {
					throw new IndexOutOfRangeException ();
				}
				iterator.Next ();
				num++;
			}
			if (index < toIndex) {
				iterator.Remove ();
			}
			for (num = index + 1; num < toIndex; num++) {
				if (!iterator.HasNext ()) {
					throw new IndexOutOfRangeException ();
				}
				iterator.Next ();
				iterator.Remove ();
			}
		}

		public virtual T Set (int index, T element)
		{
			throw new NotSupportedException ();
		}
		
		public override bool Equals (object obj)
		{
			if (obj == this)
				return true;
			IList list = obj as IList;
			if (list == null)
				return false;
			if (list.Count != Count)
				return false;
			for (int n=0; n<list.Count; n++) {
				if (!object.Equals (Get(n), list[n]))
					return false;
			}
			return true;
		}
		
		public override int GetHashCode ()
		{
			int h = 0;
			foreach (object o in this)
				if (o != null)
					h += o.GetHashCode ();
			return h;
		}

		int IList<T>.IndexOf (T item)
		{
			int num = 0;
			foreach (T t in this) {
				if (object.ReferenceEquals (t, item) || t.Equals (item))
					return num;
				num++;
			}
			return -1;
		}

		void IList<T>.Insert (int index, T item)
		{
			Add (index, item);
		}

		void IList<T>.RemoveAt (int index)
		{
			Remove (index);
		}

		public T this[int n] {
			get { return Get (n); }
			set { Set (n, value); }
		}

		private class SimpleIterator : Iterator<T>
		{
			private int current;
			private AbstractList<T> list;

			public SimpleIterator (AbstractList<T> list)
			{
				this.current = 0;
				this.list = list;
			}

			public override bool HasNext ()
			{
				return (current < list.Count);
			}

			public override T Next ()
			{
				return list.Get (current++);
			}

			public override void Remove ()
			{
				list.Remove (--current);
			}
		}
	}
}
