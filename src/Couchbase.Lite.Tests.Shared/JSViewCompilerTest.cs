//
// JSViewCompilerTest.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using Couchbase.Lite.Listener;
using NUnit.Framework;
using System.Collections.Generic;
using Couchbase.Lite.Views;
using System.Collections;
using Couchbase.Lite.Internal;

namespace Couchbase.Lite
{
    [TestFixture]
    public class JSViewCompilerTest
    {
        [Test]
        public void TestJsMapFunction()
        {
            var c = new JSViewCompiler();
            var mapBlock = c.CompileMap("function(doc){emit(doc.key, doc);}", "javascript");
            Assert.IsNotNull(mapBlock);

            var doc = new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", @"1-xyzzy" },
                { "key", "value" }
            };

            var emitted = new List<object>();
            EmitDelegate emit = (key, value) =>
            {
                Console.WriteLine("Emitted: {0} -> {1}", key, value);
                emitted.Add(key);
                emitted.Add(value);
            };
            mapBlock(doc, emit);

            CollectionAssert.AreEqual(new List<object> { "value", doc }, emitted);
        }

        [Test]
        public void TestJsReduceFunction()
        {
            var c = new JSViewCompiler();
            var reduceBlock = c.CompileReduce("function(k,v,r){return [k,v,r];}", "javascript");
            Assert.IsNotNull(reduceBlock);

            var keys = new List<object> { "master", "schlage", "medeco" };
            var values = new List<object> { 1, 2, 3 };
            var result = reduceBlock(keys, values, false);

            CollectionAssert.AreEqual(new List<object> { keys, values, false }, (IEnumerable)result);
        }

        [Test]
        public void TestJsBuiltInReduceFunction()
        {
            var c = new JSViewCompiler();
            var reduceBlock = c.CompileReduce("_count", "javascript");

            Assert.IsNotNull(reduceBlock);
            Assert.AreEqual(BuiltinReduceFunctions.Count, reduceBlock);

            reduceBlock = c.CompileReduce("_stats", "javascript");
            Assert.IsNotNull(reduceBlock);
            Assert.AreEqual(BuiltinReduceFunctions.Stats, reduceBlock);

            var keys = new List<object> { "master", "schlage", "medeco" };
            var values = new List<object> { 19, -75, 3.1416 };
            var result = reduceBlock(keys, values, false);
            var expected = new Dictionary<string, object> {
                { "count", 3 },
                { "sum", -52.8584 },
                { "sumsqr", 5995.86965056 },
                { "max", 19 },
                { "min", -75 }
            };
            CollectionAssert.AreEquivalent(expected, (IEnumerable)result);

            reduceBlock = c.CompileReduce("_frob", "javascript");
            Assert.IsNull(reduceBlock);
        }

        [Test]
        public void TestJsFilterFunction()
        {
            var c = new JSFilterCompiler();
            var filterBlock = c.CompileFilter("function(doc,req){return doc.ok;}", "javascript");
            Assert.IsNotNull(filterBlock);

            var document = new Document(null, "doc1", false);
            var rev = new RevisionInternal(new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", "1-aa" },
                { "foo", 666 }
            });
            var savedRev = new SavedRevision(document, rev);
            Assert.IsFalse(filterBlock(savedRev, null));

            rev = new RevisionInternal(new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", "1-aa" },
                { "ok", false }
            });
            savedRev = new SavedRevision(document, rev);
            Assert.IsFalse(filterBlock(savedRev, null));

            rev = new RevisionInternal(new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", "1-aa" },
                { "ok", true }
            });
            savedRev = new SavedRevision(document, rev);
            Assert.IsTrue(filterBlock(savedRev, null));

            rev = new RevisionInternal(new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", "1-aa" },
                { "ok", "mais oui" }
            });
            savedRev = new SavedRevision(document, rev);
            Assert.IsTrue(filterBlock(savedRev, null));
        }

        [Test]
        public void TestJsFilterFunctionWithParameters()
        {
            var c = new JSFilterCompiler();
            var filterBlock = c.CompileFilter("function(doc,req){return doc.name == req.name;}", "javascript");
            Assert.IsNotNull(filterBlock);
            var filterParams = new Dictionary<string, object> { { "name", "jim" } };
            var document = new Document(null, "doc1");
            var rev = new RevisionInternal(new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", "1-aa" },
                { "foo", 666 }
            });
            var savedRev = new SavedRevision(document, rev);
            Assert.IsFalse(filterBlock(savedRev, filterParams));

            rev = new RevisionInternal(new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", "1-aa" },
                { "name", "bob" }
            });
            savedRev = new SavedRevision(document, rev);
            Assert.IsFalse(filterBlock(savedRev, filterParams));

            rev = new RevisionInternal(new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", "1-aa" },
                { "name", "jim" }
            });
            savedRev = new SavedRevision(document, rev);
            Assert.IsTrue(filterBlock(savedRev, filterParams));
        }

        [Test]
        public void TestJsLogFunction()
        {
            // This case will test that calling log() function doesn't cause any errors running the JS map
            // map function.
            var c = new JSViewCompiler();
            var mapBlock = c.CompileMap("function(doc){log('Log Message'); emit(doc.key, doc);}", "javascript");
            Assert.IsNotNull(mapBlock);

            var doc = new Dictionary<string, object> {
                { "_id", "doc1" },
                { "_rev", @"1-xyzzy" },
                { "key", "value" }
            };

            var emitted = new List<object>();
            EmitDelegate emit = (key, value) => emitted.Add(value);
            mapBlock(doc, emit);

            CollectionAssert.AreEqual(new List<object> { doc }, emitted);
        }
    }
}

