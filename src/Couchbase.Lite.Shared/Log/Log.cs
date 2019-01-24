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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite.Interop;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore.Interop;

using ObjCRuntime;

namespace Couchbase.Lite.Internal.Logging
{
    /// <summary>
    /// Centralized logging facility.
    /// </summary>
    internal static unsafe class WriteLog
    {
        #region Constants

        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        [NotNull]
        private static readonly LogTo _To;

        internal static readonly C4LogDomain* LogDomainBLIP = c4log_getDomain("BLIP", false);
        internal static readonly C4LogDomain* LogDomainWebSocket = c4log_getDomain("WS", false);
        internal static readonly C4LogDomain* LogDomainSyncBusy = c4log_getDomain("SyncBusy", false);
        private static LogLevel _CurrentLevel = LogLevel.Warning;
        private static AtomicBool _Initialized = new AtomicBool(false);

        private static readonly C4LogCallback LogCallback = LiteCoreLog;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

        #endregion

        #region Variables

        #endregion

        #region Properties

        [NotNull]
        internal static LogTo To
        {
            get {
                if (!_Initialized.Set(true)) {
                    var oldLevel = Database.Log.Console.Level;
                    Database.Log.Console.Level = LogLevel.Info;
                    _To.Database.I("Startup", HTTPLogic.UserAgent);
                    Database.Log.Console.Level = oldLevel;
                }

                // Not the best place to do this, but otherwise we have to require the developer
                // To signal us when they change the log level
                RecalculateLevel();
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
                Native.c4log_writeToCallback((C4LogLevel) effectiveLevel, LogCallback, true);
            });
        }

        #endregion

        #region Private Methods

        private static C4LogDomain* c4log_getDomain(string name, bool create)
        {
            var bytes = Marshal.StringToHGlobalAnsi(name);
            return Native.c4log_getDomain((byte*) bytes, create);
        }

        [MonoPInvokeCallback(typeof(C4LogCallback))]
        private static void LiteCoreLog(C4LogDomain* domain, C4LogLevel level, IntPtr message, IntPtr ignored)
        {
            // Not the best place to do this, but otherwise we have to require the developer
            // To signal us when they change the log level
            RecalculateLevel();

            var domainName = Native.c4log_getDomainName(domain);
            var logDomain = To.DomainForString(domainName);
            var actualMessage = message.ToUTF8String();
            Database.Log.Console.Log((LogLevel)level, logDomain, actualMessage);
            Database.Log.Custom?.Log((LogLevel)level, logDomain, actualMessage);
        }

        #endregion
    }
}
