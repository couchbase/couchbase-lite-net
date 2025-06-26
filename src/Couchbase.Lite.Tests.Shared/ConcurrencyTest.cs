//
//  ConcurrencyTests.cs
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

// The tests simply take too much time in debug mode
#if !DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public sealed class ConcurrencyTest : TestCase
    {
        public ConcurrencyTest(ITestOutputHelper output) : base(output)
        {

        }

        [Fact]
        public void TestConcurrentCreate()
        {
            const int nDocs = 1000;
            const uint nConcurrent = 10;

            ConcurrentRuns(nConcurrent, (index) => {
                var tag = $"Create{index}";
                CreateDocs(nDocs, tag).Count().ShouldBe(nDocs);
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

            ConcurrentRuns(nConcurrent, (index) => {
                var tag = $"Create{index}";
                Db.InBatch(() => { CreateDocs(nDocs, tag).Count().ShouldBe(nDocs); });
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

            count.ShouldBe(nDocs, "because only the last in the previous loop should return results");
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

            var docIDs = CreateDocs(nDocs, "Create").Select(x => x.Id).ToList();

            var t1 = Task.Run(() => ReadDocs(docIDs, nRounds));
            var t2 = Task.Run(() => UpdateDocs(docIDs, nRounds, tag));

            Task.WaitAll(new[] { t1, t2 }, TimeSpan.FromSeconds(60)).ShouldBeTrue();
        }

        [Fact]
        public void TestConcurrentDelete()
        {
            const int nDocs = 1000;
            var docs = CreateDocs(nDocs, "Create").ToList();
            docs.Count.ShouldBe(nDocs);

            var delete1 = new WaitAssert();
            var ignore = delete1.RunAssertAsync(() =>
            {
                foreach (var doc in docs) {
                    DefaultCollection.Delete(doc);
                }
            });

            var delete2 = new WaitAssert();
            ignore = delete2.RunAssertAsync(() =>
            {
                foreach (var doc in docs) {
                    DefaultCollection.Delete(doc);
                }
            });

            WaitAssert.WaitFor(TimeSpan.FromSeconds(60), delete1, delete2);
            DefaultCollection.Count.ShouldBe(0UL, "because all documents were deleted");
        }

        [Fact]
        public void TestConcurrentInBatch()
        {
            const int nDocs = 1000;
            const uint nConcurrent = 10;

            ConcurrentRuns(nConcurrent, (index) => {
                if (Db == null) {
                    return;
                }

                Db.InBatch(() => {
                    var tag = $"Create{index}";
                    CreateDocs(nDocs, tag).Count().ShouldBe(nDocs); // Force evaluation, not a needed assert
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
            docs.Count.ShouldBe(nDocs);

            ConcurrentRuns(nConcurrent, index => {
                foreach (var doc in docs) {
                    try {
                        DefaultCollection.Purge(doc);
                    } catch (CouchbaseLiteException e) {
                        if (e.Error != CouchbaseLiteError.NotFound) {
                            throw;
                        }
                    }
                }
            });

            DefaultCollection.Count.ShouldBe(0UL, "because all documents were purged");
        }

        [Fact]
        public void TestConcurrentCompact()
        {
            const int nDocs = 1000;
            const uint nRounds = 10;
            const uint nConcurrent = 10;

            CreateDocs(nDocs, "Create").Count().ShouldBe(nDocs);

            ConcurrentRuns(nConcurrent, index =>
            {
                for (uint i = 0; i < nRounds; i++) {
                    Db.PerformMaintenance(MaintenanceType.Compact);
                }
            });
        }

        [Fact]
        public void TestConcurrentCreateAndCloseDB()
        {
            const int nDocs = 1000;
            
            var exp1 = new WaitAssert();
            var ignore = exp1.RunAssertAsync(() =>
            {
                Action a = () => CreateDocs(nDocs, "Create").ToList();
                Should.Throw<InvalidOperationException>(a);
            });

            Db.Close();
            exp1.WaitForResult(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void TestConcurrentCreateAndDeleteDB()
        {
            const int nDocs = 1000;
            
            var exp1 = new WaitAssert();
            var ignore = exp1.RunAssertAsync(() =>
            {
                Action a = () => CreateDocs(nDocs, "Create").ToList();
                Should.Throw<InvalidOperationException>(a);
            });

            Db.Delete();
            exp1.WaitForResult(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void TestConcurrentCreateNCompactDB()
        {
            const int nDocs = 1000;
            
            var exp1 = new WaitAssert();
            var ignore = exp1.RunAssertAsync(() =>
            {
                CreateDocs(nDocs, "Create").ToList();
            });

            Db.PerformMaintenance(MaintenanceType.Compact);
            exp1.WaitForResult(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void TestConcurrentCreateNCreateIndexDB()
        {
            const int nDocs = 1000;
            
            var exp1 = new WaitAssert();
            var ignore = exp1.RunAssertAsync(() =>
            {
                CreateDocs(nDocs, "Create").ToList();
            });

            DefaultCollection.CreateIndex("sentence", IndexBuilder.FullTextIndex(FullTextIndexItem.Property("sentence")));
            exp1.WaitForResult(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void TestDatabaseChange()
        {
            var exp1 = new WaitAssert();
            var exp2 = new WaitAssert();
            DefaultCollection.AddChangeListener(null, (sender, args) =>
            {
                exp2.RunAssert(() =>
                {
                    exp1.WaitForResult(TimeSpan.FromSeconds(20)); // Test deadlock
                });
            });

            var ignore = exp1.RunAssertAsync(() =>
            {
                DefaultCollection.Save(new MutableDocument("doc1"));
            });

            exp1.WaitForResult(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void TestDocumentChange()
        {
            var exp1 = new WaitAssert();
            var exp2 = new WaitAssert();
            DefaultCollection.AddDocumentChangeListener("doc1", (sender, args) =>
            {
                WriteLine("Reached document changed callback");
                exp2.RunAssert(() =>
                {
                    WriteLine("Waiting for exp1 in document changed callback");
                    exp1.WaitForResult(TimeSpan.FromSeconds(20)); // Test deadlock
                });
            });

            WriteLine("Triggering async save");
            var ignore = exp1.RunAssertAsync(() =>
            {
                WriteLine("Running async save");
                DefaultCollection.Save(new MutableDocument("doc1"));
                WriteLine("Async save completed");
            });

            WriteLine("Waiting for exp1 in test method");
            exp1.WaitForResult(TimeSpan.FromSeconds(10));
        }

        [Fact]
        [ForIssue("CBL-6128")]
        public void TestConcurrentCreateAndQuery()
        {
            using var enterInBatchLock = new ManualResetEventSlim();
            using var enterQueryLock = new ManualResetEventSlim();
            using var testCompleteLock = new CountdownEvent(2);

            var stepCount = 0;
            Exception? ex = null;

            var t1 = new Thread(() =>
            {
                try {
                    Db.InBatch(() =>
                    {
                        Interlocked.CompareExchange(ref stepCount, 1, 0);
                        enterInBatchLock.Set();
                        enterQueryLock.Wait(TimeSpan.FromSeconds(1)).ShouldBeFalse("because t2 should be blocked waiting for t1's InBatch");
                        Interlocked.CompareExchange(ref stepCount, 3, 2);
                    });
                } catch(Exception e) {
                    Interlocked.CompareExchange(ref ex, e, null);
                }
            });

            var t2 = new Thread(() =>
            {
                try {
                    enterInBatchLock.Wait(TimeSpan.FromSeconds(1)).ShouldBeTrue("because otherwise t1 didn't enter InBatch");
                    Interlocked.CompareExchange(ref stepCount, 2, 1);
                    using var rs = Db.CreateQuery("select * from _").Execute();
                    var realized = rs.ToList();
                    enterQueryLock.Set(); // Should have already timed out but set anyway
                    Interlocked.CompareExchange(ref stepCount, 4, 3);
                } catch (Exception e) {
                    Interlocked.CompareExchange(ref ex, e, null);
                }
            });
            t1.Start();
            t2.Start();


            // Wait for both threads, and any exceptions they may throw
            t1.Join();
            t2.Join();

            if(ex != null) {
                throw ex;
            }

            stepCount.ShouldBe(4, "because otherwise some of the steps happened out of order");
        }

        private void ReadDocs(IEnumerable<string> docIDs, uint rounds)
        {
            for (uint r = 1; r <= rounds; r++) {
                foreach (var docID in docIDs) {
                    var doc = DefaultCollection.GetDocument(docID);
                    doc.ShouldNotBeNull();
                    doc!.Id.ShouldBe(docID);
                }
            }
        }

        private void UpdateDocs(IEnumerable<string> docIDs, uint rounds, string tag)
        {
            uint n = 0;
            for (uint r = 1; r <= rounds; r++) {
                foreach (var docID in docIDs) {
                    var doc = DefaultCollection.GetDocument(docID)?.ToMutable();
                    doc.ShouldNotBeNull($"because otherwise document '{docID}' does not exist");
                    doc!.SetString("tag", tag);

                    var address = doc.GetDictionary("address");
                    address.ShouldNotBeNull();
                    var street = $"{n} street.";
                    address!.SetString("street", street);

                    var phones = doc.GetArray("phones");
                    phones.Count.ShouldBe(2);
                    var phone = $"650-000-{n}";
                    phones!.SetString(0, phone);

                    doc.SetDate("updated", DateTimeOffset.UtcNow);

                    WriteLine($"[{tag}] rounds: {r} updating {doc.Id}");
                    DefaultCollection.Save(doc);
                }
            }
        }

        private void VerifyByTagName(string name, Action<ulong, Result> test)
        {
            var TAG = Expression.Property("tag");
            var DOCID = SelectResult.Expression(Meta.ID);
            using (var q = QueryBuilder.Select(DOCID).From(DataSource.Collection(DefaultCollection)).Where(TAG.EqualTo(Expression.String(name)))) {
                WriteLine((q as XQuery)!.Explain());

                var e = q.Execute();
                ulong n = 0;
                foreach (var row in e) {
                    test(++n, row);
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

            count.ShouldBe(numRows);
        }

        private MutableDocument CreateDocument(string tag)
        {
            var doc = new MutableDocument();

            doc.SetString("tag", tag);

            doc.SetString("firstName", "Daniel");
            doc.SetString("lastName", "Tiger");

            var address = new MutableDictionaryObject();
            address.SetString("street", "1 Main street");
            address.SetString("city", "Mountain View");
            address.SetString("state", "CA");
            doc.SetDictionary("address", address);

            var phones = new MutableArrayObject();
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
                DefaultCollection.Save(doc);
                yield return doc;
            }
        }

        private void ConcurrentRuns(uint nRuns, Action<uint> block)
        {
            var expectations = new WaitAssert[nRuns];
            for (uint i = 0; i < nRuns; i++) {
                expectations[i] = new WaitAssert();
                var ignore = expectations[i].RunAssertAsync(block, i);
            }

            WaitAssert.WaitFor(TimeSpan.FromSeconds(60), expectations);
        }
    }
}
#endif