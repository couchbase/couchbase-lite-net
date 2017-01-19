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
using System.Linq;

using Couchbase.Lite;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Test
{
    [JsonObject(MemberSerialization.OptOut)]
    public class TestModel
    {
        public int IntValue { get; set; }

        public string StringValue { get; set; }

        public TestModelReference Child { get; set; } = new TestModelReference();
    }

    public class TestModelReference
    {
        public DateTimeOffset CreatedAt { get; set; }

        public TestModelReference()
        {
            CreatedAt = DateTimeOffset.Now;
        }
    }

    public class ModelTest : TestCase
    {
        [Fact]
        public void TestModel()
        {
            var model = Db.GetDocument<TestModel>();
            var item = model.Item;
            item.IntValue = 42;
            item.StringValue = "Jim";
            var date = item.Child.CreatedAt;
            model.Save().Should().BeTrue("because otherwise the save failed");

            var model2 = Db.GetDocument<TestModel>();
            item = model2.Item;
            item.IntValue = 43;
            item.StringValue = "Jim";
            model2.Save();

            using(var db1 = new Database(Db)) {
                model = db1.GetDocument<TestModel>(model.Id);
                model.Item.IntValue.Should().Be(42, "because that was the saved int value");
                model.Item.StringValue.Should().Be("Jim", "because that was the saved string value");
                model.Item.Child.CreatedAt.Should().Be(date, "because that was the saved date value");
            }

            var all = from x in new DatabaseQueryable<TestModel>(Db) where x.IntValue == 42 && x.StringValue == "Jim" select x;
            all.ToArray();
        }
    }
}
