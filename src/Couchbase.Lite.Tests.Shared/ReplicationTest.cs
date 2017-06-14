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
using System.Text;
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

        // The below tests are disabled because they require orchestration and should be moved
        // to the functional test suite
        //[Fact] 
        public void TestAuthenticationFailure()
        {
            var config = CreateConfig(false, true, new Uri("blip://localhost:4984/seekrit"));
            _repl = new Replicator(config);
            RunReplication(config, 401, C4ErrorDomain.WebSocketDomain);
        }

        //[Fact]
        public void TestAuthenticationPullHardcoded()
        {
            var config = CreateConfig(false, true, new Uri("blip://pupshaw:frank@localhost:4984/seekrit"));
            RunReplication(config, 0, 0);
        }

        //[Fact]
        public void TestAuthenticatedPull()
        {
            var config = CreateConfig(false, true, new Uri("blip://localhost:4984/seekrit"));
            config.Options = new ReplicatorOptionsDictionary {
                Auth = new AuthOptionsDictionary {
                    Username = "pupshaw",
                    Password = "frank"
                }
            };

            RunReplication(config, 0, 0);
        }

        //[Fact]
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

            
            var config = CreateConfig(true, false, new ReplicatorTarget(new Uri("blip://localhost:4984/db")));
            RunReplication(config, 0, 0);

            config = CreateConfig(false, true, new ReplicatorTarget(new Uri("blip://localhost:4984/db")));
            config.Options.Channels = new[] {"my_channel"};
            config.Database = _otherDB; 
            RunReplication(config, 0, 0);
            _otherDB.Count.Should().Be(10, "because 10 documents should be in the given channel");
        }

        //[Fact]
        public void TestSecurePull()
        {
            var config = CreateConfig(false, true, new Uri("blips://localhost:4984/db"));
            RunReplication(config, 0, 0);
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull)
        {
            var target = new ReplicatorTarget(_otherDB);
            return CreateConfig(push, pull, target);
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull, Uri url)
        {
            return CreateConfig(push, pull, new ReplicatorTarget(url));
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull, ReplicatorTarget target)
        {
            var type = (ReplicatorType) 0;
            if (push) {
                type |= ReplicatorType.Push;
            }

            if (pull) {
                type |= ReplicatorType.Pull;
            }

            return new ReplicatorConfiguration {
                Database = Db,
                Target = target,
                ReplicatorType = type
            };
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
            _waitAssert.WaitForResult(TimeSpan.FromSeconds(10));
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
