//
// BlobStoreWriterTest.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
/*
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Couchbase.Lite.Store;
using Couchbase.Lite.Tests;
using FluentAssertions;
using NUnit.Framework;

namespace Couchbase.Lite
{
   [TestFixture(false)]
   [TestFixture(true)]
    public class BlobStoreWriterTest
    {
        private BlobStore _store;
        private string _storePath;
        private bool _encrypt;

        public BlobStoreWriterTest(bool encrypt)
        {
            _encrypt = encrypt;
        }

        private void Verify(BlobKey attKey, byte[] clearText)
        {
            var path = _store.RawPathForKey(attKey);
            var raw = File.ReadAllBytes(path);
            if(_encrypt) {
                raw.Should().NotBeNull()
                    .And.Match(x => x.Locate(clearText) == -1, "because encrypted contents should not contain cleartext");
            } else {
                raw.Should().NotBeNull()
                        .And.Equal(clearText, "because the contents should serialize to disk correctly");
            }
        }

        [SetUp]
        public void Setup()
        {
            _storePath = Path.GetTempPath();
            _storePath = Path.Combine(_storePath, "CBL_BlobStore");
            if(Directory.Exists(_storePath)) {
                Directory.Delete(_storePath, true);
            }

            if(_encrypt) {
                Trace.WriteLine("----- Now enabling attachment encryption ----");
            }

            _store = new BlobStore(_storePath, _encrypt ? new SymmetricKey() : null);
            var encryptionMarkerPath = Path.Combine(_storePath, BlobStore.EncryptionMarkerFilename);
            var markerExists = File.Exists(encryptionMarkerPath);
            markerExists.Should().Be(_encrypt);
        }

        [TearDown]
        public void TearDown()
        {
            _store = null;
            Directory.Delete(_storePath, true);
        }

        [Test]
        public void TestBasicOperation()
        {
            var item = Encoding.UTF8.GetBytes("this is an item");
            var key = new BlobKey();
            var key2 = new BlobKey();
            _store.StoreBlob(item, key);
            _store.StoreBlob(item, key2);
            key.Should().Be(key2, "because keys should be deterministic");

            var readItem = _store.BlobForKey(key);
            readItem.Should().Equal(item, "beacuse the contents of a key should be read back correctly from disk");
            Verify(key, item);

            var path = _store.PathForKey(key);
            _encrypt.Should().Be(path == null, "because only plaintext attachments should return a path");
        }

        [Test]
        public void TestReopen()
        {
            var item = Encoding.UTF8.GetBytes("this is an item");
            var key = new BlobKey();
            _store.StoreBlob(item, key);

            var store2 = new BlobStore(_storePath, _store.EncryptionKey);
            var readItem = store2.BlobForKey(key);
            readItem.Should().Equal(item, "because the contents of a key should be the same in the second store");

            readItem = _store.BlobForKey(key);
            readItem.Should().Equal(item, "because the contents of a key should be the same in the first store");
            Verify(key, item);
        }

        [Test]
        public void TestBlobStoreWriter()
        {
            var writer = new BlobStoreWriter(_store);
            writer.AppendData(Encoding.UTF8.GetBytes("part 1, "));
            writer.AppendData(Encoding.UTF8.GetBytes("part 2, "));
            writer.AppendData(Encoding.UTF8.GetBytes("part 3"));
            writer.Finish();
            writer.Install();

            var expectedData = Encoding.UTF8.GetBytes("part 1, part 2, part 3");
            var readItem = _store.BlobForKey(writer.GetBlobKey());
            readItem.Should().Equal(expectedData, "because the writer should correctly write contents to disk");
            Verify(writer.GetBlobKey(), expectedData);
        }

        [Test]
        public void TestRekey()
        {
            var item = Encoding.UTF8.GetBytes("this is an item");
            var key = new BlobKey();
            _store.StoreBlob(item, key);

            var newEncryptionKey = new SymmetricKey();
            var addOrChange = _encrypt ? "Changing" : "Adding";
            Trace.WriteLine($"---- {addOrChange} key");
            _store.ChangeEncryptionKey(newEncryptionKey);
            var oldEncrypt = _encrypt;
            _store.EncryptionKey.Should().Be(newEncryptionKey, "because the key should have become the new one");
            _store.BlobForKey(key).Should().Equal(item, "because the content should be the same regardless of encryption");

            _encrypt = true;
            TestReopen();
            _encrypt = oldEncrypt;

            if(_encrypt) {
                Trace.WriteLine("---- Removing key");
                _store.ChangeEncryptionKey(null);
                _store.EncryptionKey.Should().BeNull("because the encryption was removed");
                _store.BlobForKey(key).Should().Equal(item, "because the content should be the same regardless of encryption");
                _encrypt = false;
                TestReopen();
            }
        }
    }
}
