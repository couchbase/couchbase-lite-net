//
//  ScopeCollectionTest.cs
//
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite;
using Couchbase.Lite.Query;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public class ScopeCollectionTest : TestCase
    {
#if !WINDOWS_UWP
        public ScopeCollectionTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestDefaultCollectionExists()
        {
            var defaultColl = Db.GetDefaultCollection();
            defaultColl.Should().NotBeNull("default collection is not null");
            defaultColl.Name.Should().Be(Database._defaultCollectionName, $"default collection name is {Database._defaultCollectionName}");
            var collections = Db.GetCollections();
            collections.Contains(defaultColl).Should().BeTrue("the default collection is included in the collection list when calling Database.GetCollections()");
            var scope = defaultColl.Scope;
            scope.Should().NotBeNull("the scope of the default collection is not null");
            scope.Name.Should().Be(Database._defaultScopeName, $"default collection name is {Database._defaultScopeName}");
            Db.GetCollection(Database._defaultCollectionName).Should().Be(defaultColl);
            defaultColl.Count.Should().Be(0, "default collection’s count is 0");
        }

        [Fact]
        public void TestDefaultScopeExists()
        {
            var defaultScope = Db.GetDefaultScope();
            defaultScope.Should().NotBeNull("default scope is not null");
            defaultScope.Name.Should().Be(Database._defaultScopeName, $"default scope name is {Database._defaultScopeName}");
            var scopes = Db.GetScopes();
            scopes.Contains(defaultScope).Should().BeTrue("the default scope is included in the scope list when calling Database.GetScopes()");
        }

        [Fact]
        public void TestDeleteDefaultCollection()
        {
            Db.DeleteCollection(Database._defaultCollectionName);
            var defaultColl = Db.GetDefaultCollection();
            defaultColl.Should().BeNull("default collection is deleted");
            Action badAction = (() => Db.CreateCollection(Database._defaultCollectionName));
            badAction.Should().Throw<CouchbaseLiteException>("Cannot recreate the default collection.");
            defaultColl = Db.GetDefaultCollection();
            defaultColl.Should().BeNull("default collection cannot be recreated, so the value is still null");
        }

        [Fact]
        public void TestGetDefaultScopeAfterDeleteDefaultCollection()
        {
            Db.DeleteCollection(Database._defaultCollectionName);
            var defaultScope = Db.GetDefaultScope();
            defaultScope.Should().NotBeNull("scope still exists after the default collection is deleted");
            var scopes = Db.GetScopes();
            scopes.Contains(defaultScope).Should().BeTrue("the default scope is included in the scope list when calling Database.GetScopes()");
            defaultScope.Name.Should().Be(Database._defaultScopeName, $"default scope name is {Database._defaultScopeName}");
        }

        [Fact]
        public void TestCreateAndGetCollectionsInDefaultScope()
        {
            var colA = Db.CreateCollection("colA");
            var colB = Db.CreateCollection("colB");
            var colC = Db.CreateCollection("colC", Database._defaultScopeName);
            //the created collection objects have the correct name and scope.
            colA.Name.Should().Be("colA", "object colA has the correct name colA");
            colA.Scope.Name.Should().Be(Database._defaultScopeName, $"objects colA has the correct scope {Database._defaultScopeName}");
            colB.Name.Should().Be("colB", "object colB has the correct name colB");
            colB.Scope.Name.Should().Be(Database._defaultScopeName, $"objects colB has the correct scope {Database._defaultScopeName}");
            colC.Name.Should().Be("colC", "object colC has the correct name colC");
            colC.Scope.Name.Should().Be(Database._defaultScopeName, $"objects colC has the correct scope {Database._defaultScopeName}");
            //the created collections exist when calling database.GetCollection(name: String)
            Db.GetCollection("colA").Should().Be(colA);
            Db.GetCollection("colB").Should().Be(colB);
            Db.GetCollection("colC").Should().Be(colC);
            //the created collections are in the list when calling database.GetCollections().
            var colls = Db.GetCollections();
            colls.Contains(colA).Should().BeTrue();
            colls.Contains(colB).Should().BeTrue();
            colls.Contains(colC).Should().BeTrue();
        }

        [Fact]
        public void TestCreateAndGetCollectionsInNamedScope()
        {
            Db.GetScope("scopeA").Should().BeNull("Because there is no scope scopeA in Database");
            Db.CreateCollection("colA", "scopeA");
            var scopeA = Db.GetScope("scopeA");
            scopeA.Should().NotBeNull("Because scope scopeA was created in Database two lines ago.");
            scopeA.Name.Should().Be("scopeA", "the created collection has the correct scope scopeA");
            var colA = scopeA.GetCollection("colA");
            colA.Name.Should().Be("colA", "the created collections have the correct name colA");
            var scopes = Db.GetScopes();
            scopes.Contains(scopeA).Should().BeTrue("the created collection’s scope is in the list when calling Database.GetScopes()");
            var collections = Db.GetCollections("scopeA");
            collections.Contains(colA).Should().BeTrue("the created collection is in the list from Database.GetCollections with scopeA");
        }

        [Fact]
        public void TestGetNonExistingCollection()
        {
            var col = Db.GetCollection("colA", "scoppeA");
            col.Should().BeNull("No collection colA existed");
        }

        [Fact]
        public void TestGetCollectionsFromScope()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var scope = Db.GetScope("scopeA");
            scope.GetCollection("colA").Should().Be(colA, "Because collection colA is in scopeA");
            scope.GetCollection("colB").Should().Be(colB, "Because collection colB is in scopeA");
            //Get all of the created collections by using scope.getCollections() API. Ensure that the collections are returned correctly.
            var cols = Db.GetCollections("scopeA");
            cols.Count.Should().Be(2, "total 2 collection is added in the Database");
            // Check if collections order is required
            cols.Contains(colA).Should().BeTrue();
            cols.Contains(colB).Should().BeTrue();
        }

        [Fact]
        public void TestDeleteAllCollectionsInScope()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var scopeA = Db.GetScope("scopeA");
            var collectionsInScopeA = scopeA.GetCollections();
            collectionsInScopeA.Count.Should().Be(2, "Because 2 collections were just added in the Database.");
            collectionsInScopeA.Contains(colA).Should().BeTrue("Because collecton colA is in scopeA");
            collectionsInScopeA.Contains(colB).Should().BeTrue("Because collecton colB is in scopeA");
            Db.DeleteCollection("colA", "scopeA");
            scopeA.GetCollections().Count.Should().Be(1, "Collections count should be 1 because colA is deleted from scopeA");
            Db.DeleteCollection("colB", "scopeA");
            Db.GetScope("scopeA").Should().BeNull();
            Db.GetCollection("colA", "scopeA").Should().BeNull("because colA is deleted from scopeA");
            Db.GetCollection("colB", "scopeA").Should().BeNull("because colB is deleted from scopeA");
        }

        [Fact]
        public void TestCollectionNameWithValidChars()
        {
            // None default Collection and Scope Names are allowed to contain the following characters 
            // A - Z, a - z, 0 - 9, and the symbols _, -, and % and to start with A-Z, a-z, 0-9, and -
            var str = "_%";
            for (char letter = 'A'; letter <= 'Z'; letter++) {
                Db.CreateCollection(letter + str).Should().NotBeNull($"Valid collection name '{letter + str}'.");
            }

            // TODO : wait for CBL-3195 fix
            //for (char letter = 'a'; letter <= 'z'; letter++) {
            //    Db.CreateCollection(letter + str).Should().NotBeNull($"Valid collection name '{letter + str}'.");
            //}

            for (char letter = '0'; letter <= '9'; letter++) {
                Db.CreateCollection(letter + str).Should().NotBeNull($"Valid collection name '{letter + str}'.");
            }

            Db.CreateCollection("-" + str).Should().NotBeNull($"Valid collection name '{"-" + str}'.");
        }

        [Fact]
        public void TestCollectionNameStartWithIllegalChars()
        {
            // None default Collection and Scope Names start with _ and % are prohibited
            Action badAction = (() => Db.CreateCollection("_"));
            badAction.Should().Throw<CouchbaseLiteException>("Invalid collection name '_' in scope '_default'.");
            badAction = (() => Db.CreateCollection("%"));
            badAction.Should().Throw<CouchbaseLiteException>("Invalid collection name '%' in scope '_default'.");
        }

        [Fact]
        public void TestCollectionNameContainingIllegalChars()
        {
            var str = "ab";
            /* Create collections with the collection name containing the following characters
               !, @, #, $, ^, &, *, (, ), +, -, ., <, >, ?, [, ], {, }, =, “, ‘, |, \, /,`,~ are prohibited. */
            for (char letter = '!'; letter <= '/'; letter++) {
                if (letter == '%')
                    return;

                Action badAction = (() => Db.CreateCollection(str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid collection name '{str + letter}' in scope '_default'.");
            }

            for (char letter = '<'; letter <= '@'; letter++) {
                Action badAction = (() => Db.CreateCollection(str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid collection name '{str + letter}' in scope '_default'.");
            }

            for (char letter = '['; letter <= '`'; letter++) {
                Action badAction = (() => Db.CreateCollection(str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid collection name '{str + letter}' in scope '_default'.");
            }

            for (char letter = '{'; letter <= '~'; letter++) {
                Action badAction = (() => Db.CreateCollection(str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid collection name '{str + letter}' in scope '_default'.");
            }
        }

        [Fact]
        public void TestCollectionNameLength()
        {
            /* Collection name should contain valid char with length from 1 to 251 */
            var collName = "";
            for (int i = 0; i < 251; i++) {
                collName += 'c';
                collName.Length.Should().Be(i + 1);
                Db.CreateCollection(collName).Should().NotBeNull($"Valid collection '{collName}' length {collName.Length}.");
            }

            var str = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_%-cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
            str.Length.Should().Be(251);
            Db.CreateCollection(str).Should().NotBeNull($"because the collection name can be length at {str.Length}");

            str += "e";
            str.Length.Should().Be(252);
            Action badAction = (() => Db.CreateCollection(str));
            badAction.Should().Throw<CouchbaseLiteException>($"Invalid collection name '{str}' in scope because the collection name length {str.Length} is over naming length limit.");
        }

        [Fact] // I think this test case is a dup of TestCollectionNameWithValidChars?
        public void TestCollectionNameCaseSensitive()
        {
            Db.CreateCollection("COLLECTION1").Should().NotBeNull();
            // TODO : wait for CBL-3195 fix
            //Db.CreateCollection("collection1").Should().NotBeNull("Should be able to be created because collection name is case sensitive.");
            //This will already return non-null, it will just be the same as COLLECTION1. The test should ensure they are different from each other (e.g. independent documents)
        }

        [Fact]
        public void TestScopeNameWithValidChars()
        {
            // None default Collection and Scope Names are allowed to contain the following characters 
            // A - Z, a - z, 0 - 9, and the symbols _, -, and % and to start with A-Z, a-z, 0-9, and -
            var str = "_%";
            for (char letter = 'A'; letter <= 'Z'; letter++) {
                Db.CreateCollection("abc", letter + str).Should().NotBeNull($"Valid scope name '{letter + str}'.");
            }

            // TODO : wait for CBL-3195 fix
            //for (char letter = 'a'; letter <= 'z'; letter++) {
            //    Db.CreateCollection("abc", letter + str).Should().NotBeNull($"Valid scope name '{letter + str}'.");
            //}

            for (char letter = '0'; letter <= '9'; letter++) {
                Db.CreateCollection("abc", letter + str).Should().NotBeNull($"Valid scope name '{letter + str}'.");
            }

            Db.CreateCollection("abc", "-" + str).Should().NotBeNull($"Valid scope name '{"-" + str}'.");
        }

        [Fact]
        public void TestScopeNameStartWithIllegalChars()
        {
            // None default Collection and Scope Names start with _ and % are prohibited
            Action badAction = (() => Db.CreateCollection("abc", "_"));
            badAction.Should().Throw<CouchbaseLiteException>("Invalid scope name '_'.");
            badAction = (() => Db.CreateCollection("abc", "%"));
            badAction.Should().Throw<CouchbaseLiteException>("Invalid scope name '%'.");
        }

        [Fact]
        public void TestScopeNameContainingIllegalChars()
        {
            var str = "ab";
            /* Create collections with the collection name containing the following characters
               !, @, #, $, ^, &, *, (, ), +, -, ., <, >, ?, [, ], {, }, =, “, ‘, |, \, /,`,~ are prohibited. */
            for (char letter = '!'; letter <= '/'; letter++) {
                if (letter == '%')
                    return;
                Action badAction = (() => Db.CreateCollection("abc", str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid scope name '{str + letter}'.");
            }

            for (char letter = '<'; letter <= '@'; letter++) {
                Action badAction = (() => Db.CreateCollection("abc", str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid scope name '{str + letter}'.");
            }

            for (char letter = '['; letter <= '`'; letter++) {
                Action badAction = (() => Db.CreateCollection("abc", str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid scope name '{str + letter}'.");
            }

            for (char letter = '{'; letter <= '~'; letter++) {
                Action badAction = (() => Db.CreateCollection("abc", str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid scope name '{str + letter}'.");
            }
        }

        [Fact]
        public void TestScopeNameLength()
        {
            /* Collection name should contain valid char with length from 1 to 251 */
            var collName = "";
            for (int i = 0; i < 251; i++) {
                collName += 'c';
                collName.Length.Should().Be(i + 1);
                Db.CreateCollection("abc", collName).Should().NotBeNull($"Valid scope '{collName}' length {collName.Length}.");
            }

            var str = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_%-cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
            str.Length.Should().Be(251);
            Db.CreateCollection("abc", str).Should().NotBeNull($"because the scope name can be length at {str.Length}");

            str += "e";
            str.Length.Should().Be(252);
            Action badAction = (() => Db.CreateCollection("abc", str));
            badAction.Should().Throw<CouchbaseLiteException>($"Invalid scope name '{str}' because the scope name length {str.Length} is over naming length limit.");
        }

        [Fact] // I think this test case is dup of TestScopeNameWithValidChars
        public void TestScopeNameCaseSensitive()
        {
            Db.CreateCollection("abc", "SCOPE1").Should().NotBeNull();
            // TODO : wait for CBL-3195 fix
            //Db.CreateCollection("abc", "scope1").Should().NotBeNull("Should be able to be created because scope name is case sensitive.");
        }

        /* TODO CBL-3227 8.3 Collections and Cross Database Instance
TestCreateThenGetCollectionFromDifferentDatabaseInstance : Test that creating a collection from a database instance is visible to the other database instance.
Create Database instance A and B.
Create a collection in a scope from the database instance A.
Ensure that the created collection is visible to the database instance B by using database.getCollection(name: "colA", scope: "scopeA") and database.getCollections(scope: "scopeA") API.
        */
        [Fact]
        public void TestCreateAnExistingCollection()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA")) {
                using (var doc = new MutableDocument("doc"))
                using (var doc1 = new MutableDocument("doc1"))
                using (var doc2 = new MutableDocument("doc2")) {
                    doc.SetString("str", "string");
                    doc1.SetString("str1", "string1");
                    doc2.SetString("str2", "string2");
                    colA.Save(doc);
                    colA.Save(doc1);
                    colA.Save(doc2);
                }
            }

            using (var colASame = Db.CreateCollection("colA", "scopeA")) {
                colASame.GetDocument("doc").GetString("str").Should().Be("string");
                colASame.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colASame.GetDocument("doc2").GetString("str2").Should().Be("string2");
            }
        }

        [Fact]
        public void TestDeleteCollection()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA"))
            using (var colB = Db.CreateCollection("colB", "scopeA")) {
                using (var doc = new MutableDocument("doc"))
                using (var doc1 = new MutableDocument("doc1"))
                using (var doc2 = new MutableDocument("doc2")) {
                    doc.SetString("str", "string");
                    doc1.SetString("str1", "string1");
                    doc2.SetString("str2", "string2");
                    colA.Save(doc);
                    colA.Save(doc1);
                    colA.Save(doc2);
                }

                colA.Count.Should().Be(3, "3 docs were added into colA");
                Db.DeleteCollection("colA", "scopeA");
                Db.GetCollection("colA", "scopeA").Should().BeNull("colA is deleted.");
                var colls = Db.GetCollections("scopeA");
                colls.Contains(colA).Should().BeFalse("the collection colA is already deleted.");
                var colANew = Db.CreateCollection("colA", "scopeA");
                colANew.Should().NotBeNull("collection colA should create successfully");
                colANew.Count.Should().Be(0, "no doc were added in the newly created collection");
            }
        }

        [Fact]
        public void TestCreateDocGetCollectionFromDifferentDatabaseInstance()
        {
            using (var colA = Db.CreateCollection("colA")) {
                using (var doc = new MutableDocument("doc"))
                using (var doc1 = new MutableDocument("doc1"))
                using (var doc2 = new MutableDocument("doc2")) {
                    doc.SetString("str", "string");
                    doc1.SetString("str1", "string1");
                    doc2.SetString("str2", "string2");
                    colA.Save(doc);
                    colA.Save(doc1);
                    colA.Save(doc2);
                }

                using (var otherDB = OpenDB(Db.Name)) {
                    var colAInOtherDb = otherDB.GetCollection("colA");
                    //colAInOtherDb.Count.Should().Be(3); // bug
                    var docOfColAInOtherDb = colAInOtherDb.GetDocument("doc");
                    colAInOtherDb.Count.Should().Be(3);
                    docOfColAInOtherDb.GetString("str").Should().Be("string");
                    colAInOtherDb.Delete(docOfColAInOtherDb);
                }

                colA.GetDocument("doc").Should().BeNull();
            }
        }

        [Fact]
        public void TestCreateThenGetCollectionFromDifferentDatabaseInstance()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA"))
            using (var otherDB = OpenDB(Db.Name)) {
                //TODO wait for CBL-3298 fix
                //I am using hasScope to check existance of the scope obj in order to use scope obj to get the collections
                var cols = otherDB.GetCollections(scope: "scopeA");
                //cols.Contains(colA).Should().BeTrue();
            }
        }

        /*TestDeleteThenGetCollectionFromDifferentDatabaseInstance : Test that deleting a collection from a database instance is visible to the other database instance.
        Create Database instance A and B.
        Create a collection in a scope from the database instance A.
        Add some documents to the created collection.
        Ensure that the created collection is visible to the database instance B by using database.getCollection(name: "colA", scope: "scopeA") and database.getCollections(scope: "scopeA") API.
        Ensure that the collection from the database instance B has the correct number of document counts.
        Delete the collection from the database instance A.
        Get the document count from the collection getting from the database instance B. Ensure that the document count is 0.
        Ensure that the collection is null when getting the collection from the database instance B again by using database.getCollection(name: "colA", scope: "scopeA").
        Ensure that the collection is not included when getting all collections from the database instance B again by using database.getCollections(scope: "scopeA").
        */
        [Fact]
        public void TestDeleteThenGetCollectionFromDifferentDatabaseInstance()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA"))
            using (var colB = Db.CreateCollection("colB", "scopeA")) { 
                using (var doc = new MutableDocument("doc"))
                using (var doc1 = new MutableDocument("doc1"))
                using (var doc2 = new MutableDocument("doc2")) {
                    doc.SetString("str", "string");
                    doc1.SetString("str1", "string1");
                    doc2.SetString("str2", "string2");
                    colA.Save(doc);
                    colA.Save(doc1);
                    colA.Save(doc2);
                }

                using (var otherDB = OpenDB(Db.Name)) {
                    //TODO wait for CBL-3298 fix
                    //I am using hasScope to check existance of the scope obj in order to use scope obj to get the collection
                    var colAinOtherDb = otherDB.GetCollection("colA", "scopeA");
                    //colAinOtherDb.Count.Should().Be(3);
                    Db.DeleteCollection("colA", "scopeA");
                    //colAinOtherDb.Count.Should().Be(0);
                    colAinOtherDb = otherDB.GetCollection("colA", "scopeA");
                    colAinOtherDb.Should().BeNull();
                    var collsInOtherDb = otherDB.GetCollections("scopeA");
                    //collsInOtherDb.Contains(colA).Should().BeFalse();
                }
            }
        }

        /*TestDeleteAndRecreateThenGetCollectionFromDifferentDatabaseInstance : Test that deleting a collection then recreating the collection from a database instance is visible to the other database instance.
        Create Database instance A and B.
        Create a collection in a scope from the database instance A.
        Add some documents to the created collection.
        Ensure that the created collection is visible to the database instance B by using database.getCollection(name: "colA", scope: "scopeA") and database.getCollections(scope: "scopeA") API.
        Ensure that the collection from the database instance B has the correct number of document counts.
        Delete the collection from the database instance A and recreate the collection using the database instance A.
        Get the document count from the collection getting from the database instance B. Ensure that the document count is 0.
        Ensure that the collection is not null and is different from the instance gotten before from the instanceB when getting the collection from the database instance B by using database.getCollection(name: "colA", scope: "scopeA").
        Ensure that the collection is included when getting all collections from the database instance B by using database.getCollections(scope: "scopeA").
                 */
        [Fact]
        public void TestDeleteAndRecreateThenGetCollectionFromDifferentDatabaseInstance()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA"))
            using (var colB = Db.CreateCollection("colB", "scopeA")) {
                using (var doc = new MutableDocument("doc"))
                using (var doc1 = new MutableDocument("doc1"))
                using (var doc2 = new MutableDocument("doc2")) {
                    doc.SetString("str", "string");
                    doc1.SetString("str1", "string1");
                    doc2.SetString("str2", "string2");
                    colA.Save(doc);
                    colA.Save(doc1);
                    colA.Save(doc2);
                }
          

                using (var otherDB = OpenDB(Db.Name)) {
                //TODO wait for CBL-3298 fix
                //I am using hasScope to check existance of the scope obj in order to use scope obj to get the collection
                //Add test case after CBL-3298 is fixed..
                }  
            }
        }

        [Fact]
        public void TestUseCollectionAPIsOnDeletedCollection()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA")) {
                using (var doc = new MutableDocument("doc")) {
                    doc.SetString("str", "string");
                    colA.Save(doc);
                }

                colA.GetDocument("doc").GetString("str").Should().Be("string");

                Db.DeleteCollection("colA", "scopeA");

                colA.Invoking(d => d.GetDocument("doc"))
                    .Should().Throw<CouchbaseLiteException>("Because GetDocument after collection colA is deleted.");

                var dto30 = DateTimeOffset.UtcNow.AddSeconds(30);
                using (var doc1 = new MutableDocument("doc1")) {
                    doc1.SetString("str", "string");

                    colA.Invoking(d => d.Save(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Save after collection colA is deleted.");

                    colA.Invoking(d => d.Delete(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Delete after collection colA is deleted.");

                    colA.Invoking(d => d.Purge(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Purge after collection colA is deleted.");

                    colA.Invoking(d => d.SetDocumentExpiration("doc1", dto30))
                        .Should().Throw<CouchbaseLiteException>("Because SetDocumentExpiration after collection colA is deleted.");

                    colA.Invoking(d => d.GetDocumentExpiration("doc1"))
                        .Should().Throw<CouchbaseLiteException>("Because GetDocumentExpiration after collection colA is deleted.");
                }

                colA.Invoking(d => d.CreateQuery($"SELECT firstName, lastName FROM *"))
                        .Should().Throw<CouchbaseLiteException>("Because CreateQuery after collection colA is deleted.");

                var index1 = new ValueIndexConfiguration(new string[] { "firstName", "lastName" });
                colA.Invoking(d => d.CreateIndex("index1", index1))
                    .Should().Throw<CouchbaseLiteException>("Because CreateIndex after collection colA is deleted.");

                colA.Invoking(d => d.GetIndexes())
                    .Should().Throw<CouchbaseLiteException>("Because GetIndexes after collection colA is deleted.");

                colA.Invoking(d => d.DeleteIndex("index1"))
                    .Should().Throw<CouchbaseLiteException>("Because DeleteIndex after collection colA is deleted.");

                colA.Invoking(d => d.AddChangeListener(null, (sender, args) => { }))
                    .Should().Throw<CouchbaseLiteException>("Because AddChangeListener after collection colA is deleted.");

                colA.Invoking(d => d.AddDocumentChangeListener("doc1", (sender, args) => { }))
                    .Should().Throw<CouchbaseLiteException>("Because AddDocumentChangeListener after collection colA is deleted.");

                colA.Invoking(d => d.RemoveChangeListener(d.AddDocumentChangeListener("doc1", (sender, args) => { })))
                    .Should().Throw<CouchbaseLiteException>("Because RemoveChangeListener after collection colA is deleted.");
            }
        }

        // Test that using the Collection APIs on the deleted collection which is deleted from the different database instance
        // returns the result as expected based on section 6.2.
        //[Fact] wait CBL-3298
        public void TestUseCollectionAPIOnDeletedCollectionDeletedFromDifferentDBInstance()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA")) {
                using (var doc = new MutableDocument("doc")) {
                    doc.SetString("str", "string");
                    colA.Save(doc);
                }
            }

            using (var otherDB = OpenDB(Db.Name)) {
                // CBL-3298 hasScope() returns false
                otherDB.GetCollection("colA", "scopeA").GetDocument("doc").GetString("str").Should().Be("string");

                otherDB.DeleteCollection("colA", "scopeA");

                var colA1 = otherDB.GetCollection("colA", "scopeA");

                colA1.Invoking(d => d.GetDocument("doc"))
                    .Should().Throw<CouchbaseLiteException>("Because GetDocument after collection colA is deleted from the other db.");

                var dto30 = DateTimeOffset.UtcNow.AddSeconds(30);
                using (var doc1 = new MutableDocument("doc1")) {
                    doc1.SetString("str", "string");

                    colA1.Invoking(d => d.Save(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Save after collection colA is deleted from the other db.");

                    colA1.Invoking(d => d.Delete(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Delete after collection colA is deleted from the other db.");

                    colA1.Invoking(d => d.Purge(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Purge after collection colA is deleted from the other db.");

                    colA1.Invoking(d => d.SetDocumentExpiration("doc1", dto30))
                        .Should().Throw<CouchbaseLiteException>("Because SetDocumentExpiration after collection colA is deleted from the other db.");

                    colA1.Invoking(d => d.GetDocumentExpiration("doc1"))
                        .Should().Throw<CouchbaseLiteException>("Because GetDocumentExpiration after collection colA is deleted from the other db.");
                }

                colA1.Invoking(d => d.CreateQuery($"SELECT firstName, lastName FROM *"))
                        .Should().Throw<CouchbaseLiteException>("Because CreateQuery after collection colA is deleted from the other db.");

                var index1 = new ValueIndexConfiguration(new string[] { "firstName", "lastName" });
                colA1.Invoking(d => d.CreateIndex("index1", index1))
                    .Should().Throw<CouchbaseLiteException>("Because CreateIndex after collection colA is deleted from the other db.");

                colA1.Invoking(d => d.GetIndexes())
                    .Should().Throw<CouchbaseLiteException>("Because GetIndexes after collection colA is deleted from the other db.");

                colA1.Invoking(d => d.DeleteIndex("index1"))
                    .Should().Throw<CouchbaseLiteException>("Because DeleteIndex after collection colA is deleted from the other db.");

                colA1.Invoking(d => d.AddChangeListener(null, (sender, args) => { }))
                .Should().Throw<CouchbaseLiteException>("Because AddChangeListener after collection colA is deleted from the other db.");

                colA1.Invoking(d => d.AddDocumentChangeListener("doc1", (sender, args) => { }))
                    .Should().Throw<CouchbaseLiteException>("Because AddDocumentChangeListener after collection colA is deleted from the other db.");

                colA1.Invoking(d => d.RemoveChangeListener(d.AddDocumentChangeListener("doc1", (sender, args) => { })))
                    .Should().Throw<CouchbaseLiteException>("Because RemoveChangeListener after collection colA is deleted from the other db.");
            }
        }

        [Fact]
        public void TestUseCollectionAPIsWhenDatabaseIsClosed() => TestUseCollectionAPIs(() => Db.Close());

        [Fact]
        public void TestUseCollectionAPIsWhenDatabaseIsDeleted() => TestUseCollectionAPIs(() => Db.Delete());

        [Fact]
        public void TestUseScopeWhenDatabaseIsClosed() => TestUseScope(() => Db.Close());

        [Fact]
        public void TestUseScopeWhenDatabaseIsDeleted() => TestUseScope(() => Db.Delete());

        [Fact]
        public void TestGetScopesOrCollectionsWhenDatabaseIsClosed() => TestGetScopesOrCollections(() => Db.Close());

        [Fact]
        public void TestGetScopesOrCollectionsWhenDatabaseIsDeleted() => TestGetScopesOrCollections(() => Db.Delete());

        [Fact]
        public void TestUseDatabaseAPIsWhenDefaultCollectionIsDeleted()
        {
            using (var defaultCol = Db.GetDefaultCollection()) {
                using (var doc = new MutableDocument("doc")) {
                    doc.SetString("str", "string");
                    defaultCol.Save(doc);
                }

                defaultCol.GetDocument("doc").GetString("str").Should().Be("string");

                Db.DeleteCollection(Database._defaultCollectionName);

                Db.Invoking(d => d.GetDocument("doc"))
                    .Should().Throw<InvalidOperationException>("Because GetDocument after default collection is deleted.");

                var dto30 = DateTimeOffset.UtcNow.AddSeconds(30);
                using (var doc1 = new MutableDocument("doc1")) {
                    doc1.SetString("str", "string");

                    Db.Invoking(d => Db.Count)
                        .Should().Throw<InvalidOperationException>("Because Save after default collection is deleted.");

                    Db.Invoking(d => d.Save(doc1))
                        .Should().Throw<InvalidOperationException>("Because Save after default collection is deleted.");

                    Db.Invoking(d => d.Delete(doc1))
                        .Should().Throw<InvalidOperationException>("Because Delete after default collection is deleted.");

                    Db.Invoking(d => d.Purge(doc1))
                        .Should().Throw<InvalidOperationException>("Because Purge after default collection is deleted.");

                    Db.Invoking(d => d.SetDocumentExpiration("doc", dto30))
                        .Should().Throw<InvalidOperationException>("Because SetDocumentExpiration after default collection is deleted.");

                    Db.Invoking(d => d.GetDocumentExpiration("doc"))
                        .Should().Throw<InvalidOperationException>("Because GetDocumentExpiration after default collection is deleted.");
                }

                Db.Invoking(d => d.CreateQuery($"SELECT firstName, lastName FROM *"))
                        .Should().Throw<CouchbaseLiteException>("Because CreateQuery after default collection is deleted.");

                var index1 = new ValueIndexConfiguration(new string[] { "firstName", "lastName" });
                Db.Invoking(d => d.CreateIndex("index1", index1))
                    .Should().Throw<InvalidOperationException>("Because CreateIndex after default collection is deleted.");

                Db.Invoking(d => d.GetIndexes())
                    .Should().Throw<InvalidOperationException>("Because GetIndexes after default collection is deleted.");

                Db.Invoking(d => d.DeleteIndex("index1"))
                    .Should().Throw<InvalidOperationException>("Because DeleteIndex after default collection is deleted.");

                Db.Invoking(d => d.AddChangeListener(null, (sender, args) => { } ))
                    .Should().Throw<InvalidOperationException>("Because AddChangeListener after default collection is deleted.");

                Db.Invoking(d => d.AddDocumentChangeListener("doc1", (sender, args) => { }))
                    .Should().Throw<InvalidOperationException>("Because AddDocumentChangeListener after default collection is deleted.");

                Db.Invoking(d => d.RemoveChangeListener(d.AddDocumentChangeListener("doc1", (sender, args) => { })))
                    .Should().Throw<InvalidOperationException>("Because RemoveChangeListener after default collection is deleted.");
            }
        }

        /* 8.10 Use Scope API when No Collections in the Scope */
        // Test that after all collections in the scope are deleted, calling the scope APIS returns the result as expected based on
        // section 6.5. To test this, get and retain the scope object before deleting all collections.
        [Fact]
        public void TestUseScopeAPIsAfterDeletingAllCollections()
        {
            //6.5 Get Collections from The Scope Having No Collections
            //GetCollection() NULL
            //GetCollections() empty result
            using (var colA = Db.CreateCollection("colA", "scopeA")) {
                var scopeA = Db.GetScope("scopeA");//colA.Scope;

                Db.DeleteCollection("colA", "scopeA");

                scopeA.GetCollection("colA").Should().BeNull("Because GetCollection after all collections are deleted.");
                scopeA.GetCollections().Count.Should().Be(0, "Because GetCollections after all collections are deleted.");
            }
        }

        // Test that after all collections in the scope are deleted from a different database instance, calling the scope APIS
        // returns the result as expected based on section 6.5. To test this, get and retain the scope object before deleting
        // all collections.
        //[Fact] CBL-3298
        public void TestUseScopeAPIAfterDeletingAllCollectionsFromDifferentDBInstance()
        {
            //6.5 Get Collections from The Scope Having No Collections
            //GetCollection() NULL
            //GetCollections() empty result
            using (var colA = Db.CreateCollection("colA", "scopeA")) {
                var scopeA = Db.GetScope("scopeA");
                using (var otherDB = OpenDB(Db.Name)) {
                    otherDB.DeleteCollection("colA", "scopeA");

                    scopeA.GetCollection("colA").Should().BeNull("Because GetCollection after collection colA is deleted from the other db.");
                    scopeA.GetCollections().Count.Should().Be(0, "Because GetCollections after collection colA is deleted from the other db.");
                }
            }
        }

        #region Private Methods

        private void TestGetScopesOrCollections(Action dbDispose)
        {
            var colA = Db.CreateCollection("colA", "scopeA");

            dbDispose();

            Db.Invoking(d => d.GetDefaultCollection())
                    .Should().Throw<InvalidOperationException>("Because GetDefaultCollection after db is disposed.");

            Db.Invoking(d => d.GetDefaultScope())
                    .Should().Throw<InvalidOperationException>("Because GetDefaultScope after db is disposed.");

            Db.Invoking(d => d.GetCollection("colA", "scopeA"))
                    .Should().Throw<InvalidOperationException>("Because GetCollection after db is disposed.");

            Db.Invoking(d => d.GetCollections("scopeA"))
                    .Should().Throw<InvalidOperationException>("Because GetCollections after db is disposed.");

            Db.Invoking(d => d.GetScope("scopeA"))
                    .Should().Throw<InvalidOperationException>("Because GetScope after db is disposed.");

            Db.Invoking(d => d.GetScopes())
                    .Should().Throw<InvalidOperationException>("Because GetScopes after db is disposed.");

            Db.Invoking(d => d.CreateCollection("colA", "scopeA"))
                    .Should().Throw<InvalidOperationException>("Because CreateCollection after db is disposed.");

            Db.Invoking(d => d.DeleteCollection("colA", "scopeA"))
                    .Should().Throw<InvalidOperationException>("Because DeleteCollection after db is disposed.");
        }

        private void TestUseCollectionAPIs(Action dbDispose)
        {
            using (var colA = Db.CreateCollection("colA", "scopeA")) {
                using (var doc = new MutableDocument("doc")) {
                    doc.SetString("str", "string");
                    colA.Save(doc);
                }

                Db.Delete();

                colA.Invoking(d => d.GetDocument("doc"))
                    .Should().Throw<CouchbaseLiteException>("Because GetDocument after db is disposed.");

                var dto30 = DateTimeOffset.UtcNow.AddSeconds(30);
                using (var doc1 = new MutableDocument("doc1")) {
                    doc1.SetString("str", "string");

                    colA.Invoking(d => d.Save(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Save after db is disposed.");

                    colA.Invoking(d => d.Delete(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Delete after db is disposed.");

                    colA.Invoking(d => d.Purge(doc1))
                        .Should().Throw<CouchbaseLiteException>("Because Purge after db is disposed.");

                    colA.Invoking(d => d.SetDocumentExpiration("doc1", dto30))
                        .Should().Throw<CouchbaseLiteException>("Because SetDocumentExpiration after db is disposed.");

                    colA.Invoking(d => d.GetDocumentExpiration("doc1"))
                        .Should().Throw<CouchbaseLiteException>("Because GetDocumentExpiration after db is disposed.");
                }

                colA.Invoking(d => d.CreateQuery($"SELECT firstName, lastName FROM *"))
                        .Should().Throw<CouchbaseLiteException>("Because CreateQuery after db is disposed.");

                var index1 = new ValueIndexConfiguration(new string[] { "firstName", "lastName" });
                colA.Invoking(d => d.CreateIndex("index1", index1))
                    .Should().Throw<CouchbaseLiteException>("Because CreateIndex after db is disposed.");

                colA.Invoking(d => d.GetIndexes())
                    .Should().Throw<CouchbaseLiteException>("Because GetIndexes after db is disposed.");

                colA.Invoking(d => d.DeleteIndex("index1"))
                    .Should().Throw<CouchbaseLiteException>("Because DeleteIndex after db is disposed.");

                colA.Invoking(d => d.AddChangeListener(null, (sender, args) => { }))
                    .Should().Throw<CouchbaseLiteException>("Because AddChangeListener after db is disposed.");

                colA.Invoking(d => d.AddDocumentChangeListener("doc1", (sender, args) => { }))
                    .Should().Throw<CouchbaseLiteException>("Because AddDocumentChangeListener after db is disposed.");

                colA.Invoking(d => d.RemoveChangeListener(d.AddDocumentChangeListener("doc1", (sender, args) => { })))
                    .Should().Throw<CouchbaseLiteException>("Because RemoveChangeListener after db is disposed.");
            }
        }

        private void TestUseScope(Action dbDispose)
        {
            using (var colA = Db.CreateCollection("colA", "scopeA")) {

                var scope = colA.Scope;

                dbDispose();

                scope.Invoking(d => d.GetCollection("colA"))
                    .Should().Throw<CouchbaseLiteException>("Because GetCollection after db is disposed.");

                scope.Invoking(d => d.GetCollections())
                    .Should().Throw<CouchbaseLiteException>("Because GetCollections after db is disposed.");
            }
        }

        #endregion
    }
}
