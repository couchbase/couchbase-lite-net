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
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        private X509Store _store;

        #endregion

        #region Constructors

#if !WINDOWS_UWP
        public URLEndpointListenerTest(ITestOutputHelper output) : base(output)
#else
        public URLEndpointListenerTest()
#endif
        {
            _store = new X509Store(StoreName.My);
        }


        #endregion

        #region Public Methods

        [Fact]
        public void TestPort()
        {
            //init and start a listener
            _listener = CreateListener(false);
            _listener.Port.Should().Be(WsPort);
            //stop the listener
            _listener.Stop();
            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener is stopped.");
        }

        [Fact]
        public void TestEmptyPort()
        {
            //init and start a listener
            var config = CreateListenerConfig(false, null, null, true);
            _listener = Listen(config, 0, 0);

            _listener.Port.Should().NotBe(0, "Because the port is dynamically assigned.");

            //stop the listener
            _listener.Stop();
            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener is stopped.");
        }

        [Fact]
        public void TestBusyPort()
        {
            var listener = CreateListener(false);
            //listener1 uses the same port as listener
            var config = CreateListenerConfig(false);
            var listener1 = Listen(config, PosixBase.GetCode(nameof(PosixWindows.EADDRINUSE)), CouchbaseLiteErrorType.POSIX);

            listener.Stop();
            listener1.Stop();
        }

        [Fact]
        public void TestUrls()
        {
            var listener = CreateListener(false);

            listener.Urls.Count.Should().NotBe(0);
            listener.Stop();
            listener.Urls.Count.Should().Be(0);
        }

        //[Fact] mac failed with latest LiteCore
        public void TestStatus()
        {
            HashSet<ulong> maxConnectionCount = new HashSet<ulong>(),
                maxActiveCount = new HashSet<ulong>();

            //init and start a listener
            _listener = CreateListener(false);

            //listener is started at this point
            _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because no client connection has been established.");
            _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because no client connection has been established.");

            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Sam");
                Db.Save(doc1);
            }

            var targetEndpoint = _listener.LocalEndpoint();
            var config = new ReplicatorConfiguration(Db, targetEndpoint);
            using (var repl = new Replicator(config)) {
                var waitAssert = new WaitAssert();
                var token = repl.AddChangeListener((sender, args) =>
                {
                    waitAssert.RunConditionalAssert(() =>
                    {
                        maxConnectionCount.Add(_listener.Status.ConnectionCount);
                        maxActiveCount.Add(_listener.Status.ActiveConnectionCount); 

                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                repl.Start();
                try {
                    waitAssert.WaitForResult(TimeSpan.FromSeconds(100));
                } finally {
                    repl.RemoveChangeListener(token);
                }
            }

            maxConnectionCount.Max().Should().Be(1);
            maxActiveCount.Max().Should().Be(1);//failed on late LiteCore but pass on earlier LiteCore

            //stop the listener
            _listener.Stop();
            _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because the connection is stopped.");
            _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because the connection is stopped.");
        }

        [Fact]
        public void TestPasswordAuthenticator()
        {
            var auth = new ListenerPasswordAuthenticator((sender, username, password) =>
            {
                return username == "daniel" && Encoding.Unicode.GetString(password) == "123";
            });

            _listener = CreateListener(false, auth);

            // Replicator - No authenticator
            var targetEndpoint = _listener.LocalEndpoint();
            var config = new ReplicatorConfiguration(Db, targetEndpoint);
            RunReplication(config, (int) CouchbaseLiteError.HTTPAuthRequired, CouchbaseLiteErrorType.CouchbaseLite);

            // Replicator - Wrong Credentials
            config.Authenticator = new BasicAuthenticator("daniel", Encoding.Unicode.GetBytes("456"));
            RunReplication(config, (int) CouchbaseLiteError.HTTPAuthRequired, CouchbaseLiteErrorType.CouchbaseLite);

            // Replicator - Success
            config.Authenticator = new BasicAuthenticator("daniel", Encoding.Unicode.GetBytes("123"));
            RunReplication(config, 0, 0);

            _listener.Stop();
        }

        [Fact]
        public void TestClientCertAuthenticator()
        {
            var auth = new ListenerCertificateAuthenticator((sender, cert) =>
            {

                return true;
            });

            _listener = CreateListener(true, auth);

            // User Identity
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            var id = TLSIdentity.CreateIdentity(false,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "daniel" } },
                null,
                _store,
                ClientCertLabel,
                null);

            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                new ClientCertificateAuthenticator(id),
                true,
                _listener.TlsIdentity.Certs[0],
                0,
                0
            );

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            _listener.Stop();
        }

        [Fact]
        public void TestServerCertVerificationModeSelfSigned()
        {
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
                false,
                null,
                (int)CouchbaseLiteError.TLSCertUnknownRoot,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            RunReplication(
                listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                true,
                null,
                0,
                0
            );
        }

        [Fact]
        public void TestTLSIdentity()
        {
            // TLS is disabled
            _listener = CreateListener(false);
            _listener.TlsIdentity.Should().BeNull();
            _listener.Stop();
            _listener.TlsIdentity.Should().BeNull();

            // Anonymous Identity
            _listener = CreateListener(true);
            _listener.TlsIdentity.Should().NotBeNull();
            _listener.Stop();
            _listener.TlsIdentity.Should().BeNull();

            // User Identity
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);
            var id = TLSIdentity.CreateIdentity(false,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CBL-Server" } },
                null,
                _store,
                ServerCertLabel,
                null);
            var config = CreateListenerConfig(true, null, id);
            _listener = new URLEndpointListener(config);
            _listener.TlsIdentity.Should().BeNull();
            _listener.Start();
            _listener.TlsIdentity.Should().NotBeNull();
            _listener.TlsIdentity.Should().BeEquivalentTo(config.TlsIdentity);
            _listener.Stop();
            _listener.TlsIdentity.Should().BeNull();
        }

        #endregion

        #region Private Methods

        private URLEndpointListenerConfiguration CreateListenerConfig(bool tls = true, 
            IListenerAuthenticator auth = null, TLSIdentity id = null, bool useDynamicPort = false)
        {
            _listener?.Stop();

            var config = new URLEndpointListenerConfiguration(OtherDb);
            if (useDynamicPort) {
                config.Port = 0;
            } else {
                config.Port = tls ? WssPort : WsPort;
            }

            config.DisableTLS = !tls;
            config.Authenticator = auth;
            config.TlsIdentity = id;

            return config;
        }

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

            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener has not yet started.");
            _listener.Urls.Count.Should().Be(0, "Listener's Urls count should be 0 because the listener has not yet started.");
            _listener.TlsIdentity.Should().BeNull("Listener's TlsIdentity should be null because the listener has not yet started.");
            _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because the listener has not yet started.");
            _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because the listener has not yet started.");

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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _store.Dispose();
        }
    }
}
#endif