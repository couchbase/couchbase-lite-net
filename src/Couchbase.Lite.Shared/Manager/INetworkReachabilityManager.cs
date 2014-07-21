using System;

namespace Couchbase.Lite
{
    public interface INetworkReachabilityManager
    {
        event EventHandler<NetworkReachabilityChangeEventArgs> Changed;
    }

    #region Enum

    public enum NetworkReachabilityStatus {
        Reachable,
        Unreachable
    }

    #endregion

    #region EventArgs

    ///
    /// <see cref="Couchbase.Lite.Replication"/> Change Event Arguments.
    ///
    public class NetworkReachabilityChangeEventArgs : EventArgs
    {
        public INetworkReachabilityManager Source { get; private set; }

        public NetworkReachabilityStatus Status { get; private set; }

        public NetworkReachabilityChangeEventArgs (INetworkReachabilityManager sender, NetworkReachabilityStatus status)
        {
            Source = sender;
            Status = status;
        }
    }

    #endregion
}

