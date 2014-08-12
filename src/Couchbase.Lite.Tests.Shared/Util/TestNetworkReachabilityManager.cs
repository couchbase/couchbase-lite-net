using System;

namespace Couchbase.Lite.Tests.Shared
{
    internal class TestNetworkReachabilityManager : INetworkReachabilityManager
    {
        #region INetworkReachabilityManager implementation

        public event EventHandler<NetworkReachabilityChangeEventArgs> Changed;

        #endregion

        public virtual void StartListening()
        {
        }

        public virtual void StopListening()
        {
        }
    }
}

