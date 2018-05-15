using System;
using System.Collections.Generic;
using System.Text;

using Couchbase.Lite.P2P;

namespace Couchbase.Lite
{
    public enum MockConnectionLifecycleLocation
    {
        Connect,
        Send,
        Receive,
        Close
    }

    public enum MockConnectionType
    {
        Client,
        Server
    }

    public interface IMockConnectionErrorLogic
    {
        bool ShouldClose(MockConnectionLifecycleLocation location, MockConnectionType connectionType);

        MessagingErrorException CreateException(MockConnectionLifecycleLocation location, MockConnectionType connectionType);
    }
}
