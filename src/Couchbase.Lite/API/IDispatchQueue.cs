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
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface describing an operation queue that can execute arbitrary
    /// work items in a synchronous or asynchronous fashion
    /// </summary>
    public interface IDispatchQueue
    {
        #region Public Methods

        /// <summary>
        /// Schedules the given <see cref="Action"/>
        /// </summary>
        /// <param name="a">The <see cref="Action"/> to schedule</param>
        /// <returns>A <see cref="Task"/> object representing the scheduled <see cref="Action"/></returns>
        Task DispatchAsync(Action a);

        /// <summary>
        /// Schedules the given <see cref="Func{T}"/>
        /// </summary>
        /// <typeparam name="TResult">The retuyrn type of the operation</typeparam>
        /// <param name="f">The <see cref="Func{T}"/> to schedule</param>
        /// <returns>A <see cref="Task{TResult}"/> object representing the scheduled <see cref="Func{T}"/></returns>
        Task<TResult> DispatchAsync<TResult>(Func<TResult> f);

        /// <summary>
        /// Scheduled the given <see cref="Action"/> and waits for it to complete
        /// </summary>
        /// <param name="a">The  <see cref="Action"/> to schedule</param>
        void DispatchSync(Action a);

        /// <summary>
        /// Schedules the given <see cref="Func{T}"/>, waits for it to complete
        /// and returns the result of the action
        /// </summary>
        /// <typeparam name="TResult">The return type of the operation</typeparam>
        /// <param name="f">The <see cref="Func{T}"/> to schedule</param>
        /// <returns>The result of the <see cref="Func{T}"/></returns>
        TResult DispatchSync<TResult>(Func<TResult> f);

        #endregion
    }

    /// <summary>
    /// A class containing common IDispatchQueue operations
    /// </summary>
    public static class DispatchQueueExtensions
    {
        #region Public Methods

        /// <summary>
        /// Schedules the given <see cref="Action"/> after waiting 
        /// the given amount of time
        /// </summary>
        /// <param name="queue">The queue to operate on</param>
        /// <param name="a">The <see cref="Action"/> to schedule</param>
        /// <param name="time">The delay to use before scheduling</param>
        /// <returns>A <see cref="Task"/> object representing the scheduled <see cref="Action"/></returns>
        public static Task DispatchAfter(this IDispatchQueue queue, Action a, TimeSpan time)
        {
            return Task.Delay(time).ContinueWith(t => queue.DispatchSync(a));
        }

        /// <summary>
        /// Schedules the given <see cref="Func{TResult}"/> after waiting 
        /// the given amount of time
        /// </summary>
        /// <typeparam name="TResult">The return type of the operation</typeparam>
        /// <param name="queue">The queue to oeprate on</param>
        /// <param name="f">The <see cref="Func{T}"/> to schedule</param>
        /// <param name="time">The delay to use before scheduling</param>
        /// <returns>A <see cref="Task{TResult}"/> object representing the scheduled <see cref="Func{T}"/></returns>
        public static Task<TResult> DispatchAfter<TResult>(this IDispatchQueue queue, Func<TResult> f, TimeSpan time)
        {
            return Task.Delay(time).ContinueWith(t => queue.DispatchSync(f));
        }

        #endregion
    }
}
