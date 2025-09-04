// 
//  MainThreadTaskScheduler.cs
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

using Android.Content;
using Android.OS;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using LiteCore.Interop;

namespace Couchbase.Lite.Support;

internal sealed class MainThreadTaskScheduler : TaskScheduler, IMainThreadTaskScheduler
{
    private const string Tag = nameof(MainThreadTaskScheduler);

    private readonly Handler _handler;

    public bool IsMainThread => Looper.MainLooper == Looper.MyLooper();

    public MainThreadTaskScheduler(Context context)
    {
        if(context.MainLooper == null) {
            throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError,
                "Context Main Looper is null, cannot create MainTaskScheduler!");
        }

        _handler = new Handler(context.MainLooper);
    }

    protected override IEnumerable<Task> GetScheduledTasks() => throw new NotSupportedException();

    protected override void QueueTask(Task task)
    {
        _handler.Post(() =>
        {
            if (!TryExecuteTask(task)) {
                WriteLog.To.Database.W(Tag, "Failed to execute a task in MainThreadTaskScheduler");
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

    public TaskScheduler AsTaskScheduler() => this;
}
