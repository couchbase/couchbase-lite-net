// 
// DatabaseTest.cs
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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;

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
    public unsafe class DatabaseTest : Test
    {
#if !WINDOWS_UWP
        public DatabaseTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        private void SetupAllDocs()
        {
            CreateNumberedDocs(99);

            // Add a deleted doc to make sure it's skipped by default:
            CreateRev("doc-005DEL", RevID, FLSlice.Null, C4RevisionFlags.Deleted);
        }

        private void AssertMessage(C4ErrorDomain domain, int code, string expected)
        {
            var msg = Native.c4error_getMessage(new C4Error(domain, code));
            msg.Should().Be(expected, "because the error message should match the code");
        }

        [Fact]
        public void TestAllDocs()
        {
            RunTestVariants(() => {
                SetupAllDocs();

                Native.c4db_getDocumentCount(Db).Should().Be(99UL, "because there are 99 non-deleted documents");

                // No start or end ID:
                var options = C4EnumeratorOptions.Default;
                options.flags &= ~C4EnumeratorFlags.IncludeBodies;
                var e = (C4DocEnumerator *)LiteCoreBridge.Check(err => {
                    var localOpts = options;
                    return Native.c4db_enumerateAllDocs(Db, &localOpts, err);
                });

                int i = 1;
                C4Error error;
                while(Native.c4enum_next(e, &error)) {
                    var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4enum_getDocument(e, err));
                    var docID = $"doc-{i:D3}";
                    doc->docID.CreateString().Should().Be(docID, "because the doc should have the correct doc ID");
                    doc->revID.Equals(RevID).Should().BeTrue("because the doc should have the current revID");
                    doc->selectedRev.revID.Equals(RevID).Should().BeTrue("because the selected rev should have the correct rev ID");
                    doc->selectedRev.sequence.Should().Be((ulong)i, "because the sequences should come in order");
                    doc->selectedRev.body.Equals(FLSlice.Null).Should().BeTrue("because the body is not loaded yet");
                    LiteCoreBridge.Check(err => Native.c4doc_loadRevisionBody(doc, err));
                    doc->selectedRev.body.Equals(FleeceBody).Should().BeTrue("because the loaded body should be correct");

                    C4DocumentInfo info;
                    Native.c4enum_getDocumentInfo(e, &info).Should().BeTrue("because otherwise the doc info load failed");
                    info.docID.CreateString().Should().Be(docID, "because the doc info should have the correct doc ID");
                    info.revID.Equals(RevID).Should().BeTrue("because the doc info should have the correct rev ID");
                    info.bodySize.Should().BeGreaterOrEqualTo(11).And
                        .BeLessOrEqualTo(40, "because the body should have some data");

                    Native.c4doc_free(doc);
                    i++;
                }

                Native.c4enum_free(e);
                i.Should().Be(100);
            });
        }

        [Fact]
        public void TestAllDocsInfo()
        {
            RunTestVariants(() => {
                SetupAllDocs();

                var options = C4EnumeratorOptions.Default;
                var e = (C4DocEnumerator *)LiteCoreBridge.Check(err => {
                    var localOpts = options;
                    return Native.c4db_enumerateAllDocs(Db, &localOpts, err);
                });

                int i = 1;
                C4Error error;
                while(Native.c4enum_next(e, &error)) {
                    C4DocumentInfo doc;
                    Native.c4enum_getDocumentInfo(e, &doc).Should().BeTrue("because otherwise getting the doc info failed");
                    var docID = $"doc-{i:D3}";
                    doc.docID.CreateString().Should().Be(docID, "because the doc info should have the correct doc ID");
                    doc.revID.Equals(RevID).Should().BeTrue("because the doc info should have the correct rev ID");
                    doc.sequence.Should().Be((ulong)i, "because the doc info should have the correct sequence");
                    doc.flags.Should().Be(C4DocumentFlags.DocExists, "because the doc info should have the correct flags");
                    doc.bodySize.Should().BeGreaterOrEqualTo(11).And
                        .BeLessOrEqualTo(40, "because the body should have some data");
                    i++;
                }

                Native.c4enum_free(e);
                error.code.Should().Be(0, "because otherwise an error occurred somewhere");
                i.Should().Be(100, "because all docs should be iterated, even deleted ones");
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
                Native.c4doc_setExpiration(Db, docID, expire, &err).Should().BeTrue();
                Native.c4doc_getExpiration(Db, docID, null).Should().Be(expire);
                Native.c4db_nextDocExpiration(Db).Should().Be(expire);

                Native.c4doc_setExpiration(Db, docID, 0, &err).Should().BeTrue();
                Native.c4doc_getExpiration(Db, docID, null).Should().Be(0);
                Native.c4db_nextDocExpiration(Db).Should().Be(0);
                Native.c4db_purgeExpiredDocs(Db, &err).Should().Be(0);
            });
        }

        [Fact]
        public void TestChanges()
        {
            RunTestVariants(() => {
                CreateNumberedDocs(99);

                // Since start:
                var options = C4EnumeratorOptions.Default;
                options.flags &= ~C4EnumeratorFlags.IncludeBodies;
                var e = (C4DocEnumerator *)LiteCoreBridge.Check(err => {
                    var localOpts = options;
                    return Native.c4db_enumerateChanges(Db, 0, &localOpts, err);
                });

                var seq = 1UL;
                C4Document* doc;
                C4Error error;
                while(null != (doc = c4enum_nextDocument(e, &error))) {
                    doc->selectedRev.sequence.Should().Be(seq, "because the sequence numbers should be ascending");
                    var docID = $"doc-{seq:D3}";
                    doc->docID.CreateString().Should().Be(docID, "because the doc should have the correct doc ID");
                    Native.c4doc_free(doc);
                    seq++;
                }

                Native.c4enum_free(e);

                // Since 6:
                e = (C4DocEnumerator *)LiteCoreBridge.Check(err => {
                    var localOpts = options;
                    return Native.c4db_enumerateChanges(Db, 6, &localOpts, err);
                });

                seq = 7;
                while(null != (doc = c4enum_nextDocument(e, &error))) {
                    doc->selectedRev.sequence.Should().Be(seq, "because the sequence numbers should be ascending");
                    var docID = $"doc-{seq:D3}";
                    doc->docID.CreateString().Should().Be(docID, "because the doc should have the correct doc ID");
                    Native.c4doc_free(doc);
                    seq++;
                }

                Native.c4enum_free(e);
                seq.Should().Be(100UL, "because that is the highest sequence in the DB");
            });
        }

        [Fact]
        public void TestCreateRawDoc()
        {
            RunTestVariants(() => {
                var key = FLSlice.Constant("key");
                var meta = FLSlice.Constant("meta");
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                LiteCoreBridge.Check(err => NativeRaw.c4raw_put(Db, FLSlice.Constant("test"), key, meta, 
                    FleeceBody, err));
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));

                var doc = (C4RawDocument *)LiteCoreBridge.Check(err => NativeRaw.c4raw_get(Db,
                    FLSlice.Constant("test"), key, err));
                doc->key.Equals(key).Should().BeTrue("because the key should not change");
                doc->meta.Equals(meta).Should().BeTrue("because the meta should not change");
                doc->body.Equals(FleeceBody).Should().BeTrue("because the body should not change");
                Native.c4raw_free(doc);

                // Nonexistent:
                C4Error error;
                ((long)Native.c4raw_get(Db, "test", "bogus", &error)).Should().Be(0, 
                    "because the document does not exist");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain, "because that is the correct domain");
                error.code.Should().Be((int)C4ErrorCode.NotFound, "because that is the correct error code");
            });
        }

        [Fact]
        public void TestDatabaseBlobStore()
        {
            RunTestVariants(() => {
                LiteCoreBridge.Check(err => Native.c4db_getBlobStore(Db, err));
            });
        }

        [Fact]
        public void TestDatabaseCompact()
        {
            RunTestVariants(() =>
            {
                var doc1ID = FLSlice.Constant("doc001");
                var doc2ID = FLSlice.Constant("doc002");
                var doc3ID = FLSlice.Constant("doc003");
                var content1 = "This is the first attachment";
                var content2 = "This is the second attachment";

                var atts = new List<string>();
                C4BlobKey key1, key2;
                atts.Add(content1);
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                try {
                    key1 = AddDocWithAttachments(doc1ID, atts, "text/plain")[0];
                    atts.Clear();
                    atts.Add(content2);
                    key2 = AddDocWithAttachments(doc2ID, atts, "text/plain")[0];
                    AddDocWithAttachments(doc3ID, atts, "text/plain");
                } finally {
                    LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                }

                var store = (C4BlobStore*) LiteCoreBridge.Check(err => Native.c4db_getBlobStore(Db, err));
                LiteCoreBridge.Check(err => Native.c4db_compact(Db, err));
                Native.c4blob_getSize(store, key1).Should()
                    .BeGreaterThan(0, "because the attachment should survive the first compact");
                Native.c4blob_getSize(store, key2).Should()
                    .BeGreaterThan(0, "because the attachment should survive the first compact");

                CreateRev("doc001", Rev2ID, FLSlice.Null, C4RevisionFlags.Deleted);
                LiteCoreBridge.Check(err => Native.c4db_compact(Db, err));
                Native.c4blob_getSize(store, key1).Should().Be(-1,
                    "because the attachment should be collected in the second compact");
                Native.c4blob_getSize(store, key2).Should()
                    .BeGreaterThan(0, "because the attachment should survive the second compact");

                CreateRev("doc002", Rev2ID, FLSlice.Null, C4RevisionFlags.Deleted);
                LiteCoreBridge.Check(err => Native.c4db_compact(Db, err));
                Native.c4blob_getSize(store, key1).Should().Be(-1,
                    "because the attachment should still be gone in the third compact");
                Native.c4blob_getSize(store, key2).Should()
                    .BeGreaterThan(0, "because the attachment should survive the third compact");

                CreateRev("doc003", Rev2ID, FLSlice.Null, C4RevisionFlags.Deleted);
                LiteCoreBridge.Check(err => Native.c4db_compact(Db, err));
                Native.c4blob_getSize(store, key1).Should().Be(-1,
                    "because the attachment should still be gone in the fourth compact");
                Native.c4blob_getSize(store, key2).Should().Be(-1,
                    "because the attachment should be collected in the fourth compact");
            });
        }

        [Fact]
        public void TestDeletionLock()
        {
            RunTestVariants(() =>
            {
                C4Error err;
                Native.c4db_deleteAtPath(DatabasePath(), &err).Should().BeFalse("because the database is open");
                err.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                err.code.Should().Be((int) C4ErrorCode.Busy);

                var equivalentPath = DatabasePath() + Path.DirectorySeparatorChar;
                Native.c4db_deleteAtPath(equivalentPath, &err).Should().BeFalse("because the database is open");
                err.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                err.code.Should().Be((int) C4ErrorCode.Busy);
            });
        }

        [Fact]
        public void TestDatabaseInfo()
        {
            RunTestVariants(() => {
                Native.c4db_getDocumentCount(Db).Should().Be(0, "because the database is empty");
                Native.c4db_getLastSequence(Db).Should().Be(0, "because the database is empty");
                var publicID = new C4UUID();
                var privateID = new C4UUID();
                C4Error err;
                var uuidSuccess = Native.c4db_getUUIDs(Db, &publicID, &privateID, &err);
                if (!uuidSuccess) {
                    throw CouchbaseException.Create(err);
                }
                
                var p1 = publicID;
                var p2 = privateID;
                var match = true;
                for (int i = 0; i < C4UUID.Size; i++) {
                    if (publicID.bytes[i] != privateID.bytes[i]) {
                        match = false;
                        break;
                    }
                }

                match.Should().BeFalse("because public UUID and private UUID should differ");
                (p1.bytes[6] & 0xF0).Should().Be(0x40, "because otherwise the UUID is non-conformant");
                (p1.bytes[8] & 0xC0).Should().Be(0x80, "because otherwise the UUID is non-conformant");
                (p2.bytes[6] & 0xF0).Should().Be(0x40, "because otherwise the UUID is non-conformant");
                (p2.bytes[8] & 0xC0).Should().Be(0x80, "because otherwise the UUID is non-conformant");

                // Make sure the UUIDs are persistent
                ReopenDB();
                var publicID2 = new C4UUID();
                var privateID2 = new C4UUID();
                uuidSuccess = Native.c4db_getUUIDs(Db, &publicID2, &privateID2, &err);
                if (!uuidSuccess) {
                    throw CouchbaseException.Create(err);
                }

                for (int i = 0; i < C4UUID.Size; i++) {
                    publicID2.bytes[i].Should().Be(publicID.bytes[i]);
                    privateID2.bytes[i].Should().Be(privateID.bytes[i]);
                }
            });
        }

        [Fact]
        public void TestErrorMessages()
        {
            var msg = Native.c4error_getMessage(new C4Error(C4ErrorDomain.LiteCoreDomain, 0));
            msg.Should().BeNull("because there was no error");

            AssertMessage(C4ErrorDomain.SQLiteDomain, (int)SQLiteStatus.Corrupt, "database disk image is malformed");
            AssertMessage(C4ErrorDomain.LiteCoreDomain, (int)C4ErrorCode.InvalidParameter, "invalid parameter");
            AssertMessage(C4ErrorDomain.POSIXDomain, PosixBase.GetCode(nameof(PosixBase.ENOENT)), "No such file or directory");
            AssertMessage(C4ErrorDomain.LiteCoreDomain, (int)C4ErrorCode.TransactionNotClosed, "transaction not closed");
            AssertMessage(C4ErrorDomain.SQLiteDomain, -1234, "unknown error (-1234)");
            AssertMessage((C4ErrorDomain)666, -1234, "unknown error domain");
        }

        [Fact]
        public void TestExpired()
        {
            RunTestVariants(() =>
            {
                C4Error err;
                Native.c4db_nextDocExpiration(Db).Should().Be(0L);
                Native.c4db_purgeExpiredDocs(Db, &err).Should().Be(0L);

                var docID = "expire_me";
                CreateRev(docID, RevID, FleeceBody);
                var expire = Native.c4_now() + 1000; //1000ms = 1 sec;
                Native.c4doc_setExpiration(Db, docID, expire, &err).Should()
                    .BeTrue("because otherwise the 1 second expiration failed to set");

                expire = Native.c4_now() + 2000;
                Native.c4doc_setExpiration(Db, docID, expire, &err).Should()
                    .BeTrue("because otherwise the 2 second expiration failed to set");
                Native.c4doc_setExpiration(Db, docID, expire, &err).Should()
                    .BeTrue("because setting to the same time twice should also work");

                var docID2 = "expire_me_too";
                CreateRev(docID2, RevID, FleeceBody);
                Native.c4doc_setExpiration(Db, docID2, expire, &err).Should()
                    .BeTrue("because otherwise the 2 second expiration failed to set");

                var docID3 = "dont_expire_me";
                CreateRev(docID3, RevID, FleeceBody);

                var docID4 = "expire_me_later";
                CreateRev(docID4, RevID, FleeceBody);
                Native.c4doc_setExpiration(Db, docID4, expire + 100_000, &err).Should()
                    .BeTrue("because otherwise the 100 second expiration failed to set");

                Native.c4doc_setExpiration(Db, "nonexistent", expire + 50_000, &err).Should()
                    .BeFalse("because the document is nonexistent");
                err.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                err.code.Should().Be((int) C4ErrorCode.NotFound);

                Native.c4doc_getExpiration(Db, docID, null).Should().Be(expire);
                Native.c4doc_getExpiration(Db, docID2, null).Should().Be(expire);
                Native.c4doc_getExpiration(Db, docID3, null).Should().Be(0L);
                Native.c4doc_getExpiration(Db, docID4, null).Should().Be(expire + 100_000);
                Native.c4doc_getExpiration(Db, "nonexistent", null).Should().Be(0L);
                Native.c4db_nextDocExpiration(Db).Should().Be(expire);

                WriteLine("--- Wait till expiration time...");
                Thread.Sleep(TimeSpan.FromSeconds(2));
                Native.c4_now().Should().BeGreaterOrEqualTo(expire);

                WriteLine("--- Purge expired docs");
                Native.c4db_purgeExpiredDocs(Db, &err).Should().Be(2, "because there are two expired documents");

                Native.c4db_nextDocExpiration(Db).Should().Be(expire + 100_000);

                WriteLine("--- Purge expired docs (again)");
                Native.c4db_purgeExpiredDocs(Db, &err).Should().Be(0, "because there are no expired documents");
            });
        }

        [Fact]
        public void TestExpiredMultipleInstances()
        {
            RunTestVariants(() =>
            {
                C4Error error;
                var db2 = Native.c4db_open(DatabasePath(), Native.c4db_getConfig(Db), &error);
                ((long) db2).Should().NotBe(0);

                Native.c4db_nextDocExpiration(Db).Should().Be(0);
                Native.c4db_nextDocExpiration(db2).Should().Be(0);

                var docID = "expire_me";
                CreateRev(docID, RevID, FleeceBody);
                var expire = Native.c4_now() + 1000;
                Native.c4doc_setExpiration(Db, docID, expire, &error);

                Native.c4db_nextDocExpiration(db2).Should().Be(expire);
                Native.c4db_free(db2);
            });
        }

        [Fact]
        public void TestOpenBundle()
        {
            RunTestVariants(() => {
                var config = C4DatabaseConfig.Clone(Native.c4db_getConfig(Db));
                var tmp = config;

                var bundlePath = Path.Combine(TestDir, $"cbl_core_test_bundle{Path.DirectorySeparatorChar}");
                Native.c4db_deleteAtPath(bundlePath, null);
                var bundle = (C4Database *)LiteCoreBridge.Check(err => {
                    var localConfig = tmp;
                    return Native.c4db_open(bundlePath, &localConfig, err);
                });

                var path = Native.c4db_getPath(bundle);
                path.Should().Be(bundlePath, "because the database should store the correct path");
                LiteCoreBridge.Check(err => Native.c4db_close(bundle, err));
                Native.c4db_free(bundle);

                // Reopen without the 'create' flag:
                config.flags &= ~C4DatabaseFlags.Create;
                tmp = config;
                bundle = (C4Database *)LiteCoreBridge.Check(err => {
                    var localConfig = tmp;
                    return Native.c4db_open(bundlePath, &localConfig, err);
                });
                LiteCoreBridge.Check(err => Native.c4db_close(bundle, err));
                Native.c4db_free(bundle);

                // Reopen with wrong storage type:
                NativePrivate.c4log_warnOnErrors(false);
                var engine = config.storageEngine;
                config.storageEngine = "b0gus";
                ((long)Native.c4db_open(bundlePath, &config, null)).Should().Be(0, "because the storage engine is nonsense");
                config.storageEngine = engine;

                // Open nonexistent bundle
                ((long)Native.c4db_open($"no_such_bundle{Path.DirectorySeparatorChar}", &config, null)).Should().Be(0, "because the bundle does not exist");
                NativePrivate.c4log_warnOnErrors(true);

                config.Dispose();
            });
        }

        [Fact]
        public void TestReadonlyUUIDs()
        {
            RunTestVariants(() =>
            {
                ReopenDBReadOnly();
                C4Error err;
                C4UUID publicUUID, privateUUID;
                Native.c4db_getUUIDs(Db, &publicUUID, &privateUUID, &err).Should()
                    .BeTrue("because the UUID should still be available in a read-only db");
            });
        }

        [Fact]
        public void TestTransaction()
        {
            RunTestVariants(() => {
                Native.c4db_getDocumentCount(Db).Should().Be(0, "because no documents have been added");
                Native.c4db_isInTransaction(Db).Should().BeFalse("because no transaction has started yet");
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                Native.c4db_isInTransaction(Db).Should().BeTrue("because a transaction has started");
                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                Native.c4db_isInTransaction(Db).Should().BeTrue("because another transaction has started");
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                Native.c4db_isInTransaction(Db).Should().BeTrue("because a transaction is still active");
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
                Native.c4db_isInTransaction(Db).Should().BeFalse("because all transactions have ended");

                LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
                Native.c4db_isInTransaction(Db).Should().BeTrue("because a transaction has started");
                CreateRev(DocID.CreateString(), RevID, FleeceBody);
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, false, err));
                Native.c4db_isInTransaction(Db).Should().BeFalse("because all transactions have ended");
                Native.c4db_getDocumentCount(Db).Should().Be(0, "because the transaction was aborted");
            });
        }

        [Fact]
        public void TestDatabaseCopy()
        {
            RunTestVariants(() =>
            {
                var doc1ID = "doc001";
                var doc2ID = "doc002";

                CreateRev(doc1ID, RevID, FleeceBody);
                CreateRev(doc2ID, RevID, FleeceBody);

                var srcPath = Native.c4db_getPath(Db);
                var destPath = Path.Combine(Path.GetTempPath(), $"nudb.cblite2{Path.DirectorySeparatorChar}");
                C4Error error;
                var config = *(Native.c4db_getConfig(Db));

                if (!Native.c4db_deleteAtPath(destPath, &error)) {
                    error.code.Should().Be(0);
                }
                
                LiteCoreBridge.Check(err =>
                {
                    var localConfig = config;
                    return Native.c4db_copy(srcPath, destPath, &localConfig, err);
                });
                var nudb = (C4Database*)LiteCoreBridge.Check(err =>
                {
                    var localConfig = config;
                    return Native.c4db_open(destPath, &localConfig, err);
                });

                try {
                    Native.c4db_getDocumentCount(nudb).Should().Be(2L, "because the database was seeded");
                    LiteCoreBridge.Check(err => Native.c4db_delete(nudb, err));
                }
                finally {
                    Native.c4db_free(nudb);
                }

                nudb = (C4Database*)LiteCoreBridge.Check(err =>
                {
                    var localConfig = config;
                    return Native.c4db_open(destPath, &localConfig, err);
                });

                try {
                    CreateRev(nudb, doc1ID, RevID, FleeceBody);
                    Native.c4db_getDocumentCount(nudb).Should().Be(1L, "because a document was inserted");
                }
                finally {
                    Native.c4db_free(nudb);
                }

                var originalDest = destPath;
                destPath = Path.Combine(Path.GetTempPath(), "bogus", $"nudb.cblite2{Path.DirectorySeparatorChar}");
                Action a = () => LiteCoreBridge.Check(err =>
                {
                    var localConfig = config;
                    return Native.c4db_copy(srcPath, destPath, &localConfig, err);
                });
                a.ShouldThrow<CouchbaseLiteException>().Where(e =>
                    e.Error == CouchbaseLiteError.NotFound && e.Domain == CouchbaseLiteErrorType.CouchbaseLite);

                nudb = (C4Database*)LiteCoreBridge.Check(err =>
                {
                    var localConfig = config;
                    return Native.c4db_open(originalDest, &localConfig, err);
                });

                try {
                    Native.c4db_getDocumentCount(nudb).Should().Be(1L, "because the original database should remain");
                }
                finally {
                    Native.c4db_free(nudb);
                }

                var originalSrc = srcPath;
                srcPath = $"{srcPath}bogus{Path.DirectorySeparatorChar}";
                destPath = originalDest;
                a.ShouldThrow<CouchbaseLiteException>().Where(e =>
                    e.Error == CouchbaseLiteError.NotFound && e.Domain == CouchbaseLiteErrorType.CouchbaseLite);

                nudb = (C4Database*)LiteCoreBridge.Check(err =>
                {
                    var localConfig = config;
                    return Native.c4db_open(destPath, &localConfig, err);
                });

                try {
                    Native.c4db_getDocumentCount(nudb).Should().Be(1L, "because the original database should remain");
                }
                finally {
                    Native.c4db_free(nudb);
                }

                srcPath = originalSrc;
                a.ShouldThrow<CouchbasePosixException>().Where(e =>
                    e.Error == PosixBase.GetCode(nameof(PosixBase.EEXIST)) && e.Domain == CouchbaseLiteErrorType.POSIX);
                nudb = (C4Database*)LiteCoreBridge.Check(err =>
                {
                    var localConfig = config;
                    return Native.c4db_open(destPath, &localConfig, err);
                });

                try {
                    Native.c4db_getDocumentCount(nudb).Should().Be(1L, "because the database copy failed");
                    LiteCoreBridge.Check(err => Native.c4db_delete(nudb, err));
                }
                finally {
                    Native.c4db_free(nudb);
                }
            });
        }

        #if COUCHBASE_ENTERPRISE

        [Fact]
        public void TestDatabaseRekey()
        {
            RunTestVariants(() =>
            {
                CreateNumberedDocs(99);

                // Add blob to the store:
                var blobToStore = FLSlice.Constant("This is a blob to store in the store!");
                var blobKey = new C4BlobKey();
                var blobStore = (C4BlobStore*)LiteCoreBridge.Check(err => Native.c4db_getBlobStore(Db, err));
                LiteCoreBridge.Check(err =>
                {
                    C4BlobKey local;
                    var retVal = NativeRaw.c4blob_create(blobStore, blobToStore, null, &local, err);
                    blobKey = local;
                    return retVal;
                });

                C4Error error;
                var blobResult = NativeRaw.c4blob_getContents(blobStore, blobKey, &error);
                ((FLSlice)blobResult).Should().Be(blobToStore);
                Native.FLSliceResult_Release(blobResult);

                // If we're on the unexcrypted pass, encrypt the db.  Otherwise, decrypt it:
                var newKey = new C4EncryptionKey();
                if(Native.c4db_getConfig(Db)->encryptionKey.algorithm == C4EncryptionAlgorithm.None) {
                    newKey.algorithm = C4EncryptionAlgorithm.AES256;
                    var keyBytes = Encoding.ASCII.GetBytes("a different key than default....");
                    Marshal.Copy(keyBytes, 0, (IntPtr)newKey.bytes, 32);
                }

                var tmp = newKey;
                LiteCoreBridge.Check(err =>
                {
                    var local = tmp;
                    return Native.c4db_rekey(Db, &local, err);
                });

                // Verify the db works:
                Native.c4db_getDocumentCount(Db).Should().Be(99);
                ((IntPtr)blobStore).Should().NotBe(IntPtr.Zero);
                blobResult = NativeRaw.c4blob_getContents(blobStore, blobKey, &error);
                ((FLSlice)blobResult).Should().Be(blobToStore);
                Native.FLSliceResult_Release(blobResult);

                // Check thqat db can be reopened with the new key:
                Native.c4db_getConfig(Db)->encryptionKey.algorithm.Should().Be(newKey.algorithm);
                for(int i = 0; i < 32; i++) {
                    Native.c4db_getConfig(Db)->encryptionKey.bytes[i].Should().Be(newKey.bytes[i]);
                }

                ReopenDB();
            });
        }
        #endif
    }
}