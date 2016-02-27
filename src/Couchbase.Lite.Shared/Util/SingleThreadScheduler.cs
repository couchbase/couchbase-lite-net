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
                Log.To.NoDomain.W(Tag, "SingleThreadScheduler is disposed, ignoring task {0}", task.Id);
                return;
            }

            lock (_jobQueue) {
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

            Log.To.NoDomain.D(Tag, "SingleThreadScheduler finished");
        }

        private void Drain() 
        {
            Task nextTask;
            lock (_jobQueue) {
                if (_jobQueue.Count == 0) {
                    return;
                }

                nextTask = _jobQueue.First.Value;
                _jobQueue.RemoveFirst();
                if (_jobQueue.Count == 0) {
                    _mre.Reset();
                }
            }

            if(nextTask.Status < TaskStatus.Running) {
                TryExecuteTask(nextTask);
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

                if (items.Length == 0) {
                    _mre.Set();
                } else {
                    Task.WaitAll(items);
                }

                _mre.Dispose();
            }
        }

        #endregion

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) 
        {
            if (Thread.CurrentThread == _thread) {
                if (taskWasPreviouslyQueued) {
                    if (TryDequeue(task)) {
                        return TryExecuteTask(task);
                    } else {
                        return false;
                    }
                }

                return TryExecuteTask(task);
            }

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