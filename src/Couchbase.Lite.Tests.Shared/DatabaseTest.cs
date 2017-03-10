//
//  DatabaseTest.cs
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
using System.IO;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public class DatabaseTest : TestCase
    {
        public DatabaseTest(ITestOutputHelper output) : base(output)
        {

        }

        [Fact]
        public void TestCreate()
        {
            var dir = Path.Combine(Path.GetTempPath().Replace("cache", "files"), "CouchbaseLite");
            DatabaseFactory.DeleteDatabase("db", dir);

            var options = DatabaseOptions.Default;
            options.Directory = dir;

            try {
                var db = DatabaseFactory.Create("db", options);
                db.Dispose();
            } finally {
                DatabaseFactory.DeleteDatabase("db", dir);
            }
        }

        [Fact]
        public void TestDelete()
        {
            Db.DoSync(() =>
            {
                var path = Db.Path;
                Directory.Exists(path).Should().BeTrue("because otherwise the database was not created");

                Db.Delete();
                Directory.Exists(path).Should().BeFalse("because otherwise the database was not deleted");
            });
        }

        [Fact]
        public async Task TestCreateDocument()
        {
            await Db.DoAsync(() =>
            {
                var doc = Db.CreateDocument();
                doc.Id.Should().NotBeNullOrEmpty("because every document should have an ID immediately");
                doc.Database.Should().Be(Db, "because the document should know its owning database");
                doc.Exists.Should().BeFalse("because the document is not saved yet");
                doc.IsDeleted.Should().BeFalse("because the document is not deleted");
                doc.Properties.Should().BeNull("because no properties have been saved yet");
                var doc1 = Db.GetDocument("doc1");
                doc1.Id.Should().Be("doc1", "because that is the ID it was given");
                doc1.Database.Should().Be(Db, "because the document should know its owning database");
                doc1.Exists.Should().BeFalse("because the document is not saved yet");
                doc1.IsDeleted.Should().BeFalse("because the document is not deleted");
                doc1.Properties.Should().BeNull("because no properties have been saved yet");
                Db.GetDocument("doc1").Should().BeSameAs(doc1, "because the document should be cached");
            });
        }

        [Fact]
        public async Task TestDocumentExists()
        {
            await Db.DoAsync(() =>
            {
                Db.DocumentExists("doc1").Should().BeFalse("beacause the document has not been created yet");
                var doc1 = Db.GetDocument("doc1");
                doc1.Properties.Should().BeNull("because no properties were saved");
                doc1.Save();
                Db.DocumentExists("doc1").Should().BeTrue("because now the document has been created");
            });
        }

        [Theory]
        [InlineData(true)]
        //[InlineData(false)] //TODO
        public async Task TestInBatch(bool commit)
        {

             await Db.DoAsync(() =>
            {
                var success = Db.InBatch(() =>
                {
                    for(int i = 0; i < 10; i++) {
                        var docId = $"doc{i}";
                        var doc = Db.GetDocument(docId);
                        doc.Save();
                    }

                    return commit;
                });

                success.Should().BeTrue("because otherwise the batch failed");
                var dbQueue = Db.ActionQueue;
                for (int i = 0; i < 10; i++) {
                    var docId = $"doc{i}";
                    Db.DocumentExists(docId).Should().Be(commit, "because otherwise the batch didn't commit or rollback properly");
                }
            });
        }
    }
}
