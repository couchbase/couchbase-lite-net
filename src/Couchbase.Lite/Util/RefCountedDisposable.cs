// 
//  RefCountedDisposable.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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

using System;
using System.Threading;

namespace Couchbase.Lite.Util
{
    public abstract class RefCountedDisposable : IDisposable
    {
        #region Variables
        
        private int _refCount;

        #endregion

        #region Constructors

        public RefCountedDisposable()
        {
            _refCount = 1;
        }

        ~RefCountedDisposable()
        {
            Dispose(false);
        }

        #endregion

        public void Retain()
        {
            Interlocked.Increment(ref _refCount);
        }

        public T Retain<T>() where T : RefCountedDisposable
        {
            Interlocked.Increment(ref _refCount);
            return (T) this;
        }

        #region Protected Methods

        protected abstract void Dispose(bool disposing);

        #endregion

        #region IDisposable

        public void Dispose()
        {
            var refCount = Interlocked.Decrement(ref _refCount);
            if (refCount == 0) {
                GC.SuppressFinalize(this);
                Dispose(true);
            } else if (refCount < 0) {
                throw new InvalidOperationException("Dispose called too many times!");
            }
        }

        #endregion
    }
}