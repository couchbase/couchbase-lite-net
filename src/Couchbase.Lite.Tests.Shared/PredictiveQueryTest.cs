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
using System.Text.Json;

using Couchbase.Lite;
using Couchbase.Lite.Enterprise.Query;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Query;

using Shouldly;

using LiteCore.Interop;

using Xunit;
using Xunit.Abstractions;
// ReSharper disable AccessToDisposedClosure
// ReSharper disable StringIndexOfIsCultureSpecific.1

namespace Test;

public sealed class PredictiveQueryTest : TestCase
{
    public PredictiveQueryTest(ITestOutputHelper output) : base(output)
    {
        Database.Prediction.UnregisterModel(nameof(AggregateModel));
        Database.Prediction.UnregisterModel(nameof(TextModel));
        Database.Prediction.UnregisterModel(nameof(EchoModel));

    }

    [Fact]
    public void TestRegisterAndUnregisterModel()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        var input = Expression.Dictionary(new Dictionary<string, object>
        {
            ["numbers"] = Expression.Property("numbers")
        });
        using var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                SelectResult.Expression(Function.Prediction(aggregateModel.Name, input)))
            .From(DataSource.Collection(DefaultCollection));
        Action badAction = () => q.Execute();
        var ex = Should.Throw<CouchbaseSQLiteException>(badAction);
        ex.Error.ShouldBe((int)SQLiteStatus.Error);

        aggregateModel.RegisterModel();
        var numRows = VerifyQuery(q, (_, result) =>
        {
            var numbers = result.GetArray(0)?.ToList();
            numbers.ShouldNotBeEmpty("because otherwise the data didn't come through");
            var predicate = result.GetDictionary(1);
            predicate.ShouldNotBeNull();
            predicate.GetLong("sum").ShouldBe(numbers.Cast<long>().Sum());
            predicate.GetLong("min").ShouldBe(numbers.Cast<long>().Min());
            predicate.GetLong("max").ShouldBe(numbers.Cast<long>().Max());
            predicate.GetDouble("avg").ShouldBe(numbers.Cast<long>().Average());
        });

        numRows.ShouldBe(2);
        aggregateModel.UnregisterModel();
        ex = Should.Throw<CouchbaseSQLiteException>(badAction);
        ex.Error.ShouldBe((int)SQLiteStatus.Error);
    }

    [Fact]
    public void TestRegisterMultipleModelsWithSameName()
    {
        CreateDocument(1, 2, 3, 4, 5);

        var model = "TheModel";
        var aggregateModel = new AggregateModel();
        Database.Prediction.RegisterModel(model, aggregateModel);

        try {
            var input = AggregateModel.CreateInput("numbers");
            var prediction = Function.Prediction(model, input);
            using var q = QueryBuilder.Select(SelectResult.Expression(prediction))
                .From(DataSource.Collection(DefaultCollection));
            var rows = VerifyQuery(q, (_, result) =>
            {
                var predicate = result.GetDictionary(0);
                predicate.ShouldNotBeNull("because otherwise the results didn't come through");
                predicate.GetInt("sum").ShouldBe(15);
            });
            rows.ShouldBe(1);

            var echoModel = new EchoModel();
            Database.Prediction.RegisterModel(model, echoModel);

            rows = VerifyQuery(q, (_, result) =>
            {
                var predicate = result.GetDictionary(0);
                predicate.ShouldNotBeNull("because otherwise the results didn't come through");
                predicate.GetValue("sum").ShouldBeNull("because the model should have been replaced");
                predicate.GetArray("numbers")?.SequenceEqual(new List<object> { 1L, 2L, 3L, 4L, 5L })
                    .ShouldBeTrue("because the document should simply be echoed back");
            });
            rows.ShouldBe(1);
        } finally {
            Database.Prediction.UnregisterModel(model);
        }
    }

    [Fact]
    public void TestPredictionInputOutput()
    {
        var echoModel = new EchoModel();
        echoModel.RegisterModel();

        using (var doc = new MutableDocument()) {
            doc.SetString("name", "Daniel");
            doc.SetInt("number", 2);
            doc.SetDouble("max", Double.MaxValue);
            DefaultCollection.Save(doc);
        }

        var date = DateTimeOffset.Now;
        var power = Function.Power(Expression.Property("number"), Expression.Int(2));
        var map = new Dictionary<string, object?>
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

        var submap = new Dictionary<string, object?> { ["foo"] = "bar" };
        map["dict"] = submap;
        var subList = new[] { "1", "2", "3" };
        map["array"] = subList;

        var subExprMap = new Dictionary<string, object?> { ["ping"] = "pong" };
        map["expr_value_dict"] = Expression.Value(subExprMap);
        var subExprList = new[] { "4", "5", "6" };
        map["expr_value_array"] = Expression.Value(subExprList);

        var input = Expression.Value(map);
        const string model = nameof(EchoModel);
        var prediction = Function.Prediction(model, input);
        using (var q = QueryBuilder.Select(SelectResult.Expression(prediction))
                   .From(DataSource.Collection(DefaultCollection)))
        {
            var rows = VerifyQuery(q, (_, result) =>
            {
                var predicate = result.GetDictionary(0);
                predicate.ShouldNotBeNull("because otherwise the results didn't come through");
                predicate.Count.ShouldBe(map.Count,
                    "because all properties should be serialized and recovered correctly");
                predicate.GetInt("number1").ShouldBe(10);
                predicate.GetDouble("number2").ShouldBe(10.1);
                predicate.GetInt("int_min").ShouldBe(Int32.MinValue);
                predicate.GetInt("int_max").ShouldBe(Int32.MaxValue);
                predicate.GetLong("int64_min").ShouldBe(Int64.MinValue);
                predicate.GetLong("int64_max").ShouldBe(Int64.MaxValue);
                predicate.GetFloat("float_min").ShouldBe(Single.MinValue);
                predicate.GetFloat("float_max").ShouldBe(Single.MaxValue);
                predicate.GetBoolean("boolean_true").ShouldBeTrue();
                predicate.GetBoolean("boolean_false").ShouldBeFalse();
                predicate.GetString("string").ShouldBe("hello");
                predicate.GetDate("date").ShouldBe(date);
                predicate.GetString("null").ShouldBeNull();
                predicate.GetDictionary("dict").ShouldBeEquivalentToFluent(submap);
                predicate.GetArray("array").ShouldBeEquivalentToFluent(subList);

                predicate.GetString("expr_property").ShouldBe("Daniel");
                predicate.GetInt("expr_value_number1").ShouldBe(20);
                predicate.GetDouble("expr_value_number2").ShouldBe(20.1);
                predicate.GetBoolean("expr_value_boolean").ShouldBeTrue();
                predicate.GetString("expr_value_string").ShouldBe("hi");
                predicate.GetDate("expr_value_date").ShouldBe(date);
                predicate.GetString("expr_value_null").ShouldBeNull();
                predicate.GetDictionary("expr_value_dict").ShouldBeEquivalentToFluent(subExprMap);
                predicate.GetArray("expr_value_array").ShouldBeEquivalentToFluent(subExprList);
                predicate.GetInt("expr_power").ShouldBe(4);
            });

            rows.ShouldBe(1);
        }
    }

    [Fact]
    public void TestQueryValueFromDictionaryResult()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();
        var input = Expression.Dictionary(new Dictionary<string, object>
        {
            ["numbers"] = Expression.Property("numbers")
        });
        using var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                SelectResult.Expression(Function.Prediction(aggregateModel.Name, input).Property("sum")).As("sum"))
            .From(DataSource.Collection(DefaultCollection));
        var numRows = VerifyQuery(q, (_, result) =>
        {
            var numbers = result.GetArray(0)?.ToList();
            numbers.ShouldNotBeEmpty("because otherwise the data didn't come through");
            var sum = result.GetLong(1);
            sum.ShouldBe(result.GetLong("sum"));
            sum.ShouldBe(numbers.Cast<long>().Sum());
        });

        numRows.ShouldBe(2);
        aggregateModel.UnregisterModel();
    }

    [Fact]
    public void TestQueryPredictionValues()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        const string model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        using var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                SelectResult.Expression(prediction.Property("sum")).As("sum"),
                SelectResult.Expression(prediction.Property("min")).As("min"),
                SelectResult.Expression(prediction.Property("max")).As("max"),
                SelectResult.Expression(prediction.Property("avg")).As("avg"))
            .From(DataSource.Collection(DefaultCollection));
        var rows = VerifyQuery(q, (_, result) =>
        {
            var numbers = result.GetArray(0);
            var dict = new MutableDictionaryObject();
            dict.SetArray("numbers", numbers);
            var expected = aggregateModel.Predict(dict);
            expected.ShouldNotBeNull("because otherwise the prediction failed");

            var sum = result.GetInt(1);
            var min = result.GetInt(2);
            var max = result.GetInt(3);
            var avg = result.GetDouble(4);

            result.GetInt("sum").ShouldBe(sum);
            result.GetInt("min").ShouldBe(min);
            result.GetInt("max").ShouldBe(max);
            result.GetDouble("avg").ShouldBe(avg);

            sum.ShouldBe(expected.GetInt("sum"));
            min.ShouldBe(expected.GetInt("min"));
            max.ShouldBe(expected.GetInt("max"));
            avg.ShouldBe(expected.GetDouble("avg"));
        });

        rows.ShouldBe(2);
    }

    [Fact]
    public void TestQueryWithBlobProperty()
    {
        var texts = new[]
        {
            "Knox on fox in socks in box.  Socks on Knox and Knox in box.",
            "Clocks on fix tick. Clocks on Knox tock. Six sick bricks tick.  Six sick chicks tock."
        };

        foreach (var text in texts) {
            using var doc = new MutableDocument();
            doc.SetBlob("text", new Blob("text/plain", Encoding.UTF8.GetBytes(text)));
            SaveDocument(doc);
        }

        var textModel = new TextModel();
        textModel.RegisterModel();

        var input = TextModel.CreateInput("text");
        var prediction = Function.Prediction(textModel.Name, input).Property("wc");
        using (var q = QueryBuilder
                   .Select(SelectResult.Property("text"), SelectResult.Expression(prediction).As("wc"))
                   .From(DataSource.Collection(DefaultCollection))
                   .Where(prediction.GreaterThan(Expression.Int(15)))) {
            foreach (var row in q.Execute()) {
                WriteLine(row.GetInt("wc").ToString());
            }
        }

        textModel.ContentType.ShouldBe("text/plain");
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
        using var q = QueryBuilder.Select(SelectResult.Expression(prediction).As("wc"))
            .From(DataSource.Collection(DefaultCollection));
        var parameters = new Parameters();
        parameters.SetBlob("text",
            new("text/plain",
                "Knox on fox in socks in box.  Socks on Knox and Knox in box."u8.ToArray()));
        q.Parameters = parameters;
        var numRows = VerifyQuery(q, (_, r) =>
        {
            r.GetLong(0).ShouldBe(14, "because that is the word count of the sentence in the parameter");
        });
        numRows.ShouldBe(1);
        textModel.ContentType.ShouldBe("text/plain");
        textModel.UnregisterModel();
    }

    [Fact]
    public void TestNonSupportedInput()
    {
        var echoModel = new EchoModel();
        echoModel.RegisterModel();

        var model = nameof(EchoModel);
        var input = Expression.Value("string");
        var prediction = Function.Prediction(model, input);

        //Due to possibility of optimization on new SQLite, SQLite skips these expensive query calls if there is no document before running a query.
        using (var doc = new MutableDocument()) {
            doc.SetInt("number", 1);
            SaveDocument(doc);
        }

        using (var q = QueryBuilder.Select(SelectResult.Expression(prediction))
                   .From(DataSource.Collection(DefaultCollection))) {
            var ex = Should.Throw<CouchbaseSQLiteException>(() => q.Execute());
            ex.BaseError.ShouldBe(SQLiteStatus.Error);
        }

        var dict = new Dictionary<string, object> { ["key"] = this };
        input = Expression.Dictionary(dict);
        prediction = Function.Prediction(model, input);

        using (var q = QueryBuilder.Select(SelectResult.Expression(prediction))
                   .From(DataSource.Collection(DefaultCollection))) {
            Should.Throw<ArgumentException>(() => q.Execute());
        }
    }

    [Fact]
    public void TestWhere()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        const string model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        using var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                SelectResult.Expression(prediction.Property("sum")).As("sum"),
                SelectResult.Expression(prediction.Property("min")).As("min"),
                SelectResult.Expression(prediction.Property("max")).As("max"),
                SelectResult.Expression(prediction.Property("avg")).As("avg"))
            .From(DataSource.Collection(DefaultCollection))
            .Where(prediction.Property("sum").EqualTo(Expression.Int(15)));
        var rows = VerifyQuery(q, (_, result) =>
        {
            var sum = result.GetInt(1);
            var min = result.GetInt(2);
            var max = result.GetInt(3);
            var avg = result.GetDouble(4);

            sum.ShouldBe(15);
            min.ShouldBe(1);
            max.ShouldBe(5);
            avg.ShouldBe(3.0);
        });
        rows.ShouldBe(1);
    }

    [Fact]
    public void TestOrderBy()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        const string model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        using var q = QueryBuilder.Select(SelectResult.Expression(prediction.Property("sum")).As("sum"))
            .From(DataSource.Collection(DefaultCollection))
            .Where(prediction.Property("sum").GreaterThan(Expression.Int(1)))
            .OrderBy(Ordering.Expression(prediction.Property("sum")).Descending());
        var rows = VerifyQuery(q, (n, result) =>
        {
            var sum = result.GetInt(0);
            sum.ShouldBe(n == 1 ? 40 : 15);
        });
        rows.ShouldBe(2);
    }

    [Fact]
    public void TestModelReturningNull()
    {
        CreateDocument(1, 2, 3, 4, 5);

        using (var doc = new MutableDocument()) {
            doc.SetString("text", "Knox on fox in socks in box.  Socks on Knox and Knox in box.");
            DefaultCollection.Save(doc);
        }

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        var model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        using (var q = QueryBuilder.Select(SelectResult.Expression(prediction),
                       SelectResult.Expression(prediction.Property("sum")))
                   .From(DataSource.Collection(DefaultCollection))) {
            var rows = VerifyQuery(q, (n, result) =>
            {
                if (n == 1) {
                    result.GetDictionary(0).ShouldNotBeNull();
                    result.GetInt(1).ShouldBe(15);
                } else {
                    result.GetDictionary(0).ShouldBeNull();
                    result.GetValue(1).ShouldBeNull();
                }
            });
            rows.ShouldBe(2);
        }

        using (var q = QueryBuilder.Select(SelectResult.Expression(prediction),
                       SelectResult.Expression(prediction.Property("sum")))
                   .From(DataSource.Collection(DefaultCollection))
                   .Where(prediction.IsValued())) {
            var rows = VerifyQuery(q, (_, result) =>
            {
                result.GetDictionary(0).ShouldNotBeNull();
                result.GetInt(1).ShouldBe(15);
            });
            rows.ShouldBe(1);
        }
    }

    [Fact]
    public void TestValueIndex()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();
        var input = AggregateModel.CreateInput("numbers");
        var sumPrediction = Function.Prediction(aggregateModel.Name, input).Property("sum");

        var index = IndexBuilder.ValueIndex(ValueIndexItem.Expression(sumPrediction));
        DefaultCollection.CreateIndex("SumIndex", index);

        using var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                SelectResult.Expression(sumPrediction))
            .From(DataSource.Collection(DefaultCollection))
            .Where(sumPrediction.EqualTo(Expression.Int(15)));
        q.Explain().IndexOf("USING INDEX SumIndex")
            .ShouldNotBe(-1, "because the query should make use of the index");
        var numRows = VerifyQuery(q, (_, r) =>
        {
            var numbers = r.GetArray(0)!.Cast<long>().ToList();
            var sum = r.GetLong(1);
            sum.ShouldBe(numbers.Sum());
        });

        numRows.ShouldBe(1);
        aggregateModel.NumberOfCalls.ShouldBe(2,
            "because the value should be cached and not call the prediction function again");
    }

    [Fact]
    public void TestValueIndexMultipleValues()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();
        var input = AggregateModel.CreateInput("numbers");
        var sumPrediction = Function.Prediction(aggregateModel.Name, input).Property("sum");
        var avgPrediction = Function.Prediction(aggregateModel.Name, input).Property("avg");

        var sumIndex = IndexBuilder.ValueIndex(ValueIndexItem.Expression(sumPrediction));
        DefaultCollection.CreateIndex("SumIndex", sumIndex);
        var avgIndex = IndexBuilder.ValueIndex(ValueIndexItem.Expression(avgPrediction));
        DefaultCollection.CreateIndex("AvgIndex", avgIndex);

        using var q = QueryBuilder.Select(SelectResult.Expression(sumPrediction).As("s"),
                SelectResult.Expression(avgPrediction).As("a"))
            .From(DataSource.Collection(DefaultCollection))
            .Where(sumPrediction.LessThanOrEqualTo(Expression.Int(15)).Or(avgPrediction.EqualTo(Expression.Int(8))));
        var explain = q.Explain();
        explain.IndexOf("USING INDEX SumIndex").ShouldNotBe(-1, "because the sum index should be used");
        explain.IndexOf("USING INDEX AvgIndex").ShouldNotBe(-1, "because the average index should be used");

        var numRows = VerifyQuery(q, (_, r) =>
        {
            (r.GetLong(0) == 15 || r.GetLong(1) == 8).ShouldBeTrue();
        });
        numRows.ShouldBe(2);
    }

    [Fact]
    public void TestValueIndexCompoundValues()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();
        var input = Expression.Dictionary(new Dictionary<string, object>
        {
            ["numbers"] = Expression.Property("numbers")
        });
        var sumPrediction = Function.Prediction(aggregateModel.Name, input).Property("sum");
        var avgPrediction = Function.Prediction(aggregateModel.Name, input).Property("avg");

        var index = IndexBuilder.ValueIndex(ValueIndexItem.Expression(sumPrediction),
            ValueIndexItem.Expression(avgPrediction));
        DefaultCollection.CreateIndex("SumAvgIndex", index);

        aggregateModel.AllowCalls = false;

        using var q = QueryBuilder.Select(SelectResult.Expression(sumPrediction).As("s"),
                SelectResult.Expression(avgPrediction).As("a"))
            .From(DataSource.Collection(DefaultCollection))
            .Where(sumPrediction.EqualTo(Expression.Int(15)).And(avgPrediction.EqualTo(Expression.Int(3))));
        var explain = q.Explain();
        explain.IndexOf("USING INDEX SumAvgIndex").ShouldNotBe(-1, "because the sum index should be used");

        var numRows = VerifyQuery(q, (_, r) =>
        {
            r.GetLong(0).ShouldBe(15);
            r.GetLong(1).ShouldBe(3);
        });
        numRows.ShouldBe(1);
        aggregateModel.NumberOfCalls.ShouldBe(4);
    }

    [Fact]
    public void TestPredictiveIndex()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        var model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        var index = IndexBuilder.PredictiveIndex(model, input);
        DefaultCollection.CreateIndex("AggIndex", index);

        aggregateModel.AllowCalls = false;

        using var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                SelectResult.Expression(prediction.Property("sum")).As("sum"))
            .From(DataSource.Collection(DefaultCollection))
            .Where(prediction.Property("sum").EqualTo(Expression.Int(15)));
        var explain = q.Explain();
        explain.Contains("USING INDEX AggIndex")
            .ShouldBeFalse("because unlike other indexes, predictive result indexes don't create SQLite indexes");

        var rows = VerifyQuery(q, (_, result) => { result.GetInt(1).ShouldBe(15); });
        aggregateModel.Error.ShouldBeNull();
        rows.ShouldBe(1);
        aggregateModel.NumberOfCalls.ShouldBe(2);
    }

    [Fact]
    public void TestPredictiveIndexOnValues()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        var model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        var index = IndexBuilder.PredictiveIndex(model, input, "sum");
        DefaultCollection.CreateIndex("SumIndex", index);

        aggregateModel.AllowCalls = false;

        using var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                SelectResult.Expression(prediction.Property("sum")).As("sum"))
            .From(DataSource.Collection(DefaultCollection))
            .Where(prediction.Property("sum").EqualTo(Expression.Int(15)));
        var explain = q.Explain();
        explain.Contains("USING INDEX SumIndex").ShouldBeTrue();

        var rows = VerifyQuery(q, (_, result) => { result.GetInt(1).ShouldBe(15); });
        aggregateModel.Error.ShouldBeNull();
        rows.ShouldBe(1);
        aggregateModel.NumberOfCalls.ShouldBe(2);
    }

    [Fact]
    public void TestPredictiveIndexMultipleValues()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        var model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        var sumIndex = IndexBuilder.PredictiveIndex(model, input, "sum");
        DefaultCollection.CreateIndex("SumIndex", sumIndex);

        var avgIndex = IndexBuilder.PredictiveIndex(model, input, "avg");
        DefaultCollection.CreateIndex("AvgIndex", avgIndex);

        aggregateModel.AllowCalls = false;

        using var q = QueryBuilder.Select(SelectResult.Expression(prediction.Property("sum")).As("sum"),
                SelectResult.Expression(prediction.Property("avg")).As("avg"))
            .From(DataSource.Collection(DefaultCollection))
            .Where(prediction.Property("sum").LessThanOrEqualTo(Expression.Int(15)).Or(
                prediction.Property("avg").EqualTo(Expression.Int(8))));
        var explain = q.Explain();
        explain.Contains("USING INDEX SumIndex").ShouldBeTrue();
        explain.Contains("USING INDEX AvgIndex").ShouldBeTrue();

        var rows = VerifyQuery(q, (_, result) =>
        {
            (result.GetLong(0) == 15 || result.GetLong(1) == 8).ShouldBeTrue();
        });
        aggregateModel.Error.ShouldBeNull();
        rows.ShouldBe(2);
        aggregateModel.NumberOfCalls.ShouldBe(2);
    }

    [Fact]
    public void TestPredictiveIndexCompoundValue()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        var model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        var sumIndex = IndexBuilder.PredictiveIndex(model, input, "sum", "avg");
        DefaultCollection.CreateIndex("SumAvgIndex", sumIndex);

        aggregateModel.AllowCalls = false;

        using var q = QueryBuilder.Select(SelectResult.Expression(prediction.Property("sum")).As("sum"),
                SelectResult.Expression(prediction.Property("avg")).As("avg"))
            .From(DataSource.Collection(DefaultCollection))
            .Where(prediction.Property("sum").LessThanOrEqualTo(Expression.Int(15)).And(
                prediction.Property("avg").EqualTo(Expression.Int(3))));
        var explain = q.Explain();
        explain.Contains("USING INDEX SumAvgIndex").ShouldBeTrue();

        var rows = VerifyQuery(q, (_, result) =>
        {
            result.GetInt(0).ShouldBe(15);
            result.GetInt(1).ShouldBe(3);
        });

        aggregateModel.Error.ShouldBeNull();
        rows.ShouldBe(1);
        aggregateModel.NumberOfCalls.ShouldBe(2);
    }

    [Fact]
    public void TestDeletePredictiveIndex()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        var model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        var sumIndex = IndexBuilder.PredictiveIndex(model, input, "sum");
        DefaultCollection.CreateIndex("SumIndex", sumIndex);

        aggregateModel.AllowCalls = false;

        using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
                   .From(DataSource.Collection(DefaultCollection))
                   .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
            var explain = q.Explain();
            explain.Contains("USING INDEX SumIndex").ShouldBeTrue();

            var rows = VerifyQuery(q, (_, result) => { result.GetArray(0)?.Count.ShouldBeGreaterThan(0); });
            aggregateModel.Error.ShouldBeNull();
            rows.ShouldBe(1);
            aggregateModel.NumberOfCalls.ShouldBe(2);
        }

        DefaultCollection.DeleteIndex("SumIndex");

        aggregateModel.Reset();
        using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
                   .From(DataSource.Collection(DefaultCollection))
                   .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
            var explain = q.Explain();
            explain.Contains("USING INDEX SumIndex").ShouldBeFalse();

            var rows = VerifyQuery(q, (_, result) => { result.GetArray(0)?.Count.ShouldBeGreaterThan(0); });
            aggregateModel.Error.ShouldBeNull();
            rows.ShouldBe(1);
            aggregateModel.NumberOfCalls.ShouldBe(2);
        }
    }

    [Fact]
    public void TestDeletePredictiveIndexesSharedCache()
    {
        CreateDocument(1, 2, 3, 4, 5);
        CreateDocument(6, 7, 8, 9, 10);

        var aggregateModel = new AggregateModel();
        aggregateModel.RegisterModel();

        var model = nameof(AggregateModel);
        var input = AggregateModel.CreateInput("numbers");
        var prediction = Function.Prediction(model, input);

        var aggIndex = IndexBuilder.PredictiveIndex(model, input);
        DefaultCollection.CreateIndex("AggIndex", aggIndex);

        var sumIndex = IndexBuilder.PredictiveIndex(model, input, "sum");
        DefaultCollection.CreateIndex("SumIndex", sumIndex);

        var avgIndex = IndexBuilder.PredictiveIndex(model, input, "avg");
        DefaultCollection.CreateIndex("AvgIndex", avgIndex);

        using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
                   .From(DataSource.Collection(DefaultCollection))
                   .Where(prediction.Property("sum").LessThanOrEqualTo(Expression.Int(15)).Or(
                       prediction.Property("avg").EqualTo(Expression.Int(8))))) {
            var explain = q.Explain();
            explain.Contains("USING INDEX SumIndex").ShouldBeTrue();
            explain.Contains("USING INDEX AvgIndex").ShouldBeTrue();

            var rows = VerifyQuery(q, (_, result) => { result.GetArray(0)?.Count.ShouldBeGreaterThan(0); });
            aggregateModel.Error.ShouldBeNull();
            rows.ShouldBe(2);
            aggregateModel.NumberOfCalls.ShouldBe(2);
        }

        DefaultCollection.DeleteIndex("SumIndex");

        // Note: With only one index, the SQLite optimizer does not utilize the index
        // when using an OR expression.  So test each query individually.

        aggregateModel.Reset();
        aggregateModel.AllowCalls = false;

        using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
                   .From(DataSource.Collection(DefaultCollection))
                   .Where(prediction.Property("sum").EqualTo(Expression.Int(15)))) {
            var explain = q.Explain();
            explain.Contains("USING INDEX SumIndex").ShouldBeFalse();

            var rows = VerifyQuery(q, (_, result) => { result.GetArray(0)?.Count.ShouldBeGreaterThan(0); });
            aggregateModel.Error.ShouldBeNull();
            rows.ShouldBe(1);
            aggregateModel.NumberOfCalls.ShouldBe(0);
        }

        aggregateModel.Reset();
        aggregateModel.AllowCalls = false;
        using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
                   .From(DataSource.Collection(DefaultCollection))
                   .Where(prediction.Property("avg").EqualTo(Expression.Int(8)))) {
            var explain = q.Explain();
            explain.Contains("USING INDEX AvgIndex").ShouldBeTrue();

            var rows = VerifyQuery(q, (_, result) => { result.GetArray(0)?.Count.ShouldBeGreaterThan(0); });
            aggregateModel.Error.ShouldBeNull();
            rows.ShouldBe(1);
            aggregateModel.NumberOfCalls.ShouldBe(0);
        }

        DefaultCollection.DeleteIndex("AvgIndex");

        for (int i = 0; i < 2; i++) {
            aggregateModel.Reset();
            aggregateModel.AllowCalls = i == 1;

            using (var q = QueryBuilder.Select(SelectResult.Property("numbers"))
                       .From(DataSource.Collection(DefaultCollection))
                       .Where(prediction.Property("avg").EqualTo(Expression.Int(8)))) {
                var explain = q.Explain();
                explain.Contains("USING INDEX SumIndex").ShouldBeFalse();
                explain.Contains("USING INDEX AvgIndex").ShouldBeFalse();

                var rows = VerifyQuery(q, (_, result) => { result.GetArray(0)?.Count.ShouldBeGreaterThan(0); });
                aggregateModel.Error.ShouldBeNull();
                rows.ShouldBe(1);
                if (i == 0) {
                    aggregateModel.NumberOfCalls.ShouldBe(0);
                } else {
                    aggregateModel.NumberOfCalls.ShouldBeGreaterThan(0);
                }
            }

            DefaultCollection.DeleteIndex("AggIndex");
        }
    }

    [Fact]
    public void TestEuclideanDistance()
    {
        const string tests = "[[[10, 10], [13, 14], 5]," +
                             " [[1, 2, 3], [1, 2, 3], 0]," +
                             " [[], [], 0]," +
                             " [[1, 2], [1, 2, 3], null]," +
                             " [[1, 2], \"foo\", null]]";
        TestDistanceFunction(Function.EuclideanDistance(Expression.Property("v1"), Expression.Property("v2")), tests);
    }


    [Fact]
    public void TestSquaredEuclideanDistance()
    {
        const string tests = "[[[10, 10], [13, 14], 25]," +
                             " [[1, 2, 3], [1, 2, 3], 0]," +
                             " [[], [], 0]," +
                             " [[1, 2], [1, 2, 3], null]," +
                             " [[1, 2], \"foo\", null]]";
        TestDistanceFunction(Function.SquaredEuclideanDistance(Expression.Property("v1"), Expression.Property("v2")), tests);
    }

    [Fact]
    public void TestCosineDistance()
    {
        const string tests = "[[[10, 0], [0, 99], 1]," +
                             " [[1, 2, 3], [1, 2, 3], 0]," +
                             " [[], [], null]," +
                             " [[1, 2], [1, 2, 3], null]," +
                             " [[1, 2], \"foo\", null]]";
        TestDistanceFunction(Function.CosineDistance(Expression.Property("v1"), Expression.Property("v2")), tests);
    }

    private void TestDistanceFunction(IExpression distance, string testData)
    {
        var tests = DataOps.ParseTo<IList<IList<object?>>>(testData);
        tests.ShouldNotBeNull("because otherwise testData was invalid");

        foreach (var t in tests) {
            using var doc = new MutableDocument();
            doc.SetValue("v1", t[0]);
            doc.SetValue("v2", t[1]);
            doc.SetValue("distance", t[2]);
            SaveDocument(doc);
        }

        using var q = QueryBuilder.Select(SelectResult.Expression(distance), SelectResult.Property("distance"))
            .From(DataSource.Collection(DefaultCollection));
        var numRows = VerifyQuery(q, (_, r) =>
        {
            r.GetValue(0).ShouldBe(r.GetValue(1));
        });

        numRows.ShouldBe(tests.Count);
    }

    private void CreateDocument(params int[] numbers)
    {
        using var doc = new MutableDocument();
        doc.SetValue("numbers", numbers);
        SaveDocument(doc);
    }
}

internal abstract class TestPredictiveModel : IPredictiveModel
{
    public bool AllowCalls { get; set; } = true;

    public Exception? Error { get; private set; }

    public string Name => GetType().Name;

    public int NumberOfCalls { get; private set; }

    public void RegisterModel() => Database.Prediction.RegisterModel(Name, this);

    public void Reset()
    {
        NumberOfCalls = 0;
        AllowCalls = true;
    }

    public void UnregisterModel() => Database.Prediction.UnregisterModel(Name);

    protected abstract DictionaryObject? DoPrediction(DictionaryObject input);

    public DictionaryObject? Predict(DictionaryObject input)
    {
        if (!AllowCalls) {
            Error = new InvalidOperationException("Not allowed to be called in this state");
            return null;
        }

        NumberOfCalls++;
        return DoPrediction(input);
    }
}

internal sealed class EchoModel : TestPredictiveModel
{
    protected override DictionaryObject DoPrediction(DictionaryObject input) => input;
}

internal sealed class TextModel : TestPredictiveModel
{
    public string ContentType = "";

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

    protected override DictionaryObject? DoPrediction(DictionaryObject input)
    {
        var blob = input.GetBlob("text");
        if (blob == null) {
            return null;
        }

        ContentType = blob.ContentType ?? "";

        var text = Encoding.UTF8.GetString(blob.Content ?? []);
        var wc = text.Split([' '], StringSplitOptions.RemoveEmptyEntries).Length;
        var sc = text.Split(['.'], StringSplitOptions.RemoveEmptyEntries).Length;

        var output = new MutableDictionaryObject();
        output.SetInt("wc", wc);
        output.SetInt("sc", sc);
        return output;
    }
}

internal sealed class AggregateModel : TestPredictiveModel
{
    public static IExpression CreateInput(string propertyName)
    {
        return Expression.Dictionary(new Dictionary<string, object>
        {
            ["numbers"] = Expression.Property(propertyName)
        });
    }

    protected override DictionaryObject? DoPrediction(DictionaryObject input)
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
}
#endif
