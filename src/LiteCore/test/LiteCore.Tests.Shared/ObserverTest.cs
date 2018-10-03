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

using Couchbase.Lite.Interop;

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
    public unsafe class ObserverTest : Test
    {
        private static readonly C4DatabaseObserverCallback DatabaseCallback = DBObserverCallback;
        private static readonly C4DocumentObserverCallback DocumentCallback = DocObserverCallback;

        private C4DatabaseObserver* _dbObserver;
        private C4DocumentObserver* _docObserver;

        private int _dbCallbackCalls;

        private int _docCallbackCalls;

        protected override int NumberOfOptions => 1;

#if !WINDOWS_UWP
        public ObserverTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestDBObserver()
        {
            RunTestVariants(() =>
            {
                var handle = GCHandle.Alloc(this);
                try {
                    _dbObserver = Native.c4dbobs_create(Db, DatabaseCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    CreateRev("A", FLSlice.Constant("1-aa"), Body);
                    _dbCallbackCalls.Should().Be(1, "because we should have received a callback");
                    CreateRev("B", FLSlice.Constant("1-bb"), Body);
                    _dbCallbackCalls.Should().Be(1, "because we should have received a callback");

                    CheckChanges(new[] { "A", "B" }, new[] { "1-aa", "1-bb" });

                    CreateRev("B", FLSlice.Constant("2-bbbb"), Body);
                    _dbCallbackCalls.Should().Be(2, "because we should have received a callback");
                    CreateRev("C", FLSlice.Constant("1-cc"), Body);
                    _dbCallbackCalls.Should().Be(2, "because we should have received a callback");

                    CheckChanges(new[] { "B", "C" }, new[] { "2-bbbb", "1-cc" });
                    Native.c4dbobs_free(_dbObserver);
                    _dbObserver = null;

                    CreateRev("A", FLSlice.Constant("2-aaaa"), Body);
                    _dbCallbackCalls.Should().Be(2, "because the observer was disposed");
                } finally {
                    handle.Free();
                }
            });
        }

        [Fact]
        public void TestDocObserver()
        {
            RunTestVariants(() => {
                var handle = GCHandle.Alloc(this);
                try {
                    CreateRev("A", FLSlice.Constant("1-aa"), Body);
                    _docObserver = Native.c4docobs_create(Db, "A", DocumentCallback,
                        GCHandle.ToIntPtr(handle).ToPointer());

                    CreateRev("A", FLSlice.Constant("2-bb"), Body);
                    CreateRev("B", FLSlice.Constant("1-bb"), Body);
                    _docCallbackCalls.Should().Be(1, "because there was only one update to the doc in question");
                } finally {
                    handle.Free();
                }
            });
        }

        [Fact]
        public void TestMultiDbObserver()
        {
            RunTestVariants(() => {
                var handle = GCHandle.Alloc(this);
                try {
                    _dbObserver = Native.c4dbobs_create(Db, DatabaseCallback, GCHandle.ToIntPtr(handle).ToPointer());
                    CreateRev("A", FLSlice.Constant("1-aa"), Body);
                    _dbCallbackCalls.Should().Be(1, "because we should have received a callback");
                    CreateRev("B", FLSlice.Constant("1-bb"), Body);
                    _dbCallbackCalls.Should().Be(1, "because we should have received a callback");

                    CheckChanges(new[] { "A", "B" }, new[] { "1-aa", "1-bb" });

                    // Open another database on the same file
                    var otherdb = (C4Database*) LiteCoreBridge.Check(err =>
                        Native.c4db_open(DatabasePath(), Native.c4db_getConfig(Db), err));
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(otherdb, err));
                    try {
                        CreateRev(otherdb, "C", FLSlice.Constant("1-cc"), Body);
                        CreateRev(otherdb, "D", FLSlice.Constant("1-dd"), Body);
                        CreateRev(otherdb, "E", FLSlice.Constant("1-ee"), Body);
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(otherdb, true, err));
                    }

                    _dbCallbackCalls.Should().Be(2, "because the observer should cover all connections");

                    CheckChanges(new[] { "C", "D", "E" }, new[] { "1-cc", "1-dd", "1-ee" }, true);
                    Native.c4dbobs_free(_dbObserver);
                    _dbObserver = null;

                    CreateRev("A", FLSlice.Constant("2-aaaa"), Body);
                    _dbCallbackCalls.Should().Be(2, "because the observer was disposed");

                    LiteCoreBridge.Check(err => Native.c4db_close(otherdb, err));
                    Native.c4db_free(otherdb);
                } finally {
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
            for(int i = 0; i < changeCount; i++) {
                changes[i].docID.CreateString().Should().Be(expectedDocIDs[i], "because otherwise we have an invalid document ID");
                changes[i].revID.CreateString().Should().Be(expectedRevIDs[i], "because otherwise we have an invalid document revision ID");
            }

            Native.c4dbobs_releaseChanges(changes, changeCount);
            external.Should().Be(expectedExternal, "because otherwise the external parameter was wrong");
        }

        private static void DBObserverCallback(C4DatabaseObserver* obs, void* context)
        {
            var obj = GCHandle.FromIntPtr((IntPtr) context).Target as ObserverTest;
            obj.DbObserverCalled(obs);
        }

        private static void DocObserverCallback(C4DocumentObserver* obs, FLSlice docId, ulong sequence, void* context)
        {
            var obj = GCHandle.FromIntPtr((IntPtr) context).Target as ObserverTest;
            obj.DocObserverCalled(obs, docId.CreateString(), sequence);
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

        protected override void SetupVariant(int option)
        {
            base.SetupVariant(option);

            _dbCallbackCalls = 0;
            _docCallbackCalls = 0;
        }

        protected override void TeardownVariant(int option)
        {
            Native.c4dbobs_free(_dbObserver);
            _dbObserver = null;
            Native.c4docobs_free(_docObserver);
            _docObserver = null;

            base.TeardownVariant(option);
        }
    }
}