using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public static class Logger
    {
        /// <summary>
        /// Gets the object that stores the available logging methods
        /// for Couchbase Lite
        /// </summary>
        [NotNull]
        public static Loggers Loggers { get; } = new Loggers();
    }


    public abstract class BaseLogger : ILogger
    {
        public LogLevel Level { get; }

        public LogDomain Domains { get; }

        #region Constructors

        protected BaseLogger([NotNull]LogLevel level, [NotNull]LogDomain domains)
        {
            Level = level;
            Domains = domains;
        }

        protected BaseLogger([NotNull] LogLevel level, [NotNull] params object[] domains)
        {
            Level = level;
            foreach(var d in domains) {
                if(d is LogDomain) {
                    
                }
            }

            //Not sure this method is needed since we already have the one above...
        }

        protected BaseLogger([NotNull] LogLevel level)
        {
            Level = level;
        }

        #endregion

        public virtual void Log(LogLevel level, LogDomain domain, string message)
        {
            if (level < Level || !Domains.HasFlag(domain)) {
                return;
            }

            WriteLog(level, domain, message);
        }

        public abstract void WriteLog(LogLevel level, LogDomain domain, string message);
    }
}
