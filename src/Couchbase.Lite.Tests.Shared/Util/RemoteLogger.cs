//
// RemoteLogger.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Collections.Generic;

namespace Couchbase.Lite.Util
{
    public sealed class RemoteLogger : TimeSeries, ILogger
    {
        public static readonly string DocType = "CBLRemoteLog";
        private static readonly RemoteLogger _Shared = 
            new RemoteLogger(Manager.SharedInstance.GetDatabase("cbl_logging"), DocType);

        public static RemoteLogger Shared
        {
            get {
                return _Shared;
            }
        }

        public RemoteLogger(Database db, string docType)
            : base(db, docType)
        {

        }

        private void Log(string severity, string tag, string msg, string exception = null)
        {
            AddEvent(new NonNullDictionary<string, object> {
                { "key", tag },
                { "msg", msg },
                { "level", severity },
                { "exception", exception }
            });
        }

        #region ILogger

        public void V(string tag, string msg)
        {
            Log("Verbose", tag, msg);
        }

        public void V(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                V(tag, msg);
                return;
            }

            Log("Verbose", tag, msg, tr.ToString());
        }

        public void V(string tag, string format, params object[] args)
        {
            Log("Verbose", tag, String.Format(format, args));
        }

        public void D(string tag, string msg)
        {
            Log("Debug", tag, msg);
        }

        public void D(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                D(tag, msg);
                return;
            }

            Log("Debug", tag, msg, tr.ToString());
        }

        public void D(string tag, string format, params object[] args)
        {
            Log("Debug", tag, String.Format(format, args));
        }

        public void I(string tag, string msg)
        {
            Log("Info", tag, msg);
        }

        public void I(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                I(tag, msg);
                return;
            }

            Log("Info", tag, msg, tr.ToString());
        }

        public void I(string tag, string format, params object[] args)
        {
            Log("Info", tag, String.Format(format, args));
        }

        public void W(string tag, string msg)
        {
            Log("Warn", tag, msg);
        }

        public void W(string tag, Exception tr)
        {
            if (tr == null) {
                W(tag, "<No message>");
                return;
            }

            Log("Warn", tag, "<No message>", tr.ToString());
        }

        public void W(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                W(tag, msg);
                return;
            }

            Log("Warn", tag, msg, tr.ToString());
        }

        public void W(string tag, string format, params object[] args)
        {
            Log("Warn", tag, String.Format(format, args));
        }

        public void E(string tag, string msg)
        {
            Log("Error", tag, msg);
        }

        public void E(string tag, string msg, Exception tr)
        {
            if (tr == null) {
                E(tag, msg);
                return;
            }

            Log("Error", tag, msg, tr.ToString());
        }

        public void E(string tag, string format, params object[] args)
        {
            Log("Error", tag, String.Format(format, args));
        }

        #endregion
        
    }
}

