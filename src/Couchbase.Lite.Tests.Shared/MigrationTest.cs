//
//  MigrationTest.cs
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

#if !CBL_NO_EXTERN_FILES
using Couchbase.Lite;
using Shouldly;
using System.IO.Compression;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Test;

public sealed class MigrationTest(ITestOutputHelper output) : TestCase(output)
{
    [Fact]
    public void TestOpenExistingDBv1X()
    {
        Database.Delete("android-sqlite", Directory);
        using(var za = new ZipArchive(TestCase.GetTestAsset("replacedb/android140-sqlite.cblite2.zip"))) {
            za.ExtractToDirectory(Directory);
        }

        var config = new DatabaseConfiguration{ Directory = Directory };
        using (var db = new Database("android-sqlite", config)) {
            db.GetDefaultCollection().Count.ShouldBe(2UL);
            for (var i = 1; i < 2; i++) {
                var doc = db.GetDefaultCollection().GetDocument($"doc{i}");
                doc.ShouldNotBeNull();
                doc.GetString("key").ShouldBe(i.ToString());

                var attachments = doc.GetDictionary("_attachments");
                attachments.ShouldNotBeNull();
                var key = $"attach{i}";
                var blob = attachments.GetBlob(key);
                blob.ShouldNotBeNull();
                var attach = Encoding.UTF8.GetBytes(key);
                blob.Content.ShouldBe(attach);
            }
        }

        Database.Delete("android-sqlite", Directory);
    }

    [Fact]
    public void TestOpenExistingDBv1XNoAttachment()
    {
        Database.Delete("android-sqlite", Directory);
        using (var za = new ZipArchive(TestCase.GetTestAsset("replacedb/android140-sqlite-noattachment.cblite2.zip"))) {
            za.ExtractToDirectory(Directory);
        }

        var config = new DatabaseConfiguration { Directory = Directory };
        using (var db = new Database("android-sqlite", config)) {
            db.GetDefaultCollection().Count.ShouldBe(2UL);
            for (var i = 1; i < 2; i++) {
                var doc = db.GetDefaultCollection().GetDocument($"doc{i}");
                doc.ShouldNotBeNull();
                doc.GetString("key").ShouldBe(i.ToString());
            }
        }

        Database.Delete("android-sqlite", Directory);
    }

    [Fact]
    public void TestOpenExistingDB()
    {
        Database.Delete("android-sqlite", Directory);
        using (var za = new ZipArchive(TestCase.GetTestAsset("replacedb/android200-sqlite.cblite2.zip"))) {
            za.ExtractToDirectory(Directory);
        }

        var config = new DatabaseConfiguration { Directory = Directory };
        using (var db = new Database("android-sqlite", config)) {
            db.GetDefaultCollection().Count.ShouldBe(2UL);
            for (var i = 1; i < 2; i++) {
                var doc = db.GetDefaultCollection().GetDocument($"doc{i}");
                doc.ShouldNotBeNull();
                doc.GetString("key").ShouldBe(i.ToString());
                    
                var key = $"attach{i}";
                var blob = doc.GetBlob(key);
                blob.ShouldNotBeNull();
                var attach = Encoding.UTF8.GetBytes(key);
                blob.Content.ShouldBe(attach);
            }
        }

        Database.Delete("android-sqlite", Directory);
    }
}
#endif