// 
//  DocumentTest.cs
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

#nullable disable
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

using FluentAssertions;
using LiteCore.Interop;
using LiteCore.Util;
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
    public unsafe class DocumentTest : Test
    {
#if !WINDOWS_UWP
        public DocumentTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestInvalidDocID()
        {
            RunTestVariants(() =>
            {
                NativePrivate.c4log_warnOnErrors(false);
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    Action<FLSlice> checkPutBadDocID = (FLSlice docID) =>
                    {
                        C4Error e;
                        var rq = new C4DocPutRequest();
                        rq.body = FleeceBody;
                        rq.save = true;
                        rq.docID = docID;
                        ((long) Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &rq, null, &e)).Should()
                            .Be(0, "because the invalid doc ID should cause the put to fail");
                        e.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                        e.code.Should().Be((int) C4ErrorCode.BadDocID);
                    };

                    checkPutBadDocID(FLSlice.Constant(""));
                    string tooLong = new string(Enumerable.Repeat('x', 241).ToArray());
                    using (var tooLong_ = new C4String(tooLong)) {
                        checkPutBadDocID(tooLong_.AsFLSlice());
                    }

                    checkPutBadDocID(FLSlice.Constant("oops\x00oops")); // Bad UTF-8
                    checkPutBadDocID(FLSlice.Constant("oops\noops")); // Control characters
                } finally {
                    NativePrivate.c4log_warnOnErrors(true);
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }
            });
        }

        #if !CBL_NO_EXTERN_FILES
        [Fact]
        public void TestFleeceDocs()
        {
            RunTestVariants(() => {
                ImportJSONLines("C/tests/data/names_100.json");
            });
        }

        #endif

        [Fact]
        public void TestCreateVersionedDoc()
        {
            RunTestVariants(() => {
                // Try reading doc with mustExist=true, which should fail:
                C4Error error;
                C4Document* doc = NativeRaw.c4coll_getDoc(Native.c4db_getDefaultCollection(Db, null), DocID, true, C4DocContentLevel.DocGetCurrentRev, &error);
                ((long)doc).Should().Be(0, "because the document does not exist");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int)C4ErrorCode.NotFound);
                Native.c4doc_release(doc);

                // Now get the doc with mustExist=false, which returns an empty doc:
                doc = (C4Document *)LiteCoreBridge.Check(err => NativeRaw.c4coll_getDoc(Native.c4db_getDefaultCollection(Db, null), DocID, false, C4DocContentLevel.DocGetCurrentRev, err));
                ((int)doc->flags).Should().Be(0, "because the document is empty");
                doc->docID.Equals(DocID).Should().BeTrue("because the doc ID should match what was stored");
                ((long)doc->revID.buf).Should().Be(0, "because the doc has no revision ID yet");
                ((long)doc->selectedRev.revID.buf).Should().Be(0, "because the doc has no revision ID yet");
                Native.c4doc_release(doc);

                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    var tmp = RevID;
                    var rq = new C4DocPutRequest {
                        existingRevision = true,
                        docID = DocID,
                        history = &tmp,
                        historyCount = 1,
                        body = FleeceBody,
                        save = true
                    };

                    doc = (C4Document *)LiteCoreBridge.Check(err => {
                        var localRq = rq;
                        return Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &localRq, null, err);
                    });
                    doc->revID.Equals(RevID).Should().BeTrue("because the doc should have the stored revID");
                    doc->selectedRev.revID.Equals(RevID).Should().BeTrue("because the doc should have the stored revID");
                    doc->selectedRev.flags.Should().Be(C4RevisionFlags.Leaf, "because this is a leaf revision");
                    NativeRaw.c4doc_getRevisionBody(doc).Equals(FleeceBody).Should().BeTrue("because the body should be stored correctly");
                    Native.c4doc_release(doc);
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                // Reload the doc:
                doc = (C4Document *)LiteCoreBridge.Check(err => NativeRaw.c4coll_getDoc(Native.c4db_getDefaultCollection(Db, null), DocID, true, C4DocContentLevel.DocGetCurrentRev, err));
                doc->flags.Should().Be(C4DocumentFlags.DocExists, "because this is an existing document");
                doc->docID.Equals(DocID).Should().BeTrue("because the doc should have the stored doc ID");
                doc->revID.Equals(RevID).Should().BeTrue("because the doc should have the stored rev ID");
                doc->selectedRev.revID.Equals(RevID).Should().BeTrue("because the doc should have the stored rev ID");
                doc->selectedRev.sequence.Should().Be(1, "because it is the first stored document");
                NativeRaw.c4doc_getRevisionBody(doc).Equals(FleeceBody).Should().BeTrue("because the doc should have the stored body");
                Native.c4doc_release(doc);
            });
        }

        [Fact]
        public void TestPurge()
        {
            RunTestVariants(() => {
                var body2 = JSON2Fleece("{\"ok\":\"go\"}");
                var body3 = JSON2Fleece("{\"ubu\":\"roi\"}");
                CreateRev(DocID.CreateString(), RevID, FleeceBody);
                CreateRev(DocID.CreateString(), Rev2ID, (FLSlice)body2);
                CreateRev(DocID.CreateString(), Rev3ID, (FLSlice)body3);

                var history = new[] { FLSlice.Constant("3-ababab"), Rev2ID };
                fixed(FLSlice* history_ = history) {
                    var rq = new C4DocPutRequest
                    {
                        existingRevision = true,
                        docID = DocID,
                        history = history_,
                        historyCount = 2,
                        allowConflict = true,
                        body = (FLSlice)body3,
                        save = true
                    };

                    C4Error error;
                    C4Document* doc = null;
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        doc = Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &rq, null, &error);
                        ((IntPtr)doc).Should().NotBe(IntPtr.Zero);
                        Native.c4doc_release(doc);
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        LiteCoreBridge.Check(err => NativeRaw.c4coll_purgeDoc(Native.c4db_getDefaultCollection(Db, null), DocID, err));
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    Native.c4coll_getDocumentCount(Native.c4db_getDefaultCollection(Db, null)).Should().Be(0UL);
                    Native.FLSliceResult_Release(body2);
                    Native.FLSliceResult_Release(body3);
                }   
            });
        }

        [Fact]
        public void TestPut()
        {
            RunTestVariants(() => {
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    // Creating doc given ID:
                    var rq = new C4DocPutRequest {
                        docID = DocID,
                        body = FleeceBody,
                        save = true
                    };

                    var doc = (C4Document *)LiteCoreBridge.Check(err => {
                        var localRq = rq;
                        return Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &localRq, null, err);
                    });

                    doc->docID.Equals(DocID).Should().BeTrue("because the doc should have the correct doc ID");
                    var expectedRevID = IsRevTrees(Db) ? FLSlice.Constant("1-042ca1d3a1d16fd5ab2f87efc7ebbf50b7498032") :
                        FLSlice.Constant("1@*");
                    doc->revID.Equals(expectedRevID).Should().BeTrue("because the doc should have the correct rev ID");
                    doc->flags.Should().Be(C4DocumentFlags.DocExists, "because the document exists");
                    doc->selectedRev.revID.Equals(expectedRevID).Should().BeTrue("because the selected rev should have the correct rev ID");
                    Native.c4doc_release(doc);

                    // Update doc:
                    var tmp = new[] { expectedRevID };
                    var body = JSON2Fleece("{\"ok\":\"go\"}");
                    rq.body = (FLSlice)body;
                    rq.historyCount = 1;
                    ulong commonAncestorIndex = 0UL;
                    fixed(FLSlice* history = tmp) {
                        rq.history = history;
                        doc = (C4Document *)LiteCoreBridge.Check(err => {
                            var localRq = rq;
                            UIntPtr cai;
                            var retVal = Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &localRq, &cai, err);
                            commonAncestorIndex = (ulong)cai;
                            return retVal;
                        });
                    }

                    commonAncestorIndex.Should().Be(0UL, "because there are no common ancestors");
                    var expectedRev2ID = IsRevTrees(Db) ? FLSlice.Constant("2-201796aeeaa6ddbb746d6cab141440f23412ac51") : 
                        FLSlice.Constant("2@*");
                    doc->revID.Equals(expectedRev2ID).Should().BeTrue("because the doc should have the updated rev ID");
                    doc->flags.Should().Be(C4DocumentFlags.DocExists, "because the document exists");
                    doc->selectedRev.revID.Equals(expectedRev2ID).Should().BeTrue("because the selected rev should have the correct rev ID");
                    Native.c4doc_release(doc);

                    // Insert existing rev that conflicts:
                    Native.FLSliceResult_Release(body);
                    body = JSON2Fleece("{\"from\":\"elsewhere\"}");
                    rq.body = (FLSlice)body;
                    rq.existingRevision = true;
                    rq.remoteDBID = 1;
                    var conflictRevID = IsRevTrees(Db) ? FLSlice.Constant("2-deadbeef") : FLSlice.Constant("1@binky");
                    tmp = new[] { conflictRevID, expectedRevID };
                    rq.historyCount = 2;
                    rq.allowConflict = true;
                    fixed(FLSlice* history = tmp) {
                        rq.history = history;
                        doc = (C4Document *)LiteCoreBridge.Check(err => {
                            var localRq = rq;
                            UIntPtr cai;
                            var retVal = Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &localRq, &cai, err);
                            commonAncestorIndex = (ulong)cai;
                            return retVal;
                        });
                    }

                    commonAncestorIndex.Should().Be(1UL, "because the common ancestor is at sequence 1");
                    doc->flags.Should().Be(C4DocumentFlags.DocExists|C4DocumentFlags.DocConflicted, "because the document exists");
                    doc->selectedRev.revID.Equals(conflictRevID).Should().BeTrue("because the selected rev should have the correct rev ID");
                    doc->revID.Equals(expectedRev2ID).Should().BeTrue("because the conflicting rev should never be the default");
                    Native.FLSliceResult_Release(body);
                    Native.c4doc_release(doc);
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }
            });
        }

        [Fact]
        public void TestUpdate()
        {
            RunTestVariants(() =>
            {
                WriteLine("Begin test");
                C4Document* doc = null;
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    WriteLine("Begin create");
                    doc = (C4Document*) LiteCoreBridge.Check(err => NativeRaw.c4coll_createDoc(Native.c4db_getDefaultCollection(Db, null), DocID, FleeceBody, 0, err));
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                WriteLine("After save");
                var expectedRevID = IsRevTrees(Db) 
                    ? FLSlice.Constant("1-042ca1d3a1d16fd5ab2f87efc7ebbf50b7498032")
                    : FLSlice.Constant("1@*");

                doc->revID.Equals(expectedRevID).Should().BeTrue();
                doc->flags.Should().Be(C4DocumentFlags.DocExists, "because the document was saved");
                doc->selectedRev.revID.Equals(expectedRevID).Should().BeTrue();
                doc->docID.Equals(DocID).Should().BeTrue("because that is the document ID that it was saved with");

                // Read the doc into another C4Document
                var doc2 = (C4Document*) LiteCoreBridge.Check(err => NativeRaw.c4coll_getDoc(Native.c4db_getDefaultCollection(Db, null), DocID, false, C4DocContentLevel.DocGetAll, err));
                doc->revID.Equals(expectedRevID).Should()
                    .BeTrue("because the other reference should have the same rev ID");

                for (int i = 2; i <= 5; i++)
                {
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try
                    {
                        WriteLine($"Begin save #{i}");
                        var body = JSON2Fleece("{\"ok\":\"go\"}");
                        var oldRevID = doc->revID;
                        var updatedDoc =
                            (C4Document*) LiteCoreBridge.Check(
                                err => NativeRaw.c4doc_update(doc, (FLSlice) body, 0, err));
                        //doc->selectedRev.revID.Equals(oldRevID).Should().BeTrue();
                        //doc->revID.Equals(oldRevID).Should().BeTrue();
                        Native.c4doc_release(doc);
                        doc = updatedDoc;
                        Native.FLSliceResult_Release(body);
                    }
                    finally
                    {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }
                }

                WriteLine("After multiple updates");
                var expectedRev2ID = IsRevTrees(Db) 
                    ? FLSlice.Constant("5-a452899fa8e69b06d936a5034018f6fff0a8f906")
                    : FLSlice.Constant("5@*");
                doc->revID.Equals(expectedRev2ID).Should().BeTrue();
                doc->selectedRev.revID.Equals(expectedRev2ID).Should().BeTrue();

                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    WriteLine("Begin conflicting save");
                    C4Error error;
                    var body = JSON2Fleece("{\"ok\":\"no way\"}");
                    ((long)NativeRaw.c4doc_update(doc2, (FLSlice)body, 0, &error)).Should().Be(0, "because this is a conflict");
                    error.code.Should().Be((int) C4ErrorCode.Conflict);
                    error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                    Native.FLSliceResult_Release(body);
                }
                finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    WriteLine("Begin conflicting create");
                    C4Error error;
                    var body = JSON2Fleece("{\"ok\":\"no way\"}");
                    ((long)NativeRaw.c4coll_createDoc(Native.c4db_getDefaultCollection(Db, null), DocID, (FLSlice)body, 0, &error)).Should().Be(0, "because this is a conflict");
                    error.code.Should().Be((int)C4ErrorCode.Conflict);
                    error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                    Native.FLSliceResult_Release(body);
                }
                finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                Native.c4doc_release(doc);
                Native.c4doc_release(doc2);
            });
        }
    }
}