// 
// NonNullDictionary.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Lite.Util
{
    internal sealed class CollectionDebuggerView<TKey, TValue>
    {
        #region Variables

        private readonly ICollection<KeyValuePair<TKey, TValue>> _c;

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<TKey, TValue>[] Items
        {
            get {
                var o = new KeyValuePair<TKey, TValue>[_c.Count];
                _c.CopyTo(o, 0);
                return o;
            }
        }

        #endregion

        #region Constructors

        public CollectionDebuggerView(ICollection<KeyValuePair<TKey, TValue>> col)
        {
            _c = col;
        }

        #endregion
    }

    /// <summary>
    /// A dictionary that ignores any attempts to insert a null object into it.
    /// Usefor for creating JSON objects that should not contain null values
    /// </summary>
    // ReSharper disable UseNameofExpression
    [DebuggerDisplay("Count={Count}")]
    // ReSharper restore UseNameofExpression
    [DebuggerTypeProxy(typeof(CollectionDebuggerView<,>))]
    internal sealed class NonNullDictionary<TK, TV> : IDictionary<TK, TV>, IReadOnlyDictionary<TK, TV>
    {
        #region Variables

        private readonly IDictionary<TK, TV> _data = new Dictionary<TK, TV>();

        #endregion

        #region Properties

        /// <inheritdoc />
        public int Count => _data.Count;

        /// <inheritdoc />
        public bool IsReadOnly => _data.IsReadOnly;

        /// <inheritdoc />
        public TV this[TK index]
        {
            get => _data[index];
            set {
                if(IsAddable(value)) {
                    _data[index] = value;
                }
            }
        }

        /// <inheritdoc />
        public ICollection<TK> Keys => _data.Keys;

        /// <inheritdoc />
        public ICollection<TV> Values => _data.Values;

        /// <inheritdoc />
        IEnumerable<TK> IReadOnlyDictionary<TK, TV>.Keys => _data.Keys;

        /// <inheritdoc />
        IEnumerable<TV> IReadOnlyDictionary<TK, TV>.Values => _data.Values;

        #endregion

        #region Private Methods

        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse", Justification = "item is a Nullable type during the block")]
        private static bool IsAddable(TV item)
        {
            if (!(item is ValueType)) {
                return item != null;
            }

            var underlyingType = Nullable.GetUnderlyingType(typeof(TV));
            if(underlyingType != null) {
                return item != null;
            }

            return true;
        }

        #endregion

        #region ICollection<KeyValuePair<TK,TV>>

        /// <inheritdoc />
        public void Add(KeyValuePair<TK, TV> item)
        {
            if(IsAddable(item.Value)) {
                _data.Add(item);
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            _data.Clear();
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<TK, TV> item)
        {
            return _data.Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
        {
            _data.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<TK, TV> item)
        {
            return _data.Remove(item);
        }

        #endregion

        #region IDictionary<TK,TV>

        /// <inheritdoc />
        public void Add(TK key, TV value)
        {
            if(IsAddable(value)) {
                _data.Add(key, value);
            }
        }

        /// <inheritdoc />
        public bool ContainsKey(TK key)
        {
            return _data.ContainsKey(key);
        }

        /// <inheritdoc />
        public bool Remove(TK key)
        {
            return _data.Remove(key);
        }

        /// <inheritdoc />
        public bool TryGetValue(TK key, out TV value)
        {
            return _data.TryGetValue(key, out value);
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        #endregion

        #region IEnumerable<KeyValuePair<TK,TV>>

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        #endregion
    }
}
