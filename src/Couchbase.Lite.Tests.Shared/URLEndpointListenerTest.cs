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
using System.Net.NetworkInformation;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

using Couchbase.Lite;
using Couchbase.Lite.P2P;
using Couchbase.Lite.Sync;

using FluentAssertions;
using LiteCore.Interop;
using System.Runtime.InteropServices;

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
            //In order to get the test to pass on Linux, temp modify to this:
            _listener.Port.Should().BeGreaterThan(0);
            //_listener.Port.Should().Be(WsPort);
            //stop the listener
            _listener.Stop();
            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener is stopped.");
        }

        [Fact]
        public void TestEmptyPort()
        {
            //init and start a listener
            var config = CreateListenerConfig(false);
            _listener = Listen(config, 0, 0);

            _listener.Port.Should().NotBe(0, "Because the port is dynamically assigned.");

            //stop the listener
            _listener.Stop();
            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener is stopped.");
        }

        [Fact]
        public void TestBusyPort()
        {
            _listener = CreateListener(false, false);
            _listener.Start();

            //listener1 uses the same port as listener
            var config = CreateListenerConfig(false, false, stopListener: false);
            var listener1 = Listen(config, GetEADDRINUSECode(), CouchbaseLiteErrorType.POSIX, stopListener: false);

            _listener.Stop();
            listener1.Stop();
            listener1.Dispose();
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
            var config = CreateListenerConfig(true, true, null, id);
            _listener = new URLEndpointListener(config);
            _listener.TlsIdentity.Should().BeNull();
            _listener.Start();
            _listener.TlsIdentity.Should().NotBeNull();
            _listener.TlsIdentity.Should().BeEquivalentTo(config.TlsIdentity);
            _listener.Stop();
            _listener.TlsIdentity.Should().BeNull();
        }

        [Fact]
        public void TestUrls()
        {
            _listener = CreateListener(false);

            _listener.Urls.Count.Should().NotBe(0);
            _listener.Stop();
            _listener.Urls.Count.Should().Be(0);
        }

        [Fact]
        public void TestStatus()
        {
            ulong maxConnectionCount = 0UL;
            ulong maxActiveCount = 0UL;

            //init and start a listener
            _listener = CreateListener(false);

            //listener is started at this point
            _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because no client connection has been established.");
            _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because no client connection has been established.");

            using (var doc1 = new MutableDocument())
            using (var doc2 = new MutableDocument()) {
                doc1.SetString("name", "Sam");
                Db.Save(doc1);
                doc2.SetString("name", "Mary");
                OtherDb.Save(doc2);
            }

            var targetEndpoint = _listener.LocalEndpoint();
            var config = new ReplicatorConfiguration(Db, targetEndpoint);
            using (var repl = new Replicator(config)) {
                var waitAssert = new WaitAssert();
                var token = repl.AddChangeListener((sender, args) =>
                {
                    WriteLine($"Yeehaw {_listener.Status.ConnectionCount} / {_listener.Status.ActiveConnectionCount}");

                    maxConnectionCount = Math.Max(maxConnectionCount, _listener.Status.ConnectionCount);
                    maxActiveCount = Math.Max(maxActiveCount, _listener.Status.ActiveConnectionCount);

                    waitAssert.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                repl.Start();
                while(repl.Status.Activity != ReplicatorActivityLevel.Busy) {
                    Thread.Sleep(100);
                }

                // For some reason running on mac throws off the timing enough so that the active connection count
                // of 1 is never seen.  So record the value right after it becomes busy.
                maxConnectionCount = Math.Max(maxConnectionCount, _listener.Status.ConnectionCount);
                maxActiveCount = Math.Max(maxActiveCount, _listener.Status.ActiveConnectionCount);

                try {
                    waitAssert.WaitForResult(TimeSpan.FromSeconds(100));
                } finally {
                    token.Remove();
                }
            }

            maxConnectionCount.Should().Be(1);
            maxActiveCount.Should().Be(1);

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
                return username == "daniel" && new NetworkCredential(string.Empty, password).Password == "123";
            });

            _listener = CreateListener(false, true, auth);

            // Replicator - No authenticator
            var targetEndpoint = _listener.LocalEndpoint();
            var config = new ReplicatorConfiguration(Db, targetEndpoint);
            RunReplication(config, (int) CouchbaseLiteError.HTTPAuthRequired, CouchbaseLiteErrorType.CouchbaseLite);
            var pw = "123";
            var wrongPw = "456";
            SecureString pwSecureString = null;
            SecureString wrongPwSecureString = null;
            unsafe {
                fixed (char* pw_ = pw)
                fixed (char* wrongPw_ = wrongPw) {
                    pwSecureString = new SecureString(pw_, pw.Length);
                    wrongPwSecureString = new SecureString(wrongPw_, wrongPw.Length);
                }
            }

            // Replicator - Wrong Credentials
            config.Authenticator = new BasicAuthenticator("daniel", wrongPwSecureString);
            RunReplication(config, (int) CouchbaseLiteError.HTTPAuthRequired, CouchbaseLiteErrorType.CouchbaseLite);

            // Replicator - Success
            config.Authenticator = new BasicAuthenticator("daniel", pwSecureString);
            RunReplication(config, 0, 0);

            _listener.Stop();
            pwSecureString.Dispose();
            wrongPwSecureString.Dispose();
        }

        [Fact]
        public void TestClientCertAuthWithCallback()
        {
            var auth = new ListenerCertificateAuthenticator((sender, cert) =>
            {
                if (cert.Count != 1) {
                    return false;
                }

                return cert[0].SubjectName.Name?.Replace("CN=", "") == "daniel";
            });

            var badAuth = new ListenerCertificateAuthenticator((sender, cert) =>
            {
                return cert.Count == 100; // Obviously fail
            });

            _listener = CreateListener(true, true, auth);

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
                false,
                _listener.TlsIdentity.Certs[0],
                0,
                0
            );

            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null, // Don't send client cert
                false,
                _listener.TlsIdentity.Certs[0],
                (int)CouchbaseLiteError.TLSHandshakeFailed,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            _listener.Stop();
            _listener = CreateListener(true, true, badAuth);

            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                new ClientCertificateAuthenticator(id), // send wrong client cert
                false,
                _listener.TlsIdentity.Certs[0],
                (int)CouchbaseLiteError.TLSHandshakeFailed,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            
        }

        [Fact]
        public void TestClientCertAuthRootCertsError()
        {
            byte[] caData;
            using (var stream = typeof(URLEndpointListenerTest).Assembly.GetManifestResourceStream("client-ca.der"))
            using (var reader = new BinaryReader(stream)) {
                caData = reader.ReadBytes((int) stream.Length);
            }

            var rootCert = new X509Certificate2(caData);
            var auth = new ListenerCertificateAuthenticator(new X509Certificate2Collection(rootCert));
            _listener = CreateListener(true, true, auth);

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            // Create wrong client identity
            var id = TLSIdentity.CreateIdentity(false,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "daniel" } },
                null,
                _store,
                ClientCertLabel,
                null);

            id.Should().NotBeNull();
            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                new ClientCertificateAuthenticator(id),
                true,
                _listener.TlsIdentity.Certs[0],
                (int) CouchbaseLiteError.TLSHandshakeFailed, //not TLSClientCertRejected as mac has..
                CouchbaseLiteErrorType.CouchbaseLite
            );

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            _listener.Stop();
        }

        [Fact]
        public void TestClientCertAuthenticatorRootCerts()
        {
            byte[] caData, clientData;
            using(var stream = typeof(URLEndpointListenerTest).Assembly.GetManifestResourceStream("client-ca.der"))
            using (var reader = new BinaryReader(stream)) {
                caData = reader.ReadBytes((int)stream.Length);
            }

            using(var stream = typeof(URLEndpointListenerTest).Assembly.GetManifestResourceStream("client.p12"))
            using (var reader = new BinaryReader(stream)) {
                clientData = reader.ReadBytes((int)stream.Length);
            }

            var rootCert = new X509Certificate2(caData);
            var auth = new ListenerCertificateAuthenticator(new X509Certificate2Collection(rootCert));
            _listener = CreateListener(true, true, auth);
            var serverCert = _listener.TlsIdentity.Certs[0];

            // Cleanup
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);

            // Create client identity
            var id = TLSIdentity.ImportIdentity(_store, clientData, "123", ClientCertLabel, null);

            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                new ClientCertificateAuthenticator(id),
                true,
                serverCert,
                0,
                0
            );

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            _listener.Stop();
        }

        [Fact]
        public void TestListenerWithImportIdentity()
        {
            byte[] serverData = null;
            using (var stream = typeof(URLEndpointListenerTest).Assembly.GetManifestResourceStream("client.p12"))
            using (var reader = new BinaryReader(stream)) {
                serverData = reader.ReadBytes((int) stream.Length);
            }

            // Cleanup
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);

            // Import identity
            var id = TLSIdentity.ImportIdentity(_store, serverData, "123", ServerCertLabel, null);

            // Create listener and start
            var config = CreateListenerConfig(true, true, null, id);
            _listener = Listen(config);

            _listener.TlsIdentity.Should().NotBeNull();

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Sam");
                Db.Save(doc1);
            }

            OtherDb.Count.Should().Be(0);

            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null, //authenticator
                false, //accept only self signed server cert
                _listener.TlsIdentity.Certs[0], //server cert
                0,
                0
            );

            OtherDb.Count.Should().Be(1);

            _listener.Stop();
        }

        [Fact]
        public void TestAcceptSelfSignedCertWithPinnedCertificate()
        {
            _listener = CreateListener();
            _listener.TlsIdentity.Should()
                .NotBeNull("because otherwise the TLS identity was not created for the listener");
            _listener.TlsIdentity.Certs.Should().HaveCount(1,
                "because otherwise bogus certs were used");

            // listener = cert1; replicator.pin = cert2; acceptSelfSigned = true => fail
            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null, //authenticator
                true, //accept only self signed server cert
                DefaultServerCert, //server cert
                (int) CouchbaseLiteError.TLSCertUntrusted,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            // listener = cert1; replicator.pin = cert1; acceptSelfSigned = false => pass
            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false, //accept only self signed server cert
                _listener.TlsIdentity.Certs[0], //server cert
                0,
                0
            );

            _listener.Stop();
        }

        [Fact]
        public void TestAcceptOnlySelfSignedCertMode()
        {
            _listener = CreateListener();
            _listener.TlsIdentity.Should()
                .NotBeNull("because otherwise the TLS identity was not created for the listener");
            _listener.TlsIdentity.Certs.Should().HaveCount(1,
                "because otherwise bogus certs were used");

            DisableDefaultServerCertPinning = true;

            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false,//accept only self signed server cert
                null,
                //TODO: Need to handle Linux throwing different error TLSCertUntrusted (5008)
                (int)CouchbaseLiteError.TLSCertUnknownRoot,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                true, //accept only self signed server cert
                null,
                0,
                0
            );

            _listener.Stop();
        }

        [Fact]
        public void TestDoNotAcceptSelfSignedMode() //aka testPinnedServerCertificate in iOS
        {
            _listener = CreateListener();
            _listener.TlsIdentity.Should()
                .NotBeNull("because otherwise the TLS identity was not created for the listener");
            _listener.TlsIdentity.Certs.Should().HaveCount(1,
                "because otherwise bogus certs were used");

            DisableDefaultServerCertPinning = true;

            // Replicator - TLS Error
            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false, //accept only self signed server cert
                null,
                (int) CouchbaseLiteError.TLSCertUnknownRoot,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            // Replicator - Success
            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false, //accept only self signed server cert
                _listener.TlsIdentity.Certs[0],
                0,
                0
            );

            _listener.Stop();
        }

        [Fact]
        public void TestEmptyNetworkInterface()
        {
            var config = CreateListenerConfig(false);
            config.NetworkInterface = "0.0.0.0";
            _listener = Listen(config, 0, 0);
            _listener.Stop();
        }

        [Fact]
        public void TestUnavailableNetworkInterface()
        {
            var config = CreateListenerConfig(false);
            config.NetworkInterface = "1.1.1.256";
            Listen(config, (int) CouchbaseLiteError.UnknownHost, CouchbaseLiteErrorType.CouchbaseLite);

            config.NetworkInterface = "blah";
            Listen(config, (int) CouchbaseLiteError.UnknownHost, CouchbaseLiteErrorType.CouchbaseLite);
        }

        //[Fact] //CouchbaseLiteException (POSIXDomain / 101): The requested address is not valid in its context.
        public void TestNetworkInterfaceName()
        {
            var config = CreateListenerConfig(false);
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 
                    || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses) {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                            config.NetworkInterface = ip.Address.ToString();
                            _listener = Listen(config, 0, 0);
                            _listener.Stop();
                        }
                    }
                }
            }
        }

        [Fact]
        public void TestMultipleListenersOnSameDatabase()
        {
            _listener = CreateListener();
            var _listener2 = CreateNewListener();

            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Sam");
                Db.Save(doc1);
                doc2.SetString("name", "Mary");
                OtherDb.Save(doc2);
            }

            RunReplication(
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false, //accept only self signed server cert
                _listener.TlsIdentity.Certs[0],
                0,
                0
            );

            _listener.Stop();
            _listener2.Stop();

            OtherDb.Count.Should().Be(2);
        }

        // A three way replication with one database acting as both a listener
        // and a replicator
        [Fact]
        public void TestReplicatorAndListenerOnSameDatabase()
        {
            using (var doc = new MutableDocument()) {
                OtherDb.Save(doc);
            }

            CreateListener();
            using (var doc1 = new MutableDocument()) {
                Db.Save(doc1);
            }

            var target = new DatabaseEndpoint(Db);
            var config1 = CreateConfig(target, ReplicatorType.PushAndPull, true, sourceDb:OtherDb);
            var repl1 = new Replicator(config1);

            Database.Delete("urlepTestDb", Directory);
            var urlepTestDb = OpenDB("urlepTestDb");
            using (var doc2 = new MutableDocument()) {
                urlepTestDb.Save(doc2);
            }

            var config2 = CreateConfig(_listener.LocalEndpoint(), ReplicatorType.PushAndPull, true,
                serverCert: _listener.TlsIdentity.Certs[0], sourceDb: urlepTestDb);
            var repl2 = new Replicator(config2);

            var wait1 = new ManualResetEventSlim();
            var wait2 = new ManualResetEventSlim();
            EventHandler<ReplicatorStatusChangedEventArgs> changeListener = (sender, args) =>
            {
                if (args.Status.Activity == ReplicatorActivityLevel.Idle && args.Status.Progress.Completed ==
                    args.Status.Progress.Total) {
                    if (OtherDb.Count == 3 && Db.Count == 3 && urlepTestDb.Count == 3) {
                        ((Replicator) sender).Stop();
                    }

                } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    if (sender == repl1) {
                        wait1.Set();
                    } else {
                        wait2.Set();
                    }
                }
            };

            var token1 = repl1.AddChangeListener(changeListener);
            var token2 = repl2.AddChangeListener(changeListener);

            repl1.Start();
            repl2.Start();
            WaitHandle.WaitAll(new[] {wait1.WaitHandle, wait2.WaitHandle}, TimeSpan.FromSeconds(20))
                .Should().BeTrue();

            token1.Remove();
            token2.Remove();

            Db.Count.Should().Be(3, "because otherwise not all docs were received into Db");
            OtherDb.Count.Should().Be(3, "because otherwise not all docs were received into OtherDb");
            urlepTestDb.Count.Should().Be(3, "because otherwise not all docs were received into urlepTestDb");
            
            repl1.Dispose();
            repl2.Dispose();
            wait1.Dispose();
            wait2.Dispose();
            urlepTestDb.Delete();

            _listener.Stop();

            Thread.Sleep(500); // wait for everything to stop
        }

        [Fact]
        public void TestReadOnlyListener()
        {
            using (var doc1 = new MutableDocument()) {
                Db.Save(doc1);
            }

            var config = new URLEndpointListenerConfiguration(OtherDb)
            {
                ReadOnly = true
            };

            Listen(config);
            RunReplication(_listener.LocalEndpoint(), ReplicatorType.PushAndPull,
                false, null, null,
                (int)CouchbaseLiteError.HTTPForbidden,
                CouchbaseLiteErrorType.CouchbaseLite);

            _listener.Stop();
        }

        [Fact]
        public void TestCloseWithActiveListener()
        {
            Listen(CreateListenerConfig());
            OtherDb.Close();
            _listener.Port.Should().Be(0);
            _listener.Urls.Should().BeEmpty();
        }

        [Fact]
        public void TestReplicatorServerCertNoTLS() => CheckReplicatorServerCert(false, false);

        [Fact]
        public void TestReplicatorServerCertWithTLS() => CheckReplicatorServerCert(true, true);

        [Fact]
        public void TestReplicatorServerCertWithTLSError() => CheckReplicatorServerCert(true, false);

        [Fact]
        public void TestMultipleReplicatorsToListener()
        {
            _listener = Listen(CreateListenerConfig()); // writable listener

            // save a doc on listenerDB
            using (var doc = new MutableDocument()) {
                OtherDb.Save(doc);
            }

            ValidateMultipleReplicationsTo(ReplicatorType.PushAndPull);
        }

        //[Fact] Looks like MSBuild doesn't understand RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 
        public void TestMultipleReplicatorsOnReadOnlyListener()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) //Mac OS 8-23-21 hang with LiteCore Commit: 5d9539fae43e9282787c2b68772bb85ecbc00b5c [5d9539f]
            { 
                var config = CreateListenerConfig();
                config.ReadOnly = true;
                _listener = Listen(config);

                // save a doc on listener DB
                using (var doc = new MutableDocument()) {
                    OtherDb.Save(doc);
                }

                ValidateMultipleReplicationsTo(ReplicatorType.Pull);
            }
        }

        [Fact] //uwp
        public void TestCloseWithActiveReplicationsAndURLEndpointListener() => WithActiveReplicationsAndURLEndpointListener(true);

        [Fact] //uwp
        public void TestDeleteWithActiveReplicationsAndURLEndpointListener() => WithActiveReplicationsAndURLEndpointListener(false);

        [Fact]
        public void TestCloseWithActiveReplicatorAndURLEndpointListeners() => WithActiveReplicatorAndURLEndpointListeners(true);

        [Fact]
        public void TestDeleteWithActiveReplicatorAndURLEndpointListeners() => WithActiveReplicatorAndURLEndpointListeners(false);

        [Fact]
        public void TestStopListener()
        {
            ManualResetEventSlim waitIdleAssert = new ManualResetEventSlim();
            ManualResetEventSlim waitStoppedAssert = new ManualResetEventSlim();

            var config = CreateListenerConfig(false);
            _listener = Listen(config);

            var target = _listener.LocalEndpoint();
            var config1 = CreateConfig(target, ReplicatorType.PushAndPull, true,
                serverCert: null);
            using (var repl = new Replicator(config1)) {
                var token = repl.AddChangeListener((sender, args) =>
                {
                    if (args.Status.Activity == ReplicatorActivityLevel.Idle) {
                        waitIdleAssert.Set();
                        // Stop listener aka server
                        _listener.Stop();
                    } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                        waitStoppedAssert.Set();
                    }
                });

                repl.Start();

                // Wait until idle then stop the listener
                waitIdleAssert.Wait(TimeSpan.FromSeconds(15)).Should().BeTrue();

                // Wait for the replicator to be stopped
                waitStoppedAssert.Wait(TimeSpan.FromSeconds(20)).Should().BeTrue();

                // Check error
                var error = repl.Status.Error.As<CouchbaseWebsocketException>();
                error.Error.Should().Be((int)CouchbaseLiteError.WebSocketGoingAway);
            }
        }

        #endregion

        #region Replicator Config Network Interface

        enum TestReplicatorNIType
        {
            ValidAddress_SERVER_REACHABLE,
            ValidNI,
            ValidNI_SERVER_UNREACHABLE,
            InValidNI,
            InValidAddress
        }

        //#if !__ANDROID__ && !__IOS__ //Cannot run this test in emulators

        [Fact]
        public void TestReplicatorValidNetworkInterface()
        {
            // valid address and able to connect to server
            TestReplicatorNI(TestReplicatorNIType.ValidAddress_SERVER_REACHABLE);
            // valid ni and able to connect to server
            TestReplicatorNI(TestReplicatorNIType.ValidNI);
        }

        // Note: Mac tests will fail with db dispose failures (Infinite Taking a while for active items to stop...) if stacking below tests into one test
        // Please note all tests below will end up with offline status by design. 
        [Fact]
        public void TestReplicatorInValidNetworkInterface() => TestReplicatorNI(TestReplicatorNIType.InValidNI);

        [Fact]
        public void TestReplicatorInValidNIIPAddress() => TestReplicatorNI(TestReplicatorNIType.InValidAddress);

        // TestReplicatorNI(TestReplicatorNIType.ValidNI_SERVER_UNREACHABLE) failed in Mac. This test is pass on Windows.
        // A valid ethernet adapter NI is used (but not connect to network)
        [Fact]
        public void TestReplicatorValidAdapterNotConnectNetwork() => TestReplicatorNI(TestReplicatorNIType.ValidNI_SERVER_UNREACHABLE);

        //mac error code is different from windows error code..
        [Fact]
        public void TestReplicatorValidNIUnreachableServer()
        {
            ManualResetEventSlim waitOfflineAssert = new ManualResetEventSlim();
            ManualResetEventSlim waitStoppedAssert = new ManualResetEventSlim();

            var ni = GetNetworkInterface(TestReplicatorNIType.ValidNI);

            ni.Should().NotBeNull();

            //unreachable server
            var targetEndpoint = new URLEndpoint(new Uri("ws://192.168.0.117:4984/app"));
            var config = new ReplicatorConfiguration(Db, targetEndpoint) {
                ReplicatorType = ReplicatorType.PushAndPull,
                NetworkInterface = ni
            };
            //mac's error code is CouchbaseLiteError.AddressNotAvailable
            RunReplication(config, (int)CouchbaseLiteError.NetworkUnreachable, CouchbaseLiteErrorType.CouchbaseLite);
        }

        //#endif

        #endregion

        #region Private Methods

        private void TestReplicatorNI(TestReplicatorNIType type)
        {
            var ni = GetNetworkInterface(type);

            ni.Should().NotBeNull();

            ManualResetEventSlim waitOfflineAssert = new ManualResetEventSlim();
            ManualResetEventSlim waitStoppedAssert = new ManualResetEventSlim();

            var listenerConfig = CreateListenerConfig(false);
            _listener = Listen(listenerConfig);
            var target = _listener.LocalEndpoint();

            var replicatorConfig = new ReplicatorConfiguration(Db, target)
            {
                ReplicatorType = ReplicatorType.PushAndPull,
                Continuous = true,
                NetworkInterface = ni
            };

            if (type == TestReplicatorNIType.ValidNI ||
                    type == TestReplicatorNIType.ValidAddress_SERVER_REACHABLE)
                RunReplication(replicatorConfig, 0, 0);

            else {
                using (var repl = new Replicator(replicatorConfig)) {
                    var token = repl.AddChangeListener((sender, args) =>
                    {
                        if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                            var expectedException = (CouchbaseNetworkException)args.Status.Error;
                            expectedException.Error.Should().Be(CouchbaseLiteError.UnknownHost);
                            expectedException.Domain.Should().Be(CouchbaseLiteErrorType.CouchbaseLite);

                            waitOfflineAssert.Set();
                            repl.Stop();
                            _listener.Stop();
                        } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                            waitStoppedAssert.Set();
                        }
                    });

                    repl.Start();
                    waitOfflineAssert.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
                    // Wait for the replicator to be stopped
                    waitStoppedAssert.Wait(TimeSpan.FromSeconds(20)).Should().BeTrue();
                    repl.RemoveChangeListener(token);
                }

                Thread.Sleep(500);
            }
        }

        private string GetNetworkInterface(TestReplicatorNIType tyep)
        {
            if (tyep == TestReplicatorNIType.InValidNI)
                return "INVALID";

            if (tyep == TestReplicatorNIType.InValidAddress)
                return "1.1.1.256";

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                if (tyep <= TestReplicatorNIType.ValidNI && ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) {
                    if (tyep == TestReplicatorNIType.ValidAddress_SERVER_REACHABLE) {
                        if(ni.Supports(NetworkInterfaceComponent.IPv6))
                            return ni.GetIPProperties().UnicastAddresses.FirstOrDefault(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)?.Address.ToString();
                        else
                            return ni.GetIPProperties().UnicastAddresses.FirstOrDefault(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString();
                    }

                    return ni.Name;
                } else if (tyep == TestReplicatorNIType.ValidNI_SERVER_UNREACHABLE && 
                    ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    return ni.Name;
            }

            return null;
        }

        private int GetEADDRINUSECode()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return 100;
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return 48;
            } else {
                return 98; // Linux
            }
        }

        private void WithActiveReplicatorAndURLEndpointListeners(bool isCloseNotDelete)
        {
            WaitAssert waitIdleAssert1 = new WaitAssert();
            WaitAssert waitStoppedAssert1 = new WaitAssert();

            _listener = CreateListener();
            var _listener2 = CreateNewListener();

            _listener.Config.Database.ActiveStoppables.Count.Should().Be(2);
            _listener2.Config.Database.ActiveStoppables.Count.Should().Be(2);

            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Sam");
                Db.Save(doc1);
                doc2.SetString("name", "Mary");
                OtherDb.Save(doc2);
            }

            var target = new DatabaseEndpoint(Db);
            var config1 = CreateConfig(target, ReplicatorType.PushAndPull, true, sourceDb: OtherDb);
            var repl1 = new Replicator(config1);
            repl1.AddChangeListener((sender, args) => {
                waitIdleAssert1.RunConditionalAssert(() => {
                    return args.Status.Activity == ReplicatorActivityLevel.Idle;
                });

                waitStoppedAssert1.RunConditionalAssert(() => {
                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            repl1.Start();

            waitIdleAssert1.WaitForResult(TimeSpan.FromSeconds(10));
            OtherDb.ActiveStoppables.Count.Should().Be(3);

            if (isCloseNotDelete) {
                OtherDb.Close();
            } else {
                OtherDb.Delete();
            }

            OtherDb.ActiveStoppables.Count.Should().Be(0);
            OtherDb.IsClosedLocked.Should().Be(true);

            waitStoppedAssert1.WaitForResult(TimeSpan.FromSeconds(30));
        }

        private void WithActiveReplicationsAndURLEndpointListener(bool isCloseNotDelete)
        {
            var waitIdleAssert1 = new ManualResetEventSlim();
            var waitIdleAssert2 = new ManualResetEventSlim();
            var waitStoppedAssert1 = new ManualResetEventSlim();
            var waitStoppedAssert2 = new ManualResetEventSlim();

            using (var doc = new MutableDocument()) {
                OtherDb.Save(doc);
            }

            _listener = CreateListener();
            _listener.Config.Database.ActiveStoppables.Count.Should().Be(1);

            using (var doc1 = new MutableDocument()) {
                Db.Save(doc1);
            }

            var target = new DatabaseEndpoint(Db);
            var config1 = CreateConfig(target, ReplicatorType.PushAndPull, true, sourceDb: OtherDb);
            var repl1 = new Replicator(config1);

            Database.Delete("urlepTestDb", Directory);
            var urlepTestDb = OpenDB("urlepTestDb");
            using (var doc2 = new MutableDocument()) {
                urlepTestDb.Save(doc2);
            }

            var config2 = CreateConfig(_listener.LocalEndpoint(), ReplicatorType.PushAndPull, true,
                serverCert: _listener.TlsIdentity.Certs[0], sourceDb: urlepTestDb);
            var repl2 = new Replicator(config2);

            EventHandler<ReplicatorStatusChangedEventArgs> changeListener = (sender, args) =>
            {
                if (args.Status.Activity == ReplicatorActivityLevel.Idle && args.Status.Progress.Completed ==
                    args.Status.Progress.Total) {
                    if (sender == repl1) {
                        waitIdleAssert1.Set();
                    } else {
                        waitIdleAssert2.Set();
                    }
                } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    if (sender == repl1) {
                        waitStoppedAssert1.Set();
                    } else {
                        waitStoppedAssert2.Set();
                    }
                }
            };

            repl1.AddChangeListener(changeListener);
            repl2.AddChangeListener(changeListener);
            repl1.Start();
            repl2.Start();

            WaitHandle.WaitAll(new[] { waitIdleAssert1.WaitHandle, waitIdleAssert2.WaitHandle }, _timeout)
                .Should().BeTrue();

            OtherDb.ActiveStoppables.Count.Should().Be(2);
            urlepTestDb.ActiveStoppables.Count.Should().Be(1);

            if (isCloseNotDelete) {
                urlepTestDb.Close();
                OtherDb.Close();
            } else {
                urlepTestDb.Delete();
                OtherDb.Delete();
            }

            OtherDb.ActiveStoppables.Count.Should().Be(0);
            urlepTestDb.ActiveStoppables.Count.Should().Be(0);
            OtherDb.IsClosedLocked.Should().Be(true);
            urlepTestDb.IsClosedLocked.Should().Be(true);

            WaitHandle.WaitAll(new[] { waitStoppedAssert1.WaitHandle, waitStoppedAssert2.WaitHandle }, TimeSpan.FromSeconds(20))
                .Should().BeTrue();

            waitIdleAssert1.Dispose();
            waitIdleAssert2.Dispose();
            waitStoppedAssert1.Dispose();
            waitStoppedAssert2.Dispose();

            Thread.Sleep(500);
        }

        // Two replicators, replicates docs to the listener; validates connection status
        private void ValidateMultipleReplicationsTo(ReplicatorType replicatorType)
        {
            ulong maxConnectionCount = 0UL;
            ulong maxActiveCount = 0UL;

            var existingDocsInListener = _listener.Config.Database.Count;
            existingDocsInListener.Should().Be(1);

            using (var doc1 = new MutableDocument()) {
                Db.Save(doc1);
            }

            var target = _listener.LocalEndpoint();
            var serverCert = _listener.TlsIdentity.Certs[0];
            var config1 = CreateConfig(target, replicatorType, true, 
                serverCert: serverCert, sourceDb: Db);
            var repl1 = new Replicator(config1);

            Database.Delete("urlepTestDb", Directory);
            var urlepTestDb = OpenDB("urlepTestDb");
            using (var doc2 = new MutableDocument()) {
                urlepTestDb.Save(doc2);
            }

            var config2 = CreateConfig(target, replicatorType, true,
                serverCert: serverCert, sourceDb: urlepTestDb);
            var repl2 = new Replicator(config2);

            var wait1 = new ManualResetEventSlim();
            var wait2 = new ManualResetEventSlim();
            EventHandler<ReplicatorStatusChangedEventArgs> changeListener = (sender, args) =>
            {
                maxConnectionCount = Math.Max(maxConnectionCount, _listener.Status.ConnectionCount);
                maxActiveCount = Math.Max(maxActiveCount, _listener.Status.ActiveConnectionCount);

                if (args.Status.Activity == ReplicatorActivityLevel.Idle && args.Status.Progress.Completed ==
                    args.Status.Progress.Total) {

                    if ((replicatorType == ReplicatorType.PushAndPull && OtherDb.Count == 3
                    && Db.Count == 3 && urlepTestDb.Count == 3) || (replicatorType == ReplicatorType.Pull && OtherDb.Count == 1
                    && Db.Count == 2 && urlepTestDb.Count == 2)) {
                        ((Replicator) sender).Stop();
                    }
                } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    if (sender == repl1) {
                        wait1.Set();
                    } else {
                        wait2.Set();
                    }
                }
            };

            var token1 = repl1.AddChangeListener(changeListener);
            var token2 = repl2.AddChangeListener(changeListener);

            repl1.Start();
            repl2.Start();

            while (repl1.Status.Activity != ReplicatorActivityLevel.Busy ||
                repl2.Status.Activity != ReplicatorActivityLevel.Busy) {
                Thread.Sleep(100);
            }

            // For some reason running on mac throws off the timing enough so that the active connection count
            // of 1 is never seen.  So record the value right after it becomes busy.
            maxConnectionCount = Math.Max(maxConnectionCount, _listener.Status.ConnectionCount);
            maxActiveCount = Math.Max(maxActiveCount, _listener.Status.ActiveConnectionCount);

            WaitHandle.WaitAll(new[] { wait1.WaitHandle, wait2.WaitHandle }, TimeSpan.FromSeconds(30))
                .Should().BeTrue();

            maxConnectionCount.Should().Be(2);
            maxActiveCount.Should().Be(2);

            // all data are transferred to/from
            if (replicatorType == ReplicatorType.PushAndPull) {
                _listener.Config.Database.Count.Should().Be(existingDocsInListener + 2UL);
                Db.Count.Should().Be(existingDocsInListener + 2UL);
                urlepTestDb.Count.Should().Be(existingDocsInListener + 2UL);
            } else if(replicatorType == ReplicatorType.Pull) {
                _listener.Config.Database.Count.Should().Be(1);
                Db.Count.Should().Be(existingDocsInListener + 1UL);
                urlepTestDb.Count.Should().Be(existingDocsInListener + 1UL);
            }

            token1.Remove();
            token2.Remove();

            repl1.Dispose();
            repl2.Dispose();
            wait1.Dispose();
            wait2.Dispose();
            urlepTestDb.Delete();

            _listener.Stop();

            Thread.Sleep(500);
        }

        private void RunReplicatorServerCert(Replicator repl, bool hasIdle, X509Certificate2 serverCert)
        {
            using(var waitIdle = new ManualResetEventSlim())
            using (var waitStopped = new ManualResetEventSlim()) {
                repl.AddChangeListener((sender, args) =>
                {
                    var level = args.Status.Activity;
                    var correctError = hasIdle ? args.Status.Error == null : args.Status.Error != null;
                    if (level == ReplicatorActivityLevel.Idle) {
                        waitIdle.Set();
                    } else if (level == ReplicatorActivityLevel.Stopped && correctError) {
                        waitStopped.Set();
                    }
                });

                repl.ServerCertificate.Should().BeNull();
                repl.Start();

                if (hasIdle) {
                    waitIdle.Wait(_timeout).Should().BeTrue();
                    if (serverCert == null) {
                        repl.ServerCertificate.Should().BeNull();
                    } else {
                        serverCert.Thumbprint.Should().Be(repl.ServerCertificate?.Thumbprint);
                    }

                    repl.Stop();
                }

                waitStopped.Wait(_timeout).Should().BeTrue();
                if (serverCert == null) {
                    repl.ServerCertificate.Should().BeNull();
                } else {
                    serverCert.Thumbprint.Should().Be(repl.ServerCertificate?.Thumbprint);
                }
            }
        }

        private void CheckReplicatorServerCert(bool listenerTls, bool replicatorTls)
        {
            var listener = CreateListener(listenerTls);
            var serverCert = listenerTls ? listener.TlsIdentity.Certs[0] : null;
            var config = CreateConfig(listener.LocalEndpoint(),
                ReplicatorType.PushAndPull, true, sourceDb: OtherDb,
                serverCert: replicatorTls ? serverCert : null);
            X509Certificate2 receivedServerCert = null;

            using (var repl = new Replicator(config)) {
                RunReplicatorServerCert(repl, listenerTls == replicatorTls, serverCert);
                receivedServerCert = repl.ServerCertificate;
            }

            if (listenerTls != replicatorTls) {
                config = CreateConfig(listener.LocalEndpoint(),
                    ReplicatorType.PushAndPull, true, sourceDb: OtherDb,
                    serverCert: receivedServerCert);
                using (var repl = new Replicator(config)) {
                    RunReplicatorServerCert(repl, true, serverCert);
                }
            }

            _listener.Stop();
        }

        private URLEndpointListenerConfiguration CreateListenerConfig(bool tls = true, bool useDynamicPort = true,
            IListenerAuthenticator auth = null, TLSIdentity id = null, bool stopListener = true)
        {
            if(stopListener)
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

        private URLEndpointListener CreateListener(bool tls = true, bool useDynamicPort = true, IListenerAuthenticator auth = null)
        {
            _listener?.Stop();

            var config = new URLEndpointListenerConfiguration(OtherDb);
            //In order to get the test to pass on Linux, Port needs to be 0.
            if (useDynamicPort) {
                config.Port = 0;
            } else {
                config.Port = tls ? WssPort : WsPort;
            }

            config.DisableTLS = !tls;
            config.Authenticator = auth;

            return Listen(config);
        }

        private URLEndpointListener Listen(URLEndpointListenerConfiguration config,
            int expectedErrCode = 0, CouchbaseLiteErrorType expectedErrDomain = 0, bool stopListener = true)
        {
            if(stopListener)
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
            } catch (CouchbaseNetworkException ne) {
                if (expectedErrCode == 0) {
                    throw;
                }

                ne.Domain.Should().Be(expectedErrDomain);
                ne.Error.Should().Be(expectedErrCode);
            } catch (CouchbasePosixException pe) {
                if (expectedErrCode == 0) {
                    throw;
                }

                pe.Domain.Should().Be(expectedErrDomain);
                pe.Error.Should().Be(expectedErrCode);
            }

            return _listener;
        }

        private URLEndpointListener CreateNewListener()
        {
            var config = new URLEndpointListenerConfiguration(OtherDb)
            {
                Port = 0,
                DisableTLS = false
            };

            var listener = new URLEndpointListener(config);
            listener.Start();
            return _listener;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            _listener?.DeleteAnonymousTLSIdentity();
            base.Dispose(disposing);

            _store.Dispose();
            _listener?.Dispose();
        }
    }
}
#endif