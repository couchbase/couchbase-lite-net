// 
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
//using System;
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
    public class BatcherTest : LiteTestCase
    {
        /// <summary>
        /// Submit 101 objects to batcher, and make sure that batch
        /// of first 100 are processed "immediately" (as opposed to being
        /// subjected to a delay which would add latency)
        /// </summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestBatcherLatencyInitialBatch()
        {
            CountDownLatch doneSignal = new CountDownLatch(1);
            ScheduledExecutorService workExecutor = new ScheduledThreadPoolExecutor(1);
            int inboxCapacity = 100;
            int processorDelay = 500;
            AtomicLong timeProcessed = new AtomicLong();
            Batcher batcher = new Batcher<string>(workExecutor, inboxCapacity, processorDelay
                , new _BatchProcessor_34(timeProcessed, doneSignal));
            AList<string> objectsToQueue = new AList<string>();
            for (int i = 0; i < inboxCapacity + 1; i++)
            {
                objectsToQueue.AddItem(Sharpen.Extensions.ToString(i));
            }
            long timeQueued = Runtime.CurrentTimeMillis();
            batcher.QueueObjects(objectsToQueue);
            bool didNotTimeOut = doneSignal.Await(35, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(didNotTimeOut);
            long delta = timeProcessed.Get() - timeQueued;
            NUnit.Framework.Assert.IsTrue(delta > 0);
            // we want the delta between the time it was queued until the
            // time it was processed to be as small as possible.  since
            // there is some overhead, rather than using a hardcoded number
            // express it as a ratio of the processor delay, asserting
            // that the entire processor delay never kicked in.
            int acceptableDelta = processorDelay - 1;
            Log.V(Log.Tag, "delta: %d", delta);
            NUnit.Framework.Assert.IsTrue(delta < acceptableDelta);
        }

        private sealed class _BatchProcessor_34 : BatchProcessor<string>
        {
            public _BatchProcessor_34(AtomicLong timeProcessed, CountDownLatch doneSignal)
            {
                this.timeProcessed = timeProcessed;
                this.doneSignal = doneSignal;
            }

            public void Process(IList<string> itemsToProcess)
            {
                Log.V(Database.Tag, "process called with: " + itemsToProcess);
                timeProcessed.Set(Runtime.CurrentTimeMillis());
                doneSignal.CountDown();
            }

            private readonly AtomicLong timeProcessed;

            private readonly CountDownLatch doneSignal;
        }

        /// <summary>
        /// Set batch processing delay to 500 ms, and every second, add a new item
        /// to the batcher queue.
        /// </summary>
        /// <remarks>
        /// Set batch processing delay to 500 ms, and every second, add a new item
        /// to the batcher queue.  Make sure that each item is processed immediately.
        /// </remarks>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestBatcherLatencyTrickleIn()
        {
            CountDownLatch doneSignal = new CountDownLatch(10);
            ScheduledExecutorService workExecutor = new ScheduledThreadPoolExecutor(1);
            int inboxCapacity = 100;
            int processorDelay = 500;
            AtomicLong maxObservedDelta = new AtomicLong(-1);
            Batcher batcher = new Batcher<long>(workExecutor, inboxCapacity, processorDelay, 
                new _BatchProcessor_91(maxObservedDelta, doneSignal));
            AList<long> objectsToQueue = new AList<long>();
            for (int i = 0; i < 10; i++)
            {
                batcher.QueueObjects(Arrays.AsList(Runtime.CurrentTimeMillis()));
                Sharpen.Thread.Sleep(1000);
            }
            bool didNotTimeOut = doneSignal.Await(35, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(didNotTimeOut);
            Log.V(Log.Tag, "maxDelta: %d", maxObservedDelta.Get());
            // we want the max observed delta between the time it was queued until the
            // time it was processed to be as small as possible.  since
            // there is some overhead, rather than using a hardcoded number
            // express it as a ratio of 1/4th the processor delay, asserting
            // that the entire processor delay never kicked in.
            int acceptableMaxDelta = processorDelay - 1;
            Log.V(Log.Tag, "maxObservedDelta: %d", maxObservedDelta.Get());
            NUnit.Framework.Assert.IsTrue((maxObservedDelta.Get() < acceptableMaxDelta));
        }

        private sealed class _BatchProcessor_91 : BatchProcessor<long>
        {
            public _BatchProcessor_91(AtomicLong maxObservedDelta, CountDownLatch doneSignal)
            {
                this.maxObservedDelta = maxObservedDelta;
                this.doneSignal = doneSignal;
            }

            public void Process(IList<long> itemsToProcess)
            {
                if (itemsToProcess.Count != 1)
                {
                    throw new RuntimeException("Unexpected itemsToProcess");
                }
                long timeSubmitted = itemsToProcess[0];
                long delta = Runtime.CurrentTimeMillis() - timeSubmitted;
                if (delta > maxObservedDelta.Get())
                {
                    maxObservedDelta.Set(delta);
                }
                doneSignal.CountDown();
            }

            private readonly AtomicLong maxObservedDelta;

            private readonly CountDownLatch doneSignal;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestBatcherSingleBatch()
        {
            CountDownLatch doneSignal = new CountDownLatch(10);
            ScheduledExecutorService workExecutor = new ScheduledThreadPoolExecutor(1);
            int inboxCapacity = 10;
            int processorDelay = 1000;
            Batcher batcher = new Batcher<string>(workExecutor, inboxCapacity, processorDelay
                , new _BatchProcessor_146(doneSignal));
            // add this to make it a bit more realistic
            AList<string> objectsToQueue = new AList<string>();
            for (int i = 0; i < inboxCapacity * 10; i++)
            {
                objectsToQueue.AddItem(Sharpen.Extensions.ToString(i));
            }
            batcher.QueueObjects(objectsToQueue);
            bool didNotTimeOut = doneSignal.Await(35, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(didNotTimeOut);
        }

        private sealed class _BatchProcessor_146 : BatchProcessor<string>
        {
            public _BatchProcessor_146(CountDownLatch doneSignal)
            {
                this.doneSignal = doneSignal;
            }

            public void Process(IList<string> itemsToProcess)
            {
                Log.V(Database.Tag, "process called with: " + itemsToProcess);
                try
                {
                    Sharpen.Thread.Sleep(100);
                }
                catch (Exception e)
                {
                    Sharpen.Runtime.PrintStackTrace(e);
                }
                BatcherTest.AssertNumbersConsecutive(itemsToProcess);
                doneSignal.CountDown();
            }

            private readonly CountDownLatch doneSignal;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestBatcherBatchSize5()
        {
            CountDownLatch doneSignal = new CountDownLatch(10);
            ScheduledExecutorService workExecutor = new ScheduledThreadPoolExecutor(1);
            int inboxCapacity = 10;
            int processorDelay = 1000;
            Batcher batcher = new Batcher<string>(workExecutor, inboxCapacity, processorDelay
                , new _BatchProcessor_185(processorDelay, doneSignal));
            // add this to make it a bit more realistic
            AList<string> objectsToQueue = new AList<string>();
            for (int i = 0; i < inboxCapacity * 10; i++)
            {
                objectsToQueue.AddItem(Sharpen.Extensions.ToString(i));
                if (objectsToQueue.Count == 5)
                {
                    batcher.QueueObjects(objectsToQueue);
                    objectsToQueue = new AList<string>();
                }
            }
            bool didNotTimeOut = doneSignal.Await(35, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(didNotTimeOut);
        }

        private sealed class _BatchProcessor_185 : BatchProcessor<string>
        {
            public _BatchProcessor_185(int processorDelay, CountDownLatch doneSignal)
            {
                this.processorDelay = processorDelay;
                this.doneSignal = doneSignal;
            }

            public void Process(IList<string> itemsToProcess)
            {
                Log.V(Database.Tag, "process called with: " + itemsToProcess);
                try
                {
                    Sharpen.Thread.Sleep(processorDelay * 2);
                }
                catch (Exception e)
                {
                    Sharpen.Runtime.PrintStackTrace(e);
                }
                BatcherTest.AssertNumbersConsecutive(itemsToProcess);
                doneSignal.CountDown();
            }

            private readonly int processorDelay;

            private readonly CountDownLatch doneSignal;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestBatcherBatchSize1()
        {
            CountDownLatch doneSignal = new CountDownLatch(1);
            ScheduledExecutorService workExecutor = new ScheduledThreadPoolExecutor(1);
            int inboxCapacity = 100;
            int processorDelay = 1000;
            Batcher batcher = new Batcher<string>(workExecutor, inboxCapacity, processorDelay
                , new _BatchProcessor_227(processorDelay, doneSignal));
            // add this to make it a bit more realistic
            AList<string> objectsToQueue = new AList<string>();
            for (int i = 0; i < inboxCapacity; i++)
            {
                objectsToQueue.AddItem(Sharpen.Extensions.ToString(i));
                if (objectsToQueue.Count == 5)
                {
                    batcher.QueueObjects(objectsToQueue);
                    objectsToQueue = new AList<string>();
                }
            }
            bool didNotTimeOut = doneSignal.Await(35, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(didNotTimeOut);
        }

        private sealed class _BatchProcessor_227 : BatchProcessor<string>
        {
            public _BatchProcessor_227(int processorDelay, CountDownLatch doneSignal)
            {
                this.processorDelay = processorDelay;
                this.doneSignal = doneSignal;
            }

            public void Process(IList<string> itemsToProcess)
            {
                Log.V(Database.Tag, "process called with: " + itemsToProcess);
                try
                {
                    Sharpen.Thread.Sleep(processorDelay * 2);
                }
                catch (Exception e)
                {
                    Sharpen.Runtime.PrintStackTrace(e);
                }
                BatcherTest.AssertNumbersConsecutive(itemsToProcess);
                doneSignal.CountDown();
            }

            private readonly int processorDelay;

            private readonly CountDownLatch doneSignal;
        }

        private static void AssertNumbersConsecutive(IList<string> itemsToProcess)
        {
            int previousItemNumber = -1;
            foreach (string itemString in itemsToProcess)
            {
                if (previousItemNumber == -1)
                {
                    previousItemNumber = System.Convert.ToInt32(itemString);
                }
                else
                {
                    int curItemNumber = System.Convert.ToInt32(itemString);
                    NUnit.Framework.Assert.IsTrue(curItemNumber == previousItemNumber + 1);
                    previousItemNumber = curItemNumber;
                }
            }
        }
    }
}
