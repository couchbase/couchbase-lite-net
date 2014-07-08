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
//using Couchbase.Lite.Threading;
using Sharpen;

namespace Couchbase.Lite.Threading
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

		private class SerialExecutor : Executor
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

		public void Execute()
		{
			sDefaultExecutor.Execute(this);
		}

		public abstract void Run();
	}
}
