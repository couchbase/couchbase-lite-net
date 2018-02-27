//
//  DatabaseEncryptionTest.cs
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
#if COUCHBASE_ENTERPRISE_FUTURE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
using System.Linq;
using Couchbase.Lite.Query;
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
    public class DatabaseEncryptionTest : TestCase
    {

#if !WINDOWS_UWP
        public DatabaseEncryptionTest(ITestOutputHelper output) : base(output)
#else
        public DatabaseEncryptionTest()
#endif
        {

        }

        [Fact]
        public void TestUnEncryptedDatabase()
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = OpenSeekrit(null)) {
                using (var doc = new MutableDocument(new Dictionary<string, object>
                    { ["answer"] = 42 })) {
                    seekrit.Save(doc).Dispose();
                }
            }

            this.Invoking(t => OpenSeekrit("wrong")).ShouldThrow<CouchbaseLiteException>().Which.Status.Should()
                .Be(StatusCode.Unauthorized);

            using (var seekrit = OpenSeekrit(null)) {
                seekrit.Count.Should().Be(1UL);
            }
        }

        [Fact]
        public void TestEncryptedDatabase()
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = OpenSeekrit("letmein")) {
                using (var doc = new MutableDocument(new Dictionary<string, object>
                    { ["answer"] = 42 })) {
                    seekrit.Save(doc).Dispose();
                }
            }

            this.Invoking(t => OpenSeekrit(null)).ShouldThrow<CouchbaseLiteException>().Which.Status.Should()
                .Be(StatusCode.Unauthorized);
            this.Invoking(t => OpenSeekrit("wrong")).ShouldThrow<CouchbaseLiteException>().Which.Status.Should()
                .Be(StatusCode.Unauthorized);

            using (var seekrit = OpenSeekrit("letmein")) {
                seekrit.Count.Should().Be(1UL);
            }
        }

        [Fact]
        public void TestDeleteEncryptedDatabase()
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = OpenSeekrit("letmein")) {
                seekrit.Delete();
            }

            // Recreate
            using (var seekrit = OpenSeekrit(null)) {
                seekrit.Count.Should().Be(0UL);
            }

            // Reopen
            using (var seekrit = OpenSeekrit(null)) {
                seekrit.Count.Should().Be(0UL);
            }

            this.Invoking(t => OpenSeekrit("letmein")).ShouldThrow<CouchbaseLiteException>().Which.Status.Should()
                .Be(StatusCode.Unauthorized);
        }

        [Fact]
        public void TestCompactEncryptedDatabase()
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = OpenSeekrit("letmein")) {

                using (var doc = new MutableDocument(new Dictionary<string, object>
                    { ["answer"] = 42 })) {
                    using (var doc2 = seekrit.Save(doc).ToMutable()) {
                        doc2.SetInt("answer", 84);

                        seekrit.Compact();
                    
                        doc2.SetInt("answer", 85);
                        using (var doc3 = seekrit.Save(doc2).ToMutable()) {
                            seekrit.Save(doc3).Dispose();
                        }
                    }
                }
            }

            using (var seekrit = OpenSeekrit("letmein")) {
                seekrit.Count.Should().Be(1UL);
            }
        }

        [Fact]
        public void TestEncryptedBlobs()
        {
            Database.Delete("seekrit", Directory);
            TestEncryptedBlobsInternal("letmein").Dispose();
        }

        [Fact]
        public void TestMultipleDatabases()
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = OpenSeekrit("seekrit")) {

                // Get another instance (ensure no exception)
                var seekrit2 = OpenSeekrit("seekrit");
                seekrit2.Dispose();

                // No throw
                var newKey = new EncryptionKey("foobar");
                seekrit.SetEncryptionKey(newKey);
            }
        }

        [Fact]
        public void TestAddKey() => Rekey(null, "letmein");

        [Fact]
        public void TestReKey() => Rekey("letmein", "letmeout");

        [Fact]
        public void TestRemoveKey() => Rekey("letmein", null);

        private void Rekey(string oldPass, string newPass)
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = TestEncryptedBlobsInternal(oldPass)) {

                seekrit.InBatch(() =>
                {
                    for (var i = 0; i < 100; i++) {
                        using (var doc = new MutableDocument(new Dictionary<string, object>
                            { ["seq"] = i })) {
                            seekrit.Save(doc).Dispose();
                        }
                    }
                });

                var newKey = newPass != null ? new EncryptionKey(newPass) : null;
                seekrit.SetEncryptionKey(newKey);
            }
            

            using(var seekrit = OpenSeekrit(newPass)) {
                using (var doc = seekrit.GetDocument("att")) {
                    var blob = doc.GetBlob("blob");
                    blob.Digest.Should().NotBeNull();
                    var content = Encoding.UTF8.GetString(blob.Content);
                    content.Should().Be("This is a blob!");
                }

                using (var q = QueryBuilder.Select(SelectResult.Property("seq"))
                    .From(DataSource.Database(seekrit))
                    .Where(Expression.Property("seq").NotNullOrMissing())
                    .OrderBy(Ordering.Property("seq"))) {
                    var rs = q.Execute();
                    var i = 0;
                    foreach (var r in rs) {
                        r.GetInt(0).Should().Be(i++);
                    }

                    i.Should().Be(100);
                }
            }
        }

        private Database TestEncryptedBlobsInternal(string password)
        {
            var seekrit = OpenSeekrit(password);
            var body = Encoding.UTF8.GetBytes("This is a blob!");
            var blob = new Blob("text/plain", body);

            using (var doc = new MutableDocument("att")) {
                doc.SetBlob("blob", blob);
                seekrit.Save(doc);

                blob = doc.GetBlob("blob");
                blob.Digest.Should().NotBeNull();

                var fileName = blob.Digest.Substring(5).Replace("/", "_");
                var path = $"{seekrit.Path}Attachments{Path.DirectorySeparatorChar}{fileName}.blob";
                var raw = File.ReadAllBytes(path);
                if (password != null) {
                    raw.Should().NotBeEquivalentTo(body, "because otherwise the attachment was not encrypted");
                } else {
                    raw.Should().BeEquivalentTo(body, "because otherwise the attachment was encrypted");
                }
            }

            using (var savedDoc = seekrit.GetDocument("att")) {
                blob = savedDoc.GetBlob("blob");
                blob.Digest.Should().NotBeNull();
                var content = Encoding.UTF8.GetString(blob.Content);
                content.Should().Be("This is a blob!");
            }

            return seekrit;
        }

        private Database OpenSeekrit(string password)
        {
            var config = new DatabaseConfiguration
            {
                EncryptionKey = password != null ? new EncryptionKey(password) : null,
                Directory = Directory
            };

            return new Database("seekrit", config);
        }
    }
}

#endif