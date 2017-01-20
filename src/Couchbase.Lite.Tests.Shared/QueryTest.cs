//
//  QueryTest.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Couchbase.Lite;
using FluentAssertions;
using LiteCore.Interop;
using Newtonsoft.Json;
using Xunit;

namespace Test
{
    public class NamesModel : IDocumentModel
    {
        public Name name { get; set; }

        [JsonProperty(PropertyName = "gender")]
        public string Gender { get; set; }

        [JsonProperty(PropertyName = "birthday")]
        public string Birthday { get; set; }

        [JsonProperty(PropertyName = "contact")]
        public ContactInfo Contact { get; set; }

        [JsonProperty(PropertyName = "likes")]
        public IList<string> Likes { get; set; }

        [JsonProperty(PropertyName = "memberSince")]
        public string MemberSince { get; set; }

        public DocumentMetadata Metadata { get; set; }

        public NamesModel()
        {

        }
    }

    public class Name
    {
        public string first { get; set; }

        [JsonProperty(PropertyName = "last")]
        public string Last { get; set; } 
    }

    public class ContactInfo
    {
        [JsonProperty(PropertyName = "address")]
        public Address Address { get; set; }

        [JsonProperty(PropertyName = "email")]
        public IList<string> Email { get; set; }

        [JsonProperty(PropertyName = "region")]
        public string Region;

        [JsonProperty(PropertyName = "phone")]
        public IList<string> Phone { get; set; }

        public ContactInfo()
        {
            Email = new List<string>();
            Phone = new List<string>();
        }
    }

    public class Address
    {
        [JsonProperty(PropertyName = "street")]
        public string Street { get; set; }

        [JsonProperty(PropertyName = "zip")]
        public string Zip { get; set; }

        [JsonProperty(PropertyName = "city")]
        public string City { get; set; }

        [JsonProperty(PropertyName = "state")]
        public string State { get; set; }
    }

    public class QueryTest : TestCase
    {

        //{"name":{"first":"Lue","last":"Laserna"},"gender":"female","birthday":"1983-09-18","contact":{"address":{"street":"19 Deer Loop","zip":"90732","city":"San Pedro","state":"CA"},"email":["lue.laserna@nosql-matters.org","laserna@nosql-matters.org"],"region":"310","phone":["310-8268551","310-7618427"]},"likes":["chatting"],"memberSince":"2011-05-05"}
        [Fact]
        public void TestQuery()
        {
            var content = File.ReadAllLines("../Couchbase.Lite.Tests.Shared/data/names_100.json");
            int n = 0;
            var ok = Db.InBatch(() =>
            {
                foreach(var line in content) {
                    var doc = Db.GetDocument($"person-{++n:D3}");
                    doc.Properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(line);
                    doc.Save().Should().BeTrue("beacuse otherwise the save failed");
                }

                return true;
            });
            ok.Should().BeTrue("because otherwise the batch failed");

            var q = QueryableFactory.MakeQueryable<NamesModel>(Db);
            var e = from model in q select model;

            n = 0;
            foreach(var result in e) {
                Debug.WriteLine($"Row: docID='{result.Metadata.Id}', sequence={result.Metadata.Sequence}");
                var expectedID = $"person-{++n:D3}";
                result.Metadata.Id.Should().Be(expectedID, "because otherwise the results are out of order");
                result.Metadata.Sequence.Should().Be((ulong)n, "because otherwise the results are out of order");
            }

            n.Should().Be(100, "because that is how many docuemnts are in the JSON file");
            for(int pass = 0; pass < 2; pass++) {
                n = 0;
                e = from model in q where model.name.first == "Claude" select model;
                foreach(var result in e) {
                    ++n;
                    Debug.WriteLine($"Row: docID='{result.Metadata.Id}', sequence={result.Metadata.Sequence}");
                    result.Metadata.Id.Should().Be("person-009", "because that is the only Claude in the document");
                    result.Metadata.Sequence.Should().Be(9UL, "beacuse that is the correct sequence number");
                }

                n.Should().Be(1, "because there is only one Claude in the document");

                if(pass == 0) {
                    Db.CreateIndex("name.first");
                }
            }

            Db.DeleteIndex("name.first", C4IndexType.ValueIndex);
        }
    }
}
