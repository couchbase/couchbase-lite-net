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
#if !WINDOWS_UWP
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Couchbase.Lite;
using Couchbase.Lite.Enterprise.Query;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;

using FluentAssertions;

using LiteCore.Interop;

using Newtonsoft.Json;

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
        }
        #else
        public PredictiveQueryTest()
        {
            Database.Prediction.UnregisterModel(nameof(AggregateModel));
            Database.Prediction.UnregisterModel(nameof(TextModel));
        }
#endif

        [Fact]
        public void TestRegisterAndUnregisterModel()
        {
            CreateDocumentWithNumbers(1, 2, 3, 4, 5);
            CreateDocumentWithNumbers(6, 7, 8, 9, 10);

            var aggregateModel = new AggregateModel();
            var input = Expression.Dictionary(new Dictionary<string, object>
            {
                ["numbers"] = Expression.Property("numbers")
            });
            using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                    SelectResult.Expression(Function.Prediction(aggregateModel.Name, input)))
                .From(DataSource.Database(Db))) {
                Action badAction = () => q.Execute();
                badAction.ShouldThrow<CouchbaseSQLiteException>().Which.Error.Should().Be((int) SQLiteStatus.Error);

                aggregateModel.RegisterModel();
                var numRows = VerifyQuery(q, (i, result) =>
                {
                    var numbers = result.GetArray(0)?.ToList();
                    numbers.Should().NotBeEmpty("because otherwise the data didn't come through");
                    var pred = result.GetDictionary(1);
                    pred.Should().NotBeNull();
                    pred.GetLong("sum").Should().Be(numbers.Cast<long>().Sum());
                    pred.GetLong("min").Should().Be(numbers.Cast<long>().Min());
                    pred.GetLong("max").Should().Be(numbers.Cast<long>().Max());
                    pred.GetDouble("avg").Should().Be(numbers.Cast<long>().Average());
                });

                numRows.Should().Be(2);
                aggregateModel.UnregisterModel();
                badAction.ShouldThrow<CouchbaseSQLiteException>().Which.Error.Should().Be((int) SQLiteStatus.Error);
            }
        }

        [Fact]
        public void TestQueryValueFromDictionaryResult()
        {
            CreateDocumentWithNumbers(1, 2, 3, 4, 5);
            CreateDocumentWithNumbers(6, 7, 8, 9, 10);

            var aggregateModel = new AggregateModel();
            aggregateModel.RegisterModel();
            var input = Expression.Dictionary(new Dictionary<string, object>
            {
                ["numbers"] = Expression.Property("numbers")
            });
            using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                    SelectResult.Expression(Function.Prediction(aggregateModel.Name, input).Property("sum")).As("sum"))
                .From(DataSource.Database(Db))) {
                var numRows = VerifyQuery(q, (i, result) =>
                {
                    var numbers = result.GetArray(0)?.ToList();
                    numbers.Should().NotBeEmpty("because otherwise the data didn't come through");
                    var sum = result.GetLong(1);
                    sum.Should().Be(result.GetLong("sum"));
                    sum.Should().Be(numbers.Cast<long>().Sum());
                });

                numRows.Should().Be(2);
                aggregateModel.UnregisterModel();
            }
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
                using (var doc = new MutableDocument()) {
                    doc.SetBlob("text", new Blob("text/plain", Encoding.UTF8.GetBytes(text)));
                    SaveDocument(doc);
                }
            }

            var textModel = new TextModel();
            textModel.RegisterModel();
            
            var input = Expression.Dictionary(new Dictionary<string, object>
            {
                ["text"] = new List<object> { "BLOB", ".text" }
            });
            var prediction = Function.Prediction(textModel.Name, input).Property("wc");
            using (var q = QueryBuilder
                .Select(SelectResult.Property("text"), SelectResult.Expression(prediction).As("wc"))
                .From(DataSource.Database(Db))
                .Where(prediction.GreaterThan(Expression.Int(15)))) {
                foreach (var row in q.Execute()) {
                    WriteLine(row.GetInt("wc").ToString());
                }
            }

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
                textModel.UnregisterModel();
            }
        }

        [Fact]
        public void TestIndexPredictionValue()
        {
            CreateDocumentWithNumbers(1, 2, 3, 4, 5);
            CreateDocumentWithNumbers(6, 7, 8, 9, 10);

            var aggregateModel = new AggregateModel();
            aggregateModel.RegisterModel();
            var input = Expression.Dictionary(new Dictionary<string, object>
            {
                ["numbers"] = Expression.Property("numbers")
            });
            var sumPrediction = Function.Prediction(aggregateModel.Name, input).Property("sum");

            var index = IndexBuilder.ValueIndex(ValueIndexItem.Expression(sumPrediction));
            Db.CreateIndex("SumIndex", index);

            using (var q = QueryBuilder.Select(SelectResult.Property("numbers"),
                    SelectResult.Expression(sumPrediction))
                .From(DataSource.Database(Db))
                .Where(sumPrediction.EqualTo(Expression.Int(15)))) {
                q.Explain().IndexOf("USING INDEX SumIndex").Should()
                    .NotBe(-1, "because the query should make use of the index");
                var numRows = VerifyQuery(q, (n, r) =>
                {
                    var numbers = r.GetArray(0).Cast<long>().ToList();
                    var sum = r.GetLong(1);
                    sum.Should().Be(numbers.Sum());
                });

                numRows.Should().Be(1);
                aggregateModel.NumberOfCalls.Should().Be(2,
                    "because the value should be cached and not call the prediction function again");
            }
        }

        [Fact]
        public void TestIndexMultiplePredictionValues()
        {
            CreateDocumentWithNumbers(1, 2, 3, 4, 5);
            CreateDocumentWithNumbers(6, 7, 8, 9, 10);

            var aggregateModel = new AggregateModel();
            aggregateModel.RegisterModel();
            var input = Expression.Dictionary(new Dictionary<string, object>
            {
                ["numbers"] = Expression.Property("numbers")
            });
            var sumPrediction = Function.Prediction(aggregateModel.Name, input).Property("sum");
            var avgPrediction = Function.Prediction(aggregateModel.Name, input).Property("avg");

            var sumIndex = IndexBuilder.ValueIndex(ValueIndexItem.Expression(sumPrediction));
            Db.CreateIndex("SumIndex", sumIndex);
            var avgIndex = IndexBuilder.ValueIndex(ValueIndexItem.Expression(avgPrediction));
            Db.CreateIndex("AvgIndex", avgIndex);

            using (var q = QueryBuilder.Select(SelectResult.Expression(sumPrediction).As("s"),
                    SelectResult.Expression(avgPrediction).As("a"))
                .From(DataSource.Database(Db))
                .Where(sumPrediction.LessThanOrEqualTo(Expression.Int(15)).Or(avgPrediction.EqualTo(Expression.Int(8))))) {
                var explain = q.Explain();
                explain.IndexOf("USING INDEX SumIndex").Should().NotBe(-1, "because the sum index should be used");
                explain.IndexOf("USING INDEX AvgIndex").Should().NotBe(-1, "because the average index should be used");

                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.Should().Match<Result>(x => x.GetLong(0) == 15 || x.GetLong(1) == 8);
                });
                numRows.Should().Be(2);
            }
        }

        [Fact]
        public void TestIndexCompoundPredictiveValues()
        {
            CreateDocumentWithNumbers(1, 2, 3, 4, 5);
            CreateDocumentWithNumbers(6, 7, 8, 9, 10);

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
            Db.CreateIndex("SumAvgIndex", index);

            using (var q = QueryBuilder.Select(SelectResult.Expression(sumPrediction).As("s"),
                    SelectResult.Expression(avgPrediction).As("a"))
                .From(DataSource.Database(Db))
                .Where(sumPrediction.EqualTo(Expression.Int(15)).And(avgPrediction.EqualTo(Expression.Int(3))))) {
                var explain = q.Explain();
                explain.IndexOf("USING INDEX SumAvgIndex").Should().NotBe(-1, "because the sum index should be used");

                var numRows = VerifyQuery(q, (n, r) =>
                {
                    r.GetLong(0).Should().Be(15);
                    r.GetLong(1).Should().Be(3);
                });
                numRows.Should().Be(1);
                aggregateModel.NumberOfCalls.Should().Be(4);
            }
        }

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

        private void CreateDocumentWithNumbers(params int[] numbers)
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

        public abstract string Name { get; }
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

        public DictionaryObject Prediction(DictionaryObject input)
        {
            NumberOfCalls++;
            return DoPrediction(input);
        }

        #endregion
    }

    internal sealed class TextModel : TestPredictiveModel
    {
        #region Properties

        public override string Name => nameof(TextModel);

        #endregion

        #region Overrides

        protected override DictionaryObject DoPrediction(DictionaryObject input)
        {
            var blob = input.GetBlob("text");
            if (blob == null) {
                return null;
            }

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
        #region Properties

        public override string Name => nameof(AggregateModel);

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