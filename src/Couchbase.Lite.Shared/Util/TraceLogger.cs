//
// CustomLogger.cs
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
using System.Diagnostics;
using System.Threading.Tasks;

namespace Couchbase.Lite.Util
{
    internal sealed class TraceLogger : ILogger 
    {
        private TaskFactory _scheduler = new TaskFactory(new SingleTaskThreadpoolScheduler());

        private string MakeMessage(string msg, Exception tr)
        {
            #if !NET_3_5
            var traceInfo = new TraceEventCache();
            var dateTime = traceInfo.DateTime.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            return $"[{traceInfo.ThreadId}] {dateTime} {msg}:\r\n{tr}";
            #else
            return String.Format("{0}:\r\n{1}", msg, tr);
            #endif
        }

        private string MakeMessage(string msg)
        {
#if !NET_3_5
            var traceInfo = new TraceEventCache();
            var dateTime = traceInfo.DateTime.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            return $"[{traceInfo.ThreadId}] {dateTime} {msg}";
#else
            return msg;
#endif
        }

        private void WriteLine(string level, string tag, string msg)
        {
            _scheduler.StartNew(() =>
            {
                Trace.WriteLine(MakeMessage(msg), $"{level} {tag}");
            });
        }

        private void WriteLine(string level, string tag, string msg, Exception tr)
        {
            _scheduler.StartNew(() =>
            {
                Trace.WriteLine(MakeMessage(msg, tr), $"{level} {tag}");
            });
        }

        #region ILogger

        public void V(string tag, string msg)
        {
            WriteLine("VERBOSE)", tag, msg);
        }

        public void V(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                V(tag, msg);
            }

            WriteLine("VERBOSE)", tag, msg, tr);
        }

        public void V(string tag, string format, params object[] args)
        {
            V(tag, String.Format(format, args));
        }

        public void D(string tag, string format, params object[] args)
        {
            D(tag, String.Format(format, args));
        }

        public void D(string tag, string msg)
        {
            WriteLine("DEBUG)", tag, msg);
        }

        public void D(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                D(tag, msg);
            }

            WriteLine("DEBUG)", tag, msg, tr);
        }

        public void I(string tag, string msg)
        {
            WriteLine("INFO)", tag, msg);
        }

        public void I(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                I(tag, msg);
            }

            WriteLine("INFO)", tag, msg, tr);
        }

        public void I(string tag, string format, params object[] args)
        {
            I(tag, String.Format(format, args));
        }

        public void W(string tag, string msg)
        {
            WriteLine("WARN)", tag, msg);
        }

        public void W(string tag, Exception tr)
        {
            WriteLine("WARN)", tag, "No message", tr);
        }

        public void W(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                W(tag, msg);
            }

            WriteLine("WARN)", tag, msg, tr);
        }

        public void W(string tag, string format, params object[] args)
        {
            W(tag, string.Format(format, args));
        }

        public void E(string tag, string msg)
        {
            WriteLine("ERROR)", tag, msg);
        }

        public void E(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                E(tag, msg);
            }

            WriteLine("ERROR)", tag, msg, tr);
        }

        public void E(string tag, string format, params object[] args)
        {
            E(tag, string.Format(format, args));
        }

        #endregion

    }
}
