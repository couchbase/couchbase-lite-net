using System;
using System.Collections.Generic; 
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Reflection;
using Couchbase.Lite.Shared;


namespace Couchbase.Lite.Util
{
    sealed internal class SingleThreadScheduler : TaskScheduler, IDisposable
    {
        private const string Tag = "SingleThreadScheduler";
        private bool _isRunning;
        private PrivateThreadSynchronizationContext _syncContext;

        internal ConcurrentDictionary<Int32, Task> Queue
        {
            get;
            private set;
        }

        public SingleThreadScheduler()
        {
            Queue = new ConcurrentDictionary<Int32, Task>();
            _syncContext = new PrivateThreadSynchronizationContext("Database Thread");
            _isRunning = true;
        }

        /// <summary>Queues a task to the scheduler.</summary> 
        /// <param name="task">The task to be queued.</param> 
        protected override void QueueTask(Task task) 
        {
            if (!_isRunning)
            {
                Log.D(Tag, "Currently draining task queue. Ignoring task {0}", task.Id);
                return;
            }
            Queue[task.Id] = task;
            _syncContext.Post(RunTaskInPrivateThread, task);
        } 

        private void RunTaskInPrivateThread(object state)
        {
            var task = (Task)state;
            var success = TryExecuteTask(task);
            if (!success && (task.Status != TaskStatus.Canceled && task.Status != TaskStatus.RanToCompletion))
            {
                Log.E(Tag, "Scheduled task faulted", task.Exception);
            }

            Queue.TryRemove(task.Id, out task);
        }

        #region IDisposable implementation

        public void Dispose()
        {
            _isRunning = false;
            Task.WaitAll(Queue.Values.ToArray());
            _syncContext.Dispose();
        }

        #endregion

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) 
        {
            return false;

            if (task.Status == TaskStatus.Running)
            {
                return false;
            }
            Log.D(Tag, "Executing task inline.");
            if (taskWasPreviouslyQueued) 
            {
                Log.D(Tag, "Task was previously Queued, so expect it to error out later.");
                TryDequeue(task); 
            }

            var success = false;
            QueueTask(task);
            try 
            {
                task.Wait();
                success = true;
            }
            catch (Exception e)
            {
                Log.E(Tag, "Failed to execute task inline", e);
            }
            return success;
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
                return 1; 
            } 
        } 

        protected override IEnumerable<Task> GetScheduledTasks() 
        { 
            return Queue.Values.AsEnumerable();       
        }
    } 
}