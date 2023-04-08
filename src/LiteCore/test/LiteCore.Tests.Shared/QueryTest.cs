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
using System.Runtime.InteropServices;
using System.Threading;
using System;
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
        private static readonly C4QueryObserverCallback QueryCallback = QueryObserverCallback;

        private C4QueryObserver* _queryObserver;
        private C4QueryObserver* _queryObserver2;
        private C4QueryObserver* _queryObserver3;

        private int _queryCallbackCalls;
        private int _queryCallbackCalls2;
        private int _queryCallbackCalls3;

        protected override string JsonPath => "C/tests/data/names_100.json";

#if !WINDOWS_UWP
        public QueryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        //Adding a C4QueryObserver to a query makes it "live" as in CBL: the query will be run(in the background),
        //and the observer callback will be called with the enumerator when available.
        //The query will re-run whenever the database changes, and the observer will be called every time the results change, until the observer is freed.
        /* 
        A query observer, also called a "live query", notifies the client when the query's result
        set changes. (Not just any time the database changes.)

        This is done as follows, starting from when the first time an observer on a particular
        query is enabled:

        1. A separate C4Query instance is created, on a separate database instance
           (there's one of these background database instances per C4Database.)
        2. The copied query is run on a background thread, and it saves its results.
        3. The query observer(s) are notified so they can see the initial results.
        4. The background thread listens for changes to the database, _or_ changes to the query
           parameters (\ref c4query_setParameters). In response:
           - If it's been less than 250ms since the last time it ran the query, it first waits
             500ms; during this time it ignores further database changes.
           - It runs the query.
           - It compares the new result set to the old one; if they're different, it saves the
             new results and notifies observers.Otherwise it does nothing.
        6. This background task stops when the last observer is disabled.

        Some notes on performance:
     
        * All C4Queries on a single C4Database share a single background C4Database, which can
          only do one thing at a time.That means multiple live queries can bog down since they
          have to run one after the other.
        * The first time any query observer is added in a given _C4Database_, the background
          database instance has to be opened, which takes a few milliseconds.
        * The first time an observer is added to a C4Query, a copy of that query has to be
          created and compiled by the background database, which can also take a few millseconds.
        * Running a C4Query before adding an observer is a bit of a waste, because the query will
          be run twice.It's more efficient to skip running it, and instead wait for the first
          call to the observer.
        * The timing logic in step 4 is a heuristic to provide low latency on occasional database
          changes, but prevent rapid database changes (as happen during pull replication) from
          running the query constantly and/or spamming observers with notifications.
          (The specific times are not currently alterable; they're constants in LiveQuerier.cc.)
            */
        [Fact]
        public void TestQueryObserver()
        {
            RunTestVariants(() =>
            {
                var handle = GCHandle.Alloc(this);
                try {
                    Compile(Json5("['=', ['.', 'contact', 'address', 'state'], 'CA']"));
                    C4Error error;
                    /** Creates a new query observer, with a callback that will be invoked when the query
                     *  results change, with an enumerator containing the new results.
                     *  \note The callback isn't invoked immediately after a change, and won't be invoked after
                     *  every change, to avoid performance problems. Instead, there's a brief delay so multiple
                     *  changes can be coalesced.
                     *  \note The new observer needs to be enabled by calling \ref c4queryobs_setEnabled.
                     */
                    _queryObserver = NativeRaw.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    /** Enables a query observer so its callback can be called, or disables it to stop callbacks. */
                    NativeRaw.c4queryobs_setEnabled(_queryObserver, true);

                    WriteLine("---- Waiting for query observer...");
                    Thread.Sleep(2000);

                    WriteLine("Checking query observer...");
                    _queryCallbackCalls.Should().Be(1, "because we should have received a callback");
                    var e = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        /** Returns the current query results, if any.
                         * When the observer is created, the results are initially NULL until the query finishes
                         * running in the background.
                         * Once the observer callback is called, the results are available.
                         * \note  You are responsible for releasing the returned reference.
                         * @param obs  The query observer.
                         * @param forget  If true, the observer will not hold onto the enumerator, and subsequent calls
                         * will return NULL until the next time the observer notifies you. This can help
                         * conserve memory, since the query result data will be freed as soon as you
                         * release the enumerator.
                         * @param error  If the last evaluation of the query failed, the error will be stored here.
                         * @return  The current query results, or NULL if the query hasn't run or has failed. 
                         */
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver, true, err);
                    });
                    //REQUIRE(e);
                    //CHECK(c4queryobs_getEnumerator(state.obs, true, &error) == nullptr);
                    //CHECK(error.code == 0);
                    Native.c4queryenum_getRowCount(e, &error).Should().Be(8);

                    AddPersonInState("after1", "AL");

                    WriteLine("---- Checking that query observer doesn't fire...");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    _queryCallbackCalls.Should().Be(1);

                    WriteLine("---- Changing a doc in the query");
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        AddPersonInState("after2", "CA");
                        // wait, to make sure the observer doesn't try to run the query before the commit
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        WriteLine("---- Commiting changes");
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    WriteLine("---- Waiting for 2nd call of query observer...");
                    Thread.Sleep(2000);

                    WriteLine("---- Checking query observer again...");
                    _queryCallbackCalls.Should().Be(2, "because we should have received a callback");
                    var e2 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver, false, err);
                    });
                    //REQUIRE(e2);
                    //CHECK(e2 != e);
                    var e3 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver, false, err);
                    });
                    //CHECK(e3 == e2);
                    Native.c4queryenum_getRowCount(e2, &error).Should().Be(9);

                    // Testing with purged document:
                    WriteLine("---- Purging a document...");
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        Native.c4coll_purgeDoc(Native.c4db_getDefaultCollection(Db, null), "after2", &error);
                        WriteLine("---- Commiting changes");
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    WriteLine("---- Waiting for 3rd call of query observer...");
                    Thread.Sleep(2000);

                    WriteLine("---- Checking query observer again...");
                    _queryCallbackCalls.Should().Be(3, "because we should have received a callback");
                    e2 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver, true, err);
                    });
                    //REQUIRE(e2);
                    //CHECK(e2 != e);
                    Native.c4queryenum_getRowCount(e2, &error).Should().Be(8);
                } finally {
                    handle.Free();
                }
            });
        }

        [Fact]
        public void TestMultipleQueryObservers()
        {
            RunTestVariants(() =>
            {
                var handle = GCHandle.Alloc(this);
                try {
                    Compile(Json5("['=', ['.', 'contact', 'address', 'state'], 'CA']"));
                    C4Error error;

                    _queryObserver = NativeRaw.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    //CHECK(_queryObserver);
                    NativeRaw.c4queryobs_setEnabled(_queryObserver, true);

                    _queryObserver2 = NativeRaw.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    //CHECK(_queryObserver2);
                    NativeRaw.c4queryobs_setEnabled(_queryObserver2, true);

                    WriteLine("---- Waiting for query observers...");
                    Thread.Sleep(2000);

                    WriteLine("Checking query observers...");
                    _queryCallbackCalls.Should().Be(1, "because we should have received a callback");
                    var e1 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver, true, err);
                    });
                    //REQUIRE(e1);
                    //CHECK(error.code == 0);
                    Native.c4queryenum_getRowCount(e1, &error).Should().Be(8);
                    _queryCallbackCalls2.Should().Be(1, "because we should have received a callback");
                    var e2 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver2, true, err);
                    });
                    //REQUIRE(e2);
                    //CHECK(error.code == 0);
                    //CHECK(e2 != e1);
                    Native.c4queryenum_getRowCount(e2, &error).Should().Be(8);

                    _queryObserver3 = NativeRaw.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    //CHECK(_queryObserver3);
                    NativeRaw.c4queryobs_setEnabled(_queryObserver3, true);

                    WriteLine("---- Waiting for a new query observer...");
                    Thread.Sleep(2000);

                    WriteLine("Checking a new query observer...");
                    _queryCallbackCalls3.Should().Be(1, "because we should have received a callback");
                    var e3 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver3, true, err);
                    });
                    //REQUIRE(e3);
                    //CHECK(error.code == 0);
                    //CHECK(e3 != e2);
                    Native.c4queryenum_getRowCount(e3, &error).Should().Be(8);

                    WriteLine("Iterating all query results...");
                    int count = 0;
                    while (Native.c4queryenum_next(e1, null) && Native.c4queryenum_next(e2, null) && Native.c4queryenum_next(e3, null)) {
                        ++count;
                        FLArrayIterator col1 = e1->columns;
                        FLArrayIterator col2 = e2->columns;
                        FLArrayIterator col3 = e3->columns;
                        var c = Native.FLArrayIterator_GetCount(&col1);
                        c.Should().Be(Native.FLArrayIterator_GetCount(&col2));
                        c.Should().Be(Native.FLArrayIterator_GetCount(&col3));
                        for (uint i = 0; i < c; ++i) {
                            var v1 = Native.FLArrayIterator_GetValueAt(&col1, i);
                            var v2 = Native.FLArrayIterator_GetValueAt(&col2, i);
                            var v3 = Native.FLArrayIterator_GetValueAt(&col3, i);
                            Native.FLValue_IsEqual(v1, v2).Should().BeTrue();
                            Native.FLValue_IsEqual(v2, v3).Should().BeTrue();
                        }
                    }

                    count.Should().Be(8);
                } finally {
                    handle.Free();
                }
            });
        }

        [Fact]
        public void TestQueryObserverWithChangingQueryParameters()
        {
            RunTestVariants(() =>
            {
                var handle = GCHandle.Alloc(this);
                try {
                    Compile(Json5("['=', ['.', 'contact', 'address', 'state'], ['$state']]"));
                    C4Error error;

                    //As part of this, added c4query_setParameters. Parameter values can now be stored with a query as in the CBL API.
                    //Pass null parameters to c4query_run to use the ones stored in the query.
                    Native.c4query_setParameters(_query, "{\"state\": \"CA\"}");

                    var explain = Native.c4query_explain(_query);
                    //CHECK(explain);
                    WriteLine($"Explain = {explain}");

                    _queryObserver = NativeRaw.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    //CHECK(_queryObserver);
                    NativeRaw.c4queryobs_setEnabled(_queryObserver, true);

                    WriteLine("---- Waiting for query observers...");
                    Thread.Sleep(2000);

                    WriteLine("Checking query observers...");
                    _queryCallbackCalls.Should().Be(1, "because we should have received a callback");
                    var e1 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver, true, err);
                    });
                    //REQUIRE(e);
                    //CHECK(c4queryobs_getEnumerator(state.obs, true, &error) == nullptr);
                    //CHECK(error.code == 0);
                    Native.c4queryenum_getRowCount(e1, &error).Should().Be(8);

                    Native.c4query_setParameters(_query, "{\"state\": \"NY\"}");

                    WriteLine("---- Waiting for query observers after changing the parameters...");
                    Thread.Sleep(5000);

                    WriteLine("Checking query observers...");
                    _queryCallbackCalls.Should().Be(2, "because we should have received a callback");
                    var e2 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return NativeRaw.c4queryobs_getEnumerator(_queryObserver, true, err);
                    });
                    //REQUIRE(e2);
                    //CHECK(error.code == 0);
                    Native.c4queryenum_getRowCount(e2, &error).Should().Be(9);
                } finally {
                    handle.Free();
                }
            });
        }

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
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "test", Json5("[['length()', ['.name.first']]]"), C4QueryLanguage.JSONQuery, 
                    C4IndexType.ValueIndex, null, err));
                Compile(Json5("['=', ['length()', ['.name.first']], 9]"));
                Run().Should().Equal(new[] { "0000015", "0000099" }, "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestDeleteIndexedDoc()
        {
            RunTestVariants(() => {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "test", Json5("[['length()', ['.name.first']]]"), C4QueryLanguage.JSONQuery, 
                    C4IndexType.ValueIndex, null, err));
                
                // Delete doc "0000015":
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4coll_getDoc(Native.c4db_getDefaultCollection(Db, null), 
                        "0000015", true, C4DocContentLevel.DocGetCurrentRev, err));
                    var rq = new C4DocPutRequest {
                        docID = FLSlice.Constant("0000015"),
                        history = (FLSlice *)&doc->revID,
                        historyCount = 1,
                        revFlags = C4RevisionFlags.Deleted,
                        save = true
                    };
                    var updatedDoc = (C4Document *)LiteCoreBridge.Check(err => {
                        var localRq = rq;
                        return Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &localRq, null, err);
                    });

                    Native.c4doc_release(doc);
                    Native.c4doc_release(updatedDoc);
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
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['MATCH()', 'byStreet', 'Hwy']"));

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
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byAddress", "[[\".contact.address.street\"],[\".contact.address.city\"],[\".contact.address.state\"]]", C4QueryLanguage.JSONQuery, 
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['MATCH()', 'byAddress', 'Santa']"));
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

                Compile(Json5("['MATCH()', 'byAddress', 'contact.address.street:Santa']"));
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

                Compile(Json5("['MATCH()', 'byAddress', 'contact.address.street:Santa Saint']"));
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

                Compile(Json5("['MATCH()', 'byAddress', 'contact.address.street:Santa OR Saint']"));
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
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byCity", "[[\".contact.address.city\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['AND', ['MATCH()', 'byStreet', 'Hwy'],['MATCH()', 'byCity', 'Santa']]"));
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
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byCity", "[[\".contact.address.city\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5(
                    "['AND', ['AND', ['=', ['.gender'], 'male'],['MATCH()', 'byCity', 'Santa']],['=',['.name.first'], 'Cleveland']]"));
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
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                C4Error error;
                _query = Native.c4query_new2(Db, C4QueryLanguage.JSONQuery,
                    Json5("['AND', ['MATCH()', 'byStreet', 'Hwy'], ['MATCH()', 'byStreet', 'Blvd']]"), null, &error);
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
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                C4Error error;
                _query = Native.c4query_new2(Db, C4QueryLanguage.JSONQuery,
                    Json5("['OR', ['MATCH()', 'byStreet', 'Hwy'],['=', ['.', 'contact', 'address', 'state'], 'CA']]"), null, &error);
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
                Native.c4queryenum_release(e);
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
                Native.c4queryenum_release(e);
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
                Native.c4queryenum_release(e);
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
                Native.c4queryenum_release(e);
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
                Native.c4queryenum_release(e);
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
                Native.c4queryenum_release(e);
            });
        }

        [Fact]
        public void TestQueryParserErrorMessages()
        {
            RunTestVariants(() =>
            {
                C4Error error;
                _query = Native.c4query_new2(Db, C4QueryLanguage.JSONQuery, "[\"=\"]", null, &error);
                ((long) _query).Should().Be(0L);
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int) C4ErrorCode.InvalidQuery);
                Native.c4error_getMessage(error).Should().Be("Wrong number of arguments to =");
            });
        }

        #region Scopes Collections

        /* TODO: query
         * c4coll_getIndexesInfo *
         */

        [Fact]
        public void TestDBQueryExpressionIndexScopesCollections()
        {
            RunTestVariants(() => {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "test", Json5("[['length()', ['.name.first']]]"), C4QueryLanguage.JSONQuery,
                    C4IndexType.ValueIndex, null, err));
                Compile(Json5("['=', ['length()', ['.name.first']], 9]"));
                Run().Should().Equal(new[] { "0000015", "0000099" }, "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestDeleteIndexedDocScopesCollections()
        {
            RunTestVariants(() => {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "test", Json5("[['length()', ['.name.first']]]"), C4QueryLanguage.JSONQuery,
                    C4IndexType.ValueIndex, null, err));

                // Delete doc "0000015":
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    var doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4coll_getDoc(DefaultColl, "0000015", true, C4DocContentLevel.DocGetCurrentRev, err));
                    var rq = new C4DocPutRequest
                    {
                        docID = FLSlice.Constant("0000015"),
                        history = (FLSlice*)&doc->revID,
                        historyCount = 1,
                        revFlags = C4RevisionFlags.Deleted,
                        save = true
                    };
                    var updatedDoc = (C4Document*)LiteCoreBridge.Check(err => {
                        var localRq = rq;
                        return Native.c4coll_putDoc(DefaultColl, &localRq, null, err);
                    });

                    Native.c4doc_release(doc);
                    Native.c4doc_release(updatedDoc);
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                // Now run a query that would have returned the deleted doc, if it weren't deleted:
                Compile(Json5("['=', ['length()', ['.name.first']], 9]"));
                Run().Should().Equal(new[] { "0000099" }, "because otherwise the query returned incorrect results");
            });
        }

        [Fact]
        public void TestFullTextQueryScopesCollections()
        {
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['MATCH()', 'byStreet', 'Hwy']"));

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
        public void TestFullTextMultiplePropertiesScopesCollections()
        {
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byAddress", "[[\".contact.address.street\"],[\".contact.address.city\"],[\".contact.address.state\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['MATCH()', 'byAddress', 'Santa']"));
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

                Compile(Json5("['MATCH()', 'byAddress', 'contact.address.street:Santa']"));
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

                Compile(Json5("['MATCH()', 'byAddress', 'contact.address.street:Santa Saint']"));
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

                Compile(Json5("['MATCH()', 'byAddress', 'contact.address.street:Santa OR Saint']"));
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
        public void TestMultipleFullTextIndexesScopesCollections()
        {
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byCity", "[[\".contact.address.city\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5("['AND', ['MATCH()', 'byStreet', 'Hwy'],['MATCH()', 'byCity', 'Santa']]"));
                var results = RunFTS();
                results.Count.Should().Be(1);
                results[0].Should().Equal(new C4FullTextMatch(15, 0, 0, 11, 3));
            });
        }

        [Fact]
        public void TestFullTextQueryInMultipleAndsScopesCollections()
        {
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byCity", "[[\".contact.address.city\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                Compile(Json5(
                    "['AND', ['AND', ['=', ['.gender'], 'male'],['MATCH()', 'byCity', 'Santa']],['=',['.name.first'], 'Cleveland']]"));
                Run().Should().Equal("0000015");
                var results = RunFTS();
                results.Count.Should().Be(1);
                results[0].Should().Equal(new C4FullTextMatch(15, 0, 0, 0, 5));
            });
        }

        [Fact]
        public void TestMultipleFullTextQueriesScopesCollections()
        {
            // You can't query the same FTS index multiple times in a query (says SQLite)
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                C4Error error;
                _query = Native.c4query_new2(Db, C4QueryLanguage.JSONQuery,
                    Json5("['AND', ['MATCH()', 'byStreet', 'Hwy'], ['MATCH()', 'byStreet', 'Blvd']]"), null, &error);
                ((long)_query).Should().Be(0, "because this type of query is not allowed");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int)C4ErrorCode.InvalidQuery);
                Native.c4error_getMessage(error).Should()
                    .Be("Sorry, multiple MATCHes of the same property are not allowed");
            });
        }

        [Fact]
        public void TestBuriedFullTextQueriesScopesCollections()
        {
            // You can't put an FTS match inside an expression other than a top-level AND (says SQLite)
            RunTestVariants(() =>
            {
                LiteCoreBridge.Check(err => Native.c4coll_createIndex(DefaultColl, "byStreet", "[[\".contact.address.street\"]]", C4QueryLanguage.JSONQuery,
                    C4IndexType.FullTextIndex, null, err));
                C4Error error;
                _query = Native.c4query_new2(Db, C4QueryLanguage.JSONQuery,
                    Json5("['OR', ['MATCH()', 'byStreet', 'Hwy'],['=', ['.', 'contact', 'address', 'state'], 'CA']]"), null, &error);
                ((long)_query).Should().Be(0, "because this type of query is not allowed");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int)C4ErrorCode.InvalidQuery);
                Native.c4error_getMessage(error).Should()
                    .Be("MATCH can only appear at top-level, or in a top-level AND");
            });
        }

        #endregion

        /** Callback invoked by a query observer, notifying that the query results have changed.
         *  The actual enumerator is not passed to the callback, but can be retrieved by calling
         *  \ref c4queryobs_getEnumerator.
         *  @warning  This function is called on a random background thread! Be careful of thread
         *  safety. Do not spend too long in this callback or other observers may be delayed.
         *  It's best to do nothing except schedule a call on your preferred thread/queue.
         *  @param observer  The observer triggering the callback.
         *  @param query  The C4Query that the observer belongs to.
         *  @param context  The `context` parameter you passed to \ref c4queryobs_create. */
#if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4QueryObserverCallback))]
#endif
        private static void QueryObserverCallback(C4QueryObserver* obs, C4Query* query, void* context)
        {
            var obj = GCHandle.FromIntPtr((IntPtr)context).Target as QueryTest;
            obj.QueryObserverCalled(obs, query);
        }

        private void QueryObserverCalled(C4QueryObserver* obs, C4Query* query)
        {
            WriteLine("---- Query observer called!");
            if ((long)obs == (long)_queryObserver)
                _queryCallbackCalls++;
            else if ((long)obs == (long)_queryObserver2)
                _queryCallbackCalls2++;
            else if ((long)obs == (long)_queryObserver3)
                _queryCallbackCalls3++;
        }

        protected override void SetupVariant(int option)
        {
            base.SetupVariant(option);

            _queryCallbackCalls = 0;
            _queryCallbackCalls2 = 0;
            _queryCallbackCalls3 = 0;
        }

        protected override void TeardownVariant(int option)
        {
            /** Stops an observer and frees the resources it's using.
             *  It is safe to pass NULL to this call. */
            Native.c4queryobs_free(_queryObserver);
            _queryObserver = null;
            Native.c4queryobs_free(_queryObserver2);
            _queryObserver2 = null;
            Native.c4queryobs_free(_queryObserver3);
            _queryObserver3 = null;

            base.TeardownVariant(option);
        }
    }
}
#endif