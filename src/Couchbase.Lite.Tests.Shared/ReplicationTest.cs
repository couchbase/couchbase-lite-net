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
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Sync;
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
    public sealed class ReplicationTest : TestCase
    {
        private Database _otherDB;
        private Replicator _repl;
        private bool _stopped;
        private WaitAssert _waitAssert;

#if !WINDOWS_UWP
        public ReplicationTest(ITestOutputHelper output) : base(output)
#else
        public ReplicationTest()
#endif
        {
            _otherDB = OpenDB("otherdb");
        }

        [Fact]
        public void TestBadUrl()
        {
            var config = CreateConfig(false, true, new Uri("blxp://localhost/db"));
            RunReplication(config, 15, C4ErrorDomain.LiteCoreDomain);
        }

        [Fact]
        public void TestEmptyPush()
        {
            var config = CreateConfig(true, false);
            RunReplication(config, 0, 0);
        }

        [Fact]
        public void TestLocalPush()
        {
            Db.InBatch(() =>
            {
                for (int i = 0; i < 100; i++) {
                    var doc = new Document();
                    Db.Save(doc);
                }
            });

            var config = CreateConfig(true, false);
            RunReplication(config, 0, 0);
        }

        [Fact]
        public void TestPullDoc()
        {
            // For https://github.com/couchbase/couchbase-lite-core/issues/156
            var doc1 = new Document("doc1");
            doc1.Set("name", "Tiger");
            Db.Save(doc1);
            Db.Count.Should().Be(1, "because only one document was saved so far");

            var doc2 = new Document("doc2");
            doc2.Set("name", "Cat");
            _otherDB.Save(doc2);

            var config = CreateConfig(false, true);
            RunReplication(config, 0, 0);

            Db.Count.Should().Be(2, "because the replicator should have pulled doc2 from the other DB");
            doc2.GetString("name").Should().Be("Cat");
        }

        // The below tests are disabled because they require orchestration and should be moved
        // to the functional test suite
#if HAVE_SG
        [Fact] 
#endif
        public void TestAuthenticationFailure()
        {
            var config = CreateConfig(false, true, new Uri("blip://localhost:4984/seekrit"));
            _repl = new Replicator(config);
            RunReplication(config, 401, C4ErrorDomain.WebSocketDomain);
        }

#if HAVE_SG
        [Fact] 
#endif
        public void TestAuthenticationPullHardcoded()
        {
            var config = CreateConfig(false, true, new Uri("blip://pupshaw:frank@localhost:4984/seekrit"));
            RunReplication(config, 0, 0);
        }

#if HAVE_SG
        [Fact] 
#endif
        public void TestAuthenticatedPull()
        {
            var config = CreateConfig(false, true, new Uri("blip://localhost:4984/seekrit"));
            config.Authenticator = new SessionAuthenticator("78376efd8cc74dadfc395f4049a115b7cd0ef5e3", null,
                "SyncGatewaySession");
            RunReplication(config, 0, 0);
        }

#if HAVE_SG
        [Fact]
#endif
        public void TestSelfSignedSSLFailure()
        {
            var config = CreateConfig(false, true, new Uri("blips://localhost:4984/db"));
            RunReplication(config, (int)C4NetworkErrorCode.TLSCertUntrusted, C4ErrorDomain.NetworkDomain);
        }

#if HAVE_SG
        [Fact]
#endif
        public async Task TestSelfSignedSSLPinned()
        {
            var config = CreateConfig(false, true, new Uri("blips://localhost:4984/db"));
#if WINDOWS_UWP
            var installedLocation = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var file = await installedLocation.GetFileAsync("Assets\\localhost-wrong.cert");
            var bytes = File.ReadAllBytes(file.Path);
            config.Options.PinnedServerCertificate = new X509Certificate2(bytes);
#else
            config.Options.PinnedServerCertificate = new X509Certificate2("localhost-wrong.cert");
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
                    using (var doc = new Document($"doc-{i}")) {
                        doc["foo"].Value = "bar";
                        Db.Save(doc);
                    }
                }

                for (int i = 0; i < 10; i++) {
                    using (var doc = new Document($"doc-{i+5}")) {
                        doc["channels"].Value = "my_channel";
                        Db.Save(doc);
                    }
                }
            });

            
            var config = CreateConfig(true, false, new Uri("blip://localhost:4984/db"));
            RunReplication(config, 0, 0);

            config = new ReplicatorConfiguration(Db, new Uri("blip://localhost:4984/db"));
            ModifyConfig(config, false, true);
            config.Options.Channels = new[] {"my_channel"};
            RunReplication(config, 0, 0);
            _otherDB.Count.Should().Be(10, "because 10 documents should be in the given channel");
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull)
        {
            var target = _otherDB;
            return CreateConfig(push, pull, target);
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull, Uri url)
        {
            var retVal = new ReplicatorConfiguration(Db, url);
            return ModifyConfig(retVal, push, pull);
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull, Database target)
        {
            var retVal = new ReplicatorConfiguration(Db, target);
            return ModifyConfig(retVal, push, pull);
        }

        private ReplicatorConfiguration ModifyConfig(ReplicatorConfiguration config, bool push, bool pull)
        {
            var type = (ReplicatorType)0;
            if (push) {
                type |= ReplicatorType.Push;
            }

            if (pull) {
                type |= ReplicatorType.Pull;
            }

            config.ReplicatorType = type;
            return config;
        }

        private void RunReplication(ReplicatorConfiguration config, int expectedErrCode, C4ErrorDomain expectedErrDomain)
        {
            _repl = new Replicator(config);
            _waitAssert = new WaitAssert();
            _repl.StatusChanged += (sender, args) =>
            {
                if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    _waitAssert.RunAssert(() =>
                    {
                        if (expectedErrCode != 0) {
                            args.LastError.Should().BeAssignableTo<LiteCoreException>();
                            var error = args.LastError.As<LiteCoreException>().Error;
                            error.code.Should().Be(expectedErrCode);
                            if ((int) expectedErrDomain != 0) {
                                error.domain.As<C4ErrorDomain>().Should().Be(expectedErrDomain);
                            }
                        } else {
                            args.LastError.Should().BeNull("because otherwise an unexpected error occurred");
                        }
                    });
                }
            };
            
            _repl.Start();
            try {
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(10));
            } catch {
                _repl.Stop();
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _otherDB.Delete();
            _otherDB = null;
            _repl = null;
        }
    }
}
