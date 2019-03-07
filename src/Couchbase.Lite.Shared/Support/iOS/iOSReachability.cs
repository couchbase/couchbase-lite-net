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
#if __IOS__
using System;
using System.Net;

using SystemConfiguration;

using CoreFoundation;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Sync;

namespace Couchbase.Lite.Support
{
    [CouchbaseDependency(Lazy = true, Transient = true)]
    internal sealed class iOSReachability : IReachability
    {
        #region Constants

        private const string Tag = nameof(iOSReachability);

        #endregion

        #region Variables

        private DispatchQueue _queue;

        private NetworkReachability _ref;
        private bool _started;

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        #endregion

        #region Properties

        public NetworkReachabilityFlags ReachabilityFlags { get; private set; }

        public bool ReachabilityKnown { get; private set; }

        public Uri Url { get; set; }

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
                WriteLog.To.Sync.I(Tag, $"{this}: flags <-- {flags}");
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
            _queue.DispatchSync(() =>
            {
                if (_started || Url?.IsLoopback == true) {
                    return;
                }

                _started = true;

                if (String.IsNullOrEmpty(Url?.Host))
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
                    WriteLog.To.Sync.I(Tag, $"{this}: flags={flags}; starting...");
                    NotifyFlagsChanged(flags);
                }
                else
                {
                    WriteLog.To.Sync.I(Tag, $"{this}: starting...");
                }
            });
        }

        public void Stop()
        {
            _queue.DispatchSync(() =>
            {
                if (!_started) {
                    return;
                }

                _started = false;
                ReachabilityKnown = false;
                _ref?.SetDispatchQueue(null);
                _ref?.Dispose();
                _ref = null;
            });
        }

        #endregion
    }
}
#endif