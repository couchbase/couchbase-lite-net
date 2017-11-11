//
// LogTo.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using LiteCore.Interop;
using Microsoft.Extensions.Logging;

namespace Couchbase.Lite.Logging
{
    internal sealed unsafe class DomainLogger
    {
        #region Variables

        private readonly string _domain;
		private readonly C4LogDomain* _domainObj;
        private readonly ILogger _logger;
		private LogLevel _level;

        #endregion

        #region Properties

        public string Domain => _domain;

        public LogLevel Level
		{
			get => _level;
			set {
				if(_level != value) {
					Native.c4log_setLevel(_domainObj, (C4LogLevel)value);
					_level = value;
				}
			}
		}

        #endregion

        #region Constructors

        internal DomainLogger(string domain)
        {
            _domain = domain ?? "Default";
			_domainObj = Native.c4log_getDomain(domain, true);
            Level = LogLevel.Warning;
            _logger = Log.Factory.CreateLogger(_domain);
        }

        #endregion

        #region Internal Methods

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Debug)) {
				LogToLiteCore(C4LogLevel.Debug, FormatMessage(tag, msg));
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Debug)) {
                LogToLiteCore(C4LogLevel.Debug, FormatMessage(tag, msg, tr));
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Debug)) {
                LogToLiteCore(C4LogLevel.Debug, String.Format(FormatMessage(tag, format), args));
            }
        }

        internal void E(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Error)) {
                LogToLiteCore(C4LogLevel.Error, FormatMessage(tag, msg));
            }
        }

        internal void E(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Error)) {
                LogToLiteCore(C4LogLevel.Error, FormatMessage(tag, msg, tr));
            }
        }

        internal void E(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Error)) {
                LogToLiteCore(C4LogLevel.Error, String.Format(FormatMessage(tag, format), args));
            }
        }

        internal void I(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Info)) {
                LogToLiteCore(C4LogLevel.Info, FormatMessage(tag, msg));
            }
        }

        internal void I(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Info)) {
                LogToLiteCore(C4LogLevel.Info, FormatMessage(tag, msg, tr));
            }
        }

        internal void I(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Info)) {
				LogToLiteCore(C4LogLevel.Info, String.Format(FormatMessage(tag, format), args));
            }
        }

		internal void QuickWrite(C4LogLevel level, string msg)
		{
			var cblLevel = (LogLevel)level;
			if(ShouldLog(cblLevel)) {
				switch(cblLevel) {
					case LogLevel.Debug:
						_logger.LogDebug(msg);
						break;
					case LogLevel.Error:
						_logger.LogError(msg);
						break;
					case LogLevel.Info:
						_logger.LogInformation(msg);
						break;
					case LogLevel.Warning:
						_logger.LogWarning(msg);
						break;
					case LogLevel.Verbose:
						_logger.LogTrace(msg);
						break;
				}
			}
		}

        internal void V(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Verbose)) {
               LogToLiteCore(C4LogLevel.Debug, FormatMessage(tag, msg));
            }
        }

        internal void V(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Verbose)) {
                LogToLiteCore(C4LogLevel.Debug, FormatMessage(tag, msg, tr));
            }
        }

        internal void V(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Verbose)) {
                LogToLiteCore(C4LogLevel.Verbose, String.Format(FormatMessage(tag, format), args));
            }
        }

        internal void W(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Warning)) {
                LogToLiteCore(C4LogLevel.Warning, FormatMessage(tag, msg));
            }
        }

        internal void W(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Warning)) {
                LogToLiteCore(C4LogLevel.Warning, FormatMessage(tag, msg, tr));
            }
        }

        internal void W(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Warning)) {
                LogToLiteCore(C4LogLevel.Warning, String.Format(FormatMessage(tag, format), args));
            }
        }

        #endregion

        #region Private Methods

        private string FormatMessage(string tag, string message)
        {
            return $"({tag}) [{Environment.CurrentManagedThreadId}] {DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture)} {message}";
        }

        private string FormatMessage(string tag, string message, Exception e)
        {
            return $"({tag}) [{Environment.CurrentManagedThreadId}] {DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture)} {message}: {e}";
        }

		private void LogToLiteCore(C4LogLevel level, string msg)
		{
			Native.c4slog(_domainObj, level, msg);
		}

        private bool ShouldLog(LogLevel level)
        {
            if (Log.Disabled || Level == LogLevel.None) {
                return false;
            }

            return Level <= level;
        }

        #endregion

        #region Overrides

        public override bool Equals(object obj)
        {
            var other = obj as DomainLogger;
            if (other == null) {
                return false;
            }

            return other._domain == _domain;
        }

        public override int GetHashCode()
        {
            return _domain.GetHashCode();
        }

        #endregion
    }

    internal sealed class LogTo
    {
        #region Variables

        private readonly DomainLogger[] _allLoggers;

		#endregion

		#region Properties

        internal DomainLogger Couchbase => _allLoggers[3];

        internal DomainLogger Database => _allLoggers[0];

        internal DomainLogger LiteCore => _allLoggers[4];

        internal DomainLogger Query => _allLoggers[1];

        internal DomainLogger Sync => _allLoggers[2];

        internal IEnumerable<DomainLogger> All => _allLoggers;

        #endregion

        #region Constructors

        internal LogTo()
        {
            var domains = new[] {
                "Database", "Query", "Sync", "Couchbase", "LiteCore"
            };
            _allLoggers = new DomainLogger[domains.Length];
            int i = 0;
            foreach (var domain in domains) {
                CreateAndAddLogger(domain, i++);
            }
        }

        #endregion

        #region Private Methods

        private void CreateAndAddLogger(string domain, int index)
        {
            var logger = new DomainLogger(domain) { Level = LogLevel.Warning };
            _allLoggers[index] = logger;
        }

        #endregion
    }
}

