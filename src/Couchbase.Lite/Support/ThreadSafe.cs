// 
// ThreadSafe.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Threading;

namespace Couchbase.Lite.Support
{
    internal sealed class ThreadSafety
    {
        #region Variables

        private readonly ReaderWriterLockSlim _lock;

        #endregion

        #region Constructors

        public ThreadSafety(bool recursive)
        {
            _lock = new ReaderWriterLockSlim(recursive
                ? LockRecursionPolicy.SupportsRecursion
                : LockRecursionPolicy.NoRecursion);
        }

        #endregion

        #region Public Methods

        public void LockedForRead(Action a)
        {
            bool tookLock = false;
            if (!_lock.IsUpgradeableReadLockHeld && !_lock.IsWriteLockHeld) {
                tookLock = true;
                _lock.EnterReadLock();
            }

            try {
                a();
            } finally {
                if (tookLock) {
                    _lock.ExitReadLock();
                }
            }
        }

        public void LockedForWrite(Action a)
        {
            _lock.EnterWriteLock();
            try {
                a();
            } finally {
                _lock.ExitWriteLock();
            }
        }

        public void LockedForPossibleWrite(Action a)
        {
            bool tookLock = false;
            if (!_lock.IsWriteLockHeld) {
                tookLock = true;
                _lock.EnterUpgradeableReadLock();
            }
            try {
                a();
            }
            finally {
                if (tookLock) {
                    _lock.ExitUpgradeableReadLock();
                }
            }
        }

        public T LockedForRead<T>(Func<T> f)
        {
            bool tookLock = false;
            if (!_lock.IsUpgradeableReadLockHeld && !_lock.IsWriteLockHeld) {
                tookLock = true;
                _lock.EnterReadLock();
            }

            try {
                return f();
            }
            finally {
                if (tookLock) {
                    _lock.ExitReadLock();
                }
            }
        }

        public T LockedForWrite<T>(Func<T> f)
        {
            _lock.EnterWriteLock();
            try {
                return f();
            }
            finally {
                _lock.ExitWriteLock();
            }
        }

        public T LockedForPossibleWrite<T>(Func<T> f)
        {
            _lock.EnterUpgradeableReadLock();
            try {
                return f();
            }
            finally {
                _lock.ExitUpgradeableReadLock();
            }
        }

        #endregion
    }
}
