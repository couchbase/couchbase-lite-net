//
//  SerialQueue.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    // Inspired by https://github.com/borland/SerialQueue
    internal sealed class SerialQueue : IDispatchQueue
    {
        private class SerialQueueItem
        {
            public Action Action;

            public TaskCompletionSource<bool> Tcs;
        }

        private enum SerialQueueState
        {
            Idle,
            Scheduled,
            Processing
        }

        private const string Tag = nameof(SerialQueue);

        private readonly ConcurrentQueue<SerialQueueItem> _queue = new ConcurrentQueue<SerialQueueItem>();
        private object _executionLock = new object();
        private int _state;
        private int _currentProcessingThread;
        private bool _executingSync;

        public int Count { get; private set; }

        internal bool IsInQueue
        {
            get {
                return _executingSync ||
                    Environment.CurrentManagedThreadId == _currentProcessingThread;
            }
        }

        private SerialQueueState State
        {
            get {
                return (SerialQueueState)_state;
            }
            set {
                _state = (int)value;
            }
        }

        public Task DispatchAsync(Action a)
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Enqueue(new SerialQueueItem { Action = a, Tcs = tcs });
            Count++;
            var old = Interlocked.CompareExchange(ref _state, (int)SerialQueueState.Scheduled, (int)SerialQueueState.Idle);
            if(old == (int)SerialQueueState.Idle) {
                Task.Factory.StartNew(ProcessAsync, TaskCreationOptions.LongRunning);
            }

            return tcs.Task;
        }

        public Task<T> DispatchAsync<T>(Func<T> f)
        {
            var tcs = new TaskCompletionSource<T>();
            DispatchAsync(() =>
            {
                try {
                    tcs.SetResult(f());
                } catch(Exception e) {
                    tcs.TrySetException(e);
                }
            });
            return tcs.Task;
        }

        public void DispatchSync(Action a)
        {
            var oldExecuting = _executingSync;
            if(IsInQueue || State == SerialQueueState.Idle) {
                // Nested call (or nothing is queued), so execute inline
                _executingSync = true;
                var lockTaken = false;
                if(!IsInQueue) {
                    Monitor.Enter(_executionLock, ref lockTaken);
                }

                try {
                    a();
                } finally {
                    _executingSync = oldExecuting;
                    if(lockTaken) {
                        Monitor.Exit(_executionLock);
                    }
                }

                return;
            }

            using(var asyncReady = new ManualResetEventSlim())
            using(var syncDone = new ManualResetEventSlim()) {
                DispatchAsync(() =>
                {
                    asyncReady.Set();
                    try {
                        syncDone.Wait();
                    } catch(ObjectDisposedException) {
                        // Swallow this, it means that the sync method finished
                        // Entirely between the two above lines and already disposed
                        // syncDone
                    }
                });

                try {
                    _executingSync = true;
                    asyncReady.Wait();
                    a();
                } catch(Exception e) {
                    Log.To.TaskScheduling.W(Tag, "Exception during DispatchSync", e);
                    throw; // Synchronous, so let the caller handle it
                } finally {
                    _executingSync = oldExecuting;
                    syncDone.Set();
                }
            }
        }

        public T DispatchSync<T>(Func<T> f)
        {
            var retVal = default(T);
            DispatchSync(new Action(() => retVal = f()));
            return retVal;
        }

        internal void AssertInQueue()
        {
            if(!IsInQueue) {
                if(Debugger.IsAttached) {
                    Debugger.Break();
                } else {
                    throw new ThreadSafetyViolationException();
                }
            }
        }

        private void ProcessAsync()
        {
            SerialQueueItem next;
            while(_queue.TryDequeue(out next)) {
                _currentProcessingThread = Environment.CurrentManagedThreadId;
                State = SerialQueueState.Processing;
                lock(_executionLock) {
                    try {
                        next.Action();
                        next.Tcs.SetResult(true);
                    } catch(Exception e) {
                        Log.To.TaskScheduling.W(Tag, "Exception during DispatchAsync", e);
                        next.Tcs.TrySetException(e);
                    }
                }

                Count--;
            }

            State = SerialQueueState.Idle;
            _currentProcessingThread = 0;
        }
    }
}
