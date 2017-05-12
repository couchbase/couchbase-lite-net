//
//  NotificationTest.cs
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
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
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
    public class NotificationTest : TestCase
    {
#if !WINDOWS_UWP
        public NotificationTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestDatabaseNotification()
        {
            var mre = new CountdownEvent(10);
            Db.Changed += (sender, args) =>
            {
                args.Database.Should().Be(Db, "because the event should fire for the correct database");
                args.Document.Id.Should().StartWith("doc-", "because those are the documents added");
                mre.Signal();
            };

            Db.InBatch(() =>
            {
                for (uint i = 0; i < 10; i++) {
                    var doc = Db.GetDocument($"doc-{i}");
                    doc.Set("type", "demo");
                    Db.Save(doc);
                }
            });

            mre.Wait(5000).Should().BeTrue("because otherwise the events never fired");
        }
    }
}
