﻿// 
// SerialQueue.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal.Logging;

namespace Couchbase.Lite.Support
{
    // Inspired by https://github.com/borland/SerialQueue
    internal sealed class SerialQueue
    {
        #region Enums

        private enum SerialQueueState
        {
            Idle,
            Scheduled,
            Processing
        }

        #endregion

        #region Constants

        private const string Tag = nameof(SerialQueue);

        #endregion

        #region Variables

        private readonly object _executionLock = new object();

        private readonly ConcurrentQueue<SerialQueueItem> _queue = new ConcurrentQueue<SerialQueueItem>();
        private int _currentProcessingThread;
        private int _state;

        #endregion

        #region Properties

        internal bool IsInQueue => Environment.CurrentManagedThreadId == _currentProcessingThread;

        private SerialQueueState State
        {
            get => (SerialQueueState)_state;
            set => Interlocked.Exchange(ref _state, (int)value);
        }

        #endregion

        #region Public Methods

        public Task DispatchAfter(Action a, TimeSpan time)
        {
            return Task.Delay(time).ContinueWith(t => DispatchSync(a));
        }

        public Task<TResult> DispatchAfter<TResult>(Func<TResult> f, TimeSpan time)
        {
            return Task.Delay(time).ContinueWith(t => DispatchSync(f));
        }

        public Task DispatchAsync(Action a)
        {
            if(a.Target is Sync.WebSocketWrapper)
                WriteLog.To.Sync.V(Tag, $"DispatchAsync {a.Method}");
            var tcs = new TaskCompletionSource<bool>();
            _queue.Enqueue(new SerialQueueItem { Action = a, Tcs = tcs, SyncContext = SynchronizationContext.Current ?? new SynchronizationContext() });
            StartProcessAsync();

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

        [SuppressMessage("ReSharper", "AccessToDisposedClosure", Justification = "The locking mechanism will assure that the block is executed before the using statement ends")]
        public void DispatchSync(Action a)
        {
            if(IsInQueue || State == SerialQueueState.Idle) {
                // Nested call (or nothing is queued), so execute inline
                var lockTaken = false;
                if(!IsInQueue) {
                    Monitor.Enter(_executionLock);
                    lockTaken = true;
                }

                var oldThread = _currentProcessingThread;
                _currentProcessingThread = Environment.CurrentManagedThreadId;
                try {
                    a();
                } finally {
                    _currentProcessingThread = oldThread;
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

                var oldThread = _currentProcessingThread;
                try { 
                    asyncReady.Wait();
                    _currentProcessingThread = Environment.CurrentManagedThreadId;
                    a();
                } catch(Exception e) {
					WriteLog.To.Database.W(Tag, "Exception during DispatchSync", e);
                    throw; // Synchronous, so let the caller handle it
                } finally {
                    _currentProcessingThread = oldThread;
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

        #endregion

        #region Private Methods

        private void ProcessAsync()
        {
            SerialQueueItem next;
            var oldThread = _currentProcessingThread;
            while(_queue.TryDequeue(out next)) {
                
                State = SerialQueueState.Processing;
                lock(_executionLock) {
                    _currentProcessingThread = Environment.CurrentManagedThreadId;
                    try {
                        next.Action();
                        next.SyncContext.Post(s =>
                        {
                            var item = (SerialQueueItem) s;
                            item.Tcs.SetResult(true);
                        }, next);
                    } catch(Exception e) {
                        WriteLog.To.Database.W(Tag, "Exception during DispatchAsync", e);
                        next.SyncContext.Post(s =>
                        {
                            var item = (SerialQueueItem)s;
                            item.Tcs.SetResult(true);
                        }, next);
                    } finally {
                        _currentProcessingThread = oldThread;
                    }
                }
            }
            
            State = SerialQueueState.Idle;

            if (!_queue.IsEmpty) {
                StartProcessAsync();
            }
        }

        private void StartProcessAsync()
        {
            var old = Interlocked.CompareExchange(ref _state, (int)SerialQueueState.Scheduled, (int)SerialQueueState.Idle);
            if (old == (int)SerialQueueState.Idle) {
                Task.Factory.StartNew(ProcessAsync, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        #endregion

        #region Nested

        private class SerialQueueItem
        {
            #region Variables

            public Action Action;

            public SynchronizationContext SyncContext;

            public TaskCompletionSource<bool> Tcs;

            #endregion
        }

        #endregion
    }
}
