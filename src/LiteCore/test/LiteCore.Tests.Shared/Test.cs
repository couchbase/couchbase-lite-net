// 
//  Test.cs
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
#if __IOS__
extern alias ios;
#endif
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using FluentAssertions;
using LiteCore.Interop;
using LiteCore.Util;
using System.Collections.Generic;
using System.Text;

using Couchbase.Lite.Interop;
#if !WINDOWS_UWP
using LiteCore.Tests.Util;
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using LiteCore.Tests.Util;
#endif

namespace LiteCore.Tests
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public unsafe class Test : TestBase
    {
#if __ANDROID__
        public static readonly string TestDir = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#elif false
        public static readonly string TestDir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
#elif __IOS__
        public static readonly string TestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "..", "tmp");
#else
        public static readonly string TestDir = Path.GetTempPath();
#endif

        internal static readonly C4Slice Body = C4Slice.Constant("{\"name\":007}");

        internal static readonly C4Slice FleeceBody;
        
        #if COUCHBASE_ENTERPRISE
        protected override int NumberOfOptions => 2;
        #else
        protected override int NumberOfOptions => 1;
        #endif

        private int _objectCount = 0;

        internal C4Database* Db { get; private set; }
        internal C4DocumentVersioning Versioning { get; private set; }
        protected string Storage { get; private set; }

        internal C4Slice DocID => C4Slice.Constant("mydoc");

        internal C4Slice RevID => IsRevTrees() ? C4Slice.Constant("1-abcd") : C4Slice.Constant("1@*");

        internal C4Slice Rev2ID => IsRevTrees() ? C4Slice.Constant("2-c001d00d") : C4Slice.Constant("2@*");

        internal C4Slice Rev3ID => IsRevTrees() ? C4Slice.Constant("3-deadbeef") : C4Slice.Constant("3@*");

        static Test()
        {
            #if NETCOREAPP2_0
            Couchbase.Lite.Support.NetDesktop.Activate();
            #endif
            var enc = Native.FLEncoder_New();
            Native.FLEncoder_BeginDict(enc, 1);
            Native.FLEncoder_WriteKey(enc, "answer");
            Native.FLEncoder_WriteInt(enc, 42);
            Native.FLEncoder_EndDict(enc);
            var result = NativeRaw.FLEncoder_Finish(enc, null);
            FleeceBody = (C4Slice)result;
        }

#if !WINDOWS_UWP
        public Test(ITestOutputHelper output) : base(output)
        {

        }
#endif

        internal static C4Document* c4enum_nextDocument(C4DocEnumerator* e, C4Error* outError)
        {
            return Native.c4enum_next(e, outError) ? Native.c4enum_getDocument(e, outError) : null;
        }

        protected bool IsRevTrees()
        {
            return Versioning == C4DocumentVersioning.RevisionTrees;
        }

        protected void DeleteAndRecreateDB()
        {
            var config = *Native.c4db_getConfig(Db);
            LiteCoreBridge.Check(err => Native.c4db_delete(Db, err));
            Native.c4db_free(Db);
            Db = (C4Database*)LiteCoreBridge.Check(err =>
           {
               var localConfig = config;
               return Native.c4db_open(DatabasePath(), &localConfig, err);
           });
        }

        protected override void SetupVariant(int option)
        {
            _objectCount = Native.c4_getObjectCount();
            Versioning = C4DocumentVersioning.RevisionTrees;
            Native.c4_shutdown(null);

            var config = new C4DatabaseConfig();
            config.flags = C4DatabaseFlags.Create | C4DatabaseFlags.SharedKeys;
            config.versioning = Versioning;

            var encryptedStr = (option & 1) == 1 ? "encrypted " : String.Empty;
            WriteLine($"Opening {encryptedStr}SQLite database using {Versioning}");

            C4Error err;
            config.storageEngine = C4StorageEngine.SQLite;
            if ((option & 1) == 1) {
                config.encryptionKey.algorithm = C4EncryptionAlgorithm.AES256;
                var i = 0;
                foreach (var b in Encoding.UTF8.GetBytes("this is not a random key at all.")) {
                    config.encryptionKey.bytes[i++] = b;
                }
            }

            Native.c4db_deleteAtPath(DatabasePath(), null);
            Db = Native.c4db_open(DatabasePath(), &config, &err);
            ((long)Db).Should().NotBe(0, "because otherwise the database failed to open");
        }

        protected override void TeardownVariant(int option)
        {
            var config = C4DatabaseConfig.Get(Native.c4db_getConfig(Db));
            config.Dispose();
            LiteCoreBridge.Check(err => Native.c4db_delete(Db, err));
            Native.c4db_free(Db);
            Db = null;
            //if(CurrentException == null) {
            //    Native.c4_getObjectCount().Should().Be(_objectCount, "because otherwise an object was leaked");
            //}
        }

        internal void CreateRev(C4Database *db, string docID, C4Slice revID, C4Slice body, C4RevisionFlags flags = (C4RevisionFlags)0)
        {
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(db, err));
            try {
                var curDoc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(db, docID, 
                    false, err));
                var history = new[] { revID, curDoc->revID };
                fixed(C4Slice* h = history) {
                    var rq = new C4DocPutRequest {
                        existingRevision = true,
                        docID = curDoc->docID,
                        history = h,
                        historyCount = curDoc->revID.buf != null ? 2UL : 1UL,
                        body = body,
                        revFlags = flags,
                        save = true
                    };

                    var doc = (C4Document *)LiteCoreBridge.Check(err => {
                        var localRq = rq;
                        return Native.c4doc_put(db, &localRq, null, err);
                    });
                    Native.c4doc_free(doc);
                    Native.c4doc_free(curDoc);
                }
            } finally {
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(db, true, err));
            }
        }

        internal void CreateRev(string docID, C4Slice revID, C4Slice body, C4RevisionFlags flags = (C4RevisionFlags)0)
        {
            CreateRev(Db, docID, revID, body, flags);
        }

        protected void CreateNumberedDocs(int numberOfDocs)
        {
            for (int i = 1; i < 100; i++) {
                var docID = $"doc-{i:D3}";
                CreateRev(docID, RevID, Body);
            }
        }

        protected string DatabasePath() => Path.Combine(TestDir, "cbl_core_test");

        private void Log(C4LogLevel level, C4Slice s)
        {
            WriteLine($"[{level}] {s.CreateString()}");
        }

        internal bool ReadFileByLines(string path, Func<FLSlice, bool> callback)
        {
#if WINDOWS_UWP
            var url = $"ms-appx:///Assets/{path}";
            var file = Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri(url))
                .AsTask()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            var lines = Windows.Storage.FileIO.ReadLinesAsync(file).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            foreach(var line in lines) {
#elif __ANDROID__
            var ctx = global::Couchbase.Lite.Tests.Android.MainActivity.ActivityContext;
            using (var tr = new StreamReader(ctx.Assets.Open(path))) {
                string line;
                while((line = tr.ReadLine()) != null) { 
#elif __IOS__
			var bundlePath = ios::Foundation.NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(path), Path.GetExtension(path));
			using (var tr = new StreamReader(File.Open(bundlePath, FileMode.Open, FileAccess.Read)))
			{
				string line;
				while ((line = tr.ReadLine()) != null)
				{
#else
            using(var tr = new StreamReader(File.Open(path, FileMode.Open))) {
                string line;
                while((line = tr.ReadLine()) != null) {
#endif
					using(var c4 = new C4String(line)) {
                        if(!callback((FLSlice)c4.AsC4Slice())) {
                            return false;
                        }
                    }
                }
#if !WINDOWS_UWP
        }
#endif

            return true;
        } 

        protected uint ImportJSONLines(string path)
        {
            return ImportJSONLines(path, TimeSpan.FromSeconds(15), false);
        }

        // Read a file that contains a JSON document per line. Every line becomes a document.
        protected uint ImportJSONLines(string path, TimeSpan timeout, bool verbose)
        {
            if(verbose) {
                WriteLine($"Reading {path}...");
            }

            var st = Stopwatch.StartNew();
            uint numDocs = 0;
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
            try {
                ReadFileByLines(path, line => {
                    C4Error error;
                    var body = NativeRaw.c4db_encodeJSON(Db, (C4Slice)line, &error);
                    ((long)body.buf).Should().NotBe(0, "because otherwise the encode failed");

                    var docID = (numDocs + 1).ToString("D7");

                    // Save document:
                    using(var docID_ = new C4String(docID)) {
                        var rq = new C4DocPutRequest {
                            docID = docID_.AsC4Slice(),
                            body = (C4Slice)body,
                            save = true
                        };
                        var doc = (C4Document *)LiteCoreBridge.Check(err => {
                            var localRq = rq;
                            return Native.c4doc_put(Db, &localRq, null, err);
                        });
                        Native.c4doc_free(doc);
                    }

                    Native.c4slice_free(body);
                    ++numDocs;
                    if(numDocs % 1000 == 0 && st.Elapsed >= timeout) {
                        Console.Write($"Stopping JSON import after {st.Elapsed.TotalSeconds:F3} sec ");
                        return false;
                    }

                    if(verbose && numDocs % 10000 == 0) {
                        Console.Write($"{numDocs} ");
                    }

                    return true;
                });

                if(verbose) {
                    WriteLine("Committing...");
                }
            } finally {
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
            }

            if(verbose) {
                st.PrintReport("Importing", numDocs, "doc", _output);
            }

            return numDocs;
        }

        protected void ReopenDB()
        {
            var config = C4DatabaseConfig.Get(Native.c4db_getConfig(Db));
            LiteCoreBridge.Check(err => Native.c4db_close(Db, err));
            Native.c4db_free(Db);
            Db = (C4Database *)LiteCoreBridge.Check(err => {
                var localConfig = config;
                return Native.c4db_open(DatabasePath(), &localConfig, err);
            });
        }

        internal C4BlobKey[] AddDocWithAttachments(C4Slice docID, List<string> atts, string contentType)
        {
            var keys = new List<C4BlobKey>();
            var json = new StringBuilder();
            json.Append("{attached: [");
            foreach (var att in atts) {
                var key = new C4BlobKey();
                LiteCoreBridge.Check(err =>
                {
                    var localKey = key;
                    var retVal = Native.c4blob_create(Native.c4db_getBlobStore(Db, null), Encoding.UTF8.GetBytes(att),
                        null, &localKey, err);
                    key = localKey;
                    return retVal;
                });

                keys.Add(key);
                var keyStr = Native.c4blob_keyToString(key);
                json.Append(
                    $"{{'{Constants.ObjectTypeProperty}': '{Constants.ObjectTypeBlob}', 'digest': '{keyStr}', length: {att.Length}, 'content_type': '{contentType}'}},");
            }

            json.Append("]}");
            var jsonStr = Native.FLJSON5_ToJSON(json.ToString(), null);
            using (var jsonStr_ = new C4String(jsonStr)) {
                C4Error error; 
                var body = NativeRaw.c4db_encodeJSON(Db, jsonStr_.AsC4Slice(), &error);
                ((long) body.buf).Should().NotBe(0, "because otherwise the encode failed");

                var rq = new C4DocPutRequest();
                rq.docID = docID;
                rq.revFlags = C4RevisionFlags.HasAttachments;
                rq.body = (C4Slice)body;
                rq.save = true;
                var doc = Native.c4doc_put(Db, &rq, null, &error);
                Native.c4slice_free(body);
                ((long) doc).Should().NotBe(0, "because otherwise the put failed");
                Native.c4doc_free(doc);
                return keys.ToArray();
            }
        }

        protected uint ImportJSONFile(string path)
        {
            return ImportJSONFile(path, "", TimeSpan.FromSeconds(15), false);
        }

        protected uint ImportJSONFile(string path, string prefix)
        {
            return ImportJSONFile(path, prefix, TimeSpan.FromSeconds(15), false);
        }

        protected uint ImportJSONFile(string path, string idPrefix, TimeSpan timeout, bool verbose)
        {
            WriteLine($"Reading {path} ...");
            var st = Stopwatch.StartNew();
#if WINDOWS_UWP
            var url = $"ms-appx:///Assets/{path}";
            var file = Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri(url))
                .AsTask()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            var buffer = Windows.Storage.FileIO.ReadBufferAsync(file).AsTask().ConfigureAwait(false).GetAwaiter()
                .GetResult();
            var jsonData = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(buffer);
#elif __ANDROID__
            var ctx = global::Couchbase.Lite.Tests.Android.MainActivity.ActivityContext;
            byte[] jsonData;
            using (var stream = ctx.Assets.Open(path))
            using (var ms = new MemoryStream()) {
                stream.CopyTo(ms);
                jsonData = ms.ToArray();
            }
#elif __IOS__
			var bundlePath = ios::Foundation.NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(path), Path.GetExtension(path));
			byte[] jsonData;
            using (var stream = File.Open(bundlePath, FileMode.Open, FileAccess.Read))
            using (var ms = new MemoryStream()) {
                stream.CopyTo(ms);
                jsonData = ms.ToArray();
            }
#else
			var jsonData = File.ReadAllBytes(path);
#endif

            FLError error;
            FLSliceResult fleeceData;
            fixed (byte* jsonData_ = jsonData) {
                fleeceData = NativeRaw.FLData_ConvertJSON(new FLSlice(jsonData_, (ulong)jsonData.Length), &error);
            }

            ((long)fleeceData.buf).Should().NotBe(0, "because otherwise the conversion failed");
            var root = Native.FLValue_AsArray(NativeRaw.FLValue_FromTrustedData((FLSlice)fleeceData));
            ((long)root).Should().NotBe(0, "because otherwise the value is not of the expected type");

            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
            try {
                FLArrayIterator iter;
                FLValue* item;
                uint numDocs = 0;
                for (Native.FLArrayIterator_Begin(root, &iter);
                    null != (item = Native.FLArrayIterator_GetValue(&iter));
                    Native.FLArrayIterator_Next(&iter)) {
                    var docID = idPrefix != null ? $"{idPrefix}{numDocs + 1:D7}" : $"doc{numDocs + 1:D7}";
                    var enc = Native.c4db_getSharedFleeceEncoder(Db);
                    Native.FLEncoder_WriteValue(enc, item);
                    var body = NativeRaw.FLEncoder_Finish(enc, &error);

                    var rq = new C4DocPutRequest {
                        docID = C4Slice.Allocate(docID),
                        body = (C4Slice)body,
                        save = true
                    };

                    var doc = (C4Document*)LiteCoreBridge.Check(err =>
                    {
                        var localPut = rq;
                        return Native.c4doc_put(Db, &localPut, null, err);
                    });

                    Native.c4doc_free(doc);
                    Native.FLSliceResult_Free(body);
                    C4Slice.Free(rq.docID);
                    ++numDocs;
                    if ((numDocs % 1000) == 0 && st.Elapsed > timeout) {
                        WriteLine($"WARNING: Stopping JSON import after {st.Elapsed}");
                        return numDocs;
                    }
                    if (verbose && (numDocs % 100000) == 0) {
                        WriteLine($"{numDocs}  ");
                    }
                }

                if (verbose) {
                    st.PrintReport("Importing", numDocs, "doc", _output);
                }

                return numDocs;
            }
            finally {
                Native.FLSliceResult_Free(fleeceData);
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
            }
        }
    }
}
