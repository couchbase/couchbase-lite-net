using System;
using System.Collections.Generic;
using System.Text;

using Couchbase.Lite.P2P;

namespace Couchbase.Lite
{
    [Flags]
    public enum MockConnectionLifecycleLocation
    {
        Connect = 1 << 0,
        Send = 1 << 1,
        Receive = 1 << 2,
        Close = 1 << 3
    }

    public interface IMockConnectionErrorLogic
    {
        bool ShouldClose(MockConnectionLifecycleLocation location);

        MessagingException CreateException();
    }
}
