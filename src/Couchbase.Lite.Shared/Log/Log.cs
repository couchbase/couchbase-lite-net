// 
//  Log.cs
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
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Logging;

/// <summary>
/// Centralized logging facility.
/// </summary>
internal static unsafe class WriteLog
{
    // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
    private static readonly LogTo ToObj;
    private static AtomicBool Initialized = new(false);

    private static readonly C4LogCallback LogCallback = LiteCoreLog;
    // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

    internal static LogTo To
    {
        get {
            if (Initialized.Set(true)) {
                return ToObj;
            }
            
            var oldConsole = LogSinks.Console;
            LogSinks.Console = new ConsoleLogSink(LogLevel.Info);
            ToObj.Database.I("Startup", HTTPLogic.UserAgent);
            LogSinks.Console = oldConsole;
            return ToObj;
        }
    }

    static WriteLog()
    {
        ToObj = new LogTo();
        Native.c4log_writeToCallback(C4LogLevel.Warning, LogCallback, true);
    }
    
    internal static void SetCallbackLevel(LogLevel level) => 
        Native.c4log_writeToCallback((C4LogLevel)level, LogCallback, true);

#if CBL_PLATFORM_APPLE
    [ObjCRuntime.MonoPInvokeCallback(typeof(C4LogCallback))]
#endif
    private static void LiteCoreLog(C4LogDomain* domain, C4LogLevel level, IntPtr message, IntPtr ignored)
    {
        var domainName = Native.c4log_getDomainName(domain) ?? "";
        var logDomain = To.DomainForString(domainName);
        var actualMessage = message.ToUTF8String();
        LogSinks.Console?.Log((LogLevel)level, logDomain, actualMessage);
        LogSinks.Custom?.Log((LogLevel)level, logDomain, actualMessage);
    }
}