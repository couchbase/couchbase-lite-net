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
            _items = new LinkedList<Task>();
        }

        /// <summary>Queues a task to the scheduler.</summary> 
        /// <param name="task">The task to be queued.</param> 
        protected override void QueueTask(Task task) 
        {
            // Long-running tasks can deadlock us easily.
            // We want to allow these to run without doing that.
            if (task.CreationOptions.HasFlag(TaskCreationOptions.LongRunning))
            {
                ThreadPool.UnsafeQueueUserWorkItem(s =>
                {
                    _CurrentThreadIsProcessingItems = true;
                    try {
                        if (((Task)s).Status >= TaskStatus.Running) {
                            return;
                        }

                        var success = TryExecuteTask((Task)s);
                        if (!success)
                            throw new InvalidOperationException("A spawned task failed to run correctly.");
                    } catch (Exception ex) {
                        Log.E(Tag, "Spawned task throw an unhandled exception.", ex);
                    } finally {
                        _CurrentThreadIsProcessingItems = false;
                    }
                }, task);
                return;
            }

            lock (_items) {
                _items.AddLast(task); 
                if (Interlocked.CompareExchange(ref _runningTasks, 1, 0) == 0) {
                    QueueThreadPoolWorkItem(); 
                }
            }
        } 

        private void QueueThreadPoolWorkItem() 
        { 
            ThreadPool.UnsafeQueueUserWorkItem(s => 
            { 
                _CurrentThreadIsProcessingItems = true;
                try { 
                    while (true) {
                        Task item;
                        lock(_items) {
                            if (_items.Count == 0) {
                                Interlocked.Decrement(ref _runningTasks);
                                break; 
                            } 

                            item = _items.First.Value;
                            _items.RemoveFirst();
                        }

                        if (item.Status < TaskStatus.Running)
                        {
                            TryExecuteTask(item);
                            if (item.Status == TaskStatus.Faulted) {
                                Log.E(Tag, "Task {0} faulted {1}", item.Id, item.Exception);
                            }
                        }
                    } 
                }
                catch (Exception e) {
                    Log.E(Tag, "Unhandled exception in runloop", e);
                    throw;
                } finally {
                    _CurrentThreadIsProcessingItems = false;
                }
            }, null);
        } 

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) 
        {
            if (!_CurrentThreadIsProcessingItems) {
                Log.V(Tag, "Thread {0} not processing items, so cannot execute inline", Thread.CurrentThread.ManagedThreadId);
                return false;
            }

            if (taskWasPreviouslyQueued) {
                if (TryDequeue(task)) {
                    Log.V(Tag, "Executing previously queued task {0} inline", task.Id);
                    return TryExecuteTask(task);
                } else {
                    Log.V(Tag, "Failed to dequeue task {0}", task.Id);
                    return false;
                }
            } else {
                Log.V(Tag, "Executing task {0} inline", task.Id);
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
                    throw new NotSupportedException();
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