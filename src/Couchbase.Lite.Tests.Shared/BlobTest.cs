//
//  BlobTest.cs
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
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class BlobTest : TestCase
    {
        #if !WINDOWS_UWP

        public BlobTest(ITestOutputHelper output) : base(output)
        {

        }

        #endif

        [Fact]
        public void TestGetContent()
        {
            byte[] bytes = null;
            using (var stream = typeof(BlobTest).GetTypeInfo().Assembly.GetManifestResourceStream("attachment.png"))
            using (var sr = new BinaryReader(stream)) {
                bytes = sr.ReadBytes((int)stream.Length);
            }

            var blob = new Blob("image/png", bytes);
            using (var mDoc = new MutableDocument("doc1")) {
                mDoc.SetBlob("blob", blob);
                using (var doc = Db.Save(mDoc)) {
                    var savedBlob = doc.GetBlob("blob");
                    savedBlob.Should().NotBeNull();
                    savedBlob.ContentType.Should().Be("image/png");
                    savedBlob.Content.Should().Equal(bytes);
                }
            }
        }

        [ForIssue("couchbase-lite-android/1438")]
        [Fact]
        public void TestGetContent6MBFile()
        {
            byte[] bytes = null;
            using (var stream = typeof(BlobTest).GetTypeInfo().Assembly.GetManifestResourceStream("iTunesMusicLibrary.json"))
            using (var sr = new BinaryReader(stream)) {
                bytes = sr.ReadBytes((int)stream.Length);
            }

            var blob = new Blob("application/json", bytes);
            using (var mDoc = new MutableDocument("doc1")) {
                mDoc.SetBlob("blob", blob);
                using (var doc = Db.Save(mDoc)) {
                    var savedBlob = doc.GetBlob("blob");
                    savedBlob.Should().NotBeNull();
                    savedBlob.ContentType.Should().Be("application/json");
                    savedBlob.Content.Should().Equal(bytes);
                }
            }
        }

        [Fact]
        public unsafe void TestBlobStream()
        {
            byte[] bytes = null;
            using (var stream = typeof(BlobTest).GetTypeInfo().Assembly.GetManifestResourceStream("iTunesMusicLibrary.json"))
            using (var sr = new BinaryReader(stream)) {
                bytes = sr.ReadBytes((int)stream.Length);
            }

            C4BlobKey key;
            long tmp;
            using (var stream = new BlobWriteStream(Db.BlobStore)) {
                stream.CanSeek.Should().BeFalse();
                stream.Invoking(s => tmp = s.Length).ShouldThrow<NotSupportedException>();
                stream.Invoking(s => tmp = s.Position).ShouldThrow<NotSupportedException>();
                stream.Invoking(s => s.Position = 10).ShouldThrow<NotSupportedException>();
                stream.Invoking(s => s.Read(bytes, 0, bytes.Length)).ShouldThrow<NotSupportedException>();
                stream.Invoking(s => s.SetLength(100)).ShouldThrow<NotSupportedException>();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
                key = stream.Key;
            }

            using (var stream = new BlobReadStream(Db.BlobStore, key)) {
                stream.Invoking(s => s.SetLength(100)).ShouldThrow<NotSupportedException>();
                stream.CanRead.Should().BeTrue();
                stream.CanSeek.Should().BeTrue();
                stream.CanWrite.Should().BeFalse();
                stream.Length.Should().Be(bytes.Length);
                stream.Position.Should().Be(0);
                stream.Seek(2, SeekOrigin.Begin);
                stream.Position.Should().Be(2);
                stream.ReadByte().Should().Be(bytes[2]);
                stream.Seek(1, SeekOrigin.Current);
                stream.Position.Should().Be(4); // ReadByte advanced the stream
                stream.ReadByte().Should().Be(bytes[4]);
                stream.Position = 0;
                stream.Seek(-2, SeekOrigin.End);
                stream.Position.Should().Be(bytes.Length - 2); // ReadByte advanced the stream
                stream.ReadByte().Should().Be(bytes[bytes.Length - 2]);
            }
        }
    }
}
