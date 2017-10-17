// 
// ArrayObject.cs
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
using System.Linq;
using System.Reflection;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Support;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    internal sealed class ArrayObjectConverter : JsonConverter
    {
        #region Properties

        public override bool CanRead => false;

        public override bool CanWrite => true;

        #endregion

        #region Overrides

        public override bool CanConvert(Type objectType)
        {
            return objectType.GetTypeInfo().IsAssignableFrom(typeof(ArrayObject).GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var arr = (ArrayObject)value;
            arr.LockedForRead(() =>
            {
                writer.WriteStartArray();
                foreach (var obj in arr) {
                    serializer.Serialize(writer, obj);
                }
                writer.WriteEndArray();
            });
        }

        #endregion
    }

    /// <summary>
    /// A class representing an editable collection of objects
    /// </summary>
    [JsonConverter(typeof(ArrayObjectConverter))]
    public sealed class ArrayObject : ReadOnlyArray, IArray
    {
        #region Variables
        
        private readonly ThreadSafety _threadSafety = new ThreadSafety();
        
        private IList _list;

        #endregion

        #region Properties

        /// <inheritdoc />
        public override int Count
        {
            get {
                return _threadSafety.DoLocked(() =>
                {
                    if (_list == null) {
                        return base.Count;
                    }

                    return _list.Count;
                });
            }
        }

        /// <inheritdoc />
        public new Fragment this[int index]
        {
            get {
                var value = index >= 0 && index < Count ? GetObject(index) : null;
                return new Fragment(value, this, index);
            }
        }

        private IList List
        {
            get {
                if (_list == null) {
                    CopyFleeceData();
                }

                return _list;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public ArrayObject()
            : base(default(FleeceArray))
        {
            
        }

        /// <summary>
        /// Creates an array with the given data
        /// </summary>
        /// <param name="array">The data to populate the array with</param>
        public ArrayObject(IList array)
            : this()
        {
            Set(array);
        }

        internal ArrayObject(FleeceArray data)
            : base(data)
        {
            
        }

        #endregion

        #region Internal Methods

        internal void LockedForRead(Action a)
        {
            _threadSafety.DoLocked(a);
        }

        #endregion

        #region Private Methods

        private void CopyFleeceData()
        {
            Debug.Assert(_list == null);
            var count = base.Count;
            _list = new List<object>(count);
            for (var i = 0; i < count; i++) {
                var value = base.GetObject(i);
                _list.Add(DataOps.ConvertValue(value));
            }
        }

        private void SetValue(int index, object value)
        {
            _list[index] = value;
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override Blob GetBlob(int index)
        {
            return GetObject(index) as Blob;
        }

        /// <inheritdoc />
        public override bool GetBoolean(int index)
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    return base.GetBoolean(index);
                }

                var value = _list[index];
                return DataOps.ConvertToBoolean(value);
            });
        }

        /// <inheritdoc />
        public override DateTimeOffset GetDate(int index)
        {
            return DataOps.ConvertToDate(GetObject(index));
        }

        /// <inheritdoc />
        public override double GetDouble(int index)
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    return base.GetDouble(index);
                }

                var value = _list[index];
                return DataOps.ConvertToDouble(value);
            });
        }

        /// <inheritdoc />
        public override float GetFloat(int index)
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    return base.GetFloat(index);
                }

                var value = _list[index];
                return DataOps.ConvertToFloat(value);
            });
        }

        /// <inheritdoc />
        public override IEnumerator<object> GetEnumerator()
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    return base.GetEnumerator();
                }

                return _list.Cast<object>().GetEnumerator();
            });
        }

        /// <inheritdoc />
        public override int GetInt(int index)
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    return base.GetInt(index);
                }

                var value = _list[index];
                return DataOps.ConvertToInt(value);
            });
        }

        /// <inheritdoc />
        public override long GetLong(int index)
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    return base.GetLong(index);
                }

                var value = _list[index];
                return DataOps.ConvertToLong(value);
            });
        }

        /// <inheritdoc />
        public override object GetObject(int index)
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    var value = base.GetObject(index);
                    if (value is IReadOnlyDictionary || value is IReadOnlyArray) {
                        CopyFleeceData();
                    } else {
                        return value;
                    }
                }
                return _list[index];
            });
        }

        /// <inheritdoc />
        public override string GetString(int index)
        {
            return GetObject(index) as string;
        }

        /// <inheritdoc />
        public override IList<object> ToList()
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    CopyFleeceData();
                }

                var array = new List<object>(Count);
                foreach (var item in _list) {
                    switch (item) {
                        case IReadOnlyDictionary dict:
                            array.Add(dict.ToDictionary());
                            break;
                        case IReadOnlyArray arr:
                            array.Add(arr.ToList());
                            break;
                        default:
                            array.Add(item);
                            break;
                    }
                }

                return array;
            });
        }

        #endregion

        #region IArray

        /// <inheritdoc />
        public IArray Add(object value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(DataOps.ConvertValue(value));
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(string value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(int value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(long value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(float value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(double value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(bool value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(Blob value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(DateTimeOffset value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value.ToString("o"));
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(ArrayObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Add(DictionaryObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public new IArray GetArray(int index)
        {
            return GetObject(index) as IArray;
        }

        /// <inheritdoc />
        public new IDictionaryObject GetDictionary(int index)
        {
            return GetObject(index) as IDictionaryObject;
        }

        /// <inheritdoc />
        public IArray Insert(int index, object value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, DataOps.ConvertValue(value));
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, string value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, int value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, long value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, float value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, double value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, bool value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, Blob value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, DateTimeOffset value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value.ToString("o"));
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, ArrayObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Insert(int index, DictionaryObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                List.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray RemoveAt(int index)
        {
            return _threadSafety.DoLocked(() =>
            {
                if (_list == null) {
                    CopyFleeceData();
                }
                
                _list.RemoveAt(index);
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(IList array)
        {
            return _threadSafety.DoLocked(() =>
            {
                var result = new List<object>();
                foreach (var item in array) {
                    result.Add(DataOps.ConvertValue(item));
                }

                _list = result;
                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, object value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value?.Equals(oldValue) == false) {
                    value = DataOps.ConvertValue(value);
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, string value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value?.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, int value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, long value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, float value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, double value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, bool value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, Blob value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value?.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, DateTimeOffset value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                var newValue = value.ToString("o");
                if (newValue.Equals(oldValue) == false) {
                    SetValue(index, newValue);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, ArrayObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value?.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IArray Set(int index, DictionaryObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                var oldValue = List[index];
                if (value?.Equals(oldValue) == false) {
                    SetValue(index, value);
                }

                return this;
            });
        }

        #endregion
    }
}
