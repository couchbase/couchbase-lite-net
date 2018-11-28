using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Couchbase.Lite.Logging
{
    public interface IConsoleLogger : ILogger
    {
        IList<LogDomain> Domains { get; set; }
    }
}
