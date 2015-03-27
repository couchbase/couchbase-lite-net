//
//  UnityMainThreadScheduler.cs
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
using System.Threading;
using Couchbase.Lite.Util;
using UnityEngine;
using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;

namespace Couchbase.Lite.Unity
{
    public class UnityMainThreadScheduler : MonoBehaviour
    {
        private static readonly BlockingCollection<Task> _jobQueue = new BlockingCollection<Task>();

        private static SingleThreadScheduler _taskScheduler;
        public static TaskScheduler TaskScheduler
        {
            get
            {
                if (_taskScheduler == null)
                {
                    Debug.LogError("UnityMainThreadScheduler must be initialized from the main thread by attaching it on" +
                                   "to an object in the scene");
                    throw new InvalidOperationException();
                }

                return _taskScheduler;
            }
        }

        private static TaskFactory _taskFactory;
        public static TaskFactory TaskFactory
        {
            get
            {
                if (_taskFactory != null)
                {
                    return _taskFactory;
                }

                if (_taskScheduler == null)
                {
                    Debug.LogError("UnityMainThreadScheduler must be initialized from the main thread by attaching it on" +
                                   "to an object in the scene");
                    throw new InvalidOperationException();
                }

                _taskFactory = new TaskFactory(_taskScheduler);
                return _taskFactory;
            }
        }

        void OnEnable()
        {
            if (_taskScheduler != null)
            {
                return;
            }

            _taskScheduler = new SingleThreadScheduler(Thread.CurrentThread, _jobQueue);
        }

        void FixedUpdate()
        {
            Task nextTask;
            bool gotTask = _jobQueue.TryTake(out nextTask);
            if (gotTask && nextTask.Status == TaskStatus.WaitingToRun)
            {
                _taskScheduler.TryExecuteTaskHack(nextTask);
            }
        }
    }
}