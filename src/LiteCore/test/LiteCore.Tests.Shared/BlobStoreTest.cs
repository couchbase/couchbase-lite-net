// 
//  BlobStoreTest.cs
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
    public unsafe class BlobStoreTest : TestBase
    {
#if !WINDOWS_UWP
        public BlobStoreTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        #if COUCHBASE_ENTERPRISE
        protected override int NumberOfOptions => 2;
        #else
        protected override int NumberOfOptions => 1;
        #endif

        private bool _encrypted;
        private C4BlobStore* _store;
        private readonly C4BlobKey _bogusKey = new C4BlobKey();

        protected override void SetupVariant(int options)
        {
            _encrypted = options == 1;
            C4EncryptionKey crypto = new C4EncryptionKey();
            C4EncryptionKey *encryption = null;
            if(_encrypted) {
                WriteLine("        ...encrypted");
                crypto.algorithm = C4EncryptionAlgorithm.AES256;
                for(int i = 0; i < 32; i++) {
                    crypto.bytes[i] = 0xcc;
                }

                encryption = &crypto;
            }

            _store = (C4BlobStore *)LiteCoreBridge.Check(err => Native.c4blob_openStore(Path.Combine(
                Test.TestDir, $"cbl_blob_test{Path.DirectorySeparatorChar}"), C4DatabaseFlags.Create, encryption, err));
            var bogusKey = _bogusKey;
            for(int i = 0; i < C4BlobKey.Size; i++) {
                bogusKey.bytes[i] = 0x55;
            }
        }

        protected override void TeardownVariant(int options)
        {
            LiteCoreBridge.Check(err => Native.c4blob_deleteStore(_store, err));
        }

        [Fact]
        public void TestCancelWrite()
        {
            RunTestVariants(() => {
                // Write the blob:
                var stream = (C4WriteStream *)LiteCoreBridge.Check(err => Native.c4blob_openWriteStream(_store, err));
                var buf = Encoding.UTF8.GetBytes("This is line oops\n");
                LiteCoreBridge.Check(err => Native.c4stream_write(stream, buf, err));

                Native.c4stream_closeWriter(stream);
            });
        }

        [Fact]
        public void TestCreateBlobKeyMismatch()
        {
            RunTestVariants(() =>
            {
                var blobToStore = FLSlice.Constant("This is a blob to store in the store!");
                C4BlobKey key, expectedKey = new C4BlobKey();
                var i = 0;
                foreach (var b in Enumerable.Repeat<byte>(0x55, sizeof(C4BlobKey))) {
                    expectedKey.bytes[i++] = b;
                }

                C4Error error;
                NativePrivate.c4log_warnOnErrors(false);
                try {
                    
                    NativeRaw.c4blob_create(_store, blobToStore, &expectedKey, &key, &error).Should().BeFalse();
                    error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                    error.code.Should().Be((int) C4ErrorCode.CorruptData);
                } finally {
                    NativePrivate.c4log_warnOnErrors(true);
                }

                Native.c4blob_keyFromString("sha1-QneWo5IYIQ0ZrbCG0hXPGC6jy7E=", &expectedKey);
                NativeRaw.c4blob_create(_store, blobToStore, &expectedKey, &key, &error).Should().BeTrue();
            });
        }

        [Fact]
        public void TestCreateBlobs()
        {
            RunTestVariants(() => {
                var blobToStore = Encoding.UTF8.GetBytes("This is a blob to store in the store!");

                // Add the blob to the store:
                var key = new C4BlobKey();
                var localKey = &key;
                LiteCoreBridge.Check(err => {
                    return Native.c4blob_create(_store, blobToStore, null, localKey, err);
                });

                var str = Native.c4blob_keyToString(key);
                str.Should().Be("sha1-QneWo5IYIQ0ZrbCG0hXPGC6jy7E=", "because the blob key should hash correctly");

                // Read it back and compare
                var blobSize = Native.c4blob_getSize(_store, key);
                blobSize.Should().BeGreaterOrEqualTo(blobToStore.Length, "because the size should be a conservative estimate, never lower");
                if(_encrypted) {
                    blobSize.Should().BeLessOrEqualTo(blobToStore.Length + 16, "because the estimate should never be more than 16 bytes off");
                } else {
                    blobSize.Should().Be(blobToStore.Length, "because unecrypted blobs should have the exact size");
                }

                C4Error error;
                var gotBlob = Native.c4blob_getContents(_store, key, &error);
                gotBlob.Should().NotBeNull("because the attachment should be readable");
                blobToStore.Should().Equal(gotBlob, "because the attachment shouldn't change");

                var p = Native.c4blob_getFilePath(_store, key, &error);
                if(_encrypted) {
                    p.Should().BeNull("because an encrypted store will not return a file path");
                    error.code.Should().Be((int)C4ErrorCode.WrongFormat, "because otherwise an unexpected error occurred");   
                } else {
                    p.Should().NotBeNull("because otherwise the DB failed to return its blob store");
                    var filename = "QneWo5IYIQ0ZrbCG0hXPGC6jy7E=.blob";
                    Path.GetFileName(p).Should().Be(filename, "because otherwise the store returned an invalid filename");
                }

                // Try storing it again
                var key2 = new C4BlobKey();
                localKey = &key2;
                LiteCoreBridge.Check(err => {
                    return Native.c4blob_create(_store, blobToStore, null, localKey, err);
                });

                for (int i = 0; i < C4BlobKey.Size; i++) {
                    key.bytes[i].Should().Be(key2.bytes[i]);
                }
            });
        }

        [Fact]
        public void TestDeleteBlobs()
        {
            RunTestVariants(() =>
            {
                var blobToStore = Encoding.ASCII.GetBytes("This is a blob to store in the store!");

                var key = new C4BlobKey();
                LiteCoreBridge.Check(err =>
                {
                    C4BlobKey tmp;
                    var retVal = Native.c4blob_create(_store, blobToStore, null, &tmp, err);
                    key = tmp;
                    return retVal;
                });

                var str = Native.c4blob_keyToString(key);
                str.Should().Be("sha1-QneWo5IYIQ0ZrbCG0hXPGC6jy7E=");

                LiteCoreBridge.Check(err => Native.c4blob_delete(_store, key, err));

                var blobSize = Native.c4blob_getSize(_store, key);
                blobSize.Should().Be(-1L, "because the blob was deleted");

                var gotBlob = Native.c4blob_getContents(_store, key, null);
                gotBlob.Should().BeNull("because the blob was deleted");

                C4Error error;
                var p = Native.c4blob_getFilePath(_store, key, &error);
                p.Should().BeNull("because the blob was deleted");
                error.domain.Should().Be(C4ErrorDomain.LiteCoreDomain);
                error.code.Should().Be((int) C4ErrorCode.NotFound);
            });
        }

        [Fact]
        public void TestMissingBlobs()
        {
            RunTestVariants(() => {
                Native.c4blob_getSize(_store, _bogusKey).Should().Be(-1);
                C4Error error;
                var data = Native.c4blob_getContents(_store, _bogusKey, &error);
                data.Should().BeNull("because the attachment doesn't exist");
                error.code.Should().Be((int)C4ErrorCode.NotFound, "because that is the correct error code for a missing attachment");
                var path = Native.c4blob_getFilePath(_store, _bogusKey, &error);
                path.Should().BeNull("because the attachment doesn't exist");
                error.code.Should().Be((int)C4ErrorCode.NotFound, "because that is the correct error code for a missing attachment");
            });
        }

        [Fact]
        public void TestParseBlobKeys()
        {
            var key1 = new C4BlobKey();
            for(int i = 0; i < C4BlobKey.Size; i++) {
                key1.bytes[i] = 0x55;
            }

            var str = Native.c4blob_keyToString(key1);
            str.Should().Be("sha1-VVVVVVVVVVVVVVVVVVVVVVVVVVU=", "because otherwise the parse failed");

            var key2 = new C4BlobKey();
            Native.c4blob_keyFromString(str, &key2).Should().BeTrue("because the key should survive a round trip");
            for(int i = 0; i < C4BlobKey.Size; i++) {
                key1.bytes[i].Should().Be(key2.bytes[i], "because the two keys should have equal bytes");
            }
        }

        [Fact]
        public void TestParseInvalidBlobKeys()
        {
            NativePrivate.c4log_warnOnErrors(false);
            C4BlobKey key2;
            foreach(var invalid in new[] { "", "rot13-xxxx", "sha1-", "sha1-VVVVVVVVVVVVVVVVVVVVVV", "sha1-VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVU" }) {
                Native.c4blob_keyFromString(invalid, &key2).Should().BeFalse($"because '{invalid}' is an invalid string");
            }

            NativePrivate.c4log_warnOnErrors(true);
        }

        [Fact]
        public void TestReadBlobWithStream()
        {
            RunTestVariants(() => {
                var blob = Encoding.UTF8.GetBytes("This is a blob to store in the store!");

                // Add blob to the store:
                var key = new C4BlobKey();
                LiteCoreBridge.Check(err => {
                    var localKey = key;
                    var retVal = Native.c4blob_create(_store, blob, null, &localKey, err);
                    key = localKey;
                    return retVal;
                });
                C4Error error;
                ((long)Native.c4blob_openReadStream(_store, _bogusKey, &error)).Should().Be(0, 
                    "because an invalid key should not have a stream");
                var stream = (C4ReadStream *)LiteCoreBridge.Check(err => Native.c4blob_openReadStream(_store, 
                    key, err));
                Native.c4stream_getLength(stream, &error).Should().Be(blob.Length, 
                    "because the stream should know its own length");
                
                // Read it back, 6 bytes at a time:
                var buffer = new byte[6];
                var readBack = new List<byte>();
                ulong bytesRead = 0;
                do {
                    bytesRead = Native.c4stream_read(stream, buffer, &error);
                    bytesRead.Should().BeGreaterThan(0, "because there should be new bytes");
                    readBack.AddRange(buffer.Take((int)bytesRead));
                } while(bytesRead == (ulong)buffer.Length);
                error.code.Should().Be(0, "because otherwise an error occurred");
                readBack.Should().Equal(blob, "because the data should persist correctly");

                // Try seeking:
                LiteCoreBridge.Check(err => Native.c4stream_seek(stream, 10, err));
                Native.c4stream_read(stream, buffer, 4, &error).Should().Be(4, "because reading should succeed after seeking");

                Native.c4stream_close(stream);
                Native.c4stream_close(null); // This should be a no-op, not a crash
            });
        }

        [Fact]
        public void TestWriteBlobWithStream()
        {
            RunTestVariants(() => {
                // Write the blob:
                var stream = (C4WriteStream *)LiteCoreBridge.Check(err => Native.c4blob_openWriteStream(_store, err));
                for(int i = 0; i < 1000; i++) {
                    var buf = Encoding.UTF8.GetBytes($"This is line {i:D3}.\n");
                    LiteCoreBridge.Check(err => Native.c4stream_write(stream, buf, err));
                }

                // Get the blob key, and install it:
                var key = Native.c4stream_computeBlobKey(stream);
                LiteCoreBridge.Check(err => Native.c4stream_install(stream, null, err));
                Native.c4stream_closeWriter(stream);
                Native.c4stream_closeWriter(null); // Just for fun, to make sure it doesn't crash

                var keyStr = Native.c4blob_keyToString(key);
                keyStr.Should().Be("sha1-0htkjBHcrTyIk9K8e1zZq47yWxw=", "because the data should hash correctly");
                
                // Read it back using the key:
                C4Error error;
                var contents = Native.c4blob_getContents(_store, key, &error);
                contents.Should().HaveCount(18000, "because the data should all be read");
                
                // Read it back random-access
                var reader = (C4ReadStream *)LiteCoreBridge.Check(err => Native.c4blob_openReadStream(_store, 
                    key, err));
                const int increment = 3*3*3*3;
                int line = increment;
                for(ulong i = 0; i < 1000; i++) {
                    line = (line + increment) % 1000;
                    WriteLine($"Reading line {line} at offset {18*line}");
                    var buf = Encoding.UTF8.GetBytes($"This is line {line:D3}.\n");
                    var readBuf = new byte[18];
                    LiteCoreBridge.Check(err => Native.c4stream_seek(reader, (ulong)(18*line), err));
                    Native.c4stream_read(reader, readBuf, &error).Should().Be(18, "because 18 bytes were requested");
                    Encoding.UTF8.GetString(readBuf).Should().Be(Encoding.UTF8.GetString(buf), "because the lines should match");
                }

                Native.c4stream_close(reader);
            });
        }

        [Fact]
        public void TestWriteManyBlobSizes()
        {
            var sizes = new[] { 0, 1, 15, 16, 17, 4095, 4096, 4097, 4096+15, 
                4096+16, 4096+17, 8191, 8192, 8193 };
            var chars = Encoding.UTF8.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXY");

            // The interesting sizes for encrypted blobs are right around the file block size (4096)
            // and the cipher block size (16).
            RunTestVariants(() => {
                foreach(var size in sizes) {
                    WriteLine($"Testing {size}-byte blob");
                    // Write the blob:
                    var stream = (C4WriteStream *)LiteCoreBridge.Check(err => Native.c4blob_openWriteStream(_store, err));
                    for(int i = 0; i < size; i++) {
                        var c = i % chars.Length;
                        LiteCoreBridge.Check(err => Native.c4stream_write(stream, new[] { chars[c] }, err));
                    }

                    // Get the blob key, and install it:
                    var key = Native.c4stream_computeBlobKey(stream);
                    LiteCoreBridge.Check(err => Native.c4stream_install(stream, null, err));
                    Native.c4stream_closeWriter(stream);

                    // Read it back using the key:
                    C4Error error;
                    var contents = Native.c4blob_getContents(_store, key, &error);
                    contents.Should().HaveCount(size, "because that was the size that was stored");
                    for(int i = 0; i < size; i++) {
                        contents[i].Should().Be(chars[i % chars.Length], "because the data should not change");
                    }
                }
            });
        }
    }
}