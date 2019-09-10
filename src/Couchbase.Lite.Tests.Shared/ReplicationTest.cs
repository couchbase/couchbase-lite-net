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

using Newtonsoft.Json;

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
    public sealed class ReplicatorTest : TestCase
    {
        private static int Counter;
        private Database _otherDB;
        private Replicator _repl;
        private WaitAssert _waitAssert;
        private bool _isFilteredCallback;
        private List<DocumentReplicationEventArgs> _replicationEvents = new List<DocumentReplicationEventArgs>();

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
            using (var repl = new Replicator(config)) {
                repl.Start();
                var count = 0;
                Thread.Sleep(TimeSpan.FromSeconds(51)); // The combined amount of time this should take to stop
                while (count++ <= 10 && repl.Status.Activity != ReplicatorActivityLevel.Stopped) {
                    WriteLine($"Replication status still {repl.Status.Activity}, waiting for stopped...");
                    await Task.Delay(500);
                }

                count.Should().BeLessThan(10, "because otherwise the replicator never stopped");
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
                    .Should().Throw<InvalidOperationException>("because the configuration from a replicator should be read only");
            }
        }

        [Fact]
        public void TestEmptyPush()
        {
            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);
        }

        [Fact]
        public void TestPushDocWithFilterOneShot() => TestPushDocWithFilter(false);

        [Fact]
        public void TestPushDocWithFilterContinuous() => TestPushDocWithFilter(true);

        [Fact]
        public void TestPushPullKeepsFilter()
        {
            var config = CreateConfig(true, true, false);
            config.PullFilter = _replicator__filterCallback;
            config.PushFilter = _replicator__filterCallback;

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "donotpass");
                Db.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "donotpass");
                _otherDB.Save(doc2);
            }

            for (int i = 0; i < 2; i++) {
                RunReplication(config, 0, 0);
                Db.Count.Should().Be(1, "because the pull should have rejected the other document");
                _otherDB.Count.Should().Be(1, "because the push should have rejected the local document");
            }
        }

        [Fact]
        public void TestPushDeletedDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("pass")) {
                doc1.SetString("name", "pass");
                Db.Save(doc1);

                doc2.SetString("name", "pass");
                Db.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            config.PushFilter = _replicator__filterCallback;
            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            _otherDB.GetDocument("doc1").Should().NotBeNull("because doc1 passes the filter");
            _otherDB.GetDocument("pass").Should().NotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = Db.GetDocument("doc1"))
            using (var doc2 = Db.GetDocument("pass")) {
                Db.Delete(doc1);
                Db.Delete(doc2);
            }

            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            _otherDB.GetDocument("doc1").Should().NotBeNull("because doc1's deletion should be rejected");
            _otherDB.GetDocument("pass").Should().BeNull("because the next document's deletion is not rejected");
            _isFilteredCallback = false;
        }

        [Fact]
        public void TestBlobAccessInFilter()
        {
            var content1 = new byte[] { 1, 2, 3 };
            var content2 = new byte[] { 4, 5, 6 };
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                var mutableDictionary = new MutableDictionaryObject();
                mutableDictionary.SetBlob("inner_blob", new Blob("text/plaintext", content1));
                doc1.SetDictionary("outer_dict", mutableDictionary);

                var mutableArray = new MutableArrayObject();
                mutableArray.AddBlob(new Blob("text/plaintext", content2));
                doc2.SetArray("outer_arr", mutableArray);
                Db.Save(doc1);
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(true, true, false);
            var exceptions = new List<Exception>();
            config.PullFilter = (doc, isPush) =>
            {
                try {
                    var nestedBlob = doc.GetArray("outer_arr")?.GetBlob(0);
                    nestedBlob.Should().NotBeNull("because the actual blob object should be intact");
                    var gotContent = nestedBlob.Content;
                    gotContent.Should().BeNull("because the blob is not yet available");
                } catch (Exception e) {
                    exceptions.Add(e);
                }

                return true;
            };

            config.PushFilter = (doc, isPush) =>
            {
                try {
                    var gotContent = doc.GetDictionary("outer_dict")?.GetBlob("inner_blob")?.Content;
                    gotContent.Should().NotBeNull("because the nested blob should be intact in the push");
                    gotContent.Should().ContainInOrder(content1, "because the nested blob should be intact in the push");
                } catch (Exception e) {
                    exceptions.Add(e);
                }

                return true;
            };
            RunReplication(config, 0, 0);
            exceptions.Should().BeEmpty("because there should be no errors");
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
            _isFilteredCallback.Should().BeFalse();

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

        [Fact]
        public void TestPullDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "donotpass");
                _otherDB.Save(doc1);

                doc2.SetString("name", "pass");
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            config.PullFilter = _replicator__filterCallback;
            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            Db.GetDocument("doc1").Should().BeNull("because doc1 is filtered out in the callback");
            Db.GetDocument("doc2").Should().NotBeNull("because doc2 is filtered in in the callback");
            _isFilteredCallback = false;
        }

        [Fact]
        public void TestPullDeletedDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("pass")) {
                doc1.SetString("name", "pass");
                _otherDB.Save(doc1);

                doc2.SetString("name", "pass");
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            config.PullFilter = _replicator__filterCallback;
            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            Db.GetDocument("doc1").Should().NotBeNull("because doc1 passes the filter");
            Db.GetDocument("pass").Should().NotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = _otherDB.GetDocument("doc1"))
            using (var doc2 = _otherDB.GetDocument("pass")) {
                _otherDB.Delete(doc1);
                _otherDB.Delete(doc2);
            }

            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            Db.GetDocument("doc1").Should().NotBeNull("because doc1's deletion should be rejected");
            Db.GetDocument("pass").Should().BeNull("because the next document's deletion is not rejected");
            _isFilteredCallback = false;
        }

        [Fact]
        public void TestPullRemovedDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("pass")) {
                doc1.SetString("name", "pass");
                _otherDB.Save(doc1);

                doc2.SetString("name", "pass");
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            config.PullFilter = _replicator__filterCallback;
            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            Db.GetDocument("doc1").Should().NotBeNull("because doc1 passes the filter");
            Db.GetDocument("pass").Should().NotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = _otherDB.GetDocument("doc1"))
            using (var doc2 = _otherDB.GetDocument("pass"))
            using (var doc1Mutable = doc1.ToMutable())
            using (var doc2Mutable = doc2.ToMutable()) {
                doc1Mutable.SetData(new Dictionary<string, object> { ["_removed"] = true });
                doc2Mutable.SetData(new Dictionary<string, object> { ["_removed"] = true });
                _otherDB.Save(doc1Mutable);
                _otherDB.Save(doc2Mutable);
            }

            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            Db.GetDocument("doc1").Should().NotBeNull("because doc1's removal should be rejected");
            Db.GetDocument("pass").Should().BeNull("because the next document's removal is not rejected");
            _isFilteredCallback = false;
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
            _isFilteredCallback.Should().BeFalse();

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
            config.DocumentIDs = new[] { "doc1", "doc3" };
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
                    .Should().Throw<CouchbaseLiteException>(
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
                                ((Replicator)sender).Stop();
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
        public void TestDocumentEndedEvent()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(true, true, true);//push n pull

            Misc.SafeSwap(ref _repl, new Replicator(config));
            _waitAssert = new WaitAssert();
            var token1 = _repl.AddDocumentReplicationListener(DocumentEndedUpdate);
            var token = _repl.AddChangeListener((sender, args) =>
            {
                _waitAssert.RunConditionalAssert(() =>
                {
                    VerifyChange(args, 0, 0);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator)sender).Stop();
                    }
                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            _repl.Start();
            try {
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(30));
            } catch {
                _repl.Stop();
                throw;
            } finally {
                _repl.RemoveChangeListener(token);
                _repl.RemoveChangeListener(token1);
            }

            _replicationEvents.Should().HaveCount(2);
            var push = _replicationEvents.FirstOrDefault(g => g.IsPush);
            push.Documents.First().Id.Should().Be("doc1");
            var pull = _replicationEvents.FirstOrDefault(g => !g.IsPush);
            pull.Documents.First().Id.Should().Be("doc2");
        }

        [Fact]
        public void TestDocumentErrorEvent()
        {
            // NOTE: Only push, need to think of a case that can force an error
            // for pull
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                _otherDB.Save(doc1);
            }


            // Force a conflict
            using (var doc1a = Db.GetDocument("doc1"))
            using (var doc1aMutable = doc1a.ToMutable()) {
                doc1aMutable.SetString("name", "Liger");
                Db.Save(doc1aMutable);
            }

            using (var doc1b = _otherDB.GetDocument("doc1"))
            using (var doc1bMutable = doc1b.ToMutable()) {
                doc1bMutable.SetString("name", "Lion");
                _otherDB.Save(doc1bMutable);
            }

            var config = CreateConfig(true, false, false);
            using (var repl = new Replicator(config)) {
                var wa = new WaitAssert();
                repl.AddDocumentReplicationListener((sender, args) =>
                {
                    if (args.Documents[0].Id == "doc1") {
                        wa.RunAssert(() =>
                        {
                            args.Documents[0].Error.Domain.Should().Be(CouchbaseLiteErrorType.CouchbaseLite);
                            args.Documents[0].Error.Error.Should().Be((int)CouchbaseLiteError.HTTPConflict);
                        });
                    }
                });

                repl.Start();

                wa.WaitForResult(TimeSpan.FromSeconds(10));
                repl.Stop();
                Try.Condition(() => repl.Status.Activity == ReplicatorActivityLevel.Stopped)
                    .Times(5)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .Go().Should().BeTrue();
            }
        }

        [Fact]
        public void TestDocumentDeletedEvent()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "test1");
                Db.Save(doc1);
                Db.Delete(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "test2");
                _otherDB.Save(doc2);
                _otherDB.Delete(doc2);
            }

            var config = CreateConfig(true, true, false);
            var pullWait = new WaitAssert();
            var pushWait = new WaitAssert();
            RunReplication(config, 0, 0, documentReplicated: (sender, args) =>
            {
                pushWait.RunConditionalAssert(() => args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted)));
                pullWait.RunConditionalAssert(() => !args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted)));
            });

            pushWait.WaitForResult(TimeSpan.FromSeconds(5));
            pullWait.WaitForResult(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void TestChannelRemovedEvent()
        {
            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "test2");
                _otherDB.Save(doc2);
                doc2.SetData(new Dictionary<string, object> { ["_removed"] = true });
                _otherDB.Save(doc2);
            }

            var config = CreateConfig(true, true, false);
            var pullWait = new WaitAssert();
            RunReplication(config, 0, 0, documentReplicated: (sender, args) =>
            {
                pullWait.RunConditionalAssert(() => !args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.AccessRemoved)));
            });

            pullWait.WaitForResult(TimeSpan.FromSeconds(5));
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

        [Fact]
        public void TestPushAndForget()
        {
            for (int i = 0; i < 10; i++) {
                using (var mdoc = new MutableDocument()) {
                    mdoc.SetInt("id", i);
                    Db.Save(mdoc);
                }
            }

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0, documentReplicated: (sender, args) =>
            {
                foreach (var docID in args.Documents.Select(x => x.Id)) {
                    Db.Purge(docID);
                }
            });

            var success = Try.Condition(() => Db.Count == 0).Times(5).Go();
            success.Should().BeTrue("because push and forget should purge docs");
            _otherDB.Count.Should().Be(10, "because the documents should have been pushed");
        }

        [Fact]
        public void TestExpiredNotPushed()
        {
            const string docId = "byebye";
            using (var doc1 = new MutableDocument(docId)) {
                doc1.SetString("expire_me", "now");
                Db.Save(doc1);
            }

            Db.SetDocumentExpiration(docId, DateTimeOffset.Now);
            var config = CreateConfig(true, false, false);
            var callbackCount = 0;
            RunReplication(config, 0, 0, documentReplicated: (status, args) => { callbackCount++; });
            _otherDB.Count.Should().Be(0);
            callbackCount.Should().Be(0);
            _repl.Status.Progress.Total.Should().Be(0UL);
        }

        [Fact]
        public void TestConflictResolverPropertyInReplicationConfig()
        {
            var config = CreateConfig(false, true, false);

            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                return conflict.RemoteDocument;
            });

            config.ConflictResolver.GetType().Should().Be(typeof(TestConflictResolver));

            using (var replicator = new Replicator(config)) {
                
                Action badAction = (() => replicator.Config.ConflictResolver = new FakeConflictResolver());
                badAction.Should().Throw<InvalidOperationException>("Attempt to modify a frozen object is prohibited.");
            }
        }

        [Fact]
        public void TestConflictResolverRemoteWins()
        {
            var returnRemoteDoc = true;
            TestConflictResolverWins(returnRemoteDoc);
            TestConflictResolverWins(!returnRemoteDoc);
        }

        [Fact]
        public void TestConflictResolverMergeDoc()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Jim");
                Db.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Jim");
                doc1.SetString("location", "Japan");
                _otherDB.Save(doc1);
            }

            var config = CreateConfig(true, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                var localDoc = conflict.LocalDocument;
                var remoteDoc = conflict.RemoteDocument;

                var updateDocDict = localDoc.ToDictionary();
                var curDocDict = remoteDoc.ToDictionary();

                foreach (var value in curDocDict) {
                    if (updateDocDict.ContainsKey(value.Key) && !value.Value.Equals(updateDocDict[value.Key])) {
                        updateDocDict[value.Key] = value.Value + ", " + updateDocDict[value.Key];
                    } else if (!updateDocDict.ContainsKey(value.Key)) {
                        updateDocDict.Add(value.Key, value.Value);
                    }
                }

                WriteLine($"Resulting merge: {JsonConvert.SerializeObject(updateDocDict)}");
               
                var doc1 = new MutableDocument(conflict.DocumentID);
                doc1.SetData(updateDocDict);
                return doc1;
            });

            RunReplication(config, 0, 0);

            using (var doc1a = Db.GetDocument("doc1"))
            using (var doc1aMutable = doc1a.ToMutable()) {
                doc1aMutable.SetString("name", "Jim");
                doc1aMutable.SetString("language", "English");
                Db.Save(doc1aMutable);
            }

            using (var doc1a = _otherDB.GetDocument("doc1"))
            using (var doc1aMutable = doc1a.ToMutable()) {
                doc1aMutable.SetString("name", "Jim");
                doc1aMutable.SetString("language", "C#");
                _otherDB.Save(doc1aMutable);
            }

            RunReplication(config, 0, 0);

            using (var doc1 = Db.GetDocument("doc1")) {
                doc1.GetString("name").Should().Be("Jim");
                var lanStr = doc1.GetString("language");
                lanStr.Should().Contain("English");
                lanStr.Should().Contain("C#");
                doc1.GetString("location").Should().Be("Japan");
            }

            RunReplication(config, 0, 0);

            using (var doc1 = _otherDB.GetDocument("doc1")) {
                doc1.GetString("name").Should().Be("Jim");
                var lanStr = doc1.GetString("language");
                lanStr.Should().Contain("English");
                lanStr.Should().Contain("C#");
                doc1.GetString("location").Should().Be("Japan");
            }
        }

        [Fact]
        public void TestConflictResolverNullDoc()
        {
            bool conflictResolved = false;
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            config.ConflictResolver = new TestConflictResolver((conflict) => {
                conflictResolved = true;
                return null;
            });

            RunReplication(config, 0, 0, documentReplicated: (sender, args) =>
            {
                conflictResolved.Should().Be(true, "Because the DocumentReplicationEvent be notified after the conflict has being resolved.");
            });

            Db.GetDocument("doc1").Should().BeNull(); //Because conflict resolver returns null means return a deleted document.
        }

        [Fact]
        public void TestConflictResolverDeletedLocalWin()
        {
            Document localDoc = null, remoteDoc = null;
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                _otherDB.Save(doc1);
            }

            
            Db.Delete(Db.GetDocument("doc1"));

            Db.Count.Should().Be(0);

            using (var doc1 = _otherDB.GetDocument("doc1").ToMutable()) {
                doc1.SetString("name", "Lion");
                _otherDB.Save(doc1);
            }

            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) => {
                localDoc = conflict.LocalDocument;
                remoteDoc = conflict.RemoteDocument;
                return null;
            });

            RunReplication(config, 0, 0);

            localDoc.Should().BeNull();
            remoteDoc.Should().NotBeNull();

            Db.Count.Should().Be(0);
        }

        [Fact]
        public void TestConflictResolverDeletedRemoteWin()
        {
            Document localDoc = null, remoteDoc = null;
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                _otherDB.Save(doc1);
            }

            // Force a conflict
            using (var doc1a = Db.GetDocument("doc1").ToMutable()){
                doc1a.SetString("name", "Cat");
                Db.Save(doc1a);
            }

            Db.Count.Should().Be(1);

            _otherDB.Delete(_otherDB.GetDocument("doc1"));

            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) => {
                localDoc = conflict.LocalDocument;
                remoteDoc = conflict.RemoteDocument;
                return null;
            });

            RunReplication(config, 0, 0);
            remoteDoc.Should().BeNull();
            localDoc.Should().NotBeNull();
            Db.Count.Should().Be(0);
        }

        [Fact]
        public void TestConflictResolverWrongDocID()
        {
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                var doc = new MutableDocument("wrong_id");
                doc.SetString("wrong_id_key", "wrong_id_value");
                return doc;
            });

            RunReplication(config, 0, 0);

            using (var db = Db.GetDocument("doc1")) {
                db.GetString("wrong_id_key").Should().Be("wrong_id_value");
            }
        }
        [Fact]
        public void TestConflictResolverCalledTwice()
        {
            int resolveCnt = 0;
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                if (resolveCnt == 0) {
                    using (var d = Db.GetDocument("doc1"))
                    using (var doc = d.ToMutable()) {
                        doc.SetString("name", "Cougar");
                        Db.Save(doc);
                    }
                }

                resolveCnt++;
                return conflict.LocalDocument;
            });

            RunReplication(config, 0, 0);

            resolveCnt.Should().Be(2);
            using (var doc = Db.GetDocument("doc1")) {
                    doc.GetString("name").Should().Be("Cougar");
            }
        }

        [Fact]
        public void TestExceptionThrownInConflictResolver()
        {
            CreateReplicationConflict("doc1");
            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                    using (var d = Db.GetDocument("doc1"))
                    using (var doc = d.ToMutable()) {
                        d.GetString("name").Should().Be("Tiger");
                    }
                return null;
            });

            RunReplication(config, 0, 0, isConflictResolvingFailed: true);
        }

        [Fact]
        public void TestNonBlockingDatabaseOperationConflictResolver()
        {
            int resolveCnt = 0;
            CreateReplicationConflict("doc1");
            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                if (resolveCnt == 0) {
                    using (var d = Db.GetDocument("doc1"))
                    using (var doc = d.ToMutable()) {
                        d.GetString("name").Should().Be("Cat");
                        doc.SetString("name", "Cougar");
                        Db.Save(doc);
                        doc.GetString("name").Should().Be("Cougar", "Because database save operation was not blocked");
                    }
                }
                resolveCnt++;
                return null;
            });

            RunReplication(config, 0, 0);

            using (var doc = Db.GetDocument("doc1")) {
                if(resolveCnt==1)
                    doc.Should().BeNull();
            }
        }
        
        [Fact]
        public void TestNonBlockingConflictResolver()
        {
            CreateReplicationConflict("doc1");
            CreateReplicationConflict("doc2");
            var config = CreateConfig(false, true, false);
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            Queue<string> q = new Queue<string>();
            var wa = new WaitAssert();
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                var cnt = 0;
                lock (q) {
                    q.Enqueue(conflict.LocalDocument.Id);
                    cnt = q.Count;
                }

                if (cnt == 1) {
                    manualResetEvent.WaitOne();
                }

                q.Enqueue(conflict.LocalDocument.Id);
                wa.RunConditionalAssert(() => q.Count.Equals(4));

                if (cnt != 1) {
                    manualResetEvent.Set();
                }

                return conflict.RemoteDocument;
            });

            RunReplication(config, 0, 0);
            
            wa.WaitForResult(TimeSpan.FromMilliseconds(5000));

            // make sure, first doc starts resolution but finishes last.
            // in between second doc starts and finishes it.
            q.ElementAt(0).Should().Be(q.ElementAt(3));
            q.ElementAt(1).Should().Be(q.ElementAt(2));

            q.Clear();
        }
        
        [Fact]
        public void TestDoubleConflictResolutionOnSameConflicts()
        {
            CreateReplicationConflict("doc1");

            var firstReplicatorStart = new ManualResetEventSlim();
            var secondReplicatorFinish = new ManualResetEventSlim();
            int resolveCnt = 0;

            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                firstReplicatorStart.Set();
                secondReplicatorFinish.Wait();
                Thread.Sleep(500);
                resolveCnt++;
                return conflict.LocalDocument;
            });
            Replicator replicator = new Replicator(config);

            var config1 = CreateConfig(false, true, false);
            config1.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                resolveCnt++;
                Task.Delay(500).ContinueWith(t => secondReplicatorFinish.Set()); // Set after return
                return conflict.RemoteDocument;
            });
            Replicator replicator1 = new Replicator(config1);

            _waitAssert = new WaitAssert();
            var token = replicator.AddChangeListener((sender, args) =>
            {
                _waitAssert.RunConditionalAssert(() =>
                {
                    VerifyChange(args, 0, 0);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator)sender).Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            var _waitAssert1 = new WaitAssert();
            var token1 = replicator1.AddChangeListener((sender, args) =>
            {
                _waitAssert1.RunConditionalAssert(() =>
                {
                    VerifyChange(args, 0, 0);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator)sender).Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            replicator.Start();
            firstReplicatorStart.Wait();
            replicator1.Start();

            try {
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(10));
                _waitAssert1.WaitForResult(TimeSpan.FromSeconds(10));
            } catch {
                replicator1.Stop();
                replicator.Stop();
                throw;
            } finally {
                replicator.RemoveChangeListener(token);
                replicator1.RemoveChangeListener(token1);
            }

            using (var doc = Db.GetDocument("doc1")) {
                doc.GetBlob("blob")?.Content.Should().ContainInOrder(new byte[] { 7, 7, 7 });
            }
        }

        [Fact]
        public void TestConflictResolverExceptionWhenDocumentIsPurged()
        {
            int resolveCnt = 0;
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                if (resolveCnt == 0) {
                    Db.Purge(conflict.DocumentID);
                }
                resolveCnt++;
                return conflict.RemoteDocument;
            });

            RunReplication(config, 0, 0, documentReplicated: (sender, args) =>
            {
                if (!args.IsPush) {
                    args.Documents[0].Error.Error.Should().Be((int)CouchbaseLiteError.NotFound);
                }
            });
        }

        [Fact]
        public void TestConflictResolverExceptionsReturnDocFromOtherDBThrown()
        {
            var tmpDoc = new MutableDocument("doc1");
            using (var thirdDb = new Database("different_db")) {
                tmpDoc.SetString("foo", "bar");
                thirdDb.Save(tmpDoc);

                var differentDbResolver = new TestConflictResolver((conflict) => tmpDoc);

                TestConflictResolverExceptionThrown(differentDbResolver, true);
                Db.GetDocument("doc1").GetString("name").Should().Be("Human");

                thirdDb.Delete();
            }
        }

        [Fact]
        public void TestConflictResolverExceptionThrownInConflictResolver()
        {
            var resolverWithException = new TestConflictResolver((conflict) => {
                throw new Exception("Customer side exception");
            });

            TestConflictResolverExceptionThrown(resolverWithException, false);
        }

        [Fact]
        public void TestConflictResolverReturningBlob()
        {
            var returnRemoteDoc = true;
            TestConflictResolverWins(returnRemoteDoc);
            TestConflictResolverWins(!returnRemoteDoc);

            //return new doc with a blob object
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                var evilByteArray = new byte[] { 6, 6, 6 };

                var doc = new MutableDocument();
                doc.SetBlob("blob", new Blob("text/plaintext", evilByteArray));
                return doc;
            });

            RunReplication(config, 0, 0);

            using (var doc = Db.GetDocument("doc1")) {
                doc.GetBlob("blob")?.Content.Should().ContainInOrder(new byte[] { 6, 6, 6 });
            }
        }

        [Fact]
        public void TestConflictResolverReturningBlobFromDifferentDB()
        {
            var blobFromOtherDbResolver = new TestConflictResolver((conflict) =>
            {
                var md = conflict.LocalDocument.ToMutable();
                using (var otherDbDoc = _otherDB.GetDocument("doc1")) {
                    md.SetBlob("blob", otherDbDoc.GetBlob("blob"));
                }
                return md;
            });

            TestConflictResolverExceptionThrown(blobFromOtherDbResolver, false, true);
        }

        private void TestConflictResolverExceptionThrown(TestConflictResolver resolver, bool continueWithWorkingResolver = false, bool withBlob = false)
        {
            CreateReplicationConflict("doc1");

            var config = CreateConfig(true, true, false);
            config.ConflictResolver = resolver;

            using (var repl = new Replicator(config)) {
                var wa = new WaitAssert();
                var token = repl.AddDocumentReplicationListener((sender, args) =>
                {
                    if (args.Documents[0].Id == "doc1" && !args.IsPush) {
                        wa.RunAssert(() =>
                        {
                            WriteLine($"Received document listener callback of size {args.Documents.Count}");
                            args.Documents[0].Error.Domain.Should().Be(CouchbaseLiteErrorType.CouchbaseLite,
                                $"because otherwise the wrong error ({args.Documents[0].Error.Error}) occurred");
                            args.Documents[0].Error.Error.Should().Be((int)CouchbaseLiteError.UnexpectedError);
                            var innerException = ((Couchbase.Lite.Sync.ReplicatedDocument[])args.Documents)[0].Error.InnerException;
                            if (innerException is InvalidOperationException) {
                                if (withBlob) {
                                    innerException.Message.Should().Be("A document contains a blob that was saved to a different database; the save operation cannot complete.");
                                } else {
                                    innerException.Message.Should().Contain("Resolved document db different_db is different from expected db");
                                }
                            } else if(innerException is Exception) {
                                innerException.Message.Should().Be("Customer side exception");
                            }
                        });
                    }
                });

                repl.Start();

                wa.WaitForResult(TimeSpan.FromSeconds(10));

                Try.Condition(() => repl.Status.Activity == ReplicatorActivityLevel.Stopped)
                    .Times(5)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .Go().Should().BeTrue();

                repl.Status.Activity.Should().Be(ReplicatorActivityLevel.Stopped);
                repl.RemoveChangeListener(token);

                if (!continueWithWorkingResolver)
                    return;

                config.ConflictResolver = new TestConflictResolver((conflict) => 
                {
                    var doc = new MutableDocument("doc1");
                    doc.SetString("name", "Human");
                    return doc;
                });
                RunReplication(config, 0, 0);
            }
        }

        private void TestConflictResolverWins(bool returnRemoteDoc)
        {
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                if (returnRemoteDoc) {
                    return conflict.RemoteDocument;
                } else
                    return conflict.LocalDocument;
            });

            RunReplication(config, 0, 0);

            using (var doc = Db.GetDocument("doc1")) {
                if (returnRemoteDoc) {
                    doc.GetString("name").Should().Be("Lion");
                    doc.GetBlob("blob")?.Content.Should().ContainInOrder(new byte[] { 7, 7, 7 });
                } else {
                    doc.GetString("name").Should().Be("Cat");
                    doc.GetBlob("blob")?.Content.Should().ContainInOrder(new byte[] { 6, 6, 6 });
                }
            }
        }

        private void CreateReplicationConflict(string id)
        {
            var oddByteArray = new byte[] { 1, 3, 5 };

            using (var doc1 = new MutableDocument(id)) {
                doc1.SetString("name", "Tiger");
                doc1.SetBlob("blob", new Blob("text/plaintext", oddByteArray));
                Db.Save(doc1);
            }

            using (var doc1 = new MutableDocument(id)) {
                doc1.SetString("name", "Tiger");
                doc1.SetBlob("blob", new Blob("text/plaintext", oddByteArray));
                _otherDB.Save(doc1);
            }

            // Force a conflict
            using (var doc1a = Db.GetDocument(id))
            using (var doc1aMutable = doc1a.ToMutable()) {
                var evilByteArray = new byte[] { 6, 6, 6 };

                doc1aMutable.SetString("name", "Cat");
                doc1aMutable.SetBlob("blob", new Blob("text/plaintext", evilByteArray));
                Db.Save(doc1aMutable);
            }

            using (var doc1a = _otherDB.GetDocument(id))
            using (var doc1aMutable = doc1a.ToMutable()) {
                var luckyByteArray = new byte[] { 7, 7, 7 };

                doc1aMutable.SetString("name", "Lion");
                doc1aMutable.SetBlob("blob", new Blob("text/plaintext", luckyByteArray));
                _otherDB.Save(doc1aMutable);
            }
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

#if COUCHBASE_ENTERPRISE
        private ReplicatorConfiguration CreateConfig(bool push, bool pull, bool continuous)
        {
            var target = _otherDB;
            return CreateConfig(push, pull, continuous, target);
        }

        private void TestPushDocWithFilter(bool continuous)
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "donotpass");
                Db.Save(doc1);

                doc2.SetString("name", "pass");
                Db.Save(doc2);
            }

            var config = CreateConfig(true, false, continuous);
            config.PushFilter = _replicator__filterCallback;
            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            _otherDB.GetDocument("doc1").Should().BeNull("because doc1 is filtered out in the callback");
            _otherDB.GetDocument("doc2").Should().NotBeNull("because doc2 is filtered in in the callback");
            _isFilteredCallback = false;
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

        private bool _replicator__filterCallback(Document document, DocumentFlags flags)
        {
            _isFilteredCallback = true;
            if (flags != 0) {
                return document.Id == "pass";
            }

            var name = document.GetString("name");
            return name == "pass";
        }

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
                    if ((int)domain != 0) {
                        error.Domain.As<CouchbaseLiteErrorType>().Should().Be(domain);
                    }
                } else {
                    s.Error.Should().BeNull("because otherwise an unexpected error occurred");
                }
            }
        }

        private void RunReplication(ReplicatorConfiguration config, int expectedErrCode, CouchbaseLiteErrorType expectedErrDomain, bool reset = false,
            EventHandler<DocumentReplicationEventArgs> documentReplicated = null, bool isConflictResolvingFailed = false)
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
                        ((Replicator)sender).Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            if (reset) {
                _repl.ResetCheckpoint();
            }

            if (documentReplicated != null) {
                _repl.AddDocumentReplicationListener(documentReplicated);
            }

            if (isConflictResolvingFailed) {
                _repl.AddDocumentReplicationListener((sender, args) =>
                {
                    if (!args.IsPush) {
                        foreach(var d in args.Documents) {
                            d.Error.Should().NotBeNull();
                            var error = d.Error.As<CouchbaseException>();
                            error.Error.Should().BeGreaterThan(0);
                            error.Domain.Should().NotBeNull();
                        }
                    }
                });
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

        private void DocumentEndedUpdate(object sender, DocumentReplicationEventArgs args)
        {
            _replicationEvents.Add(args);
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
            Exception ex = null;
            _repl?.Dispose();
            _repl = null;

            base.Dispose(disposing);
            var name = _otherDB?.Name;
            _otherDB?.Dispose();
            _otherDB = null;

            var success = Try.Condition(() =>
            {
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

    public class TestConflictResolver : IConflictResolver
    {
        Func<Conflict, Document> ResolveFunc { get; set; }

        public TestConflictResolver(Func<Conflict, Document> resolveFunc)
        {
            ResolveFunc = resolveFunc;
        }

        public Document Resolve(Conflict conflict)
        {
            return ResolveFunc(conflict);
        }
    }

    public class FakeConflictResolver : IConflictResolver
    {
        public Document Resolve(Conflict conflict)
        {
            throw new NotImplementedException();
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
                new SocketException((int)SocketError.ConnectionReset), true);
            _total = count;
            return this;
        }

        public TestErrorLogic WithPermanentException()
        {
            _exception = new MessagingException("Test permanent exception",
                new SocketException((int)SocketError.AccessDenied), false);
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

