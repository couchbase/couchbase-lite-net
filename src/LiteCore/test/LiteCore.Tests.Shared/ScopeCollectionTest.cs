// 
// ScopeCollectionTest.cs
// 
// Copyright (c) 2022 Couchbase, Inc All rights reserved.
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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;

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
    public unsafe class ScopeCollectionTest : Test
    {
#if !WINDOWS_UWP
        public ScopeCollectionTest(ITestOutputHelper output) : base(output)
        {

        }
#endif
        #region Database
        [Fact]
        public void TestDefaultCollection()
        {
            RunTestVariants(() =>
            {
                /** Returns the default collection, whose name is "`_default`" (`kC4DefaultCollectionName`).
                 *  This is the one collection that exists in every newly created database.
                 *  When a pre-existing database is upgraded to support collections, all its documents are put
                 *  in the default collection.
                 @note  This function never returns NULL, unless the default collection has been deleted. */
                /** Returns the name and scope of the collection. */
                var collectionSpec = Native.c4coll_getSpec(DefaultColl);
                collectionSpec.name.CreateString().Should().Be(Database._defaultCollectionName);
                /** Returns true if the collection exists. */
                var doesContainCollection = Native.c4db_hasCollection(Db, collectionSpec);
                doesContainCollection.Should().BeTrue("Because old Db does contain default collection.");

                Native.c4coll_getDocumentCount(DefaultColl).Should().Be(0, "because the default collection is empty");
                Native.c4coll_getLastSequence(DefaultColl).Should().Be(0, "because the default collection is empty");
            });
        }

        [Fact]
        public void TestCreateCollection()
        {
            var collName = "newColl";
            var scopeName = "newScope";
            RunTestVariants(() =>
            {
                using (var collName_ = new C4String(collName))
                using (var scopeName_ = new C4String(scopeName)) {
                    var collectionSpec = new C4CollectionSpec() {
                        name = collName_.AsFLSlice(),
                        //scope name cannot be null or empty or _
                        scope = scopeName_.AsFLSlice()
                    };

                    C4Collection* coll = (C4Collection*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_createCollection(Db, collectionSpec, err);
                    });

                    collectionSpec = Native.c4coll_getSpec(coll);
                    collectionSpec.name.CreateString().Should().Be(collName);
                    collectionSpec.scope.CreateString().Should().Be(scopeName);
                    var doesContainCollection = Native.c4db_hasCollection(Db, collectionSpec);
                    doesContainCollection.Should().BeTrue("Because Db contains the collection that was created 6 lines ago.");
                };
            });
        }

        [Fact]
        public void TestCreateCollectionsScopes()
        {
            var collName = "newColl";
            var scopeName = "newScope";
            RunTestVariants(() =>
            {
                using (var collName1_ = new C4String(collName + 1))
                using (var scopeName1_ = new C4String(scopeName + 1))
                using (var collName2_ = new C4String(collName + 2))
                using (var scopeName2_ = new C4String(scopeName + 2))
                using (var collName3_ = new C4String(collName + 3))
                using (var collName4_ = new C4String(collName + 4)) {
                    var collectionSpecA = new C4CollectionSpec() {
                        name = collName1_.AsFLSlice(),
                        scope = scopeName1_.AsFLSlice()
                    };

                    var coll1 = (C4Collection*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_createCollection(Db, collectionSpecA, err);
                    });

                    var collectionSpecB = new C4CollectionSpec() {
                        name = collName2_.AsFLSlice(),
                        scope = scopeName1_.AsFLSlice()
                    };

                    var coll2 = (C4Collection*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_createCollection(Db, collectionSpecB, err);
                    });

                    var collectionSpecC = new C4CollectionSpec() {
                        name = collName3_.AsFLSlice(),
                        scope = scopeName2_.AsFLSlice()
                    };

                    var coll3 = (C4Collection*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_createCollection(Db, collectionSpecC, err);
                    });

                    var c1 = Native.c4db_getCollection(Db, collectionSpecA);
                    var colSpec1 = Native.c4coll_getSpec(c1);
                    colSpec1.name.CreateString().Should().Be(collName + 1);
                    colSpec1.scope.CreateString().Should().Be(scopeName + 1);

                    var c2 = Native.c4db_getCollection(Db, collectionSpecB);
                    var colSpec2 = Native.c4coll_getSpec(c2);
                    colSpec2.name.CreateString().Should().Be(collName + 2);
                    colSpec2.scope.CreateString().Should().Be(scopeName + 1);

                    var c3 = Native.c4db_getCollection(Db, collectionSpecC);
                    var colSpec3 = Native.c4coll_getSpec(c3);
                    colSpec3.name.CreateString().Should().Be(collName + 3);
                    colSpec3.scope.CreateString().Should().Be(scopeName + 2);

                    int cnt = 0;
                    /** Returns the names of all existing collections in the given scope,
                     *  in the order in which they were created.
                      @note  You are responsible for releasing the returned Fleece array. */
                    var arrColl = Native.c4db_collectionNames(Db, scopeName + 1);
                    for (uint i = 1; i <= Native.FLArray_Count((FLArray*)arrColl); i++) {
                        var collStr = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i-1));
                        collStr.Should().Be(collName + i, "Because Scope contains all it's collections' name.");
                        cnt++;
                    }

                    cnt.Should().Be(2);
                    Native.FLValue_Release((FLValue*)arrColl);

                    var arrCol2 = Native.c4db_collectionNames(Db, scopeName + 2);
                    var collStr2 = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrCol2, 0));
                    collStr2.Should().Be(collName + 3, "Because Scope contains it's collection's name.");
                    Native.FLValue_Release((FLValue*)arrCol2);

                    var arrScope = Native.c4db_scopeNames(Db);
                    for (uint i = 1; i <= Native.FLArray_Count((FLArray*)arrScope); i++) {
                        var scopeStr = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrScope, i-1));
                        if((i-1) == 0) 
                            scopeStr.Should().Be(Database._defaultScopeName, "Because the first socpe in db is default scope.");
                        else
                            scopeStr.Should().Be(scopeName + (i-1), "Because Db contains the scopes just added.");
                    }

                    Native.FLValue_Release((FLValue*)arrScope);

                    var deleteSuccessful = (bool)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_deleteCollection(Db, collectionSpecA, err);
                    });
                    deleteSuccessful.Should().BeTrue("Deleting collection newColl successful.");

                    cnt = 0;
                    arrColl = Native.c4db_collectionNames(Db, scopeName + 1);
                    for (uint i = 1; i <= Native.FLArray_Count((FLArray*)arrColl); i++)
                    {
                        var collStr = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i - 1));
                        collStr.Should().Be(collName + (i + 1), "Because the deleted collection is no longer found in scope.");
                        cnt++;
                    }

                    cnt.Should().Be(1, "Because collection collName1 is deleted.");
                    Native.FLValue_Release((FLValue*)arrColl);
                };
            });
        }

        #endregion

        #region Document

        [Fact] //TODO: Revisit when the implementation is done to see if c4coll_putDoc is used anywhere.
        public void TestCreateVersionedDoc()
        {
            RunTestVariants(() => {
                // Try reading doc with mustExist=true, which should fail:
                C4Error error;
                C4Document* doc = NativeRaw.c4coll_getDoc(DefaultColl, DocID, true, C4DocContentLevel.DocGetCurrentRev, &error);
                ((long)doc).Should().Be(0, "because the document does not exist");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int)C4ErrorCode.NotFound);
                Native.c4doc_release(doc);

                // Now get the doc with mustExist=false, which returns an empty doc:
                doc = (C4Document*)LiteCoreBridge.Check(err => NativeRaw.c4coll_getDoc(DefaultColl, DocID, false, C4DocContentLevel.DocGetCurrentRev, err));
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

                    doc = (C4Document*)LiteCoreBridge.Check(err => {
                        var localRq = rq;
                        return Native.c4coll_putDoc(DefaultColl, &localRq, null, err);
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
                doc = (C4Document*)LiteCoreBridge.Check(err => NativeRaw.c4coll_getDoc(DefaultColl, DocID, true, C4DocContentLevel.DocGetCurrentRev, err));
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
        public void TestUpdate()
        {
            RunTestVariants(() =>
            {
                WriteLine("Begin test");
                C4Document* doc = null;
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    WriteLine("Begin create");
                    doc = (C4Document*)LiteCoreBridge.Check(err => NativeRaw.c4coll_createDoc(DefaultColl, DocID, FleeceBody, 0, err));
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
                var doc2 = (C4Document*)LiteCoreBridge.Check(err => NativeRaw.c4coll_getDoc(DefaultColl, DocID, false, C4DocContentLevel.DocGetAll, err));
                doc->revID.Equals(expectedRevID).Should()
                    .BeTrue("because the other reference should have the same rev ID");

                for (int i = 2; i <= 5; i++) {
                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        WriteLine($"Begin save #{i}");
                        var body = JSON2Fleece("{\"ok\":\"go\"}");
                        var oldRevID = doc->revID;
                        var updatedDoc =
                            (C4Document*)LiteCoreBridge.Check(
                                err => NativeRaw.c4doc_update(doc, (FLSlice)body, 0, err));
                        Native.c4doc_release(doc);
                        doc = updatedDoc;
                        Native.FLSliceResult_Release(body);
                    } finally {
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
                    error.code.Should().Be((int)C4ErrorCode.Conflict);
                    error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                    Native.FLSliceResult_Release(body);
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    WriteLine("Begin conflicting create");
                    C4Error error;
                    var body = JSON2Fleece("{\"ok\":\"no way\"}");
                    ((long)NativeRaw.c4coll_createDoc(DefaultColl, DocID, (FLSlice)body, 0, &error)).Should().Be(0, "because this is a conflict");
                    error.code.Should().Be((int)C4ErrorCode.Conflict);
                    error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                    Native.FLSliceResult_Release(body);
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                Native.c4doc_release(doc);
                Native.c4doc_release(doc2);
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
                fixed (FLSlice* history_ = history) {
                    var rq = new C4DocPutRequest {
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
                        doc = Native.c4coll_putDoc(DefaultColl, &rq, null, &error);
                        ((IntPtr)doc).Should().NotBe(IntPtr.Zero);
                        Native.c4doc_release(doc);
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        LiteCoreBridge.Check(err => NativeRaw.c4coll_purgeDoc(DefaultColl, DocID, err));
                    } finally {
                        LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                    }

                    Native.c4coll_getDocumentCount(DefaultColl).Should().Be(0UL);

                    CreateRev(DocID.CreateString(), RevID, FleeceBody);
                    CreateRev(DocID.CreateString(), Rev2ID, (FLSlice)body2);
                    CreateRev(DocID.CreateString(), Rev3ID, (FLSlice)body3);

                    LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                    try {
                        doc = Native.c4coll_putDoc(DefaultColl, &rq, null, &error);
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

                    Native.c4coll_getDocumentCount(DefaultColl).Should().Be(0UL);
                    Native.FLSliceResult_Release(body2);
                    Native.FLSliceResult_Release(body3);
                }
            });
        }

        #endregion

        #region doc expiration
        [Fact]
        public void TestExpired()
        {
            RunTestVariants(() =>
            {
                C4Error err;
                Native.c4coll_nextDocExpiration(DefaultColl).Should().Be(0L);

                var docID = "expire_me";
                CreateRev(docID, RevID, FleeceBody);
                var expire = Native.c4_now() + 1000; //1000ms = 1 sec;
                Native.c4coll_setDocExpiration(DefaultColl, docID, expire, &err).Should()
                    .BeTrue("because otherwise the 1 second expiration failed to set");

                expire = Native.c4_now() + 2000;
                Native.c4coll_setDocExpiration(DefaultColl, docID, expire, &err).Should()
                    .BeTrue("because otherwise the 2 second expiration failed to set");
                Native.c4coll_setDocExpiration(DefaultColl, docID, expire, &err).Should()
                    .BeTrue("because setting to the same time twice should also work");

                var docID2 = "expire_me_too";
                CreateRev(docID2, RevID, FleeceBody);
                Native.c4coll_setDocExpiration(DefaultColl, docID2, expire, &err).Should()
                    .BeTrue("because otherwise the 2 second expiration failed to set");

                var docID3 = "dont_expire_me";
                CreateRev(docID3, RevID, FleeceBody);

                var docID4 = "expire_me_later";
                CreateRev(docID4, RevID, FleeceBody);
                Native.c4coll_setDocExpiration(DefaultColl, docID4, expire + 100_000, &err).Should()
                    .BeTrue("because otherwise the 100 second expiration failed to set");

                Native.c4coll_setDocExpiration(DefaultColl, "nonexistent", expire + 50_000, &err).Should()
                    .BeFalse("because the document is nonexistent");
                err.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                err.code.Should().Be((int)C4ErrorCode.NotFound);

                Native.c4coll_getDocExpiration(DefaultColl, docID, null).Should().Be(expire);
                Native.c4coll_getDocExpiration(DefaultColl, docID2, null).Should().Be(expire);
                Native.c4coll_getDocExpiration(DefaultColl, docID3, null).Should().Be(0L);
                Native.c4coll_getDocExpiration(DefaultColl, docID4, null).Should().Be(expire + 100_000);
                Native.c4coll_getDocExpiration(DefaultColl, "nonexistent", null).Should().Be(0L);
                Native.c4coll_nextDocExpiration(DefaultColl).Should().Be(expire);

                WriteLine("--- Wait till expiration time...");
                Thread.Sleep(TimeSpan.FromSeconds(2));
                Native.c4_now().Should().BeGreaterOrEqualTo(expire);
            });
        }

        [Fact]
        public void TestExpiredMultipleInstances()
        {
            RunTestVariants(() =>
            {
                C4Error error;
                var defaultSpec = Native.c4coll_getSpec(DefaultColl);
                var coll2 = Native.c4db_createCollection(Db, defaultSpec, &error);
                ((long)coll2).Should().NotBe(0);

                Native.c4coll_nextDocExpiration(DefaultColl).Should().Be(0);
                Native.c4coll_nextDocExpiration(coll2).Should().Be(0);

                var docID = "expire_me";
                CreateRev(docID, RevID, FleeceBody);
                var expire = Native.c4_now() + 1000;
                Native.c4coll_setDocExpiration(DefaultColl, docID, expire, &error);
                Native.c4coll_nextDocExpiration(coll2).Should().Be(expire); 
            });
        }

        [Fact]
        public void TestCancelExpire()
        {
            RunTestVariants(() =>
            {
                var docID = "expire_me";
                CreateRev(docID, RevID, FleeceBody);
                var expire = Native.c4_now() + 2000;
                C4Error err;
                Native.c4coll_setDocExpiration(DefaultColl, docID, expire, &err).Should().BeTrue();
                Native.c4coll_getDocExpiration(DefaultColl, docID, null).Should().Be(expire);
                Native.c4coll_nextDocExpiration(DefaultColl).Should().Be(expire);

                Native.c4coll_setDocExpiration(DefaultColl, docID, 0, &err).Should().BeTrue();
                Native.c4coll_getDocExpiration(DefaultColl, docID, null).Should().Be(0);
                Native.c4coll_nextDocExpiration(DefaultColl).Should().Be(0);
            });
        }
        #endregion

        /* TODO: query
         * c4coll_createIndex 
         * c4coll_deleteIndex *
         * c4coll_getIndexesInfo *
         */
    }
}
