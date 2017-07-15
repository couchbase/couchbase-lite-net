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
using Couchbase.Lite.DI;
using Couchbase.Lite.Util;
using LiteCore.Interop;
using Microsoft.Extensions.Logging;
using ObjCRuntime;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// Centralized logging facility.
    /// </summary>
    public static class Log
    {
        #region Constants

        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private static readonly C4LogCallback _LogCallback;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

        #endregion

        #region Variables

        private static AtomicBool _ProvidersAdded = new AtomicBool(false);
        private static LogScrubSensitivity _ScrubSensitivity;

        private static LogTo _To;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicated if logging is disabled (if so,
        /// nothing will be logged)
        /// </summary>
        public static bool Disabled { get; set; }

        /// <summary>
        /// The available logging domains (for use with setting the
        /// logging level on various domains)
        /// </summary>
        public static LogDomains Domains { get; private set; }

        /// <summary>
        /// Gets or sets the logging level for Log.* calls (domains
        /// must be set with their respective interfaces
        /// </summary>
        public static LogLevel Level 
        {
            get => To.NoDomain.Level;
            set => To.NoDomain.Level = value;
        }

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

        internal static LogTo To
        {
            get {
                if (!_ProvidersAdded.Set(true)) {
                    AddLoggerProvider(Service.Provider.TryGetRequiredService<ILoggerProvider>());
                }

                return _To;
            }
            set => _To = value;
        }

        #endregion

        #region Constructors

        static unsafe Log()
        {
            Factory = new LoggerFactory();
            To = new LogTo();
            Domains = new LogDomains(_To);
            _LogCallback = LiteCoreLog;
            Native.c4log_writeToCallback(C4LogLevel.Debug, _LogCallback, true);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a provider to accept logging messages to the Log implementation (if none are added
        /// by the time the first log message comes then a default one will be chosen)
        /// </summary>
        /// <param name="provider">The provider to add</param>
        /// <param name="keepDefault">If <c>true</c>, then the logging provider will exist along with
        /// the default one, otherwise the default logger will also be added if applicable</param>
        public static void AddLoggerProvider(ILoggerProvider provider, bool keepDefault = false)
        {
            if (!keepDefault) {
                _ProvidersAdded.Set(true);
            }

            Factory.AddProvider(provider);
        }

        /// <summary>
        /// An API for setting the logging level of the native LiteCore library
        /// </summary>
        /// <param name="domain">The domain to change</param>
        /// <param name="level">The log level to set</param>
        public static void SetLiteCoreLogLevel(string domain, LogLevel level)
        {
            SetLiteCoreLogLevels(new Dictionary<string, LogLevel> {
                [domain] = level
            });
        }

        /// <summary>
        /// An API for setting logging levels of various domains in the native
        /// LiteCore library
        /// </summary>
        /// <param name="levels">A map of domains to levels</param>
        public static unsafe void SetLiteCoreLogLevels(IDictionary<string, LogLevel> levels)
        {
            var maxLevel = LogLevel.None;
            foreach (var pair in levels) {
                var log = Native.c4log_getDomain(pair.Key, false);
                if (log == null) {
                    To.LiteCore.W("Log", $"Invalid log specified in SetLiteCoreLogLevels: {pair.Key}, ignoring...");
                    continue;
                }

                maxLevel = (LogLevel)Math.Max((int) maxLevel, (int)pair.Value);
                Native.c4log_setLevel(log, Transform(pair.Value));
            }

            if (levels.Count > 0) {
                Domains.LiteCore.Level = maxLevel;
            }
        }

        #endregion

        #region Internal Methods

        internal static void ClearLoggerProviders()
        {
            _ProvidersAdded.Set(true);
            var oldFactory = Factory;
            Factory = new LoggerFactory();
            oldFactory.Dispose();
            To = new LogTo();
            Domains = new LogDomains(_To);
        }

        #endregion

        #region Private Methods

        [MonoPInvokeCallback(typeof(C4LogCallback))]
        private static unsafe void LiteCoreLog(C4LogDomain* domain, C4LogLevel level, string message, IntPtr ignored)
        {
            var name = Native.c4log_getDomainName(domain);
            switch (level) {
                case C4LogLevel.Error:
                    To.DomainOrLiteCore(name).E(name, message);
                    break;
                case C4LogLevel.Warning:
                    To.DomainOrLiteCore(name).W(name, message);
                    break;
                case C4LogLevel.Info:
                    To.DomainOrLiteCore(name).I(name, message);
                    break;
                case C4LogLevel.Verbose:
                    To.DomainOrLiteCore(name).V(name, message);
                    break;
                case C4LogLevel.Debug:
                    To.DomainOrLiteCore(name).D(name, message);
                    break;
            }
        }

        private static C4LogLevel Transform(LogLevel level)
        {
            switch (level) {
                case LogLevel.Information:
                    return C4LogLevel.Info;
                case LogLevel.Debug:
                case LogLevel.Trace:
                    return C4LogLevel.Debug;
                case LogLevel.None:
                    return C4LogLevel.None;
                case LogLevel.Warning:
                    return C4LogLevel.Warning;
                case LogLevel.Critical:
                case LogLevel.Error:
                    return C4LogLevel.Error;
                default:
                    throw new ArgumentOutOfRangeException($"Invalid log level {level}");
            }
        }

        #endregion
    }
}
