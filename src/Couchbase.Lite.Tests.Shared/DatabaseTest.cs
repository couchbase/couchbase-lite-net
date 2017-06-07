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
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
using System.Linq;
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

            var options = new DatabaseConfiguration
            {
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
                Path.GetDirectoryName(db.Path).Should().EndWith(".cblite2", "because that is the current DB extension");
                db.Count.Should().Be(0UL, "because the database is empty");

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

            var options = new DatabaseConfiguration();
            options.Directory = dir;
            using (var db = new Database("db", options)) {
                Path.GetDirectoryName(db.Path).Should().EndWith(".cblite2", "because that is the current CBL extension");
                db.Path.Should().Contain(dir, "because the directory should be present in the custom path");
                Database.Exists("db", dir).Should().BeTrue("because it was just created");
                db.Count.Should().Be(0, "because the database is empty");

                DeleteDB(db);
            }
        }

        //[Fact] Not yet implemented 
        public void TestCreateWithCustomConflictResolver()
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
        public void TestGetExistingDocWithIDFromDifferentDBInstance()
        {
            var docID = "doc1";
            GenerateDocument(docID);

            using (var otherDB = OpenDB(Db.Name)) {
                otherDB.Count.Should()
                    .Be(1UL, "because the other database instance should reflect existing data");
                otherDB.DocumentExists(docID)
                    .Should()
                    .BeTrue("because the other database should know about the document");

                VerifyGetDocument(otherDB, docID);
            }
        }

        [Fact]
        public void TestGetExistingDocWithIDInBatch()
        {
            CreateDocs(10);

            Db.InBatch(() => ValidateDocs(10));
        }

        [Fact]
        public void TestGetDocFromClosedDB()
        {
            GenerateDocument("doc1");

            Db.Close();

            Db.Invoking(d => d.GetDocument("doc1"))
                .ShouldThrow<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestSaveNewDocWithID()
        {
            TestSaveNewDoc("doc1");
        }

        [Fact]
        public void TestSaveNewDocWithSpecialCharactersDocID()
        {
            TestSaveNewDoc("`~@#$%^&*()_+{}|\\][=-/.,<>?\":;'");
        }

        [Fact]
        public void TestSaveDoc()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            doc.Set("key", 2);
            SaveDocument(doc);

            Db.Count.Should().Be(1, "because a document was updated, not added");
            Db.DocumentExists(docID).Should().BeTrue("because the document still exists");

            VerifyGetDocument(docID, 2);
        }

        [Fact]
        public void TestSaveDocInDifferentDBInstance()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);
            using (var otherDB = OpenDB(Db.Name)) {
                otherDB.Count.Should()
                    .Be(1UL, "because the other database instance should reflect existing data");
                doc.Set("key", 2);
                otherDB.Invoking(d => d.Save(doc))
                    .ShouldThrow<CouchbaseLiteException>()
                    .Which.Status.Should()
                    .Be(StatusCode.Forbidden, "because a document cannot be saved into another database instance");
            }
        }

        [Fact]
        public void TestSaveDocInDifferentDB()
        {
            Database.Delete("otherDB", Directory);
            var docID = "doc1";
            var doc = GenerateDocument(docID);
            using (var otherDB = OpenDB("otherDB")) {
                otherDB.Count.Should()
                    .Be(0UL, "because the other database is empty");
                doc.Set("key", 2);
                otherDB.Invoking(d => d.Save(doc))
                    .ShouldThrow<CouchbaseLiteException>()
                    .Which.Status.Should()
                    .Be(StatusCode.Forbidden, "because a document cannot be saved into another database");
                DeleteDB(otherDB);
            }
        }

        [Fact]
        public void TestSaveSameDocTwice()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            SaveDocument(doc);

            doc.Id.Should().Be(docID, "because the doc ID should never change");
            Db.Count.Should().Be(1UL, "because there is still only one document");
        }

        [Fact]
        public void TestSaveInBatch()
        {
            Db.InBatch(() => CreateDocs(10));
            Db.Count.Should().Be(10UL, "because 10 documents were added");

            ValidateDocs(10);
        }

        [Fact]
        public void TestSaveDocToClosedDB()
        {
            Db.Close();
            var doc = new Document("doc1");
            doc.Set("key", 1);

            Db.Invoking(d => d.Save(doc))
                .ShouldThrow<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestSaveDocToDeletedDB()
        {
            DeleteDB(Db);
            var doc = new Document("doc1");
            doc.Set("key", 1);

            Db.Invoking(d => d.Save(doc))
                .ShouldThrow<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestDeletePreSaveDoc()
        {
            var doc = new Document("doc1");
            doc.Set("key", 1);

            Db.Invoking(d => d.Delete(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Which.Status.Should()
                .Be(StatusCode.NotFound, "because deleting a non-existent document is not allowed");
            Db.Count.Should().Be(0UL, "because the database should still be empty");
        }

        [Fact]
        public void TestDeleteDoc()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            Db.Delete(doc);
            Db.Count.Should().Be(0UL, "because the only document was deleted");

            doc.Id.Should().Be(docID, "because the document ID should never change");
            doc.IsDeleted.Should().BeTrue("because the document was deleted");
            doc.Sequence.Should().Be(2UL, "because the deletion is the second revision");
            doc.GetObject("key").Should().BeNull("because a deleted document has no properties");
        }

        [Fact]
        public void TestDeleteDocInDifferentDBInstance()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            using (var otherDB = OpenDB(Db.Name)) {
                otherDB.Count.Should()
                    .Be(1UL, "because the other database instance should reflect existing data");
                otherDB.Invoking(d => d.Delete(doc))
                    .ShouldThrow<CouchbaseLiteException>()
                    .Which.Status.Should()
                    .Be(StatusCode.Forbidden, "because a document cannot be deleted from another database instance");

                otherDB.Count.Should().Be(1UL, "because the delete failed");
                Db.Count.Should().Be(1UL, "because the delete failed");
                doc.IsDeleted.Should().BeFalse("because the delete failed");
            }
        }

        [Fact]
        public void TestDeleteDocInDifferentDB()
        {
            Database.Delete("otherDB", Directory);
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            using (var otherDB = OpenDB("otherDB")) {
                otherDB.Count.Should()
                    .Be(0UL, "because the other database should be empty");
                otherDB.Invoking(d => d.Delete(doc))
                    .ShouldThrow<CouchbaseLiteException>()
                    .Which.Status.Should()
                    .Be(StatusCode.Forbidden, "because a document cannot be deleted from another database");

                otherDB.Count.Should().Be(0UL, "because the database is still empty");
                Db.Count.Should().Be(1UL, "because the delete failed");
                doc.IsDeleted.Should().BeFalse("because the delete failed");

                otherDB.Delete();
            }
        }

        [Fact]
        public void TestDeleteSameDocTwice()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            Db.Delete(doc);
            Db.Count.Should().Be(0UL, "because the only document was deleted");
            doc.GetObject("key").Should().BeNull("because a deleted document has no properties");
            doc.IsDeleted.Should().BeTrue("because the document was deleted");
            doc.Sequence.Should().Be(2UL, "because the deletion is the second revision");

            // Second deletion
            Db.Delete(doc);
            Db.Count.Should().Be(0UL, "because the only document was deleted");
            doc.GetObject("key").Should().BeNull("because a deleted document has no properties");
            doc.IsDeleted.Should().BeTrue("because the document was deleted");
            doc.Sequence.Should().Be(3UL, "because the deletion is the third revision");
        }

        [Fact]
        public void TestDeleteDocInBatch()
        {
            CreateDocs(10);
            Db.InBatch(() =>
            {
                for (int i = 0; i < 10; i++) {
                    var docID = $"doc_{i:D3}";
                    var doc = Db.GetDocument(docID);
                    Db.Delete(doc);
                    doc.IsDeleted.Should().BeTrue("because the doc was just deleted");
                    doc.GetObject("key").Should().BeNull("because deleted docs have no properties");
                    Db.Count.Should().Be(9UL - (ulong)i, "because the document count should be accurate after deletion");
                }
            });

            Db.Count.Should().Be(0, "because all documents were deleted");
        }

        [Fact]
        public void TestDeleteDocOnClosedDB()
        {
            var doc = GenerateDocument("doc1");

            Db.Close();
            Db.Invoking(d => d.Delete(doc))
                .ShouldThrow<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestDeleteDocOnDeletedDB()
        {
            var doc = GenerateDocument("doc1");

            DeleteDB(Db);
            Db.Invoking(d => d.Delete(doc))
                .ShouldThrow<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestPurgePreSaveDoc()
        {
            var doc = new Document("doc1");
            doc.Set("key", 1);

            Db.Invoking(d => d.Purge(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Which.Status.Should()
                .Be(StatusCode.NotFound, "because deleting a non-existent document is not allowed");
            Db.Count.Should().Be(0UL, "because the database should still be empty");
        }

        [Fact]
        public void TestPurgeDoc()
        {
            var doc = GenerateDocument("doc1");
            PurgeDocAndVerify(doc);
            Db.Count.Should().Be(0UL, "because the only document was purged");
            SaveDocument(doc);
            doc.Sequence.Should().Be(2UL, "because it is the second entry into the database");
        }

        [Fact]
        public void TestPurgeDocInDifferentDBInstance()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            using (var otherDB = OpenDB(Db.Name)) {
                otherDB.Count.Should()
                    .Be(1UL, "because the other database instance should reflect existing data");
                otherDB.Invoking(d => d.Purge(doc))
                    .ShouldThrow<CouchbaseLiteException>()
                    .Which.Status.Should()
                    .Be(StatusCode.Forbidden, "because a document cannot be purged from another database instance");

                otherDB.Count.Should().Be(1UL, "because the delete failed");
                Db.Count.Should().Be(1UL, "because the delete failed");
                doc.IsDeleted.Should().BeFalse("because the delete failed");
            }
        }

        [Fact]
        public void TestPurgeDocInDifferentDB()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);
            Database.Delete("otherDB", Directory);

            using (var otherDB = OpenDB("otherDB")) {
                otherDB.Count.Should()
                    .Be(0UL, "because the other database should be empty");
                otherDB.Invoking(d => d.Purge(doc))
                    .ShouldThrow<CouchbaseLiteException>()
                    .Which.Status.Should()
                    .Be(StatusCode.Forbidden, "because a document cannot be purged from another database");

                otherDB.Count.Should().Be(0UL, "because the database is still empty");
                Db.Count.Should().Be(1UL, "because the delete failed");
                doc.IsDeleted.Should().BeFalse("because the delete failed");

                otherDB.Delete();
            }
        }

        [Fact]
        public void TestPurgeSameDocTwice()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            var doc1 = Db.GetDocument(docID);
            doc1.Should().NotBeNull("because the document was just created and it should exist");

            PurgeDocAndVerify(doc);
            Db.Count.Should().Be(0UL, "because the only document was purged");

            // Second purge
            PurgeDocAndVerify(doc1);
            Db.Count.Should().Be(0UL, "because the only document was purged");
        }

        [Fact]
        public void TestPurgeInBatch()
        {
            CreateDocs(10);
            Db.InBatch(() =>
            {
                for (int i = 0; i < 10; i++) {
                    var docID = $"doc_{i:D3}";
                    var doc = Db.GetDocument(docID);
                    Db.Purge(doc);
                    doc.IsDeleted.Should().BeFalse("because the doc has no more record");
                    doc.GetObject("key").Should().BeNull("because deleted docs have no properties");
                    Db.Count.Should().Be(9UL - (ulong)i, "because the document count should be accurate after deletion");
                }
            });

            Db.Count.Should().Be(0, "because all documents were purged");
        }

        [Fact]
        public void TestPurgeDocOnClosedDB()
        {
            var doc = GenerateDocument("doc1");

            Db.Close();
            Db.Invoking(d => d.Purge(doc))
                .ShouldThrow<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestPurgeDocOnDeletedDB()
        {
            var doc = GenerateDocument("doc1");

            DeleteDB(Db);
            Db.Invoking(d => d.Purge(doc))
                .ShouldThrow<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestClose()
        {
            Db.Close();
        }

        [Fact]
        public void TestCloseTwice()
        {
            Db.Close();
            Db.Close();
        }

        [Fact]
        public void TestCloseThenAccessDoc()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            Db.Close();

            doc.Id.Should().Be(docID, "because a document's ID should never change");
            doc.GetInt("key").Should().Be(1, "because the document's data should still be accessible");

            // Modification should still succeed
            doc.Set("key", 2);
            doc.Set("key1", "value");
        }

        [Fact]
        public void TestCloseThenAccessBlob()
        {
            var doc = GenerateDocument("doc1");
            StoreBlob(Db, doc, Encoding.UTF8.GetBytes("12345"));

            Db.Close();
            var blob = doc.GetBlob("data");
            blob.Should().NotBeNull("because the blob should still exist and be accessible");
            blob.Length.Should().Be(5UL, "because the blob's metadata should still be accessible");
            blob.Content.Should().BeNull("because the content cannot be read from a closed database");
        }

        [Fact]
        public void TestCloseThenGetDatabaseName()
        {
            Db.Close();
            Db.Name.Should().Be("testdb", "because the name of the database should still be accessible");
        }

        [Fact]
        public void TestCloseThenGetDatabasePath()
        {
            Db.Close();
            Db.Path.Should().BeNull("because a non-open database has no path");
        }

        [Fact]
        public void TestCloseThenCallInBatch()
        {
            Db.Invoking(d => d.InBatch(() =>
            {
                Db.Close();
            }))
            .ShouldThrow<LiteCoreException>()
            .Which.Error.Should()
            .Match<C4Error>(
                e => e.code == (int) LiteCoreError.TransactionNotClosed && e.domain == C4ErrorDomain.LiteCoreDomain,
                "because a database can't be closed in the middle of a batch");
        }

        [Fact]
        public void TestDelete()
        {
            DeleteDB(Db);
        }

        //[Fact] Known failure, possibly invalid
        public void TestDeleteTwice()
        {
            DeleteDB(Db);
            DeleteDB(Db);
        }

        [Fact]
        public void TestDeleteThenAccessDoc()
        {
            var docID = "doc1";
            var doc = GenerateDocument(docID);

            DeleteDB(Db);

            doc.Id.Should().Be(docID, "because a document's ID should never change");
            doc.GetInt("key").Should().Be(1, "because the document's data should still be accessible");

            // Modification should still succeed
            doc.Set("key", 2);
            doc.Set("key1", "value");
        }

        [Fact]
        public void TestDeleteThenAccessBlob()
        {
            var doc = GenerateDocument("doc1");
            StoreBlob(Db, doc, Encoding.UTF8.GetBytes("12345"));

            DeleteDB(Db);
            var blob = doc.GetBlob("data");
            blob.Should().NotBeNull("because the blob should still exist and be accessible");
            blob.Length.Should().Be(5UL, "because the blob's metadata should still be accessible");
            blob.Content.Should().BeNull("because the content cannot be read from a closed database");
            //TODO: TO BE CLARIFIED: Instead of returning null should a Forbidden exception be thrown?
        }

        [Fact]
        public void TestDeleteThenGetDatabaseName()
        {
            DeleteDB(Db);
            Db.Name.Should().Be("testdb", "because the name of the database should still be accessible");
        }

        [Fact]
        public void TestDeleteThenGetDatabasePath()
        {
            DeleteDB(Db);
            Db.Path.Should().BeNull("because a non-open database has no path");
        }

        [Fact]
        public void TestDeleteThenCallInBatch()
        {
            Db.Invoking(d => d.InBatch(() =>
            {
                Db.Delete();
            }))
            .ShouldThrow<LiteCoreException>()
            .Which.Error.Should()
            .Match<C4Error>(
                e => e.code == (int)LiteCoreError.TransactionNotClosed && e.domain == C4ErrorDomain.LiteCoreDomain,
                "because a database can't be closed in the middle of a batch");
        }

        [Fact]
        public void TestDeleteDBOpenByOtherInstance()
        {
            using (var otherDB = OpenDB(Db.Name)) {
                Db.Invoking(d => d.Delete())
                    .ShouldThrow<LiteCoreException>()
                    .Which.Error.Should()
                    .Match<C4Error>(e => e.code == (int) LiteCoreError.Busy &&
                                         e.domain == C4ErrorDomain.LiteCoreDomain,
                        "because an in-use database cannot be deleted");
            }
        }

        [Fact]
        public void TestDeleteByStaticMethod()
        {
            var dir = Directory;
            var options = new DatabaseConfiguration();
            options.Directory = dir;
            string path = null;
            using (var db = new Database("db", options)) {
                path = db.Path;
            }

            Database.Delete("db", dir);
            System.IO.Directory.Exists(path).Should().BeFalse("because the database was deleted");
        }

        [Fact]
        public void TestDeleteOpeningDBByStaticMethod()
        {
            var dir = Directory;
            var options = new DatabaseConfiguration();
            options.Directory = dir;
            using (var db = new Database("db", options)) {
                LiteCoreException e = null;
                try {
                    Database.Delete("db", dir);
                } catch (LiteCoreException ex) {
                    e = ex;
                    ex.Error.code.Should().Be((int) LiteCoreError.Busy, "because an in-use database cannot be deleted");
                    ex.Error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain, "because this is a LiteCore error");
                }

                e.Should().NotBeNull("because an exception is expected");
            }
        }

        [Fact]
        public void TestDeleteNonExistingDB()
        {
            // Expect no-op
            Database.Delete("notexistdb", Directory);
        }

        [Fact]
        public void TestDatabaseExistsWithDir()
        {
            var dir = Directory;
            Database.Delete("db", dir);
            Database.Exists("db", dir).Should().BeFalse("because this database has not been created");

            var options = new DatabaseConfiguration();
            options.Directory = dir;
            string path = null;
            using (var db = new Database("db", options)) {
                path = db.Path;
                Database.Exists("db", dir).Should().BeTrue("because the database is now created");
            }

            Database.Exists("db", dir).Should().BeTrue("because the database still exists after close");
            Database.Delete("db", dir);
            System.IO.Directory.Exists(path).Should().BeFalse("because the database was deleted");
            Database.Exists("db", dir).Should().BeFalse("because the database was deleted");
        }

        [Fact]
        public void TestDatabaseExistsAainstNonExistDB()
        {
            Database.Exists("nonexist", Directory).Should().BeFalse("because that DB does not exist");
        }

        //[Fact] Needs LiteCore fix
        public void TestCompact()
        {
            var docs = CreateDocs(20);

            Db.InBatch(() =>
            {
                foreach (var doc in docs) {
                    for (int i = 0; i < 25; i++) {
                        doc.Set("number", i);
                        SaveDocument(doc);
                    }
                }
            });

            foreach (var doc in docs) {
                var content = Encoding.UTF8.GetBytes(doc.Id);
                var blob = new Blob("text/plain", content);
                doc.Set("blob", blob);
                SaveDocument(doc);
            }

            Db.Count.Should().Be(20, "because that is the number of documents that were added");

            var attsDir = new DirectoryInfo(Path.Combine(Db.Path, "Attachments"));
            var atts = attsDir.EnumerateFiles();
            atts.Should().HaveCount(20, "because there should be one blob per document");

            Db.Compact();

            foreach (var doc in docs) {
                Db.Delete(doc);
                doc.IsDeleted.Should().BeTrue("because the document was just deleted");
            }

            Db.Count.Should().Be(0, "because all documents were deleted");
            Db.Compact();

            atts = attsDir.EnumerateFiles();
            atts.Should().BeEmpty("because the blobs should be collected by the compaction");
        }

        [Fact]
        public void TestCreateConfiguration()
        {
            var config1 = new DatabaseConfiguration();
            config1.Directory.Should().NotBeNullOrEmpty("because the directory should have a default value");
            config1.ConflictResolver.Should().BeNull("because it was not set");
            config1.EncryptionKey.Should().BeNull("because it was not set");

            var config1a = new DatabaseConfiguration(config1);
            config1.Directory.Should().NotBeNullOrEmpty("because the directory should have a default value");
            config1a.ConflictResolver.Should().BeNull("because it was not set");
            config1a.EncryptionKey.Should().BeNull("because it was not set");

            var resolver = new DummyResolver();
            var config2 = new DatabaseConfiguration();
            var key = new SymmetricKey("key");
            config2.Directory = "/tmp/mydb";
            config2.ConflictResolver = resolver;
            config2.EncryptionKey = key;
            config2.Directory.Should().Be("/tmp/mydb", "because that is what was set");
            config2.ConflictResolver.Should().Be(resolver, "because that is what was set");
            config2.EncryptionKey.Should().Be(key, "because that is what was set");

            var config2a = new DatabaseConfiguration(config2);
            config2.Directory.Should().Be("/tmp/mydb", "because that is what was set");
            config2.ConflictResolver.Should().Be(resolver, "because that is what was set");
            config2.EncryptionKey.Should().Be(key, "because that is what was set");
        }

        [Fact]
        public void TestGetSetConfiguration()
        {
            var config = new DatabaseConfiguration();
            using (var db = new Database("db", config))
            {
                db.Config.Should().NotBeNull("because it was set in the constructor");
                db.Config.Should().NotBeSameAs(config, "because the configuration should be copied");
                db.Config.Directory.Should().Be(config.Directory, "because the directory should be the same");
                db.Config.ConflictResolver.Should().Be(config.ConflictResolver,
                    "because the conflict resolver should be the same");
                db.Config.EncryptionKey.Should().Be(config.EncryptionKey,
                    "because the encryption key should be the same");
            }
        }

        [Fact]
        public void TestConfigurationIsCopiedWhenGetSet()
        {
            var config = new DatabaseConfiguration();
            using (var db = new Database("db", config))
            {
                config.ConflictResolver = new DummyResolver();
                db.Config.Should().NotBeNull("because it was set in the constructor");
                db.Config.Should().NotBeSameAs(config, "because the configuration should be copied");
                db.Config.Directory.Should().Be(config.Directory, "because the directory should be the same");
                db.Config.ConflictResolver.Should().NotBe(config.ConflictResolver,
                    "because the conflict resolver should be different now");
            }
        }

        private void DeleteDB(Database db)
        {
            var path = db.Path;
            System.IO.Directory.Exists(path).Should().BeTrue("because the database should exist if it is going to be deleted");
            db.Delete();
            System.IO.Directory.Exists(path).Should().BeFalse("because the database should not exist anymore");
        }

        private Document GenerateDocument(string docID)
        {
            var doc = new Document(docID);
            doc.Set("key", 1);

            SaveDocument(doc);
            Db.Count.Should().Be(1UL, "because this is the first document");
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

        private void VerifyGetDocument(Database db, string docID)
        {
            VerifyGetDocument(db, docID, 1);
        }

        private void VerifyGetDocument(Database db, string docID, int value)
        {
            var doc = Db.GetDocument(docID);
            doc.Id.Should().Be(docID, "because that was the requested ID");
            doc.IsDeleted.Should().BeFalse("because the test uses a non-deleted document");
            doc.GetInt("key").Should().Be(value, "because that is the value that was passed as expected");
        }

        private IList<Document> CreateDocs(int n)
        {
            var docs = new List<Document>();
            for (int i = 0; i < n; i++) {
                var doc = new Document($"doc_{i:D3}");
                doc.Set("key", i);
                SaveDocument(doc);
                docs.Add(doc);
            }

            Db.Count.Should().Be((ulong)n, "because otherwise an incorrect number of documents were made");
            return docs;
        }

        private void ValidateDocs(int n)
        {
            for (int i = 0; i < n; i++) {
                VerifyGetDocument($"doc_{i:D3}", i);
            }
        }

        private void TestSaveNewDoc(string docID)
        {
            GenerateDocument(docID);

            Db.Count.Should().Be(1UL, "because the database only has one document");
            Db.DocumentExists(docID).Should().BeTrue("because otherwise the wrong document is in the database");

            VerifyGetDocument(docID);
        }

        private void PurgeDocAndVerify(Document doc)
        {
            var docID = doc.Id;
            Db.Purge(doc);
            doc.Id.Should().Be(docID, "because a document's ID should never change");
            doc.Sequence.Should().Be(0UL, "because the document no longer has a database entry");
            doc.IsDeleted.Should().BeFalse("because the document has no record of deletion");
            doc.GetObject("key").Should().BeNull("because the content should now be empty");
        }

        private void StoreBlob(Database db, Document doc, byte[] content)
        {
            var blob = new Blob("text/plain", content);
            doc.Set("data", blob);
            SaveDocument(doc);
        }

        internal sealed class DummyResolver : IConflictResolver
        {
            public ReadOnlyDocument Resolve(Conflict conflict)
            {
                throw new NotImplementedException();
            }
        }
    }
}
