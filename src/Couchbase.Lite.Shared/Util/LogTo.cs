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
    public interface IDomainLogging
    {
        Log.LogLevel Level { get; set; }
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
            if (ShouldLog(Log.LogLevel.Verbose)) {
                Log.Logger.V(MakeTag(tag), msg);
            }
        }
            
        internal void V(string tag, string msg, Exception tr)
        {
            if (ShouldLog(Log.LogLevel.Verbose)) {
                Log.Logger.V(MakeTag(tag), msg, tr);
            }
        }

        internal void V(string tag, string format, params object[] args)
        {
            if (ShouldLog(Log.LogLevel.Verbose)) {
                Log.Logger.V(MakeTag(tag), format, args);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string msg)
        {
            if (ShouldLog(Log.LogLevel.Debug)) {
                Log.Logger.D(MakeTag(tag), msg);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string msg, Exception tr)
        {
            if (ShouldLog(Log.LogLevel.Debug)) {
                Log.Logger.D(MakeTag(tag), msg, tr);
            }
        }
            
        [System.Diagnostics.Conditional("DEBUG")]
        internal void D(string tag, string format, params object[] args)
        {
            if (ShouldLog(Log.LogLevel.Debug)) {
                Log.Logger.D(MakeTag(tag), format, args);
            }
        }

        internal void I(string tag, string msg)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.I(MakeTag(tag), msg);
            }
        }

        internal void I(string tag, string msg, Exception tr)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.I(MakeTag(tag), msg, tr);
            }
        }
            
        internal void I(string tag, string format, params object[] args)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.I(MakeTag(tag), format, args);
            }
        }

        internal void W(string tag, string msg)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.W(MakeTag(tag), msg);
            }
        }

        internal void W(string tag, string msg, Exception tr)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.W(MakeTag(tag), msg, tr);
            }
        }

        internal void W(string tag, string format, params object[] args)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.W(MakeTag(tag), format, args);
            }
        }

        internal void E(string tag, string msg)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.E(MakeTag(tag), msg);
            }
        }

        internal void E(string tag, string msg, Exception tr)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.E(MakeTag(tag), msg, tr);
            }
        }

        internal void E(string tag, string format, params object[] args)
        {
            if (ShouldLog(Log.LogLevel.Base)) {
                Log.Logger.E(MakeTag(tag), format, args);
            }
        }

        private string MakeTag(string tag)
        {
            return _makeTag ? String.Format("{0} ({1})", _domain, tag) : tag;
        }
            
        private bool ShouldLog(Log.LogLevel level)
        {
            if (Log.Logger == null) {
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
    }

    internal sealed class LogTo
    {
        private readonly DomainLogger[] _allLoggers;

        internal IEnumerable<IDomainLogging> AllLoggers
        {
            get {
                return _allLoggers.Cast<IDomainLogging>();
            }
        }

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

        internal DomainLogger All { get { return _allLoggers[11]; } }

        internal LogTo()
        {
            var domains = new[] { "DATABASE", "QUERY", "VIEW", "ROUTER", "SYNC",
                "SYNC PERF", "CHANGE TRACKER", "VALIDATION", "UPGRADE", "LISTENER", "DISCOVERY" };
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

