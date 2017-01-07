using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using Couchbase.Lite.Internal;

namespace Couchbase.Lite
{
    /// <summary>
    /// The interface governing an object which can check network reachability
    /// and react to changes in reachability
    /// </summary>
    internal interface INetworkReachabilityManager
    {
        /// <summary>
        /// Occurs when reachability changes.
        /// </summary>
        event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        /// <summary>
        /// Gets the last error that occured while trying to connect
        /// </summary>
        Exception LastError { get; }

        /// <summary>
        /// Returns whether or not a given endpoint is reachable
        /// </summary>
        /// <returns><c>true</c> if this instance can reach the specified endpoint; otherwise, <c>false</c>.</returns>
        /// <param name="remoteUri">The endpoint to test</param>
        /// <param name="timeout">The amount of time to wait for a response</param>
        bool CanReach(RemoteSession session, string remoteUri, TimeSpan timeout);
    }

    #region Enum

    /// <summary>
    /// The current status of the network
    /// </summary>
    [Serializable]
    public enum NetworkReachabilityStatus 
    {
        /// <summary>
        /// The status has not been evaluated yet
        /// </summary>
        Unknown,
        /// <summary>
        /// The network is reachable
        /// </summary>
        Reachable,
        /// <summary>
        /// The network is not reachable
        /// </summary>
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
        /// <summary>
        /// Gets the current network reachability status
        /// </summary>
        /// <value>The status.</value>
        public NetworkReachabilityStatus Status { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="status">The current network reachability status</param>
        public NetworkReachabilityChangeEventArgs (NetworkReachabilityStatus status)
        {
            Status = status;
        }
    }

    #endregion
}

