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
using System.Diagnostics;

using Couchbase.Lite.Support;
using LiteCore.Interop;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// Centralized logging facility.
    /// </summary>
    public static class Log
    {
        #region Enums

        /// <summary>
        /// A level of logging verbosity
        /// </summary>
        public enum LogLevel
        {
            /// <summary>
            /// No logs are output
            /// </summary>
            None,

            /// <summary>
            /// Informational logs are output (the default for most)
            /// </summary>
            Base,

            /// <summary>
            /// Verbose logs are output
            /// </summary>
            Verbose,

            /// <summary>
            /// Debugging logs are output (Only applicable in debug builds)
            /// </summary>
            Debug
        }

        #endregion

        #region Constants

        internal static readonly LogTo To = new LogTo();

        /// <summary>
        /// The available logging domains (for use with setting the
        /// logging level on various domains)
        /// </summary>
        public static readonly LogDomains Domains = new LogDomains(To);

        internal static IEnumerable<ILogger> Loggers => _Loggers;

        #endregion

        #region Variables

        private static List<ILogger> _Loggers = new List<ILogger> { InjectableCollection.GetImplementation<ILogger>() };
        private static LogScrubSensitivity _ScrubSensitivity;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicated if logging is disabled (if so,
        /// nothing will be logged)
        /// </summary>
        public static bool Disabled { get; set; }

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
                        foreach (var logger in Loggers) {
                            logger.I("Log", "SCRUBBING DISABLED, THIS LOG MAY CONTAIN SENSITIVE INFORMATION");
                        }
                    }

                    _ScrubSensitivity = value;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Add a logger to the list of loggers to write output to
        /// </summary>
        /// <param name="logger">The logger to add</param>
        public static void AddLogger(ILogger logger)
        {
            if (logger != null) {
                _Loggers.Add (logger);
            }
        }

        /// <summary>
        /// Sets the logger to the library provided logger
        /// </summary>
        /// <returns><c>true</c>, if logger was set, <c>false</c> otherwise.</returns>
        public static bool SetDefaultLogger()
        {
            return SetLogger(InjectableCollection.GetImplementation<ILogger>());
        }

        /// <summary>
        /// Sets the logger, disposing and removing all others.
        /// </summary>
        /// <returns><c>true</c>, if Logger was set, <c>false</c> otherwise.</returns>
        /// <param name="customLogger">Custom logger.</param>
        public static bool SetLogger(ILogger customLogger)
        {
            var loggers = _Loggers;
            if (loggers != null) {
                foreach (var logger in loggers) {
                    var disposable = logger as IDisposable;
                    disposable?.Dispose();
                }
            }

            _Loggers = customLogger != null ? new List<ILogger> { customLogger } : new List<ILogger>();
            return true;
        }

        public static void SetLiteCoreLogLevel(string domain, LogLevel level)
        {
            SetLiteCoreLogLevels(new Dictionary<string, LogLevel> {
                [domain] = level
            });
        }

        public static unsafe void SetLiteCoreLogLevels(IDictionary<string, LogLevel> levels)
        {
            foreach (var pair in levels) {
                var log = Native.c4log_getDomain(pair.Key, false);
                if (log == null) {
                    Log.To.LiteCore.W("Log", $"Invalid log specified in SetLiteCoreLogLevels: {pair.Key}, ignoring...");
                    continue;
                }

                Native.c4log_setLevel(log, Transform(pair.Value));
            }
        }

        private static C4LogLevel Transform(LogLevel level)
        {
            switch (level) {
                case LogLevel.Base:
                    return C4LogLevel.Info;
                case LogLevel.Debug:
                    return C4LogLevel.Debug;
                case LogLevel.None:
                    return C4LogLevel.None;
                case LogLevel.Verbose:
                    return C4LogLevel.Verbose;
                default:
                    throw new ArgumentOutOfRangeException($"Invalid log level {level}");
            }
        }

        #endregion
    }
}
