//
//  ScopesCollectionsReplicationTest.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Couchbase.Lite;
using Couchbase.Lite.Sync;

using Shouldly;

using Newtonsoft.Json;
using System.Collections.Immutable;

using Test.Util;
#if COUCHBASE_ENTERPRISE
#endif

using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public sealed class ScopesCollectionsReplicationTest(ITestOutputHelper output) : ReplicatorTestBase(output)
    {
        private enum PendingDocIDSel { Create, Update, Delete, Purge, Filter }

#if COUCHBASE_ENTERPRISE

        #region 8.13 ReplicatorConfiguration

        [Fact]
        public void TestCollectionsWithoutCollectionConfig()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var collectionConfigs = CollectionConfiguration.FromCollections(colA, colB);
            var config = new ReplicatorConfiguration(collectionConfigs, new DatabaseEndpoint(OtherDb));
            var colAConfig = config.Collections.FirstOrDefault(x => x.Collection.Equals(colA));
            colAConfig.ShouldNotBeNull("Because collection colA just added");
            var colBConfig = config.Collections.FirstOrDefault(x => x.Collection.Equals(colB));
            colBConfig.ShouldNotBeNull("Because collection colB just added");
            colAConfig.ShouldNotBeSameAs(colBConfig, "Because the returned configs should be different instances.");
            colAConfig.ConflictResolver.ShouldBe(ConflictResolver.Default, $"Property was never assigned and default value was {ConflictResolver.Default}.");
            colAConfig.PushFilter.ShouldBeNull("Property was never assigned and default value was null.");
            colAConfig.PullFilter.ShouldBeNull("Property was never assigned and default value was null.");
            colBConfig.ConflictResolver.ShouldBe(ConflictResolver.Default, $"Property was never assigned and default value was {ConflictResolver.Default}.");
            colBConfig.PushFilter.ShouldBeNull("Property was never assigned and default value was null.");
            colBConfig.PullFilter.ShouldBeNull("Property was never assigned and default value was null.");
        }

        [Fact]
        public void TestCollectionsWithCollectionConfig()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var resolver = new FakeConflictResolver();
            var collectionConfigs = new List<Collection> { colA, colB }.Select(x => new CollectionConfiguration(x)
            {
                ConflictResolver = resolver,
                PullFilter = _replicator__filterCallbackTrue,
                PushFilter = _replicator__filterCallbackTrue
            });
            
            var config = new ReplicatorConfiguration(collectionConfigs, new DatabaseEndpoint(OtherDb));
            var colAConfig = config.Collections.FirstOrDefault(x => x.Collection.Equals(colA));
            colAConfig.ShouldNotBeNull("Because collection colA just added");
            var colBConfig = config.Collections.FirstOrDefault(x => x.Collection.Equals(colB));
            colBConfig.ShouldNotBeNull("Because collection colB just added");
            colAConfig.ShouldNotBeNull("because it was just set");
            colBConfig.ShouldNotBeNull("because it was just set");
            colAConfig.ConflictResolver.ShouldBe(colBConfig.ConflictResolver, "Both properties were assigned with same value.");
            colAConfig.PushFilter.ShouldBe(colBConfig.PushFilter, "Both properties were assigned with same value.");
            colAConfig.PullFilter.ShouldBe(colBConfig.PullFilter, "Both properties were assigned with same value.");
        }
        
        [Fact]
        public void TestCollectionsFromDifferentDatabaseInstance()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = OtherDb.CreateCollection("colB", "scopeA");
            var collectionConfigs = CollectionConfiguration.FromCollections(colA, colB);
            var ex = Should.Throw<CouchbaseLiteException>(() => new ReplicatorConfiguration(collectionConfigs, new DatabaseEndpoint(OtherDb)));
            ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
            ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact]
        public void TestDeletedCollections()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = OtherDb.CreateCollection("colB", "scopeA");
            OtherDb.DeleteCollection("colB", "scopeA");
            var collectionConfigs = CollectionConfiguration.FromCollections(colA, colB);
            var ex = Should.Throw<CouchbaseLiteException>(() => new ReplicatorConfiguration(collectionConfigs, new DatabaseEndpoint(OtherDb)));
            ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
            ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        }

        #endregion

        #region 8.14 Replicator

        [Fact]
        public void TestCollectionSingleShotPushReplication()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Push);
            LoadCollectionsDocs();
            RunReplication(config, 0, 0);

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            using var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            using var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA");
            colAInOtherDb.ShouldNotBeNull("because it was just created");
            colBInOtherDb.ShouldNotBeNull("because it was just created");
            colAInOtherDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInOtherDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        [Fact]
        public void TestCollectionSingleShotPullReplication()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Pull);
            LoadCollectionsDocs();
            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            using var colAInDb = Db.GetCollection("colA", "scopeA");
            using var colBInDb = Db.GetCollection("colB", "scopeA");
            colAInDb.ShouldNotBeNull("because it was just created");
            colBInDb.ShouldNotBeNull("because it was just created");
            colAInDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        [Fact]
        public void TestCollectionSingleShotPushPullReplicationCRUD()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.PushAndPull);
            //Test Add and Read docs in replication
            LoadCollectionsDocs();
            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colBInOtherDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInOtherDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }

            //Test Update doc in replication
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                using (var doc = colAInDb.GetDocument("doc4"))
                using (var mdoc = doc?.ToMutable()) {
                    doc?.GetString("str4").ShouldBe("string4");
                    colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                    mdoc!.SetString("str4", "string4 update");
                    colAInDb.Save(mdoc);
                }
            }

            RunReplication(config, 0, 0);

            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4 update");
            }

            //Test Delete doc in replication
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.ShouldNotBeNull("because it was just created");

                using (var doc = colAInDb.GetDocument("doc4")) {
                    doc?.GetString("str4").ShouldBe("string4 update");
                    colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4 update");
                    colAInDb.Delete(doc!);
                }
            }

            RunReplication(config, 0, 0);

            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.GetDocument("doc4").ShouldBeNull();
            }
        }

        [Fact]
        public void TestCollectionContinuousPushReplication()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Push, true);
            LoadCollectionsDocs();
            RunReplication(config, 0, 0);

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            using var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            using var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA");
            colAInOtherDb.ShouldNotBeNull("because it was just created");
            colBInOtherDb.ShouldNotBeNull("because it was just created");
            colAInOtherDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInOtherDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        [Fact]
        public void TestCollectionContinuousPullReplication()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Pull, true);
            LoadCollectionsDocs();
            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            using var colAInDb = Db.GetCollection("colA", "scopeA");
            using var colBInDb = Db.GetCollection("colB", "scopeA");
            colAInDb.ShouldNotBeNull("because it was just created");
            colBInDb.ShouldNotBeNull("because it was just created");
            colAInDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        [Fact]
        public void TestCollectionContinuousPushPullReplication()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.PushAndPull, true);
            LoadCollectionsDocs();

            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colBInOtherDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInOtherDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }
        }

        [Fact]
        public void TestCollectionResetReplication()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Pull);
            LoadCollectionsDocs();
            RunReplication(config, 0, 0);

            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) 
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colBInOtherDb.ShouldNotBeNull("because it was just created");

                // Check docs in Db
                colAInDb.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                var doc4 = colAInDb.GetDocument("doc4");
                doc4?.GetString("str4").ShouldBe("string4");
                var doc5 = colAInDb.GetDocument("doc5");
                doc5?.GetString("str5").ShouldBe("string5");
                colBInDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");

                // Purge all docs in colA in Db
                colAInDb.Purge(doc4!);
                colAInDb.Purge(doc5!);

                // docs in colA in Db are now purged
                colAInDb.GetDocument("doc4").ShouldBeNull();
                colAInDb.GetDocument("doc5").ShouldBeNull();
            }

            RunReplication(config, 0, 0);

            using (var colAInDb = Db.GetCollection("colA", "scopeA")) {
                colAInDb.ShouldNotBeNull();

                // docs in colA in Db are still gone
                colAInDb.GetDocument("doc4").ShouldBeNull();
                colAInDb.GetDocument("doc5").ShouldBeNull();
            }

            RunReplication(config, 0, 0, true);

            using (var colAInDb = Db.GetCollection("colA", "scopeA")) {
                colAInDb.ShouldNotBeNull();

                // After reset in replication, colA in Db are pulled from otherDb again
                colAInDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            }
        }

        [Fact]
        public void TestMismatchedCollectionReplication()
        {
            using var colA = Db.CreateCollection("colA", "scopeA");
            using var unused = OtherDb.CreateCollection("colB", "scopeA");
            var collectionConfigs = CollectionConfiguration.FromCollections(colA);
            var config = CreateConfig(collectionConfigs, ReplicatorType.Pull);
            RunReplication(config, (int)CouchbaseLiteError.WebSocketUserPermanent, CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact]
        public void TestCollectionDocumentReplicationEvents()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.PushAndPull);
            LoadCollectionsDocs();

            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colBInOtherDb.ShouldNotBeNull("because it was just created");

                using (var docInColAInDb = colAInDb.GetDocument("doc"))
                using (var doc2InColBDb = colBInDb.GetDocument("doc2"))
                using (var doc4InColAInOtherDb = colAInOtherDb.GetDocument("doc4"))
                using (var doc6InColBInOtherDb = colBInOtherDb.GetDocument("doc6")) {
                    colAInDb.Delete(docInColAInDb!);
                    colBInDb.Delete(doc2InColBDb!);
                    colAInOtherDb.Delete(doc4InColAInOtherDb!);
                    colBInOtherDb.Delete(doc6InColBInOtherDb!);
                }
            }

            var pullWait = new WaitAssert();
            var pushWait = new WaitAssert();
            RunReplication(config, 0, 0, onReplicatorReady: (r) =>
            {
                r.AddDocumentReplicationListener((_, args) =>
                {
                    _output.WriteLine($"{args.IsPush} {JsonConvert.SerializeObject(args.Documents.Select(x => $"{x.Flags} {x.CollectionName} {x.Id}"))}");
                    pushWait.RunConditionalAssert(() =>
                        args.IsPush 
                        && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted) && x is { CollectionName: "colA", Id: "doc" })
                        && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted) && x is { CollectionName: "colB", Id: "doc2" }));
                    pullWait.RunConditionalAssert(() =>
                        !args.IsPush 
                        && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted) && x is { CollectionName: "colA", Id: "doc4" })
                        && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted) && x is { CollectionName: "colB", Id: "doc6" }));
                });
            });

            pushWait.WaitForResult(TimeSpan.FromSeconds(5));
            pullWait.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TestCollectionDefaultConflictResolver()
        {
            var collectionConfigs = CollectionConfiguration.FromCollections(
                Db.CreateCollection("colA", "scopeA"),
                Db.CreateCollection("colB", "scopeA"));
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.PushAndPull);
            LoadCollectionsDocs();
            RunReplication(config, 0, 0);
            using var colAInDb = Db.GetCollection("colA", "scopeA");
            using var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            colAInDb.ShouldNotBeNull("because it was just created");
            colAInOtherDb.ShouldNotBeNull("because it was just created");
            using (var doc = colAInDb.GetDocument("doc1"))
            using (var mdoc = doc?.ToMutable()) {
                mdoc.ShouldNotBeNull("because it was just saved");
                mdoc.SetString("str1", "string1 update");
                colAInDb.Save(mdoc);
            }

            using (var doc = colAInOtherDb.GetDocument("doc1"))
            using (var mdoc = doc?.ToMutable()) {
                mdoc.ShouldNotBeNull("because it was just saved");
                mdoc.SetString("str1", "string1 update1");
                colAInOtherDb.Save(mdoc);
            }

            using (var doc = colAInOtherDb.GetDocument("doc1"))
            using (var mdoc = doc?.ToMutable()) {
                mdoc.ShouldNotBeNull("because it was just saved");
                mdoc.SetString("str1", "string1 update again");
                colAInOtherDb.Save(mdoc);
            }

            RunReplication(config, 0, 0);

            colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1 update again");
            colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1 update again");
        }

        [Fact]
        public void TestCollectionConflictResolver()
        {
            LoadCollectionsDocs();

            using var colAInDb = Db.GetCollection("colA", "scopeA");
            using var colBInDb = Db.GetCollection("colB", "scopeA");
            using var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            using var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA");
            colAInDb.ShouldNotBeNull("because it was just created");
            colBInDb.ShouldNotBeNull("because it was just created");
            colAInOtherDb.ShouldNotBeNull("because it was just created");
            colBInOtherDb.ShouldNotBeNull("because it was just created");

            var collectionConfigs = new List<CollectionConfiguration>
            {
                new(colAInDb)
                {
                    ConflictResolver = new TestConflictResolver(conflict => conflict.LocalDocument)
                },
                new(colBInDb)
                {
                    ConflictResolver = new TestConflictResolver(conflict => conflict.RemoteDocument)
                }
            };
                
            var config = CreateConfig(collectionConfigs, ReplicatorType.PushAndPull);
            RunReplication(config, 0, 0);

            using (var doc = colAInDb.GetDocument("doc1"))
            using (var mdoc = doc?.ToMutable()) {
                mdoc.ShouldNotBeNull("because it was just saved");
                mdoc.SetString("str1", "string1 update");
                colAInDb.Save(mdoc);
            }

            using (var doc = colBInDb.GetDocument("doc3"))
            using (var mdoc = doc?.ToMutable()) {
                mdoc.ShouldNotBeNull("because it was just saved");
                mdoc.SetString("str3", "string3 update");
                colBInDb.Save(mdoc);
            }

            using (var doc = colAInOtherDb.GetDocument("doc1"))
            using (var mdoc = doc?.ToMutable()) {
                mdoc.ShouldNotBeNull("because it was just saved");
                mdoc.SetString("str1", "string1 update1");
                colAInOtherDb.Save(mdoc);
            }

            using (var doc = colBInOtherDb.GetDocument("doc3"))
            using (var mdoc = doc?.ToMutable()) {
                mdoc.ShouldNotBeNull("because it was just saved");
                mdoc.SetString("str3", "string3 update1");
                colBInOtherDb.Save(mdoc);
            }

            RunReplication(config, 0, 0);

            colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1 update");
            colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3 update1");
        }

        [Fact]
        public void TestCollectionPushFilter()
        {
            LoadCollectionsDocs();
            var collectionConfigs = new List<Collection>
            {
                Db.GetCollection("colA", "scopeA")!,
                Db.GetCollection("colB", "scopeA")!
            }.Select(x => new CollectionConfiguration(x)
            {
                PushFilter = _replicator__filterAllowsOddDocIdsCallback
            });
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Push);
            RunReplication(config, 0, 0);

            // Check docs in OtherDb - make sure docs with odd doc ids are pushed to the OtherDb from the Db
            using var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            using var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA");
            colAInOtherDb!.GetDocument("doc").ShouldBeNull();
            colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInOtherDb!.GetDocument("doc2")?.ShouldBeNull();
            colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        [Fact]
        public void TestCollectionPullFilter()
        {
            LoadCollectionsDocs();
            var collectionConfigs = new List<Collection>
            {
                Db.GetCollection("colA", "scopeA")!,
                Db.GetCollection("colB", "scopeA")!
            }.Select(x => new CollectionConfiguration(x)
            {
                PullFilter = _replicator__filterAllowsOddDocIdsCallback
            });
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Pull);

            using var colAInDb = Db.GetCollection("colA", "scopeA");
            using var colBInDb = Db.GetCollection("colB", "scopeA");
            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs with odd doc ids are pulled from the OtherDb to the Db
            colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInDb.GetDocument("doc4")?.ShouldBeNull();
            colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInDb.GetDocument("doc6")?.ShouldBeNull();
            colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        private readonly IImmutableList<string> _allowOddIds = ImmutableArray.Create("doc1", "doc3", "doc5", "doc7");

        [Fact]
        public void TestCollectionDocumentIDsPushFilter()
        {
            LoadCollectionsDocs();
            var collectionConfigs = new List<Collection>
            {
                Db.GetCollection("colA", "scopeA")!,
                Db.GetCollection("colB", "scopeA")!
            }.Select(x => new CollectionConfiguration(x)
            {
                DocumentIDs = _allowOddIds
            });
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Push);
            RunReplication(config, 0, 0);

            // Check docs in OtherDb - make sure docs with odd doc ids are pushed to the OtherDb from the Db
            using var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            using var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA");
            colAInOtherDb!.GetDocument("doc").ShouldBeNull();
            colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInOtherDb!.GetDocument("doc2").ShouldBeNull();
            colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        [Fact]
        public void TestCollectionDocumentIDsPullFilter()
        {
            LoadCollectionsDocs();
            var collectionConfigs = new List<Collection>
            {
                Db.GetCollection("colA", "scopeA")!,
                Db.GetCollection("colB", "scopeA")!
            }.Select(x => new CollectionConfiguration(x)
            {
                DocumentIDs = _allowOddIds
            });
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Pull);
            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs with odd doc ids are pulled from the OtherDb to the Db
            using var colAInDb = Db.GetCollection("colA", "scopeA");
            using var colBInDb = Db.GetCollection("colB", "scopeA");
            colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInDb.GetDocument("doc4").ShouldBeNull();
            colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInDb.GetDocument("doc6").ShouldBeNull();
            colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        [Fact]
        public void TestCollectionGetPendingDocIDsWithCreate() => ValidatePendingDocumentIds(PendingDocIDSel.Create);

        [Fact]
        public void TestCollectionGetPendingDocIDsWithUpdate() => ValidatePendingDocumentIds(PendingDocIDSel.Update);

        [Fact]
        public void TestCollectionGetPendingDocIDsWithDelete() => ValidatePendingDocumentIds(PendingDocIDSel.Delete);

        [Fact]
        public void TestCollectionGetPendingDocIDsWithPurge() => ValidatePendingDocumentIds(PendingDocIDSel.Purge);

        [Fact]
        public void TestCollectionGetPendingDocIDsWithFilter() => ValidatePendingDocumentIds(PendingDocIDSel.Filter);

        [Fact]
        public void TestCollectionIsDocumentPendingWithCreate() => ValidateIsDocumentPending(PendingDocIDSel.Create);

        [Fact]
        public void TestCollectionIsDocumentPendingWithUpdate() => ValidateIsDocumentPending(PendingDocIDSel.Update);

        [Fact]
        public void TestCollectionIsDocumentPendingWithDelete() => ValidateIsDocumentPending(PendingDocIDSel.Delete);

        [Fact]
        public void TestCollectionIsDocumentPendingWithPurge() => ValidateIsDocumentPending(PendingDocIDSel.Purge);

        [Fact]
        public void TestCollectionIsDocumentPendingWithFilter() => ValidateIsDocumentPending(PendingDocIDSel.Filter);

        [Fact]
        public void TestCreateReplicatorWithNoCollections()
        {
            void BadAction()
            {
                _ = new ReplicatorConfiguration(new List<CollectionConfiguration>(), new DatabaseEndpoint(OtherDb));
            }

            Should.Throw<CouchbaseLiteException>(BadAction, "Replicator Configuration must contain at least one collection.");
        }

        #endregion

        #region Private Methods

        private void ValidatePendingDocumentIds(PendingDocIDSel selection)
        {
            LoadCollectionsDocs();
            IImmutableSet<string> pendingDocIds;
            var colADocId = "doc";
            var colBDocId = "doc2";

            List<CollectionConfiguration> collectionConfigs;
            if (selection == PendingDocIDSel.Filter) {
                collectionConfigs =
                [
                    new(Db.GetCollection("colA", "scopeA")!)
                    {
                        PushFilter = (doc, _) => doc.Id.Equals(colADocId)
                    },

                    new(Db.GetCollection("colB", "scopeA")!)
                    {
                        PushFilter = (doc, _) => doc.Id.Equals(colBDocId)
                    }
                ];
            } else {
                collectionConfigs = CollectionConfiguration.FromCollections(
                    Db.GetCollection("colA", "scopeA")!,
                    Db.GetCollection("colB", "scopeA")!);
            }
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Push);
            
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                if (selection == PendingDocIDSel.Update) {
                    using (var doc = colAInDb!.GetDocument(colADocId))
                    using (var mdoc = doc!.ToMutable()) {
                        mdoc.SetString("str", "string update");
                        colAInDb.Save(mdoc);
                    }

                    using (var doc = colBInDb!.GetDocument(colBDocId))
                    using (var mdoc = doc!.ToMutable()) {
                        mdoc.SetString("str2", "string2 update");
                        colBInDb.Save(mdoc);
                    }
                } else if (selection == PendingDocIDSel.Delete) {
                    using (var doc = colAInDb!.GetDocument(colADocId)) {
                        colAInDb.Delete(doc!);
                    }

                    using (var doc = colBInDb!.GetDocument(colBDocId)) {
                        colBInDb.Delete(doc!);
                    }
                } else if (selection == PendingDocIDSel.Purge) {
                    using (var doc = colAInDb!.GetDocument(colADocId)) {
                        colAInDb.Purge(doc!);
                    }

                    using (var doc = colBInDb!.GetDocument(colBDocId)) {
                        colBInDb.Purge(doc!);
                    }
                }

                using (var replicator = new Replicator(config)) {
                    var wa = new WaitAssert();
                    var token = replicator.AddChangeListener((_, args) =>
                    {
                        wa.RunConditionalAssert(() =>
                        {
                            // ReSharper disable AccessToDisposedClosure
                            if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                                pendingDocIds = replicator.GetPendingDocumentIDs(colAInDb!);
                                pendingDocIds.Count.ShouldBe(0);

                                pendingDocIds = replicator.GetPendingDocumentIDs(colBInDb!);
                                pendingDocIds.Count.ShouldBe(0);
                            }
                            // ReSharper restore AccessToDisposedClosure

                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    pendingDocIds = replicator.GetPendingDocumentIDs(colAInDb!);
                    var pendingDocIds1 = replicator.GetPendingDocumentIDs(colBInDb!);
                    if (selection == PendingDocIDSel.Filter) {
                        pendingDocIds.Count.ShouldBe(1);
                        pendingDocIds.ElementAt(0).ShouldBe(colADocId);
                        pendingDocIds1.Count.ShouldBe(1);
                        pendingDocIds1.ElementAt(0).ShouldBe(colBDocId);
                    } else if (selection == PendingDocIDSel.Purge) {
                        pendingDocIds.Contains("doc").ShouldBeFalse();
                        pendingDocIds.Contains("doc1").ShouldBeTrue();
                        pendingDocIds1.Contains("doc2").ShouldBeFalse();
                        pendingDocIds1.Contains("doc3").ShouldBeTrue();
                    } else {
                        pendingDocIds.Contains("doc").ShouldBeTrue();
                        pendingDocIds.Contains("doc1").ShouldBeTrue();
                        pendingDocIds1.Contains("doc2").ShouldBeTrue();
                        pendingDocIds1.Contains("doc3").ShouldBeTrue();
                    }

                    replicator.Start();

                    wa.WaitForResult(TimeSpan.FromSeconds(50));

                    // ReSharper disable once AccessToDisposedClosure
                    Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                        .Times(5)
                        .Delay(TimeSpan.FromMilliseconds(500))
                        .Go().ShouldBeTrue();

                    replicator.GetPendingDocumentIDs(colAInDb!).Count.ShouldBe(0);
                    replicator.GetPendingDocumentIDs(colBInDb!).Count.ShouldBe(0);
                    token.Remove();
                }
            }

            Thread.Sleep(500); //it takes a while to get the replicator to actually released...
        }

        private void ValidateIsDocumentPending(PendingDocIDSel selection)
        {
            bool docIdIsPending;
            const string ColADocId = "doc";
            const string ColBDocId = "doc2";
            LoadCollectionsDocs();

            List<CollectionConfiguration> collectionConfigs;
            if (selection == PendingDocIDSel.Filter) {
                collectionConfigs =
                [
                    new(Db.GetCollection("colA", "scopeA")!)
                    {
                        PushFilter = (doc, _) => doc.Id.Equals(ColADocId)
                    },

                    new(Db.GetCollection("colB", "scopeA")!)
                    {
                        PushFilter = (doc, _) => doc.Id.Equals(ColBDocId)
                    }
                ];
            } else {
                collectionConfigs = CollectionConfiguration.FromCollections(
                    Db.GetCollection("colA", "scopeA")!,
                    Db.GetCollection("colB", "scopeA")!);
            }
            
            var config = CreateConfig(collectionConfigs, ReplicatorType.Push);

            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                switch (selection) {
                    case PendingDocIDSel.Update:
                    {
                        using var doc1 = colAInDb!.GetDocument(ColADocId);
                        using var mdoc1 = doc1!.ToMutable();
                        mdoc1.SetString("str", "string update");
                        colAInDb.Save(mdoc1);

                        using var doc2 = colBInDb!.GetDocument(ColBDocId);
                        using var mdoc2 = doc2!.ToMutable();
                        mdoc2.SetString("str2", "string2 update");
                        colBInDb.Save(mdoc2);

                        break;
                    }
                    case PendingDocIDSel.Delete:
                    {
                        using var doc1 = colAInDb!.GetDocument(ColADocId);
                        colAInDb.Delete(doc1!);

                        using var doc2 = colBInDb!.GetDocument(ColBDocId);
                        colBInDb.Delete(doc2!);

                        break;
                    }
                    case PendingDocIDSel.Purge:
                    {
                        using var doc1 = colAInDb!.GetDocument(ColADocId);
                        colAInDb.Purge(doc1!);

                        using var doc2 = colBInDb!.GetDocument(ColBDocId);
                        colBInDb.Purge(doc2!);

                        break;
                    }
                    case PendingDocIDSel.Create:
                    case PendingDocIDSel.Filter:
                    default:
                        break;
                }

                using (var replicator = new Replicator(config)) {
                    var wa = new WaitAssert();
                    var token = replicator.AddChangeListener((_, args) =>
                    {
                        // ReSharper disable AccessToDisposedClosure
                        if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                            docIdIsPending = replicator.IsDocumentPending(ColADocId, colAInDb!);
                            docIdIsPending.ShouldBeFalse();
                            docIdIsPending = replicator.IsDocumentPending(ColBDocId, colBInDb!);
                            docIdIsPending.ShouldBeFalse();
                        }
                        // ReSharper restore AccessToDisposedClosure

                        wa.RunConditionalAssert(() => args.Status.Activity == ReplicatorActivityLevel.Stopped);
                    });

                    docIdIsPending = replicator.IsDocumentPending(ColADocId, colAInDb!);
                    var docIdIsPending1 = replicator.IsDocumentPending(ColBDocId, colBInDb!);
                    switch (selection) {
                        case PendingDocIDSel.Create:
                        case PendingDocIDSel.Update:
                        case PendingDocIDSel.Delete:
                            docIdIsPending.ShouldBeTrue();
                            docIdIsPending = replicator.IsDocumentPending("IdNotThere", colAInDb!);
                            docIdIsPending.ShouldBeFalse();
                            docIdIsPending1.ShouldBeTrue();
                            docIdIsPending1 = replicator.IsDocumentPending("IdNotThere", colBInDb!);
                            break;
                        case PendingDocIDSel.Filter:
                            docIdIsPending.ShouldBeTrue();
                            docIdIsPending = replicator.IsDocumentPending("doc1", colAInDb!);
                            docIdIsPending.ShouldBeFalse();
                            docIdIsPending1.ShouldBeTrue();
                            docIdIsPending1 = replicator.IsDocumentPending("doc3", colBInDb!);
                            break;
                        case PendingDocIDSel.Purge:
                            docIdIsPending.ShouldBeFalse();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(selection), selection, null);
                    }

                    docIdIsPending1.ShouldBeFalse();
                    replicator.Start();

                    wa.WaitForResult(TimeSpan.FromSeconds(50));

                    // ReSharper disable once AccessToDisposedClosure
                    Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                        .Times(5)
                        .Delay(TimeSpan.FromMilliseconds(500))
                        .Go().ShouldBeTrue();

                    replicator.IsDocumentPending(ColADocId, colAInDb!).ShouldBeFalse();
                    replicator.IsDocumentPending(ColBDocId, colBInDb!).ShouldBeFalse();
                    token.Remove();
                }
            }

            Thread.Sleep(500); //it takes a while to get the replicator to actually released...
        }

        private void LoadCollectionsDocs()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var colC = OtherDb.CreateCollection("colA", "scopeA");
            var colD = OtherDb.CreateCollection("colB", "scopeA");
            using (var doc = new MutableDocument("doc"))
            using (var doc1 = new MutableDocument("doc1")) {
                doc.SetString("str", "string");
                doc1.SetString("str1", "string1");
                colA.Save(doc);
                colA.Save(doc1);
            }

            using (var doc = new MutableDocument("doc2"))
            using (var doc1 = new MutableDocument("doc3")) {
                doc.SetString("str2", "string2");
                doc1.SetString("str3", "string3");
                colB.Save(doc);
                colB.Save(doc1);
            }

            using (var doc = new MutableDocument("doc4"))
            using (var doc1 = new MutableDocument("doc5")) {
                doc.SetString("str4", "string4");
                doc1.SetString("str5", "string5");
                colC.Save(doc);
                colC.Save(doc1);
            }

            using (var doc = new MutableDocument("doc6"))
            using (var doc1 = new MutableDocument("doc7")) {
                doc.SetString("str6", "string6");
                doc1.SetString("str7", "string7");
                colD.Save(doc);
                colD.Save(doc1);
            }

            // Check docs in Db - before replication
            var colAInDb = Db.GetCollection("colA", "scopeA");
            var colBInDb = Db.GetCollection("colB", "scopeA");
            colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInDb.GetDocument("doc4").ShouldBeNull("Because doc4 is not created in colA in Db");
            colAInDb.GetDocument("doc5").ShouldBeNull("Because doc5 is not created in colA in Db");
            colBInDb.GetDocument("doc6").ShouldBeNull("Because doc6 is not created in colB in Db");
            colBInDb.GetDocument("doc7").ShouldBeNull("Because doc7 is not created in colB in Db");

            // Check docs in OtherDb - before replication
            var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA");
            colAInOtherDb!.GetDocument("doc").ShouldBeNull("Because doc is not created in colA in OtherDb");
            colAInOtherDb.GetDocument("doc1").ShouldBeNull("Because doc1 is not created in colA in OtherDb");
            colBInOtherDb!.GetDocument("doc2").ShouldBeNull("Because doc2 is not created in colA in OtherDb");
            colBInOtherDb.GetDocument("doc3").ShouldBeNull("Because doc3 is not created in colA in OtherDb");
            colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        private ReplicatorConfiguration CreateConfig(IEnumerable<CollectionConfiguration> configs, ReplicatorType type, bool continuous = false)
        {
            var config = new ReplicatorConfiguration(configs, new DatabaseEndpoint(OtherDb))
            {
                ReplicatorType = type,
                Continuous = continuous
            };
            return config;
        }

        private bool _replicator__filterAllowsOddDocIdsCallback(Document document, DocumentFlags flags)
        {
            document.RevisionID.ShouldNotBeNull();
            if (_allowOddIds.Any(id => id == document.Id))
                return true;

            return false;
        }

        private bool _replicator__filterCallbackTrue(Document document, DocumentFlags flags)
        {
            return true;
        }

        #endregion

#endif
    }
}

