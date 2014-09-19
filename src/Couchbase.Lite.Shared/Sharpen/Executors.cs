//
// Executors.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/*
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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
using System.Threading;
using System.Collections.Generic;
using SThread = System.Threading.Thread;

namespace Sharpen
{
    internal class Executors
    {
        static ThreadFactory defaultThreadFactory = new ThreadFactory ();
        
        public static ExecutorService NewFixedThreadPool (int threads)
        {
            return new FixedThreadPoolExecutorService ();
        }
        
        public static ThreadFactory DefaultThreadFactory ()
        {
            return defaultThreadFactory;
        }
    }
    
    internal class FixedThreadPoolExecutorService: ExecutorService
    {
        List<WaitHandle> tasks = new List<WaitHandle> ();
        bool shuttingDown;
        
        #region ExecutorService implementation
        public bool AwaitTermination (long n, Sharpen.TimeUnit unit)
        {
            WaitHandle[] handles;
            lock (tasks) {
                if (tasks.Count == 0)
                    return true;
                handles = tasks.ToArray ();
            }
            return WaitHandle.WaitAll (handles, (int) unit.Convert (n, TimeUnit.Milliseconds));
        }
    
        public void ShutdownNow ()
        {
            Shutdown ();
        }

        public void Shutdown ()
        {
            lock (tasks) {
                shuttingDown = true;
            }
        }
    
        public Future<T> Submit<T> (Sharpen.Callable<T> c)
        {
            TaskFuture<T> future = new TaskFuture<T> (this);
            lock (tasks) {
                if (shuttingDown)
                    throw new RejectedExecutionException ();
                tasks.Add (future.DoneEvent);
                ThreadPool.QueueUserWorkItem (delegate {
                    future.Run (c);
                });
            }
            return future;
        }
        
        internal void RemoveTask (WaitHandle handle)
        {
            lock (tasks) {
                tasks.Remove (handle);
            }
        }
        
        #endregion
    
        #region Executor implementation
        public void Execute (Sharpen.Runnable runnable)
        {
            throw new System.NotImplementedException ();
        }
        #endregion
    }
    
    internal interface FutureBase
    {
    }
    
    class TaskFuture<T>: Future<T>, FutureBase
    {
        SThread t;
        T result;
        ManualResetEvent doneEvent = new ManualResetEvent (false);
        Exception error;
        bool canceled;
        bool started;
        bool done;
        FixedThreadPoolExecutorService service;
        
        public TaskFuture (FixedThreadPoolExecutorService service)
        {
            this.service = service;
        }
        
        public WaitHandle DoneEvent {
            get { return doneEvent; }
        }
        
        public void Run (Callable<T> c)
        {
            try {
                lock (this) {
                    if (canceled)
                        return;
                    t = SThread.CurrentThread;
                    started = true;
                }
                result = c.Call ();
            } catch (ThreadAbortException ex) {
                SThread.ResetAbort ();
                error = ex;
            } catch (Exception ex) {
                error = ex;
            } finally {
                lock (this) {
                    done = true;
                    service.RemoveTask (doneEvent);
                }
                doneEvent.Set ();
            }
        }
        
        public bool Cancel (bool mayInterruptIfRunning)
        {
            lock (this) {
                if (done || canceled)
                    return false;
                canceled = true;
                doneEvent.Set ();
                if (started) {
                    if (mayInterruptIfRunning) {
                        try {
                            t.Abort ();
                        } catch {}
                    }
                    else
                        return false;
                }
                return true;
            }
        }
        
        public T Get ()
        {
            doneEvent.WaitOne ();
            if (canceled)
                throw new CancellationException ();
            
            if (error != null)
                throw new ExecutionException (error);
            else
                return result;
        }
    }
    
    internal class CancellationException: Exception
    {
    }
    
    internal class RejectedExecutionException: Exception
    {
    }
}
