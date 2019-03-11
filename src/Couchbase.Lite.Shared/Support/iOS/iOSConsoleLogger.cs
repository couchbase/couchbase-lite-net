// 
// iOSConsoleLogger.cs
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
#if __IOS__
using System;
using System.Collections.Generic;

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    [CouchbaseDependency]
    internal sealed class iOSConsoleLogger : IConsoleLogger
    {
        #region Properties

        public LogDomain Domains { get; set; } = LogDomain.All;

        public LogLevel Level { get; set; } = LogLevel.Warning;

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
            if (level < Level || !Domains.HasFlag(domain)) {
                return;
            }

            var finalStr = MakeMessage($"{domain.ToString()} {message}");
            Console.WriteLine(finalStr); // Console.WriteLine == NSLog
        }

        #endregion
    }
}
#endif