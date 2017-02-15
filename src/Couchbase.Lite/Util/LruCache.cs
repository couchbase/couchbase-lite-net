//
// LruCache.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
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
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Util
{
    //LruCache implementation (null values disallowed)
    internal class LruCache<TKey, TValue> : IDisposable
    where TKey: class 
    where TValue: class
    {
        #region Constants

        private const string Tag = "LruCache";

        #endregion

        #region Variables

        private readonly IDictionary<TKey, WeakReference<TValue>> _allValues = new Dictionary<TKey, WeakReference<TValue>>();
        private readonly Object _locker = new Object ();
        private readonly LinkedList<TKey> _nodes = new LinkedList<TKey>();

        private readonly IDictionary<TKey, TValue> _recents = new Dictionary<TKey, TValue>();

        #endregion

        #region Properties

        public int CreateCount { get; private set; }
        public int EvictionCount { get; private set; }
        public int HitCount { get; private set; }

        public TValue this[TKey key] {
            get { return Get (key); }
            set { Put (key, value); }
        }

        public int MaxSize { get; private set; }
        public int MissCount { get; private set; }
        public int PutCount { get; private set; }

        public int Size { get; private set; }

        #endregion

        #region Constructors

        public LruCache(int maxSize)
        {
            if (maxSize <= 0) {
                Log.To.NoDomain.E(Tag, "maxSize is <= 0 ({0}) in ctor, throwing...", maxSize);
                throw new ArgumentException("maxSize <= 0");
            }

            MaxSize = maxSize;
        }

        #endregion

        #region Public Methods

        public void Clear()
        {
            Log.To.NoDomain.D(Tag, "Entering lock in Clear");
            lock (_locker) {
                _recents.Clear();
                _allValues.Clear();
                _nodes.Clear();
                Size = 0;
            }
            Log.To.NoDomain.D(Tag, "Exited lock in Clear");
        }

        public TValue Get(TKey key)
        {
            if (key == null) {
                Log.To.NoDomain.E(Tag, "key cannot be null in Get, throwing...");
                throw new ArgumentNullException(nameof(key));
            }

            Log.To.NoDomain.D(Tag, "Entering first lock in Get");
            TValue mapValue;
            lock (_locker) {
                mapValue = _recents.Get(key);
                if (mapValue != null) {
                    Log.To.NoDomain.V(Tag, "LruCache hit for {0}, returning {1}...",
                        new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure),
                        new SecureLogString(mapValue, LogMessageSensitivity.PotentiallyInsecure));
                    HitCount++;
                    _nodes.Remove(key);
                    _nodes.AddFirst(key);
                    return mapValue;
                }
                MissCount++;

                Log.To.NoDomain.V(Tag, "LruCache miss for {0}, searching alive objects...",
                    new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure));
                var mapValueRef = _allValues.Get(key);
                if (mapValueRef != null && mapValueRef.TryGetTarget(out mapValue)) {
                    Log.To.NoDomain.V(Tag, "...Alive object {0} found!",
                        new SecureLogString(mapValue, LogMessageSensitivity.PotentiallyInsecure));
                    Put(key, mapValue);
                    return mapValue;
                }

                Log.To.NoDomain.V(Tag, "...No alive object found!");
                _allValues.Remove(key);
            }
            Log.To.NoDomain.D(Tag, "Exited first lock in Get");

            TValue createdValue = Create(key);
            if (createdValue == null) {
                return default(TValue);
            }

            Log.To.NoDomain.V(Tag, "Autocreated default object {0}, inserting into cache",
                new SecureLogString(createdValue, LogMessageSensitivity.PotentiallyInsecure));
            Log.To.NoDomain.D(Tag, "Entering second lock in Get");
            lock (_locker) {
                CreateCount++;
                mapValue = _recents.Get(key);
                _recents[key] = createdValue;
                _nodes.Remove(key);
                _nodes.AddFirst(key);
                if (mapValue != null) {
                    Log.To.NoDomain.W(Tag, "Cache already has a value for {0}, aborting insert", 
                        new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure));
                    // There was a conflict so undo that last put
                    _recents[key] = mapValue;
                }
                else {
                    Size += SafeSizeOf(key, createdValue);
                }
            }
            Log.To.NoDomain.D(Tag, "Exited second lock in Get");

            if (mapValue != null) {
                EntryRemoved(false, key, createdValue, mapValue);
                return mapValue;
            } else {
                Trim();
                return createdValue;
            }
        }

        public TValue Put(TKey key, TValue value)
        {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            Log.To.NoDomain.D(Tag, "Entering lock in Put");
            TValue previous;
            lock (_locker) {
                PutCount++;
                Size += SafeSizeOf(key, value);
                previous = _recents.Get(key);
                _recents[key] = value;
                if (previous != null) {
                    Log.To.NoDomain.W(Tag, "Cache already has a value for {0} ({1}), aborting insert",
                        new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure),
                        new SecureLogString(previous, LogMessageSensitivity.PotentiallyInsecure));
                    Size -= SafeSizeOf(key, previous);
                }
                else {
                    Log.To.NoDomain.V(Tag, "Adding {0} for key {1} into cache",
                        new SecureLogString(value, LogMessageSensitivity.PotentiallyInsecure),
                        new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure));
                    _nodes.AddFirst(key);
                    _allValues[key] = new WeakReference<TValue>(value);
                }
            }
            Log.To.NoDomain.D(Tag, "Exited lock in Put");

            if (previous != null) {
                EntryRemoved(false, key, previous, value);
            }

            Trim();
            return previous;
        }

        public TValue Remove(TKey key)
        {
            if (key == null) {
                Log.To.NoDomain.E(Tag, "key cannot be null in Remove, throwing...");
                throw new ArgumentNullException(nameof(key));
            }

            Log.To.NoDomain.D(Tag, "Entering lock in Remove");
            TValue previous;
            lock (_locker) {
                Log.To.NoDomain.V(Tag, "Attempting to remove {0}...",
                    new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure));
                if (_recents.TryGetValue(key, out previous)) {
                    _recents.Remove(key);
                    Size -= SafeSizeOf(key, previous);
                    _nodes.Remove(key);
                    _allValues.Remove(key);
                    Log.To.NoDomain.V(Tag, "...Success!");
                } else {
                    Log.To.NoDomain.V(Tag, "...Key not found!");
                }
            }

            Log.To.NoDomain.D(Tag, "Exited lock in Remove");

            if (previous != null) {
                EntryRemoved(false, key, previous, default(TValue));
            }

            return previous;
        }

        public virtual void Resize(int maxSize)
        {
            if (maxSize <= 0) {
                Log.To.NoDomain.E(Tag, "maxSize is <= 0 ({0}) in Resize, throwing...", maxSize);
                throw new ArgumentException("maxSize <= 0");
            }

            Log.To.NoDomain.D(Tag, "Entering lock in Resize");
            lock (_locker) {
                MaxSize = maxSize;
            }
            Log.To.NoDomain.D(Tag, "Exited lock in Resize");

            Trim();
        }

        #endregion

        #region Protected Internal Methods

        protected internal virtual TValue Create(TKey key)
        {
            //Return the compiler default value for a new TValue, but can be overriden in subclasses
            return default(TValue);
        }

        protected internal virtual void EntryRemoved(bool evicted, TKey key, TValue oldValue, TValue newValue)
        {
            var disp = oldValue as IDisposable;
            disp?.Dispose();
        }

        protected internal virtual int SizeOf(TKey key, TValue value)
        {
            //The size of an entry in user defined units.  This must not change
            //for any given entry while it is in the cache
            return 1;
        }

        #endregion

        #region Private Methods

        private int SafeSizeOf(TKey key, TValue value)
        {
            int result = SizeOf(key, value);
            if (result < 0) {
                Log.To.NoDomain.E(Tag, "SizeOf reported an invalid size for <{0} / {1}> ({2}), throwing...",
                    new SecureLogString(key, LogMessageSensitivity.PotentiallyInsecure),
                    new SecureLogString(value, LogMessageSensitivity.PotentiallyInsecure),
                    result);
                throw new InvalidOperationException("Invalid size returned by SizeOf in LruCache");
            }

            return result;
        }

        private void Trim()
        {
            while (true) {
                TKey key;
                TValue value;
                Log.To.NoDomain.D(Tag, "Entering lock in Trim");
                lock (_locker) {
                    if (Size < 0 || _recents.Count != Size || _nodes.Count != Size) {
                        Log.To.NoDomain.E(Tag, "Size is reporting inconsistent results (Size={0} CacheCount={1} NodeCount={2}), throwing...",
                            Size, _recents.Count, _nodes.Count);
                        throw new InvalidOperationException(GetType().FullName + "Size is reporting inconsistent results!");
                    }

                    if (Size <= MaxSize || Size == 0) {
                        break;
                    }
                        
                    key = _nodes.Last.Value;
                    value = _recents[key];
                    _recents.Remove(key);
                    _nodes.RemoveLast();
                    Size -= SafeSizeOf(key, value);
                    EvictionCount++;
                }
                Log.To.NoDomain.D(Tag, "Exited lock in Trim");

                EntryRemoved(true, key, value, default(TValue));
            }
        }

        #endregion

        #region Overrides

        public sealed override string ToString()
        {
            Log.To.NoDomain.D(Tag, "Entering lock in ToString");
            lock (_locker) {
                
                var accesses = HitCount + MissCount;
                var hitPercent = accesses != 0 ? (int)Math.Round((100 * (HitCount / (double)accesses))) : 0;

                Log.To.NoDomain.D(Tag, "Exiting lock via return");
                return $"LruCache[maxSize={MaxSize},hits={HitCount},misses={MissCount},hitRate={hitPercent:P}%]";
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Log.To.NoDomain.D(Tag, "Entering lock in Dispose");
            lock(_locker) {
                foreach(var val in _allValues.Values) {
                    TValue foo;
                    if(val.TryGetTarget(out foo)) {
                        var disposable = foo as IDisposable;
                        if(disposable != null) {
                            var threadSafe = foo as IThreadSafe;
                            if(threadSafe != null) {
                                threadSafe.ActionQueue.DispatchSync(() => disposable.Dispose());
                            } else {
                                disposable.Dispose();
                            }
                        }
                    }
                }

                _recents.Clear();
                _allValues.Clear();
                _nodes.Clear();
                Size = 0;
            }
            Log.To.NoDomain.D(Tag, "Exited lock in Dispose");
        }

        #endregion
    }
}
