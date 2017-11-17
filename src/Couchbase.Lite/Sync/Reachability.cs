// 
// Reachability.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;

namespace Couchbase.Lite.Sync
{
    internal enum NetworkReachabilityStatus
    {
        Unknown,
        Reachable,
        Unreachable
    }

    internal sealed class NetworkReachabilityChangeEventArgs : EventArgs
    {
        #region Properties

        public NetworkReachabilityStatus Status { get; }

        #endregion

        #region Constructors

        public NetworkReachabilityChangeEventArgs(NetworkReachabilityStatus status)
        {
            Status = status;
        }

        #endregion
    }

    internal sealed class Reachability : IReachability
    {
        #region Constants

        private const string Tag = nameof(Reachability);

        #endregion

        #region Variables

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        internal static bool AllowLoopback = false; // For unit tests
        
        #endregion

        #region Internal Methods

        internal void InvokeNetworkChangeEvent(NetworkReachabilityStatus status)
        {
            var eventArgs = new NetworkReachabilityChangeEventArgs(status);
            Task.Factory.StartNew(() => StatusChanged?.Invoke(this, eventArgs));
        }

        #endregion

        #region Private Methods

        private static bool IsInterfaceValid(NetworkInterface ni)
        {
            Log.To.Sync.V(Tag, "    Testing {0} ({1})...", ni.Name, ni.Description);
            if (ni.OperationalStatus != OperationalStatus.Up) {
                Log.To.Sync.V(Tag, "    NIC invalid (not up)");
                return false;
            }

            if ((!AllowLoopback && ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel
                || ni.Description.IndexOf("Loopback", StringComparison.OrdinalIgnoreCase) >= 0) {
                Log.To.Sync.V(Tag, "    NIC invalid (not outward facing)");
                return false;
            }

            if (ni.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0) {
                Log.To.Sync.V(Tag, "    NIC invalid (virtual)");
                return false;
            }

            Log.To.Sync.I(Tag, "Found Acceptable NIC {0} ({1})", ni.Name, ni.Description);
            return true;
        }

        private void OnNetworkChange(object sender, EventArgs e)
        {
            Log.To.Sync.I(Tag, "Network change detected, analyzing connection status...");
            NetworkReachabilityStatus status;
            // https://social.msdn.microsoft.com/Forums/vstudio/en-US/a6b3541b-b7de-49e2-a7a6-ba0687761af5/networkavailabilitychanged-event-does-not-fire
            if (!NetworkInterface.GetIsNetworkAvailable()) {
                Log.To.Sync.I(Tag, "NetworkInterface.GetIsNetworkAvailable() indicated no network available");
                status = NetworkReachabilityStatus.Unreachable;
            }
            else {
                var firstValidIP = NetworkInterface.GetAllNetworkInterfaces().Where(IsInterfaceValid)
                    .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                    .Select(x => x.Address).FirstOrDefault();

                if (firstValidIP == null) {
                    Log.To.Sync.I(Tag, "No acceptable IP addresses found, signaling network unreachable");
                    status = NetworkReachabilityStatus.Unreachable;
                }
                else {
                    Log.To.Sync.I(Tag, "At least one acceptable IP address found ({0}), signaling network reachable", new SecureLogString(firstValidIP, LogMessageSensitivity.PotentiallyInsecure));
                    status = NetworkReachabilityStatus.Reachable;
                }
            }

            InvokeNetworkChangeEvent(status);
        }

        #endregion

        #region IReachability

        public void Start()
        {
            NetworkChange.NetworkAddressChanged += OnNetworkChange;
        }

        public void Stop()
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkChange;
        }

        #endregion
    }
}
