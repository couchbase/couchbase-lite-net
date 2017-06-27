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
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// An interface describing a logger which logs to a specific domain
    /// </summary>
    public interface IDomainLogging : IEnumerable<IDomainLogging>
    {
        #region Properties

        /// <summary>
        /// Gets the domain of this logger
        /// </summary>
        string Domain { get; }

        /// <summary>
        /// Gets or sets the current logging level of this logger
        /// </summary>
        LogLevel Level { get; set; }

        #endregion
    }

    internal sealed class DomainLogger : IDomainLogging
    {
        #region Variables

        private readonly string _domain;
        private readonly ILogger _logger;

        #endregion

        #region Properties

        public string Domain => _domain;

        public LogLevel Level { get; set; }

        #endregion

        #region Constructors

        internal DomainLogger(string domain)
        {
            _domain = domain ?? "Default";
            _logger = Log.Factory.CreateLogger(_domain);
        }

        #endregion

        #region Internal Methods

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Debug)) {
                _logger.LogDebug(FormatMessage(tag, msg));
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Debug)) {
                _logger.LogDebug(FormatMessage(tag, msg, tr));
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Debug)) {
                _logger.LogDebug(FormatMessage(tag, format), args);
            }
        }

        internal void E(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Critical)) {
                _logger.LogError(FormatMessage(tag, msg));
            }
        }

        internal void E(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Critical)) {
                _logger.LogError(FormatMessage(tag, msg, tr));
            }
        }

        internal void E(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Critical)) {
                _logger.LogError(FormatMessage(tag, format), args);
            }
        }

        internal void I(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Information)) {
                _logger.LogInformation(FormatMessage(tag, msg));
            }
        }

        internal void I(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Information)) {
                _logger.LogInformation(FormatMessage(tag, msg, tr));
            }
        }

        internal void I(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Information)) {
                _logger.LogInformation(FormatMessage(tag, format), args);
            }
        }

        internal void V(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Debug)) {
                _logger.LogDebug(FormatMessage(tag, msg));
            }
        }

        internal void V(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Debug)) {
                _logger.LogDebug(FormatMessage(tag, msg, tr));
            }
        }

        internal void V(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Debug)) {
                _logger.LogDebug(FormatMessage(tag, format), args);
            }
        }

        internal void W(string tag, string msg)
        {
            if (ShouldLog(LogLevel.Warning)) {
                _logger.LogWarning(FormatMessage(tag, msg));
            }
        }

        internal void W(string tag, string msg, Exception tr)
        {
            if (ShouldLog(LogLevel.Warning)) {
                _logger.LogWarning(FormatMessage(tag, msg, tr));
            }
        }

        internal void W(string tag, string format, params object[] args)
        {
            if (ShouldLog(LogLevel.Warning)) {
                _logger.LogWarning(FormatMessage(tag, format), args);
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

        #region IEnumerable

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<IDomainLogging>

        public IEnumerator<IDomainLogging> GetEnumerator()
        {
            return new OneShotEnumerator(this);
        }

        #endregion

        #region Nested

        private class OneShotEnumerator : IEnumerator<IDomainLogging>
        {
            #region Variables

            private readonly DomainLogger _parent;
            private bool _moved;

            #endregion

            #region Properties

            public IDomainLogging Current => _parent;

            object System.Collections.IEnumerator.Current => _parent;

            #endregion

            #region Constructors

            public OneShotEnumerator(DomainLogger parent)
            {
                _parent = parent;
            }

            #endregion

            #region IDisposable

            public void Dispose()
            {
                // No op
            }

            #endregion

            #region IEnumerator

            public bool MoveNext()
            {
                var moved = _moved;
                _moved = true;
                return !moved;
            }

            public void Reset()
            {
                _moved = false;
            }

            #endregion
        }

        #endregion
    }

    internal sealed class LogTo
    {
        #region Variables

        private readonly DomainLogger[] _allLoggers;

        #endregion

        #region Properties

        internal DomainLogger Database => _allLoggers[0];

        internal DomainLogger LiteCore => _allLoggers[7];

        internal DomainLogger Listener => _allLoggers[5];

        internal DomainLogger NoDomain => _allLoggers[8];

        internal DomainLogger Query => _allLoggers[1];

        internal DomainLogger Router => _allLoggers[2];

        internal DomainLogger Sync => _allLoggers[3];

        internal DomainLogger SyncPerf => _allLoggers[4];

        internal DomainLogger TaskScheduling => _allLoggers[6];

        #endregion

        #region Constructors

        internal LogTo()
        {
            var domains = new[] {
                "DB", "QUERY", "ROUTER", "SYNC",
                "SYNC PERF", "LISTENER", "TASK SCHEDULING", "LITECORE"
            };
            _allLoggers = new DomainLogger[domains.Length + 1];
            int i = 0;
            foreach (var domain in domains) {
                CreateAndAddLogger(domain, i++);
            }

            SyncPerf.Level = LogLevel.None;

            _allLoggers[_allLoggers.Length - 1] = new DomainLogger(null) { Level = LogLevel.Information };
        }

        #endregion

        #region Internal Methods

        internal DomainLogger DomainOrLiteCore(string domainName)
        {
            foreach (var logger in _allLoggers) {
                if (logger.Domain?.ToUpper() == domainName) {
                    return logger;
                }
            }

            return LiteCore;
        }

        #endregion

        #region Private Methods

        private void CreateAndAddLogger(string domain, int index)
        {
            var logger = new DomainLogger(domain) { Level = LogLevel.Information };
            _allLoggers[index] = logger;
        }

        #endregion
    }
}

