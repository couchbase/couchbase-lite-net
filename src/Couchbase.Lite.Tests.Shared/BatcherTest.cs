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
using System.Threading;

namespace Couchbase.Lite
{
    public class BatcherTest : LiteTestCase
    {
        public const string Tag = "BatcherTest";

        private Int32 inboxCapacity;

        private Int32 processorDelay;

        private CountdownEvent doneSignal = null;

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
        public void TestBatcherSingleBatch()
        {
            doneSignal = new CountdownEvent(10);

            inboxCapacity = 10;
            processorDelay = 1000;

            var scheduler = new SingleTaskThreadpoolScheduler();
            var batcher = new Batcher<string>(new TaskFactory(scheduler), 
                inboxCapacity, processorDelay, TestBatcherSingleBatchProcessor);

            var objectsToQueue = new List<string>();
            for (var i = 0; i < inboxCapacity * 10; i++)
            {
                objectsToQueue.Add(i.ToString());
            }

            batcher.QueueObjects(objectsToQueue);

            var success = doneSignal.Wait(TimeSpan.FromSeconds(35));
            Assert.IsTrue(success);
        }

        public void TestBatcherSingleBatchProcessor(IList<string> itemsToProcess)
        {
            Log.V(Tag, "TestBatcherSingleBatchProcessor : process called with : " + itemsToProcess.Count);

            AssertNumbersConsecutive(itemsToProcess);

            doneSignal.Signal();
        }

        [Test]
        public void TestBatcherBatchSize5()
        {
            doneSignal = new CountdownEvent(10);

            inboxCapacity = 10;
            processorDelay = 1000;

            var scheduler = new SingleTaskThreadpoolScheduler();
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

            var success = doneSignal.Wait(TimeSpan.FromSeconds(35));
            Assert.IsTrue(success);
        }

        public void TestBatcherBatchSize5Processor(IList<string> itemsToProcess)
        {
            Log.V(Tag, "TestBatcherBatchSize5 : process called with : " + itemsToProcess.Count);

            AssertNumbersConsecutive(itemsToProcess);

            doneSignal.Signal();
        }

        [Test]
        public void TestBatcherCancel()
        {
            var mre = new ManualResetEventSlim();
            var scheduler = new SingleTaskThreadpoolScheduler();
            var batcher = new Batcher<int>(new TaskFactory(scheduler), 5, 500, (inbox) =>
            {
                mre.Set();
            });

            batcher.QueueObject(0);
            Assert.IsTrue(mre.Wait(1000), "Batcher didn't initially run");
            mre.Reset();

            batcher.QueueObject(0);
            batcher.Clear();
            Assert.False(mre.Wait(TimeSpan.FromSeconds(1)), "Batcher ran after being cancelled");
        }

        [Test]
        public void TestBatcherAddAfterCancel()
        {
            var evt = new CountdownEvent(1);
            var scheduler = new SingleTaskThreadpoolScheduler();
            var batcher = new Batcher<int>(new TaskFactory(scheduler), 5, 500, (inbox) =>
            {
                evt.Signal();
            });

            batcher.QueueObject(0);
            Assert.IsTrue(evt.Wait(1000), "Batcher didn't initially run");
            evt.Reset(2);

            batcher.QueueObject(0);
            batcher.Clear();
            batcher.QueueObject(0);
            Assert.False(evt.Wait(TimeSpan.FromSeconds(1)), "Batcher ran too many times");
            Assert.True(evt.CurrentCount == 1, "Batcher never ran");
        }
    }
}
