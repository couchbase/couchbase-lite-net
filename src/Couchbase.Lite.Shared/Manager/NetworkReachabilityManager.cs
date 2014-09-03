using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace Couchbase.Lite
{
    /// <summary>
    /// This uses the NetworkAvailability API to listen for network reachability
    /// change events and fires off changes internally.
    /// </summary>
    internal sealed class NetworkReachabilityManager : INetworkReachabilityManager
    {
        #region INetworkReachabilityManager implementation

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        /// <summary>This method starts listening for network connectivity state changes.</summary>
        /// <remarks>This method starts listening for network connectivity state changes.</remarks>
        public void StartListening()
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChange;
        }

        /// <summary>This method stops this class from listening for network changes.</summary>
        /// <remarks>This method stops this class from listening for network changes.</remarks>
        public void StopListening()
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChange;          
        }
        #endregion

        /// <summary>Notify listeners that the network is now reachable/unreachable.</summary>
        private void OnNetworkChange(Object sender, NetworkAvailabilityEventArgs args)
        {
            var evt = StatusChanged;
            if (evt == null)
            {
                return;
            }

            var status = args.IsAvailable
                ? NetworkReachabilityStatus.Reachable
                : NetworkReachabilityStatus.Unreachable;

            var eventArgs = new NetworkReachabilityChangeEventArgs(status);
            evt(this, eventArgs);
        }
    }
}

