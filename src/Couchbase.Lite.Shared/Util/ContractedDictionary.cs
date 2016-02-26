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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// An attribute used on the ContractedDictionary class to specify information about its
    /// keys in a way that is easily visible to the consumer of the class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DictionaryContractAttribute : Attribute
    {

        #region Variables

        internal readonly IDictionary<string, Tuple<ContractedDictionary.KeyType, Type>> Contract =
            new Dictionary<string, Tuple<ContractedDictionary.KeyType, Type>>();

        #endregion

        #region Properties

        /// <summary>
        /// Sets a list of keys, followed by the required type, that this dictionary instance requires.
        /// It is specified as an array of (string)key, (Type)type, (string)key, (Type)type, ...
        /// </summary>
        /// <exception cref="System.ArgumentException">Throw if the number of entries is not even (
        /// there needs to be two entries per key, name and type)</exception>
        public object[] RequiredKeys
        {
            get { return null; }
            set
            { 
                if (value == null) {
                    throw new ArgumentNullException("value");
                }

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

        /// <summary>
        /// Sets a list of keys which may or may not be present but must conform
        /// to the given type specified.
        /// It is specified as an array of (string)key, (Type)type, (string)key, (Type)type, ...
        /// </summary>
        /// <exception cref="System.ArgumentException">Throw if the number of entries is not even (
        /// there needs to be two entries per key, name and type)</exception>
        public object[] OptionalKeys
        {
            get { return null; }
            set
            { 
                if (value == null) {
                    throw new ArgumentNullException("value");
                }

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

        #endregion
    }

    /// <summary>
    /// A dictionary which conforms to certains keys and types specified as an attribute
    /// </summary>
    public abstract class ContractedDictionary : ICollection<KeyValuePair<string, object>>, IDictionary<string, object>
    {

        #region Enums

        /// <summary>
        /// A type of ContractedDictionaryKey
        /// </summary>
        public enum KeyType
        {
            /// <summary>
            /// A key which is optional
            /// </summary>
            Optional,

            /// <summary>
            /// A key which is required
            /// </summary>
            Required
        }

        #endregion

        #region Constants

        private const string TAG = "ContractedDictionary";

        #endregion

        #region Variables

        private readonly ConcurrentDictionary<string, object> _inner =
            new ConcurrentDictionary<string, object>();

        private readonly IDictionary<string, Tuple<KeyType, Type>> _contract;

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        protected ContractedDictionary()
        {
            var att = (DictionaryContractAttribute)GetType().GetCustomAttributes(typeof(DictionaryContractAttribute), false).FirstOrDefault();
            if (att == null) {
                throw new InvalidOperationException("ContractedDictionary requires the DictionaryContract attribute");
            }

            _contract = att.Contract;
        }

        /// <summary>
        /// Constrcutor for initializing with predetermined values
        /// </summary>
        /// <param name="source">The values to use in the dictionary.</param>
        protected ContractedDictionary(IDictionary<string, object> source) : this()
        {
            _inner = new ConcurrentDictionary<string, object>(source);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets or sets an object via a given key
        /// </summary>
        /// <param name="key">The key to get or set</param>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown if the given key was not
        /// present in the dictionary</exception>
        public object this[string key]
        {
            get
            { 
                object retVal;
                if (!_inner.TryGetValue(key, out retVal)) {
                    throw new KeyNotFoundException();
                }

                return retVal;
            }
            set { Add(key, value); }
        }

        /// <summary>
        /// Add the specified key and value.
        /// </summary>
        /// <param name="key">The key to add</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="System.ArgumentException">Thrown if the key/value combination is
        /// not valid for this dictionary according to its contract</exception>
        public void Add(string key, object value)
        {
            var errMsg = Validate(key, value);
            if (errMsg != null) {
                throw new ArgumentException(errMsg);
            }

            _inner[key] = value;
        }

        /// <summary>
        /// Validates the dictionary
        /// </summary>
        /// <param name="message">If unsuccesful, this paramater stores the error message</param>
        /// <returns><c>true</c> on success, otherwise <c>false</c></returns>
        public bool Validate(out string message)
        {
            message = Validate();
            return message == null;
        }

        /// <summary>
        /// Checks if this dictionary contains the given key
        /// </summary>
        /// <returns><c>true</c>, if the key was found, <c>false</c> otherwise.</returns>
        /// <param name="key">The key to check</param>
        public bool ContainsKey(string key)
        {
            return _inner.ContainsKey(key);
        }

        /// <summary>
        /// Tries to get the value of the specified key
        /// </summary>
        /// <returns><c>true</c>, if key's value was retrieved, <c>false</c> otherwise.</returns>
        /// <param name="key">The key to check</param>
        /// <param name="value">On success, stores the value</param>
        /// <typeparam name="T">The type of object to store in value</typeparam>
        /// <exception cref="System.ArgumentException">Thrown if an incorrect type was specified in T</exception>
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
            } catch (InvalidCastException) {
                Log.W(TAG, "Incorrect type {0} specified for object with type {1}", typeof(T), rawValue.GetType());
                return false;
            }
        }

        #endregion

        #region Internal Methods

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

            if (!rule.Item2.IsInstanceOfType(value)) {
                return String.Format("Incorrect type for key '{0}'.  Cannot assign {1} to {2}.", key, value.GetType(), rule.Item2);
            }

            return null;
        }

        #endregion

        #region ICollection
        #pragma warning disable 1591

        public void Add(KeyValuePair<string, object> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return _inner.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, object>>)_inner).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return ((ICollection<KeyValuePair<string, object>>)_inner).Remove(item);
        }

        public int Count
        {
            get
            {
                return _inner.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((ICollection<KeyValuePair<string, object>>)_inner).IsReadOnly;
            }
        }

        #endregion

        #region IDictionary

        public bool Remove(string key)
        {
            return ((IDictionary<string, object>)_inner).Remove(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            return _inner.TryGetValue(key, out value);
        }

        public ICollection<string> Keys
        {
            get
            {
                return _inner.Keys;
            }
        }

        public ICollection<object> Values
        {
            get
            {
                return _inner.Values;
            }
        }

        #endregion

        #region IEnumerable

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        #pragma warning restore 1591
        #endregion
    }
}

