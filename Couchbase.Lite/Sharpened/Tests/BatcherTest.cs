/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using System;
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class BatcherTest : LiteTestCase
	{
		/// <exception cref="System.Exception"></exception>
		public virtual void TestBatcherSingleBatch()
		{
			CountDownLatch doneSignal = new CountDownLatch(10);
			ScheduledExecutorService workExecutor = new ScheduledThreadPoolExecutor(1);
			int inboxCapacity = 10;
			int processorDelay = 1000;
			Batcher batcher = new Batcher<string>(workExecutor, inboxCapacity, processorDelay
				, new _BatchProcessor_25(doneSignal));
			// add this to make it a bit more realistic
			AList<string> objectsToQueue = new AList<string>();
			for (int i = 0; i < inboxCapacity * 10; i++)
			{
				objectsToQueue.AddItem(Sharpen.Extensions.ToString(i));
			}
			batcher.QueueObjects(objectsToQueue);
			bool didNotTimeOut = doneSignal.Await(5, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(didNotTimeOut);
		}

		private sealed class _BatchProcessor_25 : BatchProcessor<string>
		{
			public _BatchProcessor_25(CountDownLatch doneSignal)
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
				, new _BatchProcessor_64(processorDelay, doneSignal));
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

		private sealed class _BatchProcessor_64 : BatchProcessor<string>
		{
			public _BatchProcessor_64(int processorDelay, CountDownLatch doneSignal)
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
				, new _BatchProcessor_106(processorDelay, doneSignal));
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

		private sealed class _BatchProcessor_106 : BatchProcessor<string>
		{
			public _BatchProcessor_106(int processorDelay, CountDownLatch doneSignal)
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
