using System;

namespace Couchbase.Lite
{
    public interface INetworkReachabilityManager
    {
        event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;
        void StartListening();
        void StopListening();
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
        public NetworkReachabilityStatus Status { get; private set; }

        public NetworkReachabilityChangeEventArgs (NetworkReachabilityStatus status)
        {
            Status = status;
        }
    }

    #endregion
}

