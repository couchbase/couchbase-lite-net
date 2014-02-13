//
// LinkedHashMap.cs
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
	using System.Collections.Generic;
	using System.Linq;

	internal class LinkedHashMap<T, U> : AbstractMap<T, U>
	{
        internal List<KeyValuePair<T, U>> List { get; private set; }
        internal Dictionary<T, U> Table { get; private set; }

        public LinkedHashMap ()
        {
            this.Table = new Dictionary<T, U> ();
            this.List = new List<KeyValuePair<T, U>> ();
        }

        public LinkedHashMap (Int32 initialCapacity)
        {
            this.Table = new Dictionary<T, U> ();
            this.List = new List<KeyValuePair<T, U>> (initialCapacity);
        }

        public LinkedHashMap (Int32 initialCapacity, Single loadFactor, Boolean accessOrder) : this(initialCapacity) { }

        public LinkedHashMap (LinkedHashMap<T, U> map)
        {
            this.Table = map.Table;
            this.List = map.List;
        }

		public override void Clear ()
		{
			Table.Clear ();
			List.Clear ();
		}
		
		public override int Count {
			get {
				return List.Count;
			}
		}
		
		public override bool ContainsKey (object name)
		{
			return Table.ContainsKey ((T)name);
		}

		public override ICollection<KeyValuePair<T, U>> EntrySet ()
		{
			return this;
		}

		public override U Get (object key)
		{
			U local;
			Table.TryGetValue ((T)key, out local);
			return local;
		}

		protected override IEnumerator<KeyValuePair<T, U>> InternalGetEnumerator ()
		{
			return List.GetEnumerator ();
		}

		public override bool IsEmpty ()
		{
			return (Table.Count == 0);
		}

		public override U Put (T key, U value)
		{
			U old;
			if (Table.TryGetValue (key, out old)) {
				int index = List.FindIndex (p => p.Key.Equals (key));
				if (index != -1)
					List.RemoveAt (index);
			}
			Table[key] = value;
			List.Add (new KeyValuePair<T, U> (key, value));
			return old;
		}

		public override U Remove (object key)
		{
			U local = default(U);
			if (Table.TryGetValue ((T)key, out local)) {
				int index = List.FindIndex (p => p.Key.Equals (key));
				if (index != -1)
					List.RemoveAt (index);
				Table.Remove ((T)key);
			}
			return local;
		}

		public override IEnumerable<T> Keys {
			get { return List.Select (p => p.Key); }
		}

		public override IEnumerable<U> Values {
			get { return List.Select (p => p.Value); }
		}
	}
}
