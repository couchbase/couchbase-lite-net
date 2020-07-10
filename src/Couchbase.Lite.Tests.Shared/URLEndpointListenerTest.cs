// 
// URLEndpointListenerTest.cs
// 
// Copyright (c) 2020 Couchbase, Inc All rights reserved.
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

#if COUCHBASE_ENTERPRISE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Couchbase.Lite;
using Couchbase.Lite.P2P;
using Couchbase.Lite.Sync;

using FluentAssertions;

#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
    internal static class URLEndpointListenerExtensions
    {
        #region Public Methods

        public static URLEndpoint LocalEndpoint(this URLEndpointListener listener)
            => new URLEndpoint(LocalUrl(listener));

        public static Uri LocalUrl(this URLEndpointListener listener)
        {
            Debug.Assert(listener.Port > 0);
            var builder = new UriBuilder(
                listener.Config.DisableTLS ? "ws" : "wss",
                "localhost",
                listener.Port,
                $"/{listener.Config.Database.Name}"
            );

            return builder.Uri;
        }

        #endregion
    }

#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class URLEndpointListenerTest : ReplicatorTestBase
    {
        #region Constants

        private const ushort WsPort = 4984;
        private const ushort WssPort = 4985;
        private const string ServerCertLabel = "CBL-Server-Cert";
        private const string ClientCertLabel = "CBL-Client-Cert";

        #endregion

        #region Variables

        private URLEndpointListener _listener;

        #endregion

        #region Constructors

#if !WINDOWS_UWP
        public URLEndpointListenerTest(ITestOutputHelper output) : base(output)
        {
        }
#endif

        #endregion

        #region Public Methods

        [Fact]
        public void TestServerCertVerificationModeSelfSigned()
        {
            if (!HasPersistentKeyStorage) return;

            var listener = CreateListener();
            listener.TlsIdentity.Should()
                .NotBeNull("because otherwise the TLS identity was not created for the listener");
            listener.TlsIdentity.Certs.Should().HaveCount(1,
                "because otherwise bogus certs were used");

            DisableDefaultServerCertPinning = true;

            RunReplication(
                listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                ServerCertificateVerificationMode.CACert,
                null,
                (int)CouchbaseLiteError.TLSCertUnknownRoot,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            RunReplication(
                listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                ServerCertificateVerificationMode.SelfSignedCert,
                null,
                0,
                0
            );
        }

        #endregion

        #region Private Methods

        private URLEndpointListener CreateListener(bool tls = true, IListenerAuthenticator auth = null)
        {
            _listener?.Stop();

            var config = new URLEndpointListenerConfiguration(OtherDb);
            config.Port = tls ? WssPort : WsPort;
            config.DisableTLS = !tls;
            config.Authenticator = auth;

            return Listen(config);
        }

        private URLEndpointListener Listen(URLEndpointListenerConfiguration config,
            int expectedErrCode = 0, CouchbaseLiteErrorType expectedErrDomain = 0)
        {
            _listener?.Stop();

            _listener = new URLEndpointListener(config);
            try {
                _listener.Start();
            } catch (CouchbaseLiteException e) {
                if (expectedErrCode == 0) {
                    throw;
                }

                e.Domain.Should().Be(expectedErrDomain);
                e.Error.Should().Be(expectedErrCode);
            }

            return _listener;
        }

        private X509Certificate2 CreateClientCert(string name)
        {
            var ecdsa = ECDsa.Create();
            var req = new CertificateRequest($"cn={name}", ecdsa, HashAlgorithmName.SHA256);
            return req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        }

        #endregion
    }
}
#endif