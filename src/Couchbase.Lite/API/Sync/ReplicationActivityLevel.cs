using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Sync
{
    internal enum ReplicationActivityLevel
    {
        Stopped,
        Offline,
        Connecting,
        Idle,
        Busy
    }
}
