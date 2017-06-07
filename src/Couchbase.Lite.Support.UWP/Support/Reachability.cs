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
using Couchbase.Lite.DI;
using Couchbase.Lite.Sync;
using Windows.Networking.Connectivity;

namespace Couchbase.Lite.Support
{
    internal sealed class Reachability : IReachability
    {
        #region Variables

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        private SerialQueue _dispatchQueue;

        #endregion

        #region Private Methods

        private void OnNetworkStatusChanged(object sender)
        {
            var connection = NetworkInformation.GetInternetConnectionProfile();
            var status = connection == null
                ? NetworkReachabilityStatus.Unreachable
                : NetworkReachabilityStatus.Reachable;

            StatusChanged?.Invoke(this, new NetworkReachabilityChangeEventArgs(status));
        }

        #endregion

        #region IReachability

        public void Start(SerialQueue queue)
        {
            _dispatchQueue = queue;
            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
        }

        public void Stop()
        {
            NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
        }

        #endregion
    }
}
