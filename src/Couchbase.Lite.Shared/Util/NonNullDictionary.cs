//
//  NonNullDictionary.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Couchbase.Lite.Util
{
    internal sealed class CollectionDebuggerView<T, U>
    {
        readonly ICollection<KeyValuePair<T, U>> c;

        public CollectionDebuggerView (ICollection<KeyValuePair<T, U>> col)
        {
            this.c = col;
        }

        [DebuggerBrowsable (DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<T, U>[] Items {
            get {
                var o = new KeyValuePair<T, U> [c.Count];
                c.CopyTo (o, 0);
                return o;
            }
        }
    }

    /// <summary>
    /// A dictionary that ignores any attempts to insert a null object into it.
    /// Usefor for creating JSON objects that should not contain null values
    /// </summary>
    [DebuggerDisplay ("Count={Count}")]
    [DebuggerTypeProxy (typeof (CollectionDebuggerView<,>))]
    public sealed class NonNullDictionary<K, V> : IEnumerable<KeyValuePair<K, V>>, IDictionary<K, V>
    {

        #region Variables

        private readonly IDictionary<K, V> _data = new Dictionary<K, V>();

        #endregion

        #region Private Methods

        private bool IsAddable(V item)
        {
            if (item is ValueType) {
                var underlyingType = Nullable.GetUnderlyingType(typeof(V));
                if (underlyingType != null) {
                    return item != null;
                }

                return true;
            }

            return item != null;
        }

        #endregion

        #pragma warning disable 1591
        #region Overrides

        public override string ToString()
        {
            return Manager.GetObjectMapper().WriteValueAsString(this);
        }

        #endregion

        #region IEnumerable

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        #endregion

        #region IDictionary

        public void Add(K key, V value)
        {
            if (IsAddable(value)) {
                _data.Add(key, value);
            }
        }

        public bool ContainsKey(K key)
        {
            return _data.ContainsKey(key);
        }

        public bool Remove(K key)
        {
            return _data.Remove(key);
        }

        public bool TryGetValue(K key, out V value)
        {
            return _data.TryGetValue(key, out value);
        }

        public V this[K index]
        {
            get {
                return _data[index];
            }
            set {
                if (IsAddable(value)) {
                    _data[index] = value;
                }
            }
        }

        public ICollection<K> Keys
        {
            get {
                return _data.Keys;
            }
        }

        public ICollection<V> Values
        {
            get {
                return _data.Values;
            }
        }

        public void Add(KeyValuePair<K, V> item)
        {
            if (IsAddable(item.Value)) {
                _data.Add(item);
            }
        }

        public void Clear()
        {
            _data.Clear();
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            return _data.Contains(item);
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            _data.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            return _data.Remove(item);
        }

        public int Count
        {
            get {
                return _data.Count;
            }
        }

        public bool IsReadOnly
        {
            get {
                return _data.IsReadOnly;
            }
        }

        #endregion
        #pragma warning restore 1591

    }
}

