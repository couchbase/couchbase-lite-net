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
    public class DatabaseTest : TestCase
    {
#if !WINDOWS_UWP
        public DatabaseTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestCreate()
        {
            var dir = Path.Combine(Path.GetTempPath().Replace("cache", "files"), "CouchbaseLite");
            Database.Delete("db", dir);

            var options = new DatabaseConfiguration(new DatabaseConfiguration.Builder {
                Directory = dir
            });

            try {
                var db = new Database("db", options);
                db.Dispose();
            } finally {
                Database.Delete("db", dir);
            }
        }

        [Fact]
        public void TestDelete()
        {
            var path = Db.Path;
            Directory.Exists(path).Should().BeTrue("because otherwise the database was not created");

            Db.Delete();
            Directory.Exists(path).Should().BeFalse("because otherwise the database was not deleted");
        }

        [Fact]
        public void TestCreateDocument()
        {
            using (var doc = new Document()) {
                doc.Id.Should().NotBeNullOrEmpty("because every document should have an ID immediately");
                doc.IsDeleted.Should().BeFalse("because the document is not deleted");
                doc.ToDictionary().Should().BeEmpty("because no properties have been saved yet");
            }

            var docA = new Document("doc-a");
            docA.Id.Should().Be("doc-a", "because that is the ID it was given");
            docA.IsDeleted.Should().BeFalse("because the document is not deleted");
            docA.ToDictionary().Should().BeEmpty("because no properties have been saved yet");
        }

        [Fact]
        public void TestInBatch()
        {
            Db.InBatch(() =>
            {
                for (var i = 0; i < 10; i++) {
                    var docId = $"doc{i}";
                    var doc = new Document(docId);
                    Db.Save(doc);
                }
            });

            for (var i = 0; i < 10; i++) {
                Db.GetDocument($"doc{i}").Should().NotBeNull("because otherwise the insertion in batch failed");
            }
        }
    }
}
