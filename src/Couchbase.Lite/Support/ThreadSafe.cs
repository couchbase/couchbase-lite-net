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

namespace Couchbase.Lite.Support
{
    internal sealed class ThreadSafety
    {
        #region Variables

        private readonly object _lock = new object();

        #endregion

        #region Public Methods

        public void DoLocked(Action a)
        {
#if !NO_THREADSAFE
            lock (_lock) {
#endif
                a();
#if !NO_THREADSAFE
            }
#endif
        }

        public T DoLocked<T>(Func<T> f)
        {
#if !NO_THREADSAFE
            lock (_lock) {
#endif
                return f();
#if !NO_THREADSAFE
            }
#endif
        }

        #endregion
    }
}
