//
//  IDispatchQueue.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed class ThreadSafetyViolationException : Exception
    {
        internal ThreadSafetyViolationException() : base("An attempt was made to use a thread safe object outside of its action queue")
        {

        }
    }

    public interface IDispatchQueue
    {
        void DispatchSync(Action a);

        Task DispatchAsync(Action a);

        T DispatchSync<T>(Func<T> f);

        Task<T> DispatchAsync<T>(Func<T> f);
    }
}
