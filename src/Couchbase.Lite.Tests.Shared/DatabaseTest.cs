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
using Shouldly;
using System.Threading;

using Couchbase.Lite.Query;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
// ReSharper disable AccessToDisposedClosure

namespace Test;

public class DatabaseTest(ITestOutputHelper output) : TestCase(output)
{
    [Fact]
    public void TestSimpleN1QLQuery()
    {
        using (var d = new MutableDocument())
        using (var d2 = new MutableDocument())
        {
            d.SetString("firstName", "Jerry");
            d.SetString("lastName", "Ice Cream");
            DefaultCollection.Save(d);

            d2.SetString("firstName", "Ben");
            d2.SetString("lastName", "Ice Cream");
            DefaultCollection.Save(d2);
        }

        // Test to catch N1QL query compile error in database CreateQuery() call
        Should.Throw<CouchbaseLiteException>((() => Db.CreateQuery($"SELECT firstName, lastName FROM *")), 
            "because the input N1QL query string has syntax error near character 33.");

        // With new collections implementation, N1QL call will look like "SELECT firstName, lastName FROM _" or "SELECT firstName, lastName FROM _ as whatever"
        // default collection is named _.
        // "SELECT firstName, lastName FROM {Db.Name}" is still valid as well.
        using (var q = Db.CreateQuery($"SELECT firstName, lastName FROM _"))
        {
            var res = q.Execute().AllResults();
            res.Count.ShouldBe(2);
            res[0].GetString(0).ShouldBe("Jerry");
            res[0].GetString(1).ShouldBe("Ice Cream");
            res[1].GetString(0).ShouldBe("Ben");
            res[1].GetString(1).ShouldBe("Ice Cream");
        }
    }

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
    public void TestCreateWithDefaultConfiguration()
    {
        using var db = new Database("db", new DatabaseConfiguration());
        db.GetDefaultCollection().Count.ShouldBe(0UL);
        DeleteDB(db);
    }

    [Fact]
    public void TestCreateWithSpecialCharacterDBNames()
    {
        using var db = OpenDB("`~@#$%&'()_+{}][=-.,;'");
        db.Name.ShouldBe("`~@#$%&'()_+{}][=-.,;'", "because that is the (weird) name that was set");
        Path.GetDirectoryName(db.Path).ShouldEndWith(".cblite2", Case.Sensitive, "because that is the current DB extension");
        db.GetDefaultCollection().Count.ShouldBe(0UL, "because the database is empty");

        db.Delete();
    }

    [Fact]
    public void TestCreateWithEmptyDBNames()
    {
        var ex = Should.Throw<CouchbaseLiteException>(() => OpenDB(""),
            "because an empty db name is invalid");
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        ex.Error.ShouldBe(CouchbaseLiteError.WrongFormat);
    }

    [Fact]
    public void TestCreateWithCustomDirectory()
    {
        var dir = Directory;
        Database.Delete("db", dir);
        Database.Exists("db", dir).ShouldBeFalse("because it was just deleted");

        var options = new DatabaseConfiguration
            { Directory = dir };
        using var db = new Database("db", options);
        Path.GetDirectoryName(db.Path).ShouldEndWith(".cblite2", Case.Sensitive, "because that is the current CBL extension");
        db.Path?.Contains(dir).ShouldBeTrue("because the directory should be present in the custom path");
        Database.Exists("db", dir).ShouldBeTrue("because it was just created");
        db.GetDefaultCollection().Count.ShouldBe(0UL, "because the database is empty");

        DeleteDB(db);
    }

    [Fact]
    public void TestGetDocumentWithEmptyStringId()
    {
        Should.Throw<CouchbaseLiteException>(() => DefaultCollection.GetDocument(""),
            @"CouchbaseLiteException (LiteCoreDomain / 29): Invalid docID "".");
    }

    [Fact]
    public void TestGetNonExistingDocWithID()
    {
        DefaultCollection.GetDocument("non-exist").ShouldBeNull("because it doesn't exist");
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
            using var doc = new MutableDocument($"doc_{i:D3}");
            doc.SetInt("key", i);
            DefaultCollection.Save(doc);
        }

        DefaultCollection.Count.ShouldBe((ulong)NumDocs);
        ValidateDocs(NumDocs);
    }

    [Fact]
    public void TestGetExistingDocWithIDFromDifferentDBInstance()
    {
        const string docID = "doc1";
        GenerateDocument(docID);

        using var otherDB = OpenDB(Db.Name);
        otherDB.GetDefaultCollection().Count
            .ShouldBe(1UL, "because the other database instance should reflect existing data");

        VerifyGetDocument(docID, otherDB);
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
        using var _ = GenerateDocument("doc1");
        Db.Close();

        Should.Throw<CouchbaseLiteException>(() => Db.GetDefaultCollection().GetDocument("doc1"))
            .Error.ShouldBe(CouchbaseLiteError.NotOpen);
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
        const string docID = "doc1";
        using var doc = GenerateDocument(docID).ToMutable();
        doc.SetInt("key", 2);
        DefaultCollection.Save(doc);

        DefaultCollection.Count.ShouldBe(1UL, "because a document was updated, not added");

        VerifyGetDocument(docID, value: 2);
    }

    [Fact]
    public void TestSaveDocInDifferentDBInstance()
    {
        const string docID = "doc1";
        using var doc = GenerateDocument(docID).ToMutable();
        using var otherDB = OpenDB(Db.Name);
        otherDB.GetDefaultCollection().Count
            .ShouldBe(1UL, "because the other database instance should reflect existing data");
        doc.SetInt("key", 2);
        var ex = Should.Throw<CouchbaseLiteException>(() => otherDB.GetDefaultCollection().Save(doc), 
            "because a document cannot be saved into another database instance");
        ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
    }

    [Fact]
    public void TestSaveDocInDifferentDB()
    {
        Database.Delete("otherDB", Directory);
        const string docID = "doc1";
        using var doc = GenerateDocument(docID).ToMutable();
        using var otherDB = OpenDB("otherDB");
        otherDB.GetDefaultCollection().Count
            .ShouldBe(0UL, "because the other database is empty");
        doc.SetInt("key", 2);
        var ex = Should.Throw<CouchbaseLiteException>(() => otherDB.GetDefaultCollection().Save(doc),
            "because a document cannot be saved into another database");
        ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        DeleteDB(otherDB);
    }

    [Fact]
    public void TestSaveSameDocTwice()
    {
        const string docID = "doc1";
        using var doc = GenerateDocument(docID).ToMutable();
        DefaultCollection.Save(doc);
        doc.Id.ShouldBe(docID, "because the doc ID should never change");
        DefaultCollection.Count.ShouldBe(1UL, "because there is still only one document");
    }

    [Fact]
    public void TestSaveInBatch()
    {
        Db.InBatch(() => CreateDocs(10));
        DefaultCollection.Count.ShouldBe(10UL, "because 10 documents were added");

        ValidateDocs(10);
    }

    [Fact]
    public void TestSaveConcurrencyControl()
    {
        using var doc1A = new MutableDocument("doc1");
        using var doc1B = new MutableDocument("doc1");
        doc1A.SetString("name", "Jim");
        DefaultCollection.Save(doc1A);
        doc1B.SetString("name", "Tim");
        DefaultCollection.Save(doc1B, ConcurrencyControl.FailOnConflict)
            .ShouldBeFalse("because a conflict should not be allowed in this mode");
        using (var gotDoc = DefaultCollection.GetDocument(doc1A.Id)) {
            gotDoc.ShouldNotBeNull("because it was just saved");
            gotDoc.GetString("name").ShouldBe("Jim");
        }

        DefaultCollection.Save(doc1B, ConcurrencyControl.LastWriteWins);
        using (var gotDoc = DefaultCollection.GetDocument(doc1A.Id)) {
            gotDoc.ShouldNotBeNull("because it was just saved");
            gotDoc.GetString("name").ShouldBe("Tim");
        }
    }

    [Fact]
    public void TestConflictHandlerSaveMergedDocument()
    {
        using (var doc1 = new MutableDocument("doc1")){
            doc1.SetString("name", "Jim");

            DefaultCollection.Save(doc1);

            // Get two doc1 document objects (doc1A and doc1B):
            var doc1A = DefaultCollection.GetDocument(doc1.Id)?.ToMutable();
            var doc1B = DefaultCollection.GetDocument(doc1.Id)?.ToMutable();
            doc1A.ShouldNotBeNull("because doc1a was just saved");
            doc1B.ShouldNotBeNull("because doc1b was just saved");

            // Modify doc1A:
            doc1A.SetString("name", "Jim");
            doc1A.SetString("language", "English");

            DefaultCollection.Save(doc1A);

            // Modify doc1B:
            doc1B.SetString("name", "Jim");
            doc1B.SetString("language", "C#");
            doc1B.SetString("location", "Japan");
            DefaultCollection.Save(doc1B, ResolveConflict);
        }

        using (var doc1 = DefaultCollection.GetDocument("doc1")) {
            doc1.ShouldNotBeNull("because the conflict should have been resolved and the document then saved");
            doc1.GetString("name").ShouldBe("Jim");
            var lanStr = doc1.GetString("language");
            lanStr.ShouldNotBeNull();
            lanStr.ShouldContain("English");
            lanStr.ShouldContain("C#");
            doc1.GetString("location").ShouldBe("Japan");
        }
    }

    [Fact]
    public void TestConflictHandlerWhenDocumentIsPurged()
    {
        using (var doc = new MutableDocument("doc1")) {
            doc.SetString("firstName", "Tiger");
            DefaultCollection.Save(doc);
        }

        using (var doc1 = DefaultCollection.GetDocument("doc1"))
        using (var doc1B = doc1?.ToMutable()) {
            DefaultCollection.Purge("doc1");
            doc1B.ShouldNotBeNull("because the document was saved earlier");
            doc1B.SetString("nickName", "Scott");

            var ex = Should.Throw<CouchbaseLiteException>(() => DefaultCollection.Save(doc1B, (_, _) => true), 
                "because the document is purged");

            ex.Error.ShouldBe(CouchbaseLiteError.NotFound);
            ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        }
    }

    [Fact]
    public void TestConflictHandlerReturnsTrue()
    {
        using var doc1 = new MutableDocument("doc1");
        doc1.SetString("name", "Jim");
        DefaultCollection.Save(doc1, (_, _) => true);
        DefaultCollection.GetDocument("doc1")?.GetString("name").ShouldBe("Jim");

        var doc1A = new MutableDocument(doc1.Id);
        doc1A.SetString("name", "Kim");
        DefaultCollection.Save(doc1A, (_, _) => true);
        DefaultCollection.GetDocument("doc1")?.GetString("name").ShouldBe("Kim");
    }

    [Fact]
    public void TestConflictHandlerReturnsFalse()
    {
        using var doc1 = new MutableDocument("doc1");
        doc1.SetString("name", "Jim");
        DefaultCollection.Save(doc1, (_, _) => false);
        DefaultCollection.GetDocument(doc1.Id)?.GetString("name").ShouldBe("Jim");

        var doc1A = new MutableDocument(doc1.Id);
        doc1A.SetString("name", "Kim");
        DefaultCollection.Save(doc1A, (_, _) => false);
        DefaultCollection.GetDocument("doc1")?.GetString("name").ShouldBe("Jim");
    }

    [Fact]
    public void TestConflictHandlerWithMultipleIncomingConflicts()
    {
        using var doc1 = new MutableDocument("doc1");
        using var doc1A = new MutableDocument("doc1");
        using var doc1B = new MutableDocument("doc1");
        var waitObj = new AutoResetEvent(true);
        doc1.SetString("name", "Jim");
        DefaultCollection.Save(doc1);
        Task.Factory.StartNew(() => {
            doc1A.SetString("name", "Kim");
            DefaultCollection.Save(doc1A, (_, _) => {
                waitObj.Set();
                Thread.Sleep(250);
                waitObj.WaitOne(TimeSpan.FromMilliseconds(250));
                return false;
            });
            waitObj.Set();
            Thread.Sleep(250);
        });
        
        waitObj.WaitOne(TimeSpan.FromMilliseconds(250));
        doc1B.SetString("name", "Tim");
        DefaultCollection.Save(doc1B);
        DefaultCollection.GetDocument("doc1")?.GetString("name").ShouldBe("Tim");
        waitObj.Set();
        Thread.Sleep(250);
        waitObj.WaitOne(TimeSpan.FromMilliseconds(250));
        DefaultCollection.GetDocument("doc1")?.GetString("name").ShouldBe("Tim");
    }

    [Fact]
    public void TestConflictHandlerWithDeletedOldDoc()
    {
        using var doc1 = new MutableDocument("doc1");
        doc1.SetString("name", "Jim");
        DefaultCollection.Save(doc1);

        var doc1A = DefaultCollection.GetDocument("doc1")?.ToMutable();
        doc1A.ShouldNotBeNull("because the document was just saved");
        doc1A.SetString("name", "Kim");

        Document? currDoc = null;
        var updatedDocName = "";

        //delete old doc
        DefaultCollection.Delete(doc1);
        DefaultCollection.Save(doc1A, (updated, current) =>
        {
            currDoc = current;
            updatedDocName = updated.GetString("name");
            return true;
        });

        currDoc.ShouldBeNull();
        updatedDocName.ShouldBe("Kim");
        DefaultCollection.GetDocument("doc1")?.GetString("name").ShouldBe("Kim");
    }

    public enum DisposeType
    {
        Close,
        Delete
    }

    [Theory]
    [InlineData(DisposeType.Close)]
    public void TestSaveDocToDisposedDB(DisposeType disposeType)
    {
        if(disposeType == DisposeType.Close) {
            Db.Close();
        } else {
            DeleteDB(Db);
        }

        var doc = new MutableDocument("doc1");
        doc.SetInt("key", 1);

        Should.Throw<CouchbaseLiteException>(() => Db.GetDefaultCollection().Save(doc), 
            "because this operation is invalid").Error.ShouldBe(CouchbaseLiteError.NotOpen);
    }

    [Fact]
    public void TestDeletePreSaveDoc()
    {
        var doc = new MutableDocument("doc1");
        doc.SetInt("key", 1);

        var ex = Should.Throw<CouchbaseLiteException>(() => DefaultCollection.Delete(doc),
            "because deleting an unsaved document is not allowed");
        ex.Error.ShouldBe(CouchbaseLiteError.NotFound);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        DefaultCollection.Count.ShouldBe(0UL, "because the database should still be empty");
    }

    [Fact]
    public void TestDeleteDoc()
    {
        var docID = "doc1";
        using (var doc = GenerateDocument(docID)) {

            DefaultCollection.Delete(doc);
            DefaultCollection.Count.ShouldBe(0UL, "because the only document was deleted");
        }

        var gotDoc = DefaultCollection.GetDocument(docID);
        gotDoc.ShouldBeNull("because the document was deleted");
    }

    [Fact]
    public void TestDeleteDocInDifferentDBInstance()
    {
        const string docID = "doc1";
        var doc = GenerateDocument(docID);

        using var otherDB = OpenDB(Db.Name);
        otherDB.GetDefaultCollection().Count
            .ShouldBe(1UL, "because the other database instance should reflect existing data");
        var ex = Should.Throw<CouchbaseLiteException>(() => otherDB.GetDefaultCollection().Delete(doc),
            "because a document cannot be deleted from another database instance");
        ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        otherDB.GetDefaultCollection().Count.ShouldBe(1UL, "because the delete failed");
        DefaultCollection.Count.ShouldBe(1UL, "because the delete failed");
        doc.IsDeleted.ShouldBeFalse("because the delete failed");
    }

    [Fact]
    public void TestDeleteDocInDifferentDB()
    {
        Database.Delete("otherDB", Directory);
        const string docID = "doc1";
        var doc = GenerateDocument(docID);

        using var otherDB = OpenDB("otherDB");
        otherDB.GetDefaultCollection().Count
            .ShouldBe(0UL, "because the other database should be empty");
        var ex = Should.Throw<CouchbaseLiteException>(() => otherDB.GetDefaultCollection().Delete(doc),
            "because a document cannot be deleted from another database");
        ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        otherDB.GetDefaultCollection().Count.ShouldBe(0UL, "because the database is still empty");
        DefaultCollection.Count.ShouldBe(1UL, "because the delete failed");
        doc.IsDeleted.ShouldBeFalse("because the delete failed");

        otherDB.Delete();
    }

    [Fact]
    public void TestDeleteDocInBatch()
    {
        CreateDocs(10);
        Db.InBatch(() =>
        {
            for (var i = 0; i < 10; i++) {
                var docID = $"doc_{i:D3}";
                var doc = DefaultCollection.GetDocument(docID);
                doc.ShouldNotBeNull("because the document was created in CreateDocs");
                DefaultCollection.Delete(doc);
                DefaultCollection.Count.ShouldBe(9UL - (ulong)i, "because the document count should be accurate after deletion");
            }
        });

        DefaultCollection.Count.ShouldBe(0UL, "because all documents were deleted");
    }

    [Theory]
    [InlineData(DisposeType.Close)]
    public void TestDeleteDocOnDisposedDB(DisposeType disposeType)
    {
        var doc = GenerateDocument("doc1");

        if(disposeType == DisposeType.Close) {
            Db.Close();
        } else {
            DeleteDB(Db);
        }

        Should.Throw<CouchbaseLiteException>(() => Db.GetDefaultCollection().Delete(doc))
            .Error.ShouldBe(CouchbaseLiteError.NotOpen);
    }

    [Fact]
    public void TestPurgePreSaveDoc()
    {
        var doc = new MutableDocument("doc1");
        doc.SetInt("key", 1);

        var ex = Should.Throw<CouchbaseLiteException>(() => DefaultCollection.Purge(doc),
            "because purging an unsaved document is not allowed");
        ex.Error.ShouldBe(CouchbaseLiteError.NotFound);

        DefaultCollection.Count.ShouldBe(0UL, "because the database should still be empty");
    }

    [Fact]
    public void TestPurgeDoc()
    {
        var doc = GenerateDocument("doc1");

        PurgeDocAndVerify(doc);
        DefaultCollection.Count.ShouldBe(0UL, "because the only document was purged");
    }

    [Fact]
    public void TestPurgeDocInDifferentDBInstance()
    {
        const string docID = "doc1";
        var doc = GenerateDocument(docID);

        using var otherDB = OpenDB(Db.Name);
        otherDB.GetDefaultCollection().Count
            .ShouldBe(1UL, "because the other database instance should reflect existing data");
        var ex = Should.Throw<CouchbaseLiteException>(() => otherDB.GetDefaultCollection().Purge(doc),
            "because a document cannot be purged from another database instance");
        ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);

        otherDB.GetDefaultCollection().Count.ShouldBe(1UL, "because the delete failed");
        DefaultCollection.Count.ShouldBe(1UL, "because the delete failed");
        doc.IsDeleted.ShouldBeFalse("because the delete failed");
    }

    [Fact]
    public void TestPurgeDocInDifferentDB()
    {
        const string docID = "doc1";
        var doc = GenerateDocument(docID);
        Database.Delete("otherDB", Directory);

        using var otherDB = OpenDB("otherDB");
        otherDB.GetDefaultCollection().Count
            .ShouldBe(0UL, "because the other database should be empty");
        var ex = Should.Throw<CouchbaseLiteException>(() => otherDB.GetDefaultCollection().Purge(doc),
            "because a document cannot be purged from another database");
        ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);

        otherDB.GetDefaultCollection().Count.ShouldBe(0UL, "because the database is still empty");
        DefaultCollection.Count.ShouldBe(1UL, "because the delete failed");
        doc.IsDeleted.ShouldBeFalse("because the delete failed");

        otherDB.Delete();
    }

    [Fact]
    public void TestPurgeSameDocTwice()
    {
        const string docID = "doc1";
        var doc = GenerateDocument(docID);

        var doc1 = DefaultCollection.GetDocument(docID);
        doc1.ShouldNotBeNull("because the document was just created and it should exist");

        PurgeDocAndVerify(doc);
        DefaultCollection.Count.ShouldBe(0UL, "because the only document was purged");

        // Second purge and throw error
        var ex = Should.Throw<CouchbaseLiteException>(() => DefaultCollection.Purge(doc));
        ex.Error.ShouldBe(CouchbaseLiteError.NotFound);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
    }

    [Fact]
    public void TestPurgeDocInBatch()
    {
        CreateDocs(10);
        Db.InBatch(() =>
        {
            for (int i = 0; i < 10; i++) {
                var docID = $"doc_{i:D3}";
                var doc = DefaultCollection.GetDocument(docID);
                doc.ShouldNotBeNull("because it was saved in CreateDocs");
                PurgeDocAndVerify(doc);
                DefaultCollection.Count.ShouldBe(9UL - (ulong)i, "because the document count should be accurate after deletion");
            }
        });

        DefaultCollection.Count.ShouldBe(0UL, "because all documents were purged");
    }

    [Theory]
    [InlineData(DisposeType.Close)]
    [InlineData(DisposeType.Delete)]
    public void TestPurgeDocOnDisposedDB(DisposeType disposeType)
    {
        var doc = GenerateDocument("doc1");

        if (disposeType == DisposeType.Close) {
            Db.Close();
        } else {
            DeleteDB(Db);
        }
                
        Should.Throw<CouchbaseLiteException>(() => Db.GetDefaultCollection().Purge(doc))
            .Error.ShouldBe(CouchbaseLiteError.NotOpen);
    }

    [Fact]
    public void TestCloseTwice()
    {
        Db.Close();
        Should.NotThrow(() => Db.Close());
    }

    [Fact]
    public void TestCloseThenAccessDoc()
    {
        var docID = "doc1";
        var doc = GenerateDocument(docID);

        Db.Close();

        doc.Id.ShouldBe(docID, "because a document's ID should never change");
        doc.GetInt("key").ShouldBe(1, "because the document's data should still be accessible");

        Should.NotThrow(() =>
        {
            var updatedDoc = doc.ToMutable();
            updatedDoc.SetInt("key", 2);
            updatedDoc.SetString("key1", "value");
        });
    }

    [Fact]
    public void TestCloseThenAccessBlob()
    {
        using var doc = GenerateDocument("doc1");
        var savedDoc = StoreBlob(Db, doc, Encoding.UTF8.GetBytes("12345"));

        Db.Close();
        var blob = savedDoc.GetBlob("data");
        blob.ShouldNotBeNull("because the blob should still exist and be accessible");
        blob.Length.ShouldBe(5, "because the blob's metadata should still be accessible");
        blob.Content.ShouldBeNull("because the content cannot be read from a closed database");
    }

    [Fact]
    public void TestCloseThenGetDatabaseName()
    {
        var name = Db.Name;
        Db.Close();
        Db.Name.ShouldBe(name, "because the name of the database should still be accessible");
    }

    [Fact]
    public void TestCloseThenGetDatabasePath()
    {
        Db.Close();
        Db.Path.ShouldBeNull("because a non-open database has no path");
    }

    [Fact]
    public void TestCloseThenDeleteDatabase()
    {
        Db.Dispose();
        Should.Throw<CouchbaseLiteException>(() => DeleteDB(Db)).Error.ShouldBe(CouchbaseLiteError.NotOpen);
    }

    [Theory]
    [InlineData(DisposeType.Close)]
    [InlineData(DisposeType.Delete)]
    public void TestDisposeThenCallInBatch(DisposeType disposeType)
    {
        var ex = Should.Throw<CouchbaseLiteException>(() => Db.InBatch(() =>
            {
                if (disposeType == DisposeType.Close) {
                    Db.Close();
                } else {
                    DeleteDB(Db);
                }
            }), $"because a database can't be {disposeType}d in the middle of a batch");

        ex.Error.ShouldBe(CouchbaseLiteError.TransactionNotClosed);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
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
        Should.Throw<CouchbaseLiteException>(() => Db.Delete())
            .Error.ShouldBe(CouchbaseLiteError.NotOpen);
    }

    [Fact]
    public void TestDeleteThenAccessDoc()
    {
        var docID = "doc1";
        var doc = GenerateDocument(docID);

        DeleteDB(Db);

        doc.Id.ShouldBe(docID, "because a document's ID should never change");
        doc.GetInt("key").ShouldBe(1, "because the document's data should still be accessible");

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
        blob.ShouldNotBeNull("because the blob should still exist and be accessible");
        blob.Length.ShouldBe(5, "because the blob's metadata should still be accessible");
        blob.Content.ShouldBeNull("because the content cannot be read from a closed database");
    }

    [Fact]
    public void TestDeleteThenGetDatabaseName()
    {
        var name = Db.Name;
        DeleteDB(Db);
        Db.Name.ShouldBe(name, "because the name of the database should still be accessible");
    }

    [Fact]
    public void TestDeleteThenGetDatabasePath()
    {
        DeleteDB(Db);
        Db.Path.ShouldBeNull("because a non-open database has no path");
    }

#if !SANITY_ONLY
        [Fact]
        public void TestDeleteDBOpenByOtherInstance()
        {
            using (var otherDB = OpenDB(Db.Name)) {
                var ex = Should.Throw<CouchbaseLiteException>(Db.Delete, "because an in-use database cannot be deleted");
                ex.Error.ShouldBe(CouchbaseLiteError.Busy);
                ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
            }
        }
#endif
        
    [Fact]
    public void TestDeleteWithDefaultDirDB()
    {
        string? path;
        using (var db = new Database("db")) {
            path = db.Path;
            path.ShouldNotBeNull();
        }

        Database.Delete("db", null);
        System.IO.Directory.Exists(path).ShouldBeFalse();
    }

#if !SANITY_ONLY
        [Fact]
        public void TestDeleteOpeningDBWithDefaultDir()
        {
            using (var db = new Database("db")) {
                var ex = Should.Throw<CouchbaseLiteException>(() =>
                {
                    Database.Delete("db", null);
                });
                ex.Error.ShouldBe(CouchbaseLiteError.Busy);
                ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
            }
        }
#endif

    [Fact]
    public void TestDeleteByStaticMethod()
    {
        var dir = Directory;
        var options = new DatabaseConfiguration
        {
            Directory = dir
        };
        string? path;
        using (var db = new Database("db", options)) {
            path = db.Path;
        }

        Database.Delete("db", dir);
        System.IO.Directory.Exists(path).ShouldBeFalse("because the database was deleted");
    }

#if !SANITY_ONLY

        [Fact]
        public void TestDeleteOpeningDBByStaticMethod()
        {
            var dir = Directory;
            var options = new DatabaseConfiguration
            {
                Directory = dir
            };
            using (var db = new Database("db", options)) {
                var ex = Should.Throw<CouchbaseLiteException>(() => Database.Delete("db", dir), "because a database cannot be deleted while open");
                ex.Error.ShouldBe(CouchbaseLiteError.Busy);
                ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
            }
        }
#endif

    [Fact]
    public void TestDeleteNonExistingDBWithDefaultDir()
    {
        Should.NotThrow(() => Database.Delete("notexistdb", null));
    }

    [Fact]
    public void TestDeleteNonExistingDB()
    {
        Should.NotThrow(() => Database.Delete("notexistdb", Directory));
    }

    [Fact]
    public void TestDatabaseExistsWithDefaultDir()
    {
        Database.Delete("db", null);
        Database.Exists("db", null).ShouldBeFalse();

        using (var db = new Database("db")) {
            Database.Exists("db", null).ShouldBeTrue();
            DeleteDB(db);
        }

        Database.Exists("db", null).ShouldBeFalse();
    }

    [Fact]
    public void TestDatabaseExistsWithDir()
    {
        var dir = Directory;
        Database.Delete("db", dir);
        Database.Exists("db", dir).ShouldBeFalse("because this database has not been created");

        var options = new DatabaseConfiguration
        {
            Directory = dir
        };
        string? path;
        using (var db = new Database("db", options)) {
            path = db.Path;
            Database.Exists("db", dir).ShouldBeTrue("because the database is now created");
        }

        Database.Exists("db", dir).ShouldBeTrue("because the database still exists after close");
        Database.Delete("db", dir);
        System.IO.Directory.Exists(path).ShouldBeFalse("because the database was deleted");
        Database.Exists("db", dir).ShouldBeFalse("because the database was deleted");
    }

    [Fact]
    public void TestDatabaseExistsAgainstNonExistDBWithDefaultDir()
    {
        Database.Exists("nonexist", null).ShouldBeFalse("because that DB does not exist");
    }

    [Fact]
    public void TestDatabaseExistsAgainstNonExistDB()
    {
        Database.Exists("nonexist", Directory).ShouldBeFalse("because that DB does not exist");
    }

    [Fact]
    public void TestCompact()
    {
        var docs = CreateDocs(20);

        Db.InBatch(() =>
        {
            foreach (var doc in docs) {
                for (var i = 0; i < 25; i++) {
                    using var mDoc = doc.ToMutable();
                    mDoc.SetInt("number", i);
                    SaveDocument(mDoc);
                }
            }
        });
            
        foreach (var doc in docs) {
            var content = Encoding.UTF8.GetBytes(doc.Id);
            var blob = new Blob("text/plain", content);
            var mDoc = doc.ToMutable();
            mDoc.SetBlob("blob", blob);
            SaveDocument(mDoc);
        }

        DefaultCollection.Count.ShouldBe(20UL, "because that is the number of documents that were added");

        Db.Path.ShouldNotBeNull("because an open database should always have a path");
        var attachDict = new DirectoryInfo(Path.Combine(Db.Path!, "Attachments"));
        var attachments = attachDict.EnumerateFiles();
        attachments.Count().ShouldBe(20, "because there should be one blob per document");

        Db.PerformMaintenance(MaintenanceType.Compact);

        foreach (var doc in docs) {
            var savedDoc = DefaultCollection.GetDocument(doc.Id);
            savedDoc.ShouldNotBeNull($"because the document '{doc.Id}' was saved earlier");
            DefaultCollection.Delete(savedDoc);
            DefaultCollection.GetDocument(savedDoc.Id).ShouldBeNull($"because the document '{doc.Id}' was just deleted");
        }

        DefaultCollection.Count.ShouldBe(0UL, "because all documents were deleted");
        Db.PerformMaintenance(MaintenanceType.Compact);

        attachments = attachDict.EnumerateFiles();
        attachments.ShouldBeEmpty("because the blobs should be collected by the compaction");
    }

    [Fact]
    public void TestPerformMaintenanceCompact()
    {
        var docs = CreateDocs(20);

        Db.InBatch(() =>
        {
            foreach (var doc in docs) {
                for (var i = 0; i < 25; i++) {
                    using var mDoc = doc.ToMutable();
                    mDoc.SetInt("number", i);
                    SaveDocument(mDoc);
                }
            }
        });

        foreach (var doc in docs) {
            var content = Encoding.UTF8.GetBytes(doc.Id);
            var blob = new Blob("text/plain", content);
            var mDoc = doc.ToMutable();
            mDoc.SetBlob("blob", blob);
            SaveDocument(mDoc);
        }

        DefaultCollection.Count.ShouldBe(20UL, "because that is the number of documents that were added");

        Db.Path.ShouldNotBeNull("because an open database should always have a path");
        var attachDict = new DirectoryInfo(Path.Combine(Db.Path!, "Attachments"));
        var attachments = attachDict.EnumerateFiles();
        attachments.Count().ShouldBe(20, "because there should be one blob per document");

        Db.PerformMaintenance(MaintenanceType.Compact);

        foreach (var doc in docs) {
            var savedDoc = DefaultCollection.GetDocument(doc.Id);
            savedDoc.ShouldNotBeNull($"because the document '{doc.Id}' was saved earlier");
            DefaultCollection.Delete(savedDoc);
            DefaultCollection.GetDocument(savedDoc.Id).ShouldBeNull($"because the document '{doc.Id}' was just deleted");
        }

        DefaultCollection.Count.ShouldBe(0UL, "because all documents were deleted");
        Db.PerformMaintenance(MaintenanceType.Compact);

        attachments = attachDict.EnumerateFiles();
        attachments.ShouldBeEmpty("because the blobs should be collected by the compaction");
    }

    [Fact]
    public void TestPerformMaintenanceReindex()
    {
        CreateDocs(20);

        // Reindex when there is no index
        Db.PerformMaintenance(MaintenanceType.Reindex);

        //Create an index
        var key = Expression.Property("key");
        var keyItem = ValueIndexItem.Expression(key);
        var keyIndex = IndexBuilder.ValueIndex(keyItem);
        DefaultCollection.CreateIndex("KeyIndex", keyIndex);
        DefaultCollection.GetIndexes().Count.ShouldBe(1);

        var q = QueryBuilder.Select(SelectResult.Expression(key))
            .From(DataSource.Collection(DefaultCollection))
            .Where(key.GreaterThan(Expression.Int(9)));
        q.Explain().Contains("USING INDEX KeyIndex").ShouldBeTrue();

        //Reindex
        Db.PerformMaintenance(MaintenanceType.Reindex);

        //Check if the index is still there and used
        DefaultCollection.GetIndexes().Count.ShouldBe(1);
        q.Explain().Contains("USING INDEX KeyIndex").ShouldBeTrue();
    }

    [Fact]
    public void TestPerformMaintenanceIntegrityCheck()
    {
        var docs = CreateDocs(20);

        // Update each doc 25 times
        Db.InBatch(() =>
        {
            foreach (var doc in docs) {
                for (var i = 0; i < 25; i++) {
                    using var mDoc = doc.ToMutable();
                    mDoc.SetInt("number", i);
                    SaveDocument(mDoc);
                }
            }
        });

        // Add each doc with a blob object
        foreach (var doc in docs) {
            var content = Encoding.UTF8.GetBytes(doc.Id);
            var blob = new Blob("text/plain", content);
            var mDoc = doc.ToMutable();
            mDoc.SetBlob("blob", blob);
            SaveDocument(mDoc);
        }

        DefaultCollection.Count.ShouldBe(20UL, "because that is the number of documents that were added");

        // Integrity Check
        Db.PerformMaintenance(MaintenanceType.IntegrityCheck);

        foreach (var doc in docs) {
            DefaultCollection.Delete(doc);
        }

        DefaultCollection.Count.ShouldBe(0UL);
        // Integrity Check
        Db.PerformMaintenance(MaintenanceType.IntegrityCheck);
    }

    [Fact]
    public void TestCreateConfiguration()
    {
        var builder1 = new DatabaseConfiguration();
        var config1 = builder1;
        config1.Directory.ShouldNotBeEmpty("because the directory should have a default value");

#if COUCHBASE_ENTERPRISE
        config1.EncryptionKey.ShouldBeNull("because it was not set");
        var key = new EncryptionKey("key");
#endif

        var builder2 = new DatabaseConfiguration()
        {
            Directory = "/tmp/mydb",
#if COUCHBASE_ENTERPRISE
            EncryptionKey = key
#endif
        };

        var config2 = builder2;
        config2.Directory.ShouldBe("/tmp/mydb", "because that is what was set");

#if COUCHBASE_ENTERPRISE
        config2.EncryptionKey.ShouldBe(key, "because that is what was set");
#endif
    }

    [Fact]
    public void TestGetSetConfiguration()
    {
        var config = new DatabaseConfiguration();
        using var db = new Database("db", config);
        db.Config.Directory.ShouldBe(config.Directory, "because the directory should be the same");

#if COUCHBASE_ENTERPRISE
        db.Config.EncryptionKey.ShouldBe(config.EncryptionKey,
            "because the encryption key should be the same");
#endif
    }

    [Fact]
    public void TestCopy()
    {
        for (var i = 0; i < 10; i++) {
            var docID = $"doc{i}";
            using var doc = new MutableDocument(docID);
            doc.SetString("name", docID);

            var data = Encoding.UTF8.GetBytes(docID);
            var blob = new Blob("text/plain", data);
            doc.SetBlob("data", blob);

            DefaultCollection.Save(doc);
        }

        const string dbName = "nudb";
        var config = Db.Config;
        var dir = config.Directory;

        Database.Delete(dbName, dir);
        Db.Path.ShouldNotBeNull("because an open database should always have a path");
        Database.Copy(Db.Path!, dbName, config);

        Database.Exists(dbName, dir).ShouldBeTrue();
        using (var nuDb = new Database(dbName, config)) {
            nuDb.GetDefaultCollection().Count.ShouldBe(10UL, "because it is a copy of another database with 10 items");
            var docId = Meta.ID;
            var sDocId = SelectResult.Expression(docId);
            using (var q = QueryBuilder.Select(sDocId).From(DataSource.Collection(nuDb.GetDefaultCollection()))) {
                var rs = q.Execute();
                foreach (var r in rs) {
                    var docID = r.GetString(0);
                    docID.ShouldNotBeNull();

                    var doc = nuDb.GetDefaultCollection().GetDocument(docID);
                    doc.ShouldNotBeNull();
                    doc.GetString("name").ShouldBe(docID);

                    var blob = doc.GetBlob("data");
                    blob?.Content.ShouldNotBeNull();

                    var data = Encoding.UTF8.GetString(blob!.Content!);
                    data.ShouldBe(docID);
                }
            }
        }

        Database.Delete(dbName, dir);
    }

    [Fact]
    public void TestCreateN1QLQueryIndex()
    {
        DefaultCollection.GetIndexes().ShouldBeEmpty();

        var index1 = new ValueIndexConfiguration("firstName", "lastName");
        DefaultCollection.CreateIndex("index1", index1);

        var index2 = new FullTextIndexConfiguration("detail");
        DefaultCollection.CreateIndex("index2", index2);

        // '-' in "es-detail" caused Couchbase.Lite.CouchbaseLiteException : CouchbaseLiteException (LiteCoreDomain / 23): Invalid N1QL in index expression.
        // Basically '-' is the minus sign in N1QL expression. So needs to escape the expression string.
        // But I just couldn't get it to work...
        // var index3 = new FullTextIndexConfiguration(new string[]{ "es"+@"\-"+"detail" }, true, "es");
        var index3 = new FullTextIndexConfiguration("es_detail")
        {
            IgnoreAccents = true,
            Language = "es"
        };
            
        DefaultCollection.CreateIndex("index3", index3);

        DefaultCollection.GetIndexes().ShouldBeEquivalentToFluent(new[] { "index1", "index2", "index3" });

        using var q = DefaultCollection.CreateQuery("SELECT firstName FROM _ WHERE firstName = 'Jim'");
        var str = q.Explain();
        str.ShouldContain("USING INDEX index1", Case.Insensitive, "because the above value index should be used in the query");
    }

    [Fact]
    public void TestCreateIndex()
    {
        DefaultCollection.GetIndexes().ShouldBeEmpty();

        var lName = Expression.Property("lastName");

        var fNameItem = ValueIndexItem.Property("firstName");
        var lNameItem = ValueIndexItem.Expression(lName);

        var index1 = IndexBuilder.ValueIndex(fNameItem, lNameItem);
        DefaultCollection.CreateIndex("index1", index1);

        var detailItem = FullTextIndexItem.Property("detail");
        var index2 = IndexBuilder.FullTextIndex(detailItem);
        DefaultCollection.CreateIndex("index2", index2);

        var detailItem2 = FullTextIndexItem.Property("es-detail");
        var index3 = IndexBuilder.FullTextIndex(detailItem2).IgnoreAccents(true).SetLanguage("es");
        DefaultCollection.CreateIndex("index3", index3);

        DefaultCollection.GetIndexes().ShouldBeEquivalentToFluent(new[] { "index1", "index2", "index3" });
    }

    [Fact]
    public void TestCreateSameIndexTwice()
    {
        var item = ValueIndexItem.Expression(Expression.Property("firstName"));
        var index = IndexBuilder.ValueIndex(item);
        DefaultCollection.CreateIndex("myIndex", index);
        DefaultCollection.CreateIndex("myIndex", index);

        DefaultCollection.GetIndexes().ShouldBeEquivalentToFluent(new[] {"myIndex"});
    }

    [Fact]
    public void TestCreateSameNameIndexes()
    {
        var fName = Expression.Property("firstName");
        var lName = Expression.Property("lastName");

        var fNameItem = ValueIndexItem.Expression(fName);
        var fNameIndex = IndexBuilder.ValueIndex(fNameItem);
        DefaultCollection.CreateIndex("myIndex", fNameIndex);

        var lNameItem = ValueIndexItem.Expression(lName);
        var lNameIndex = IndexBuilder.ValueIndex(lNameItem);
        DefaultCollection.CreateIndex("myIndex", lNameIndex);

        DefaultCollection.GetIndexes().ShouldBeEquivalentToFluent(new[] {"myIndex"}, "because lNameIndex should overwrite fNameIndex");

        var detailItem = FullTextIndexItem.Property("detail");
        var detailIndex = IndexBuilder.FullTextIndex(detailItem);
        DefaultCollection.CreateIndex("myIndex", detailIndex);

        DefaultCollection.GetIndexes().ShouldBeEquivalentToFluent(new[] { "myIndex" }, "because detailIndex should overwrite lNameIndex");
    }

    [Fact]
    public void TestDeleteIndex()
    {
        TestCreateIndex();

        DefaultCollection.DeleteIndex("index1");
        DefaultCollection.GetIndexes().ShouldBeEquivalentToFluent(new[] {"index2", "index3"});

        DefaultCollection.DeleteIndex("index2");
        DefaultCollection.GetIndexes().ShouldBeEquivalentToFluent(new[] { "index3" });

        DefaultCollection.DeleteIndex("index3");
        DefaultCollection.GetIndexes().ShouldBeEmpty();

        DefaultCollection.DeleteIndex("dummy");
        DefaultCollection.DeleteIndex("index1");
        DefaultCollection.DeleteIndex("index2");
        DefaultCollection.DeleteIndex("index3");
    }

    [Fact]
    public void TestGetDocFromDeletedDB()
    {
        using var _ = GenerateDocument("doc1");
        Db.Delete();

        Should.Throw<CouchbaseLiteException>(() => Db.GetDefaultCollection().GetDocument("doc1"))
            .Error.ShouldBe(CouchbaseLiteError.NotOpen);
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
        using var mDoc1 = new MutableDocument("abc");
        using var mDoc2 = new MutableDocument("abc");
        mDoc1.SetString("someKey", "someVar");
        mDoc2.SetString("someKey", "newVar");

        // This causes a conflict, the default conflict resolver should be applied
        DefaultCollection.Save(mDoc1);
        DefaultCollection.Save(mDoc2);

        // NOTE: Both doc1 and doc2 are generation 1.  Last write should win.
        DefaultCollection.Count.ShouldBe(1UL);
        using var doc = DefaultCollection.GetDocument("abc");
        doc.ShouldNotBeNull();
        doc.GetString("someKey").ShouldBe("newVar", "because the last write should win");
    }

    [ForIssue("couchbase-lite-android/1416")]
    [Fact]
    public void TestDeleteAndOpenDB()
    {
        using var database1 = new Database("application");
        database1.Delete();

        using var database2 = new Database("application");
        database2.InBatch(() =>
        {
            for (var i = 0; i < 100; i++) {
                using var doc = new MutableDocument();
                doc.SetInt("index", i);
                for (var j = 0; j < 10; j++) {
                    doc.SetInt($"item_{j}", j);
                }

                database2.GetDefaultCollection().Save(doc);
            }
        });
    }

    [Fact]
    public void TestDatabaseSaveAndGetCookies()
    {
        var uri = new Uri("http://example.com/");
        var cookieStr = "id=a3fWa; Domain=.example.com; Secure; HttpOnly";
        Db.SaveCookie(cookieStr, uri, false).ShouldBeTrue("because otherwise the cookie did not save");
        Db.GetCookies(uri).ShouldBe("id=a3fWa");
        cookieStr = "id=a3fWa; Domain=www.example.com; Secure; HttpOnly";
        Db.SaveCookie(cookieStr, uri, false).ShouldBeTrue("because otherwise the cookie did not save");
        Db.GetCookies(uri).ShouldBe("id=a3fWa");
        uri = new Uri("http://www.example.com/");

        //cookieStr = "id=a3fWa; Domain=.example.com; Secure; HttpOnly";
        //No longer throw exception. Will log warning messages.
        //Action badAction = (() => Db.SaveCookie(cookieStr, uri));
        //badAction.Should().Throw<CouchbaseLiteException>(); //CouchbaseLiteException (LiteCoreDomain / 9): Invalid cookie.

        cookieStr = "id=a3fWa; Domain=www.example.com; Secure; HttpOnly";
        Db.SaveCookie(cookieStr, uri, false).ShouldBeTrue("because otherwise the cookie did not save");
        Db.GetCookies(uri).ShouldBe("id=a3fWa; id=a3fWa");

        uri = new Uri("http://foo.example.com");
        cookieStr = "id=a3fWa; Domain=.example.com; Secure; HttpOnly";
        Db.SaveCookie(cookieStr, uri, true).ShouldBeTrue("because otherwise the cookie did not save");
        Db.SaveCookie(cookieStr, uri, false).ShouldBeFalse("because otherwise the cookie saved improperly");
    }

    [ForIssue("CBL-3947")]
    [Fact]
    public void TestCookiesExpiration()
    {
        var uri = new Uri("http://exampletest.com/");
        const string cookieStr = "id=a3fWa; Expires=Wed, 21 Oct 2015 07:28:00 GMT; Secure; HttpOnly";
        Db.SaveCookie(cookieStr, uri, false).ShouldBeTrue("because otherwise the cookie did not save");
        Db.GetCookies(uri).ShouldBeNull("cookie is expired");

        string[] noneExpiredCookies =
        [
            // RFC 822, updated by RFC 1123
            "id=a3fWa;expires=Wed, 06 Jan 2100 05:54:52 GMT;Path=/",
            "id=a3fWa;expires=Mon, 04 Jan 2100 05:54:52 GMT;Path=/",
            // ANSI C's time format
            "id=a3fWa;expires=Wed Jan  6 05:54:52 2100       ;Path=/",
            "id=a3fWa;expires=Mon Jan  4 05:54:52 2100;Path=/",
            // GCLB cookie format
            "id=a3fWa; path=/; HttpOnly; expires=Mon, 4-Jan-2100 05:54:52 GMT",
            "id=a3fWa;path=/;HttpOnly;expires=Mon, 4-Jan-2100 05:54:52 GMT"
        ];

        foreach (var cookie in noneExpiredCookies) {
            Db.SaveCookie(cookie, uri, false).ShouldBeTrue("because otherwise the cookie did not save");
            Db.GetCookies(uri).ShouldBe("id=a3fWa");
        }
    }

    private void WithActiveLiveQueries(bool isCloseNotDelete)
    {
        Database.Delete("closeDB", Db.Config.Directory);
        using (var otherDb = new Database("closeDB", Db.Config)) {
            var otherDefaultColl = otherDb.GetDefaultCollection();
            var query = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Collection(otherDefaultColl));
            using var doc1Listener = new WaitAssert();
            query.AddChangeListener(null, (_, args) => {
                foreach (var row in args.Results) {
                    if (row.GetString("id") == "doc1") {
                        doc1Listener.Fulfill();
                    }
                }
            });

            using (var doc = new MutableDocument("doc1")) {
                doc.SetString("value", "string");
                otherDefaultColl.Save(doc); // Should still trigger since it is pointing to the same DB
            }

            otherDb.ActiveStoppables.Count.ShouldBe(1);

            doc1Listener.WaitForResult(TimeSpan.FromSeconds(20));

            if (isCloseNotDelete)
                otherDb.Close();
            else
                otherDb.Delete();

            otherDb.ActiveStoppables.Count.ShouldBe(0);
            otherDb.IsClosedLocked.ShouldBe(true);
        }

        Database.Delete("closeDB", Db.Config.Directory);
    }

    private bool ResolveConflict(MutableDocument updatedDoc, Document? currentDoc)
    {
        var updateDocDict = updatedDoc.ToDictionary();
        var curDocDict = currentDoc?.ToDictionary();
        if(curDocDict == null) {
            return false;
        }

        foreach (var value in curDocDict)
            if (updateDocDict.ContainsKey(value.Key) && !value.Value?.Equals(updateDocDict[value.Key]) == true)
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
            System.IO.Directory.Exists(path)
                .ShouldBeTrue("because the database should exist if it is going to be deleted");
        }

        db.Delete();
        if (path != null) {
            System.IO.Directory.Exists(path).ShouldBeFalse("because the database should not exist anymore");
        }
    }

    private MutableDocument GenerateDocument(string docID)
    {
        var doc = new MutableDocument(docID);
        doc.SetInt("key", 1);

        DefaultCollection.Save(doc);
        DefaultCollection.Count.ShouldBe(1UL, "because this is the first document");
        doc.Sequence.ShouldBe(1UL, "because this is the first document");
        return doc;
    }

    private void VerifyGetDocument(string docID, Database? db = null, int value = 1)
    {
        db ??= Db;
        var doc = db.GetDefaultCollection().GetDocument(docID);
        doc.ShouldNotBeNull($"because otherwise document '{docID}' doesn't exist");
        doc.Id.ShouldBe(docID, "because that was the requested ID");
        doc.IsDeleted.ShouldBeFalse("because the test uses a non-deleted document");
        doc.GetInt("key").ShouldBe(value, "because that is the value that was passed as expected");
    }

    private IList<Document> CreateDocs(int n)
    {
        var docs = new List<Document>();
        for (var i = 0; i < n; i++) {
            var doc = new MutableDocument($"doc_{i:D3}");
            doc.SetInt("key", i);
            DefaultCollection.Save(doc);
            docs.Add(doc);
        }

        DefaultCollection.Count.ShouldBe((ulong)n, "because otherwise an incorrect number of documents were made");
        return docs;
    }

    private void ValidateDocs(int n)
    {
        for (var i = 0; i < n; i++) {
            VerifyGetDocument($"doc_{i:D3}", value: i);
        }
    }

    private void TestSaveNewDoc(string docID)
    {
        GenerateDocument(docID);

        DefaultCollection.Count.ShouldBe(1UL, "because the database only has one document");

        VerifyGetDocument(docID);
    }

    private void PurgeDocAndVerify(Document doc)
    {
        var docID = doc.Id;
        DefaultCollection.Purge(doc);
        DefaultCollection.GetDocument(docID).ShouldBeNull("because it no longer exists");
    }

    private static Document StoreBlob(Database db, MutableDocument doc, byte[] content)
    {
        var blob = new Blob("text/plain", content);
        doc.SetBlob("data", blob);
        var coll = db.GetDefaultCollection();
        coll.Save(doc);
        var retrieved = coll.GetDocument(doc.Id);
        retrieved.ShouldNotBeNull("because otherwise the save failed");
        return retrieved;
    }
}