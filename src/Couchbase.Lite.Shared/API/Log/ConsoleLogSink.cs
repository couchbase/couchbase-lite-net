// 
//  ConsoleLogSink.cs
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

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Couchbase.Lite.Logging;

/// <summary>
/// A log sink that writes to the console of a given platform.  For console
/// applications and WinUI, this means calling <see cref="Console.WriteLine(string)"/>
/// and, if a debugger is attached, <see cref="Debug.WriteLine(string)"/>.  Android
/// applications will make use of <c>Android.Util.Log</c> with a "CouchbaseLite" domain,
/// while iOS / Mac Catalyst will use <c>CoreFoundation.OSLog</c> with a subsystem 
/// "CouchbaseLite" and category "dotnet".
/// </summary>
/// <param name="level">The log level to emit (see <see cref="BaseLogSink.Level"/>)</param>
public sealed class ConsoleLogSink(LogLevel level) : BaseLogSink(level)
{
    private readonly IConsoleLogWriter _logWriter = Service.GetRequiredInstance<IConsoleLogWriter>();

    /// <summary>
    /// Gets the domains to include for logging (useful for reducing noise when
    /// you want to focus on a certain area of logging)
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public LogDomain Domains { get; init; } = LogDomain.All;

    /// <summary>
    /// Copy constructor
    /// </summary>
    /// <param name="other">The object to copy from</param>
    public ConsoleLogSink(ConsoleLogSink other) : this(other.Level)
    {
        Domains = other.Domains;
    }

    /// <inheritdoc />
    protected override void WriteLog(LogLevel level, LogDomain domain, string message)
    {
        var finalStr = MakeMessage(message, level, domain);
        if ((Domains & domain) != 0) {
            _logWriter.Write(level, finalStr);
        }
    }

    private static string MakeMessage(string message, LogLevel level, LogDomain domain)
    {
        var dateTime = DateTime.Now.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
        var threadId = Thread.CurrentThread.Name ?? Environment.CurrentManagedThreadId.ToString();
        return $"{dateTime} [{threadId}]| {level.ToString().ToUpperInvariant()})  [{domain}] {message}";
    }
}