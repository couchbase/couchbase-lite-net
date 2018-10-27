// 
// iOSReachability.cs
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
using System.Net;
using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using CoreFoundation;
using Foundation;
using SystemConfiguration;

namespace Couchbase.Lite.Support
{
    [CouchbaseDependency(Lazy = true, Transient = true)]
    internal sealed class iOSReachability : IReachability
    {

        #region Constants

        private const string Tag = nameof(iOSReachability);

        #endregion

        #region Variables

        private NetworkReachability _ref;
        private DispatchQueue _queue;
        private AtomicBool _started;

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        #endregion

        #region Properties

        public Uri Url { get; set; }

        public bool ReachabilityKnown { get; private set; }

        public NetworkReachabilityFlags ReachabilityFlags { get; private set; }

        #endregion

        #region Constructors

        public iOSReachability()
        {
            _queue = new DispatchQueue("Reachability", false);
        }

        #endregion

        #region Private Methods

        private void ClientCallback(NetworkReachabilityFlags flags)
        {
            FlagsChanged(flags);
        }

        private void FlagsChanged(NetworkReachabilityFlags flags)
        {
            if (!ReachabilityKnown || flags != ReachabilityFlags)
            {
                ReachabilityFlags = flags;
                ReachabilityKnown = true;
                Log.To.Sync.I(Tag, $"{this}: flags <-- {flags}");
                var status = flags.HasFlag(NetworkReachabilityFlags.Reachable) && !flags.HasFlag(NetworkReachabilityFlags.InterventionRequired) ?
                                  NetworkReachabilityStatus.Reachable : NetworkReachabilityStatus.Unreachable;
                StatusChanged?.Invoke(this, new NetworkReachabilityChangeEventArgs(status));
            }
        }

        private void NotifyFlagsChanged(NetworkReachabilityFlags flags)
        {
            _queue.DispatchAsync(() =>
            {
                FlagsChanged(flags);
            });
        }

        #endregion

        #region IReachability

        public void Start()
        {
            if (_started.Set(true))
            {
                return;
            }

            if (Url == null)
            {
                _ref = new NetworkReachability(new IPAddress(0));
            }
            else
            {
                _ref = new NetworkReachability(Url.Host);
            }

            _ref.SetDispatchQueue(_queue);
            _ref.SetNotification(ClientCallback);
            if (_ref.GetFlags(out var flags) == StatusCode.OK)
            {
                Log.To.Sync.I(Tag, $"{this}: flags={flags}; starting...");
                NotifyFlagsChanged(flags);
            }
            else
            {
                Log.To.Sync.I(Tag, $"{this}: starting...");
            }
        }

        public void Stop()
        {
            if (!_started.Set(false))
            {
                return;
            }

            ReachabilityKnown = false;
            _ref.SetDispatchQueue(null);
        }

        #endregion 
    }
}
