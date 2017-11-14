// 
// MutableDictionary.cs
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
using System.Collections.Generic;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a writeable string to object dictionary
    /// </summary>
    public sealed class MutableDictionary : DictionaryObject, IMutableDictionary
    {
        #region Constants

        internal static readonly object RemovedValue = new object();

        #endregion

        #region Properties

        /// <inheritdoc />
        public new MutableFragment this[string key] => new MutableFragment(this, key);

        internal bool HasChanges => _dict.IsMutated;

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public MutableDictionary()
        {
            
        }

        /// <summary>
        /// Creates a dictionary given the initial set of keys and values
        /// from an existing dictionary
        /// </summary>
        /// <param name="dict">The dictionary to copy the keys and values from</param>
        public MutableDictionary(IDictionary<string, object> dict)
        {
            Set(dict);
        }

        internal MutableDictionary(MDict dict, bool isMutable)
        {
            _dict.InitAsCopyOf(dict, isMutable);
        }

        internal MutableDictionary(MValue mv, MCollection parent)
            : base(mv, parent)
        {
            
        }

        #endregion

        private void SetValue(string key, object value)
        {
            _threadSafety.DoLocked(() =>
            {
                var oldValue = _dict.Get(key);
                if (value != null) {
                    value = DataOps.ToCouchbaseObject(value);
                    if (DataOps.ValueWouldChange(value, oldValue, _dict)) {
                        _dict.Set(key, new MValue(value));
                        KeysChanged();
                    }
                } else {
                    if (!oldValue.IsEmpty) {
                        _dict.Remove(key);
                        KeysChanged();
                    }
                }
            });
        }

        #region IMutableDictionary

        /// <inheritdoc />
        public new IMutableArray GetArray(string key)
        {
            return base.GetArray(key) as IMutableArray;
        }

        /// <inheritdoc />
        public new IMutableDictionary GetDictionary(string key)
        {
            return base.GetDictionary(key) as IMutableDictionary;
        }

        /// <inheritdoc />
        public IMutableDictionary Remove(string key)
        {
            _threadSafety.DoLocked(() => _dict.Remove(key));
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, object value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(IDictionary<string, object> dictionary)
        {
            _threadSafety.DoLocked(() =>
            {
                _dict.Clear();
                foreach (var item in dictionary) {
                    _dict.Set(item.Key, new MValue(DataOps.ToCouchbaseObject(item.Value)));
                }

                KeysChanged();
            });

            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, string value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, int value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, long value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, float value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, double value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, bool value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, Blob value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, DateTimeOffset value)
        {
            SetValue(key, value.ToString("o"));
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, MutableArray value)
        {
            SetValue(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary Set(string key, MutableDictionary value)
        {
            SetValue(key, value);
            return this;
        }

        #endregion
    }
}
