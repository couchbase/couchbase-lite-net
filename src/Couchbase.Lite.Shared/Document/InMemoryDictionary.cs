// 
//  InMemoryDictionary.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed class InMemoryDictionary : IMutableDictionary, IFLEncodable
    {
        #region Constants

        private const string Tag = nameof(InMemoryDictionary);

        #endregion

        #region Variables

        private IDictionary<string, object> _dict;

        #endregion

        #region Properties

        public bool HasChanges { get; private set; }

        IFragment IDictionaryFragment.this[string key] => new Fragment(this, key);

        public int Count => _dict.Count;

        public ICollection<string> Keys => _dict.Keys;

        public IMutableFragment this[string key] => new Fragment(this, key);

        #endregion

        #region Constructors

        public InMemoryDictionary()
        {
            _dict = new Dictionary<string, object>();
        }

        public InMemoryDictionary(IDictionary<string, object> dictionary)
        {
            _dict = new Dictionary<string, object>(dictionary);
            HasChanges = _dict.Count > 0;
        }

        public InMemoryDictionary([NotNull]InMemoryDictionary other)
            : this(other._dict)
        {
            
        }

        #endregion

        #region Private Methods

        private void SetObject(string key, object value)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(key), key);

            value = DataOps.ToCouchbaseObject(value);
            var oldValue = _dict.Get(key);
            if (!_dict.ContainsKey(key) || (value != oldValue && value?.Equals(oldValue) != true)) {
                _dict[key] = value;
                HasChanges = true;
            }
        }

        #endregion

        #region IDictionaryObject

        public bool Contains(string key)
        {
            return _dict.ContainsKey(key);
        }

        public ArrayObject GetArray(string key)
        {
            return GetValue(key) as ArrayObject;
        }

        public Blob GetBlob(string key)
        {
             return GetValue(key) as Blob;
        }

        public bool GetBoolean(string key)
        {
            return DataOps.ConvertToBoolean(GetValue(key));
        }

        public DateTimeOffset GetDate(string key)
        {
            return DataOps.ConvertToDate(GetValue(key));
        }

        public DictionaryObject GetDictionary(string key)
        {
            return GetValue(key) as DictionaryObject;
        }

        public double GetDouble(string key)
        {
            return DataOps.ConvertToDouble(GetValue(key));
        }

        public float GetFloat(string key)
        {
            return DataOps.ConvertToFloat(GetValue(key));
        }

        public int GetInt(string key)
        {
            return DataOps.ConvertToInt(GetValue(key));
        }

        public long GetLong(string key)
        {
            return DataOps.ConvertToLong(GetValue(key));
        }

        public string GetString(string key)
        {
            return GetValue(key) as string;
        }

        public object GetValue(string key)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(key), key);

            if (!_dict.TryGetValue(key, out var obj)) {
                return null;
            }

            var cblObj = DataOps.ToCouchbaseObject(obj);
            if (cblObj != obj && cblObj?.GetType() != obj.GetType()) {
                _dict[key] = cblObj;
            }

            return cblObj;
        }

        public Dictionary<string, object> ToDictionary()
        {
            return _dict.ToDictionary(x => x.Key, x => DataOps.ToNetObject(x.Value));
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _dict.GetEnumerator();
        }

        #endregion

        #region IFLEncodable

        public unsafe void FLEncode(FLEncoder* enc)
        {
            _dict.FLEncode(enc);
        }

        #endregion

        #region IMutableDictionary

        MutableArrayObject IMutableDictionary.GetArray(string key)
        {
            return GetValue(key) as MutableArrayObject;
        }

        MutableDictionaryObject IMutableDictionary.GetDictionary(string key)
        {
            return GetValue(key) as MutableDictionaryObject;
        }

        public IMutableDictionary Remove(string key)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(key), key);

            if (_dict.ContainsKey(key)) {
                _dict.Remove(key);
                HasChanges = true;
            }

            return this;
        }

        public IMutableDictionary SetArray(string key, ArrayObject value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetBlob(string key, Blob value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetBoolean(string key, bool value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetData(IDictionary<string, object> dictionary)
        {
            _dict = dictionary.ToDictionary(x => x.Key, x => DataOps.ToCouchbaseObject(x.Value));
            HasChanges = true;
            return this;
        }

        public IMutableDictionary SetDate(string key, DateTimeOffset value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetDictionary(string key, DictionaryObject value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetDouble(string key, double value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetFloat(string key, float value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetInt(string key, int value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetLong(string key, long value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetString(string key, string value)
        {
            SetObject(key, value);
            return this;
        }

        public IMutableDictionary SetValue(string key, object value)
        {
            SetObject(key, value);
            return this;
        }

        #endregion
    }
}