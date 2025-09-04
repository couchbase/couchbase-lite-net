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
using System.Diagnostics;
using System.Threading;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Query;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Test;

public sealed class LoadTest(ITestOutputHelper output) : TestCase(output)
{
    [Fact]
    public void TestCreate()
    {
        var stopwatch = Stopwatch.StartNew();

        const int n = 2000;
        const string tag = "Create";
        CreateDocumentNSave(tag, n);
        VerifyByTagName(tag, n);
        DefaultCollection.Count.ShouldBe((ulong)n);

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
        using (var doc = DefaultCollection.GetDocument(docID)) {
            doc.ShouldNotBeNull();
            doc.Id.ShouldBe(docID);
            doc.GetString("tag").ShouldBe(tag);

            tag = "Update";
            using (var mDoc = doc.ToMutable()) {
                UpdateDoc(mDoc, n, tag);
            }
        }

        using (var doc = DefaultCollection.GetDocument(docID)) {
            doc.ShouldNotBeNull();
            doc.Id.ShouldBe(docID);
            doc.GetString("tag").ShouldBe(tag);
            doc.GetInt("update").ShouldBe(n);
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
            using var doc = DefaultCollection.GetDocument(docID);
            doc.ShouldNotBeNull();
            doc.Id.ShouldBe(docID);
            doc.GetString("tag").ShouldBe(tag);
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
            DefaultCollection.Count.ShouldBe(1UL);
            using var doc = DefaultCollection.GetDocument(docID);
            doc.ShouldNotBeNull();
            doc.GetString("tag").ShouldBe(tag);
            DefaultCollection.Delete(doc);
            DefaultCollection.Count.ShouldBe(0UL);
        }

        stopwatch.Stop();
        LogPerformanceStats("TestDelete()", stopwatch.Elapsed);
    }

    private static MutableDocument CreateDocumentWithTag(string id, string tag)
    {
        var doc = new MutableDocument(id);

        doc.SetString("tag", tag);

        doc.SetString("firstName", "Daniel");
        doc.SetString("lastName", "Tiger");

        var address = new MutableDictionaryObject();
        address.SetString("street", "1 Main street");
        address.SetString("city", "Mountain View");
        address.SetString("state", "CA");
        doc.SetDictionary("address", address);

        var phones = new MutableArrayObject();
        phones.AddString("650-123-0001").AddString("650-123-0002");
        doc.SetArray("phones", phones);

        doc.SetDate("updated", DateTimeOffset.UtcNow);

        return doc;
    }

    private void CreateDocumentNSave(string id, string tag)
    {
        using var doc = CreateDocumentWithTag(id, tag);
        DefaultCollection.Save(doc);
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
            address.ShouldNotBeNull();
            var street = $"{i} street.";
            address.SetString("street", street);

            var phones = doc.GetArray("phones");
            phones.ShouldNotBeNull();
            phones.Count.ShouldBe(2);
            var phone = $"650-000-{i:D4}";
            phones.SetString(0, phone);

            doc.SetDate("updated", DateTimeOffset.UtcNow);

            DefaultCollection.Save(doc);
        }
    }

    private void VerifyByTagName(string tag, Action<int, Result> verify)
    {
        var tagExpr = Expression.Property("tag");
        var docId = SelectResult.Expression(Meta.ID);
        var ds = DataSource.Collection(DefaultCollection);
        using var q = QueryBuilder.Select(docId).From(ds).Where(tagExpr.EqualTo(Expression.String(tag)));
        WriteLine($"query -> {(q as XQuery)!.Explain()}");
        var rs = q.Execute();
        var n = 0;
        foreach (var row in rs) {
            verify(++n, row);
        }
    }

    private void VerifyByTagName(string tag, int nRows)
    {
        var count = 0;
        VerifyByTagName(tag, (_, _) => { Interlocked.Increment(ref count); });
        count.ShouldBe(nRows);
    }

    private void LogPerformanceStats(string name, TimeSpan time) => WriteLine($"PerformanceStats: {name} -> {time.TotalMilliseconds}ms");
}
#endif