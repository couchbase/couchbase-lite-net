// 
// ReplicatorOptionsDictionary.cs
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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

using Couchbase.Lite.Info;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Sync;

/// <summary>
///  A container for options that have to do with a <see cref="Replicator"/>
/// </summary>
internal sealed class ReplicatorOptionsDictionary : OptionsDictionary, IDisposable
{
    private const string Tag = nameof(ReplicatorOptionsDictionary);

    // Replicator option dictionary keys:
    private const string ChannelsKey = "channels";
    private const string CheckpointIntervalKey = "checkpointInterval";
    private const string ClientCertKey = "clientCert";
    private const string DocIDsKey = "docIDs";
    private const string FilterKey = "filter";
    private const string FilterParamsKey = "filterParams";
    private const string RemoteDBUniqueIDKey = "remoteDBUniqueID";
    private const string ResetKey = "reset";
    private const string MaxRetriesKey = "maxRetries";
    private const string MaxRetryIntervalKey = "maxRetryInterval";
    private const string EnableAutoPurgeKey = "autoPurge";
    private const string HeartbeatIntervalKey = "heartbeat"; //Interval in secs to send a keepalive ping
    private const string AcceptParentDomainCookiesKey = "acceptParentDomainCookies";

    // HTTP options:
    private const string HeadersKey = "headers";
    private const string CookiesKey = "cookies";
    private const string AuthOption = "auth";
    private const string ProxyAuthOption = "proxyAuth";

    // TLS options:
    private const string PinnedCertKey = "pinnedCert";
    private const string OnlySelfSignedServerCert = "onlySelfSignedServer";

    // WebSocket options:
    private const string ProtocolsOptionKey = "WS-Protocols";
    private const string NetworkInterfaceKey = "networkInterface";

    private GCHandle _pinnedCertHandle;
    private GCHandle _clientCertHandle;
    private TimeSpan? _heartbeat = Constants.DefaultReplicatorHeartbeat;
    private int _maxAttempts = Constants.DefaultReplicatorMaxAttemptsSingleShot;
    private TimeSpan? _maxAttemptsWaitTime = Constants.DefaultReplicatorMaxAttemptsWaitTime;

    /// <summary>
    /// Gets or sets whether a cookie can be set on a parent domain
    /// of the host that issued it (i.e. foo.bar.com can set a cookie for all
    /// of bar.com)
    /// </summary>
    public bool AcceptParentDomainCookies
    {
        get => this.GetCast<bool>(AcceptParentDomainCookiesKey);
        set => this[AcceptParentDomainCookiesKey] = value;
    }

    /// <summary>
    /// Gets or sets the authentication parameters
    /// </summary>
    public AuthOptionsDictionary? Auth
    {
        get => this.GetCast<AuthOptionsDictionary>(AuthOption);
        set => this[AuthOption] = value;
    }

    public AuthOptionsDictionary? ProxyAuth
    {
        get => this.GetCast<AuthOptionsDictionary>(ProxyAuthOption);
        set => this[ProxyAuthOption] = value;
    }

    /// <summary>
    /// Gets or sets the channels to replicate (pull only)
    /// </summary>
    public IImmutableList<string>? Channels
    {
        get => this.GetCast<IImmutableList<string>>(ChannelsKey);
        set => this[ChannelsKey] = value;
    }

    public TimeSpan CheckpointInterval
    {
        get => TimeSpan.FromSeconds(this.GetCast<double>(CheckpointIntervalKey));
        set {
            if (value > TimeSpan.Zero) {
                this[CheckpointIntervalKey] = value.TotalSeconds;
            } else {
                Remove(CheckpointIntervalKey);
            }
        }
    }

    /// <summary>
    /// Gets or set the certificate to be used with client side
    /// authentication during TLS requests (optional)
    /// </summary>
    public X509Certificate2? ClientCert { get; set; }

    /// <summary>
    /// Gets or sets a collection of cookie objects to be passed along
    /// with the initial HTTP request of the <see cref="Replicator"/>
    /// </summary>
    public ICollection<Cookie> Cookies { get; set; } = new List<Cookie>();

    /// <summary>
    /// Gets or sets the docIDs to replicate
    /// </summary>
    public IImmutableList<string>? DocIDs
    {
        get => this.GetCast<IImmutableList<string>>(DocIDsKey);
        set => this[DocIDsKey] = value;
    }

    /// <summary>
    /// Gets or sets the filter to use when replicating
    /// </summary>
    public string? Filter
    {
        get => this.GetCast<string>(FilterKey);
        set => this[FilterKey] = value;
    }

    /// <summary>
    /// Gets or sets the parameters that will be passed along with the filter
    /// </summary>
    public IDictionary<string, object>? FilterParams
    {
        get => this.GetCast<IDictionary<string, object>>(FilterParamsKey);
        set => this[FilterParamsKey] = value;
    }

    /// <summary>
    /// Gets a mutable collection of headers to be passed along with the initial
    /// HTTP request that starts replication
    /// </summary>
    public IImmutableDictionary<string, string?> Headers
    {
        get => this.GetCast<IImmutableDictionary<string, string?>>(HeadersKey, ImmutableDictionary<string, string?>.Empty)!;
        set => this[HeadersKey] = value;
    }

    /// <summary>
    /// Gets or sets a certificate to trust.  All other certificates received
    /// by a <see cref="Replicator"/> with this configuration will be rejected.
    /// </summary>
    internal X509Certificate2? PinnedServerCertificate { get; set; }

    internal string? NetworkInterface
    {
        get => this.GetCast<string>(NetworkInterfaceKey);
        set => this[NetworkInterfaceKey] = value;
    }

    /// <summary>
    /// Stable ID for remote db with unstable URL
    /// </summary>
    public string? RemoteDBUniqueID
    {
        get => this.GetCast<string>(RemoteDBUniqueIDKey);
        set {
            if (value != null) {
                this[RemoteDBUniqueIDKey] = value;
            } else {
                Remove(RemoteDBUniqueIDKey);
            }
        }
    }

    /// <summary>
    /// Checkpoint Reset
    /// </summary>
    public bool Reset
    {
        get => this.GetCast<bool>(ResetKey);
        set {
            if (value) {
                this[ResetKey] = true;
            } else {
                Remove(ResetKey);
            }
        }
    }

    internal bool EnableAutoPurge
    {
        get => this.GetCast<bool>(EnableAutoPurgeKey);
        set => this[EnableAutoPurgeKey] = value;
    }

    internal TimeSpan? Heartbeat
    {
        get => _heartbeat;
        set 
        {
            if (_heartbeat != value) {
                if (value != null) {
                    long sec = value.Value.Ticks / TimeSpan.TicksPerSecond;
                    if (sec > 0) {
                        this[HeartbeatIntervalKey] = sec;
                    } else {
                        throw new ArgumentException(CouchbaseLiteErrorMessage.InvalidHeartbeatInterval);
                    }
                } else { // Backward compatible if null is set
                    this[HeartbeatIntervalKey] = Constants.DefaultReplicatorHeartbeat.TotalSeconds;
                }
                    
                _heartbeat = value;
            }
        }
    }

    internal int MaxAttempts
    {
        get => _maxAttempts;
        set
        {
            if (_maxAttempts != value) {
                if (value < 0) {
                    throw new ArgumentException(CouchbaseLiteErrorMessage.InvalidMaxAttempts);
                } 
                    
                this[MaxRetriesKey] = value - 1;
                _maxAttempts = value;
            }
        }
    }

    internal TimeSpan? MaxAttemptsWaitTime
    {
        get => _maxAttemptsWaitTime;
        set
        {
            if (_maxAttemptsWaitTime != value) {
                if (value != null) {
                    long sec = value.Value.Ticks / TimeSpan.TicksPerSecond;
                    if (sec > 0) {
                        this[MaxRetryIntervalKey] = sec;
                    } else {
                        throw new ArgumentException(CouchbaseLiteErrorMessage.InvalidMaxAttemptsInterval);
                    }
                } else { // Backward compatible if null is set
                    this[HeartbeatIntervalKey] = Constants.DefaultReplicatorMaxAttemptsWaitTime.TotalSeconds;
                }

                _maxAttemptsWaitTime = value;
            }
        }
    }

#if COUCHBASE_ENTERPRISE
    internal bool AcceptOnlySelfSignedServerCertificate
    {
        get => this.GetCast<bool>(OnlySelfSignedServerCert);
        set => this[OnlySelfSignedServerCert] = value;
    }
#endif

    internal string? CookieString => this.GetCast<string>(CookiesKey);

    internal string? Protocols => this.GetCast<string>(ProtocolsOptionKey);

    /// <summary>
    /// Default constructor
    /// </summary>
    public ReplicatorOptionsDictionary()
    {
        EnableAutoPurge = Constants.DefaultReplicatorEnableAutoPurge;
            #if COUCHBASE_ENTERPRISE
        AcceptOnlySelfSignedServerCertificate = Constants.DefaultReplicatorSelfSignedCertificateOnly;
            #endif
    }

    ~ReplicatorOptionsDictionary()
    {
        Dispose(true);
    }

    internal ReplicatorOptionsDictionary(ReplicatorOptionsDictionary other)
    {
        AcceptParentDomainCookies = other.AcceptParentDomainCookies;
        if(other.Auth != null) {
            Auth = new(other.Auth);
        }
        
        CheckpointInterval = other.CheckpointInterval;
        Channels = other.Channels;
        ClientCert = other.ClientCert;
        Cookies = other.Cookies;
        DocIDs = other.DocIDs;
        EnableAutoPurge = other.EnableAutoPurge;
        Filter = other.Filter;
        Headers = other.Headers;
        Heartbeat = other.Heartbeat;
        MaxAttempts = other.MaxAttempts;
        MaxAttemptsWaitTime = other.MaxAttemptsWaitTime;
        NetworkInterface = other.NetworkInterface;
        PinnedServerCertificate = other.PinnedServerCertificate;
        if(other.ProxyAuth != null) {
            ProxyAuth = new(other.ProxyAuth);
        }
        
        RemoteDBUniqueID = other.RemoteDBUniqueID;
        
#if COUCHBASE_ENTERPRISE
        AcceptOnlySelfSignedServerCertificate = other.AcceptOnlySelfSignedServerCertificate;
#endif
    }

    internal ReplicatorOptionsDictionary(Dictionary<string, object?> raw) : base(raw)
    {
        if (ContainsKey(AuthOption)) {
            Auth = new((this[AuthOption] as Dictionary<string, object?>)!);
        }

        if (ContainsKey(ProxyAuthOption)) {
            ProxyAuth = new((this[ProxyAuthOption] as Dictionary<string, object?>)!);
        }

        if (ContainsKey(ChannelsKey)) {
            Channels = (this[ChannelsKey] as IImmutableList<string>)!;
        }

        if (ContainsKey(DocIDsKey)) {
            DocIDs = (this[DocIDsKey] as IImmutableList<string>)!;
        }

        if (ContainsKey(HeadersKey)) {
            Headers = (this[HeadersKey] as IImmutableDictionary<string, string>)!;
        }

        if (ContainsKey(PinnedCertKey)) {
            PinnedServerCertificate = GCHandle.FromIntPtr((IntPtr)this.GetCast<long>(PinnedCertKey)).Target as X509Certificate2;
        }

        if (ContainsKey(ClientCertKey)) {
            ClientCert = GCHandle.FromIntPtr((IntPtr)this.GetCast<long>(ClientCertKey)).Target as X509Certificate2;
        }

        if (ContainsKey(CookiesKey)) {
            AddCookies(this[CookiesKey] as string);
        }
    }

    private void AddCookies(string? cookiesString)
    {
        var split = cookiesString?.Split(';') ?? Enumerable.Empty<string>();
        foreach (var entry in split) {
            var pieces = entry.Split('=');
            if (pieces.Length != 2) {
                WriteLog.To.Sync.W(Tag, "Garbage cookie value, ignoring");
                continue;
            }

            Cookies.Add(new(pieces[0].Trim(), pieces[1].Trim()));
        }
    }

    private void Dispose(bool _)
    {
        if (_clientCertHandle.IsAllocated) {
            _clientCertHandle.Free();
        }

        if (_pinnedCertHandle.IsAllocated) {
            _pinnedCertHandle.Free();
        }
    }

    internal override void BuildInternal()
    {
        Auth?.Build();

        // If Headers contain Cookie
        if(Headers.TryGetValue("Cookie", out var value))
            AddCookies(value);

        if (Cookies.Count > 0) {
            this[CookiesKey] = Cookies.Select(x => $"{x.Name}={x.Value}").Aggregate((l, r) => $"{l}; {r}");
        }

        if (PinnedServerCertificate != null) {
            if (!_pinnedCertHandle.IsAllocated) {
                _pinnedCertHandle = GCHandle.Alloc(PinnedServerCertificate, GCHandleType.Weak);
            }

            this[PinnedCertKey] = GCHandle.ToIntPtr(_pinnedCertHandle).ToInt64();
        }

        if (ClientCert != null) {
            if (!_clientCertHandle.IsAllocated) {
                _clientCertHandle = GCHandle.Alloc(ClientCert, GCHandleType.Weak);
            }

            this[ClientCertKey] = GCHandle.ToIntPtr(_clientCertHandle).ToInt64();
        }

        Headers = Headers.SetItem("User-Agent", HTTPLogic.UserAgent);
    }

    internal override bool Validate(string key, object? value)
    {
        switch (key) {
            case AuthOption:
                return value is AuthOptionsDictionary;
            case CookiesKey:
                return value is string;
            case PinnedCertKey:
            case ClientCertKey:
                return value is long;
            default:
                return true;
        }
    }

    public void Dispose()
    {
        Dispose(false);
        GC.SuppressFinalize(this);
    }
}