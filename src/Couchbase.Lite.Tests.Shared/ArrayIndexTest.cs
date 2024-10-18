//
//  ArrayIndexTest.cs
//
//  Copyright (c) 2024 Couchbase, Inc All rights reserved.
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
using Xunit.Abstractions;
using Xunit;
using Couchbase.Lite.Query;
using FluentAssertions;
using System.Linq;
using LiteCore.Interop;
using System.Runtime.InteropServices;
using LiteCore;
using System.Collections.Generic;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite;
using System.IO;
using System.Linq.Expressions;

namespace Test
{
    [ImplementsTestSpec("T0004-Unnest-Array-Index", "1.0.2")]
    public sealed class ArrayIndexTest : TestCase
    {
        public ArrayIndexTest(ITestOutputHelper output) : base(output)
        {
            
        }

        public static IEnumerable<object[]> BadExpressionValues = new List<object[]>()
        {
            new object[] { new List<string>() },
            new object[] { new List<string> { "" } }
        };

        private void TestCreateArrayIndexWith(string indexName, string path, params string[] expressions)
        {
            var indexNameB = $"{indexName}b";
            using var profiles = Db.CreateCollection("profiles");
            LoadJSONResource("profiles_100", coll: profiles);
            var indexConfig = new ArrayIndexConfiguration(path, expressions);
            var indexConfigb = new ArrayIndexConfiguration(path, expressions.Any() ? new List<string>(expressions) : null);
            profiles.CreateIndex(indexName, indexConfig);
            profiles.CreateIndex(indexNameB, indexConfigb);
            profiles.GetIndexes().Any(x => x == indexName).Should().BeTrue("because the index was just created");
            profiles.GetIndexes().Any(x => x == indexNameB).Should().BeTrue("because the index was just created");
            C4Error err;
            IDictionary<string, object>? indexInfo;
            IDictionary<string, object>? indexInfoB;
            unsafe {
                var allIndexInfo = TestNative.c4coll_getIndexesInfo(profiles, &err);
                allIndexInfo.Should().NotBeNull("because an index exists");
                indexInfo = allIndexInfo!.FirstOrDefault(x => (x["name"] as string) == indexName);
                indexInfoB = allIndexInfo!.FirstOrDefault(x => (x["name"] as string) == indexNameB);
            }
            indexInfo.Should().NotBeNull("because otherwise the contacts index does not exist");
            ((long)indexInfo!["type"]).Should().Be((long)C4IndexType.ArrayIndex, "because otherwise the wrong type of index was created");
            (indexInfo["lang"] as string).Should().Be("n1ql", "because otherwise the wrong query language was used");
            (indexInfo["expr"] as string).Should().Be(expressions != null ? String.Join(",", expressions) : "", "because otherwise the wrong expression was used");

            unsafe {
                var gotIndex = Native.c4coll_getIndex(profiles.c4coll, indexName, &err);
                var gotIndexB = Native.c4coll_getIndex(profiles.c4coll, indexNameB, &err);

                try {
                    ((IntPtr)gotIndex).Should().NotBe(IntPtr.Zero, "because the index should exist via c4coll_getIndex");
                    ((IntPtr)gotIndexB).Should().NotBe(IntPtr.Zero, "because the index should exist via c4coll_getIndex");
                    var gotOpts = new C4IndexOptions();
                    TestNative.c4index_getOptions(gotIndex, &gotOpts).Should().BeTrue("because otherwise c4index_getOptions failed");
                    gotOpts.unnestPath.Should().Be(path, "because otherwise the unnestPath didn't match");
                    TestNative.c4index_getOptions(gotIndexB, &gotOpts).Should().BeTrue("because otherwise c4index_getOptions failed");
                    gotOpts.unnestPath.Should().Be(path, "because otherwise the unnestPath didn't match");
                } finally {
                    Native.c4index_release(gotIndex);
                    Native.c4index_release(gotIndexB);
                }
            }
            
        }

        /// <summary>
        /// Description
        /// Test that creating an ArrayIndexConfiguration with invalid expressions which are
        /// an empty expressions or contain null.
        /// 
        /// Steps
        /// 1. Create a ArrayIndexConfiguration object.
        ///     - path: "contacts"
        ///     - expressions: []
        /// 2. Check that an invalid arument exception is thrown.
        /// 3. Create a ArrayIndexConfiguration object.
        ///     - path: "contacts"
        ///     - expressions: [""]
        /// 4. Check that an invalid arument exception is thrown.
        /// </summary>
        /// <param name="badExpressions"></param>
        [Theory]
        [MemberData(nameof(BadExpressionValues))]
        public void TestArrayIndexConfigInvalidExpressions(List<string> badExpressions)
        {
            using var profiles = Db.CreateCollection("profiles");
            FluentActions.Invoking(() => new ArrayIndexConfiguration("contacts", badExpressions)).Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// Test that creating an array index with only path works as expected.
        /// 
        /// Steps
        /// 1. Load profiles.json into the collection named "_default.profiles".
        /// 2. Create a ArrayIndexConfiguration object.
        ///     - path: "contacts"
        ///     - expressions:  null
        /// 3. Create an array index named "contacts" in the profiles collection.
        /// 4. Get index names from the profiles collection and check that the index named "contacts" exists.
        /// 5. Get info of the index named "contacts" using an internal API and check that the index has
        ///     path and expressions as configured.
        /// </summary>
        [Fact]
        public void TestCreateArrayIndexWithPath()
            => TestCreateArrayIndexWith("contacts", "contacts");

        /// <summary>
        /// Test that creating an array index with path and expressions works as expected.
        /// 
        /// Steps
        /// 1. Load profiles.json into the collection named "_default.profiles".
        /// 2. Create a ArrayIndexConfiguration object.
        ///     - path: "contacts"
        ///     - expressions:  ["address.city", "address.state"]
        /// 3. Create an array index named "contacts" in the profiles collection.
        /// 4. Get index names from the profiles collection and check that the index named "contacts" exists.
        /// 5. Get info of the index named "contacts" using an internal API and check that the index has
        ///     path and expressions as configured.
        /// </summary>
        [Fact]
        public void TestCreateArrayIndexWithPathAndExpressions()
            => TestCreateArrayIndexWith("contacts", "contacts", "address.city", "address.state");
    }

    internal unsafe static partial class TestNative
    {
        public static IList<IDictionary<string, object>>? c4coll_getIndexesInfo(Collection collection, C4Error* error)
        {
            using var rawData = TestNativeRaw.c4coll_getIndexesInfo(collection.c4coll, error);
            if (rawData.size == 0) {
                return null;
            }

            var flValue = NativeRaw.FLValue_FromData((FLSlice)rawData, FLTrust.Trusted);
            var halfwayConverted = FLValueConverter.ToCouchbaseObject(flValue, null, true) as IList<object>;
            return halfwayConverted?.Cast<IDictionary<string, object>>()?.ToList();
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4index_getOptions(C4Index* index, C4IndexOptions* outOpts);
    }

    internal unsafe static partial class TestNativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4coll_getIndexesInfo(C4Collection* collection, C4Error* error);
        
    }
}