//
// UniqueList.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Lite.Util
{
    internal sealed class UniqueQueue<T> : IDisposable
    {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly HashSet<T> _used = new HashSet<T>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public int Count
        {
            get {
                _lock.EnterReadLock();
                try {
                    return _queue.Count;
                } finally {
                    _lock.ExitReadLock();
                }
            }
        }

        public UniqueQueue()
        {
        }

        public void Enqueue(T obj)
        {
            _lock.EnterWriteLock();
            try {
                if(_used.Contains(obj)) {
                    return;
                }

                _used.Add(obj);
                _queue.Enqueue(obj);
            } finally {
                _lock.ExitWriteLock();
            }
        }

        public T Dequeue()
        {
            _lock.EnterWriteLock();
            try {
                var retVal = _queue.Dequeue();
                _used.Remove(retVal);
                return retVal;
            } finally {
                _lock.ExitWriteLock();
            }
        }

        public bool TryDequeue(out T obj)
        {
            _lock.EnterWriteLock();
            try {
                obj = default(T);
                if(_queue.Count == 0) {
                    return false;
                }

                obj = _queue.Dequeue();
                _used.Remove(obj);
                return true;
            } finally {
                _lock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
