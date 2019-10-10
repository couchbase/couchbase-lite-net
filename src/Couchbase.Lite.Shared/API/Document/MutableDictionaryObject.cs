﻿// 
//  MutableDictionaryObject.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using Couchbase.Lite.Fleece;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a writeable string to object dictionary
    /// </summary>
    public sealed class MutableDictionaryObject : DictionaryObject, IMutableDictionary
    {
        #region Properties

        internal bool HasChanges => _dict.IsMutated;

        /// <summary>
        /// Gets the fragment value for the given key in this dictionary
        /// </summary>
        /// <param name="key">The key whose value to retrieve</param>
        /// <returns>The fragment representing the value at the given key</returns>
        public new IMutableFragment this[string key] => new Fragment(this, key);

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public MutableDictionaryObject()
        {
            
        }

        /// <summary>
        /// Creates a dictionary given the initial set of keys and values
        /// from an existing dictionary
        /// </summary>
        /// <param name="dict">The dictionary to copy the keys and values from</param>
        public MutableDictionaryObject(IDictionary<string, object> dict)
        {
            SetData(dict);
        }

        internal MutableDictionaryObject(FleeceMutableDictionary dict, bool isMutable)
        {
            _dict.InitAsCopyOf(dict, isMutable);
        }

        internal MutableDictionaryObject(MValue mv, MCollection parent)
            : base(mv, parent)
        {
            
        }

        #endregion

        #region Private Methods

        private void SetValueInternal([NotNull]string key, object value)
        {
            _threadSafety.DoLocked(() =>
            {
                var oldValue = _dict.Get(key);
                value = DataOps.ToCouchbaseObject(value);
                if (DataOps.ValueWouldChange(value, oldValue, _dict)) {
                    _dict.Set(key, new MValue(value));
                    KeysChanged();
                }
            });
        }

        #endregion

        #region Overrides

        internal override DictionaryObject ToImmutable()
        {
            return new DictionaryObject(_dict, false);
        }

        #endregion

        #region IMutableDictionary

        /// <inheritdoc />
        public new MutableArrayObject GetArray(string key)
        {
            return base.GetArray(key) as MutableArrayObject;
        }

        /// <inheritdoc />
        public new MutableDictionaryObject GetDictionary(string key)
        {
            return base.GetDictionary(key) as MutableDictionaryObject;
        }

        /// <inheritdoc />
        public IMutableDictionary Remove(string key)
        {
            _threadSafety.DoLocked(() => _dict.Remove(key));
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetValue(string key, object value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetData(IDictionary<string, object> dictionary)
        {
            _threadSafety.DoLocked(() =>
            {
                _dict.Clear();
                if (dictionary != null) {
                    foreach (var item in dictionary) {
                        _dict.Set(item.Key, new MValue(DataOps.ToCouchbaseObject(item.Value)));
                    }
                }

                KeysChanged();
            });

            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetString(string key, string value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetInt(string key, int value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetLong(string key, long value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetFloat(string key, float value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetDouble(string key, double value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetBoolean(string key, bool value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetBlob(string key, Blob value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetDate(string key, DateTimeOffset value)
        {
            SetValueInternal(key, value.ToString("o"));
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetArray(string key, ArrayObject value)
        {
            SetValueInternal(key, value);
            return this;
        }

        /// <inheritdoc />
        public IMutableDictionary SetDictionary(string key, DictionaryObject value)
        {
            SetValueInternal(key, value);
            return this;
        }

        #endregion
    }
}
