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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Couchbase.Lite.P2P;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
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
        private Database _otherDB;
        private Replicator _repl;
        private WaitAssert _waitAssert;

#if !WINDOWS_UWP
        public ReplicatorTest(ITestOutputHelper output) : base(output)
#else
        public ReplicatorTest()
#endif
        {
            ReopenDB();
            _otherDB = OpenDB("otherdb");
        }

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

            using (var doc = Db.GetDocument("doc2")) {
                Db.Purge(doc);
            }
            
            Db.Count.Should().Be(0UL, "because the documents were purged");
            RunReplication(config, 0, 0);

            Db.Count.Should().Be(0UL, "because the documents were purged and the replicator is already past them");
            RunReplication(config, 0, 0, true);

            Db.Count.Should().Be(2UL, "because the replicator was reset");
        }

        [Fact]
        public void TestShortP2P()
        {
            using (var mdoc = new MutableDocument("livesindb")) {
                mdoc.SetString("name", "db");
                Db.Save(mdoc);
            }

            using (var mdoc = new MutableDocument("livesinotherdb")) {
                mdoc.SetString("name", "otherdb");
                _otherDB.Save(mdoc);
            }


            // PUSH
            var listener = new MockServerConnection(_otherDB);
            var config = new ReplicatorConfiguration(Db,
                    new MessageEndpoint("p2ptest1", listener, ProtocolType.ByteStream, new MockConnectionFactory(Db)))
            {
                ReplicatorType = ReplicatorType.Push,
                Continuous = false
            };
            RunReplication(config, 0, 0);
            _otherDB.Count.Should().Be(2UL, "because it contains the original and new");
            Db.Count.Should().Be(1UL, "because there is no pull, so the first db should only have the original");
            
            // PULL
            listener = new MockServerConnection(_otherDB);
            config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest2", listener, ProtocolType.ByteStream, new MockConnectionFactory(Db)))
            {
                ReplicatorType = ReplicatorType.Pull,
                Continuous = false
            };

            RunReplication(config, 0, 0);
            Db.Count.Should().Be(2UL, "because the pull should add the document from otherDB");

            using(var savedDoc = Db.GetDocument("livesinotherdb"))
            using (var mdoc = savedDoc.ToMutable()) {
                mdoc.SetBoolean("modified", true);
                Db.Save(mdoc);
            }

            using(var savedDoc = _otherDB.GetDocument("livesindb"))
            using (var mdoc = savedDoc.ToMutable()) {
                mdoc.SetBoolean("modified", true);
                _otherDB.Save(mdoc);
            }

            // PUSH & PULL
            listener = new MockServerConnection(_otherDB);
            config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest3", listener, ProtocolType.ByteStream, new MockConnectionFactory(Db)))
            {
                Continuous = false
            };

            RunReplication(config, 0, 0);
            Db.Count.Should().Be(2UL, "because no new documents were added");

            using (var savedDoc = Db.GetDocument("livesindb")) {
                savedDoc.GetBoolean("modified").Should()
                    .BeTrue("because the property change should have come from the other DB");
            }

            using (var savedDoc = _otherDB.GetDocument("livesinotherdb")) {
                savedDoc.GetBoolean("modified").Should()
                    .BeTrue("because the proeprty change should come from the original DB");
            }
        }

        // Not yet passing
        //[Fact]
        public void TestContinuousP2P()
        {
            RunTwoStepContinuous(ReplicatorType.Push);
            RunTwoStepContinuous(ReplicatorType.Pull);
            RunTwoStepContinuous(ReplicatorType.PushAndPull);
        }

        private void RunTwoStepContinuous(ReplicatorType type)
        {
            var listener = new MockServerConnection(_otherDB);
            var config = new ReplicatorConfiguration(Db,
                new MessageEndpoint("p2ptest1", listener, ProtocolType.ByteStream, new MockConnectionFactory(Db)))
            {
                ReplicatorType = type,
                Continuous = true
            };
            var replicator = new Replicator(config);
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

            while (replicator.Status.Progress.Completed == 0 ||
                   replicator.Status.Activity != ReplicatorActivityLevel.Idle) {
                Thread.Sleep(100);
            }

            var previousCompleted = replicator.Status.Progress.Completed;
            firstTarget.Count.Should().Be(1);

            using(var savedDoc = secondSource.GetDocument("livesindb"))
            using (var mdoc = savedDoc.ToMutable()) {
                mdoc.SetInt("version", 2);
                secondSource.Save(mdoc);
            }

            while (replicator.Status.Progress.Completed == previousCompleted ||
                   replicator.Status.Activity != ReplicatorActivityLevel.Idle) {
                Thread.Sleep(100);
            }

            using (var savedDoc = secondTarget.GetDocument("livesindb")) {
                savedDoc.GetInt("version").Should().Be(2);
            }

            replicator.Stop();
            while (replicator.Status.Activity != ReplicatorActivityLevel.Stopped) {
                Thread.Sleep(100);
            }

            replicator.Dispose();
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
                    s.Error.Should().BeAssignableTo<CouchbaseLiteException>();
                    var error = s.Error.As<CouchbaseLiteException>();
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
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(1000));
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
            private readonly Database _db;

            public MockConnectionFactory(Database db)
            {
                _db = db;
            }

            public IMessageEndpointConnection CreateConnection(MessageEndpoint endpoint)
            {
                return new MockClientConnection(_db, endpoint.Target as MockServerConnection);
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
}
