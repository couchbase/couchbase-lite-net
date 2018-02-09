//
// LogTo.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

using Couchbase.Lite.DI;

using JetBrains.Annotations;

using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Logging
{
    internal sealed unsafe class DomainLogger
    {
        #region Variables

        private readonly string _domain;
		private readonly C4LogDomain* _domainObj;
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
            var bytes = Marshal.StringToHGlobalAnsi(_domain);
            _domainObj = Native.c4log_getDomain((byte*) bytes, true);
            Level = LogLevel.Warning;
        }

        #endregion

        #region Internal Methods

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D([NotNull]string tag, [NotNull]string msg)
        {
            if (ShouldLog(LogLevel.Debug)) {
				LogToLiteCore(C4LogLevel.Debug, FormatMessage(tag, msg));
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D([NotNull]string tag, [NotNull]string msg, [NotNull]Exception tr)
        {
            if (ShouldLog(LogLevel.Debug)) {
                LogToLiteCore(C4LogLevel.Debug, FormatMessage(tag, msg, tr));
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        [StringFormatMethod("format")]
        internal void D([NotNull]string tag, [NotNull]string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Debug)) {
                LogToLiteCore(C4LogLevel.Debug, String.Format(FormatMessage(tag, format), args));
            }
        }

        internal void E([NotNull]string tag, [NotNull]string msg)
        {
            if (ShouldLog(LogLevel.Error)) {
                LogToLiteCore(C4LogLevel.Error, FormatMessage(tag, msg));
            }
        }

        internal void E([NotNull]string tag, [NotNull]string msg, [NotNull]Exception tr)
        {
            if (ShouldLog(LogLevel.Error)) {
                LogToLiteCore(C4LogLevel.Error, FormatMessage(tag, msg, tr));
            }
        }

        [StringFormatMethod("format")]
        internal void E([NotNull]string tag, [NotNull]string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Error)) {
                LogToLiteCore(C4LogLevel.Error, String.Format(FormatMessage(tag, format), args));
            }
        }

        internal void I([NotNull]string tag, [NotNull]string msg)
        {
            if (ShouldLog(LogLevel.Info)) {
                LogToLiteCore(C4LogLevel.Info, FormatMessage(tag, msg));
            }
        }

        internal void I([NotNull]string tag, [NotNull]string msg, [NotNull]Exception tr)
        {
            if (ShouldLog(LogLevel.Info)) {
                LogToLiteCore(C4LogLevel.Info, FormatMessage(tag, msg, tr));
            }
        }

        [StringFormatMethod("format")]
        internal void I([NotNull]string tag, [NotNull]string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Info)) {
				LogToLiteCore(C4LogLevel.Info, String.Format(FormatMessage(tag, format), args));
            }
        }

		internal void QuickWrite(C4LogLevel level, [NotNull]string msg, ILogger textLogger)
		{
			var cblLevel = (LogLevel)level;
			if(ShouldLog(cblLevel)) {
			    textLogger?.Log(cblLevel, Domain, msg);
			}
		}

        internal void V([NotNull]string tag, [NotNull]string msg)
        {
            if (ShouldLog(LogLevel.Verbose)) {
               LogToLiteCore(C4LogLevel.Debug, FormatMessage(tag, msg));
            }
        }

        internal void V([NotNull]string tag, [NotNull]string msg, [NotNull]Exception tr)
        {
            if (ShouldLog(LogLevel.Verbose)) {
                LogToLiteCore(C4LogLevel.Debug, FormatMessage(tag, msg, tr));
            }
        }

        [StringFormatMethod("format")]
        internal void V([NotNull]string tag, [NotNull]string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Verbose)) {
                LogToLiteCore(C4LogLevel.Verbose, String.Format(FormatMessage(tag, format), args));
            }
        }

        internal void W([NotNull]string tag, [NotNull]string msg)
        {
            if (ShouldLog(LogLevel.Warning)) {
                LogToLiteCore(C4LogLevel.Warning, FormatMessage(tag, msg));
            }
        }

        internal void W([NotNull]string tag, [NotNull]string msg, [NotNull]Exception tr)
        {
            if (ShouldLog(LogLevel.Warning)) {
                LogToLiteCore(C4LogLevel.Warning, FormatMessage(tag, msg, tr));
            }
        }

        [StringFormatMethod("format")]
        internal void W([NotNull]string tag, [NotNull]string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Warning)) {
                LogToLiteCore(C4LogLevel.Warning, String.Format(FormatMessage(tag, format), args));
            }
        }

        #endregion

        #region Private Methods

        [NotNull]
        private string FormatMessage([NotNull]string tag, [NotNull]string message)
        {
            Debug.Assert(tag != null && message != null);

            return $"({tag}) [{Environment.CurrentManagedThreadId}] {DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture)} {message}";
        }

        [NotNull]
        private string FormatMessage([NotNull]string tag, [NotNull]string message, [NotNull]Exception e)
        {
            Debug.Assert(tag != null && message != null && e != null);

            return $"({tag}) [{Environment.CurrentManagedThreadId}] {DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture)} {message}: {e}";
        }

		private void LogToLiteCore(C4LogLevel level, [NotNull]string msg)
		{
			Native.c4slog(_domainObj, level, msg);
		}

        private bool ShouldLog(LogLevel level)
        {
            if (Level == LogLevel.None) {
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

        [NotNull]
        internal DomainLogger Couchbase => _allLoggers[3];
        
        [NotNull]
        internal DomainLogger Database => _allLoggers[0];
        
        [NotNull]
        internal DomainLogger LiteCore => _allLoggers[4];
        
        [NotNull]
        internal DomainLogger Query => _allLoggers[1];
        
        [NotNull]
        internal DomainLogger Sync => _allLoggers[2];
        
        [NotNull]
        internal IEnumerable<DomainLogger> All => _allLoggers;

        #endregion

        #region Constructors

        internal LogTo()
        {
            var domains = new[] {
                "DB", "Query", "Sync", "Couchbase", "LiteCore"
            };
            _allLoggers = new DomainLogger[domains.Length];
            int i = 0;
            foreach (var domain in domains) {
                CreateAndAddLogger(domain, i++);
            }
        }

        #endregion

        #region Private Methods

        [ContractAnnotation("domain:null => halt")]
        private void CreateAndAddLogger(string domain, int index)
        {
            var logger = new DomainLogger(domain) { Level = LogLevel.Warning };
            _allLoggers[index] = logger;
        }

        #endregion
    }
}

