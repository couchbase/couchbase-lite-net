//
//  UpdateJob.cs
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
using System.Threading;
using System.Collections.Generic;
using Couchbase.Lite.Store;
using System.Threading.Tasks;
using System.Linq;

namespace Couchbase.Lite
{
    internal sealed class UpdateJob
    {
        private readonly Func<IList<IViewStore>, bool> _logic;
        private readonly IEnumerable<IViewStore> _args;
        private Task<bool> _task;
        public readonly long[] LastSequences;

        public Status Result 
        {
            get {
                if (_task.IsFaulted) {
                    var ce = _task.Exception.InnerException as CouchbaseLiteException;
                    if (ce != null) {
                        return ce.CBLStatus;
                    }

                    return new Status(StatusCode.Exception);
                }

                if (_task.IsCompleted) {
                    return _task.Result ? new Status(StatusCode.Ok) : new Status(StatusCode.DbError);
                }

                return new Status(StatusCode.Unknown);
            }
        }

        public event EventHandler Finished;

        public UpdateJob(Func<IList<IViewStore>, bool> logic, IEnumerable<IViewStore> args, IEnumerable<long> lastSequences)
        {
            _logic = logic;
            _args = args;
            LastSequences = lastSequences.ToArray();
            _task = new Task<bool>(() => _logic(_args.ToList()));
        }

        public void Run()
        {
            if (_task.Status <= TaskStatus.Running) {
                _task.Start(TaskScheduler.Default);
                _task.ContinueWith(t =>
                {
                    if(Finished != null) {
                        Finished(this, null);
                    }
                }, TaskScheduler.Default);
            }
        }

        public void Wait()
        {
            _task.Wait();
        }
    }
}

