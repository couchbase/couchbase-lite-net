using System;
using System.Collections.Generic; 
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace Couchbase.Lite.Util
{
    sealed internal class SingleThreadScheduler : TaskScheduler, IDisposable
    {
        private const string Tag = "SingleThreadScheduler";
        private readonly LinkedList<Task> _jobQueue;
        private readonly Thread _thread;
        private bool _disposed;
        private ManualResetEventSlim _mre = new ManualResetEventSlim();

        public SingleThreadScheduler()
        {
            Log.To.TaskScheduling.I(Tag, "New single thread task scheduler created with private thread");
            _jobQueue = new LinkedList<Task>();
            _thread = new Thread(Run) 
            {
                Name = "Database Thread",
                IsBackground = true
            };
            _thread.Start();
        }

        public SingleThreadScheduler(Thread thread, LinkedList<Task> jobQueue)
        {
            if (thread == null)
            {
                throw new ArgumentNullException("thread");
            }

            if(jobQueue == null)
            {
                throw new ArgumentNullException("jobQueue");
            }

            Log.To.TaskScheduling.I(Tag, "New single thread task scheduler created with specified thread {0}",
                thread);
            _thread = thread;
            _jobQueue = jobQueue;
        }

        internal void TryExecuteTaskHack(Task task)
        {
            TryExecuteTask(task);
        }

        /// <summary>Queues a task to the scheduler.</summary> 
        /// <param name="task">The task to be queued.</param> 
        protected override void QueueTask(Task task) 
        {
            if (_disposed) {
                Log.To.TaskScheduling.W(Tag, "SingleThreadScheduler is disposed, ignoring task {0}", task.Id);
                return;
            }

            if (Thread.CurrentThread == _thread) {
                Log.To.TaskScheduling.V(Tag, "Executing re-entrant task out of order");
                TryExecuteTask(task);
                return;
            }

            lock (_jobQueue) {
                Log.To.TaskScheduling.V(Tag, "Adding task to scheduler: {0}", task.Id);
                _jobQueue.AddLast(task);
                _mre.Set();
            }
        } 

        private void Run()
        {
            while (!_disposed || _jobQueue.Count > 0) {
                _mre.Wait();
                Drain();
            }

            Log.To.TaskScheduling.I(Tag, "Scheduler finished, run loop exiting");
        }

        private void Drain() 
        {
            Task nextTask;
            lock (_jobQueue) {
                if (_jobQueue.Count == 0) {
                    Log.To.TaskScheduling.V(Tag, "No more jobs scheduled, waiting...");
                    return;
                }

                nextTask = _jobQueue.First.Value;
                _jobQueue.RemoveFirst();
                if (_jobQueue.Count == 0) {
                    _mre.Reset();
                }
            }

            if(nextTask.Status < TaskStatus.Running) {
                Log.To.TaskScheduling.V(Tag, "Starting task {0}", nextTask.Id);
                TryExecuteTask(nextTask);
                if (nextTask.Status != TaskStatus.RanToCompletion) {
                    if (nextTask.Status == TaskStatus.Canceled) {
                        Log.To.TaskScheduling.V(Tag, "Task {0} cancelled", nextTask.Id);
                    } else {
                        Log.To.TaskScheduling.V(Tag, String.Format("Task {0} faulted", nextTask.Id), nextTask.Exception);
                    }
                } else {
                    Log.To.TaskScheduling.V(Tag, "Task {0} finished successfully", nextTask.Id);
                }
            }
        }

        #region IDisposable implementation

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Task[] items;
                lock (_jobQueue) {
                    items = _jobQueue.ToArray();
                }

                Log.To.TaskScheduling.V(Tag, "Dispose called");
                if (items.Length == 0) {
                    _mre.Set();
                } else {
                    Log.To.TaskScheduling.I(Tag, "Waiting for {0} tasks...", items.Length);
                    Task.WaitAll(items);
                }

                _mre.Dispose();
            }
        }

        #endregion

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) 
        {
            Log.To.TaskScheduling.V(Tag, "TryExecuteTaskInline invoked...");
            if (Thread.CurrentThread == _thread) {
                if (taskWasPreviouslyQueued) {
                    if (TryDequeue(task)) {
                        Log.To.TaskScheduling.V(Tag, "...executing previously queued Task!");
                        return TryExecuteTask(task);
                    } else {
                        Log.To.TaskScheduling.V(Tag, "...Task marked as previously queued, but not found, returning false");
                        return false;
                    }
                }

                Log.To.TaskScheduling.V(Tag, "...executing Task!");
                return TryExecuteTask(task);
            }

            Log.To.TaskScheduling.V(Tag, "...not allowed from outside threads, returning false");
            return false;
        } 

        protected override bool TryDequeue(Task task)
        {
            lock (_jobQueue) {
                return _jobQueue.Remove(task);
            }
        }

        public override int MaximumConcurrencyLevel { 
            get { 
                return 1; 
            } 
        } 

        protected override IEnumerable<Task> GetScheduledTasks() 
        { 
            bool lockTaken = false;
            try
            {
                lockTaken = Monitor.TryEnter(_jobQueue);
                if (lockTaken) {
                    return _jobQueue;
                } else {
                    throw new NotSupportedException();
                }
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_jobQueue);
            }
        }
    } 
}