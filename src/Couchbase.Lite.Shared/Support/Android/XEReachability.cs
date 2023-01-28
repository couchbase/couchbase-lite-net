// 
// XEReachability.cs
// 
// Copyright (c) 2023 Couchbase, Inc All rights reserved.
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
#if __ANDROID__
using Couchbase.Lite.DI;
using Couchbase.Lite.Sync;
using System;
using Xamarin.Essentials;

namespace Couchbase.Lite.Support
{
    [CouchbaseDependency]
    internal sealed class XEReachability : IReachability
    {
        public Uri Url { get; set; }

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        #region Private Methods

        private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            Console.WriteLine($"XEReachability changed: {e.NetworkAccess}");
            var state = NetworkReachabilityStatus.Unknown;
            if (e.NetworkAccess == NetworkAccess.None) {
                state = NetworkReachabilityStatus.Unreachable;
            } else if(e.NetworkAccess != NetworkAccess.Unknown) {
                state = NetworkReachabilityStatus.Reachable;
            }

            var eventArgs = new NetworkReachabilityChangeEventArgs(state);
            StatusChanged?.Invoke(this, eventArgs);
        }

        #endregion

        #region IReachability

        public void Start()
        {
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
        }

        public void Stop()
        {
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        }

        #endregion
    }
}
#endif