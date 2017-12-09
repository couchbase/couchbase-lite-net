// 
//  MainThreadTaskScheduler.cs
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
using System.Collections.Generic;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;

using Foundation;

namespace Couchbase.Lite.Support
{
    [CouchbaseDependency(Lazy = true, Transient = true)]
    public class MainThreadTaskScheduler : TaskScheduler, IMainThreadTaskScheduler
    {
        #region Constants

        private const string Tag = nameof(MainThreadTaskScheduler);

        #endregion

        #region Properties

        public bool IsMainThread => NSThread.IsMain;

        #endregion

        #region Overrides

        protected override void QueueTask(Task task)
        {
            NSRunLoop.Main.BeginInvokeOnMainThread(() =>
            {
                if (!TryExecuteTask(task)) {
                    Log.To.Couchbase.W(Tag, "Failed to execute task");
                }
            });
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (taskWasPreviouslyQueued || !IsMainThread) {
                return false;
            }

            return TryExecuteTask(task);
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            throw new NotSupportedException();
        }

        #endregion

        #region IMainThreadTaskScheduler

        public TaskScheduler AsTaskScheduler() => this;

        #endregion
    }
}