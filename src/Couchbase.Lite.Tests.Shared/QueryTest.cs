﻿//
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Query;

using Shouldly;

using Newtonsoft.Json;

using Test.Util;
using Xunit;
using Xunit.Abstractions;

namespace Test
{

    public class QueryTest : TestCase
    {
        public enum QueryTestMode
        {
            SQL,
            QueryBuilder
        }

        Type queryTypeExpressionType = typeof(QueryTypeExpression);

        public QueryTest(ITestOutputHelper output) : base(output)
        {

        }

        [Fact]
        public void TestMetaRevisionID()
        {
            var docId = "doc1";
            // Create doc:
            using (var doc = new MutableDocument(docId)) {
                doc.SetInt("answer", 42);
                doc.SetString("a", "string");
                DefaultCollection.Save(doc);

                using (var q = QueryBuilder.Select(RevID)
                    .From(DataSource.Collection(DefaultCollection))
                    .Where(Meta.ID.EqualTo(Expression.String(docId)))) {

                    VerifyQuery(q, (n, row) => {
                        row.GetString(0).ShouldBe(doc.RevisionID!.ToString());
                    });

                    // Update doc:
                    doc.SetString("foo", "bar");
                    DefaultCollection.Save(doc);

                    VerifyQuery(q, (n, row) => {
                        row.GetString(0).ShouldBe(doc.RevisionID!.ToString());
                    });
                }

                // Use meta.revisionID in WHERE clause
                using (var q = QueryBuilder.Select(DocID)
                .From(DataSource.Collection(DefaultCollection))
                .Where(Meta.RevisionID.EqualTo(Expression.String(docId)))) {

                    VerifyQuery(q, (n, row) => {
                        row.GetString(0).ShouldBe(docId.ToString());
                    });
                }

                // Delete doc:
                DefaultCollection.Delete(doc);

                using (var q = QueryBuilder.Select(RevID)
                .From(DataSource.Collection(DefaultCollection))
                .Where(Meta.IsDeleted.EqualTo(Expression.Boolean(true)))) {
                    VerifyQuery(q, (n, row) => {
                        row.GetString(0).ShouldBe(doc.RevisionID!.ToString());
                    });
                }
            }
        }

#if !SANITY_ONLY
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
                DefaultCollection.Save(doc1a);

                doc1b.SetInt("answer", 42);
                doc1b.SetString("b", "string");
                DefaultCollection.Save(doc1b);

                doc1c.SetInt("answer", 42);
                doc1c.SetString("c", "string");
                DefaultCollection.Save(doc1c);

                DefaultCollection.SetDocumentExpiration("doc1", dto2).ShouldBe(true);
                DefaultCollection.SetDocumentExpiration("doc2", dto3).ShouldBe(true);
                DefaultCollection.SetDocumentExpiration("doc3", dto4).ShouldBe(true);
            }

            Thread.Sleep(4100);

            Try.Assertion(() =>
            {
                using (var r = QueryBuilder.Select(DocID, Expiration)
                    .From(DataSource.Collection(DefaultCollection))
                    .Where(Meta.Expiration
                        .LessThan(Expression.Long(dto6InMS)))) {

                    var b = r.Execute().AllResults();
                    b.Count.ShouldBe(0);
                }
            }).Times(5).Delay(TimeSpan.FromMilliseconds(500)).Go().ShouldBeTrue();
        }
#endif

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
                DefaultCollection.Save(doc1a);

                doc1b.SetInt("answer", 42);
                doc1b.SetString("b", "string");
                DefaultCollection.Save(doc1b);

                doc1c.SetInt("answer", 42);
                doc1c.SetString("c", "string");
                DefaultCollection.Save(doc1c);

                DefaultCollection.SetDocumentExpiration("doc1", dto20).ShouldBe(true);
                DefaultCollection.SetDocumentExpiration("doc2", dto30).ShouldBe(true);
                DefaultCollection.SetDocumentExpiration("doc3", dto40).ShouldBe(true);
            }

            using (var r = QueryBuilder.Select(DocID, Expiration)
                .From(DataSource.Collection(DefaultCollection))
                .Where(Meta.Expiration
                .LessThan(Expression.Long(dto60InMS)))) {

                var b = r.Execute().AllResults();
                b.Count.ShouldBe(3);
            }
        }

        [Fact]
        public void TestQueryDocumentIsNotDeleted()
        {
            using (var doc1 = new MutableDocument("doc1")){
                doc1.SetInt("answer", 42);
                doc1.SetString("a", "string");
                DefaultCollection.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetInt("answer", 42);
                doc2.SetString("a", "string");
                DefaultCollection.Save(doc2);
                DefaultCollection.Delete(doc2);
            }
            
            using (var q = QueryBuilder.Select(DocID, IsDeleted)
                 .From(DataSource.Collection(DefaultCollection))
                 .Where(Meta.IsDeleted.EqualTo(Expression.Boolean(false)))) {
                var count = VerifyQuery(q, (n, r) =>
                {
                    r.GetString(0).ShouldBe("doc1");
                    r.GetBoolean(1).ShouldBeFalse();
                });
                count.ShouldBe(1);
            }
        }

        [Fact]
        public void TestQueryDocumentIsDeleted()
        {
            using (var doc1 = new MutableDocument("doc1")){
                doc1.SetInt("answer", 42);
                doc1.SetString("a", "string");
                DefaultCollection.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetInt("answer", 42);
                doc2.SetString("a", "string");
                DefaultCollection.Save(doc2);
                DefaultCollection.Delete(doc2);
            }

            using (var q = QueryBuilder.Select(DocID, IsDeleted)
                 .From(DataSource.Collection(DefaultCollection))
                 .Where(Meta.IsDeleted.EqualTo(Expression.Boolean(true)))) {
                var count = VerifyQuery(q, (n, r) =>
                {
                    r.GetString(0).ShouldBe("doc2");
                    r.GetBoolean(1).ShouldBeTrue();
                });
                count.ShouldBe(1);
            }
        }

        [Fact]
        public void TestExpiredNotInQuery()
        {
            const string docId = "byebye";
            using (var doc1 = new MutableDocument(docId)) {
                doc1.SetString("expire_me", "now");
                DefaultCollection.Save(doc1);
            }
            
            DefaultCollection.SetDocumentExpiration(docId, DateTimeOffset.Now);
            Thread.Sleep(50);

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("expire_me", "never");
                DefaultCollection.Save(doc2);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(DefaultCollection))) {
                var count = VerifyQuery(q, (n, r) => { r.GetString(0).ShouldBe("doc2"); });
                count.ShouldBe(1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(DefaultCollection))
                .Where(Meta.IsDeleted.EqualTo(Expression.Boolean(true)))) {
                var count = VerifyQuery(q, (n, r) => throw new ShouldAssertException("No results should be present"));
                count.ShouldBe(0);
            }
        }

        [Fact]
        public void TestReadOnlyParameters()
        {
            using (var q = QueryBuilder.Select(DocID, Sequence).From(DataSource.Collection(DefaultCollection))) {
                var parameters = new Parameters().SetString("foo", "bar");
                q.Parameters = parameters;
                q.Parameters.GetValue("foo").ShouldBe("bar");
                Should.Throw<InvalidOperationException>(() => q.Parameters.SetValue("foo2", "bar2"),
                        "because the parameters are read only once in use");
            }
        }

        [Fact]
        public void TestParametersWithDictionaryArgument()
        {
            var dict = new Dictionary<string, object?>();
            var utcNow = DateTime.UtcNow;
            dict.Add(utcNow.ToShortDateString(), utcNow);
            var parameters = new Parameters(dict);
            parameters.GetValue(utcNow.ToShortDateString()).ShouldBe(utcNow);
        }

#if !CBL_NO_EXTERN_FILES
        [Fact]
        public void TestNoWhereQuery()
        {
            LoadJSONResource("names_100");
            using (var q = QueryBuilder.Select(DocID, Sequence).From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var expectedID = $"doc-{n:D3}";
                    row.GetString(0).ShouldBe(expectedID, "because otherwise the IDs were out of order");
                    row.GetLong(1).ShouldBe(n, "because otherwise the sequences were out of order");

                    var doc = DefaultCollection.GetDocument(row.GetString(0)!);
                    doc.ShouldNotBeNull("because the document should be retrievable");
                    doc!.Id.ShouldBe(expectedID, "because the document ID on the row should match the document");
                    doc.Sequence
                        .ShouldBe((ulong)n, "because the sequence on the row should match the document");
                });

                numRows.ShouldBe(100, "because otherwise the incorrect number of rows was returned");
            }
        }
#endif

        [Fact]
        [Obsolete]
        public void TestWhereNullOrMissing()
        {
            MutableDocument? doc1 = null, doc2 = null;
            doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            DefaultCollection.Save(doc1);

            doc2 = new MutableDocument("doc2");
            doc2.SetString("name", "Tiger");
            doc2.SetString("address", "123 1st ave.");
            doc2.SetInt("age", 20);
            DefaultCollection.Save(doc2);

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
                using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Collection(DefaultCollection)).Where(exp)) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        if (n <= expectedDocs.Length) {
                            var doc = expectedDocs[n - 1];
                            row.GetString("id")
                                .ShouldBe(doc.Id, $"because otherwise the row results were different than expected ({testNum})");
                        }
                    });

                    numRows.ShouldBe(expectedDocs.Length, "because otherwise too many rows were returned");
                }

                testNum++;
            }
        }

        [Fact]
        public void TestWhereValued()
        {
            MutableDocument? doc1 = null, doc2 = null;
            doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            DefaultCollection.Save(doc1);

            doc2 = new MutableDocument("doc2");
            doc2.SetString("name", "Tiger");
            doc2.SetString("address", "123 1st ave.");
            doc2.SetInt("age", 20);
            DefaultCollection.Save(doc2);

            var name = Expression.Property("name");
            var address = Expression.Property("address");
            var age = Expression.Property("age");
            var work = Expression.Property("work");

            var tests = new[] {
                Tuple.Create(name.IsValued(), new[] { doc1, doc2 }),
                Tuple.Create(name.IsNotValued(), new MutableDocument[0]),
                Tuple.Create(address.IsValued(), new[] { doc2 }),
                Tuple.Create(address.IsNotValued(), new[] { doc1 }),
                Tuple.Create(age.IsValued(), new[] { doc2 }),
                Tuple.Create(age.IsNotValued(), new[] { doc1 }),
                Tuple.Create(work.IsValued(), new MutableDocument[0]),
                Tuple.Create(work.IsNotValued(), new[] { doc1, doc2 })
            };

            int testNum = 1;
            foreach (var test in tests)
            {
                var exp = test.Item1;
                var expectedDocs = test.Item2;
                using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Collection(DefaultCollection)).Where(exp))
                {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        if (n <= expectedDocs.Length)
                        {
                            var doc = expectedDocs[n - 1];
                            row.GetString("id")
                                .ShouldBe(doc.Id, $"because otherwise the row results were different than expected ({testNum})");
                        }
                    });

                    numRows.ShouldBe(expectedDocs.Length, "because otherwise too many rows were returned");
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
                    TestWhereCompareValidator, (object?)l3),
                Tuple.Create(n1.LessThanOrEqualTo(Expression.Int(3)),
                    TestWhereCompareValidator, (object?)le3),
                Tuple.Create(n1.GreaterThan(Expression.Int(6)),
                    TestWhereCompareValidator, (object?)g6),
                Tuple.Create(n1.GreaterThanOrEqualTo(Expression.Int(6)),
                    TestWhereCompareValidator, (object?)ge6),
                Tuple.Create(n1.EqualTo(Expression.Int(7)),
                    TestWhereCompareValidator, (object?)e7),
                Tuple.Create(n1.NotEqualTo(Expression.Int(7)),
                    TestWhereCompareValidator, (object?)ne7)
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
                    TestWhereMathValidator, (object?)m2g8),
                Tuple.Create(n1.Divide(Expression.Int(2)).GreaterThan(Expression.Int(3)),
                    TestWhereMathValidator, (object?)d2g3),
                Tuple.Create(n1.Modulo(Expression.Int(2)).EqualTo(Expression.Int(0)),
                    TestWhereMathValidator, (object?)m2e0),
                Tuple.Create(n1.Add(Expression.Int(5)).GreaterThan(Expression.Int(10)),
                    TestWhereMathValidator, (object?)a5g10),
                Tuple.Create(n1.Subtract(Expression.Int(5)).GreaterThan(Expression.Int(0)),
                    TestWhereMathValidator, (object?)s5g0),
                Tuple.Create(n1.Multiply(n2).GreaterThan(Expression.Int(10)),
                    TestWhereMathValidator, (object?)mn2g10),
                Tuple.Create(n2.Divide(n1).GreaterThan(Expression.Int(3)),
                    TestWhereMathValidator, (object?)dn1g3),
                Tuple.Create(n1.Modulo(n2).EqualTo(Expression.Int(0)),
                    TestWhereMathValidator, (object?)mn2e0),
                Tuple.Create(n1.Add(n2).EqualTo(Expression.Int(10)),
                    TestWhereMathValidator, (object?)an2e10),
                Tuple.Create(n1.Subtract(n2).GreaterThan(Expression.Int(0)),
                    TestWhereMathValidator, (object?)sn2g0)
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
                    TestWhereAndValidator, default(object)),
                Tuple.Create(n1.LessThan(Expression.Int(3)).Or(n2.LessThan(Expression.Int(3))),
                    TestWhereOrValidator, default(object))
            };
            LoadNumbers(10);
            RunTestWithNumbers(new[] { 3, 5 }, cases);
        }

        [Fact]
        public void TestWhereIs()
        {
            var doc1 = new MutableDocument();
            doc1.SetString("string", "string");
            DefaultCollection.Save(doc1);

            using (var q = QueryBuilder.Select(DocID)
                .From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("string").EqualTo(Expression.String("string")))) {

                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).ShouldNotBeNull("because otherwise the query didn't get correct results");
                    var doc = DefaultCollection.GetDocument(row.GetString(0)!);
                    doc.ShouldNotBeNull("because otherwise the save failed");
                    doc!.Id.ShouldBe(doc1.Id, "because otherwise the wrong document ID was populated");
                    doc["string"].ToString().ShouldBe("string", "because otherwise garbage data was inserted");
                });
                numRows.ShouldBe(1, "beacuse one row matches the given query");
            }

            using (var q = QueryBuilder.Select(DocID)
                .From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("string").NotEqualTo(Expression.String("string1")))) {

                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).ShouldNotBeNull("because otherwise the query didn't get correct results");
                    var doc = DefaultCollection.GetDocument(row.GetString(0)!);
                    doc.ShouldNotBeNull("because otherwise the save failed");
                    doc!.Id.ShouldBe(doc1.Id, "because otherwise the wrong document ID was populated");
                    doc["string"].ToString().ShouldBe("string", "because otherwise garbage data was inserted");
                });
                numRows.ShouldBe(1, "because one row matches the 'IS NOT' query");
            }
        }

        [Fact]
        public void TestWhereBetween()
        {
            LoadNumbers(10);
            var n1 = Expression.Property("number1");
            var cases = new[] {
                Tuple.Create(n1.Between(Expression.Int(3), Expression.Int(7)), 
                TestWhereBetweenValidator, default(object))
            };

            RunTestWithNumbers(new[] { 5 }, cases);
        }

#if !CBL_NO_EXTERN_FILES
        [Fact]
        public void TestWhereIn()
        {
            LoadJSONResource("names_100");

            var expected = new[] {"Marcy", "Margaretta", "Margrett", "Marlen", "Maryjo" };
            var inExpression = expected.Select(Expression.String); // Note, this is LINQ Select, so don't get confused

            var firstName = Expression.Property("name.first");
            using (var q = QueryBuilder.Select(SelectResult.Expression(firstName))
                .From(DataSource.Collection(DefaultCollection))
                .Where(firstName.In(inExpression.ToArray()))
                .OrderBy(Ordering.Property("name.first"))) {

                var numRows = VerifyQuery(q, (n, row) =>
                {

                    var name = row.GetString(0);
                    name.ShouldBe(expected[n - 1], "because otherwise incorrect rows were returned");
                });

                numRows.ShouldBe(expected.Length, "because otherwise an incorrect number of rows were returned");
            }
        }

        [Fact]
        public void TestWhereLike()
        {
            LoadJSONResource("names_100");

            var where = Expression.Property("name.first").Like(Expression.String("%Mar%"));
            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                .From(DataSource.Collection(DefaultCollection))
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

                numRows.ShouldBe(5, "because there are 5 rows like that in the data source");
                firstNames.All(x => x.Contains("Mar")).ShouldBeTrue("because otherwise an incorrect entry came in");
            }
        }

        [Fact]
        public void TestWhereRegex()
        {
            LoadJSONResource("names_100");

            var where = Expression.Property("name.first").Regex(Expression.String("^Mar.*"));
            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                .From(DataSource.Collection(DefaultCollection))
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

                numRows.ShouldBe(5, "because there are 5 rows like that in the data source");
                var regex = new Regex("^Mar.*");
                firstNames.All(x => regex.IsMatch(x)).ShouldBeTrue("because otherwise an incorrect entry came in");
            }
        }

        [Fact]
        public void TestN1QLFTSQuery()
        {
            LoadJSONResource("sentences");
            DefaultCollection.CreateIndex("sentence", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("sentence")));

            using (var q = Db.CreateQuery("SELECT _id FROM _ WHERE MATCH(sentence, 'Dummie woman')")) 
            { // CBL developer notes: use '_default' instead of '_' also valid. (No one else needs to know about this ;) )
                var numRows = VerifyQuery(q, (n, row) =>
                {

                });

                numRows.ShouldBe(2, "because two rows in the data match the query");
            }

            using(var db = new Database("testN1QLDB")) {
                LoadJSONResource("sentences", db);
                db.GetDefaultCollection().CreateIndex("sentence", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("sentence")));

                using (var q = db.CreateQuery("SELECT _id FROM testN1QLDB WHERE MATCH(sentence, 'Dummie woman')")) { 
                    var numRows = VerifyQuery(q, (n, row) =>
                    {

                    });

                    numRows.ShouldBe(2, "because two rows in the data match the query");
                }
            }
        }

        [Fact]
        public void TestWhereMatch()
        {
            LoadJSONResource("sentences");

            var sentence = Expression.Property("sentence");
            var s_sentence = SelectResult.Expression(sentence);

            var w = FullTextFunction.Match(Expression.FullTextIndex("sentence"), "'Dummie woman'");
            var o = Ordering.Expression(FullTextFunction.Rank(Expression.FullTextIndex("sentence"))).Descending();

            var index = IndexBuilder.FullTextIndex(FullTextIndexItem.Property("sentence"));
            DefaultCollection.CreateIndex("sentence", index);
            using (var q = QueryBuilder.Select(DocID, s_sentence)
                .From(DataSource.Collection(DefaultCollection))
                .Where(w)
                .OrderBy(o)) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    
                });

                numRows.ShouldBe(2, "because two rows in the data match the query");
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
                    .From(DataSource.Collection(DefaultCollection)).OrderBy(order))  {
                    var firstNames = new List<object>();
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        var firstName = row.GetString(0);
                        if (firstName != null) {
                            firstNames.Add(firstName);
                        }
                    });

                    numRows.ShouldBe(100, "because otherwise the wrong number of rows was retrieved");
                    firstNames.Count.ShouldBe(numRows, "because otherwise some rows were null");
                    var firstNamesCopy = new List<object>(firstNames);
                    firstNames.Sort();
                    if (!ascending) {
                        firstNames.Reverse();
                    }

                    firstNames.ShouldBeEquivalentToFluent(firstNamesCopy, "because otherwise the results were not sorted");
                }
            }
        }

#if !SANITY_ONLY
        [Fact]
        public void TestQueryObserver()
        {
            var n1qlQ = Db.CreateQuery("SELECT META().id, contact FROM _ WHERE contact.address.state = 'CA'");
            TestQueryObserverWithQuery(n1qlQ, isLegacy: true);
            n1qlQ.Dispose();
            var query = QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
                .From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("contact.address.state").EqualTo(Expression.String("CA")));
            TestQueryObserverWithQuery(query, isLegacy: true);
            query.Dispose();
        }
#endif

        [Fact]
        public void TestMultipleQueryObservers()
        {
            var n1qlQ = Db.CreateQuery("SELECT META().id, contact FROM _ WHERE contact.address.state = 'CA'");
            TestMultipleQueryObserversWithQuery(n1qlQ);
            n1qlQ.Dispose();
            var query = QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
                .From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("contact.address.state").EqualTo(Expression.String("CA")));
            TestMultipleQueryObserversWithQuery(query);
            query.Dispose();
        }

        // How to use N1QL Query Parameter
        // https://docs.couchbase.com/couchbase-lite/3.0/csharp/query-n1ql-mobile.html#lbl-query-params
        [Theory]
        [InlineData(QueryTestMode.SQL)]
        [InlineData(QueryTestMode.QueryBuilder)]
        public void TestQueryObserverWithChangingQueryParameters(QueryTestMode mode)
        {
            using var query = mode == QueryTestMode.SQL
                ? Db.CreateQuery("SELECT META().id, contact FROM _ WHERE contact.address.state = $state")
                : QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
                    .From(DataSource.Collection(DefaultCollection))
                    .Where(Expression.Property("contact.address.state").EqualTo(Expression.Parameter("state")));
            TestQueryObserverWithChangingQueryParametersWithQuery(query);
        }

#endif

        [Fact]
        public void TestSelectDistinct()
        {
            var doc1 = new MutableDocument();
            doc1.SetInt("number", 1);
            DefaultCollection.Save(doc1);

            var doc2 = new MutableDocument();
            doc2.SetInt("number", 1);
            DefaultCollection.Save(doc2);

            using (var q = QueryBuilder.SelectDistinct(SelectResult.Expression(Expression.Property("number")))
                .From(DataSource.Collection(DefaultCollection)))
            {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var number = row.GetInt(0);
                    number.ShouldBe(1);
                });

                numRows.ShouldBe(1, "because there is only one distinct row");
            }
        }

        [Fact]
        public async Task TestLiveQuery()
        {
            LoadNumbers(100);
            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("number1"))).From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("number1").LessThan(Expression.Int(10))).OrderBy(Ordering.Property("number1"))) {
                using var wa = new WaitAssert();
                using var wa2 = new WaitAssert();
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

#if !SANITY_ONLY
        [Fact]
        public async Task TestLiveQueryNoUpdate() => await TestLiveQueryNoUpdateInternal(false);

        [Fact]
        public async Task TestLiveQueryNoUpdateConsumeAll() => await TestLiveQueryNoUpdateInternal(true);
#endif

        [Fact]
        public void TestJoin()
        {
            LoadNumbers(100);
            var testDoc = new MutableDocument("joinme");
            testDoc.SetInt("theone", 42);
            DefaultCollection.Save(testDoc);
            var number2Prop = Expression.Property("number2");
            using (var q = QueryBuilder.Select(SelectResult.Expression(number2Prop.From("main")))
                .From(DataSource.Collection(DefaultCollection).As("main"))
                .Join(Join.InnerJoin(DataSource.Collection(DefaultCollection).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                var results = q.Execute().ToList();
                results.Count.ShouldBe(1, "because only one document should match 42");
                results.First().GetInt(0).ShouldBe(58,
                    "because that was the number stored in 'number2' of the matching doc");
            }

            using (var q = QueryBuilder.Select(SelectResult.All().From("main"))
                .From(DataSource.Collection(DefaultCollection).As("main"))
                .Join(Join.InnerJoin(DataSource.Collection(DefaultCollection).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                var results = q.Execute().ToList();
                results.Count.ShouldBe(1, "because only one document should match 42");
                results.First().Keys.FirstOrDefault().ShouldBe("main");
            }
        }

        [Fact]
        public void TestLeftJoin()
        {
            LoadNumbers(100);
            var testDoc = new MutableDocument("joinme");
            testDoc.SetInt("theone", 42);
            DefaultCollection.Save(testDoc);
            var number2Prop = Expression.Property("number2");
            using (var q = QueryBuilder.Select(SelectResult.Expression(number2Prop.From("main")), SelectResult.Expression(Expression.Property("theone").From("secondary")))
                .From(DataSource.Collection(DefaultCollection).As("main"))
                .Join(Join.LeftJoin(DataSource.Collection(DefaultCollection).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                var results = q.Execute().ToList();
                results.Count.ShouldBe(101);
                results[41].GetInt(0).ShouldBe(58);
                results[41].GetInt(1).ShouldBe(42);
                results[42].GetInt(0).ShouldBe(57);
                results[42].GetValue(1).ShouldBeNull();
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(number2Prop.From("main")), SelectResult.Expression(Expression.Property("theone").From("secondary")))
                .From(DataSource.Collection(DefaultCollection).As("main"))
                .Join(Join.LeftOuterJoin(DataSource.Collection(DefaultCollection).As("secondary"))
                    .On(Expression.Property("number1").From("main")
                        .EqualTo(Expression.Property("theone").From("secondary"))))) {
                var results = q.Execute().ToList();
                results.Count.ShouldBe(101);
                results[41].GetInt(0).ShouldBe(58);
                results[41].GetInt(1).ShouldBe(42);
                results[42].GetInt(0).ShouldBe(57);
                results[42].GetValue(1).ShouldBeNull();
            }
        }

        [Fact]
        [ForIssue("couchbase-lite-core/497")]
        public void TestLeftJoinWithSelectAll()
        {
            LoadNumbers(100);

            using (var joinme = new MutableDocument("joinme")) {
                joinme.SetInt("theone", 42);
                DefaultCollection.Save(joinme);
            }

            var on = Expression.Property("number1").From("main").EqualTo(Expression.Property("theone").From("secondary"));
            var join = Join.LeftJoin(DataSource.Collection(DefaultCollection).As("secondary")).On(on);

            using (var q = QueryBuilder.Select(SelectResult.All().From("main"),
                    SelectResult.All().From("secondary"))
                .From(DataSource.Collection(DefaultCollection).As("main"))
                .Join(join)) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    var main = r.GetDictionary(0);
                    var secondary = r.GetDictionary(1);
                    main.ShouldNotBeNull("because otherwise main wasn't present in the results");

                    var number1 = main!.GetInt("number1");
                    if (number1 == 42) {
                        secondary.ShouldNotBeNull("because the JOIN matched");
                        secondary!.GetInt("theone").ShouldBe(number1, "because this is the join entry");
                    } else {
                        secondary.ShouldBeNull("because the JOIN didn't match");
                    }
                });

                numRows.ShouldBe(101);
            }

        }

        [Fact]
        public void TestCrossJoin()
        {
            LoadNumbers(10);
            var num1 = Expression.Property("number1").From("main");
            var num2 = Expression.Property("number2").From("secondary");

            using (var q = QueryBuilder.Select(SelectResult.Expression(num1), SelectResult.Expression(num2))
                .From(DataSource.Collection(DefaultCollection).As("main"))
                .Join(Join.CrossJoin(DataSource.Collection(DefaultCollection).As("secondary")))
                .OrderBy(Ordering.Expression(num2))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    ((row.GetInt(0) - 1) % 10).ShouldBe((n - 1) % 10);
                    row.GetInt(1).ShouldBe((n - 1) / 10);
                });

                count.ShouldBe(100);
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
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetDouble(0).ShouldBe(50.5, Double.Epsilon);
                    row.GetInt(1).ShouldBe(100);
                    row.GetInt(2).ShouldBe(1);
                    row.GetInt(3).ShouldBe(100);
                    row.GetInt(4).ShouldBe(5050);
                });

                numRows.ShouldBe(1);
            }
        }

#if !CBL_NO_EXTERN_FILES
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
                .From(DataSource.Collection(DefaultCollection))
                .Where(gender.EqualTo(Expression.String("female")))
                .GroupBy(STATE)
                .OrderBy(Ordering.Expression(STATE))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var state = row.GetString(0);
                    var count = row.GetInt(1);
                    var maxZip = row.GetString(2);
                    if (n - 1 < expectedStates.Length) {
                        state.ShouldBe(expectedStates[n - 1]);
                        count.ShouldBe(expectedCounts[n - 1]);
                        maxZip.ShouldBe(expectedZips[n - 1]);
                    }
                });
                numRows.ShouldBe(31);
            }

            expectedStates = new[] { "CA", "IA", "IN" };
            expectedCounts = new[] { 6, 3, 2 };
            expectedZips = new[] { "94153", "50801", "47952" };

            using (var q = QueryBuilder.Select(SelectResult.Expression(STATE), SelectResult.Expression(COUNT), SelectResult.Expression(MAXZIP))
                .From(DataSource.Collection(DefaultCollection))
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
                        state.ShouldBe(expectedStates[n - 1]);
                        count.ShouldBe(expectedCounts[n - 1]);
                        maxZip.ShouldBe(expectedZips[n - 1]);
                    }
                });
                numRows.ShouldBe(15);
            }
        }
#endif

        [Fact]
        public void TestParameters()
        {
            LoadNumbers(10);

            var NUMBER1 = Expression.Property("number1");
            var PARAM_N1 = Expression.Parameter("num1");
            var PARAM_N2 = Expression.Parameter("num2");

            using (var q = QueryBuilder.Select(SelectResult.Expression(NUMBER1))
                .From(DataSource.Collection(DefaultCollection))
                .Where(NUMBER1.Between(PARAM_N1, PARAM_N2))
                .OrderBy(Ordering.Expression(NUMBER1))) {
                var parameters = new Parameters().SetInt("num1", 2).SetInt("num2", 5);
                q.Parameters = parameters;

                var expectedNumbers = new[] {2, 3, 4, 5};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var number = row.GetInt(0);
                    number.ShouldBe(expectedNumbers[n - 1]);
                });

                numRows.ShouldBe(4);
            }
        }

        // Verify fix of CBL-1107 Fix bad interpretation of '$' properties
        [Fact]
        public void TestQueryDocumentWithDollarSign()
        {
            var prod1 = new MutableDocument("doc1");
            prod1.SetString("$type", "book")
                .SetString("$description", "about cats")
                .SetString("$price", "$100");

            var prod2 = new MutableDocument("doc2");
            prod2.SetString("$type", "book")
                .SetString("$description", "about dogs")
                .SetString("$price", "$95");

            var prod3 = new MutableDocument("doc3");
            prod3.SetString("$type", "animal")
                .SetString("$description", "puppy")
                .SetString("$price", "$195");

            DefaultCollection.Save(prod1);
            DefaultCollection.Save(prod2);
            DefaultCollection.Save(prod3);

            int bookPriceLessThan100Cnt = 0;
            int totalBooksCnt = 0;

            using (var q = QueryBuilder.Select(DocID, 
                    SelectResult.Expression(Expression.Property("$type")),
                    SelectResult.Expression(Expression.Property("$price")))
                .From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("$type").EqualTo(Expression.String("book")))) {

                var res = q.Execute();
                foreach (var r in res) {
                    totalBooksCnt++;
                    var p = r.GetString("$price")?.Remove(0, 1);
                    p.ShouldNotBeNull("because otherwise the query didn't return correct results");
                    if (Convert.ToInt32(p) < 100)
                        bookPriceLessThan100Cnt++;
                }
            }

            totalBooksCnt.ShouldBe(2);
            bookPriceLessThan100Cnt.ShouldBe(1);
        }

        [Fact]
        public void TestMeta()
        {
            LoadNumbers(5);

            var NUMBER1 = Expression.Property("number1");
            var RES_NUMBER1 = SelectResult.Expression(NUMBER1);

            using (var q = QueryBuilder.Select(DocID, Sequence, RevID, RES_NUMBER1)
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Expression(Meta.Sequence))) {
                var expectedDocIDs = new[] {"doc1", "doc2", "doc3", "doc4", "doc5"};
                var expectedSeqs = new[] {1, 2, 3, 4, 5};
                var expectedNumbers = expectedSeqs;

                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var docID = row.GetString(0);
                    var docID2 = row.GetString("id");
                    docID.ShouldBe(docID2, "because these calls are two ways of accessing the same info");
                    var seq = row.GetInt(1);
                    var seq2 = row.GetInt("sequence");
                    seq.ShouldBe(seq2, "because these calls are two ways of accessing the same info");
                    var revID1 = row.GetString(2);
                    var revID2 = row.GetString("revisionID");
                    revID1.ShouldBe(revID2, "because these calls are two ways of accessing the same info");
                    var number = row.GetInt(3);

                    docID.ShouldBe(expectedDocIDs[n - 1]);
                    seq.ShouldBe(expectedSeqs[n - 1]);
                    using (var d = DefaultCollection.GetDocument(docID!)) {
                        d.ShouldNotBeNull($"because otherwise the document '{docID}' was missing");
                        revID1.ShouldBe(d!.RevisionID);
                    }
                    number.ShouldBe(expectedNumbers[n - 1]);
                });

                numRows.ShouldBe(5);
            }
        }

        [Fact]
        public void TestLimit()
        {
            LoadNumbers(10);

            var LIMIT = Expression.Parameter("limit");
            var NUMBER = Expression.Property("number1");

            using (var q = QueryBuilder.Select(SelectResult.Property("number1"))
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Expression(NUMBER))
                .Limit(Expression.Int(5))) {

                var expectedNumbers = new[] {1, 2, 3, 4, 5};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetInt(0).ShouldBe(expectedNumbers[n - 1]);
                });

                numRows.ShouldBe(5);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("number1"))
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Expression(NUMBER))
                .Limit(LIMIT)) {
                var parameters = new Parameters().SetInt("limit", 3);
                q.Parameters = parameters;

                var expectedNumbers = new[] {1, 2, 3};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetInt(0).ShouldBe(expectedNumbers[n - 1]);
                });

                numRows.ShouldBe(3);
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
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Expression(NUMBER))
                .Limit(Expression.Int(5), Expression.Int(3))) {

                var expectedNumbers = new[] {4, 5, 6, 7, 8};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetInt(0).ShouldBe(expectedNumbers[n - 1]);
                });

                numRows.ShouldBe(5);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("number1"))
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Expression(NUMBER))
                .Limit(LIMIT, OFFSET)) {
                var parameters = new Parameters().SetInt("limit", 3).SetInt("offset", 5);
                q.Parameters = parameters;

                var expectedNumbers = new[] {6, 7, 8};
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    row.GetInt(0).ShouldBe(expectedNumbers[n - 1]);
                });

                numRows.ShouldBe(3);
            }
        }

#if !CBL_NO_EXTERN_FILES
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
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetValue("firstname").ShouldBe(r.GetValue(0));
                    r.GetValue("lastname").ShouldBe(r.GetValue(1));
                    r.GetValue("gender").ShouldBe(r.GetValue(2));
                    r.GetValue("city").ShouldBe(r.GetValue(3));
                });

                numRows.ShouldBe(100);
            }
        }
#endif

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
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetDouble("$1").ShouldBe(r.GetDouble(0));
                    r.GetInt("$2").ShouldBe(r.GetInt(1));
                    r.GetInt("min").ShouldBe(r.GetInt(2));
                    r.GetInt("$3").ShouldBe(r.GetInt(3));
                    r.GetInt("sum").ShouldBe(r.GetInt(4));
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
                DefaultCollection.Save(doc);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(ArrayFunction.Length(Expression.Property("array"))))
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetInt(0).ShouldBe(2);
                });

                numRows.ShouldBe(1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(ArrayFunction.Contains(Expression.Property("array"), Expression.String("650-123-0001"))),
                    SelectResult.Expression(ArrayFunction.Contains(Expression.Property("array"), Expression.String("650-123-0003"))))
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetBoolean(0).ShouldBeTrue();
                    r.GetBoolean(1).ShouldBeFalse();
                });

                numRows.ShouldBe(1);
            }
        }

        [Fact]
        public void TestMathFunctions()
        {
            const double num = 0.6;
            using (var doc = new MutableDocument("doc1")) {
                doc.SetDouble("number", num);
                DefaultCollection.Save(doc);
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
                    .From(DataSource.Collection(DefaultCollection))) {
                    var numRows = VerifyQuery(q, (n, r) =>
                    {
                        r.GetDouble(0).ShouldBe(expectedValues[index++]);
                    });

                    numRows.ShouldBe(1);
                }
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Function.E().Multiply(Expression.Int(2))),
                    SelectResult.Expression(Function.Pi().Multiply(Expression.Int(2))))
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetDouble(0).ShouldBe(Math.E * 2);
                    r.GetDouble(1).ShouldBe(Math.PI * 2);
                });

                numRows.ShouldBe(1);
            }
        }

        [Fact]
        public void TestStringFunctions()
        {
            const string str = "  See you l8r  ";
            using (var doc = new MutableDocument("doc1")) {
                doc.SetString("greeting", str);
                DefaultCollection.Save(doc);
            }

            var prop = Expression.Property("greeting");
            using (var q = QueryBuilder.Select(SelectResult.Expression(Function.Contains(prop, Expression.String("8"))),
                    SelectResult.Expression(Function.Contains(prop, Expression.String("9"))))
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetBoolean(0).ShouldBeTrue();
                    r.GetBoolean(1).ShouldBeFalse();
                });

                numRows.ShouldBe(1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Function.Length(prop)))
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetInt(0).ShouldBe(str.Length);
                });

                numRows.ShouldBe(1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Function.Lower(prop)),
                    SelectResult.Expression(Function.Ltrim(prop)),
                    SelectResult.Expression(Function.Rtrim(prop)),
                    SelectResult.Expression(Function.Trim(prop)),
                    SelectResult.Expression(Function.Upper(prop)))
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetString(0).ShouldBe(str.ToLowerInvariant());
                    r.GetString(1).ShouldBe(str.TrimStart());
                    r.GetString(2).ShouldBe(str.TrimEnd());
                    r.GetString(3).ShouldBe(str.Trim());
                    r.GetString(4).ShouldBe(str.ToUpperInvariant());
                });

                numRows.ShouldBe(1);
            }
        }

#if !CBL_NO_EXTERN_FILES
        [Fact]
        public void TestQuantifiedOperators()
        {
            LoadJSONResource("names_100");

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(DefaultCollection))
                .Where(ArrayExpression.Any(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("climbing"))))) {
                var expected = new[] {"doc-017", "doc-021", "doc-023", "doc-045", "doc-060"};
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id"));
                received.ShouldBeEquivalentToFluent(expected);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(DefaultCollection))
                .Where(ArrayExpression.Every(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("taxes"))))) {
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id")).ToList();
                received.Count.ShouldBe(42, "because empty array results are included");
                received[0].ShouldBe("doc-007");
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(DefaultCollection))
                .Where(ArrayExpression.AnyAndEvery(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("taxes"))))) {
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id")).ToList();
                received.Count.ShouldBe(0, "because nobody likes taxes...");
            }
        }
#endif

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
                    DefaultCollection.Save(doc);
                }
            }

            var DOC_ID = Meta.ID;
            var S_DOC_ID = SelectResult.Expression(DOC_ID);

            var PATHS = Expression.Property("paths");
            var VAR_PATH = ArrayExpression.Variable("path.city");
            var where = ArrayExpression.Any(ArrayExpression.Variable("path")).In(PATHS).Satisfies(VAR_PATH.EqualTo(Expression.String("San Francisco")));

            using (var q = QueryBuilder.Select(S_DOC_ID)
                .From(DataSource.Collection(DefaultCollection))
                .Where(where)) {
                var expected = new[] { "doc-0", "doc-2" };
                var numRows = VerifyQuery(q, (n, row) => { row.GetString(0).ShouldBe(expected[n - 1]); });
                numRows.ShouldBe(expected.Length);
            }
        }

        [Fact]
        public void TestSelectAll()
        {
            LoadNumbers(100);
            using (var q = QueryBuilder.Select(SelectResult.All(), SelectResult.Expression(Expression.Property("number1")))
                .From(DataSource.Collection(DefaultCollection))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    var all = r.GetDictionary(0);
                    all.ShouldNotBeNull("because otherwise the query returned incorrect results");
                    all!.GetInt("number1").ShouldBe(n);
                    all.GetInt("number2").ShouldBe(100 - n);
                    r.GetInt(1).ShouldBe(n);
                });

                numRows.ShouldBe(100);
            }

            using (var q = QueryBuilder.Select(SelectResult.All().From("db"), SelectResult.Expression(Expression.Property("number1").From("db")))
                .From(DataSource.Collection(DefaultCollection).As("db"))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    var all = r.GetDictionary(0);
                    all.ShouldNotBeNull("because otherwise the query returned incorrect results");
                    all!.GetInt("number1").ShouldBe(n);
                    all.GetInt("number2").ShouldBe(100 - n);
                    r.GetInt(1).ShouldBe(n);
                });

                numRows.ShouldBe(100);
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

            var expr = (Expression.Property("test") as QueryExpression)!;
            bothSensitive.SetOperand(expr);
            accentSensitive.SetOperand(expr);
            caseSensitive.SetOperand(expr);
            noSensitive.SetOperand(expr);
            ascii.SetOperand(expr);
            asciiNoSensitive.SetOperand(expr);
            locale.SetOperand(expr);

            bothSensitive.ConvertToJSON().ShouldBeEquivalentToFluent(new object[] { "COLLATE", new Dictionary<string, object> {
                ["UNICODE"] = true,
				["LOCALE"] = Collation.DefaultLocale
            }, new[] { ".test" }});
            accentSensitive.ConvertToJSON().ShouldBeEquivalentToFluent(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["UNICODE"] = true,
                ["CASE"] = false,
				["LOCALE"] = Collation.DefaultLocale
            }, new[] { ".test" }});
            caseSensitive.ConvertToJSON().ShouldBeEquivalentToFluent(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["UNICODE"] = true,
                ["DIAC"] = false,
				["LOCALE"] = Collation.DefaultLocale
            }, new[] { ".test" }});
            noSensitive.ConvertToJSON().ShouldBeEquivalentToFluent(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["UNICODE"] = true,
                ["DIAC"] = false,
                ["CASE"] = false,
				["LOCALE"] = Collation.DefaultLocale
            }, new[] { ".test" }});
            ascii.ConvertToJSON().ShouldBeEquivalentToFluent(new object[] { "COLLATE", new Dictionary<string, object>
            {
            }, new[] { ".test" }});
            asciiNoSensitive.ConvertToJSON().ShouldBeEquivalentToFluent(new object[] { "COLLATE", new Dictionary<string, object>
            {
                ["CASE"] = false
            }, new[] { ".test" }});

            locale.ConvertToJSON().ShouldBeEquivalentToFluent(new object[] { "COLLATE", new Dictionary<string, object>
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
                    DefaultCollection.Save(doc);
                }
            }

            var stringProp = Expression.Property("string");

            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("string")))
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Expression(stringProp.Collate(Collation.Unicode())))) {
                var results = q.Execute();
                results.Select(x => x.GetString(0)).ShouldBeEquivalentToFluent(new[] {"A", "Å", "B", "Z"},
                    "because by default Å comes between A and B");
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("string")))
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Expression(stringProp.Collate(Collation.Unicode().Locale("se"))))) {
                var results = q.Execute();
                results.Select(x => x.GetString(0)).ShouldBeEquivalentToFluent(new[] { "A", "B", "Z", "Å" },
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
                    DefaultCollection.Save(doc);

                    var comparison = data.Item3
                        ? Expression.Property("value").Collate(data.Item4).EqualTo(Expression.String(data.Item2))
                        : Expression.Property("value").Collate(data.Item4).LessThan(Expression.String(data.Item2));

                    using (var q = QueryBuilder.Select(SelectResult.All())
                        .From(DataSource.Collection(DefaultCollection))
                        .Where(comparison)) {
                        var result = q.Execute();
                        result.Count().ShouldBe(1,
                            $"because otherwise the comparison failed for {data.Item1} and {data.Item2} (position {i})");
                    }

                    DefaultCollection.Delete(DefaultCollection.GetDocument(doc.Id)!);
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
                    DefaultCollection.Save(doc);
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
                    .From(DataSource.Collection(DefaultCollection))
                    .OrderBy(Ordering.Expression(property.Collate(data.Item2)))) {
                    var results = q.Execute();
                    results.Select(x => x.GetString(0)).ShouldBeEquivalentToFluent(data.Item3);
                }
            }
        }

        [ForIssue("couchbase-lite-android/1356")]
        [Fact]
        public void TestCountFunctions()
        {
            LoadNumbers(100);

            var ds = DataSource.Collection(DefaultCollection);
            var cnt = Function.Count(Expression.Property("number1"));
            var rsCnt = SelectResult.Expression(cnt);
            using (var q = QueryBuilder.Select(rsCnt).From(ds)) {
                var numRows = VerifyQuery(q, (n, row) => { row.GetInt(0).ShouldBe(100); });
                numRows.ShouldBe(1);
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
                DefaultCollection.Save(hotel1);

                hotel2.SetString("type", "hotel");
                hotel2.SetString("name", "Sheraton");
                DefaultCollection.Save(hotel2);

                hotel3.SetString("type", "hotel");
                hotel3.SetString("name", "Marriot");
                DefaultCollection.Save(hotel3);

                bookmark1.SetString("type", "bookmark");
                bookmark1.SetString("title", "Bookmark for Hawaii");
                var hotels1 = new MutableArrayObject();
                hotels1.AddString("hotel1").AddString("hotel2");
                bookmark1.SetArray("hotels", hotels1);
                DefaultCollection.Save(bookmark1);

                bookmark2.SetString("type", "bookmark");
                bookmark2.SetString("title", "Bookmark for New York");
                var hotels2 = new MutableArrayObject();
                hotels2.AddString("hotel3");
                bookmark2.SetArray("hotels", hotels2);
                DefaultCollection.Save(bookmark2);
            }

            var mainDS = DataSource.Collection(DefaultCollection).As("main");
            var secondaryDS = DataSource.Collection(DefaultCollection).As("secondary");

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
                    .From(DataSource.Collection(DefaultCollection))
                    .Where(Expression.Property("type").EqualTo(Expression.String("task")))) {
                    var rs = q.Execute();
                    var counter = 0;
                    foreach (var r in rs) {
                        WriteLine($"Round 1: Result -> {JsonConvert.SerializeObject(r.ToDictionary())}");
                        counter++;
                    }

                    counter.ShouldBe(2);

                    task1.IsDeleted.ShouldBeFalse();
                    DefaultCollection.Delete(task1);
                    DefaultCollection.Count.ShouldBe(1UL);
                    DefaultCollection.GetDocument(task1.Id).ShouldBeNull();

                    rs = q.Execute();
                    counter = 0;
                    foreach (var r in rs) {
                        r.GetString(0).ShouldBe(task2.Id);
                        WriteLine($"Round 2: Result -> {JsonConvert.SerializeObject(r.ToDictionary())}");
                        counter++;
                    }

                    counter.ShouldBe(1);
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
                DefaultCollection.Count.ShouldBe(3UL);

                var exprType = Expression.Property("type");
                var exprComplete = Expression.Property("complete");
                var srCount = SelectResult.Expression(Function.Count(Expression.All()));
                using (var q = QueryBuilder.Select(SelectResult.All())
                    .From(DataSource.Collection(DefaultCollection))
                    .Where(exprType.EqualTo(Expression.String("task")).And(exprComplete.EqualTo(Expression.Boolean(true))))) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        WriteLine($"res -> {JsonConvert.SerializeObject(row.ToDictionary())}");
                        var dict = row.GetDictionary("_default");
                        dict.ShouldNotBeNull("because otherwise the query returned incorrect data");
                        dict!.GetBoolean("complete").ShouldBeTrue();
                        dict.GetString("type").ShouldBe("task");
                        dict.GetString("title").ShouldStartWith("Task ");
                    });

                    numRows.ShouldBe(2);
                }

                using (var q = QueryBuilder.Select(SelectResult.All())
                    .From(DataSource.Collection(DefaultCollection))
                    .Where(exprType.EqualTo(Expression.String("task")).And(exprComplete.EqualTo(Expression.Boolean(false))))) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        WriteLine($"res -> {JsonConvert.SerializeObject(row.ToDictionary())}");
                        var dict = row.GetDictionary("_default");
                        dict.ShouldNotBeNull("because otherwise the query returned incorrect data");
                        dict!.GetBoolean("complete").ShouldBeFalse();
                        dict.GetString("type").ShouldBe("task");
                        dict.GetString("title").ShouldStartWith("Task ");
                    });

                    numRows.ShouldBe(1);
                }

                using (var q = QueryBuilder.Select(srCount)
                    .From(DataSource.Collection(DefaultCollection))
                    .Where(exprType.EqualTo(Expression.String("task")).And(exprComplete.EqualTo(Expression.Boolean(true))))) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        WriteLine($"res -> {JsonConvert.SerializeObject(row.ToDictionary())}");
                        row.GetInt(0).ShouldBe(2);
                    });

                    numRows.ShouldBe(1);
                }

                using (var q = QueryBuilder.Select(srCount)
                    .From(DataSource.Collection(DefaultCollection))
                    .Where(exprType.EqualTo(Expression.String("task")).And(exprComplete.EqualTo(Expression.Boolean(false))))) {
                    var numRows = VerifyQuery(q, (n, row) =>
                    {
                        WriteLine($"res -> {JsonConvert.SerializeObject(row.ToDictionary())}");
                        row.GetInt(0).ShouldBe(1);
                    });

                    numRows.ShouldBe(1);
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
                DefaultCollection.Save(doc1);
            }

            var mainDS = DataSource.Collection(DefaultCollection).As("main");
            var secondaryDS = DataSource.Collection(DefaultCollection).As("secondary");

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

                    mainAll1?.GetInt("number1").ShouldBe(42);
                    mainAll2?.GetInt("number1").ShouldBe(42);
                    mainAll1?.GetInt("number2").ShouldBe(58);
                    mainAll1?.GetInt("number2").ShouldBe(58);
                    secondAll1?.GetInt("theone").ShouldBe(42);
                    secondAll2?.GetInt("theone").ShouldBe(42);
                });

                numRows.ShouldBe(1);
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
                DefaultCollection.Save(doc1);
            }

            var mainDS = DataSource.Collection(DefaultCollection).As("main");
            var secondaryDS = DataSource.Collection(DefaultCollection).As("secondary");

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
                    n.ShouldBe(1);
                    var docID = row.GetString("mainDocID");
                    docID.ShouldNotBeNull("because otherwise the query returned incorrect results");
                    using (var doc = DefaultCollection.GetDocument(docID!)) {
                        doc.ShouldNotBeNull("because otherwise the document disappeared from the database");
                        doc!.GetInt("number1").ShouldBe(1);
                        doc.GetInt("number2").ShouldBe(99);

                        row.GetString("secondaryDocID").ShouldBe("joinme");
                        row.GetInt("theone").ShouldBe(42);
                    }
                });

                numRows.ShouldBe(1);
            }
        }

        [Fact]
        public void TestMissingValues()
        {
            using (var doc1 = new MutableDocument("joinme")) {
                doc1.SetInt("theone", 42);
                doc1.SetString("numberID", "doc1");
                doc1.SetString("nullval", null);
                DefaultCollection.Save(doc1);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("name"), SelectResult.Property("nullval"))
                .From(DataSource.Collection(DefaultCollection))) {
                var results = q.Execute();
                foreach (var result in results) {
                    result.Count.ShouldBe(1);
                    result.Contains("name").ShouldBeFalse();
                    result.GetString("name").ShouldBeNull();
                    result.GetValue("name").ShouldBeNull();
                    result.GetString(0).ShouldBeNull();
                    result.GetValue(0).ShouldBeNull();
                    result.Contains("nullval").ShouldBeTrue();
                    result.GetString("nullval").ShouldBeNull();
                    result.GetValue("nullval").ShouldBeNull();
                    result.GetString(1).ShouldBeNull();
                    result.GetValue(1).ShouldBeNull();

                    result?.ToList<object?>().ShouldBeEquivalentToFluent(new object?[] { null });
                    result?.ToDictionary().ShouldBeEquivalentToFluent(new Dictionary<string, object?>
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

            var parameters = builder;
#pragma warning disable CS8605 // Unboxing a possibly null value.
            ((bool)parameters.GetValue("true")).ShouldBeTrue();
            ((DateTimeOffset)parameters.GetValue("now")).ShouldBe(now);
            ((double)parameters.GetValue("pi")).ShouldBe(Math.PI);
            ((float)parameters.GetValue("simple_pi")).ShouldBe(3.14159f);
            ((long)parameters.GetValue("big_num")).ShouldBe(Int64.MaxValue);
            ((string?)parameters.GetValue("name")).ShouldBe("Jim");
#pragma warning restore CS8605 // Unboxing a possibly null value.
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
                DefaultCollection.Save(doc);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("array"),
                    SelectResult.Property("blob"),
                    SelectResult.Property("created_at"),
                    SelectResult.Property("simple_pi"),
                    SelectResult.Property("big_num"),
                    SelectResult.Property("boolean"))
                .From(DataSource.Collection(DefaultCollection))) {
                VerifyQuery(q, (n, row) =>
                {
                    row.GetArray(0).ShouldBeEquivalentToFluent(new[] { 1L, 2L, 3L });
                    row.GetArray("array").ShouldBeEquivalentToFluent(new[] { 1L, 2L, 3L });
                    row.GetBlob(1)?.Content.ShouldBeEquivalentToFluent(blobContent);
                    row.GetBlob("blob")?.Content.ShouldBeEquivalentToFluent(blobContent);
                    row.GetDate(2).ShouldBe(now);
                    row.GetDate("created_at").ShouldBe(now);
                    row.GetFloat(3).ShouldBe(3.14159f);
                    row.GetFloat("simple_pi").ShouldBe(3.14159f);
                    row.GetLong(4).ShouldBe(Int64.MaxValue);
                    row.GetLong("big_num").ShouldBe(Int64.MaxValue);

                    row[4].Long.ShouldBe(Int64.MaxValue);
                    row["big_num"].Long.ShouldBe(Int64.MaxValue);
                    row.GetBoolean(5).ShouldBe(true);
                    row.GetBoolean("boolean").ShouldBe(true);

                    var resultList = row.ToList();
                    resultList.Count.ShouldBe(6);
                    resultList.ElementAtOrDefault(2).ShouldBe(now.ToString("o"));
                });
            }
        }

        [Fact]
        public void TestFTSStemming()
        {
            // Can't rely on the default locale, it could be anything and that would fail the test
            DefaultCollection.CreateIndex("passageIndex", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("passage")).SetLanguage("en"));
            DefaultCollection.CreateIndex("passageIndexStemless", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("passage")).SetLanguage(null));

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("passage", "The boy said to the child, 'Mommy, I want a cat.'");
                DefaultCollection.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("passage", "The mother replied 'No, you already have too many cats.'");
                DefaultCollection.Save(doc2);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(DefaultCollection))
                .Where(FullTextFunction.Match(Expression.FullTextIndex("passageIndex"), "cat"))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).ShouldBe($"doc{n}");
                });
                count.ShouldBe(2);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(DefaultCollection))
                .Where(FullTextFunction.Match(Expression.FullTextIndex("passageIndexStemless"), "cat"))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).ShouldBe($"doc{n}");
                });
                count.ShouldBe(1);
            }
        }

        [Fact]
        public void TestQueryExpressions()
        {
            var doubleValue = Math.Round(Math.PI, 14);
            var floatValue = 1.5F;
            var longValue = 4294967296L;
            var value = (object)"stringObject";

            DictionaryObject?[] resultsDouble;
            DictionaryObject?[] resultsFloat;
            DictionaryObject?[] resultsLong;
            DictionaryObject?[] resultsValue;

            using (var document = new MutableDocument("ExpressionTypes")) {
                document.SetDouble("doubleValue", doubleValue);
                DefaultCollection.Save(document);

                document.SetFloat("floatValue", floatValue);
                DefaultCollection.Save(document);

                document.SetLong("longValue", longValue);
                DefaultCollection.Save(document);

                document.SetValue("value", value);
                DefaultCollection.Save(document);
            }

            var v = DefaultCollection.GetDocument("ExpressionTypes")?.GetDouble("doubleValue");

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("doubleValue").EqualTo(Expression.Double(doubleValue))
                )) {
                resultsDouble = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("floatValue").EqualTo(Expression.Float(floatValue))
                )) {
                resultsFloat = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("longValue").EqualTo(Expression.Long(longValue))
                )) {
                resultsLong = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("value").EqualTo(Expression.Value(value))
                )) {
                resultsValue = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            resultsDouble.Length.ShouldBe(1);
            resultsFloat.Length.ShouldBe(1);
            resultsLong.Length.ShouldBe(1);
            resultsValue.Length.ShouldBe(1);
        }

        [Fact]
        public void TestQueryExpression()
        {
            var dto1 = 168;
            var dto2 = 68;

            DictionaryObject?[] results1;
            DictionaryObject?[] results2;

            using (var document = new MutableDocument("TestQueryExpression")) {
                document.SetInt("onesixeight", dto1);
                DefaultCollection.Save(document);
            }

            using (var from = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))) {
                var l = from.Execute().ToArray();
                l.Count().ShouldBe(1);
            }
;
            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("onesixeight").Is(Expression.Int(dto1))
                )) {
                results1 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("onesixeight").IsNot(Expression.Int(dto2))
                )) {
                results2 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            results1.Length.ShouldBe(1);
            results2.Length.ShouldBe(1);
        }

        [Fact]
        public void TestQueryResultSet()
        {
            QueryResultSet resultset;
            var dto1 = DateTimeOffset.UtcNow;
            using (var document = new MutableDocument("TestQueryResultSet")) {
                document.SetDate("timestamp", dto1);
                DefaultCollection.Save(document);
            }
            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("timestamp").EqualTo(Expression.Date(dto1))
                )) {
                resultset = (QueryResultSet)query.Execute();
                resultset.Collection.ShouldBe(DefaultCollection);
                List<Result> allRes = resultset.AllResults();
                allRes.Count.ShouldBe(1);
                var columnName = resultset.ColumnNames;
            }
            resultset.Refresh();
        }

        [ForIssue("#1052")]
        [Fact]
        public void TestQueryDateTimeOffset()
        {
            var dto1 = DateTimeOffset.UtcNow;
            var dto2 = new DateTimeOffset(15, new TimeSpan(0));
            var dto3 = new DateTimeOffset(15000, new TimeSpan(0));

            DictionaryObject?[] results1;
            DictionaryObject?[] results2;
            DictionaryObject?[] results3;

            using (var document = new MutableDocument("TestQueryDateTimeOffset.1")) {
                document.SetDate("timestamp", dto1);
                DefaultCollection.Save(document);
            }
            using (var document = new MutableDocument("TestQueryDateTimeOffset.2")) {
                document.SetDate("timestamp", dto2);
                DefaultCollection.Save(document);
            }
            using (var document = new MutableDocument("TestQueryDateTimeOffset.3")) {
                document.SetDate("timestamp", dto3);
                DefaultCollection.Save(document);
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("timestamp").EqualTo(Expression.Date(dto1))
                )) {
                results1 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("timestamp").EqualTo(Expression.Date(dto2))
                )) {
                results2 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            using (var query = QueryBuilder
                .Select(SelectResult.All())
                .From(DataSource.Collection(DefaultCollection))
                .Where(
                    Expression.Property("timestamp").EqualTo(Expression.Date(dto3))
                )) {
                results3 = query.Execute().Select(r => r.GetDictionary(Db.Name)).ToArray();
            }

            results1.Length.ShouldBe(1);
            results2.Length.ShouldBe(1);
            results3.Length.ShouldBe(1);
        }

        [ForIssue("couchbase-lite-core/497")]
        [Fact]
        public void TestQueryJoinAndSelectAll()
        {
            LoadNumbers(100);

            using (var doc1 = new MutableDocument("joinme")) {
                doc1.SetInt("theone", 42);
                DefaultCollection.Save(doc1);
            }

            var mainDS = DataSource.Collection(DefaultCollection).As("main");
            var secondaryDS = DataSource.Collection(DefaultCollection).As("secondary");

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
                        row.GetDictionary("main")?.GetInt("number2").ShouldBe(59);
                        row.GetDictionary("secondary").ShouldBeNull();
                    } else if (n == 42) {
                        WriteLine($"42: {JsonConvert.SerializeObject(row.ToDictionary())}");
                        row.GetDictionary("main")?.GetInt("number2").ShouldBe(58);
                        row.GetDictionary("secondary")?.GetInt("theone").ShouldBe(42);
                    }
                });

                numRows.ShouldBe(101);
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
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Property("local").Ascending())) {
                foreach (var result in q.Execute()) {
                    result.GetLong(0).ShouldBe(expectedLocal[i]);
                    result.GetLong(1).ShouldBe(expectedJST[i]);
                    result.GetLong(2).ShouldBe(expectedJST[i]);
                    result.GetLong(3).ShouldBe(expectedPST[i]);
                    result.GetLong(4).ShouldBe(expectedPST[i]);
                    result.GetLong(5).ShouldBe(expectedUTC[i]);
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
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Property("local").Ascending())) {
                foreach (var result in q.Execute()) {
                    result.GetString(0).ShouldBe(expectedLocal.ElementAt(i));
                    result.GetString(1).ShouldBe(expectedJST[i]);
                    result.GetString(2).ShouldBe(expectedJST[i]);
                    result.GetString(3).ShouldBe(expectedPST[i]);
                    result.GetString(4).ShouldBe(expectedPST[i]);
                    result.GetString(5).ShouldBe(expectedUTC[i]);
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
                    DefaultCollection.Save(doc);
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
                .From(DataSource.Collection(DefaultCollection))
                .OrderBy(Ordering.Property("timestamp").Ascending())) {
                foreach (var result in q.Execute()) {
                    result.GetString(0).ShouldBe(expectedLocal.ElementAt(i));
                    result.GetString(1).ShouldBe(expectedUTC[i]);
                    i++;
                }
            }
        }

        [Fact]
        public void TestSelectEmptyClause()
        {
            LoadNumbers(100);
            using (var q = QueryBuilder.Select().From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("number1").LessThan(Expression.Int(10))).OrderBy(Ordering.Property("number1"))) {
                var res = q.Execute();
                foreach(var result in res) {
                    result.Keys.ElementAt(0).ShouldBe("_id");
                    result.Keys.ElementAt(1).ShouldBe("_sequence");
                }
            }
        }

        [Fact]
        public void TestQueryResultTypesInDictionaryToJSON()
        {
            var dic = PopulateDictData();
            using (var doc = new MutableDocument("test_doc")) {
                foreach (var item in dic) {
                    doc.SetValue(item.Key, item.Value);
                }

                DefaultCollection.Save(doc);
            }

            using (var q = QueryBuilder.Select(SelectResult.Property("nullObj"),
                    SelectResult.Property("byteVal"),
                    SelectResult.Property("sbyteVal"),
                    SelectResult.Property("ushortVal"),
                    SelectResult.Property("shortVal"),
                    SelectResult.Property("intVal"),
                    SelectResult.Property("uintVal"),
                    SelectResult.Property("longVal"),
                    SelectResult.Property("ulongVal"),
                    SelectResult.Property("boolVal"),
                    SelectResult.Property("stringVal"),
                    SelectResult.Property("floatVal"),
                    SelectResult.Property("doubleVal"),
                    SelectResult.Property("dateTimeOffset"),
                    SelectResult.Property("array"),
                    SelectResult.Property("dictionary"),
                    SelectResult.Property("blob"))
                .From(DataSource.Collection(DefaultCollection))) {
                VerifyQuery(q, (n, row) =>
                {
                    var json = row.ToJSON();
                    ValidateToJsonValues(json, dic);
                });
            }
        }

        [ForIssue("CBL-3356")]
        [Fact]
        public void TestSelectGreatOrEqualThan32Items()
        {
            using (var q = Db.CreateQuery(@"select
                `1`,`2`,`3`,`4`,`5`,`6`,`7`,`8`,`9`,`10`,`11`,`12`,
                `13`,`14`,`15`,`16`,`17`,`18`,`19`,`20`,`21`,`22`,`23`,`24`,
                `25`,`26`,`27`,`28`,`29`,`30`,`31`,`32`, `key` from _ limit 1")) {
                //ColumnNames is an internal property.
                ((QueryBase)q).ColumnNames.Count.ShouldBeGreaterThanOrEqualTo(32);
            }
        }

        [ForIssue("CBL-4010")]
        [Fact]
        public void TestFullTextIndexExpression()
        {
            Db.GetDefaultCollection()
                .CreateIndex("passageIndex", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("passage"))
                .SetLanguage("en"));

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("passage", "The boy said to the child, 'Mommy, I want a cat.'")
                    .SetString("lang", "en");
                Db.GetDefaultCollection().Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("passage", "The mother replied 'No, you already have too many cats.'")
                    .SetString("lang", "en");
                Db.GetDefaultCollection().Save(doc2);
            }

            var plainIndex = Expression.FullTextIndex("passageIndex");
            var qualifiedIndex = Expression.FullTextIndex("passageIndex").From("main");

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(Db.GetDefaultCollection()).As("main"))
                .Where(FullTextFunction.Match(plainIndex, "cat"))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).ShouldBe($"doc{n}");
                });
                count.ShouldBe(2);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(Db.GetDefaultCollection()).As("main"))
                .Where(FullTextFunction.Match(qualifiedIndex, "cat"))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).ShouldBe($"doc{n}");
                });
                count.ShouldBe(2);
            }
        }

        [ForIssue("CBL-3994")]
        [Fact]
        public void TestFTSQueryWithJoin()
        {
            Db.GetDefaultCollection()
                .CreateIndex("passageIndex", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("passage"))
                .SetLanguage("en"));

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("passage", "The boy said to the child, 'Mommy, I want a cat.'")
                    .SetString("lang", "en");
                Db.GetDefaultCollection().Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("passage", "The mother replied 'No, you already have too many cats.'")
                    .SetString("lang", "en");
                Db.GetDefaultCollection().Save(doc2);
            }

            var plainIndex = Expression.FullTextIndex("passageIndex");
            var qualifiedIndex = Expression.FullTextIndex("passageIndex").From("main");

            // CBL-3994
            //using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID.From("main")))
            //    .From(DataSource.Collection(Db.GetDefaultCollection()).As("main"))
            //    .Join(Join.LeftJoin(DataSource.Collection(Db.GetDefaultCollection()).As("secondary"))
            //        .On(Expression.Property("lang").From("main").EqualTo(Expression.Property("lang").From("secondary"))))
            //    .Where(FullTextFunction.Match(plainIndex, "cat"))
            //    .OrderBy(Ordering.Expression(Meta.ID.From("main")))) {
            //    var count = VerifyQuery(q, (n, row) =>
            //    {
            //        row.GetString(0).Should().StartWith("doc");
            //    });
            //    count.ShouldBe(4);
            //}

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID.From("main")))
                .From(DataSource.Collection(Db.GetDefaultCollection()).As("main"))
                .Join(Join.LeftJoin(DataSource.Collection(Db.GetDefaultCollection()).As("secondary"))
                    .On(Expression.Property("lang").From("main").EqualTo(Expression.Property("lang").From("secondary"))))
                .Where(FullTextFunction.Match(qualifiedIndex, "cat"))
                .OrderBy(Ordering.Expression(Meta.ID.From("main")))) {
                var count = VerifyQuery(q, (n, row) =>
                {
                    row.GetString(0).ShouldStartWith("doc");
                });
                count.ShouldBe(4);
            }
        }

        [Fact]
        public void TestSelectAllResultKey()
        {
            // Section 8.11.10 / 8.12.4 - CBL Helium - Scope and Collection API
            using var c = Db.CreateCollection("flowers", "test");

            var sqlInputAndResult = new List<(string queryID, string resultName)>
            {
               (Db.Name, Db.Name),
               ("_", "_"),
               ("_default._default", "_default"),
               ("test.flowers", "flowers"),
               ("test.flowers as f", "f")
            };

#pragma warning disable CS0618 // Type or member is obsolete
            var queryBuilderInputAndResult = new List<(IDataSource querySource, string resultName)>
            {
                (DataSource.Database(Db), Db.Name),
                (DataSource.Collection(DefaultCollection).As("db-alias"), "db-alias"),
                (DataSource.Collection(DefaultCollection), "_default"),
                (DataSource.Collection(c), "flowers"),
                (DataSource.Collection(c).As("collection-alias"), "collection-alias")
            };
#pragma warning restore CS0618 // Type or member is obsolete


            using var doc1 = new MutableDocument("foo1");
            doc1.SetString("test", "test");
            c.Save(doc1);

            using var doc2 = new MutableDocument("foo2");
            doc2.SetString("test", "test");
            DefaultCollection.Save(doc2);

            foreach (var pair in sqlInputAndResult) {
                using var q = Db.CreateQuery($"SELECT * FROM {pair.queryID}");
                var results = q.Execute();
                results.First()[pair.resultName].Exists.ShouldBeTrue($"because otherwise the result column name {pair.resultName} was not present");
            }

            foreach (var pair in queryBuilderInputAndResult) {
                using var q = QueryBuilder.Select(SelectResult.All()).From(pair.querySource);
                var results = q.Execute();
                results.First()[pair.resultName].Exists.ShouldBeTrue($"because otherwise the result column name {pair.resultName} was not present");
            }
        }

        [Fact]
        public void TestResultSetAfterDispose()
        {
            // Want to make sure any access to a result after its parent set has been 
            // disposed is guarded by an exception to avoid using released native data
            using var doc = new MutableDocument("test");
            var arr = new MutableArrayObject();
            arr.AddInt(1).AddInt(2);
            var dict = new MutableDictionaryObject();
            dict.SetString("foo", "bar");
            var blob = new Blob("application/octet-stream", new byte[] { 1, 2, 3, 4, 5 });
            doc.SetArray("arr", arr);
            doc.SetDictionary("dict", dict);
            doc.SetBlob("blob", blob);
            doc.SetInt("int", 42);
            DefaultCollection.Save(doc);

            using var query = Db.CreateQuery("SELECT arr, dict, blob, int FROM _");
            var rs = query.Execute();
            var r = rs.First();
            var arr1 = r.GetArray("arr");
            arr1.ShouldNotBeNull("because otherwise the query didn't contain 'arr'");
            var dict1 = r.GetDictionary("dict");
            dict1.ShouldNotBeNull("because otherwise the query didn't contain 'dict'");

            rs.Dispose();

            Should.Throw<ObjectDisposedException>(() => r.GetArray("arr"));
            Should.Throw<ObjectDisposedException>(() => r.GetDictionary("dict"));
            Should.Throw<ObjectDisposedException>(() => r.GetBlob("blob"));
            r.GetInt("int").ShouldBe(42);

            Should.Throw<ObjectDisposedException>(() => arr1!.GetValue(0));
            Should.Throw<ObjectDisposedException>(() => dict1!.GetValue("foo"));
            arr.GetInt(0).ShouldBe(1);
            dict.GetString("foo").ShouldBe("bar");
        }

        [Fact]
        [ForIssue("CBL-5727")]
        public void TestColumnNamesAfterDispose()
        {
            var q = Db.CreateQuery(@"select foo from _") as QueryBase;
            q.ShouldNotBeNull();
            q.ColumnNames.Count.ShouldBe(1, "because there is one column");
            q.Dispose();
            Should.Throw<ObjectDisposedException>(() => q.ColumnNames, "because the object was disposed");
        }

        private void CreateDateDocs()
        {
            using (var doc = new MutableDocument()) {
                doc.SetString("local", "1985-10-26");
                DefaultCollection.Save(doc);
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
                    DefaultCollection.Save(doc);
                }
            }
        }

        private Document CreateTaskDocument(string title, bool complete)
        {
            var doc = new MutableDocument();
            doc.SetString("type", "task");
            doc.SetString("title", title);
            doc.SetBoolean("complete", complete);
            DefaultCollection.Save(doc);
            return doc;
        }

        private async Task TestLiveQueryNoUpdateInternal(bool consumeAll)
        {
            LoadNumbers(100);
            using (var q = QueryBuilder.Select().From(DataSource.Collection(DefaultCollection))
                .Where(Expression.Property("number1").LessThan(Expression.Int(10))).OrderBy(Ordering.Property("number1"))) {

                var are = new AutoResetEvent(false);
                var token = q.AddChangeListener(null, (sender, args) =>
                {
                    if (consumeAll) {
                        var rs = args.Results;
                        rs.ToArray().ShouldNotBeNull(); // No-op
                    }

                    are.Set();
                });

                await Task.Delay(500);

                try {
                    // This change will not affect the query results because 'number1 < 10' 
                    // is not true
                    CreateDocInSeries(111, 100);
                    are.WaitOne(5000)
                        .ShouldBeTrue("because the Changed event should fire once for the initial results");
                    are.WaitOne(5000).ShouldBeFalse("because the Changed event should not fire needlessly");
                } finally {
                    token.Remove();
                }
            }
        }

        private bool TestWhereCompareValidator(IDictionary<string, object?> properties, object? context)
        {
            var ctx = (Func<int, bool>)context!;
            return ctx(Convert.ToInt32(properties["number1"]));
        }

        private bool TestWhereMathValidator(IDictionary<string, object?> properties, object? context)
        {
            var ctx = (Func<int, int, bool>)context!;
            return ctx(Convert.ToInt32(properties["number1"]), Convert.ToInt32(properties["number2"]));
        }

        private bool TestWhereBetweenValidator(IDictionary<string, object?> properties, object? context)
        {
            return Convert.ToInt32(properties["number1"]) >= 3 &&
                   Convert.ToInt32(properties["number1"]) <= 7;
        }

        private bool TestWhereAndValidator(IDictionary<string, object?> properties, object? context)
        {
            return Convert.ToInt32(properties["number1"]) > 3 &&
                   Convert.ToInt32(properties["number2"]) > 3;
        }

        private bool TestWhereOrValidator(IDictionary<string, object?> properties, object? context)
        {
            return Convert.ToInt32(properties["number1"]) < 3 ||
                   Convert.ToInt32(properties["number2"]) < 3;
        }

        private void RunTestWithNumbers(IList<int> expectedResultCount,
            IList<Tuple<IExpression, Func<IDictionary<string, object?>, object?, bool>, object?>> validator)
        {
            int index = 0;
            foreach (var c in validator) {
                using (var q = QueryBuilder.Select(DocID).From(DataSource.Collection(DefaultCollection)).Where(c.Item1)) {
                    var lastN = 0;
                    VerifyQuery(q, (n, row) =>
                    {
                        row.GetString(0).ShouldNotBeNull("because otherwise the query returned incorrect information");
                        var doc = DefaultCollection.GetDocument(row.GetString(0)!);
                        doc.ShouldNotBeNull($"because otherwise document '{row.GetString(0)}' didn't exist");
                        var props = doc!.ToDictionary();
                        c.Item2(props, c.Item3).ShouldBeTrue("because otherwise the row failed validation");
                        lastN = n;
                    });

                    lastN
                        .ShouldBe(expectedResultCount[index++], "because otherwise there was an incorrect number of rows");
                }
            }
        }

        private Document CreateDocInSeries(int entry, int max)
        {
            var docID = $"doc{entry}";
            var doc = new MutableDocument(docID);
            doc.SetInt("number1", entry);
            doc.SetInt("number2", max - entry);
            DefaultCollection.Save(doc);
            return doc;
        }
    }
}
