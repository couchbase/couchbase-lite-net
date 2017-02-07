//
//  IPropertyContainer.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

using Couchbase.Lite.Serialization;
using Couchbase.Lite.Util;
using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    public interface IPropertyContainer
    {
        IDictionary<string, object> Properties { get; set; }

        IPropertyContainer Set(string key, object value);

        object Get(string key);

        string GetString(string key);

        long GetLong(string key);

        float GetFloat(string key);

        double GetDouble(string key);

        bool GetBoolean(string key);

        DateTimeOffset? GetDate(string key);

        IList<object> GetArray(string key);

        IPropertyContainer Remove(string key);

        bool Contains(string key);

        object this[string key] { get; set; }
    }

    internal unsafe abstract class PropertyContainer : IPropertyContainer
    {
        private FLDict* _root;

        private IReadOnlyDictionary<string, object> _rootProps;

        private Dictionary<string, object> _properties;

        public IDictionary<string, object> Properties
        {
            get {
                if(_properties == null) {
                    var saved = SavedProperties;
                    _properties = new Dictionary<string, object>();
                    if(saved != null) {
                        foreach(var pair in saved) {
                            _properties[pair.Key] = pair.Value;
                        }
                    }
                }
                return _properties;
            }
            set {
                _properties = value != null ? new Dictionary<string, object>(value) : new Dictionary<string, object>();
            }
        }

        internal bool HasChanges { get; set; }

        protected IReadOnlyDictionary<string, object> SavedProperties
        {
            get {
                if(_properties != null && HasChanges) {
                    return _properties;
                }

                if(_root != null) {
                    return FLValueConverter.ToObject((FLValue *)_root, this, GetSharedStrings()) as IReadOnlyDictionary<string, object>;
                }

                return _rootProps;
            }
        }

        public IPropertyContainer Set(string key, object value)
        {
            ValidateObjectType(value);
            MutateProperties();

            var oldValue = Properties.Get(key);
            if(value != oldValue && value != null && !value.Equals(oldValue)) {
                _properties[key] = value;
                MarkChanges();
            }

            return this;
        }

        public object Get(string key)
        {
            if(_properties != null) {
                return Properties.Get(key);
            }

            return FLValueConverter.ToObject(FleeceValueForKey(key), this, GetSharedStrings());
        }

        public string GetString(string key)
        {
            var retVal = String.Empty;
            if(TryGet(key, out retVal)) {
                return retVal;
            }

            return Native.FLValue_AsString(FleeceValueForKey(key));
        }

        public long GetLong(string key)
        {
            var retVal = 0L;
            if(TryGet(key, out retVal)) {
                return retVal;
            }

            return Native.FLValue_AsInt(FleeceValueForKey(key));
        }

        public float GetFloat(string key)
        {
            var retVal = 0.0f;
            if(TryGet(key, out retVal)) {
                return retVal;
            }

            return Native.FLValue_AsFloat(FleeceValueForKey(key));
        }

        public double GetDouble(string key)
        {
            var retVal = 0.0;
            if(TryGet(key, out retVal)) {
                return retVal;
            }

            return Native.FLValue_AsDouble(FleeceValueForKey(key));
        }

        public bool GetBoolean(string key)
        {
            var retVal = false;
            if(TryGet(key, out retVal)) {
                return retVal;
            }

            return Native.FLValue_AsBool(FleeceValueForKey(key));
        }

        public DateTimeOffset? GetDate(string key)
        {
            var retVal = default(DateTimeOffset);
            if(TryGet(key, out retVal)) {
                return retVal;
            }

            var dateString = GetString(key);
            if(dateString == null) {
                return null;
            }

            return DateTimeOffset.ParseExact(dateString, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public IList<object> GetArray(string key)
        {
            throw new NotImplementedException();
        }

        public Subdocument GetSubdocument(string key)
        {
            throw new NotImplementedException();
        }

        public T GetSubdocument<T>(string key) where T : ISubdocumentModel
        {
            throw new NotImplementedException();
        }

        public Document GetDocument(string key)
        {
            throw new NotImplementedException();
        }

        public IList<Document> GetDocuments(string key)
        {
            throw new NotImplementedException();
        }

        public Property GetProperty(string key)
        {
            throw new NotImplementedException();
        }

        public T GetProperty<T>(string key) where T : IPropertyModel
        {
            throw new NotImplementedException();
        }

        public IPropertyContainer Remove(string key)
        {
            _properties.Remove(key);
            return this;
        }

        public bool Contains(string key)
        {
            if(_properties != null) {
                return _properties.ContainsKey(key);
            }

            return FleeceValueForKey(key) != null;
        }

        public object this[string key]
        {
            get {
                return Get(key);
            }
            set {
                Set(key, value);
            }
        }

        protected internal abstract IBlob CreateBlob(IDictionary<string, object> properties);

        internal void SetRoot(FLDict* root, IReadOnlyDictionary<string, object> props)
        {
            _root = null;
            _rootProps = null;
            if(root != null) {
                _root = root;
            }

            if(props != null) {
                _rootProps = props;
            }

            HasChanges = false;
        }

        internal void ResetChanges()
        {
            _properties = null;
            HasChanges = false;
        }

        internal virtual SharedStringCache GetSharedStrings()
        {
            return null;
        }

        private static readonly TypeInfo[] ValidTypes = new[] {
            typeof(string).GetTypeInfo(),
            typeof(DateTime).GetTypeInfo(),
            typeof(DateTimeOffset).GetTypeInfo(),
            typeof(decimal).GetTypeInfo()
        };

        private static bool IsValidScalarType(Type type)
        {
            var info = type.GetTypeInfo();
            if(info.IsPrimitive) {
                return true;
            }

            return ValidTypes.Any(x => info.IsAssignableFrom(x));
        }

        private static void ValidateObjectType(object value)
        {
            /*var type = value.GetType();
            if(IsValidScalarType(type)) {
                return;
            }

            var array = value as IList;
            if(array == null) {
                throw new ArgumentException($"Invalid type in document properties: {type.Name}", nameof(value));
            }

            foreach(var item in array) {
                ValidateObjectType(item);
            }*/
        }

        private void MutateProperties()
        {
            if(_properties == null) {
                _properties = new Dictionary<string, object>();
                if(SavedProperties != null) {
                    foreach(var pair in SavedProperties) {
                        _properties[pair.Key] = pair.Value;
                    }
                }
            }
        }

        private void MarkChanges()
        {
            HasChanges = true;
        }

        private bool TryGet<T>(string key, out T value)
        {
            if(_properties != null) {
                if(Properties.TryGetValue<T>(key, out value)) {
                    return true;
                }
            } else if(_rootProps != null) {
                if(_rootProps.TryGetValue<T>(key, out value)) {
                    return true;
                }
            }

            value = default(T);
            return false;
        }

        private FLValue* FleeceValueForKey(string key)
        {
            return Native.FLDict_GetSharedKey(_root, Encoding.UTF8.GetBytes(key), GetSharedStrings().SharedKeys);
        }
    }
}
