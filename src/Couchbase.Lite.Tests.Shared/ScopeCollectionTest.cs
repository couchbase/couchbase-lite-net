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
            //TODO wait for CBL-3257 fix
            //scopes.Contains(defaultScope).Should().BeTrue("the default scope is included in the scope list when calling Database.GetScopes()");
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

        /* TODO 8.2 Part 1*/
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
            scopeA.Collections.Count.Should().Be(2, "Because 2 collections were just added in the Database.");
            scopeA.Collections.Contains(colA).Should().BeTrue("Because collecton colA is in scopeA");
            scopeA.Collections.Contains(colB).Should().BeTrue("Because collecton colB is in scopeA");
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
                Db.CreateCollection(letter+str).Should().NotBeNull($"Valid collection name '{letter + str}'.");
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
            for( char letter = '!'; letter <= '/'; letter++) {
                if (letter == '%')
                    return;
                Action badAction = (() => Db.CreateCollection(str+letter));
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
            for(int i=0; i<251; i++) {
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

        /* TODO CBL-3225 8.2 Part 2
         TestCreateAnExistingCollection : Test that creating an existing collection returns an existing collection.
Create a new collection by using database.createCollection(name: "colA", scope: "scopeA").
Add some documents to the collection.
Create the same collection again. 
Ensure that the existing collection is returned, and all the existing documents still exist.*/
        [Fact]
        public void TestCreateAnExistingCollection()
        {

        }

        /*TestDeleteCollection : Test that deleting a collection is successful.
        Create a new collection by using database.createCollection(name: "colA", scope: "scopeA").
        Add some documents to the collection.
        Delete the collection.
        Ensure that the collection is deleted successfully.
        Ensure that getting the collection using database.getCollection(name: "colA", scope: "scopeA") returns null.
        Ensure that the collections from database.getCollections(scope: "scopeA") doesn’t include the deleted collection.
        Try to recreate the same collection.
        Ensure that the collection can be recreated. */
        [Fact]
        public void TestDeleteCollection()
        {

        }

        /* TODO CBL-3227 8.3 Collections and Cross Database Instance
TestCreateThenGetCollectionFromDifferentDatabaseInstance : Test that creating a collection from a database instance is visible to the other database instance.
Create Database instance A and B.
Create a collection in a scope from the database instance A.
Ensure that the created collection is visible to the database instance B by using database.getCollection(name: "colA", scope: "scopeA") and database.getCollections(scope: "scopeA") API.
        */
        [Fact]
        public void TestCreateThenGetCollectionFromDifferentDatabaseInstance()
        {

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

        }

        //TODO: CBL-3235 Add tests to test database functions when database is closed deleted
    }
}
