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
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Doc;

using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public sealed class BlobTest : TestCase
    {
        public BlobTest(ITestOutputHelper output) : base(output)
        {

        }

        [Fact]
        public void TestGetContent()
        {
            byte[] bytes = GetFileByteArray("attachment.png", typeof(BlobTest));
            var blob = new Blob("image/png", bytes);
            using (var mDoc = new MutableDocument("doc1")) {
                mDoc.SetBlob("blob", blob);
                DefaultCollection.Save(mDoc);
                using (var doc = DefaultCollection.GetDocument(mDoc.Id)) {
                    doc.Should().NotBeNull("because it was just saved a few lines ago");
                    var savedBlob = doc!.GetBlob("blob");
                    savedBlob.Should().NotBeNull();
                    savedBlob!.ContentType.Should().Be("image/png");
                    savedBlob.Content.Should().Equal(bytes);
                }
            }
        }

        [ForIssue("couchbase-lite-android/1438")]
        [Fact]
        public void TestGetContent6MBFile()
        {
            byte[] bytes = GetFileByteArray("iTunesMusicLibrary.json", typeof(BlobTest));
            var blob = new Blob("application/json", bytes);
            using (var mDoc = new MutableDocument("doc1")) {
                mDoc.SetBlob("blob", blob);
                DefaultCollection.Save(mDoc);
                using (var doc = DefaultCollection.GetDocument(mDoc.Id)) {
                    doc.Should().NotBeNull("because it was just saved a few lines ago");
                    var savedBlob = doc!.GetBlob("blob");
                    savedBlob.Should().NotBeNull();
                    savedBlob!.ContentType.Should().Be("application/json");
                    savedBlob.Content.Should().Equal(bytes);
                }
            }
        }

        [Fact]
        public unsafe void TestBlobStream()
        {
            byte[] bytes = GetFileByteArray("iTunesMusicLibrary.json", typeof(BlobTest));
            C4BlobKey key;
            using (var stream = new BlobWriteStream(Db.BlobStore)) {
                stream.CanSeek.Should().BeFalse();
                stream.Invoking(s => s.Position = 10).Should().Throw<NotSupportedException>();
                stream.Invoking(s => s.Read(bytes, 0, bytes.Length)).Should().Throw<NotSupportedException>();
                stream.Invoking(s => s.SetLength(100)).Should().Throw<NotSupportedException>();
                stream.Write(bytes, 0, bytes.Length);
                stream.Length.Should().Be(bytes.Length);
                stream.Position.Should().Be(bytes.Length);
                stream.Flush();
                key = stream.Key;
            }

            using (var stream = new BlobReadStream(Db.BlobStore, key)) {
                stream.Invoking(s => s.SetLength(100)).Should().Throw<NotSupportedException>();
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
                stream.Flush();
            }
        }

        [Fact]
        public unsafe void TestBlobStreamCopyTo()
        {
            byte[] bytes = GetFileByteArray("iTunesMusicLibrary.json", typeof(BlobTest));
            C4BlobKey key;
            #if NET6_0_WINDOWS10 || NET_ANDROID || NET_APPLE
            using (var stream = FileSystem.OpenAppPackageFileAsync("iTunesMusicLibrary.json").Result) {
            #else
            using (var stream = typeof(BlobTest).GetTypeInfo().Assembly
                .GetManifestResourceStream("iTunesMusicLibrary.json")) {
#endif
                stream.Should().NotBeNull("because otherwise the test data source is missing");
                using (var writeStream = new BlobWriteStream(Db.BlobStore)) {
                    stream!.CopyTo(writeStream);
                    writeStream.Flush();
                    key = writeStream.Key;
                }
            }

            using (var stream = new BlobReadStream(Db.BlobStore, key)) {
                stream.Length.Should().Be(bytes.Length);
            }
        }

        [Fact]
        public void TestBlobToJSON()
        {
            var blob = ArrayTestBlob();
            Action badAction = (() => blob.ToJSON());
            badAction.Should().Throw<InvalidOperationException>(CouchbaseLiteErrorMessage.MissingDigestDueToBlobIsNotSavedToDB);

            Db.SaveBlob(blob);

            var json = blob.ToJSON();

            var blobFromJStr = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json);
            blobFromJStr.Should().NotBeNull("because otherwise the blob JSON was invalid");
            blob.JSONEquals(blobFromJStr!).Should().BeTrue();
        }

        [Fact]
        public void TestGetBlobFromDB()
        {
            var blob = ArrayTestBlob();
            Db.SaveBlob(blob);

            var blobDict = new Dictionary<string, object?>() {
                { Blob.ContentTypeKey, blob.ContentType },
                { Blob.DigestKey, blob.Digest},
                { Blob.LengthKey, blob.Length},
                { Constants.ObjectTypeProperty, "blob" }
            };

            var blobFromDict = Db.GetBlob(blobDict);
            blob.Equals(blobFromDict).Should().BeTrue();

            //At this point Constants.ObjectTypeProperty key value pair is removed from the blobDict
            Action badAction = (() => Db.GetBlob(blobDict));
            badAction.Should().Throw<ArgumentException>(CouchbaseLiteErrorMessage.InvalidJSONDictionaryForBlob);

            //Add back Constants.ObjectTypeProperty key value pair
            blobDict.Add(Constants.ObjectTypeProperty, "blob");
            blobDict.Remove(Blob.DigestKey);
            blobFromDict = Db.GetBlob(blobDict);
            blobFromDict.Should().BeNull();
        }

        [Fact]
        public void TestDocumentBlobToJSON()
        {
            var blob = ArrayTestBlob();
            using (var md = new MutableDocument("doc1")) {
                md.SetBlob("blob", blob);
                DefaultCollection.Save(md);
            }

            using(var d = DefaultCollection.GetDocument("doc1")) {
                d.Should().NotBeNull("because it was just saved a few lines ago");
                var b = d!.GetBlob("blob");
                b.Should().NotBeNull("because it was saved into the document");
                var json = b!.ToJSON();
                var blobFromJson = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json);
                blobFromJson.Should().NotBeNull("because otherwise the blob JSON was invalid");
                blobFromJson.Should().ContainKeys(Blob.ContentTypeKey, Blob.DigestKey, Blob.LengthKey, Constants.ObjectTypeProperty);
            }
        }

        [Fact]
        public void TestGetBlobFromCompactDB()
        {
            var blob = ArrayTestBlob();
            Db.SaveBlob(blob);
            Db.PerformMaintenance(MaintenanceType.Compact);

            var blobDict = new Dictionary<string, object?>() {
                { Blob.ContentTypeKey, blob.ContentType },
                { Blob.DigestKey, blob.Digest},
                { Blob.LengthKey, blob.Length},
                { Constants.ObjectTypeProperty, "blob" }
            };

            var blobFromDict = Db.GetBlob(blobDict);
            blobFromDict.Should().BeNull();
        }

        [Fact]
        public void TestDbBlobToJson()
        {
            var blob = ArrayTestBlob();
            Db.SaveBlob(blob);

            var bjson = blob.ToJSON();

            JObject o = JObject.Parse(bjson);
            var mdictFromJObj = o.ToObject<Dictionary<string, object>>();

            using (var cbDoc = new MutableDocument("doc1")) {
                cbDoc.SetValue("dict", mdictFromJObj);
                DefaultCollection.Save(cbDoc);
            }

            var gotBlob = DefaultCollection.GetDocument("doc1")?.GetValue("dict");
            gotBlob.Should().BeOfType<Blob>();
            var newJson = ((Blob?)gotBlob)!.ToJSON();

            var bjsonD = JsonConvert.DeserializeObject<Dictionary<string, object>>(bjson);
            bjsonD.Should().NotBeNull("because otherwise the presave blob JSON was invalid");
            var newJsonD = JsonConvert.DeserializeObject<Dictionary<string, object>>(newJson);
            newJsonD.Should().NotBeNull("because otherwise the postsave blob JSON was invalid");

            foreach (var kv in bjsonD!) {
                newJsonD![kv.Key].ToString().Should().Be(kv.Value.ToString());
            }
        }

        [Fact]
        public void TestAccessContentFromBlobCreatedFromJson()
        {
            var blob = ArrayTestBlob();
            Db.SaveBlob(blob);
            var blobDict = 
                new List<object> {
                new Dictionary<string, object?>() {
                { Blob.ContentTypeKey, blob.ContentType },
                { Blob.DigestKey, blob.Digest},
                { Blob.LengthKey, blob.Length},
                { Constants.ObjectTypeProperty, "blob" }
            }};

            var listContainsBlobJson = JsonConvert.SerializeObject(blobDict);
            using (var md = new MutableDocument("doc1")) {
                var ma = new MutableArrayObject(listContainsBlobJson);
                var blobInMa = (MutableDictionaryObject?)ma.GetValue(0);
                blobInMa.Should().NotBeNull("beacuse otherwise the saved value was corrupted");
                var blobInMD = new Blob(blobInMa!.ToDictionary());
                blobInMD.Content.Should().BeNull(CouchbaseLiteErrorMessage.BlobDbNull);
            }
        }

        [Fact]
        public void TestBlobJsonStringInLayersOfMutableDict()
        {
            var blob = ArrayTestBlob();
            Db.SaveBlob(blob); 
            var nestedBlob = new Blob("text/plain", Encoding.UTF8.GetBytes("abcde"));
            Db.SaveBlob(nestedBlob);
            var b1 = new Blob("text/plain", Encoding.UTF8.GetBytes("alpha"));
            Db.SaveBlob(b1);
            var b2 = new Blob("text/plain", Encoding.UTF8.GetBytes("beta"));
            Db.SaveBlob(b2);
            var b3 = new Blob("text/plain", Encoding.UTF8.GetBytes("omega"));
            Db.SaveBlob(b3);

            var KeyValueDictionary = new Dictionary<string, object>()
            {
                { "blob", blob },
                { "blobUnderDict", new Dictionary<string, object>() { { "nestedBlob" , nestedBlob } } },
                { "blobUnderArr", new List<object>() { b1, b2, b3 } }
            };

            var dicJson = JsonConvert.SerializeObject(KeyValueDictionary);
            var md = new MutableDictionaryObject(dicJson);
            using (var mdoc = new MutableDocument("doc1")) {
                mdoc.SetDictionary("dict", md);
                DefaultCollection.Save(mdoc);
            }

            var dic = DefaultCollection.GetDocument("doc1")?.GetDictionary("dict");
            dic.Should().NotBeNull("because it was just saved into the database");

            var blob1 = dic!.GetBlob("blob");
            blob1.Should().NotBeNull("because it was saved as part of the document");
            blob1!.Content.Should().NotBeNull();
            blob1.Should().BeEquivalentTo(KeyValueDictionary["blob"]);

            var blob2 = dic.GetDictionary("blobUnderDict")?.GetBlob("nestedBlob");
            blob2.Should().NotBeNull("because it was saved nested in the document");
            blob2!.Content.Should().NotBeNull();
            var d = (Dictionary<string, object?>) KeyValueDictionary["blobUnderDict"];
            blob2.Should().BeEquivalentTo(d["nestedBlob"]);

            var blobs = dic.GetArray("blobUnderArr");
            blobs.Should().NotBeNull("because it was saved inside the document array");
            var cnt = blobs!.Count;
            var blobList = (List<object>)KeyValueDictionary["blobUnderArr"];
            for(int i=0; i < cnt; i++) {
                var b = blobs.GetBlob(i);
                b?.Content.Should().NotBeNull();
                b.Should().BeEquivalentTo(blobList[i]);
            }
        }
    }
}
