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

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Couchbase.Lite;
using Couchbase.Lite.Query;

using Shouldly;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable AccessToDisposedClosure

namespace Test;

public sealed class ScopesCollectionsQueryTest(ITestOutputHelper output) : TestCase(output)
{
    public enum QueryTestMode
    {
        SQL,
        QueryBuilder
    }


#if !CBL_NO_EXTERN_FILES

#if !SANITY_ONLY
    [Fact]
    public void TestQueryObserver()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        using var n1Qlq = Db.CreateQuery("SELECT META().id, contact FROM scopeA.collA WHERE contact.address.state = 'CA'");
        TestQueryObserverWithQuery(n1Qlq, isLegacy: false);
        using var query = QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
            .From(DataSource.Collection(CollA))
            .Where(Expression.Property("contact.address.state").EqualTo(Expression.String("CA")));
        TestQueryObserverWithQuery(query, isLegacy: false);
    }
#endif

    [Fact]
    public void TestMultipleQueryObservers()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        using var n1Qlq = Db.CreateQuery("SELECT META().id, contact FROM scopeA.collA WHERE contact.address.state = 'CA'");
        TestMultipleQueryObserversWithQuery(n1Qlq, isLegacy: false);
        using var query = QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
            .From(DataSource.Collection(CollA))
            .Where(Expression.Property("contact.address.state").EqualTo(Expression.String("CA")));
        TestMultipleQueryObserversWithQuery(query, isLegacy: false);
    }

    // How to use N1QL Query Parameter
    // https://docs.couchbase.com/couchbase-lite/3.0/csharp/query-n1ql-mobile.html#lbl-query-params
    [Theory]
    [InlineData(QueryTestMode.SQL)]
    [InlineData(QueryTestMode.QueryBuilder)]
    public void TestQueryObserverWithChangingQueryParameters(QueryTestMode testMode)
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        using var query = testMode == QueryTestMode.SQL
            ? Db.CreateQuery("SELECT META().id, contact FROM scopeA.collA WHERE contact.address.state = $state")
            : QueryBuilder.Select(DocID, SelectResult.Expression(Expression.Property("contact")))
                .From(DataSource.Collection(CollA))
                .Where(Expression.Property("contact.address.state").EqualTo(Expression.Parameter("state")));
        TestQueryObserverWithChangingQueryParametersWithQuery(query, isLegacy: false);
    }

    [Fact]
    public void TestGroupBy()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        var expectedStates = new[] { "AL", "CA", "CO", "FL", "IA" };
        var expectedCounts = new[] { 1, 6, 1, 1, 3 };
        var expectedZips = new[] { "35243", "94153", "81223", "33612", "50801" };

        LoadJSONResource("names_100", coll: CollA);

        var sState = Expression.Property("contact.address.state");
        var sGender = Expression.Property("gender");
        var sCount = Function.Count(Expression.All());
        var sZip = Expression.Property("contact.address.zip");
        var sMaxZip = Function.Max(sZip);

        using (var q = QueryBuilder.Select(SelectResult.Expression(sState), SelectResult.Expression(sCount), SelectResult.Expression(sMaxZip))
                   .From(DataSource.Collection(CollA))
                   .Where(sGender.EqualTo(Expression.String("female")))
                   .GroupBy(sState)
                   .OrderBy(Ordering.Expression(sState))) {
            var numRows = VerifyQuery(q, (n, row) =>
            {
                var state = row.GetString(0);
                var count = row.GetInt(1);
                var maxZip = row.GetString(2);
                if (n - 1 >= expectedStates.Length) {
                    return;
                }
                
                state.ShouldBe(expectedStates[n - 1]);
                count.ShouldBe(expectedCounts[n - 1]);
                maxZip.ShouldBe(expectedZips[n - 1]);
            });
            numRows.ShouldBe(31);
        }

        expectedStates = ["CA", "IA", "IN"];
        expectedCounts = [6, 3, 2];
        expectedZips = ["94153", "50801", "47952"];

        using (var q = QueryBuilder.Select(SelectResult.Expression(sState), SelectResult.Expression(sCount), SelectResult.Expression(sMaxZip))
                   .From(DataSource.Collection(CollA))
                   .Where(sGender.EqualTo(Expression.String("female")))
                   .GroupBy(sState)
                   .Having(sCount.GreaterThan(Expression.Int(1)))
                   .OrderBy(Ordering.Expression(sState))) {
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

    [Fact]
    public void TestNoWhereQuery()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        LoadJSONResource("names_100", coll: CollA);
        using var q = QueryBuilder.Select(DocID, Sequence).From(DataSource.Collection(CollA));
        var numRows = VerifyQuery(q, (n, row) =>
        {
            var expectedID = $"doc-{n:D3}";
            row.GetString(0).ShouldBe(expectedID, "because otherwise the IDs were out of order");
            row.GetLong(1).ShouldBe(n, "because otherwise the sequences were out of order");

            var doc = CollA.GetDocument(row.GetString(0)!);
            doc?.Id.ShouldBe(expectedID, "because the document ID on the row should match the document");
            doc?.Sequence
                .ShouldBe((ulong)n, "because the sequence on the row should match the document");
        });

        numRows.ShouldBe(100, "because otherwise the incorrect number of rows was returned");
    }

    [Fact]
    public void TestOrderBy()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        LoadJSONResource("names_100", coll: CollA);
        foreach (var ascending in new[] { true, false })
        {
            var order = ascending ? Ordering.Property("name.first").Ascending() : Ordering.Property("name.first").Descending();

            using var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
                .From(DataSource.Collection(CollA)).OrderBy(order);
            var firstNames = new List<object>();
            var numRows = VerifyQuery(q, (_, row) =>
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

    [Fact]
    public void TestQuantifiedOperators()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        LoadJSONResource("names_100", coll: CollA);

        using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                   .From(DataSource.Collection(CollA))
                   .Where(ArrayExpression.Any(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                       .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("climbing"))))) {
            var expected = new[] { "doc-017", "doc-021", "doc-023", "doc-045", "doc-060" };
            var results = q.Execute();
            var received = results.Select(x => x.GetString("id"));
            received.ShouldBeEquivalentToFluent(expected);
        }

        using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                   .From(DataSource.Collection(CollA))
                   .Where(ArrayExpression.Every(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                       .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("taxes"))))) {
            var results = q.Execute();
            var received = results.Select(x => x.GetString("id")).ToList();
            received.Count.ShouldBe(42, "because empty array results are included");
            received[0].ShouldBe("doc-007");
        }

        using (var q = QueryBuilder.Select(SelectResult.Expression(Meta.ID))
                   .From(DataSource.Collection(CollA))
                   .Where(ArrayExpression.AnyAndEvery(ArrayExpression.Variable("like")).In(Expression.Property("likes"))
                       .Satisfies(ArrayExpression.Variable("like").EqualTo(Expression.String("taxes"))))) {
            var results = q.Execute();
            var received = results.Select(x => x.GetString("id")).ToList();
            received.Count.ShouldBe(0, "because nobody likes taxes...");
        }
    }

    [Fact]
    public void TestQueryResult()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        LoadJSONResource("names_100", coll: CollA);

        var fName = Expression.Property("name.first");
        var lName = Expression.Property("name.last");
        var gender = Expression.Property("gender");
        var city = Expression.Property("contact.address.city");

        var resFName = SelectResult.Expression(fName).As("firstname");
        var resLName = SelectResult.Expression(lName).As("lastname");
        var resGender = SelectResult.Expression(gender);
        var resCity = SelectResult.Expression(city);

        using var q = QueryBuilder.Select(resFName, resLName, resGender, resCity)
            .From(DataSource.Collection(CollA));
        var numRows = VerifyQuery(q, (_, r) =>
        {
            r.GetValue("firstname").ShouldBe(r.GetValue(0));
            r.GetValue("lastname").ShouldBe(r.GetValue(1));
            r.GetValue("gender").ShouldBe(r.GetValue(2));
            r.GetValue("city").ShouldBe(r.GetValue(3));
        });

        numRows.ShouldBe(100);
    }

    [Fact]
    public void TestWhereIn()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        LoadJSONResource("names_100", coll: CollA);

        // ReSharper disable StringLiteralTypo
        var expected = new[] { "Marcy", "Margaretta", "Margrett", "Marlen", "Maryjo" };
        // ReSharper restore StringLiteralTypo
        
        var inExpression = expected.Select(Expression.String); // Note, this is LINQ Select, so don't get confused

        var firstName = Expression.Property("name.first");
        using var q = QueryBuilder.Select(SelectResult.Expression(firstName))
            .From(DataSource.Collection(CollA))
            .Where(firstName.In(inExpression.ToArray()))
            .OrderBy(Ordering.Property("name.first"));
        var numRows = VerifyQuery(q, (n, row) =>
        {
            var name = row.GetString(0);
            name.ShouldBe(expected[n - 1], "because otherwise incorrect rows were returned");
        });

        numRows.ShouldBe(expected.Length, "because otherwise an incorrect number of rows were returned");
    }

    [Fact]
    public void TestWhereLike()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        LoadJSONResource("names_100", coll: CollA);

        var where = Expression.Property("name.first").Like(Expression.String("%Mar%"));
        using var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
            .From(DataSource.Collection(CollA))
            .Where(where)
            .OrderBy(Ordering.Property("name.first").Ascending());
        var firstNames = new List<string>();
        var numRows = VerifyQuery(q, (_, row) =>
        {
            var firstName = row.GetString(0);
            if (firstName != null) {
                firstNames.Add(firstName);
            }
        });

        numRows.ShouldBe(5, "because there are 5 rows like that in the data source");
        firstNames.All(x => x.Contains("Mar")).ShouldBeTrue("because otherwise an incorrect entry came in");
    }

    [Fact]
    public void TestWhereRegex()
    {
        CollA = Db.CreateCollection("collA", "scopeA");
        LoadJSONResource("names_100", coll: CollA);

        var where = Expression.Property("name.first").Regex(Expression.String("^Mar.*"));
        using var q = QueryBuilder.Select(SelectResult.Expression(Expression.Property("name.first")))
            .From(DataSource.Collection(CollA))
            .Where(where)
            .OrderBy(Ordering.Property("name.first").Ascending());
        var firstNames = new List<string>();
        var numRows = VerifyQuery(q, (_, row) =>
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

    #region 8.11 SQL++ Query

    [Fact]
    public void TestQueryDefaultCollection()
    {
        //Test that query the default collection by using each default collection identity works as expected.
        LoadJSONResource("names_100", coll: DefaultCollection);
        var listQueries = new List<string>()
        {
            "SELECT name.first FROM _ ORDER BY name.first LIMIT 1",
            "SELECT name.first FROM _default ORDER BY name.first limit 1",
            $"SELECT name.first FROM {Db.Name} ORDER BY name.first limit 1"
        };

        foreach (var qExp in listQueries) {
            using var q = DefaultCollection.CreateQuery(qExp);
            var res = q.Execute().ToList();
            res.Count.ShouldBe(1);
            res[0].GetString("first").ShouldBe("Abe");
        }
    }

    [Fact]
    public void TestQueryDefaultScope()
    {
        // Test that query a collection in the default scope works as expected.
        using var collWithDefaultScope = Db.CreateCollection("names");
        LoadJSONResource("names_100", coll: collWithDefaultScope);
        var listQueries = new List<string>()
        {
            "SELECT name.first FROM _default.names ORDER BY name.first limit 1",
            "SELECT name.first FROM names ORDER BY name.first limit 1"
        };

        foreach (var qExp in listQueries) {
            using var q = collWithDefaultScope.CreateQuery(qExp);
            var res = q.Execute().ToList();
            res.Count.ShouldBe(1);
            res[0].GetString("first").ShouldBe("Abe");
        }
    }

    [Fact]
    public void TestQueryNamedCollection()
    {
        // Test that query a collection in non-default scope works as expected.
        using var coll = Db.CreateCollection("names", "people");
        LoadJSONResource("names_100", coll: coll);
        using var q = coll.CreateQuery("SELECT name.first FROM people.names ORDER BY name.first limit 1");
        var res = q.Execute().ToList();
        res.Count.ShouldBe(1);
        res[0].GetString("first").ShouldBe("Abe");
    }
        
    [Fact]
    public void TestQueryNonExistingCollection()
    {
        // Test that query non-existing collection returns an error as expected.
        using var coll = Db.CreateCollection("names", "people");
        LoadJSONResource("names_100", coll: coll);
        void BadAction() => coll.CreateQuery("SELECT name.first FROM person.names ORDER BY name.first limit 1");
        Should.Throw<CouchbaseLiteException>(BadAction)
            .Message.ShouldBe("CouchbaseLiteException (LiteCoreDomain / 23): no such collection \"person.names\".");
    }
        
    [Fact]
    public void TestJoinWithCollections()
    {
        // Test that query by joining collections works as expected.
        using var flowersCol = Db.CreateCollection("flowers", "test");
        using var colorsCol = Db.CreateCollection("colors", "test");
        
        // flowers collections
        using (var mdoc = new MutableDocument("c1")) {
            mdoc.SetString("cid", "c1");
            mdoc.SetString("name", "rose");
            flowersCol.Save(mdoc);
        }

        using (var mdoc = new MutableDocument("c2")) {
            mdoc.SetString("cid", "c2");
            mdoc.SetString("name", "hydrangea");
            flowersCol.Save(mdoc);
        }

        // colors collections
        using (var mdoc = new MutableDocument("c1")) {
            mdoc.SetString("cid", "c1");
            mdoc.SetString("color", "red");
            colorsCol.Save(mdoc);
        }

        using (var mdoc = new MutableDocument("c2")) {
            mdoc.SetString("cid", "c2");
            mdoc.SetString("color", "blue");
            colorsCol.Save(mdoc);
        }

        using (var mdoc = new MutableDocument("c3")) {
            mdoc.SetString("cid", "c3");
            mdoc.SetString("color", "white");
            colorsCol.Save(mdoc);
        }

        using (var q = Db.CreateQuery("SELECT a.name, b.color FROM test.flowers a JOIN test.colors b ON a.cid = b.cid ORDER BY a.name")) {
            var res = q.Execute().ToList();
            res.Count().ShouldBe(2);
            res[0].GetString("name").ShouldBe("hydrangea");
            res[0].GetString("color").ShouldBe("blue");
            res[1].GetString("name").ShouldBe("rose");
            res[1].GetString("color").ShouldBe("red");
        }
    }

    #endregion

    #region 8.12 QueryBuilder

    [Fact]
    public void TestQueryBuilderWithDefaultCollectionAsDataSource()
    {
        // Test that query by using the default collection as data source works as expected.
        LoadJSONResource("names_100", coll: DefaultCollection);
        using var q = QueryBuilder.Select(SelectResult.Property("name.first"))
            .From(DataSource.Collection(DefaultCollection))
            .OrderBy(Ordering.Property("name.first"))
            .Limit(Expression.Int(1));
        var res = q.Execute().ToList();
        res.Count.ShouldBe(1);
        res[0].GetString("first").ShouldBe("Abe");
    }

    [Fact]
    public void TestQueryBuilderWithCollectionAsDataSource()
    {
        // Test that query by using a collection as data source works as expected.
        using var coll = Db.CreateCollection("names", "people");
        LoadJSONResource("names_100", coll: coll);
        using var q = QueryBuilder.Select(SelectResult.Property("name.first"))
            .From(DataSource.Collection(coll))
            .OrderBy(Ordering.Property("name.first"))
            .Limit(Expression.Int(1));
        var res = q.Execute().ToList();
        res.Count.ShouldBe(1);
        res[0].GetString("first").ShouldBe("Abe");
    }

    #endregion

    protected override void Dispose(bool disposing) => CollA.Dispose();

#endif
}