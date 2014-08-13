using System;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>
    /// This uses system api (on Android, uses the Context) to listen for network reachability
    /// change events and notifies all NetworkReachabilityListeners that have registered themselves.
    /// </summary>
    /// <remarks>
    /// This uses system api (on Android, uses the Context) to listen for network reachability
    /// change events and notifies all NetworkReachabilityListeners that have registered themselves.
    /// (an example of a NetworkReachabilityListeners is a Replicator that wants to pause when
    /// it's been detected that the network is not reachable)
    /// </remarks>
    public abstract class NetworkReachabilityManager : INetworkReachabilityManager
    {
        #region INetworkReachabilityManager implementation

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        /// <summary>This method starts listening for network connectivity state changes.</summary>
        /// <remarks>This method starts listening for network connectivity state changes.</remarks>
        public abstract void StartListening();

        /// <summary>This method stops this class from listening for network changes.</summary>
        /// <remarks>This method stops this class from listening for network changes.</remarks>
        public abstract void StopListening();
        #endregion

        /// <summary>Notify listeners that the network is now reachable</summary>
        public virtual void OnNetworkReachable()
        {
            var evt = StatusChanged;
            if (evt == null)
            {
                return;
            }

            var args = new NetworkReachabilityChangeEventArgs(NetworkReachabilityStatus.Reachable);
            evt(this, args);
        }

        /// <summary>Notify listeners that the network is now unreachable</summary>
        public virtual void OnNetworkUneachable()
        {
            var evt = StatusChanged;
            if (evt == null)
            {
                return;
            }

            var args = new NetworkReachabilityChangeEventArgs(NetworkReachabilityStatus.Unreachable);
            evt(this, args);
        }
    }
}

