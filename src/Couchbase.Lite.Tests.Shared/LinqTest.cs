//
//  LinqTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
#if CBL_LINQ
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
using System.Linq;
using Couchbase.Lite.Query;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Linq;
using Newtonsoft.Json;
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
    public sealed class LinqTest : TestCase
    {
#if !WINDOWS_UWP
        public LinqTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        // {"name":{"first":"Lue","last":"Laserna"},"gender":"female","birthday":"1983-09-18",
        // "contact":{"address":{"street":"19 Deer Loop","zip":"90732","city":"San Pedro","state":"CA"},
        // "email":["lue.laserna@nosql-matters.org","laserna@nosql-matters.org"],"region":"310",
        // "phone":["310-8268551","310-7618427"]},"likes":["chatting"],"memberSince":"2011-05-05"}
        private sealed class NamesModel : IDocumentModel
        {
            [JsonProperty("name")]
            public Name Name { get; set; }

            [JsonProperty("gender")]
            public string Gender { get; set; }

            [JsonProperty("birthday")]
            public string Birthday { get; set; }

            [JsonProperty("likes")]
            public List<string> Likes { get; set; }

            [JsonProperty("memberSince")]
            public string MemberSince { get; set; }

            public Document Document { get; set; }
        }

        private sealed class Contact
        {
            [JsonProperty("address")]
            public Address Address { get; set; }

            [JsonProperty("email")]
            public List<string> Email { get; set; }

            [JsonProperty("region")]
            public string Region { get; set; }

            [JsonProperty("phone")]
            public List<string> Phone { get; set; }
        }

        private sealed class Address
        {
            [JsonProperty("street")]
            public string Street { get; set; }

            [JsonProperty("city")]
            public string City { get; set; }

            [JsonProperty("state")]
            public string State { get; set; }

            [JsonProperty("zip")]
            public string Zip { get; set; }
        }

        private sealed class Name
        {
            [JsonProperty("first")]
            public string First { get; set; }

            [JsonProperty("last")]
            public string Last { get; set; }
        }

        private sealed class SimpleModel : IDocumentModel
        {
            public string Name { get; set; }

            public string Address { get; set; }

            public int Age { get; set; }

            public object Work { get; set; }

            public Document Document { get; set; }
        }

        [Fact]
        public void TestNoWhereQuery()
        {
            LoadJSONResource("names_100");

            var q = from x in new DatabaseQueryable<NamesModel>(Db)
                select new object[] { x.Id(), x.Sequence() };

            var index = 1;
            foreach (var result in q) {
                var expectedID = $"doc-{index:D3}";

                result[0].Should().Be(expectedID);
                result[1].Should().Be((long) index);
                index++;
            }

            index.Should().Be(101);
        }

        [Fact]
        public void TestWhereNull()
        {
            SimpleModel doc1 = new SimpleModel(), doc2 = new SimpleModel();
            doc1.Document = new MutableDocument("doc1");
            doc1.Name = "Scott";
            Db.Save(doc1);
            
            doc2.Document = new MutableDocument("doc2");
            doc2.Name = "Tiger";
            doc2.Address = "123 1st ave.";
            doc2.Age = 20;
            Db.Save(doc2);

            var q = from x in new DatabaseQueryable<SimpleModel>(Db)
                where x.Name != null
                select x.Id();

            var expectedDocs = new[] { "doc1", "doc2" };
            VerifyQuery(q, (n, r) =>
            {
                if (n <= expectedDocs.Length) {
                    var doc = expectedDocs[n - 1];
                    r.Should().Be(doc);
                }
            });

            q = from x in new DatabaseQueryable<SimpleModel>(Db)
                where x.Name == null
                select x.Id();

            expectedDocs = new string[0];
            VerifyQuery(q, (n, r) =>
            {
                if (n <= expectedDocs.Length) {
                    var doc = expectedDocs[n - 1];
                    r.Should().Be(doc);
                }
            });
        }

        private int VerifyQuery<T>(IQueryable<T> q, Action<int, T> callback)
        {
            var i = 0;
            foreach (var result in q) {
                callback(++i, result);
            }

            return i;
        }
    }
}
#endif