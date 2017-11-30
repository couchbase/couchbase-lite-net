// 
// OptionsDictionary.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// An abstract base class for options dictionaries.  These dictionaries are simply
    /// dictionaries of <see cref="String"/> and <see cref="Object"/> but they provide 
    /// safe accessors to get the data without having to know the keys they are stored
    /// under
    /// </summary>
    public abstract class OptionsDictionary : IDictionary<string, object>
    {
        #region Variables

        [NotNull]
        private readonly Dictionary<string, object> _inner = new Dictionary<string, object>();
        private bool _readonly;

        #endregion

        #region Properties

        /// <inheritdoc />
        public int Count => _inner.Count;

        /// <inheritdoc />
        public bool IsReadOnly => _readonly;

        /// <inheritdoc />
        public object this[string key]
        {
            get => _inner[key];
            set {
                if (_readonly) {
                    throw new InvalidOperationException("Cannot modify this dictionary once it is in use");
                }

                if (!Validate(key, value)) {
                    throw new InvalidOperationException($"Invalid value {value} for key '{key}'");
                }

                _inner[key] = value;
            }
        }

        /// <inheritdoc />
        public ICollection<string> Keys => _inner.Keys;

        /// <inheritdoc />
        public ICollection<object> Values => _inner.Values;

        #endregion

        #region Constructors

        internal OptionsDictionary()
        {

        }

        internal OptionsDictionary(Dictionary<string, object> raw)
        {
            if(raw != null) {
                _inner = raw;
            }
        }

        #endregion

        #region Internal Methods

        internal void Freeze()
        {
            FreezeInternal();
            _readonly = true;
        }

        internal virtual void FreezeInternal()
        { }

        internal virtual bool KeyIsRequired(string key)
        {
            return false;
        }

        internal virtual bool Validate(string key, object value)
        {
            return true;
        }

        #endregion

        #region ICollection<KeyValuePair<string,object>>

        /// <inheritdoc />
        public void Add(KeyValuePair<string, object> item)
        {
            if (_readonly) {
                throw new InvalidOperationException("Cannot modify this dictionary once it is in use");
            }

            if (!Validate(item.Key, item.Value)) {
                throw new InvalidOperationException($"Invalid value {item.Value} for key '{item.Key}'");
            }

            ((ICollection<KeyValuePair<string, object>>)_inner).Add(item);
        }

        /// <inheritdoc />
        public void Clear()
        {
            if (_readonly) {
                throw new InvalidOperationException("Cannot modify this dictionary once it is in use");
            }

            _inner.Clear();
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<string, object> item)
        {
            return ((ICollection<KeyValuePair<string, object>>)_inner).Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, object>>)_inner).CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<string, object> item)
        {
            if (_readonly) {
                throw new InvalidOperationException("Cannot modify this dictionary once it is in use");
            }

            if (KeyIsRequired(item.Key)) {
                throw new InvalidOperationException($"Cannot remove the required key '{item.Key}'");
            }

            return ((ICollection<KeyValuePair<string, object>>)_inner).Remove(item);
        }

        #endregion

        #region IDictionary<string,object>

        /// <inheritdoc />
        public void Add(string key, object value)
        {
            if (_readonly) {
                throw new InvalidOperationException("Cannot modify this dictionary once it is in use");
            }

            if (!Validate(key, value)) {
                throw new InvalidOperationException($"Invalid value {value} for key '{key}'");
            }

            _inner.Add(key, value);
        }

        /// <inheritdoc />
        public bool ContainsKey(string key)
        {
            return _inner.ContainsKey(key);
        }

        /// <inheritdoc />
        public bool Remove(string key)
        {
            if (_readonly) {
                throw new InvalidOperationException("Cannot modify this dictionary once it is in use");
            }

            if (KeyIsRequired(key)) {
                throw new InvalidOperationException($"Cannot remove the required key '{key}'");
            }

            return _inner.Remove(key);
        }

        /// <inheritdoc />
        public bool TryGetValue(string key, out object value)
        {
            return _inner.TryGetValue(key, out value);
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        #endregion
    }
}
