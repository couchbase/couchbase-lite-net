// 
// ReplicatorOptionsDictionary.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    ///  A container for options that have to do with a <see cref="Replicator"/>
    /// </summary>
    internal sealed class ReplicatorOptionsDictionary : OptionsDictionary
    {
        #region Constants

        private const string AuthOption = "auth";
        private const string ChannelsKey = "channels";
        private const string ClientCertKey = "clientCert";
        private const string CookiesKey = "cookies";
        private const string DocIDsKey = "docIDs";
        private const string FilterKey = "filter";
        private const string FilterParamsKey = "filterParams";
        private const string HeadersKey = "headers";
        private const string PinnedCertKey = "pinnedCert";
        private const string ProtocolsOptionKey = "WS-Protocols";
        private const string Tag = nameof(ReplicatorOptionsDictionary);

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the authentication parameters
        /// </summary>
        [CanBeNull]
        public AuthOptionsDictionary Auth
        {
            get => this.GetCast<AuthOptionsDictionary>(AuthOption);
            set => this[AuthOption] = value;
        }

        /// <summary>
        /// Gets or sets the channels to replicate (pull only)
        /// </summary>
        [CanBeNull]
        [ItemNotNull]
        public IList<string> Channels
        {
            get => this.GetCast<IList<string>>(ChannelsKey);
            set => this[ChannelsKey] = value;
        }

        /// <summary>
        /// Gets or set the certificate to be used with client side
        /// authentication during TLS requests (optional)
        /// </summary>
        [CanBeNull]
        public X509Certificate2 ClientCert { get; set; }

        /// <summary>
        /// Gets or sets a collection of cookie objects to be passed along
        /// with the initial HTTP request of the <see cref="Replicator"/>
        /// </summary>
        [NotNull]
        [ItemNotNull]
        public ICollection<Cookie> Cookies { get; set; } = new List<Cookie>();

        /// <summary>
        /// Gets or sets the docIDs to replicate
        /// </summary>
        [CanBeNull]
        [ItemNotNull]
        public IList<string> DocIDs
        {
            get => this.GetCast<IList<string>>(DocIDsKey);
            set => this[DocIDsKey] = value;
        }

        /// <summary>
        /// Gets or sets the filter to use when replicating
        /// </summary>
        [CanBeNull]
        public string Filter
        {
            get => this.GetCast<string>(FilterKey);
            set => this[FilterKey] = value;
        }

        /// <summary>
        /// Gets or sets the parameters that will be passed along with the filter
        /// </summary>
        [CanBeNull]
        public IDictionary<string, object> FilterParams
        {
            get => this.GetCast<IDictionary<string, object>>(FilterParamsKey);
            set => this[FilterParamsKey] = value;
        }

        /// <summary>
        /// Gets a mutable collection of headers to be passed along with the initial
        /// HTTP request that starts replication
        /// </summary>
        [NotNull]
        public IDictionary<string, string> Headers
        {
            get => this.GetCast<IDictionary<string, string>>(HeadersKey) ?? new Dictionary<string, string>();
            set => this[HeadersKey] = value;
        }

        /// <summary>
        /// Gets or sets a certificate to trust.  All other certificates received
        /// by a <see cref="Replicator"/> with this configuration will be rejected.
        /// </summary>
        [CanBeNull]
        public X509Certificate2 PinnedServerCertificate { get; set; }
        
        internal string CookieString => this.GetCast<string>(CookiesKey);
        
        internal string Protocols => this.GetCast<string>(ProtocolsOptionKey);

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public ReplicatorOptionsDictionary()
        {
            Headers = new Dictionary<string, string>();
        }

        internal ReplicatorOptionsDictionary([NotNull]Dictionary<string, object> raw) : base(raw)
        {
            if (ContainsKey(AuthOption)) {
                Auth = new AuthOptionsDictionary(this[AuthOption] as Dictionary<string, object>);
            }

            if (ContainsKey(ChannelsKey)) {
                Channels = (this[ChannelsKey] as IList<object>)?.Cast<string>().ToList();
            }

            if (ContainsKey(DocIDsKey)) {
                DocIDs = (this[DocIDsKey] as IList<object>)?.Cast<string>().ToList();
            }

            if (ContainsKey(HeadersKey)) {
                Headers = (this[HeadersKey] as IDictionary<string, object>)?.ToDictionary(x => x.Key,
                    x => x.Value as string) ?? new Dictionary<string, string>();
            }

            if (ContainsKey(PinnedCertKey)) {
                PinnedServerCertificate = new X509Certificate2(this.GetCast<byte[]>(PinnedCertKey));
            }

            if (ContainsKey(ClientCertKey)) {
                ClientCert = new X509Certificate2(this.GetCast<byte[]>(ClientCertKey));
            }

            if (ContainsKey(CookiesKey)) {
                var split = ((string) this[CookiesKey])?.Split(';') ?? Enumerable.Empty<string>();
                foreach (var entry in split) {
                    var pieces = entry?.Split('=');
                    if (pieces?.Length != 2) {
                        Log.To.Sync.W(Tag, "Garbage cookie value, ignoring:  {0}", new SecureLogString(entry, LogMessageSensitivity.Insecure));
                        continue;
                    }

                    Cookies.Add(new Cookie(pieces[0]?.Trim(), pieces[1]?.Trim()));
                }
            }
        }

        #endregion

        #region Overrides

        internal override void FreezeInternal()
        {
            Auth?.Freeze();
            if (Cookies.Count > 0) {
                this[CookiesKey] = Cookies.Select(x => $"{x.Name}={x.Value}").Aggregate((l, r) => $"{l}; {r}");
            }

            if (PinnedServerCertificate != null) {
                this[PinnedCertKey] = PinnedServerCertificate.Export(X509ContentType.Cert);
            }

            if (ClientCert != null) {
                this[ClientCertKey] = ClientCert.Export(X509ContentType.Pfx);
            }

            Headers["User-Agent"] = HTTPLogic.UserAgent;
        }

        internal override bool Validate(string key, object value)
        {
            switch (key) {
                case AuthOption:
                    return value is AuthOptionsDictionary;
                case CookiesKey:
                    return value is string;
                case PinnedCertKey:
                    return value is byte[];
                default:
                    return true;
            }
        }

        #endregion
    }
}
