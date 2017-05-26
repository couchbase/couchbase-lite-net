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
        private IReplication _repl;
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
        public void TestEmptyPush()
        {
            PerformReplication(true, false);
        }

        // TODO: Figure out what to do about the current .NET limitation
        // which doesn't give any information about why a web socket failed
        // to connect (https://github.com/dotnet/corefx/issues/13773)
        //[Fact] 
        public void TestAuthenticationFailure()
        {
            _repl = Db.CreateReplication(new Uri("ws://localhost:4984/seekrit"));
            RunReplication(false, true);
            _repl.LastError.Should().BeAssignableTo<LiteCoreException>();
            _repl.LastError.As<LiteCoreException>().Error.code.Should().Be(401);
            _repl.LastError.As<LiteCoreException>().Error.domain.Should().Be(C4ErrorDomain.WebSocketDomain);
        }

        //[Fact]
        public void TestAuthenticationPullHardcoded()
        {
            _repl = Db.CreateReplication(new Uri("ws://pupshaw:frank@localhost:4984/seekrit"));
            RunReplication(false, true);
            _repl.LastError.Should().BeNull("because otherwise the authentication failed");
        }

        private void RunReplication(bool push, bool pull)
        {
            _repl.Push = push;
            _repl.Pull = pull;
            _repl.StatusChanged += ReplicationStatusChanged;
            _waitAssert = new WaitAssert();
            _repl.Start();
            _waitAssert.WaitForResult(TimeSpan.FromSeconds(50));
        }

        private void PerformReplication(bool push, bool pull)
        {
            _repl = Db.CreateReplication(_otherDB);
            RunReplication(push, pull);
        }

        private void ReplicationStatusChanged(object sender, ReplicationStatusChangedEventArgs e)
        {
            _waitAssert.RunConditionalAssert(() => e.Status.Activity == ReplicationActivityLevel.Stopped);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _otherDB.Dispose();
            _otherDB = null;
            _repl = null;
        }
    }
}
