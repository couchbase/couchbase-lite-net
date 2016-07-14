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
using System.Linq;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// An interface describing a logger which logs to a specific domain
    /// </summary>
    public interface IDomainLogging : IEnumerable<IDomainLogging>
    {
        /// <summary>
        /// Gets or sets the current logging level of this logger
        /// </summary>
        Log.LogLevel Level { get; set; }

        /// <summary>
        /// Gets the domain of this logger
        /// </summary>
        string Domain { get; }
    }

    internal sealed class DomainLogger : IDomainLogging
    {
        private readonly string _domain;
        private readonly bool _makeTag;

        public Log.LogLevel Level { get; set; }

        public string Domain 
        {
            get { return _domain; }
        }

        internal DomainLogger(string domain, bool makeTag)
        {
            _domain = domain;
            _makeTag = makeTag;
        }
            
        internal void V(string tag, string msg)
        {
            PerformLog(logger => logger.V(MakeTag(tag), msg), Log.LogLevel.Verbose);
        }
            
        internal void V(string tag, string msg, Exception tr)
        {
            PerformLog(logger => logger.V(MakeTag(tag), msg, tr), Log.LogLevel.Verbose);
        }

        internal void V(string tag, string format, params object[] args)
        {
            PerformLog(logger => logger.V(MakeTag(tag), format, args), Log.LogLevel.Verbose);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string msg)
        {
            PerformLog(logger => logger.D(MakeTag(tag), msg), Log.LogLevel.Debug);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string msg, Exception tr)
        {
            PerformLog(logger => logger.D(MakeTag(tag), msg, tr), Log.LogLevel.Debug);
        }
            
        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string format, params object[] args)
        {
            PerformLog(logger => logger.D(MakeTag(tag), format, args), Log.LogLevel.Debug);
        }

        internal void I(string tag, string msg)
        {
            PerformLog(logger => logger.I(MakeTag(tag), msg), Log.LogLevel.Base);
        }

        internal void I(string tag, string msg, Exception tr)
        {
            PerformLog(logger => logger.I(MakeTag(tag), msg, tr), Log.LogLevel.Base);
        }
            
        internal void I(string tag, string format, params object[] args)
        {
            PerformLog(logger => logger.I(MakeTag(tag), format, args), Log.LogLevel.Base);
        }

        internal void W(string tag, string msg)
        {
            PerformLog(logger => logger.W(MakeTag(tag), msg), Log.LogLevel.Base);
        }

        internal void W(string tag, string msg, Exception tr)
        {
            PerformLog(logger => logger.W(MakeTag(tag), msg, tr), Log.LogLevel.Base);
        }

        internal void W(string tag, string format, params object[] args)
        {
            PerformLog(logger => logger.W(MakeTag(tag), format, args), Log.LogLevel.Base);
        }

        internal void E(string tag, string msg)
        {
            PerformLog(logger => logger.E(MakeTag(tag), msg), Log.LogLevel.Base);
        }

        internal void E(string tag, string msg, Exception tr)
        {
            PerformLog(logger => logger.E(MakeTag(tag), msg, tr), Log.LogLevel.Base);
        }

        internal void E(string tag, string format, params object[] args)
        {
            PerformLog(logger => logger.E(MakeTag(tag), format, args), Log.LogLevel.Base);
        }

        private string MakeTag(string tag)
        {
            return _makeTag ? String.Format("{0} ({1})", _domain, tag) : tag;
        }

        private void PerformLog(Action<ILogger> callback, Log.LogLevel level)
        {
            if (callback == null) {
                return;
            }

            var loggers = Log.Loggers;
            if (loggers == null) {
                return;
            }

            if (ShouldLog(level)) {
                foreach (var logger in loggers) {
                    callback(logger);
                }
            }
        }
            
        private bool ShouldLog(Log.LogLevel level)
        {
            if (Log.Disabled) {
                return false;
            }

            return Level >= level;
        }
            
        public override int GetHashCode()
        {
            return _domain.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as DomainLogger;
            if (other == null) {
                return false;
            }

            return other._domain == _domain;
        }

        public IEnumerator<IDomainLogging> GetEnumerator()
        {
            return new OneShotEnumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #region Private Classes

        private class OneShotEnumerator : IEnumerator<IDomainLogging>
        {
            private readonly DomainLogger _parent;
            private bool _moved;

            public OneShotEnumerator(DomainLogger parent)
            {
                _parent = parent;
            }

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

            object System.Collections.IEnumerator.Current
            {
                get {
                    return _parent;
                }
            }

            public void Dispose()
            {
                // No op
            }

            public IDomainLogging Current
            {
                get {
                    return _parent;
                }
            }
        }

        #endregion
    }

    internal sealed class LogTo
    {
        private readonly DomainLogger[] _allLoggers;

        internal DomainLogger Database { get { return _allLoggers[0]; } }

        internal DomainLogger Query { get { return _allLoggers[1]; } }

        internal DomainLogger View { get { return _allLoggers[2]; } }

        internal DomainLogger Router { get { return _allLoggers[3]; } }

        internal DomainLogger Sync { get { return _allLoggers[4]; } }

        internal DomainLogger SyncPerf { get { return _allLoggers[5]; } }

        internal DomainLogger ChangeTracker { get { return _allLoggers[6]; } }

        internal DomainLogger Validation { get { return _allLoggers[7]; } }

        internal DomainLogger Upgrade { get { return _allLoggers[8]; } }

        internal DomainLogger Listener { get { return _allLoggers[9]; } }

        internal DomainLogger Discovery { get { return _allLoggers[10]; } }

        internal DomainLogger TaskScheduling { get { return _allLoggers[11]; } }

        internal DomainLogger NoDomain { get { return _allLoggers[12]; } }

        internal LogTo()
        {
            var domains = new[] { "DATABASE", "QUERY", "VIEW", "ROUTER", "SYNC",
                "SYNC PERF", "CHANGE TRACKER", "VALIDATION", "UPGRADE", "LISTENER", "DISCOVERY",
                "TASK SCHEDULING"};
            _allLoggers = new DomainLogger[domains.Length + 1];
            int i = 0;
            foreach (var domain in domains) {
                CreateAndAddLogger(domain, i++);
            }

            SyncPerf.Level = Log.LogLevel.None;

            _allLoggers[_allLoggers.Length - 1] = new DomainLogger(null, false) { Level = Log.LogLevel.Base };
        }

        private void CreateAndAddLogger(string domain, int index)
        {
            var logger = new DomainLogger(domain, true) { Level = Log.LogLevel.Base };
            _allLoggers[index] = logger;
        }
    }
}

