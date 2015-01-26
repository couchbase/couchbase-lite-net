using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Shared
{
    public class PrivateThreadSynchronizationContext : SynchronizationContext, IDisposable
    {
        private const string Tag = "PrivateThreadSynchronizationContext";

        private readonly Thread _thread;
        private readonly BlockingCollection<Task> _postQueue;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public bool CanReceive
        {
            get
            {
                return !_postQueue.IsAddingCompleted;
            }
        }

        public PrivateThreadSynchronizationContext(String name)
        {

            _postQueue = new BlockingCollection<Task>();
            _thread = new Thread(Run) 
            {
                Name = name,
                IsBackground = true, 
                Priority = ThreadPriority.Highest
            };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            // Push into a queue.
            _postQueue.TryAdd(new Task(() => d(state)));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            Task t = new Task(() => d(state));
            _postQueue.TryAdd(t);
            t.Wait();

            if (t.Exception != null)
                throw t.Exception;
        }

        void Run()
        {
            Task nextTask;
            try {
                while (!_postQueue.IsCompleted && _postQueue.TryTake(out nextTask, -1, _cts.Token))
                {
                    nextTask.Start();
                }
            }
            catch(OperationCanceledException) {
            }

            Log.V(Tag, "Consumer thread finished");
        }

        internal int QueueLength { get { return _postQueue.Count; } }

        #region IDisposable implementation

        /// <summary>
        /// Releases all resource used by the <see cref="Couchbase.Lite.Shared.PrivateThreadSynchronizationContext"/> object.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the
        /// <see cref="Couchbase.Lite.Shared.PrivateThreadSynchronizationContext"/>. The <see cref="Dispose"/> method
        /// leaves the <see cref="Couchbase.Lite.Shared.PrivateThreadSynchronizationContext"/> in an unusable state.
        /// After calling <see cref="Dispose"/>, you must release all references to the
        /// <see cref="Couchbase.Lite.Shared.PrivateThreadSynchronizationContext"/> so the garbage collector can reclaim
        /// the memory that the <see cref="Couchbase.Lite.Shared.PrivateThreadSynchronizationContext"/> was occupying.</remarks>
        public void Dispose()
        {
            if (!_postQueue.IsAddingCompleted)
            {
                _cts.Cancel();
                _postQueue.CompleteAdding();
            }
        }

        #endregion
    }
}

