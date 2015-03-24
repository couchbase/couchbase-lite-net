using System;
using System.Net.NetworkInformation;

namespace Couchbase.Lite
{
    public interface INetworkReachabilityManager
    {
        event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;
        bool CanReach(string remoteUri);
        void StartListening();
        void StopListening();
    }

    #region Enum

    public enum NetworkReachabilityStatus 
    {
        Unknown,
        Reachable,
        Unreachable
    }

    #endregion

    #region EventArgs
       
    // <see cref="Couchbase.Lite.Replication"/> Change Event Arguments.

    /// <summary>
    /// Network reachability change event arguments.
    /// </summary>
    /// <remarks>
    /// Need this class because .NET's NetworkAvailabilityEventArgs
    /// only has an internal constructor.
    /// </remarks>
    public sealed class NetworkReachabilityChangeEventArgs : EventArgs
    {
        public NetworkReachabilityStatus Status { get; private set; }

        public NetworkReachabilityChangeEventArgs (NetworkReachabilityStatus status)
        {
            Status = status;
        }
    }

    #endregion
}

