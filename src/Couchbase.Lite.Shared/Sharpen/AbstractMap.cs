//
// AbstractMap.cs
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
	using System.Linq;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;

    internal abstract class AbstractMap<T, U> : IEnumerable, ICollection<KeyValuePair<T, U>>, IEnumerable<KeyValuePair<T, U>>, IDictionary<T, U>
	{
		protected AbstractMap ()
		{
		}

		public virtual void Clear ()
		{
			EntrySet ().Clear ();
		}

		public virtual bool ContainsKey (object name)
		{
			return EntrySet ().Any (p => p.Key.Equals ((T)name));
		}

		public abstract ICollection<KeyValuePair<T, U>> EntrySet ();

		public virtual U Get (object key)
		{
			return EntrySet ().Where (p => p.Key.Equals (key)).Select (p => p.Value).FirstOrDefault ();
		}

		protected virtual IEnumerator<KeyValuePair<T, U>> InternalGetEnumerator ()
		{
			return EntrySet ().GetEnumerator ();
		}

		public virtual bool IsEmpty ()
		{
			return !EntrySet ().Any ();
		}

		public virtual U Put (T key, U value)
		{
			throw new NotSupportedException ();
		}

		public virtual U Remove (object key)
		{
			Sharpen.Iterator<U> iterator = EntrySet () as Sharpen.Iterator<U>;
			if (iterator == null) {
				throw new NotSupportedException ();
			}
			while (iterator.HasNext ()) {
				U local = iterator.Next ();
				if (local.Equals ((T)key)) {
					iterator.Remove ();
					return local;
				}
			}
			return default(U);
		}

		void ICollection<KeyValuePair<T, U>>.Add (KeyValuePair<T, U> item)
		{
			Put (item.Key, item.Value);
		}

		bool ICollection<KeyValuePair<T, U>>.Contains (KeyValuePair<T, U> item)
		{
			throw new NotImplementedException ();
		}

		void ICollection<KeyValuePair<T, U>>.CopyTo (KeyValuePair<T, U>[] array, int arrayIndex)
		{
			EntrySet ().CopyTo (array, arrayIndex);
		}

		bool ICollection<KeyValuePair<T, U>>.Remove (KeyValuePair<T, U> item)
		{
			Remove (item.Key);
			return true;
		}

		void IDictionary<T, U>.Add (T key, U value)
		{
			Put (key, value);
		}

		bool IDictionary<T, U>.ContainsKey (T key)
		{
			return ContainsKey (key);
		}

		bool IDictionary<T, U>.Remove (T key)
		{
			if (ContainsKey (key)) {
				Remove (key);
				return true;
			}
			return false;
		}

		bool IDictionary<T, U>.TryGetValue (T key, out U value)
		{
			if (ContainsKey (key)) {
				value = Get (key);
				return true;
			}
			value = default(U);
			return false;
		}

		IEnumerator<KeyValuePair<T, U>> IEnumerable<KeyValuePair<T, U>>.GetEnumerator ()
		{
			return InternalGetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return InternalGetEnumerator ();
		}

		public virtual int Count {
			get { return EntrySet ().Count; }
		}

		public U this[T key] {
			get { return Get (key); }
			set { Put (key, value); }
		}

		public virtual IEnumerable<T> Keys {
			get { return EntrySet ().Select (p => p.Key); }
		}

		int ICollection<KeyValuePair<T, U>>.Count {
			get { return Count; }
		}

		bool ICollection<KeyValuePair<T, U>>.IsReadOnly {
			get { return false; }
		}

		ICollection<T> IDictionary<T, U>.Keys {
			get { return Keys.ToList<T> (); }
		}

		ICollection<U> IDictionary<T, U>.Values {
			get { return (ICollection<U>)Values.ToList<U> (); }
		}

		public virtual IEnumerable<U> Values {
			get { return EntrySet ().Select (p => p.Value); }
		}
	}
}
