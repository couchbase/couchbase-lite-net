//
//  QueryTest.cs
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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Query;

using FluentAssertions;
using FluentAssertions.Execution;

using Newtonsoft.Json;

using Test.Util;
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
    public class QueryTest : TestCase
    {
        private static readonly ISelectResult DocID = SelectResult.Expression(Meta.ID);
        private static readonly ISelectResult Sequence = SelectResult.Expression(Meta.Sequence);
        private static readonly ISelectResult IsDeleted = SelectResult.Expression(Meta.IsDeleted);
        private static readonly ISelectResult Expiration = SelectResult.Expression(Meta.Expiration);

        Type queryTypeExpressionType = typeof(QueryTypeExpression);

#if !WINDOWS_UWP
        public QueryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestQueryDocumentExpirationAfterDocsExpired()
        {
            var dto2 = DateTimeOffset.Now.AddSeconds(2);
            var dto3 = DateTimeOffset.Now.AddSeconds(3);
            var dto4 = DateTimeOffset.Now.AddSeconds(4);
            var dto6InMS = DateTimeOffset.Now.AddSeconds(6).ToUnixTimeMilliseconds();

            using (var doc1a = new MutableDocument("doc1"))
            using (var doc1b = new MutableDocument("doc2"))
            using (var doc1c = new MutableDocument("doc3")) {
                doc1a.SetInt("answer", 42);
                doc1a.SetString("a", "string");
                Db.Save(doc1a);

                doc1b.SetInt("answer", 42);
                doc1b.SetString("b", "string");
                Db.Save(doc1b);

                doc1c.SetInt("answer", 42);
                doc1c.SetString("c", "string");
                Db.Save(doc1c);

                Db.SetDocumentExpiration("doc1", dto2).Should().Be(true);
                Db.SetDocumentExpiration("doc2", dto3).Should().Be(true);
                Db.SetDocumentExpiration("doc3", dto4).Should().Be(true);
            }

            Thread.Sleep(4100);

            Try.Assertion(() =>
            {
                using (var r = QueryBuilder.Select(DocID, Expiration)
                    .From(DataSource.Database(Db))
                    .Where(Meta.Expiration
                        .LessThan(Expression.Long(dto6InMS)))) {

                    var b = r.Execute().AllResults();
                    b.Count.Should().Be(0);
                }
            }).Times(5).Delay(TimeSpan.FromMilliseconds(500)).Go().Should().BeTrue();
        }

        [Fact]
        public void TestQueryDocumentExpiration()
        {
            var dto20 = DateTimeOffset.Now.AddSeconds(20);
            var dto30 = DateTimeOffset.Now.AddSeconds(30);
            var dto40 = DateTimeOffset.Now.AddSeconds(40);
            var dto60InMS = DateTimeOffset.Now.AddSeconds(60).ToUnixTimeMilliseconds();

            using (var doc1a = new MutableDocument("doc1"))
            using (var doc1b = new MutableDocument("doc2"))
            using (var doc1c = new MutableDocument("doc3")) {
                doc1a.SetInt("answer", 42);
                doc1a.SetString("a", "string");
                Db.Save(doc1a);

                doc1b.SetInt("answer", 42);
                doc1b.SetString("b", "string");
                Db.Save(doc1b);

                doc1c.SetInt("answer", 42);
                doc1c.SetString("c", "string");
                Db.Save(doc1c);

                Db.SetDocumentExpiration("doc1", dto20).Should().Be(true);
                Db.SetDocumentExpiration("doc2", dto30).Should().Be(true);
                Db.SetDocumentExpiration("doc3", dto40).Should().Be(true);
            }

            using (var r = QueryBuilder.Select(DocID, Expiration)
                .From(DataSource.Database(Db))
                .Where(Meta.Expiration
                .LessThan(Expression.Long(dto60InMS)))) {

                var b = r.Execute().AllResults();
                b.Should().HaveCount(3);
            }
        }

        [Fact]
        public void TestQueryDocumentIsNotDeleted()
        {
            using (var doc1 = new MutableDocument("doc1")){
                doc1.SetInt("answer", 42);
                doc1.SetString("a", "string");
                Db.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetInt("answer", 42);
                doc2.SetString("a", "string");
                Db.Save(doc2);
                Db.Delete(doc2);
            }
            
            using (var q = QueryBuilder.Select(DocID, IsDeleted)
                 .From(DataSource.Database(Db))
                 .Where(Meta.IsDeleted.EqualTo(Expression.Boolean(false)))) {
                var count = VerifyQuery(q, (n, r) =>
                {
                    r.GetString(0).Should().Be("doc1");
                    r.GetBoolean(1).Should().BeFalse();
                });
                count.Should().Be(1);
            }
        }

        [Fact]
        public void TestQueryDocumentIsDeleted()
        {
            using (var doc1 = new MutableDocument("doc1")){
                doc1.SetInt("answer", 42);
                doc1.SetString("a", "string");
                Db.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetInt("answer", 42);
                doc2.SetString("a", "string");
                Db.Save(doc2);
                Db.Delete(doc2);
            }

            using (var q = QueryBuilder.Select(DocID, IsDeleted)
                 .From(DataSource.Database(Db))
                 .Where(Meta.IsDeleted.EqualTo(Expression.Boolean(true)))) {
                var count = VerifyQuery(q, (n, r) =>
                {
                    r.GetString(0).Should().Be("doc2");
                    r.GetBoolean(1).Should().BeTrue();
                });
                count.Should().Be(1);
            }
        }

        [Fact]
        public void TestExpiredNotInQuery()
        {
            const string docId = "byebye";
            using (var doc1 = new MutableDocument(docId)) {
                doc1.SetString("expire_me", "now");
                Db.Save(doc1);
            }
            
            Db.SetDocumentExpiration(docId, DateTimeOffset.Now);

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("expire_me", "never");
                Db.Save(doc2);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Database(Db))) {
                var count = VerifyQuery(q, (n, r) => { r.GetString(0).Should().Be("doc2"); });
                count.Should().Be(1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Database(Db))
                .Where(Meta.IsDeleted.EqualTo(Expression.Boolean(true)))) {
                var count = VerifyQuery(q, (n, r) => throw new AssertionFailedException("No results should be present"));
                count.Should().Be(0);
            }
        }

        [Fact]
        public void TestReadOnlyParameters()
        {
            using (var q = QueryBuilder.Select(DocID, Sequence).From(DataSource.Database(Db))) {
                var parameters = new Parameters().SetString("foo", "bar");
                q.Parameters = parameters;
                q.Parameters.GetValue("foo").Should().Be("bar");
                q.Invoking(q2 => q2.Parameters.SetValue("foo2", "bar2"))
                    .ShouldThrow<InvalidOperationException>("because the parameters are read only once in use");
            }
        }

        [Fact]
        public void TestParametersWithDictionaryArgument()
        {
            var dict = new Dictionary<string, object>();
            var utcNow = DateTime.UtcNow;
            dict.Add(utcNow.ToShortDateString(), utcNow);
            var parameters = new Parameters(dict);
            parameters.GetValue(utcNow.ToShortDateString()).Should().Be(utcNow);
        }

        [Fact]
        public void TestNoWhereQuery()
        {
            LoadJSONResource("names_100");
            using (var q = QueryBuilder.Select(DocID, Sequence).From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var expectedID = $"doc-{n:D3}";
                    row.GetString(0).Should().Be(expectedID, "because otherwise the IDs were out of order");
                    row.GetLong(1).Should().Be(n, "because otherwise the sequences were out of order");

                    var doc = Db.GetDocument(row.GetString(0));
                    doc.Id.Should().Be(expectedID, "because the document ID on the row should match the document");
                    doc.Sequence.Should()
                        .Be((ulong)n, "because the sequence on the row should match the document");
                });

                numRows.Should().Be(100, "because otherwise the incorrect number of rows was returned");
            }
        }

        [Fact]
        public void TestWhereNullOrMissing()
        {
            MutableDocument doc1 = null, doc2 = null;
            doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            Db.Save(doc1);

            doc2 = new MutableDocument("doc2");
            doc2.SetString("name", "Tiger");
            doc2.SetString("address", "123 1st ave.");
            doc2.SetInt("age", 20);
            Db.Save(doc2);

            var name = Expression.Property("name");
            var address = Expression.Property("address");
            var age = Expression.Property("age");
            var work = Expression.Property("work");

            var tests = new[] {
                Tuple.Create(name.NotNullOrMissing(), new[] { doc1, doc2 }),
                Tuple.Create(name.IsNullOrMissing(), new MutableDocument[0]),
                Tuple.Create(address.NotNullOrMissing(), new[] { doc2 }),
                Tuple.Create(address.IsNullOrMissing(), new[] { doc1 }),
                Tuple.Create(age.NotNullOrMissing(), new[] { doc2 }),
                Tuple.Create(age.IsNullOrMissing(), new[] { doc1 }),
                Tuple.Create(work.NotNullOrMissing(), new MutableDocument[0]),
                Tuple.Create(work.IsNullOrMissing(), new[] { doc1, doc2 })
            };

            int testNum = 1;
            foreach (var test in tests) {
                var exp = test.Item1;
                var expectedDocs = test.Item2;
                using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Database(Db)).Where(exp)) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        if (n <= expectedDocs.Length) {
                            var doc = expectedDocs[n - 1];
                            row.GetString("id").Should()
                                .Be(doc.Id, $"because otherwise the row results were different than expected ({testNum})");
                        }
                    });

                    numRows.Should().Be(expectedDocs.Length, "because otherwise too many rows were returned");
                }

                testNum++;
            }
        }

        [Fact]
        public void TestWhereComparison()
        {
            var n1 = Expression.Property("number1");

            var l3 = new Func<int, bool>(n => n < 3);
            var le3 = new Func<int, bool>(n => n <= 3);
            var g6 = new Func<int, bool>(n => n > 6);
            var ge6 = new Func<int, bool>(n => n >= 6);
            var e7 = new Func<int, bool>(n => n == 7);
            var ne7 = new Func<int, bool>(n => n != 7);
            var cases = new[] {
                Tuple.Create(n1.LessThan(Expression.Int(3)),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)l3),
                Tuple.Create(n1.LessThanOrEqualTo(Expression.Int(3)),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)le3),
                Tuple.Create(n1.GreaterThan(Expression.Int(6)),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)g6),
                Tuple.Create(n1.GreaterThanOrEqualTo(Expression.Int(6)),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)ge6),
                Tuple.Create(n1.EqualTo(Expression.Int(7)),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)e7),
                Tuple.Create(n1.NotEqualTo(Expression.Int(7)),
                    (Func<IDictionary<string, object>, object, bool>) TestWhereCompareValidator, (object)ne7)
            };

            LoadNumbers(10);
            RunTestWithNumbers(new[] { 2, 3, 4, 5, 1, 9 }, cases);
        }

        [Fact]
        public void TestWhereArithmetic()
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
                Tuple.Create(n1.Multiply(Expression.Int(2)).GreaterThan(Expression.Int(8)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)m2g8),
                Tuple.Create(n1.Divide(Expression.Int(2)).GreaterThan(Expression.Int(3)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)d2g3),
                Tuple.Create(n1.Modulo(Expression.Int(2)).EqualTo(Expression.Int(0)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)m2e0),
                Tuple.Create(n1.Add(Expression.Int(5)).GreaterThan(Expression.Int(10)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)a5g10),
                Tuple.Create(n1.Subtract(Expression.Int(5)).GreaterThan(Expression.Int(0)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)s5g0),
                Tuple.Create(n1.Multiply(n2).GreaterThan(Expression.Int(10)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)mn2g10),
                Tuple.Create(n2.Divide(n1).GreaterThan(Expression.Int(3)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)dn1g3),
                Tuple.Create(n1.Modulo(n2).EqualTo(Expression.Int(0)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)mn2e0),
                Tuple.Create(n1.Add(n2).EqualTo(Expression.Int(10)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)an2e10),
                Tuple.Create(n1.Subtract(n2).GreaterThan(Expression.Int(0)),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereMathValidator, (object)sn2g0)
            };

            LoadNumbers(10);
            RunTestWithNumbers(new[] { 6, 3, 5, 5, 5, 7, 2, 3, 10, 5 }, cases);
        }

        [Fact]
        public void TestWhereAndOr()
        {
            var n1 = Expression.Property("number1");
            var n2 = Expression.Property("number2");
            var cases = new[] {
                Tuple.Create(n1.GreaterThan(Expression.Int(3)).And(n2.GreaterThan(Expression.Int(3))),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereAndValidator, default(object)),
                Tuple.Create(n1.LessThan(Expression.Int(3)).Or(n2.LessThan(Expression.Int(3))),
                    (Func<IDictionary<string, object>, object, bool>)TestWhereOrValidator, default(object))
            };
            LoadNumbers(10);
            RunTestWithNumbers(new[] { 3, 5 }, cases);
        }

        [Fact]
        public void TestWhereIs()
        {
            var doc1 = new MutableDocument();
            doc1.SetString("string", "string");
            Db.Save(doc1);

            using (var q = QueryBuilder.Select(DocID)
                .From(DataSource.Database(Db))
                .Where(Expression.Property("string").EqualTo(Expression.String("string")))) {

                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var doc = Db.GetDocument(row.GetString(0));
                    doc.Id.Should().Be(doc1.Id, "because otherwise the wrong document ID was populated");
                    doc["string"].ToString().Should().Be("string", "because otherwise garbage data was inserted");
                });
                numRows.Should().Be(1, "beacuse one row matches the given query");
            }

            using (var q = QueryBuilder.Select(DocID)
                .From(DataSource.Database(Db))
                .Where(Expression.Property("string").NotEqualTo(Expression.String("string1")))) {

                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var doc = Db.GetDocument(row.GetString(0));
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
                Tuple.Create(n1.Between(Expression.Int(3), Expression.Int(7)), 
                (Func<IDictionary<string, object>, object, bool>) TestWhereBetweenValidator, (object)null)
            };

            RunTestWithNumbers(new[] { 5 }, cases);
        }

        [Fact]
        public void TestWhereIn()
        {
            LoadJSONResource("names_100");

            var expected = new[] {"Marcy", "Margaretta", "Margrett", "Marlen", "Maryjo" };
            var inExpression = expected.Select(Expression.String); // Note, this is LINQ Select, so don't get confused

            var firstName = Expression.Property("name.first");
            using (var q = QueryBuilder.Select(SelectResult.Expression(firstName))
                .From(DataSource.Database(Db))
                .Where(firstName.In(inExpression.ToArray()))
                .OrderBy(Ordering.Property("name.first"))) {

                var numRows = VerifyQuery(q, (n, row) =>
                {

                    var name = row.GetString(0);
                    name.Should().Be(expected[n - 1], "because otherwise incorrect rows were returned");
                });

                numRows.Should().Be(expected.Length, "because otherwise an incorrect number of rows were returned");
            }
        }

        [Fact]
        public void TestWhereLike()
        {
            LoadJSONResource("names_100");

            var where = Expression.Property("name.first").Like(Expression.String("%Mar%"));
            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                .From(DataSource.Database(Db))
                .Where(where)
                .OrderBy(Ordering.Property("name.first").Ascending())) {

                var firstNames = new List<string>();
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var firstName = row.GetString(0);
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

            var where = Expression.Property("name.first").Regex(Expression.String("^Mar.*"));
            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                .From(DataSource.Database(Db))
                .Where(where)
                .OrderBy(Ordering.Property("name.first").Ascending())) {

                var firstNames = new List<string>();
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var firstName = row.GetString(0);
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

            var sentence = Expression.Property("sentence");
            var s_sentence = SelectResult.Expression(sentence);

            var w = FullTextExpression.Index("sentence").Match("'Dummie woman'");
            var o = Ordering.Expression(FullTextFunction.Rank("sentence")).Descending();

            var index = IndexBuilder.FullTextIndex(FullTextIndexItem.Property("sentence"));
            Db.CreateIndex("sentence", index);
            using (var q = QueryBuilder.Select(DocID, s_sentence)
                .From(DataSource.Database(Db))
                .Where(w)
                .OrderBy(o)) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    
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

                using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                    .From(DataSource.Database(Db)).OrderBy(order))  {
                    var firstNames = new List<object>();
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        var firstName = row.GetString(0);
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

        [Fact]
        public void TestSelectDistinct()
        {
            var doc1 = new MutableDocument();
            doc1.SetInt("number", 1);
            Db.Save(doc1);

            var doc2 = new MutableDocument();
            doc2.SetInt("number", 1);
            Db.Save(doc2);

            using (var q = QueryBuilder.SelectDistinct(SelectResult.Expression(Expression.Property("number")))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var number = row.GetInt(0);
                    number.Should().Be(1);
                });

                numRows.Should().Be(1, "because there is only one distinct row");
            }
        }

        [Fact]
        public async Task TestLiveQuery()
        {
            LoadNumbers(100);
            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("number1"))).From(DataSource.Database(Db))
                .Where(Expression.Property("number1").LessThan(Expression.Int(10))).OrderBy(Ordering.Property("number1"))) {
                var wa = new WaitAssert();
                var wa2 = new WaitAssert();
                var count = 1;
                q.AddChangeListener(null, (sender, args) =>
                {
                    if (count++ == 1) {
                        wa.RunConditionalAssert(
                            () => args.Results.Count() == 9);
                    } else {
                        var list = args.Results.ToList();
                        wa2.RunConditionalAssert(
                            () => list.Count() == 10 && list.First().GetInt(0) == -1);
                    }
                    

                });

                await Task.Delay(500);
                wa.WaitForResult(TimeSpan.FromSeconds(5));
                CreateDocInSeries(-1, 100);
                wa2.WaitForResult(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task TestLiveQueryNoUpdate() => await TestLiveQueryNoUpdateInternal(false);

        [Fact]
        public async Task TestLiveQueryNoUpdateConsumeAll() => await TestLiveQueryNoUpdateInternal(true);

        [Fact]
        public void TestJoin()
        {
            LoadNumbers(100);
            var testDoc = new MutableDocument("joinme");
            testDoc.SetInt("theone", 42);
            Db.Save(testDoc);
            var number2Prop = Expression.Property("number2");
            using (var q = QueryBuilder.Select(SelectResult.Expression(number2Prop.From("main")))
                .From(DataSource.Database(Db).As("main"))
                .Join(Join.InnerJoin(DataSource.Database(Db).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                var results = q.Execute().ToList();
                results.Should().HaveCount(1, "because only one document should match 42");
                results.First().GetInt(0).Should().Be(58,
                    "because that was the number stored in 'number2' of the matching doc");
            }

            using (var q = QueryBuilder.Select(SelectResult.All().From("main"))
                .From(DataSource.Database(Db).As("main"))
                .Join(Join.InnerJoin(DataSource.Database(Db).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                var results = q.Execute().ToList();
                results.Should().HaveCount(1, "because only one document should match 42");
                results.First().Keys.FirstOrDefault().Should().Be("main");
            }
        }

        [Fact]
        public void TestLeftJoin()
        {
            LoadNumbers(100);
            var testDoc = new MutableDocument("joinme");
            testDoc.SetInt("theone", 42);
            Db.Save(testDoc);
            var number2Prop = Expression.Property("number2");
            using (var q = QueryBuilder.Select(SelectResult.Expression(number2Prop.From("main")), SelectResult.Expression(Expression.Property("theone").From("secondary")))
                .From(DataSource.Database(Db).As("main"))
                .Join(Join.LeftJoin(DataSource.Database(Db).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                var results = q.Execute().ToList();
                results.Should().HaveCount(101);
                results[41].GetInt(0).Should().Be(58);
                results[41].GetInt(1).Should().Be(42);
                results[42].GetInt(0).Should().Be(57);
                results[42].GetValue(1).Should().BeNull();
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(number2Prop.From("main")), SelectResult.Expression(Expression.Property("theone").From("secondary")))
                .From(DataSource.Database(Db).As("main"))
                .Join(Join.LeftOuterJoin(DataSource.Database(Db).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                var results = q.Execute().ToList();
                results.Should().HaveCount(101);
                results[41].GetInt(0).Should().Be(58);
                results[41].GetInt(1).Should().Be(42);
                results[42].GetInt(0).Should().Be(57);
                results[42].GetValue(1).Should().BeNull();
            }
        }

        [Fact]
        public void TestCrossJoin()
        {
            LoadNumbers(10);
            var num1 = Expression.Property("number1").From("main");
            var num2 = Expression.Property("number2").From("secondary");

            using (var q = QueryBuilder.Select(SelectResult.Expression(num1), SelectResult.Expression(num2))
                .From(DataSource.Database(Db).As("main"))
                .Join(Join.CrossJoin(DataSource.Database(Db).As("secondary")))
                .OrderBy(Ordering.Expression(num2))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    ((row.GetInt(0) - 1) % 10).Should().Be((n - 1) % 10);
                    row.GetInt(1).Should().Be((n - 1) / 10);
                });

                count.Should().Be(100);
            }
        }

        [Fact]
        public void TestAggregateFunctions()
        {
            LoadNumbers(100);

            var avg = SelectResult.Expression(Function.Avg(Expression.Property("number1")));
            var cnt = SelectResult.Expression(Function.Count(Expression.Property("number1")));
            var min = SelectResult.Expression(Function.Min(Expression.Property("number1")));
            var max = SelectResult.Expression(Function.Max(Expression.Property("number1")));
            var sum = SelectResult.Expression(Function.Sum(Expression.Property("number1")));
            using (var q = QueryBuilder.Select(avg, cnt, min, max, sum)
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
            var expectedStates = new[] { "AL", "CA", "CO", "FL", "IA" };
            var expectedCounts = new[] { 1, 6, 1, 1, 3 };
            var expectedZips = new[] { "35243", "94153", "81223", "33612", "50801" };

            LoadJSONResource("names_100");

            var STATE = Expression.Property("contact.address.state");
            var gender = Expression.Property("gender");
            var COUNT = Function.Count(Expression.All());
            var zip = Expression.Property("contact.address.zip");
            var MAXZIP = Function.Max(zip);

            using (var q = QueryBuilder.Select(SelectResult.Expression(STATE), SelectResult.Expression(COUNT), SelectResult.Expression(MAXZIP))
                .From(DataSource.Database(Db))
                .Where(gender.EqualTo(Expression.String("female")))
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
            expectedZips = new[] { "94153", "50801", "47952" };

            using (var q = QueryBuilder.Select(SelectResult.Expression(STATE), SelectResult.Expression(COUNT), SelectResult.Expression(MAXZIP))
                .From(DataSource.Database(Db))
                .Where(gender.EqualTo(Expression.String("female")))
                .GroupBy(STATE)
                .Having(COUNT.GreaterThan(Expression.Int(1)))
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

            using (var q = QueryBuilder.Select(SelectResult.Expression(NUMBER1))
                .From(DataSource.Database(Db))
                .Where(NUMBER1.Between(PARAM_N1, PARAM_N2))
                .OrderBy(Ordering.Expression(NUMBER1))) {
                var parameters = new Parameters().SetInt("num1", 2).SetInt("num2", 5);
                q.Parameters = parameters;

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

            var DOC_ID = Meta.ID;
            var DOC_SEQ = Meta.Sequence;
            var NUMBER1 = Expression.Property("number1");

            var RES_DOC_ID = SelectResult.Expression(DOC_ID);
            var RES_DOC_SEQ = SelectResult.Expression(DOC_SEQ);
            var RES_NUMBER1 = SelectResult.Expression(NUMBER1);

            using (var q = QueryBuilder.Select(RES_DOC_ID, RES_DOC_SEQ, RES_NUMBER1)
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Expression(DOC_SEQ))) {
                var expectedDocIDs = new[] {"doc1", "doc2", "doc3", "doc4", "doc5"};
                var expectedSeqs = new[] {1, 2, 3, 4, 5};
                var expectedNumbers = expectedSeqs;

                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var docID = row.GetString(0);
                    var docID2 = row.GetString("id");
                    docID.Should().Be(docID2, "because these calls are two ways of accessing the same info");
                    var seq = row.GetInt(1);
                    var seq2 = row.GetInt("sequence");
                    seq.Should().Be(seq2, "because these calls are two ways of accessing the same info");
                    var number = row.GetInt(2);

                    docID.Should().Be(expectedDocIDs[n - 1]);
                    seq.Should().Be(expectedSeqs[n - 1]);
                    number.Should().Be(expectedNumbers[n - 1]);
                });

                numRows.Should().Be(5);
            }
        }

        [Fact]
        public void TestLimit()
        {
            LoadNumbers(10);

            var LIMIT = Expression.Parameter("limit");
            var NUMBER = Expression.Property("number1");

            using (var q = QueryBuilder.Select(SelectResult.Property("number1"))
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Expression(NUMBER))
                .Limit(Expression.Int(5))) {

                var expectedNumbers = new[] {1, 2, 3, 4, 5};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetInt(0).Should().Be(expectedNumbers[n - 1]);
                });

                numRows.Should().Be(5);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("number1"))
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Expression(NUMBER))
                .Limit(LIMIT)) {
                var parameters = new Parameters().SetInt("limit", 3);
                q.Parameters = parameters;

                var expectedNumbers = new[] {1, 2, 3};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetInt(0).Should().Be(expectedNumbers[n - 1]);
                });

                numRows.Should().Be(3);
            }
        }

        [Fact]
        public void TestLimitOffset()
        {
            LoadNumbers(10);

            var LIMIT = Expression.Parameter("limit");
            var OFFSET = Expression.Parameter("offset");
            var NUMBER = Expression.Property("number1");

            using (var q = QueryBuilder.Select(SelectResult.Property("number1"))
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Expression(NUMBER))
                .Limit(Expression.Int(5), Expression.Int(3))) {

                var expectedNumbers = new[] {4, 5, 6, 7, 8};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetInt(0).Should().Be(expectedNumbers[n - 1]);
                });

                numRows.Should().Be(5);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("number1"))
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Expression(NUMBER))
                .Limit(LIMIT, OFFSET)) {
                var parameters = new Parameters().SetInt("limit", 3).SetInt("offset", 5);
                q.Parameters = parameters;

                var expectedNumbers = new[] {6, 7, 8};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetInt(0).Should().Be(expectedNumbers[n - 1]);
                });

                numRows.Should().Be(3);
            }
        }

        [Fact]
        public void TestQueryResult()
        {
            LoadJSONResource("names_100");

            var FNAME = Expression.Property("name.first");
            var LNAME = Expression.Property("name.last");
            var GENDER = Expression.Property("gender");
            var CITY = Expression.Property("contact.address.city");

            var RES_FNAME = SelectResult.Expression(FNAME).As("firstname");
            var RES_LNAME = SelectResult.Expression(LNAME).As("lastname");
            var RES_GENDER = SelectResult.Expression(GENDER);
            var RES_CITY = SelectResult.Expression(CITY);

            using (var q = QueryBuilder.Select(RES_FNAME, RES_LNAME, RES_GENDER, RES_CITY)
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetValue("firstname").Should().Be(r.GetValue(0));
                    r.GetValue("lastname").Should().Be(r.GetValue(1));
                    r.GetValue("gender").Should().Be(r.GetValue(2));
                    r.GetValue("city").Should().Be(r.GetValue(3));
                });

                numRows.Should().Be(100);
            }
        }

        [Fact]
        public void TestQueryProjectingKeys()
        {
            LoadNumbers(100);

            var avg = SelectResult.Expression(Function.Avg(Expression.Property("number1")));
            var cnt = SelectResult.Expression(Function.Count(Expression.Property("number1")));
            var min = SelectResult.Expression(Function.Min(Expression.Property("number1"))).As("min");
            var max = SelectResult.Expression(Function.Max(Expression.Property("number1")));
            var sum = SelectResult.Expression(Function.Sum(Expression.Property("number1"))).As("sum");
            using (var q = QueryBuilder.Select(avg, cnt, min, max, sum)
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetDouble("$1").Should().Be(r.GetDouble(0));
                    r.GetInt("$2").Should().Be(r.GetInt(1));
                    r.GetInt("min").Should().Be(r.GetInt(2));
                    r.GetInt("$4").Should().Be(r.GetInt(3));
                    r.GetInt("sum").Should().Be(r.GetInt(4));
                });
            }
        }

        [Fact]
        public void TestArrayFunctions()
        {
            using (var doc = new MutableDocument("doc1")) {
                var array = new MutableArrayObject();
                array.AddString("650-123-0001");
                array.AddString("650-123-0002");
                doc.SetArray("array", array);
                Db.Save(doc);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(ArrayFunction.Length(Expression.Property("array"))))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetInt(0).Should().Be(2);
                });

                numRows.Should().Be(1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(ArrayFunction.Contains(Expression.Property("array"), Expression.String("650-123-0001"))),
                    SelectResult.Expression(ArrayFunction.Contains(Expression.Property("array"), Expression.String("650-123-0003"))))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetBoolean(0).Should().BeTrue();
                    r.GetBoolean(1).Should().BeFalse();
                });

                numRows.Should().Be(1);
            }
        }

        [Fact]
        public void TestMathFunctions()
        {
            const double num = 0.6;
            using (var doc = new MutableDocument("doc1")) {
                doc.SetDouble("number", num);
                Db.Save(doc);
            }

            var expectedValues = new[] {
                Math.Abs(num), Math.Acos(num), Math.Asin(num), Math.Atan(num), Math.Atan2(90, num), // Note that Atan2 is (y, x)
                Math.Ceiling(num), Math.Cos(num), num * 180.0 / Math.PI, Math.Exp(num),
                Math.Floor(num), Math.Log(num), Math.Log10(num), Math.Pow(num, 2), num * Math.PI / 180.0,
                Math.Round(num), Math.Round(num, 1), Math.Sign(num), Math.Sin(num), Math.Sqrt(num),
                Math.Tan(num), Math.Truncate(num), Math.Truncate(num * 10.0) / 10.0
            };

            int index = 0;
            var prop = Expression.Property("number");
            foreach (var function in new[] {
                Function.Abs(prop), Function.Acos(prop), Function.Asin(prop), Function.Atan(prop),
                Function.Atan2(prop, Expression.Int(90)), // Note: N1QL function definition is the basis for this call, so (x, y)
                Function.Ceil(prop), Function.Cos(prop), Function.Degrees(prop), Function.Exp(prop),
                Function.Floor(prop),
                Function.Ln(prop), Function.Log(prop), Function.Power(prop, Expression.Int(2)), Function.Radians(prop),
                Function.Round(prop),
                Function.Round(prop, Expression.Int(1)), Function.Sign(prop), Function.Sin(prop), Function.Sqrt(prop),
                Function.Tan(prop),
                Function.Trunc(prop), Function.Trunc(prop, Expression.Int(1))
            }) {
                using (var q = QueryBuilder.Select(SelectResult.Expression(function))
                    .From(DataSource.Database(Db))) {
                    var numRows = VerifyQuery(q, (n, r) =>
                    {
                        r.GetDouble(0).Should().Be(expectedValues[index++]);
                    });

                    numRows.Should().Be(1);
                }
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Function.E().Multiply(Expression.Int(2))),
                    SelectResult.Expression(Function.Pi().Multiply(Expression.Int(2))))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetDouble(0).Should().Be(Math.E * 2);
                    r.GetDouble(1).Should().Be(Math.PI * 2);
                });

                numRows.Should().Be(1);
            }
        }

        [Fact]
        public void TestStringFunctions()
        {
            const string str = "  See you l8r  ";
            using (var doc = new MutableDocument("doc1")) {
                doc.SetString("greeting", str);
                Db.Save(doc);
            }

            var prop = Expression.Property("greeting");
            using (var q = QueryBuilder.Select(SelectResult.Expression(Function.Contains(prop, Expression.String("8"))),
                    SelectResult.Expression(Function.Contains(prop, Expression.String("9"))))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetBoolean(0).Should().BeTrue();
                    r.GetBoolean(1).Should().BeFalse();
                });

                numRows.Should().Be(1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Function.Length(prop)))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetInt(0).Should().Be(str.Length);
                });

                numRows.Should().Be(1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Function.Lower(prop)),
                    SelectResult.Expression(Function.Ltrim(prop)),
                    SelectResult.Expression(Function.Rtrim(prop)),
                    SelectResult.Expression(Function.Trim(prop)),
                    SelectResult.Expression(Function.Upper(prop)))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetString(0).Should().Be(str.ToLowerInvariant());
                    r.GetString(1).Should().Be(str.TrimStart());
                    r.GetString(2).Should().Be(str.TrimEnd());
                    r.GetString(3).Should().Be(str.Trim());
                    r.GetString(4).Should().Be(str.ToUpperInvariant());
                });

                numRows.Should().Be(1);
            }
        }

        [Fact]
        public void TestQuantifiedOperators()
        {
            LoadJSONResource("names_100");

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Database(Db))
                .Where(ArrayExpression.Any(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("climbing"))))) {
                var expected = new[] {"doc-017", "doc-021", "doc-023", "doc-045", "doc-060"};
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id"));
                received.ShouldBeEquivalentTo(expected);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Database(Db))
                .Where(ArrayExpression.Every(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("taxes"))))) {
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id")).ToList();
                received.Count.Should().Be(42, "because empty array results are included");
                received[0].Should().Be("doc-007");
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Database(Db))
                .Where(ArrayExpression.AnyAndEvery(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("taxes"))))) {
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id")).ToList();
                received.Count.Should().Be(0, "because nobody likes taxes...");
            }
        }

        [Fact]
        public void TestQuantifiedOperatorVariableKeyPath()
        {
            var data = new[]
            {
                new[]
                {
                    new Dictionary<string, object> { ["city"] = "San Francisco" },
                    new Dictionary<string, object> { ["city"] = "Palo Alto" },
                    new Dictionary<string, object> { ["city"] = "San Jose" }
                },
                new[]
                {
                    new Dictionary<string, object> { ["city"] = "Mountain View" },
                    new Dictionary<string, object> { ["city"] = "Palo Alto" },
                    new Dictionary<string, object> { ["city"] = "Belmont" }
                },
                new[]
                {
                    new Dictionary<string, object> { ["city"] = "San Francisco" },
                    new Dictionary<string, object> { ["city"] = "Redwood City" },
                    new Dictionary<string, object> { ["city"] = "San Mateo" }
                }
            };

            var i = 0;
            foreach (var cities in data) {
                var docID = $"doc-{i++}";
                using (var doc = new MutableDocument(docID)) {
                    doc.SetValue("paths", cities);
                    var d = JsonConvert.SerializeObject(doc.ToDictionary());
                    WriteLine(d);
                    Db.Save(doc);
                }
            }

            var DOC_ID = Meta.ID;
            var S_DOC_ID = SelectResult.Expression(DOC_ID);

            var PATHS = Expression.Property("paths");
            var VAR_PATH = ArrayExpression.Variable("path.city");
            var where = ArrayExpression.Any(ArrayExpression.Variable("path")).In(PATHS).Satisfies(VAR_PATH.EqualTo(Expression.String("San Francisco")));

            using (var q = QueryBuilder.Select(S_DOC_ID)
                .From(DataSource.Database(Db))
                .Where(where)) {
                var expected = new[] { "doc-0", "doc-2" };
                var numRows = VerifyQuery(q, (n, row) => { row.GetString(0).Should().Be(expected[n - 1]); });
                numRows.Should().Be(expected.Length);
            }
        }

        [Fact]
        public void TestSelectAll()
        {
            LoadNumbers(100);
            using (var q = QueryBuilder.Select(SelectResult.All(), SelectResult.Expression(Expression.Property("number1")))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    var all = r.GetDictionary(0);
                    all.GetInt("number1").Should().Be(n);
                    all.GetInt("number2").Should().Be(100 - n);
                    r.GetInt(1).Should().Be(n);
                });

                numRows.Should().Be(100);
            }

            using (var q = QueryBuilder.Select(SelectResult.All().From("db"), SelectResult.Expression(Expression.Property("number1").From("db")))
                .From(DataSource.Database(Db).As("db"))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    var all = r.GetDictionary(0);
                    all.GetInt("number1").Should().Be(n);
                    all.GetInt("number2").Should().Be(100 - n);
                    r.GetInt(1).Should().Be(n);
                });

                numRows.Should().Be(100);
            }
        }
        
        [Fact]
        public void TestGenerateJSONCollation ()
        {
            var bothSensitive = (QueryCollation)(Collation.Unicode());
            var accentSensitive = (QueryCollation)(Collation.Unicode().IgnoreCase(true));
            var caseSensitive = (QueryCollation)(Collation.Unicode().IgnoreAccents(true));
            var noSensitive = (QueryCollation)(Collation.Unicode().IgnoreCase(true).IgnoreAccents(true));

            var ascii = (QueryCollation)(Collation.ASCII());
            var asciiNoSensitive = (QueryCollation)(Collation.ASCII().IgnoreCase(true));

            var locale = (QueryCollation) Collation.Unicode().Locale("ja");

            bothSensitive.SetOperand(Expression.Property("test") as QueryExpression);
            accentSensitive.SetOperand(Expression.Property("test") as QueryExpression);
            caseSensitive.SetOperand(Expression.Property("test") as QueryExpression);
            noSensitive.SetOperand(Expression.Property("test") as QueryExpression);
            ascii.SetOperand(Expression.Property("test") as QueryExpression);
            asciiNoSensitive.SetOperand(Expression.Property("test") as QueryExpression);
            locale.SetOperand(Expression.Property("test") as QueryExpression);

            bothSensitive.ConvertToJSON().ShouldBeEquivalentTo(new object[] { "COLLATE", new Dictionary<string, object> {
                ["UNICODE"] = true,
				["LOCALE"] = Collation.DefaultLocale
            }, new[] { ".test" }});
            accentSensitive.ConvertToJSON().ShouldBeEquivalentTo(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["UNICODE"] = true,
                ["CASE"] = false,
				["LOCALE"] = Collation.DefaultLocale
            }, new[] { ".test" }});
            caseSensitive.ConvertToJSON().ShouldBeEquivalentTo(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["UNICODE"] = true,
                ["DIAC"] = false,
				["LOCALE"] = Collation.DefaultLocale
            }, new[] { ".test" }});
            noSensitive.ConvertToJSON().ShouldBeEquivalentTo(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["UNICODE"] = true,
                ["DIAC"] = false,
                ["CASE"] = false,
				["LOCALE"] = Collation.DefaultLocale
            }, new[] { ".test" }});
            ascii.ConvertToJSON().ShouldBeEquivalentTo(new object[] { "COLLATE", new Dictionary<string, object>
            {
            }, new[] { ".test" }});
            asciiNoSensitive.ConvertToJSON().ShouldBeEquivalentTo(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["CASE"] = false
            }, new[] { ".test" }});

            locale.ConvertToJSON().ShouldBeEquivalentTo(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["UNICODE"] = true,
                ["LOCALE"] = "ja"
            }, new[] { ".test" }});
        }

        [Fact]
        public void TestUnicodeCollationWithLocale()
        {
            foreach (var letter in new[] {"B", "A", "Z", "Å"}) {
                using (var doc = new MutableDocument()) {
                    doc.SetString("string", letter);
                    Db.Save(doc);
                }
            }

            var stringProp = Expression.Property("string");

            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("string")))
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Expression(stringProp.Collate(Collation.Unicode())))) {
                var results = q.Execute();
                results.Select(x => x.GetString(0)).ShouldBeEquivalentTo(new[] {"A", "Å", "B", "Z"},
                    "because by default Å comes between A and B");
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("string")))
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Expression(stringProp.Collate(Collation.Unicode().Locale("se"))))) {
                var results = q.Execute();
                results.Select(x => x.GetString(0)).ShouldBeEquivalentTo(new[] { "A", "B", "Z", "Å" },
                    "because in Swedish Å comes after Z");
            }
        }

        [Fact]
        public void TestCompareWithUnicodeCollation()
        {
            var bothSensitive = Collation.Unicode();
            var accentSensitive = Collation.Unicode().IgnoreCase(true);
            var caseSensitive = Collation.Unicode().IgnoreAccents(true);
            var noSensitive = Collation.Unicode().IgnoreCase(true).IgnoreAccents(true);

            var testData = new[] {
                // Edge cases: empty and 1-char strings
                Tuple.Create("", "", true, bothSensitive),
                Tuple.Create("", "a", false, bothSensitive),
                Tuple.Create("a", "a", true, bothSensitive),

                // Case sensitive: lowercase comes first by Unicode rules
                Tuple.Create("a", "A", false, bothSensitive),
                Tuple.Create("abc", "abc", true, bothSensitive),
                Tuple.Create("Aaa", "abc", false, bothSensitive),
                Tuple.Create("abc", "abC", false, bothSensitive),
                Tuple.Create("AB", "abc", false, bothSensitive),

                // Case insensitive
                Tuple.Create("ABCDEF", "ZYXWVU", false, accentSensitive),
                Tuple.Create("ABCDEF", "Z", false, accentSensitive),

                Tuple.Create("a", "A", true, accentSensitive),
                Tuple.Create("abc", "ABC", true, accentSensitive),
                Tuple.Create("ABA", "abc", false, accentSensitive),

                Tuple.Create("commonprefix1", "commonprefix2", false, accentSensitive),
                Tuple.Create("commonPrefix1", "commonprefix2", false, accentSensitive),

                Tuple.Create("abcdef", "abcdefghijklm", false, accentSensitive),
                Tuple.Create("abcdeF", "abcdefghijklm", false, accentSensitive),

                //---- Now bring in non-ASCII characters ----:
                Tuple.Create("a", "á", false, accentSensitive),
                Tuple.Create("", "á", false, accentSensitive),
                Tuple.Create("á", "á", true, accentSensitive),
                Tuple.Create("•a", "•A", true, accentSensitive),

                Tuple.Create("test a", "test á", false, accentSensitive),
                Tuple.Create("test á", "test b", false, accentSensitive),
                Tuple.Create("test á", "test Á", true, accentSensitive),
                Tuple.Create("test á1", "test Á2", false, accentSensitive),

                // Case sensitive, diacritic sensitive:
                Tuple.Create("ABCDEF", "ZYXWVU", false, bothSensitive),
                Tuple.Create("ABCDEF", "Z", false, bothSensitive),
                Tuple.Create("a", "A", false, bothSensitive),
                Tuple.Create("abc", "ABC", false, bothSensitive),
                Tuple.Create("•a", "•A", false, bothSensitive),
                Tuple.Create("test a", "test á", false, bothSensitive),
                Tuple.Create("Ähnlichkeit", "apple", false, bothSensitive), // Because 'h'-vs-'p' beats 'Ä'-vs-'a'
                Tuple.Create("ax", "Äz", false, bothSensitive),
                Tuple.Create("test a", "test Á", false, bothSensitive),
                Tuple.Create("test Á", "test e", false, bothSensitive),
                Tuple.Create("test á", "test Á", false, bothSensitive),
                Tuple.Create("test á", "test b", false, bothSensitive),
                Tuple.Create("test u", "test Ü", false, bothSensitive),

                // Case sensitive, diacritic insensitive
                Tuple.Create("abc", "ABC", false, caseSensitive),
                Tuple.Create("test á", "test a", true, caseSensitive),
                Tuple.Create("test á", "test A", false, caseSensitive),
                Tuple.Create("test á", "test b", false, caseSensitive),
                Tuple.Create("test á", "test Á", false, caseSensitive),

                // Case and diacritic insensitive
                Tuple.Create("test á", "test Á", true, noSensitive)
            };

            int i = 0;
            foreach (var data in testData) {
                using (var doc = new MutableDocument()) {
                    doc.SetString("value", data.Item1);
                    Db.Save(doc);

                    var comparison = data.Item3
                        ? Expression.Property("value").Collate(data.Item4).EqualTo(Expression.String(data.Item2))
                        : Expression.Property("value").Collate(data.Item4).LessThan(Expression.String(data.Item2));

                    using (var q = QueryBuilder.Select(SelectResult.All())
                        .From(DataSource.Database(Db))
                        .Where(comparison)) {
                        var result = q.Execute();
                        result.Should().HaveCount(1,
                            $"because otherwise the comparison failed for {data.Item1} and {data.Item2} (position {i})");
                    }

                    Db.Delete(Db.GetDocument(doc.Id));
                }

                i++;
            }
        }

        [Fact]
        public void TestAllComparison()
        {
            foreach (var val in new[] {"Apple", "Aardvark", "Ångström", "Zebra", "äpple"}) {
                using (var doc = new MutableDocument()) {
                    doc.SetString("hey", val);
                    Db.Save(doc);
                }
            }

            var testData = new[] {
                Tuple.Create("BINARY collation", (ICollation) Collation.ASCII(),
                    new[] {"Aardvark", "Apple", "Zebra", "Ångström", "äpple"}),
                Tuple.Create("NOCASE collation", (ICollation) Collation.ASCII().IgnoreCase(true),
                    new[] {"Aardvark", "Apple", "Zebra", "Ångström", "äpple"}),
                Tuple.Create("Unicode case-sensitive, diacritic-sensitive collation", (ICollation) Collation.Unicode(),
                    new[] {"Aardvark", "Ångström", "Apple", "äpple", "Zebra"}),
                Tuple.Create("Unicode case-INsensitive, diacritic-sensitive collation",
                    (ICollation) Collation.Unicode().IgnoreCase(true),
                    new[] {"Aardvark", "Ångström", "Apple", "äpple", "Zebra"}),
                Tuple.Create("Unicode case-sensitive, diacritic-INsensitive collation",
                    (ICollation) Collation.Unicode().IgnoreAccents(true),
                    new[] {"Aardvark", "Ångström", "äpple", "Apple", "Zebra"}),
                Tuple.Create("Unicode case-INsensitive, diacritic-INsensitive collation",
                    (ICollation) Collation.Unicode().IgnoreAccents(true).IgnoreCase(true),
                    new[] {"Aardvark", "Ångström", "Apple", "äpple", "Zebra"})
            };

            var property = Expression.Property("hey");
            foreach (var data in testData) {
                WriteLine(data.Item1);
                using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("hey")))
                    .From(DataSource.Database(Db))
                    .OrderBy(Ordering.Expression(property.Collate(data.Item2)))) {
                    var results = q.Execute();
                    results.Select(x => x.GetString(0)).Should().ContainInOrder(data.Item3);
                }
            }
        }

        [Fact]
        public void TestLiveQueryBlocksClose()
        {
            var otherDb = new Database(Db.Name, Db.Config);
            var query = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Database(otherDb));
            var doc1Listener = new WaitAssert();
            var token = query.AddChangeListener(null, (sender, args) =>
            {
                foreach (var row in args.Results) {
                    if (row.GetString("id") == "doc1") {
                        doc1Listener.Fulfill();
                    }
                }
            });

            try {
                using (var doc = new MutableDocument("doc1")) {
                    doc.SetString("value", "string");
                    Db.Save(doc); // Should still trigger since it is pointing to the same DB
                }

                doc1Listener.WaitForResult(TimeSpan.FromSeconds(20));
                otherDb.Invoking(d => d.Dispose())
                    .ShouldThrow<CouchbaseLiteException>("because the live query is still active");
            } finally {
                query.RemoveChangeListener(token);
                query.Dispose();
                otherDb.Dispose();
            }
        }

        [ForIssue("couchbase-lite-android/1356")]
        [Fact]
        public void TestCountFunctions()
        {
            LoadNumbers(100);

            var ds = DataSource.Database(Db);
            var cnt = Function.Count(Expression.Property("number1"));
            var rsCnt = SelectResult.Expression(cnt);
            using (var q = QueryBuilder.Select(rsCnt).From(ds)) {
                var numRows = VerifyQuery(q, (n, row) => { row.GetInt(0).Should().Be(100); });
                numRows.Should().Be(1);
            }
        }

        [Fact]
        public void TestJoinWithArrayContains()
        {
            using (var hotel1 = new MutableDocument("hotel1"))
            using (var hotel2 = new MutableDocument("hotel2"))
            using (var hotel3 = new MutableDocument("hotel3"))
            using (var bookmark1 = new MutableDocument("bookmark1"))
            using (var bookmark2 = new MutableDocument("bookmark2")) {
                hotel1.SetString("type", "hotel");
                hotel1.SetString("name", "Hilton");
                Db.Save(hotel1);

                hotel2.SetString("type", "hotel");
                hotel2.SetString("name", "Sheraton");
                Db.Save(hotel2);

                hotel3.SetString("type", "hotel");
                hotel3.SetString("name", "Marriot");
                Db.Save(hotel3);

                bookmark1.SetString("type", "bookmark");
                bookmark1.SetString("title", "Bookmark for Hawaii");
                var hotels1 = new MutableArrayObject();
                hotels1.AddString("hotel1").AddString("hotel2");
                bookmark1.SetArray("hotels", hotels1);
                Db.Save(bookmark1);

                bookmark2.SetString("type", "bookmark");
                bookmark2.SetString("title", "Bookmark for New York");
                var hotels2 = new MutableArrayObject();
                hotels2.AddString("hotel3");
                bookmark2.SetArray("hotels", hotels2);
                Db.Save(bookmark2);
            }

            var mainDS = DataSource.Database(Db).As("main");
            var secondaryDS = DataSource.Database(Db).As("secondary");

            var typeExpr = Expression.Property("type").From("main");
            var hotelsExpr = Expression.Property("hotels").From("main");
            var hotelIdExpr = Meta.ID.From("secondary");
            var joinExpr = ArrayFunction.Contains(hotelsExpr, hotelIdExpr);
            var join = Join.InnerJoin(secondaryDS).On(joinExpr);

            var srMainAll = SelectResult.All().From("main");
            var srSecondaryAll = SelectResult.All().From("secondary");
            using (var q = QueryBuilder.Select(srMainAll, srSecondaryAll)
                .From(mainDS)
                .Join(join)
                .Where(typeExpr.EqualTo(Expression.String("bookmark")))) {
                var rs = q.Execute();
                foreach (var r in rs) {
                    WriteLine(JsonConvert.SerializeObject(r.ToDictionary()));
                }
            }
        }

        [ForIssue("couchbase-lite-android/1385")]
        [Fact]
        public void TestQueryDeletedDocument()
        {
            using (var task1 = CreateTaskDocument("Task 1", false))
            using (var task2 = CreateTaskDocument("Task 2", false)) {
                using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID), SelectResult.All())
                    .From(DataSource.Database(Db))
                    .Where(Expression.Property("type").EqualTo(Expression.String("task")))) {
                    var rs = q.Execute();
                    var counter = 0;
                    foreach (var r in rs) {
                        WriteLine($"Round 1: Result -> {JsonConvert.SerializeObject(r.ToDictionary())}");
                        counter++;
                    }

                    counter.Should().Be(2);

                    task1.IsDeleted.Should().BeFalse();
                    Db.Delete(task1);
                    Db.Count.Should().Be(1);
                    Db.GetDocument(task1.Id).Should().BeNull();

                    rs = q.Execute();
                    counter = 0;
                    foreach (var r in rs) {
                        r.GetString(0).Should().Be(task2.Id);
                        WriteLine($"Round 2: Result -> {JsonConvert.SerializeObject(r.ToDictionary())}");
                        counter++;
                    }

                    counter.Should().Be(1);
                }
            }
        }

        [ForIssue("couchbase-lite-android/1389")]
        [Fact]
        public void TestQueryWhereBooleanExpression()
        {
            using (var task1 = CreateTaskDocument("Task 1", false))
            using (var task2 = CreateTaskDocument("Task 2", true))
            using (var task3 = CreateTaskDocument("Task 3", true)) {
                Db.Count.Should().Be(3);

                var exprType = Expression.Property("type");
                var exprComplete = Expression.Property("complete");
                var srCount = SelectResult.Expression(Function.Count(Expression.All()));

                using (var q = QueryBuilder.Select(SelectResult.All())
                    .From(DataSource.Database(Db))
                    .Where(exprType.EqualTo(Expression.String("task")).And(exprComplete.EqualTo(Expression.Boolean(true))))) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        WriteLine($"res -> {JsonConvert.SerializeObject(row.ToDictionary())}");
                        var dict = row.GetDictionary(Db.Name);
                        dict.GetBoolean("complete").Should().BeTrue();
                        dict.GetString("type").Should().Be("task");
                        dict.GetString("title").Should().StartWith("Task ");
                    });

                    numRows.Should().Be(2);
                }

                using (var q = QueryBuilder.Select(SelectResult.All())
                    .From(DataSource.Database(Db))
                    .Where(exprType.EqualTo(Expression.String("task")).And(exprComplete.EqualTo(Expression.Boolean(false))))) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        WriteLine($"res -> {JsonConvert.SerializeObject(row.ToDictionary())}");
                        var dict = row.GetDictionary(Db.Name);
                        dict.GetBoolean("complete").Should().BeFalse();
                        dict.GetString("type").Should().Be("task");
                        dict.GetString("title").Should().StartWith("Task ");
                    });

                    numRows.Should().Be(1);
                }

                using (var q = QueryBuilder.Select(srCount)
                    .From(DataSource.Database(Db))
                    .Where(exprType.EqualTo(Expression.String("task")).And(exprComplete.EqualTo(Expression.Boolean(true))))) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        WriteLine($"res -> {JsonConvert.SerializeObject(row.ToDictionary())}");
                        row.GetInt(0).Should().Be(2);
                    });

                    numRows.Should().Be(1);
                }

                using (var q = QueryBuilder.Select(srCount)
                    .From(DataSource.Database(Db))
                    .Where(exprType.EqualTo(Expression.String("task")).And(exprComplete.EqualTo(Expression.Boolean(false))))) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        WriteLine($"res -> {JsonConvert.SerializeObject(row.ToDictionary())}");
                        row.GetInt(0).Should().Be(1);
                    });

                    numRows.Should().Be(1);
                }
            }
        }

        [ForIssue("couchbase-lite-android/1413")]
        [Fact]
        public void TestJoinAll()
        {
            LoadNumbers(100);

            using (var doc1 = new MutableDocument("joinme")) {
                doc1.SetInt("theone", 42);
                Db.Save(doc1);
            }

            var mainDS = DataSource.Database(Db).As("main");
            var secondaryDS = DataSource.Database(Db).As("secondary");

            var mainPropExpr = Expression.Property("number1").From("main");
            var secondaryExpr = Expression.Property("theone").From("secondary");
            var joinExpr = mainPropExpr.EqualTo(secondaryExpr);
            var join = Join.InnerJoin(secondaryDS).On(joinExpr);

            var mainAll = SelectResult.All().From("main");
            var secondaryAll = SelectResult.All().From("secondary");

            using (var q = QueryBuilder.Select(mainAll, secondaryAll)
                .From(mainDS)
                .Join(join)) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var mainAll1 = row.GetDictionary(0);
                    var mainAll2 = row.GetDictionary("main");
                    var secondAll1 = row.GetDictionary(1);
                    var secondAll2 = row.GetDictionary("secondary");
                    WriteLine($"mainAll1 -> {JsonConvert.SerializeObject(mainAll1)}");
                    WriteLine($"mainAll2 -> {JsonConvert.SerializeObject(mainAll2)}");
                    WriteLine($"secondAll1 -> {JsonConvert.SerializeObject(secondAll1)}");
                    WriteLine($"secondAll2 -> {JsonConvert.SerializeObject(secondAll2)}");

                    mainAll1.GetInt("number1").Should().Be(42);
                    mainAll2.GetInt("number1").Should().Be(42);
                    mainAll1.GetInt("number2").Should().Be(58);
                    mainAll1.GetInt("number2").Should().Be(58);
                    secondAll1.GetInt("theone").Should().Be(42);
                    secondAll2.GetInt("theone").Should().Be(42);
                });

                numRows.Should().Be(1);
            }
        }

        [ForIssue("couchbase-lite-android/1413")]
        [Fact]
        public void TestJoinByDocID()
        {
            LoadNumbers(100);

            using (var doc1 = new MutableDocument("joinme")) {
                doc1.SetInt("theone", 42);
                doc1.SetString("numberID", "doc1");
                Db.Save(doc1);
            }

            var mainDS = DataSource.Database(Db).As("main");
            var secondaryDS = DataSource.Database(Db).As("secondary");

            var mainPropExpr = Meta.ID.From("main");
            var secondaryExpr = Expression.Property("numberID").From("secondary");
            var joinExpr = mainPropExpr.EqualTo(secondaryExpr);
            var join = Join.InnerJoin(secondaryDS).On(joinExpr);

            var mainDocID = SelectResult.Expression(mainPropExpr).As("mainDocID");
            var secondaryDocID = SelectResult.Expression(Meta.ID.From("secondary")).As("secondaryDocID");
            var secondaryTheOne = SelectResult.Expression(Expression.Property("theone").From("secondary"));

            using (var q = QueryBuilder.Select(mainDocID, secondaryDocID, secondaryTheOne)
                .From(mainDS)
                .Join(join)) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    n.Should().Be(1);
                    var docID = row.GetString("mainDocID");
                    using (var doc = Db.GetDocument(docID)) {
                        doc.GetInt("number1").Should().Be(1);
                        doc.GetInt("number2").Should().Be(99);

                        row.GetString("secondaryDocID").Should().Be("joinme");
                        row.GetInt("theone").Should().Be(42);
                    }
                });

                numRows.Should().Be(1);
            }
        }

        [Fact]
        public void TestMissingValues()
        {
            using (var doc1 = new MutableDocument("joinme")) {
                doc1.SetInt("theone", 42);
                doc1.SetString("numberID", "doc1");
                doc1.SetString("nullval", null);
                Db.Save(doc1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("name"), SelectResult.Property("nullval"))
                .From(DataSource.Database(Db))) {
                var results = q.Execute();
                foreach (var result in results) {
                    result.Count.Should().Be(1);
                    result.Contains("name").Should().BeFalse();
                    result.GetString("name").Should().BeNull();
                    result.GetValue("name").Should().BeNull();
                    result.GetString(0).Should().BeNull();
                    result.GetValue(0).Should().BeNull();
                    result.Contains("nullval").Should().BeTrue();
                    result.GetString("nullval").Should().BeNull();
                    result.GetValue("nullval").Should().BeNull();
                    result.GetString(1).Should().BeNull();
                    result.GetValue(1).Should().BeNull();

                    result.ToList<object>().Should().ContainInOrder(new object[] { null });
                    result.ToDictionary().ShouldBeEquivalentTo(new Dictionary<string, object>
                    {
                        ["nullval"] = null
                    });
                }
            }
        }

        [Fact]
        public void TestQueryParameters()
        {
            var now = DateTimeOffset.UtcNow;
            var builder = new Parameters()
                .SetBoolean("true", true)
                .SetDate("now", now)
                .SetDouble("pi", Math.PI)
                .SetFloat("simple_pi", 3.14159f)
                .SetLong("big_num", Int64.MaxValue)
                .SetString("name", "Jim");

            builder.Invoking(b => b.SetValue("bad", new[] { 1, 2, 3 })).ShouldThrow<ArgumentException>();

            var parameters = builder;
            parameters.GetValue("true").As<bool>().Should().BeTrue();
            parameters.GetValue("now").As<DateTimeOffset>().Should().Be(now);
            parameters.GetValue("pi").As<double>().Should().Be(Math.PI);
            parameters.GetValue("simple_pi").As<float>().Should().Be(3.14159f);
            parameters.GetValue("big_num").As<long>().Should().Be(Int64.MaxValue);
            parameters.GetValue("name").As<string>().Should().Be("Jim");
        }

        [Fact]
        public void TestQueryResultTypes()
        {
            var blobContent = Encoding.ASCII.GetBytes("The keys to the kingdom");
            var array = new MutableArrayObject(new[] { 1, 2, 3 });
            var now = DateTimeOffset.UtcNow;
            using (var doc = new MutableDocument("test_doc")) {
                doc.SetArray("array", array);
                doc.SetBlob("blob", new Blob("text/plain", blobContent));
                doc.SetDate("created_at", now);
                doc.SetFloat("simple_pi", 3.14159f);
                doc.SetLong("big_num", Int64.MaxValue);
                doc.SetBoolean("boolean", true);
                Db.Save(doc);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("array"),
                    SelectResult.Property("blob"),
                    SelectResult.Property("created_at"),
                    SelectResult.Property("simple_pi"),
                    SelectResult.Property("big_num"),
                    SelectResult.Property("boolean"))
                .From(DataSource.Database(Db))) {
                VerifyQuery(q, (n, row) =>
                {
                    row.GetArray(0).Should().ContainInOrder(1L, 2L, 3L);
                    row.GetArray("array").Should().ContainInOrder(1L, 2L, 3L);
                    row.GetBlob(1).Content.Should().ContainInOrder(blobContent);
                    row.GetBlob("blob").Content.Should().ContainInOrder(blobContent);
                    row.GetDate(2).Should().Be(now);
                    row.GetDate("created_at").Should().Be(now);
                    row.GetFloat(3).Should().Be(3.14159f);
                    row.GetFloat("simple_pi").Should().Be(3.14159f);
                    row.GetLong(4).Should().Be(Int64.MaxValue);
                    row.GetLong("big_num").Should().Be(Int64.MaxValue);

                    row[4].Long.Should().Be(Int64.MaxValue);
                    row["big_num"].Long.Should().Be(Int64.MaxValue);
                    row.GetBoolean(5).Should().Be(true);
                    row.GetBoolean("boolean").Should().Be(true);

                    var resultList = row.ToList();
                    resultList.Count.Should().Be(6);
                    resultList.ElementAtOrDefault(2).Should().Be(now.ToString("o"));
                });
            }
        }

        [Fact]
        public void TestFTSStemming()
        {
            // Can't rely on the default locale, it could be anything and that would fail the test
            Db.CreateIndex("passageIndex", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("passage")).SetLanguage("en"));
            Db.CreateIndex("passageIndexStemless", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("passage")).SetLanguage(null));

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("passage", "The boy said to the child, 'Mommy, I want a cat.'");
                Db.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("passage", "The mother replied 'No, you already have too many cats.'");
                Db.Save(doc2);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Database(Db))
                .Where(FullTextExpression.Index("passageIndex").Match("cat"))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).Should().Be($"doc{n}");
                });
                count.Should().Be(2);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Database(Db))
                .Where(FullTextExpression.Index("passageIndexStemless").Match("cat"))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).Should().Be($"doc{n}");
                });
                count.Should().Be(1);
            }
        }

        [Fact]
        public void TestQueryExpressions()
        {
            var doubleValue = Math.PI;
            var floatValue = 1.5F;
            var longValue = 4294967296L;
            var value = (object)"stringObject";

            DictionaryObject[] resultsDouble;
            DictionaryObject[] resultsFloat;
            DictionaryObject[] resultsLong;
            DictionaryObject[] resultsValue;

            using (var document = new MutableDocument("ExpressionTypes")) {
                document.SetDouble("doubleValue", doubleValue);
                Db.Save(document);

                document.SetFloat("floatValue", floatValue);
                Db.Save(document);

                document.SetLong("longValue", longValue);
                Db.Save(document);

                document.SetValue("value", value);
                Db.Save(document);
            }

            var v = Db.GetDocument("ExpressionTypes").GetDouble("doubleValue");

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("doubleValue").EqualTo(Expression.Double(doubleValue))
                )) {
                resultsDouble = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("floatValue").EqualTo(Expression.Float(floatValue))
                )) {
                resultsFloat = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("longValue").EqualTo(Expression.Long(longValue))
                )) {
                resultsLong = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("value").EqualTo(Expression.Value(value))
                )) {
                resultsValue = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            resultsDouble.Length.ShouldBeEquivalentTo(1);
            resultsFloat.Length.ShouldBeEquivalentTo(1);
            resultsLong.Length.ShouldBeEquivalentTo(1);
            resultsValue.Length.ShouldBeEquivalentTo(1);
        }

        [Fact]
        public void TestNullResultSet()
        {
            NullResultSet resultset = new NullResultSet();
            var resultCnt = resultset.Count();
            var current = resultset?.Current;
            var enumerator = resultset?.GetEnumerator();
            var canMoveNext = resultset?.MoveNext();
            var allresult = resultset?.AllResults();
            //resultset.Dispose();
        }

        [Fact]
        public void TestQueryExpression()
        {
            var dto1 = 168;
            var dto2 = 68;

            DictionaryObject[] results1;
            DictionaryObject[] results2;

            using (var document = new MutableDocument("TestQueryExpression")) {
                document.SetInt("onesixeight", dto1);
                Db.Save(document);
            }

            using (var from = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))) {
                var l = from.Execute().ToArray();
                l.Count().Should().Be(1);
            }
;
            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("onesixeight").Is(Expression.Int(dto1))
                )) {
                results1 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("onesixeight").IsNot(Expression.Int(dto2))
                )) {
                results2 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            results1.Length.ShouldBeEquivalentTo(1);
            results2.Length.ShouldBeEquivalentTo(1);
        }

        [Fact]
        public void TestQueryResultSet()
        {
            QueryResultSet resultset;
            var dto1 = DateTimeOffset.UtcNow;
            using (var document = new MutableDocument("TestQueryResultSet")) {
                document.SetDate("timestamp", dto1);
                Db.Save(document);
            }
            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("timestamp").EqualTo(Expression.Date(dto1))
                )) {
                resultset = (QueryResultSet)query.Execute();
                resultset.Database.Should().Be(Db);
                List<Result> allRes = resultset.AllResults();
                allRes.Count.Should().Be(1);
                var columnName = resultset.ColumnNames;
            }
            resultset.Refresh();
            var queryTypeExpression = new QueryTypeExpression("doubleValue", ExpressionType.KeyPath);
            var method = queryTypeExpressionType.GetMethod("CalculateKeyPath", BindingFlags.NonPublic | BindingFlags.Instance);
            var res = method.Invoke(queryTypeExpression, null);
        }

        [ForIssue("#1052")]
        [Fact]
        public void TestQueryDateTimeOffset()
        {
            var dto1 = DateTimeOffset.UtcNow;
            var dto2 = new DateTimeOffset(15, new TimeSpan(0));
            var dto3 = new DateTimeOffset(15000, new TimeSpan(0));

            DictionaryObject[] results1;
            DictionaryObject[] results2;
            DictionaryObject[] results3;

            using (var document = new MutableDocument("TestQueryDateTimeOffset.1")) {
                document.SetDate("timestamp", dto1);
                Db.Save(document);
            }
            using (var document = new MutableDocument("TestQueryDateTimeOffset.2")) {
                document.SetDate("timestamp", dto2);
                Db.Save(document);
            }
            using (var document = new MutableDocument("TestQueryDateTimeOffset.3")) {
                document.SetDate("timestamp", dto3);
                Db.Save(document);
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("timestamp").EqualTo(Expression.Date(dto1))
                )) {
                results1 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("timestamp").EqualTo(Expression.Date(dto2))
                )) {
                results2 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Database(Db))
                .Where(
                    Expression.Property("timestamp").EqualTo(Expression.Date(dto3))
                )) {
                results3 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            results1.Length.ShouldBeEquivalentTo(1);
            results2.Length.ShouldBeEquivalentTo(1);
            results3.Length.ShouldBeEquivalentTo(1);
        }

        [ForIssue("couchbase-lite-core/497")]
        [Fact]
        public void TestQueryJoinAndSelectAll()
        {
            LoadNumbers(100);

            using (var doc1 = new MutableDocument("joinme")) {
                doc1.SetInt("theone", 42);
                Db.Save(doc1);
            }

            var mainDS = DataSource.Database(Db).As("main");
            var secondaryDS = DataSource.Database(Db).As("secondary");

            var mainPropExpr = Expression.Property("number1").From("main");
            var secondaryExpr = Expression.Property("theone").From("secondary");
            var joinExpr = mainPropExpr.EqualTo(secondaryExpr);
            var join = Join.LeftJoin(secondaryDS).On(joinExpr);

            var mainAll = SelectResult.All().From("main");
            var secondaryAll = SelectResult.All().From("secondary");

            using (var q = QueryBuilder.Select(mainAll, secondaryAll)
                .From(mainDS)
                .Join(join)) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    if (n == 41) {
                        WriteLine($"41: {JsonConvert.SerializeObject(row.ToDictionary())}");
                        row.GetDictionary("main").GetInt("number2").Should().Be(59);
                        row.GetDictionary("secondary").Should().BeNull();
                    } else if (n == 42) {
                        WriteLine($"42: {JsonConvert.SerializeObject(row.ToDictionary())}");
                        row.GetDictionary("main").GetInt("number2").Should().Be(58);
                        row.GetDictionary("secondary").GetInt("theone").Should().Be(42);
                    }
                });

                numRows.Should().Be(101);
            }
        }

        [Fact]
        public void TestStringToMillis()
        {
            CreateDateDocs();

            var selections = new ISelectResult[]
            {
                SelectResult.Expression(Function.StringToMillis(Expression.Property("local"))),
                SelectResult.Expression(Function.StringToMillis(Expression.Property("JST"))),
                SelectResult.Expression(Function.StringToMillis(Expression.Property("JST2"))),
                SelectResult.Expression(Function.StringToMillis(Expression.Property("PST"))),
                SelectResult.Expression(Function.StringToMillis(Expression.Property("PST2"))),
                SelectResult.Expression(Function.StringToMillis(Expression.Property("UTC")))
            };

            var expectedJST = new[] { 0L, 499105260000, 499105290000, 499105290500, 499105290550, 499105290555 };
            var expectedPST = new[] { 0L, 499166460000, 499166490000, 499166490500, 499166490550, 499166490555 };
            var expectedUTC = new[] { 0L, 499137660000, 499137690000, 499137690500, 499137690550, 499137690555 };
            var expectedLocal = new List<long>();

            var offset = (long)TimeZoneInfo.Local.BaseUtcOffset.TotalMilliseconds;
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(499132800000);
            if (TimeZoneInfo.Local.IsDaylightSavingTime(dto)) {
                offset += 3600000;
            }

            expectedLocal.Add(499132800000 - offset); // 499132800000 is 2018-10-26T00:00:00Z
            foreach (var entry in expectedUTC.Skip(1)) {
                expectedLocal.Add(entry - offset);
            }

            var i = 0;
            using (
                var q = QueryBuilder.Select(selections)
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Property("local").Ascending())) {
                foreach (var result in q.Execute()) {
                    result.GetLong(0).Should().Be(expectedLocal[i]);
                    result.GetLong(1).Should().Be(expectedJST[i]);
                    result.GetLong(2).Should().Be(expectedJST[i]);
                    result.GetLong(3).Should().Be(expectedPST[i]);
                    result.GetLong(4).Should().Be(expectedPST[i]);
                    result.GetLong(5).Should().Be(expectedUTC[i]);
                    i++;
                }
            }
        }

        [Fact]
        public void TestStringToUTC()
        {
            CreateDateDocs();

            var selections = new ISelectResult[]
            {
                SelectResult.Expression(Function.StringToUTC(Expression.Property("local"))),
                SelectResult.Expression(Function.StringToUTC(Expression.Property("JST"))),
                SelectResult.Expression(Function.StringToUTC(Expression.Property("JST2"))),
                SelectResult.Expression(Function.StringToUTC(Expression.Property("PST"))),
                SelectResult.Expression(Function.StringToUTC(Expression.Property("PST2"))),
                SelectResult.Expression(Function.StringToUTC(Expression.Property("UTC")))
            };

            var expectedJST = new[] { null, "1985-10-25T16:21:00Z", "1985-10-25T16:21:30Z", "1985-10-25T16:21:30.500Z", 
                "1985-10-25T16:21:30.550Z", "1985-10-25T16:21:30.555Z" };
            var expectedPST = new[] { null, "1985-10-26T09:21:00Z", "1985-10-26T09:21:30Z", "1985-10-26T09:21:30.500Z", 
                "1985-10-26T09:21:30.550Z", "1985-10-26T09:21:30.555Z" };
            var expectedUTC = new[] { null, "1985-10-26T01:21:00Z", "1985-10-26T01:21:30Z", "1985-10-26T01:21:30.500Z",
                "1985-10-26T01:21:30.550Z", "1985-10-26T01:21:30.555Z" };
            var expectedLocal = (new[]
            {
                "1985-10-26", "1985-10-26 01:21", "1985-10-26 01:21:30", "1985-10-26 01:21:30.5",
                "1985-10-26 01:21:30.55", "1985-10-26 01:21:30.555"
            }).Select(x => DateTimeOffset.Parse(x).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ").Replace(".000",""));

            var i = 0;
            using (var q = QueryBuilder.Select(selections)
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Property("local").Ascending())) {
                foreach (var result in q.Execute()) {
                    result.GetString(0).Should().Be(expectedLocal.ElementAt(i));
                    result.GetString(1).Should().Be(expectedJST[i]);
                    result.GetString(2).Should().Be(expectedJST[i]);
                    result.GetString(3).Should().Be(expectedPST[i]);
                    result.GetString(4).Should().Be(expectedPST[i]);
                    result.GetString(5).Should().Be(expectedUTC[i]);
                    i++;
                }
            }
        }

        [Fact]
        public void TestMillisConversion()
        {
            var millisToUse = new[] { 499132800000, 499137660000, 499137690000, 499137690500, 499137690550, 499137690555 };
            foreach (var millis in millisToUse) {
                using (var doc = new MutableDocument()) {
                    doc.SetLong("timestamp", millis);
                    Db.Save(doc);
                }
            }

            var expectedUTC = new[]
            {
                "1985-10-26T00:00:00Z", "1985-10-26T01:21:00Z", "1985-10-26T01:21:30Z", 
                "1985-10-26T01:21:30.500Z", "1985-10-26T01:21:30.550Z", "1985-10-26T01:21:30.555Z"
            };

            var expectedLocal = millisToUse.Select(x =>
            {
                var date = DateTimeOffset.FromUnixTimeMilliseconds(x).ToLocalTime();
                if (date.Offset == TimeSpan.Zero) {
                    return date.ToString("yyyy-MM-ddTHH:mm:ss.fffZ").Replace(".000", "");
                }


                var almost = date.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz").Replace(".000", "");
                return almost.Remove(almost.Length - 3, 1); // Remove colon since formatting cannot do it
            });

            var selections = new ISelectResult[]
            {
                SelectResult.Expression(Function.MillisToString(Expression.Property("timestamp"))),
                SelectResult.Expression(Function.MillisToUTC(Expression.Property("timestamp"))),
            };

            var i = 0;
            using (var q = QueryBuilder.Select(selections)
                .From(DataSource.Database(Db))
                .OrderBy(Ordering.Property("timestamp").Ascending())) {
                foreach (var result in q.Execute()) {
                    result.GetString(0).Should().Be(expectedLocal.ElementAt(i));
                    result.GetString(1).Should().Be(expectedUTC[i]);
                    i++;
                }
            }
        }

        private void CreateDateDocs()
        {
            using (var doc = new MutableDocument()) {
                doc.SetString("local", "1985-10-26");
                Db.Save(doc);
            }

            var dateTimeFormats = new[]
            {
                "1985-10-26T01:21", "1985-10-26T01:21:30", "1985-10-26T01:21:30.5",
                "1985-10-26T01:21:30.55", "1985-10-26T01:21:30.555"
            };

            
            foreach (var format in dateTimeFormats) {
                using (var doc = new MutableDocument()) {
                    doc.SetString("local", format);
                    doc.SetString("JST", format + "+09:00");
                    doc.SetString("JST2", format + "+0900");
                    doc.SetString("PST", format + "-08:00");
                    doc.SetString("PST2", format + "-0800");
                    doc.SetString("UTC", format + "Z");
                    Db.Save(doc);
                }
            }
        }

        private Document CreateTaskDocument(string title, bool complete)
        {
            var doc = new MutableDocument();
            doc.SetString("type", "task");
            doc.SetString("title", title);
            doc.SetBoolean("complete", complete);
            Db.Save(doc);
            return doc;
        }

        private async Task TestLiveQueryNoUpdateInternal(bool consumeAll)
        {
            LoadNumbers(100);
            using (var q = QueryBuilder.Select().From(DataSource.Database(Db))
                .Where(Expression.Property("number1").LessThan(Expression.Int(10))).OrderBy(Ordering.Property("number1"))) {

                var are = new AutoResetEvent(false);
                var token = q.AddChangeListener(null, (sender, args) =>
                {
                    if (consumeAll) {
                        var rs = args.Results;
                        rs.ToArray().Should().NotBeNull(); // No-op
                    }

                    are.Set();
                });

                await Task.Delay(500);

                try {
                    // This change will not affect the query results because 'number1 < 10' 
                    // is not true
                    CreateDocInSeries(111, 100);
                    are.WaitOne(5000).Should()
                        .BeTrue("because the Changed event should fire once for the initial results");
                    are.WaitOne(5000).Should().BeFalse("because the Changed event should not fire needlessly");
                } finally {
                    q.RemoveChangeListener(token);
                }
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
                using (var q = QueryBuilder.Select(DocID).From(DataSource.Database(Db)).Where(c.Item1)) {
                    var lastN = 0;
                    VerifyQuery(q, (n, row) =>
                    {
                        var doc = Db.GetDocument(row.GetString(0));
                        var props =doc.ToDictionary();
                        c.Item2(props, c.Item3).Should().BeTrue("because otherwise the row failed validation");
                        lastN = n;
                    });

                    lastN.Should()
                        .Be(expectedResultCount[index++], "because otherwise there was an incorrect number of rows");
                }
            }
        }

        private Document CreateDocInSeries(int entry, int max)
        {
            var docID = $"doc{entry}";
            var doc = new MutableDocument(docID);
            doc.SetInt("number1", entry);
            doc.SetInt("number2", max - entry);
            Db.Save(doc);
            return doc;
        }
    }
}
