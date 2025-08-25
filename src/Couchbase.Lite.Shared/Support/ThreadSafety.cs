// 
// ThreadSafety.cs
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

#if NET9_0_OR_GREATER
using LockType = System.Threading.Lock;
#else
using System.Threading;
using LockType = object;
#endif

namespace Couchbase.Lite.Support;

// TODO: After .NET 8.0 is EOL, switch to use System.Threading.Lock always
internal sealed class ThreadSafety : IThreadSafety
{
    private readonly LockType _lock = new();

    public IDisposable BeginLockedScope()
    {
#if !NO_THREADSAFE
#if NET9_0_OR_GREATER
        const bool lockTaken = true;
        _lock.Enter();
#else
        var lockTaken = false;
        Monitor.Enter(_lock, ref lockTaken);
#endif
#endif
        return new ScopeExit(_lock, lockTaken);
    }

    private sealed class ScopeExit(LockType locker, bool mustUnlock) : IDisposable
    {
        private bool _mustUnlock = mustUnlock;

        public void Dispose()
        {
#if !NO_THREADSAFE
            if (!_mustUnlock) {
                return;
            }
            
#if NET9_0_OR_GREATER
            locker.Exit();
#else
            Monitor.Exit(locker);
#endif
            _mustUnlock = false;
#endif
        }
    }
}