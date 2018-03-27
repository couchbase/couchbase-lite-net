// 
//  AllDocsPerformanceTest.cs
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
using System.Diagnostics;
using System.Linq;

using Couchbase.Lite.Interop;

using FluentAssertions;
using LiteCore.Interop;
using LiteCore.Util;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace LiteCore.Tests
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public unsafe class AllDocsPerformanceTest : Test
    {
        private const int SizeOfDocument = 1000;
        private const int NumDocuments = 100000;

#if !WINDOWS_UWP
        public AllDocsPerformanceTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

#if PERFORMANCE

        [Fact]
        [Trait("Slow", "true")]
        public void TestAllDocsPerformance() 
        {
            RunTestVariants(() => {
                var st = Stopwatch.StartNew();

                var options = C4EnumeratorOptions.Default;
                options.flags &= ~C4EnumeratorFlags.IncludeBodies;
                C4Error err;
                var e = NativeRaw.c4db_enumerateAllDocs(Db, C4Slice.Null, C4Slice.Null, &options, &err);
                ((long)e).Should().NotBe(0, "because the enumerator should be created successfully");
                C4Document* doc;
                uint i = 0;
                while(null != (doc = Native.c4enum_nextDocument(e, &err))) {
                    i++;
                    Native.c4doc_free(doc);
                }

                Native.c4enum_free(e);
                i.Should().Be(NumDocuments, "because the query should return all docs");
                var elapsed = st.Elapsed.TotalMilliseconds;
                Console.WriteLine($"Enumerating {i} docs took {elapsed:F3} ms ({elapsed/i:F3} ms/doc)");
            });
        }

#endif

        protected override void SetupVariant(int option)
        {
            base.SetupVariant(option);

            var c = new List<char>(Enumerable.Repeat('a', SizeOfDocument));
            c.Add((char)0);
            var content = new string(c.ToArray());

            C4Error err;
            Native.c4db_beginTransaction(Db, &err).Should().BeTrue("because starting a transaction should succeed");
            var rng = new Random();
            for(int i = 0; i < NumDocuments; i++) {
                using(var docID = new C4String($"doc-{rng.Next():D8}-{rng.Next():D8}-{rng.Next():D8}-{i:D4}"))
                using(var revID = new C4String("1-deadbeefcafebabe80081e50"))
                using(var json = new C4String("{{\"content\":\"{content}\"}}")) {
                    var history = IsRevTrees() ? new C4String[1] { new C4String("1-deadbeefcafebabe80081e50") }
                        : new C4String[1] { new C4String("1@deadbeefcafebabe80081e50") };

                    var rawHistory = history.Select(x => x.AsC4Slice()).ToArray();
                    fixed(C4Slice* rawHistory_ = rawHistory) {
                        var rq = new C4DocPutRequest();
                        rq.existingRevision = true;
                        rq.docID = docID.AsC4Slice();
                        rq.history = rawHistory_;
                        rq.historyCount = 1;
                        rq.body = json.AsC4Slice();
                        rq.save = true;
                        var doc = Native.c4doc_put(Db, &rq, null, &err);
                        ((long)doc).Should().NotBe(0, $"because otherwise the put failed");
                        Native.c4doc_free(doc);
                    }
                }
            }

            Native.c4db_endTransaction(Db, true, &err).Should().BeTrue("because otherwise the transaction failed to end");
            Console.WriteLine($"Created {NumDocuments} docs");
            Native.c4db_getDocumentCount(Db).Should().Be(NumDocuments, "because the number of documents should be the number that was just inserted");
        }
    }
}
