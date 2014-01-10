/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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
using System.Threading.Tasks;
using System.Threading;
using Couchbase.Lite.Internal;

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
        private readonly TaskFactory workExecutor;

        private Task flushFuture;

		private readonly int capacity;

		private readonly int delay;

		private IList<T> inbox;

        private readonly Action<IList<T>> processor;

        private readonly Action processNowRunnable;

        private readonly Object locker;

        public Batcher(TaskFactory workExecutor, int capacity, int delay, Action<IList<T>> processor, CancellationTokenSource tokenSource = null)
		{
            processNowRunnable = new Action(()=>
            {
                try
                {
                  ProcessNow();
                }
                catch (Exception e)
                {
                  // we don't want this to crash the batcher
                  Log.E(Database.Tag, "BatchProcessor throw exception", e);
                }
            });
            this.locker = new Object ();
			this.workExecutor = workExecutor;
            this.cancellationSource = tokenSource;
			this.capacity = capacity;
			this.delay = delay;
			this.processor = processor;
		}

		public void ProcessNow()
		{
			IList<T> toProcess = null;
			lock (locker)
			{
				if (inbox == null || inbox.Count == 0)
				{
					return;
				}
				toProcess = inbox;
				inbox = null;
				flushFuture = null;
			}
			if (toProcess != null)
			{
                processor(toProcess);
			}
		}

        CancellationTokenSource cancellationSource;

		public void QueueObject(T obj)
		{
			lock (locker)
            {
				if (inbox != null && inbox.Count >= capacity)
				{
					Flush();
				}
				if (inbox == null)
				{
					inbox = new AList<T>();
					if (workExecutor != null)
					{
                        cancellationSource = new CancellationTokenSource();
                        flushFuture = Task.Delay(delay).ContinueWith(task => { processNowRunnable (); }, cancellationSource.Token);
					}
				}
				inbox.AddItem(obj);
			}
		}

		public void Flush()
		{
			lock (locker)
			{
				if (inbox != null)
				{
                    var didcancel = false;
					if (flushFuture != null)
					{
                        try {
                            cancellationSource.Cancel(false);
                            didcancel = true;
                        } catch (Exception) { } // Swallow it.
					}
					//assume if we didn't cancel it was because it was already running
					if (didcancel)
					{
						ProcessNow();
					}
					else
					{
						Log.V(Database.Tag, "skipping process now because didcancel false");
					}
				}
			}
		}

		public int Count()
		{
            lock (locker) {
                if (inbox == null) {
                    return 0;
                }
                return inbox.Count;
            }
		}

		public void Close()
		{
		}
	}
}
