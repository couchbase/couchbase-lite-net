using System;
using System.Collections.Generic; 
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace Couchbase.Lite.Util
{
    sealed internal class SingleThreadScheduler : TaskScheduler, IDisposable
    {
        private const string Tag = "SingleThreadScheduler";
        private readonly BlockingCollection<Task> _jobQueue = new BlockingCollection<Task>();
        private readonly Thread _thread;

        public SingleThreadScheduler()
        {
            _thread = new Thread(Run) 
            {
                Name = "Database Thread",
                IsBackground = true, 
                Priority = ThreadPriority.Highest
            };
            _thread.Start();
        }

        public SingleThreadScheduler(Thread thread, BlockingCollection<Task> jobQueue)
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
            if (_jobQueue.IsAddingCompleted)
            {
                Log.D(Tag, "Currently draining task queue. Ignoring task {0}", task.Id);
                return;
            }
                
            if (Thread.CurrentThread == _thread)
            {
                //The only way this can happen is if QueueTask was called from
                //within a Task executing on the internal thread.  The only way
                //for it to get there is if it was previously queued, so this
                //can only be a dependent Task that need to be executed out of
                //order
                TryExecuteTask(task);
            }
            else
            {
                _jobQueue.Add(task);
            }
        } 

        private void Run()
        {
            while (!_jobQueue.IsCompleted) {
                Drain();
            }
        }

        private void Drain() 
        {
            Task nextTask;
            bool gotTask = _jobQueue.TryTake(out nextTask, 15000);
            if(gotTask && nextTask.Status < TaskStatus.Running) {
                TryExecuteTask(nextTask);
            }
        }

        #region IDisposable implementation

        public void Dispose()
        {
            if (!_jobQueue.IsAddingCompleted)
            {
                _jobQueue.CompleteAdding();
                Task.WaitAll(_jobQueue.ToArray());
            }
        }

        #endregion

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) 
        {
            //The whole point of this class is to execute things on a single thread
            //so inlining defeats the purpose
            return false;
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
            return _jobQueue.AsEnumerable();     
        }
    } 
}