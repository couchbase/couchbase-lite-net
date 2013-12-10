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

using Couchbase.Threading;
using Sharpen;

namespace Couchbase.Threading
{
	public abstract class BackgroundTask : Runnable
	{
		private const int CorePoolSize = 5;

		private const int MaximumPoolSize = 128;

		private const int KeepAlive = 1;

		private sealed class _ThreadFactory_33 : ThreadFactory
		{
			public _ThreadFactory_33()
			{
				this.mCount = new AtomicInteger(1);
			}

			private readonly AtomicInteger mCount;

			public Sharpen.Thread NewThread(Runnable r)
			{
				return new Sharpen.Thread(r, "BackgroundTask #" + this.mCount.GetAndIncrement());
			}
		}

		private static readonly ThreadFactory sThreadFactory = new _ThreadFactory_33();

		private static readonly BlockingQueue<Runnable> sPoolWorkQueue = new LinkedBlockingQueue
			<Runnable>(10);

		public static readonly Executor ThreadPoolExecutor = new ThreadPoolExecutor(CorePoolSize
			, MaximumPoolSize, KeepAlive, TimeUnit.Seconds, sPoolWorkQueue, sThreadFactory);

		public static readonly Executor SerialExecutor = new BackgroundTask.SerialExecutor
			();

		private static volatile Executor sDefaultExecutor = SerialExecutor;


		public void Execute()
		{
			sDefaultExecutor.Execute(this);
		}

		public abstract void Run();
	}

     class SerialExecutor : Executor
    {
        internal readonly ArrayDeque<Runnable> mTasks = new ArrayDeque<Runnable>();

        internal Runnable mActive;

        // An Executor that can be used to execute tasks in parallel.
        // An Executor that executes tasks one at a time in serial order.  This
        // serialization is global to a particular process.
        public virtual void Execute(Runnable r)
        {
            lock (this)
            {
                mTasks.Offer(new _Runnable_58(this, r));
                if (mActive == null)
                {
                    ScheduleNext();
                }
            }
        }

        private sealed class _Runnable_58 : Runnable
        {
            public _Runnable_58(SerialExecutor _enclosing, Runnable r)
            {
                this._enclosing = _enclosing;
                this.r = r;
            }

            public void Run()
            {
                try
                {
                    r.Run();
                }
                finally
                {
                    this._enclosing.ScheduleNext();
                }
            }

            private readonly SerialExecutor _enclosing;

            private readonly Runnable r;
        }

        protected internal virtual void ScheduleNext()
        {
            lock (this)
            {
                if ((mActive = mTasks.Poll()) != null)
                {
                    ThreadPoolExecutor.Execute(mActive);
                }
            }
        }
    }
}

