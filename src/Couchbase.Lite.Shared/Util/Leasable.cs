//  Leasable.cs
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
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Lite.Internal
{
    // This class is used to mitigate race conditions between checking for a valid state
    // of a variable and then using it.  It's basically reference counting for .NET objects,
    // except releasing to zero only disposes if a dispose flag has been set.
    internal sealed class Leasable<T> : IDisposable where T : class
    {
        public static readonly TimeSpan DefaultLeasePeriod = TimeSpan.FromMilliseconds(200);
        private int _leaseCount;
        private T _value;

        public bool Disposed
        {
            get; private set;
        }

        public Leasable(T value)
        {
            _value = value;
        }

        // Guarantees that the resource will remain usable throughout the given action
        // (if not usable, action will not run)
        public void Borrow(Action<T> action)
        {
            var tmp = default(T);
            if(!Acquire(out tmp)) {
                return;
            }

            try {
                action(tmp);
            } finally {
                Release();
            }
        }

        // Guarantees that the resource will remain usable throughout the given action
        // so that it may return a value.  Returns a default value if resource is unusable
        public V Borrow<V>(Func<T, V> action, V defaultVal = default(V))
        {
            var tmp = default(T);
            if(!Acquire(out tmp)) {
                return defaultVal;
            }

            try {
                return action(tmp);
            } finally {
                Release();
            }
        }

        // Acquire the resource and dispose it in one step.  This acts as the final
        // use of the resource (for cleanup, etc)
        public bool AcquireAndDispose(out T outValue)
        {
            outValue = default(T);
            if(Disposed) {
                return false;
            }

            
            var retVal = AcquireInternal(out outValue);
            Dispose();
            return retVal;
        }

        // Attempts to acquire the resource (must be balanced with a call to Release())
        public bool Acquire(out T outValue)
        {
            outValue = default(T);
            if(Disposed) {
                return false;
            }

            return AcquireInternal(out outValue);
        }

        // Acquire the resource and automatically release it later
        public bool AcquireFor(TimeSpan length, out T outValue)
        {
            if(!Acquire(out outValue)) {
                return false;
            }

            Task.Delay(length).ContinueWith(t => Release());
            return true;
        }

        // Acquire the resource and automatically release it after the default
        // lease period
        public bool AcquireTemp(out T outValue)
        {
            return AcquireFor(DefaultLeasePeriod, out outValue);
        }

        // Release the resource
        public void Release()
        {
            var newVal = Interlocked.Decrement(ref _leaseCount);
            if(newVal == 0 && Disposed) {
                DisposeInternal();
            }
        }

        private bool AcquireInternal(out T outValue)
        {
            Interlocked.Increment(ref _leaseCount);
            outValue = _value;
            return true;
        }

        private void DisposeInternal()
        {
            var disposable = Interlocked.Exchange<T>(ref _value, default(T)) as IDisposable;
            disposable?.Dispose();
        }

        public static implicit operator Leasable<T>(T input)
        {
            return new Leasable<T>(input);
        }

        public void Dispose()
        {
            if(Disposed) {
                return;
            }

            Disposed = true;
            if(_leaseCount == 0) {
                DisposeInternal();
            }
        }
    }
}
