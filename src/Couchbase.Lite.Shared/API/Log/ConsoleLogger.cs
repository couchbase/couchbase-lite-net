using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Couchbase.Lite.Logging
{
    public interface IConsoleLogger : ILogger
    {
        LogDomain Domains { get; set; }
    }
}
