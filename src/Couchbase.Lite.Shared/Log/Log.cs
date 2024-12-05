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
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Logging
{
    /// <summary>
    /// Centralized logging facility.
    /// </summary>
    internal static unsafe class WriteLog
    {
        #region Constants

        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private static readonly LogTo _To;

        internal static readonly C4LogDomain* LogDomainBLIP = c4log_getDomain("BLIP", false);
        internal static readonly C4LogDomain* LogDomainWebSocket = c4log_getDomain("WS", false);
        internal static readonly C4LogDomain* LogDomainSyncBusy = c4log_getDomain("SyncBusy", false);
        private static LogLevel _CurrentLevel = LogLevel.Warning;
        private static AtomicBool _Initialized = new AtomicBool(false);

        private static readonly C4LogCallback LogCallback = LiteCoreLog;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

        #endregion

        #region Properties

        internal static LogTo To
        {
            get {
                if (!_Initialized.Set(true)) {
                    if (DomainLogger.OldApiUsed()) {
#pragma warning disable CS0618 // Type or member is obsolete
                        var oldLevel = Database.Log.Console.Level;
                        Database.Log.Console.Level = LogLevel.Info;
                        _To.Database.I("Startup", HTTPLogic.UserAgent);
                        Database.Log.Console.Level = oldLevel;
#pragma warning restore CS0618 // Type or member is obsolete
                    } else {
                        var oldConsole = LogSinks.Console;
                        LogSinks.Console = new ConsoleLogSink(LogLevel.Info);
                        _To.Database.I("Startup", HTTPLogic.UserAgent);
                        LogSinks.Console = oldConsole;
                    }
                }

                // Not the best place to do this, but otherwise we have to require the developer
                // To signal us when they change the log level
                if (DomainLogger.OldApiUsed()) {
#pragma warning disable CS0612 // Type or member is obsolete
                    RecalculateLevel();
#pragma warning restore CS0612 // Type or member is obsolete
                }
                return _To;
            }
        }

        #endregion

        #region Constructors

        static WriteLog()
        {
            _To = new LogTo();
            Native.c4log_writeToCallback(C4LogLevel.Warning, LogCallback, true);
        }

        #endregion

        #region Internal Methods

        [Obsolete]
        internal static void RecalculateLevel()
        {
            var effectiveLevel = (LogLevel)Math.Min((int) Database.Log.Console.Level,
                (int?) Database.Log.Custom?.Level ?? (int) LogLevel.Error);
            if (effectiveLevel == _CurrentLevel) {
                return;
            }

            _CurrentLevel = effectiveLevel;
            Task.Factory.StartNew(() =>
            {
                SetCallbackLevel(effectiveLevel);
                LogSinks.SetDomainLevels(Database.Log.OverallLogLevel);
            });
        }

        internal static void SetCallbackLevel(LogLevel level) 
        {
            Native.c4log_writeToCallback((C4LogLevel)level, LogCallback, true);
        }

        #endregion

        #region Private Methods

        private static C4LogDomain* c4log_getDomain(string name, bool create)
        {
            var bytes = Marshal.StringToHGlobalAnsi(name);
            return Native.c4log_getDomain((byte*) bytes, create);
        }

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4LogCallback))]
        #endif
        private static void LiteCoreLog(C4LogDomain* domain, C4LogLevel level, IntPtr message, IntPtr ignored)
        {
            // Not the best place to do this, but otherwise we have to require the developer
            // To signal us when they change the log level
            if (DomainLogger.OldApiUsed()) {
#pragma warning disable CS0612 // Type or member is obsolete
                RecalculateLevel();
#pragma warning restore CS0612 // Type or member is obsolete
            }

            var domainName = Native.c4log_getDomainName(domain) ?? "";
            var logDomain = To.DomainForString(domainName);
            var actualMessage = message.ToUTF8String();
            if (DomainLogger.OldApiUsed()) {
#pragma warning disable CS0618 // Type or member is obsolete
                Database.Log.Console.Log((LogLevel)level, logDomain, actualMessage);
                Database.Log.Custom?.Log((LogLevel)level, logDomain, actualMessage);
#pragma warning restore CS0618 // Type or member is obsolete
            } else {
                LogSinks.Console?.Log((LogLevel)level, logDomain, actualMessage);
                LogSinks.Custom?.Log((LogLevel)level, logDomain, actualMessage);
            }
        }

        #endregion
    }
}
