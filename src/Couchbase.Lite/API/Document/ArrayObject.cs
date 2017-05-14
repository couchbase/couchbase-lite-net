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
using System.Globalization;
using System.Linq;
using System.Reflection;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    internal sealed class ArrayObjectConverter : JsonConverter
    {
        public override bool CanRead => false;

        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var arr = (ArrayObject)value;
            writer.WriteStartArray();
            foreach(var obj in arr) {
                serializer.Serialize(writer, obj);
            }
            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.GetTypeInfo().IsAssignableFrom(typeof(ArrayObject).GetTypeInfo());
        }
    }

    /// <summary>
    /// A class representing an editable collection of objects
    /// </summary>
    [JsonConverter(typeof(ArrayObjectConverter))]
    public sealed class ArrayObject : ReadOnlyArray, IArray
    {
        #region Variables

        internal event EventHandler<ObjectChangedEventArgs<ArrayObject>> Changed;
        private bool _changed;
        private IList _list;

        #endregion

        #region Properties

        /// <inheritdoc />
        public override int Count => _list.Count;

        /// <inheritdoc />
        public new Fragment this[int index]
        {
            get {
                var value = index >= 0 && index < Count ? GetObject(index) : null;
                return new Fragment(value, this, index);
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public ArrayObject()
            : this(default(FleeceArray))
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
            _list = new List<object>();
            LoadBackingData();
        }

        #endregion

        #region Private Methods

        private void LoadBackingData()
        {
            var count = base.Count;
            for (int i = 0; i < count; i++) {
                var value = base.GetObject(i);
                _list.Add(DataOps.ConvertValue(value, ObjectChanged, ObjectChanged));
            }
        }

        private void ObjectChanged(object sender, ObjectChangedEventArgs<DictionaryObject> args)
        {
            SetChanged();
        }

        private void ObjectChanged(object sender, ObjectChangedEventArgs<ArrayObject> args)
        {
            SetChanged();
        }

        private void RemoveAllChangedListeners()
        {
            foreach (var obj in _list) {
                RemoveChangedListener(obj);
            }
        }

        private void RemoveChangedListener(object value)
        {
            switch (value) {
                case DictionaryObject subdoc:
                    subdoc.Changed -= ObjectChanged;
                    break;
                case ArrayObject array:
                    array.Changed -= ObjectChanged;
                    break;
            }
        }

        private void SetChanged()
        {
            if (!_changed) {
                _changed = true;
                Changed?.Invoke(this, new ObjectChangedEventArgs<ArrayObject>(this));
            }
        }

        private void SetValue(int index, object value, bool isChange)
        {
            _list[index] = value;
            if (isChange) {
                SetChanged();
            }
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override IEnumerator<object> GetEnumerator()
        {
            return _list.Cast<object>().GetEnumerator();
        }

        /// <inheritdoc />
        public override Blob GetBlob(int index)
        {
            return _list[index] as Blob;
        }

        /// <inheritdoc />
        public override bool GetBoolean(int index)
        {
            var value = _list[index];
            return DataOps.ConvertToBoolean(value);
        }

        /// <inheritdoc />
        public override DateTimeOffset GetDate(int index)
        {
            var value = _list[index];
            return DataOps.ConvertToDate(value);
        }

        /// <inheritdoc />
        public override double GetDouble(int index)
        {
            var value = _list[index];
            return DataOps.ConvertToDouble(value);
        }

        /// <inheritdoc />
        public override int GetInt(int index)
        {
            var value = _list[index];
            return DataOps.ConvertToInt(value);
        }

        /// <inheritdoc />
        public override long GetLong(int index)
        {
            var value = _list[index];
            return DataOps.ConvertToLong(value);
        }

        /// <inheritdoc />
        public override object GetObject(int index)
        {
            return _list[index];
        }

        /// <inheritdoc />
        public override string GetString(int index)
        {
            return _list[index] as string;
        }

        /// <inheritdoc />
        public override IList<object> ToList()
        {
            var array = new List<object>();
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
        }

        #endregion

        #region IArray

        /// <inheritdoc />
        public IArray Add(object value)
        {
            _list.Add(DataOps.ConvertValue(value, ObjectChanged, ObjectChanged));
            SetChanged();
            return this;
        }

        /// <inheritdoc />
        public new IArray GetArray(int index)
        {
            return _list[index] as IArray;
        }

        /// <inheritdoc />
        public new IDictionaryObject GetDictionary(int index)
        {
            return _list[index] as IDictionaryObject;
        }

        /// <inheritdoc />
        public IArray Insert(int index, object value)
        {
            _list.Insert(index, DataOps.ConvertValue(value, ObjectChanged, ObjectChanged));
            SetChanged();
            return this;
        }

        /// <inheritdoc />
        public IArray RemoveAt(int index)
        {
            var value = _list[index];
            RemoveChangedListener(value);
            _list.RemoveAt(index);
            SetChanged();
            return this;
        }

        /// <inheritdoc />
        public IArray Set(IList array)
        {
            RemoveAllChangedListeners();

            var result = new List<object>();
            foreach (var item in array) {
                result.Add(DataOps.ConvertValue(item, ObjectChanged, ObjectChanged));
            }

            _list = result;
            SetChanged();
            return this;
        }

        /// <inheritdoc />
        public IArray Set(int index, object value)
        {
            var oldValue = _list[index];
            if (value?.Equals(oldValue) == false) {
                value = DataOps.ConvertValue(value, ObjectChanged, ObjectChanged);
                RemoveChangedListener(oldValue);
                SetValue(index, value, true);
            }

            return this;
        }

        #endregion

    }
}
