//
//  P2PTest.cs
//
//  Copyright (c) 2020 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Logging;

using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;
using Couchbase.Lite.Query;

using FluentAssertions;
using LiteCore;
using LiteCore.Interop;

using Newtonsoft.Json;
using System.Collections.Immutable;

using Test.Util;
#if COUCHBASE_ENTERPRISE
using Couchbase.Lite.P2P;
using ProtocolType = Couchbase.Lite.P2P.ProtocolType;
#endif

#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class P2PTest : TestCase
    {
        const ushort Port = 0;
        const string ServerCertLabel = "CBL-Server-Cert";
        const string ClientCertLabel = "CBL-Client-Cert";

        private static int Counter;
        private Database _otherDB;
        private Replicator _repl;
        private WaitAssert _waitAssert;
        URLEndpointListener _listener;
        URLEndpointListenerConfiguration _config;
#if !WINDOWS_UWP
        public P2PTest(ITestOutputHelper output) : base(output)
#else
        public P2PTest()
#endif
        {
            ReopenDB();
            var nextCounter = Interlocked.Increment(ref Counter);
            Database.Delete($"otherdb{nextCounter}", Directory);
            _otherDB = OpenDB($"otherdb{nextCounter}");
            //uncomment the code below when you need to see more detail log
            //Database.Log.Console.Level = LogLevel.Debug;
        }

#if COUCHBASE_ENTERPRISE        
        #region p2p unit tests
        
        [Fact]
        public void TestShortP2P()
        {
            //var testNo = 1;
            foreach (var protocolType in new[] { ProtocolType.ByteStream, ProtocolType.MessageStream }) {
                using (var mdoc = new MutableDocument("livesindb")) {
                    mdoc.SetString("name", "db");
                    Db.Save(mdoc);
                }

                using (var mdoc = new MutableDocument("livesinotherdb")) {
                    mdoc.SetString("name", "otherdb");
                    _otherDB.Save(mdoc);
                }

                var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, protocolType));
                var server = new MockServerConnection(listener, protocolType);
                var messageendpoint = new MessageEndpoint($"p2ptest1", server, protocolType,
                        new MockConnectionFactory(null));
                var uid = messageendpoint.Uid;

                // PUSH
                var config = new ReplicatorConfiguration(Db, messageendpoint) {
                    ReplicatorType = ReplicatorType.Push,
                    Continuous = false
                };
                RunReplication(config, 0, 0);
                _otherDB.Count.Should().Be(2UL, "because it contains the original and new");
                Db.Count.Should().Be(1UL, "because there is no pull, so the first db should only have the original");

                // PULL
                config = new ReplicatorConfiguration(Db, messageendpoint) {
                    ReplicatorType = ReplicatorType.Pull,
                    Continuous = false
                };

                RunReplication(config, 0, 0);
                Db.Count.Should().Be(2UL, "because the pull should add the document from otherDB");

                using (var savedDoc = Db.GetDocument("livesinotherdb"))
                using (var mdoc = savedDoc.ToMutable()) {
                    mdoc.SetBoolean("modified", true);
                    Db.Save(mdoc);
                }

                using (var savedDoc = _otherDB.GetDocument("livesindb"))
                using (var mdoc = savedDoc.ToMutable()) {
                    mdoc.SetBoolean("modified", true);
                    _otherDB.Save(mdoc);
                }

                // PUSH & PULL
                config = new ReplicatorConfiguration(Db,
                        new MessageEndpoint($"p2ptest1", server, protocolType,
                            new MockConnectionFactory(null))) { Continuous = false };

                RunReplication(config, 0, 0);
                Db.Count.Should().Be(2UL, "because no new documents were added");

                using (var savedDoc = Db.GetDocument("livesindb")) {
                    savedDoc.GetBoolean("modified").Should()
                        .BeTrue("because the property change should have come from the other DB");
                }

                using (var savedDoc = _otherDB.GetDocument("livesinotherdb")) {
                    savedDoc.GetBoolean("modified").Should()
                        .BeTrue("because the property change should come from the original DB");
                }

                Db.Delete();
                ReopenDB();
                _otherDB.Delete();
                _otherDB.Dispose();
                _otherDB = OpenDB(_otherDB.Name);
            }
        }

        [Fact]
        public void TestContinuousP2P()
        {
            _otherDB.Delete();
            _otherDB = OpenDB(_otherDB.Name);
            Db.Delete();
            ReopenDB();
            RunTwoStepContinuous(ReplicatorType.Push, "p2ptest1");
            _otherDB.Delete();
            _otherDB = OpenDB(_otherDB.Name);
            Db.Delete();
            ReopenDB();
            RunTwoStepContinuous(ReplicatorType.Pull, "p2ptest2");
            _otherDB.Delete();
            _otherDB = OpenDB(_otherDB.Name);
            Db.Delete();
            ReopenDB();
            RunTwoStepContinuous(ReplicatorType.PushAndPull, "p2ptest3");
        }

        //[Fact]
        //public void TestP2PRecoverableFailureDuringOpen() => TestP2PError(MockConnectionLifecycleLocation.Connect, true);

        //[Fact]
        //public void TestP2PRecoverableFailureDuringSend() => TestP2PError(MockConnectionLifecycleLocation.Send, true);

        //[Fact]
        //public void TestP2PRecoverableFailureDuringReceive() => TestP2PError(MockConnectionLifecycleLocation.Receive, true);

        [Fact]
        public void TestP2PPermanentFailureDuringOpen() => TestP2PError(MockConnectionLifecycleLocation.Connect, false);

        [Fact]
        public void TestP2PPermanentFailureDuringSend() => TestP2PError(MockConnectionLifecycleLocation.Send, false);

        [Fact]
        public void TestP2PPermanentFailureDuringReceive() => TestP2PError(MockConnectionLifecycleLocation.Receive, false);

        [Fact]
        public void TestP2PFailureDuringClose()
        {
            using (var mdoc = new MutableDocument("livesindb")) {
                mdoc.SetString("name", "db");
                Db.Save(mdoc);
            }

            var config = CreateFailureP2PConfiguration(ProtocolType.ByteStream, MockConnectionLifecycleLocation.Close,
                false);
            RunReplication(config, (int)CouchbaseLiteError.WebSocketUserPermanent, CouchbaseLiteErrorType.CouchbaseLite);
            config = CreateFailureP2PConfiguration(ProtocolType.MessageStream, MockConnectionLifecycleLocation.Close,
                false);
            RunReplication(config, (int)CouchbaseLiteError.WebSocketUserPermanent, CouchbaseLiteErrorType.CouchbaseLite, true);
        }

        [Fact]
        public void TestP2PPassiveClose()
        {
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, ProtocolType.MessageStream));
            var awaiter = new ListenerAwaiter(listener);
            var serverConnection = new MockServerConnection(listener, ProtocolType.MessageStream);
            var errorLogic = new ReconnectErrorLogic();
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", serverConnection, ProtocolType.MessageStream,
                    new MockConnectionFactory(errorLogic))) {
                Continuous = true
            };

            using (var replicator = new Replicator(config)) {
                replicator.Start();

                var count = 0;
                while (count++ < 10 && replicator.Status.Activity != ReplicatorActivityLevel.Idle) {
                    Thread.Sleep(500);
                    count.Should().BeLessThan(10, "because otherwise the replicator never went idle");
                }
                var connection = listener.Connections;
                errorLogic.ErrorActive = true;
                listener.Close(serverConnection);
                count = 0;
                while (count++ < 10 && replicator.Status.Activity != ReplicatorActivityLevel.Stopped) {
                    Thread.Sleep(500);
                    count.Should().BeLessThan(10, "because otherwise the replicator never stopped");
                }

                awaiter.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
                awaiter.Validate();

                replicator.Status.Error.Should()
                    .NotBeNull("because closing the passive side creates an error on the active one");
            }
        }

        [Fact]
        public void TestP2PPassiveCloseAll()
        {
            using (var doc = new MutableDocument("test")) {
                doc.SetString("name", "Smokey");
                Db.Save(doc);
            }

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, ProtocolType.MessageStream));
            var serverConnection1 = new MockServerConnection(listener, ProtocolType.MessageStream);
            var serverConnection2 = new MockServerConnection(listener, ProtocolType.MessageStream);
            var closeWait1 = new ManualResetEventSlim();
            var closeWait2 = new ManualResetEventSlim();
            var errorLogic = new ReconnectErrorLogic();
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", serverConnection1, ProtocolType.MessageStream,
                    new MockConnectionFactory(errorLogic))) {
                Continuous = true
            };

            var config2 = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest2", serverConnection2, ProtocolType.MessageStream,
                    new MockConnectionFactory(errorLogic))) {
                Continuous = true
            };

            using (var replicator = new Replicator(config))
            using (var replicator2 = new Replicator(config2)) {
                replicator.Start();
                replicator2.Start();

                var count = 0;
                while (count++ < 10 && replicator.Status.Activity != ReplicatorActivityLevel.Idle &&
                       replicator2.Status.Activity != ReplicatorActivityLevel.Idle) {
                    Thread.Sleep(500);
                    count.Should().BeLessThan(10, "because otherwise the replicator(s) never went idle");
                }

                errorLogic.ErrorActive = true;
                listener.AddChangeListener((sender, args) => {
                    if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                        if (args.Connection == serverConnection1) {
                            closeWait1.Set();
                        } else {
                            closeWait2.Set();
                        }
                    }
                });
                var connection = listener.Connections;
                listener.CloseAll();
                count = 0;
                while (count++ < 10 && replicator.Status.Activity != ReplicatorActivityLevel.Stopped &&
                       replicator2.Status.Activity != ReplicatorActivityLevel.Stopped) {
                    Thread.Sleep(500);
                    count.Should().BeLessThan(10, "because otherwise the replicator(s) never stopped");
                }

                closeWait1.Wait(TimeSpan.FromSeconds(5)).Should()
                    .BeTrue("because otherwise the first listener did not stop");
                closeWait2.Wait(TimeSpan.FromSeconds(5)).Should()
                    .BeTrue("because otherwise the second listener did not stop");

                replicator.Status.Error.Should()
                    .NotBeNull("because closing the passive side creates an error on the active one");
                replicator2.Status.Error.Should()
                    .NotBeNull("because closing the passive side creates an error on the active one");
            }
        }

        [Fact]
        public void TestP2PChangeListener()
        {
            var statuses = new List<ReplicatorActivityLevel>();
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, ProtocolType.ByteStream));
            var awaiter = new ListenerAwaiter(listener);
            var serverConnection = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", serverConnection, ProtocolType.ByteStream,
                    new MockConnectionFactory(null))) {
                Continuous = true
            };
            listener.AddChangeListener((sender, args) => {
                statuses.Add(args.Status.Activity);
            });
            var connection = listener.Connections;
            RunReplication(config, 0, 0);
            awaiter.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
            awaiter.Validate();
            statuses.Count.Should()
                .BeGreaterThan(1, "because otherwise there were no callbacks to the change listener");
        }

        [Fact]
        public void TestRemoveChangeListener()
        {
            var statuses = new List<ReplicatorActivityLevel>();
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, ProtocolType.ByteStream));
            var awaiter = new ListenerAwaiter(listener);
            var serverConnection = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", serverConnection, ProtocolType.ByteStream,
                    new MockConnectionFactory(null))) {
                Continuous = true
            };
            var token = listener.AddChangeListener((sender, args) => {
                statuses.Add(args.Status.Activity);
            });
            var connection = listener.Connections;
            listener.RemoveChangeListener(token);
            RunReplication(config, 0, 0);
            awaiter.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
            awaiter.Validate();

            statuses.Count.Should().Be(0);
        }
        
        #endregion

        #region URLEndpointListener tests

        [Fact]
        public void TestPort()
        {
            int exCnt = 0;

            _config = new URLEndpointListenerConfiguration(_otherDB);
            _config.Port = Port;
            _config.DisableTLS = true;

            //init a listener
            _listener = new URLEndpointListener(_config);
            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener has not yet started.");

            try {
                //start the listener
                _listener.Start();
            } catch {
                exCnt++;
            } finally {
                exCnt.Should().Be(0, "Because listener start should work without exception thrown.");
                _listener.Port.Should().NotBe(0);
                //stop the listener
                _listener.Stop();
                _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener is stopped.");
            }
        }

        [Fact]
        public void TestEmptyPort()
        {
            int exCnt = 0;

            _config = new URLEndpointListenerConfiguration(_otherDB);
            _config.Port = 0;
            _config.DisableTLS = true;

            //init a listener
            _listener = new URLEndpointListener(_config);
            _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener has not yet started.");

            try {
                //start the listener
                _listener.Start();
            } catch {
                exCnt++;
            } finally {
                exCnt.Should().Be(0, "Because listener start should work without exception thrown.");
                _listener.Port.Should().NotBe(0);
                //stop the listener
                _listener.Stop();
                _listener.Port.Should().Be(0, "Listener's port should be 0 because the listener is stopped.");
            }
        }

        [Fact]
        public void TestBusyPort()
        {
            CouchbasePosixException expectedException = null;
            var listener = ListenerWithTLS(false, null);

            var config = new URLEndpointListenerConfiguration(_otherDB);
            config.Port = listener.Port;
            config.DisableTLS = true;
            var listener1 = new URLEndpointListener(config);

            try {
                listener1.Start();
            } catch (CouchbasePosixException ex) {
                expectedException = ex;
            } finally {
                listener.Stop();
                listener1.Stop();
            }

            expectedException.Domain.Should().Be(CouchbaseLiteErrorType.POSIX);
            expectedException.Error.Should().Be(PosixBase.GetCode(nameof(PosixWindows.EADDRINUSE)));
        }

        [Fact]
        public void TestUrls()
        {
            int exCnt = 0;
            var config = new URLEndpointListenerConfiguration(_otherDB);
            var listener = new URLEndpointListener(config);
            listener.Urls.Count.Should().Be(0);

            try {
                listener.Start();
            } catch {
                exCnt++;
            } finally {
                exCnt.Should().Be(0, "Because listener start should work without exception thrown.");
                listener.Urls.Count.Should().NotBe(0);
                listener.Stop();
                listener.Urls.Count.Should().Be(0);
            }
        }

        [Fact]
        public void TestStatus()
        {
            int exCnt = 0;
            HashSet<ulong> maxConnectionCount = new HashSet<ulong>(),
                maxActiveCount = new HashSet<ulong>();

            _config = new URLEndpointListenerConfiguration(_otherDB);
            _config.Port = Port;
            _config.DisableTLS = true;

            //init a listener
            _listener = new URLEndpointListener(_config);
            _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because the listener has not yet started.");
            _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because the listener has not yet started.");

            try {
                //start the listener
                _listener.Start();
            } catch {
                exCnt++;
            } finally {
                exCnt.Should().Be(0, "Because listener start should work without exception thrown.");
                _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because no client connection has been established.");
                _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because no client connection has been established.");

                using (var doc1 = new MutableDocument("doc1"))
                using (var doc2 = new MutableDocument("doc2")) {
                    doc1.SetString("name", "Sam");
                    Db.Save(doc1);
                    doc2.SetString("name", "Mary");
                    _otherDB.Save(doc2);
                }

                var targetEndpoint = new URLEndpoint(new Uri($"{_listener.Urls[0]}".Replace("http", "ws")));
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
                maxActiveCount.Max().Should().Be(1);

                //stop the listener
                _listener.Stop();
                _listener.Status.ConnectionCount.Should().Be(0, "Listener's connection count should be 0 because the connection is stopped.");
                _listener.Status.ActiveConnectionCount.Should().Be(0, "Listener's active connection count should be 0 because the connection is stopped.");
            }
        }

        [Fact]
        public void TestPasswordAuthenticator()
        {
            var auth = new ListenerPasswordAuthenticator((sender, username, password) =>
            {
                return username == "daniel" && Encoding.Unicode.GetString(password) == "123";
            });

            _listener = ListenerWithTLS(false, auth);

            // Replicator - No authenticator
            var targetEndpoint = new URLEndpoint(new Uri($"{_listener.Urls[0]}".Replace("http", "ws")));
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

        #endregion

        #region TLSIdentity tests

        [Fact]
        public void TestCreateGetDeleteServerIdentity() => CreateGetDeleteServerIdentity(true);

        [Fact]
        public void TestCreateDuplicateServerIdentity() => CreateDuplicateServerIdentity(true);

        [Fact]
        public void TestCreateGetDeleteClientIdentity() => CreateGetDeleteServerIdentity(false);

        [Fact]
        public void TestCreateDuplicateClientIdentity() => CreateDuplicateServerIdentity(false);

        [Fact]
        public void TestGetIdentityWithCertCollection()
        {
            TLSIdentity id;
            X509Certificate2Collection certs = new X509Certificate2Collection();
            X509Chain certChain = new X509Chain();
            TLSIdentity.DeleteIdentity(ClientCertLabel);
            X509Certificate2 cert = Certificate.CreateX509Certificate2(false,
                new Dictionary<string, string>() { { Certificate.CommonName, "CA-P2PTest1" } },
                null,
                ClientCertLabel,
                false);
            //certChain.Build(cert);
            certs.Add(cert);

            id = TLSIdentity.GetIdentity(certs);
            id.Should().NotBeNull();

            // Delete
            TLSIdentity.DeleteIdentity(ClientCertLabel).Should().BeTrue();
        }
        
        [Fact]
        public void TestCreateIdentityFromCertCollectionAndImportIdentity()
        {
            TLSIdentity id;
            X509Certificate2Collection certs = new X509Certificate2Collection();
            X509Chain certChain = new X509Chain();
            TLSIdentity.DeleteIdentity(ClientCertLabel);

            //needs to know how to move the cert to the test location
            X509Certificate2 cert = GetMyCert();

            bool validChain = certChain.Build(cert);

            if (!validChain) {
                // Whatever you want to do about that.

                foreach (var status in certChain.ChainStatus) {
                    // In reality you can == this, since X509Chain.ChainStatus builds
                    // an object per flag, but since it's [Flags] let's play it safe.
                    if ((status.Status & X509ChainStatusFlags.PartialChain) != 0) {
                        // Incomplete chain.
                    }
                }
            }

            foreach (var element in certChain.ChainElements) {
                certs.Add(element.Certificate);
            }

            var hasPK = certs[0].HasPrivateKey;

            id = TLSIdentity.CreateIdentity(certs, null, ClientCertLabel);
            id.Should().NotBeNull();

            TLSIdentity.DeleteIdentity(ClientCertLabel).Should().BeTrue();

            // Import
            id = TLSIdentity.ImportIdentity(certs, null, ClientCertLabel);

            // Get
            id = TLSIdentity.GetIdentity(ClientCertLabel);
            id.Should().NotBeNull();

            // Delete
            TLSIdentity.DeleteIdentity(ClientCertLabel).Should().BeTrue();
        }

        [Fact]
        public void TestCreateIdentityWithNoAttributesOrEmptyAttributes()
        {
            // Delete 
            TLSIdentity.DeleteIdentity(ServerCertLabel).Should().BeTrue();

            //Get
            var id = TLSIdentity.GetIdentity(ServerCertLabel);
            id.Should().BeNull();

            // Create id with empty Attributes
            Action badAction = (() => TLSIdentity.CreateIdentity(true,
                new Dictionary<string, string>() { },
                null,
                ServerCertLabel));
            badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.CreateCertAttributeEmpty);

            // Create id with null Attributes
            badAction = (() => TLSIdentity.CreateIdentity(true,
                null,
                null,
                ServerCertLabel));
            badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.CreateCertAttributeEmpty);
        }

        [Fact]
        public void TestCertificateExpiration()
        {
            TLSIdentity id;

            // Delete 
            TLSIdentity.DeleteIdentity(ServerCertLabel).Should().BeTrue();

            //Get
            id = TLSIdentity.GetIdentity(ServerCertLabel);
            id.Should().BeNull();

            var fiveMinToExpireCert = DateTimeOffset.UtcNow.AddMinutes(5);
            id = TLSIdentity.CreateIdentity(true,
                new Dictionary<string, string>() { { Certificate.CommonName, "CA-P2PTest" } },
                fiveMinToExpireCert,
                ServerCertLabel);

            (id.Expiration - fiveMinToExpireCert).Should().BeGreaterThan(TimeSpan.MinValue);

            // Delete 
            TLSIdentity.DeleteIdentity(ServerCertLabel).Should().BeTrue();
        }

        #endregion

        #region TLSIdentity tests helpers

        static private X509Certificate2 GetMyCert()
        {
            //TODO move the cert into the test local location
            X509Certificate2 cert = null;

            // Load the certificate
            var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certCollection = store.Certificates.Find
            (
                X509FindType.FindBySubjectName,
                "certs",
                false    // Including invalid certificates
            );

            if (certCollection.Count > 0) {
                cert = certCollection[0];
            }

            store.Close();

            return cert;
        }

        private void AddRootCert()
        {
            X509Store store = new X509Store("Trust");
            store.Open(OpenFlags.ReadWrite);

            string certString = "MIIDGjCCAgKgAwIBAgICApowDQYJKoZIhvcNAQEFBQAwLjELMAkGA1UEBhMCQ1oxDjAMBgNVBAoTBVJlYmV4MQ8wDQYDVQQDEwZUZXN0Q0EwHhcNMDAwMTAxMDAwMDAwWhcNNDkxMjMxMDAwMDAwWjAuMQswCQYDVQQGEwJDWjEOMAwGA1UEChMFUmViZXgxDzANBgNVBAMTBlRlc3RDQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMgeRAcaNLTFaaBhFx8RDJ8b9K655dNUXmO11mbImDbPq4qVeZXDgAjnzPov8iBscwfqBvBpF38LsxBIPA2i1d0RMsQruOhJHttA9I0enElUXXj63sOEVNMSQeg1IMyvNeEotag+Gcx6SF+HYnariublETaZGzwAOD2SM49mfqUyfkgeTjdO6qp8xnoEr7dS5pEBHDg70byj/JEeZd3gFea9TiOXhbCrI89dKeWYBeoHFYhhkaSB7q9EOaUEzKo/BQ6PBHFu6odfGkOjXuwfPkY/wUy9U4uj75LmdhzvJf6ifsJS9BQZF4//JcUYSxiyzpxDYqSbTF3g9w5Ds2LOAscCAwEAAaNCMEAwDgYDVR0PAQH/BAQDAgB/MA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFD1v20tPgvHTEK/0eLO09j0rL2qXMA0GCSqGSIb3DQEBBQUAA4IBAQAZIjcdR3EZiFJ67gfCnPBrxVgFNvaRAMCYEYYIGDCAUeB4bLTu9dvun9KFhgVNqjgx+xTTpx9d/5mAZx5W3YAG6faQPCaHccLefB1M1hVPmo8md2uw1a44RHU9LlM0V5Lw8xTKRkQiZz3Ysu0sY27RvLrTptbbfkE4Rp9qAMguZT9cFrgPAzh+0zuo8NNj9Jz7/SSa83yIFmflCsHYSuNyKIy2iaX9TCVbTrwJmRIB65gqtTb6AKtFGIPzsb6nayHvgGHFchrFovcNrvRpE71F38oVG+eCjT23JfiIZim+yJLppSf56167u8etDcQ39j2b9kzWlHIVkVM0REpsKF7S";
            X509Certificate2 rootCert = new X509Certificate2(Convert.FromBase64String(certString));
            if (!store.Certificates.Contains(rootCert))
                store.Add(rootCert);

            store.Close();
        }

        private void CreateDuplicateServerIdentity(bool isServer)
        {
            string commonName = isServer ? "CBL-Server" : "CBL-Client";
            string label = isServer ? ServerCertLabel : ClientCertLabel;
            TLSIdentity id;
            Dictionary<string, string> attr = new Dictionary<string, string>() { { Certificate.CommonName, commonName } };

            // Delete 
            TLSIdentity.DeleteIdentity(label).Should().BeTrue();

            // Create
            id = TLSIdentity.CreateIdentity(isServer,
                attr,
                null,
                label);
            id.Should().NotBeNull();
            id.Certs.Count.Should().Be(1);

            //Get
            id = TLSIdentity.GetIdentity(label);
            id.Should().NotBeNull();

            // Create again with the same label
            Action badAction = (() => TLSIdentity.CreateIdentity(isServer,
                attr,
                null,
                label));
            badAction.Should().Throw<CouchbaseLiteException>(CouchbaseLiteErrorMessage.DuplicateCertificate);
        }

        private byte[] GetPublicKeyHashFromCert(X509Certificate2 cert)
        {
            return cert.GetPublicKey();
        }
        #endregion

        private URLEndpoint LocalEndPoint(bool tls, Uri uri)
        {
            var scheme = tls ? "wss" : "ws";
            return new URLEndpoint(new Uri($"{uri}".Replace("ws", "wss")));
        }

        private URLEndpointListener ListenerWithTLS(bool tls, IListenerAuthenticator auth)
        {
            int exCnt = 0;
            
            var config = new URLEndpointListenerConfiguration(_otherDB);
            config.DisableTLS = !tls;
            config.Authenticator = auth;

            var listener = new URLEndpointListener(config);

            try {
                //start the listener
                listener.Start();
            } catch {
                exCnt++;
            }

            if (exCnt == 0)
                return listener;
            else
                return null;
        }

        

        private ReplicatorConfiguration CreateFailureP2PConfiguration(ProtocolType protocolType, MockConnectionLifecycleLocation location, bool recoverable)
        {
            var errorLocation = TestErrorLogic.FailWhen(location);

            if (recoverable) {
                errorLocation.WithRecoverableException();
            } else {
                errorLocation.WithPermanentException();
            }

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, protocolType));
            var server = new MockServerConnection(listener, protocolType) {
                ErrorLogic = errorLocation
            };

            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", server, protocolType, new MockConnectionFactory(errorLocation))) {
                ReplicatorType = ReplicatorType.Push,
                Continuous = false
            };

            var connection = listener.Connections;
            return config;
        }

        private void TestP2PError(MockConnectionLifecycleLocation location, bool recoverable)
        {
            using (var mdoc = new MutableDocument("livesindb")) {
                mdoc.SetString("name", "db");
                Db.Save(mdoc);
            }

            var expectedDomain = recoverable ? 0 : CouchbaseLiteErrorType.CouchbaseLite;
            var expectedCode = recoverable ? 0 : (int)CouchbaseLiteError.WebSocketUserPermanent;

            var config = CreateFailureP2PConfiguration(ProtocolType.ByteStream, location, recoverable);
            RunReplication(config, expectedCode, expectedDomain);
            config = CreateFailureP2PConfiguration(ProtocolType.MessageStream, location, recoverable);
            RunReplication(config, expectedCode, expectedDomain, true);
        }

        private void RunTwoStepContinuous(ReplicatorType type, string uid)
        {
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, ProtocolType.ByteStream));
            var server = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint(uid, server, ProtocolType.ByteStream, new MockConnectionFactory(null))) {
                ReplicatorType = type,
                Continuous = true
            };

            using (var replicator = new Replicator(config)) {
                replicator.Start();

                Database firstSource = null;
                Database secondSource = null;
                Database firstTarget = null;
                Database secondTarget = null;
                if (type == ReplicatorType.Push) {
                    firstSource = Db;
                    secondSource = Db;
                    firstTarget = _otherDB;
                    secondTarget = _otherDB;
                } else if (type == ReplicatorType.Pull) {
                    firstSource = _otherDB;
                    secondSource = _otherDB;
                    firstTarget = Db;
                    secondTarget = Db;
                } else {
                    firstSource = Db;
                    secondSource = _otherDB;
                    firstTarget = _otherDB;
                    secondTarget = Db;
                }

                using (var mdoc = new MutableDocument("livesindb")) {
                    mdoc.SetString("name", "db");
                    mdoc.SetInt("version", 1);
                    firstSource.Save(mdoc);
                }

                var count = 0;
                while (replicator.Status.Progress.Completed == 0 ||
                       replicator.Status.Activity != ReplicatorActivityLevel.Idle) {
                    Thread.Sleep(500);
                    count++;
                    count.Should().BeLessThan(10, "because otherwise the replicator did not advance");
                }

                var previousCompleted = replicator.Status.Progress.Completed;
                firstTarget.Count.Should().Be(1);

                using (var savedDoc = secondSource.GetDocument("livesindb"))
                using (var mdoc = savedDoc.ToMutable()) {
                    mdoc.SetInt("version", 2);
                    secondSource.Save(mdoc);
                }

                count = 0;
                while (replicator.Status.Progress.Completed == previousCompleted ||
                       replicator.Status.Activity != ReplicatorActivityLevel.Idle) {
                    Thread.Sleep(500);
                    count++;
                    count.Should().BeLessThan(10, "because otherwise the replicator did not advance");
                }

                using (var savedDoc = secondTarget.GetDocument("livesindb")) {
                    savedDoc.GetInt("version").Should().Be(2);
                }

                replicator.Stop();
                while (replicator.Status.Activity != ReplicatorActivityLevel.Stopped) {
                    Thread.Sleep(100);
                }
            }
        }

        private void VerifyChange(ReplicatorStatusChangedEventArgs change, int errorCode, CouchbaseLiteErrorType domain)
        {
            var s = change.Status;
            WriteLine($"---Status: {s.Activity} ({s.Progress.Completed} / {s.Progress.Total}), lastError = {s.Error}");
            if (s.Activity == ReplicatorActivityLevel.Stopped) {
                if (errorCode != 0) {
                    s.Error.Should().NotBeNull();
                    s.Error.Should().BeAssignableTo<CouchbaseException>();
                    var error = s.Error.As<CouchbaseException>();
                    error.Error.Should().Be(errorCode);
                    if ((int)domain != 0) {
                        error.Domain.As<CouchbaseLiteErrorType>().Should().Be(domain);
                    }
                } else {
                    s.Error.Should().BeNull("because otherwise an unexpected error occurred");
                }
            }
        }

        private void RunReplication(ReplicatorConfiguration config, int expectedErrCode, CouchbaseLiteErrorType expectedErrDomain, bool reset = false,
            EventHandler<DocumentReplicationEventArgs> documentReplicated = null)
        {
            Misc.SafeSwap(ref _repl, new Replicator(config));
            _waitAssert = new WaitAssert();
            var token = _repl.AddChangeListener((sender, args) => {
                _waitAssert.RunConditionalAssert(() => {
                    VerifyChange(args, expectedErrCode, expectedErrDomain);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator)sender).Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            if (documentReplicated != null) {
                _repl.AddDocumentReplicationListener(documentReplicated);
            }

            _repl.Start(reset);
            try {
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(10));
            } catch {
                _repl.Stop();
                throw;
            } finally {
                _repl.RemoveChangeListener(token);
            }
        }

        private class MockConnectionFactory : IMessageEndpointDelegate
        {
            private readonly IMockConnectionErrorLogic _errorLogic;

            public MockConnectionFactory(IMockConnectionErrorLogic errorLogic)
            {
                _errorLogic = errorLogic;
            }

            public IMessageEndpointConnection CreateConnection(MessageEndpoint endpoint)
            {
                var retVal =
                    new MockClientConnection(endpoint) {
                        ErrorLogic = _errorLogic,
                    };
                return retVal;
            }
        }

#endif

        protected override void Dispose(bool disposing)
        {
            Exception ex = null;
            _repl?.Dispose();
            _repl = null;

            base.Dispose(disposing);
            var name = _otherDB?.Name;
            _otherDB?.Dispose();
            _otherDB = null;

            var success = Try.Condition(() => {
                try {
                    if (name != null) {
                        Database.Delete(name, Directory);
                    }
                } catch (Exception e) {
                    ex = e;
                    return false;
                }

                return true;
            }).Times(5).Delay(TimeSpan.FromSeconds(1)).WriteProgress(WriteLine).Go();

            if (!success) {
                throw ex;
            }
        }
    }
}

