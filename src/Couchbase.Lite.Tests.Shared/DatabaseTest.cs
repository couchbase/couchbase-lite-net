//
//  DatabaseTest.cs
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
using System.Threading;

using Couchbase.Lite.Query;
using Couchbase.Lite.Logging;
using Couchbase.Lite.DI;
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
                options = db.Config;
                db.Dispose();
            } finally {
                Database.Delete("db", dir);
            }

            #if COUCHBASE_ENTERPRISE
            options.Invoking(o => o.EncryptionKey = new EncryptionKey("foo")).Should().Throw<InvalidOperationException>("because the configuration is in use");
            #endif
        }

        [Fact]
        public void TestCreateWithDefaultConfiguration()
        {
            using (var db = new Database("db", new DatabaseConfiguration())) {
                db.Count.Should().Be(0);
                DeleteDB(db);
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
            CouchbaseLiteException e = null; ;
            try {
                OpenDB("");
            } catch (CouchbaseLiteException ex) {
                e = ex;
                ex.Error.Should().Be(CouchbaseLiteError.WrongFormat, "because the database cannot have an empty name");
                ex.Domain.Should().Be(CouchbaseLiteErrorType.CouchbaseLite, "because this is a LiteCore error");
                var exception = new CouchbaseLiteException(C4ErrorCode.NotFound, "database cannot have an empty name", ex);
            }

            e.Should().NotBeNull("because an exception is expected");
        }

        [Fact]
        public void TestCreateWithCustomDirectory()
        {
            var dir = Directory;
            Database.Delete("db", dir);
            Database.Exists("db", dir).Should().BeFalse("because it was just deleted");

            var options = new DatabaseConfiguration
                { Directory = dir };
            using (var db = new Database("db", options)) {
                Path.GetDirectoryName(db.Path).Should().EndWith(".cblite2", "because that is the current CBL extension");
                db.Path.Should().Contain(dir, "because the directory should be present in the custom path");
                Database.Exists("db", dir).Should().BeTrue("because it was just created");
                db.Count.Should().Be(0, "because the database is empty");

                DeleteDB(db);
            }
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
        public void TestSaveAndGetMultipleDocs()
        {
            const int NumDocs = 10;
            for (var i = 0; i < NumDocs; i++) {
                using (var doc = new MutableDocument($"doc_{i:D3}")) {
                    doc.SetInt("key", i);
                    Db.Save(doc);
                }
            }

            Db.Count.Should().Be(NumDocs);
            ValidateDocs(NumDocs);
        }

        [Fact]
        public void TestGetExistingDocWithIDFromDifferentDBInstance()
        {
            var docID = "doc1";
            GenerateDocument(docID);

            using (var otherDB = OpenDB(Db.Name)) {
                otherDB.Count.Should()
                    .Be(1UL, "because the other database instance should reflect existing data");

                VerifyGetDocument(otherDB, docID);
            }
        }

        [Fact]
        public void TestGetExistingDocWithIDInBatch()
        {
            var docs = CreateDocs(10);

            Db.InBatch(() => ValidateDocs(10));

            foreach(var doc in docs) {
                doc.Dispose();
            }
        }

        [Fact]
        public void TestGetDocFromClosedDB()
        {
            using(var doc = GenerateDocument("doc1")) {

                Db.Close();

                Db.Invoking(d => d.GetDocument("doc1"))
                    .Should().Throw<InvalidOperationException>()
                    .WithMessage("Attempt to perform an operation on a closed database.",
                        "because this operation is invalid");
            }
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
            using(var doc = GenerateDocument(docID).ToMutable()) {
                doc.SetInt("key", 2);
                Db.Save(doc);

                Db.Count.Should().Be(1, "because a document was updated, not added");

                VerifyGetDocument(docID, 2);
            }
        }

        [Fact]
        public void TestSaveDocInDifferentDBInstance()
        {
            var docID = "doc1";
            using (var doc = GenerateDocument(docID).ToMutable())
            using (var otherDB = OpenDB(Db.Name)) {
                otherDB.Count.Should()
                    .Be(1UL, "because the other database instance should reflect existing data");
                doc.SetInt("key", 2);
                otherDB.Invoking(d => d.Save(doc))
                    .Should().Throw<CouchbaseLiteException>()
                    .Where(
                        e => e.Error == CouchbaseLiteError.InvalidParameter &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                        "because a document cannot be saved into another database instance");
            }
        }

        [Fact]
        public void TestSaveDocInDifferentDB()
        {
            Database.Delete("otherDB", Directory);
            var docID = "doc1";
            using (var doc = GenerateDocument(docID).ToMutable())
            using (var otherDB = OpenDB("otherDB")) {
                otherDB.Count.Should()
                    .Be(0UL, "because the other database is empty");
                doc.SetInt("key", 2);
                otherDB.Invoking(d => d.Save(doc))
                    .Should().Throw<CouchbaseLiteException>()
                    .Where(
                        e => e.Error == CouchbaseLiteError.InvalidParameter &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                        "because a document cannot be saved into another database");
                DeleteDB(otherDB);
            }
        }

        [Fact]
        public void TestSaveSameDocTwice()
        {
            var docID = "doc1";
            using(var doc = GenerateDocument(docID).ToMutable()) {
                Db.Save(doc);
                doc.Id.Should().Be(docID, "because the doc ID should never change");
                Db.Count.Should().Be(1UL, "because there is still only one document");
            }
        }

        [Fact]
        public void TestSaveInBatch()
        {
            Db.InBatch(() => CreateDocs(10));
            Db.Count.Should().Be(10UL, "because 10 documents were added");

            ValidateDocs(10);
        }

        [Fact]
        public void TestSaveConcurrencyControl()
        {
            using(var doc1a = new MutableDocument("doc1"))
            using (var doc1b = new MutableDocument("doc1")) {
                doc1a.SetString("name", "Jim");
                Db.Save(doc1a);
                doc1b.SetString("name", "Tim");
                Db.Save(doc1b, ConcurrencyControl.FailOnConflict).Should()
                    .BeFalse("beacuse a conflict should not be allowed in this mode");
                using (var gotDoc = Db.GetDocument(doc1a.Id)) {
                    gotDoc.GetString("name").Should().Be("Jim");
                }

                Db.Save(doc1b, ConcurrencyControl.LastWriteWins);
                using (var gotDoc = Db.GetDocument(doc1a.Id)) {
                    gotDoc.GetString("name").Should().Be("Tim");
                }
            }
        }

        [Fact]
        public void TestConflictHandlerSaveMergedDocument()
        {
            using (var doc1 = new MutableDocument("doc1")){
                doc1.SetString("name", "Jim");
                
                Db.Save(doc1);

                // Get two doc1 document objects (doc1a and doc1b):
                var doc1a = Db.GetDocument(doc1.Id).ToMutable();
                var doc1b = Db.GetDocument(doc1.Id).ToMutable();

                // Modify doc1a:
                doc1a.SetString("name", "Jim");
                doc1a.SetString("language", "English");
                        
                Db.Save(doc1a);

                // Modify doc1b:
                doc1b.SetString("name", "Jim");
                doc1b.SetString("language", "C#");
                doc1b.SetString("location", "Japan");        
                Db.Save(doc1b, ResolveConflict);
            }

            using (var doc1 = Db.GetDocument("doc1")) {
                doc1.GetString("name").Should().Be("Jim");
                var lanStr = doc1.GetString("language");
                lanStr.Should().Contain("English");
                lanStr.Should().Contain("C#");
                doc1.GetString("location").Should().Be("Japan");
            }
        }

        [Fact]
        public void TestConflictHandlerWhenDocumentIsPurged()
        {
            using (var doc = new MutableDocument("doc1")) {
                doc.SetString("firstName", "Tiger");
                Db.Save(doc);
            }

            using (var doc = Db.GetDocument("doc1")) {
                doc.Generation.Should().Be(1);
            }

            using (var doc1 = Db.GetDocument("doc1"))
            using (var doc1b = doc1.ToMutable()) {
                Db.Purge("doc1");
                doc1b.SetString("nickName", "Scott");
                Db.Invoking(d => Db.Save(doc1b, (updated, current) =>
                {
                    return true;
                })).Should().Throw<CouchbaseLiteException>()
                    .Where(
                        e => e.Error == CouchbaseLiteError.NotFound &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                        "because the document is purged");
            }
        }

        [Fact]
        public void TestConflictHandlerReturnsTrue()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Jim");
                Db.Save(doc1, (updated, current) => {
                    return true;
                });

                Db.GetDocument("doc1").GetString("name").Should().Be("Jim");

                var doc1a = new MutableDocument(doc1.Id);
                doc1a.SetString("name", "Kim");
                Db.Save(doc1a, (updated, current) => {
                    return true;
                });

                Db.GetDocument("doc1").GetString("name").Should().Be("Kim");
            }
        }

        [Fact]
        public void TestConflictHandlerReturnsFalse()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Jim");
                Db.Save(doc1, (updated, current) => {
                    return false;
                });

                Db.GetDocument(doc1.Id).GetString("name").Should().Be("Jim");

                var doc1a = new MutableDocument(doc1.Id);
                doc1a.SetString("name", "Kim");
                Db.Save(doc1a, (updated, current) => {
                    return false;
                });

                Db.GetDocument("doc1").GetString("name").Should().Be("Jim");
            }
        }

        [Fact]
        public void TestConflictHandlerWithMultipleIncomingConflicts()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc1a = new MutableDocument("doc1"))
            using (var doc1b = new MutableDocument("doc1")) {
                var waitObj = new AutoResetEvent(true);
                doc1.SetString("name", "Jim");
                Db.Save(doc1);
                Task task2 = Task.Factory.StartNew(() => {
                    doc1a.SetString("name", "Kim");
                    Db.Save(doc1a, (updated, current) => {
                        waitObj.Set();
                        Thread.Sleep(250);
                        waitObj.WaitOne(TimeSpan.FromMilliseconds(250));
                        return false;
                    });
                    waitObj.Set();
                    Thread.Sleep(250);
                });
                waitObj.WaitOne(TimeSpan.FromMilliseconds(250));
                doc1b.SetString("name", "Tim");
                Db.Save(doc1b);
                Db.GetDocument("doc1").GetString("name").Should().Be("Tim");
                waitObj.Set();
                Thread.Sleep(250);
                waitObj.WaitOne(TimeSpan.FromMilliseconds(250));
                Db.GetDocument("doc1").GetString("name").Should().Be("Tim");
            }
        }

        [Fact]
        public void TestConflictHandlerWithDeletedOldDoc()
        {
            using (var doc1 = new MutableDocument("doc1")){
                doc1.SetString("name", "Jim");
                Db.Save(doc1);

                var doc1a = Db.GetDocument("doc1").ToMutable();
                doc1a.SetString("name", "Kim");

                Document currDoc = null;
                var updatedDocName = "";

                //delete old doc
                Db.Delete(doc1);
                Db.Save(doc1a, (updated, current) =>
                {
                    currDoc = current;
                    updatedDocName = updated.GetString("name");
                    return true;
                });

                currDoc.Should().BeNull();
                updatedDocName.Should().Be("Kim");
                Db.GetDocument("doc1").GetString("name").Should().Be("Kim");
            }
        }

        [Fact]
        public void TestSaveDocToClosedDB()
        {
            Db.Close();
            var doc = new MutableDocument("doc1");
            doc.SetInt("key", 1);

            Db.Invoking(d => d.Save(doc))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database.",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestSaveDocToDeletedDB()
        {
            DeleteDB(Db);
            var doc = new MutableDocument("doc1");
            doc.SetInt("key", 1);

            Db.Invoking(d => d.Save(doc))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database.",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestDeletePreSaveDoc()
        {
            var doc = new MutableDocument("doc1");
            doc.SetInt("key", 1);

            Db.Invoking(d => d.Delete(doc))
                .Should().Throw<CouchbaseLiteException>()
                .Where(
                    e => e.Error == CouchbaseLiteError.NotFound &&
                         e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                    "because deleting an unsaved document is not allowed");
            Db.Count.Should().Be(0UL, "because the database should still be empty");
        }

        [Fact]
        public void TestDeleteDoc()
        {
            var docID = "doc1";
            using (var doc = GenerateDocument(docID)) {

                Db.Delete(doc);
                Db.Count.Should().Be(0UL, "because the only document was deleted");
            }

            var gotDoc = Db.GetDocument(docID);
            gotDoc.Should().BeNull("because the document was deleted");
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
                    .Should().Throw<CouchbaseLiteException>()
                    .Where(
                        e => e.Error == CouchbaseLiteError.InvalidParameter &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                        "because a document cannot be deleted from another database instance");

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
                    .Should().Throw<CouchbaseLiteException>()
                    .Where(
                        e => e.Error == CouchbaseLiteError.InvalidParameter &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                        "because a document cannot be deleted from another database");

                otherDB.Count.Should().Be(0UL, "because the database is still empty");
                Db.Count.Should().Be(1UL, "because the delete failed");
                doc.IsDeleted.Should().BeFalse("because the delete failed");

                otherDB.Delete();
            }
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
                .Should().Throw<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database.",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestDeleteDocOnDeletedDB()
        {
            var doc = GenerateDocument("doc1");

            DeleteDB(Db);
            Db.Invoking(d => d.Delete(doc))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database.",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestPurgePreSaveDoc()
        {
            var doc = new MutableDocument("doc1");
            doc.SetInt("key", 1);

            Db.Invoking(db => db.Purge(doc)).Should().Throw<CouchbaseLiteException>()
                .Where(e => e.Error == CouchbaseLiteError.NotFound);

            Db.Count.Should().Be(0UL, "because the database should still be empty");
        }

        [Fact]
        public void TestPurgeDoc()
        {
            var doc = GenerateDocument("doc1");

            PurgeDocAndVerify(doc);
            Db.Count.Should().Be(0UL, "because the only document was purged");
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
                    .Should().Throw<CouchbaseLiteException>()
                    .Where(
                        e => e.Error == CouchbaseLiteError.InvalidParameter &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite, "because a document cannot be purged from another database instance");

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
                    .Should().Throw<CouchbaseLiteException>()
                    .Where(
                        e => e.Error == CouchbaseLiteError.InvalidParameter &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite, "because a document cannot be purged from another database");

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

            // Second purge and throw error
            Db.Invoking(db => db.Purge(doc)).Should().Throw<CouchbaseLiteException>().Where(e =>
                e.Error == CouchbaseLiteError.NotFound && e.Domain == CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact]
        public void TestPurgeDocInBatch()
        {
            CreateDocs(10);
            Db.InBatch(() =>
            {
                for (int i = 0; i < 10; i++) {
                    var docID = $"doc_{i:D3}";
                    var doc = Db.GetDocument(docID);
                    PurgeDocAndVerify(doc);
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
                .Should().Throw<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database.",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestPurgeDocOnDeletedDB()
        {
            var doc = GenerateDocument("doc1");

            DeleteDB(Db);
            Db.Invoking(d => d.Purge(doc))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("Attempt to perform an operation on a closed database.",
                    "because this operation is invalid");
        }

        [Fact]
        public void TestClose()
        {
            Thread.Sleep(1500);
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
            var updatedDoc = doc.ToMutable();
            updatedDoc.SetInt("key", 2);
            updatedDoc.SetString("key1", "value");
        }

        [Fact]
        public void TestCloseThenAccessBlob()
        {
            using (var doc = GenerateDocument("doc1")) {
                var savedDoc = StoreBlob(Db, doc, Encoding.UTF8.GetBytes("12345"));

                Db.Close();
                var blob = savedDoc.GetBlob("data");
                blob.Should().NotBeNull("because the blob should still exist and be accessible");
                blob.Length.Should().Be(5, "because the blob's metadata should still be accessible");
                blob.Content.Should().BeNull("because the content cannot be read from a closed database");
            }
        }

        [Fact]
        public void TestCloseThenGetDatabaseName()
        {
            var name = Db.Name;
            Db.Close();
            Db.Name.Should().Be(name, "because the name of the database should still be accessible");
        }

        [Fact]
        public void TestCloseThenGetDatabasePath()
        {
            Db.Close();
            Db.Path.Should().BeNull("because a non-open database has no path");
        }

        [Fact]
        public void TestCloseThenDeleteDatabase()
        {
            Db.Dispose();
            Db.Invoking(DeleteDB).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TestCloseThenCallInBatch()
        {
            Db.Invoking(d => d.InBatch(() =>
            {
                Db.Close();
            }))
            .Should().Throw<CouchbaseLiteException>()
            .Where(
                e => e.Error == CouchbaseLiteError.TransactionNotClosed && e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                "because a database can't be closed in the middle of a batch");
        }

        [Fact]
        public void TestDelete()
        {
            DeleteDB(Db);
        }

        [Fact]
        public void TestDeleteTwice()
        {
            Db.Delete();
            Db.Invoking(d => d.Delete()).Should().Throw<InvalidOperationException>();
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
            var updatedDoc = doc.ToMutable();
            updatedDoc.SetInt("key", 2);
            updatedDoc.SetString("key1", "value");
        }

        [Fact]
        public void TestDeleteThenAccessBlob()
        {
            var doc = GenerateDocument("doc1").ToMutable();
            var savedDoc = StoreBlob(Db, doc, Encoding.UTF8.GetBytes("12345"));

            DeleteDB(Db);
            var blob = savedDoc.GetBlob("data");
            blob.Should().NotBeNull("because the blob should still exist and be accessible");
            blob.Length.Should().Be(5, "because the blob's metadata should still be accessible");
            blob.Content.Should().BeNull("because the content cannot be read from a closed database");
        }

        [Fact]
        public void TestDeleteThenGetDatabaseName()
        {
            var name = Db.Name;
            DeleteDB(Db);
            Db.Name.Should().Be(name, "because the name of the database should still be accessible");
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
            .Should().Throw<CouchbaseLiteException>()
            .Where(
                e => e.Error == CouchbaseLiteError.TransactionNotClosed && e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                "because a database can't be closed in the middle of a batch");
        }

        [Fact]
        public void TestDeleteDBOpenByOtherInstance()
        {
            using (var otherDB = OpenDB(Db.Name)) {
                Db.Invoking(d => d.Delete())
                    .Should().Throw<CouchbaseLiteException>()
                    .Where(e => e.Error == CouchbaseLiteError.Busy &&
                                         e.Domain == CouchbaseLiteErrorType.CouchbaseLite,
                        "because an in-use database cannot be deleted");
            }
        }
        
        [Fact]
        public void TestDeleteWithDefaultDirDB()
        {
            string path;
            using (var db = new Database("db")) {
                path = db.Path;
                path.Should().NotBeNull();
            }

            Database.Delete("db", null);
            System.IO.Directory.Exists(path).Should().BeFalse();
        }

        [Fact]
        public void TestDeleteOpeningDBWithDefaultDir()
        {
            using (var db = new Database("db")) {
                Action a = () =>
                {
                    Database.Delete("db", null);
                };
                a.Should().Throw<CouchbaseLiteException>().Where(e =>
                    e.Error == CouchbaseLiteError.Busy && e.Domain == CouchbaseLiteErrorType.CouchbaseLite);
            }
        }

        [Fact]
        public void TestDeleteByStaticMethod()
        {
            var dir = Directory;
            var options = new DatabaseConfiguration
            {
                Directory = dir
            };
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
            var options = new DatabaseConfiguration
            {
                Directory = dir
            };
            using (var db = new Database("db", options)) {
                CouchbaseLiteException e = null;
                try {
                    Database.Delete("db", dir);
                } catch (CouchbaseLiteException ex) {
                    e = ex;
                    ex.Error.Should().Be(CouchbaseLiteError.Busy, "because an in-use database cannot be deleted");
                    ex.Domain.Should().Be(CouchbaseLiteErrorType.CouchbaseLite, "because this is a LiteCore error");
                }

                e.Should().NotBeNull("because an exception is expected");
            }
        }

        [Fact]
        public void TestDeleteNonExistingDBWithDefaultDir()
        {
            // Expect no-op
            Database.Delete("notexistdb", null);
        }

        [Fact]
        public void TestDeleteNonExistingDB()
        {
            // Expect no-op
            Database.Delete("notexistdb", Directory);
        }

        [Fact]
        public void TestDatabaseExistsWithDefaultDir()
        {
            Database.Delete("db", null);
            Database.Exists("db", null).Should().BeFalse();

            using (var db = new Database("db")) {
                Database.Exists("db", null).Should().BeTrue();
                DeleteDB(db);
            }

            Database.Exists("db", null).Should().BeFalse();
        }

        [Fact]
        public void TestDatabaseExistsWithDir()
        {
            var dir = Directory;
            Database.Delete("db", dir);
            Database.Exists("db", dir).Should().BeFalse("because this database has not been created");

            var options = new DatabaseConfiguration
            {
                Directory = dir
            };
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
        public void TestDatabaseExistsAgainstNonExistDBWithDefaultDir()
        {
            Database.Exists("nonexist", null).Should().BeFalse("because that DB does not exist");
        }

        [Fact]
        public void TestDatabaseExistsAgainstNonExistDB()
        {
            Database.Exists("nonexist", Directory).Should().BeFalse("because that DB does not exist");
        }

        [Fact]
        public void TestCompact()
        {
            var docs = CreateDocs(20);

            Db.InBatch(() =>
            {
                foreach (var doc in docs) {
                    var docToUse = doc;
                    for (int i = 0; i < 25; i++) {
                        var mDoc = docToUse.ToMutable();
                        mDoc.SetInt("number", i);
                        SaveDocument(mDoc);
                    };
                }
            });
            
            foreach (var doc in docs) {
                var content = Encoding.UTF8.GetBytes(doc.Id);
                var blob = new Blob("text/plain", content);
                var mDoc = doc.ToMutable();
                mDoc.SetBlob("blob", blob);
                SaveDocument(mDoc);
            }

            Db.Count.Should().Be(20, "because that is the number of documents that were added");

            var attsDir = new DirectoryInfo(Path.Combine(Db.Path, "Attachments"));
            var atts = attsDir.EnumerateFiles();
            atts.Should().HaveCount(20, "because there should be one blob per document");

            Db.Compact();
            
            foreach (var doc in docs) {
                var savedDoc = Db.GetDocument(doc.Id);
                Db.Delete(savedDoc);
                Db.GetDocument(savedDoc.Id).Should().BeNull("because the document was just deleted");
            }

            Db.Count.Should().Be(0, "because all documents were deleted");
            Db.Compact();

            atts = attsDir.EnumerateFiles();
            atts.Should().BeEmpty("because the blobs should be collected by the compaction");
        }

        [Fact]
        public void TestCreateConfiguration()
        {
            var builder1 = new DatabaseConfiguration();
            var config1 = builder1;
            config1.Directory.Should().NotBeNullOrEmpty("because the directory should have a default value");

            #if COUCHBASE_ENTERPRISE
            config1.EncryptionKey.Should().BeNull("because it was not set");
            #endif
            
            var builder2 = new DatabaseConfiguration();
            builder2.Directory = "/tmp/mydb";

            #if COUCHBASE_ENTERPRISE
            var key = new EncryptionKey("key");
            builder2.EncryptionKey = key;
            #endif

            var config2 = builder2;
            config2.Directory.Should().Be("/tmp/mydb", "because that is what was set");

            #if COUCHBASE_ENTERPRISE
            config2.EncryptionKey.Should().Be(key, "because that is what was set");
            #endif
        }

        [Fact]
        public void TestGetSetConfiguration()
        {
            var config = new DatabaseConfiguration();
            using (var db = new Database("db", config))
            {
                db.Config.Should().NotBeSameAs(config, "because the configuration should be copied and frozen");
                db.Config.Directory.Should().Be(config.Directory, "because the directory should be the same");

                #if COUCHBASE_ENTERPRISE
                db.Config.EncryptionKey.Should().Be(config.EncryptionKey,
                    "because the encryption key should be the same");
                #endif
            }
        }

        [Fact]
        public void TestCopy()
        {
            for (int i = 0; i < 10; i++) {
                var docID = $"doc{i}";
                using (var doc = new MutableDocument(docID)) {
                    doc.SetString("name", docID);

                    var data = Encoding.UTF8.GetBytes(docID);
                    var blob = new Blob("text/plain", data);
                    doc.SetBlob("data", blob);

                    Db.Save(doc);
                }
            }

            var dbName = "nudb";
            var config = Db.Config;
            var dir = config.Directory;

            Database.Delete(dbName, dir);
            Database.Copy(Db.Path, dbName, config);

            Database.Exists(dbName, dir).Should().BeTrue();
            using (var nudb = new Database(dbName, config)) {
                nudb.Count.Should().Be(10, "because it is a copy of another database with 10 items");
                var DOCID = Meta.ID;
                var S_DOCID = SelectResult.Expression(DOCID);
                using (var q = QueryBuilder.Select(S_DOCID).From(DataSource.Database(nudb))) {
                    var rs = q.Execute();
                    foreach (var r in rs) {
                        var docID = r.GetString(0);
                        docID.Should().NotBeNull();

                        var doc = nudb.GetDocument(docID);
                        doc.Should().NotBeNull();
                        doc.GetString("name").Should().Be(docID);

                        var blob = doc.GetBlob("data");
                        blob.Should().NotBeNull();

                        var data = Encoding.UTF8.GetString(blob.Content);
                        data.Should().Be(docID);
                    }
                }
            }

            Database.Delete(dbName, dir);
        }

        [Fact]
        public void TestCreateIndex()
        {
            Db.GetIndexes().Should().BeEmpty();

            var fName = Expression.Property("firstName");
            var lName = Expression.Property("lastName");

            var fNameItem = ValueIndexItem.Property("firstName");
            var lNameItem = ValueIndexItem.Expression(lName);

            var index1 = IndexBuilder.ValueIndex(fNameItem, lNameItem);
            Db.CreateIndex("index1", index1);

            var detailItem = FullTextIndexItem.Property("detail");
            var index2 = IndexBuilder.FullTextIndex(detailItem);
            Db.CreateIndex("index2", index2);

            var detailItem2 = FullTextIndexItem.Property("es-detail");
            var index3 = IndexBuilder.FullTextIndex(detailItem2).IgnoreAccents(true).SetLanguage("es");
            Db.CreateIndex("index3", index3);

            Db.GetIndexes().Should().BeEquivalentTo(new[] { "index1", "index2", "index3" });
        }

        [Fact]
        public void TestCreateSameIndexTwice()
        {
            var item = ValueIndexItem.Expression(Expression.Property("firstName"));
            var index = IndexBuilder.ValueIndex(item);
            Db.CreateIndex("myindex", index);
            Db.CreateIndex("myindex", index);

            Db.GetIndexes().Should().BeEquivalentTo(new[] {"myindex"});
        }

        [Fact]
        public void TestCreateSameNameIndexes()
        {
            var fName = Expression.Property("firstName");
            var lName = Expression.Property("lastName");

            var fNameItem = ValueIndexItem.Expression(fName);
            var fNameIndex = IndexBuilder.ValueIndex(fNameItem);
            Db.CreateIndex("myindex", fNameIndex);

            var lNameItem = ValueIndexItem.Expression(lName);
            var lNameIndex = IndexBuilder.ValueIndex(lNameItem);
            Db.CreateIndex("myindex", lNameIndex);

            Db.GetIndexes().Should().BeEquivalentTo(new[] {"myindex"}, "because lNameIndex should overwrite fNameIndex");

            var detailItem = FullTextIndexItem.Property("detail");
            var detailIndex = IndexBuilder.FullTextIndex(detailItem);
            Db.CreateIndex("myindex", detailIndex);

            Db.GetIndexes().Should().BeEquivalentTo(new[] { "myindex" }, "because detailIndex should overwrite lNameIndex");
        }

        [Fact]
        public void TestDeleteIndex()
        {
            TestCreateIndex();

            Db.DeleteIndex("index1");
            Db.GetIndexes().Should().BeEquivalentTo(new[] {"index2", "index3"});

            Db.DeleteIndex("index2");
            Db.GetIndexes().Should().BeEquivalentTo(new[] { "index3" });

            Db.DeleteIndex("index3");
            Db.GetIndexes().Should().BeEmpty();

            Db.DeleteIndex("dummy");
            Db.DeleteIndex("index1");
            Db.DeleteIndex("index2");
            Db.DeleteIndex("index3");
        }

        [Fact]
        public void TestGetDocFromDeletedDB()
        {
            using(var doc = GenerateDocument("doc1")) {

                Db.Delete();

                Db.Invoking(d => d.GetDocument("doc1"))
                    .Should().Throw<InvalidOperationException>()
                    .WithMessage("Attempt to perform an operation on a closed database.",
                        "because this operation is invalid");
            }
        }

        [Fact]
        public void TestCloseWithActiveLiveQueries()
        {
            WithActiveLiveQueries(true);
        }

        [Fact]
        public void TestDeleteWithActiveLiveQueries()
        {
            WithActiveLiveQueries(false);
        }

        [ForIssue("couchbase-lite-android/1231")]
        [Fact]
        public void TestOverwriteDocWithNewDocInstance()
        {
            using (var mDoc1 = new MutableDocument("abc"))
            using (var mDoc2 = new MutableDocument("abc")) {
                mDoc1.SetString("somekey", "someVar");
                mDoc2.SetString("somekey", "newVar");

                // This causes a conflict, the default conflict resolver should be applied
                Db.Save(mDoc1);
                Db.Save(mDoc2);

                // NOTE: Both doc1 and doc2 are generation 1.  Last write should win.
                Db.Count.Should().Be(1UL);
                using (var doc = Db.GetDocument("abc")) {
                    doc.Should().NotBeNull();
                    doc.GetString("somekey").Should().Be("newVar", "because the last write should win");
                }  
            }
        }

        [ForIssue("couchbase-lite-android/1416")]
        [Fact]
        public void TestDeleteAndOpenDB()
        {
            using (var database1 = new Database("application")) {
                database1.Delete();

                using (var database2 = new Database("application")) {
                    database2.InBatch(() =>
                    {
                        for (var i = 0; i < 100; i++) {
                            using (var doc = new MutableDocument()) {
                                doc.SetInt("index", i);
                                for (var j = 0; j < 10; j++) {
                                    doc.SetInt($"item_{j}", j);
                                }

                                database2.Save(doc);
                            }
                        }
                    });
                }
            }
        }

        private void WithActiveLiveQueries(bool isCloseNotDelete)
        {
            Database.Delete("closeDB", Db.Config.Directory);
            using (var otherDb = new Database("closeDB", Db.Config)) {
                var query = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Database(otherDb));
                var query1 = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Database(otherDb));
                var doc1Listener = new WaitAssert();
                var token = query.AddChangeListener(null, (sender, args) => {
                    foreach (var row in args.Results) {
                        if (row.GetString("id") == "doc1") {
                            doc1Listener.Fulfill();
                        }
                    }
                });

                var doc1Listener1 = new WaitAssert();
                var token1 = query1.AddChangeListener(null, (sender, args) => {
                    foreach (var row in args.Results) {
                        if (row.GetString("id") == "doc1") {
                            doc1Listener1.Fulfill();
                        }
                    }
                });

                using (var doc = new MutableDocument("doc1")) {
                    doc.SetString("value", "string");
                    otherDb.Save(doc); // Should still trigger since it is pointing to the same DB
                }

                otherDb.ActiveLiveQueries.Count.Should().Be(2);

                doc1Listener.WaitForResult(TimeSpan.FromSeconds(20));
                doc1Listener1.WaitForResult(TimeSpan.FromSeconds(20));

                if (isCloseNotDelete)
                    otherDb.Close();
                else
                    otherDb.Delete();

                otherDb.ActiveLiveQueries.Count.Should().Be(0);
                otherDb.IsClosedLocked.Should().Be(true);
            }

            //Database.Delete("closeDB", Db.Config.Directory);
        }

        private bool ResolveConflict(MutableDocument updatedDoc, Document currentDoc)
        {
            var updateDocDict = updatedDoc.ToDictionary();
            var curDocDict = currentDoc.ToDictionary();

            foreach (var value in curDocDict)
                if (updateDocDict.ContainsKey(value.Key) && !value.Value.Equals(updateDocDict[value.Key]))
                    updateDocDict[value.Key] = value.Value + ", " + updateDocDict[value.Key];
                else if (!updateDocDict.ContainsKey(value.Key))
                    updateDocDict.Add(value.Key, value.Value);

            updatedDoc.SetData(updateDocDict);
            return true;
        }

        private void DeleteDB(Database db)
        {
            var path = db.Path;
            if (path != null) {
                System.IO.Directory.Exists(path).Should()
                    .BeTrue("because the database should exist if it is going to be deleted");
            }

            db.Delete();
            if (path != null) {
                System.IO.Directory.Exists(path).Should().BeFalse("because the database should not exist anymore");
            }
        }

        private MutableDocument GenerateDocument(string docID)
        {
            var doc = new MutableDocument(docID);
            doc.SetInt("key", 1);

            Db.Save(doc);
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
                var doc = new MutableDocument($"doc_{i:D3}");
                doc.SetInt("key", i);
                Db.Save(doc);
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

            VerifyGetDocument(docID);
        }

        private void PurgeDocAndVerify(Document doc)
        {
            var docID = doc.Id;
            Db.Purge(doc);
            Db.GetDocument(docID).Should().BeNull("because it no longer exists");
        }

        private Document StoreBlob(Database db, MutableDocument doc, byte[] content)
        {
            var blob = new Blob("text/plain", content);
            doc.SetBlob("data", blob);
            Db.Save(doc);
            return Db.GetDocument(doc.Id);
        }
    }
}
