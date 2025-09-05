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
using System.Diagnostics.CodeAnalysis;

using SystemConfiguration;

using CoreFoundation;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Sync;

using Foundation;

using Network;

namespace Couchbase.Lite.Support;

[CouchbaseDependency(Lazy = true, Transient = true)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal sealed class iOSReachability : IReachability
{
    private const string Tag = nameof(iOSReachability);

    private readonly DispatchQueue _queue = new("Reachability", false);

    private readonly NWPathMonitor _pathMonitor = new();
    private bool _started;
    private bool _disposed;

    public event EventHandler<NetworkReachabilityChangeEventArgs>? StatusChanged;

    public NetworkReachabilityFlags ReachabilityFlags { get; private set; }

    public bool ReachabilityKnown { get; private set; }

    public Uri? Url { get; set; }

    private void FlagsChanged(NetworkReachabilityFlags flags)
    {
        if (ReachabilityKnown && flags == ReachabilityFlags) {
            return;
        }
        
        ReachabilityFlags = flags;
        ReachabilityKnown = true;
        WriteLog.To.Sync.I(Tag, $"{this}: flags <-- {flags}");
        var status = flags.HasFlag(NetworkReachabilityFlags.Reachable) && !flags.HasFlag(NetworkReachabilityFlags.InterventionRequired) ?
            NetworkReachabilityStatus.Reachable : NetworkReachabilityStatus.Unreachable;
        StatusChanged?.Invoke(this, new NetworkReachabilityChangeEventArgs(status));
    }

    private void NotifyFlagsChanged(NetworkReachabilityFlags flags)
    {
        _queue.DispatchAsync(() =>
        {
            FlagsChanged(flags);
        });
    }

    private NetworkReachabilityFlags ConvertPathToFlags(NWPath path)
    {
        var flags = default(NetworkReachabilityFlags);
        if (path.Status != NWPathStatus.Satisfied) {
            return flags;
        }
        
        flags |= NetworkReachabilityFlags.Reachable;
        if (path.IsExpensive) {
            flags |= NetworkReachabilityFlags.IsWWAN;
        }

        if (path.IsConstrained) {
            flags |= NetworkReachabilityFlags.InterventionRequired;
        }

        return flags;
    }
    
    private void ValidateHostReachability(NetworkReachabilityFlags baseFlags)
    {
        if (Url == null) {
            NotifyFlagsChanged(baseFlags);
            return;
        }

        using var session = NSUrlSession.SharedSession;
        var request = new NSUrlRequest(new NSUrl(Url.AbsoluteUri));

        var task = session.CreateDataTask(request, (_, response, error) =>
        {
            var flags = baseFlags;
            if (error.Code != 0 || response is NSHttpUrlResponse { StatusCode: >= 400 }) {
                flags &= ~NetworkReachabilityFlags.Reachable;
            }
            
            NotifyFlagsChanged(flags);
        });
        
        task.Resume();
    }

    public void Start()
    {
        _queue.DispatchSync(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(iOSReachability));
            if (_started || Url?.IsLoopback == true) {
                return;
            }

            _started = true;
            _pathMonitor.SetQueue(_queue);
            _pathMonitor.SnapshotHandler = path =>
            {
                var flags = ConvertPathToFlags(path);
                WriteLog.To.Sync.I(Tag, $"{this}: path status={path.Status}, flags={flags}");
                if (!String.IsNullOrEmpty(Url?.Host) && path.Status == NWPathStatus.Satisfied) {
                    ValidateHostReachability(flags);
                } else {
                    NotifyFlagsChanged(flags);
                }
            };
            
            _pathMonitor.Start();
            WriteLog.To.Sync.I(Tag, $"{this}: starting path monitor");
        });
    }

    public void Stop()
    {
        _queue.DispatchSync(() =>
        {
            if (!_started || _disposed) {
                return;
            }

            _started = false;
            _disposed = true;
            ReachabilityKnown = false;
            _pathMonitor.Dispose();
        });
    }
}
