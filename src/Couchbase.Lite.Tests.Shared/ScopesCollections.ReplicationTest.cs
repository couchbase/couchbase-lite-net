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
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Logging;

using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;
using Couchbase.Lite.Query;

using Shouldly;
using LiteCore;
using LiteCore.Interop;

using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Reflection;

using Test.Util;
#if COUCHBASE_ENTERPRISE
using Couchbase.Lite.P2P;
using ProtocolType = Couchbase.Lite.P2P.ProtocolType;
#endif

using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public sealed class ScopesCollectionsReplicationTest : ReplicatorTestBase
    {
        private enum PENDING_DOC_ID_SEL { CREATE = 0, UPDATE, DELETE, PURGE, FILTER }
        private List<DocumentReplicationEventArgs> _replicationEvents = new List<DocumentReplicationEventArgs>();

        public ScopesCollectionsReplicationTest(ITestOutputHelper output) : base(output)

        {

        }
#if COUCHBASE_ENTERPRISE

        #region 8.13 ReplicatorConfiguration 

        [Fact]
        [Obsolete]
        public void TestCreateConfigWithDatabase()
        {
            var config = new ReplicatorConfiguration(Db, new DatabaseEndpoint(OtherDb));
            config.Collections.ShouldContain(Db.GetDefaultCollection(), "Because Default collection configuration with default collection is created with ReplicatorConfiguration init.");
            var collConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            collConfig.ShouldNotBeNull("because it was just set");
            collConfig!.GetType().ShouldBe(typeof(CollectionConfiguration));
            collConfig.Equals(config.DefaultCollectionConfig).ShouldBeTrue();
            collConfig.ConflictResolver.ShouldBe(ConflictResolver.Default);
            collConfig.PushFilter.ShouldBeNull();
            collConfig.PullFilter.ShouldBeNull();
            collConfig.Channels.ShouldBeNull();
            collConfig.DocumentIDs.ShouldBeNull();
            config.Database.ShouldBe(Db);
        }

        [Fact]
        [Obsolete]
        public void TestCreateConfigWithDatabaseAndConflictResolver()
        {
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = new FakeConflictResolver()
            });

            var collConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            collConfig?.ConflictResolver.ShouldBe(config.ConflictResolver);
        }

        [Fact]
        [Obsolete]
        public void TestUpdateConflictResolverForDefaultCollection()
        {
            var config = new ReplicatorConfiguration(Db, new DatabaseEndpoint(OtherDb)) { ConflictResolver = new FakeConflictResolver() };
            var collConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            collConfig.ShouldNotBeNull("because it was just set");
            collConfig!.ConflictResolver.ShouldBe(config.ConflictResolver);
            config.ConflictResolver = new TestConflictResolver((conflict) => { return conflict.LocalDocument; });
            collConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            collConfig.ShouldNotBeNull("because it was just set");
            collConfig!.ConflictResolver.ShouldBe(config.ConflictResolver);
            collConfig.ConflictResolver = new FakeConflictResolver();
            config.AddCollection(Db.GetDefaultCollection(), collConfig);
            collConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            collConfig?.ConflictResolver.ShouldBe(config.ConflictResolver);
        }

        [Fact]
        [Obsolete]
        public void TestCreateConfigWithDatabaseAndFilters()
        {
            var config = new ReplicatorConfiguration(Db, new DatabaseEndpoint(OtherDb))
            {
                Channels = new List<string> { "channelA", "channelB" },
                DocumentIDs = new List<string> { "doc1", "doc2" },
                ConflictResolver = new FakeConflictResolver(),
                PullFilter = _replicator__filterCallbackTrue,
                PushFilter = _replicator__filterCallbackTrue
            };

            var defaultCollConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            defaultCollConfig.ShouldNotBeNull("because the default collection should be present by default");
            defaultCollConfig!.Channels.ShouldBeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.ShouldBeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.ShouldBeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.ShouldBeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.ShouldBeSameAs(config.PushFilter);
        }

        [Fact]
        [Obsolete]
        public void TestUpdateFiltersForDefaultCollection()
        {
            var config = new ReplicatorConfiguration(Db, new DatabaseEndpoint(OtherDb))
            {
                Channels = new List<string> { "channelA", "channelB" },
                DocumentIDs = new List<string> { "doc1", "doc2" },
                ConflictResolver = new FakeConflictResolver(),
                PullFilter = _replicator__filterCallbackTrue,
                PushFilter = _replicator__filterCallbackTrue
            };

            var defaultCollConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            defaultCollConfig.ShouldNotBeNull("because the default collection should be present by default");
            defaultCollConfig!.Channels.ShouldBeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.ShouldBeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.ShouldBeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.ShouldBeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.ShouldBeSameAs(config.PushFilter);

            config.Channels = new List<string> { "channelA1", "channelB1" };
            config.DocumentIDs = new List<string> { "doc1a", "doc2a" };
            config.ConflictResolver = new TestConflictResolver((conflict) => { return conflict.LocalDocument; });
            config.PullFilter = _replicator__filterCallbackFalse;
            config.PushFilter = _replicator__filterCallbackFalse;

            defaultCollConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            defaultCollConfig.ShouldNotBeNull("because the default collection should be present by default");
            defaultCollConfig!.Channels.ShouldBeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.ShouldBeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.ShouldBeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.ShouldBeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.ShouldBeSameAs(config.PushFilter);

            defaultCollConfig.Channels = new List<string> { "channelA", "channelB" };
            defaultCollConfig.DocumentIDs = new List<string> { "doc1", "doc2" };
            defaultCollConfig.ConflictResolver = new FakeConflictResolver();
            defaultCollConfig.PullFilter = _replicator__filterCallbackTrue;
            defaultCollConfig.PushFilter = _replicator__filterCallbackTrue;

            config.AddCollection(Db.GetDefaultCollection(), defaultCollConfig);

            defaultCollConfig = config.GetCollectionConfig(Db.GetDefaultCollection());
            defaultCollConfig.ShouldNotBeNull("because the default collection should be present by default");
            defaultCollConfig!.Channels.ShouldBeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.ShouldBeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.ShouldBeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.ShouldBeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.ShouldBeSameAs(config.PushFilter);
        }

        [Fact]
        [Obsolete]
        public void TestCreateConfigWithEndpointOnly()
        {
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.Collections.ShouldBeEmpty("Because no collection was added via AddCollection or AddCollections");
            Should.Throw<CouchbaseLiteException>(() => config.Database, "Because there is no Database atm since neither AddCollection nor AddCollections were called.");
        }

        [Fact]
        public void TestAddCollectionsWithoutCollectionConfig()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(colA);
            config.AddCollection(colB);
            config.Collections.Contains(colA).ShouldBeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).ShouldBeTrue("Because collection colB just added via AddCollection method");
            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ShouldNotBeNull("because it was just set");
            colBConfig.ShouldNotBeNull("because it was just set");
            colAConfig.ShouldNotBeSameAs(colBConfig, "Because the returned configs should be different instances.");
            colAConfig!.ConflictResolver.ShouldBe(ConflictResolver.Default, $"Property was never assigned and default value was {ConflictResolver.Default}.");
            colAConfig.PushFilter.ShouldBeNull("Property was never assigned and default value was null.");
            colAConfig.PullFilter.ShouldBeNull("Property was never assigned and default value was null.");
            colBConfig!.ConflictResolver.ShouldBe(ConflictResolver.Default, $"Property was never assigned and default value was {ConflictResolver.Default}.");
            colBConfig.PushFilter.ShouldBeNull("Property was never assigned and default value was null.");
            colBConfig.PullFilter.ShouldBeNull("Property was never assigned and default value was null.");
        }

        [Fact]
        public void TestAddCollectionsWithCollectionConfig()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            var colConfig = new CollectionConfiguration()
            {
                ConflictResolver = new FakeConflictResolver(),
                PullFilter = _replicator__filterCallbackTrue,
                PushFilter = _replicator__filterCallbackTrue
            };
            config.AddCollection(colA, colConfig);
            config.AddCollection(colB, colConfig);
            config.Collections.Contains(colA).ShouldBeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).ShouldBeTrue("Because collection colB just added via AddCollection method");
            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ShouldNotBeNull("because it was just set");
            colBConfig.ShouldNotBeNull("because it was just set");
            colAConfig.ShouldNotBe(colBConfig, "Because the returned configs should be different instances.");
            colAConfig!.ConflictResolver.ShouldBe(colBConfig!.ConflictResolver, "Both properties were assigned with same value.");
            colAConfig.PushFilter.ShouldBe(colBConfig.PushFilter, "Both properties were assigned with same value.");
            colAConfig.PullFilter.ShouldBe(colBConfig.PullFilter, "Both properties were assigned with same value.");
        }

        [Fact]
        public void TestAddCollection()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(colA);
            var colConfig = new CollectionConfiguration()
            {
                ConflictResolver = new FakeConflictResolver(),
                PullFilter = _replicator__filterCallbackTrue,
                PushFilter = _replicator__filterCallbackTrue
            };
            config.AddCollection(colB, colConfig);
            config.Collections.Contains(colA).ShouldBeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).ShouldBeTrue("Because collection colB just added via AddCollection method");
            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ShouldNotBeNull("because it was just set");
            colBConfig.ShouldNotBeNull("because it was just set");
            colAConfig!.ConflictResolver.ShouldBe(ConflictResolver.Default, $"Property was never assigned and default value was {ConflictResolver.Default}.");
            colAConfig.PushFilter.ShouldBeNull("Property was never assigned and default value was null.");
            colAConfig.PullFilter.ShouldBeNull("Property was never assigned and default value was null.");
            colBConfig!.ConflictResolver.ShouldBe(colConfig.ConflictResolver, "Property was just updated via AddCollection.");
            colBConfig.PushFilter.ShouldBe(colConfig.PushFilter, "Property was just updated via AddCollection.");
            colBConfig.PullFilter.ShouldBe(colConfig.PullFilter, "Property was just updated via AddCollection.");
        }

        [Fact]
        public void TestUpdateCollectionConfig()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            var colConfig = new CollectionConfiguration()
            {
                ConflictResolver = new FakeConflictResolver(),
                PullFilter = _replicator__filterCallbackTrue,
                PushFilter = _replicator__filterCallbackTrue
            };
            config.AddCollection(colA, colConfig);
            config.AddCollection(colB, colConfig);
            config.Collections.Contains(colA).ShouldBeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).ShouldBeTrue("Because collection colB just added via AddCollection method");

            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ShouldNotBeNull("because it was just set");
            colBConfig.ShouldNotBeNull("because it was just set");
            colAConfig!.ConflictResolver.ShouldBe(colConfig.ConflictResolver);
            colBConfig!.ConflictResolver.ShouldBe(colConfig.ConflictResolver);
            colAConfig.PullFilter.ShouldBe(colConfig.PullFilter);
            colBConfig.PullFilter.ShouldBe(colConfig.PullFilter);
            colAConfig.PushFilter.ShouldBe(colConfig.PushFilter);
            colBConfig.PushFilter.ShouldBe(colConfig.PushFilter);

            config.AddCollection(colA);
            colConfig = new CollectionConfiguration()
            {
                ConflictResolver = new TestConflictResolver((conflict) => { return conflict.LocalDocument; }),
                PullFilter = _replicator__filterCallbackFalse,
                PushFilter = _replicator__filterCallbackFalse
            };
            config.AddCollection(colB, colConfig);

            colAConfig = config.GetCollectionConfig(colA);
            colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ShouldNotBeNull("because it was just set");
            colBConfig.ShouldNotBeNull("because it was just set");
            colAConfig!.ConflictResolver.ShouldBe(ConflictResolver.Default);
            colBConfig!.ConflictResolver.ShouldBe(colConfig.ConflictResolver);
            colAConfig.PullFilter.ShouldBe(null);
            colBConfig.PullFilter.ShouldBe(colConfig.PullFilter);
            colAConfig.PushFilter.ShouldBe(null);
            colBConfig.PushFilter.ShouldBe(colConfig.PushFilter);
        }

        [Fact]
        public void TestRemoveCollection()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            var colConfig = new CollectionConfiguration()
            {
                ConflictResolver = new FakeConflictResolver(),
                PullFilter = _replicator__filterCallbackTrue,
                PushFilter = _replicator__filterCallbackTrue
            };
            config.AddCollection(colA, colConfig);
            config.AddCollection(colB, colConfig);
            config.Collections.Contains(colA).ShouldBeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).ShouldBeTrue("Because collection colB just added via AddCollection method");

            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ShouldNotBeNull("because it was just set");
            colBConfig.ShouldNotBeNull("because it was just set");
            colAConfig!.ConflictResolver.ShouldBe(colConfig.ConflictResolver);
            colBConfig!.ConflictResolver.ShouldBe(colConfig.ConflictResolver);
            colAConfig.PullFilter.ShouldBe(colConfig.PullFilter);
            colBConfig.PullFilter.ShouldBe(colConfig.PullFilter);
            colAConfig.PushFilter.ShouldBe(colConfig.PushFilter);
            colBConfig.PushFilter.ShouldBe(colConfig.PushFilter);

            config.RemoveCollection(colB);
            config.Collections.Contains(colA).ShouldBeTrue("Because collection colA should still be there");
            config.Collections.Contains(colB).ShouldBeFalse("Because collection colB just removed via RemoveCollection method");

            config.GetCollectionConfig(colA).ShouldNotBeNull("Because collection colA should still be there, thus it's config should also exist as well.");
            config.GetCollectionConfig(colB).ShouldBeNull("Because collection colB just removed via RemoveCollection method, thus it's config should also be null.");
        }

        [Fact]
        public void TestAddCollectionsFromDifferentDatabaseInstances()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = OtherDb.CreateCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(colA);
            config.Collections.Contains(colA).ShouldBeTrue();
            var ex = Should.Throw<CouchbaseLiteException>(() => config.AddCollection(colB));
            ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
            ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact]
        public void TestAddDeletedCollections()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = OtherDb.CreateCollection("colB", "scopeA");
            OtherDb.DeleteCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(colA);
            config.Collections.Contains(colA).ShouldBeTrue();
            var ex = Should.Throw<CouchbaseLiteException>(() => config.AddCollection(colB));
            ex.Error.ShouldBe(CouchbaseLiteError.InvalidParameter);
            ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        }

        #endregion

        #region 8.14 Replicator

        [Fact]
        public void TestCollectionSingleShotPushReplication()
        {
            var config = CreateConfig(ReplicatorType.Push);
            LoadCollectionsDocs(config);
            RunReplication(config, 0, 0);

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colBInOtherDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInOtherDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }
        }

        [Fact]
        public void TestCollectionSingleShotPullReplication()
        {
            var config = CreateConfig(ReplicatorType.Pull);
            LoadCollectionsDocs(config);
            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }
        }

        [Fact]
        public void TestCollectionSingleShotPushPullReplicationCRUD()
        {
            var config = CreateConfig(ReplicatorType.PushAndPull);
            //Test Add and Read docs in replication
            LoadCollectionsDocs(config);
            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
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
                colAInOtherDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInOtherDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
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
                using (var doc = colAInDb!.GetDocument("doc4"))
                using (var mdoc = doc?.ToMutable()) {
                    doc?.GetString("str4").ShouldBe("string4");
                    colAInOtherDb!.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                    mdoc!.SetString("str4", "string4 update");
                    colAInDb.Save(mdoc);
                }
            }

            RunReplication(config, 0, 0);

            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb!.GetDocument("doc4")?.GetString("str4").ShouldBe("string4 update");
            }

            //Test Delete doc in replication
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.ShouldNotBeNull("because it was just created");

                using (var doc = colAInDb!.GetDocument("doc4")) {
                    doc?.GetString("str4").ShouldBe("string4 update");
                    colAInOtherDb!.GetDocument("doc4")?.GetString("str4").ShouldBe("string4 update");
                    colAInDb.Delete(doc!);
                }
            }

            RunReplication(config, 0, 0);

            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb!.GetDocument("doc4").ShouldBeNull();
            }
        }

        [Fact]
        public void TestCollectionContinuousPushReplication()
        {
            var config = CreateConfig(ReplicatorType.Push, true);
            LoadCollectionsDocs(config);
            RunReplication(config, 0, 0);

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colBInOtherDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInOtherDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }
        }

        [Fact]
        public void TestCollectionContinuousPullReplication()
        {
            var config = CreateConfig(ReplicatorType.Pull, true);
            LoadCollectionsDocs(config);
            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }
        }

        [Fact]
        public void TestCollectionContinuousPushPullReplication()
        {
            var config = CreateConfig(ReplicatorType.PushAndPull, true);
            LoadCollectionsDocs(config);

            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
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
                colAInOtherDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInOtherDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
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
            var config = CreateConfig(ReplicatorType.Pull);
            LoadCollectionsDocs(config);
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
                colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
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
                colAInDb!.GetDocument("doc4").ShouldBeNull();
                colAInDb.GetDocument("doc5").ShouldBeNull();
            }

            RunReplication(config, 0, 0, true);

            using (var colAInDb = Db.GetCollection("colA", "scopeA")) {
                colAInDb.ShouldNotBeNull();

                // After reset in replication, colA in Db are pulled from otherDb again
                colAInDb!.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            }
        }

        [Fact]
        public void TestMismatchedCollectionReplication()
        {
            var config = CreateConfig(ReplicatorType.Pull);
            using (var colA = Db.CreateCollection("colA", "scopeA"))
            using (var colB = OtherDb.CreateCollection("colB", "scopeA")) {
                config.AddCollection(colA);
                RunReplication(config, (int)CouchbaseLiteError.WebSocketUserPermanent, CouchbaseLiteErrorType.CouchbaseLite);
            }
        }

        [Fact]
        public void TestCollectionDocumentReplicationEvents()
        {
            var config = CreateConfig(ReplicatorType.PushAndPull);
            LoadCollectionsDocs(config);

            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colBInOtherDb.ShouldNotBeNull("because it was just created");

                using (var docInColAInDb = colAInDb!.GetDocument("doc"))
                using (var doc2InColBDb = colBInDb!.GetDocument("doc2"))
                using (var doc4InColAInOtherDb = colAInOtherDb!.GetDocument("doc4"))
                using (var doc6InColBInOtherDb = colBInOtherDb!.GetDocument("doc6")) {
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
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    Console.WriteLine($"{args.IsPush} {JsonConvert.SerializeObject(args.Documents.Select(x => $"{x.Flags} {x.CollectionName} {x.Id}"))}");
                    pushWait.RunConditionalAssert(() =>
                        args.IsPush 
                        && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted) && x.CollectionName == "colA" && x.Id == "doc")
                        && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted) && x.CollectionName == "colB" && x.Id == "doc2"));
                    pullWait.RunConditionalAssert(() =>
                        !args.IsPush 
                        && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted) && x.CollectionName == "colA" && x.Id == "doc4")
                        && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted) && x.CollectionName == "colB" && x.Id == "doc6"));
                });
            });

            pushWait.WaitForResult(TimeSpan.FromSeconds(5));
            pullWait.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TestCollectionDefaultConflictResolver()
        {
            var config = CreateConfig(ReplicatorType.PushAndPull);
            LoadCollectionsDocs(config);
            RunReplication(config, 0, 0);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                using (var doc = colAInDb!.GetDocument("doc1"))
                using (var mdoc = doc?.ToMutable()) {
                    mdoc.ShouldNotBeNull("because it was just saved");
                    mdoc!.SetString("str1", "string1 update");
                    colAInDb.Save(mdoc);
                }

                using (var doc = colAInOtherDb!.GetDocument("doc1"))
                using (var mdoc = doc?.ToMutable()) {
                    mdoc.ShouldNotBeNull("because it was just saved");
                    mdoc!.SetString("str1", "string1 update1");
                    colAInOtherDb.Save(mdoc);
                }

                using (var doc = colAInOtherDb.GetDocument("doc1"))
                using (var mdoc = doc?.ToMutable()) {
                    mdoc.ShouldNotBeNull("because it was just saved");
                    mdoc!.SetString("str1", "string1 update again");
                    colAInOtherDb.Save(mdoc);
                }

                RunReplication(config, 0, 0);

                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1 update again");
                colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1 update again"); 
            }
        }

        [Fact]
        public void TestCollectionConflictResolver()
        {
            var config = CreateConfig(ReplicatorType.PushAndPull);
            LoadCollectionsDocs(config);
            RunReplication(config, 0, 0);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInDb.ShouldNotBeNull("because it was just created");
                colBInDb.ShouldNotBeNull("because it was just created");
                colAInOtherDb.ShouldNotBeNull("because it was just created");
                colBInOtherDb.ShouldNotBeNull("because it was just created");

                var cllConfigADb = config.GetCollectionConfig(colAInDb!);
                cllConfigADb!.ConflictResolver = new TestConflictResolver((conflict) => {
                    return conflict.LocalDocument;
                });

                var cllConfigBDb = config.GetCollectionConfig(colBInDb!);
                cllConfigBDb!.ConflictResolver = new TestConflictResolver((conflict) => {
                    return conflict.RemoteDocument;
                });

                using (var doc = colAInDb!.GetDocument("doc1"))
                using (var mdoc = doc?.ToMutable()) {
                    mdoc.ShouldNotBeNull("because it was just saved");
                    mdoc!.SetString("str1", "string1 update");
                    colAInDb.Save(mdoc);
                }

                using (var doc = colBInDb!.GetDocument("doc3"))
                using (var mdoc = doc?.ToMutable()) {
                    mdoc.ShouldNotBeNull("because it was just saved");
                    mdoc!.SetString("str3", "string3 update");
                    colBInDb.Save(mdoc);
                }

                using (var doc = colAInOtherDb!.GetDocument("doc1"))
                using (var mdoc = doc?.ToMutable()) {
                    mdoc.ShouldNotBeNull("because it was just saved");
                    mdoc!.SetString("str1", "string1 update1");
                    colAInOtherDb.Save(mdoc);
                }

                using (var doc = colBInOtherDb!.GetDocument("doc3"))
                using (var mdoc = doc?.ToMutable()) {
                    mdoc.ShouldNotBeNull("because it was just saved");
                    mdoc!.SetString("str3", "string3 update1");
                    colBInOtherDb.Save(mdoc);
                }

                RunReplication(config, 0, 0);

                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1 update");
                colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3 update1");
            }
        }

        [Fact]
        public void TestCollectionPushFilter()
        {
            var config = CreateConfig(ReplicatorType.Push);
            LoadCollectionsDocs(config);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                config.GetCollectionConfig(colAInDb!)!.PushFilter = _replicator__filterAllowsOddDocIdsCallback;
                config.GetCollectionConfig(colBInDb!)!.PushFilter = _replicator__filterAllowsOddDocIdsCallback;

                RunReplication(config, 0, 0);

                // Check docs in OtherDb - make sure docs with odd doc ids are pushed to the OtherDb from the Db
                using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
                using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                    colAInOtherDb!.GetDocument("doc").ShouldBeNull();
                    colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                    colBInOtherDb!.GetDocument("doc2")?.ShouldBeNull();
                    colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                    colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                    colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                    colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                    colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
                }
            }
        }

        [Fact]
        public void TestCollectionPullFilter()
        {
            var config = CreateConfig(ReplicatorType.Pull);
            LoadCollectionsDocs(config);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                config.GetCollectionConfig(colAInDb!)!.PullFilter = _replicator__filterAllowsOddDocIdsCallback;
                config.GetCollectionConfig(colBInDb!)!.PullFilter = _replicator__filterAllowsOddDocIdsCallback;

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
        }

        IList<string> allowOddIds = new List<string> { "doc1", "doc3", "doc5", "doc7" };

        [Fact]
        public void TestCollectionDocumentIDsPushFilter()
        {
            var config = CreateConfig(ReplicatorType.Push);
            LoadCollectionsDocs(config);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                config.GetCollectionConfig(colAInDb!)!.DocumentIDs = allowOddIds;
                config.GetCollectionConfig(colBInDb!)!.DocumentIDs = allowOddIds;
            }

            RunReplication(config, 0, 0);

            // Check docs in OtherDb - make sure docs with odd doc ids are pushed to the OtherDb from the Db
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInOtherDb!.GetDocument("doc").ShouldBeNull();
                colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInOtherDb!.GetDocument("doc2").ShouldBeNull();
                colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
                colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
                colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }
        }

        [Fact]
        public void TestCollectionDocumentIDsPullFilter()
        {
            var config = CreateConfig(ReplicatorType.Pull);
            LoadCollectionsDocs(config);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                config.GetCollectionConfig(colAInDb!)!.DocumentIDs = allowOddIds;
                config.GetCollectionConfig(colBInDb!)!.DocumentIDs = allowOddIds;
            }

            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs with odd doc ids are pulled from the OtherDb to the Db
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
                colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
                colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
                colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
                colAInDb.GetDocument("doc4").ShouldBeNull();
                colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
                colBInDb.GetDocument("doc6").ShouldBeNull();
                colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
            }
        }

        [Fact]
        public void TestCollectionGetPendingDocIDsWithCreate() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.CREATE);

        [Fact]
        public void TestCollectionGetPendingDocIDsWithUpdate() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.UPDATE);

        [Fact]
        public void TestCollectionGetPendingDocIDsWithDelete() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.DELETE);

        [Fact]
        public void TestCollectionGetPendingDocIDsWithPurge() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.PURGE);

        [Fact]
        public void TestCollectionGetPendingDocIDsWithFilter() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.FILTER);

        [Fact]
        public void TestCollectionIsDocumentPendingWithCreate() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.CREATE);

        [Fact]
        public void TestCollectionIsDocumentPendingWithUpdate() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.UPDATE);

        [Fact]
        public void TestCollectionIsDocumentPendingWithDelete() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.DELETE);

        [Fact]
        public void TestCollectionIsDocumentPendingWithPurge() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.PURGE);

        [Fact]
        public void TestCollectionIsDocumentPendingWithFilter() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.FILTER);

        [Fact]
        public void TestCreateReplicatorWithNoCollections()
        {
            var config = CreateConfig(ReplicatorType.PushAndPull);
            config.AddCollections(new List<Collection>());
            Action badAction = (() => new Replicator(config));
            Should.Throw<CouchbaseLiteException>(badAction, "Replicator Configuration must contain at least one collection.");
        }

        [Fact]
        public void TestAddCollectionsToDatabaseInitiatedConfig()
        {
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(DefaultCollection);
            LoadCollectionsDocs(config);

            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs are pulled from the OtherDb to the Db
            var colAInDb = Db.GetCollection("colA", "scopeA");
            var colBInDb = Db.GetCollection("colB", "scopeA");
            colAInDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA");
            colAInOtherDb!.GetDocument("doc")?.GetString("str").ShouldBe("string");
            colAInOtherDb.GetDocument("doc1")?.GetString("str1").ShouldBe("string1");
            colBInOtherDb!.GetDocument("doc2")?.GetString("str2").ShouldBe("string2");
            colBInOtherDb.GetDocument("doc3")?.GetString("str3").ShouldBe("string3");
            colAInOtherDb.GetDocument("doc4")?.GetString("str4").ShouldBe("string4");
            colAInOtherDb.GetDocument("doc5")?.GetString("str5").ShouldBe("string5");
            colBInOtherDb.GetDocument("doc6")?.GetString("str6").ShouldBe("string6");
            colBInOtherDb.GetDocument("doc7")?.GetString("str7").ShouldBe("string7");
        }

        [Fact]
        [Obsolete]
        public void TestOuterFiltersWithCollections()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");

            var targetEndpoint = new URLEndpoint(new Uri("wss://foo:4984/"));
            var config = new ReplicatorConfiguration(targetEndpoint); ;

            config.AddCollections(new List<Collection>() { colA, colB });

            var defaultCollection = Db.GetDefaultCollection();

            // set the outer filters after adding default collection
            config.AddCollection(defaultCollection);

            config.Channels = new List<string> { "channelA", "channelB" };
            config.DocumentIDs = new List<string> { "doc1", "doc2" };
            config.ConflictResolver = new FakeConflictResolver();
            config.PullFilter = _replicator__filterCallbackTrue;
            config.PushFilter = _replicator__filterCallbackTrue;

            var defaultCollConfig = config.GetCollectionConfig(defaultCollection);
            defaultCollConfig!.Channels.ShouldBeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.ShouldBeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.ShouldBeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.ShouldBeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.ShouldBeSameAs(config.PushFilter);
        }

        [Fact]
        [Obsolete]
        public void TestUpdateCollectionConfigWithDefault()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var defaultCollection = Db.GetDefaultCollection();

            var targetEndpoint = new URLEndpoint(new Uri("wss://foo:4984/"));
            var replConfig = new ReplicatorConfiguration(targetEndpoint);

            var collConfig = new CollectionConfiguration()
            {
                PullFilter = _replicator__filterCallbackTrue
            };

            // Use addCollections() to add both colA and the default collection to the replicator config 
            // configured with the the CollectionConfiguration.
            replConfig.AddCollections(new List<Collection>() { defaultCollection, colA }, collConfig);

            collConfig = replConfig.GetCollectionConfig(defaultCollection);
            collConfig!.PullFilter.ShouldNotBeNull("Because defaultCollection's collConfig PullFilter is assigned");
            collConfig.PushFilter.ShouldBeNull("Because no PushFilter is assigned in defaultCollection's collConfig");

            collConfig = replConfig.GetCollectionConfig(colA);
            collConfig!.PullFilter.ShouldNotBeNull("Because colA's collConfig PullFilter is assigned");
            collConfig.PushFilter.ShouldBeNull("Because no PushFilter is assigned in colA's collConfig");

            // Set ReplicationFilter as the push filter using the deprecated ReplicatorConfiguration.setPushFilter method.
            replConfig.PushFilter = _replicator__filterCallbackTrue;

            collConfig = replConfig.GetCollectionConfig(defaultCollection);
            collConfig!.PullFilter.ShouldNotBeNull("Because defaultCollection's collConfig PullFilter is assigned");
            collConfig.PushFilter.ShouldNotBeNull("Because defaultCollection's collConfig PushFilter is assigned");

            // Verify that the only thing that has changed is the default collection's push filter.
            collConfig = replConfig.GetCollectionConfig(colA);
            collConfig!.PullFilter.ShouldNotBeNull("Because colA's collConfig PullFilter is assigned");
            collConfig.PushFilter.ShouldBeNull("Because no PushFilter is assigned in colA's collConfig");
        }

        #endregion

        #region Private Methods

        private void ValidatePendingDocumentIds(PENDING_DOC_ID_SEL selection)
        {
            IImmutableSet<string> pendingDocIds;
            var colADocId = "doc";
            var colBDocId = "doc2";

            var config = CreateConfig(ReplicatorType.Push);
            LoadCollectionsDocs(config);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                if (selection == PENDING_DOC_ID_SEL.UPDATE) {
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
                } else if (selection == PENDING_DOC_ID_SEL.DELETE) {
                    using (var doc = colAInDb!.GetDocument(colADocId)) {
                        colAInDb.Delete(doc!);
                    }

                    using (var doc = colBInDb!.GetDocument(colBDocId)) {
                        colBInDb.Delete(doc!);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                    using (var doc = colAInDb!.GetDocument(colADocId)) {
                        colAInDb.Purge(doc!);
                    }

                    using (var doc = colBInDb!.GetDocument(colBDocId)) {
                        colBInDb.Purge(doc!);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                    config.GetCollectionConfig(colAInDb!)!.PushFilter = (doc, isPush) =>
                    {
                        if (doc.Id.Equals(colADocId))
                            return true;
                        return false;
                    };

                    config.GetCollectionConfig(colBInDb!)!.PushFilter = (doc, isPush) =>
                    {
                        if (doc.Id.Equals(colBDocId))
                            return true;
                        return false;
                    };
                }

                using (var replicator = new Replicator(config)) {
                    var wa = new WaitAssert();
                    var token = replicator.AddChangeListener((sender, args) =>
                    {
                        wa.RunConditionalAssert(() =>
                        {
                            if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                                pendingDocIds = replicator.GetPendingDocumentIDs(colAInDb!);
                                pendingDocIds.Count.ShouldBe(0);

                                pendingDocIds = replicator.GetPendingDocumentIDs(colBInDb!);
                                pendingDocIds.Count.ShouldBe(0);
                            }

                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    pendingDocIds = replicator.GetPendingDocumentIDs(colAInDb!);
                    var pendingDocIds1 = replicator.GetPendingDocumentIDs(colBInDb!);
                    if (selection == PENDING_DOC_ID_SEL.FILTER) {
                        pendingDocIds.Count.ShouldBe(1);
                        pendingDocIds.ElementAt(0).ShouldBe(colADocId);
                        pendingDocIds1.Count.ShouldBe(1);
                        pendingDocIds1.ElementAt(0).ShouldBe(colBDocId);
                    } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
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

        private void ValidateIsDocumentPending(PENDING_DOC_ID_SEL selection)
        {
            bool docIdIsPending;
            var colADocId = "doc";
            var colBDocId = "doc2";

            var config = CreateConfig(ReplicatorType.Push);
            LoadCollectionsDocs(config);

            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                if (selection == PENDING_DOC_ID_SEL.UPDATE) {
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
                } else if (selection == PENDING_DOC_ID_SEL.DELETE) {
                    using (var doc = colAInDb!.GetDocument(colADocId)) {
                        colAInDb.Delete(doc!);
                    }

                    using (var doc = colBInDb!.GetDocument(colBDocId)) {
                        colBInDb.Delete(doc!);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                    using (var doc = colAInDb!.GetDocument(colADocId)) {
                        colAInDb.Purge(doc!);
                    }

                    using (var doc = colBInDb!.GetDocument(colBDocId)) {
                        colBInDb.Purge(doc!);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                    config.GetCollectionConfig(colAInDb!)!.PushFilter = (doc, isPush) =>
                    {
                        if (doc.Id.Equals(colADocId))
                            return true;
                        return false;
                    };

                    config.GetCollectionConfig(colBInDb!)!.PushFilter = (doc, isPush) =>
                    {
                        if (doc.Id.Equals(colBDocId))
                            return true;
                        return false;
                    };
                }

                using (var replicator = new Replicator(config)) {
                    var wa = new WaitAssert();
                    var token = replicator.AddChangeListener((sender, args) =>
                    {
                        if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                            docIdIsPending = replicator.IsDocumentPending(colADocId, colAInDb!);
                            docIdIsPending.ShouldBeFalse();
                            docIdIsPending = replicator.IsDocumentPending(colBDocId, colBInDb!);
                            docIdIsPending.ShouldBeFalse();
                        }

                        wa.RunConditionalAssert(() =>
                        {
                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    docIdIsPending = replicator.IsDocumentPending(colADocId, colAInDb!);
                    var docIdIsPending1 = replicator.IsDocumentPending(colBDocId, colBInDb!);
                    if (selection == PENDING_DOC_ID_SEL.CREATE || selection == PENDING_DOC_ID_SEL.UPDATE
                        || selection == PENDING_DOC_ID_SEL.DELETE) {
                        docIdIsPending.ShouldBeTrue();
                        docIdIsPending = replicator.IsDocumentPending("IdNotThere", colAInDb!);
                        docIdIsPending.ShouldBeFalse();
                        docIdIsPending1.ShouldBeTrue();
                        docIdIsPending1 = replicator.IsDocumentPending("IdNotThere", colBInDb!);
                        docIdIsPending1.ShouldBeFalse();
                    } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                        docIdIsPending.ShouldBeTrue();
                        docIdIsPending = replicator.IsDocumentPending("doc1", colAInDb!);
                        docIdIsPending.ShouldBeFalse();
                        docIdIsPending1.ShouldBeTrue();
                        docIdIsPending1 = replicator.IsDocumentPending("doc3", colBInDb!);
                        docIdIsPending1.ShouldBeFalse();
                    } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                        docIdIsPending.ShouldBeFalse();
                        docIdIsPending1.ShouldBeFalse();
                    }

                    replicator.Start();

                    wa.WaitForResult(TimeSpan.FromSeconds(50));

                    Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                        .Times(5)
                        .Delay(TimeSpan.FromMilliseconds(500))
                        .Go().ShouldBeTrue();

                    replicator.IsDocumentPending(colADocId, colAInDb!).ShouldBeFalse();
                    replicator.IsDocumentPending(colBDocId, colBInDb!).ShouldBeFalse();
                    token.Remove();
                }
            }

            Thread.Sleep(500); //it takes a while to get the replicator to actually released...
        }

        private void LoadCollectionsDocs(ReplicatorConfiguration config)
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

            config.AddCollection(colA);
            config.AddCollection(colB);
        }

        private ReplicatorConfiguration CreateConfig(ReplicatorType type, bool continuous = false)
        {
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.ReplicatorType = type;
            config.Continuous = continuous;
            return config;
        }

        private void DocumentEndedUpdate(object sender, DocumentReplicationEventArgs args)
        {
            _replicationEvents.Add(args);
        }

        private bool _replicator__filterAllowsOddDocIdsCallback(Document document, DocumentFlags flags)
        {
            document.RevisionID.ShouldNotBeNull();
            if (allowOddIds.Any(id => id == document.Id))
                return true;

            return false;
        }

        private bool _replicator__filterCallbackTrue(Document document, DocumentFlags flags)
        {
            return true;
        }

        private bool _replicator__filterCallbackFalse(Document document, DocumentFlags flags)
        {
            return false;
        }

        #endregion

#endif
    }
}

