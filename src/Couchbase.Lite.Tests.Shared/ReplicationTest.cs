//
//  ReplicationTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
            ConflictResolver = new MergeThenTheirsWins();
            ReopenDB();
            _otherDB = OpenDB("otherdb");
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
                Db.Save(doc1).Dispose();
                Db.Count.Should().Be(1UL);

                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2).Dispose();
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
                Db.Save(doc1).Dispose();
                Db.Count.Should().Be(1UL);

                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2).Dispose();
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
                Db.Save(doc1).Dispose();
                Db.Count.Should().Be(1, "because only one document was saved so far");
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2).Dispose();
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
                Db.Save(doc1).Dispose();
                Db.Count.Should().Be(1, "because only one document was saved so far");
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                _otherDB.Save(doc2).Dispose();
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
        public void TestPullConflict()
        {
            var doc1 = new MutableDocument("doc");
            doc1.SetString("species", "Tiger");
            Db.Save(doc1).Dispose();

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);

            doc1.Dispose();
            doc1 = Db.GetDocument("doc")?.ToMutable();
            doc1.Should().NotBeNull();
            doc1.SetString("name", "Hobbes");
            Db.Save(doc1).Dispose();
            doc1.Dispose();

            var doc2 = _otherDB.GetDocument("doc")?.ToMutable();
            doc2.Should().NotBeNull();
            doc2.SetString("pattern", "striped");
            _otherDB.Save(doc2).Dispose();
            doc2.Dispose();

            config = CreateConfig(false, true, false);
            config.ConflictResolver = new MergeThenTheirsWins
            {
                RequireBaseRevision = true
            };

            RunReplication(config, 0, 0);
            Db.Count.Should().Be(1, "because the document should go through the conflict handler");
            var gotDoc1 = Db.GetDocument("doc");
            gotDoc1.ShouldBeEquivalentTo(new Dictionary<string, object>
            {
                ["species"] = "Tiger",
                ["name"] = "Hobbes",
                ["pattern"] = "striped"
            });
            gotDoc1.Dispose();;
        }

        [Fact]
        public void TestDocIDFilter()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("species", "Tiger");
            var saved = Db.Save(doc1);
            Misc.SafeSwap(ref doc1, saved.ToMutable());
            doc1.SetString("name", "Hobbes");
            Db.Save(doc1);

             var doc2 = new MutableDocument("doc2");
            doc2.SetString("species", "Tiger");
            saved = Db.Save(doc2);
            Misc.SafeSwap(ref doc2, saved.ToMutable());
            doc2.SetString("pattern", "striped");
            Db.Save(doc2);

            var doc3 = new MutableDocument("doc3");
            doc3.SetString("species", "Tiger");
            saved = _otherDB.Save(doc3);
            Misc.SafeSwap(ref doc3, saved.ToMutable());
            doc3.SetString("name", "Hobbes");
            _otherDB.Save(doc3);

            var doc4 = new MutableDocument("doc4");
            doc4.SetString("species", "Tiger");
            saved = _otherDB.Save(doc4);
            Misc.SafeSwap(ref doc4, saved.ToMutable());
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
        public void TestPullConflictNoBaseRevision()
        {
            // Create the conflicting docs separately in each database.  They have the same base revID
            // because the contents are identical, but because the DB never pushed revision 1, it doesn't
            // think it needs to preserve the body; so when it pulls a conflict, there won't be a base
            // revision for the resolver.

            var doc1 = new MutableDocument("doc");
            doc1.SetString("species", "tiger");
            var saved = Db.Save(doc1);
            Misc.SafeSwap(ref doc1, saved.ToMutable());
            doc1.SetString("name", "Hobbes");
            Db.Save(doc1);

            var doc2 = new MutableDocument("doc");
            doc2.SetString("species", "Tiger");
            saved = _otherDB.Save(doc2);
            Misc.SafeSwap(ref doc2, saved.ToMutable());
            doc2.SetString("pattern", "striped");
            _otherDB.Save(doc2);

            var config = CreateConfig(false, true, false);
            config.ConflictResolver = new MergeThenTheirsWins();
            RunReplication(config, 0, 0);

            Db.Count.Should().Be(1, "because the document in otherDB has the same ID");
            var gotDoc1 = Db.GetDocument("doc");
            gotDoc1.ToDictionary().ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["species"] = "Tiger",
                ["name"] = "Hobbes",
                ["pattern"] = "striped"
            });
        }

        [Fact]
        public async Task TestReplicatorStopWhenClosed()
        {
            var config = CreateConfig(true, true, true);
            using (var repl = new Replicator(config.Build())) {
                repl.Start();
                while (repl.Status.Activity != ReplicatorActivityLevel.Idle) {
                    WriteLine($"Replication status still {repl.Status.Activity}, waiting for idle...");
                    await Task.Delay(500);
                }

                ReopenDB();

                var attemptCount = 0;
                while (attemptCount++ < 10 && repl.Status.Activity != ReplicatorActivityLevel.Stopped) {
                    WriteLine(
                        $"Replication status still {repl.Status.Activity}, waiting for stopped (remaining attempts {10 - attemptCount})...");
                    await Task.Delay(500);
                }

                attemptCount.Should().BeLessOrEqualTo(10);
            }
        }
        
        [Fact]
        public void TestStopContinuousReplicator()
        {
            var config = CreateConfig(true, false, true);
            using (var r = new Replicator(config.Build())) {
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

        // The below tests are disabled because they require orchestration and should be moved
        // to the functional test suite
#if HAVE_SG
        [Fact] 
#endif
        public void TestAuthenticationFailure()
        {
            var config = CreateConfig(false, true, false, new URLEndpoint(new Uri("ws://localhost/seekrit")));
            _repl = new Replicator(config.Build());
            RunReplication(config, 401, C4ErrorDomain.WebSocketDomain);
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
            RunReplication(config, (int)C4NetworkErrorCode.TLSCertUntrusted, C4ErrorDomain.NetworkDomain);
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

            config = new ReplicatorConfiguration.Builder(_otherDB, new URLEndpoint(new Uri("ws://localhost/db")));
            ModifyConfig(config, false, true, false);
            config.Channels = new[] {"my_channel"};
            RunReplication(config, 0, 0);
            _otherDB.Count.Should().Be(10, "because 10 documents should be in the given channel");
        }

        private ReplicatorConfiguration.Builder CreateConfig(bool push, bool pull, bool continuous)
        {
            var target = _otherDB;
            return CreateConfig(push, pull, continuous, target);
        }

        private ReplicatorConfiguration.Builder CreateConfig(bool push, bool pull, bool continuous, URLEndpoint endpoint)
        {
            var retVal = new ReplicatorConfiguration.Builder(Db, endpoint);
            return ModifyConfig(retVal, push, pull, continuous);
        }

        private ReplicatorConfiguration.Builder CreateConfig(bool push, bool pull, bool continuous, Database target)
        {
            var retVal = new ReplicatorConfiguration.Builder(Db, new DatabaseEndpoint(target));
            return ModifyConfig(retVal, push, pull, continuous);
        }

        private ReplicatorConfiguration.Builder ModifyConfig(ReplicatorConfiguration.Builder config, bool push, bool pull, bool continuous)
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

        private void VerifyChange(ReplicatorStatusChangedEventArgs change, int errorCode, C4ErrorDomain domain)
        {
            var s = change.Status;
            WriteLine($"---Status: {s.Activity} ({s.Progress.Completed} / {s.Progress.Total}), lastError = {s.Error}");
            if (s.Activity == ReplicatorActivityLevel.Stopped) {
                if (errorCode != 0) {
                    s.Error.Should().BeAssignableTo<LiteCoreException>();
                    var error = s.Error.As<LiteCoreException>().Error;
                    error.code.Should().Be(errorCode);
                    if ((int) domain != 0) {
                        error.domain.As<C4ErrorDomain>().Should().Be(domain);
                    }
                } else {
                    s.Error.Should().BeNull("because otherwise an unexpected error occurred");
                }
            }
        }

        private void RunReplication(ReplicatorConfiguration.Builder config, int expectedErrCode, C4ErrorDomain expectedErrDomain)
        {
            Misc.SafeSwap(ref _repl, new Replicator(config.Build()));
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
