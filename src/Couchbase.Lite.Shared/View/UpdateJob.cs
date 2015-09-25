﻿//
//  UpdateJob.cs
//
//  Author:
//      Jim Borden  <jim.borden@couchbase.com>
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

namespace Couchbase.Lite.Internal
{
    internal sealed class UpdateJob
    {
        private readonly Func<IList<View>, Status> _logic;
        private readonly IEnumerable<View> _args;
        private Task<Status> _task;
        public readonly long[] LastSequences;

        public Status Result 
        {
            get {
                return _task.IsCompleted ? _task.Result : new Status(StatusCode.Unknown);
            }
        }

        private EventHandler _finished;
        public event EventHandler Finished
        {
            add { _finished = (EventHandler)Delegate.Combine(_finished, value); }
            remove { _finished = (EventHandler)Delegate.Remove(_finished, value); }
        }

        public UpdateJob(Func<IList<View>, Status> logic, IEnumerable<View> args, IEnumerable<long> lastSequences)
        {
            _logic = logic;
            _args = args;
            LastSequences = lastSequences.ToArray();
        }

        public void Run()
        {
            if (_task == null) {
                _task = Task.Factory.StartNew<Status>(() => _logic(_args.ToList()));
                _task.ContinueWith(t =>
                {
                    if(_finished != null) {
                        _finished(this, null);
                    }
                });
            }
        }

        public void Wait()
        {
            _task.Wait();
        }
    }
}
