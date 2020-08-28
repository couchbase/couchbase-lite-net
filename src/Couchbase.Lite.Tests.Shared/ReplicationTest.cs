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
using Couchbase.Lite.Query;

using FluentAssertions;
using LiteCore;
using LiteCore.Interop;

using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Reflection;

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
    public abstract class ReplicatorTestBase : TestCase
    {
        private const string OtherDbName = "otherdb";

        private static int Counter;

        protected Replicator _repl;
        protected WaitAssert _waitAssert;
        protected TimeSpan _timeout;

        public Database OtherDb { get; internal set; }

        public bool DisableDefaultServerCertPinning { get; set; }

        public X509Certificate2 DefaultServerCert
        {
            get {
                using (var cert = typeof(ReplicatorTestBase).GetTypeInfo().Assembly.GetManifestResourceStream("SelfSigned.cer"))
                using (var ms = new MemoryStream()) {
                    cert.CopyTo(ms);
                    return new X509Certificate2(ms.ToArray());
                }
            }
        }

#if !WINDOWS_UWP
        protected ReplicatorTestBase(ITestOutputHelper output) : base(output)
#else
        protected ReplicatorTestBase()
#endif
        {
            ReopenDB();
            ReopenOtherDb();
            _timeout = TimeSpan.FromSeconds(15);

            //uncomment the code below when you need to see more detail log
            //Database.Log.Console.Level = LogLevel.Debug;
        }

        protected void OpenOtherDb()
        {
            OtherDb.Should().BeNull("because otherwise this is an invalid state to call Open");
            var nextCounter = Interlocked.Increment(ref Counter);
            var nextDbName = $"{OtherDbName}{nextCounter}";
            Database.Delete(nextDbName, Directory);
            OtherDb = OpenDB(nextDbName);
        }

        protected void ReopenOtherDb()
        {
            OtherDb?.Close();
            OtherDb = null;
            OpenOtherDb();
        }

        protected ReplicatorConfiguration CreateConfig(IEndpoint target, ReplicatorType type, bool continuous,
            Authenticator authenticator = null, X509Certificate2 serverCert = null, Database sourceDb = null)
        {
            var c = new ReplicatorConfiguration(sourceDb ?? Db, target)
            {
                ReplicatorType = type,
                Continuous = continuous,
                Authenticator = authenticator
            };

            if ((target as URLEndpoint)?.Url?.Scheme == "wss") {
                if (serverCert != null) {
                    c.PinnedServerCertificate = serverCert;
                } else if (!DisableDefaultServerCertPinning) {
                    c.PinnedServerCertificate = DefaultServerCert;
                }
            }

            if (continuous) {
                c.CheckpointInterval = TimeSpan.FromSeconds(1);
            }

            return c;
        }

        #if COUCHBASE_ENTERPRISE
        protected ReplicatorConfiguration CreateConfig(IEndpoint target, ReplicatorType type, bool continuous,
            bool acceptOnlySelfSignedServerCertificate, Authenticator authenticator = null,
            X509Certificate2 serverCert = null)
        {
            var c = CreateConfig(target, type, continuous, authenticator, serverCert);
            c.AcceptOnlySelfSignedServerCertificate = acceptOnlySelfSignedServerCertificate;
            return c;
        }
        #endif

        protected void RunReplication(ReplicatorConfiguration config, int expectedErrCode, CouchbaseLiteErrorType expectedErrDomain, bool reset = false,
            Action<Replicator> onReplicatorReady = null)
        {
            Misc.SafeSwap(ref _repl, new Replicator(config));
            onReplicatorReady?.Invoke(_repl);

            RunReplication(_repl, expectedErrCode, expectedErrDomain, reset);
        }

        protected void RunReplication(IEndpoint target, ReplicatorType type, bool continuous,
            Authenticator authenticator, X509Certificate2 serverCert, int expectedErrCode,
            CouchbaseLiteErrorType expectedErrorDomain)
        {
            var config = CreateConfig(target, type, continuous, authenticator, serverCert);
            RunReplication(config, expectedErrCode, expectedErrorDomain);
        }

        #if COUCHBASE_ENTERPRISE
        protected void RunReplication(IEndpoint target, ReplicatorType type, bool continuous,
            Authenticator authenticator, bool acceptOnlySelfSignedServerCertificate,
            X509Certificate2 serverCert, int expectedErrCode, CouchbaseLiteErrorType expectedErrorDomain)
        {
            var config = CreateConfig(target, type, continuous, acceptOnlySelfSignedServerCertificate, authenticator,
                serverCert);
            RunReplication(config, expectedErrCode, expectedErrorDomain);
        }
        #endif

        protected void RunReplication(Replicator replicator, int expectedErrCode,
            CouchbaseLiteErrorType expectedErrDomain, bool reset = false)
        {
            _waitAssert = new WaitAssert();
            var token = _repl.AddChangeListener((sender, args) => {
                _waitAssert.RunConditionalAssert(() => {
                    VerifyChange(args, expectedErrCode, expectedErrDomain);
                    if (replicator.Config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator)sender).Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

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

        protected void VerifyChange(ReplicatorStatusChangedEventArgs change, int errorCode, CouchbaseLiteErrorType domain)
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

        protected override void Dispose(bool disposing)
        {
            Exception ex = null;
            _repl?.Dispose();
            _repl = null;

            base.Dispose(disposing);
            var name = OtherDb?.Name;
            OtherDb?.Dispose();
            OtherDb = null;

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

#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class ReplicatorTest : ReplicatorTestBase
    {
        private bool _isFilteredCallback;
        private List<DocumentReplicationEventArgs> _replicationEvents = new List<DocumentReplicationEventArgs>();

#if !WINDOWS_UWP
        public ReplicatorTest(ITestOutputHelper output) : base(output)
        {
        }
#endif

#if !WINDOWS_UWP
        [Fact]
        public async Task TestReplicatorStopsWhenEndpointInvalid()
        {
            // If this IP address happens to exist, then change it.  It needs to be an address that does not
            // exist on the network
            var uri = new Uri("ws://www.cbltest.com:4984/app");
            //hijack this test to test cookies
            var cookieStr = "id=a3fWa; expires:\"Tue, 20-Oct-2020 05:54:52 GMT\"; Domain:cbltest.com; Secure; HttpOnly";
            Db.SaveCookie(cookieStr, uri);
            var targetEndpoint = new URLEndpoint(uri);
            var config = new ReplicatorConfiguration(Db, targetEndpoint);
            using (var repl = new Replicator(config)) {
                repl.Start();
                //test cookies
                config.Options.Cookies.ElementAt(0).Value.Should().Be("a3fWa");
                config.Options.Cookies.ElementAt(0).Name.Should().Be("id");
                //end test cookies
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
                OtherDb.Save(doc2);
            }

            for (int i = 0; i < 2; i++) {
                RunReplication(config, 0, 0);
                Db.Count.Should().Be(1, "because the pull should have rejected the other document");
                OtherDb.Count.Should().Be(1, "because the push should have rejected the local document");
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
            OtherDb.GetDocument("doc1").Should().NotBeNull("because doc1 passes the filter");
            OtherDb.GetDocument("pass").Should().NotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = Db.GetDocument("doc1"))
            using (var doc2 = Db.GetDocument("pass")) {
                Db.Delete(doc1);
                Db.Delete(doc2);
            }

            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            OtherDb.GetDocument("doc1").Should().NotBeNull("because doc1's deletion should be rejected");
            OtherDb.GetDocument("pass").Should().BeNull("because the next document's deletion is not rejected");
            _isFilteredCallback = false;
        }

        [Fact]
        public void TestRevisionIdInPushPullFilters()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetInt("One", 1);
                Db.Save(doc1);
                doc2.SetInt("Two", 2);
                OtherDb.Save(doc2);
            }

            var config = CreateConfig(true, true, false);
            var exceptions = new List<Exception>();
            config.PullFilter = (doc, isPush) =>
            {
                try {
                    doc.GetInt("Two").Should().Be(2);
                    doc.RevisionID.Should().NotBeNull();
                    Action act = () => doc.ToMutable();
                    act.Should().Throw<InvalidOperationException>()
                      .WithMessage(CouchbaseLiteErrorMessage.NoDocEditInReplicationFilter);
                } catch (Exception e) {
                    exceptions.Add(e);
                }

                return true;
            };

            config.PushFilter = (doc, isPush) =>
            {
                try {
                    doc.GetInt("One").Should().Be(1);
                    doc.RevisionID.Should().NotBeNull();
                    Action act = () => doc.ToMutable();
                    act.Should().Throw<InvalidOperationException>()
                      .WithMessage(CouchbaseLiteErrorMessage.NoDocEditInReplicationFilter);

                } catch (Exception e) {
                    exceptions.Add(e);
                }

                return true;
            };

            RunReplication(config, 0, 0);
            exceptions.Count.Should().Be(0);
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
                OtherDb.Save(doc2);
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
                    doc.RevisionID.Should().NotBeNull();
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
                    doc.RevisionID.Should().NotBeNull();
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
                OtherDb.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeFalse();

            OtherDb.Count.Should().Be(2UL);
            using (var savedDoc1 = OtherDb.GetDocument("doc1")) {
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
                OtherDb.Save(doc2);
            }

            var config = CreateConfig(true, false, true);
            config.CheckpointInterval = TimeSpan.FromSeconds(1);
            RunReplication(config, 0, 0);

            OtherDb.Count.Should().Be(2UL);
            using (var savedDoc1 = OtherDb.GetDocument("doc1")) {
                savedDoc1.GetString("name").Should().Be("Tiger");
            }
        }

        [Fact]
        public void TestPullDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "donotpass");
                OtherDb.Save(doc1);

                doc2.SetString("name", "pass");
                OtherDb.Save(doc2);
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
                OtherDb.Save(doc1);

                doc2.SetString("name", "pass");
                OtherDb.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            config.PullFilter = _replicator__filterCallback;
            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            Db.GetDocument("doc1").Should().NotBeNull("because doc1 passes the filter");
            Db.GetDocument("pass").Should().NotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = OtherDb.GetDocument("doc1"))
            using (var doc2 = OtherDb.GetDocument("pass")) {
                OtherDb.Delete(doc1);
                OtherDb.Delete(doc2);
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
                OtherDb.Save(doc1);

                doc2.SetString("name", "pass");
                OtherDb.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            config.PullFilter = _replicator__filterCallback;
            RunReplication(config, 0, 0);
            _isFilteredCallback.Should().BeTrue();
            Db.GetDocument("doc1").Should().NotBeNull("because doc1 passes the filter");
            Db.GetDocument("pass").Should().NotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = OtherDb.GetDocument("doc1"))
            using (var doc2 = OtherDb.GetDocument("pass"))
            using (var doc1Mutable = doc1.ToMutable())
            using (var doc2Mutable = doc2.ToMutable()) {
                doc1Mutable.SetData(new Dictionary<string, object> { ["_removed"] = true });
                doc2Mutable.SetData(new Dictionary<string, object> { ["_removed"] = true });
                OtherDb.Save(doc1Mutable);
                OtherDb.Save(doc2Mutable);
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
                OtherDb.Save(doc2);
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
                OtherDb.Save(doc2);
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
            OtherDb.Save(doc3);
            doc3.SetString("name", "Hobbes");
            OtherDb.Save(doc3);

            var doc4 = new MutableDocument("doc4");
            doc4.SetString("species", "Tiger");
            OtherDb.Save(doc4);
            doc4.SetString("pattern", "striped");
            OtherDb.Save(doc4);

            var config = CreateConfig(true, true, false);
            config.DocumentIDs = new[] { "doc1", "doc3" };
            RunReplication(config, 0, 0);
            Db.Count.Should().Be(3, "because only one document should have been pulled");
            Db.GetDocument("doc3").Should().NotBeNull();
            OtherDb.Count.Should().Be(3, "because only one document should have been pushed");
            OtherDb.GetDocument("doc1").Should().NotBeNull();
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

                    Task.Delay(500).Wait(); // increase delay time to prevent intermittent failures due to replicator ref might not completely dererf yet atm
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
                OtherDb.Save(doc2);
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
                        ((Replicator) sender).Stop();
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
                OtherDb.Save(doc1);
            }


            // Force a conflict
            using (var doc1a = Db.GetDocument("doc1"))
            using (var doc1aMutable = doc1a.ToMutable()) {
                doc1aMutable.SetString("name", "Liger");
                Db.Save(doc1aMutable);
            }

            using (var doc1b = OtherDb.GetDocument("doc1"))
            using (var doc1bMutable = doc1b.ToMutable()) {
                doc1bMutable.SetString("name", "Lion");
                OtherDb.Save(doc1bMutable);
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
                            args.Documents[0].Error.Error.Should().Be((int) CouchbaseLiteError.HTTPConflict);
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
                OtherDb.Save(doc2);
                OtherDb.Delete(doc2);
            }

            var config = CreateConfig(true, true, false);
            var pullWait = new WaitAssert();
            var pushWait = new WaitAssert();
            RunReplication(config, 0, 0, onReplicatorReady: (r) =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    pushWait.RunConditionalAssert(() =>
                        args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted)));
                    pullWait.RunConditionalAssert(() =>
                        !args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted)));
                });
            });

            pushWait.WaitForResult(TimeSpan.FromSeconds(5));
            pullWait.WaitForResult(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void TestChannelRemovedEvent()
        {
            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "test2");
                OtherDb.Save(doc2);
                doc2.SetData(new Dictionary<string, object> { ["_removed"] = true });
                OtherDb.Save(doc2);
            }

            var config = CreateConfig(true, true, false);
            var pullWait = new WaitAssert();
            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    pullWait.RunConditionalAssert(() =>
                        !args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.AccessRemoved)));
                });
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

            OtherDb.Count.Should().Be(1UL);
            using (var doc1 = OtherDb.GetDocument("doc1")) {
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

            OtherDb.Count.Should().Be(2UL);
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
        public void TestPushAndForget()
        {
            for (int i = 0; i < 10; i++) {
                using (var mdoc = new MutableDocument()) {
                    mdoc.SetInt("id", i);
                    Db.Save(mdoc);
                }
            }

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    foreach (var docID in args.Documents.Select(x => x.Id)) {
                        Db.Purge(docID);
                    }
                });
            });

            var success = Try.Condition(() => Db.Count == 0).Times(5).Go();
            success.Should().BeTrue("because push and forget should purge docs");
            OtherDb.Count.Should().Be(10, "because the documents should have been pushed");
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
            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((status, args) => { callbackCount++; });
            });
            OtherDb.Count.Should().Be(0);
            callbackCount.Should().Be(0);
            _repl.Status.Progress.Total.Should().Be(0UL);
        }

        //conflict resolving tests

        [Fact]
        public void TestConflictResolverBothRemoteLocalDelete()
        {
            int resolveCnt = 0;
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                Db.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                OtherDb.Save(doc1);
            }

            // Force a conflict
            using (var doc1a = Db.GetDocument("doc1").ToMutable()) {
                doc1a.SetString("name", "Cat");
                Db.Save(doc1a);
            }

            Db.Count.Should().Be(1);

            OtherDb.Delete(OtherDb.GetDocument("doc1"));

            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                using (var doc1 = Db.GetDocument("doc1")) {
                    Db.Delete(doc1);
                }
                resolveCnt++;
                return conflict.LocalDocument;
            });

            RunReplication(config, 0, 0);
            resolveCnt.Should().Be(1);
            Db.Count.Should().Be(0);
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
                OtherDb.Save(doc1);
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

            using (var doc1a = OtherDb.GetDocument("doc1"))
            using (var doc1aMutable = doc1a.ToMutable()) {
                doc1aMutable.SetString("name", "Jim");
                doc1aMutable.SetString("language", "C#");
                OtherDb.Save(doc1aMutable);
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

            using (var doc1 = OtherDb.GetDocument("doc1")) {
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

            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                conflictResolved = true;
                return null;
            });

            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    conflictResolved.Should().Be(true,
                        "Because the DocumentReplicationEvent be notified after the conflict has being resolved.");
                });
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
                OtherDb.Save(doc1);
            }


            Db.Delete(Db.GetDocument("doc1"));

            Db.Count.Should().Be(0);

            using (var doc1 = OtherDb.GetDocument("doc1").ToMutable()) {
                doc1.SetString("name", "Lion");
                OtherDb.Save(doc1);
            }

            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
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
                OtherDb.Save(doc1);
            }

            // Force a conflict
            using (var doc1a = Db.GetDocument("doc1").ToMutable()) {
                doc1a.SetString("name", "Cat");
                Db.Save(doc1a);
            }

            Db.Count.Should().Be(1);

            OtherDb.Delete(OtherDb.GetDocument("doc1"));

            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
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
                        using (var docCheck = Db.GetDocument("doc1")) {
                            docCheck.GetString("name").Should().Be("Cougar", "Because database save operation was not blocked");
                        }
                    }
                }
                resolveCnt++;
                return null;
            });

            RunReplication(config, 0, 0);

            // This will be 0 if the test resolver threw an exception
            resolveCnt.Should().NotBe(0, "because otherwise the conflict resolver didn't complete");

            using (var doc = Db.GetDocument("doc1")) {
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
                        ((Replicator) sender).Stop();
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
                        ((Replicator) sender).Stop();
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
                replicator.Dispose();
                replicator1.Dispose();
            }

            using (var doc = Db.GetDocument("doc1")) {
                doc.GetBlob("blob")?.Content.Should().Contain(new byte[] { 7, 7, 7 });
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

            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    if (!args.IsPush) {
                        args.Documents[0].Error.Error.Should().Be((int) CouchbaseLiteError.NotFound);
                    }
                });
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
            var resolverWithException = new TestConflictResolver((conflict) =>
            {
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
                using (var otherDbDoc = OtherDb.GetDocument("doc1")) {
                    md.SetBlob("blob", otherDbDoc.GetBlob("blob"));
                }
                return md;
            });

            TestConflictResolverExceptionThrown(blobFromOtherDbResolver, false, true);
        }

        //CBL-623: Revision flags get cleared while saving resolved document
        [Fact]
        public void TestConflictResolverPreservesFlags()
        {
            //force conflicts and check flags
            CreateReplicationConflict("doc1", true);

            var config = CreateConfig(false, true, false);
            C4DocumentFlags flags = (C4DocumentFlags) 0;
            config.ConflictResolver = new TestConflictResolver((conflict) =>
            {
                unsafe {
                    flags = conflict.LocalDocument.c4Doc.RawDoc->flags;
                    flags.HasFlag(C4DocumentFlags.DocConflicted).Should().BeTrue();
                    flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).Should().BeTrue();
                    return conflict.LocalDocument;
                }
            });

            RunReplication(config, 0, 0);

            using (var doc = Db.GetDocument("doc1")) {
                doc.GetBlob("blob")?.Content.Should().ContainInOrder(new byte[] { 6, 6, 6 });
                unsafe {
                    flags = doc.c4Doc.RawDoc->flags;
                }
            }

            flags.HasFlag(C4DocumentFlags.DocConflicted).Should().BeFalse();
            flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).Should().BeTrue();
        }

        //end conflict resolveing tests

        [Fact]
        public void TestCloseWithActiveReplications() => WithActiveReplications(true);

        [Fact]
        public void TestDeleteWithActiveReplications() => WithActiveReplications(false);

        [Fact]
        public void TestCloseWithActiveReplicationAndQuery() => WithActiveReplicationAndQuery(true);

        [Fact]
        public void TestDeleteWithActiveReplicationAndQuery() => WithActiveReplicationAndQuery(false);

        // Pending Doc Ids unit tests

        [Fact]
        public void TestPendingDocIDsPullOnlyException()
        {
            LoadDocs();
            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                OtherDb.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            using (var replicator = new Replicator(config)) {
                var wa = new WaitAssert();
                var token = replicator.AddChangeListener((sender, args) =>
                {
                    wa.RunConditionalAssert(() =>
                    {
                        if (args.Status.Activity == ReplicatorActivityLevel.Busy) {
                            Action badAct = () => ((Replicator) sender).GetPendingDocumentIDs();
                            badAct.Should().Throw<CouchbaseLiteException>().WithMessage(CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs);
                        }

                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                replicator.Start();

                try {
                    wa.WaitForResult(TimeSpan.FromSeconds(100));
                    replicator.Status.Activity.Should().Be(ReplicatorActivityLevel.Stopped);
                } finally {
                    replicator.RemoveChangeListener(token);
                }
            }
        }

        [Fact]
        public void TestPendingDocIDsWithCreate() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.CREATE);

        [Fact]
        public void TestPendingDocIDsWithUpdate() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.UPDATE);

        [Fact]
        public void TestPendingDocIDsWithDelete() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.DELETE);

        [Fact]
        public void TestPendingDocIDsWithPurge() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.PURGE);

        [Fact]
        public void TestPendingDocIDsWithFilter() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.FILTER);

        [Fact]
        public void TestIsDocumentPendingPullOnlyException()
        {
            LoadDocs();
            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                OtherDb.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            using (var replicator = new Replicator(config)) {
                var wa = new WaitAssert();
                var token = replicator.AddChangeListener((sender, args) =>
                {
                    wa.RunConditionalAssert(() =>
                    {
                        if (args.Status.Activity == ReplicatorActivityLevel.Busy) {
                            Action badAct = () => ((Replicator) sender).IsDocumentPending("doc-001");
                            badAct.Should().Throw<CouchbaseLiteException>().WithMessage(CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs);
                        }

                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });

                });

                replicator.Start();

                try {
                    wa.WaitForResult(TimeSpan.FromSeconds(100));
                    replicator.Status.Activity.Should().Be(ReplicatorActivityLevel.Stopped);
                } finally {
                    replicator.RemoveChangeListener(token);
                }

            }
        }

        [Fact]
        public void TestIsDocumentPendingWithCreate() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.CREATE);

        [Fact]
        public void TestIsDocumentPendingWithUpdate() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.UPDATE);

        [Fact]
        public void TestIsDocumentPendingWithDelete() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.DELETE);

        [Fact]
        public void TestIsDocumentPendingWithPurge() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.PURGE);

        [Fact]
        public void TestIsDocumentPendingWithFilter() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.FILTER);

        [Fact]
        public void TestGetPendingDocIdsWithCloseDb()
        {
            var config = CreateConfig(true, false, false);
            using (var replicator = new Replicator(config)) {
                Db.Close();
                Action badAct = () => replicator.GetPendingDocumentIDs();
                badAct.Should().Throw<InvalidOperationException>().WithMessage(CouchbaseLiteErrorMessage.DBClosed);
            }

            ReopenDB();
            using (var replicator = new Replicator(config)) {
                OtherDb.Close();
                Action badAct = () => replicator.GetPendingDocumentIDs();
                badAct.Should().Throw<InvalidOperationException>().WithMessage(CouchbaseLiteErrorMessage.DBClosed);
            }
        }

        [Fact]
        public void TestIsDocumentPendingWithCloseDb()
        {
            var config = CreateConfig(true, false, false);
            using (var replicator = new Replicator(config)) {
                Db.Close();
                Action badAct = () => replicator.IsDocumentPending("doc1");
                badAct.Should().Throw<InvalidOperationException>().WithMessage(CouchbaseLiteErrorMessage.DBClosed);
            }

            ReopenDB();
            using (var replicator = new Replicator(config)) {
                OtherDb.Close();
                Action badAct = () => replicator.IsDocumentPending("doc1");
                badAct.Should().Throw<InvalidOperationException>().WithMessage(CouchbaseLiteErrorMessage.DBClosed);
            }
        }

        //end pending doc id tests

#endif

        enum PENDING_DOC_ID_SEL { CREATE = 0, UPDATE, DELETE, PURGE, FILTER }

        private HashSet<string> LoadDocs()
        {
            var result = new HashSet<string>();
            var n = 0ul;
            while (n < 50) {
                var docID = $"doc-{++n:D3}";
                using (var doc = new MutableDocument(docID)) {
                    result.Add(docID);
                    doc.SetString(docID, docID);
                    Db.Save(doc);
                }
            }

            return result;
        }


#if COUCHBASE_ENTERPRISE
        private void ValidatePendingDocumentIds(PENDING_DOC_ID_SEL selection)
        {
            IImmutableSet<string> pendingDocIds;
            var idsSet = LoadDocs();
            var config = CreateConfig(true, false, false);
            var DocIdForTest = "doc-001";
            if (selection == PENDING_DOC_ID_SEL.UPDATE) {
                using (var d = Db.GetDocument(DocIdForTest))
                using (var md = d.ToMutable()) {
                    md.SetString("addString", "This is a new string.");
                    Db.Save(md);
                }
            } else if (selection == PENDING_DOC_ID_SEL.DELETE) {
                using (var d = Db.GetDocument(DocIdForTest)) {
                    Db.Delete(d);
                }
            } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                using (var d = Db.GetDocument(DocIdForTest)) {
                    Db.Purge(d);
                    idsSet.Remove(DocIdForTest);
                }
            } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                config.PushFilter = (doc, isPush) =>
                {
                    if (doc.Id.Equals(DocIdForTest))
                        return true;
                    return false;
                };
            }

            using (var replicator = new Replicator(config)) {
                var wa = new WaitAssert();
                var token = replicator.AddChangeListener((sender, args) =>
                {
                    wa.RunConditionalAssert(() =>
                    {
                        if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                            pendingDocIds = replicator.GetPendingDocumentIDs();
                            pendingDocIds.Count.Should().Be(0);
                        }

                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                pendingDocIds = replicator.GetPendingDocumentIDs();
                if (selection == PENDING_DOC_ID_SEL.FILTER) {
                    pendingDocIds.Count.Should().Be(1);
                    pendingDocIds.ElementAt(0).Should().Be(DocIdForTest);
                } else {
                    idsSet.ToImmutableSortedSet<string>().Should().BeEquivalentTo(pendingDocIds);
                    idsSet.Count.Should().Be(pendingDocIds.Count);
                }

                replicator.Start();

                wa.WaitForResult(TimeSpan.FromSeconds(50));

                Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                    .Times(5)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .Go().Should().BeTrue();

                replicator.GetPendingDocumentIDs().Count.Should().Be(0);
                replicator.RemoveChangeListener(token);
            }

            Thread.Sleep(500); //it takes a while to get the replicator to actually released...
        }

        private void ValidateIsDocumentPending(PENDING_DOC_ID_SEL selection)
        {
            bool docIdIsPending;
            var DocIdForTest = "doc-001";
            var idsSet = LoadDocs();
            var config = CreateConfig(true, false, false);
            if (selection == PENDING_DOC_ID_SEL.UPDATE) {
                using (var d = Db.GetDocument(DocIdForTest))
                using (var md = d.ToMutable()) {
                    md.SetString("addString", "This is a new string.");
                    Db.Save(md);
                }
            } else if (selection == PENDING_DOC_ID_SEL.DELETE) {
                using (var d = Db.GetDocument(DocIdForTest)) {
                    Db.Delete(d);
                }
            } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                using (var d = Db.GetDocument(DocIdForTest)) {
                    Db.Purge(d);
                }
            } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                config.PushFilter = (doc, isPush) =>
                {
                    if (doc.Id.Equals(DocIdForTest))
                        return true;
                    return false;
                };
            }

            using (var replicator = new Replicator(config)) {
                var wa = new WaitAssert();
                var token = replicator.AddChangeListener((sender, args) =>
                {
                    if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                        docIdIsPending = replicator.IsDocumentPending(DocIdForTest);
                        docIdIsPending.Should().BeFalse();
                    }

                    wa.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                docIdIsPending = replicator.IsDocumentPending(DocIdForTest);
                if (selection == PENDING_DOC_ID_SEL.CREATE || selection == PENDING_DOC_ID_SEL.UPDATE
                    || selection == PENDING_DOC_ID_SEL.DELETE) {
                    docIdIsPending.Should().BeTrue();
                    docIdIsPending = replicator.IsDocumentPending("IdNotThere");
                    docIdIsPending.Should().BeFalse();
                } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                    docIdIsPending.Should().BeTrue();
                    docIdIsPending = replicator.IsDocumentPending("doc-002");
                    docIdIsPending.Should().BeFalse();
                } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                    docIdIsPending.Should().BeFalse();
                }

                replicator.Start();

                wa.WaitForResult(TimeSpan.FromSeconds(50));

                Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                    .Times(5)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .Go().Should().BeTrue();

                replicator.IsDocumentPending(DocIdForTest).Should().BeFalse();
                replicator.RemoveChangeListener(token);
            }

            Thread.Sleep(500); //it takes a while to get the replicator to actually released...
        }

        private void WithActiveReplicationAndQuery(bool isCloseNotDelete)
        {
            Database.Delete("closeDB", Db.Config.Directory);
            using (var otherDb = new Database("closeDB", Db.Config)) {
                WaitAssert waitIdleAssert = new WaitAssert();
                WaitAssert waitStoppedAssert = new WaitAssert();
                var config = CreateConfig(true, true, true, otherDb);
                using (var repl = new Replicator(config)) {

                    var query = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Database(Db));
                    var doc1Listener = new WaitAssert();
                    query.AddChangeListener(null, (sender, args) => {
                        foreach (var row in args.Results) {
                            if (row.GetString("id") == "doc1") {
                                doc1Listener.Fulfill();
                            }
                        }
                    });

                    repl.AddChangeListener((sender, args) => {
                        waitIdleAssert.RunConditionalAssert(() => {
                            return args.Status.Activity == ReplicatorActivityLevel.Idle;
                        });

                        waitStoppedAssert.RunConditionalAssert(() => {
                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    repl.Start();

                    using (var doc = new MutableDocument("doc1")) {
                        doc.SetString("value", "string");
                        Db.Save(doc); // Should still trigger since it is pointing to the same DB
                    }

                    doc1Listener.WaitForResult(TimeSpan.FromSeconds(20));
                    waitIdleAssert.WaitForResult(TimeSpan.FromSeconds(10));

                    Db.ActiveStoppables.Count.Should().Be(2);
                    //Db.ActiveLiveQueries.Count.Should().Be(1);

                    if (isCloseNotDelete)
                        Db.Close();
                    else
                        Db.Delete();

                    Db.ActiveStoppables.Count.Should().Be(0);
                    //Db.ActiveLiveQueries.Count.Should().Be(0);
                    Db.IsClosedLocked.Should().Be(true);

                    waitStoppedAssert.WaitForResult(TimeSpan.FromSeconds(30));
                }
            }

            Database.Delete("closeDB", Db.Config.Directory);
        }

        private void WithActiveReplications(bool isCloseNotDelete)
        {
            Database.Delete("closeDB", Db.Config.Directory);
            using (var otherDb = new Database("closeDB", Db.Config)) {
                WaitAssert waitIdleAssert = new WaitAssert();
                WaitAssert waitStoppedAssert = new WaitAssert();
                WaitAssert waitIdleAssert1 = new WaitAssert();
                WaitAssert waitStoppedAssert1 = new WaitAssert();

                var config = CreateConfig(true, true, true, otherDb);
                using (var repl = new Replicator(config))
                using (var repl1 = new Replicator(config)) {
                    var token = repl.AddChangeListener((sender, args) => {
                        waitIdleAssert.RunConditionalAssert(() => {
                            return args.Status.Activity == ReplicatorActivityLevel.Idle;
                        });

                        waitStoppedAssert.RunConditionalAssert(() => {
                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    var token1 = repl1.AddChangeListener((sender, args) => {
                        waitIdleAssert1.RunConditionalAssert(() => {
                            return args.Status.Activity == ReplicatorActivityLevel.Idle;
                        });

                        waitStoppedAssert1.RunConditionalAssert(() => {
                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    repl.Start();
                    repl1.Start();

                    using (var doc = new MutableDocument("doc1")) {
                        doc.SetString("value", "string");
                        otherDb.Save(doc); // Should still trigger since it is pointing to the same DB
                    }

                    waitIdleAssert.WaitForResult(TimeSpan.FromSeconds(10));
                    waitIdleAssert1.WaitForResult(TimeSpan.FromSeconds(10));

                    Db.ActiveStoppables.Count.Should().Be(2);

                    if (isCloseNotDelete)
                        Db.Close();
                    else
                        Db.Delete();

                    Db.ActiveStoppables.Count.Should().Be(0);
                    Db.IsClosedLocked.Should().Be(true);

                    waitStoppedAssert.WaitForResult(TimeSpan.FromSeconds(30));
                    waitStoppedAssert1.WaitForResult(TimeSpan.FromSeconds(30));
                }
            }

            Database.Delete("closeDB", Db.Config.Directory);
        }

        private void TestConflictResolverExceptionThrown(TestConflictResolver resolver, bool continueWithWorkingResolver = false, bool withBlob = false)
        {
            CreateReplicationConflict("doc1");

            var config = CreateConfig(true, true, false);
            config.ConflictResolver = resolver;

            using (var repl = new Replicator(config)) {
                var wa = new WaitAssert();
                var token = repl.AddDocumentReplicationListener((sender, args) => {
                    if (args.Documents[0].Id == "doc1" && !args.IsPush) {
                        wa.RunAssert(() => {
                            WriteLine($"Received document listener callback of size {args.Documents.Count}");
                            args.Documents[0].Error.Domain.Should().Be(CouchbaseLiteErrorType.CouchbaseLite,
                                $"because otherwise the wrong error ({args.Documents[0].Error.Error}) occurred");
                            args.Documents[0].Error.Error.Should().Be((int)CouchbaseLiteError.UnexpectedError);
                            var innerException = ((Couchbase.Lite.Sync.ReplicatedDocument[])args.Documents)[0].Error.InnerException;
                            if (innerException is InvalidOperationException) {
                                if (withBlob) {
                                    innerException.Message.Should().Be(CouchbaseLiteErrorMessage.BlobDifferentDatabase);
                                } else {
                                    innerException.Message.Should().Contain("Resolved document's database different_db is different from expected database");
                                }
                            } else if (innerException is Exception) {
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

                config.ConflictResolver = new TestConflictResolver((conflict) => {
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

            config.ConflictResolver = new TestConflictResolver((conflict) => {
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
#endif

        private void CreateReplicationConflict(string id, bool checkFlags = false)
        {
            unsafe {
                var oddByteArray = new byte[] { 1, 3, 5 };
                C4DocumentFlags flags = (C4DocumentFlags)0;
                using (var doc1 = new MutableDocument(id)) {
                    doc1.SetString("name", "Tiger");
                    doc1.SetBlob("blob", new Blob("text/plaintext", oddByteArray));
                    Db.Save(doc1);
                    if (checkFlags) {
                        flags = doc1.c4Doc.RawDoc->flags;
                        flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).Should().BeTrue();
                    }
                }

                using (var doc1 = new MutableDocument(id)) {
                    doc1.SetString("name", "Tiger");
                    doc1.SetBlob("blob", new Blob("text/plaintext", oddByteArray));
                    OtherDb.Save(doc1);
                    if (checkFlags) {
                        flags = doc1.c4Doc.RawDoc->flags;
                        flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).Should().BeTrue();
                    }
                }

                // Force a conflict
                using (var doc1a = Db.GetDocument(id))
                using (var doc1aMutable = doc1a.ToMutable()) {
                    var evilByteArray = new byte[] { 6, 6, 6 };

                    doc1aMutable.SetString("name", "Cat");
                    doc1aMutable.SetBlob("blob", new Blob("text/plaintext", evilByteArray));
                    Db.Save(doc1aMutable);
                    if (checkFlags) {
                        flags = doc1aMutable.c4Doc.RawDoc->flags;
                        flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).Should().BeTrue();
                    }
                }

                using (var doc1a = OtherDb.GetDocument(id))
                using (var doc1aMutable = doc1a.ToMutable()) {
                    var luckyByteArray = new byte[] { 7, 7, 7 };

                    doc1aMutable.SetString("name", "Lion");
                    doc1aMutable.SetBlob("blob", new Blob("text/plaintext", luckyByteArray));
                    OtherDb.Save(doc1aMutable);
                    if (checkFlags) {
                        flags = doc1aMutable.c4Doc.RawDoc->flags;
                        flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).Should().BeTrue();
                    }
                }
            }
        }


#if COUCHBASE_ENTERPRISE
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
            OtherDb.GetDocument("doc1").Should().BeNull("because doc1 is filtered out in the callback");
            OtherDb.GetDocument("doc2").Should().NotBeNull("because doc2 is filtered in in the callback");
            _isFilteredCallback = false;
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull, bool continuous)
        {
            var target = OtherDb;
            return CreateConfig(push, pull, continuous, target);
        }

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
            document.RevisionID.Should().NotBeNull();
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
                    new MockClientConnection(endpoint) {
                        ErrorLogic = _errorLogic,
                    };
                return retVal;
            }
        }

#endif
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

