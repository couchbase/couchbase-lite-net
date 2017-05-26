// 
// DictionaryObject.cs
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
using System.Linq;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    internal sealed class ObjectChangedEventArgs<T> : EventArgs
    {
        #region Properties

        public T ChangedObject { get; }

        #endregion

        #region Constructors

        internal ObjectChangedEventArgs(T changedObject)
        {
            ChangedObject = changedObject;
        }

        #endregion
    }

    internal sealed class DictionaryObjectConverter : JsonConverter
    {
        #region Properties

        public override bool CanRead => false;

        public override bool CanWrite => true;

        #endregion

        #region Overrides

        public override bool CanConvert(Type objectType)
        {
            return typeof(DictionaryObject) == objectType;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dict = (IDictionaryObject)value;
            writer.WriteStartObject();
            foreach (var pair in dict) {
                if (Object.ReferenceEquals(pair.Value, DictionaryObject.RemovedValue)) {
                    continue;
                }

                writer.WritePropertyName(pair.Key);
                serializer.Serialize(writer, pair.Value);
            }
            writer.WriteEndObject();
        }

        #endregion
    }

    /// <summary>
    /// A class representing a writeable string to object dictionary
    /// </summary>
    [JsonConverter(typeof(DictionaryObjectConverter))]
    public sealed class DictionaryObject : ReadOnlyDictionary, IDictionaryObject
    {
        #region Constants

        internal static readonly object RemovedValue = new object();

        #endregion

        #region Variables

        internal event EventHandler<ObjectChangedEventArgs<DictionaryObject>> Changed;
        private Dictionary<string, object> _dict = new Dictionary<string, object>();

        #endregion

        #region Properties

        /// <inheritdoc />
        public override int Count
        {
            get {
                var count = _dict.Count;
                foreach(var key in Keys) {
                    if(!_dict.ContainsKey(key)) {
                        count += 1;
                    }
                }

                foreach(var val in _dict.Values) {
                    if(Object.ReferenceEquals(val, RemovedValue)) {
                        count -= 1;
                    }
                }

                return count;
            }
        }

        /// <inheritdoc />
        public new Fragment this[string key]
        {
            get {
                var value = GetObject(key);
                return new Fragment(value, this, key);
            }
        }

        /// <inheritdoc />
        public override ICollection<string> Keys
        {
            get {
                var result = new HashSet<string>();
                foreach (var key in base.Keys) {
                    result.Add(key);
                }

                foreach (var pair in _dict) {
                    if (!ReferenceEquals(pair.Value, RemovedValue)) {
                        result.Add(pair.Key);
                    }
                }

                return result;
            }
        }

        internal bool HasChanges
        {
            get; private set;
        }

        internal override bool IsEmpty => _dict.All(x => ReferenceEquals(x.Value, RemovedValue)) && base.IsEmpty;

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public DictionaryObject()
            : this(default(FleeceDictionary))
        {
            
        }

        /// <summary>
        /// Creates a dictionary given the initial set of keys and values
        /// from an existing dictionary
        /// </summary>
        /// <param name="dict">The dictionary to copy the keys and values from</param>
        public DictionaryObject(IDictionary<string, object> dict)
            : this(default(FleeceDictionary))
        {
            Set(dict);
        }

        internal DictionaryObject(FleeceDictionary data)
            : base(data)
        {

        }

        #endregion

        #region Private Methods

        private IEnumerator<KeyValuePair<string, object>> GetGenerator()
        {
            foreach (var key in Keys) {
                yield return new KeyValuePair<string, object>(key, GetObject(key));
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
            foreach(var val in _dict.Values) {
                RemoveChangedListener(val);
            }
        }

        private void RemoveChangedListener(object value)
        {
            switch(value) {
                case DictionaryObject dict:
                    dict.Changed -= ObjectChanged;
                    break;
                case ArrayObject arr:
                    arr.Changed -= ObjectChanged;
                    break;
            }
        }

        private void SetChanged()
        {
            if(!HasChanges) {
                HasChanges = true;
                Changed?.Invoke(this, new ObjectChangedEventArgs<DictionaryObject>(this));
            }
        }

        private void SetValue(string key, object value, bool isChange)
        {
            _dict[key] = value;
            if(isChange) {
                SetChanged();
            }
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override bool Contains(string key)
        {
            return (_dict.ContainsKey(key) && !ReferenceEquals(_dict[key], RemovedValue)) || base.Contains(key);
        }

        /// <inheritdoc />
        public override Blob GetBlob(string key)
        {
            object value;
            if (!_dict.TryGetValue(key, out value)) {
                return base.GetBlob(key);
            }

            return value as Blob;
        }

        /// <inheritdoc />
        public override bool GetBoolean(string key)
        {
            if(!_dict.ContainsKey(key)) {
                return base.GetBoolean(key);
            }

            var value = _dict[key];
            return DataOps.ConvertToBoolean(value);
        }

        /// <inheritdoc />
        public override DateTimeOffset GetDate(string key)
        {
            if (!_dict.ContainsKey(key)) {
                return base.GetDate(key);
            }

            return DataOps.ConvertToDate(_dict[key]);
        }

        /// <inheritdoc />
        public override double GetDouble(string key)
        {
            if (!_dict.ContainsKey(key)) {
                return base.GetDouble(key);
            }

            var value = _dict[key];
            return DataOps.ConvertToDouble(value);
        }

        /// <inheritdoc />
        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            if (!HasChanges) {
                return base.GetEnumerator();
            }

            return GetGenerator();
        }

        /// <inheritdoc />
        public override int GetInt(string key)
        {
            if(!_dict.ContainsKey(key)) {
                return base.GetInt(key);
            }

            var value = _dict[key];
            return DataOps.ConvertToInt(value);
        }

        /// <inheritdoc />
        public override long GetLong(string key)
        {
            if (!_dict.ContainsKey(key)) {
                return base.GetLong(key);
            }

            var value = _dict[key];
            return DataOps.ConvertToLong(value);
        }

        /// <inheritdoc />
        public override object GetObject(string key)
        {
            object value = null;
            if(!_dict.TryGetValue(key, out value)) {
                value = base.GetObject(key);
                switch(value) {
                    case ReadOnlyDictionary sub:
                        value = DataOps.ConvertRODictionary(sub, ObjectChanged);
                        SetValue(key, value, false);
                        break;
                    case ReadOnlyArray arr:
                        value = DataOps.ConvertROArray(arr, ObjectChanged);
                        SetValue(key, value, false);
                        break;
                }
            } else if (Object.ReferenceEquals(value, RemovedValue)) {
                value = null;
            }

            return value;
        }

        /// <inheritdoc />
        public override string GetString(string key)
        {
            object value;
            if(!_dict.TryGetValue(key, out value)) {
                return base.GetString(key);
            }

            if (value is DateTimeOffset dto) {
                return dto.ToString("o");
            }

            return value as string;
        }

        /// <inheritdoc />
        public override IDictionary<string, object> ToDictionary()
        {
            var result = new Dictionary<string, object>(_dict);
            var backingData = base.ToDictionary();
            foreach(var pair in backingData) {
                if (!result.ContainsKey(pair.Key)) {
                    result[pair.Key] = pair.Value;
                }
            }

            foreach(var key in result.Keys.ToArray()) {
                var value = result[key];
                switch(value) {
                    case IReadOnlyDictionary dic:
                        result[key] = dic.ToDictionary();
                        break;
                    case IReadOnlyArray arr:
                        result[key] = arr.ToList();
                        break;
                    default:
                        if (ReferenceEquals(value, RemovedValue)) {
                            result.Remove(key);
                        }

                        break;
                }
            }

            return result;
        }

        #endregion

        #region IDictionaryObject

        /// <inheritdoc />
        public new IArray GetArray(string key)
        {
            object value;
            if (!_dict.TryGetValue(key, out value)) {
                value = base.GetArray(key);
                if (value != null) {
                    var array = DataOps.ConvertROArray((ReadOnlyArray)value, ObjectChanged);
                    SetValue(key, array, false);
                    return array;
                }
            }

            return value as ArrayObject;
        }

        /// <inheritdoc />
        public new IDictionaryObject GetDictionary(string key)
        {
            object value;
            if(!_dict.TryGetValue(key, out value)) {
                value = base.GetDictionary(key);
                if (value != null) {
                    var subdoc = DataOps.ConvertRODictionary((ReadOnlyDictionary)value, ObjectChanged);
                    SetValue(key, subdoc, false);
                    return subdoc;
                }
            }

            return value as DictionaryObject;
        }

        /// <inheritdoc />
        public IDictionaryObject Remove(string key)
        {
            if (Contains(key)) {
                Set(key, RemovedValue);
            }

            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, object value)
        {
            var oldValue = GetObject(key);
            if(value == null || !value.Equals(oldValue)) {
                value = DataOps.ConvertValue(value, ObjectChanged, ObjectChanged);
                RemoveChangedListener(oldValue);
                SetValue(key, value, true);
            }

            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(IDictionary<string, object> dictionary)
        {
            RemoveAllChangedListeners();

            var result = new Dictionary<string, object>();
            foreach(var pair in dictionary) {
                result[pair.Key] = DataOps.ConvertValue(pair.Value, ObjectChanged, ObjectChanged);
            }

            var backingData = base.ToDictionary();
            foreach(var pair in backingData) {
                if(!result.ContainsKey(pair.Key)) {
                    result[pair.Key] = RemovedValue;
                }
            }

            _dict = result;

            SetChanged();
            return this;
        }

        #endregion
    }
}
