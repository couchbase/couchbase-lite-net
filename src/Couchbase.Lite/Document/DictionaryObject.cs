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
using System.Globalization;
using System.Linq;
using System.Reflection;
using Couchbase.Lite.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed class ObjectChangedEventArgs<T> : EventArgs
    {
        public T ChangedObject { get; }

        internal ObjectChangedEventArgs(T changedObject)
        {
            ChangedObject = changedObject;
        }
    }

    internal sealed class DictionaryObjectConverter : JsonConverter
    {
        public override bool CanRead => false;

        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dict = (IDictionaryObject)value;
            writer.WriteStartObject();
            foreach (var pair in dict) {
                writer.WritePropertyName(pair.Key);
                serializer.Serialize(writer, pair.Value);
            }
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(DictionaryObject) == objectType || typeof(Subdocument) == objectType;
        }
    }

    [JsonConverter(typeof(DictionaryObjectConverter))]
    internal sealed class DictionaryObject : ReadOnlyDictionary, IDictionaryObject
    {
        #region Variables

        internal event EventHandler<ObjectChangedEventArgs<DictionaryObject>> Changed;
        private Dictionary<string, object> _dict = new Dictionary<string, object>();

        #endregion

        #region Properties

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
                    if(val == null) {
                        count -= 1;
                    }
                }

                return count;
            }
        }

        internal override bool IsEmpty => _dict.All(x => x.Value == null) && base.IsEmpty;

        public override ICollection<string> Keys
        {
            get {
                var result = new HashSet<string>();
                foreach (var key in base.Keys) {
                    result.Add(key);
                }

                foreach (var pair in _dict) {
                    if (pair.Value != null) {
                        result.Add(pair.Key);
                    }
                }

                return result;
            }
        }

        public new Fragment this[string key]
        {
            get {
                var value = GetObject(key);
                return new Fragment(value, this, key);
            }
        }

        internal bool HasChanges
        {
            get; private set;
        }

        #endregion

        #region Constructors

        public DictionaryObject(IReadOnlyDictionary data)
            : base(data)
        {

        }

        #endregion

        #region Private Methods

        private void RemoveAllChangedListeners()
        {
            foreach(var val in _dict.Values) {
                RemoveChangedListener(val);
            }
        }

        private void RemoveChangedListener(object value)
        {
            switch(value) {
                case Subdocument subdoc:
                    subdoc.Dictionary.Changed -= ObjectChanged;
                    break;
                case ArrayObject arr:
                    arr.Changed -= ObjectChanged;
                    break;
            }
        }

        private object PrepareValue(object value)
        {
            DataOps.ValidateValue(value);
            return DataOps.ConvertValue(value, ObjectChanged, ObjectChanged);
        }
        
        private void ObjectChanged(object sender, ObjectChangedEventArgs<DictionaryObject> args)
        {
            SetChanged();
        }

        private void ObjectChanged(object sender, ObjectChangedEventArgs<ArrayObject> args)
        {
            SetChanged();
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

        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        public override bool Contains(string key)
        {
            return (_dict.ContainsKey(key) && _dict[key] != null) || base.Contains(key);
        }

        public override Blob GetBlob(string key)
        {
            object value;
            if (!_dict.TryGetValue(key, out value)) {
                return base.GetBlob(key);
            }

            return value as Blob;
        }

        public override bool GetBoolean(string key)
        {
            if(!_dict.ContainsKey(key)) {
                return base.GetBoolean(key);
            }

            var value = _dict[key];
            if(value == null) {
                return false;
            }

            try {
                return Convert.ToBoolean(value);
            } catch(InvalidCastException) {
                return false;
            }
        }

        public override DateTimeOffset GetDate(string key)
        {
            if (!_dict.ContainsKey(key)) {
                return base.GetDate(key);
            }

            var value = _dict[key] as string;
            if (value == null) {
                return DateTimeOffset.MinValue;
            }

            return DateTimeOffset.ParseExact(value, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public override double GetDouble(string key)
        {
            if (!_dict.ContainsKey(key)) {
                return base.GetDouble(key);
            }

            var value = _dict[key];
            if (value == null) {
                return 0.0;
            }

            try {
                return Convert.ToDouble(value);
            } catch (InvalidCastException) {
                return 0.0;
            }
        }

        public override int GetInt(string key)
        {
            if(!_dict.ContainsKey(key)) {
                return base.GetInt(key);
            }

            var value = _dict[key];
            if (value == null) {
                return 0;
            }

            try {
                return Convert.ToInt32(value);
            } catch (InvalidCastException) {
                return 0;
            }
        }

        public override long GetLong(string key)
        {
            if (!_dict.ContainsKey(key)) {
                return base.GetLong(key);
            }

            var value = _dict[key];
            if (value == null) {
                return 0L;
            }

            try {
                return Convert.ToInt64(value);
            } catch (InvalidCastException) {
                return 0L;
            }
        }

        public override object GetObject(string key)
        {
            object value = null;
            if(!_dict.TryGetValue(key, out value)) {
                value = base.GetObject(key);
                switch(value) {
                    case ReadOnlySubdocument sub:
                        value = Doc.DataOps.ConvertROSubdocument(sub, ObjectChanged);
                        SetValue(key, value, false);
                        break;
                    case ReadOnlyArray arr:
                        value = Doc.DataOps.ConvertROArray(arr, ObjectChanged);
                        SetValue(key, value, false);
                        break;
                }
            }

            return value;
        }

        public override string GetString(string key)
        {
            object value;
            if(!_dict.TryGetValue(key, out value)) {
                return base.GetString(key);
            }

            return value as string;
        }

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
                    case null:
                        result.Remove(key);
                        break;
                    case IReadOnlyDictionary dic:
                        result[key] = dic.ToDictionary();
                        break;
                    case IReadOnlyArray arr:
                        result[key] = arr.ToList();
                        break;
                }
            }

            return result;
        }

        #endregion

        #region IDictionaryObject

        public new ArrayObject GetArray(string key)
        {
            object value;
            if (!_dict.TryGetValue(key, out value)) {
                value = base.GetArray(key);
                if (value != null) {
                    var array = Doc.DataOps.ConvertROArray((ReadOnlyArray)value, ObjectChanged);
                    SetValue(key, array, false);
                    return array;
                }
            }

            return value as ArrayObject;
        }

        public new Subdocument GetSubdocument(string key)
        {
            object value;
            if(!_dict.TryGetValue(key, out value)) {
                value = base.GetSubdocument(key);
                if (value != null) {
                    var subdoc = DataOps.ConvertROSubdocument((ReadOnlySubdocument)value, ObjectChanged);
                    SetValue(key, subdoc, false);
                    return subdoc;
                }
            }

            return value as Subdocument;
        }

        public IDictionaryObject Remove(string key)
        {
            Set(key, null);
            return this;
        }

        public IDictionaryObject Set(string key, object value)
        {
            var oldValue = GetObject(key);
            if (value == null && oldValue == null) {
                return this;
            }

            if(value == null || !value.Equals(oldValue)) {
                value = PrepareValue(value);
                RemoveChangedListener(oldValue);
                SetValue(key, value, true);
            }

            return this;
        }

        public IDictionaryObject Set(IDictionary<string, object> dictionary)
        {
            RemoveAllChangedListeners();

            var result = new Dictionary<string, object>();
            foreach(var pair in dictionary) {
                result[pair.Key] = PrepareValue(pair.Value);
            }

            var backingData = base.ToDictionary();
            foreach(var pair in backingData) {
                if(!result.ContainsKey(pair.Key)) {
                    result[pair.Key] = null;
                }
            }

            _dict = result;

            SetChanged();
            return this;
        }

        #endregion

        private sealed class Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private readonly DictionaryObject _parent;
            private readonly HashSet<string> _usedKeys = new HashSet<string>();
            private AggregateEnumerator<KeyValuePair<string, object>> _underlying;

            public Enumerator(DictionaryObject parent)
            {
                _parent = parent;
                _underlying = new AggregateEnumerator<KeyValuePair<string, object>>(_parent._dict.GetEnumerator(),
                    _parent.Data.GetEnumerator());
            }

            public bool MoveNext()
            {
                if (!_underlying.MoveNext()) {
                    return false;
                }

                while (_usedKeys.Contains(Current.Key)) {
                    if (!_underlying.MoveNext()) {
                        return false;
                    }
                }

                _usedKeys.Add(Current.Key);
                return true;
            }

            public void Reset()
            {
                _underlying?.Dispose();
                _underlying = new AggregateEnumerator<KeyValuePair<string, object>>(_parent._dict.GetEnumerator(),
                    _parent.Data.GetEnumerator());
                _usedKeys.Clear();
            }

            public KeyValuePair<string, object> Current => _underlying.Current;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _underlying?.Dispose();
            }
        }
    }
}
