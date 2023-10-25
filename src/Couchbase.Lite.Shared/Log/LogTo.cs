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
using System.Threading;

using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Internal.Logging
{
    internal sealed class DomainLogger
    {
        #region Properties

        public LogDomain Domain { get; }

        public string Subdomain { get; }

        #endregion

        #region Constructors

        internal DomainLogger(string? domainStr, LogDomain domain)
        {
            Subdomain = domainStr ?? "Default";
            Domain = domain;
        }

        #endregion

        #region Internal Methods

        [Conditional("DEBUG")]
        internal void D(string tag, string msg)
        {
            SendToLoggers(LogLevel.Debug, FormatMessage(tag, msg));
        }

        [Conditional("DEBUG")]
        internal void D(string tag, string msg, Exception tr)
        {
            SendToLoggers(LogLevel.Debug, FormatMessage(tag, msg, tr));
        }

        [Conditional("DEBUG")]
        internal void D(string tag, string format, params object?[] args)
        {
            SendToLoggers(LogLevel.Debug, String.Format(FormatMessage(tag, format), args));
        }

        internal void E(string tag, string msg)
        {
            SendToLoggers(LogLevel.Error, FormatMessage(tag, msg));
        }

        internal void E(string tag, string msg, Exception tr)
        {
            SendToLoggers(LogLevel.Error, FormatMessage(tag, msg, tr));
        }

        internal void E(string tag, string format, params object?[] args)
        {
            SendToLoggers(LogLevel.Error, String.Format(FormatMessage(tag, format), args));
        }

        internal void I(string tag, string msg)
        {
            SendToLoggers(LogLevel.Info, FormatMessage(tag, msg));
        }

        internal void I(string tag, string msg, Exception tr)
        {
            SendToLoggers(LogLevel.Info, FormatMessage(tag, msg, tr));
        }

        internal void I(string tag, string format, params object?[] args)
        {
            SendToLoggers(LogLevel.Info, String.Format(FormatMessage(tag, format), args));
        }

        internal void V(string tag, string msg)
        {
           SendToLoggers(LogLevel.Verbose, FormatMessage(tag, msg));
        }

        internal void V(string tag, string msg, Exception tr)
        {
            SendToLoggers(LogLevel.Verbose, FormatMessage(tag, msg, tr));
        }

        internal void V(string tag, string format, params object?[] args)
        {
            SendToLoggers(LogLevel.Verbose, String.Format(FormatMessage(tag, format), args));
        }

        internal void W(string tag, string msg)
        {
            SendToLoggers(LogLevel.Warning, FormatMessage(tag, msg));
        }

        internal void W(string tag, string msg, Exception tr)
        {
            SendToLoggers(LogLevel.Warning, FormatMessage(tag, msg, tr));
        }

        internal void W(string tag, string format, params object?[] args)
        {
            SendToLoggers(LogLevel.Warning, String.Format(FormatMessage(tag, format), args));
        }

        #endregion

        #region Private Methods

        private string FormatMessage(string tag, string message)
        {
            Debug.Assert(tag != null && message != null);

            var threadId = Thread.CurrentThread?.Name != null
                ? $"{Thread.CurrentThread.Name} ({Thread.CurrentThread.ManagedThreadId})"
                : Environment.CurrentManagedThreadId.ToString();

            return $"({tag}) [{threadId}] {message}";
        }

        private string FormatMessage(string tag, string message, Exception e)
        {
            Debug.Assert(tag != null && message != null && e != null);

            var threadId = Thread.CurrentThread?.Name != null
                ? $"{Thread.CurrentThread.Name} ({Thread.CurrentThread.ManagedThreadId})"
                : Environment.CurrentManagedThreadId.ToString();

            return $"({tag}) [{threadId}] {message}: {e}";
        }

        private void SendToLoggers(LogLevel level, string msg)
		{
            Database.Log.Console.Log(level, Domain, msg);
		    var fileSucceeded = false;
		    try {
		        Database.Log.File.Log(level, Domain, msg);
		        fileSucceeded = true;
		        Database.Log.Custom?.Log(level, Domain, msg);
		    } catch (Exception e) {
		        var logType = fileSucceeded
		            ? Database.Log.Custom?.GetType().Name
		            : "log file";
		        var errMsg = FormatMessage("FILELOG", $"Error writing to {logType}", e);
                Database.Log.Console.Log(LogLevel.Error, LogDomain.None, errMsg);
		        if (!fileSucceeded) {
		            Database.Log.Custom?.Log(LogLevel.Error, LogDomain.None, errMsg);
		        }
		    }
		}

        #endregion

        #region Overrides

        public override bool Equals(object? obj)
        {
            var other = obj as DomainLogger;
            if (other == null) {
                return false;
            }

            return other.Domain == Domain;
        }

        public override int GetHashCode()
        {
            return Domain.GetHashCode();
        }

        #endregion
    }

    internal sealed class LogTo
    {
        #region Constants

         private static readonly Dictionary<string, LogDomain> DomainMap = new Dictionary<string, LogDomain>
        {
            ["DB"] = LogDomain.Database,
            ["SQL"] = LogDomain.Database,
            ["Blob"] = LogDomain.Database,
            ["Sync"] = LogDomain.Replicator,
            ["SyncBusy"] = LogDomain.Replicator,
            ["Actor"] = LogDomain.Replicator,
            ["Changes"] = LogDomain.Database,
            ["Query"] = LogDomain.Query,
            ["Enum"] = LogDomain.Query,
            ["WS"] = LogDomain.Network,
            ["BLIP"] = LogDomain.Network,
            ["BLIPMessages"] = LogDomain.Network,
            ["Zip"] = LogDomain.Network,
            ["TLS"] = LogDomain.Network,
#if COUCHBASE_ENTERPRISE
            ["Listener"] = LogDomain.Listener
#endif
        };

        #endregion

        #region Variables

        private readonly DomainLogger[] _allLoggers;

        #endregion

        #region Properties

        
        internal IEnumerable<DomainLogger> All => _allLoggers;

        
        internal DomainLogger Database => _allLoggers[0];

        
        internal DomainLogger Query => _allLoggers[1];

        
        internal DomainLogger Sync => _allLoggers[2];

        
        internal DomainLogger Listener => _allLoggers[3];

        #endregion

        #region Constructors

        internal LogTo()
        {
            var domainStrings = new[] {
                "DB", "Query", "Sync"
                #if COUCHBASE_ENTERPRISE
                , "Listener"
                #endif
            };

            var domains = new[] {
                LogDomain.Database, LogDomain.Query, LogDomain.Replicator
                #if COUCHBASE_ENTERPRISE
                , LogDomain.Listener
                #endif
            };

            _allLoggers = new DomainLogger[domains.Length];
            for(int i = 0; i < domainStrings.Length; i++) {
                CreateAndAddLogger(domainStrings[i], domains[i], i);
            }
        }

        #endregion

        #region Internal Methods

        internal LogDomain DomainForString(string domainStr)
        {
            if (DomainMap.TryGetValue(domainStr, out var domain)) {
                return domain;
            }

            return LogDomain.Database;
        }

        #endregion

        #region Private Methods

        private void CreateAndAddLogger(string domainStr, LogDomain domain, int index)
        {
            Debug.Assert(domainStr != null);
            var logger = new DomainLogger(domainStr, domain);
            _allLoggers[index] = logger;
        }

        #endregion
    }
}

