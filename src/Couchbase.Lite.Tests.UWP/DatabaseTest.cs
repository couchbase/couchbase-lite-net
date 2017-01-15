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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
using Xunit;

namespace Test
{
    public class DatabaseTest : TestCase
    {
        [Fact]
        public void TestCreate()
        {
#if __UWP__
            var dir = Path.Combine(Windows.Storage.ApplicationData.Current.TemporaryFolder.Path, "CouchbaseLite");
#else
            var dir = Path.Combine(Path.GetTempPath(), "CouchbaseLite");
#endif
            Database.Delete("db", dir);

            var options = DatabaseOptions.Default;
            options.Directory = dir;

            try {
                var db = new Database("db", options);
                db.Close();
            } finally {
                Database.Delete("db", dir);
            }
        }

        [Fact]
        public void TestDelete()
        {
            var path = Db.Path;
            Directory.Exists(path).Should().BeTrue("because otherwise the database was not created");

            Db.Close();
            Db.Delete();
            Directory.Exists(path).Should().BeFalse("because otherwise the database was not deleted");
        }

        [Fact]
        public void TestCreateDocument()
        {
            var doc = Db.GetDocument();
            doc.Id.Should().NotBeNullOrEmpty("because every document should have an ID immediately");
            doc.Database.Should().Be(Db, "because the document should know its owning database");
            doc.Exists.Should().BeFalse("because the document is not saved yet");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.Properties.Should().BeEmpty("because no properties have been saved yet");

            var doc1 = Db.GetDocument("doc1");
            doc1.Id.Should().Be("doc1", "because that is the ID it was given");
            doc1.Database.Should().Be(Db, "because the document should know its owning database");
            doc1.Exists.Should().BeFalse("because the document is not saved yet");
            doc1.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc1.Properties.Should().BeEmpty("because no properties have been saved yet");
            Db.GetDocument("doc1").Should().BeSameAs(doc1, "because the document should be cached");
        }

        [Fact]
        public void TestDocumentExists()
        {
            Db.Exists("doc1").Should().BeFalse("beacause the document has not been created yet");
            var doc1 = Db.GetDocument("doc1");
            doc1.Save();
            Db.Exists("doc1").Should().BeTrue("because now the document has been created");
            doc1.Properties.Should().BeEmpty("because no properties were saved");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestInBatch(bool commit)
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
            for(int i = 0; i < 10; i++) {
                var docId = $"doc{i}";
                Db.Exists(docId).Should().Be(commit, "because otherwise the batch didn't commit or rollback properly");
            }
        }
    }
}
