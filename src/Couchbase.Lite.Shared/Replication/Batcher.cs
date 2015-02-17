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
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System.Threading.Tasks;
using System.Threading;
using Couchbase.Lite.Internal;
using System.Linq;
using System.Collections.Concurrent;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// Utility that queues up objects until the queue fills up or a time interval elapses,
    /// then passes all the objects at once to a client-supplied processor block.
    /// </summary>
    /// <remarks>
    /// Utility that queues up objects until the queue fills up or a time interval elapses,
    /// then passes all the objects at once to a client-supplied processor block.
    /// </remarks>
    internal class Batcher<T>
    {
        private const string Tag = "Batcher";

        private readonly TaskFactory workExecutor;

        private Task flushFuture;

        private readonly int capacity;

        private readonly int delay;

        private int scheduledDelay;

        private ConcurrentQueue<T> inbox;

        private readonly Action<IList<T>> processor;

        private Boolean scheduled;

        private DateTime lastProcessedTime;

        private CancellationTokenSource cancellationSource;

        /// <summary>Initializes a batcher.</summary>
        /// <remarks>Initializes a batcher.</remarks>
        /// <param name="workExecutor">the work executor that performs actual work</param>
        /// <param name="capacity">The maximum number of objects to batch up. If the queue reaches this size, the queued objects will be sent to the processor immediately.
        ///     </param>
        /// <param name="delay">The maximum waiting time to collect objects before processing them. In some circumstances objects will be processed sooner.
        ///     </param>
        /// <param name="processor">The callback/block that will be called to process the objects.
        ///     </param>
        public Batcher(TaskFactory workExecutor, int capacity, int delay, Action<IList<T>> processor)
        {
            Log.D(Tag, "New batcher created with capacity: {0}, delay: {1}", capacity, delay);

            this.workExecutor = workExecutor;
            this.capacity = capacity;
            this.delay = delay;
            this.processor = processor;
            this.cancellationSource = new CancellationTokenSource();
            this.inbox = new ConcurrentQueue<T>();
        }

        public void ProcessNow()
        {
            Log.V(Tag, "ProcessNow() called");

            scheduled = false;
            var inboxRef = inbox;

            var toProcess = new List<T>();
            if (inboxRef == null || inboxRef.Count == 0)
            {
                Log.V(Tag, "ProcessNow() called, but inbox is empty");
                return;
            }

            T next;
            while (toProcess.Count < capacity && inboxRef.TryDequeue(out next))
            {
                toProcess.Add(next);
                    Log.D(Tag, "ProcessNow() called, inbox size: {0}", inbox.Count);
            }
            if(inboxRef.Count > 0)
            {
                // There are more objects left, so schedule them Real Soon:
                ScheduleWithDelay(DelayToUse());
            }

            if (toProcess != null && toProcess.Count > 0)
            {
                Log.D(Tag, "invoking processor with " + toProcess.Count + " items ");
                processor(toProcess);
            }
            else
            {
                Log.D(Tag, "nothing to process");
            }

            lastProcessedTime = DateTime.UtcNow;
            Log.D(Tag, "Set lastProcessedTime to {0}", lastProcessedTime.ToString());
        }

        public void QueueObjects(IList<T> objects)
        {
            Log.V(Tag, "QueueObjects called with {0} objects", objects.Count);

            if (objects == null || objects.Count == 0)
            {
                return;
            }

            Log.V(Tag, "inbox size before adding objects: {0}", inbox.Count);
            var inboxRef = inbox;
            foreach (var obj in objects)
            {
                inboxRef.Enqueue(obj);
            }
            ScheduleWithDelay(DelayToUse());
        }

        /// <summary>Adds an object to the queue.</summary>
        public void QueueObject(T o)
        {
            var objects = new List<T> { o };
            QueueObjects(objects);
        }

        /// <summary>Sends queued objects to the processor block (up to the capacity).</summary>
        public void Flush()
        {
            ScheduleWithDelay(DelayToUse());
        }

        /// <summary>Sends _all_ the queued objects at once to the processor block.</summary>
        public void FlushAll()
        {
            while(inbox.Count > 0)
            {
                Unschedule();

                var toProcess = inbox.ToList();
                inbox = new ConcurrentQueue<T>();
                processor(toProcess);
                lastProcessedTime = DateTime.UtcNow;
            }
        }

        /// <summary>Number of items to be processed.</summary>
        public int Count()
        {
            return inbox.Count;
        }

        /// <summary>Empties the queue without processing any of the objects in it.</summary>
        public void Clear()
        {
            Log.V(Tag, "clear() called, setting inbox to null");
            Unschedule();
            inbox = new ConcurrentQueue<T>();
        }

        private void ScheduleWithDelay(Int32 suggestedDelay)
        {
            if (scheduled)
                Log.V(Tag, "ScheduleWithDelay called with delay: {0} ms but already scheduled", suggestedDelay);

            if (scheduled && (suggestedDelay < scheduledDelay))
            {
                Log.V(Tag, "Unscheduling");
                Unschedule();
            }

            if (!scheduled) {
                Log.D(Tag, "not already scheduled");

                scheduled = true;
                scheduledDelay = suggestedDelay;

                Log.D(Tag, "ScheduleWithDelay called with delay: {0} ms, scheduler: {1}/{2}", suggestedDelay, workExecutor.Scheduler.GetType().Name, ((SingleTaskThreadpoolScheduler)workExecutor.Scheduler).ScheduledTasks.Count());

                flushFuture = workExecutor.StartNew(()=> {
                    if(!(cancellationSource.IsCancellationRequested)) {
                        try {
                            ProcessNow();
                        } catch (Exception e) {
                            // we don't want this to crash the batcher
                            Log.E(Tag, "BatchProcessor throw exception", e);
                        }
                    }
                    cancellationSource = new CancellationTokenSource();
                }, cancellationSource.Token, TaskCreationOptions.None, workExecutor.Scheduler);
            } else {
                if (flushFuture == null || flushFuture.IsCompleted) {
                    throw new InvalidOperationException("Flushfuture missing despite scheduled.");
                }
            }
        }

        private void Unschedule()
        {
            Log.V(Tag, "unschedule() called");
            scheduled = false;
            if (cancellationSource != null && flushFuture != null)
            {
                try
                {
                    cancellationSource.Cancel(true);
                }
                catch (Exception)
                {
                    // Swallow it.
                } 
                Log.V(Tag, "cancallationSource.Cancel() called");
            }
            else
            {
                Log.V(Tag, "cancellationSource or flushFuture was null, doing nothing");
            }
        }

        /// <summary>
        /// Calculates the delay to use when scheduling the next batch of objects to process.
        /// </summary>
        /// <remarks>
        /// There is a balance required between clearing down the input queue as fast as possible
        /// and not exhausting downstream system resources such as sockets and http response buffers
        /// by processing too many batches concurrently.
        /// </remarks>
        /// <returns>The to use.</returns>
        private Int32 DelayToUse()
        {
            var delta = (Int32)(DateTime.UtcNow - lastProcessedTime).TotalMilliseconds;
            var delayToUse = delta >= delay
                ? 0
                : delay;

            Log.V(Tag, "DelayToUse() delta: {0}, delayToUse: {1}, delay: {2} [last: {3}]", delta, delayToUse, delay, lastProcessedTime.ToString());

            return delayToUse;
        }
    }
}
