// 
// Log.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
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
using System;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite.DI;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using LiteCore.Interop;
using Microsoft.Extensions.Logging;
using ObjCRuntime;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// Centralized logging facility.
    /// </summary>
    public static unsafe class Log
    {
        private static readonly LogTo _To;
        private static AtomicBool _Initialized = new AtomicBool(false);

        #region Constants

        internal static LogTo To
        {
            get {
                if (!_Initialized.Set(true)) {
                    var oldLevel = Database.GetLogLevels(LogDomain.Couchbase)[LogDomain.Couchbase];
                    Database.SetLogLevel(LogDomain.Couchbase, LogLevel.Info);
                    To.Couchbase.I("Startup", HTTPLogic.UserAgent);
                    To.Couchbase.I("Startup", Native.c4_getBuildInfo());
                    Database.SetLogLevel(LogDomain.Couchbase, oldLevel);
                }

                return _To;
            }
        }

        internal static readonly C4LogDomain* LogDomainDB = Native.c4log_getDomain("DB", true);

        internal static readonly C4LogDomain* LogDomainSQL = Native.c4log_getDomain("SQL", true);

        internal static readonly C4LogDomain* LogDomainBLIP = Native.c4log_getDomain("BLIP", true);

        internal static readonly C4LogDomain* LogDomainActor = Native.c4log_getDomain("Actor", true);

        internal static readonly C4LogDomain* LogDomainWebSocket = Native.c4log_getDomain("WebSocket", true);

        #endregion

        #region Variables

        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private static C4LogCallback _LogCallback;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

        private static LogScrubSensitivity _ScrubSensitivity;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicated if logging is disabled (if so,
        /// nothing will be logged)
        /// </summary>
        public static bool Disabled { get; set; }

        /// <summary>
        /// Gets or sets the level at which the logger will redacted sensivity
        /// information from the logs.
        /// </summary>
        public static LogScrubSensitivity ScrubSensitivity 
        {
            get => _ScrubSensitivity;
            set { 
                if (value != _ScrubSensitivity) {
                    if (value == LogScrubSensitivity.AllOk) {
                        Factory.CreateLogger("Log")
                            .LogInformation("SCRUBBING DISABLED, THIS LOG MAY CONTAIN SENSITIVE INFORMATION");
                    }

                    _ScrubSensitivity = value;
                }
            }
        }

        internal static ILoggerFactory Factory { get; private set; }

        #endregion

        #region Constructors

        static Log()
        {
            Factory = new LoggerFactory();
            _To = new LogTo();
			var dir = Service.Provider.TryGetRequiredService<IDefaultDirectoryResolver>();
			var binaryLogDir = Path.Combine(dir.DefaultDirectory(), "Logs");
			Directory.CreateDirectory(binaryLogDir);
			C4Error err;
            #if DEBUG
            var defaultLevel = C4LogLevel.Debug;
            #else
            var defaultLevel = C4LogLevel.Verbose;
            #endif

			var success = Native.c4log_writeToBinaryFile(defaultLevel, 
			                                             Path.Combine(binaryLogDir, 
			                                             $"log-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"), 
			                                             &err);
			if(!success) {
				Console.WriteLine($"COUCHBASE LITE WARNING: FAILED TO INITIALIZE LOGGING FILE IN {binaryLogDir}");
				Console.WriteLine($"ERROR {err.domain} / {err.code}");
			}
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a provider to accept logging messages to the Log implementation (if none are added
        /// by the time the first log message comes then a default one will be chosen)
        /// </summary>
        /// <param name="provider">The provider to add</param>
        public static void AddLoggerProvider(ILoggerProvider provider)
        {
            Factory.AddProvider(provider);
			if(_LogCallback == null) {
				_LogCallback = LiteCoreLog;
				Native.c4log_writeToCallback(C4LogLevel.Debug, _LogCallback, true);
			}
        }

        #endregion

        #region Internal Methods

        internal static void ClearLoggerProviders()
        {
			_LogCallback = null;
			Native.c4log_writeToCallback(C4LogLevel.Debug, null, true);
            var oldFactory = Factory;
            Factory = new LoggerFactory();
            oldFactory.Dispose();
        }

        #endregion

        #region Private Methods

        [MonoPInvokeCallback(typeof(C4LogCallback))]
        private static void LiteCoreLog(C4LogDomain* domain, C4LogLevel level, string message, IntPtr ignored)
        {
            var domainName = Native.c4log_getDomainName(domain);
            foreach (var logger in To.All) {
                if (logger.Domain == domainName) {
                    logger.QuickWrite(level, message);
                    return;
                }
            }

            To.LiteCore.QuickWrite(level, message);
        }

        #endregion
    }
}
