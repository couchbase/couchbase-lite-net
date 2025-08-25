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

#if COUCHBASE_ENTERPRISE
using System.Collections.Generic;
using System.IO;
using System.Text;

using Couchbase.Lite;
using Shouldly;
using Couchbase.Lite.Query;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public class DatabaseEncryptionTest : TestCase
    {
        private static IDictionary<string, EncryptionKey> KeyCache = new Dictionary<string, EncryptionKey>();

        public DatabaseEncryptionTest(ITestOutputHelper output) : base(output)
        {

        }

        [Fact]
        public void TestUnEncryptedDatabase()
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = OpenSeekrit(null)) {
                using (var doc = new MutableDocument(new Dictionary<string, object?>
                    { ["answer"] = 42 })) {
                    seekrit.GetDefaultCollection().Save(doc);
                }
            }

            var ex = Should.Throw<CouchbaseLiteException>(() => OpenSeekrit("wrong"));
            ex.Error.ShouldBe(CouchbaseLiteError.UnreadableDatabase);

            using (var seekrit = OpenSeekrit(null)) {
                seekrit.GetDefaultCollection().Count.ShouldBe(1UL);
            }
        }

        [Fact]
        public void TestEncryptedDatabase()
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = OpenSeekrit("letmein")) {
                using (var doc = new MutableDocument(new Dictionary<string, object?>
                    { ["answer"] = 42 })) {
                    seekrit.GetDefaultCollection().Save(doc);
                }
            }

            var ex = Should.Throw<CouchbaseLiteException>(() => OpenSeekrit(null));
            ex.Error.ShouldBe(CouchbaseLiteError.UnreadableDatabase);
            ex = Should.Throw<CouchbaseLiteException>(() => OpenSeekrit("wrong"));
            ex.Error.ShouldBe(CouchbaseLiteError.UnreadableDatabase);

            using (var seekrit = OpenSeekrit("letmein")) {
                seekrit.GetDefaultCollection().Count.ShouldBe(1UL);
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
                seekrit.GetDefaultCollection().Count.ShouldBe(0UL);
            }

            // Reopen
            using (var seekrit = OpenSeekrit(null)) {
                seekrit.GetDefaultCollection().Count.ShouldBe(0UL);
            }

            var ex = Should.Throw<CouchbaseLiteException>(() => OpenSeekrit("letmein"));
            ex.Error.ShouldBe(CouchbaseLiteError.UnreadableDatabase);
        }

        [Fact]
        public void TestCompactEncryptedDatabase()
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = OpenSeekrit("letmein")) {
                using (var doc = new MutableDocument(new Dictionary<string, object?>
                    { ["answer"] = 42 })) {
                    seekrit.GetDefaultCollection().Save(doc);
                    doc.SetInt("answer", 84);

                    seekrit.PerformMaintenance(MaintenanceType.Compact);

                    doc.SetInt("answer", 85);
                    seekrit.GetDefaultCollection().Save(doc);
                }
            }

            using (var seekrit = OpenSeekrit("letmein")) {
                seekrit.GetDefaultCollection().Count.ShouldBe(1UL);
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
                seekrit.ChangeEncryptionKey(newKey);
            }
        }

        [Fact]
        public void TestChangeEncryptionKeyNSaveDocOnNewDB()
        {
            Database.Delete("master3", Directory);

            var config = new DatabaseConfiguration {
                Directory = Directory
            };

            using (var db1 = new Database("master3", config)) {
                db1.ChangeEncryptionKey(new EncryptionKey("password")); // setting encryption key on the database file, the database file didn't exist yet in this case
                using (MutableDocument saveDoc = db1.GetDefaultCollection().GetDocument("my-doc")?.ToMutable() ?? new MutableDocument("my-doc")) {
                    saveDoc.SetString("prop", "value");
                    db1.GetDefaultCollection().Save(saveDoc);
                }
            }
        }

        [Fact]
        public void TestAddKey() => Rekey(null, "letmein");

        [Fact]
        public void TestReKey() => Rekey("letmein", "letmeout");

        [Fact]
        public void TestRemoveKey() => Rekey("letmein", null);

        private void Rekey(string? oldPass, string? newPass)
        {
            Database.Delete("seekrit", Directory);
            using (var seekrit = TestEncryptedBlobsInternal(oldPass)) {

                seekrit.InBatch(() =>
                {
                    // ReSharper disable AccessToDisposedClosure
                    for (var i = 0; i < 100; i++) {
                        using var doc = new MutableDocument(new Dictionary<string, object?>
                            { ["seq"] = i });
                        seekrit.GetDefaultCollection().Save(doc);
                    }
                    // ReSharper restore AccessToDisposedClosure
                });

                var newKey = newPass != null ? new EncryptionKey(newPass) : null;
                seekrit.ChangeEncryptionKey(newKey);
            }
            

            using(var seekrit = OpenSeekrit(newPass)) {
                using (var doc = seekrit.GetDefaultCollection().GetDocument("att")) {
                    doc.ShouldNotBeNull("because it was saved at the beginning of the test");
                    var blob = doc.GetBlob("blob");
                    blob.ShouldNotBeNull("because the blob was saved to the document");
                    blob.Digest.ShouldNotBeNull("because the blob should have a digest upon save");
                    blob.Content.ShouldNotBeNull("because the blob should not be empty");
                    var content = Encoding.UTF8.GetString(blob.Content!);
                    content.ShouldBe("This is a blob!");
                }

                using (var q = QueryBuilder.Select(SelectResult.Property("seq"))
                    .From(DataSource.Collection(seekrit.GetDefaultCollection()))
                    .Where(Expression.Property("seq").IsValued())
                    .OrderBy(Ordering.Property("seq"))) {
                    var rs = q.Execute();
                    var i = 0;
                    foreach (var r in rs) {
                        r.GetInt(0).ShouldBe(i++);
                    }

                    i.ShouldBe(100);
                }
            }
        }

        private Database TestEncryptedBlobsInternal(string? password)
        {
            var seekrit = OpenSeekrit(password);
            var body = Encoding.UTF8.GetBytes("This is a blob!");
            var blob = new Blob("text/plain", body);

            using (var doc = new MutableDocument("att")) {
                doc.SetBlob("blob", blob);
                seekrit.GetDefaultCollection().Save(doc);

                blob = doc.GetBlob("blob");
                blob?.Digest.ShouldNotBeNull();

                var fileName = blob!.Digest!.Substring(5).Replace("/", "_");
                var path = $"{seekrit.Path}Attachments{Path.DirectorySeparatorChar}{fileName}.blob";
                var raw = File.ReadAllBytes(path);

                if (password != null) {
                    raw.ShouldNotBeEquivalentTo(body, "because otherwise the attachment was not encrypted");
                } else {
                    raw.ShouldBeEquivalentTo(body, "because otherwise the attachment was encrypted");
                }

                if (password == null) {
                    raw.ShouldBeEquivalentTo(body, "because otherwise the attachment was encrypted");
                }
            }

            using (var savedDoc = seekrit.GetDefaultCollection().GetDocument("att")) {
                blob = savedDoc?.GetBlob("blob");
                blob.ShouldNotBeNull("because the document and blob were saved earlier in the test");
                blob.Digest.ShouldNotBeNull();
                blob.Content.ShouldNotBeNull("because the blob should not be empty");
                var content = Encoding.UTF8.GetString(blob.Content!);
                content.ShouldBe("This is a blob!");
            }

            return seekrit;
        }

        private Database OpenSeekrit(string? password)
        {
            if(password != null && !KeyCache.ContainsKey(password)) {
                KeyCache[password] = new EncryptionKey(password);
            }

            var config = new DatabaseConfiguration
            {
                EncryptionKey = password != null ? KeyCache[password] : null,
                Directory = Directory
            };

            return new Database("seekrit", config);
        }
    }
}

#endif