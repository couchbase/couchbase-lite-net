// 
//  PerfTest.cs
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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using LiteCore.Interop;
using LiteCore.Util;
#if !WINDOWS_UWP
using LiteCore.Tests.Util;
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
    public unsafe class PerfTest : Test
    {
        private const string JsonFilePath = "../../../C/tests/data/iTunesMusicLibrary.json";

#if !WINDOWS_UWP
        public PerfTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

#if PERFORMANCE_FUTURE

        [Fact]
        [Trait("Slow", "true")]
        public void TestImportGeoBlocks()
        {
            var rng = new Random();
            RunTestVariants(() => {
                var numDocs = ImportJSONLines("../../../C/tests/data/geoblocks.json",
                    TimeSpan.FromSeconds(15), true);
                ReopenDB();
                var st = Stopwatch.StartNew();
                int readNo = 0;
                for( ; readNo < 100000; ++readNo) {
                    var docID = rng.Next(1, (int)numDocs + 1).ToString("D7");
                    var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID,
                        true, err));
                    doc->selectedRev.body.size.Should().BeGreaterThan(10);
                    Native.c4doc_free(doc);
                }

                st.PrintReport("Reading random docs", (uint)readNo, "doc", _output);
            });
        }

        [Fact]
        [Trait("Slow", "true")]
        public void TestImportNames()
        {
            // Docs look like:
            // {"name":{"first":"Travis","last":"Mutchler"},"gender":"female","birthday":"1990-12-21","contact":{"address":{"street":"22 Kansas Cir","zip":"45384","city":"Wilberforce","state":"OH"},"email":["Travis.Mutchler@nosql-matters.org","Travis@nosql-matters.org"],"region":"937","phone":["937-3512486"]},"likes":["travelling"],"memberSince":"2010-01-01"}

            RunTestVariants(() => {
                var numDocs = ImportJSONLines("../../../C/tests/data/names_300000.json",
                    TimeSpan.FromSeconds(15), true);
                var complete = numDocs == 300000;
#if !DEBUG
                numDocs.Should().Be(300000, "because otherwise the operation was too slow");
#endif

                for (int pass = 0; pass < 2; ++pass) {
                    var st = Stopwatch.StartNew();
                    var n = QueryWhere("{\"contact.address.state\": \"WA\"}", true);
                    st.PrintReport("SQL query of state", n, "doc", _output);
                    if(complete) {
                        n.Should().Be(5053, "because that is the number of WA state contact addresses in the document");
                    }

                    if(pass == 0) {
                        var st2 = Stopwatch.StartNew();
                        LiteCoreBridge.Check(err => NativeRaw.c4db_createIndex(Db, C4Slice.Constant("contact.address.state"), C4IndexType.ValueIndex,
                            null, err));
                        st2.PrintReport("Creating SQL index of state", 1, "index", _output);
                    }
                }
            });
        }

        private uint QueryWhere(string whereStr, bool verbose = false)
        {
            var docIDs = new List<string>(1200);

            var query = (C4Query *)LiteCoreBridge.Check(err => Native.c4query_new(Db, whereStr, err));
            var e = (C4QueryEnumerator *)LiteCoreBridge.Check(err => Native.c4query_run(query, null, null, err));
            string artist;
            C4Error error;
            while(Native.c4queryenum_next(e, &error)) {
                Native.FLArrayIterator_GetCount(&e->columns).Should().BeGreaterThan(0);
                artist = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0));
                if(verbose) {
                    Write($"{artist}  ");
                }

                docIDs.Add(artist);
            }

            Native.c4queryenum_free(e);
            Native.c4query_free(query);
            if(verbose) {
                WriteLine();
            }

            return (uint)docIDs.Count;
        }

        private uint InsertDocs(FLArray* docs)
        {
            var typeKey   = NativeRaw.FLDictKey_Init(FLSlice.Constant("Track Type"), true);
            var idKey     = NativeRaw.FLDictKey_Init(FLSlice.Constant("Persistent ID"), true);
            var nameKey   = NativeRaw.FLDictKey_Init(FLSlice.Constant("Name"), true);
            var albumKey  = NativeRaw.FLDictKey_Init(FLSlice.Constant("Album"), true);
            var artistKey = NativeRaw.FLDictKey_Init(FLSlice.Constant("Artist"), true);
            var timeKey   = NativeRaw.FLDictKey_Init(FLSlice.Constant("Total Time"), true);
            var genreKey  = NativeRaw.FLDictKey_Init(FLSlice.Constant("Genre"), true);
            var yearKey   = NativeRaw.FLDictKey_Init(FLSlice.Constant("Year"), true);
            var trackNoKey= NativeRaw.FLDictKey_Init(FLSlice.Constant("Track Number"), true);
            var compKey   = NativeRaw.FLDictKey_Init(FLSlice.Constant("Compilation"), true);

            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
            try {
                var enc = Native.FLEncoder_New();
                FLArrayIterator iter;
                Native.FLArrayIterator_Begin(docs, &iter);
                uint numDocs = 0;
                while(Native.FLArrayIterator_Next(&iter)) {
                    // Check that track is correct type:
                    var track = Native.FLValue_AsDict(Native.FLArrayIterator_GetValue(&iter));
                    var trackType = NativeRaw.FLValue_AsString(Native.FLDict_GetWithKey(track, &typeKey));
                    if(!trackType.Equals(FLSlice.Constant("File")) && !trackType.Equals(FLSlice.Constant("Remote"))) {
                        continue;
                    }

                    var trackID = NativeRaw.FLValue_AsString(Native.FLDict_GetWithKey(track, &idKey));
                    ((long)trackID.buf).Should().NotBe(0, "because otherwise the data was not read correctly");

                    // Encode doc body:
                    Native.FLEncoder_BeginDict(enc, 0);
                    CopyValue(track, &nameKey, enc).Should().BeTrue("because otherwise the copy failed");
                    CopyValue(track, &albumKey, enc);
                    CopyValue(track, &artistKey, enc);
                    CopyValue(track, &timeKey, enc);
                    CopyValue(track, &genreKey, enc);
                    CopyValue(track, &yearKey, enc);
                    CopyValue(track, &trackNoKey, enc);
                    CopyValue(track, &compKey, enc);
                    Native.FLEncoder_EndDict(enc);
                    FLError err;
                    var body = NativeRaw.FLEncoder_Finish(enc, &err);
                    body.Should().NotBeNull("because otherwise the encoding process failed");
                    Native.FLEncoder_Reset(enc);

                    // Save Document:
                    var rq = new C4DocPutRequest {
                        docID = (C4Slice) trackID,
                        body = (C4Slice) body,
                        save = true
                    };
                    var doc = (C4Document *)LiteCoreBridge.Check(c4err => {
                        var localRq = rq;
                        return Native.c4doc_put(Db, &localRq, null, c4err);
                    });
                    
                    Native.c4doc_free(doc);
                    ++numDocs;
                }

                Native.FLEncoder_Free(enc);
                return numDocs;
            } finally {
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
            }
        }

        private static bool CopyValue(FLDict* source, FLDictKey* key, FLEncoder* enc)
        {
            var value = Native.FLDict_GetWithKey(source, key);
            if(value == null) {
                return false;
            }

            Native.FLEncoder_WriteKey(enc, Native.FLDictKey_GetString(key));
            Native.FLEncoder_WriteValue(enc, value);
            return true;
        }
         #endif
    } 
}