// 
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
//using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
    /// <summary>An in-memory object cache.</summary>
    /// <remarks>
    /// An in-memory object cache.
    /// It keeps track of all added objects as long as anything else has retained them,
    /// and it keeps a certain number of recently-accessed objects with no external references.
    /// It's intended for use by a parent resource, to cache its children.
    /// </remarks>
    public class Cache<K, V>
    {
        private const int DefaultRetainLimit = 50;

        internal int retainLimit = DefaultRetainLimit;

        private LruCache<K, V> strongReferenceCache;

        private WeakValueHashMap<K, V> weakReferenceCache;

        public Cache() : this(DefaultRetainLimit)
        {
        }

        public Cache(int retainLimit)
        {
            // how many items to retain strong references to
            // the underlying strong reference cache
            // the underlying weak reference cache
            this.retainLimit = retainLimit;
            strongReferenceCache = new LruCache<K, V>(this.retainLimit);
            weakReferenceCache = new WeakValueHashMap<K, V>();
        }

        public virtual V Put(K key, V value)
        {
            strongReferenceCache.Put(key, value);
            weakReferenceCache.Put(key, value);
            return value;
        }

        public virtual V Get(K key)
        {
            V value = null;
            if (weakReferenceCache.ContainsKey(key))
            {
                value = weakReferenceCache.Get(key);
            }
            if (value != null && strongReferenceCache.Get(key) == null)
            {
                strongReferenceCache.Put(key, value);
            }
            // re-add doc to NSCache since it's recently used
            return value;
        }

        public virtual V Remove(K key)
        {
            V removedStrongValue = null;
            V removedWeakValue = null;
            removedStrongValue = strongReferenceCache.Remove(key);
            removedWeakValue = Sharpen.Collections.Remove(weakReferenceCache, key);
            if (removedStrongValue != null)
            {
                return removedStrongValue;
            }
            if (removedWeakValue != null)
            {
                return removedWeakValue;
            }
            return null;
        }

        public virtual void Clear()
        {
            strongReferenceCache.EvictAll();
            weakReferenceCache.Clear();
        }
    }
}
