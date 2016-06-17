//
//  LogicalDictionary.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Text;

namespace Couchbase.Lite.Util
{
    internal enum LogicalDictionaryEvent
    {
        Add,
        Change,
        Remove
    }

    internal abstract class LogicalDictionaryRule<K, V>
    {
        public void Validate(LogicalDictionaryEvent e, K key, V newVal = default(V), V oldVal = default(V))
        {
            Func<K, V, V, bool> validator = (k, n, o) => false;
            switch(e) {
                case LogicalDictionaryEvent.Add:
                    validator = (k, n, o) => CanAddKey(k, n);
                    break;
                case LogicalDictionaryEvent.Change:
                    validator = CanChangeKey;
                    break;
                case LogicalDictionaryEvent.Remove:
                    validator = (k, n, o) => CanRemoveKey(k);
                    break;
                default:
                    break;
            }

            if(!validator(key, newVal, oldVal)) {
                throw new InvalidOperationException(GetExceptionMessage(e, key, newVal, oldVal));
            }
        }

        protected virtual bool CanAddKey(K key, V value)
        {
            return true;
        }

        protected virtual bool CanChangeKey(K key, V newVal, V oldVal)
        {
            return true;
        }

        protected virtual bool CanRemoveKey(K key)
        {
            return true;
        }

        protected virtual string GetExceptionMessage_Internal(LogicalDictionaryEvent e, K key,
            V newVal, V oldVal)
        {
            return null;
        }

        private string GetExceptionMessage(LogicalDictionaryEvent e, K key,
           V newVal, V oldVal)
        {
            var msg = GetExceptionMessage_Internal(e, key, newVal, oldVal);
            if(msg != null) {
                return msg;
            }

            if(e == LogicalDictionaryEvent.Add) {
                return $"Cannot add {newVal} for key `{key}`";
            } else if(e == LogicalDictionaryEvent.Change) {
                return $"Cannot change key `{key}` from {oldVal} to {newVal}";
            } else if(e == LogicalDictionaryEvent.Remove) {
                return $"Cannot remove key `{key}`";
            }

            return $"Unknown event {e}";
        }
    }

    internal sealed class LogicalDictionary<K, V> : IDictionary<K, V>
    {
        private readonly Dictionary<K, V> _internal = new Dictionary<K, V>();
        private readonly ICollection<LogicalDictionaryRule<K, V>> _ruleset;
        private static readonly ICollection<LogicalDictionaryRule<K, V>> Placeholder =
            new HashSet<LogicalDictionaryRule<K, V>>();

        public LogicalDictionary(ICollection<LogicalDictionaryRule<K, V>> ruleset)
        {
            _ruleset = ruleset ?? Placeholder;
        }

        public V this[K key]
        {
            get {
                return _internal[key];
            }

            set {
                V oldVal = default(V);
                var e = LogicalDictionaryEvent.Change;
                if(!TryGetValue(key, out oldVal)) {
                    e = LogicalDictionaryEvent.Add;
                    oldVal = default(V);
                }

                foreach(var rule in _ruleset) {
                    rule.Validate(e, key, value, oldVal);
                }

                _internal[key] = value;
            }
        }

        public int Count
        {
            get {
                return _internal.Count;
            }
        }

        public bool IsReadOnly
        {
            get {
                return ((ICollection<KeyValuePair<K, V>>)_internal).IsReadOnly;
            }
        }

        public ICollection<K> Keys
        {
            get {
                return _internal.Keys;
            }
        }

        public ICollection<V> Values
        {
            get {
                return _internal.Values;
            }
        }

        public void Add(KeyValuePair<K, V> item)
        {
            foreach(var rule in _ruleset) {
                rule.Validate(LogicalDictionaryEvent.Add, item.Key, item.Value);
            }

            ((ICollection<KeyValuePair<K, V>>)_internal).Add(item);
        }

        public void Add(K key, V value)
        {
            foreach(var rule in _ruleset) {
                rule.Validate(LogicalDictionaryEvent.Add, key, value);
            }

            _internal.Add(key, value);
        }

        public void Clear()
        {
            foreach(var rule in _ruleset) {
                foreach(var key in Keys) {
                    rule.Validate(LogicalDictionaryEvent.Remove, key);
                }
            }

            _internal.Clear();
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            return ((ICollection<KeyValuePair<K, V>>)_internal).Contains(item);
        }

        public bool ContainsKey(K key)
        {
            return _internal.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<K, V>>)_internal).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<K, V>>)_internal).GetEnumerator();
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            foreach(var rule in _ruleset) {
                rule.Validate(LogicalDictionaryEvent.Remove, item.Key);
            }

            return ((ICollection<KeyValuePair<K, V>>)_internal).Remove(item);
        }

        public bool Remove(K key)
        {
            foreach(var rule in _ruleset) {
                rule.Validate(LogicalDictionaryEvent.Remove, key);
            }

            return _internal.Remove(key);
        }

        public bool TryGetValue(K key, out V value)
        {
            return _internal.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_internal).GetEnumerator();
        }
    }
}