// 
//  NativeWrapper.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LiteCore.Interop;

internal abstract class NativeWrapper : IDisposable
{
    protected readonly IntPtr _nativeInstance;
    private int _refCount;
    private AtomicBool _disposed = false;

    public ThreadSafety InstanceSafety { get; }

    public NativeWrapper(IntPtr instance)
    {
        _nativeInstance = instance;
        InstanceSafety = new();
    }

    public NativeWrapper(IntPtr instance, ThreadSafety instanceSafety)
    {
        _nativeInstance = instance;
        InstanceSafety = instanceSafety;
    }

    ~NativeWrapper()
    {
        // int is safe to access here
        var refCount = Interlocked.Decrement(ref _refCount);
        if (refCount == 0) {
            Dispose(disposing: false);
        }
    }

    protected IDisposable BeginLockedScope(bool withInstanceLock, params ThreadSafety[] additionalSafeties)
    {
        if(_disposed) {
            throw new ObjectDisposedException(GetType().Name);
        }

        var initial = withInstanceLock ? Enumerable.Repeat(InstanceSafety, 1) : Enumerable.Empty<ThreadSafety>();
        return new ScopeExit(initial.Concat(additionalSafeties));
    }

    private sealed class ScopeExit : IDisposable
    {
        private readonly List<IDisposable> _exits;

        public ScopeExit(IEnumerable<ThreadSafety> safeties)
        {
            var exits = safeties.Select(x => x.BeginLockedScope());
            _exits = exits.ToList();
            _exits.Reverse();
        }

        public void Dispose()
        {
            foreach(var exit in _exits) {
                exit.Dispose();
            }
        }
    }

    public void Retain()
    {
        if (_disposed) {
            throw new ObjectDisposedException(GetType().Name);
        }

        Interlocked.Increment(ref _refCount);
    }

    public T Retain<T>() where T : NativeWrapper
    {
        if (_disposed) {
            throw new ObjectDisposedException(GetType().Name);
        }

        Interlocked.Increment(ref _refCount);
        return (T)this;
    }

    public void Dispose()
    {
        var refCount = Interlocked.Decrement(ref _refCount);
        if (refCount == 0 && !_disposed.Set(true)) {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
    }

    protected abstract void Dispose(bool disposing);
}