//
//  ReplicationTest.cs
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

using FluentAssertions;
using LiteCore;
using LiteCore.Interop;

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
    public sealed class ReplicatorTest : TestCase
    {
        private static int Counter;
        private Database _otherDB;
        private Replicator _repl;
        private WaitAssert _waitAssert;
        #if COUCHBASE_ENTERPRISE
        private IMockConnectionErrorLogic _p2PErrorLogic;
        #endif

#if !WINDOWS_UWP
        public ReplicatorTest(ITestOutputHelper output) : base(output)
#else
        public ReplicatorTest()
#endif
        {
            ReopenDB();
            var nextCounter = Interlocked.Increment(ref Counter);
            Database.Delete($"otherdb{nextCounter}", Directory);
            _otherDB = OpenDB($"otherdb{nextCounter}");
            //uncomment the code below when you need to see more detail log
            //Database.SetLogLevel(LogDomain.Replicator, LogLevel.Verbose);
        }
#if !WINDOWS_UWP
        [Fact]
        public async Task TestReplicatorStopsWhenEndpointInvalid()
        {
            // If this IP address happens to exist, then change it.  It needs to be an address that does not
            // exist on the LAN
            var targetEndpoint = new URLEndpoint(new Uri("ws://192.168.0.11:4984/app"));
            var config = new ReplicatorConfiguration(Db, targetEndpoint);
            using (var repl = new Replicator(config))
            {
                repl.Start();
                var count = 0;
                while (count++ <= 35 && repl.Status.Activity != ReplicatorActivityLevel.Stopped)
                {
                    WriteLine($"Replication status still {repl.Status.Activity}, waiting for stopped...");
                    await Task.Delay(500);
                }

                count.Should().BeLessThan(35, "because otherwise the replicator never stopped");
            }
        }
#endif
#if COUCHBASE_ENTERPRISE
        [Fact]
        public void TestReadOnlyConfiguration()
        {
            var config = CreateConfig(true, false, false);
            using (var repl = new Replicator(config)) {
                config = repl.Config;
                config.Invoking(c => c.ReplicatorType = ReplicatorType.PushAndPull)
                    .ShouldThrow<InvalidOperationException>("because the configuration from a replicator should be read only");
            }
        }

        [Fact]
        public void TestEmptyPush()
        {
            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);
        }

        [Fact]
        public void TestPushDoc()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
                Db.Count.Should().Be(1UL);

                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);

            _otherDB.Count.Should().Be(2UL);
            using (var savedDoc1 = _otherDB.GetDocument("doc1")) {
                savedDoc1.GetString("name").Should().Be("Tiger");
            }
        }

        [Fact]
        public void TestPushDocContinuous()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
                Db.Count.Should().Be(1UL);

                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(true, false, true);
            config.CheckpointInterval = TimeSpan.FromSeconds(1);
            RunReplication(config, 0, 0);

            _otherDB.Count.Should().Be(2UL);
            using (var savedDoc1 = _otherDB.GetDocument("doc1")) {
                savedDoc1.GetString("name").Should().Be("Tiger");
            }
        }

        [ForIssue("couchbase-lite-core/156")]
        [Fact]
        public void TestPullDoc()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
                Db.Count.Should().Be(1, "because only one document was saved so far");
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            RunReplication(config, 0, 0);

            Db.Count.Should().Be(2, "because the replicator should have pulled doc2 from the other DB");
            using (var doc2 = Db.GetDocument("doc2")) {
                doc2.GetString("name").Should().Be("Cat");
            }
        }

        [ForIssue("couchbase-lite-core/156")]
        [Fact]
        public void TestPullDocContinuous()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
                Db.Count.Should().Be(1, "because only one document was saved so far");
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(false, true, true);
            config.CheckpointInterval = TimeSpan.FromSeconds(1);
            RunReplication(config, 0, 0);

            Db.Count.Should().Be(2, "because the replicator should have pulled doc2 from the other DB");
            using (var doc2 = Db.GetDocument("doc2")) {
                doc2.GetString("name").Should().Be("Cat");
            }
        }

        [Fact]
        public void TestDocIDFilter()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("species", "Tiger");
            Db.Save(doc1);
            doc1.SetString("name", "Hobbes");
            Db.Save(doc1);

             var doc2 = new MutableDocument("doc2");
            doc2.SetString("species", "Tiger");
            Db.Save(doc2);
            doc2.SetString("pattern", "striped");
            Db.Save(doc2);

            var doc3 = new MutableDocument("doc3");
            doc3.SetString("species", "Tiger");
             _otherDB.Save(doc3);
            doc3.SetString("name", "Hobbes");
            _otherDB.Save(doc3);

            var doc4 = new MutableDocument("doc4");
            doc4.SetString("species", "Tiger");
            _otherDB.Save(doc4);
            doc4.SetString("pattern", "striped");
            _otherDB.Save(doc4);

            var config = CreateConfig(true, true, false);
            config.DocumentIDs = new[] {"doc1", "doc3"};
            RunReplication(config, 0, 0);
            Db.Count.Should().Be(3, "because only one document should have been pulled");
            Db.GetDocument("doc3").Should().NotBeNull();
            _otherDB.Count.Should().Be(3, "because only one document should have been pushed");
            _otherDB.GetDocument("doc1").Should().NotBeNull();
        }

        [Fact]
        public async Task TestReplicatorStopWhenClosed()
        {
            var config = CreateConfig(true, true, true);
            using (var repl = new Replicator(config)) {
                repl.Start();
                while (repl.Status.Activity != ReplicatorActivityLevel.Idle) {
                    WriteLine($"Replication status still {repl.Status.Activity}, waiting for idle...");
                    await Task.Delay(500);
                }

                this.Invoking(x => ReopenDB())
                    .ShouldThrow<CouchbaseLiteException>(
                        "because the database cannot be closed while replication is running");

                repl.Stop();
                while (repl.Status.Activity != ReplicatorActivityLevel.Stopped) {
                    WriteLine($"Replication status still {repl.Status.Activity}, waiting for stopped...");
                    await Task.Delay(500);
                }
            }
        }
        
        [Fact]
        public void TestStopContinuousReplicator()
        {
            var config = CreateConfig(true, false, true);
            using (var r = new Replicator(config)) {
                var stopWhen = new[]
                {
                    ReplicatorActivityLevel.Connecting, ReplicatorActivityLevel.Busy,
                    ReplicatorActivityLevel.Idle, ReplicatorActivityLevel.Idle
                };

                foreach (var when in stopWhen) {
                    var stopped = 0;
                    var waitAssert = new WaitAssert();
                    var token = r.AddChangeListener((sender, args) =>
                    {
                        waitAssert.RunConditionalAssert(() =>
                        {
                            VerifyChange(args, 0, 0);

                            // On Windows, at least, sometimes the connection is so fast that Connecting never gets called
                            if ((args.Status.Activity == when ||
                                (when == ReplicatorActivityLevel.Connecting && args.Status.Activity > when))
                                && Interlocked.Exchange(ref stopped, 1) == 0) {
                                WriteLine("***** Stop Replicator *****");
                                ((Replicator) sender).Stop();
                            }

                            if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                                WriteLine("Stopped!");
                            }

                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    WriteLine("***** Start Replicator *****");
                    r.Start();
                    try {
                        waitAssert.WaitForResult(TimeSpan.FromSeconds(5));
                    } finally {
                        r.RemoveChangeListener(token);
                    }

                    Task.Delay(100).Wait();
                }
            }
        }

        [Fact]
        public void TestDocumentIDs()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("species", "Tiger");
                doc1.SetString("name", "Hobbes");
                Db.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("species", "Tiger");
                doc2.SetString("pattern", "striped");
                Db.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            config.DocumentIDs = new[] { "doc1" };
            RunReplication(config, 0, 0);

            _otherDB.Count.Should().Be(1UL);
            using (var doc1 = _otherDB.GetDocument("doc1")) {
                doc1.Should().NotBeNull();
                doc1.GetString("species").Should().Be("Tiger");
                doc1.GetString("name").Should().Be("Hobbes");
            }
        }

        [Fact]
        [ForIssue("couchbase-lite-core/447")]
        public void TestResetCheckpoint()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("species", "Tiger");
                doc1.SetString("name", "Hobbes");
                Db.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("species", "Tiger");
                doc2.SetString("pattern", "striped");
                Db.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);
            config = CreateConfig(false, true, false);
            RunReplication(config, 0, 0);

            _otherDB.Count.Should().Be(2UL);
            using (var doc = Db.GetDocument("doc1")) {
                Db.Purge(doc);
            }

            Db.Purge("doc2");
               
            Db.Count.Should().Be(0UL, "because the documents were purged");
            RunReplication(config, 0, 0);

            Db.Count.Should().Be(0UL, "because the documents were purged and the replicator is already past them");
            RunReplication(config, 0, 0, true);

            Db.Count.Should().Be(2UL, "because the replicator was reset");
        }

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
                var replicationDict = _otherDB.Replications;

                // PUSH
                var config = new ReplicatorConfiguration(Db, messageendpoint)
                {
                    ReplicatorType = ReplicatorType.Push,
                    Continuous = false
                };
                RunReplication(config, 0, 0);
                _otherDB.Count.Should().Be(2UL, "because it contains the original and new");
                Db.Count.Should().Be(1UL, "because there is no pull, so the first db should only have the original");

                // PULL
                config = new ReplicatorConfiguration(Db, messageendpoint)
                {
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
                            new MockConnectionFactory(null)))
                    { Continuous = false };

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
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, ProtocolType.MessageStream));
            var awaiter = new ListenerAwaiter(listener);
            var serverConnection = new MockServerConnection(listener, ProtocolType.MessageStream);
            var errorLogic = new ReconnectErrorLogic();
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", serverConnection, ProtocolType.MessageStream,
                    new MockConnectionFactory(errorLogic)))
            {
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
                    new MockConnectionFactory(errorLogic)))
            {
                Continuous = true
            };

            var config2 = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest2", serverConnection2, ProtocolType.MessageStream,
                    new MockConnectionFactory(errorLogic)))
            {
                Continuous = true
            };

            using(var replicator = new Replicator(config))
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
                listener.AddChangeListener((sender, args) =>
                {
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
                    new MockConnectionFactory(null)))
            {
                Continuous = true
            };
            listener.AddChangeListener((sender, args) =>
            {
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
                    new MockConnectionFactory(null)))
            {
                Continuous = true
            };
            var token = listener.AddChangeListener((sender, args) =>
            {
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

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(_otherDB, protocolType));
            var server = new MockServerConnection(listener, protocolType)
            {
                ErrorLogic = errorLocation
            };
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", server, protocolType, new MockConnectionFactory(errorLocation)))
            {
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
                new MessageEndpoint(uid, server, ProtocolType.ByteStream, new MockConnectionFactory(null)))
            {
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

#endif

        // The below tests are disabled because they require orchestration and should be moved
        // to the functional test suite
#if HAVE_SG
        [Fact] 
#endif
        public void TestAuthenticationFailure()
        {
            var config = CreateConfig(false, true, false, new URLEndpoint(new Uri("ws://localhost/seekrit")));
            _repl = new Replicator(config);
            RunReplication(config, (int)CouchbaseLiteError.HTTPAuthRequired, CouchbaseLiteErrorType.CouchbaseLite);
        }

#if HAVE_SG
        [Fact] 
#endif
        public void TestAuthenticatedPull()
        {
            var config = CreateConfig(false, true, false, new URLEndpoint(new Uri("ws://localhost/seekrit")));
            config.Authenticator = new SessionAuthenticator("78376efd8cc74dadfc395f4049a115b7cd0ef5e3");
            RunReplication(config, 0, 0);
        }

#if HAVE_SG
        [Fact]
#endif
        public void TestSelfSignedSSLFailure()
        {
            var config = CreateConfig(false, true, false, new URLEndpoint(new Uri("wss://localhost/db")));
            RunReplication(config, (int)CouchbaseLiteError.TLSCertUntrusted, CouchbaseLiteErrorType.CouchbaseLite);
        }

#if HAVE_SG
        [Fact]
#endif
        public async Task TestSelfSignedSSLPinned()
        {
            var config = CreateConfig(false, true, false,  new URLEndpoint(new Uri("wss://localhost/db")));
#if WINDOWS_UWP
            var installedLocation = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var file = await installedLocation.GetFileAsync("Assets\\localhost-wrong.cert");
            var bytes = File.ReadAllBytes(file.Path);
            config.PinnedServerCertificate = new X509Certificate2(bytes);
#else
            config.PinnedServerCertificate = new X509Certificate2("localhost-wrong.cert");
#endif
            RunReplication(config, 0, 0);
        }

#if HAVE_SG
        [Fact] 
#endif
        public void TestChannelPull()
        {
            _otherDB.Count.Should().Be(0);
            Db.InBatch(() =>
            {
                for (int i = 0; i < 5; i++) {
                    using (var doc = new MutableDocument($"doc-{i}")) {
                        doc["foo"].Value = "bar";
                        Db.Save(doc);
                    }
                }

                for (int i = 0; i < 10; i++) {
                    using (var doc = new MutableDocument($"doc-{i+5}")) {
                        doc["channels"].Value = "my_channel";
                        Db.Save(doc);
                    }
                }
            });

            
            var config = CreateConfig(true, false, false,  new URLEndpoint(new Uri("ws://localhost/db")));
            RunReplication(config, 0, 0);

            config = new ReplicatorConfiguration(_otherDB, new URLEndpoint(new Uri("ws://localhost/db")));
            ModifyConfig(config, false, true, false);
            config.Channels = new[] {"my_channel"};
            RunReplication(config, 0, 0);
            _otherDB.Count.Should().Be(10, "because 10 documents should be in the given channel");
        }

#if COUCHBASE_ENTERPRISE
        private ReplicatorConfiguration CreateConfig(bool push, bool pull, bool continuous)
        {
            var target = _otherDB;
            return CreateConfig(push, pull, continuous, target);
        }
#endif

        private ReplicatorConfiguration CreateConfig(bool push, bool pull, bool continuous, URLEndpoint endpoint)
        {
            var retVal = new ReplicatorConfiguration(Db, endpoint);
            return ModifyConfig(retVal, push, pull, continuous);
        }

#if COUCHBASE_ENTERPRISE
        private ReplicatorConfiguration CreateConfig(bool push, bool pull, bool continuous, Database target)
        {
            var retVal = new ReplicatorConfiguration(Db, new DatabaseEndpoint(target));
            return ModifyConfig(retVal, push, pull, continuous);
        }
#endif

        private ReplicatorConfiguration ModifyConfig(ReplicatorConfiguration config, bool push, bool pull, bool continuous)
        {
            var type = (ReplicatorType)0;
            if (push) {
                type |= ReplicatorType.Push;
            }

            if (pull) {
                type |= ReplicatorType.Pull;
            }

            config.ReplicatorType = type;
            config.Continuous = continuous;
            return config;
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
                    if ((int) domain != 0) {
                        error.Domain.As<CouchbaseLiteErrorType>().Should().Be(domain);
                    }
                } else {
                    s.Error.Should().BeNull("because otherwise an unexpected error occurred");
                }
            }
        }

        private void RunReplication(ReplicatorConfiguration config, int expectedErrCode, CouchbaseLiteErrorType expectedErrDomain, bool reset = false)
        {
            Misc.SafeSwap(ref _repl, new Replicator(config));
            _waitAssert = new WaitAssert();
            var token = _repl.AddChangeListener((sender, args) =>
            {
                _waitAssert.RunConditionalAssert(() =>
                {
                    VerifyChange(args, expectedErrCode, expectedErrDomain);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator) sender).Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            if (reset) {
                _repl.ResetCheckpoint();
            }
            
            _repl.Start();
            try {
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(10));
            } catch {
                _repl.Stop();
                throw;
            } finally {
                _repl.RemoveChangeListener(token);
            }
        }

#if COUCHBASE_ENTERPRISE

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
                    new MockClientConnection(endpoint)
                    {
                        ErrorLogic = _errorLogic,
                    };
                return retVal;
            }
        }

#endif

        protected override void Dispose(bool disposing)
        {
            _repl?.Dispose();
            _repl = null;

            base.Dispose(disposing);

            _otherDB.Delete();
            _otherDB = null;
        }
    }

#if COUCHBASE_ENTERPRISE
    public class TestErrorLogic : IMockConnectionErrorLogic
    {
        private readonly MockConnectionLifecycleLocation _locations;
        private MessagingException _exception;
        private int _current, _total;

        private TestErrorLogic(MockConnectionLifecycleLocation locations)
        {
            _locations = locations;
        }

        public static TestErrorLogic FailWhen(MockConnectionLifecycleLocation locations)
        {
            return new TestErrorLogic(locations);
        }

        public TestErrorLogic WithRecoverableException(int count = 1)
        {
            _exception = new MessagingException("Test recoverable exception",
                new SocketException((int) SocketError.ConnectionReset), true);
            _total = count;
            return this;
        }

        public TestErrorLogic WithPermanentException()
        {
            _exception = new MessagingException("Test permanent exception",
                new SocketException((int) SocketError.AccessDenied), false);
            _total = Int32.MaxValue;
            return this;
        }

        public bool ShouldClose(MockConnectionLifecycleLocation location)
        {
            return _current < _total && _locations.HasFlag(location);
        }

        public MessagingException CreateException()
        {
            _current++;
            return _exception;
        }
    }

    public class ReconnectErrorLogic : IMockConnectionErrorLogic
    {
        public bool ErrorActive { get; set; }

        public bool ShouldClose(MockConnectionLifecycleLocation location)
        {
            return ErrorActive;
        }

        public MessagingException CreateException()
        {
            return new MessagingException("Server no longer listening", null, false);
        }
    }

    public class ListenerAwaiter
    {
        private readonly ListenerToken _token;
        private readonly ManualResetEventSlim _mre = new ManualResetEventSlim();
        private readonly List<Exception> _exceptions = new List<Exception>();
        private readonly MessageEndpointListener _listener;

        public WaitHandle WaitHandle => _mre.WaitHandle;

        public ListenerAwaiter(MessageEndpointListener listener)
        {
            _token = listener.AddChangeListener(CheckForStopped);
            _listener = listener;
        }

        public void Validate()
        {
            _mre.Dispose();
            _exceptions.Should().BeEmpty("because otherwise an unexpected error occurred");
        }

        private void CheckForStopped(object sender, MessageEndpointListenerChangedEventArgs e)
        {
            if (e.Status.Error != null) {
                _exceptions.Add(e.Status.Error);
            }

            if (e.Status.Activity == ReplicatorActivityLevel.Stopped) {
                _listener.RemoveChangeListener(_token);
                _mre.Set();
            }
        }
    }
#endif
}

