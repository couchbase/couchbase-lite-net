// 
//  ObserverTest.cs
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
using System.Runtime.InteropServices;
using System.Threading;
using FluentAssertions;
using LiteCore.Interop;
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
    public unsafe class ObserverTest : QueryTestBase
    {
        private static readonly C4DatabaseObserverCallback DatabaseCallback = DBObserverCallback;
        private static readonly C4DocumentObserverCallback DocumentCallback = DocObserverCallback;
        private static readonly C4QueryObserverCallback QueryCallback = QueryObserverCallback;

        private C4DatabaseObserver* _dbObserver;
        private C4DocumentObserver* _docObserver;
        private C4QueryObserver* _queryObserver;
        private C4QueryObserver* _queryObserver2;
        private C4QueryObserver* _queryObserver3;

        private int _dbCallbackCalls;

        private int _docCallbackCalls;

        private int _queryCallbackCalls;
        private int _queryCallbackCalls2;
        private int _queryCallbackCalls3;

        protected override int NumberOfOptions => 1;

        protected override string JsonPath => "C/tests/data/names_100.json";

#if !WINDOWS_UWP
        public ObserverTest(ITestOutputHelper output) : base(output)
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
                    _queryObserver = Native.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    /** Enables a query observer so its callback can be called, or disables it to stop callbacks. */
                    Native.c4queryobs_setEnabled(_queryObserver, true);

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
                        return Native.c4queryobs_getEnumerator(_queryObserver, true, err);
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
                        return Native.c4queryobs_getEnumerator(_queryObserver, false, err);
                    });
                    //REQUIRE(e2);
                    //CHECK(e2 != e);
                    var e3 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4queryobs_getEnumerator(_queryObserver, false, err);
                    });
                    //CHECK(e3 == e2);
                    Native.c4queryenum_getRowCount(e2, &error).Should().Be(9);

                    // Testing with purged document:
                    WriteLine("---- Purging a document...");
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        Native.c4db_purgeDoc(Db, "after2", &error);
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
                        return Native.c4queryobs_getEnumerator(_queryObserver, true, err);
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
                try
                {
                    Compile(Json5("['=', ['.', 'contact', 'address', 'state'], 'CA']"));
                    C4Error error;

                    _queryObserver = Native.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    //CHECK(_queryObserver);
                    Native.c4queryobs_setEnabled(_queryObserver, true);

                    _queryObserver2 = Native.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    //CHECK(_queryObserver2);
                    Native.c4queryobs_setEnabled(_queryObserver2, true);

                    WriteLine("---- Waiting for query observers...");
                    Thread.Sleep(2000);

                    WriteLine("Checking query observers...");
                    _queryCallbackCalls.Should().Be(1, "because we should have received a callback");
                    var e1 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4queryobs_getEnumerator(_queryObserver, true, err);
                    });
                    //REQUIRE(e1);
                    //CHECK(error.code == 0);
                    Native.c4queryenum_getRowCount(e1, &error).Should().Be(8);
                    _queryCallbackCalls2.Should().Be(1, "because we should have received a callback");
                    var e2 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4queryobs_getEnumerator(_queryObserver2, true, err);
                    });
                    //REQUIRE(e2);
                    //CHECK(error.code == 0);
                    //CHECK(e2 != e1);
                    Native.c4queryenum_getRowCount(e2, &error).Should().Be(8);

                    _queryObserver3 = Native.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    //CHECK(_queryObserver3);
                    Native.c4queryobs_setEnabled(_queryObserver3, true);

                    WriteLine("---- Waiting for a new query observer...");
                    Thread.Sleep(2000);

                    WriteLine("Checking a new query observer...");
                    _queryCallbackCalls3.Should().Be(1, "because we should have received a callback");
                    var e3 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4queryobs_getEnumerator(_queryObserver3, true, err);
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
                try
                {
                    Compile(Json5("['=', ['.', 'contact', 'address', 'state'], ['$state']]"));
                    C4Error error;

                    //As part of this, added c4query_setParameters. Parameter values can now be stored with a query as in the CBL API.
                    //Pass null parameters to c4query_run to use the ones stored in the query.
                    Native.c4query_setParameters(_query, "{\"state\": \"CA\"}");

                    var explain = Native.c4query_explain(_query);
                    //CHECK(explain);
                    WriteLine($"Explain = {explain}");

                    _queryObserver = Native.c4queryobs_create(_query, QueryCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    //CHECK(_queryObserver);
                    Native.c4queryobs_setEnabled(_queryObserver, true);

                    WriteLine("---- Waiting for query observers...");
                    Thread.Sleep(2000);

                    WriteLine("Checking query observers...");
                    _queryCallbackCalls.Should().Be(1, "because we should have received a callback");
                    var e1 = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4queryobs_getEnumerator(_queryObserver, true, err);
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
                        return Native.c4queryobs_getEnumerator(_queryObserver, true, err);
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
        public void TestDBObserver()
        {
            RunTestVariants(() =>
            {
                var handle = GCHandle.Alloc(this);
                try
                {
                    _dbObserver = Native.c4dbobs_create(Db, DatabaseCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    CreateRev("A", FLSlice.Constant("1-aa"), FleeceBody);
                    _dbCallbackCalls.Should().Be(1, "because we should have received a callback");
                    CreateRev("B", FLSlice.Constant("1-bb"), FleeceBody);
                    _dbCallbackCalls.Should().Be(1, "because we should have received a callback");

                    CheckChanges(new[] { "A", "B" }, new[] { "1-aa", "1-bb" });

                    CreateRev("B", FLSlice.Constant("2-bbbb"), FleeceBody);
                    _dbCallbackCalls.Should().Be(2, "because we should have received a callback");
                    CreateRev("C", FLSlice.Constant("1-cc"), FleeceBody);
                    _dbCallbackCalls.Should().Be(2, "because we should have received a callback");

                    CheckChanges(new[] { "B", "C" }, new[] { "2-bbbb", "1-cc" });
                    Native.c4dbobs_free(_dbObserver);
                    _dbObserver = null;

                    CreateRev("A", FLSlice.Constant("2-aaaa"), FleeceBody);
                    _dbCallbackCalls.Should().Be(2, "because the observer was disposed");
                }
                finally
                {
                    handle.Free();
                }
            });
        }

        [Fact]
        public void TestDocObserver()
        {
            RunTestVariants(() =>
            {
                var handle = GCHandle.Alloc(this);
                try {
                    CreateRev("A", FLSlice.Constant("1-aa"), FleeceBody);
                    _docObserver = Native.c4docobs_create(Db, "A", DocumentCallback,
                        GCHandle.ToIntPtr(handle).ToPointer());

                    CreateRev("A", FLSlice.Constant("2-bb"), FleeceBody);
                    CreateRev("B", FLSlice.Constant("1-bb"), FleeceBody);
                    _docCallbackCalls.Should().Be(1, "because there was only one update to the doc in question");
                }
                finally
                {
                    handle.Free();
                }
            });
        }

        [Fact]
        public void TestMultiDbObserver()
        {
            RunTestVariants(() =>
            {
                var handle = GCHandle.Alloc(this);
                try
                {
                    _dbObserver = Native.c4dbobs_create(Db, DatabaseCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    CreateRev("A", FLSlice.Constant("1-aa"), FleeceBody);
                    _dbCallbackCalls.Should().Be(1, "because we should have received a callback");
                    CreateRev("B", FLSlice.Constant("1-bb"), FleeceBody);
                    _dbCallbackCalls.Should().Be(1, "because we should have received a callback");

                    CheckChanges(new[] { "A", "B" }, new[] { "1-aa", "1-bb" });

                    // Open another database on the same file
                    var otherdb = (C4Database*)LiteCoreBridge.Check(err =>
                       Native.c4db_openNamed(DBName, Native.c4db_getConfig2(Db), err));
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(otherdb, err));
                    try {
                        CreateRev(otherdb, "C", FLSlice.Constant("1-cc"), FleeceBody);
                        CreateRev(otherdb, "D", FLSlice.Constant("1-dd"), FleeceBody);
                        CreateRev(otherdb, "E", FLSlice.Constant("1-ee"), FleeceBody);
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(otherdb, true, err));
                    }

                    _dbCallbackCalls.Should().Be(2, "because the observer should cover all connections");

                    CheckChanges(new[] { "C", "D", "E" }, new[] { "1-cc", "1-dd", "1-ee" }, true);
                    Native.c4dbobs_free(_dbObserver);
                    _dbObserver = null;

                    CreateRev("A", FLSlice.Constant("2-aaaa"), FleeceBody);
                    _dbCallbackCalls.Should().Be(2, "because the observer was disposed");

                    LiteCoreBridge.Check(err => Native.c4db_close(otherdb, err));
                    Native.c4db_release(otherdb);
                }
                finally
                {
                    handle.Free();
                }
            });
        }

        private void CheckChanges(IList<string> expectedDocIDs, IList<string> expectedRevIDs, bool expectedExternal = false)
        {
            var changes = new C4DatabaseChange[100];
            bool external;
            var changeCount = Native.c4dbobs_getChanges(_dbObserver, changes, 100, &external);
            changeCount.Should().Be((uint)expectedDocIDs.Count, "because otherwise we didn't get the correct number of changes");
            for (int i = 0; i < changeCount; i++)
            {
                changes[i].docID.CreateString().Should().Be(expectedDocIDs[i], "because otherwise we have an invalid document ID");
                changes[i].revID.CreateString().Should().Be(expectedRevIDs[i], "because otherwise we have an invalid document revision ID");
            }

            Native.c4dbobs_releaseChanges(changes, changeCount);
            external.Should().Be(expectedExternal, "because otherwise the external parameter was wrong");
        }


#if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4DatabaseObserverCallback))]
#endif
        private static void DBObserverCallback(C4DatabaseObserver* obs, void* context)
        {
            var obj = GCHandle.FromIntPtr((IntPtr) context).Target as ObserverTest;
            obj.DbObserverCalled(obs);
        }

#if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4DocumentObserverCallback))]
#endif
        private static void DocObserverCallback(C4DocumentObserver* obs, FLSlice docId, ulong sequence, void* context)
        {
            var obj = GCHandle.FromIntPtr((IntPtr) context).Target as ObserverTest;
            obj.DocObserverCalled(obs, docId.CreateString(), sequence);
        }

        /** Callback invoked by a query observer, notifying that the query results have changed.
        The actual enumerator is not passed to the callback, but can be retrieved by calling
        \ref c4queryobs_getEnumerator.
        @warning  This function is called on a random background thread! Be careful of thread
        safety. Do not spend too long in this callback or other observers may be delayed.
        It's best to do nothing except schedule a call on your preferred thread/queue.
        @param observer  The observer triggering the callback.
        @param query  The C4Query that the observer belongs to.
        @param context  The `context` parameter you passed to \ref c4queryobs_create. */
#if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4QueryObserverCallback))]
#endif
        private static void QueryObserverCallback(C4QueryObserver* obs, C4Query* query, void* context)
        {
            var obj = GCHandle.FromIntPtr((IntPtr)context).Target as ObserverTest;
            obj.QueryObserverCalled(obs, query);
        }

        private void DbObserverCalled(C4DatabaseObserver *obs)
        {
            ((long)obs).Should().Be((long)_dbObserver, "because the callback should be for the proper DB");
            _dbCallbackCalls++;
        }

        private void DocObserverCalled(C4DocumentObserver *obs, string docID, ulong sequence)
        {
            ((long)obs).Should().Be((long)_docObserver, "because the callback should be for the proper DB");
            _docCallbackCalls++;
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

            _dbCallbackCalls = 0;
            _docCallbackCalls = 0;
            _queryCallbackCalls = 0;
            _queryCallbackCalls2 = 0;
            _queryCallbackCalls3 = 0;
        }

        protected override void TeardownVariant(int option)
        {
            Native.c4dbobs_free(_dbObserver);
            _dbObserver = null;
            Native.c4docobs_free(_docObserver);
            _docObserver = null;
            /** Stops an observer and frees the resources it's using.
        It is safe to pass NULL to this call. */
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
