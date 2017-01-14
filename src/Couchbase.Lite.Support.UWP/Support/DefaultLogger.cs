//
//  DefaultLogger.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    internal sealed class DefaultLogger : ILogger
    {
        private string MakeMessage(string msg, Exception tr)
        {
            var dateTime = DateTime.Now.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            return $"[{Environment.CurrentManagedThreadId}] {dateTime} {msg}:\r\n{tr}";
        }

        private string MakeMessage(string msg)
        {
            var dateTime = DateTime.Now.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            return $"[{Environment.CurrentManagedThreadId}] {dateTime} {msg}";
        }

        private string MakeLine(string level, string tag, string msg)
        {
            return $"[{level}] {tag} {MakeMessage(msg)}";
        }

        private string MakeLine(string level, string tag, string msg, Exception tr)
        {
            return $"{level} {tag} {MakeMessage(msg, tr)}";
        }

        private void PerformWrite(string final)
        {
            Debug.WriteLine(final);
        }

        public void D(string tag, string msg)
        {
            var line = MakeLine("DEBUG", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void D(string tag, string format, params object[] args)
        {
            var line = MakeLine("DEBUG", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void D(string tag, string msg, Exception tr)
        {
            var line = MakeLine("DEBUG", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void E(string tag, string msg)
        {
            var line = MakeLine("ERROR", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void E(string tag, string format, params object[] args)
        {
            var line = MakeLine("ERROR", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void E(string tag, string msg, Exception tr)
        {
            var line = MakeLine("ERROR", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void I(string tag, string msg)
        {
            var line = MakeLine("INFO", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void I(string tag, string format, params object[] args)
        {
            var line = MakeLine("INFO", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void I(string tag, string msg, Exception tr)
        {
            var line = MakeLine("INFO", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void V(string tag, string msg)
        {
            var line = MakeLine("VERBOSE", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void V(string tag, string format, params object[] args)
        {
            var line = MakeLine("VERBOSE", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void V(string tag, string msg, Exception tr)
        {
            var line = MakeLine("VERBOSE", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void W(string tag, string msg)
        {
            var line = MakeLine("WARN", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void W(string tag, string format, params object[] args)
        {
            var line = MakeLine("WARN", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        public void W(string tag, string msg, Exception tr)
        {
            var line = MakeLine("WARN", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }
    }
}
