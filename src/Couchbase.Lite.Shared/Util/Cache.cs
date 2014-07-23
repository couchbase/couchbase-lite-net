//
// Cache.cs
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

namespace Couchbase.Lite.Util
{
    public class Cache <TKey, TValue>
    {
        #region Constants

        private const Int32 DefaultMaxSize = 50;

        #endregion

        #region Private Variables

        private readonly LruCache<TKey, TValue> cache;

        private readonly WeakValueDictionary<TKey, TValue> weakValueDictionary;

        private readonly Object mutex = new Object ();

        #endregion

        #region Constructors

        public Cache() : this(DefaultMaxSize) { }

        public Cache(Int32 maxSize)
        {
            cache = new LruCache<TKey, TValue>(maxSize);
            weakValueDictionary = new WeakValueDictionary<TKey, TValue>();
        }

        #endregion

        #region Public

        public TValue Put(TKey key, TValue value)
        {
            lock(mutex)
            {
                cache.Put(key, value);
                weakValueDictionary.Add(key, value);
                return value;
            }
        }

        public TValue Get(TKey key)
        {
            lock(mutex)
            {
                var value = cache.Get(key);
                if (value == null)
                {
                    weakValueDictionary.TryGetValue(key, out value);
                    if (value != null)
                    {
                        cache.Put(key, value);
                    }
                }
                return value;
            }
        }

        public TValue this[TKey key] {
            get { return Get (key); }
            set { Put (key, value); }
        }

        public TValue Remove(TKey key)
        {
            lock(mutex)
            {
                var value1 = cache.Remove(key);

                TValue value2;
                weakValueDictionary.TryGetValue(key, out value2);
                weakValueDictionary.Remove(key);

                if (value1 != null) 
                    return value1;
                else if (value2 != null)
                    return value2;
                else
                    return default(TValue);
            }
        }

        public void Clear()
        {
            lock(mutex)
            {
                cache.EvictAll();
                weakValueDictionary.Clear();
            }
        }

        #endregion
    }
}
