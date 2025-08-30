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
using System.Net;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

using Couchbase.Lite;
using Couchbase.Lite.P2P;
using Couchbase.Lite.Sync;

using Shouldly;

using Xunit;
using Xunit.Abstractions;
using System.Diagnostics.CodeAnalysis;

#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Test
{
    internal static class URLEndpointListenerExtensions
    {
        public static URLEndpoint LocalEndpoint(this URLEndpointListener listener) => new(LocalUrl(listener));

        private static Uri LocalUrl(this URLEndpointListener listener)
        {
            Debug.Assert(listener.Port > 0);
            var builder = new UriBuilder(
                listener.Config.DisableTLS ? "ws" : "wss",
                "localhost",
                listener.Port,
                $"/{listener.Config.Collections[0].Database.Name}"
            );

            return builder.Uri;
        }
    }

    public sealed class URLEndpointListenerTest(ITestOutputHelper output) : ReplicatorTestBase(output)
    {
        private const ushort WsPort = 5984;
        private const ushort WssPort = 5985;
        private const string ServerCertLabel = "CBL-Server-Cert";
        private const string ClientCertLabel = "CBL-Client-Cert";

        private URLEndpointListener? _listener;
        private readonly X509Store _store = new(StoreName.My);

        #if !NET_ANDROID

        [Fact]
        public void TestPort()
        {
            //init and start a listener
            _listener = CreateListener(false);
            //In order to get the test to pass on Linux, temp modify to this:
            _listener.Port.ShouldBeGreaterThan((ushort)0);
            //_listener.Port.ShouldBe(WsPort);
            //stop the listener
            _listener.Stop();
            _listener.Port.ShouldBe((ushort)0, "Listener's port should be 0 because the listener is stopped.");
        }

        [Fact]
        public void TestEmptyPort()
        {
            //init and start a listener
            var config = CreateListenerConfig(false);
            _listener = Listen(config);
            _listener.Port.ShouldNotBe((ushort)0, "Because the port is dynamically assigned.");

            //stop the listener
            _listener.Stop();
            _listener.Port.ShouldBe((ushort)0, "Listener's port should be 0 because the listener is stopped.");
        }

        [Fact]
        public void TestBusyPort()
        {
            _listener = CreateListener(false, false);
            _listener.Start();

            //listener1 uses the same port as listener
            var config = CreateListenerConfig(false, false, stopListener: false);
            var listener1 = Listen(config, GetEADDRINUSECode(), CouchbaseLiteErrorType.POSIX, stopListener: false);

            listener1.Dispose();
        }

        [Fact]
        public void TestTLSIdentity()
        {
            // TLS is disabled
            _listener = CreateListener(false);
            _listener.TlsIdentity.ShouldBeNull();
            _listener.Stop();
            _listener.TlsIdentity.ShouldBeNull();

            // Anonymous Identity
            _listener = CreateListener();
            _listener.TlsIdentity.ShouldNotBeNull();
            _listener.Stop();
            _listener.TlsIdentity.ShouldBeNull();

            // User Identity
            TLSIdentity.DeleteIdentity(_store, ServerCertLabel, null);
            var id = TLSIdentity.CreateIdentity(KeyUsages.ClientAuth,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "CBL-Server" } },
                null,
                _store,
                ServerCertLabel,
                null);
            var config = CreateListenerConfig(true, true, null, id);
            _listener = new URLEndpointListener(config);
            _listener.TlsIdentity.ShouldBeNull();
            _listener.Start();
            _listener.TlsIdentity.ShouldNotBeNull();
            _listener.TlsIdentity.ShouldBeEquivalentTo(config.TlsIdentity);
            _listener.Stop();
            _listener.TlsIdentity.ShouldBeNull();
        }

        [Fact]
        public void TestUrls()
        {
            _listener = CreateListener(false);

            _listener.Urls.ShouldNotBeNull();
            _listener.Urls.Count.ShouldBeGreaterThan(0);
            _listener.Stop();
            _listener.Urls.Count.ShouldBe(0);
        }

        [Fact]
        public void TestStatus()
        {
            ulong maxConnectionCount = 0UL;
            ulong maxActiveCount = 0UL;

            //init and start a listener
            _listener = CreateListener(false);

            //listener is started at this point
            _listener.Status.ConnectionCount.ShouldBe(0UL, "Listener's connection count should be 0 because no client connection has been established.");
            _listener.Status.ActiveConnectionCount.ShouldBe(0UL, "Listener's active connection count should be 0 because no client connection has been established.");

            using (var doc1 = new MutableDocument())
            using (var doc2 = new MutableDocument()) {
                doc1.SetString("name", "Sam");
                DefaultCollection.Save(doc1);
                doc2.SetString("name", "Mary");
                OtherDefaultCollection.Save(doc2);
            }

            var targetEndpoint = _listener.LocalEndpoint();
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            var config = new ReplicatorConfiguration(collectionConfigs, targetEndpoint);

            using (var repl = new Replicator(config)) {
                var waitAssert = new WaitAssert();
                var token = repl.AddChangeListener((_, args) =>
                {
                    WriteLine($"Yeehaw {_listener.Status.ConnectionCount} / {_listener.Status.ActiveConnectionCount}");

                    // ReSharper disable AccessToModifiedClosure
                    maxConnectionCount = Math.Max(maxConnectionCount, _listener.Status.ConnectionCount);
                    maxActiveCount = Math.Max(maxActiveCount, _listener.Status.ActiveConnectionCount);
                    // ReSharper restore AccessToModifiedClosure

                    waitAssert.RunConditionalAssert(() => args.Status.Activity == ReplicatorActivityLevel.Stopped);
                });

                repl.Start();
                while(repl.Status.Activity != ReplicatorActivityLevel.Busy) {
                    Thread.Sleep(100);
                }

                // For some reason running on Mac throws off the timing enough so that the active connection count
                // of 1 is never seen.  So record the value right after it becomes busy.
                maxConnectionCount = Math.Max(maxConnectionCount, _listener.Status.ConnectionCount);
                maxActiveCount = Math.Max(maxActiveCount, _listener.Status.ActiveConnectionCount);

                try {
                    waitAssert.WaitForResult(TimeSpan.FromSeconds(100));
                } finally {
                    token.Remove();
                }
            }

            maxConnectionCount.ShouldBe(1UL);
            maxActiveCount.ShouldBe(1UL);

            //stop the listener
            _listener.Stop();
            _listener.Status.ConnectionCount.ShouldBe(0UL, "Listener's connection count should be 0 because the connection is stopped.");
            _listener.Status.ActiveConnectionCount.ShouldBe(0UL, "Listener's active connection count should be 0 because the connection is stopped.");
        }

        [Fact]
        public void TestPasswordAuthenticator()
        {
            var auth = new ListenerPasswordAuthenticator((_, username, password) => 
                username == "daniel" && new NetworkCredential(String.Empty, password).Password == "123");

            _listener = CreateListener(false, true, auth);

            // Replicator - No authenticator
            var targetEndpoint = _listener.LocalEndpoint();
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            var config = new ReplicatorConfiguration(collectionConfigs, targetEndpoint);

            RunReplication(config, (int) CouchbaseLiteError.HTTPAuthRequired, CouchbaseLiteErrorType.CouchbaseLite);
            const string Pw = "123";
            const string WrongPw = "456";
            SecureString? pwSecureString;
            SecureString? wrongPwSecureString;
            unsafe {
                fixed (char* nativePw = Pw)
                fixed (char* nativeWrongPw = WrongPw) {
                    pwSecureString = new SecureString(nativePw, Pw.Length);
                    wrongPwSecureString = new SecureString(nativeWrongPw, WrongPw.Length);
                }
            }

            // Replicator - Wrong Credentials
            var nextConfig = config with
            {
                Authenticator = new BasicAuthenticator("daniel", wrongPwSecureString)
            };
            RunReplication(nextConfig, (int) CouchbaseLiteError.HTTPAuthRequired, CouchbaseLiteErrorType.CouchbaseLite);

            // Replicator - Success
            nextConfig = config with
            {
                Authenticator = new BasicAuthenticator("daniel", pwSecureString)
            };
            RunReplication(nextConfig, 0, 0);

            _listener.Stop();
            pwSecureString.Dispose();
            wrongPwSecureString.Dispose();
        }

#if !NET_ANDROID
#if !SANITY_ONLY
        [Fact]
        public void TestClientCertAuthWithCallback()
        {
            var auth = new ListenerCertificateAuthenticator((_, cert) =>
            {
                if (cert.Count != 1) {
                    return false;
                }

                return cert[0].SubjectName.Name.Replace("CN=", "") == "daniel";
            });

            // Obviously Fail
            var badAuth = new ListenerCertificateAuthenticator((_, cert) => cert.Count == 100);

            _listener = CreateListener(true, true, auth);

            // User Identity
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            var id = TLSIdentity.CreateIdentity(KeyUsages.ClientAuth,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "daniel" } },
                null,
                _store,
                ClientCertLabel,
                null);
            id.ShouldNotBeNull();

            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                new ClientCertificateAuthenticator(id),
                false,
                _listener.TlsIdentity!.Certs[0],
                0,
                0
            );

            RunReplication(
                collectionConfigs,
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
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                new ClientCertificateAuthenticator(id), // send wrong client cert
                false,
                _listener.TlsIdentity!.Certs[0],
                (int)CouchbaseLiteError.TLSHandshakeFailed,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            
        }
#endif
#endif

#if !SANITY_ONLY
        [Fact]
        public void TestClientCertAuthRootCertsError()
        {
            byte[] caData = GetFileByteArray("client-ca.der", typeof(URLEndpointListenerTest));
            var rootCert = new X509Certificate2(caData);
            var auth = new ListenerCertificateAuthenticator(new X509Certificate2Collection(rootCert));
            _listener = CreateListener(true, true, auth);

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            // Create wrong client identity
            var id = TLSIdentity.CreateIdentity(KeyUsages.ClientAuth,
                new Dictionary<string, string>() { { Certificate.CommonNameAttribute, "daniel" } },
                null,
                _store,
                ClientCertLabel,
                null);

            id.ShouldNotBeNull();
            
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                new ClientCertificateAuthenticator(id),
                true,
                _listener.TlsIdentity!.Certs[0],
                (int) CouchbaseLiteError.TLSHandshakeFailed, //not TLSClientCertRejected as mac has.
                CouchbaseLiteErrorType.CouchbaseLite
            );

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            _listener.Stop();
        }
#endif

#if !NET_ANDROID
#if !SANITY_ONLY
        [Fact]
        public void TestClientCertAuthenticatorRootCerts()
        {
            byte[] caData = GetFileByteArray("client-ca.der", typeof(URLEndpointListenerTest));

#if NET_ANDROID
            byte[] clientData = GetFileByteArray("client.pfx", typeof(URLEndpointListenerTest));
#else
            byte[] clientData = GetFileByteArray("client.p12", typeof(URLEndpointListenerTest)); 
#endif

            var rootCert = new X509Certificate2(caData);
            var auth = new ListenerCertificateAuthenticator(new X509Certificate2Collection(rootCert));
            _listener = CreateListener(true, true, auth);
            var serverCert = _listener.TlsIdentity!.Certs[0];

            // Cleanup
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);

            // Create client identity
            var id = TLSIdentity.ImportIdentity(_store, clientData, "123", ClientCertLabel, null);
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);

            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                new ClientCertificateAuthenticator(id!),
                true,
                serverCert,
                0,
                0
            );

            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);
            _listener.Stop();
        }
#endif

        [Fact]
        public void TestListenerWithImportIdentity()
        {
#if NET_ANDROID
            byte[] serverData = GetFileByteArray("client.pfx", typeof(URLEndpointListenerTest));
#else
            byte[] serverData = GetFileByteArray("client.p12", typeof(URLEndpointListenerTest)); 
#endif

            // Cleanup
            TLSIdentity.DeleteIdentity(_store, ClientCertLabel, null);

            // Import identity
            var id = TLSIdentity.ImportIdentity(_store, serverData, "123", ServerCertLabel, null);

            // Create listener and start
            var config = CreateListenerConfig(true, true, null, id);
            _listener = Listen(config);

            _listener.TlsIdentity.ShouldNotBeNull();

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Sam");
                DefaultCollection.Save(doc1);
            }

            OtherDefaultCollection.Count.ShouldBe(0UL);

            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null, //authenticator
                false, //accept only self-signed server cert
                _listener.TlsIdentity!.Certs[0], //server cert
                0,
                0
            );

            OtherDefaultCollection.Count.ShouldBe(1UL);

            _listener.Stop();
        }

        [Fact]
        public void TestAcceptSelfSignedCertWithPinnedCertificate()
        {
            _listener = CreateListener();
            _listener.TlsIdentity
                .ShouldNotBeNull("because otherwise the TLS identity was not created for the listener");
            _listener.TlsIdentity!.Certs.Count.ShouldBe(1,
                "because otherwise bogus certs were used");

            // listener = cert1; replicator.pin = cert2; acceptSelfSigned = true => fail
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null, //authenticator
                true, //accept only self-signed server cert
                DefaultServerCert, //server cert
                (int) CouchbaseLiteError.TLSCertUntrusted,
                CouchbaseLiteErrorType.CouchbaseLite
            );

            // listener = cert1; replicator.pin = cert1; acceptSelfSigned = false => pass
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false, //accept only self-signed server cert
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
            _listener.TlsIdentity
                .ShouldNotBeNull("because otherwise the TLS identity was not created for the listener");
            _listener.TlsIdentity!.Certs.Count.ShouldBe(1,
                "because otherwise bogus certs were used");

            DisableDefaultServerCertPinning = true;

            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false,//accept only self-signed server cert
                null,
                //TODO: Need to handle Linux throwing different error TLSCertUntrusted (5008)
                (int)CouchbaseLiteError.TLSCertUnknownRoot, //maui android 5006
                CouchbaseLiteErrorType.CouchbaseLite
            );

            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                true, //accept only self-signed server cert
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
            _listener.TlsIdentity
                .ShouldNotBeNull("because otherwise the TLS identity was not created for the listener");
            _listener.TlsIdentity!.Certs.Count.ShouldBe(1,
                "because otherwise bogus certs were used");

            DisableDefaultServerCertPinning = true;

            // Replicator - TLS Error
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false, //accept only self-signed server cert
                null,
                (int) CouchbaseLiteError.TLSCertUnknownRoot, //maui android 5006
                CouchbaseLiteErrorType.CouchbaseLite
            );

            // Replicator - Success
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false, //accept only self-signed server cert
                _listener.TlsIdentity.Certs[0],
                0,
                0
            );

            _listener.Stop();
        }
#endif

        [Fact]
        public void TestEmptyNetworkInterface()
        {
            var config = CreateListenerConfig(false, networkInterface: "0.0.0.0");
            _listener = Listen(config);
            _listener.Stop();
        }

        [Fact]
        public void TestUnavailableNetworkInterface()
        {
            var config = CreateListenerConfig(false, networkInterface: "1.1.1.256");
            Listen(config, (int) CouchbaseLiteError.UnknownHost);
            
            config = CreateListenerConfig(false, networkInterface: "blah");
            Listen(config, (int) CouchbaseLiteError.UnknownHost);
        }

#if false
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
#endif

#if !NET_ANDROID
        [Fact]
        public void TestMultipleListenersOnSameDatabase()
        {
            _listener = CreateListener();
            var listener2 = CreateNewListener();

            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Sam");
                DefaultCollection.Save(doc1);
                doc2.SetString("name", "Mary");
                OtherDefaultCollection.Save(doc2);
            }

            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            RunReplication(
                collectionConfigs,
                _listener.LocalEndpoint(),
                ReplicatorType.PushAndPull,
                false,
                null,
                false, //accept only self-signed server cert
                _listener.TlsIdentity!.Certs[0],
                0,
                0
            );

            listener2.Stop();
            OtherDefaultCollection.Count.ShouldBe(2UL);
        }

#if !SANITY_ONLY
        // A three-way replication with one database acting as both a listener
        // and a replicator
        [Fact]
        public void TestReplicatorAndListenerOnSameDatabase()
        {
            using (var doc = new MutableDocument()) {
                OtherDefaultCollection.Save(doc);
            }

            CreateListener();
            using (var doc1 = new MutableDocument()) {
                DefaultCollection.Save(doc1);
            }

            var target = new DatabaseEndpoint(Db);
            var collectionConfigs = CollectionConfiguration.FromCollections(OtherDefaultCollection);
            var config1 = CreateConfig(collectionConfigs, target, ReplicatorType.PushAndPull, true);
            var repl1 = new Replicator(config1);

            Database.Delete("urlepTestDb", Directory);
            using var urlepTestDb = OpenDB("urlepTestDb");
            using (var doc2 = new MutableDocument()) {
                urlepTestDb.GetDefaultCollection().Save(doc2);
            }

            var collectionConfigs2 = CollectionConfiguration.FromCollections(urlepTestDb.GetDefaultCollection());
            var config2 = CreateConfig(collectionConfigs2, _listener.LocalEndpoint(), ReplicatorType.PushAndPull, true,
                serverCert: _listener.TlsIdentity!.Certs[0]);
            var repl2 = new Replicator(config2);

            var wait1 = new ManualResetEventSlim();
            var wait2 = new ManualResetEventSlim();
            EventHandler<ReplicatorStatusChangedEventArgs> changeListener = (sender, args) =>
            {
                switch (args.Status.Activity) {
                    // ReSharper disable AccessToDisposedClosure
                    case ReplicatorActivityLevel.Idle when args.Status.Progress.Completed ==
                        args.Status.Progress.Total:
                    {
                        if (OtherDefaultCollection.Count == 3 && DefaultCollection.Count == 3 && urlepTestDb.GetDefaultCollection().Count == 3) {
                            ((Replicator?) sender)!.Stop();
                        }
                        break;
                    }
                    case ReplicatorActivityLevel.Stopped when sender == repl1:
                        wait1.Set();
                        break;
                    case ReplicatorActivityLevel.Stopped:
                        wait2.Set();
                        break;
                }
                // ReSharper restore AccessToDisposedClosure
            };

            var token1 = repl1.AddChangeListener(changeListener);
            var token2 = repl2.AddChangeListener(changeListener);

            repl1.Start();
            repl2.Start();
            WaitAssert.WaitAll([wait1, wait2], TimeSpan.FromSeconds(20))
                .ShouldBeTrue();

            token1.Remove();
            token2.Remove();

            DefaultCollection.Count.ShouldBe(3UL, "because otherwise not all docs were received into Db");
            OtherDefaultCollection.Count.ShouldBe(3UL, "because otherwise not all docs were received into OtherDb");
            urlepTestDb.GetDefaultCollection().Count.ShouldBe(3UL, "because otherwise not all docs were received into urlepTestDb");
            
            repl1.Dispose();
            repl2.Dispose();
            wait1.Dispose();
            wait2.Dispose();
            urlepTestDb.Close();

            Thread.Sleep(500); // wait for everything to stop
        }
#endif
#endif

        [Fact]
        public void TestReadOnlyListener()
        {
            using (var doc1 = new MutableDocument()) {
                DefaultCollection.Save(doc1);
            }

            var config = new URLEndpointListenerConfiguration([OtherDefaultCollection])
            {
                DisableTLS = true,
                ReadOnly = true
            };

            Listen(config);
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            RunReplication(collectionConfigs, _listener.LocalEndpoint(), ReplicatorType.PushAndPull,
                false, null, null,
                (int)CouchbaseLiteError.HTTPForbidden,
                CouchbaseLiteErrorType.CouchbaseLite);

            _listener.Stop();
        }

        [Fact]
        public void TestCloseWithActiveListener()
        {
            Listen(CreateListenerConfig(false));
            OtherDb.Close();
            _listener.Port.ShouldBe((ushort)0);
            _listener.Urls.ShouldBeEmpty();
        }

        [Fact]
        public void TestReplicatorServerCertNoTLS() => CheckReplicatorServerCert(false, false);

#if !NET_ANDROID
        [Fact]
        public void TestReplicatorServerCertWithTLS() => CheckReplicatorServerCert(true, true);

        [Fact]
        public void TestReplicatorServerCertWithTLSError() => CheckReplicatorServerCert(true, false);

#if !SANITY_ONLY
        [Fact] //hang maui android
        public void TestMultipleReplicatorsToListener()
        {
            _listener = Listen(CreateListenerConfig()); // writable listener

            // save a doc on listenerDB
            using (var doc = new MutableDocument()) {
                OtherDefaultCollection.Save(doc);
            }

            ValidateMultipleReplications(ReplicatorType.PushAndPull, 3, 3);
        }
#endif

#if NET6_0_OR_GREATER
        [Fact]
        public void TestMultipleReplicatorsOnReadOnlyListener()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            { 
                var config = CreateListenerConfig(readOnly: true);
                _listener = Listen(config);

                // save a doc on listener DB
                using (var doc = new MutableDocument()) {
                    OtherDefaultCollection.Save(doc);
                }

                ValidateMultipleReplications(ReplicatorType.Pull, 1, 2);
			}
        }
#endif

#if !SANITY_ONLY
        [Fact] //hang maui android
        public void TestCloseWithActiveReplicationsAndURLEndpointListener() => WithActiveReplicationsAndURLEndpointListener(true);

        [Fact]//hang maui android
        public void TestDeleteWithActiveReplicationsAndURLEndpointListener() => WithActiveReplicationsAndURLEndpointListener(false);
#endif

#endif

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
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            var config1 = CreateConfig(collectionConfigs, target, ReplicatorType.PushAndPull, true,
                serverCert: null);
            using var repl = new Replicator(config1);
            repl.AddChangeListener((_, args) =>
            {
                switch (args.Status.Activity) {
                    case ReplicatorActivityLevel.Idle:
                        waitIdleAssert.Set();
                        // Stop listener aka server
                        _listener.Stop();
                        break;
                    case ReplicatorActivityLevel.Stopped:
                        waitStoppedAssert.Set();
                        break;
                    case ReplicatorActivityLevel.Offline:
                    case ReplicatorActivityLevel.Connecting:
                    case ReplicatorActivityLevel.Busy:
                    default:
                        break;
                }
            });

            repl.Start();

            // Wait until idle then stop the listener
            waitIdleAssert.Wait(TimeSpan.FromSeconds(15)).ShouldBeTrue();

            // Wait for the replicator to be stopped
            waitStoppedAssert.Wait(TimeSpan.FromSeconds(20)).ShouldBeTrue();

            // Check error
            var error = repl.Status.Error as CouchbaseWebsocketException;
            error.ShouldNotBeNull();
            ((int)error.Error).ShouldBe((int)CouchbaseLiteError.WebSocketGoingAway);
        }

        [Fact]
        public void TestCollectionsSingleShotPushPullReplication() => CollectionsPushPullReplication(continuous: false);

        [Fact]
        public void TestCollectionsContinuousPushPullReplication() => CollectionsPushPullReplication(continuous: true);

        [Fact]
        public void TestMismatchedCollectionReplication()
        {
            using var colAOtherDb = OtherDb.CreateCollection("colA", "scopeA");
            using var colADb = Db.CreateCollection("colB", "scopeA");
            var collsOtherDb = new List<Collection>
            {
                colAOtherDb
            };

            var config = new URLEndpointListenerConfiguration(collsOtherDb)
            {
                Port = 0,
                DisableTLS = true
            };

            var listener = new URLEndpointListener(config);
            listener.Start();
            var targetEndpoint = listener.LocalEndpoint();
            var collectionConfigs = CollectionConfiguration.FromCollections(colADb);
            var replConfig = new ReplicatorConfiguration(collectionConfigs, targetEndpoint)
            {
                ReplicatorType = ReplicatorType.PushAndPull,
                Continuous = false
            };

            RunReplication(replConfig, (int)CouchbaseLiteError.HTTPNotFound, CouchbaseLiteErrorType.CouchbaseLite);

            listener.Stop();
        }

        [Fact]
        public void TestCreateListenerConfigWithEmptyCollection()
        {
            void BadAct() =>
                _ = new URLEndpointListenerConfiguration(new List<Collection>())
                {
                    Port = 0,
                    DisableTLS = true
                };

            Should.Throw<CouchbaseLiteException>((Action)BadAct).Message.ShouldBe("The given collections must not be null or empty.");
        }
#endif

        private void CollectionsPushPullReplication(bool continuous)
        {
            using var colAOtherDb = OtherDb.CreateCollection("colA", "scopeA");
            using var colADb = Db.CreateCollection("colA", "scopeA");
            using (var doc = new MutableDocument("doc"))
            using (var doc1 = new MutableDocument("doc1")) {
                doc.SetString("str", "string");
                doc1.SetString("str1", "string1");
                colADb.Save(doc);
                colADb.Save(doc1);
            }

            using (var doc = new MutableDocument("doc2"))
            using (var doc1 = new MutableDocument("doc3")) {
                doc.SetString("str2", "string2");
                doc1.SetString("str3", "string3");
                colAOtherDb.Save(doc);
                colAOtherDb.Save(doc1);
            }

            var collsOtherDb = new List<Collection>() { colAOtherDb };

            var config = new URLEndpointListenerConfiguration(collsOtherDb)
            {
                Port = 0,
                DisableTLS = true
            };

            var listener = new URLEndpointListener(config);
            listener.Start();

            var targetEndpoint = listener.LocalEndpoint();
            var collectionConfigs = CollectionConfiguration.FromCollections(colADb);
            var replConfig = new ReplicatorConfiguration(collectionConfigs, targetEndpoint)
            {
                ReplicatorType = ReplicatorType.PushAndPull,
                Continuous = continuous
            };

            RunReplication(replConfig, 0, 0);

            // Check docs are replicated between collections colADb & colAOtherDb
            colAOtherDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colADb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colADb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                
            listener.Stop();
        }


        private static int GetEADDRINUSECode()
        {
#if NET6_0_OR_GREATER && !__MOBILE__
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return 100;
            }

            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 48 : 98; // Linux
#elif __IOS__
            return 48;
#elif __ANDROID__
            return 98;
#else
            return 100;
#endif
        }


        private void WithActiveReplicatorAndURLEndpointListeners(bool isCloseNotDelete)
        {
            WaitAssert waitIdleAssert1 = new WaitAssert();
            WaitAssert waitStoppedAssert1 = new WaitAssert();

            _listener = CreateListener(false);
            var listener2 = CreateNewListener();

            _listener.Config.Collections[0].Database.ActiveStoppables.Count.ShouldBe(2);
            listener2.Config.Collections[0].Database.ActiveStoppables.Count.ShouldBe(2);

            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Sam");
                DefaultCollection.Save(doc1);
                doc2.SetString("name", "Mary");
                OtherDefaultCollection.Save(doc2);
            }

            var target = new DatabaseEndpoint(Db);
            var collectionConfigs = CollectionConfiguration.FromCollections(OtherDefaultCollection);
            var config1 = CreateConfig(collectionConfigs, target, ReplicatorType.PushAndPull, true);
            var repl1 = new Replicator(config1);
            repl1.AddChangeListener((_, args) => {
                waitIdleAssert1.RunConditionalAssert(() => args.Status.Activity == ReplicatorActivityLevel.Idle);
                waitStoppedAssert1.RunConditionalAssert(() => args.Status.Activity == ReplicatorActivityLevel.Stopped);
            });

            repl1.Start();

            waitIdleAssert1.WaitForResult(TimeSpan.FromSeconds(10));
            OtherDb.ActiveStoppables.Count.ShouldBe(3);

            if (isCloseNotDelete) {
                OtherDb.Close();
            } else {
                OtherDb.Delete();
            }

            OtherDb.ActiveStoppables.Count.ShouldBe(0);
            OtherDb.IsClosedLocked.ShouldBe(true);

            waitStoppedAssert1.WaitForResult(TimeSpan.FromSeconds(30));
        }

        private void WithActiveReplicationsAndURLEndpointListener(bool isCloseNotDelete)
        {
            var waitIdleAssert1 = new ManualResetEventSlim();
            var waitIdleAssert2 = new ManualResetEventSlim();
            var waitStoppedAssert1 = new ManualResetEventSlim();
            var waitStoppedAssert2 = new ManualResetEventSlim();

            using (var doc = new MutableDocument()) {
                OtherDefaultCollection.Save(doc);
            }

            _listener = CreateListener();
            _listener.Config.Collections[0].Database.ActiveStoppables.Count.ShouldBe(1);

            using (var doc1 = new MutableDocument()) {
                DefaultCollection.Save(doc1);
            }

            var target = new DatabaseEndpoint(Db);
            var collectionConfigs = CollectionConfiguration.FromCollections(OtherDefaultCollection);
            var config1 = CreateConfig(collectionConfigs, target, ReplicatorType.PushAndPull, true);
            var repl1 = new Replicator(config1);

            Database.Delete("urlepTestDb", Directory);
            using var urlepTestDb = OpenDB("urlepTestDb");
            using (var doc2 = new MutableDocument()) {
                urlepTestDb.GetDefaultCollection().Save(doc2);
            }

            var collectionConfigs2 = CollectionConfiguration.FromCollections(urlepTestDb.GetDefaultCollection());
            var config2 = CreateConfig(collectionConfigs2, _listener.LocalEndpoint(), ReplicatorType.PushAndPull, true,
                serverCert: _listener.TlsIdentity!.Certs[0]);
            var repl2 = new Replicator(config2);

            EventHandler<ReplicatorStatusChangedEventArgs> changeListener = (sender, args) =>
            {
                // ReSharper disable AccessToDisposedClosure
                switch (args.Status.Activity) {
                    case ReplicatorActivityLevel.Idle when args.Status.Progress.Completed ==
                        args.Status.Progress.Total:
                    {
                        if (sender == repl1) {
                            waitIdleAssert1.Set();
                        } else {
                            waitIdleAssert2.Set();
                        }
                        break;
                    }
                    case ReplicatorActivityLevel.Stopped when sender == repl1:
                        waitStoppedAssert1.Set();
                        break;
                    case ReplicatorActivityLevel.Stopped:
                        waitStoppedAssert2.Set();
                        break;
                    case ReplicatorActivityLevel.Offline:
                    case ReplicatorActivityLevel.Connecting:
                    case ReplicatorActivityLevel.Busy:
                    default:
                        break;
                }
                // ReSharper restore AccessToDisposedClosure
            };

            repl1.AddChangeListener(changeListener);
            repl2.AddChangeListener(changeListener);
            repl1.Start();
            repl2.Start();

            WaitAssert.WaitAll([waitIdleAssert1, waitIdleAssert2], _timeout)
                .ShouldBeTrue();

            OtherDb.ActiveStoppables.Count.ShouldBe(2);
            urlepTestDb.ActiveStoppables.Count.ShouldBe(1);

            if (isCloseNotDelete) {
                urlepTestDb.Close();
                OtherDb.Close();
            } else {
                urlepTestDb.Delete();
                OtherDb.Delete();
            }

            OtherDb.ActiveStoppables.Count.ShouldBe(0, "because OtherDb's active items should all be stopped");
            urlepTestDb.ActiveStoppables.Count.ShouldBe(0, "because urlepTestDb's active items should all be stopped");
            OtherDb.IsClosedLocked.ShouldBe(true);
            urlepTestDb.IsClosedLocked.ShouldBe(true);

            WaitAssert.WaitAll([waitStoppedAssert1, waitStoppedAssert2], TimeSpan.FromSeconds(20))
                .ShouldBeTrue();

            waitIdleAssert1.Dispose();
            waitIdleAssert2.Dispose();
            waitStoppedAssert1.Dispose();
            waitStoppedAssert2.Dispose();

            Thread.Sleep(500);
        }

        // Two replicators, replicates docs to the listener; validates connection status
        private void ValidateMultipleReplications(ReplicatorType replicatorType, ulong expectedListenerCount, ulong expectedLocalCount)
        {
            // This test used to check the max active count, however that is not reliable because
            // it is hard to catch the max active count with the information available.  By the time
            // the status changed callback runs the passive side could very well already be idle and
            // this show an active count of zero.
            ulong maxConnectionCount = 0UL;

            _listener.ShouldNotBeNull();
            var existingDocsInListener = _listener!.Config.Collections[0].Count;
            existingDocsInListener.ShouldBe(1UL);

            using (var doc1 = new MutableDocument()) {
                DefaultCollection.Save(doc1);
            }

            var target = _listener.LocalEndpoint();
            var serverCert = _listener.TlsIdentity!.Certs[0];
            var collectionConfigs = CollectionConfiguration.FromCollections(DefaultCollection);
            var config1 = CreateConfig(collectionConfigs, target, replicatorType, true, 
                serverCert: serverCert);
            var repl1 = new Replicator(config1);

            Database.Delete("urlepTestDb", Directory);
            using var urlepTestDb = OpenDB("urlepTestDb");
            using (var doc2 = new MutableDocument()) {
                urlepTestDb.GetDefaultCollection().Save(doc2);
            }

            var collectionConfigs2 = CollectionConfiguration.FromCollections(urlepTestDb.GetDefaultCollection());
            var config2 = CreateConfig(collectionConfigs2, target, replicatorType, true,
                serverCert: serverCert);
            var repl2 = new Replicator(config2);

            using var busy1 = new ManualResetEventSlim();
            using var busy2 = new ManualResetEventSlim();
            using var stopped1 = new ManualResetEventSlim();
            using var stopped2 = new ManualResetEventSlim();

            // Grab these now to avoid a race condition between Set() and WaitHandle side effect
            var busyHandles = new[] { busy1, busy2 };
            var stoppedHandles = new[] { stopped1, stopped2 };
            EventHandler<ReplicatorStatusChangedEventArgs> changeListener = (sender, args) =>
            {
                // ReSharper disable AccessToDisposedClosure
                var senderIsRepl1 = sender == repl1;
                var name = (senderIsRepl1) ? "repl1" : "repl2";
                WriteLine($"{name} -> {args.Status.Activity}");
                maxConnectionCount = Math.Max(maxConnectionCount, _listener.Status.ConnectionCount);
                switch (args.Status.Activity) {
                    case ReplicatorActivityLevel.Busy when senderIsRepl1:
                        WriteLine("Setting wait1 (busy)...");
                        busy1.Set();
                        break;
                    case ReplicatorActivityLevel.Busy:
                        WriteLine("Setting wait2 (busy)...");
                        busy2.Set();
                        break;
                    case ReplicatorActivityLevel.Idle when args.Status.Progress.Completed ==
                        args.Status.Progress.Total:
                    {
                        bool foundAllLocalDocs;
                        if(senderIsRepl1) {
                            foundAllLocalDocs = DefaultCollection.Count == expectedLocalCount;
                        } else {
                            foundAllLocalDocs = urlepTestDb.GetDefaultCollection().Count == expectedLocalCount;
                        }

                        if (OtherDefaultCollection.Count == expectedListenerCount && foundAllLocalDocs) {
                            WriteLine($"Stopping {sender}...");
                            ((Replicator?) sender)!.Stop();
                        }
                        break;
                    }
                    case ReplicatorActivityLevel.Stopped when sender == repl1:
                        WriteLine("Setting wait1 (stopped)...");
                        stopped1.Set();
                        break;
                    case ReplicatorActivityLevel.Stopped:
                        WriteLine("Setting wait2 (stopped)...");
                        stopped2.Set();
                        break;
                }
                // ReSharper restore AccessToDisposedClosure
            };

            var token1 = repl1.AddChangeListener(changeListener);
            var token2 = repl2.AddChangeListener(changeListener);

            repl1.Start();
            repl2.Start();

            WaitAssert.WaitAll(busyHandles, TimeSpan.FromSeconds(5))
                .ShouldBeTrue("because otherwise one of the replicators never became busy");

            WaitAssert.WaitAll(stoppedHandles, TimeSpan.FromSeconds(30))
                .ShouldBeTrue("because otherwise one of the replicators never stopped");

            // Depending on the whim of the divine entity, there are a number of ways in which the connections
            // can happen.  Commonly they run concurrently which results in a max connection count of 2.
            // However, they can also run sequentially which means only a count of 1.
            maxConnectionCount.ShouldBeGreaterThan(0UL);

            // all data are transferred to/from
            if (replicatorType == ReplicatorType.PushAndPull) {
                _listener.Config.Collections[0].Count.ShouldBe(existingDocsInListener + 2UL);
                DefaultCollection.Count.ShouldBe(existingDocsInListener + 2UL);
                urlepTestDb.GetDefaultCollection().Count.ShouldBe(existingDocsInListener + 2UL);
            } else if(replicatorType == ReplicatorType.Pull) {
                _listener.Config.Collections[0].Count.ShouldBe(1UL);
                DefaultCollection.Count.ShouldBe(existingDocsInListener + 1UL);
                urlepTestDb.GetDefaultCollection().Count.ShouldBe(existingDocsInListener + 1UL);
            }

            token1.Remove();
            token2.Remove();

            repl1.Dispose();
            repl2.Dispose();
            urlepTestDb.Close();

            Thread.Sleep(500);
        }

        private void RunReplicatorServerCert(Replicator repl, bool hasIdle, X509Certificate2? serverCert)
        {
            using var waitIdle = new ManualResetEventSlim();
            using var waitStopped = new ManualResetEventSlim();
            repl.AddChangeListener((_, args) =>
            {
                // ReSharper disable AccessToDisposedClosure
                var level = args.Status.Activity;
                var correctError = hasIdle ? args.Status.Error == null : args.Status.Error != null;
                switch (level) {
                    case ReplicatorActivityLevel.Idle:
                        waitIdle.Set();
                        break;
                    case ReplicatorActivityLevel.Stopped when correctError:
                        waitStopped.Set();
                        break;
                    case ReplicatorActivityLevel.Offline:
                    case ReplicatorActivityLevel.Connecting:
                    case ReplicatorActivityLevel.Busy:
                    default:
                        break;
                }
                // ReSharper restore AccessToDisposedClosure
            });

            repl.ServerCertificate.ShouldBeNull();
            repl.Start();

            if (hasIdle) {
                waitIdle.Wait(_timeout).ShouldBeTrue();
                if (serverCert == null) {
                    repl.ServerCertificate.ShouldBeNull();
                } else {
                    serverCert.Thumbprint.ShouldBe(repl.ServerCertificate?.Thumbprint);
                }

                repl.Stop();
            }

            waitStopped.Wait(_timeout).ShouldBeTrue();
            if (serverCert == null) {
                repl.ServerCertificate.ShouldBeNull();
            } else {
                serverCert.Thumbprint.ShouldBe(repl.ServerCertificate?.Thumbprint);
            }
        }

        private void CheckReplicatorServerCert(bool listenerTls, bool replicatorTls)
        {
            var listener = CreateListener(listenerTls);
            var serverCert = listenerTls ? listener.TlsIdentity!.Certs[0] : null;
            var collectionConfigs = CollectionConfiguration.FromCollections(OtherDefaultCollection);
            var config = CreateConfig(collectionConfigs, listener.LocalEndpoint(),
                ReplicatorType.PushAndPull, true,
                serverCert: replicatorTls ? serverCert : null);
            X509Certificate2? receivedServerCert;

            using (var repl = new Replicator(config)) {
                RunReplicatorServerCert(repl, listenerTls == replicatorTls, serverCert);
                receivedServerCert = repl.ServerCertificate;
            }

            if (listenerTls != replicatorTls) {
                config = CreateConfig(collectionConfigs, listener.LocalEndpoint(),
                    ReplicatorType.PushAndPull, true,
                    serverCert: receivedServerCert);
                using var repl = new Replicator(config);
                RunReplicatorServerCert(repl, true, serverCert);
            }

            _listener.Stop();
        }

        private URLEndpointListenerConfiguration CreateListenerConfig(bool tls = true, bool useDynamicPort = true,
            IListenerAuthenticator? auth = null, TLSIdentity? id = null, bool stopListener = true, string? networkInterface = null,
            bool readOnly = false)
        {
            if(stopListener)
                _listener?.Stop();

            var config = new URLEndpointListenerConfiguration([OtherDefaultCollection])
            {
                Port = (ushort)(useDynamicPort ? 0 : tls ? WssPort : WsPort),
                DisableTLS = !tls,
                Authenticator = auth,
                TlsIdentity = id,
                NetworkInterface = networkInterface,
                ReadOnly = readOnly
            };

            return config;
        }

        [MemberNotNull(nameof(_listener))]
        private URLEndpointListener CreateListener(bool tls = true, bool useDynamicPort = true, IListenerAuthenticator? auth = null)
        {
            _listener?.Stop();

            var config = new URLEndpointListenerConfiguration([OtherDefaultCollection])
            {
                //In order to get the test to pass on Linux, Port needs to be 0.
                Port = (ushort)(useDynamicPort ? 0 : tls ? WssPort : WsPort),
                DisableTLS = !tls,
                Authenticator = auth
            };

            return Listen(config);
        }

        [MemberNotNull(nameof(_listener))]
        private URLEndpointListener Listen(URLEndpointListenerConfiguration config,
            int expectedErrCode = 0, CouchbaseLiteErrorType expectedErrDomain = 0, bool stopListener = true)
        {
            if(stopListener)
                _listener?.Stop();

            _listener = new URLEndpointListener(config);

            _listener.Port.ShouldBe((ushort)0, "Listener's port should be 0 because the listener has not yet started.");
            _listener.Urls.ShouldNotBeNull();
            _listener.Urls.Count.ShouldBe(0, "Listener's Urls count should be 0 because the listener has not yet started.");
            _listener.TlsIdentity.ShouldBeNull("Listener's TlsIdentity should be null because the listener has not yet started.");
            _listener.Status.ConnectionCount.ShouldBe(0UL, "Listener's connection count should be 0 because the listener has not yet started.");
            _listener.Status.ActiveConnectionCount.ShouldBe(0UL, "Listener's active connection count should be 0 because the listener has not yet started.");

            try {
                _listener.Start();
            } catch (CouchbaseLiteException e) {
                if (expectedErrCode == 0) {
                    throw;
                }

                e.Domain.ShouldBe(expectedErrDomain);
                ((int)e.Error).ShouldBe(expectedErrCode); 
            } catch (CouchbaseNetworkException ne) {
                if (expectedErrCode == 0) {
                    throw;
                }

                ne.Domain.ShouldBe(expectedErrDomain);
                ((int)ne.Error).ShouldBe(expectedErrCode);
            } catch (CouchbasePosixException pe) {
                if (expectedErrCode == 0) {
                    throw;
                }

                pe.Domain.ShouldBe(expectedErrDomain);
                pe.Error.ShouldBe(expectedErrCode);
            }

            return _listener;
        }

        private URLEndpointListener CreateNewListener(bool enableTls = false)
        {
            var config = new URLEndpointListenerConfiguration([OtherDefaultCollection]) {
                Port = 0,
                DisableTLS = !enableTls
            };

            var listener = new URLEndpointListener(config);
            listener.Start();
            return listener;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _listener?.DeleteAnonymousTLSIdentity();
            _store.Dispose();
            _listener?.Dispose();
        }
    }
}
#endif