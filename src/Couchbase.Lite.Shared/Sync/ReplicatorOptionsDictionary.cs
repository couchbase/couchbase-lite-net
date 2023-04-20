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
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

using Couchbase.Lite.Info;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    ///  A container for options that have to do with a <see cref="Replicator"/>
    /// </summary>
    internal sealed class ReplicatorOptionsDictionary : OptionsDictionary, IDisposable
    {
        #region Constants

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

        // TLS options:
        private const string PinnedCertKey = "pinnedCert";
        private const string OnlySelfSignedServerCert = "onlySelfSignedServer";

        // WebSocket options:
        private const string ProtocolsOptionKey = "WS-Protocols";
        private const string NetworkInterfaceKey = "networkInterface";

        #endregion

        #region Variables

        private GCHandle _pinnedCertHandle;
        private GCHandle _clientCertHandle;
        private TimeSpan? _heartbeat = Constants.DefaultReplicatorHeartbeat;
        private int _maxAttempts = Constants.DefaultReplicatorMaxAttemptsSingleShot;
        private TimeSpan? _maxAttemptsWaitTime = Constants.DefaultReplicatorMaxAttemptWaitTime;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether or not a cookie can be set on a parent domain
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

        /// <summary>
        /// Gets or sets the channels to replicate (pull only)
        /// </summary>
        public IList<string>? Channels
        {
            get => this.GetCast<IList<string>>(ChannelsKey);
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
        public IList<string>? DocIDs
        {
            get => this.GetCast<IList<string>>(DocIDsKey);
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
        public IDictionary<string, string?> Headers
        {
            get => this.GetCast<IDictionary<string, string?>>(HeadersKey, new Dictionary<string, string?>())!;
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
                        this[HeartbeatIntervalKey] = Constants.DefaultReplicatorMaxAttemptWaitTime.TotalSeconds;
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

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public ReplicatorOptionsDictionary()
        {
            Headers = new Dictionary<string, string?>();
            EnableAutoPurge = Constants.DefaultReplicatorEnableAutoPurge;
            #if COUCHBASE_ENTERPRISE
            AcceptOnlySelfSignedServerCertificate = Constants.DefaultReplicatorSelfSignedCertificateOnly;
            #endif
        }

        ~ReplicatorOptionsDictionary()
        {
            Dispose(true);
        }

        internal ReplicatorOptionsDictionary(Dictionary<string, object?> raw) : base(raw)
        {
            if (ContainsKey(AuthOption)) {
                Auth = new AuthOptionsDictionary((this[AuthOption] as Dictionary<string, object?>)!);
            }

            if (ContainsKey(ChannelsKey)) {
                Channels = (this[ChannelsKey] as IList<object>)?.Cast<string>().ToList();
            }

            if (ContainsKey(DocIDsKey)) {
                DocIDs = (this[DocIDsKey] as IList<object>)?.Cast<string>().ToList();
            }

            if (ContainsKey(HeadersKey)) {
                Headers = (this[HeadersKey] as IDictionary<string, object?>)?.ToDictionary(x => x.Key,
                    x => x.Value as string) ?? new Dictionary<string, string?>();
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

        #endregion

        #region Private Methods

        private void AddCookies(string? cookiesString)
        {
            var split = cookiesString?.Split(';') ?? Enumerable.Empty<string>();
            foreach (var entry in split) {
                var pieces = entry?.Split('=');
                if (pieces?.Length != 2) {
                    WriteLog.To.Sync.W(Tag, "Garbage cookie value, ignoring");
                    continue;
                }

                Cookies.Add(new Cookie(pieces[0]?.Trim()!, pieces[1]?.Trim()));
            }
        }

        private void Dispose(bool finalizing)
        {
            if (_clientCertHandle.IsAllocated) {
                _clientCertHandle.Free();
            }

            if (_pinnedCertHandle.IsAllocated) {
                _pinnedCertHandle.Free();
            }
        }

        #endregion

        #region Overrides

        internal override void BuildInternal()
        {
            Auth?.Build();

            // If Headers contain Cookie
            if(Headers.ContainsKey("Cookie"))
                AddCookies(Headers["Cookie"]);

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

            Headers["User-Agent"] = HTTPLogic.UserAgent;
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

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
