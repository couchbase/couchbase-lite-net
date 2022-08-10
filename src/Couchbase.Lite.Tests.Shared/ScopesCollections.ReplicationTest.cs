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

using FluentAssertions;
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
    public sealed class ScopesCollectionsReplicationTest : ReplicatorTestBase
    {
        private enum PENDING_DOC_ID_SEL { CREATE = 0, UPDATE, DELETE, PURGE, FILTER }
        private List<DocumentReplicationEventArgs> _replicationEvents = new List<DocumentReplicationEventArgs>();
#if !WINDOWS_UWP
        public ScopesCollectionsReplicationTest(ITestOutputHelper output) : base(output)
#else
        public ScopesCollectionsReplicationTest()
#endif
        {

        }
#if COUCHBASE_ENTERPRISE

        #region 8.13 ReplicatorConfiguration 

        [Fact]
        public void TestCreateConfigWithDatabase()
        {
            var config = new ReplicatorConfiguration(Db, new DatabaseEndpoint(OtherDb));
            config.Collections.Should().Contain(Db.DefaultCollection, "Because Default collection configuration with default collection is created with ReplicatorConfiguration init.");
            var collConfig = config.GetCollectionConfig(Db.DefaultCollection);
            collConfig.GetType().Should().Be(typeof(CollectionConfiguration));
            collConfig.Equals(config.DefaultCollectionConfig).Should().BeTrue();
            collConfig.ConflictResolver.Should().BeNull();
            collConfig.PushFilter.Should().BeNull();
            collConfig.PullFilter.Should().BeNull();
            collConfig.Channels.Should().BeNull();
            collConfig.DocumentIDs.Should().BeNull();
            config.Database.Should().Be(Db);
        }

        [Fact]
        public void TestCreateConfigWithDatabaseAndConflictResolver()
        {
            var config = new ReplicatorConfiguration(Db, new DatabaseEndpoint(OtherDb)) { ConflictResolver = new FakeConflictResolver() };
            var collConfig = config.GetCollectionConfig(Db.DefaultCollection);
            collConfig.ConflictResolver.Should().Be(config.ConflictResolver);
        }

        [Fact]
        public void TestUpdateConflictResolverForDefaultCollection()
        {
            var config = new ReplicatorConfiguration(Db, new DatabaseEndpoint(OtherDb)) { ConflictResolver = new FakeConflictResolver() };
            var collConfig = config.GetCollectionConfig(Db.DefaultCollection);
            collConfig.ConflictResolver.Should().Be(config.ConflictResolver);
            config.ConflictResolver = new TestConflictResolver((conflict) => { return conflict.LocalDocument; });
            collConfig = config.GetCollectionConfig(Db.DefaultCollection);
            collConfig.ConflictResolver.Should().Be(config.ConflictResolver);
            collConfig.ConflictResolver = new FakeConflictResolver();
            config.AddCollection(Db.DefaultCollection, collConfig);
            collConfig = config.GetCollectionConfig(Db.DefaultCollection);
            collConfig.ConflictResolver.Should().Be(config.ConflictResolver);
        }

        [Fact]
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

            var defaultCollConfig = config.GetCollectionConfig(Db.DefaultCollection);
            defaultCollConfig.Channels.Should().BeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.Should().BeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.Should().BeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.Should().BeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.Should().BeSameAs(config.PushFilter);
        }

        [Fact]
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

            var defaultCollConfig = config.GetCollectionConfig(Db.DefaultCollection);
            defaultCollConfig.Channels.Should().BeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.Should().BeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.Should().BeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.Should().BeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.Should().BeSameAs(config.PushFilter);

            config.Channels = new List<string> { "channelA1", "channelB1" };
            config.DocumentIDs = new List<string> { "doc1a", "doc2a" };
            config.ConflictResolver = new TestConflictResolver((conflict) => { return conflict.LocalDocument; });
            config.PullFilter = _replicator__filterCallbackFalse;
            config.PushFilter = _replicator__filterCallbackFalse;

            defaultCollConfig = config.GetCollectionConfig(Db.DefaultCollection);
            defaultCollConfig.Channels.Should().BeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.Should().BeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.Should().BeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.Should().BeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.Should().BeSameAs(config.PushFilter);

            defaultCollConfig.Channels = new List<string> { "channelA", "channelB" };
            defaultCollConfig.DocumentIDs = new List<string> { "doc1", "doc2" };
            defaultCollConfig.ConflictResolver = new FakeConflictResolver();
            defaultCollConfig.PullFilter = _replicator__filterCallbackTrue;
            defaultCollConfig.PushFilter = _replicator__filterCallbackTrue;

            config.AddCollection(Db.DefaultCollection, defaultCollConfig);

            defaultCollConfig = config.GetCollectionConfig(Db.DefaultCollection);
            defaultCollConfig.Channels.Should().BeSameAs(config.Channels);
            defaultCollConfig.DocumentIDs.Should().BeSameAs(config.DocumentIDs);
            defaultCollConfig.ConflictResolver.Should().BeSameAs(config.ConflictResolver);
            defaultCollConfig.PullFilter.Should().BeSameAs(config.PullFilter);
            defaultCollConfig.PushFilter.Should().BeSameAs(config.PushFilter);
        }

        [Fact]
        public void TestCreateConfigWithEndpointOnly()
        {
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.Collections.Should().BeEmpty("Because no collection was added via AddCollection or AddCollections");
            config.Invoking(c => c.Database).Should().Throw<CouchbaseLiteException>("Because there is no Database atm since neither AddCollection nor AddCollections were called.");
        }

        [Fact]
        public void TestAddCollectionsWithoutCollectionConfig()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(colA);
            config.AddCollection(colB);
            config.Collections.Contains(colA).Should().BeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).Should().BeTrue("Because collection colB just added via AddCollection method");
            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.Should().NotBeSameAs(colBConfig, "Because the returned configs should be different instances.");
            colAConfig.ConflictResolver.Should().BeNull("Property was never assigned and default value was null.");
            colAConfig.PushFilter.Should().BeNull("Property was never assigned and default value was null.");
            colAConfig.PullFilter.Should().BeNull("Property was never assigned and default value was null.");
            colBConfig.ConflictResolver.Should().BeNull("Property was never assigned and default value was null.");
            colBConfig.PushFilter.Should().BeNull("Property was never assigned and default value was null.");
            colBConfig.PullFilter.Should().BeNull("Property was never assigned and default value was null.");
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
            config.Collections.Contains(colA).Should().BeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).Should().BeTrue("Because collection colB just added via AddCollection method");
            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.Equals(colBConfig).Should().BeFalse("Because the returned configs should be different instances.");
            colAConfig.ConflictResolver.Should().Be(colBConfig.ConflictResolver, "Both properties were assigned with same value.");
            colAConfig.PushFilter.Should().Be(colBConfig.PushFilter, "Both properties were assigned with same value.");
            colAConfig.PullFilter.Should().Be(colBConfig.PullFilter, "Both properties were assigned with same value.");
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
            config.Collections.Contains(colA).Should().BeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).Should().BeTrue("Because collection colB just added via AddCollection method");
            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ConflictResolver.Should().BeNull("Property was never assigned and default value was null.");
            colAConfig.PushFilter.Should().BeNull("Property was never assigned and default value was null.");
            colAConfig.PullFilter.Should().BeNull("Property was never assigned and default value was null.");
            colBConfig.ConflictResolver.Should().Be(colConfig.ConflictResolver, "Property was just updated via AddCollection.");
            colBConfig.PushFilter.Should().Be(colConfig.PushFilter, "Property was just updated via AddCollection.");
            colBConfig.PullFilter.Should().Be(colConfig.PullFilter, "Property was just updated via AddCollection.");
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
            config.Collections.Contains(colA).Should().BeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).Should().BeTrue("Because collection colB just added via AddCollection method");

            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ConflictResolver.Should().Be(colConfig.ConflictResolver);
            colBConfig.ConflictResolver.Should().Be(colConfig.ConflictResolver);
            colAConfig.PullFilter.Should().Be(colConfig.PullFilter);
            colBConfig.PullFilter.Should().Be(colConfig.PullFilter);
            colAConfig.PushFilter.Should().Be(colConfig.PushFilter);
            colBConfig.PushFilter.Should().Be(colConfig.PushFilter);

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
            colAConfig.ConflictResolver.Should().Be(null);
            colBConfig.ConflictResolver.Should().Be(colConfig.ConflictResolver);
            colAConfig.PullFilter.Should().Be(null);
            colBConfig.PullFilter.Should().Be(colConfig.PullFilter);
            colAConfig.PushFilter.Should().Be(null);
            colBConfig.PushFilter.Should().Be(colConfig.PushFilter);
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
            config.Collections.Contains(colA).Should().BeTrue("Because collection colA just added via AddCollection method");
            config.Collections.Contains(colB).Should().BeTrue("Because collection colB just added via AddCollection method");

            var colAConfig = config.GetCollectionConfig(colA);
            var colBConfig = config.GetCollectionConfig(colB);
            colAConfig.ConflictResolver.Should().Be(colConfig.ConflictResolver);
            colBConfig.ConflictResolver.Should().Be(colConfig.ConflictResolver);
            colAConfig.PullFilter.Should().Be(colConfig.PullFilter);
            colBConfig.PullFilter.Should().Be(colConfig.PullFilter);
            colAConfig.PushFilter.Should().Be(colConfig.PushFilter);
            colBConfig.PushFilter.Should().Be(colConfig.PushFilter);

            config.RemoveCollection(colB);
            config.Collections.Contains(colA).Should().BeTrue("Because collection colA should still be there");
            config.Collections.Contains(colB).Should().BeFalse("Because collection colB just removed via RemoveCollection method");

            config.GetCollectionConfig(colA).Should().NotBeNull("Because collection colA should still be there, thus it's config should also exist as well.");
            config.GetCollectionConfig(colB).Should().BeNull("Because collection colB just removed via RemoveCollection method, thus it's config should also be null.");
        }

        [Fact]
        public void TestAddCollectionsFromDifferentDatabaseInstances()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = OtherDb.CreateCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(colA);
            config.Collections.Contains(colA).Should().BeTrue();
            config.Invoking(c => c.AddCollection(colB)).Should().Throw<CouchbaseLiteException>().Where(
                        e => e.Error == CouchbaseLiteError.InvalidParameter &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact]
        public void TestAddDeletedCollections()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = OtherDb.CreateCollection("colB", "scopeA");
            OtherDb.DeleteCollection("colB", "scopeA");
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(colA);
            config.Collections.Contains(colA).Should().BeTrue();
            config.Invoking(c=>c.AddCollection(colB)).Should().Throw<CouchbaseLiteException>().Where(
                        e => e.Error == CouchbaseLiteError.InvalidParameter &&
                             e.Domain == CouchbaseLiteErrorType.CouchbaseLite);
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
                colAInOtherDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInOtherDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInOtherDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInOtherDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInOtherDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInOtherDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInOtherDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
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
                colAInDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
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
                colAInDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
            }

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInOtherDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInOtherDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInOtherDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInOtherDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInOtherDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInOtherDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInOtherDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
            }

            //Test Update doc in replication
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                using (var doc = colAInDb.GetDocument("doc4"))
                using (var mdoc = doc.ToMutable()) {
                    doc.GetString("str4").Should().Be("string4");
                    colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                    mdoc.SetString("str4", "string4 update");
                    colAInDb.Save(mdoc);
                }
            }

            RunReplication(config, 0, 0);

            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4 update");
            }

            //Test Delete doc in replication
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                using (var doc = colAInDb.GetDocument("doc4")) {
                    doc.GetString("str4").Should().Be("string4 update");
                    colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4 update");
                    colAInDb.Delete(doc);
                }
            }

            RunReplication(config, 0, 0);

            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA")) {
                colAInOtherDb.GetDocument("doc4").Should().BeNull();
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
                colAInOtherDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInOtherDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInOtherDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInOtherDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInOtherDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInOtherDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInOtherDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
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
                colAInDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
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
                colAInDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
            }

            // Check docs in OtherDb - make sure docs are pushed to the OtherDb from the Db
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInOtherDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInOtherDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInOtherDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInOtherDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInOtherDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInOtherDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInOtherDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
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

                // Check docs in Db
                colAInDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                var doc4 = colAInDb.GetDocument("doc4");
                doc4.GetString("str4").Should().Be("string4");
                var doc5 = colAInDb.GetDocument("doc5");
                doc5.GetString("str5").Should().Be("string5");
                colBInDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInDb.GetDocument("doc7").GetString("str7").Should().Be("string7");

                // Purge all docs in colA in Db
                colAInDb.Purge(doc4);
                colAInDb.Purge(doc5);

                // docs in colA in Db are now purged
                colAInDb.GetDocument("doc4").Should().BeNull();
                colAInDb.GetDocument("doc5").Should().BeNull();
            }

            RunReplication(config, 0, 0);

            using (var colAInDb = Db.GetCollection("colA", "scopeA")) {
                // docs in colA in Db are still gone
                colAInDb.GetDocument("doc4").Should().BeNull();
                colAInDb.GetDocument("doc5").Should().BeNull();
            }

            RunReplication(config, 0, 0, true);

            using (var colAInDb = Db.GetCollection("colA", "scopeA")) {
                // After reset in replication, colA in Db are pulled from otherDb again
                colAInDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
            }
        }

        /* Create collection "colA"  in the scope "scopeA" in database A.
                Create collection "colB"  in the scope "scopeA" in database B.
                Create a ReplicatorConfiguration using a database endpoint to database B.
                Add colA to the config.
                Create a replicator with config.
                Start the replicator and wait until the replicator stops.
                Check if there is an error with invalidParameter code occurred (Note : error code might be changed) */
        [Fact]
        public void TestMismatchedCollectionReplication()
        {
            var config = CreateConfig(ReplicatorType.Pull);
            using (var colA = Db.CreateCollection("colA", "scopeA"))
            using (var colB = OtherDb.CreateCollection("colB", "scopeA"))
            {
                using (var doc = new MutableDocument("doc"))
                using (var doc1 = new MutableDocument("doc1"))
                {
                    doc.SetString("str", "string");
                    doc1.SetString("str1", "string1");
                    colA.Save(doc);
                    colA.Save(doc1);
                }

                using (var doc = new MutableDocument("doc2"))
                using (var doc1 = new MutableDocument("doc3"))
                {
                    doc.SetString("str2", "string2");
                    doc1.SetString("str3", "string3");
                    colB.Save(doc);
                    colB.Save(doc1);
                }

                config.AddCollection(colA);

                //Action badAct = () => RunReplication(config, 0, 0);
                //badAct.Should().Throw<CouchbaseLiteException>().WithMessage(CouchbaseLiteErrorMessage.DBClosed);
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
                using (var docInColAInDb = colAInDb.GetDocument("doc"))
                using (var doc2InColBDb = colBInDb.GetDocument("doc2"))
                using (var doc4InColAInOtherDb = colAInOtherDb.GetDocument("doc4"))
                using (var doc6InColBInOtherDb = colBInOtherDb.GetDocument("doc6")) {
                    colAInDb.Delete(docInColAInDb);
                    colBInDb.Delete(doc2InColBDb);
                    colAInOtherDb.Delete(doc4InColAInOtherDb);
                    colBInOtherDb.Delete(doc6InColBInOtherDb);
                }
            }

            var pullWait = new WaitAssert();
            var pushWait = new WaitAssert();
            RunReplication(config, 0, 0, onReplicatorReady: (r) =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
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
                using (var doc = colAInDb.GetDocument("doc1"))
                using (var mdoc = doc.ToMutable()) {
                    mdoc.SetString("str1", "string1 update");
                    colAInDb.Save(mdoc);
                }

                using (var doc = colAInOtherDb.GetDocument("doc1"))
                using (var mdoc = doc.ToMutable()) {
                    mdoc.SetString("str1", "string1 update1");
                    colAInOtherDb.Save(mdoc);
                }

                using (var doc = colAInOtherDb.GetDocument("doc1"))
                using (var mdoc = doc.ToMutable()) {
                    mdoc.SetString("str1", "string1 update again");
                    colAInOtherDb.Save(mdoc);
                }

                RunReplication(config, 0, 0);

                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1 update again");
                colAInOtherDb.GetDocument("doc1").GetString("str1").Should().Be("string1 update again"); 
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
                var cllConfigADb = config.GetCollectionConfig(colAInDb);
                cllConfigADb.ConflictResolver = new TestConflictResolver((conflict) => {
                    return conflict.LocalDocument;
                });

                var cllConfigBDb = config.GetCollectionConfig(colBInDb);
                cllConfigBDb.ConflictResolver = new TestConflictResolver((conflict) => {
                    return conflict.RemoteDocument;
                });

                using (var doc = colAInDb.GetDocument("doc1"))
                using (var mdoc = doc.ToMutable()) {
                    mdoc.SetString("str1", "string1 update");
                    colAInDb.Save(mdoc);
                }

                using (var doc = colBInDb.GetDocument("doc3"))
                using (var mdoc = doc.ToMutable()) {
                    mdoc.SetString("str3", "string3 update");
                    colBInDb.Save(mdoc);
                }

                using (var doc = colAInOtherDb.GetDocument("doc1"))
                using (var mdoc = doc.ToMutable()) {
                    mdoc.SetString("str1", "string1 update1");
                    colAInOtherDb.Save(mdoc);
                }

                using (var doc = colBInOtherDb.GetDocument("doc3"))
                using (var mdoc = doc.ToMutable()) {
                    mdoc.SetString("str3", "string3 update1");
                    colBInOtherDb.Save(mdoc);
                }

                RunReplication(config, 0, 0);

                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1 update");
                colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3 update1");
            }
        }

        [Fact]
        public void TestCollectionPushFilter()
        {
            var config = CreateConfig(ReplicatorType.Push);
            LoadCollectionsDocs(config);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                config.GetCollectionConfig(colAInDb).PushFilter = _replicator__filterAllowsOddDocIdsCallback;
                config.GetCollectionConfig(colBInDb).PushFilter = _replicator__filterAllowsOddDocIdsCallback;

                RunReplication(config, 0, 0);

                // Check docs in OtherDb - make sure docs with odd doc ids are pushed to the OtherDb from the Db
                using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
                using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                    colAInOtherDb.GetDocument("doc").Should().BeNull();
                    colAInOtherDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                    colBInOtherDb.GetDocument("doc2").Should().BeNull();
                    colBInOtherDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                    colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                    colAInOtherDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                    colBInOtherDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                    colBInOtherDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
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
                config.GetCollectionConfig(colAInDb).PullFilter = _replicator__filterAllowsOddDocIdsCallback;
                config.GetCollectionConfig(colBInDb).PullFilter = _replicator__filterAllowsOddDocIdsCallback;

                RunReplication(config, 0, 0);

                // Check docs in Db - make sure all docs with odd doc ids are pulled from the OtherDb to the Db
                colAInDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInDb.GetDocument("doc4").Should().BeNull();
                colAInDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInDb.GetDocument("doc6").Should().BeNull();
                colBInDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
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
                config.GetCollectionConfig(colAInDb).DocumentIDs = allowOddIds;
                config.GetCollectionConfig(colBInDb).DocumentIDs = allowOddIds;
            }

            RunReplication(config, 0, 0);

            // Check docs in OtherDb - make sure docs with odd doc ids are pushed to the OtherDb from the Db
            using (var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA"))
            using (var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA")) {
                colAInOtherDb.GetDocument("doc").Should().BeNull();
                colAInOtherDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInOtherDb.GetDocument("doc2").Should().BeNull();
                colBInOtherDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
                colAInOtherDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInOtherDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
                colBInOtherDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
            }
        }

        [Fact]
        public void TestCollectionDocumentIDsPullFilter()
        {
            var config = CreateConfig(ReplicatorType.Pull);
            LoadCollectionsDocs(config);
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                config.GetCollectionConfig(colAInDb).DocumentIDs = allowOddIds;
                config.GetCollectionConfig(colBInDb).DocumentIDs = allowOddIds;
            }

            RunReplication(config, 0, 0);

            // Check docs in Db - make sure all docs with odd doc ids are pulled from the OtherDb to the Db
            using (var colAInDb = Db.GetCollection("colA", "scopeA"))
            using (var colBInDb = Db.GetCollection("colB", "scopeA")) {
                colAInDb.GetDocument("doc").GetString("str").Should().Be("string");
                colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
                colBInDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
                colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
                colAInDb.GetDocument("doc4").Should().BeNull();
                colAInDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
                colBInDb.GetDocument("doc6").Should().BeNull();
                colBInDb.GetDocument("doc7").GetString("str7").Should().Be("string7");
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
                    using (var doc = colAInDb.GetDocument(colADocId))
                    using (var mdoc = doc.ToMutable()) {
                        mdoc.SetString("str", "string update");
                        colAInDb.Save(mdoc);
                    }

                    using (var doc = colBInDb.GetDocument(colBDocId))
                    using (var mdoc = doc.ToMutable()) {
                        mdoc.SetString("str2", "string2 update");
                        colBInDb.Save(mdoc);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.DELETE) {
                    using (var doc = colAInDb.GetDocument(colADocId)) {
                        colAInDb.Delete(doc);
                    }

                    using (var doc = colBInDb.GetDocument(colBDocId)) {
                        colBInDb.Delete(doc);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                    using (var doc = colAInDb.GetDocument(colADocId)) {
                        colAInDb.Purge(doc);
                    }

                    using (var doc = colBInDb.GetDocument(colBDocId)) {
                        colBInDb.Purge(doc);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                    config.GetCollectionConfig(colAInDb).PushFilter = (doc, isPush) =>
                    {
                        if (doc.Id.Equals(colADocId))
                            return true;
                        return false;
                    };

                    config.GetCollectionConfig(colBInDb).PushFilter = (doc, isPush) =>
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
                                pendingDocIds = replicator.GetPendingDocumentIDs(colAInDb);
                                pendingDocIds.Count.Should().Be(0);

                                pendingDocIds = replicator.GetPendingDocumentIDs(colBInDb);
                                pendingDocIds.Count.Should().Be(0);
                            }

                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    pendingDocIds = replicator.GetPendingDocumentIDs(colAInDb);
                    var pendingDocIds1 = replicator.GetPendingDocumentIDs(colBInDb);
                    if (selection == PENDING_DOC_ID_SEL.FILTER) {
                        pendingDocIds.Count.Should().Be(1);
                        pendingDocIds.ElementAt(0).Should().Be(colADocId);
                        pendingDocIds1.Count.Should().Be(1);
                        pendingDocIds1.ElementAt(0).Should().Be(colBDocId);
                    } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                        pendingDocIds.Contains("doc").Should().BeFalse();
                        pendingDocIds.Contains("doc1").Should().BeTrue();
                        pendingDocIds1.Contains("doc2").Should().BeFalse();
                        pendingDocIds1.Contains("doc3").Should().BeTrue();
                    } else {
                        pendingDocIds.Contains("doc").Should().BeTrue();
                        pendingDocIds.Contains("doc1").Should().BeTrue();
                        pendingDocIds1.Contains("doc2").Should().BeTrue();
                        pendingDocIds1.Contains("doc3").Should().BeTrue();
                    }

                    replicator.Start();

                    wa.WaitForResult(TimeSpan.FromSeconds(50));

                    Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                        .Times(5)
                        .Delay(TimeSpan.FromMilliseconds(500))
                        .Go().Should().BeTrue();

                    replicator.GetPendingDocumentIDs(colAInDb).Count.Should().Be(0);
                    replicator.GetPendingDocumentIDs(colBInDb).Count.Should().Be(0);
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
                    using (var doc = colAInDb.GetDocument(colADocId))
                    using (var mdoc = doc.ToMutable()) {
                        mdoc.SetString("str", "string update");
                        colAInDb.Save(mdoc);
                    }

                    using (var doc = colBInDb.GetDocument(colBDocId))
                    using (var mdoc = doc.ToMutable()) {
                        mdoc.SetString("str2", "string2 update");
                        colBInDb.Save(mdoc);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.DELETE) {
                    using (var doc = colAInDb.GetDocument(colADocId)) {
                        colAInDb.Delete(doc);
                    }

                    using (var doc = colBInDb.GetDocument(colBDocId)) {
                        colBInDb.Delete(doc);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                    using (var doc = colAInDb.GetDocument(colADocId)) {
                        colAInDb.Purge(doc);
                    }

                    using (var doc = colBInDb.GetDocument(colBDocId)) {
                        colBInDb.Purge(doc);
                    }
                } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                    config.GetCollectionConfig(colAInDb).PushFilter = (doc, isPush) =>
                    {
                        if (doc.Id.Equals(colADocId))
                            return true;
                        return false;
                    };

                    config.GetCollectionConfig(colBInDb).PushFilter = (doc, isPush) =>
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
                            docIdIsPending = replicator.IsDocumentPending(colADocId, colAInDb);
                            docIdIsPending.Should().BeFalse();
                            docIdIsPending = replicator.IsDocumentPending(colBDocId, colBInDb);
                            docIdIsPending.Should().BeFalse();
                        }

                        wa.RunConditionalAssert(() =>
                        {
                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    docIdIsPending = replicator.IsDocumentPending(colADocId, colAInDb);
                    var docIdIsPending1 = replicator.IsDocumentPending(colBDocId, colBInDb);
                    if (selection == PENDING_DOC_ID_SEL.CREATE || selection == PENDING_DOC_ID_SEL.UPDATE
                        || selection == PENDING_DOC_ID_SEL.DELETE) {
                        docIdIsPending.Should().BeTrue();
                        docIdIsPending = replicator.IsDocumentPending("IdNotThere", colAInDb);
                        docIdIsPending.Should().BeFalse();
                        docIdIsPending1.Should().BeTrue();
                        docIdIsPending1 = replicator.IsDocumentPending("IdNotThere", colBInDb);
                        docIdIsPending1.Should().BeFalse();
                    } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                        docIdIsPending.Should().BeTrue();
                        docIdIsPending = replicator.IsDocumentPending("doc1", colAInDb);
                        docIdIsPending.Should().BeFalse();
                        docIdIsPending1.Should().BeTrue();
                        docIdIsPending1 = replicator.IsDocumentPending("doc3", colBInDb);
                        docIdIsPending1.Should().BeFalse();
                    } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                        docIdIsPending.Should().BeFalse();
                        docIdIsPending1.Should().BeFalse();
                    }

                    replicator.Start();

                    wa.WaitForResult(TimeSpan.FromSeconds(50));

                    Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                        .Times(5)
                        .Delay(TimeSpan.FromMilliseconds(500))
                        .Go().Should().BeTrue();

                    replicator.IsDocumentPending(colADocId, colAInDb).Should().BeFalse();
                    replicator.IsDocumentPending(colBDocId, colBInDb).Should().BeFalse();
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
            colAInDb.GetDocument("doc").GetString("str").Should().Be("string");
            colAInDb.GetDocument("doc1").GetString("str1").Should().Be("string1");
            colBInDb.GetDocument("doc2").GetString("str2").Should().Be("string2");
            colBInDb.GetDocument("doc3").GetString("str3").Should().Be("string3");
            colAInDb.GetDocument("doc4").Should().BeNull("Because doc4 is not created in colA in Db");
            colAInDb.GetDocument("doc5").Should().BeNull("Because doc5 is not created in colA in Db");
            colBInDb.GetDocument("doc6").Should().BeNull("Because doc6 is not created in colB in Db");
            colBInDb.GetDocument("doc7").Should().BeNull("Because doc7 is not created in colB in Db");

            // Check docs in OtherDb - before replication
            var colAInOtherDb = OtherDb.GetCollection("colA", "scopeA");
            var colBInOtherDb = OtherDb.GetCollection("colB", "scopeA");
            colAInOtherDb.GetDocument("doc").Should().BeNull("Because doc is not created in colA in OtherDb");
            colAInOtherDb.GetDocument("doc1").Should().BeNull("Because doc1 is not created in colA in OtherDb");
            colBInOtherDb.GetDocument("doc2").Should().BeNull("Because doc2 is not created in colA in OtherDb");
            colBInOtherDb.GetDocument("doc3").Should().BeNull("Because doc3 is not created in colA in OtherDb");
            colAInOtherDb.GetDocument("doc4").GetString("str4").Should().Be("string4");
            colAInOtherDb.GetDocument("doc5").GetString("str5").Should().Be("string5");
            colBInOtherDb.GetDocument("doc6").GetString("str6").Should().Be("string6");
            colBInOtherDb.GetDocument("doc7").GetString("str7").Should().Be("string7");

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
            document.RevisionID.Should().NotBeNull();
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

