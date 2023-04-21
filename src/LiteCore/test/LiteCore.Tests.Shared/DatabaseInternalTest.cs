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

#nullable disable
using System;
using System.Collections.Generic;
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
                            doc = Native.c4coll_putDoc(Native.c4db_getDefaultCollection(db, null), &local, null, error);
                        } else {
                            doc = (C4Document*)LiteCoreBridge.Check(err =>
                           {
                               var local = rq;
                               return Native.c4coll_putDoc(Native.c4db_getDefaultCollection(db, null), &local, null, err);
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
            Native.c4doc_release(doc);
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
                            doc = Native.c4coll_putDoc(Native.c4db_getDefaultCollection(db, null), &local, null, error);
                        } else {
                            doc = (C4Document*)LiteCoreBridge.Check(err =>
                           {
                               var local = rq;
                               return Native.c4coll_putDoc(Native.c4db_getDefaultCollection(db, null), &local, null, err);
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

        private C4Document* GetDoc(string docID, C4DocContentLevel contentLevel = C4DocContentLevel.DocGetCurrentRev)
        {
            return GetDoc(Db, docID, contentLevel);
        }

        private C4Document* GetDoc(C4Database* db, string docID, C4DocContentLevel contentLevel)
        {
            var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4coll_getDoc(Native.c4db_getDefaultCollection(db, null), docID, true, contentLevel, err));
            doc->docID.CreateString().Should().Be(docID);
            return doc;
        }

        private void VerifyRev(C4Document* doc, string[] history, FLSlice body)
        {
            doc->revID.CreateString().Should().Be(history[0]);
            doc->selectedRev.revID.CreateString().Should().Be(history[0]);
            Native.FLSlice_Equal(NativeRaw.c4doc_getRevisionBody(doc), body).Should().BeTrue();

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
        public void TestExpectedRevIDs()
        {
            // It's not strictly required that revisions alwauys generate the same revIDs, but it helps
            // prevent false conflicts when two peers make the same change to the same parent revision.
            RunTestVariants(() =>
            {
                if (!IsRevTrees(Db)) {
                    return;
                }

                var body = JSON2Fleece("{'property':'value'}");
                var doc = PutDoc("doc", null, (FLSlice)body);
                Native.FLSliceResult_Release(body);
                var docID = doc->docID.CreateString();
                var revID1 = doc->revID.CreateString();
                revID1.Should().Be("1-d65a07abdb5c012a1bd37e11eef1d0aca3fa2a90");
                Native.c4doc_release(doc);

                body = JSON2Fleece("{'property':'newvalue'}");
                doc = PutDoc("doc", revID1, (FLSlice)body);
                var revID2 = doc->revID.CreateString();
                revID2.Should().Be("2-eaaa643f551df08eb0c60f87f3f011ac4355f834");
                Native.c4doc_release(doc);

                doc = PutDoc("doc", revID2, FLSlice.Null, C4RevisionFlags.Deleted);
                doc->revID.CreateString().Should().Be("3-3ae8fab29af3a5bfbfa5a4c5fd91c58214cb0c5a");
                Native.c4doc_release(doc);
            });
        }

        [Fact]
        public void TestDeleteWithProperties()
        {
            // Test that it's possible to delete a document by PUTting a revision with _deleted=true,
            // and that the saved deleted revision will preserve any extra properties
            RunTestVariants(() =>
            {
                if (!IsRevTrees(Db)) {
                    return;
                }

                var body1 = JSON2Fleece("{'property':'newvalue'}");
                var doc = PutDoc(null, null, (FLSlice)body1);
                Native.FLSliceResult_Release(body1);
                var docID = doc->docID.CreateString();
                var revID1 = doc->revID.CreateString();
                Native.c4doc_release(doc);

                var body2 = JSON2Fleece("{'property':'newvalue'}");
                doc = PutDoc(docID, revID1, (FLSlice)body2, C4RevisionFlags.Deleted);
                var revID2 = doc->revID.CreateString();
                Native.c4doc_release(doc);

                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4coll_getDoc(Native.c4db_getDefaultCollection(Db, null), docID, true, C4DocContentLevel.DocGetCurrentRev, err));
                LiteCoreBridge.Check(err => Native.c4doc_selectRevision(doc, revID2, true, err));
                doc->flags.Should().Be(C4DocumentFlags.DocExists | C4DocumentFlags.DocDeleted);
                doc->selectedRev.flags.Should().Be(C4RevisionFlags.Leaf | C4RevisionFlags.Deleted);
                Native.FLSlice_Equal(NativeRaw.c4doc_getRevisionBody(doc), (FLSlice) body2).Should().BeTrue();
                Native.c4doc_release(doc);

                doc = PutDoc(docID, null, (FLSlice)body2);
                var revID3 = doc->revID.CreateString();
                revID3.Should().StartWith("3-", "because even though it was created as 'new', it is actually on top of a previous delete and its generation should reflect that");
                Native.c4doc_release(doc);
                Native.FLSliceResult_Release(body2);

                doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4coll_getDoc(Native.c4db_getDefaultCollection(Db, null), docID, true, C4DocContentLevel.DocGetCurrentRev, err));
                doc->revID.CreateString().Should().Be(revID3);
                Native.c4doc_release(doc);
            });
        }

        [Fact]
        public void TestDeleteAndRecreate()
        {
            RunTestVariants(() =>
            {
                if (!IsRevTrees(Db)) {
                    return;
                }

                var body = JSON2Fleece("{'property':'value'}");
                var doc = PutDoc("dock", null, (FLSlice)body);
                var revID1 = doc->revID.CreateString();
                revID1.Should().StartWith("1-", "because otherwise the generation is incorrect");
                Native.FLSlice_Equal(NativeRaw.c4doc_getRevisionBody(doc), (FLSlice) body).Should().BeTrue();
                Native.c4doc_release(doc);

                doc = PutDoc("dock", revID1, FLSlice.Null, C4RevisionFlags.Deleted);
                var revID2 = doc->revID.CreateString();
                revID2.Should().StartWith("2-", "because otherwise the generation is incorrect");
                doc->selectedRev.flags.Should().Be(C4RevisionFlags.Leaf | C4RevisionFlags.Deleted);
                NativeRaw.c4doc_getRevisionBody(doc).CreateString().Should().NotBeNull("because a valid revision should not have a null body");
                Native.c4doc_release(doc);

                doc = PutDoc("dock", revID2, (FLSlice)body);
                doc->revID.CreateString().Should().StartWith("3-", "because otherwise the generation is incorrect");
                Native.FLSlice_Equal(NativeRaw.c4doc_getRevisionBody(doc), (FLSlice) body).Should().BeTrue();
                Native.c4doc_release(doc);
                Native.FLSliceResult_Release(body);
            });
        }

        [Fact]
        public void TestDeterministicRevIDs()
        {
            RunTestVariants(() =>
            {
                if (IsRevTrees(Db)) {
                    return;
                }

                var docID = "mydoc";
                var body = JSON2Fleece("{'key':'value'}");
                var doc = PutDoc(docID, null, (FLSlice)body);
                var revID = doc->revID.CreateString();
                Native.c4doc_release(doc);

                DeleteAndRecreateDB();

                doc = PutDoc(docID, null, (FLSlice)body);
                doc->revID.CreateString().Should().Be(revID);
                doc->selectedRev.revID.CreateString().Should().Be(revID);
                Native.c4doc_release(doc);
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
                Native.c4doc_release(doc);

                Native.FLSliceResult_Release(body);
                body = JSON2Fleece("{'key':'newvalue'}");
                doc = PutDoc(docID, revID, (FLSlice)body);
                var revID2a = doc->revID.CreateString();
                Native.c4doc_release(doc);

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
                                return Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &local, null, err);
                            });

                            doc->docID.CreateString().Should().Be(docID);
                            Native.FLSliceResult_Release(body);
                        }
                    }
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, success, err));
                }

                var revID2b = doc->revID.CreateString();
                Native.c4doc_release(doc);

                revID2b.Should().Be(revID2a, "because an identical revision was inserted");
            });
        }
    }
}
