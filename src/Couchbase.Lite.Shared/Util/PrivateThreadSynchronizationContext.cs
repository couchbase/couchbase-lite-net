using System;
using System.Threading;
using System.Collections.Concurrent;
using Couchbase.Lite.Util;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Shared
{
    public class PrivateThreadSynchronizationContext : SynchronizationContext, IDisposable
    {
        const string Tag = "PrivateThreadSynchronizationContext";

        readonly Thread _thread;
        readonly ManualResetEvent _runLoop;
        readonly ConcurrentQueue<Tuple<SendOrPostCallback,Object>> _postQueue;

        Boolean _isRunning;
//        Tuple<SendOrPostCallback,Object> _sendDelegate;
//        ManualResetEvent _sendWaitHandle;

        public PrivateThreadSynchronizationContext(String name)
        {
//            _sendDelegate = null;
//            _sendWaitHandle = null;

            _postQueue = new ConcurrentQueue<Tuple<SendOrPostCallback,Object>>();
            _runLoop = new ManualResetEvent(false);

            _thread = new Thread(Run) 
            {
                Name = name,
                IsBackground = true, 
                Priority = ThreadPriority.Highest
            };
            _thread.Start();

            _isRunning = true;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            // Push into a queue.
            _postQueue.Enqueue(Tuple.Create(d, state));

            // FIXME: Do we need to base invoke?
            //base.Post(d, state);

            // Let the runloop run.
            _runLoop.Set();
        }

        void Run()
        {
            while (_isRunning)
            {
                Tuple<SendOrPostCallback,Object> callback = null;

                // Then process the Post queue.
                while (_postQueue.TryDequeue(out callback))
                {
                    callback.Item1(callback.Item2);
                }

                // Reset the runloop reset event.
                _runLoop.Reset();
                // Wait for new tasks.
                _runLoop.WaitOne();
            }
        }

        internal int QueueLength { get { return _postQueue.Count; } }

//        public override void Send(SendOrPostCallback d, object state)
//        {
//            // Set the current Send delegate.
//            var newDelegate = Tuple.Create(d, state);
//            var currentDelegate = _sendDelegate;
//
//            Interlocked.MemoryBarrier(); // Don't let ARM cpu's reorder the previous line to after the CAS.
//
//            var oldDelegate = Interlocked.CompareExchange(ref _sendDelegate, newDelegate, currentDelegate);
//
//            if (oldDelegate == newDelegate)
//            {
//                do
//                {
//                    Log.D(Tag, "CAS failed.");
//                    _spinWait.SpinOnce();
//                    currentDelegate = _sendDelegate;
//                    Interlocked.MemoryBarrier(); // Don't let ARM cpu's reorder the previous line to after the CAS.
//                    
//                } while (Interlocked.CompareExchange(ref _sendDelegate, newDelegate, currentDelegate) != newDelegate);
//            }
//
//            // FIXME: Do we need to base invoke?
//            //base.Send(d, state);
//
//            // Wait for the runloop to either finish it's current task,
//            // or run our callback.
//            _sendWaitHandle = new ManualResetEvent(false);
//            _runLoop.Set();
//            _sendWaitHandle.WaitOne();
//        }



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
            if (_isRunning)
            {
                _isRunning = false;
                _thread.Abort();
            }
        }

        #endregion
    }
}

