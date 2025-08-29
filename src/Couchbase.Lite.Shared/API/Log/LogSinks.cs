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
    private static FileLogSink? BackingFile;
    private static ConsoleLogSink? BackingConsole = new(LogLevel.Warning);
    private static BaseLogSink? BackingCustom;

    private static readonly unsafe C4LogDomain* LogDomainBLIP = Native.c4log_getDomain("BLIP", true);
    private static readonly unsafe C4LogDomain* LogDomainSyncBusy = Native.c4log_getDomain("SyncBusy", true);
    private static readonly unsafe C4LogDomain* LogDomainWebSocket = Native.c4log_getDomain("WebSocket", true);

    private static LogLevel PreviousOverallLogLevel = LogLevel.Warning;
    private static LogLevel PreviousPlatformSideLogLevel = LogLevel.Warning;

    internal static readonly Dictionary<LogDomain, IntPtr> DomainObjects = new();

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
        get => BackingFile;
        set {
            FileLogSink.Apply(BackingFile, value);
            BackingFile = value;
            SetDomainLevels(OverallLogLevel);
            if (BackingFile == null) {
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
        get => BackingConsole;
        set {
            BackingConsole = value;
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
        get => BackingCustom;
        set {
            BackingCustom = value;
            SetDomainLevels(OverallLogLevel);
            SetPlatformSideLogLevel(PlatformSideLogLevel);
        }
    }

    private static LogLevel OverallLogLevel
    {
        get {
            var fileLevel = BackingFile?.Level ?? LogLevel.None;
            return (LogLevel)Math.Min((int)fileLevel, (int)PlatformSideLogLevel);
        }
    }

    private static LogLevel PlatformSideLogLevel
    {
        get {
            var consoleLevel = BackingConsole?.Level ?? LogLevel.None;
            var customLevel = BackingCustom?.Level ?? LogLevel.None;

            return (LogLevel)Math.Min((int)consoleLevel, (int)customLevel);
        }
    }

    internal static unsafe C4LogDomain* GetDomainObject(LogDomain domain) =>
        DomainObjects.TryGetValue(domain, out var o) ? (C4LogDomain*)o.ToPointer() : null;

    private static unsafe void SetupDomainObjects()
    {
        DomainObjects[LogDomain.Couchbase] = (IntPtr)Native.c4log_getDomain("Couchbase", true);
        DomainObjects[LogDomain.Database] = (IntPtr)Native.c4log_getDomain("DB", true);
        DomainObjects[LogDomain.Query] = (IntPtr)Native.c4log_getDomain("Query", true);
        DomainObjects[LogDomain.Replicator] = (IntPtr)Native.c4log_getDomain("Sync", true);
        #if COUCHBASE_ENTERPRISE
        DomainObjects[LogDomain.Listener] = (IntPtr)Native.c4log_getDomain("Listener", true);
        #endif
    }

    internal static unsafe void SetDomainLevels(LogLevel level)
    {
        if (level == PreviousOverallLogLevel) {
            return;
        }

        PreviousOverallLogLevel = level;
        foreach (var domain in DomainObjects) {
            Native.c4log_setLevel((C4LogDomain*)domain.Value.ToPointer(),
                (C4LogLevel)level);
        }

        Native.c4log_setLevel(LogDomainBLIP, (C4LogLevel)level);
        Native.c4log_setLevel(LogDomainSyncBusy, (C4LogLevel)level);
        Native.c4log_setLevel(LogDomainWebSocket, (C4LogLevel)level);
    }

    private static void SetPlatformSideLogLevel(LogLevel level)
    {
        if (PreviousPlatformSideLogLevel == level) {
            return;
        }

        PreviousPlatformSideLogLevel = level;
        WriteLog.SetCallbackLevel(level);
    }
}