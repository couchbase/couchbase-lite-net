//
// Thread.cs
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
namespace Sharpen
{
    using System;
    using System.Threading;
    using System.Collections.Generic;

    internal class SharpenThread : Runnable
    {
        private static ThreadGroup defaultGroup = new ThreadGroup ();
        private bool interrupted;
        private Runnable runnable;
        private ThreadGroup tgroup;
        private System.Threading.Thread thread;
        
        [ThreadStatic]
        private static Sharpen.SharpenThread wrapperThread;

        public SharpenThread () : this(null, null, null)
        {
        }

        public SharpenThread (string name) : this (null, null, name)
        {
        }

        public SharpenThread (ThreadGroup grp, string name) : this (null, grp, name)
        {
        }

        public SharpenThread (Runnable runnable): this (runnable, null, null)
        {
        }
        
        public SharpenThread (Runnable runnable, string name): this (runnable, null, name)
        {
        }

        SharpenThread (Runnable runnable, ThreadGroup grp, string name)
        {
            thread = new System.Threading.Thread (new ThreadStart (InternalRun));
            
            this.runnable = runnable ?? this;
            tgroup = grp ?? defaultGroup;
            tgroup.Add (this);
            if (name != null)
                thread.Name = name;
        }

        private SharpenThread (System.Threading.Thread t)
        {
            thread = t;
            tgroup = defaultGroup;
            tgroup.Add (this);
        }

        public static Sharpen.SharpenThread CurrentThread ()
        {
            if (wrapperThread == null) {
                wrapperThread = new Sharpen.SharpenThread (System.Threading.Thread.CurrentThread);
            }
            return wrapperThread;
        }

        public string GetName ()
        {
            return thread.Name;
        }

        public ThreadGroup GetThreadGroup ()
        {
            return tgroup;
        }

        private void InternalRun ()
        {
            wrapperThread = this;
            try {
                runnable.Run ();
            } catch (Exception exception) {
                Console.WriteLine (exception);
            } finally {
                tgroup.Remove (this);
            }
        }
        
        public static void Yield ()
        {
        }

        public void Interrupt ()
        {
            lock (thread) {
                interrupted = true;
                thread.Interrupt ();
            }
        }

        public static bool Interrupted ()
        {
            if (Sharpen.SharpenThread.wrapperThread == null) {
                return false;
            }
            Sharpen.SharpenThread wrapperThread = Sharpen.SharpenThread.wrapperThread;
            lock (wrapperThread) {
                bool interrupted = Sharpen.SharpenThread.wrapperThread.interrupted;
                Sharpen.SharpenThread.wrapperThread.interrupted = false;
                return interrupted;
            }
        }

        public bool IsAlive ()
        {
            return thread.IsAlive;
        }

        public void Join ()
        {
            thread.Join ();
        }

        public void Join (long timeout)
        {
            thread.Join ((int)timeout);
        }

        public virtual void Run ()
        {
        }

        public void SetDaemon (bool daemon)
        {
            thread.IsBackground = daemon;
        }

        public void SetName (string name)
        {
            thread.Name = name;
        }

//      public static void Sleep (long milis)
//      {
//          System.Threading.Thread.Sleep ((int)milis);
//      }

        public void Start ()
        {
            thread.Start ();
        }
        
        public void Abort ()
        {
            thread.Abort ();
        }
        
    }

    internal class ThreadGroup
    {
        private List<SharpenThread> threads = new List<SharpenThread> ();
        
        public ThreadGroup()
        {
        }
        
        public ThreadGroup (string name)
        {
        }

        internal void Add (SharpenThread t)
        {
            lock (threads) {
                threads.Add (t);
            }
        }
        
        internal void Remove (SharpenThread t)
        {
            lock (threads) {
                threads.Remove (t);
            }
        }

        public int Enumerate (SharpenThread[] array)
        {
            lock (threads) {
                int count = Math.Min (array.Length, threads.Count);
                threads.CopyTo (0, array, 0, count);
                return count;
            }
        }
    }
}
