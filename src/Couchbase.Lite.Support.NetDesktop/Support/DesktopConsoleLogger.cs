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

// Windows 2012 doesn't define the more generic variants

#define DEBUG
using System;
using System.Diagnostics;
using System.Threading;

using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    internal sealed class DesktopConsoleLogger : IConsoleLogger
    {
        #region Properties

        public LogDomain Domains { get; set; } = LogDomain.All;

        public LogLevel Level { get; set; } = LogLevel.Warning;

        #endregion

        #region Private Methods

        private static string MakeMessage(string message, LogLevel level, LogDomain domain)
        {
            var threadId = Thread.CurrentThread.Name ?? Thread.CurrentThread.ManagedThreadId.ToString();
            return $"[{threadId}]| {level.ToString().ToUpperInvariant()})  [{domain}] {message}";
        }

        #endregion

        #region ILogger

        public void Log(LogLevel level, LogDomain domain, string message)
        {
            if (level < Level || !Domains.HasFlag(domain)) {
                return;
            }
            
            var dateTime = DateTime.Now.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            var finalStr = MakeMessage(message, level, domain);
            Console.WriteLine($"{dateTime} {finalStr}");
            if (Debugger.IsAttached) {
                Debug.WriteLine(finalStr);
            }
        }

        #endregion
    }
}
