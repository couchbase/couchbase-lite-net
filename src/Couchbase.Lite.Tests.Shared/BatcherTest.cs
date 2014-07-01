//
// Manager.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc
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
using System.Threading.Tasks;
using System.Collections.Generic;

using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using NUnit.Framework;
using Sharpen;
using System.Threading;

namespace Couchbase.Lite
{
    public class BatcherTest : LiteTestCase
    {
        public const string Tag = "BatcherTest";

        private Int32 inboxCapacity;

        private Int32 processorDelay;

        private CountDownLatch doneSignal = null;

        private long maxObservedDelta = -1L;

        private readonly object mutex = new object();

        private void AssertNumbersConsecutive(IList<string> itemsToProcess)
        {
            var previousItemNumber = -1;
            foreach(var itemString in itemsToProcess)
            {
                if (previousItemNumber == -1)
                {
                    previousItemNumber = Int32.Parse(itemString);
                }
                else
                {
                    var curItemNumber = Int32.Parse(itemString);
                    Assert.IsTrue(curItemNumber == previousItemNumber + 1);
                    previousItemNumber = curItemNumber;
                }
            }
        }

        [Test]
        public void TestBatcherLatencyInitialBatch()
        {
            var doneEvent = new ManualResetEvent(false);

            inboxCapacity = 100;
            processorDelay = 500;

            var timeProcessed = default(DateTime);
            var scheduler = new SingleThreadTaskScheduler();
            var batcher = new Batcher<string>(
                new TaskFactory(scheduler), 
                inboxCapacity,
                processorDelay, 
                itemsToProcess =>
                {
                    Log.V(Tag, "process called with: " + itemsToProcess);

                    timeProcessed = DateTime.UtcNow;

                    doneEvent.Set();
                });

            var objectsToQueue = new List<string>();
            for (var i = 0; i < inboxCapacity + 1; i++) {
                objectsToQueue.Add(i.ToString());
            }

            var timeQueued = DateTime.UtcNow;

            batcher.QueueObjects(objectsToQueue);

            var success = doneEvent.WaitOne(TimeSpan.FromSeconds(35));
            Assert.IsTrue(success);

            var delta = (timeProcessed - timeQueued).TotalMilliseconds;
            Assert.IsTrue(delta >= 0);

            // we want the delta between the time it was queued until the
            // time it was processed to be as small as possible.  since
            // there is some overhead, rather than using a hardcoded number
            // express it as a ratio of the processor delay, asserting
            // that the entire processor delay never kicked in.
            int acceptableDelta = processorDelay - 1;

            Log.V(Tag, string.Format("TestBatcherLatencyInitialBatch : delta: {0}", delta));

            Assert.IsTrue(delta < acceptableDelta);
        }

        [Test]
        public void TestBatcherLatencyTrickleIn()
        {
            doneSignal = new CountDownLatch(10);

            inboxCapacity = 100;
            processorDelay = 500;

            maxObservedDelta = -1L;

            var scheduler = new SingleThreadTaskScheduler();
            var batcher = new Batcher<long>(new TaskFactory(scheduler), 
                inboxCapacity, processorDelay, TestBatcherLatencyTrickleInProcessor);
                
            for (var i = 0; i < 10; i++)
            {            
                var objectsToQueue = new List<long>();
                objectsToQueue.Add(Runtime.CurrentTimeMillis());
                batcher.QueueObjects(objectsToQueue);
                System.Threading.Thread.Sleep(1000);
            }

            var success = doneSignal.Await(TimeSpan.FromSeconds(35));
            Assert.IsTrue(success);

            // we want the max observed delta between the time it was queued until the
            // time it was processed to be as small as possible.  since
            // there is some overhead, rather than using a hardcoded number
            // express it as a ratio of 1/4th the processor delay, asserting
            // that the entire processor delay never kicked in.
            int acceptableMaxDelta = processorDelay - 1;

            Log.V(Tag, string.Format("TestBatcherLatencyTrickleIn : maxObservedDelta: {0}", maxObservedDelta));

            Assert.IsTrue((maxObservedDelta < acceptableMaxDelta));
        }

        public void TestBatcherLatencyTrickleInProcessor(IList<long> itemsToProcess)
        {
            if (itemsToProcess.Count != 1)
            {
                throw new RuntimeException("Unexpected itemsToProcess");
            }

            var timeSubmmitted = itemsToProcess[0];
            long delta = Runtime.CurrentTimeMillis() - timeSubmmitted;

            lock (mutex)
            {
                if (delta > maxObservedDelta)
                {
                    maxObservedDelta = delta;
                }
            }

            doneSignal.CountDown();
        }

        [Test]
        public void TestBatcherSingleBatch()
        {
            doneSignal = new CountDownLatch(10);

            inboxCapacity = 10;
            processorDelay = 1000;

            var scheduler = new SingleThreadTaskScheduler();
            var batcher = new Batcher<string>(new TaskFactory(scheduler), 
                inboxCapacity, processorDelay, TestBatcherSingleBatchProcessor);

            var objectsToQueue = new List<string>();
            for (var i = 0; i < inboxCapacity * 10; i++)
            {
                objectsToQueue.Add(i.ToString());
            }

            batcher.QueueObjects(objectsToQueue);

            var success = doneSignal.Await(TimeSpan.FromSeconds(35));
            Assert.IsTrue(success);
        }

        public void TestBatcherSingleBatchProcessor(IList<string> itemsToProcess)
        {
            Log.V(Tag, "TestBatcherSingleBatchProcessor : process called with : " + itemsToProcess.Count);

            AssertNumbersConsecutive(itemsToProcess);

            doneSignal.CountDown();
        }

        [Test]
        public void TestBatcherBatchSize5()
        {
            doneSignal = new CountDownLatch(10);

            inboxCapacity = 10;
            processorDelay = 1000;

            var scheduler = new SingleThreadTaskScheduler();
            var batcher = new Batcher<string>(new TaskFactory(scheduler), 
                inboxCapacity, processorDelay, TestBatcherBatchSize5Processor);

            var objectsToQueue = new List<string>();
            for (var i = 0; i < inboxCapacity * 10; i++)
            {
                objectsToQueue.Add(i.ToString());
                if (objectsToQueue.Count == 5)
                {
                    batcher.QueueObjects(objectsToQueue);
                    objectsToQueue.Clear();
                }
            }

            var success = doneSignal.Await(TimeSpan.FromSeconds(35));
            Assert.IsTrue(success);
        }

        public void TestBatcherBatchSize5Processor(IList<string> itemsToProcess)
        {
            Log.V(Tag, "TestBatcherBatchSize5 : process called with : " + itemsToProcess.Count);

            AssertNumbersConsecutive(itemsToProcess);

            doneSignal.CountDown();
        }
    }
}
