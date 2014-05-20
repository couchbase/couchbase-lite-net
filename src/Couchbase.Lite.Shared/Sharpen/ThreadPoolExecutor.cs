//
// ThreadPoolExecutor.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
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
using System.Collections.Generic;
using ST = System.Threading;

namespace Sharpen
{
	class ThreadPoolExecutor
	{
		ThreadFactory tf;
		int corePoolSize;
		int maxPoolSize;
		List<Thread> pool = new List<Thread> ();
		int runningThreads;
		int freeThreads;
		bool shutdown;
		Queue<Runnable> pendingTasks = new Queue<Runnable> ();
		
		public ThreadPoolExecutor (int corePoolSize, ThreadFactory factory)
		{
			this.corePoolSize = corePoolSize;
			maxPoolSize = corePoolSize;
			tf = factory;
		}
		
		public void SetMaximumPoolSize (int size)
		{
			maxPoolSize = size;
		}
		
		public bool IsShutdown ()
		{
			return shutdown;
		}
		
		public virtual bool IsTerminated ()
		{
			lock (pendingTasks) {
				return shutdown && pendingTasks.Count == 0;
			}
		}
		
		public virtual bool IsTerminating ()
		{
			lock (pendingTasks) {
				return shutdown && !IsTerminated ();
			}
		}
		
		public int GetCorePoolSize ()
		{
			return corePoolSize;
		}
		
		public void PrestartAllCoreThreads ()
		{
			lock (pendingTasks) {
				while (runningThreads < corePoolSize)
					StartPoolThread ();
			}
		}
		
		public void SetThreadFactory (ThreadFactory f)
		{
			tf = f;
		}
		
		public void Execute (Runnable r)
		{
			InternalExecute (r, true);
		}
		
		internal void InternalExecute (Runnable r, bool checkShutdown)
		{
			lock (pendingTasks) {
				if (shutdown && checkShutdown)
					throw new InvalidOperationException ();
				if (runningThreads < corePoolSize) {
					StartPoolThread ();
				}
				else if (freeThreads > 0) {
					freeThreads--;
				}
				else if (runningThreads < maxPoolSize) {
					StartPoolThread ();
				}
				pendingTasks.Enqueue (r);
				ST.Monitor.PulseAll (pendingTasks);
			}
		}
		
		void StartPoolThread ()
		{
			runningThreads++;
			pool.Add (tf.NewThread (new RunnableAction (RunPoolThread)));
		}
		
		public void RunPoolThread ()
		{
			while (!IsTerminated ()) {
				try {
					Runnable r = null;
					lock (pendingTasks) {
						freeThreads++;
						while (!IsTerminated () && pendingTasks.Count == 0)
							ST.Monitor.Wait (pendingTasks);
						if (IsTerminated ())
							break;
						r = pendingTasks.Dequeue ();
					}
					if (r != null)
						r.Run ();
				}
				catch (ST.ThreadAbortException) {
					// Do not catch a thread abort. If we've been aborted just let the thread die.
					// Currently reseting an abort which was issued because the appdomain is being
					// torn down results in the process living forever and consuming 100% cpu time.
					return;
				}
				catch {
				}
			}
		}
		
		public virtual void Shutdown ()
		{
			lock (pendingTasks) {
				shutdown = true;
				ST.Monitor.PulseAll (pendingTasks);
			}
		}
		
		public virtual List<Runnable> ShutdownNow ()
		{
			lock (pendingTasks) {
				shutdown = true;
				foreach (var t in pool) {
					try {
						t.Abort ();
					} catch {}
				}
				pool.Clear ();
				freeThreads = 0;
				runningThreads = 0;
				var res = new List<Runnable> (pendingTasks);
				pendingTasks.Clear ();
				return res;
			}
		}
	}
	
	class RunnableAction: Runnable
	{
		Action action;
		
		public RunnableAction (Action a)
		{
			action = a;
		}
		
		public void Run ()
		{
			action ();
		}
	}
}
