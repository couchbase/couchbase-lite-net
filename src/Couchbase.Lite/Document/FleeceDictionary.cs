// 
// FleeceDictionary.cs
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
using System.Globalization;

using Couchbase.Lite.Internal.DB;
using Couchbase.Lite.Internal.Serialization;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class FleeceDictionary : IReadOnlyDictionary
    {
        #region Variables

        private readonly Database _database;
        private readonly FLDict* _dict;
        private readonly C4Document* _document;
        private readonly SharedStringCache _sharedKeys;

        #endregion

        #region Properties

        public int Count => (int)Native.FLDict_Count(_dict);

        public IReadOnlyFragment this[string key] => new ReadOnlyFragment(GetObject(key));

        public ICollection<string> Keys
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

        #endregion

        #region Constructors

        public FleeceDictionary()
        {
            
        }

        public FleeceDictionary(FLDict* dict, C4Document* document, IDatabase database)
        {
            var db = database as Database ?? throw new InvalidOperationException("Custom IDatabase not supported");
            _dict = dict;
            _document = document;
            _database = db;
            _sharedKeys = _database.SharedStrings;
        }

        #endregion

        #region Private Methods

        private FLValue* FleeceValueForKey(string key)
        {
            return _sharedKeys.GetDictValue(_dict, key);
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
            return new Enumerator(this);
        }

        #endregion

        #region IReadOnlyDictionary

        public bool Contains(string key)
        {
            var type = Native.FLValue_GetType(FleeceValueForKey(key));
            return type != FLValueType.Undefined;
        }

        public IReadOnlyArray GetArray(string key)
        {
            return GetObject(key) as IReadOnlyArray;
        }

        public IBlob GetBlob(string key)
        {
            return GetObject(key) as IBlob;
        }

        public bool GetBoolean(string key)
        {
            return Native.FLValue_AsBool(FleeceValueForKey(key));
        }

        public DateTimeOffset GetDate(string key)
        {
            var dateString = GetString(key);
            if (dateString == null) {
                return DateTimeOffset.MinValue;
            }

            return DateTimeOffset.ParseExact(dateString, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public double GetDouble(string key)
        {
            return Native.FLValue_AsDouble(FleeceValueForKey(key));
        }

        public int GetInt(string key)
        {
            return (int) GetLong(key);
        }

        public long GetLong(string key)
        {
            return Native.FLValue_AsInt(FleeceValueForKey(key));
        }

        public object GetObject(string key)
        {
            return FLValueConverter.ToCouchbaseObject(FleeceValueForKey(key), _sharedKeys, _document, _database);
        }

        public string GetString(string key)
        {
            return Native.FLValue_AsString(FleeceValueForKey(key));
        }

        public IReadOnlySubdocument GetSubdocument(string key)
        {
            return GetObject(key) as IReadOnlySubdocument;
        }

        public IDictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>((int)Native.FLDict_Count(_dict));
            FLDictIterator iter;
            Native.FLDictIterator_Begin(_dict, &iter);
            string key;
            while (null != (key = _sharedKeys.GetDictIterKey(&iter))) {
                var value = FleeceValueForKey(key);
                var typedObject = FLValueConverter.ToTypedObject(value, _sharedKeys, _database);
                if (typedObject != null) {
                    dict[key] = typedObject;
                } else {
                    dict[key] = FLValueConverter.ToObject(value, _sharedKeys);
                }

                Native.FLDictIterator_Next(&iter);
            }

            return dict;
        }

        #endregion

        #region Nested

        private class Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            #region Variables

            private readonly FleeceDictionary _parent;
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

            public Enumerator(FleeceDictionary parent)
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

        #endregion
    }
}
