//
//  LoadTest.cs
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

#if PERFORMANCE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
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
    public sealed class LoadTest : TestCase
    {
#if !WINDOWS_UWP

        public LoadTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestCreate()
        {
            var stopwatch = Stopwatch.StartNew();

            const int n = 2000;
            const string tag = "Create";
            CreateDocumentNSave(tag, n);
            VerifyByTagName(tag, n);
            Db.Count.Should().Be(n);

            stopwatch.Stop();
            LogPerformanceStats("TestCreate()", stopwatch.Elapsed);
        }

        [Fact]
        public void TestUpdate()
        {
            var stopwatch = Stopwatch.StartNew();

            const int n = 2000;
            const string docID = "doc1";
            var tag = "Create";


            CreateDocumentNSave(docID, tag);
            using (var doc = Db.GetDocument(docID)) {
                doc.Should().NotBeNull();
                doc.Id.Should().Be(docID);
                doc.GetString("tag").Should().Be(tag);

                tag = "Update";
                using (var mDoc = doc.ToMutable()) {
                    UpdateDoc(mDoc, n, tag);
                }
            }

            using (var doc = Db.GetDocument(docID)) {
                doc.Should().NotBeNull();
                doc.Id.Should().Be(docID);
                doc.GetString("tag").Should().Be(tag);
                doc.GetInt("update").Should().Be(n);
            }

            stopwatch.Stop();
            LogPerformanceStats("TestUpdate()", stopwatch.Elapsed);
        }

        [Fact]
        public void TestRead()
        {
            var stopwatch = Stopwatch.StartNew();

            const int n = 2000;
            const string docID = "doc1";
            const string tag = "Read";

            CreateDocumentNSave(docID, tag);

            for (var i = 0; i < n; i++) {
                using (var doc = Db.GetDocument(docID)) {
                    doc.Should().NotBeNull();
                    doc.Id.Should().Be(docID);
                    doc.GetString("tag").Should().Be(tag);
                }
            }

            stopwatch.Stop();
            LogPerformanceStats("TestRead()", stopwatch.Elapsed);
        }

        [Fact]
        public void TestDelete()
        {
            var stopwatch = Stopwatch.StartNew();

            const int n = 2000;
            const string tag = "Delete";

            for (var i = 0; i < n; i++) {
                var docID = $"doc-{i:D10}";
                CreateDocumentNSave(docID, tag);
                Db.Count.Should().Be(1);
                using (var doc = Db.GetDocument(docID)) {
                    doc.GetString("tag").Should().Be(tag);
                    Db.Delete(doc);
                    Db.Count.Should().Be(0);
                }
            }

            stopwatch.Stop();
            LogPerformanceStats("TestDelete()", stopwatch.Elapsed);
        }

        private MutableDocument CreateDocumentWithTag(string id, string tag)
        {
            var doc = new MutableDocument(id);

            doc.SetString("tag", tag);

            doc.SetString("firstName", "Daniel");
            doc.SetString("lastName", "Tiger");

            var address = new MutableDictionary();
            address.SetString("street", "1 Main street");
            address.SetString("city", "Mountain View");
            address.SetString("state", "CA");
            doc.SetDictionary("address", address);

            var phones = new MutableArray();
            phones.AddString("650-123-0001").AddString("650-123-0002");
            doc.SetArray("phones", phones);

            doc.SetDate("updated", DateTimeOffset.UtcNow);

            return doc;
        }

        private void CreateDocumentNSave(string id, string tag)
        {
            using (var doc = CreateDocumentWithTag(id, tag)) {
                Db.Save(doc).Dispose();
            }
        }

        private void CreateDocumentNSave(string tag, int nDocs)
        {
            for (var i = 0; i < nDocs; i++) {
                var docID = $"doc-{i:D10}";
                CreateDocumentNSave(docID, tag);
            }
        }

        private void UpdateDoc(MutableDocument doc, int rounds, string tag)
        {
            for (var i = 1; i <= rounds; i++) {
                doc.SetInt("update", i);
                doc.SetString("tag", tag);

                var address = doc.GetDictionary("address");
                address.Should().NotBeNull();
                var street = $"{i} street.";
                address.SetString("street", street);

                var phones = doc.GetArray("phones");
                phones.Should().NotBeNull();
                phones.Count.Should().Be(2);
                var phone = $"650-000-{i:D4}";
                phones.SetString(0, phone);

                doc.SetDate("updated", DateTimeOffset.UtcNow);

                Db.Save(doc).Dispose();
            }
        }

        private void VerifyByTagName(string tag, Action<int, IResult> verify)
        {
            var TAG_EXPR = Expression.Property("tag");
            var DOCID = SelectResult.Expression(Meta.ID);
            var ds = DataSource.Database(Db);
            using (var q = Query.Select(DOCID).From(ds).Where(TAG_EXPR.EqualTo(tag))) {
                WriteLine($"query -> {(q as XQuery).Explain()}");
                using (var rs = q.Execute()) {
                    int n = 0;
                    foreach (var row in rs) {
                        verify(++n, row);
                    }
                }
            }
        }

        private void VerifyByTagName(string tag, int nRows)
        {
            var count = 0;
            VerifyByTagName(tag, (n, row) => { Interlocked.Increment(ref count); });
            count.Should().Be(nRows);
        }

        private void LogPerformanceStats(string name, TimeSpan time)
        {
            WriteLine($"PerformanceStats: {name} -> {time.TotalMilliseconds}ms");
        }
    }
}
#endif