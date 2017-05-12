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

            var options = new DatabaseOptions  {
                Directory = dir
            };

            try {
                var db = new Database("db", options);
                db.Dispose();
            } finally {
                Database.Delete("db", dir);
            }
        }

        [Fact]
        public void TestCreateWithSpecialCharacterDBNames()
        {
            using (var db = OpenDB("`~@#$%&'()_+{}][=-.,;'")) {
                db.Name.Should().Be("`~@#$%&'()_+{}][=-.,;'", "because that is the (weird) name that was set");
                Path.GetExtension(db.Path).Should().Be(".cblite2", "because that is the current DB extension");
                db.DocumentCount.Should().Be(0UL, "because the database is empty");

                db.Delete();
            }
        }

        [Fact]
        public void TestCreateWithEmptyDBNames()
        {
            LiteCoreException e = null; ;
            try {
                OpenDB("");
            } catch (LiteCoreException ex) {
                e = ex;
                ex.Error.code.Should().Be((int)LiteCoreError.WrongFormat, "because the database cannot have an empty name");
                ex.Error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain, "because this is a LiteCore error");
            }

            e.Should().NotBeNull("because an exception is expected");
        }

        [Fact]
        public void TestCreateWithCustomDirectory()
        {
            var dir = Directory;
            Database.Delete("db", dir);
            Database.Exists("db", dir).Should().BeFalse("because it was just deleted");

            var options = DatabaseOptions.Default;
            options.Directory = dir;
            using (var db = new Database("db", options)) {
                Path.GetExtension(db.Path).Should().Be(".cblite2", "because that is the current CBL extension");
                db.Path.Should().Contain(dir, "because the directory should be present in the custom path");
                Database.Exists("db", dir).Should().BeTrue("because it was just created");
                db.DocumentCount.Should().Be(0, "because the database is empty");

                DeleteDB(db);
            }
        }

        [Fact(Skip = "Not yet implemented")]
        public void TestCreateWitHCustomConflictResolver()
        {
            
        }

        [Fact]
        public void TestGetNonExistingDocWithID()
        {
            Db.GetDocument("non-exist").Should().BeNull("because it doesn't exist");
        }

        [Fact]
        public void TestGetExistingDocWithID()
        {
            var docID = "doc1";
            GenerateDocument(docID);
            VerifyGetDocument(docID);
        }

        [Fact]
        public void TestDelete()
        {
            var path = Db.Path;
            System.IO.Directory.Exists(path).Should().BeTrue("because otherwise the database was not created");

            Db.Delete();
            System.IO.Directory.Exists(path).Should().BeFalse("because otherwise the database was not deleted");
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

        private Database OpenDB(string name)
        {
            var options = DatabaseOptions.Default;
            options.Directory = Directory;
            return new Database(name, options);
        }

        private void DeleteDB(Database db)
        {
            File.Exists(db.Path).Should().BeTrue("because the database should exist if it is going to be deleted");
            db.Delete();
            File.Exists(db.Path).Should().BeFalse("because the database should not exist anymore");
        }

        private Document GenerateDocument(string docID)
        {
            var doc = new Document(docID);
            doc.Set("key", 1);

            SaveDocument(doc);
            Db.DocumentCount.Should().Be(1UL, "because this is the first document");
            doc.Sequence.Should().Be(1UL, "because this is the first document");
            return doc;
        }

        private void VerifyGetDocument(string docID)
        {
            VerifyGetDocument(docID, 1);
        }

        private void VerifyGetDocument(string docID, int value)
        {
            VerifyGetDocument(Db, docID, value);
        }

        private void VerifyGetDocument(Database db, string docID, int value)
        {
            var doc = Db.GetDocument(docID);
            doc.Id.Should().Be(docID, "because that was the requested ID");
            doc.IsDeleted.Should().BeFalse("because the test uses a non-deleted document");
            doc.GetInt("value").Should().Be(value, "because that is the value that was passed as expected");
        }
    }
}
