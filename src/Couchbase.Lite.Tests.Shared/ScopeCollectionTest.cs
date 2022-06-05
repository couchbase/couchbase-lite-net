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
            var collections = Db.GetCollections() as List<ICollection>;
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
            var scopes = Db.GetScopes() as List<Scope>;
            scopes.Contains(defaultScope).Should().BeTrue("the default scope is included in the scope list when calling Database.GetScopes()");
        }

        [Fact]
        public void TestDeleteDefaultCollection()
        {
            Db.DeleteCollection(Database._defaultCollectionName);
            var defaultColl = Db.GetDefaultCollection();
            defaultColl.Should().BeNull("default collection is deleted");
            Db.CreateCollection(Database._defaultCollectionName);
            defaultColl = Db.GetDefaultCollection();
            defaultColl.Should().BeNull("default collection cannot be recreated, so the value is still null");
        }

        [Fact]
        public void TestGetDefaultScopeAfterDeleteDefaultCollection()
        {
            Db.DeleteCollection(Database._defaultCollectionName);
            var defaultScope = Db.GetDefaultScope();
            defaultScope.Should().NotBeNull("scope still exists after the default collection is deleted");
            var scopes = Db.GetScopes() as List<Scope>;
            scopes.Contains(defaultScope).Should().BeTrue("the default scope is included in the scope list when calling Database.GetScopes()");
            defaultScope.Name.Should().Be(Database._defaultScopeName, $"default scope name is {Database._defaultScopeName}");
        }


    }
}
