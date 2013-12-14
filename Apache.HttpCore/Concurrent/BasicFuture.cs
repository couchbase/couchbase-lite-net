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
using Org.Apache.Http.Concurrent;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Concurrent
{
	/// <summary>
	/// Basic implementation of the
	/// <see cref="Sharpen.Future{V}">Sharpen.Future&lt;V&gt;</see>
	/// interface. <tt>BasicFuture<tt>
	/// can be put into a completed state by invoking any of the following methods:
	/// <see cref="BasicFuture{T}.Cancel()">BasicFuture&lt;T&gt;.Cancel()</see>
	/// ,
	/// <see cref="BasicFuture{T}.Failed(System.Exception)">BasicFuture&lt;T&gt;.Failed(System.Exception)
	/// 	</see>
	/// , or
	/// <see cref="BasicFuture{T}.Completed(object)">BasicFuture&lt;T&gt;.Completed(object)
	/// 	</see>
	/// .
	/// </summary>
	/// <?></?>
	/// <since>4.2</since>
    public class BasicFuture<T> : Future<T>, Cancellable where T: class
	{
		private readonly FutureCallback<T> callback;

		private volatile bool completed;

		private volatile bool cancelled;

		private volatile T result;

		private volatile Exception ex;

		public BasicFuture(FutureCallback<T> callback) : base()
		{
			this.callback = callback;
		}

		public virtual bool IsCancelled()
		{
			return this.cancelled;
		}

		public virtual bool IsDone()
		{
			return this.completed;
		}

		/// <exception cref="Sharpen.ExecutionException"></exception>
		private T GetResult()
		{
			if (this.ex != null)
			{
				throw new ExecutionException(this.ex);
			}
			return this.result;
		}

		/// <exception cref="System.Exception"></exception>
		/// <exception cref="Sharpen.ExecutionException"></exception>
		public virtual T Get()
		{
			lock (this)
			{
				while (!this.completed)
				{
					Sharpen.Runtime.Wait(this);
				}
				return GetResult();
			}
		}

		/// <exception cref="System.Exception"></exception>
		/// <exception cref="Sharpen.ExecutionException"></exception>
		/// <exception cref="Sharpen.TimeoutException"></exception>
		public virtual T Get(long timeout, TimeUnit unit)
		{
			lock (this)
			{
				Args.NotNull(unit, "Time unit");
                long msecs = unit.Convert(timeout, TimeUnit.MILLISECONDS);
				long startTime = (msecs <= 0) ? 0 : Runtime.CurrentTimeMillis();
				long waitTime = msecs;
				if (this.completed)
				{
					return GetResult();
				}
				else
				{
					if (waitTime <= 0)
					{
						throw new TimeoutException();
					}
					else
					{
						for (; ; )
						{
							Sharpen.Runtime.Wait(this, waitTime);
							if (this.completed)
							{
								return GetResult();
							}
							else
							{
								waitTime = msecs - (Runtime.CurrentTimeMillis() - startTime);
								if (waitTime <= 0)
								{
									throw new TimeoutException();
								}
							}
						}
					}
				}
			}
		}

		public virtual bool Completed(T result)
		{
			lock (this)
			{
				if (this.completed)
				{
					return false;
				}
				this.completed = true;
				this.result = result;
				Sharpen.Runtime.NotifyAll(this);
			}
			if (this.callback != null)
			{
				this.callback.Completed(result);
			}
			return true;
		}

		public virtual bool Failed(Exception exception)
		{
			lock (this)
			{
				if (this.completed)
				{
					return false;
				}
				this.completed = true;
				this.ex = exception;
				Sharpen.Runtime.NotifyAll(this);
			}
			if (this.callback != null)
			{
				this.callback.Failed(exception);
			}
			return true;
		}

		public virtual bool Cancel(bool mayInterruptIfRunning)
		{
			lock (this)
			{
				if (this.completed)
				{
					return false;
				}
				this.completed = true;
				this.cancelled = true;
				Sharpen.Runtime.NotifyAll(this);
			}
			if (this.callback != null)
			{
				this.callback.Cancelled();
			}
			return true;
		}

		public virtual bool Cancel()
		{
			return Cancel(true);
		}
	}
}
