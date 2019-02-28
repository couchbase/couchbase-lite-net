// 
// DatabaseInternalTest.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Couchbase.Lite.Interop;

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
    // These tests are repurposes from the CBL 1.x test suite
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public unsafe class DatabaseInternalTest : Test
    {
        private uint _remoteDocID;

#if !WINDOWS_UWP
        public DatabaseInternalTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        private FLSliceResult EncodeBodyIfJSON(FLSlice body)
        {
            var contents = (byte*) body.buf;
            if (contents != null && contents[0] == '{' && contents[(int)body.size - 1] == '}') {
                return JSON2Fleece(body);
            }

            return (FLSliceResult) FLSlice.Null;
        }

        private C4Document* PutDoc(string docID, string revID, FLSlice body, C4RevisionFlags flags = 0)
        {
            return PutDoc(Db, docID, revID, body, flags);
        }

        private C4Document* PutDoc(C4Database* db, string docID, string revID, FLSlice body, C4RevisionFlags flags, C4Error* error = null)
        {
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(db, err));
            var success = false;
            try {
                var encoded = EncodeBodyIfJSON(body);
                using (var docID_ = new C4String(docID))
                using (var revID_ = new C4String(revID)) {
                    var history = new FLSlice[] { revID_.AsFLSlice() };
                    fixed (FLSlice* history_ = history) {
                        var rq = new C4DocPutRequest
                        {
                            allowConflict = false,
                            docID = docID_.AsFLSlice(),
                            history = revID == null ? null : history_,
                            historyCount = revID == null ? 0UL : 1UL,
                            body = encoded.buf == null ? body : (FLSlice)encoded,
                            revFlags = flags,
                            remoteDBID = _remoteDocID,
                            save = true
                        };

                        C4Document* doc;
                        if (error != null) {
                            var local = rq;
                            doc = Native.c4doc_put(db, &local, null, error);
                        } else {
                            doc = (C4Document*)LiteCoreBridge.Check(err =>
                           {
                               var local = rq;
                               return Native.c4doc_put(db, &local, null, err);
                           });
                        }
                        
                        Native.FLSliceResult_Release(encoded);
                        success = true;
                        return doc;
                    }
                }
            } finally {
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(db, success, err));
            }
        }

        private void PutDocMustFail(string docID, string revID, FLSlice body, C4RevisionFlags flags, C4Error expected)
        {
            PutDocMustFail(Db, docID, revID, body, flags, expected);
        }

        private void PutDocMustFail(C4Database* db, string docID, string revID, FLSlice body, C4RevisionFlags flags, C4Error expected)
        {
            C4Error error;
            var doc = PutDoc(db, docID, revID, body, flags, &error);
            ((IntPtr)doc).Should().Be(IntPtr.Zero, "because the put operation was expected to fail");
            WriteLine($"Error: {Native.c4error_getMessage(error)}");
            error.domain.Should().Be(expected.domain);
            error.code.Should().Be(expected.code);
        }

        private void ForceInsert(string docID, string[] history, FLSlice body, C4RevisionFlags flags = 0)
        {
            var doc = ForceInsert(Db, docID, history, body, flags);
            Native.c4doc_free(doc);
        }

        private C4Document* ForceInsert(C4Database* db, string docID, string[] history, FLSlice body, C4RevisionFlags flags, C4Error* error = null)
        {
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(db, err));
            var c4History = new C4String[history.Length];
            var success = false;
            try {
                var i = 0;
                var sliceHistory = new FLSlice[history.Length];
                foreach(var entry in history) {
                    var c4Str = new C4String(entry);
                    c4History[i] = c4Str;
                    sliceHistory[i++] = c4Str.AsFLSlice();
                }

                using (var docID_ = new C4String(docID)) {
                    fixed (FLSlice* sliceHistory_ = sliceHistory) {
                        var rq = new C4DocPutRequest
                        {
                            docID = docID_.AsFLSlice(),
                            existingRevision = true,
                            allowConflict = true,
                            history = sliceHistory_,
                            historyCount = (ulong)history.Length,
                            body = body,
                            revFlags = flags,
                            remoteDBID = _remoteDocID,
                            save = true
                        };

                        C4Document* doc;
                        if(error != null) {
                            var local = rq;
                            doc = Native.c4doc_put(db, &local, null, error);
                        } else {
                            doc = (C4Document*)LiteCoreBridge.Check(err =>
                           {
                               var local = rq;
                               return Native.c4doc_put(db, &local, null, err);
                           });
                        }

                        success = true;
                        return doc;
                    }
                }
            } finally {
                foreach(var entry in c4History) {
                    entry.Dispose();
                }

                LiteCoreBridge.Check(err => Native.c4db_endTransaction(db, success, err));
            }
        }

        private C4Document* GetDoc(string docID)
        {
            return GetDoc(Db, docID);
        }

        private C4Document* GetDoc(C4Database* db, string docID)
        {
            var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(db, docID, true, err));
            doc->docID.CreateString().Should().Be(docID);
            return doc;
        }

        private void VerifyRev(C4Document* doc, string[] history, FLSlice body)
        {
            doc->revID.CreateString().Should().Be(history[0]);
            doc->selectedRev.revID.CreateString().Should().Be(history[0]);
            Native.FLSlice_Equal(doc->selectedRev.body, body).Should().BeTrue();

            var revs = GetAllParentRevisions(doc);
            revs.Count.Should().Be(history.Length);
            revs.SequenceEqual(history).Should().BeTrue();
        }

        private List<string> GetAllParentRevisions(C4Document* doc)
        {
            var history = new List<string>();
            do {
                history.Add(doc->selectedRev.revID.CreateString());
            } while (Native.c4doc_selectParentRevision(doc));

            return history;
        }

        private List<string> GetRevisionHistory(C4Document* doc, bool onlyCurrent, bool includeDeleted)
        {
            var history = new List<string>();
            do {
                if (onlyCurrent && !doc->selectedRev.flags.HasFlag(C4RevisionFlags.Leaf)) {
                    continue;
                }

                if (!includeDeleted && doc->selectedRev.flags.HasFlag(C4RevisionFlags.Deleted)) {
                    continue;
                }

                history.Add(doc->selectedRev.revID.CreateString());
            } while (Native.c4doc_selectNextRevision(doc));

            return history;
        }

        [Fact]
        public void TestCRUD()
        {
            RunTestVariants(() =>
            {
                if(!IsRevTrees()) {
                    return;
                }

                var body = JSON2Fleece("{'foo':1, 'bar':false}");
                var updatedBody = JSON2Fleece("{'foo':1, 'bar':false, 'status': 'updated!'}");

                // TODO: Observer

                C4Error error;
                var doc = Native.c4doc_get(Db, "nonexistent", true, &error);
                ((IntPtr)doc).Should().Be(IntPtr.Zero, "because it does not exist");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int)C4ErrorCode.NotFound);

                // KeepBody => Revision's body should not be discarded when non-leaf
                doc = PutDoc(null, null, (FLSlice)body, C4RevisionFlags.KeepBody);
                doc->docID.size.Should().BeGreaterOrEqualTo(10, "because otherwise no docID was created");

                var docID = doc->docID.CreateString();
                var revID1 = doc->revID.CreateString();
                revID1.Should().StartWith("1-", "because otherwise the generation is invalid");
                Native.c4doc_free(doc);

                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                doc->docID.CreateString().Should().Be(docID);
                doc->selectedRev.revID.CreateString().Should().Be(revID1);
                Native.FLSlice_Equal(doc->selectedRev.body, (FLSlice)body).Should().BeTrue();
                Native.c4doc_free(doc);

                doc = PutDoc(docID, revID1, (FLSlice)updatedBody, C4RevisionFlags.KeepBody);
                doc->docID.CreateString().Should().Be(docID);
                Native.FLSlice_Equal(doc->selectedRev.body, (FLSlice)updatedBody).Should().BeTrue();
                var revID2 = doc->revID.CreateString();
                revID2.Should().StartWith("2-", "because otherwise the generation is invalid");
                Native.c4doc_free(doc);
                Native.FLSliceResult_Release(body);

                error = new C4Error(C4ErrorCode.Conflict);
                PutDocMustFail(docID, revID1, (FLSlice)updatedBody, C4RevisionFlags.KeepBody, error);

                var e = (C4DocEnumerator *)LiteCoreBridge.Check(err =>
                {
                    var options = C4EnumeratorOptions.Default;
                    return Native.c4db_enumerateChanges(Db, 0, &options, err);
                });

                var seq = 2UL;
                while(null != (doc = c4enum_nextDocument(e, &error))) {
                    doc->selectedRev.sequence.Should().Be(seq);
                    doc->selectedRev.revID.CreateString().Should().Be(revID2);
                    doc->docID.CreateString().Should().Be(docID);
                    Native.c4doc_free(doc);
                    seq++;
                }

                seq.Should().Be(3UL);
                Native.c4enum_free(e);
                Native.FLSliceResult_Release(updatedBody);

                // NOTE: Filter is out of LiteCore scope

                error = new C4Error(C4ErrorCode.InvalidParameter);
                PutDocMustFail(docID, null, FLSlice.Null, C4RevisionFlags.Deleted, error);

                doc = PutDoc(docID, revID2, FLSlice.Null, C4RevisionFlags.Deleted);
                doc->flags.Should().Be(C4DocumentFlags.DocExists | C4DocumentFlags.DocDeleted);
                doc->docID.CreateString().Should().Be(docID);
                var revID3 = doc->revID.CreateString();
                revID3.Should().StartWith("3-", "because otherwise the generation is invalid");
                Native.c4doc_free(doc);

                doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                doc->docID.CreateString().Should().Be(docID);
                doc->revID.CreateString().Should().Be(revID3);
                doc->flags.Should().Be(C4DocumentFlags.DocExists | C4DocumentFlags.DocDeleted);
                doc->selectedRev.revID.CreateString().Should().Be(revID3);
                doc->selectedRev.body.CreateString().Should().NotBeNull("because a valid revision should have a valid body");
                doc->selectedRev.flags.Should().Be(C4RevisionFlags.Leaf | C4RevisionFlags.Deleted);
                Native.c4doc_free(doc);

                PutDocMustFail("fake", null, FLSlice.Null, C4RevisionFlags.Deleted, error);

                e = (C4DocEnumerator*)LiteCoreBridge.Check(err =>
               {
                   var options = C4EnumeratorOptions.Default;
                   return Native.c4db_enumerateChanges(Db, 0, &options, err);
               });

                seq = 3UL;
                while(null != (doc = c4enum_nextDocument(e, &error))) {
                    Native.c4doc_free(doc);
                    seq++;
                }

                seq.Should().Be(3UL, "because deleted documents were not included");
                Native.c4enum_free(e);

                e = (C4DocEnumerator*)LiteCoreBridge.Check(err =>
                {
                    var options = C4EnumeratorOptions.Default;
                    options.flags |= C4EnumeratorFlags.IncludeDeleted;
                    return Native.c4db_enumerateChanges(Db, 0, &options, err);
                });

                seq = 3UL;
                while(null != (doc = c4enum_nextDocument(e, &error))) {
                    doc->selectedRev.sequence.Should().Be(seq);
                    doc->selectedRev.revID.CreateString().Should().Be(revID3);
                    doc->docID.CreateString().Should().Be(docID);
                    Native.c4doc_free(doc);
                    seq++;
                }

                seq.Should().Be(4UL, "because deleted documents were included");
                Native.c4enum_free(e);

                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                var latest = 3;
                do {
                    switch (latest) {
                        case 3:
                            doc->selectedRev.revID.CreateString().Should().Be(revID3);
                            break;
                        case 2:
                            doc->selectedRev.revID.CreateString().Should().Be(revID2);
                            break;
                        case 1:
                            doc->selectedRev.revID.CreateString().Should().Be(revID1);
                            break;
                        default:
                            throw new InvalidOperationException("Invalid switch portion reached");
                    }

                    latest--;
                } while (Native.c4doc_selectParentRevision(doc));

                latest.Should().Be(0, "because otherwise the history is not valid");
                Native.c4doc_free(doc);

                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                LiteCoreBridge.Check(err => Native.c4doc_selectRevision(doc, revID2, true, err));
                doc->selectedRev.revID.CreateString().Should().Be(revID2);
                Native.c4doc_free(doc);

                LiteCoreBridge.Check(err => Native.c4db_compact(Db, err));
                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                LiteCoreBridge.Check(err => Native.c4doc_selectRevision(doc, revID2, true, err));
                doc->selectedRev.revID.CreateString().Should().Be(revID2);
                // doc->selectedRev.body.CreateString().Should().BeNull("because the database was compacted");
                Native.c4doc_free(doc);

                // Check history again after compaction
                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                latest = 3;
                do {
                    switch (latest) {
                        case 3:
                            doc->selectedRev.revID.CreateString().Should().Be(revID3);
                            break;
                        case 2:
                            doc->selectedRev.revID.CreateString().Should().Be(revID2);
                            break;
                        case 1:
                            doc->selectedRev.revID.CreateString().Should().Be(revID1);
                            break;
                        default:
                            throw new InvalidOperationException("Invalid switch portion reached");
                    }

                    latest--;
                } while (Native.c4doc_selectParentRevision(doc));

                latest.Should().Be(0, "because otherwise the history is not valid");
                Native.c4doc_free(doc);
            });
        }

        [Fact]
        public void TestEmptyDoc()
        {
            RunTestVariants(() =>
            {
                if(!IsRevTrees()) {
                    return;
                }

                var doc = PutDoc(null, null, FLSlice.Constant("{}"));
                var docID = doc->docID.CreateString();
                Native.c4doc_free(doc);
                var e = (C4DocEnumerator*)LiteCoreBridge.Check(err =>
                {
                    var options = C4EnumeratorOptions.Default;
                    return Native.c4db_enumerateAllDocs(Db, &options, err);
                });

                var seq = 1UL;
                C4Error error;
                while(null != (doc = c4enum_nextDocument(e, &error))) {
                    doc->selectedRev.sequence.Should().Be(seq);
                    doc->docID.CreateString().Should().Be(docID);
                    Native.c4doc_free(doc);
                    seq++;
                }

                seq.Should().Be(2UL);
                Native.c4enum_free(e);
            });
        }

        [Fact]
        public void TestExpectedRevIDs()
        {
            // It's not strictly required that revisions alwauys generate the same revIDs, but it helps
            // prevent false conflicts when two peers make the same change to the same parent revision.
            RunTestVariants(() =>
            {
                if(!IsRevTrees()) {
                    return;
                }

                var body = JSON2Fleece("{'property':'value'}");
                var doc = PutDoc("doc", null, (FLSlice)body);
                Native.FLSliceResult_Release(body);
                var docID = doc->docID.CreateString();
                var revID1 = doc->revID.CreateString();
                revID1.Should().Be("1-d65a07abdb5c012a1bd37e11eef1d0aca3fa2a90");
                Native.c4doc_free(doc);

                body = JSON2Fleece("{'property':'newvalue'}");
                doc = PutDoc("doc", revID1, (FLSlice)body);
                var revID2 = doc->revID.CreateString();
                revID2.Should().Be("2-eaaa643f551df08eb0c60f87f3f011ac4355f834");
                Native.c4doc_free(doc);

                doc = PutDoc("doc", revID2, FLSlice.Null, C4RevisionFlags.Deleted);
                doc->revID.CreateString().Should().Be("3-3ae8fab29af3a5bfbfa5a4c5fd91c58214cb0c5a");
                Native.c4doc_free(doc);
            });
        }

        [Fact]
        public void TestDeleteWithProperties()
        {
            // Test that it's possible to delete a document by PUTting a revision with _deleted=true,
            // and that the saved deleted revision will preserve any extra properties
            RunTestVariants(() =>
            {
                if(!IsRevTrees()) {
                    return;
                }

                var body1 = JSON2Fleece("{'property':'newvalue'}");
                var doc = PutDoc(null, null, (FLSlice)body1);
                Native.FLSliceResult_Release(body1);
                var docID = doc->docID.CreateString();
                var revID1 = doc->revID.CreateString();
                Native.c4doc_free(doc);

                var body2 = JSON2Fleece("{'property':'newvalue'}");
                doc = PutDoc(docID, revID1, (FLSlice)body2, C4RevisionFlags.Deleted);
                var revID2 = doc->revID.CreateString();
                Native.c4doc_free(doc);

                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                LiteCoreBridge.Check(err => Native.c4doc_selectRevision(doc, revID2, true, err));
                doc->flags.Should().Be(C4DocumentFlags.DocExists | C4DocumentFlags.DocDeleted);
                doc->selectedRev.flags.Should().Be(C4RevisionFlags.Leaf | C4RevisionFlags.Deleted);
                Native.FLSlice_Equal(doc->selectedRev.body, (FLSlice) body2).Should().BeTrue();
                Native.c4doc_free(doc);

                doc = PutDoc(docID, null, (FLSlice)body2);
                var revID3 = doc->revID.CreateString();
                revID3.Should().StartWith("3-", "because even though it was created as 'new', it is actually on top of a previous delete and its generation should reflect that");
                Native.c4doc_free(doc);
                Native.FLSliceResult_Release(body2);

                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db, docID, true, err));
                doc->revID.CreateString().Should().Be(revID3);
                Native.c4doc_free(doc);
            });
        }

        [Fact]
        public void TestDeleteAndRecreate()
        {
            RunTestVariants(() =>
            {
                if(!IsRevTrees()) {
                    return;
                }

                var body = JSON2Fleece("{'property':'value'}");
                var doc = PutDoc("dock", null, (FLSlice)body);
                var revID1 = doc->revID.CreateString();
                revID1.Should().StartWith("1-", "because otherwise the generation is incorrect");
                Native.FLSlice_Equal(doc->selectedRev.body, (FLSlice) body).Should().BeTrue();
                Native.c4doc_free(doc);

                doc = PutDoc("dock", revID1, FLSlice.Null, C4RevisionFlags.Deleted);
                var revID2 = doc->revID.CreateString();
                revID2.Should().StartWith("2-", "because otherwise the generation is incorrect");
                doc->selectedRev.flags.Should().Be(C4RevisionFlags.Leaf | C4RevisionFlags.Deleted);
                doc->selectedRev.body.CreateString().Should().NotBeNull("because a valid revision should not have a null body");
                Native.c4doc_free(doc);

                doc = PutDoc("dock", revID2, (FLSlice)body);
                doc->revID.CreateString().Should().StartWith("3-", "because otherwise the generation is incorrect");
                Native.FLSlice_Equal(doc->selectedRev.body, (FLSlice) body).Should().BeTrue();
                Native.c4doc_free(doc);
                Native.FLSliceResult_Release(body);
            });
        }

        [Fact]
        public void TestRevTree()
        {
            RunTestVariants(() =>
            {
                if(!IsRevTrees()) {
                    return;
                }

                // TODO: Observer

                var docID = "MyDocID";
                var body = JSON2Fleece("{'message':'hi'}");
                var history = new[] { "4-4444", "3-3333", "2-2222", "1-1111" };
                ForceInsert(docID, history, (FLSlice)body);

                Native.c4db_getDocumentCount(Db).Should().Be(1UL);

                var doc = GetDoc(docID);
                VerifyRev(doc, history, (FLSlice)body);
                Native.c4doc_free(doc);

                var lastSeq = Native.c4db_getLastSequence(Db);
                ForceInsert(docID, history, (FLSlice)body);
                Native.c4db_getLastSequence(Db).Should().Be(lastSeq, "because the last operation should have been a no-op");

                _remoteDocID = 1;
                var conflictHistory = new[] { "5-5555", "4-4545", "3-3030", "2-2222", "1-1111" };
                var conflictBody = JSON2Fleece("{'message':'yo'}");
                ForceInsert(docID, conflictHistory, (FLSlice)conflictBody);
                _remoteDocID = 0;

                // Conflicts are a bit different than CBL 1.x here.  A conflicted revision is marked with the Conflict flag,
                // and such revisions can never be current.  So in other words, the oldest revision always wins the conflict;
                // it has nothing to do with revIDs
                Native.c4db_getDocumentCount(Db).Should().Be(1UL);
                doc = GetDoc(docID);
                VerifyRev(doc, history, (FLSlice)body);
                Native.c4doc_free(doc);

                // TODO: Conflict check

                var otherDocID = "AnotherDocID";
                var otherBody = JSON2Fleece("{'language':'jp'}");
                var otherHistory = new[] { "1-1010" };
                ForceInsert(otherDocID, otherHistory, (FLSlice)otherBody);

                doc = GetDoc(docID);
                LiteCoreBridge.Check(err => Native.c4doc_selectRevision(doc, "2-2222", false, err));
                doc->selectedRev.flags.Should().NotHaveFlag(C4RevisionFlags.KeepBody);
                doc->selectedRev.body.CreateString().Should().BeNull();
                Native.c4doc_free(doc);

                doc = GetDoc(otherDocID);
                C4Error error;
                Native.c4doc_selectRevision(doc, "666-6666", false, &error).Should().BeFalse();
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int)C4ErrorCode.NotFound);
                Native.c4doc_free(doc);

                Native.c4db_getLastSequence(Db).Should().Be(3UL, "because duplicate inserted rows should not advance the last sequence");

                doc = GetDoc(docID);
                doc->revID.CreateString().Should().Be(history[0], "because the earlier revision should win the conflict");
                doc->selectedRev.revID.CreateString().Should().Be(history[0]);
                Native.c4doc_free(doc);

                doc = GetDoc(docID);
                var conflictingRevs = GetRevisionHistory(doc, true, true);
                conflictingRevs.Count.Should().Be(2);
                conflictingRevs.Should().Equal(history[0], conflictHistory[0]);
                Native.c4doc_free(doc);

                var e = (C4DocEnumerator*)LiteCoreBridge.Check(err =>
               {
                   var options = C4EnumeratorOptions.Default;
                   return Native.c4db_enumerateChanges(Db, 0, &options, err);
               });

                var counter = 0;
                while(Native.c4enum_next(e, &error)) {
                    C4DocumentInfo docInfo;
                    Native.c4enum_getDocumentInfo(e, &docInfo);
                    if(counter == 0) {
                        docInfo.docID.CreateString().Should().Be(docID);
                        docInfo.revID.CreateString().Should().Be(history[0]);
                    } else if(counter == 1) {
                        docInfo.docID.CreateString().Should().Be(otherDocID);
                        docInfo.revID.CreateString().Should().Be(otherHistory[0]);
                    }

                    counter++;
                }

                Native.c4enum_free(e);
                counter.Should().Be(2, "because only two documents are present");

                e = (C4DocEnumerator*)LiteCoreBridge.Check(err =>
                {
                    var options = C4EnumeratorOptions.Default;
                    options.flags |= C4EnumeratorFlags.IncludeDeleted;
                    return Native.c4db_enumerateChanges(Db, 0, &options, err);
                });

                counter = 0;
                while(Native.c4enum_next(e, &error)) {
                    doc = Native.c4enum_getDocument(e, &error);
                    if (doc == null) {
                        break;
                    }

                    do {
                        if (counter == 0) {
                            doc->docID.CreateString().Should().Be(docID);
                            doc->selectedRev.revID.CreateString().Should().Be(history[0]);
                        } else if (counter == 1) {
                            doc->docID.CreateString().Should().Be(docID);
                            doc->selectedRev.revID.CreateString().Should().Be(conflictHistory[0]);
                        } else if (counter == 2) {
                            doc->docID.CreateString().Should().Be(otherDocID);
                            doc->selectedRev.revID.CreateString().Should().Be(otherHistory[0]);
                        }

                        counter++;
                    } while (Native.c4doc_selectNextLeafRevision(doc, true, false, &error));

                    Native.c4doc_free(doc);
                }

                Native.c4enum_free(e);
                counter.Should().Be(3, "because only two documents are present, but one has two conflicting revisions");

                doc = PutDoc(docID, conflictHistory[0], FLSlice.Null, C4RevisionFlags.Deleted);
                Native.c4doc_free(doc);
                doc = GetDoc(docID);
                doc->revID.CreateString().Should().Be(history[0]);
                doc->selectedRev.revID.CreateString().Should().Be(history[0]);
                VerifyRev(doc, history, (FLSlice)body);
                Native.c4doc_free(doc);

                doc = PutDoc(docID, history[0], FLSlice.Null, C4RevisionFlags.Deleted);
                Native.c4doc_free(doc);
                Native.FLSliceResult_Release(body);
                Native.FLSliceResult_Release(otherBody);
                Native.FLSliceResult_Release(conflictBody);

                // TODO: Need to implement following tests
            });
        }

        [Fact]
        public void TestDeterministicRevIDs()
        {
            RunTestVariants(() =>
            {
                if (IsRevTrees()) {
                    return;
                }

                var docID = "mydoc";
                var body = JSON2Fleece("{'key':'value'}");
                var doc = PutDoc(docID, null, (FLSlice)body);
                var revID = doc->revID.CreateString();
                Native.c4doc_free(doc);

                DeleteAndRecreateDB();

                doc = PutDoc(docID, null, (FLSlice)body);
                doc->revID.CreateString().Should().Be(revID);
                doc->selectedRev.revID.CreateString().Should().Be(revID);
                Native.c4doc_free(doc);
                Native.FLSliceResult_Release(body);
            });
        }

        [Fact]
        public void TestDuplicateRev()
        {
            RunTestVariants(() =>
            {
                var docID = "mydoc";
                var body = JSON2Fleece("{'key':'value'}");
                var doc = PutDoc(docID, null, (FLSlice)body);
                var revID = doc->revID.CreateString();
                Native.c4doc_free(doc);

                Native.FLSliceResult_Release(body);
                body = JSON2Fleece("{'key':'newvalue'}");
                doc = PutDoc(docID, revID, (FLSlice)body);
                var revID2a = doc->revID.CreateString();
                Native.c4doc_free(doc);

                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                var success = false;
                try {
                    using (var docID_ = new C4String(docID))
                    using (var revID_ = new C4String(revID)) {
                        var history = new FLSlice[] { revID_.AsFLSlice() };
                        fixed (FLSlice* history_ = history) {
                            var rq = new C4DocPutRequest
                            {
                                allowConflict = true,
                                docID = docID_.AsFLSlice(),
                                history = history_,
                                historyCount = 1,
                                body = (FLSlice)body,
                                revFlags = 0,
                                save = true
                            };

                            doc = (C4Document*) LiteCoreBridge.Check(err =>
                            {
                                var local = rq;
                                return Native.c4doc_put(Db, &local, null, err);
                            });

                            doc->docID.CreateString().Should().Be(docID);
                            Native.FLSliceResult_Release(body);
                        }
                    }
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, success, err));
                }

                var revID2b = doc->revID.CreateString();
                Native.c4doc_free(doc);

                revID2b.Should().Be(revID2a, "because an identical revision was inserted");
            });
        }
    }
}
