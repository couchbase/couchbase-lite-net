// 
// SslStreamFactory.cs
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
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    internal sealed class SslStreamFactory : ISslStreamFactory
    {
        #region ISslStreamFactory

        public ISslStream Create(Stream inner)
        {
            return new SslStreamImpl(inner);
        }

        #endregion
    }

    internal sealed class SslStreamImpl : ISslStream
    {
        #region Constants

        private const string Tag = nameof(SslStreamImpl);

        #endregion

        #region Variables

        private readonly SslStream _innerStream;

        #endregion

        #region Properties

        public X509Certificate2 PinnedServerCertificate { get; set; }

        #endregion

        #region Constructors

        public SslStreamImpl(Stream inner)
        {
            _innerStream = new SslStream(inner, false, ValidateServerCert);
        }

        #endregion

        #region Private Methods

        private bool ValidateServerCert(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (PinnedServerCertificate != null) {
                var retVal = certificate.Equals(PinnedServerCertificate);
                if (!retVal) {
                    Log.To.Sync.W(Tag, "Server certificate did not match the pinned one!");
                }

                return retVal;
            }

            if (sslPolicyErrors != SslPolicyErrors.None) {
                Log.To.Sync.W(Tag, $"Error validating TLS chain: {sslPolicyErrors}");
                if (chain?.ChainStatus != null) {
                    for (int i = 0; i < chain.ChainStatus.Length; i++) {
                        var element = chain.ChainElements[i];
                        var status = chain.ChainStatus[i];
                        if (status.Status != X509ChainStatusFlags.NoError) {
                            Log.To.Sync.V(Tag,
                                $"Error {status.Status} ({status.StatusInformation}) for certificate:{Environment.NewLine}{element.Certificate}");
                        }
                    }
                }
            }

            return sslPolicyErrors == SslPolicyErrors.None;
        }

        #endregion

        #region ISslStream

        public Stream AsStream()
        {
            return _innerStream;
        }

        public Task ConnectAsync(string targetHost, ushort targetPort, X509CertificateCollection clientCertificates,
            bool checkCertificateRevocation)
        {
            return _innerStream.AuthenticateAsClientAsync(targetHost, clientCertificates, SslProtocols.Tls12,
                checkCertificateRevocation);
        }

        #endregion
    }
}
