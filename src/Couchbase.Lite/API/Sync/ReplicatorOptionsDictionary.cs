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
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    ///  A container for options that have to do with a <see cref="Replicator"/>
    /// </summary>
    public sealed class ReplicatorOptionsDictionary : OptionsDictionary
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

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the authentication parameters
        /// </summary>
        public AuthOptionsDictionary Auth
        {
            get => this.GetCast<AuthOptionsDictionary>(AuthOption);
            set => this[AuthOption] = value;
        }

        /// <summary>
        /// Gets or sets the channels to replicate (pull only)
        /// </summary>
        public IList<string> Channels
        {
            get => this.GetCast<IList<string>>(ChannelsKey);
            set => this[ChannelsKey] = value;
        }

        /// <summary>
        /// Gets or set the certificate to be used with client side
        /// authentication during TLS requests (optional)
        /// </summary>
        public X509Certificate2 ClientCert { get; set; }

        /// <summary>
        /// Gets or sets a collection of cookie objects to be passed along
        /// with the initial HTTP request of the <see cref="Replicator"/>
        /// </summary>
        public ICollection<Cookie> Cookies { get; set; } = new List<Cookie>();

        /// <summary>
        /// Gets or sets the docIDs to replicate
        /// </summary>
        public IList<string> DocIDs
        {
            get => this.GetCast<IList<string>>(DocIDsKey);
            set => this[DocIDsKey] = value;
        }

        /// <summary>
        /// Gets or sets the filter to use when replicating
        /// </summary>
        public string Filter
        {
            get => this.GetCast<string>(FilterKey);
            set => this[FilterKey] = value;
        }

        /// <summary>
        /// Gets or sets the parameters that will be passed along with the filter
        /// </summary>
        public IDictionary<string, object> FilterParams
        {
            get => this.GetCast<IDictionary<string, object>>(FilterParamsKey);
            set => this[FilterParamsKey] = value;
        }

        /// <summary>
        /// Gets a mutable collection of headers to be passed along with the initial
        /// HTTP request that starts replication
        /// </summary>
        public IDictionary<string, object> Headers
        {
            get => this.GetCast<IDictionary<string, object>>(HeadersKey);
            private set => this[HeadersKey] = value;
        }

        /// <summary>
        /// Gets or sets a certificate to trust.  All other certificates received
        /// by a <see cref="Replicator"/> with this configuration will be rejected.
        /// </summary>
        public X509Certificate2 PinnedServerCertificate { get; set; }

        internal string CookieString => this.GetCast<string>(CookiesKey);

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public ReplicatorOptionsDictionary()
        {
            Headers = new Dictionary<string, object>();
        }

        internal ReplicatorOptionsDictionary(Dictionary<string, object> raw) : base(raw)
        {
            if (raw.ContainsKey(AuthOption)) {
                Auth = new AuthOptionsDictionary(raw[AuthOption] as Dictionary<string, object>);
            }

            if (raw.ContainsKey(ChannelsKey)) {
                Channels = (this[ChannelsKey] as IList<object>).Cast<string>().ToList();
            }

            if (raw.ContainsKey(DocIDsKey)) {
                DocIDs = (this[DocIDsKey] as IList<object>).Cast<string>().ToList();
            }

            if (raw.ContainsKey(PinnedCertKey)) {
                PinnedServerCertificate = new X509Certificate2(this.GetCast<byte[]>(PinnedCertKey));
            }

            if (raw.ContainsKey(ClientCertKey)) {
                ClientCert = new X509Certificate2(this.GetCast<byte[]>(ClientCertKey));
            }

            if (raw.ContainsKey(CookiesKey)) {
                var split = ((string) this[CookiesKey]).Split(';');
                foreach (var entry in split) {
                    var pieces = entry.Split('=');
                    Cookies.Add(new Cookie(pieces[0].Trim(), pieces[1].Trim()));
                }
            }
        }

        #endregion

        #region Overrides

        internal override void FreezeInternal()
        {
            Auth?.Freeze();
            if (Cookies?.Count > 0) {
                this[CookiesKey] = Cookies.Select(x => $"{x.Name}={x.Value}").Aggregate((l, r) => $"{l}; {r}");
            }

            if (PinnedServerCertificate != null) {
                this[PinnedCertKey] = PinnedServerCertificate.Export(X509ContentType.Cert);
            }

            if (ClientCert != null) {
                this[ClientCertKey] = ClientCert.Export(X509ContentType.Pfx);
            }
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
