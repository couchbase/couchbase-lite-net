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
using System.Threading;
using Couchbase.Lite;

using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using Shouldly;
using Couchbase.Lite.P2P;
using ProtocolType = Couchbase.Lite.P2P.ProtocolType;

using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public sealed class P2PTest(ITestOutputHelper output) : ReplicatorTestBase(output)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _waitAssert?.Dispose();
        }

#if !SANITY_ONLY
        [Fact]
        public void TestShortP2P()
        {
            foreach (var protocolType in new[] { ProtocolType.ByteStream, ProtocolType.MessageStream }) {
                using (var mDoc = new MutableDocument("livesindb")) {
                    mDoc.SetString("name", "db");
                    DefaultCollection.Save(mDoc);
                }

                using (var mDoc = new MutableDocument("livesinotherdb")) {
                    mDoc.SetString("name", "otherdb");
                    OtherDefaultCollection.Save(mDoc);
                }

                var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration([OtherDefaultCollection], protocolType));
                var server = new MockServerConnection(listener, protocolType);
                var messageEndpoint = new MessageEndpoint($"p2ptest1", server, protocolType,
                        new MockConnectionFactory(null));

                // PUSH
                var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), messageEndpoint) {
                    ReplicatorType = ReplicatorType.Push,
                    Continuous = false
                };
                
                RunReplication(config, 0, 0);
                OtherDefaultCollection.Count.ShouldBe(2UL, "because it contains the original and new");
                DefaultCollection.Count.ShouldBe(1UL, "because there is no pull, so the first db should only have the original");

                // PULL
                config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), messageEndpoint) {
                    ReplicatorType = ReplicatorType.Pull,
                    Continuous = false
                };

                RunReplication(config, 0, 0);
                DefaultCollection.Count.ShouldBe(2UL, "because the pull should add the document from otherDB");

                using (var savedDoc = DefaultCollection.GetDocument("livesinotherdb"))
                using (var mDoc = savedDoc?.ToMutable()) {
                    mDoc.ShouldNotBeNull("because otherwise the retrieval of 'livesinotherdb' failed");
                    mDoc.SetBoolean("modified", true);
                    DefaultCollection.Save(mDoc);
                }

                using (var savedDoc = OtherDefaultCollection.GetDocument("livesindb"))
                using (var mDoc = savedDoc?.ToMutable()) {
                    mDoc.ShouldNotBeNull("because otherwise the retrieval of 'livesindb' failed");
                    mDoc.SetBoolean("modified", true);
                    OtherDefaultCollection.Save(mDoc);
                }

                // PUSH & PULL
                config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                    new MessageEndpoint($"p2ptest1", server, protocolType,  new MockConnectionFactory(null))) { Continuous = false };

                RunReplication(config, 0, 0);
                DefaultCollection.Count.ShouldBe(2UL, "because no new documents were added");

                using (var savedDoc = DefaultCollection.GetDocument("livesindb")) {
                    savedDoc.ShouldNotBeNull("because otherwise the retrieval of 'livesindb' failed");
                    savedDoc.GetBoolean("modified")
                        .ShouldBeTrue("because the property change should have come from the other DB");
                }

                using (var savedDoc = OtherDefaultCollection.GetDocument("livesinotherdb")) {
                    savedDoc.ShouldNotBeNull("because otherwise the retrieval of 'livesinotherdb' failed");
                    savedDoc.GetBoolean("modified")
                        .ShouldBeTrue("because the property change should come from the original DB");
                }

                Db.Delete();
                ReopenDB();
                OtherDb.Delete();
                OtherDb = OpenDB(OtherDb.Name);
            }
        }

        [Fact] 
        public void TestContinuousPushP2P() => RunTwoStepContinuous(ReplicatorType.Push, "p2ptest1");

        [Fact]
        public void TestContinuousPullP2P() => RunTwoStepContinuous(ReplicatorType.Pull, "p2ptest2");

        [Fact] 
        public void TestContinuousPushAndPullP2P() => RunTwoStepContinuous(ReplicatorType.PushAndPull, "p2ptest3");

        [Fact]
        public void TestP2PRecoverableFailureDuringOpen() => TestP2PError(MockConnectionLifecycleLocation.Connect, true);

        [Fact]
        public void TestP2PRecoverableFailureDuringSend() => TestP2PError(MockConnectionLifecycleLocation.Send, true);

        [Fact]
        public void TestP2PRecoverableFailureDuringReceive() => TestP2PError(MockConnectionLifecycleLocation.Receive, true);
#endif

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
                DefaultCollection.Save(mdoc);
            }

            var config = CreateFailureP2PConfiguration(ProtocolType.ByteStream, MockConnectionLifecycleLocation.Close,
                false);
            RunReplication(config, (int)CouchbaseLiteError.WebSocketUserPermanent, CouchbaseLiteErrorType.CouchbaseLite);
            config = CreateFailureP2PConfiguration(ProtocolType.MessageStream, MockConnectionLifecycleLocation.Close,
                false);
            RunReplication(config, (int)CouchbaseLiteError.WebSocketUserPermanent, CouchbaseLiteErrorType.CouchbaseLite, true);
        }

#if !SANITY_ONLY
        [Fact]
        public void TestP2PPassiveClose()
        {
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration([OtherDefaultCollection], ProtocolType.MessageStream));
            var awaiter = new ListenerAwaiter(listener);
            var serverConnection = new MockServerConnection(listener, ProtocolType.MessageStream);
            var errorLogic = new ReconnectErrorLogic();
            var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint("p2ptest1", serverConnection, ProtocolType.MessageStream, new MockConnectionFactory(errorLogic))) {
                Continuous = true
            };

            using var replicator = new Replicator(config);
            replicator.Start();

            var count = 0;
            while (count++ < 10 && replicator.Status.Activity != ReplicatorActivityLevel.Idle) {
                Thread.Sleep(500);
                count.ShouldBeLessThan(10, "because otherwise the replicator never went idle");
            }
            
            errorLogic.ErrorActive = true;
            listener.Close(serverConnection);
            count = 0;
            while (count++ < 10 && replicator.Status.Activity != ReplicatorActivityLevel.Stopped) {
                Thread.Sleep(500);
                count.ShouldBeLessThan(10, "because otherwise the replicator never stopped");
            }

            awaiter.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            awaiter.Validate();

            replicator.Status.Error
                .ShouldNotBeNull("because closing the passive side creates an error on the active one");
        }
#endif

        [Fact]
        public void TestP2PPassiveCloseAll()
        {
            using (var doc = new MutableDocument("test")) {
                doc.SetString("name", "Smokey");
                DefaultCollection.Save(doc);
            }

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration([OtherDefaultCollection], ProtocolType.MessageStream));
            var serverConnection1 = new MockServerConnection(listener, ProtocolType.MessageStream);
            var serverConnection2 = new MockServerConnection(listener, ProtocolType.MessageStream);
            var closeWait1 = new ManualResetEventSlim();
            var closeWait2 = new ManualResetEventSlim();
            var errorLogic = new ReconnectErrorLogic();
            var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint("p2ptest1", serverConnection1, ProtocolType.MessageStream, new MockConnectionFactory(errorLogic)))
            {
                Continuous = true
            };

            var config2 = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint("p2ptest2", serverConnection2, ProtocolType.MessageStream, new MockConnectionFactory(errorLogic)))
            {
                Continuous = true
            };

            using (var replicator = new Replicator(config))
            using (var replicator2 = new Replicator(config2))
            {
                EventHandler<ReplicatorStatusChangedEventArgs> changeListener = (sender, args) =>
                {
                    if (args.Status.Activity != ReplicatorActivityLevel.Stopped) {
                        return;
                    }
                    
                    // ReSharper disable AccessToDisposedClosure
                    if (sender == replicator) {
                        closeWait1.Set();
                    } else {
                        closeWait2.Set();
                    }
                    // ReSharper restore AccessToDisposedClosure
                };

                replicator.AddChangeListener(changeListener);
                replicator2.AddChangeListener(changeListener);
                replicator.Start();
                replicator2.Start();

                errorLogic.ErrorActive = true;
                listener.CloseAll();


                WaitAssert.WaitAll([closeWait1, closeWait2], TimeSpan.FromSeconds(20)).ShouldBeTrue();

                replicator.Status.Error
                    .ShouldNotBeNull("because closing the passive side creates an error on the active one");
                replicator2.Status.Error
                    .ShouldNotBeNull("because closing the passive side creates an error on the active one");
            }

            closeWait1.Dispose();
            closeWait2.Dispose();
        }

        [Fact]
        public void TestP2PChangeListener()
        {
            var statuses = new List<ReplicatorActivityLevel>();
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration([OtherDefaultCollection], ProtocolType.ByteStream));
            var awaiter = new ListenerAwaiter(listener);
            var serverConnection = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint( "p2ptest1", serverConnection, ProtocolType.ByteStream, new MockConnectionFactory(null))) {
                Continuous = true
            };
            
            listener.AddChangeListener((_, args) => {
                statuses.Add(args.Status.Activity);
            });
            
            RunReplication(config, 0, 0);
            awaiter.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            awaiter.Validate();
            statuses.Count
                .ShouldBeGreaterThan(1, "because otherwise there were no callbacks to the change listener");
        }

        [Fact]
        public void TestRemoveChangeListener()
        {
            var statuses = new List<ReplicatorActivityLevel>();
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration([OtherDefaultCollection], ProtocolType.ByteStream));
            var awaiter = new ListenerAwaiter(listener);
            var serverConnection = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint("p2ptest1", serverConnection, ProtocolType.ByteStream, new MockConnectionFactory(null))) {
                Continuous = true
            };

            var token = listener.AddChangeListener((_, args) => {
                statuses.Add(args.Status.Activity);
            });
            
            token.Remove();
            RunReplication(config, 0, 0);
            awaiter.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            awaiter.Validate();

            statuses.Count.ShouldBe(0);
        }

        [Fact]
        public void TestCollectionsSingleShotPushPullReplication() => CollectionPushPullReplication(continuous: false);

        [Fact]
        public void TestCollectionsContinuousPushPullReplication() => CollectionPushPullReplication(continuous: true);

        [Fact]
        public void TestMismatchedCollectionReplication()
        {
            using var colAOtherDb = OtherDb.CreateCollection("colA", "scopeA");
            using var colADb = Db.CreateCollection("colB", "scopeA");
            var collsOtherDb = new List<Collection>
            {
                colAOtherDb
            };
            
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(collsOtherDb, ProtocolType.ByteStream));
            var server = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint("p2pCollsTests", server, ProtocolType.ByteStream, new MockConnectionFactory(null)))
            {
                ReplicatorType = ReplicatorType.PushAndPull,
                Continuous = false
            };

            RunReplication(config, (int)CouchbaseLiteError.HTTPNotFound, CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact]
        public void TestCreateListenerConfigWithEmptyCollection()
        {
            void BadAct()
            {
                _ = new MessageEndpointListenerConfiguration(new List<Collection>(), ProtocolType.ByteStream);
            }

            var ex = Should.Throw<CouchbaseLiteException>(BadAct);
            ex.Message.ShouldBe("The given collections must not be null or empty.");
        }

        private void CollectionPushPullReplication(bool continuous)
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

            var collsOtherDb = new List<Collection>
            {
                colAOtherDb
            };
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration(collsOtherDb, ProtocolType.ByteStream));
            var server = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint("p2pCollsTests", server, ProtocolType.ByteStream, new MockConnectionFactory(null)))
            {
                ReplicatorType = ReplicatorType.PushAndPull,
                Continuous = continuous
            };

            RunReplication(config, 0, 0);

            // Check docs are replicated between collections colADb & colAOtherDb
            colAOtherDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colADb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colADb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
        }

        private ReplicatorConfiguration CreateFailureP2PConfiguration(ProtocolType protocolType, MockConnectionLifecycleLocation location, bool recoverable)
        {
            var errorLocation = TestErrorLogic.FailWhen(location);

            if (recoverable) {
                errorLocation.WithRecoverableException();
            } else {
                errorLocation.WithPermanentException();
            }

            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration([OtherDefaultCollection], protocolType));
            var server = new MockServerConnection(listener, protocolType) {
                ErrorLogic = errorLocation
            };

            var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint("p2ptest1", server, protocolType, new MockConnectionFactory(errorLocation))) {
                ReplicatorType = ReplicatorType.Push,
                Continuous = false,
                MaxAttempts = 2,
                MaxAttemptsWaitTime = TimeSpan.FromMinutes(10)
            };
            
            return config;
        }

        private void TestP2PError(MockConnectionLifecycleLocation location, bool recoverable)
        {
            using (var mdoc = new MutableDocument("livesindb")) {
                mdoc.SetString("name", "db");
                DefaultCollection.Save(mdoc);
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
            using var otherDb1 = OpenDB(OtherDb.Name);
            using var db1 = OpenDB(Db.Name);
            var listener = new MessageEndpointListener(new MessageEndpointListenerConfiguration([otherDb1.GetDefaultCollection()], ProtocolType.ByteStream));
            var server = new MockServerConnection(listener, ProtocolType.ByteStream);
            var config = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(DefaultCollection), 
                new MessageEndpoint(uid, server, ProtocolType.ByteStream, new MockConnectionFactory(null))) {
                ReplicatorType = type,
                Continuous = true
            };

            using var replicator = new Replicator(config);
            Collection? firstSource;
            Collection? secondSource;
            Collection? firstTarget;
            Collection? secondTarget;
            
            switch (type) {
                case ReplicatorType.Push:
                    firstSource = db1.GetDefaultCollection();
                    secondSource = db1.GetDefaultCollection();
                    firstTarget = otherDb1.GetDefaultCollection();
                    secondTarget = otherDb1.GetDefaultCollection();
                    break;
                case ReplicatorType.Pull:
                    firstSource = otherDb1.GetDefaultCollection();
                    secondSource = otherDb1.GetDefaultCollection();
                    firstTarget = db1.GetDefaultCollection();
                    secondTarget = db1.GetDefaultCollection();
                    break;
                case ReplicatorType.PushAndPull:
                default:
                    firstSource = db1.GetDefaultCollection();
                    secondSource = otherDb1.GetDefaultCollection();
                    firstTarget = otherDb1.GetDefaultCollection();
                    secondTarget = db1.GetDefaultCollection();
                    break;
            }

            replicator.Start();

            using (var mdoc = new MutableDocument("livesindb")) {
                mdoc.SetString("name", "db");
                mdoc.SetInt("version", 1);
                firstSource.Save(mdoc);
            }

            var count = 0;
            if (type != ReplicatorType.Push) {
                while (true) {
                    count++;
                    Thread.Sleep(1000);
                    if (replicator.Status.Progress.Completed > 0 &&
                        replicator.Status.Activity == ReplicatorActivityLevel.Idle)
                        break;
                }
                        
                count.ShouldBeLessThan(10, "because otherwise the replicator did not advance");
            } else { //when both doc updates happens on local side with push only, replicator.Status.Progress value wipe out too fast, so skip while loop
                Thread.Sleep(1000);
            }

            var previousCompleted = replicator.Status.Progress.Completed;
            firstTarget.Count.ShouldBe(1UL);

            using (var savedDoc = secondSource.GetDocument("livesindb"))
            using (var mdoc = savedDoc?.ToMutable()) {
                mdoc.ShouldNotBeNull("because otherwise the retrieval of 'livesindb' failed");
                mdoc.SetInt("version", 2);
                secondSource.Save(mdoc);
            }

            count = 0;
            if (type != ReplicatorType.Push) {
                while (true) {
                    count++;
                    Thread.Sleep(1000);
                    if (replicator.Status.Progress.Completed > previousCompleted &&
                        replicator.Status.Activity == ReplicatorActivityLevel.Idle)
                        break;
                }
                        
                count.ShouldBeLessThan(10, "because otherwise the replicator did not advance");
            } else { //when both doc updates happens on local side with push only, replicator.Status.Progress value wipe out too fast, so skip while loop
                Thread.Sleep(1000);
            }
                    
            using (var savedDoc = secondTarget.GetDocument("livesindb")) {
                savedDoc.ShouldNotBeNull("because otherwise the retrieval of 'livesindb' failed");
                savedDoc.GetInt("version").ShouldBe(2);
            }

            replicator.Stop();
            Thread.Sleep(100);
            while (true) {
                if (replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                    break;
            }
        }

        private void RunReplication(ReplicatorConfiguration config, int expectedErrCode, CouchbaseLiteErrorType expectedErrDomain, bool reset = false,
            EventHandler<DocumentReplicationEventArgs>? documentReplicated = null)
        {
            Misc.SafeSwap(ref _repl, new Replicator(config));
            _waitAssert = new WaitAssert();
            var token = _repl!.AddChangeListener((sender, args) => {
                _waitAssert.RunConditionalAssert(() => {
                    VerifyChange(args, expectedErrCode, expectedErrDomain);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator?)sender)!.Stop();
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
                token.Remove();
            }
        }

        private class MockConnectionFactory(IMockConnectionErrorLogic? errorLogic) : IMessageEndpointDelegate
        {
            public IMessageEndpointConnection CreateConnection(MessageEndpoint endpoint)
            {
                var retVal =
                    new MockClientConnection(endpoint) {
                        ErrorLogic = errorLogic,
                    };
                return retVal;
            }
        }
    }
}

#endif