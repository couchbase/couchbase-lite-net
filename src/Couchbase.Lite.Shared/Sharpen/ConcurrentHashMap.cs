//
// ConcurrentHashMap.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
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

    internal class ConcurrentHashMap<T, U> : AbstractMap<T, U>, IEnumerable, ConcurrentMap<T, U>, IDictionary<T, U>, IEnumerable<KeyValuePair<T, U>>, ICollection<KeyValuePair<T, U>>
    {
        private IDictionary<T, U> table;

        public ConcurrentHashMap ()
        {
            table = new Dictionary<T, U> ();
        }

        public ConcurrentHashMap (int initialCapacity, float loadFactor, int concurrencyLevel) : this(initialCapacity)
        { }

        public ConcurrentHashMap (int initialCapacity)
        {
            table = new Dictionary<T, U> (initialCapacity);
        }

        public ConcurrentHashMap (IDictionary<T, U> source) {
            table = source;
        }

        public override void Clear ()
        {
            lock (table) {
                table = new Dictionary<T, U> ();
            }
        }

        public override bool ContainsKey (object name)
        {
            return table.ContainsKey ((T)name);
        }

        public override ICollection<KeyValuePair<T, U>> EntrySet ()
        {
            return this;
        }

        public override U Get (object key)
        {
            U local;
            table.TryGetValue ((T)key, out local);
            return local;
        }

        protected override IEnumerator<KeyValuePair<T, U>> InternalGetEnumerator ()
        {
            return table.GetEnumerator ();
        }

        public override bool IsEmpty ()
        {
            return table.Count == 0;
        }

        public override U Put (T key, U value)
        {
            lock (table) {
                U old = Get (key);
                Dictionary<T, U> newTable = new Dictionary<T, U> (table);
                newTable[key] = value;
                table = newTable;
                return old;
            }
        }

        public U PutIfAbsent (T key, U value)
        {
            lock (table) {
                if (!ContainsKey (key)) {
                    Dictionary<T, U> newTable = new Dictionary<T, U> (table);
                    newTable[key] = value;
                    table = newTable;
                    return value;
                }
                return Get (key);
            }
        }

        public override U Remove (object key)
        {
            lock (table) {
                U old = Get ((T)key);
                Dictionary<T, U> newTable = new Dictionary<T, U> (table);
                newTable.Remove ((T)key);
                table = newTable;
                return old;
            }
        }

        public bool Remove (object key, object value)
        {
            lock (table) {
                if (ContainsKey (key) && value.Equals (Get (key))) {
                    Dictionary<T, U> newTable = new Dictionary<T, U> (table);
                    newTable.Remove ((T)key);
                    table = newTable;
                    return true;
                }
                return false;
            }
        }

        public bool Replace (T key, U oldValue, U newValue)
        {
            lock (table) {
                if (ContainsKey (key) && oldValue.Equals (Get (key))) {
                    Dictionary<T, U> newTable = new Dictionary<T, U> (table);
                    newTable[key] = newValue;
                    table = newTable;
                    return true;
                }
                return false;
            }
        }

        public override IEnumerable<T> Keys {
            get { return table.Keys; }
        }

        public override IEnumerable<U> Values {
            get { return table.Values; }
        }
    }
}
