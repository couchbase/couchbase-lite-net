// 
//  RefCountedDisposable.cs
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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;

using JetBrains.Annotations;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// A utility base class that implements a referenced counted disposable
    /// object.  This object can be retained by calls to <see cref="Retain"/>
    /// and must be Disposed an equal number of times, plus once more to balance
    /// the creation.  Each call to dispose will decrement the reference count and
    /// the final call will perform the disposal
    /// </summary>
    internal abstract class RefCountedDisposable : IDisposable
    {
        #region Variables
        
        private int _refCount;
        private AtomicBool _disposed = false;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        protected RefCountedDisposable()
        {
            _refCount = 1;
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~RefCountedDisposable()
        {
            Dispose(false);
        }

        #endregion

        /// <summary>
        /// Adds one to the reference count of the object, meaning that
        /// another call to <see cref="IDisposable.Dispose"/> is needed to actually
        /// dispose of this object
        /// </summary>
        public void Retain()
        {
            if (_disposed) {
                throw new ObjectDisposedException(GetType().Name);
            }

            Interlocked.Increment(ref _refCount);
        }

        /// <summary>
        /// Retains the object and returns it as a downcasted object
        /// </summary>
        /// <typeparam name="T">The type of object being returned</typeparam>
        /// <returns>The object that was retained</returns>
        [NotNull]
        public T Retain<T>() where T : RefCountedDisposable
        {
            if (_disposed) {
                throw new ObjectDisposedException(GetType().Name);
            }

            Interlocked.Increment(ref _refCount);
            return (T) this;
        }

        #region Protected Methods

        /// <summary>
        /// Performs the actual dispose
        /// </summary>
        /// <param name="disposing">Whether or not this method is being called from
        /// inside <see cref="IDisposable.Dispose"/> (vs the finalizer)</param>
        protected abstract void Dispose(bool disposing);

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            var refCount = Interlocked.Decrement(ref _refCount);
            if (refCount == 0 && !_disposed.Set(true)) {
                GC.SuppressFinalize(this);
                Dispose(true);
            }
        }

        #endregion
    }
}