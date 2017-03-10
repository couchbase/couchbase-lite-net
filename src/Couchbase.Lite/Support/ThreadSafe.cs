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
using System.Threading.Tasks;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    internal abstract class ThreadSafe : IThreadSafe
    {
        #region Variables

        private SerialQueue _serialQueue = new SerialQueue();

        #endregion

        #region Properties

        public IDispatchQueue ActionQueue
        {
            get {
                return _serialQueue;
            }
            set {
                _serialQueue = value as SerialQueue;
            }
        }

        internal bool CheckThreadSafety
        {
            get; set;
        }

        #endregion

        #region Protected Methods

        protected void AssertSafety()
        {
            if (!CheckThreadSafety) {
                return;
            }

            _serialQueue.AssertInQueue();
        }

        #endregion

        #region IThreadSafe

        public Task DoAsync(Action a)
        {
            return _serialQueue.DispatchAsync(a);
        }

        public Task<T> DoAsync<T>(Func<T> f)
        {
            return _serialQueue.DispatchAsync(f);
        }

        public void DoSync(Action a)
        {
            _serialQueue.DispatchSync(a);
        }

        public T DoSync<T>(Func<T> f)
        {
            return _serialQueue.DispatchSync(f);
        }

        #endregion
    }
}
