//
//  ModelTest.cs
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
using System.Linq;

using Couchbase.Lite;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Test
{
    [JsonObject(MemberSerialization.OptOut)]
    public class TestModel : IDocumentModel
    {
        public int IntValue { get; set; }

        public string StringValue { get; set; }

        public TestModelReference Child { get; set; } = new TestModelReference();

        public DocumentMetadata Metadata
        {
            get; set;
        }
    }

    public class TestModelReference
    {
        public IList<int> IntValues { get; set; }

        public TestModelReference()
        {
            IntValues = new List<int>();
        }
    }

    public class ModelTest : TestCase
    {
        //[Fact]
        //public void TestModel()
        //{
        //    var model = Db.GetDocument<TestModel>();
        //    var item = model.Item;
        //    item.IntValue = 42;
        //    item.StringValue = "Jim";
        //    item.Child.IntValues = new[] { 1, 2, 3, 4 };
        //    model.Save().Should().BeTrue("because otherwise the save failed");

        //    var model2 = Db.GetDocument<TestModel>();
        //    item = model2.Item;
        //    item.IntValue = 43;
        //    item.StringValue = "Jim";
        //    item.Child.IntValues = new[] { 1, 2, 3, 4, 5 };
        //    model2.Save();

        //    var all = from x in QueryableFactory.MakeQueryable<TestModel>(Db) where x.Child.IntValues.Sum() > 10 select x;
        //    all.ToArray();
        //}
    }
}
