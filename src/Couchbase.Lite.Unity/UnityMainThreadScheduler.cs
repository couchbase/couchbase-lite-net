//
//  UnityMainThreadScheduler.cs
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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Util;

using UnityEngine;

namespace Couchbase.Lite.Unity
{

    /// <summary>
    /// A convenience class for running actions on the Unity3D main thread.
    /// </summary>
    /// <description>
    /// Many Unity3D methods are disallowed from outside the main thread, and
    /// this causes many headaches when trying to do asynchronous logic.  With
    /// this class it becomes easy to get things back on track.  Just call into
    /// this class and don't worry about "can only be called from the main thread"
    /// errors anymore.
    /// </description>
    /// <remarks>
    /// You must attach an instance of this class to an object in the scene in the
    /// Unity editor for it to perform correctly.
    /// </remarks>
    public class UnityMainThreadScheduler : MonoBehaviour
    {
        #region Private Variables

        private static readonly BlockingCollection<Task> _jobQueue = new BlockingCollection<Task>();

        #endregion

        #region Properties

        public static string PersistentDataPath { get; private set; }

        /// <summary>
        /// The task scheduler for scheduling actions to run on the Unity3D
        /// main thread
        /// </summary>
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
        private static SingleThreadScheduler _taskScheduler;

        /// <summary>
        /// The task factory for scheduling actions to run on the main
        /// Unity3D thread
        /// </summary>
        /// <value>The task factory.</value>
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
        private static TaskFactory _taskFactory;

        #endregion

        #region Unity messages

        void OnEnable()
        {
            if (_taskScheduler != null)
            {
                return;
            }

            _taskScheduler = new SingleThreadScheduler(Thread.CurrentThread, _jobQueue);
            PersistentDataPath = Application.persistentDataPath;
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

        #endregion
    }
}