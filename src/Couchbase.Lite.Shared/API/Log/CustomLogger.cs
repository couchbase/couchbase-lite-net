using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Couchbase.Lite.Logging
{
    public sealed class CustomLogger : BaseLogger
    {
        protected CustomLogger([NotNull] LogLevel level, [NotNull] LogDomain domains) : base(level, domains)
        {

        }

        protected CustomLogger([NotNull] LogLevel level) : base(level)
        {

        }

        public override void WriteLog(LogLevel level, LogDomain domain, string message)
        {
            throw new NotImplementedException();
        }
    }
}
