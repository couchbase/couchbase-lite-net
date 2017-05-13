// 
// ReadOnlyDictionary.cs
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
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Support;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a key-value collection that is read only
    /// </summary>
    public unsafe class ReadOnlyDictionary : IReadOnlyDictionary
    {
        #region Variables

        internal readonly ThreadSafety _threadSafety = new ThreadSafety();
        private readonly FLDict* _dict;
        private readonly SharedStringCache _sharedKeys;

        #endregion

        #region Properties
#pragma warning disable 1591

        public virtual int Count => (int)Native.FLDict_Count(_dict);

        public ReadOnlyFragment this[string key] => new ReadOnlyFragment(GetObject(key));

        public virtual ICollection<string> Keys
        {
            get {
                var keys = new List<string>();
                if (_dict != null) {
                    FLDictIterator iter;
                    Native.FLDictIterator_Begin(_dict, &iter);
                    string key;
                    while (null != (key = _sharedKeys.GetDictIterKey(&iter))) {
                        keys.Add(key);
                        Native.FLDictIterator_Next(&iter);
                    }
                }

                return keys;
            }
        }

#pragma warning restore 1591

        internal FleeceDictionary Data { get; set; }

        internal virtual bool IsEmpty => Count == 0;

        #endregion

        #region Constructors

        internal ReadOnlyDictionary(FleeceDictionary data)
        {
            Data = data;
            _dict = data != null ? data.Dict : null;
            _sharedKeys = data?.Database?.SharedStrings;
        }

        #endregion

        private FLValue* FleeceValueForKey(string key)
        {
            if (_sharedKeys == null) {
                return null;
            }

            return _sharedKeys.GetDictValue(_dict, key);
        }

        private object FleeceValueToObject(string key)
        {
            var value = FleeceValueForKey(key);
            if (value != null) {
                var c4Doc = Data != null ? Data.C4Doc : null;
                return FLValueConverter.ToCouchbaseObject(value, _sharedKeys, c4Doc, Data?.Database);
            }

            return null;
        }

#pragma warning disable 1591
        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        public virtual IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        #endregion

        #region IReadOnlyDictionary

        public virtual bool Contains(string key)
        {
            var type = Native.FLValue_GetType(FleeceValueForKey(key));
            return type != FLValueType.Undefined;
        }

        public IReadOnlyArray GetArray(string key)
        {
            return FleeceValueToObject(key) as IReadOnlyArray;
        }

        public virtual Blob GetBlob(string key)
        {
            return FleeceValueToObject(key) as Blob;
        }

        public virtual bool GetBoolean(string key)
        {
            return Native.FLValue_AsBool(FleeceValueForKey(key));
        }

        public virtual DateTimeOffset GetDate(string key)
        {
            return DataOps.ConvertToDate(FleeceValueToObject(key));
        }

        public virtual double GetDouble(string key)
        {
            return Native.FLValue_AsDouble(FleeceValueForKey(key));
        }

        public virtual int GetInt(string key)
        {
            return (int)Native.FLValue_AsInt(FleeceValueForKey(key));
        }

        public virtual long GetLong(string key)
        {
            return Native.FLValue_AsInt(FleeceValueForKey(key));
        }

        public virtual object GetObject(string key)
        {
            return FleeceValueToObject(key);
        }

        public virtual string GetString(string key)
        {
            return FleeceValueToObject(key) as string;
        }

        public IReadOnlyDictionary GetDictionary(string key)
        {
            return FleeceValueToObject(key) as IReadOnlyDictionary;
        }

        public virtual IDictionary<string, object> ToDictionary()
        {
            if (Count == 0) {
                return new Dictionary<string, object>();
            }

            var dict = new Dictionary<string, object>();
            foreach (var pair in this) {
                switch(pair.Value) {
                    case IReadOnlyDictionary d:
                        dict[pair.Key] = d.ToDictionary();
                        break;
                    case IReadOnlyArray a:
                        dict[pair.Key] = a.ToList();
                        break;
                    default:
                        dict[pair.Key] = pair.Value;
                        break;
                }
            }

            return dict;
        }

        #endregion
#pragma warning restore 1591

        private class Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            #region Variables

            private readonly ReadOnlyDictionary _parent;
            private bool _first;
            private FLDictIterator _iter;

            #endregion

            #region Properties

            public KeyValuePair<string, object> Current
            {
                get {
                    fixed (FLDictIterator* i = &_iter) {
                        var key = _parent._sharedKeys.GetDictIterKey(i);
                        var value = _parent.GetObject(key);
                        return new KeyValuePair<string, object>(key, value);
                    }
                }
            }

            object IEnumerator.Current => Current;

            #endregion

            #region Constructors

            public Enumerator(ReadOnlyDictionary parent)
            {
                _parent = parent;
                _first = true;
            }

            #endregion

            #region IDisposable

            public void Dispose()
            {
                // No-op
            }

            #endregion

            #region IEnumerator

            public bool MoveNext()
            {
                if (_first) {
                    if (_parent._dict == null) {
                        return false;
                    }

                    _first = false;
                    fixed (FLDictIterator* i = &_iter) {
                        Native.FLDictIterator_Begin(_parent._dict, i);
                    }

                    return true;
                }

                fixed (FLDictIterator* i = &_iter) {
                    return Native.FLDictIterator_Next(i);
                }
            }

            public void Reset()
            {
                _first = true;
            }

            #endregion
        }

    }
}
