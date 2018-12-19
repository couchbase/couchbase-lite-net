using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Couchbase.Lite.Logging
{
    public interface IConsoleLogger : ILogger
    {
        new LogLevel Level { get; set; }

        LogDomain Domains { get; set; }
    }
}
