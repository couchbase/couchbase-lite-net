//
// WeakValueDictionary.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Util
{
    public class WeakValueDictionary<TKey, TValue> : IDictionary<TKey, TValue> 
    {
        #region Private variables

        private IDictionary<TKey, WeakReference> dictionary;

        #endregion

        #region Constructor

        public WeakValueDictionary()
        {
            dictionary = new Dictionary<TKey, WeakReference>();
        }

        #endregion

        #region Private

        private void PruneDeadReferences()
        {
            var keys = dictionary.Where(entry => !entry.Value.IsAlive)
                .Select(entry => entry.Key);
            foreach(var key in keys)
            {
                dictionary.Remove(key);
            }
        }

        #endregion

        #region IDictionary implementation

        public void Add(TKey key, TValue value)
        {
            dictionary.Add(key, new WeakReference(value));
        }

        public bool ContainsKey(TKey key)
        {
            WeakReference value;
            var hasValue = dictionary.TryGetValue(key, out value);
            if (hasValue)
            {
                return value.IsAlive;
            }
            else
            {
                return hasValue;
            }
        }

        public bool Remove(TKey key)
        {
            WeakReference value;
            var hasValue = dictionary.TryGetValue(key, out value);
            if (hasValue)
            {
                dictionary.Remove(key);
                return value.IsAlive;
            }
            else 
            {
                return false;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            WeakReference weakValue;
            var hasValue = dictionary.TryGetValue(key, out weakValue);

            if (hasValue)
            {
                if (weakValue.IsAlive)
                {
                    value = (TValue)weakValue.Target;
                    return true;
                }
                else
                {
                    // Prune dead entry
                    dictionary.Remove(key);
                }
            }

            value = default(TValue);
            return false;
        }

        public TValue this[TKey index]
        {
            get
            {
                return (TValue)dictionary[index].Target;
            }
            set
            {
                dictionary[index] = new WeakReference(value);
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                PruneDeadReferences();

                return dictionary.Keys;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                PruneDeadReferences();

                var result = new List<TValue>();
                foreach (var weakValue in dictionary.Values)
                {
                    var value = weakValue.Target;
                    if (value != null)
                    {
                        result.Add((TValue)value);
                    }
                }

                return result;
            }
        }

        #endregion

        #region ICollection implementation

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ContainsKey(item.Key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            PruneDeadReferences();

            var result = new List<KeyValuePair<TKey, TValue>>();

            var entries = dictionary.ToArray();
            foreach (var entry in entries)
            {
                var value = entry.Value.Target;
                if (value != null)
                {
                    result.Add(new KeyValuePair<TKey, TValue>(entry.Key, (TValue)value));
                }
            }

            result.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public int Count
        {
            get
            {
                PruneDeadReferences();

                return dictionary.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region IEnumerable implementation

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach(KeyValuePair<TKey, WeakReference> entry in dictionary)
            {
                var value = entry.Value.Target;
                if (value != null)
                {
                    yield return new KeyValuePair<TKey, TValue>(entry.Key, (TValue)value);
                }
            }
        }

        #endregion

        #region IEnumerable implementation

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region extra public

        public void Compact()
        {
            PruneDeadReferences();
        }

        #endregion
    }
}
