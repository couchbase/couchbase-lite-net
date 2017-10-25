//
//  ConcurrencyTests.cs
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
using System.Linq;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Query;
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
    public sealed class ConcurrencyTests : TestCase
    {
#if !WINDOWS_UWP
        public ConcurrencyTests(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestConcurrentCreate()
        {
            const int nDocs = 1000;
            const uint nConcurrent = 10;

            ConcurrentRuns(nConcurrent, (index) =>
            {
                var tag = $"Create{index}";
                CreateDocs(nDocs, tag).Should().HaveCount(nDocs);
            });

            for (uint i = 0; i < nConcurrent; i++) {
                var tag = $"Create{i}";
                VerifyByTagName(tag, nDocs);
            }
        }

        [Fact]
        public void TestConcurrentUpdate()
        {
            const uint nDocs = 10;
            const uint nRounds = 10;
            const uint nConcurrent = 10;

            var docs = CreateDocs(nDocs, "Create");
            var docIDs = docs.Select(x => x.Id).ToList();

            ConcurrentRuns(nConcurrent, (index) =>
            {
                var tag = $"Update{index}";
                UpdateDocs(docIDs, nRounds, tag);
            });

            uint count = 0;

            for (uint i = 0; i < nConcurrent; i++) {
                var tag = $"Update{i}";
                VerifyByTagName(tag, (n, row) =>
                {
                    count++;
                });
            }

            count.Should().Be(nDocs, "because only the last in the previous loop should return results");
        }

        [Fact]
        public void TestConcurrentRead()
        {
            const uint nDocs = 10;
            const uint nRounds = 100;
            const uint nConcurrent = 10;

            var docs = CreateDocs(nDocs, "Create");
            var docIDs = docs.Select(x => x.Id).ToList();

            ConcurrentRuns(nConcurrent, (index) =>
            {
                ReadDocIDs(docIDs, nRounds);
            });
        }

        [Fact]
        public void TestConcurrentDelete()
        {
            const int nDocs = 1000;
            var docs = CreateDocs(nDocs, "Create").ToList();
            docs.Count.Should().Be(nDocs);

            var delete1 = new WaitAssert();
            delete1.RunAssertAsync(() =>
            {
                foreach (var doc in docs) {
                    Db.Delete(doc);
                }
            });

            var delete2 = new WaitAssert();
            delete2.RunAssertAsync(() =>
            {
                foreach (var doc in docs) {
                    Db.Delete(doc);
                }
            });

            WaitAssert.WaitFor(TimeSpan.FromSeconds(60), delete1, delete2);
            Db.Count.Should().Be(0, "because all documents were deleted");
        }

        [Fact]
        public void TestConcurrentInBatch()
        {
            const int nDocs = 1000;
            const uint nConcurrent = 10;

            ConcurrentRuns(nConcurrent, (index) =>
            {
                if (Db == null) {
                    return;
                }

                Db.InBatch(() =>
                {
                    var tag = $"Create{index}";
                    CreateDocs(nDocs, tag).Should().HaveCount(nDocs); // Force evaluation, not a needed assert
                });
            });

            for (uint i = 0; i < nConcurrent; i++) {
                var tag = $"Create{i}";
                VerifyByTagName(tag, nDocs);
            }
        }

        [Fact]
        public void TestConcurrentPurge()
        {
            const int nDocs = 1000;
            const uint nConcurrent = 10;

            var docs = CreateDocs(nDocs, "Create").ToList();
            docs.Count.Should().Be(nDocs);

            ConcurrentRuns(nConcurrent, index =>
            {
                foreach (var doc in docs) {
                    try {
                        Db.Purge(doc);
                    } catch (CouchbaseLiteException e) {
                        if (e.Status != StatusCode.NotFound) {
                            throw;
                        }
                    }
                }
            });

            Db.Count.Should().Be(0, "because all documents were purged");
        }

        [Fact]
        public void TestConcurrentCompact()
        {
            const int nDocs = 1000;
            const uint nRounds = 10;
            const uint nConcurrent = 10;

            CreateDocs(nDocs, "Create").Should().HaveCount(nDocs);

            ConcurrentRuns(nConcurrent, index =>
            {
                for (uint i = 0; i < nRounds; i++) {
                    Db.Compact();
                }
            });
        }

        [Fact]
        public void TestConcurrentCreateAndCloseDB()
        {
            const int nDocs = 1000;

            var tag1 = "Create";
            var exp1 = new WaitAssert();
            exp1.RunAssertAsync(() =>
            {
                Action a = () => CreateDocs(nDocs, "Create").ToList();
                a.ShouldThrow<InvalidOperationException>();
            });

            Db.Close();
            exp1.WaitForResult(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void TestConcurrentCreateAndDeleteDB()
        {
            const int nDocs = 1000;

            var tag1 = "Create";
            var exp1 = new WaitAssert();
            exp1.RunAssertAsync(() =>
            {
                Action a = () => CreateDocs(nDocs, "Create").ToList();
                a.ShouldThrow<InvalidOperationException>();
            });

            Db.Delete();
            exp1.WaitForResult(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void TestDatabaseChange()
        {
            var exp1 = new WaitAssert();
            var exp2 = new WaitAssert();
            Db.Changed += (sender, args) =>
            {
                exp2.RunAssert(() =>
                {
                    exp1.WaitForResult(TimeSpan.FromSeconds(20)); // Test deadlock
                });
            };

            exp1.RunAssertAsync(() =>
            {
                Db.Save(new Document("doc1"));
            });

            exp1.WaitForResult(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void TestDocumentChange()
        {
            var exp1 = new WaitAssert();
            var exp2 = new WaitAssert();
            Db.AddDocumentChangedListener("doc1", (sender, args) =>
            {
                exp2.RunAssert(() =>
                {
                    exp1.WaitForResult(TimeSpan.FromSeconds(20)); // Test deadlock
                });
            });

            exp1.RunAssertAsync(() =>
            {
                Db.Save(new Document("doc1"));
            });

            exp1.WaitForResult(TimeSpan.FromSeconds(10));
        }

        private void ReadDocIDs(IEnumerable<string> docIDs, uint rounds)
        {
            for (uint r = 1; r <= rounds; r++) {
                foreach (var docID in docIDs) {
                    var doc = Db.GetDocument(docID);
                    doc.Should().NotBeNull();
                    doc.Id.Should().Be(docID);
                }
            }
        }

        private void UpdateDocs(IEnumerable<string> docIDs, uint rounds, string tag)
        {
            uint n = 0;
            for (uint r = 1; r <= rounds; r++) {
                foreach (var docID in docIDs) {
                    var doc = Db.GetDocument(docID);
                    doc.Set("tag", tag);

                    var address = doc.GetDictionary("address");
                    address.Should().NotBeNull();
                    var street = $"{n} street.";
                    address.Set("street", street);

                    var phones = doc.GetArray("phones");
                    phones.Should().NotBeNull().And.HaveCount(2);
                    var phone = $"650-000-{n}";
                    phones.Set(0, phone);

                    doc.Set("updated", DateTimeOffset.UtcNow);

                    WriteLine($"[{tag}] rounds: {r} updating {doc.Id}");
                    Db.Save(doc);
                }
            }
        }

        private void VerifyByTagName(string name, Action<ulong, IResult> test)
        {
            var TAG = Expression.Property("tag");
            var DOCID = SelectResult.Expression(Expression.Meta().ID);
            using (var q = Query.Select(DOCID).From(DataSource.Database(Db)).Where(TAG.EqualTo(name))) {
                WriteLine((q as XQuery).Explain());

                using (var e = q.Run()) {
                    ulong n = 0;
                    foreach (var row in e) {
                        test(++n, row);
                    }
                }
            }
        }

        private void VerifyByTagName(string name, uint numRows)
        {
            uint count = 0;
            VerifyByTagName(name, (n, row) =>
            {
                count++;
            });

            count.Should().Be(numRows);
        }

        private Document CreateDocument(string tag)
        {
            var doc = new Document();

            doc.Set("tag", tag);

            doc.Set("firstName", "Daniel");
            doc.Set("lastName", "Tiger");

            var address = new DictionaryObject();
            address.Set("street", "1 Main street");
            address.Set("city", "Mountain View");
            address.Set("state", "CA");
            doc.Set("address", address);

            var phones = new ArrayObject {
                "650-123-0001",
                "650-123-0002"
            };
            doc.Set("phones", phones);

            doc.Set("updated", DateTimeOffset.UtcNow);

            return doc;
        }

        private IEnumerable<Document> CreateDocs(uint count, string tag)
        {
            for (uint i = 1; i <= count; i++) {
                var doc = CreateDocument(tag);
                WriteLine($"[{tag}] rounds: {i} saving {doc.Id}");
                Db.Save(doc);
                yield return doc;
            }
        }

        private void ConcurrentRuns(uint nRuns, Action<uint> block)
        {
            var expectations = new WaitAssert[nRuns];
            for (uint i = 0; i < nRuns; i++) {
                expectations[i] = new WaitAssert();
                expectations[i].RunAssertAsync(block, i);
            }

            foreach (var exp in expectations) {
                exp.WaitForResult(TimeSpan.FromSeconds(60));
            }
        }
    }
}
