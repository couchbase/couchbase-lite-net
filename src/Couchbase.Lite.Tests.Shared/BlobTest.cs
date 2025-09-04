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

using Shouldly;
using LiteCore;
using LiteCore.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test;

public sealed class BlobTest(ITestOutputHelper output) : TestCase(output)
{
    [Fact]
    public void TestGetContent()
    {
        var bytes = GetFileByteArray("attachment.png", typeof(BlobTest));
        var blob = new Blob("image/png", bytes);
        using var mDoc = new MutableDocument("doc1");
        mDoc.SetBlob("blob", blob);
        DefaultCollection.Save(mDoc);
        using var doc = DefaultCollection.GetDocument(mDoc.Id);
        doc.ShouldNotBeNull("because it was just saved a few lines ago");
        var savedBlob = doc.GetBlob("blob");
        savedBlob.ShouldNotBeNull();
        savedBlob.ContentType.ShouldBe("image/png");
        savedBlob.Content.ShouldBe(bytes);
    }

    [ForIssue("couchbase-lite-android/1438")]
    [Fact]
    public void TestGetContent6MBFile()
    {
        var bytes = GetFileByteArray("iTunesMusicLibrary.json", typeof(BlobTest));
        var blob = new Blob("application/json", bytes);
        using var mDoc = new MutableDocument("doc1");
        mDoc.SetBlob("blob", blob);
        DefaultCollection.Save(mDoc);
        using var doc = DefaultCollection.GetDocument(mDoc.Id);
        doc.ShouldNotBeNull("because it was just saved a few lines ago");
        var savedBlob = doc.GetBlob("blob");
        savedBlob.ShouldNotBeNull();
        savedBlob.ContentType.ShouldBe("application/json");
        savedBlob.Content.ShouldBe(bytes);
    }

    [Fact]
    public unsafe void TestBlobStream()
    {
        byte[] bytes = GetFileByteArray("iTunesMusicLibrary.json", typeof(BlobTest));
        C4BlobKey key;
        using (var stream = new BlobWriteStream(Db.BlobStore)) {
            stream.CanSeek.ShouldBeFalse();
            Should.Throw<NotSupportedException>(() => stream.Position = 10);
            Should.Throw<NotSupportedException>(() => stream.Read(bytes, 0, bytes.Length));
            Should.Throw<NotSupportedException>(() => stream.SetLength(100));
            stream.Write(bytes, 0, bytes.Length);
            stream.Length.ShouldBe(bytes.Length);
            stream.Position.ShouldBe(bytes.Length);
            stream.Flush();
            key = stream.Key;
        }

        using (var stream = new BlobReadStream(Db.BlobStore, key)) {
            Should.Throw<NotSupportedException>(() => stream.SetLength(100));
            stream.CanRead.ShouldBeTrue();
            stream.CanSeek.ShouldBeTrue();
            stream.CanWrite.ShouldBeFalse();
            stream.Length.ShouldBe(bytes.Length);
            stream.Position.ShouldBe(0);
            stream.Seek(2, SeekOrigin.Begin);
            stream.Position.ShouldBe(2);
            stream.ReadByte().ShouldBe(bytes[2]);
            stream.Seek(1, SeekOrigin.Current);
            stream.Position.ShouldBe(4); // ReadByte advanced the stream
            stream.ReadByte().ShouldBe(bytes[4]);
            stream.Position = 0;
            stream.Seek(-2, SeekOrigin.End);
            stream.Position.ShouldBe(bytes.Length - 2); // ReadByte advanced the stream
            stream.ReadByte().ShouldBe(bytes[^2]);
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
            stream.ShouldNotBeNull("because otherwise the test data source is missing");
            using (var writeStream = new BlobWriteStream(Db.BlobStore)) {
                stream.CopyTo(writeStream);
                writeStream.Flush();
                key = writeStream.Key;
            }
        }

        using (var stream = new BlobReadStream(Db.BlobStore, key)) {
            stream.Length.ShouldBe(bytes.Length);
        }
    }

    [Fact]
    public void TestBlobToJSON()
    {
        var blob = ArrayTestBlob();
        var ex = Should.Throw<InvalidOperationException>(() => blob.ToJSON());
        ex.Message.ShouldBe(CouchbaseLiteErrorMessage.MissingDigestDueToBlobIsNotSavedToDB);

        Db.SaveBlob(blob);

        var json = blob.ToJSON();

        var blobFromJStr = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json);
        blobFromJStr.ShouldNotBeNull("because otherwise the blob JSON was invalid");
        blob.JSONEquals(blobFromJStr!).ShouldBeTrue();
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
        blob.Equals(blobFromDict).ShouldBeTrue();

        //At this point Constants.ObjectTypeProperty key value pair is removed from the blobDict
        var ex = Should.Throw<ArgumentException>(() => Db.GetBlob(blobDict));
        ex.Message.ShouldBe(CouchbaseLiteErrorMessage.InvalidJSONDictionaryForBlob);

        //Add back Constants.ObjectTypeProperty key value pair
        blobDict.Add(Constants.ObjectTypeProperty, "blob");
        blobDict.Remove(Blob.DigestKey);
        blobFromDict = Db.GetBlob(blobDict);
        blobFromDict.ShouldBeNull();
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
            d.ShouldNotBeNull("because it was just saved a few lines ago");
            var b = d.GetBlob("blob");
            b.ShouldNotBeNull("because it was saved into the document");
            var json = b.ToJSON();
            var blobFromJson = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json);
            blobFromJson.ShouldNotBeNull("because otherwise the blob JSON was invalid");
            blobFromJson.ShouldContainKeys(Blob.ContentTypeKey, Blob.DigestKey, Blob.LengthKey, Constants.ObjectTypeProperty);
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
        blobFromDict.ShouldBeNull();
    }

    [Fact]
    public void TestDbBlobToJson()
    {
        var blob = ArrayTestBlob();
        Db.SaveBlob(blob);

        var blobJson = blob.ToJSON();

        var o = JObject.Parse(blobJson);
        var mDictFromJObj = o.ToObject<Dictionary<string, object>>();

        using (var cbDoc = new MutableDocument("doc1")) {
            cbDoc.SetValue("dict", mDictFromJObj);
            DefaultCollection.Save(cbDoc);
        }

        var gotBlob = DefaultCollection.GetDocument("doc1")?.GetValue("dict") as Blob;
        gotBlob.ShouldNotBeNull();
        var newJson = gotBlob.ToJSON();

        var blobJsonD = JsonConvert.DeserializeObject<Dictionary<string, object>>(blobJson);
        blobJsonD.ShouldNotBeNull("because otherwise the pre-save blob JSON was invalid");
        var newJsonD = JsonConvert.DeserializeObject<Dictionary<string, object>>(newJson);
        newJsonD.ShouldNotBeNull("because otherwise the post-save blob JSON was invalid");

        foreach (var kv in blobJsonD) {
            newJsonD[kv.Key].ToString().ShouldBe(kv.Value.ToString());
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
        using var md = new MutableDocument("doc1");
        var ma = new MutableArrayObject(listContainsBlobJson);
        var blobInMa = (MutableDictionaryObject?)ma.GetValue(0);
        blobInMa.ShouldNotBeNull("because otherwise the saved value was corrupted");
        var blobInMd = new Blob(blobInMa.ToDictionary());
        blobInMd.Content.ShouldBeNull(CouchbaseLiteErrorMessage.BlobDbNull);
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

        var keyValueDictionary = new Dictionary<string, object>()
        {
            { "blob", blob },
            { "blobUnderDict", new Dictionary<string, object>() { { "nestedBlob" , nestedBlob } } },
            { "blobUnderArr", new List<object>() { b1, b2, b3 } }
        };

        var dicJson = JsonConvert.SerializeObject(keyValueDictionary);
        var md = new MutableDictionaryObject(dicJson);
        using (var mdoc = new MutableDocument("doc1")) {
            mdoc.SetDictionary("dict", md);
            DefaultCollection.Save(mdoc);
        }

        var dic = DefaultCollection.GetDocument("doc1")?.GetDictionary("dict");
        dic.ShouldNotBeNull("because it was just saved into the database");

        var blob1 = dic.GetBlob("blob");
        blob1.ShouldNotBeNull("because it was saved as part of the document");
        blob1.Content.ShouldNotBeNull();
        blob1.ShouldBe(keyValueDictionary["blob"]);

        var blob2 = dic.GetDictionary("blobUnderDict")?.GetBlob("nestedBlob");
        blob2.ShouldNotBeNull("because it was saved nested in the document");
        blob2.Content.ShouldNotBeNull();
        var d = (Dictionary<string, object?>) keyValueDictionary["blobUnderDict"];
        blob2.ShouldBe(d["nestedBlob"]);

        var blobs = dic.GetArray("blobUnderArr");
        blobs.ShouldNotBeNull("because it was saved inside the document array");
        var cnt = blobs.Count;
        var blobList = (List<object>)keyValueDictionary["blobUnderArr"];
        for(var i=0; i < cnt; i++) {
            var b = blobs.GetBlob(i);
            b?.Content.ShouldNotBeNull();
            b.ShouldBe(blobList[i]);
        }
    }
}