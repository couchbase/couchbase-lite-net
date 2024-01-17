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

#nullable disable

using Couchbase.Lite;
using Couchbase.Lite.Query;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public sealed class ScopeCollectionTest : TestCase
    {
#if !WINDOWS_UWP
        public ScopeCollectionTest(ITestOutputHelper output) : base(output)
        {

        }
#endif
        #region 8.1 Default Scope and Default Collection

        [Fact]
        public void TestDefaultCollectionExists()
        {
            using (var defaultColl = Db.GetDefaultCollection()) {
                defaultColl.Should().NotBeNull("default collection is not null");
                defaultColl.Name.Should().Be(Database._defaultCollectionName, $"default collection name is {Database._defaultCollectionName}");
                var collections = Db.GetCollections();
                collections.Contains(defaultColl).Should().BeTrue("the default collection is included in the collection list when calling Database.GetCollections()");
                var scope = defaultColl.Scope;
                scope.Should().NotBeNull("the scope of the default collection is not null");
                scope.Name.Should().Be(Database._defaultScopeName, $"default collection name is {Database._defaultScopeName}");
                using (var col = Db.GetCollection(Database._defaultCollectionName))
                    col.Should().Be(defaultColl);

                defaultColl.Count.Should().Be(0, "default collection’s count is 0");
            }
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
            Action badAction = (() => Db.DeleteCollection(Database._defaultCollectionName));
            badAction.Should().Throw<CouchbaseLiteException>("Cannot delete the default collection.");

            Db.CreateCollection(Database._defaultCollectionName); //no-op since default collection is already existed and cannot be deleted
            using (var defaultColl = Db.GetDefaultCollection())
                defaultColl.Should().NotBeNull("default collection cannot be deleted, so the value is none null");
        }

        #endregion

        #region 8.2 Collections

        [Fact]
        public void TestCreateAndGetCollectionsInDefaultScope()
        {
            using(var colA = Db.CreateCollection("colA"))
            using(var colB = Db.CreateCollection("colB"))
            using(var colC = Db.CreateCollection("colC")) {
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
            using (var colA = Db.CreateCollection("colA", "scopeA"))
            using (var colB = Db.CreateCollection("colB", "scopeA")) {
                var scopeA = Db.GetScope("scopeA");
                var collectionsInScopeA = scopeA.GetCollections();
                collectionsInScopeA.Count.Should().Be(2, "Because 2 collections were just added in the Database.");
                collectionsInScopeA.Contains(colA).Should().BeTrue("Because collecton colA is in scopeA");
                collectionsInScopeA.Contains(colB).Should().BeTrue("Because collecton colB is in scopeA");
                Db.DeleteCollection("colA", "scopeA");
                scopeA.GetCollections().Count.Should().Be(1, "Collections count should be 1 because colA is deleted from scopeA");
                Db.DeleteCollection("colB", "scopeA");
                Db.GetScope("scopeA").Should().BeNull();
                using (var col = Db.GetCollection("colA", "scopeA"))
                    col.Should().BeNull("because colA is deleted from scopeA");

                using (var col = Db.GetCollection("colB", "scopeA"))
                    col.Should().BeNull("because colB is deleted from scopeA");
            }
        }

        [Fact]
        public void TestCollectionNameWithValidChars()
        {
            // None default Collection and Scope Names are allowed to contain the following characters 
            // A - Z, a - z, 0 - 9, and the symbols _, -, and % and to start with A-Z, a-z, 0-9, and -
            var str = "_%";
            for (char letter = 'A'; letter <= 'Z'; letter++) {
                using (var col = Db.CreateCollection(letter + str))
                    col.Should().NotBeNull($"Valid collection name '{letter + str}'.");
            }

            for (char letter = 'a'; letter <= 'z'; letter++) {
                using(var col = Db.CreateCollection(letter + str))
                    col.Should().NotBeNull($"Valid collection name '{letter + str}'.");
            }

            for (char letter = '0'; letter <= '9'; letter++) {
                using (var col = Db.CreateCollection(letter + str))
                    col.Should().NotBeNull($"Valid collection name '{letter + str}'.");
            }

            using (var col = Db.CreateCollection("-" + str))
                col.Should().NotBeNull($"Valid collection name '{"-" + str}'.");
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
               !, @, #, $, ^, &, *, (, ), +, ., :, ;, <, >, ?, [, ], {, }, =, “, ‘, |, \, /,`,~ are prohibited. */
            for (char letter = '!'; letter <= '/'; letter++) {
                if (letter == '%' || letter == '-')
                    continue;

                Action badAction = (() => Db.CreateCollection(str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid collection name '{str + letter}' in scope '_default'.");
            }

            for (char letter = ':'; letter <= '@'; letter++) {
                Action badAction = (() => Db.CreateCollection(str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid collection name '{str + letter}' in scope '_default'.");
            }

            for (char letter = '['; letter <= '`'; letter++) {
                if (letter == '_')
                    continue;

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
                using (var col = Db.CreateCollection(collName))
                    col.Should().NotBeNull($"Valid collection '{collName}' length {collName.Length}.");
            }

            var str = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_%-cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
            str.Length.Should().Be(251);
            using (var col = Db.CreateCollection(str))
                col.Should().NotBeNull($"because the collection name can be length at {str.Length}");

            str += "e";
            str.Length.Should().Be(252);
            Action badAction = (() => Db.CreateCollection(str));
            badAction.Should().Throw<CouchbaseLiteException>($"Invalid collection name '{str}' in scope because the collection name length {str.Length} is over naming length limit.");
        }

        [Fact] 
        public void TestCollectionNameCaseSensitive()
        {
            using (var collCap = Db.CreateCollection("COLLECTION1"))
            using (var coll = Db.CreateCollection("collection1")) {
                collCap.Should().NotBeNull();
                coll.Should().NotBeNull("Should be able to be created because collection name is case sensitive.");
                coll.Should().NotBeSameAs(collCap);
            }
        }

        [Fact]
        public void TestScopeNameWithValidChars()
        {
            // None default Collection and Scope Names are allowed to contain the following characters 
            // A - Z, a - z, 0 - 9, and the symbols _, -, and % and to start with A-Z, a-z, 0-9, and -
            var str = "_%";
            for (char letter = 'A'; letter <= 'Z'; letter++) {
                using (var col = Db.CreateCollection("abc", letter + str))
                    col.Should().NotBeNull($"Valid scope name '{letter + str}'.");
            }

            for (char letter = 'a'; letter <= 'z'; letter++) {
                using (var col = Db.CreateCollection("abc", letter + str))
                    col.Should().NotBeNull($"Valid scope name '{letter + str}'.");
            }

            for (char letter = '0'; letter <= '9'; letter++) {
                using (var col = Db.CreateCollection("abc", letter + str))
                    col.Should().NotBeNull($"Valid scope name '{letter + str}'.");
            }

            using (var col = Db.CreateCollection("abc", "-" + str))
                col.Should().NotBeNull($"Valid scope name '{"-" + str}'.");
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
               !, @, #, $, ^, &, *, (, ), +, ., ;, :, <, >, ?, [, ], {, }, =, “, ‘, |, \, /,`,~ are prohibited. */
            for (char letter = '!'; letter <= '/'; letter++) {
                if (letter == '%' || letter == '-')
                    continue;

                Action badAction = (() => Db.CreateCollection("abc", str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid scope name '{str + letter}'.");
            }

            for (char letter = ':'; letter <= '@'; letter++) {
                Action badAction = (() => Db.CreateCollection("abc", str + letter));
                badAction.Should().Throw<CouchbaseLiteException>($"Invalid scope name '{str + letter}'.");
            }

            for (char letter = '['; letter <= '`'; letter++) {
                if (letter == '_')
                    continue;

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
                using (var col = Db.CreateCollection("abc", collName))
                    col.Should().NotBeNull($"Valid scope '{collName}' length {collName.Length}.");
            }

            var str = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_%-cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
            str.Length.Should().Be(251);
            using (var col = Db.CreateCollection("abc", str))
                col.Should().NotBeNull($"because the scope name can be length at {str.Length}");

            str += "e";
            str.Length.Should().Be(252);
            Action badAction = (() => Db.CreateCollection("abc", str));
            badAction.Should().Throw<CouchbaseLiteException>($"Invalid scope name '{str}' because the scope name length {str.Length} is over naming length limit.");
        }

        [Fact]
        public void TestScopeNameCaseSensitive()
        {
            using (var scopeCap = Db.CreateCollection("abc", "SCOPE1"))  
            using (var scope = Db.CreateCollection("abc", "scope1")) {
                scopeCap.Should().NotBeNull();
                scope.Should().NotBeNull("Should be able to be created because scope name is case sensitive.");
                scope.Should().NotBeSameAs(scopeCap);
            }
        }

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

        #endregion

        #region 8.3 Collections and Cross Database Instance

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
                    colAInOtherDb.Count.Should().Be(3);
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
                var cols = otherDB.GetCollections(scope: "scopeA");
                cols.Contains(colA).Should().BeTrue();
                using (var col = otherDB.GetCollection("colA", "scopeA"))
                    col.Should().NotBeNull();
            }
        }

        [Fact]
        public void TestDeleteThenGetCollectionFromDifferentDatabaseInstance()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA")){ 
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
                    var colAinOtherDb = otherDB.GetCollection("colA", "scopeA");
                    colAinOtherDb.Count.Should().Be(3);
                    Db.DeleteCollection("colA", "scopeA");
                    colAinOtherDb.Count.Should().Be(0);
                    colAinOtherDb = otherDB.GetCollection("colA", "scopeA");
                    colAinOtherDb.Should().BeNull();
                    var collsInOtherDb = otherDB.GetCollections("scopeA");
                    collsInOtherDb.Contains(colA).Should().BeFalse();
                }
            }
        }

        [Fact]
        public void TestDeleteAndRecreateThenGetCollectionFromDifferentDatabaseInstance()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA")){
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
                    var colAinOtherDb = otherDB.GetCollection("colA", "scopeA");
                    colAinOtherDb.Count.Should().Be(3);
                    Db.DeleteCollection("colA", "scopeA");
                    colAinOtherDb.Count.Should().Be(0);
                    colAinOtherDb = otherDB.GetCollection("colA", "scopeA");
                    colAinOtherDb.Should().BeNull();
                    // Re-create Collection
                    var colATheSecond = Db.CreateCollection("colA", "scopeA");
                    var colATheSecondinOtherDb = otherDB.GetCollection("colA", "scopeA");
                    //Ensure that the collection is not null and is different from the instance gotten before from the instanceB when getting the collection from the database instance B by using database.getCollection(name: "colA", scope: "scopeA").
                    colATheSecondinOtherDb.Should().NotBeNull();
                    colATheSecondinOtherDb.Equals(colAinOtherDb).Should().BeFalse();
                    //Ensure that the collection is included when getting all collections from the database instance B by using database.getCollections(scope: "scopeA").
                    otherDB.GetCollections("scopeA").Contains(colATheSecond).Should().BeTrue();
                }  
            }
        }

        #endregion

        #region 8.5 Use Collection APIs on Deleted Collection

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

                var item = ValueIndexItem.Expression(Expression.Property("firstName"));
                var index = IndexBuilder.ValueIndex(item);
                colA.Invoking(d => d.CreateIndex("myindex", index))
                    .Should().Throw<CouchbaseLiteException>("Because CreateIndex after collection colA is deleted.");

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
        [Fact]
        public void TestUseCollectionAPIOnDeletedCollectionDeletedFromDifferentDBInstance()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA")) {
                using (var doc = new MutableDocument("doc")) {
                    doc.SetString("str", "string");
                    colA.Save(doc);
                }
            }

            using (var otherDB = OpenDB(Db.Name)) {
                var colA1 = otherDB.GetCollection("colA", "scopeA");
                colA1.GetDocument("doc").GetString("str").Should().Be("string");

                otherDB.DeleteCollection("colA", "scopeA");

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

                var item = ValueIndexItem.Expression(Expression.Property("firstName"));
                var index = IndexBuilder.ValueIndex(item);
                colA1.Invoking(d => d.CreateIndex("myindex", index))
                    .Should().Throw<CouchbaseLiteException>("Because CreateIndex after collection colA is deleted from the other db.");

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

        #endregion

        #region 8.6 Use Collection API on Closed or Deleted Database

        [Fact]
        public void TestUseCollectionAPIsWhenDatabaseIsClosed() => TestUseCollectionAPIs(() => Db.Close());

        [Fact]
        public void TestUseCollectionAPIsWhenDatabaseIsDeleted() => TestUseCollectionAPIs(() => Db.Delete());

        #endregion

        #region 8.7 Use Scope API on Closed or Deleted Database

        [Fact]
        public void TestUseScopeWhenDatabaseIsClosed() => TestUseScope(() => Db.Close());

        [Fact]
        public void TestUseScopeWhenDatabaseIsDeleted() => TestUseScope(() => Db.Delete());

        #endregion

        #region 8.8 Get Scopes or Collections on Closed or Deleted Database

        [Fact]
        public void TestGetScopesOrCollectionsWhenDatabaseIsClosed() => TestGetScopesOrCollections(() => Db.Close());

        [Fact]
        public void TestGetScopesOrCollectionsWhenDatabaseIsDeleted() => TestGetScopesOrCollections(() => Db.Delete());

        #endregion

        #region 8.10 Use Scope API when No Collections in the Scope
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
        [Fact]
        public void TestUseScopeAPIAfterDeletingAllCollectionsFromDifferentDBInstance()
        {
            //6.5 Get Collections from The Scope Having No Collections
            //GetCollection() NULL
            //GetCollections() empty result
            using (var colA = Db.CreateCollection("colA", "scopeA")) {
                using (var scopeA = Db.GetScope("scopeA"))
                using (var otherDB = OpenDB(Db.Name)) {
                    otherDB.DeleteCollection("colA", "scopeA");

                    using (var col = scopeA.GetCollection("colA"))
                        col.Should().BeNull("Because GetCollection after collection colA is deleted from the other db.");

                    scopeA.GetCollections().Count.Should().Be(0, "Because GetCollections after collection colA is deleted from the other db.");
                }
            }
        }

        #endregion

        #region Collection FullName

        // Spec: https://docs.google.com/document/d/1nUgaCgXIB3lLViudf6Pw6H9nPa_OeYU6uM_9xAd08M0

        [Fact]
        public void TestCollectionFullName()
        {
            // 3.1 TestGetFullNameFromDefaultCollection
            using (var col = Db.GetDefaultCollection())
            {
                col.Should().NotBeNull("Default collection should not be null");
                col.FullName.Should().Be("_default._default");
            }

            // 3.2 TestGetFullNameFromNewCollectionInDefaultScope
            using (var col = Db.CreateCollection("colA"))
            {
                col.Should().NotBeNull("Created colA should not be null");
                col.FullName.Should().Be("_default.colA");
            }

            // 3.3 TestGetFullNameFromNewCollectionInCustomScope
            using (var col = Db.CreateCollection("colA", "scopeA"))
            {
                col.Should().NotBeNull("Created colA should not be null");
                col.FullName.Should().Be("scopeA.colA");
            }

            // 3.4 TestGetFullNameExistingCollectionInDefaultScope
            using (var col = Db.GetCollection("colA"))
            {
                col.Should().NotBeNull("Existing colA should not be null");
                col.FullName.Should().Be("_default.colA");
            }

            // 3.5 TestGetFullNameFromExistingCollectionInCustomScope
            using (var col = Db.GetCollection("colA", "scopeA"))
            {
                col.Should().NotBeNull("Existing colA should not be null");
                col.FullName.Should().Be("scopeA.colA");
            }
        }

        #endregion

        #region Scope's and Collection's Database

        // Spec: https://docs.google.com/document/d/1nUgaCgXIB3lLViudf6Pw6H9nPa_OeYU6uM_9xAd08M0

        public void TestCollectionDatabase()
        {
            // 3.1 TestGetDatabaseFromNewCollection
            using (var col = Db.CreateCollection("colA", "scopeA"))
            {
                col.Should().NotBeNull("Created colA should not be null");
                col.Database.Should().Be(Db);
            }

            // 3.2 TestGetDatabaseFromExistingCollection
            using (var col = Db.GetCollection("colA", "scopeA"))
            {
                col.Should().NotBeNull("Created colA should not be null");
                col.Database.Should().Be(Db);
            }
        }

        [Fact]
        public void TestScopeDatabase()
        {
            // 3.3 TestGetDatabaseFromScopeObtainedFromCollection
            using (var col = Db.CreateCollection("colA", "scopeA"))
            {
                col.Should().NotBeNull("Created colA should not be null");
                var scope = col.Scope;
                scope.Should().NotBeNull("scopeA should not be null");
                scope.Database.Should().Be(Db);
            }

            // 3.4 TestGetDatabaseFromScopeObtainedFromDatabase
            using (var scope = Db.GetScope("scopeA"))
            {
                scope.Should().NotBeNull("scopeA should not be null");
                scope.Database.Should().Be(Db);
            }
        }

        #endregion

        #region Create Index from Query / Index Builder Using Collection

        [Fact]
        public void TestPerformMaintenanceReindex()
        {
            int n = 20;
            var docs = new List<Document>();
            for (int i = 0; i < n; i++) {
                var doc = new MutableDocument($"doc_{i:D3}");
                doc.SetInt("key", i);
                CollA.Save(doc);
                docs.Add(doc);
            }

            CollA.Count.Should().Be((ulong)n, "because otherwise an incorrect number of documents were made");

            // Reindex when there is no index
            Db.PerformMaintenance(MaintenanceType.Reindex);

            //Create an index
            var key = Expression.Property("key");
            var keyItem = ValueIndexItem.Expression(key);
            var keyIndex = IndexBuilder.ValueIndex(keyItem);
            CollA.CreateIndex("KeyIndex", keyIndex);
            CollA.GetIndexes().Count.Should().Be(1);

            var q = QueryBuilder.Select(SelectResult.Expression(key))
                .From(DataSource.Collection(CollA))
                .Where(key.GreaterThan(Expression.Int(9)));
            q.Explain().Contains("USING INDEX KeyIndex").Should().BeTrue();

            //Reindex
            Db.PerformMaintenance(MaintenanceType.Reindex);

            //Check if the index is still there and used
            CollA.GetIndexes().Count.Should().Be(1);
            q.Explain().Contains("USING INDEX KeyIndex").Should().BeTrue();
        }

        [Fact]
        public void TestCreateIndex()
        {
            CollA.GetIndexes().Should().BeEmpty();

            var lName = Expression.Property("lastName");
            var fNameItem = ValueIndexItem.Property("firstName");
            var lNameItem = ValueIndexItem.Expression(lName);

            var index1 = IndexBuilder.ValueIndex(fNameItem, lNameItem);
            CollA.CreateIndex("index1", index1);

            var detailItem = FullTextIndexItem.Property("detail");
            var index2 = IndexBuilder.FullTextIndex(detailItem);
            CollA.CreateIndex("index2", index2);

            var detailItem2 = FullTextIndexItem.Property("es-detail");
            var index3 = IndexBuilder.FullTextIndex(detailItem2).IgnoreAccents(true).SetLanguage("es");
            CollA.CreateIndex("index3", index3);

            CollA.GetIndexes().Should().BeEquivalentTo(new[] { "index1", "index2", "index3" });
        }

        [Fact]
        public void TestCreateSameIndexTwice()
        {
            var item = ValueIndexItem.Expression(Expression.Property("firstName"));
            var index = IndexBuilder.ValueIndex(item);
            CollA.CreateIndex("myindex", index);
            CollA.CreateIndex("myindex", index);

            CollA.GetIndexes().Should().BeEquivalentTo(new[] { "myindex" });
        }

        [Fact]
        public void TestCreateSameNameIndexes()
        {
            var fName = Expression.Property("firstName");
            var lName = Expression.Property("lastName");

            var fNameItem = ValueIndexItem.Expression(fName);
            var fNameIndex = IndexBuilder.ValueIndex(fNameItem);
            CollA.CreateIndex("myindex", fNameIndex);

            var lNameItem = ValueIndexItem.Expression(lName);
            var lNameIndex = IndexBuilder.ValueIndex(lNameItem);
            CollA.CreateIndex("myindex", lNameIndex);

            CollA.GetIndexes().Should().BeEquivalentTo(new[] { "myindex" }, "because lNameIndex should overwrite fNameIndex");

            var detailItem = FullTextIndexItem.Property("detail");
            var detailIndex = IndexBuilder.FullTextIndex(detailItem);
            CollA.CreateIndex("myindex", detailIndex);

            CollA.GetIndexes().Should().BeEquivalentTo(new[] { "myindex" }, "because detailIndex should overwrite lNameIndex");
        }

        #endregion

        [Fact]
        public void TestDisposingDefaultScopeCollection()
        {
            // This is advised against, but need to make sure nothing too bad happens.
            // Previously, if the caller disposed the result of GetDefaultCollection, 
            // further calls returned null with no recourse other than re-opening the db
            var scope = Db.GetDefaultScope();
            var collection = scope.CreateCollection("foo");
            var defaultCollection = Db.GetDefaultCollection();

            scope.Dispose();
            collection.Dispose();
            defaultCollection.Dispose();

            scope = Db.GetDefaultScope();
            collection = scope.GetCollection("foo");
            collection.IsValid.Should().BeTrue("because it still exists in LiteCore");
            defaultCollection = Db.GetDefaultCollection();
            defaultCollection.Should().NotBeNull("because a new object should be created");
            defaultCollection.IsValid.Should().BeTrue("because the new created object should be valid");
            defaultCollection.Count.Should().Be(0);
        }

        #region Document Subscript

        [Fact]
        public void TestDocumentSubscript()
        {
            var defaultCollection = Db.GetDefaultCollection();
            var doc1 = defaultCollection["doc1"];
            doc1.Exists.Should().BeFalse("because the document id'doc1' doesn't exist in the collection");

            using (var doc = new MutableDocument("doc1")) {
                doc.SetString("str", "string");
                defaultCollection.Save(doc);    
            }
            doc1 = defaultCollection["doc1"];
            doc1.Exists.Should().BeTrue("because the document id 'doc1' exists in the collection");
            doc1["foo"].Exists.Should().BeFalse("because this portion of the data doesn't exist");
            doc1["str"].Exists.Should().BeTrue("because this portion of the data exists");
            doc1["str"].String.Should().Be("string", "because that is the stored value");
        }
        
        #endregion

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

                var item = ValueIndexItem.Expression(Expression.Property("firstName"));
                var index = IndexBuilder.ValueIndex(item);
                colA.Invoking(d => d.CreateIndex("myindex", index))
                    .Should().Throw<CouchbaseLiteException>("Because CreateIndex after db is disposed.");

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
