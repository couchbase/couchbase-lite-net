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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Query;
using FluentAssertions;
using LiteCore.Interop;
using Newtonsoft.Json;
using Couchbase.Lite.Internal.Query;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

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

        public Document Document { get; set; }

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

#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public class QueryTest : TestCase
    {
#if !WINDOWS_UWP
        public QueryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestNoWhereQuery()
        {
            LoadJSONResource("names_100");
            using (var q = QueryFactory.Select().From(DataSourceFactory.Database(Db))) {
                var numRows = VerifyQuery(q, true, (n, row) =>
                {
                    var expectedID = $"doc-{n:D3}";
                    row.DocumentID.Should().Be(expectedID, "because otherwise the IDs were out of order");
                    row.Sequence.Should().Be((uint)n, "because otherwise the sequences were out of order");
                    var doc = row.Document;
                    doc.Id.Should().Be(expectedID, "because the document ID on the row should match the document");
                    doc.Sequence.Should()
                        .Be((ulong)n, "because the sequence on the row should match the document");
                });

                numRows.Should().Be(100, "because otherwise the incorrect number of rows was returned");
            }
        }

        /*[Fact]
        public void TestWhereCheckNull()
        {
            IDocument doc1 = null, doc2 = null;
            doc1 = Db["doc1"];
            doc1["name"] = "Scott";
            doc1["address"] = null;
            doc1.Save();

            doc2 = Db["doc2"];
            doc2["name"] = "Tiger";
            doc2["address"] = "123 1st ave.";
            doc2["age"] = 20;
            doc2.Save();

            var name = ExpressionFactory.Property("name");
            var address = ExpressionFactory.Property("address");
            var age = ExpressionFactory.Property("age");
            var work = ExpressionFactory.Property("work");

            var tests = new[] {
                Tuple.Create(name.NotNull(), new[] { doc1, doc2 }),
                Tuple.Create(name.IsNull(), new IDocument[0]),
                Tuple.Create(address.NotNull(), new[] { doc2 }),
                Tuple.Create(address.IsNull(), new[] { doc1 }),
                Tuple.Create(age.NotNull(), new[] { doc1, doc2 }), // null != missing
                Tuple.Create(age.IsNull(), new IDocument[0]),
                Tuple.Create(work.NotNull(), new[] { doc1, doc2 }),
                Tuple.Create(work.IsNull(), new IDocument[0])
            };

            int testNum = 1;
            foreach (var test in tests) {
                var exp = test.Item1;
                var expectedDocs = test.Item2;
                using (var q = QueryFactory.Select().From(DataSourceFactory.Database(Db)).Where(exp)) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        if (n <= expectedDocs.Length) {
                            var doc = expectedDocs[n - 1];
                            row.DocumentID.Should()
                                .Be(doc.Id, $"because otherwise the row results were different than expected ({testNum})");
                        }
                    });

                    numRows.Should().Be(expectedDocs.Length, "because otherwise too many rows were returned");
                }

                testNum++;
            }
        }*/

        [Fact]
        public void TestWhereComparison()
        {
            var n1 = ExpressionFactory.Property("number1");

            var l3 = new Func<int, bool>(n => n < 3);
            var nl3 = new Func<int, bool>(n => !(n < 3));
            var le3 = new Func<int, bool>(n => n <= 3);
            var nle3 = new Func<int, bool>(n => !(n <= 3));
            var g6 = new Func<int, bool>(n => n > 6);
            var ng6 = new Func<int, bool>(n => !(n > 6));
            var ge6 = new Func<int, bool>(n => n >= 6);
            var nge6 = new Func<int, bool>(n => !(n >= 6));
            var e7 = new Func<int, bool>(n => n == 7);
            var ne7 = new Func<int, bool>(n => n != 7);
            var cases = new[] {
                Tuple.Create(n1.LessThan(3),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)l3),
                Tuple.Create(n1.NotLessThan(3),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)nl3),
                Tuple.Create(n1.LessThanOrEqualTo(3),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)le3),
                Tuple.Create(n1.NotLessThanOrEqualTo(3),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)nle3),
                Tuple.Create(n1.GreaterThan(6),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)g6),
                Tuple.Create(n1.NotGreaterThan(6),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)ng6),
                Tuple.Create(n1.GreaterThanOrEqualTo(6),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)ge6),
                Tuple.Create(n1.NotGreaterThanOrEqualTo(6),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)nge6),
                Tuple.Create(n1.EqualTo(7),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)e7),
                Tuple.Create(n1.NotEqualTo(7),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)ne7)
            };

            LoadNumbers(10);
            RunTestWithNumbers(new[] {2, 8, 3, 7, 4, 6, 5, 5, 1, 9}, cases);
        }

        [Fact]
        public void TestWhereWithArithmetic()
        {
            var n1 = ExpressionFactory.Property("number1");
            var n2 = ExpressionFactory.Property("number2");

            var m2g8 = new Func<int, int, bool>((x1, x2) => x1 * 2 > 8);
            var d2g3 = new Func<int, int, bool>((x1, x2) => x1 / 2 > 3);
            var m2e0 = new Func<int, int, bool>((x1, x2) => (x1 % 2) == 0);
            var a5g10 = new Func<int, int, bool>((x1, x2) => x1 + 5 > 10);
            var s5g0 = new Func<int, int, bool>((x1, x2) => x1 - 5 > 0);
            var mn2g10 = new Func<int, int, bool>((x1, x2) => x1 * x2 > 10);
            var dn1g3 = new Func<int, int, bool>((x1, x2) => x2 / x1 > 3);
            var mn2e0 = new Func<int, int, bool>((x1, x2) => (x1 % x2) == 0);
            var an2e10 = new Func<int, int, bool>((x1, x2) => x1 + x2 == 10);
            var sn2g0 = new Func<int, int, bool>((x1, x2) => x1 - x2 > 0);
            var cases = new[] {
                Tuple.Create(n1.Multiply(2).GreaterThan(8),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)m2g8),
                Tuple.Create(n1.Divide(2).GreaterThan(3),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)d2g3),
                Tuple.Create(n1.Modulo(2).EqualTo(0),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)m2e0),
                Tuple.Create(n1.Add(5).GreaterThan(10),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)a5g10),
                Tuple.Create(n1.Subtract(5).GreaterThan(0),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)s5g0),
                Tuple.Create(n1.Multiply(n2).GreaterThan(10),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)mn2g10),
                Tuple.Create(n2.Divide(n1).GreaterThan(3),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)dn1g3),
                Tuple.Create(n1.Modulo(n2).EqualTo(0),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)mn2e0),
                Tuple.Create(n1.Add(n2).EqualTo(10),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)an2e10),
                Tuple.Create(n1.Subtract(n2).GreaterThan(0),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)sn2g0)
            };

            LoadNumbers(10);
            RunTestWithNumbers(new[] {6, 3, 5, 5, 5, 7, 2, 3, 10, 5}, cases);
        }

        [Fact]
        public void TestWhereAndOr()
        {
            var n1 = ExpressionFactory.Property("number1");
            var n2 = ExpressionFactory.Property("number2");
            var cases = new[] {
                Tuple.Create(n1.GreaterThan(3).And(n2.GreaterThan(3)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereAndValidator, default(object)),
                Tuple.Create(n1.LessThan(3).Or(n2.LessThan(3)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereOrValidator, default(object))
            };
            LoadNumbers(10);
            RunTestWithNumbers(new[] { 3, 5 }, cases);
        }

        [Fact]
        public void TestWhereIs()
        {
            var doc1 = new Document();
            doc1.Set("string", "string");
            Db.Save(doc1);

            using (var q = QueryFactory.Select()
                .From(DataSourceFactory.Database(Db))
                .Where(ExpressionFactory.Property("string").Is("string"))) {

                var numRows = VerifyQuery(q, true, (n, row) =>
                {
                    var doc = row.Document;
                    doc.Id.Should().Be(doc1.Id, "because otherwise the wrong document ID was populated");
                    doc["string"].ToString().Should().Be("string", "because otherwise garbage data was inserted");
                });
                numRows.Should().Be(1, "beacuse one row matches the given query");
            }

            using (var q = QueryFactory.Select()
                .From(DataSourceFactory.Database(Db))
                .Where(ExpressionFactory.Property("string").IsNot("string1"))) {

                var numRows = VerifyQuery(q, true, (n, row) =>
                {
                    var doc = row.Document;
                    doc.Id.Should().Be(doc1.Id, "because otherwise the wrong document ID was populated");
                    doc["string"].ToString().Should().Be("string", "because otherwise garbage data was inserted");
                });
                numRows.Should().Be(1, "because one row matches the 'IS NOT' query");
            }
        }

        [Fact]
        public void TestWhereBetween()
        {
            LoadNumbers(10);
            var n1 = ExpressionFactory.Property("number1");
            var cases = new[] {
                Tuple.Create(n1.Between(3, 7), 
                (Func<IDictionary<string, object>, object, bool>) TestWhereBetweenValidator, (object)null)
            };

            RunTestWithNumbers(new[] { 5 }, cases);
        }

        [Fact]
        public void TestWhereIn()
        {
            LoadJSONResource("names_100");

            var expected = new[] {"Marcy", "Margaretta", "Margrett", "Marlen", "Maryjo" };
            var firstName = ExpressionFactory.Property("name.first");
            using (var q = QueryFactory.Select()
                .From(DataSourceFactory.Database(Db))
                .Where(firstName.InExpressions(expected))
                .OrderBy(OrderByFactory.Property("name.first"))) {

                var numRows = VerifyQuery(q, true, (n, row) =>
                {
                    var name = row.Document.GetDictionary("name").GetString("first");
                    name.Should().Be(expected[n - 1], "because otherwise incorrect rows were returned");
                });

                numRows.Should().Be(expected.Length, "because otherwise an incorrect number of rows were returned");
            }
        }

        [Fact]
        public void TestWhereLike()
        {
            LoadJSONResource("names_100");

            var where = ExpressionFactory.Property("name.first").Like("%Mar%");
            using (var q = QueryFactory.Select()
                .From(DataSourceFactory.Database(Db))
                .Where(where)
                .OrderBy(OrderByFactory.Property("name.first").Ascending())) {

                var firstNames = new List<string>();
                var numRows = VerifyQuery(q, false, (n, row) =>
                {
                    var doc = row.Document;
                    var firstName = doc.GetDictionary("name")?.GetString("first");
                    if (firstName != null) {
                        firstNames.Add(firstName);
                    }
                });

                numRows.Should().Be(5, "because there are 5 rows like that in the data source");
                firstNames.Should()
                    .OnlyContain(str => str.Contains("Mar"), "because otherwise an incorrect entry came in");
            }
        }

        [Fact]
        public void TestWhereRegex()
        {
            LoadJSONResource("names_100");

            var where = ExpressionFactory.Property("name.first").Regex("^Mar.*");
            using (var q = QueryFactory.Select()
                .From(DataSourceFactory.Database(Db))
                .Where(where)
                .OrderBy(OrderByFactory.Property("name.first").Ascending())) {

                var firstNames = new List<string>();
                var numRows = VerifyQuery(q, false, (n, row) =>
                {
                    var doc = row.Document;
                    var firstName = doc.GetDictionary("name")?.GetString("first");
                    if (firstName != null) {
                        firstNames.Add(firstName);
                    }
                });

                numRows.Should().Be(5, "because there are 5 rows like that in the data source");
                var regex = new Regex("^Mar.*");
                firstNames.Should()
                    .OnlyContain(str => regex.IsMatch(str), "because otherwise an incorrect entry came in");
            }
        }

        [Fact]
        public void TestWhereMatch()
        {
            LoadJSONResource("sentences");

            Db.CreateIndex(new[] {"sentence"}, IndexType.FullTextIndex, null);
            using (var q = QueryFactory.Select()
                .From(DataSourceFactory.Database(Db))
                .Where(ExpressionFactory.Property("sentence").Match("'Dummie woman'"))
                .OrderBy(OrderByFactory.Property("rank(sentence)").Descending())) {
                var numRows = VerifyQuery(q, true, (n, row) =>
                {
                    var ftsRow = row as IFullTextQueryRow;
                    var text = ftsRow?.FullTextMatched;
                    text.Should()
                        .Contain("Dummie")
                        .And.Subject.Should()
                        .Contain("woman", "because otherwise the full text query failed");
                    ftsRow.MatchCount.Should().Be(2u, "because otherwise an incorrect number of matches was returned");
                });

                numRows.Should().Be(2, "because two rows in the data match the query");
            }
        }

        [Fact]
        public void TestOrderBy()
        {
            LoadJSONResource("names_100");
            foreach (var ascending in new[] {true, false}) {
                IOrderBy order;
                if (ascending) {
                    order = OrderByFactory.Property("name.first").Ascending();
                } else {
                    order = OrderByFactory.Property("name.first").Descending();
                }

                using (var q = QueryFactory.Select().From(DataSourceFactory.Database(Db)).Where(null).OrderBy(order)) {
                    var firstNames = new List<object>();
                    var numRows = VerifyQuery(q, false, (n, row) =>
                    {
                        var doc = row.Document;
                        var firstName = doc.GetDictionary("name").GetString("first");
                        if (firstName != null) {
                            firstNames.Add(firstName);
                        }
                    });

                    numRows.Should().Be(100, "because otherwise the wrong number of rows was retrieved");
                    firstNames.Should().HaveCount(numRows, "because otherwise some rows were null");
                    var firstNamesCopy = new List<object>(firstNames);
                    firstNames.Sort();
                    if (!ascending) {
                        firstNames.Reverse();
                    }

                    firstNames.Should().ContainInOrder(firstNamesCopy, "because otherwise the results were not sorted");
                }
            }
        }

        //[Fact]
        public void TestSelectDistinct()
        {
            // TODO: Needs LiteCore fix
            var doc1 = new Document();
            doc1.Set("number", 1);
            Db.Save(doc1);

            var doc2 = new Document();
            doc2.Set("number", 1);
            Db.Save(doc2);

            var q = QueryFactory.SelectDistinct().From(DataSourceFactory.Database(Db));
            var numRows = VerifyQuery(q, true, (n, row) =>
            {
                var doc = row.Document;
                doc.Id.Should().Be(doc1.Id, "because doc2 is identical and should be skipped");
            });

            numRows.Should().Be(1, "because there is only one distinct row");
        }

        [Fact]
        public async Task TestLiveQuery()
        {
            LoadNumbers(100);
            using (var q = QueryFactory.Select().From(DataSourceFactory.Database(Db))
                .Where(ExpressionFactory.Property("number1").LessThan(10)).OrderBy(OrderByFactory.Property("number1"))
                .ToLive()) {
                var wa = new WaitAssert();
                var wa2 = new WaitAssert();
                var count = 1;
                q.Changed += (sender, args) =>
                {
                    if (count++ == 1) {
                        wa.RunConditionalAssert(
                            () => args.Rows.Count == 9);
                    } else {
                        wa2.RunConditionalAssert(
                            () => args.Rows.Count == 10 && args.Rows[0].Document.GetInt("number1") == -1);
                    }
                    
                };

                q.Run();
                await Task.Delay(500).ConfigureAwait(false);
                wa.WaitForResult(TimeSpan.FromSeconds(5));
                CreateDocInSeries(-1, 100);
                wa2.WaitForResult(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task TestLiveQueryNoUpdate()
        {
            LoadNumbers(100);
            using (var q = QueryFactory.Select().From(DataSourceFactory.Database(Db))
                .Where(ExpressionFactory.Property("number1").LessThan(10)).OrderBy(OrderByFactory.Property("number1"))
                .ToLive()) {

                var mre = new ManualResetEventSlim();
                q.Changed += (sender, args) =>
                {
                    mre.Set();
                };

                await Task.Delay(500).ConfigureAwait(false);

                // This change will not affect the query results because 'number1 < 10' 
                // is not true
                CreateDocInSeries(111, 100);
                mre.Wait(5000).Should().BeFalse("because the Changed event should not fire needlessly");
            }
        }

        private bool TestWhereCompareValidator(IDictionary<string, object> properties, object context)
        {
            var ctx = (Func<int, bool>)context;
            return ctx(Convert.ToInt32(properties["number1"]));
        }

        private bool TestWhereMathValidator(IDictionary<string, object> properties, object context)
        {
            var ctx = (Func<int, int, bool>)context;
            return ctx(Convert.ToInt32(properties["number1"]), Convert.ToInt32(properties["number2"]));
        }

        private bool TestWhereBetweenValidator(IDictionary<string, object> properties, object context)
        {
            return Convert.ToInt32(properties["number1"]) >= 3 &&
                   Convert.ToInt32(properties["number1"]) <= 7;
        }

        private bool TestWhereAndValidator(IDictionary<string, object> properties, object context)
        {
            return Convert.ToInt32(properties["number1"]) > 3 &&
                   Convert.ToInt32(properties["number2"]) > 3;
        }

        private bool TestWhereOrValidator(IDictionary<string, object> properties, object context)
        {
            return Convert.ToInt32(properties["number1"]) < 3 ||
                   Convert.ToInt32(properties["number2"]) < 3;
        }

        private void RunTestWithNumbers(IList<int> expectedResultCount,
            IList<Tuple<IExpression, Func<IDictionary<string, object>, object, bool>, object>> validator)
        {
            int index = 0;
            foreach (var c in validator) {
                using (var q = QueryFactory.Select().From(DataSourceFactory.Database(Db)).Where(c.Item1)) {
                    var lastN = 0;
                    VerifyQuery(q, false, (n, row) =>
                    {
                        var props =row.Document.ToDictionary();
                        c.Item2(props, c.Item3).Should().BeTrue("because otherwise the row failed validation");
                        lastN = n;
                    });

                    lastN.Should()
                        .Be(expectedResultCount[index++], "because otherwise there was an incorrect number of rows");
                }
            }
        }

        private void LoadNumbers(int num)
        {
            var numbers = new List<IDictionary<string, object>>();
            Db.InBatch(() =>
            {
                for (int i = 1; i <= num; i++) {
                    var docID = $"doc{i}";
                    var doc = new Document(docID);
                    doc.Set("number1", i);
                    doc.Set("number2", num - i);
                    Db.Save(doc);
                    numbers.Add(doc.ToDictionary());
                }
            });
        }

        private Document CreateDocInSeries(int entry, int max)
        {
            var docID = $"doc{entry}";
            var doc = new Document(docID);
            doc.Set("number1", entry);
            doc.Set("number2", max - entry);
            Db.Save(doc);
            return doc;
        }

        private int VerifyQuery(IQuery query, bool randomAccess, Action<int, IQueryRow> block)
        {
            var result = query.Run();
            using (var e = result.GetEnumerator()) {
                var n = 0;
                while(e.MoveNext()) { 
                    block?.Invoke(++n, e.Current);
                }

                if (randomAccess && n > 0) {
                    // Note:  The block's first parameter is 1-based, while IList is 0-based
                    block(n, result[n - 1]);
                    block(1, result[0]);
                    block(n / 2 + 1, result[n / 2]);
                }

                return n;
            }
        }
    }
}
