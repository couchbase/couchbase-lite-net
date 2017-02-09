//
//  DefaultLogger.cs
//
//  Author:
//      Jim Borden  <jim.borden@couchbase.com>
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
using System.Threading.Tasks;

using Couchbase.Lite.Logging;
using Log = Android.Util.Log;

namespace Couchbase.Lite.Support
{
    internal sealed class AndroidDefaultLogger : ILogger
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

        private string MakeLine(string tag, string msg)
        {
            return $"{tag} {MakeMessage(msg)}";
        }

        private string MakeLine(string tag, string msg, Exception tr)
        {
            return $"{tag} {MakeMessage(msg, tr)}";
        }

        public void D(string tag, string msg)
        {
            var line = MakeLine(tag, msg);
            Task.Factory.StartNew(() => Log.Debug("CouchbaseLite", line));
        }

        public void D(string tag, string format, params object[] args)
        {
            var line = MakeLine(tag, String.Format(format, args));
            Task.Factory.StartNew(() => Log.Debug("CouchbaseLite", line));
        }

        public void D(string tag, string msg, Exception tr)
        {
            var line = MakeLine(tag, msg, tr);
            Task.Factory.StartNew(() => Log.Debug("CouchbaseLite", line));
        }

        public void E(string tag, string msg)
        {
            var line = MakeLine(tag, msg);
            Task.Factory.StartNew(() => Log.Error("CouchbaseLite", line));
        }

        public void E(string tag, string format, params object[] args)
        {
            var line = MakeLine(tag, String.Format(format, args));
            Task.Factory.StartNew(() => Log.Error("CouchbaseLite", line));
        }

        public void E(string tag, string msg, Exception tr)
        {
            var line = MakeLine(tag, msg, tr);
            Task.Factory.StartNew(() => Log.Error("CouchbaseLite", line));
        }

        public void I(string tag, string msg)
        {
            var line = MakeLine(tag, msg);
            Task.Factory.StartNew(() => Log.Info("CouchbaseLite", line));
        }

        public void I(string tag, string format, params object[] args)
        {
            var line = MakeLine(tag, String.Format(format, args));
            Task.Factory.StartNew(() => Log.Info("CouchbaseLite", line));
        }

        public void I(string tag, string msg, Exception tr)
        {
            var line = MakeLine(tag, msg, tr);
            Task.Factory.StartNew(() => Log.Info("CouchbaseLite", line));
        }

        public void V(string tag, string msg)
        {
            var line = MakeLine(tag, msg);
            Task.Factory.StartNew(() => Log.Verbose("CouchbaseLite", line));
        }

        public void V(string tag, string format, params object[] args)
        {
            var line = MakeLine(tag, String.Format(format, args));
            Task.Factory.StartNew(() => Log.Verbose("CouchbaseLite", line));
        }

        public void V(string tag, string msg, Exception tr)
        {
            var line = MakeLine(tag, msg, tr);
            Task.Factory.StartNew(() => Log.Verbose("CouchbaseLite", line));
        }

        public void W(string tag, string msg)
        {
            var line = MakeLine(tag, msg);
            Task.Factory.StartNew(() => Log.Warn("CouchbaseLite", line));
        }

        public void W(string tag, string format, params object[] args)
        {
            var line = MakeLine(tag, String.Format(format, args));
            Task.Factory.StartNew(() => Log.Warn("CouchbaseLite", line));
        }

        public void W(string tag, string msg, Exception tr)
        {
            var line = MakeLine(tag, msg, tr);
            Task.Factory.StartNew(() => Log.Warn("CouchbaseLite", line));
        }
    }
}
