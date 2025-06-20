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
using System.Linq;
using LiteCore.Interop;
using System.Runtime.InteropServices;
using LiteCore;
using System.Collections.Generic;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite;
using Shouldly;

namespace Test
{
    // https://github.com/couchbaselabs/couchbase-lite-api/blob/013c871dbaaa8d023a70ab71848c157de02bb57a/spec/tests/T0004-Unnest-Array-Index.md
    // T0004 Unnest and Array Index Tests v1.0.2

    public sealed class ArrayIndexTest : TestCase
    {
        public ArrayIndexTest(ITestOutputHelper output) : base(output)
        {
            
        }

        private void TestCreateArrayIndexWith(string indexName, string path, params string[] expressions)
        {
            var indexNameB = $"{indexName}b";
            using var profiles = Db.CreateCollection("profiles");
            LoadJSONResource("profiles_100", coll: profiles);
            var indexConfig = new ArrayIndexConfiguration(path, expressions);
            var indexConfigb = new ArrayIndexConfiguration(path, expressions.Any() ? new List<string>(expressions) : null);
            profiles.CreateIndex(indexName, indexConfig);
            profiles.CreateIndex(indexNameB, indexConfigb);
            profiles.GetIndexes().Any(x => x == indexName).ShouldBeTrue("because the index was just created");
            profiles.GetIndexes().Any(x => x == indexNameB).ShouldBeTrue("because the index was just created");
            C4Error err;
            IDictionary<string, object>? indexInfo;
            IDictionary<string, object>? indexInfoB;
            unsafe {
                var allIndexInfo = TestNative.c4coll_getIndexesInfo(profiles, &err);
                allIndexInfo.ShouldNotBeNull("because an index exists");
                indexInfo = allIndexInfo!.FirstOrDefault(x => (x["name"] as string) == indexName);
                indexInfoB = allIndexInfo!.FirstOrDefault(x => (x["name"] as string) == indexNameB);
            }
            indexInfo.ShouldNotBeNull("because otherwise the contacts index does not exist");
            ((long)indexInfo!["type"]).ShouldBe((long)C4IndexType.ArrayIndex, "because otherwise the wrong type of index was created");
            (indexInfo["lang"] as string).ShouldBe("n1ql", "because otherwise the wrong query language was used");
            (indexInfo["expr"] as string).ShouldBe(expressions != null ? String.Join(",", expressions) : "", "because otherwise the wrong expression was used");
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
        [Fact]
        public void TestArrayIndexConfigInvalidExpressions()
        {
            using var profiles = Db.CreateCollection("profiles");
            Should.Throw<ArgumentException>(() => new ArrayIndexConfiguration("contacts", new List<string> { "" }));
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
            using var rawData = TestNativeRaw.c4coll_getIndexesInfo(collection.c4coll.RawCollection, error);
            if (rawData.size == 0) {
                return null;
            }

            var flValue = NativeRaw.FLValue_FromData((FLSlice)rawData, FLTrust.Trusted);
            var halfwayConverted = FLValueConverter.ToCouchbaseObject(flValue, null, true) as IList<object>;
            return halfwayConverted?.Cast<IDictionary<string, object>>()?.ToList();
        }
    }

    internal unsafe static partial class TestNativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4coll_getIndexesInfo(C4Collection* collection, C4Error* error);
    }
}