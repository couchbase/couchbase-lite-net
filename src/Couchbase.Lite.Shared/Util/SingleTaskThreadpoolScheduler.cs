using System;
using System.Collections.Generic; 
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Reflection;

namespace Couchbase.Lite.Util
{
    sealed internal class SingleTaskThreadpoolScheduler : TaskScheduler 
    {
        private const string Tag = "SingleTaskThreadpoolScheduler";

        [ThreadStatic]
        private static bool _CurrentThreadIsProcessingItems;

        private readonly LinkedList<Task> _items;
        private int _runningTasks;

        public SingleTaskThreadpoolScheduler()
        {
            Log.To.TaskScheduling.I(Tag, "New scheduler created");
            _items = new LinkedList<Task>();
        }

        /// <summary>Queues a task to the scheduler.</summary> 
        /// <param name="task">The task to be queued.</param> 
        protected override void QueueTask(Task task) 
        {
            Log.To.TaskScheduling.V(Tag, "Received task for queue: {0}", task.Id);

            // Long-running tasks can deadlock us easily.
            // We want to allow these to run without doing that.
            if (task.CreationOptions.HasFlag(TaskCreationOptions.LongRunning))
            {
                Log.To.TaskScheduling.V(Tag, "Long running task detected, adding directly to runtime threadpool...");
                ThreadPool.UnsafeQueueUserWorkItem(s =>
                {
                    var submittedTask = (Task)s;
                    Log.To.TaskScheduling.V(Tag, "Processing long running task {0}", submittedTask.Id);
                    _CurrentThreadIsProcessingItems = true;
                    try {
                        if (((Task)s).Status >= TaskStatus.Running) {
                            Log.To.TaskScheduling.V(Tag, "Skipping already running task {0}...", submittedTask.Id);
                            return;
                        }

                        var success = TryExecuteTask((Task)s);
                        if (!success) {
                            if(((Task)s).Status == TaskStatus.Faulted) {
                                Log.To.TaskScheduling.E(Tag, "A task in the scheduler failed to run", submittedTask.Exception);
                            }
                        }
                    } catch (Exception ex) {
                        Log.To.TaskScheduling.E(Tag, "Spawned task throw an unhandled exception.", ex);
                    } finally {
                        _CurrentThreadIsProcessingItems = false;
                    }
                }, task);
                return;
            }

            lock (_items) {
                Log.To.TaskScheduling.V(Tag, "Adding task to processing queue...");
                _items.AddLast(task); 
                if (Interlocked.CompareExchange(ref _runningTasks, 1, 0) == 0) {
                    Log.To.TaskScheduling.V(Tag, "Spinning up processing queue...");
                    QueueThreadPoolWorkItem(); 
                }
            }
        } 

        private void QueueThreadPoolWorkItem() 
        { 
            ThreadPool.UnsafeQueueUserWorkItem(s => 
            { 
                Log.To.TaskScheduling.V(Tag, "Processing queue started...");
                _CurrentThreadIsProcessingItems = true;
                try { 
                    while (true) {
                        Task item;
                        lock(_items) {
                            if (_items.Count == 0) {
                                Log.To.TaskScheduling.V(Tag, "Processing queue finished!");
                                Interlocked.Decrement(ref _runningTasks);
                                break; 
                            } 

                            item = _items.First.Value;
                            _items.RemoveFirst();
                            Log.To.TaskScheduling.V(Tag, "Next task to execute: {0}", item.Id);
                        }

                        if (item.Status < TaskStatus.Running)
                        {
                            Log.To.TaskScheduling.V(Tag, "Executing task...");
                            var success = TryExecuteTask(item);
                            if(item.Status == TaskStatus.Faulted) {
                                Log.To.TaskScheduling.V(Tag, "Task failed to run", item.Exception);
                            }
                        } else {
                            Log.To.TaskScheduling.V(Tag, "Skipping already running task...");
                        }
                    } 
                }
                catch (Exception e) {
                    Log.To.TaskScheduling.E(Tag, "Unhandled exception in processing queue, aborting...", e);
                } finally {
                    _CurrentThreadIsProcessingItems = false;
                }
            }, null);
        } 

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) 
        {
            if (!_CurrentThreadIsProcessingItems) {
                Log.To.TaskScheduling.V(Tag, "Thread {0} not processing items, so cannot execute inline", Thread.CurrentThread.ManagedThreadId);
                return false;
            }

            if (taskWasPreviouslyQueued) {
                if (TryDequeue(task)) {
                    Log.To.TaskScheduling.V(Tag, "Executing previously queued task {0} inline", task);
                    return TryExecuteTask(task);
                } else {
                    Log.To.TaskScheduling.V(Tag, "Failed to dequeue task {0}", task);
                    return false;
                }
            } else {
                Log.To.TaskScheduling.V(Tag, "Executing task {0} inline", task);
                return TryExecuteTask(task);
            }
        } 

        protected override bool TryDequeue(Task task) 
        {
            lock (_items) {
                return _items.Remove(task);
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
                lockTaken = Monitor.TryEnter(_items);
                if (lockTaken) {
                    return _items;
                } else {
                    return null;
                }
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_items);
            }
        }

        internal IEnumerable<Task> ScheduledTasks { get { return GetScheduledTasks(); } }
    } 
}