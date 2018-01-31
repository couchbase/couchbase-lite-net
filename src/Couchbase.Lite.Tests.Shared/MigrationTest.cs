//
//  MigrationTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using System.IO.Compression;
using System.Text;
using Couchbase.Lite;
using FluentAssertions;
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
    public sealed class MigrationTest : TestCase
    {
#if !WINDOWS_UWP
        public MigrationTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestOpenExistingDBv1x()
        {
            Database.Delete("android-sqlite", Directory);
            using(var za = new ZipArchive(GetTestAsset("replacedb/android140-sqlite.cblite2.zip"))) {
                za.ExtractToDirectory(Directory);
            }

            var config = new DatabaseConfiguration{ Directory = Directory };
            using(var db = new Database("android-sqlite", config)) {
                db.Count.Should().Be(2);
                for (int i = 1; i < 2; i++) {
                    var doc = db.GetDocument($"doc{i}");
                    doc.Should().NotBeNull();
                    doc.GetString("key").Should().Be(i.ToString());

                    var attachments = doc.GetDictionary("_attachments");
                    attachments.Should().NotBeNull();
                    var key = $"attach{i}";
                    var blob = attachments.GetBlob(key);
                    blob.Should().NotBeNull();
                    var attach = Encoding.UTF8.GetBytes(key);
                    blob.Content.Should().Equal(attach);
                }
            }

            Database.Delete("android-sqlite", Directory);
        }

        [Fact]
        public void TestOpenExistingDBv1xNoAttachment()
        {
            Database.Delete("android-sqlite", Directory);
            using (var za = new ZipArchive(GetTestAsset("replacedb/android140-sqlite-noattachment.cblite2.zip"))) {
                za.ExtractToDirectory(Directory);
            }

            var config = new DatabaseConfiguration { Directory = Directory };
            using (var db = new Database("android-sqlite", config)) {
                db.Count.Should().Be(2);
                for (int i = 1; i < 2; i++) {
                    var doc = db.GetDocument($"doc{i}");
                    doc.Should().NotBeNull();
                    doc.GetString("key").Should().Be(i.ToString());
                }
            }

            Database.Delete("android-sqlite", Directory);
        }

        [Fact]
        public void TestOpenExistingDB()
        {
            Database.Delete("android-sqlite", Directory);
            using (var za = new ZipArchive(GetTestAsset("replacedb/android200-sqlite.cblite2.zip"))) {
                za.ExtractToDirectory(Directory);
            }

            var config = new DatabaseConfiguration { Directory = Directory };
            using (var db = new Database("android-sqlite", config)) {
                db.Count.Should().Be(2);
                for (int i = 1; i < 2; i++) {
                    var doc = db.GetDocument($"doc{i}");
                    doc.Should().NotBeNull();
                    doc.GetString("key").Should().Be(i.ToString());
                    
                    var key = $"attach{i}";
                    var blob = doc.GetBlob(key);
                    blob.Should().NotBeNull();
                    var attach = Encoding.UTF8.GetBytes(key);
                    blob.Content.Should().Equal(attach);
                }
            }

            Database.Delete("android-sqlite", Directory);
        }
    }
}
