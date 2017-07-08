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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Query;
using FluentAssertions;
using Newtonsoft.Json;
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
            using (var q = Query.Select().From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, row) =>
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

            var name = Expression.Property("name");
            var address = Expression.Property("address");
            var age = Expression.Property("age");
            var work = Expression.Property("work");

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
                using (var q = Query.Select().From(DataSourceFactory.Database(Db)).Where(exp)) {
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
            var n1 = Expression.Property("number1");

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
            var n1 = Expression.Property("number1");
            var n2 = Expression.Property("number2");

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
            var n1 = Expression.Property("number1");
            var n2 = Expression.Property("number2");
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

            using (var q = Query.Select()
                .From(DataSource.Database(Db))
                .Where(Expression.Property("string").Is("string"))) {

                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var doc = row.Document;
                    doc.Id.Should().Be(doc1.Id, "because otherwise the wrong document ID was populated");
                    doc["string"].ToString().Should().Be("string", "because otherwise garbage data was inserted");
                });
                numRows.Should().Be(1, "beacuse one row matches the given query");
            }

            using (var q = Query.Select()
                .From(DataSource.Database(Db))
                .Where(Expression.Property("string").IsNot("string1"))) {

                var numRows = VerifyQuery(q, (n, row) =>
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
            var n1 = Expression.Property("number1");
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
            var firstName = Expression.Property("name.first");
            using (var q = Query.Select()
                .From(DataSource.Database(Db))
                .Where(firstName.InExpressions(expected))
                .OrderBy(Ordering.Property("name.first"))) {

                var numRows = VerifyQuery(q, (n, row) =>
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

            var where = Expression.Property("name.first").Like("%Mar%");
            using (var q = Query.Select()
                .From(DataSource.Database(Db))
                .Where(where)
                .OrderBy(Ordering.Property("name.first").Ascending())) {

                var firstNames = new List<string>();
                var numRows = VerifyQuery(q, (n, row) =>
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

            var where = Expression.Property("name.first").Regex("^Mar.*");
            using (var q = Query.Select()
                .From(DataSource.Database(Db))
                .Where(where)
                .OrderBy(Ordering.Property("name.first").Ascending())) {

                var firstNames = new List<string>();
                var numRows = VerifyQuery(q, (n, row) =>
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
            using (var q = Query.Select()
                .From(DataSource.Database(Db))
                .Where(Expression.Property("sentence").Match("'Dummie woman'"))
                .OrderBy(Ordering.Property("rank(sentence)").Descending())) {
                var numRows = VerifyQuery(q, (n, row) =>
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
                IOrdering order;
                if (ascending) {
                    order = Ordering.Property("name.first").Ascending();
                } else {
                    order = Ordering.Property("name.first").Descending();
                }

                using (var q = Query.Select().From(DataSource.Database(Db)).Where(null).OrderBy(order)) {
                    var firstNames = new List<object>();
                    var numRows = VerifyQuery(q, (n, row) =>
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

            var q = Query.SelectDistinct().From(DataSource.Database(Db));
            var numRows = VerifyQuery(q, (n, row) =>
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
            using (var q = Query.Select().From(DataSource.Database(Db))
                .Where(Expression.Property("number1").LessThan(10)).OrderBy(Ordering.Property("number1"))
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
                            () => args.Rows.Count == 10 && args.Rows.First().Document.GetInt("number1") == -1);
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
            using (var q = Query.Select().From(DataSource.Database(Db))
                .Where(Expression.Property("number1").LessThan(10)).OrderBy(Ordering.Property("number1"))
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

        [Fact]
        public void TestJoin()
        {
            LoadNumbers(100);
            var testDoc = new Document("joinme");
            testDoc.Set("theone", 42);
            Db.Save(testDoc);
            var number2Prop = Expression.Property("number2");
            using (var q = Query.Select(SelectResult.Expression(number2Prop.From("main")))
                .From(DataSource.Database(Db).As("main"))
                .Joins(Join.DefaultJoin(DataSource.Database(Db).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                using (var results = q.Run()) {
                    results.Count.Should().Be(1, "because only one document should match 42");
                    results.First().GetInt(0).Should().Be(58,
                        "because that was the number stored in 'number2' of the matching doc");
                }
            }
        }

        [Fact]
        public void TestAggregateFunction()
        {
            LoadNumbers(100);

            var avg = SelectResult.Expression(Function.Avg(Expression.Property("number1")));
            var cnt = SelectResult.Expression(Function.Count(Expression.Property("number1")));
            var min = SelectResult.Expression(Function.Min(Expression.Property("number1")));
            var max = SelectResult.Expression(Function.Max(Expression.Property("number1")));
            var sum = SelectResult.Expression(Function.Sum(Expression.Property("number1")));
            using (var q = Query.Select(avg, cnt, min, max, sum)
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetDouble(0).Should().BeApproximately(50.5, Double.Epsilon);
                    row.GetInt(1).Should().Be(100);
                    row.GetInt(2).Should().Be(1);
                    row.GetInt(3).Should().Be(100);
                    row.GetInt(4).Should().Be(5050);
                });

                numRows.Should().Be(1);
            }
        }

        [Fact]
        public void TestGroupBy()
        {
            var expectedStates = new[] {"AL", "CA", "CO", "FL", "IA"};
            var expectedCounts = new[] {1, 6, 1, 1, 3};
            var expectedZips = new[] {"35243", "94153", "81223", "33612", "50801"};

            LoadJSONResource("names_100");

            var STATE = Expression.Property("contact.address.state");
            var gender = Expression.Property("gender");
            var COUNT = Function.Count(1);
            var zip = Expression.Property("contact.address.zip");
            var MAXZIP = Function.Max(zip);

            using (var q = Query.Select(SelectResult.Expression(STATE), SelectResult.Expression(COUNT), SelectResult.Expression(MAXZIP))
                .From(DataSource.Database(Db))
                .Where(gender.EqualTo("female"))
                .GroupBy(STATE)
                .OrderBy(Ordering.Expression(STATE))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var state = row.GetString(0);
                    var count = row.GetInt(1);
                    var maxZip = row.GetString(2);
                    if (n - 1 < expectedStates.Length) {
                        state.Should().Be(expectedStates[n - 1]);
                        count.Should().Be(expectedCounts[n - 1]);
                        maxZip.Should().Be(expectedZips[n - 1]);
                    }
                });
                numRows.Should().Be(31);
            }

            expectedStates = new[] { "CA", "IA", "IN" };
            expectedCounts = new[] { 6, 3, 2 };
            expectedZips = new[] {"94153", "50801", "47952"};

            using (var q = Query.Select(SelectResult.Expression(STATE), SelectResult.Expression(COUNT), SelectResult.Expression(MAXZIP))
                .From(DataSource.Database(Db))
                .Where(gender.EqualTo("female"))
                .GroupBy(STATE)
                .Having(COUNT.GreaterThan(1))
                .OrderBy(Ordering.Expression(STATE))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var state = row.GetString(0);
                    var count = row.GetInt(1);
                    var maxZip = row.GetString(2);
                    if (n - 1 < expectedStates.Length) {
                        state.Should().Be(expectedStates[n - 1]);
                        count.Should().Be(expectedCounts[n - 1]);
                        maxZip.Should().Be(expectedZips[n - 1]);
                    }
                });
                numRows.Should().Be(15);
            }
        }

        [Fact]
        public void TestParameters()
        {
            LoadNumbers(10);

            var NUMBER1 = Expression.Property("number1");
            var PARAM_N1 = Expression.Parameter("num1");
            var PARAM_N2 = Expression.Parameter("num2");

            using (var q = Query.Select(SelectResult.Expression(NUMBER1))
                .From(DataSource.Database(Db))
                .Where(NUMBER1.Between(PARAM_N1, PARAM_N2))
                .OrderBy(Ordering.Expression(NUMBER1))) {
                q.Parameters.Set("num1", 2);
                q.Parameters.Set("num2", 5);

                var expectedNumbers = new[] {2, 3, 4, 5};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var number = row.GetInt(0);
                    number.Should().Be(expectedNumbers[n - 1]);
                });

                numRows.Should().Be(4);
            }
        }

        [Fact]
        public void TestMeta()
        {
            LoadNumbers(5);

            var DOC_ID = Expression.Meta().DocumentID;
            var DOC_SEQ = Expression.Meta().Sequence;
            var NUMBER1 = Expression.Property("number1");

            var RES_DOC_ID = SelectResult.Expression(DOC_ID);
            var RES_DOC_SEQ = SelectResult.Expression(DOC_SEQ);
            var RES_NUMBER1 = SelectResult.Expression(NUMBER1);

            using (var q = Query.Select(RES_DOC_ID, RES_DOC_SEQ, RES_NUMBER1)
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Expression(DOC_SEQ))) {
                var expectedDocIDs = new[] {"doc1", "doc2", "doc3", "doc4", "doc5"};
                var expectedSeqs = new[] {1, 2, 3, 4, 5};
                var expectedNumbers = expectedSeqs;

                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var docID = row.GetString(0);
                    var seq = row.GetInt(1);
                    var number = row.GetInt(2);

                    docID.Should().Be(expectedDocIDs[n - 1]);
                    seq.Should().Be(expectedSeqs[n - 1]);
                    number.Should().Be(expectedNumbers[n - 1]);
                });

                numRows.Should().Be(5);
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
                using (var q = Query.Select().From(DataSource.Database(Db)).Where(c.Item1)) {
                    var lastN = 0;
                    VerifyQuery(q, (n, row) =>
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

        private int VerifyQuery(IQuery query, Action<int, IQueryRow> block)
        {
            using (var result = query.Run()) {
                using (var e = result.GetEnumerator()) {
                    var n = 0;
                    while (e.MoveNext()) {
                        block?.Invoke(++n, e.Current);
                    }


                    return n;
                }
            }
        }
    }
}
