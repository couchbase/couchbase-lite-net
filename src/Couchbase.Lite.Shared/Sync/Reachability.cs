// 
// Reachability.cs
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

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Sync;

/// <summary>
/// An enum describing the state of network connectivity
/// </summary>
public enum NetworkReachabilityStatus
{
    /// <summary>
    /// Unable to determine
    /// </summary>
    Unknown,
        
    /// <summary>
    /// The network endpoint is reachable
    /// </summary>
    Reachable,

    /// <summary>
    /// The network endpoint is not reachable
    /// </summary>
    Unreachable
}

/// <summary>
/// Arguments to the <see cref="IReachability.StatusChanged" /> event
/// </summary>
public sealed class NetworkReachabilityChangeEventArgs : EventArgs
{
    /// <summary>
    /// The new reachability status
    /// </summary>
    public NetworkReachabilityStatus Status { get; }

    /// <summary>
    /// Default Constructor
    /// </summary>
    /// <param name="status">The network status to send</param>
    public NetworkReachabilityChangeEventArgs(NetworkReachabilityStatus status)
    {
        Status = status;
    }
}

internal sealed class Reachability : IReachability
{
    private const string Tag = nameof(Reachability);

    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    // ReSharper disable once MemberCanBePrivate.Global
    internal static bool AllowLoopback = false; // For unit tests

    public event EventHandler<NetworkReachabilityChangeEventArgs>? StatusChanged;

    public Uri? Url { get; set; }

    internal void InvokeNetworkChangeEvent(NetworkReachabilityStatus status)
    {
        var eventArgs = new NetworkReachabilityChangeEventArgs(status);
        StatusChanged?.Invoke(this, eventArgs);
    }

    private static bool IsInterfaceValid(NetworkInterface ni)
    {
        WriteLog.To.Sync.V(Tag, "    Testing {0} ({1})...", ni.Name, ni.Description);
        if (ni.OperationalStatus != OperationalStatus.Up) {
            WriteLog.To.Sync.V(Tag, "    NIC invalid (not up)");
            return false;
        }

        if ((!AllowLoopback && ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel
            || ni.Description.IndexOf("Loopback", StringComparison.OrdinalIgnoreCase) >= 0) {
            WriteLog.To.Sync.V(Tag, "    NIC invalid (not outward facing)");
            return false;
        }

        if (ni.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0) {
            WriteLog.To.Sync.V(Tag, "    NIC invalid (virtual)");
            return false;
        }

        WriteLog.To.Sync.I(Tag, "Found Acceptable NIC {0} ({1})", ni.Name, ni.Description);
        return true;
    }

    private void OnNetworkChange(object? sender, EventArgs e)
    {
        WriteLog.To.Sync.I(Tag, "Network change detected, analyzing connection status...");
        NetworkReachabilityStatus status;
        // https://social.msdn.microsoft.com/Forums/vstudio/en-US/a6b3541b-b7de-49e2-a7a6-ba0687761af5/networkavailabilitychanged-event-does-not-fire
        if (!NetworkInterface.GetIsNetworkAvailable()) {
            WriteLog.To.Sync.I(Tag, "NetworkInterface.GetIsNetworkAvailable() indicated no network available");
            status = NetworkReachabilityStatus.Unreachable;
        } else {
            var firstValidIP = NetworkInterface.GetAllNetworkInterfaces().Where(IsInterfaceValid)
                .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                .Select(x => x.Address).FirstOrDefault();

            if (firstValidIP == null) {
                WriteLog.To.Sync.I(Tag, "No acceptable IP addresses found, signaling network unreachable");
                status = NetworkReachabilityStatus.Unreachable;
            } else {
                WriteLog.To.Sync.I(Tag, "At least one acceptable IP address found ({0}), signaling network reachable", new SecureLogString(firstValidIP, LogMessageSensitivity.PotentiallyInsecure));
                status = NetworkReachabilityStatus.Reachable;
            }
        }

        InvokeNetworkChangeEvent(status);
    }

    public void Start()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChange;
    }

    public void Stop()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChange;
    }
}