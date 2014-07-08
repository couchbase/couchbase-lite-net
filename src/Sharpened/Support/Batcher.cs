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
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
	/// <summary>
	/// Utility that queues up objects until the queue fills up or a time interval elapses,
	/// then passes objects, in groups of its capacity, to a client-supplied processor block.
	/// </summary>
	/// <remarks>
	/// Utility that queues up objects until the queue fills up or a time interval elapses,
	/// then passes objects, in groups of its capacity, to a client-supplied processor block.
	/// </remarks>
	public class Batcher<T>
	{
		private ScheduledExecutorService workExecutor;

		private ScheduledFuture<object> flushFuture;

		private int capacity;

		private int delay;

		private int scheduledDelay;

		private LinkedHashSet<T> inbox;

		private BatchProcessor<T> processor;

		private bool scheduled = false;

		private long lastProcessedTime;

		private sealed class _Runnable_31 : Runnable
		{
			public _Runnable_31(Batcher<T> _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Run()
			{
				try
				{
					this._enclosing.ProcessNow();
				}
				catch (Exception e)
				{
					// we don't want this to crash the batcher
					Log.E(Log.TagSync, this + ": BatchProcessor throw exception", e);
				}
			}

			private readonly Batcher<T> _enclosing;
		}

		private Runnable processNowRunnable;

		/// <summary>Initializes a batcher.</summary>
		/// <remarks>Initializes a batcher.</remarks>
		/// <param name="workExecutor">the work executor that performs actual work</param>
		/// <param name="capacity">The maximum number of objects to batch up. If the queue reaches this size, the queued objects will be sent to the processor immediately.
		/// 	</param>
		/// <param name="delay">The maximum waiting time to collect objects before processing them. In some circumstances objects will be processed sooner.
		/// 	</param>
		/// <param name="processor">The callback/block that will be called to process the objects.
		/// 	</param>
		public Batcher(ScheduledExecutorService workExecutor, int capacity, int delay, BatchProcessor
			<T> processor)
		{
			processNowRunnable = new _Runnable_31(this);
			this.workExecutor = workExecutor;
			this.capacity = capacity;
			this.delay = delay;
			this.processor = processor;
		}

		/// <summary>Adds multiple objects to the queue.</summary>
		/// <remarks>Adds multiple objects to the queue.</remarks>
		public virtual void QueueObjects(IList<T> objects)
		{
			lock (this)
			{
				Log.V(Log.TagSync, "%s: queueObjects called with %d objects. ", this, objects.Count
					);
				if (objects.Count == 0)
				{
					return;
				}
				if (inbox == null)
				{
					inbox = new LinkedHashSet<T>();
				}
				Log.V(Log.TagSync, "%s: inbox size before adding objects: %d", this, inbox.Count);
				Sharpen.Collections.AddAll(inbox, objects);
				ScheduleWithDelay(DelayToUse());
			}
		}

		/// <summary>Adds an object to the queue.</summary>
		/// <remarks>Adds an object to the queue.</remarks>
		public virtual void QueueObject(T @object)
		{
			IList<T> objects = Arrays.AsList(@object);
			QueueObjects(objects);
		}

		/// <summary>Sends queued objects to the processor block (up to the capacity).</summary>
		/// <remarks>Sends queued objects to the processor block (up to the capacity).</remarks>
		public virtual void Flush()
		{
			ScheduleWithDelay(DelayToUse());
		}

		/// <summary>Sends _all_ the queued objects at once to the processor block.</summary>
		/// <remarks>
		/// Sends _all_ the queued objects at once to the processor block.
		/// After this method returns, the queue is guaranteed to be empty.
		/// </remarks>
		public virtual void FlushAll()
		{
			while (inbox.Count > 0)
			{
				Unschedule();
				IList<T> toProcess = new AList<T>();
				Sharpen.Collections.AddAll(toProcess, inbox);
				processor.Process(toProcess);
				lastProcessedTime = Runtime.CurrentTimeMillis();
			}
		}

		/// <summary>Empties the queue without processing any of the objects in it.</summary>
		/// <remarks>Empties the queue without processing any of the objects in it.</remarks>
		public virtual void Clear()
		{
			Log.V(Log.TagSync, "%s: clear() called, setting inbox to null", this);
			Unschedule();
			inbox = null;
		}

		public virtual int Count()
		{
			lock (this)
			{
				if (inbox == null)
				{
					return 0;
				}
				return inbox.Count;
			}
		}

		private void ProcessNow()
		{
			Log.V(Log.TagSync, this + ": processNow() called");
			scheduled = false;
			IList<T> toProcess = new AList<T>();
			lock (this)
			{
				if (inbox == null || inbox.Count == 0)
				{
					Log.V(Log.TagSync, this + ": processNow() called, but inbox is empty");
					return;
				}
				else
				{
					if (inbox.Count <= capacity)
					{
						Log.V(Log.TagSync, "%s: inbox.size() <= capacity, adding %d items from inbox -> toProcess"
							, this, inbox.Count);
						Sharpen.Collections.AddAll(toProcess, inbox);
						inbox = null;
					}
					else
					{
						Log.V(Log.TagSync, "%s: processNow() called, inbox size: %d", this, inbox.Count);
						int i = 0;
						foreach (T item in inbox)
						{
							toProcess.AddItem(item);
							i += 1;
							if (i >= capacity)
							{
								break;
							}
						}
						foreach (T item_1 in toProcess)
						{
							Log.V(Log.TagSync, "%s: processNow() removing %s from inbox", this, item_1);
							inbox.Remove(item_1);
						}
						Log.V(Log.TagSync, "%s: inbox.size() > capacity, moving %d items from inbox -> toProcess array"
							, this, toProcess.Count);
						// There are more objects left, so schedule them Real Soon:
						ScheduleWithDelay(DelayToUse());
					}
				}
			}
			if (toProcess != null && toProcess.Count > 0)
			{
				Log.V(Log.TagSync, "%s: invoking processor with %d items ", this, toProcess.Count
					);
				processor.Process(toProcess);
			}
			else
			{
				Log.V(Log.TagSync, "%s: nothing to process", this);
			}
			lastProcessedTime = Runtime.CurrentTimeMillis();
		}

		private void ScheduleWithDelay(int suggestedDelay)
		{
			Log.V(Log.TagSync, "%s: scheduleWithDelay called with delay: %d ms", this, suggestedDelay
				);
			if (scheduled && (suggestedDelay < scheduledDelay))
			{
				Log.V(Log.TagSync, "%s: already scheduled and: %d < %d --> unscheduling", this, suggestedDelay
					, scheduledDelay);
				Unschedule();
			}
			if (!scheduled)
			{
				Log.V(Log.TagSync, "not already scheduled");
				scheduled = true;
				scheduledDelay = suggestedDelay;
				Log.V(Log.TagSync, "workExecutor.schedule() with delay: %d ms", suggestedDelay);
				flushFuture = workExecutor.Schedule(processNowRunnable, suggestedDelay, TimeUnit.
					Milliseconds);
			}
		}

		private void Unschedule()
		{
			Log.V(Log.TagSync, this + ": unschedule() called");
			scheduled = false;
			if (flushFuture != null)
			{
				bool didCancel = flushFuture.Cancel(false);
				Log.V(Log.TagSync, "tried to cancel flushFuture, result: %s", didCancel);
			}
			else
			{
				Log.V(Log.TagSync, "flushFuture was null, doing nothing");
			}
		}

		private int DelayToUse()
		{
			//initially set the delay to the default value for this Batcher
			int delayToUse = delay;
			//get the time interval since the last batch completed to the current system time
			long delta = (Runtime.CurrentTimeMillis() - lastProcessedTime);
			//if the time interval is greater or equal to the default delay then set the
			// delay so that the next batch gets scheduled to process immediately
			if (delta >= delay)
			{
				delayToUse = 0;
			}
			Log.V(Log.TagSync, "%s: delayToUse() delta: %d, delayToUse: %d, delay: %d", this, 
				delta, delayToUse, delta);
			return delayToUse;
		}
	}
}
