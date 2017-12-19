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
using System.Threading.Tasks;

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
    public sealed class ConcurrencyTest : TestCase
    {
#if !WINDOWS_UWP
        public ConcurrencyTest(ITestOutputHelper output) : base(output)
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
        public void TestConcurrentCreateInBatch()
        {
            const int nDocs = 1000;
            const uint nConcurrent = 10;

            ConcurrentRuns(nConcurrent, (index) =>
            {
                var tag = $"Create{index}";
                Db.InBatch(() => { CreateDocs(nDocs, tag).Should().HaveCount(nDocs); });
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
                ReadDocs(docIDs, nRounds);
            });
        }

        [Fact]
        public void TestConcurrentReadInBatch()
        {
            const uint nDocs = 10;
            const uint nRounds = 100;
            const uint nConcurrent = 10;

            var docs = CreateDocs(nDocs, "Create");
            var docIDs = docs.Select(x => x.Id).ToList();

            ConcurrentRuns(nConcurrent, (index) =>
            {
                Db.InBatch(() => { ReadDocs(docIDs, nRounds); });
            });
        }

        [Fact]
        public void TestConcurrentReadNUpdate()
        {
            const uint nDocs = 10;
            const uint nRounds = 100;
            const string tag = "Update";

            var locker = new object();

            var docIDs = CreateDocs(nDocs, "Create").Select(x => x.Id).ToList();

            var t1 = Task.Factory.StartNew(() => ReadDocs(docIDs, nRounds));
            var t2 = Task.Factory.StartNew(() => UpdateDocs(docIDs, nRounds, tag));

            Task.WaitAll(new[] { t1, t2 }, TimeSpan.FromSeconds(60)).Should().BeTrue();
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
        public void TestConcurrentCreateNCompactDB()
        {
            const int nDocs = 1000;

            var tag1 = "Create";
            var exp1 = new WaitAssert();
            exp1.RunAssertAsync(() =>
            {
                CreateDocs(nDocs, "Create").ToList();
            });

            Db.Compact();
            exp1.WaitForResult(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void TestConcurrentCreateNCreateIndexDB()
        {
            const int nDocs = 1000;

            var tag1 = "Create";
            var exp1 = new WaitAssert();
            exp1.RunAssertAsync(() =>
            {
                CreateDocs(nDocs, "Create").ToList();
            });

            Db.CreateIndex("sentence", Index.FTSIndex(FTSIndexItem.Expression(Expression.Property("sentence"))));
            exp1.WaitForResult(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void TestDatabaseChange()
        {
            var exp1 = new WaitAssert();
            var exp2 = new WaitAssert();
            Db.AddChangeListener(null, (sender, args) =>
            {
                exp2.RunAssert(() =>
                {
                    exp1.WaitForResult(TimeSpan.FromSeconds(20)); // Test deadlock
                });
            });

            exp1.RunAssertAsync(() =>
            {
                Db.Save(new MutableDocument("doc1"));
            });

            exp1.WaitForResult(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void TestDocumentChange()
        {
            var exp1 = new WaitAssert();
            var exp2 = new WaitAssert();
            Db.AddDocumentChangeListener("doc1", (sender, args) =>
            {
                WriteLine("Reached document changed callback");
                exp2.RunAssert(() =>
                {
                    WriteLine("Waiting for exp1 in document changed callback");
                    exp1.WaitForResult(TimeSpan.FromSeconds(20)); // Test deadlock
                });
            });

            WriteLine("Triggering async save");
            exp1.RunAssertAsync(() =>
            {
                WriteLine("Running async save");
                Db.Save(new MutableDocument("doc1"));
                WriteLine("Async save completed");
            });

            WriteLine("Waiting for exp1 in test method");
            exp1.WaitForResult(TimeSpan.FromSeconds(10));
        }

         

        private void ReadDocs(IEnumerable<string> docIDs, uint rounds)
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
                    var doc = Db.GetDocument(docID).ToMutable();
                    doc.SetString("tag", tag);

                    var address = doc.GetDictionary("address");
                    address.Should().NotBeNull();
                    var street = $"{n} street.";
                    address.SetString("street", street);

                    var phones = doc.GetArray("phones");
                    phones.Should().NotBeNull().And.HaveCount(2);
                    var phone = $"650-000-{n}";
                    phones.SetString(0, phone);

                    doc.SetDate("updated", DateTimeOffset.UtcNow);

                    WriteLine($"[{tag}] rounds: {r} updating {doc.Id}");
                    Db.Save(doc);
                }
            }
        }

        private void VerifyByTagName(string name, Action<ulong, IResult> test)
        {
            var TAG = Expression.Property("tag");
            var DOCID = SelectResult.Expression(Meta.ID);
            using (var q = Query.Select(DOCID).From(DataSource.Database(Db)).Where(TAG.EqualTo(name))) {
                WriteLine((q as XQuery).Explain());

                using (var e = q.Execute()) {
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

        private MutableDocument CreateDocument(string tag)
        {
            var doc = new MutableDocument();

            doc.SetString("tag", tag);

            doc.SetString("firstName", "Daniel");
            doc.SetString("lastName", "Tiger");

            var address = new MutableDictionary();
            address.SetString("street", "1 Main street");
            address.SetString("city", "Mountain View");
            address.SetString("state", "CA");
            doc.SetDictionary("address", address);

            var phones = new MutableArray();
            phones.AddString("650-123-0001")
                .AddString("650-123-0001");

            doc.SetArray("phones", phones);

            doc.SetDate("updated", DateTimeOffset.UtcNow);

            return doc;
        }

        private IEnumerable<Document> CreateDocs(uint count, string tag)
        {
            for (uint i = 1; i <= count; i++) {
                var doc = CreateDocument(tag);
                WriteLine($"[{tag}] rounds: {i} saving {doc.Id}");
                yield return Db.Save(doc);
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
