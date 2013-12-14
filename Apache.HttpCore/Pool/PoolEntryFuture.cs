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
using System.IO;
using Org.Apache.Http.Concurrent;
using Org.Apache.Http.Util;
using Sharpen;
using System.Threading;

namespace Org.Apache.Http.Pool
{
	internal abstract class PoolEntryFuture<T> : Future<T>
	{
		private readonly FutureCallback<T> callback;

        private readonly Semaphore condition;

		private volatile bool cancelled;

		private volatile bool completed;

		private T result;

        internal PoolEntryFuture(Semaphore semaphore, FutureCallback<T> callback) : base()
		{
            this.condition = semaphore;
			this.callback = callback;
		}

		public virtual bool Cancel(bool mayInterruptIfRunning)
		{
            Monitor.Enter(this);
			try
			{
				if (this.completed)
				{
					return false;
				}
				this.completed = true;
				this.cancelled = true;
				if (this.callback != null)
				{
					this.callback.Cancelled();
				}
                this.condition.Release();
				return true;
			}
			finally
			{
                Monitor.Exit(this);
			}
		}

		public virtual bool IsCancelled()
		{
			return this.cancelled;
		}

		public virtual bool IsDone()
		{
			return this.completed;
		}

		/// <exception cref="System.Exception"></exception>
		/// <exception cref="Sharpen.ExecutionException"></exception>
		public virtual T Get()
		{
			try
			{
                return Get(0, TimeUnit.MILLISECONDS);
			}
			catch (TimeoutException ex)
			{
				throw new ExecutionException(ex);
			}
		}

		/// <exception cref="System.Exception"></exception>
		/// <exception cref="Sharpen.ExecutionException"></exception>
		/// <exception cref="Sharpen.TimeoutException"></exception>
		public virtual T Get(long timeout, TimeUnit unit)
		{
			Args.NotNull(unit, "Time unit");
            Monitor.Enter(this);
			try
			{
				if (this.completed)
				{
					return this.result;
				}
				this.result = GetPoolEntry(timeout, unit);
				this.completed = true;
				if (this.callback != null)
				{
					this.callback.Completed(this.result);
				}
				return result;
			}
			catch (IOException ex)
			{
				this.completed = true;
                this.result = default(T);
				if (this.callback != null)
				{
					this.callback.Failed(ex);
				}
				throw new ExecutionException(ex);
			}
			finally
			{
                Monitor.Exit(this);
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="System.Exception"></exception>
		/// <exception cref="Sharpen.TimeoutException"></exception>
		protected internal abstract T GetPoolEntry(long timeout, TimeUnit unit);

		/// <exception cref="System.Exception"></exception>
		public virtual bool Await(DateTime deadline)
		{
            Monitor.Enter(this);
			try
			{
				if (this.cancelled)
				{
					throw new Exception("Operation interrupted");
				}
				bool success;
                var waitDuration = deadline.ToUniversalTime() - DateTime.UtcNow;
                if (waitDuration.TotalMilliseconds > 0)
				{
                    success = this.condition.WaitOne(waitDuration);
				}
				else
				{
                    this.condition.WaitOne();
					success = true;
				}
				if (this.cancelled)
				{
					throw new Exception("Operation interrupted");
				}
				return success;
			}
			finally
			{
                Monitor.Exit(this);
			}
		}

		public virtual void Wakeup()
		{
            Monitor.Enter(this);
			try
			{
                this.condition.Release();
			}
			finally
			{
                Monitor.Exit(this);
			}
		}
	}
}
