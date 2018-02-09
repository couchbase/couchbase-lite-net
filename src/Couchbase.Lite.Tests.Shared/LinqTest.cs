//
//  LinqTest.cs
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
#if CBL_LINQ
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Couchbase.Lite.Query;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif
using LinqExpression = System.Linq.Expressions.Expression;

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class LinqTest : TestCase
    {
#if !WINDOWS_UWP
        public LinqTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        // {"name":{"first":"Lue","last":"Laserna"},"gender":"female","birthday":"1983-09-18",
        // "contact":{"address":{"street":"19 Deer Loop","zip":"90732","city":"San Pedro","state":"CA"},
        // "email":["lue.laserna@nosql-matters.org","laserna@nosql-matters.org"],"region":"310",
        // "phone":["310-8268551","310-7618427"]},"likes":["chatting"],"memberSince":"2011-05-05"}
        private sealed class NamesModel : IDocumentModel, IDisposable
        {
            [JsonProperty("name")]
            public Name Name { get; set; }

            [JsonProperty("gender")]
            public string Gender { get; set; }

            [JsonProperty("birthday")]
            public string Birthday { get; set; }

            [JsonProperty("contact")]
            public Contact Contact { get; set; }

            [JsonProperty("likes")]
            public List<string> Likes { get; set; }

            [JsonProperty("memberSince")]
            public string MemberSince { get; set; }

            public Document Document { get; set; }

            public void Dispose()
            {
                Document?.Dispose();
            }
        }

        private sealed class Contact
        {
            [JsonProperty("address")]
            public Address Address { get; set; }

            [JsonProperty("email")]
            public List<string> Email { get; set; }

            [JsonProperty("region")]
            public string Region { get; set; }

            [JsonProperty("phone")]
            public List<string> Phone { get; set; }
        }

        private sealed class Address
        {
            [JsonProperty("street")]
            public string Street { get; set; }

            [JsonProperty("city")]
            public string City { get; set; }

            [JsonProperty("state")]
            public string State { get; set; }

            [JsonProperty("zip")]
            public string Zip { get; set; }
        }

        private sealed class Name
        {
            [JsonProperty("first")]
            public string First { get; set; }

            [JsonProperty("last")]
            public string Last { get; set; }
        }

        private sealed class SentencesModel : IDocumentModel, IDisposable
        {
            [JsonProperty("sentence")]
            public string Sentence { get; set; }

            public Document Document { get; set; }

            public void Dispose()
            {
                Document?.Dispose();
            }
        }

        private sealed class SimpleModel : IDocumentModel, IDisposable
        {
            public string Name { get; set; }

            public string Address { get; set; }

            public int Age { get; set; }

            public object Work { get; set; }

            public Document Document { get; set; }

            public void Dispose()
            {
                Document?.Dispose();
            }
        }

        private sealed class NumbersModel : IDocumentModel, IDisposable
        {
            public Document Document { get; set; }

            public int Number1 { get; set; }

            public int Number2 { get; set; }

            public void Dispose()
            {
                Document?.Dispose();
            }
        }

        private sealed class JoinedNumbersModel : IDocumentModel, IDisposable
        {
            public int TheOne { get; set; }

            public Document Document { get; set; }

            public void Dispose()
            {
                Document?.Dispose();
            }
        }

        private sealed class DoubleModel : IDocumentModel, IDisposable
        {
            public double Number { get; set; }

            public Document Document { get; set; }

            public void Dispose()
            {
                Document?.Dispose();
            }
        }

        [Fact]
        public void TestNoWhereQuery()
        {
            LoadJSONResource("names_100");

            var q = from x in new DatabaseQueryable<NamesModel>(Db)
                select new object[] { x.Id(), x.Sequence() };

            var index = 1;
            foreach (var result in q) {
                var expectedID = $"doc-{index:D3}";

                result[0].Should().Be(expectedID);
                result[1].Should().Be((long) index);
                index++;
            }

            index.Should().Be(101);
        }

        [Fact]
        public void TestWhereNull()
        {
            SimpleModel doc1 = new SimpleModel(), doc2 = new SimpleModel();
            doc1.Document = new MutableDocument("doc1");
            doc1.Name = "Scott";
            Db.Save(doc1);
            
            doc2.Document = new MutableDocument("doc2");
            doc2.Name = "Tiger";
            doc2.Address = "123 1st ave.";
            doc2.Age = 20;
            Db.Save(doc2);

            var q = from x in new DatabaseQueryable<SimpleModel>(Db)
                where x.Name != null
                select x.Id();

            var expectedDocs = new[] { "doc1", "doc2" };
            VerifyQuery(q, (n, r) =>
            {
                if (n <= expectedDocs.Length) {
                    var doc = expectedDocs[n - 1];
                    r.Should().Be(doc);
                }
            });

            q = from x in new DatabaseQueryable<SimpleModel>(Db)
                where x.Name == null
                select x.Id();

            expectedDocs = new string[0];
            VerifyQuery(q, (n, r) =>
            {
                if (n <= expectedDocs.Length) {
                    var doc = expectedDocs[n - 1];
                    r.Should().Be(doc);
                }
            });
        }

        [Fact]
        public void TestWhereComparison()
        {
            // Partial LINQ expression (x => x.Number1)
            var parameter = LinqExpression.Parameter(typeof(NumbersModel), "x");
            var n1 = LinqExpression.Property(parameter, "Number1");

            var l3 = new Func<int, bool>(n => n < 3);
            var le3 = new Func<int, bool>(n => n <= 3);
            var g6 = new Func<int, bool>(n => n > 6);
            var ge6 = new Func<int, bool>(n => n >= 6);
            var e7 = new Func<int, bool>(n => n == 7);
            var ne7 = new Func<int, bool>(n => n != 7);
            var cases = new[] {
                Tuple.Create((LinqExpression)LinqExpression.LessThan(n1, LinqExpression.Constant(3)),
                    (Func<NumbersModel, object, bool>) TestWhereCompareValidator, (object)l3, parameter),
                Tuple.Create((LinqExpression)LinqExpression.LessThanOrEqual(n1, LinqExpression.Constant(3)),
                    (Func<NumbersModel, object, bool>) TestWhereCompareValidator, (object)le3, parameter),
                Tuple.Create((LinqExpression)LinqExpression.GreaterThan(n1, LinqExpression.Constant(6)),
                    (Func<NumbersModel, object, bool>) TestWhereCompareValidator, (object)g6, parameter),
                Tuple.Create((LinqExpression)LinqExpression.GreaterThanOrEqual(n1, LinqExpression.Constant(6)),
                    (Func<NumbersModel, object, bool>) TestWhereCompareValidator, (object)ge6, parameter),
                Tuple.Create((LinqExpression)LinqExpression.Equal(n1, LinqExpression.Constant(7)),
                    (Func<NumbersModel, object, bool>) TestWhereCompareValidator, (object)e7, parameter),
                Tuple.Create((LinqExpression)LinqExpression.NotEqual(n1, LinqExpression.Constant(7)),
                    (Func<NumbersModel, object, bool>) TestWhereCompareValidator, (object)ne7, parameter)
            };

            LoadModelNumbers(10);
            Db.Count.Should().Be(10);
            RunTestWithNumbers(new[] {2, 3, 4, 5, 1, 9}, cases);
        }

        [Fact]
        public void TestWhereArithmetic()
        {
            var parameter = LinqExpression.Parameter(typeof(NumbersModel), "x");
            var n1 = LinqExpression.Property(parameter, "Number1");
            var n2 = LinqExpression.Property(parameter, "Number2");

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
                Tuple.Create((LinqExpression)LinqExpression.GreaterThan(LinqExpression.Multiply(n1, LinqExpression.Constant(2)), LinqExpression.Constant(8)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)m2g8, parameter),
                Tuple.Create((LinqExpression)LinqExpression.GreaterThan(LinqExpression.Divide(n1, LinqExpression.Constant(2)), LinqExpression.Constant(3)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)d2g3, parameter),
                Tuple.Create((LinqExpression)LinqExpression.Equal(LinqExpression.Modulo(n1, LinqExpression.Constant(2)), LinqExpression.Constant(0)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)m2e0, parameter),
                Tuple.Create((LinqExpression)LinqExpression.GreaterThan(LinqExpression.Add(n1, LinqExpression.Constant(5)), LinqExpression.Constant(10)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)a5g10, parameter),
                Tuple.Create((LinqExpression)LinqExpression.GreaterThan(LinqExpression.Subtract(n1, LinqExpression.Constant(5)), LinqExpression.Constant(0)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)s5g0, parameter),
                Tuple.Create((LinqExpression)LinqExpression.GreaterThan(LinqExpression.Multiply(n1, n2), LinqExpression.Constant(10)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)mn2g10, parameter),
                Tuple.Create((LinqExpression)LinqExpression.GreaterThan(LinqExpression.Divide(n2, n1), LinqExpression.Constant(3)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)dn1g3, parameter),
                Tuple.Create((LinqExpression)LinqExpression.Equal(LinqExpression.Modulo(n1, n2), LinqExpression.Constant(0)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)mn2e0, parameter),
                Tuple.Create((LinqExpression)LinqExpression.Equal(LinqExpression.Add(n1, n2), LinqExpression.Constant(10)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)an2e10, parameter),
                Tuple.Create((LinqExpression)LinqExpression.GreaterThan(LinqExpression.Subtract(n1, n2), LinqExpression.Constant(0)),
                    (Func<NumbersModel, object, bool>)TestWhereMathValidator, (object)sn2g0, parameter)
            };

            LoadModelNumbers(10);
            RunTestWithNumbers(new[] {6, 3, 5, 5, 5, 7, 2, 3, 10, 5}, cases);
        }

        [Fact]
        public void TestWhereAndOr()
        {
            var parameter = LinqExpression.Parameter(typeof(NumbersModel), "x");
            var n1 = LinqExpression.Property(parameter, "Number1");
            var n2 = LinqExpression.Property(parameter, "Number2");
            var cases = new[] {
                Tuple.Create((LinqExpression)LinqExpression.AndAlso(LinqExpression.GreaterThan(n1, LinqExpression.Constant(3)), LinqExpression.GreaterThan(n2, LinqExpression.Constant(3))),
                    (Func<NumbersModel, object, bool>)TestWhereAndValidator, default(object), parameter),
                Tuple.Create((LinqExpression)LinqExpression.OrElse(LinqExpression.LessThan(n1, LinqExpression.Constant(3)), LinqExpression.LessThan(n2, LinqExpression.Constant(3))),
                    (Func<NumbersModel, object, bool>)TestWhereOrValidator, default(object), parameter)
            };
            LoadModelNumbers(10);
            RunTestWithNumbers(new[] { 3, 5 }, cases);
        }

        [Fact]
        public void TestWhereIs()
        {
            using (var doc1 = new SimpleModel
                { Name = "string" }) {
                Db.Save(doc1);
            }

            var queryable = new DatabaseQueryable<SimpleModel>(Db);
            var q = from x in queryable
                where x.Name == "string"
                select x.Id();

            var numRows = VerifyQuery(q, (n, row) =>
            {
                using (var doc = Db.GetDocument(row).ToModel<SimpleModel>()) {
                    doc.Name.Should().Be("string");
                }
            });
            numRows.Should().Be(1, "beacuse one row matches the given query");

            q = from x in queryable
                where x.Name != "string1"
                select x.Id();

            numRows = VerifyQuery(q, (n, row) =>
            {
                using (var doc = Db.GetDocument(row).ToModel<SimpleModel>()) {
                    doc.Name.Should().Be("string");
                }
            });
            numRows.Should().Be(1, "because one row matches the 'IS NOT' query");
        }

        [Fact]
        public void TestWhereBetween()
        {
            LoadModelNumbers(10);
            var parameter = LinqExpression.Parameter(typeof(NumbersModel), "x");
            var n1 = LinqExpression.Property(parameter, "Number1");

            // (x => x.Between(3, 7))
            var cases = new[] {
                Tuple.Create((LinqExpression)LinqExpression.Call(typeof(LinqExtensionMethods).GetMethod("Between", new [] { typeof(int), typeof(int), typeof(int) }), n1, LinqExpression.Constant(3), LinqExpression.Constant(7)),
                    (Func<NumbersModel, object, bool>) TestWhereBetweenValidator, (object)null, parameter)
            };

            RunTestWithNumbers(new[] { 5 }, cases);
        }

        [Fact]
        public void TestWhereIn()
        {
            LoadJSONResource("names_100");

            var expected = new[] {"Marcy", "Margaretta", "Margrett", "Marlen", "Maryjo" };

            var queryable = new DatabaseQueryable<NamesModel>(Db);
            var q = from x in queryable
                where expected.Contains(x.Name.First)
                orderby x.Name.First
                select x.Name.First;
                    
            var numRows = VerifyQuery(q, (n, row) =>
            {
                var name = row;
                name.Should().Be(expected[n - 1], "because otherwise incorrect rows were returned");
            });

            numRows.Should().Be(expected.Length, "because otherwise an incorrect number of rows were returned");
        }

        [Fact]
        public void TestWhereLike()
        {
            LoadJSONResource("names_100");

            var queryable = new DatabaseQueryable<NamesModel>(Db);
            var q = from x in queryable
                where x.Name.First.Like("%Mar%")
                orderby x.Name.First ascending
                select x.Name.First;

            var firstNames = new List<string>();
            var numRows = VerifyQuery(q, (n, row) =>
            {
                var firstName = row;
                if (firstName != null) {
                    firstNames.Add(firstName);
                }
            });

            numRows.Should().Be(5, "because there are 5 rows like that in the data source");
            firstNames.Should()
                .OnlyContain(str => str.Contains("Mar"), "because otherwise an incorrect entry came in");
        }

        [Fact]
        public void TestWhereRegex()
        {
            LoadJSONResource("names_100");
            var regex = new Regex("^Mar.*");

            var queryable = new DatabaseQueryable<NamesModel>(Db);
            var q = from x in queryable
                where regex.IsMatch(x.Name.First)
                orderby x.Name.First
                select x.Name.First;

            var firstNames = new List<string>();
            var numRows = VerifyQuery(q, (n, row) =>
            {
                var firstName = row;
                if (firstName != null) {
                    firstNames.Add(firstName);
                }
            });

            numRows.Should().Be(5, "because there are 5 rows like that in the data source");
            firstNames.Should()
                .OnlyContain(str => regex.IsMatch(str), "because otherwise an incorrect entry came in");
        }

        [Fact]
        public void TestOrderBy()
        {
            LoadJSONResource("names_100");
            foreach (var ascending in new[] {true, false}) {
                var queryable = new DatabaseQueryable<NamesModel>(Db);
                IQueryable<string> q = null;
                if (ascending) {
                    q = from x in queryable
                        orderby x.Name.First
                        select x.Name.First;
                } else {
                    q = from x in queryable
                        orderby x.Name.First descending 
                        select x.Name.First;
                }

                var firstNames = new List<object>();
                var numRows = VerifyQuery(q, (n, row) =>
                {
                    var firstName = row;
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

        [Fact]
        public void TestSelectDistinct()
        {
            var doc1 = new SimpleModel { Age = 1 };
            Db.Save(doc1);

            var doc2 = new SimpleModel { Age = 1 };
            Db.Save(doc2);

            var queryable = new DatabaseQueryable<SimpleModel>(Db);
            var q = (from x in queryable
                select x.Age).Distinct();

            var numRows = VerifyQuery(q, (n, row) =>
            {
                var number = row;
                number.Should().Be(1);
            });

            numRows.Should().Be(1, "because there is only one distinct row");
        }

        [Fact]
        public void TestJoin()
        {
            LoadModelNumbers(100);
            var testModel = new JoinedNumbersModel
            {
                TheOne = 42,
                Document = new MutableDocument("joinme")
            };
            Db.Save(testModel);
            
            var queryable = new DatabaseQueryable<NumbersModel>(Db);
            var secondQueryable = new DatabaseQueryable<JoinedNumbersModel>(Db);
            var q = from x in queryable
                from y in secondQueryable
                join secondary in secondQueryable on x.Number1 equals secondary.TheOne
                select x.Number2;

            q.Should().HaveCount(1);
            q.First().Should().Be(58);
        }

        [Fact]
        public void TestAggregateFunctions()
        {
            LoadModelNumbers(100);

            var queryable = new DatabaseQueryable<NumbersModel>(Db);
            var result = (from x in queryable
                select x.Number1).Average();

            result.Should().BeApproximately(50.5, Double.Epsilon);

            queryable = new DatabaseQueryable<NumbersModel>(Db);
            result = (from x in queryable
                select x.Number1).Count();

            result.Should().Be(100);

            queryable = new DatabaseQueryable<NumbersModel>(Db);
            result = (from x in queryable
                select x.Number1).Min();

            result.Should().Be(1);

            queryable = new DatabaseQueryable<NumbersModel>(Db);
            result = (from x in queryable
                select x.Number1).Max();

            result.Should().Be(100);

            queryable = new DatabaseQueryable<NumbersModel>(Db);
            result = (from x in queryable
                select x.Number1).Sum();

            result.Should().Be(5050);
        }

        [Fact]
        public void TestLimit()
        {
            LoadModelNumbers(10);

            var queryable = new DatabaseQueryable<NumbersModel>(Db);
            var q = (from x in queryable
                orderby x.Number1
                select x.Number1).Take(5);

            var expectedNumbers = new[] {1, 2, 3, 4, 5};
            var numRows = VerifyQuery(q, (n, row) =>
            {
                row.Should().Be(expectedNumbers[n - 1]);
            });
        }

        [Fact]
        public void TestLimitOffset()
        {
            LoadModelNumbers(10);

            var queryable = new DatabaseQueryable<NumbersModel>(Db);
            var q = (from x in queryable
                orderby x.Number1
                select x.Number1).Skip(3).Take(5);

            var expectedNumbers = new[] {4, 5, 6, 7, 8};
            var numRows = VerifyQuery(q, (n, row) =>
            {
                row.Should().Be(expectedNumbers[n - 1]);
            });

            numRows.Should().Be(5);
        }

        [Fact]
        public void TestArrayFunctions()
        {
            var model = new NamesModel
            {
                Contact = new Contact
                {
                    Phone = new List<string> { "650-123-0001", "650-123-0002" }
                },
                Document = new MutableDocument("doc1")
            };
            Db.Save(model);

            var queryable = new DatabaseQueryable<NamesModel>(Db);
            var q = from x in queryable
                select x.Contact.Phone.Count;

            var numRows = VerifyQuery(q, (n, r) =>
            {
                r.Should().Be(2);
            });

            var q2 = from x in queryable
                select new
                {
                    TrueVal = x.Contact.Phone.Contains("650-123-0001"),
                    FalseVal = x.Contact.Phone.Contains("650-123-0003")
                };

            numRows = VerifyQuery(q2, (n, r) =>
            {
                r.TrueVal.Should().BeTrue();
                r.FalseVal.Should().BeFalse();
            });

            numRows.Should().Be(1);
        }

        [Fact]
        public void TestMathFunctions()
        {
            const double num = 0.6;
            var model = new DoubleModel
            {
                Number = num,
                Document = new MutableDocument("doc1")
            };

            Db.Save(model);

            var expectedValues = new[] {
                Math.Abs(num), Math.Acos(num), Math.Asin(num), Math.Atan(num),
                Math.Ceiling(num), Math.Exp(num),
                Math.Floor(num), Math.Log(num), Math.Log10(num),
                Math.Round(num), Math.Sin(num), Math.Sqrt(num),
                Math.Tan(num), Math.Truncate(num)
            };

            int index = 0;
            var queryable = new DatabaseQueryable<DoubleModel>(Db);
            var parameter = LinqExpression.Parameter(typeof(DoubleModel), "x");
            foreach (var function in new[] {
                "Abs", "Acos", "Asin", "Atan",
                "Ceiling", "Exp",
                "Floor", "Log", "Log10",
                "Round", "Sin", "Sqrt",
                "Tan", "Truncate"
            }) {
                var methodCall = LinqExpression.Lambda<Func<DoubleModel, double>>(LinqExpression.Call(typeof(Math).GetMethod(function, new[] { typeof(double) }), LinqExpression.Property(parameter, "Number")), parameter);
                var q = queryable.Select(methodCall);
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.Should().Be(expectedValues[index++]);
                });

                numRows.Should().Be(1);
            }
        }

        private void LoadModelNumbers(int num)
        {
            Db.InBatch(() =>
            {
                for (int i = 1; i <= num; i++) {
                    var docID = $"doc{i}";
                    var doc = new MutableDocument(docID);
                    var model = new NumbersModel
                    {
                        Number1 = i,
                        Number2 = num - i,
                        Document = doc
                    };

                    Db.Save(model);
                }
            });
        }

        private void RunTestWithNumbers(IList<int> expectedResultCount,
            IList<Tuple<LinqExpression, Func<NumbersModel, object, bool>, object, ParameterExpression>> validator)
        {
            int index = 0;
            var queryable = new DatabaseQueryable<NumbersModel>(Db);
            foreach (var c in validator) {
                var q = queryable.Where(LinqExpression.Lambda<Func<NumbersModel, bool>>(c.Item1, c.Item4)).Select(x => x.Id());
                
                var lastN = 0;
                VerifyQuery(q, (n, row) =>
                {
                    var doc = Db.GetDocument(row).ToModel<NumbersModel>();
                    c.Item2(doc, c.Item3).Should().BeTrue("because otherwise the row failed validation");
                    lastN = n;
                });

                lastN.Should()
                    .Be(expectedResultCount[index++], "because otherwise there was an incorrect number of rows");
            }
        }

        private bool TestWhereMathValidator(NumbersModel model, object context)
        {
            var ctx = (Func<int, int, bool>)context;
            return ctx(model.Number1, model.Number2);
        }

        private bool TestWhereCompareValidator(NumbersModel model, object context)
        {
            var ctx = (Func<int, bool>)context;
            return ctx(model.Number1);
        }

        private bool TestWhereAndValidator(NumbersModel model, object context)
        {
            return model.Number1 > 3 &&
                   model.Number2 > 3;
        }

        private bool TestWhereOrValidator(NumbersModel model, object context)
        {
            return model.Number1 < 3 ||
                   model.Number2 < 3;
        }

        private bool TestWhereBetweenValidator(NumbersModel model, object context)
        {
            return model.Number1 >= 3 &&
                   model.Number2 <= 7;
        }

        private int VerifyQuery<T>(IQueryable<T> q, Action<int, T> callback)
        {
            var i = 0;
            foreach (var result in q) {
                callback(++i, result);
            }

            return i;
        }
    }
}
#endif