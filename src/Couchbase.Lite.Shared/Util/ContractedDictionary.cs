//
//  ContractedDictionary.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Collections.Concurrent;
using System.Linq;

namespace Couchbase.Lite.Util
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DictionaryContractAttribute : Attribute
    {
        internal readonly IDictionary<string, Tuple<ContractedDictionary.KeyType, Type>> Contract =
            new Dictionary<string, Tuple<ContractedDictionary.KeyType, Type>>();

        public object[] RequiredKeys 
        {
            get { return null; }
            set { 
                if ((value.Length % 2) == 1) {
                    throw new ArgumentException("RequiredKeys needs an even number of entries:  key, type, key, type, ...");
                }

                for (int i = 0; i < value.Length; i += 2) {
                    var nextKey = value[i] as string;
                    var nextType = value[i + 1] as Type;
                    if (nextKey == null) {
                        Log.W("ContractedDictionary", "Non-string key found!");
                        continue;
                    }

                    if (nextType == null) {
                        Log.W("ContractedDictionary", "Non-Type value found!");
                        continue;
                    }

                    Contract[nextKey] = Tuple.Create(ContractedDictionary.KeyType.Required, nextType);
                }
            }
        }

        public object[] OptionalKeys 
        {
            get { return null; }
            set { 
                if ((value.Length % 2) == 1) {
                    throw new ArgumentException("OptionalKeys needs an even number of entries:  key, type, key, type, ...");
                }

                for (int i = 0; i < value.Length; i += 2) {
                    var nextKey = value[i] as string;
                    var nextType = value[i + 1] as Type;
                    if (nextKey == null) {
                        Log.W("ContractedDictionary", "Non-string key found!");
                        continue;
                    }

                    if (nextType == null) {
                        Log.W("ContractedDictionary", "Non-Type value found!");
                        continue;
                    }

                    Contract[nextKey] = Tuple.Create(ContractedDictionary.KeyType.Optional, nextType);
                }
            }
        }
    }

    public abstract class ContractedDictionary : IEnumerable<KeyValuePair<string, object>>
    {
        public enum KeyType
        {
            Optional,
            Required
        }

        private readonly ConcurrentDictionary<string, object> _inner =
            new ConcurrentDictionary<string, object>();

        private readonly IDictionary<string, Tuple<KeyType, Type>> _contract;

        public ContractedDictionary()
        {
            var att = (DictionaryContractAttribute)GetType().GetCustomAttributes(typeof(DictionaryContractAttribute), false).FirstOrDefault();
            if (att == null) {
                throw new InvalidOperationException("ContractedDictionary requires the DictionaryContract attribute");
            }

            _contract = att.Contract;
        }

        public object this[string key]
        {
            get { 
                object retVal;
                if (!_inner.TryGetValue(key, out retVal)) {
                    throw new KeyNotFoundException();
                }

                return retVal;
            }
            set { 
                Add(key, value);
            }
        }

        public void Add(string key, object value)
        {
            var errMsg = Validate(key, value);
            if (errMsg != null) {
                throw new ArgumentException(errMsg);
            }

            _inner[key] = value;
        }

        public bool Validate(out string message)
        {
            message = Validate();
            return message == null;
        }

        public bool ContainsKey(string key) 
        {
            return _inner.ContainsKey(key);
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            value = default(T);
            var rawValue = default(object);
            if (!_inner.TryGetValue(key, out rawValue)) {
                return false;
            }

            try {
                value = (T)rawValue;
                return true;
            } catch(InvalidCastException) {
                throw new ArgumentException(String.Format("Incorrect type {0} specified for object with type {1}", typeof(T), rawValue.GetType()));
            }
        }

        internal string Validate()
        {
            foreach (var pair in _contract) {
                var required = pair.Value.Item1 == KeyType.Required;
                if (required && !ContainsKey(pair.Key)) {
                    return String.Format("Required key '{0}' not found.", pair.Key);
                }
            }

            return null;
        }

        internal string Validate(string key, object value)
        {
            Tuple<KeyType, Type> rule;
            if (!_contract.TryGetValue(key, out rule)) {
                return null;
            }

            if(!rule.Item2.IsAssignableFrom(value.GetType())) {
                return String.Format("Incorrect type for key '{0}'.  Cannot assign {1} to {2}.", value.GetType(), rule.Item2);
            }

            return null;
        }
        
        #region IEnumerable

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        #endregion
    }
}

