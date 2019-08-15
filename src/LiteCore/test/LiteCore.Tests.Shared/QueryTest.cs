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
#if !CBL_NO_EXTERN_FILES
using System.Collections.Generic;
using System.Text;

using LiteCore.Interop;
using FluentAssertions;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace LiteCore.Tests
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public unsafe class QueryTest : QueryTestBase
    {
        protected override string JsonPath => "C/tests/data/names_100.json";

#if !WINDOWS_UWP
        public QueryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestQueryDB()
        {
            RunTestVariants(() => {
                Compile(Json5("['=', ['.', 'contact', 'address', 'state'], 'CA']"));
                Run().Should().Equal(new[] { "0000001", "0000015", "0000036", "0000043", "0000053", "0000064", 
                "0000072", "0000073" }, "because otherwise the query returned incorrect results");

                Compile(Json5("['=', ['.', 'contact', 'address', 'state'], 'CA']"), addOffsetLimit: true);
                Run("{\"offset\":1,\"limit\":8}").Should().Equal(new[] { "0000015", "0000036", "0000043", "0000053", "0000064", 
                "0000072", "0000073" }, "because otherwise the query returned incorrect results");
                Run("{\"offset\":1,\"limit\":4}").Should().Equal(new[] { "0000015", "0000036", "0000043", "0000053" }, 
                "because otherwise the query returned incorrect results");

                Compile(Json5("['AND', ['=', ['array_count()', ['.', 'contact', 'phone']], 2]," +
                           "['=', ['.', 'gender'], 'male']]"));
                Run().Should().Equal(new[] { "0000002", "0000014", "0000017", "0000027", "0000031", "0000033", 
                "0000038", "0000039", "0000045", "0000047", "0000049", "0000056", "0000063", "0000065", "0000075", 
                "0000082", "0000089", "0000094", "0000097" }, "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestQueryDBIn()
        {
            RunTestVariants(() =>
            {
                // Type 1: RHS is an expression; generates a call to array_contains
                Compile(Json5("['IN', 'reading', ['.', 'likes']]"));
                Run().Should().Equal( "0000004", "0000056", "0000064", "0000079", "0000099");

                // Type 2: RHS is an array literal; generates a SQL "IN" expression
                Compile(Json5("['IN', ['.', 'name', 'first'], ['[]', 'Eddie', 'Verna']]"));
                Run().Should().Equal("0000091", "0000093");
            });
        }

        [Fact]
        public void TestQueryDBSorted()
        {
            RunTestVariants(() => {
                Compile(Json5("['=', ['.', 'contact', 'address', 'state'], 'CA']"), Json5("[['.', 'name', 'last']]"));
                Run().Should().Equal(new[] { "0000015", "0000036", "0000072", "0000043", "0000001", "0000064", 
                "0000073", "0000053" }, "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestQueryDBBindings()
        {
            RunTestVariants(() => {
                Compile(Json5("['=', ['.', 'contact', 'address', 'state'], ['$', 1]]"));
                Run("{\"1\": \"CA\"}").Should().Equal(new[] { "0000001", "0000015", "0000036", "0000043", 
                "0000053", "0000064", "0000072", "0000073" }, 
                "because otherwise the query returned incorrect results");

                Compile(Json5("['=', ['.', 'contact', 'address', 'state'], ['$', 'state']]"));
                Run("{\"state\": \"CA\"}").Should().Equal(new[] { "0000001", "0000015", "0000036", "0000043", 
                "0000053", "0000064", "0000072", "0000073" }, 
                "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestDBQueryAny()
        {
            RunTestVariants(() => {
                Compile(Json5("['ANY', 'like', ['.', 'likes'], ['=', ['?', 'like'], 'climbing']]"));
                Run().Should().Equal(new[] { "0000017", "0000021", "0000023", "0000045", "0000060" }, 
                    "because otherwise the query returned incorrect results");

                // This EVERY query has lots of results because every empty `likes` array matches it
                Compile(Json5("['EVERY', 'like', ['.', 'likes'], ['=', ['?', 'like'], 'taxes']]"));
                var results = Run();
                results.Count.Should().Be(42, "because otherwise the query returned incorrect results");
                results[0].Should().Be("0000007", "because otherwise the query returned incorrect results");

                // Changing the op to ANY AND EVERY returns no results
                Compile(Json5("['ANY AND EVERY', 'like', ['.', 'likes'], ['=', ['?', 'like'], 'taxes']]"));
                Run().Should().BeEmpty("because otherwise the query returned incorrect results");

                // Look for people where every like contains an L:
                Compile(Json5("['ANY AND EVERY', 'like', ['.', 'likes'], ['LIKE', ['?', 'like'], '%l%']]"));
                Run().Should().Equal(new[] { "0000017", "0000027", "0000060", "0000068" }, 
                    "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestDBQueryAnyOfDict()
        {
            RunTestVariants(() =>
            {
                Compile(Json5("['ANY', 'n', ['.', 'name'], ['=', ['?', 'n'], 'Arturo']]"));
                Run().Should().Equal("0000090");

                Compile(Json5("['ANY', 'n', ['.', 'name'], ['contains()', ['?', 'n'], 'V']]"));
                Run().Should().Equal("0000044", "0000048", "0000053", "0000093");
            });
        }

        [Fact]
        public void TestDBQueryExpressionIndex()
        {
            RunTestVariants(() => {
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "test", Json5("[['length()', ['.name.first']]]"), 
                    C4IndexType.ValueIndex, null, err));
                Compile(Json5("['=', ['length()', ['.name.first']], 9]"));
                Run().Should().Equal(new[] { "0000015", "0000099" }, "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestDeleteIndexedDoc()
        {
            RunTestVariants(() => {
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "test", Json5("[['length()', ['.name.first']]]"), 
                    C4IndexType.ValueIndex, null, err));
                
                // Delete doc "0000015":
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, "0000015", true, err));
                    var rq = new C4DocPutRequest {
                        docID = FLSlice.Constant("0000015"),
                        history = (FLSlice *)&doc->revID,
                        historyCount = 1,
                        revFlags = C4RevisionFlags.Deleted,
                        save = true
                    };
                    var updatedDoc = (C4Document *)LiteCoreBridge.Check(err => {
                        var localRq = rq;
                        return Native.c4doc_put(Db, &localRq, null, err);
                    });

                    Native.c4doc_free(doc);
                    Native.c4doc_free(updatedDoc);
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                // Now run a query that would have returned the deleted doc, if it weren't deleted:
                Compile(Json5("['=', ['length()', ['.name.first']], 9]"));
                Run().Should().Equal(new[] { "0000099" }, "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestFullTextQuery()
        {
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "byStreet", "[[\".contact.address.street\"]]",
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['MATCH', 'byStreet', 'Hwy']"));

                var expected = new[]
                {
                    new C4FullTextMatch(13, 0, 0, 10, 3),
                    new C4FullTextMatch(15, 0, 0, 11, 3),
                    new C4FullTextMatch(43, 0, 0, 12, 3),
                    new C4FullTextMatch(44, 0, 0, 12, 3),
                    new C4FullTextMatch(52, 0, 0, 11, 3)
                };

                int index = 0;
                foreach (var result in RunFTS()) {
                    foreach (var match in result) {
                        match.Should().Be(expected[index++]);
                    }
                }
            });
        }

        [Fact]
        public void TestFullTextMultipleProperties()
        {
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "byAddress", "[[\".contact.address.street\"],[\".contact.address.city\"],[\".contact.address.state\"]]", 
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['MATCH', 'byAddress', 'Santa']"));
                var expected = new[]
                {
                    new C4FullTextMatch(15, 1, 0, 0, 5),
                    new C4FullTextMatch(44, 0, 0, 3, 5),
                    new C4FullTextMatch(68, 0, 0, 3, 5),
                    new C4FullTextMatch(72, 1, 0, 0, 5)
                };

                int index = 0;
                foreach (var result in RunFTS()) {
                    foreach (var match in result) {
                        match.Should().Be(expected[index++]);
                    }
                }

                Compile(Json5("['MATCH', 'byAddress', 'contact.address.street:Santa']"));
                expected = new[]
                {
                    new C4FullTextMatch(44, 0, 0, 3, 5),
                    new C4FullTextMatch(68, 0, 0, 3, 5)
                };

                index = 0;
                foreach (var result in RunFTS()) {
                    foreach (var match in result) {
                        match.Should().Be(expected[index++]);
                    }
                }

                Compile(Json5("['MATCH', 'byAddress', 'contact.address.street:Santa Saint']"));
                expected = new[]
                {
                    new C4FullTextMatch(68, 0, 0, 3, 5),
                    new C4FullTextMatch(68, 1, 1, 0, 5)
                };

                index = 0;
                foreach (var result in RunFTS()) {
                    foreach (var match in result) {
                        match.Should().Be(expected[index++]);
                    }
                }

                Compile(Json5("['MATCH', 'byAddress', 'contact.address.street:Santa OR Saint']"));
                expected = new[]
                {
                    new C4FullTextMatch(20, 1, 1, 0, 5),
                    new C4FullTextMatch(44, 0, 0, 3, 5),
                    new C4FullTextMatch(68, 0, 0, 3, 5),
                    new C4FullTextMatch(68, 1, 1, 0, 5),
                    new C4FullTextMatch(77, 1, 1, 0, 5)
                };

                index = 0;
                foreach (var result in RunFTS()) {
                    foreach (var match in result) {
                        match.Should().Be(expected[index++]);
                    }
                }
            });
        }

        [Fact]
        public void TestMultipleFullTextIndexes()
        {
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "byStreet", "[[\".contact.address.street\"]]",
                    C4IndexType.FullTextIndex, null, err));
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "byCity", "[[\".contact.address.city\"]]",
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['AND', ['MATCH', 'byStreet', 'Hwy'],['MATCH', 'byCity', 'Santa']]"));
                var results = RunFTS();
                results.Count.Should().Be(1);
                results[0].Should().Equal(new C4FullTextMatch(15, 0, 0, 11, 3));
            });
        }

        [Fact]
        public void TestFullTextQueryInMultipleAnds()
        {
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "byStreet", "[[\".contact.address.street\"]]",
                    C4IndexType.FullTextIndex, null, err));
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "byCity", "[[\".contact.address.city\"]]",
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5(
                    "['AND', ['AND', ['=', ['.gender'], 'male'],['MATCH', 'byCity', 'Santa']],['=',['.name.first'], 'Cleveland']]"));
                Run().Should().Equal("0000015");
                var results = RunFTS();
                results.Count.Should().Be(1);
                results[0].Should().Equal(new C4FullTextMatch(15, 0, 0, 0, 5));
            });
        }

        [Fact]
        public void TestMultipleFullTextQueries()
        {
            // You can't query the same FTS index multiple times in a query (says SQLite)
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "byStreet", "[[\".contact.address.street\"]]",
                    C4IndexType.FullTextIndex, null, err));
                C4Error error;
                _query = Native.c4query_new(Db,
                    Json5("['AND', ['MATCH', 'byStreet', 'Hwy'], ['MATCH', 'byStreet', 'Blvd']]"), &error);
                ((long) _query).Should().Be(0, "because this type of query is not allowed");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int) C4ErrorCode.InvalidQuery);
                Native.c4error_getMessage(error).Should()
                    .Be("Sorry, multiple MATCHes of the same property are not allowed");
            });
        }

        [Fact]
        public void TestBuriedFullTextQueries()
        {
            // You can't put an FTS match inside an expression other than a top-level AND (says SQLite)
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4db_createIndex(Db, "byStreet", "[[\".contact.address.street\"]]",
                    C4IndexType.FullTextIndex, null, err));
                C4Error error;
                _query = Native.c4query_new(Db,
                    Json5("['OR', ['MATCH', 'byStreet', 'Hwy'],['=', ['.', 'contact', 'address', 'state'], 'CA']]"), &error);
                ((long) _query).Should().Be(0, "because this type of query is not allowed");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int) C4ErrorCode.InvalidQuery);
                Native.c4error_getMessage(error).Should()
                    .Be("MATCH can only appear at top-level, or in a top-level AND");
            });
        }

        [Fact]
        public void TestDBQueryWhat()
        {
            RunTestVariants(() =>
            {
                var expectedFirst = new[] { "Cleveland", "Georgetta", "Margaretta" };
                var expectedLast = new[] { "Bejcek", "Kolding", "Ogwynn" };
                var query = CompileSelect(Json5("{WHAT: ['.name.first', '.name.last'], " +
                            "WHERE: ['>=', ['length()', ['.name.first']], 9]," +
                            "ORDER_BY: [['.name.first']]}"));

                Native.c4query_columnCount(query).Should().Be(2, "because there are two requested items in WHAT");
                var e = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
               {
                   var localOpts = C4QueryOptions.Default;
                   return Native.c4query_run(query, &localOpts, null, err);
               });

                int i = 0;
                C4Error error;
                while (Native.c4queryenum_next(e, &error)) {
                    Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0)).Should()
                        .Be(expectedFirst[i], "because otherwise the query returned incorrect results");
                    Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 1)).Should().Be(expectedLast[i], "because otherwise the query returned incorrect results");
                    ++i;
                }

                error.code.Should().Be(0, "because otherwise an error occurred during enumeration");
                i.Should().Be(3, "because that is the number of expected rows");
                Native.c4queryenum_free(e);
            });
        }

        [Fact]
        public void TestDBQueryWhatReturningObject()
        {
            RunTestVariants(() =>
            {
                var expectedFirst = new[] { "Cleveland", "Georgetta", "Margaretta" };
                var expectedLast = new[] { "Bejcek", "Kolding", "Ogwynn" };
                CompileSelect(Json5(
                    "{WHAT: ['.name'], WHERE: ['>=', ['length()', ['.name.first']], 9], ORDER_BY: [['.name.first']]}"));
                Native.c4query_columnCount(_query).Should().Be(1);

                var e = (C4QueryEnumerator*) LiteCoreBridge.Check(err =>
                {
                    var opts = C4QueryOptions.Default;
                    return Native.c4query_run(_query, &opts, null, err);
                });

                var i = 0;
                C4Error error;
                while (Native.c4queryenum_next(e, &error)) {
                    var col = Native.FLArrayIterator_GetValueAt(&e->columns, 0);
                    Native.FLValue_GetType(col).Should().Be(FLValueType.Dict);
                    var name = Native.FLValue_AsDict(col);
                    WriteLine(Native.FLValue_ToJSONX(col, false, false));
                    Native.FLValue_AsString(Native.FLDict_Get(name, Encoding.UTF8.GetBytes("first")))
                        .Should().Be(expectedFirst[i]);
                    Native.FLValue_AsString(Native.FLDict_Get(name, Encoding.UTF8.GetBytes("last")))
                        .Should().Be(expectedLast[i]);
                    ++i;
                }

                error.code.Should().Be(0, "because otherwise an error occurred during enumeration");
                i.Should().Be(3, "because that is the number of expected rows");
                Native.c4queryenum_free(e);
            });
        }

        [Fact]
        public void TestDBQueryAggregate()
        {
            RunTestVariants(() =>
            {
                CompileSelect(Json5("{WHAT: [['min()', ['.name.last']], ['max()', ['.name.last']]]}"));
                var e = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                {
                    var opts = C4QueryOptions.Default;
                    return Native.c4query_run(_query, &opts, null, err);
                });

                var i = 0;
                C4Error error;
                while (Native.c4queryenum_next(e, &error)) {
                    Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0)).Should().Be("Aerni",
                        "because otherwise the query returned incorrect results");
                    Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 1)).Should().Be("Zirk",
                        "because otherwise the query returned incorrect results");
                    ++i;
                }

                error.code.Should().Be(0, "because otherwise an error occurred during enumeration");
                i.Should().Be(1, "because there is only one result for the query");
                Native.c4queryenum_free(e);
            });
        }

        [Fact]
        public void TestDBQueryGrouped()
        {
            RunTestVariants(() =>
            {
                var expectedState = new[] {"AL", "AR", "AZ", "CA"};
                var expectedMin = new[] {"Laidlaw", "Okorududu", "Kinatyan", "Bejcek"};
                var expectedMax = new[] {"Mulneix", "Schmith", "Kinatyan", "Visnic"};
                const int expectedRowCount = 42;
                CompileSelect(Json5(
                    "{WHAT: [['.contact.address.state'],['min()', ['.name.last']],['max()', ['.name.last']]],GROUP_BY: [['.contact.address.state']]}"));
                var e = (C4QueryEnumerator*) LiteCoreBridge.Check(err =>
                {
                    var opts = C4QueryOptions.Default;
                    return Native.c4query_run(_query, &opts, null, err);
                });

                C4Error error;
                int i = 0;
                while (Native.c4queryenum_next(e, &error)) {
                    var state = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0));
                    var minName = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 1));
                    var maxName = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 2));
                    WriteLine($"state={state}, first={minName}, last={maxName}");
                    if (i < expectedState.Length) {
                        state.Should().Be(expectedState[i]);
                        minName.Should().Be(expectedMin[i]);
                        maxName.Should().Be(expectedMax[i]);
                    }

                    ++i;
                }

                error.code.Should().Be(0);
                i.Should().Be(expectedRowCount);
                Native.c4queryenum_free(e);
            });
        }

        [Fact]
        public void TestDBQueryJoin()
        {
            RunTestVariants(() =>
            {
                ImportJSONFile("C/tests/data/states_titlecase.json", "state-");
                var expectedFirst = new[] {"Cleveland", "Georgetta", "Margaretta"};
                var expectedState = new[] {"California", "Ohio", "South Dakota"};
                CompileSelect(Json5("{WHAT: ['.person.name.first', '.state.name']," +
                              "FROM: [{as: 'person'}, {as: 'state', on: ['=', ['.state.abbreviation'],['.person.contact.address.state']]}]," +
                              "WHERE: ['>=', ['length()', ['.person.name.first']], 9]," +
                              "ORDER_BY: [['.person.name.first']]}"));
                var e = (C4QueryEnumerator*) LiteCoreBridge.Check(err =>
                {
                    var opts = C4QueryOptions.Default;
                    return Native.c4query_run(_query, &opts, null, err);
                });

                int i = 0;
                C4Error error;
                while (Native.c4queryenum_next(e, &error)) {
                    var first = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0));
                    var state = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 1));
                    WriteLine($"first='{first}', state='{state}'");
                    first.Should().Be(expectedFirst[i]);
                    state.Should().Be(expectedState[i]);
                    ++i;
                }

                error.code.Should().Be(0);
                i.Should().Be(3, "because there should be three resulting rows");
                Native.c4queryenum_free(e);
            });
        }

        [Fact]
        public void TestDBQuerySeek()
        {
            RunTestVariants(() =>
            {
                Compile(Json5("['=', ['.', 'contact', 'address', 'state'], 'CA']"));
                var e = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                {
                    var opts = C4QueryOptions.Default;
                    return Native.c4query_run(_query, &opts, null, err);
                });

                C4Error error;
                Native.c4queryenum_next(e, &error).Should().BeTrue();
                Native.FLArrayIterator_GetCount(&e->columns).Should().BeGreaterThan(0);
                var docID = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0));
                docID.Should().Be("0000001");
                Native.c4queryenum_next(e, &error).Should().BeTrue();
                Native.c4queryenum_seek(e, 0, &error).Should().BeTrue();
                docID = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0));
                docID.Should().Be("0000001");
                Native.c4queryenum_seek(e, 7, &error).Should().BeTrue();
                docID = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0));
                docID.Should().Be("0000073");
                Native.c4queryenum_seek(e, 100, &error).Should().BeFalse();
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int) C4ErrorCode.InvalidParameter);
                Native.c4queryenum_free(e);
            });
        }

        [Fact]
        public void TestQueryParserErrorMessages()
        {
            RunTestVariants(() =>
            {
                C4Error error;
                _query = Native.c4query_new(Db, "[\"=\"]", &error);
                ((long) _query).Should().Be(0L);
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int) C4ErrorCode.InvalidQuery);
                Native.c4error_getMessage(error).Should().Be("Wrong number of arguments to =");
            });
        }
    }
}
#endif