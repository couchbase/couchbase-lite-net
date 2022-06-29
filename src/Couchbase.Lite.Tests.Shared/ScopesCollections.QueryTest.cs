//
//  ScopesCollectionsQueryTest.cs
//
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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
    public class ScopesCollectionsQueryTest : TestCase
    {
#if !WINDOWS_UWP
        public ScopesCollectionsQueryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestQueryObserverScopesCollections()
        {
            CollA = Db.CreateCollection("collA", "scopeA");
            var n1qlQ = Db.CreateQuery("SELECT META().id, contact FROM scopeA.collA WHERE contact.address.state = 'CA'");
            TestQueryObserverWithQuery(n1qlQ, isDefaultCollection: false);
            n1qlQ.Dispose();
            var query = QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
                .From(DataSource.Collection(CollA))
                .Where(Expression.Property("contact.address.state").EqualTo(Expression.String("CA")));
            TestQueryObserverWithQuery(query, isDefaultCollection: false);
            query.Dispose();
        }

        [Fact]
        public void TestMultipleQueryObserversScopesCollections()
        {
            CollA = Db.CreateCollection("collA", "scopeA");
            var n1qlQ = Db.CreateQuery("SELECT META().id, contact FROM scopeA.collA WHERE contact.address.state = 'CA'");
            TestMultipleQueryObserversWithQuery(n1qlQ, isDefaultCollection: false);
            n1qlQ.Dispose();
            var query = QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
                .From(DataSource.Collection(CollA))
                .Where(Expression.Property("contact.address.state").EqualTo(Expression.String("CA")));
            TestMultipleQueryObserversWithQuery(query, isDefaultCollection: false);
            query.Dispose();
        }

        // How to use N1QL Query Parameter
        // https://docs.couchbase.com/couchbase-lite/3.0/csharp/query-n1ql-mobile.html#lbl-query-params
        [Fact]
        public void TestQueryObserverWithChangingQueryParametersScopesCollections()
        {
            CollA = Db.CreateCollection("collA", "scopeA");
            var n1qlQ = Db.CreateQuery("SELECT META().id, contact FROM scopeA.collA WHERE contact.address.state = $state");
            TestQueryObserverWithChangingQueryParametersWithQuery(n1qlQ, isDefaultCollection: false);
            n1qlQ.Dispose();
            var query = QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
                .From(DataSource.Collection(CollA))
                .Where(Expression.Property("contact.address.state").EqualTo(Expression.Parameter("state")));
            TestQueryObserverWithChangingQueryParametersWithQuery(query, isDefaultCollection: false);
            query.Dispose();
        }

#if !CBL_NO_EXTERN_FILES
        [Fact]
        public void TestGroupByScopesCollections()
        {
            var expectedStates = new[] { "AL", "CA", "CO", "FL", "IA" };
            var expectedCounts = new[] { 1, 6, 1, 1, 3 };
            var expectedZips = new[] { "35243", "94153", "81223", "33612", "50801" };

            LoadJSONResource("names_100", isDefaultCollection: false);

            var STATE = Expression.Property("contact.address.state");
            var gender = Expression.Property("gender");
            var COUNT = Function.Count(Expression.All());
            var zip = Expression.Property("contact.address.zip");
            var MAXZIP = Function.Max(zip);

            using (var q = QueryBuilder.Select(SelectResult.Expression(STATE), SelectResult.Expression(COUNT), SelectResult.Expression(MAXZIP))
                .From(DataSource.Collection(CollA))
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
                .From(DataSource.Collection(CollA))
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
        public void TestNoWhereQueryScopesCollections()
        {
            LoadJSONResource("names_100", isDefaultCollection: false);
            using (var q = QueryBuilder.Select(DocID, Sequence).From(DataSource.Collection(CollA))) {
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var expectedID = $"doc-{n:D3}";
                    row.GetString(0).Should().Be(expectedID, "because otherwise the IDs were out of order");
                    row.GetLong(1).Should().Be(n, "because otherwise the sequences were out of order");

                    var doc = CollA.GetDocument(row.GetString(0));
                    doc.Id.Should().Be(expectedID, "because the document ID on the row should match the document");
                    doc.Sequence.Should()
                        .Be((ulong)n, "because the sequence on the row should match the document");
                });

                numRows.Should().Be(100, "because otherwise the incorrect number of rows was returned");
            }
        }

        [Fact]
        public void TestOrderByScopesCollections()
        {
            LoadJSONResource("names_100", isDefaultCollection: false);
            foreach (var ascending in new[] { true, false })
            {
                IOrdering order;
                if (ascending) {
                    order = Ordering.Property("name.first").Ascending();
                } else {
                    order = Ordering.Property("name.first").Descending();
                }

                using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                    .From(DataSource.Collection(CollA)).OrderBy(order)) {
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
        public void TestQuantifiedOperatorsScopesCollections()
        {
            LoadJSONResource("names_100", isDefaultCollection: false);

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(CollA))
                .Where(ArrayExpression.Any(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("climbing"))))) {
                var expected = new[] { "doc-017", "doc-021", "doc-023", "doc-045", "doc-060" };
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id"));
                received.Should().BeEquivalentTo(expected);
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(CollA))
                .Where(ArrayExpression.Every(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("taxes"))))) {
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id")).ToList();
                received.Count.Should().Be(42, "because empty array results are included");
                received[0].Should().Be("doc-007");
            }

            using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                .From(DataSource.Collection(CollA))
                .Where(ArrayExpression.AnyAndEvery(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                    .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("taxes"))))) {
                var results = q.Execute();
                var received = results.Select(x => x.GetString("id")).ToList();
                received.Count.Should().Be(0, "because nobody likes taxes...");
            }
        }

        [Fact]
        public void TestQueryResultScopesCollections()
        {
            LoadJSONResource("names_100", isDefaultCollection: false);

            var FNAME = Expression.Property("name.first");
            var LNAME = Expression.Property("name.last");
            var GENDER = Expression.Property("gender");
            var CITY = Expression.Property("contact.address.city");

            var RES_FNAME = SelectResult.Expression(FNAME).As("firstname");
            var RES_LNAME = SelectResult.Expression(LNAME).As("lastname");
            var RES_GENDER = SelectResult.Expression(GENDER);
            var RES_CITY = SelectResult.Expression(CITY);

            using (var q = QueryBuilder.Select(RES_FNAME, RES_LNAME, RES_GENDER, RES_CITY)
                .From(DataSource.Collection(CollA))) {
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
        public void TestWhereInScopesCollections()
        {
            LoadJSONResource("names_100", isDefaultCollection: false);

            var expected = new[] { "Marcy", "Margaretta", "Margrett", "Marlen", "Maryjo" };
            var inExpression = expected.Select(Expression.String); // Note, this is LINQ Select, so don't get confused

            var firstName = Expression.Property("name.first");
            using (var q = QueryBuilder.Select(SelectResult.Expression(firstName))
                .From(DataSource.Collection(CollA))
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
        public void TestWhereLikeScopesCollections()
        {
            LoadJSONResource("names_100", isDefaultCollection: false);

            var where = Expression.Property("name.first").Like(Expression.String("%Mar%"));
            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                .From(DataSource.Collection(CollA))
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
        public void TestWhereRegexScopesCollections()
        {
            LoadJSONResource("names_100", isDefaultCollection: false);

            var where = Expression.Property("name.first").Regex(Expression.String("^Mar.*"));
            using (var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                .From(DataSource.Collection(CollA))
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

#endif
    }
}
