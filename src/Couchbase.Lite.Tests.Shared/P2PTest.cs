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

#if COUCHBASE_ENTERPRISE    

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
using Couchbase.Lite.P2P;
using ProtocolType = Couchbase.Lite.P2P.ProtocolType;

#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
using System.Reflection;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class P2PTest : ReplicatorTestBase
    {
#if !WINDOWS_UWP
        public P2PTest(ITestOutputHelper output) : base(output)
#else
        public P2PTest()
#endif
        {
            //uncomment the code below when you need to see more detail log
            //Database.Log.Console.Level = LogLevel.Debug;
        }
        
        [Fact]
        public void TestShortP2P()
        {
            //var testNo = 1;
            foreach (var protocolType in new[] { ProtocolType.ByteStream, ProtocolType.MessageStream }) {
                Db.Delete();
                ReopenDB();
                OtherDb.Delete();
                ReopenOtherDb();

                using (var mdoc = new MutableDocument("livesindb")) {
                    mdoc.SetString("name", "db");
                    Db.Save(mdoc);
                }

                using (var mdoc = new MutableDocument("livesinotherdb")) {
                    mdoc.SetString("name", "otherdb");
                    OtherDb.Save(mdoc);
                }

                var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(OtherDb, protocolType));
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
                OtherDb.Count.Should().Be(2UL, "because it contains the original and new");
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

                using (var savedDoc = OtherDb.GetDocument("livesindb"))
                using (var mdoc = savedDoc.ToMutable()) {
                    mdoc.SetBoolean("modified", true);
                    OtherDb.Save(mdoc);
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

                using (var savedDoc = OtherDb.GetDocument("livesinotherdb")) {
                    savedDoc.GetBoolean("modified").Should()
                        .BeTrue("because the property change should come from the original DB");
                }
            }
        }

        [Fact]
        public void TestContinuousP2P()
        {
            RunTwoStepContinuous(ReplicatorType.Push, "p2ptest1");
            RunTwoStepContinuous(ReplicatorType.Pull, "p2ptest2");
            RunTwoStepContinuous(ReplicatorType.PushAndPull, "p2ptest3");
        }

        [Fact]
        public void TestP2PRecoverableFailureDuringOpen() => TestP2PError(MockConnectionLifecycleLocation.Connect, true);

        [Fact]
        public void TestP2PRecoverableFailureDuringSend() => TestP2PError(MockConnectionLifecycleLocation.Send, true);

        [Fact]
        public void TestP2PRecoverableFailureDuringReceive() => TestP2PError(MockConnectionLifecycleLocation.Receive, true);

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
            WaitAssert waitIdleAssert1 = new WaitAssert();
            WaitAssert waitStoppedAssert1 = new WaitAssert();

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(OtherDb, ProtocolType.MessageStream));
            var awaiter = new ListenerAwaiter(listener);
            var serverConnection = new MockServerConnection(listener, ProtocolType.MessageStream);
            var errorLogic = new ReconnectErrorLogic();
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", serverConnection, ProtocolType.MessageStream,
                    new MockConnectionFactory(errorLogic))) {
                Continuous = true
            };

            using (var replicator = new Replicator(config)) {
                replicator.AddChangeListener((sender, args) => {
                     waitIdleAssert1.RunConditionalAssert(() => {
                         return args.Status.Activity == ReplicatorActivityLevel.Idle;
                     });

                     waitStoppedAssert1.RunConditionalAssert(() => {
                         return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                     });
                 });
                replicator.Start();

                waitIdleAssert1.WaitForResult(TimeSpan.FromSeconds(15));
                var connection = listener.Connections;
                errorLogic.ErrorActive = true;
                listener.Close(serverConnection);
  
                waitStoppedAssert1.WaitForResult(TimeSpan.FromSeconds(15));
                awaiter.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
                awaiter.Validate();

                replicator.Status.Error.Should()
                    .NotBeNull("because closing the passive side creates an error on the active one");
            }
        }

        [Fact]
        public void TestP2PPassiveCloseAll()
        {
            var waitIdleAssert1 = new ManualResetEventSlim();
            var waitIdleAssert2 = new ManualResetEventSlim();
            var waitStoppedAssert1 = new ManualResetEventSlim();
            var waitStoppedAssert2 = new ManualResetEventSlim();

            using (var doc = new MutableDocument("test")) {
                doc.SetString("name", "Smokey");
                Db.Save(doc);
            }

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(OtherDb, ProtocolType.MessageStream));
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
                EventHandler<ReplicatorStatusChangedEventArgs> changeListener = (sender, args) =>
                {
                    if (args.Status.Activity == ReplicatorActivityLevel.Idle && args.Status.Progress.Completed ==
                        args.Status.Progress.Total) {

                        if (sender == replicator) {
                            waitIdleAssert1.Set();
                        } else {
                            waitIdleAssert2.Set();
                        }
                    } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                        if (sender == replicator) {
                            waitStoppedAssert1.Set();
                        } else {
                            waitStoppedAssert2.Set();
                        }
                    }
                };
                replicator.AddChangeListener(changeListener);
                replicator2.AddChangeListener(changeListener);
                replicator.Start();
                replicator2.Start();

                WaitHandle.WaitAll(new[] { waitIdleAssert1.WaitHandle, waitIdleAssert2.WaitHandle }, TimeSpan.FromSeconds(30))
                .Should().BeTrue();

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

                WaitHandle.WaitAll(new[] { waitStoppedAssert1.WaitHandle, waitStoppedAssert2.WaitHandle }, TimeSpan.FromSeconds(30))
                .Should().BeTrue();

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
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(OtherDb, ProtocolType.ByteStream));
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
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(OtherDb, ProtocolType.ByteStream));
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

        private ReplicatorConfiguration CreateFailureP2PConfiguration(ProtocolType protocolType, MockConnectionLifecycleLocation location, bool recoverable)
        {
            var errorLocation = TestErrorLogic.FailWhen(location);

            if (recoverable) {
                errorLocation.WithRecoverableException();
            } else {
                errorLocation.WithPermanentException();
            }

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(OtherDb, protocolType));
            var server = new MockServerConnection(listener, protocolType) {
                ErrorLogic = errorLocation
            };

            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", server, protocolType, new MockConnectionFactory(errorLocation))) {
                ReplicatorType = ReplicatorType.Push,
                Continuous = false
            };
            
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
            OtherDb.Delete();
            Db.Delete();
            ReopenDB();
            ReopenOtherDb();

            int idleCnt = 0;
            WaitAssert waitIdleAssert1 = new WaitAssert();
            WaitAssert waitIdleAssert2 = new WaitAssert();
            WaitAssert waitStoppedAssert1 = new WaitAssert();

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(OtherDb, ProtocolType.ByteStream));
            var server = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint(uid, server, ProtocolType.ByteStream, new MockConnectionFactory(null))) {
                ReplicatorType = type,
                Continuous = true
            };

            using (var replicator = new Replicator(config)) {
                var token = replicator.AddChangeListener((sender, args) => {
                    var c = args.Status.Progress.Completed;
                    var t = args.Status.Progress.Total;
                    waitIdleAssert1.RunConditionalAssert(() => {
                         return idleCnt == 0 && c==t && args.Status.Activity == ReplicatorActivityLevel.Idle;
                     });

                    waitIdleAssert2.RunConditionalAssert(() => {
                        return idleCnt == 1 && c == t && args.Status.Activity == ReplicatorActivityLevel.Idle;
                    });

                    waitStoppedAssert1.RunConditionalAssert(() => {
                         return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                     });
                 });

                replicator.Start();

                Database firstSource = null;
                Database secondSource = null;
                Database firstTarget = null;
                Database secondTarget = null;
                if (type == ReplicatorType.Push) {
                    firstSource = Db;
                    secondSource = Db;
                    firstTarget = OtherDb;
                    secondTarget = OtherDb;
                } else if (type == ReplicatorType.Pull) {
                    firstSource = OtherDb;
                    secondSource = OtherDb;
                    firstTarget = Db;
                    secondTarget = Db;
                } else {
                    firstSource = Db;
                    secondSource = OtherDb;
                    firstTarget = OtherDb;
                    secondTarget = Db;
                }

                using (var mdoc = new MutableDocument("livesindb")) {
                    mdoc.SetString("name", "db");
                    mdoc.SetInt("version", 1);
                    firstSource.Save(mdoc);
                }

                waitIdleAssert1.WaitForResult(TimeSpan.FromSeconds(15));
                idleCnt++;
                firstTarget.Count.Should().Be(1);

                using (var savedDoc = secondSource.GetDocument("livesindb"))
                using (var mdoc = savedDoc.ToMutable()) {
                    mdoc.SetInt("version", 2);
                    secondSource.Save(mdoc);
                }

                waitIdleAssert2.WaitForResult(TimeSpan.FromSeconds(15));
                idleCnt++;
                using (var savedDoc = secondTarget.GetDocument("livesindb")) {
                    savedDoc.GetInt("version").Should().Be(2);
                }

                replicator.Stop();

                waitStoppedAssert1.WaitForResult(TimeSpan.FromSeconds(30));
                replicator.RemoveChangeListener(token);
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
            var waitStoppedAssert1 = new ManualResetEventSlim();
            Misc.SafeSwap(ref _repl, new Replicator(config));

            var token = _repl.AddChangeListener((sender, args) =>
            {

                VerifyChange(args, expectedErrCode, expectedErrDomain);
                if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                      && args.Status.Progress.Completed == args.Status.Progress.Total) {
                    ((Replicator) sender).Stop();
                }

                if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    waitStoppedAssert1.Set();
                }
            });

            if (documentReplicated != null) {
                _repl.AddDocumentReplicationListener(documentReplicated);
            }

            _repl.Start(reset);

            try {
                waitStoppedAssert1.Wait(TimeSpan.FromSeconds(15));
            } catch {
                _repl.Stop();
                throw;
            } finally {
                _repl.RemoveChangeListener(token);
                waitStoppedAssert1.Dispose();
            }

            Thread.Sleep(500);
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
    }
}

#endif