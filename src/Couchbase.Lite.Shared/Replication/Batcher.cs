//
// Batcher.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Util;

namespace Couchbase.Lite.Support
{
    internal class BatcherOptions<T> : ConstructorOptions
    {
        [RequiredProperty]
        public TaskFactory WorkExecutor { get; set; }
        public int Capacity { get; set; }

        public TimeSpan Delay { get; set; }

        [RequiredProperty]
        public Action<IList<T>> Processor { get; set; }
        
        [RequiredProperty(CreateDefault = true)]
        public CancellationTokenSource TokenSource { get; set; } 
    }

    /// <summary>
    /// Utility that queues up objects until the queue fills up or a time interval elapses,
    /// then passes all the objects at once to a client-supplied processor block.
    /// </summary>
    internal class Batcher<T>
    {

        #region Constants

        private const string TAG = "Batcher";

        #endregion

        #region Variables

        private BatcherOptions<T> _options;
        private Task _flushFuture;
        private TimeSpan _scheduledDelay;
        private bool _scheduled;
        private DateTime _lastProcessedTime;
        private CancellationTokenSource _cancellationSource;
        private UniqueQueue<T> _inbox = new UniqueQueue<T>();
        private object _scheduleLocker = new object();

        #endregion

        #region Constructor

        public Batcher(BatcherOptions<T> options)
        {
            options.Validate();
            _options = options;
            _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(options.TokenSource.Token);
        }

        #endregion

        #region Public Methods

        public void ProcessNow()
        {
            _scheduled = false;

            var amountToTake = Math.Min(_options.Capacity, _inbox.Count);
            List<T> toProcess = new List<T>();
            T nextObj;
            int i = 0;
            while(i++ < amountToTake && _inbox.TryDequeue(out nextObj)) {
                toProcess.Add(nextObj);
            }

            if (toProcess != null && toProcess.Count > 0) {
                Log.To.NoDomain.D(TAG, "Invoking processor with {0} items ", toProcess.Count);
                _options.Processor(toProcess);
            }

            _lastProcessedTime = DateTime.UtcNow;
            if (_inbox.Count > 0) {
                ScheduleWithDelay(DelayToUse());
            }
        }

        public int QueueObjects(IList<T> objects)
        {
            if (objects == null || objects.Count == 0) {
                return 0;
            }

            Log.To.NoDomain.V(TAG, "QueueObjects called with {0} objects", objects.Count);
            int added = 0;
            foreach (var obj in objects) {
                if(_inbox.Enqueue(obj)) {
                    added++;
                }

                if(_inbox.Count >= _options.Capacity) {
                    ScheduleWithDelay(TimeSpan.Zero);
                }
            }

            ScheduleWithDelay(DelayToUse());
            return added;
        }

        /// <summary>Adds an object to the queue.</summary>
        public bool QueueObject(T o)
        {
            var objects = new List<T> { o };
            return QueueObjects(objects) == 1;
        }

        /// <summary>Sends queued objects to the processor block (up to the capacity).</summary>
        public void Flush()
        {
            ScheduleWithDelay(DelayToUse());
        }

        /// <summary>Sends _all_ the queued objects at once to the processor block.</summary>
        public void FlushAll()
        {
            while(_inbox.Count > 0) {
                Unschedule();
                
                ProcessNow();
                _lastProcessedTime = DateTime.UtcNow;
            }
        }

        /// <summary>Number of items to be processed.</summary>
        public int Count()
        {
            return _inbox.Count;
        }

        #endregion

        #region Internal Methods

        // Only used for testing
        internal void Clear()
        {
            Log.To.NoDomain.D(TAG, "clear() called, setting _jobQueue to null");
            Unschedule();

            var itemCount = _inbox.Count;
            Misc.SafeDispose(ref _inbox);
            _inbox = new UniqueQueue<T>();

            Log.To.NoDomain.D(TAG, "Discarded {0} items", itemCount);
        }

        /// <summary>
        /// Calculates the delay to use when scheduling the next batch of objects to process.
        /// </summary>
        /// <remarks>
        /// There is a balance required between clearing down the input queue as fast as possible
        /// and not exhausting downstream system resources such as sockets and http response buffers
        /// by processing too many batches concurrently.
        /// </remarks>
        /// <returns>The delay o use.</returns>
        internal TimeSpan DelayToUse()
        {
            if(_inbox.Count > _options.Capacity) {
                return TimeSpan.Zero;
            }

            var delta = (DateTime.UtcNow - _lastProcessedTime);
            var delayToUse = delta >= _options.Delay
                ? TimeSpan.Zero
                : _options.Delay;

            Log.To.NoDomain.D(TAG, "DelayToUse() delta: {0}, delayToUse: {1}, delay: {2} [last: {3}]", delta, delayToUse, _options.Delay, _lastProcessedTime.ToString());

            return delayToUse;
        }

        #endregion

        #region Private Methods

        private void ScheduleWithDelay(TimeSpan suggestedDelay)
        {
            lock(_scheduleLocker) {
                if (_scheduled) {
                    Log.To.NoDomain.D(TAG, "ScheduleWithDelay called with delay: {0} ms but already scheduled", suggestedDelay);
                }
    
                if (_scheduled && (suggestedDelay < _scheduledDelay)) {
                    Log.To.NoDomain.D(TAG, "Unscheduling");
                    Unschedule();
                }
    
                if (!_scheduled) {
                    _scheduled = true;
                    _scheduledDelay = suggestedDelay;
                    var cts = _cancellationSource;
                    _flushFuture = Task.Delay(suggestedDelay).ContinueWith((t) =>
                    {
                        if (!cts.IsCancellationRequested) {
                            ProcessNow();
                        }
    
                        return true;
                    }, cts.Token, TaskContinuationOptions.None, _options.WorkExecutor.Scheduler);
                } else {
                    if (_flushFuture == null || _flushFuture.IsCompleted) {
                        Log.To.NoDomain.E(TAG, "Batcher got into an inconsistent state, flush future is scheduled " +
                        "but missing.  Throwing...");
                        throw new InvalidOperationException("Flushfuture missing despite scheduled.");
                    }
                }
            }
        }

        private void Unschedule()
        {
            lock(_scheduleLocker) {
                _scheduled = false;
                if (_cancellationSource != null) {
                    try {
                        _cancellationSource.Cancel(true);
                    } catch (Exception) {
                        // Swallow it.
                    } 
    
                    _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_options.TokenSource.Token);
                }
            }
        }

        #endregion

    }
}
