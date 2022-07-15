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
#endif

        private bool _replicator__filterCallbackTrue(Document document, DocumentFlags flags)
        {
            return true;
        }

        private bool _replicator__filterCallbackFalse(Document document, DocumentFlags flags)
        {
            return false;
        }
    }
}

