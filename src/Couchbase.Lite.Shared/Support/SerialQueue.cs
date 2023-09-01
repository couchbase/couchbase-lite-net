// 
// SerialQueue.cs
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
using System.Threading.Tasks;

namespace Dispatch
{
    /// <summary>Useful extension methods for queues</summary>
    public static class IDispatchQueueExtensions
    {
        public static Task<T> DispatchAfter<T>(this IDispatchQueue queue, TimeSpan dueTime, Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            queue.DispatchAfter(dueTime, () => {
                try {
                    tcs.SetResult(func());
                } catch (Exception e) {
                    tcs.TrySetException(e);
                }
            });
            return tcs.Task;
        }
    }
}