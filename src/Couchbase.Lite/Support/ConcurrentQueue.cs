//
//  ConcurrentQueue.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using System.Threading.Tasks;

namespace Couchbase.Lite.Support
{
    internal sealed class ConcurrentQueue : IDispatchQueue
    {
        #region IDispatchQueue

        public Task DispatchAsync(Action a)
        {
            return Task.Factory.StartNew(a);
        }

        public Task<T> DispatchAsync<T>(Func<T> f)
        {
            return Task.Factory.StartNew(f);
        }

        public void DispatchSync(Action a)
        {
            Task.Factory.StartNew(a).Wait();
        }

        public T DispatchSync<T>(Func<T> f)
        {
            return Task.Factory.StartNew(f).Result;
        }

        #endregion
    }
}
