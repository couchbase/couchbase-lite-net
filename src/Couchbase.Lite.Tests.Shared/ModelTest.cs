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
using Couchbase.Lite.Query;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    [JsonObject(MemberSerialization.OptOut)]
    public class TestModel : IDocumentModel
    {
        public int IntValue { get; set; }

        public string StringValue { get; set; }

        public TestModelReference Child { get; set; } = new TestModelReference();

        public IDocument Document
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
        public ModelTest(ITestOutputHelper output) : base(output)
        {

        }

        //[Fact]
        //public void TestModel()
        //{
        //    Db.DoSync(() =>
        //    {
        //        var model = Db.CreateDocument().AsModel<TestModel>();
        //        model.IntValue = 42;
        //        model.StringValue = "Jim";
        //        model.Child.IntValues = new[] {1, 2, 3, 4};
        //        model.Save();

        //        var model2 = Db.CreateDocument().AsModel<TestModel>();
        //        model2.IntValue = 43;
        //        model2.StringValue = "Jim";
        //        model2.Child.IntValues = new[] {1, 2, 3, 4, 5};
        //        model2.Save();

        //        var all = from x in DataSourceFactory.LinqDataSource<TestModel>(Db, true)
        //            where x.Child.IntValues.Sum() > 10
        //            select x;

        //        var test = all.ToArray();
        //        test.Count().Should().Be(1);
        //        test[0].IntValue.Should().Be(43);
        //    });
        //}
    }
}
