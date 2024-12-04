// 
//  BaseLogSink.cs
// 
//  Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

using System.Threading.Tasks;

namespace Couchbase.Lite.Logging;

/// <summary>
/// The abstract base class of log sinks to inherit from for 
/// <see cref="LogSinks.Custom"/>
/// </summary>
/// <param name="level">The log level to emit.  Levels more verbose than this will not
/// be received by <see cref="WriteLog(LogLevel, LogDomain, string)"/></param>
public abstract class BaseLogSink(LogLevel level)
{
    /// <summary>
    ///  The log level to emit.  Levels more verbose than this will not
    /// be received by <see cref="WriteLog(LogLevel, LogDomain, string)"/>
    /// </summary>
    public LogLevel Level { get; init; } = level;

    /// <summary>
    /// Send the log message to its final destination.
    /// </summary>
    /// <param name="level">The level of the message to send</param>
    /// <param name="domain">The domain of the message to send</param>
    /// <param name="message">The message contents</param>
    protected abstract void WriteLog(LogLevel level, LogDomain domain, string message);

    internal void Log(LogLevel level, LogDomain domain, string message)
    {
        if(level >= Level) {
            WriteLog(level, domain, message);
        }
    }
}