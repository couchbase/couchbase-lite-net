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

		private sealed class _Runnable_30 : Runnable
		{
			public _Runnable_30(Batcher<T> _enclosing)
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
					Log.E(Database.Tag, this + ": BatchProcessor throw exception", e);
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
			processNowRunnable = new _Runnable_30(this);
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
				Log.D(Database.Tag, "queuObjects called with " + objects.Count + " objects. ");
				if (objects.Count == 0)
				{
					return;
				}
				if (inbox == null)
				{
					inbox = new LinkedHashSet<T>();
				}
				Log.D(Database.Tag, "inbox size before adding objects: " + inbox.Count);
				Sharpen.Collections.AddAll(inbox, objects);
				Log.D(Database.Tag, objects.Count + " objects added to inbox.  inbox size: " + inbox
					.Count);
				if (inbox.Count < capacity)
				{
					// Schedule the processing. To improve latency, if we haven't processed anything
					// in at least our delay time, rush these object(s) through ASAP:
					Log.D(Database.Tag, "inbox.size() < capacity, schedule processing");
					int delayToUse = delay;
					long delta = (Runtime.CurrentTimeMillis() - lastProcessedTime);
					if (delta >= delay)
					{
						Log.D(Database.Tag, "delta " + delta + " >= delay " + delay + " --> using delay 0"
							);
						delayToUse = 0;
					}
					else
					{
						Log.D(Database.Tag, "delta " + delta + " < delay " + delay + " --> using delay " 
							+ delayToUse);
					}
					ScheduleWithDelay(delayToUse);
				}
				else
				{
					// If inbox fills up, process it immediately:
					Log.D(Database.Tag, "inbox.size() >= capacity, process immediately");
					Unschedule();
					ProcessNow();
				}
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
			Unschedule();
			ProcessNow();
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
			Log.D(Database.Tag, this + ": clear() called, setting inbox to null");
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
			Log.D(Database.Tag, this + ": processNow() called");
			scheduled = false;
			IList<T> toProcess = new AList<T>();
			lock (this)
			{
				if (inbox == null || inbox.Count == 0)
				{
					Log.D(Database.Tag, this + ": processNow() called, but inbox is empty");
					return;
				}
				else
				{
					if (inbox.Count <= capacity)
					{
						Log.D(Database.Tag, this + ": processNow() called, inbox size: " + inbox.Count);
						Log.D(Database.Tag, this + ": inbox.size() <= capacity, adding " + inbox.Count + 
							" items to toProcess array");
						Sharpen.Collections.AddAll(toProcess, inbox);
						inbox = null;
					}
					else
					{
						Log.D(Database.Tag, this + ": processNow() called, inbox size: " + inbox.Count);
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
							Log.D(Database.Tag, this + ": processNow() removing " + item_1 + " from inbox");
							inbox.Remove(item_1);
						}
						Log.D(Database.Tag, this + ": inbox.size() > capacity, moving " + toProcess.Count
							 + " items from inbox -> toProcess array");
						// There are more objects left, so schedule them Real Soon:
						ScheduleWithDelay(0);
					}
				}
			}
			if (toProcess != null && toProcess.Count > 0)
			{
				Log.D(Database.Tag, this + ": invoking processor with " + toProcess.Count + " items "
					);
				processor.Process(toProcess);
			}
			else
			{
				Log.D(Database.Tag, this + ": nothing to process");
			}
			lastProcessedTime = Runtime.CurrentTimeMillis();
		}

		private void ScheduleWithDelay(int suggestedDelay)
		{
			Log.D(Database.Tag, "scheduleWithDelay called with delay: " + suggestedDelay + " ms"
				);
			if (scheduled && (suggestedDelay < scheduledDelay))
			{
				Log.D(Database.Tag, "already scheduled and : " + suggestedDelay + " < " + scheduledDelay
					 + " --> unscheduling");
				Unschedule();
			}
			if (!scheduled)
			{
				Log.D(Database.Tag, "not already scheduled");
				scheduled = true;
				scheduledDelay = suggestedDelay;
				Log.D(Database.Tag, "workExecutor.schedule() with delay: " + suggestedDelay + " ms"
					);
				flushFuture = workExecutor.Schedule(processNowRunnable, suggestedDelay, TimeUnit.
					Milliseconds);
			}
		}

		private void Unschedule()
		{
			Log.D(Database.Tag, this + ": unschedule() called");
			scheduled = false;
			if (flushFuture != null)
			{
				bool didCancel = flushFuture.Cancel(false);
				Log.D(Database.Tag, "tried to cancel flushFuture, result: " + didCancel);
			}
			else
			{
				Log.D(Database.Tag, "flushFuture was null, doing nothing");
			}
		}
	}
}
