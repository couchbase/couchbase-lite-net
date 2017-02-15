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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

using Couchbase.Lite.Serialization;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using LiteCore.Interop;

namespace Couchbase.Lite.DB
{
    internal abstract unsafe class PropertyContainer : ThreadSafe, IPropertyContainer
    {
        #region Constants

        private static readonly TypeInfo[] _ValidTypes = new[] {
            typeof(string).GetTypeInfo(),
            typeof(DateTime).GetTypeInfo(),
            typeof(DateTimeOffset).GetTypeInfo(),
            typeof(decimal).GetTypeInfo()
        };

        #endregion

        #region Variables

        private bool _hasChanges;

        private Dictionary<string, object> _properties;
        private FLDict* _root;

        private IReadOnlyDictionary<string, object> _rootProps;

        #endregion

        #region Properties

        public object this[string key]
        {
            get {
                AssertSafety();
                return Get(key);
            }
            set {
                AssertSafety();
                Set(key, value);
            }
        }

        public IDictionary<string, object> Properties
        {
            get {
                AssertSafety();
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
                AssertSafety();
                _properties = value != null ? new Dictionary<string, object>(value) : new Dictionary<string, object>();
            }
        }

        protected IReadOnlyDictionary<string, object> SavedProperties
        {
            get {
                AssertSafety();
                if(_properties != null && HasChanges) {
                    return _properties;
                }

                if(_root != null) {
                    return FLValueConverter.ToObject((FLValue *)_root, this, GetSharedStrings()) as IReadOnlyDictionary<string, object>;
                }

                return _rootProps;
            }
        }

        internal bool HasChanges
        {
            get {
                AssertSafety();
                return _hasChanges;
            }
            set {
                AssertSafety();
                _hasChanges = value;
            }
        }

        #endregion

        #region Public Methods

        public Document GetDocument(string key)
        {
            throw new NotImplementedException();
        }

        public IList<Document> GetDocuments(string key)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Protected Internal Methods

        protected internal abstract IBlob CreateBlob(IDictionary<string, object> properties);

        #endregion

        #region Protected Methods

        protected void SetRoot(FLDict* root, IReadOnlyDictionary<string, object> props)
        {
            _root = null;
            _rootProps = null;
            if(root != null) {
                _root = root;
            }

            if(props != null) {
                _rootProps = props;
            }

            _hasChanges = false;
        }

        #endregion

        #region Internal Methods

        internal virtual SharedStringCache GetSharedStrings()
        {
            AssertSafety();
            return null;
        }

        internal void ResetChanges()
        {
            AssertSafety();
            _properties = null;
            HasChanges = false;
        }

        #endregion

        #region Private Methods

        private static bool IsValidScalarType(Type type)
        {
            var info = type.GetTypeInfo();
            if(info.IsPrimitive) {
                return true;
            }

            return _ValidTypes.Any(x => info.IsAssignableFrom(x));
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

        private FLValue* FleeceValueForKey(string key)
        {
            return Native.FLDict_GetSharedKey(_root, Encoding.UTF8.GetBytes(key), GetSharedStrings().SharedKeys);
        }

        private void MarkChanges()
        {
            HasChanges = true;
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

        private bool TryGet<T>(string key, out T value)
        {
            if(_properties != null) {
                if(Properties.TryGetValue(key, out value)) {
                    return true;
                }
            } else if(_rootProps != null) {
                if(_rootProps.TryGetValue(key, out value)) {
                    return true;
                }
            }

            value = default(T);
            return false;
        }

        #endregion

        #region IPropertyContainer

        public bool Contains(string key)
        {
            AssertSafety();
            if(_properties != null) {
                return _properties.ContainsKey(key);
            }

            return FleeceValueForKey(key) != null;
        }

        public object Get(string key)
        {
            AssertSafety();
            if(_properties != null) {
                return Properties.Get(key);
            }

            return FLValueConverter.ToObject(FleeceValueForKey(key), this, GetSharedStrings());
        }

        public IList<object> GetArray(string key)
        {
            throw new NotImplementedException();
        }

        public IBlob GetBlob(string key)
        {
            AssertSafety();
            return Get(key) as IBlob;
        }

        public bool GetBoolean(string key)
        {
            AssertSafety();
            bool retVal;
            return TryGet(key, out retVal) ? retVal : Native.FLValue_AsBool(FleeceValueForKey(key));
        }

        public DateTimeOffset? GetDate(string key)
        {
            AssertSafety();
            DateTimeOffset retVal;
            if(TryGet(key, out retVal)) {
                return retVal;
            }

            var dateString = GetString(key);
            if(dateString == null) {
                return null;
            }

            return DateTimeOffset.ParseExact(dateString, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public double GetDouble(string key)
        {
            AssertSafety();
            double retVal;
            return TryGet(key, out retVal) ? retVal : Native.FLValue_AsDouble(FleeceValueForKey(key));
        }

        public float GetFloat(string key)
        {
            AssertSafety();
            float retVal;
            return TryGet(key, out retVal) ? retVal : Native.FLValue_AsFloat(FleeceValueForKey(key));
        }

        public long GetLong(string key)
        {
            AssertSafety();
            long retVal;
            return TryGet(key, out retVal) ? retVal : Native.FLValue_AsInt(FleeceValueForKey(key));
        }

        public string GetString(string key)
        {
            AssertSafety();
            string retVal;
            return TryGet(key, out retVal) ? retVal : Native.FLValue_AsString(FleeceValueForKey(key));
        }

        public IPropertyContainer Remove(string key)
        {
            AssertSafety();
            _properties.Remove(key);
            return this;
        }

        public IPropertyContainer Set(string key, object value)
        {
            AssertSafety();
            ValidateObjectType(value);
            MutateProperties();

            var oldValue = Properties.Get(key);
            if(value == null || !value.Equals(oldValue)) {
                _properties[key] = value;
                MarkChanges();
            }

            return this;
        }

        #endregion
    }
}
