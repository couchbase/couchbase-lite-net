using System;
using System.Collections.Generic; 
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;


namespace Couchbase.Lite.Util
{
    sealed class SingleThreadTaskScheduler : TaskScheduler 
    {
        const string Tag = "SingleThreadTaskScheduler";

 
        private readonly BlockingCollection<Task> queue = new BlockingCollection<Task>(new ConcurrentQueue<Task>());
        private const int maxConcurrency = 1;
        private int runningTasks = 0;
        private int longRunningTasks = 0;

        /// <summary>Queues a task to the scheduler.</summary> 
        /// <param name="task">The task to be queued.</param> 
        protected override void QueueTask(Task task) 
        {
            // Long-running tasks can deadlock us easily.
            // We want to allow these to run without doing that.
            if (task.CreationOptions.HasFlag(TaskCreationOptions.LongRunning))
            {
                Interlocked.Increment(ref longRunningTasks);
            }
            queue.Add (task); 
            if ((runningTasks - longRunningTasks) < maxConcurrency)
            {
                Interlocked.Increment(ref runningTasks);
                QueueThreadPoolWorkItem (); 
            }
        } 

        private void QueueThreadPoolWorkItem() 
        { 
            ThreadPool.QueueUserWorkItem(s => 
            { 
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
                        // Remove our temporary easing of max concurrency limit.
                        if (task.CreationOptions.HasFlag(TaskCreationOptions.LongRunning))
                        {
                            Interlocked.Decrement(ref longRunningTasks);
                        }
                        if (!success && (task.Status != TaskStatus.Canceled || task.Status != TaskStatus.RanToCompletion))
                            Log.E(Tag, "Scheduled task faulted", task.Exception);
                    } 
                }
                catch (Exception e)
                {
                    Log.E(Tag, "Unhandled exception in runloop", e.ToString());
                    throw;
                }
            }, null);
        } 

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) 
        {
            Log.D(Tag, "Executing task inline.");
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