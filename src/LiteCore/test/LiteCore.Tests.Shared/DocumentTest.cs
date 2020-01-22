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
                        ((long) Native.c4doc_put(Db, &rq, null, &e)).Should()
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
        public void TestPossibleAncestors()
        {
            RunTestVariants(() => {
                if(!IsRevTrees()) {
                    return;
                }

                var docID = DocID.CreateString();
                CreateRev(docID, RevID, FleeceBody);
                CreateRev(docID, Rev2ID, FleeceBody);
                CreateRev(docID, Rev3ID, FleeceBody);

                var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                var newRevID = "3-f00f00";
                LiteCoreBridge.Check(err => Native.c4doc_selectFirstPossibleAncestorOf(doc, newRevID));
                doc->selectedRev.revID.Should().Be(Rev2ID, "because the 2nd generation is the first ancestor of the third");
                LiteCoreBridge.Check(err => Native.c4doc_selectNextPossibleAncestorOf(doc, newRevID));
                doc->selectedRev.revID.Should().Be(RevID, "because the first generation comes before the second");
                Native.c4doc_selectNextPossibleAncestorOf(doc, newRevID).Should().BeFalse("because we are at the root");

                newRevID = "2-f00f00";
                LiteCoreBridge.Check(err => Native.c4doc_selectFirstPossibleAncestorOf(doc, newRevID));
                doc->selectedRev.revID.Should().Be(RevID, "because the first generation comes before the second");
                Native.c4doc_selectNextPossibleAncestorOf(doc, newRevID).Should().BeFalse("because we are at the root");

                newRevID = "1-f00f00";
                Native.c4doc_selectFirstPossibleAncestorOf(doc, newRevID).Should().BeFalse("because we are at the root");
                Native.c4doc_release(doc);
            });
        }

        [Fact]
        public void TestCreateVersionedDoc()
        {
            RunTestVariants(() => {
                // Try reading doc with mustExist=true, which should fail:
                C4Error error;
                C4Document* doc = NativeRaw.c4doc_get(Db, DocID, true, &error);
                ((long)doc).Should().Be(0, "because the document does not exist");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int)C4ErrorCode.NotFound);
                Native.c4doc_release(doc);

                // Now get the doc with mustExist=false, which returns an empty doc:
                doc = (C4Document *)LiteCoreBridge.Check(err => NativeRaw.c4doc_get(Db, DocID, false, err));
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
                        return Native.c4doc_put(Db, &localRq, null, err);
                    });
                    doc->revID.Equals(RevID).Should().BeTrue("because the doc should have the stored revID");
                    doc->selectedRev.revID.Equals(RevID).Should().BeTrue("because the doc should have the stored revID");
                    doc->selectedRev.flags.Should().Be(C4RevisionFlags.Leaf, "because this is a leaf revision");
                    doc->selectedRev.body.Equals(FleeceBody).Should().BeTrue("because the body should be stored correctly");
                    Native.c4doc_release(doc);
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                // Reload the doc:
                doc = (C4Document *)LiteCoreBridge.Check(err => NativeRaw.c4doc_get(Db, DocID, true, err));
                doc->flags.Should().Be(C4DocumentFlags.DocExists, "because this is an existing document");
                doc->docID.Equals(DocID).Should().BeTrue("because the doc should have the stored doc ID");
                doc->revID.Equals(RevID).Should().BeTrue("because the doc should have the stored rev ID");
                doc->selectedRev.revID.Equals(RevID).Should().BeTrue("because the doc should have the stored rev ID");
                doc->selectedRev.sequence.Should().Be(1, "because it is the first stored document");
                doc->selectedRev.body.Equals(FleeceBody).Should().BeTrue("because the doc should have the stored body");
                Native.c4doc_release(doc);

                // Get the doc by its sequence
                doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_getBySequence(Db, 1, err));
                doc->flags.Should().Be(C4DocumentFlags.DocExists, "because this is an existing document");
                doc->docID.Equals(DocID).Should().BeTrue("because the doc should have the stored doc ID");
                doc->revID.Equals(RevID).Should().BeTrue("because the doc should have the stored rev ID");
                doc->selectedRev.revID.Equals(RevID).Should().BeTrue("because the doc should have the stored rev ID");
                doc->selectedRev.sequence.Should().Be(1, "because it is the first stored document");
                doc->selectedRev.body.Equals(FleeceBody).Should().BeTrue("because the doc should have the stored body");
                Native.c4doc_release(doc);
            });
        }

        [Fact]
        public void TestCreateMultipleRevisions()
        {
            RunTestVariants(() => {
                var Body2 = JSON2Fleece("{\"ok\":\"go\"}");
                var Body3 = JSON2Fleece("{\"ubu\":\"roi\"}");
                var docID = DocID.CreateString();
                CreateRev(docID, RevID, FleeceBody);
                CreateRev(docID, Rev2ID, (FLSlice)Body2, C4RevisionFlags.KeepBody);
                CreateRev(docID, Rev2ID, (FLSlice)Body2); // test redundant Insert

                // Reload the doc:
                var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                doc->flags.Should().HaveFlag(C4DocumentFlags.DocExists, "because the document was saved");
                doc->docID.Should().Be(DocID, "because the doc ID should save correctly");
                doc->revID.Should().Be(Rev2ID, "because the doc's rev ID should load correctly");
                doc->selectedRev.revID.Should().Be(Rev2ID, "because the rev's rev ID should load correctly");
                doc->selectedRev.sequence.Should().Be(2, "because it is the second revision");
                doc->selectedRev.body.Should().Be(Body2, "because the body should load correctly");

                if(Versioning == C4DocumentVersioning.RevisionTrees) {
                    // Select 1st revision:
                    LiteCoreBridge.Check(err => Native.c4doc_selectParentRevision(doc));
                    doc->selectedRev.revID.Should().Be(RevID, "because now the first revision is selected");
                    doc->selectedRev.sequence.Should().Be(1, "because now the first revision is selected");
                    doc->selectedRev.body.Should().Be(FLSlice.Null, "because the body of the old revision should be gone");
                    Native.c4doc_hasRevisionBody(doc).Should().BeFalse("because the body of the old revision should be gone");
                    Native.c4doc_selectParentRevision(doc).Should().BeFalse("because a root revision has no parent");
                    Native.c4doc_release(doc);

                    // Add a 3rd revision:
                    CreateRev(docID, Rev3ID, (FLSlice)Body3);
                    // Revision 2 should keep its body due to the KeepBody flag
                    doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                    Native.c4doc_selectParentRevision(doc).Should().BeTrue("because otherwise the selection of the 2nd revision failed");
                    doc->selectedRev.revID.Should().Be(Rev2ID, "because the rev's rev ID should load correctly");
                    doc->selectedRev.sequence.Should().Be(2, "because it is the second revision");
                    doc->selectedRev.flags.Should().HaveFlag(C4RevisionFlags.KeepBody, "because the KeepBody flag was saved on the revision");
                    doc->selectedRev.body.Should().Be(Body2, "because the body should load correctly");
                    Native.c4doc_release(doc);

                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                        var nPurged = NativeRaw.c4doc_purgeRevision(doc, Rev3ID, null);
                        nPurged.Should().Be(3, "because there are three revisions to purge");
                        LiteCoreBridge.Check(err => Native.c4doc_save(doc, 20, err));
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                        Native.c4doc_release(doc);
                        doc = null;
                    }
                }

                Native.FLSliceResult_Release(Body2);
                Native.FLSliceResult_Release(Body3);
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
                        doc = Native.c4doc_put(Db, &rq, null, &error);
                        ((IntPtr)doc).Should().NotBe(IntPtr.Zero);
                        Native.c4doc_release(doc);
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        LiteCoreBridge.Check(err => NativeRaw.c4db_purgeDoc(Db, DocID, err));
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    Native.c4db_getDocumentCount(Db).Should().Be(0UL);

                    CreateRev(DocID.CreateString(), RevID, FleeceBody);
                    CreateRev(DocID.CreateString(), Rev2ID, (FLSlice)body2);
                    CreateRev(DocID.CreateString(), Rev3ID, (FLSlice)body3);

                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        doc = Native.c4doc_put(Db, &rq, null, &error);
                        ((IntPtr)doc).Should().NotBe(IntPtr.Zero);
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        LiteCoreBridge.Check(err => Native.c4doc_purgeRevision(doc, null, err));
                        LiteCoreBridge.Check(err => Native.c4doc_save(doc, 0, err));
                    } finally {
                        Native.c4doc_release(doc);
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    Native.c4db_getDocumentCount(Db).Should().Be(0UL);
                    Native.FLSliceResult_Release(body2);
                    Native.FLSliceResult_Release(body3);
                }   
            });
        }

        [Fact]
        public void TestMaxRevTreeDepth()
        {
            RunTestVariants(() =>
            {
                if (IsRevTrees()) {
                    Native.c4db_getMaxRevTreeDepth(Db).Should().Be(20, "because that is the default");
                    Native.c4db_setMaxRevTreeDepth(Db, 30U);
                    Native.c4db_getMaxRevTreeDepth(Db).Should().Be(30);
                    ReopenDB();
                    Native.c4db_getMaxRevTreeDepth(Db).Should().Be(30, "because the value should be persistent");
                }

                const uint NumRevs = 10000;
                var st = Stopwatch.StartNew();
                var doc = (C4Document*) LiteCoreBridge.Check(err => NativeRaw.c4doc_get(Db, DocID, false, err));
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    for (uint i = 0; i < NumRevs; i++) {
                        var rq = new C4DocPutRequest();
                        rq.docID = doc->docID;
                        rq.history = (FLSlice*)&doc->revID;
                        rq.historyCount = 1;
                        rq.body = FleeceBody;
                        rq.save = true;
                        var savedDoc = (C4Document*) LiteCoreBridge.Check(err =>
                        {
                            var localPut = rq;
                            return Native.c4doc_put(Db, &localPut, null, err);
                        });
                        Native.c4doc_release(doc);
                        doc = savedDoc;
                    }
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                st.Stop();
                WriteLine($"Created {NumRevs} revisions in {st.ElapsedMilliseconds} ms");

                uint nRevs = 0;
                Native.c4doc_selectCurrentRevision(doc);
                do {
                    if (IsRevTrees()) {
                        NativeRaw.c4rev_getGeneration(doc->selectedRev.revID).Should()
                            .Be(NumRevs - nRevs, "because the tree should be pruned");
                    }

                    ++nRevs;
                } while (Native.c4doc_selectParentRevision(doc));

                WriteLine($"Document rev tree depth is {nRevs}");
                if (IsRevTrees()) {
                    nRevs.Should().Be(30, "because the tree should be pruned");
                }

                Native.c4doc_release(doc);
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
                        return Native.c4doc_put(Db, &localRq, null, err);
                    });

                    doc->docID.Equals(DocID).Should().BeTrue("because the doc should have the correct doc ID");
                    var expectedRevID = IsRevTrees() ? FLSlice.Constant("1-042ca1d3a1d16fd5ab2f87efc7ebbf50b7498032") :
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
                            ulong cai;
                            var retVal = Native.c4doc_put(Db, &localRq, &cai, err);
                            commonAncestorIndex = cai;
                            return retVal;
                        });
                    }

                    commonAncestorIndex.Should().Be(0UL, "because there are no common ancestors");
                    var expectedRev2ID = IsRevTrees() ? FLSlice.Constant("2-201796aeeaa6ddbb746d6cab141440f23412ac51") :
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
                    var conflictRevID = IsRevTrees() ? FLSlice.Constant("2-deadbeef") : FLSlice.Constant("1@binky");
                    tmp = new[] { conflictRevID, expectedRevID };
                    rq.historyCount = 2;
                    rq.allowConflict = true;
                    fixed(FLSlice* history = tmp) {
                        rq.history = history;
                        doc = (C4Document *)LiteCoreBridge.Check(err => {
                            var localRq = rq;
                            ulong cai;
                            var retVal = Native.c4doc_put(Db, &localRq, &cai, err);
                            commonAncestorIndex = cai;
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
                    doc = (C4Document*) LiteCoreBridge.Check(err => NativeRaw.c4doc_create(Db, DocID, FleeceBody, 0, err));
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                WriteLine("After save");
                var expectedRevID = IsRevTrees()
                    ? FLSlice.Constant("1-042ca1d3a1d16fd5ab2f87efc7ebbf50b7498032")
                    : FLSlice.Constant("1@*");

                doc->revID.Equals(expectedRevID).Should().BeTrue();
                doc->flags.Should().Be(C4DocumentFlags.DocExists, "because the document was saved");
                doc->selectedRev.revID.Equals(expectedRevID).Should().BeTrue();
                doc->docID.Equals(DocID).Should().BeTrue("because that is the document ID that it was saved with");

                // Read the doc into another C4Document
                var doc2 = (C4Document*) LiteCoreBridge.Check(err => NativeRaw.c4doc_get(Db, DocID, false, err));
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
                        doc->selectedRev.revID.Equals(oldRevID).Should().BeTrue();
                        doc->revID.Equals(oldRevID).Should().BeTrue();
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
                var expectedRev2ID = IsRevTrees()
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
                    ((long)NativeRaw.c4doc_create(Db, DocID, (FLSlice)body, 0, &error)).Should().Be(0, "because this is a conflict");
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

        [Fact]
        public void TestConflict()
        {
            RunTestVariants(() =>
            {
                if(!IsRevTrees()) {
                    return;
                }

                var body2 = JSON2Fleece("{\"ok\":\"go\"}");
                var body3 = JSON2Fleece("{\"ubu\":\"roi\"}");
                CreateRev(DocID.CreateString(), RevID, FleeceBody);
                CreateRev(DocID.CreateString(), Rev2ID, (FLSlice)body2, C4RevisionFlags.KeepBody);
                CreateRev(DocID.CreateString(), FLSlice.Constant("3-aaaaaa"), (FLSlice)body3);

                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    // "Pull" a conflicting revision:
                    var history = new FLSlice[] { FLSlice.Constant("4-dddd"), FLSlice.Constant("3-ababab"), Rev2ID };
                    fixed(FLSlice* history_ = history) {
                        var rq = new C4DocPutRequest
                        {
                            existingRevision = true,
                            docID = DocID,
                            history = history_,
                            historyCount = 3,
                            allowConflict = true,
                            body = (FLSlice)body3,
                            save = true,
                            remoteDBID = 1
                        };

                        C4Error error;
                        var doc = Native.c4doc_put(Db, &rq, null, &error);
                        ((IntPtr)doc).Should().NotBe(IntPtr.Zero);

                        Native.FLSliceResult_Release(body2);
                        Native.FLSliceResult_Release(body3);

                        Native.c4doc_selectCommonAncestorRevision(doc, "3-aaaaaa", "4-dddd").Should().BeTrue();
                        doc->selectedRev.revID.CreateString().Should().Be(Rev2ID.CreateString());
                        Native.c4doc_selectCommonAncestorRevision(doc, "4-dddd", "3-aaaaaa").Should().BeTrue();
                        doc->selectedRev.revID.CreateString().Should().Be(Rev2ID.CreateString());

                        Native.c4doc_selectCommonAncestorRevision(doc, "3-ababab", "3-aaaaaa").Should().BeTrue();
                        doc->selectedRev.revID.CreateString().Should().Be(Rev2ID.CreateString());
                        Native.c4doc_selectCommonAncestorRevision(doc, "3-aaaaaa", "3-ababab").Should().BeTrue();
                        doc->selectedRev.revID.CreateString().Should().Be(Rev2ID.CreateString());

                        Native.c4doc_selectCommonAncestorRevision(doc, Rev2ID.CreateString(), "3-aaaaaa").Should().BeTrue();
                        doc->selectedRev.revID.CreateString().Should().Be(Rev2ID.CreateString());
                        Native.c4doc_selectCommonAncestorRevision(doc, "3-aaaaaa", Rev2ID.CreateString()).Should().BeTrue();
                        doc->selectedRev.revID.CreateString().Should().Be(Rev2ID.CreateString());

                        NativeRaw.c4doc_selectCommonAncestorRevision(doc, Rev2ID, Rev2ID).Should().BeTrue();
                        doc->selectedRev.revID.CreateString().Should().Be(Rev2ID.CreateString());
                    }
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                var mergedBody = JSON2Fleece("{\"merged\":true}");
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                     var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, DocID.CreateString(), true, err));
                     LiteCoreBridge.Check(err => NativeRaw.c4doc_resolveConflict(doc, FLSlice.Constant("4-dddd"), FLSlice.Constant("3-aaaaaa"), (FLSlice)mergedBody, 0, err));
                     Native.c4doc_selectCurrentRevision(doc);
                     doc->selectedRev.revID.CreateString().Should().Be("5-79b2ecd897d65887a18c46cc39db6f0a3f7b38c4");
                     doc->selectedRev.body.Equals(mergedBody).Should().BeTrue();
                     Native.c4doc_selectParentRevision(doc);
                     doc->selectedRev.revID.CreateString().Should().Be("4-dddd");
                } finally {
                     LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, false, err));
                }

                 LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                 try {
                     var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, DocID.CreateString(), true, err));
                     LiteCoreBridge.Check(err => NativeRaw.c4doc_resolveConflict(doc, FLSlice.Constant("3-aaaaaa"), FLSlice.Constant("4-dddd"), (FLSlice)mergedBody, 0, err));
                     Native.c4doc_selectCurrentRevision(doc);
                     doc->selectedRev.revID.CreateString().Should().Be("4-1fa2dbcb66b5e0456f6d6fc4a90918d42f3dd302");
                     doc->selectedRev.body.Equals(mergedBody).Should().BeTrue();
                     Native.c4doc_selectParentRevision(doc);
                     doc->selectedRev.revID.CreateString().Should().Be("3-aaaaaa");
                 } finally {
                     LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, false, err));
                 }
            });
        }

        [Fact]
        public void TestLegacyProperties()
        {
            RunTestVariants(() =>
            {
                Native.c4doc_isOldMetaProperty("_attachments").Should().BeTrue();
                Native.c4doc_isOldMetaProperty("@type").Should().BeFalse();

                var enc = Native.c4db_getSharedFleeceEncoder(Db);
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    Native.FLEncoder_BeginDict(enc, 2);
                    Native.FLEncoder_WriteKey(enc, "@type");
                    Native.FLEncoder_WriteString(enc, "blob");
                    Native.FLEncoder_WriteKey(enc, "digest");
                    Native.FLEncoder_WriteString(enc, String.Empty);
                    Native.FLEncoder_EndDict(enc);
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                var result = Native.FLEncoder_FinishDoc(enc, null);
                try {
                    ((long) result).Should().NotBe(0);
                    ((long) Native.FLDoc_GetSharedKeys(result)).Should().NotBe(0);
                    var val = Native.FLDoc_GetRoot(result);
                    var d = Native.FLValue_AsDict(val);
                    ((long) d).Should().NotBe(0);

                    var testKey = Native.FLDictKey_Init("@type");
                    var testVal = Native.FLDict_GetWithKey(d, &testKey);

                    Native.FLValue_AsString(testVal).Should().Be("blob");
                    ((long)Native.FLValue_FindDoc((FLValue *)d)).Should().Be((long)result);
                    Native.c4doc_dictContainsBlobs(d).Should().BeTrue();
                } finally {
                    Native.FLDoc_Release(result);
                }

                enc = Native.c4db_getSharedFleeceEncoder(Db);
                Native.FLEncoder_BeginDict(enc, 0);
                Native.FLEncoder_EndDict(enc);
                result = Native.FLEncoder_FinishDoc(enc, null);
                try {
                    ((long) result).Should().NotBe(0);
                    var val = Native.FLDoc_GetRoot(result);
                    var d = Native.FLValue_AsDict(val);
                    Native.c4doc_dictContainsBlobs(d).Should().BeFalse();
                } finally {
                    Native.FLDoc_Release(result);
                }
            });
        }
    }
}