// 
// ConsoleLogWriter.cs
// 
// Copyright (c) 2024 Couchbase, Inc All rights reserved.
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
using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using System.Collections.Generic;

namespace Couchbase.Lite.Support;

[CouchbaseDependency]
internal sealed class ConsoleLogWriter : IConsoleLogWriter
{
    private CoreFoundation.OSLog _logger = new CoreFoundation.OSLog("CouchbaseLite", "dotnet");

    private static readonly IReadOnlyDictionary<LogLevel, CoreFoundation.OSLogLevel> LevelMap
        = new Dictionary<LogLevel, CoreFoundation.OSLogLevel>
        {
            [LogLevel.Debug] = CoreFoundation.OSLogLevel.Debug,
            [LogLevel.Verbose] = CoreFoundation.OSLogLevel.Info, // No verbose level in Apple
            [LogLevel.Info] = CoreFoundation.OSLogLevel.Info,
            [LogLevel.Warning] = CoreFoundation.OSLogLevel.Error, // No warning level in Apple
            [LogLevel.Error] = CoreFoundation.OSLogLevel.Error,
        };

    public void Write(LogLevel level, string message)
    {
        var appleLevel = LevelMap[level];
        _logger.Log(appleLevel, message);
    }
}
#endif