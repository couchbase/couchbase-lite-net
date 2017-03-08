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
using System.Diagnostics;
using System.Threading;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    internal abstract class ThreadSafe : IThreadSafe
    {
        #region Constants

        private const string Tag = nameof(ThreadSafe);

        #endregion

        #region Variables

        private readonly int _owner;
        private SerialQueue _serialQueue;

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IDispatchQueue ActionQueue
        {
            get {
                return _serialQueue ?? (_serialQueue = new SerialQueue());
            }
            internal set {
                if (_serialQueue != null) {
                    throw new InvalidOperationException("Cannot reset the queue of a thread safe object");
                }

                _serialQueue = value as SerialQueue;
            }
        }

        public IDispatchQueue CallbackQueue { get; set; } = new ConcurrentQueue();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal SerialQueue ActionQueue_Internal
        {
            get {
                return _serialQueue;
            }
        }

        #endregion

        #region Constructors

        protected ThreadSafe()
        {
            _owner = Environment.CurrentManagedThreadId;
        }

        #endregion

        #region Protected Methods

        protected void AssertSafety()
        {
            if (_serialQueue != null) {
                _serialQueue.AssertInQueue();
            } else {
                if (_owner != Environment.CurrentManagedThreadId) {
                    if (Debugger.IsAttached) {
                        Debugger.Break();
                    } else {
                        Log.To.Database.E(Tag, $"Thread safety violation at {Environment.NewLine}{Environment.StackTrace}");
                        throw new ThreadSafetyViolationException(false);
                    }
                }
            }
        }

        #endregion
    }
}
