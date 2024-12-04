// 
//  Loggers.cs
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

using Couchbase.Lite.Internal.Logging;
using LiteCore.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Couchbase.Lite.Logging;

/// <summary>
/// The static container for each of the three log sinks that Couchbase Lite can make use of.
/// </summary>
public static class LogSinks
{
    private static FileLogSink? _File;
    private static ConsoleLogSink? _Console = new ConsoleLogSink(LogLevel.Warning);
    private static BaseLogSink? _Custom;

    private static LogLevel _PreviousOverallLogLevel = LogLevel.Warning;
    private static LogLevel _PreviousPlatformSideLogLevel = LogLevel.Warning;

    private static readonly Dictionary<LogDomain, IntPtr> DomainObjects = new Dictionary<LogDomain, IntPtr>();

    static LogSinks()
    {
        Native.c4log_enableFatalExceptionBacktrace();
        SetupDomainObjects();
        SetDomainLevels(LogLevel.Warning);
    }

    /// <summary>
    /// Gets or sets the file logging sink.  This must be set up in order to receive SDK support, as 
    /// the logs files it generates will be requested.  
    /// </summary>
    public static FileLogSink? File
    {
        get {
            return _File;
        }
        set {
            DomainLogger.ThrowIfOldApiUsed();
            FileLogSink.Apply(_File, value);
            _File = value;
            SetDomainLevels(OverallLogLevel);
            if(_File == null) {
                WriteLog.To.Database.W("Logging", "LogSinks.FileLogSink is now null, meaning file logging is disabled.  Log files required for product support are not being generated.");
            }
        }
    }

    /// <summary>
    /// Gets or sets the console logging sink.  Useful for quick debugging, but expensive
    /// for production use. 
    /// </summary>
    public static ConsoleLogSink? Console
    {
        get {
            return _Console;
        }
        set {
            DomainLogger.ThrowIfOldApiUsed();
            _Console = value;
            SetDomainLevels(OverallLogLevel);
            SetPlatformSideLogLevel(PlatformSideLogLevel);
        }
    }

    /// <summary>
    /// Gets or sets the custom logging sink.  Useful for hooking into an existing logging
    /// infrastructure that may be set up in an application already. 
    /// </summary>
    public static BaseLogSink? Custom
    {
        get {
            return _Custom;
        }
        set {
            DomainLogger.ThrowIfOldApiUsed();
            _Custom = value;
            SetDomainLevels(OverallLogLevel);
            SetPlatformSideLogLevel(PlatformSideLogLevel);
        }
    }

    private static LogLevel OverallLogLevel
    {
        get {
            var fileLevel = _File?.Level ?? LogLevel.None;

            return (LogLevel)Math.Min((int)fileLevel, (int)PlatformSideLogLevel);
        }
    }

    private static LogLevel PlatformSideLogLevel
    {
        get {
            var consoleLevel = _Console?.Level ?? LogLevel.None;
            var customLevel = _Custom?.Level ?? LogLevel.None;

            return (LogLevel)Math.Min((int)consoleLevel, (int)customLevel);
        }
    }

    internal static unsafe C4LogDomain* GetDomainObject(LogDomain domain)
    {
        return DomainObjects.ContainsKey(domain) ? (C4LogDomain *)DomainObjects[domain].ToPointer() : null;
    }

    private static unsafe void SetupDomainObjects()
    {
        var bytes = (byte*)Marshal.StringToHGlobalAnsi("Couchbase");
        DomainObjects[LogDomain.Couchbase] = (IntPtr)Native.c4log_getDomain(bytes, true);

        bytes = (byte*)Marshal.StringToHGlobalAnsi("DB");
        DomainObjects[LogDomain.Database] = (IntPtr)Native.c4log_getDomain(bytes, true);

        bytes = (byte*)Marshal.StringToHGlobalAnsi("Query");
        DomainObjects[LogDomain.Query] = (IntPtr)Native.c4log_getDomain(bytes, true);

        bytes = (byte*)Marshal.StringToHGlobalAnsi("Sync");
        DomainObjects[LogDomain.Replicator] = (IntPtr)Native.c4log_getDomain(bytes, true);
    }

    internal static unsafe void SetDomainLevels(LogLevel level)
    {
        if (level == _PreviousOverallLogLevel) {
            return;
        }

        _PreviousOverallLogLevel = level;
        foreach (var domain in DomainObjects) {
            Native.c4log_setLevel((C4LogDomain*)domain.Value.ToPointer(),
                (C4LogLevel)level);
        }

        Native.c4log_setLevel(WriteLog.LogDomainBLIP, (C4LogLevel)level);
        Native.c4log_setLevel(WriteLog.LogDomainSyncBusy, (C4LogLevel)level);
        Native.c4log_setLevel(WriteLog.LogDomainWebSocket, (C4LogLevel)level);
    }

    private static void SetPlatformSideLogLevel(LogLevel level)
    {
        if(_PreviousPlatformSideLogLevel == level) {
            return;
        }

        _PreviousPlatformSideLogLevel = level;
        WriteLog.SetCallbackLevel(level);
    }
}