// 
//  PredictiveQueryTest.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
#if COUCHBASE_ENTERPRISE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Couchbase.Lite;
using Couchbase.Lite.Enterprise.Query;
using Couchbase.Lite.Query;

using FluentAssertions;

using JetBrains.Annotations;

using LiteCore.Interop;

using Newtonsoft.Json;

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
    public sealed class PredictiveQueryTest : TestCase
    {
#if !WINDOWS_UWP
        public PredictiveQueryTest(ITestOutputHelper output) : base(output)
        {
            Database.Prediction.UnregisterModel(nameof(AggregateModel));
            Database.Prediction.UnregisterModel(nameof(TextModel));
            Database.Prediction.UnregisterModel(nameof(EchoModel));

        }
        #else
        public PredictiveQueryTest()
        {
            Database.Prediction.UnregisterModel(nameof(AggregateModel));
            Database.Prediction.UnregisterModel(nameof(TextModel));
            Database.Prediction.UnregisterModel(nameof(EchoModel));
        }
#endif

        //[Fact]
        //public void TestRegisterAndUnregisterModel()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    var input = Expression.Dictionary(new Dictionary<string, object>
        //    {
        //        ["numbers"] = Expression.Property("numbers")
        //    });
        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
        //            SelectResult.Expression(Function.Prediction(aggregateModel.Name, input)))
        //        .From(DataSource.Database(Db))) {
        //        Action badAction = () => q.Execute();
        //        badAction.Should().Throw<CouchbaseSQLiteException>().Which.Error.Should().Be((int) SQLiteStatus.Error);

        //        aggregateModel.RegisterModel();
        //        var numRows = VerifyQuery(q, (i, result) =>
        //        {
        //            var numbers = result.GetArray(0)?.ToList();
        //            numbers.Should().NotBeEmpty("because otherwise the data didn't come through");
        //            var pred = result.GetDictionary(1);
        //            pred.Should().NotBeNull();
        //            pred.GetLong("sum").Should().Be(numbers.Cast<long>().Sum());
        //            pred.GetLong("min").Should().Be(numbers.Cast<long>().Min());
        //            pred.GetLong("max").Should().Be(numbers.Cast<long>().Max());
        //            pred.GetDouble("avg").Should().Be(numbers.Cast<long>().Average());
        //        });

        //        numRows.Should().Be(2);
        //        aggregateModel.UnregisterModel();
        //        badAction.Should().Throw<CouchbaseSQLiteException>().Which.Error.Should().Be((int) SQLiteStatus.Error);
        //    }
        //}

        //[Fact]
        //public void TestRegisterMultipleModelsWithSameName()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);

        //    var model = "TheModel";
        //    var aggregateModel = new AggregateModel();
        //    Database.Prediction.RegisterModel(model, aggregateModel);

        //    try {
        //        var input = AggregateModel.CreateInput("numbers");
        //        var prediction = Function.Prediction(model, input);
        //        using (var q = QueryBuilder.Select(SelectResult.Expression(prediction))
        //            .From(DataSource.Database(Db))) {
        //            var rows = VerifyQuery(q, (n, result) =>
        //            {
        //                var pred = result.GetDictionary(0);
        //                pred.GetInt("sum").Should().Be(15);
        //            });
        //            rows.Should().Be(1);

        //            var echoModel = new EchoModel();
        //            Database.Prediction.RegisterModel(model, echoModel);

        //            rows = VerifyQuery(q, (n, result) =>
        //            {
        //                var pred = result.GetDictionary(0);
        //                pred.GetValue("sum").Should().BeNull("because the model should have been replaced");
        //                pred.GetArray("numbers").Should().ContainInOrder(new[] { 1L, 2L, 3L, 4L, 5L },
        //                    "because the document should simply be echoed back");
        //            });
        //            rows.Should().Be(1);
        //        }

                
        //    } finally {
        //        Database.Prediction.UnregisterModel(model);
        //    }
        //}

        [Fact]
        public void TestPredictionInputOutput()
        {
            var echoModel = new EchoModel();
            echoModel.RegisterModel();

            using (var doc = new MutableDocument()) {
                doc.SetString("name", "Daniel");
                doc.SetInt("number", 2);
                doc.SetDouble("max", Double.MaxValue);
                Db.Save(doc);
            }

            var date = DateTimeOffset.Now;
            var power = Function.Power(Expression.Property("number"), Expression.Int(2));
            var map = new Dictionary<string, object>
            {
                ["null"] = null,
                ["number1"] = 10,
                ["number2"] = 10.1,
                ["int_min"] = Int32.MinValue,
                ["int_max"] = Int32.MaxValue,
                ["int64_min"] = Int64.MinValue,
                ["int64_max"] = Int64.MaxValue,
                ["float_min"] = Single.MinValue,
                ["float_max"] = Single.MaxValue, // NOTE: Double limits are not guaranteed
                ["boolean_true"] = true,
                ["boolean_false"] = false,
                ["string"] = "hello",
                ["date"] = date,
                ["expr_property"] = Expression.Property("name"),
                ["expr_value_number1"] = Expression.Value(20),
                ["expr_value_number2"] = Expression.Value(20.1),
                ["expr_value_boolean"] = Expression.Value(true),
                ["expr_value_string"] = Expression.Value("hi"),
                ["expr_value_date"] = Expression.Value(date),
                ["expr_value_null"] = Expression.Value(null),
                ["expr_power"] = power
            };

            var submap = new Dictionary<string, object> { ["foo"] = "bar" };
            map["dict"] = submap;
            var subList = new[] { "1", "2", "3" };
            map["array"] = subList;

            var subExprMap = new Dictionary<string, object> { ["ping"] = "pong" };
            map["expr_value_dict"] = Expression.Value(subExprMap);
            var subExprList = new[] { "4", "5", "6" };
            map["expr_value_array"] = Expression.Value(subExprList);

            var input = Expression.Value(map);
            var model = nameof(EchoModel);
            var prediction = Function.Prediction(model, input);
            using (var q = QueryBuilder.Select(SelectResult.Expression(prediction))
                .From(DataSource.Database(Db)))
            {
                var rows = VerifyQuery(q, (n, result) =>
                {
                    var pred = result.GetDictionary(0);
                    pred.Count.Should().Be(map.Count,
                        "because all properties should be serialized and recovered correctly");
                    pred.GetInt("number1").Should().Be(10);
                    pred.GetDouble("number2").Should().Be(10.1);
                    pred.GetInt("int_min").Should().Be(Int32.MinValue);
                    pred.GetInt("int_max").Should().Be(Int32.MaxValue);
                    pred.GetLong("int64_min").Should().Be(Int64.MinValue);
                    pred.GetLong("int64_max").Should().Be(Int64.MaxValue);
                    pred.GetFloat("float_min").Should().Be(Single.MinValue);
                    pred.GetFloat("float_max").Should().Be(Single.MaxValue);
                    pred.GetBoolean("boolean_true").Should().BeTrue();
                    pred.GetBoolean("boolean_false").Should().BeFalse();
                    pred.GetString("string").Should().Be("hello");
                    pred.GetDate("date").Should().Be(date);
                    pred.GetString("null").Should().BeNull();
                    pred.GetDictionary("dict").Should().Contain(submap);
                    pred.GetArray("array").Should().ContainInOrder(subList);

                    pred.GetString("expr_property").Should().Be("Daniel");
                    pred.GetInt("expr_value_number1").Should().Be(20);
                    pred.GetDouble("expr_value_number2").Should().Be(20.1);
                    pred.GetBoolean("expr_value_boolean").Should().BeTrue();
                    pred.GetString("expr_value_string").Should().Be("hi");
                    pred.GetDate("expr_value_date").Should().Be(date);
                    pred.GetString("expr_value_null").Should().BeNull();
                    pred.GetDictionary("expr_value_dict").Should().Contain(subExprMap);
                    pred.GetArray("expr_value_array").Should().ContainInOrder(subExprList);
                    pred.GetInt("expr_power").Should().Be(4);
                });

                rows.Should().Be(1);
            }
        }

        //[Fact]
        //public void TestQueryValueFromDictionaryResult()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();
        //    var input = Expression.Dictionary(new Dictionary<string, object>
        //    {
        //        ["numbers"] = Expression.Property("numbers")
        //    });
        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
        //            SelectResult.Expression(Function.Prediction(aggregateModel.Name, input).Property("sum")).As("sum"))
        //        .From(DataSource.Database(Db))) {
        //        var numRows = VerifyQuery(q, (i, result) =>
        //        {
        //            var numbers = result.GetArray(0)?.ToList();
        //            numbers.Should().NotBeEmpty("because otherwise the data didn't come through");
        //            var sum = result.GetLong(1);
        //            sum.Should().Be(result.GetLong("sum"));
        //            sum.Should().Be(numbers.Cast<long>().Sum());
        //        });

        //        numRows.Should().Be(2);
        //        aggregateModel.UnregisterModel();
        //    }
        //}

        //[Fact]
        //public void TestQueryPredictionValues()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
        //            SelectResult.Expression(prediction.Property("sum")).As("sum"),
        //            SelectResult.Expression(prediction.Property("min")).As("min"),
        //            SelectResult.Expression(prediction.Property("max")).As("max"),
        //            SelectResult.Expression(prediction.Property("avg")).As("avg"))
        //        .From(DataSource.Database(Db))) {
        //        var rows = VerifyQuery(q, (n, result) =>
        //        {
        //            var numbers = result.GetArray(0);
        //            var dict = new MutableDictionaryObject();
        //            dict.SetArray("numbers", numbers);
        //            var expected = aggregateModel.Predict(dict);

        //            var sum = result.GetInt(1);
        //            var min = result.GetInt(2);
        //            var max = result.GetInt(3);
        //            var avg = result.GetDouble(4);

        //            result.GetInt("sum").Should().Be(sum);
        //            result.GetInt("min").Should().Be(min);
        //            result.GetInt("max").Should().Be(max);
        //            result.GetDouble("avg").Should().Be(avg);

        //            sum.Should().Be(expected.GetInt("sum"));
        //            min.Should().Be(expected.GetInt("min"));
        //            max.Should().Be(expected.GetInt("max"));
        //            avg.Should().Be(expected.GetDouble("avg"));
        //        });

        //        rows.Should().Be(2);
        //    }
        //}

        [Fact]
        public void TestQueryWithBlobProperty()
        {
            var texts = new[]
            {
                "Knox on fox in socks in box.  Socks on Knox and Knox in box.",
                "Clocks on fix tick. Clocks on Knox tock. Six sick bricks tick.  Six sick chicks tock."
            };

            foreach (var text in texts) {
                using (var doc = new MutableDocument()) {
                    doc.SetBlob("text", new Blob("text/plain", Encoding.UTF8.GetBytes(text)));
                    SaveDocument(doc);
                }
            }

            var textModel = new TextModel();
            textModel.RegisterModel();

            var input = TextModel.CreateInput("text");
            var prediction = Function.Prediction(textModel.Name, input).Property("wc");
            using (var q = QueryBuilder
                .Select(SelectResult.Property("text"), SelectResult.Expression(prediction).As("wc"))
                .From(DataSource.Database(Db))
                .Where(prediction.GreaterThan(Expression.Int(15)))) {
                foreach (var row in q.Execute()) {
                    WriteLine(row.GetInt("wc").ToString());
                }
            }

            textModel.ContentType.Should().Be("text/plain");
            Database.Prediction.UnregisterModel(textModel.Name);
        }

        [Fact]
        public void TestQueryWithBlobParameter()
        {
            SaveDocument(new MutableDocument());

            var textModel = new TextModel();
            textModel.RegisterModel();
            
            var input = Expression.Dictionary(new Dictionary<string, object>
            {
                ["text"] = Expression.Parameter("text")
            });
            var prediction = Function.Prediction(textModel.Name, input).Property("wc");
            using (var q = QueryBuilder.Select(SelectResult.Expression(prediction).As("wc"))
                .From(DataSource.Database(Db))) {
                var parameters = new Parameters();
                parameters.SetBlob("text",
                    new Blob("text/plain",
                        Encoding.UTF8.GetBytes("Knox on fox in socks in box.  Socks on Knox and Knox in box.")));
                q.Parameters = parameters;
                var numRows = VerifyQuery(q, (n, r) =>
                    {
                        r.GetLong(0).Should().Be(14, "because that is the word count of the sentence in the parameter");
                    });
                numRows.Should().Be(1);
                textModel.ContentType.Should().Be("text/plain");
                textModel.UnregisterModel();
            }
        }

        [Fact]
        public void TestNonSupportedInput()
        {
            var echoModel = new EchoModel();
            echoModel.RegisterModel();

            var model = nameof(EchoModel);
            var input = Expression.Value("string");
            var prediction = Function.Prediction(model, input);

            using (var q = QueryBuilder.Select(SelectResult.Expression(prediction))
                .From(DataSource.Database(Db))) {
                q.Invoking(x => x.Execute()).Should().Throw<CouchbaseSQLiteException>()
                    .Where(x => x.BaseError == SQLiteStatus.Error);
            }

            var dict = new Dictionary<string, object> { ["key"] = this };
            input = Expression.Dictionary(dict);
            prediction = Function.Prediction(model, input);

            using (var q = QueryBuilder.Select(SelectResult.Expression(prediction))
                .From(DataSource.Database(Db))) {
                q.Invoking(x => x.Execute()).Should().Throw<ArgumentException>();
            }
        }

        //[Fact]
        //public void TestWhere()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
        //            SelectResult.Expression(prediction.Property("sum")).As("sum"),
        //            SelectResult.Expression(prediction.Property("min")).As("min"),
        //            SelectResult.Expression(prediction.Property("max")).As("max"),
        //            SelectResult.Expression(prediction.Property("avg")).As("avg"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
        //        var rows = VerifyQuery(q, (n, result) =>
        //        {
        //            var sum = result.GetInt(1);
        //            var min = result.GetInt(2);
        //            var max = result.GetInt(3);
        //            var avg = result.GetDouble(4);

        //            sum.Should().Be(15);
        //            min.Should().Be(1);
        //            max.Should().Be(5);
        //            avg.Should().Be(3.0);
        //        });
        //        rows.Should().Be(1);
        //    }
        //}

        //[Fact]
        //public void TestOrderBy()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    using (var q = QueryBuilder.Select(SelectResult.Expression(prediction.Property("sum")).As("sum"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").GreaterThan(Expression.Int(1)))
        //        .OrderBy(Ordering.Expression(prediction.Property("sum")).Descending())) {
        //        var rows = VerifyQuery(q, (n, result) =>
        //        {
        //            var sum = result.GetInt(0);
        //            sum.Should().Be(n == 1 ? 40 : 15);
        //        });
        //        rows.Should().Be(2);
        //    }
        //}

        //[Fact]
        //public void TestModelReturningNull()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);

        //    using (var doc = new MutableDocument()) {
        //        doc.SetString("text", "Knox on fox in socks in box.  Socks on Knox and Knox in box.");
        //        Db.Save(doc);
        //    }

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    using (var q = QueryBuilder.Select(SelectResult.Expression(prediction),
        //            SelectResult.Expression(prediction.Property("sum")))
        //        .From(DataSource.Database(Db))) {
        //        var rows = VerifyQuery(q, (n, result) =>
        //        {
        //            if (n == 1) {
        //                result.GetDictionary(0).Should().NotBeNull();
        //                result.GetInt(1).Should().Be(15);
        //            } else {
        //                result.GetDictionary(0).Should().BeNull();
        //                result.GetValue(1).Should().BeNull();
        //            }
        //        });
        //        rows.Should().Be(2);
        //    }

        //    using (var q = QueryBuilder.Select(SelectResult.Expression(prediction),
        //            SelectResult.Expression(prediction.Property("sum")))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.NotNullOrMissing())) {
        //        var explain = q.Explain();
        //        var rows = VerifyQuery(q, (n, result) =>
        //        {
        //            result.GetDictionary(0).Should().NotBeNull();
        //            result.GetInt(1).Should().Be(15);
        //        });
        //        rows.Should().Be(1);
        //    }
        //}

        //[Fact]
        //public void TestValueIndex()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();
        //    var input = AggregateModel.CreateInput("numbers");
        //    var sumPrediction = Function.Prediction(aggregateModel.Name, input).Property("sum");

        //    var index = IndexBuilder.ValueIndex(ValueIndexItem.Expression(sumPrediction));
        //    Db.CreateIndex("SumIndex", index);

        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
        //            SelectResult.Expression(sumPrediction))
        //        .From(DataSource.Database(Db))
        //        .Where(sumPrediction.EqualTo(Expression.Int(15)))) {
        //        q.Explain().IndexOf("USING INDEX SumIndex").Should()
        //            .NotBe(-1, "because the query should make use of the index");
        //        var numRows = VerifyQuery(q, (n, r) =>
        //        {
        //            var numbers = r.GetArray(0).Cast<long>().ToList();
        //            var sum = r.GetLong(1);
        //            sum.Should().Be(numbers.Sum());
        //        });

        //        numRows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(2,
        //            "because the value should be cached and not call the prediction function again");
        //    }
        //}

        //[Fact]
        //public void TestValueIndexMultipleValues()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();
        //    var input = AggregateModel.CreateInput("numbers");
        //    var sumPrediction = Function.Prediction(aggregateModel.Name, input).Property("sum");
        //    var avgPrediction = Function.Prediction(aggregateModel.Name, input).Property("avg");

        //    var sumIndex = IndexBuilder.ValueIndex(ValueIndexItem.Expression(sumPrediction));
        //    Db.CreateIndex("SumIndex", sumIndex);
        //    var avgIndex = IndexBuilder.ValueIndex(ValueIndexItem.Expression(avgPrediction));
        //    Db.CreateIndex("AvgIndex", avgIndex);

        //    using (var q = QueryBuilder.Select(SelectResult.Expression(sumPrediction).As("s"),
        //            SelectResult.Expression(avgPrediction).As("a"))
        //        .From(DataSource.Database(Db))
        //        .Where(sumPrediction.LessThanOrEqualTo(Expression.Int(15)).Or(avgPrediction.EqualTo(Expression.Int(8))))) {
        //        var explain = q.Explain();
        //        explain.IndexOf("USING INDEX SumIndex").Should().NotBe(-1, "because the sum index should be used");
        //        explain.IndexOf("USING INDEX AvgIndex").Should().NotBe(-1, "because the average index should be used");

        //        var numRows = VerifyQuery(q, (n, r) =>
        //        {
        //            r.Should().Match<Result>(x => x.GetLong(0) == 15 || x.GetLong(1) == 8);
        //        });
        //        numRows.Should().Be(2);
        //    }
        //}

        //[Fact]
        //public void TestValueIndexCompoundValues()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();
        //    var input = Expression.Dictionary(new Dictionary<string, object>
        //    {
        //        ["numbers"] = Expression.Property("numbers")
        //    });
        //    var sumPrediction = Function.Prediction(aggregateModel.Name, input).Property("sum");
        //    var avgPrediction = Function.Prediction(aggregateModel.Name, input).Property("avg");

        //    var index = IndexBuilder.ValueIndex(ValueIndexItem.Expression(sumPrediction),
        //        ValueIndexItem.Expression(avgPrediction));
        //    Db.CreateIndex("SumAvgIndex", index);

        //    aggregateModel.AllowCalls = false;

        //    using (var q = QueryBuilder.Select(SelectResult.Expression(sumPrediction).As("s"),
        //            SelectResult.Expression(avgPrediction).As("a"))
        //        .From(DataSource.Database(Db))
        //        .Where(sumPrediction.EqualTo(Expression.Int(15)).And(avgPrediction.EqualTo(Expression.Int(3))))) {
        //        var explain = q.Explain();
        //        explain.IndexOf("USING INDEX SumAvgIndex").Should().NotBe(-1, "because the sum index should be used");

        //        var numRows = VerifyQuery(q, (n, r) =>
        //        {
        //            r.GetLong(0).Should().Be(15);
        //            r.GetLong(1).Should().Be(3);
        //        });
        //        numRows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(4);
        //    }
        //}

        //[Fact]
        //public void TestPredictiveIndex()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    var index = IndexBuilder.PredictiveIndex(model, input, null);
        //    Db.CreateIndex("AggIndex", index);

        //    aggregateModel.AllowCalls = false;

        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
        //            SelectResult.Expression(prediction.Property("sum")).As("sum"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX AggIndex").Should()
        //            .BeFalse("because unlike other indexes, predictive result indexes don't create SQLite indexes");

        //        var rows = VerifyQuery(q, (n, result) => { result.GetInt(1).Should().Be(15); });
        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(2);
        //    }
        //}

        //[Fact]
        //public void TestPredictiveIndexOnValues()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    var index = IndexBuilder.PredictiveIndex(model, input, "sum");
        //    Db.CreateIndex("SumIndex", index);

        //    aggregateModel.AllowCalls = false;

        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
        //            SelectResult.Expression(prediction.Property("sum")).As("sum"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX SumIndex").Should().BeTrue();

        //        var rows = VerifyQuery(q, (n, result) => { result.GetInt(1).Should().Be(15); });
        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(2);
        //    }
        //}

        //[Fact]
        //public void TestPredictiveIndexMultipleValues()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    var sumIndex = IndexBuilder.PredictiveIndex(model, input, "sum");
        //    Db.CreateIndex("SumIndex", sumIndex);

        //    var avgIndex = IndexBuilder.PredictiveIndex(model, input, "avg");
        //    Db.CreateIndex("AvgIndex", avgIndex);

        //    aggregateModel.AllowCalls = false;

        //    using (var q = QueryBuilder.Select(SelectResult.Expression(prediction.Property("sum")).As("sum"),
        //            SelectResult.Expression(prediction.Property("avg")).As("avg"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").LessThanOrEqualTo(Expression.Int(15)).Or(
        //            prediction.Property("avg").EqualTo(Expression.Int(8))))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX SumIndex").Should().BeTrue();
        //        explain.Contains("USING INDEX AvgIndex").Should().BeTrue();

        //        var rows = VerifyQuery(q, (n, result) =>
        //            {
        //                result.Should().Match<Result>(x => x.GetInt(0) == 15 || x.GetInt(1) == 8);
        //            });
        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(2);
        //        aggregateModel.NumberOfCalls.Should().Be(2);
        //    }
        //}

        //[Fact]
        //public void TestPredictiveIndexCompoundValue()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    var sumIndex = IndexBuilder.PredictiveIndex(model, input, "sum", "avg");
        //    Db.CreateIndex("SumAvgIndex", sumIndex);

        //    aggregateModel.AllowCalls = false;

        //    using (var q = QueryBuilder.Select(SelectResult.Expression(prediction.Property("sum")).As("sum"),
        //            SelectResult.Expression(prediction.Property("avg")).As("avg"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").LessThanOrEqualTo(Expression.Int(15)).And(
        //            prediction.Property("avg").EqualTo(Expression.Int(3))))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX SumAvgIndex").Should().BeTrue();

        //        var rows = VerifyQuery(q, (n, result) =>
        //        {
        //            result.GetInt(0).Should().Be(15);
        //            result.GetInt(1).Should().Be(3);
        //        });

        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(2);
        //    }
        //}

        //[Fact]
        //public void TestDeletePredictiveIndex()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    var sumIndex = IndexBuilder.PredictiveIndex(model, input, "sum");
        //    Db.CreateIndex("SumIndex", sumIndex);

        //    aggregateModel.AllowCalls = false;

        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX SumIndex").Should().BeTrue();

        //        var rows = VerifyQuery(q, (n, result) => { result.GetArray(0)?.Count.Should().BeGreaterThan(0); });
        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(2);
        //    }

        //    Db.DeleteIndex("SumIndex");

        //    aggregateModel.Reset();
        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX SumIndex").Should().BeFalse();

        //        var rows = VerifyQuery(q, (n, result) => { result.GetArray(0)?.Count.Should().BeGreaterThan(0); });
        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(2);
        //    }
        //}

        //[Fact]
        //public void TestDeletePredictiveIndexesSharedCache()
        //{
        //    CreateDocument(1, 2, 3, 4, 5);
        //    CreateDocument(6, 7, 8, 9, 10);

        //    var aggregateModel = new AggregateModel();
        //    aggregateModel.RegisterModel();

        //    var model = nameof(AggregateModel);
        //    var input = AggregateModel.CreateInput("numbers");
        //    var prediction = Function.Prediction(model, input);

        //    var aggIndex = IndexBuilder.PredictiveIndex(model, input, null);
        //    Db.CreateIndex("AggIndex", aggIndex);

        //    var sumIndex = IndexBuilder.PredictiveIndex(model, input, "sum");
        //    Db.CreateIndex("SumIndex", sumIndex);

        //    var avgIndex = IndexBuilder.PredictiveIndex(model, input, "avg");
        //    Db.CreateIndex("AvgIndex", avgIndex);

        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").LessThanOrEqualTo(Expression.Int(15)).Or(
        //            prediction.Property("avg").EqualTo(Expression.Int(8))))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX SumIndex").Should().BeTrue();
        //        explain.Contains("USING INDEX AvgIndex").Should().BeTrue();

        //        var rows = VerifyQuery(q, (n, result) => { result.GetArray(0)?.Count.Should().BeGreaterThan(0); });
        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(2);
        //        aggregateModel.NumberOfCalls.Should().Be(2);
        //    }

        //    Db.DeleteIndex("SumIndex");

        //    // Note: With only one index, the SQLite optimizer does not utilize the index
        //    // when using an OR expression.  So test each query individually.

        //    aggregateModel.Reset();
        //    aggregateModel.AllowCalls = false;

        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX SumIndex").Should().BeFalse();

        //        var rows = VerifyQuery(q, (n, result) => { result.GetArray(0)?.Count.Should().BeGreaterThan(0); });
        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(0);
        //    }

        //    aggregateModel.Reset();
        //    aggregateModel.AllowCalls = false;
        //    using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
        //        .From(DataSource.Database(Db))
        //        .Where(prediction.Property("avg").EqualTo(Expression.Int(8)))) {
        //        var explain = q.Explain();
        //        explain.Contains("USING INDEX AvgIndex").Should().BeTrue();

        //        var rows = VerifyQuery(q, (n, result) => { result.GetArray(0)?.Count.Should().BeGreaterThan(0); });
        //        aggregateModel.Error.Should().BeNull();
        //        rows.Should().Be(1);
        //        aggregateModel.NumberOfCalls.Should().Be(0);
        //    }

        //    Db.DeleteIndex("AvgIndex");

        //    for (int i = 0; i < 2; i++) {
        //        aggregateModel.Reset();
        //        aggregateModel.AllowCalls = i == 1;

        //        using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
        //            .From(DataSource.Database(Db))
        //            .Where(prediction.Property("avg").EqualTo(Expression.Int(8)))) {
        //            var explain = q.Explain();
        //            explain.Contains("USING INDEX SumIndex").Should().BeFalse();
        //            explain.Contains("USING INDEX AvgIndex").Should().BeFalse();

        //            var rows = VerifyQuery(q, (n, result) => { result.GetArray(0)?.Count.Should().BeGreaterThan(0); });
        //            aggregateModel.Error.Should().BeNull();
        //            rows.Should().Be(1);
        //            if (i == 0) {
        //                aggregateModel.NumberOfCalls.Should().Be(0);
        //            } else {
        //                aggregateModel.NumberOfCalls.Should().BeGreaterThan(0);
        //            }
        //        }

        //        Db.DeleteIndex("AggIndex");
        //    }
        //}

        [Fact]
        public void TestEuclidientDistance()
        {
            var tests = "[[[10, 10], [13, 14], 5]," +
                        " [[1, 2, 3], [1, 2, 3], 0]," +
                        " [[], [], 0]," +
                        " [[1, 2], [1, 2, 3], null]," +
                        " [[1, 2], \"foo\", null]]";
            TestDistanceFunction(Function.EuclideanDistance(Expression.Property("v1"), Expression.Property("v2")), tests);
        }


        [Fact]
        public void TestSquaredEuclidientDistance()
        {
            var tests = "[[[10, 10], [13, 14], 25]," +
                        " [[1, 2, 3], [1, 2, 3], 0]," +
                        " [[], [], 0]," +
                        " [[1, 2], [1, 2, 3], null]," +
                        " [[1, 2], \"foo\", null]]";
            TestDistanceFunction(Function.SquaredEuclideanDistance(Expression.Property("v1"), Expression.Property("v2")), tests);
        }

        [Fact]
        public void TestCosineDistance()
        {
            var tests = "[[[10, 0], [0, 99], 1]," +
                        " [[1, 2, 3], [1, 2, 3], 0]," +
                        " [[], [], null]," +
                        " [[1, 2], [1, 2, 3], null]," +
                        " [[1, 2], \"foo\", null]]";
            TestDistanceFunction(Function.CosineDistance(Expression.Property("v1"), Expression.Property("v2")), tests);
        }

        private void TestDistanceFunction(IExpression distance, string testData)
        {
            var tests = JsonConvert.DeserializeObject<IList<IList<object>>>(testData);

            foreach (var t in tests) {
                using (var doc = new MutableDocument()) {
                    doc.SetValue("v1", t[0]);
                    doc.SetValue("v2", t[1]);
                    doc.SetValue("distance", t[2]);
                    SaveDocument(doc);
                }
            }
            
            using (var q = QueryBuilder.Select(SelectResult.Expression(distance), SelectResult.Property("distance"))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetValue(0).Should().Be(r.GetValue(1));
                });

                numRows.Should().Be(tests.Count);
            }
        }

        private void CreateDocument(params int[] numbers)
        {
            using (var doc = new MutableDocument()) {
                doc.SetValue("numbers", numbers);
                SaveDocument(doc);
            }
        }
    }

    internal abstract class TestPredictiveModel : IPredictiveModel
    {
        #region Properties

        public bool AllowCalls { get; set; } = true;

        public Exception Error { get; private set; }

        public string Name => GetType().Name;

        public int NumberOfCalls { get; private set; }

        #endregion

        #region Public Methods

        public void RegisterModel()
        {
            Database.Prediction.RegisterModel(Name, this);
        }

        public void Reset()
        {
            NumberOfCalls = 0;
            AllowCalls = true;
        }

        public void UnregisterModel()
        {
            Database.Prediction.UnregisterModel(Name);
        }

        #endregion

        #region Protected Methods

        protected abstract DictionaryObject DoPrediction(DictionaryObject input);

        #endregion

        #region IPredictiveModel

        public DictionaryObject Predict(DictionaryObject input)
        {
            if (!AllowCalls) {
                Error = new InvalidOperationException("Not allowed to be called in this state");
                return null;
            }

            NumberOfCalls++;
            return DoPrediction(input);
        }

        #endregion
    }

    internal sealed class EchoModel : TestPredictiveModel
    {
        #region Overrides

        protected override DictionaryObject DoPrediction(DictionaryObject input)
        {
            return input;
        }

        #endregion
    }

    internal sealed class TextModel : TestPredictiveModel
    {
        #region Variables

        public string ContentType = "";

        #endregion

        #region Public Methods

        public static IExpression CreateInput(string propertyName)
        {
            return CreateInput(Expression.Property(propertyName));
        }

        public static IExpression CreateInput(IExpression expression)
        {
            return Expression.Dictionary(new Dictionary<string, object>
            {
                ["text"] = expression
            }); 
        }

        #endregion

        #region Overrides

        protected override DictionaryObject DoPrediction(DictionaryObject input)
        {
            var blob = input.GetBlob("text");
            if (blob == null) {
                return null;
            }

            ContentType = blob.ContentType;

            var text = Encoding.UTF8.GetString(blob.Content);
            var wc = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var sc = text.Split('.', StringSplitOptions.RemoveEmptyEntries).Length;

            var output = new MutableDictionaryObject();
            output.SetInt("wc", wc);
            output.SetInt("sc", sc);
            return output;
        }

        #endregion
    }

    internal sealed class AggregateModel : TestPredictiveModel
    {
        #region Public Methods

        [NotNull]
        public static IExpression CreateInput(string propertyName)
        {
            return Expression.Dictionary(new Dictionary<string, object>
            {
                ["numbers"] = Expression.Property(propertyName)
            });
        }

        #endregion

        #region Overrides

        protected override DictionaryObject DoPrediction(DictionaryObject input)
        {
            var numbers = input.GetArray("numbers");
            if (numbers == null) {
                return null;
            }

            var output = new MutableDictionaryObject();
            output.SetLong("sum", numbers.Cast<long>().Sum());
            output.SetLong("min", numbers.Cast<long>().Min());
            output.SetLong("max", numbers.Cast<long>().Max());
            output.SetDouble("avg", numbers.Cast<long>().Average());
            return output;
        }

        #endregion
    }
}
#endif
