//
// LiteTestCase.cs
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
#if VERBOSE
#define __DEBUGGER__
#define __CONSOLE__
#endif

using System;
using System.Diagnostics;

namespace Couchbase.Lite.Util
{
    class CouchbaseTraceListener : DefaultTraceListener
    {
        static readonly string Indent = "    ";

        SourceLevels Level;

        string Category;

        public CouchbaseTraceListener() { }

        public CouchbaseTraceListener(SourceLevels logLevel)
        {
            Level = logLevel;
            Name = "Couchbase";
            TraceOutputOptions = Level.HasFlag(SourceLevels.All)
                ? TraceOptions.ThreadId | TraceOptions.DateTime | TraceOptions.Timestamp
                : TraceOptions.None;
        }


        void WriteOptionalTraceInfo()
        {
            #if !__MOBILE__
            var traceInfo = new TraceEventCache();
            if (TraceOutputOptions.HasFlag(TraceOptions.ThreadId))
            {
                PrintThreadId(traceInfo);
            }
            if (TraceOutputOptions.HasFlag(TraceOptions.DateTime))
            {
                PrintDateTime(traceInfo);
            }
            if (TraceOutputOptions.HasFlag(TraceOptions.Timestamp))
            {
                PrintTimeStamp(traceInfo);
            }
            #endif
        }

    #if !__MOBILE__
        void PrintThreadId(TraceEventCache info)
        {
            #if __DEBUGGER__
            Debugger.Log((int)SourceLevels.Critical, Category, "Thread Name: " + System.Threading.Thread.CurrentThread.Name + Environment.NewLine);
            #endif
            #if __CONSOLE__
            WriteIndent();
            Console.Out.Write("Thread Name: ");
            Console.Out.Write(info.ThreadId);
            Console.Out.Write(Environment.NewLine);
            #endif
        }

        void PrintDateTime(TraceEventCache info)
        {
            #if __DEBUGGER__
            Debugger.Log((int)SourceLevels.Critical, Category, "Date Time: " + DateTime.Now.ToString("o") + Environment.NewLine);
            #endif
            #if __CONSOLE__
            WriteIndent();
            Console.Out.Write("Date Time:   ");
            Console.Out.Write(info.DateTime);
            Console.Out.Write(Environment.NewLine);
            #endif
        }

        void PrintTimeStamp(TraceEventCache info)
        {
            #if __DEBUGGER__
            Debugger.Log((int)SourceLevels.Critical, Category, "Date Time: " + DateTime.Now + Environment.NewLine);
            #endif
            #if __CONSOLE__
            WriteIndent();
            Console.Out.Write("Timestamp:   ");
            Console.Out.Write(info.Timestamp);
            Console.Out.Write(Environment.NewLine);
            #endif
        }
    #endif
        public void WriteLine(SourceLevels level, string message, string category)
        {
            Level = level;
            WriteLine(message, category);
        }


        #region implemented abstract members of TraceListener

        public override string Name { get; set; }

        public override void Write(string message)
        {
            #if __DEBUGGER__
            Debugger.Log((int)Level, null, message);
            #endif
            #if __CONSOLE__
            Console.Out.Write(message);
            Console.Out.Flush();
            #endif
        }
        
        public override void WriteLine(string message)
        {
            WriteLine(message, null);
        }

        public override void WriteLine(string message, string category)
        {
            WriteOptionalTraceInfo();
            #if __DEBUGGER__
            Debugger.Log((int)Level, category, message + Environment.NewLine);
            #endif
            #if __CONSOLE__
            Console.Out.Write(category);
            Console.Out.Write(": ");
            Console.Out.Write(message);
            Console.Out.Write(Environment.NewLine);
            Console.Out.Flush();
            #endif
        }      

        public override void Write(object o)
        {
            Write(o.ToString());
        }

        public override void Write(object o, string category)
        {
            Write(o.ToString(), category);
        }

        public override void WriteLine(object o)
        {
            WriteLine(o.ToString());
        }

        public override void WriteLine(object o, string category)
        {
            WriteLine(o.ToString(), category);
        }

        public override void Write(string message, string category)
        {
            #if __DEBUGGER__
            Debugger.Log((int)Level, category, message);
            #endif
            #if __CONSOLE__
            Console.Out.Write(category);
            Console.Out.Write(": ");
            Console.Out.Write(message);
            Console.Out.Flush();
            #endif
        }

        protected override void WriteIndent()
        {
            Write(Indent);
        }

        public override void Fail (string message)
        {
            WriteOptionalTraceInfo();

            #if __DEBUGGER__
            Debugger.Log((int)SourceLevels.Critical, Category, message + Environment.NewLine);
            #endif
            #if __CONSOLE__
            Console.Out.Write(message);
            Console.Out.Write(Environment.NewLine);
            #endif
        }

        public override void Fail (string message, string detailMessage)
        {
            Fail (message);
            var lines = detailMessage.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach(var line in lines)
            {
                WriteIndent();
                Write(line);
                Write(Environment.NewLine);
            }
        }
        #endregion
    }
}
