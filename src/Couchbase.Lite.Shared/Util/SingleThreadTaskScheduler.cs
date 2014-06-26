﻿using System;
using System.Collections.Generic; 
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading; 


namespace Couchbase.Lite.Util
{
    sealed class SingleThreadTaskScheduler : TaskScheduler 
    { 
        [ThreadStatic] 
        private static bool allowInlining; 
        private readonly BlockingCollection<Task> queue = new BlockingCollection<Task>(new ConcurrentQueue<Task>());
        private const int maxConcurrency = 1;
        private int runningTasks = 0;

        /// <summary>Queues a task to the scheduler.</summary> 
        /// <param name="task">The task to be queued.</param> 
        protected override void QueueTask(Task task) 
        { 
            queue.Add (task); 
            if (runningTasks < maxConcurrency)
            {
                ++runningTasks; 
                QueueThreadPoolWorkItem (); 
            }
        } 

        private void QueueThreadPoolWorkItem() 
        { 
            ThreadPool.UnsafeQueueUserWorkItem(s => 
                { 
                    allowInlining = true; 
                    try 
                    { 
                        while (true) 
                        { 
                            Task task; 
                            if (queue.Count == 0) 
                            { 
                                --runningTasks; 
                                break; 
                            } 

                            task = queue.Take(); 
                            var success = TryExecuteTask(task);
                            if (!success && task.Status != TaskStatus.Canceled && task.Status != TaskStatus.RanToCompletion)
                                Trace.TraceError("Scheduled task failed to execute.", task.Exception.ToString());
                        } 
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Unhandled exception in runloop", e.ToString());
                        throw;
                    }
                    finally 
                    {
                        allowInlining = false;
                    } 
                }, null);
        } 

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) 
        { 
            if (!allowInlining)
                return false; 

            if (taskWasPreviouslyQueued)
                TryDequeue(task); 

            return TryExecuteTask(task); 
        } 

        protected override bool TryDequeue(Task task) 
        {
            // Our concurrent collection does not let
            // use efficiently re-order the queue,
            // so we won't try to.
            return false;
        } 

        public override int MaximumConcurrencyLevel { 
            get { 
                return maxConcurrency; 
            } 
        } 
        protected override IEnumerable<Task> GetScheduledTasks() 
        { 
            return queue.ToArray(); 
        } 
    } 
}