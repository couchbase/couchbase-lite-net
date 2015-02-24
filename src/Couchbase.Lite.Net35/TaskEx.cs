//
//  TaskEx.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Threading;

namespace Couchbase.Lite
{
    internal class TaskEx
    {
        static int CheckTimeout (TimeSpan timeout)
        {
            try {
                return checked ((int)timeout.TotalMilliseconds);
            } catch (System.OverflowException) {
                throw new ArgumentOutOfRangeException ("timeout");
            }
        }

        public static Task Delay (int millisecondsDelay)
        {
            return Delay (millisecondsDelay, CancellationToken.None);
        }

        public static Task Delay (TimeSpan delay)
        {
            return Delay (CheckTimeout (delay), CancellationToken.None);
        }

        public static Task Delay (TimeSpan delay, CancellationToken cancellationToken)
        {
            return Delay (CheckTimeout (delay), cancellationToken);
        }

        public static Task Delay (int millisecondsDelay, CancellationToken cancellationToken)
        {
            if (millisecondsDelay < -1)
                throw new ArgumentOutOfRangeException ("millisecondsDelay");

            var tcs = new TaskCompletionSource<bool>();

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            //            var task = new Task (TaskActionInvoker.Empty, null, cancellationToken, TaskCreationOptions.None, null, null);
            //            task.SetupScheduler (TaskScheduler.Default);

            var task = tcs.Task;

            if (millisecondsDelay != Timeout.Infinite) {
                var timer = new Timer (delegate (object state) {
                    var t = (Task) state;
                    //                    if (t.Status == TaskStatus.WaitingForActivation) {
                    //                        t.Status = TaskStatus.Running;
                    //                    }
                    tcs.SetResult(true);
                }, task, millisecondsDelay, -1);
                //
                task.ContinueWith ((t) => timer.Dispose());
            }

            return task;
        }
    }
}

