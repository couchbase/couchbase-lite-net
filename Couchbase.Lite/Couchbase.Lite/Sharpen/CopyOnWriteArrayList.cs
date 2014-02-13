//
// CopyOnWriteArrayList.cs
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

	internal class CopyOnWriteArrayList<T> : Iterable<T>, IEnumerable, ICollection<T>, IEnumerable<T>, IList<T>
	{
		private List<T> list;

		public CopyOnWriteArrayList ()
		{
			this.list = new List<T> ();
		}

		public virtual void Add (T element)
		{
			lock (list) {
				List<T> newList = new List<T> (list);
				newList.Add (element);
				list = newList;
			}
		}

		public virtual void Add (int index, T element)
		{
			lock (list) {
				List<T> newList = new List<T> (list);
				newList.Insert (index, element);
				list = newList;
			}
		}

		public virtual void Clear ()
		{
			lock (list) {
				list = new List<T> ();
			}
		}

		public virtual T Get (int index)
		{
			return list[index];
		}

		public override Iterator<T> Iterator ()
		{
			return new EnumeratorWrapper<T> (list, list.GetEnumerator ());
		}

		public virtual T Remove (int index)
		{
			lock (list) {
				T old = list[index];
				List<T> newList = new List<T> (list);
				newList.RemoveAt (index);
				list = newList;
				return old;
			}
		}

		public virtual T Set (int index, T element)
		{
			lock (list) {
				T old = list[index];
				List<T> newList = new List<T> (list);
				newList[index] = element;
				list = newList;
				return old;
			}
		}

		bool ICollection<T>.Contains (T item)
		{
			return list.Contains (item);
		}

		void ICollection<T>.CopyTo (T[] array, int arrayIndex)
		{
			list.CopyTo (array, arrayIndex);
		}

		bool ICollection<T>.Remove (T item)
		{
			lock (list) {
				List<T> newList = new List<T> (list);
				bool removed = newList.Remove (item);
				list = newList;
				return removed;
			}
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

		public virtual int Count {
			get { return list.Count; }
		}

		public T this[int n] {
			get { return Get (n); }
			set { Set (n, value); }
		}

		bool ICollection<T>.IsReadOnly {
			get { return false; }
		}
	}
}
