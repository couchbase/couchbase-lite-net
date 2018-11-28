// 
// DesktopConsoleLogger.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

// Define DEBUG just for this file (#define in C# only operates per file
// as opposed to C / C++ which defines from that point onward) to be able
// to use Debug.WriteLine even in a release build
#define DEBUG 
using System;
using System.Collections.Generic;
using System.Diagnostics;

using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    internal sealed class DesktopConsoleLogger : IConsoleLogger
    {
        #region Properties

        public IList<LogDomain> Domains { get; set; }

        public LogLevel Level { get; set; }

        #endregion

        #region Private Methods

        private string MakeMessage(string msg)
        {
            var dateTime = DateTime.Now.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            return $"[{Environment.CurrentManagedThreadId}] {dateTime} {msg}";
        }

        #endregion

        #region ILogger

        public void Log(LogLevel level, LogDomain domain, string message)
        {
            if (level > Level || !Domains.Contains(domain)) {
                return;
            }

            var finalStr = MakeMessage($"{domain.ToString()} {message}");
            Console.WriteLine(finalStr);
            if (Debugger.IsAttached) {
                Debug.WriteLine(finalStr);
            }
        }

        #endregion
    }
}
