//
// ViewsTest.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;
using Newtonsoft.Json.Linq;

namespace Couchbase.Lite
{
    public class ViewsTest : LiteTestCase
    {
        public const string Tag = "Views";

        [Test]
        public void TestQueryDefaultIndexUpdateMode()
        {
            View view = database.GetView("aview");
            Query query = view.CreateQuery();
            Assert.AreEqual(IndexUpdateMode.Before, query.IndexUpdateMode);
        }

        [Test]
        public void TestViewCreation()
        {
            Assert.IsNull(database.GetExistingView("aview"));
            var view = database.GetView("aview");
            Assert.IsNotNull(view);
            Assert.AreEqual(database, view.Database);
            Assert.AreEqual("aview", view.Name);
            Assert.IsNull(view.Map);
            Assert.AreEqual(view, database.GetExistingView("aview"));

            //no-op
            var changed = view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=> { }, null, "1");
            Assert.IsTrue(changed);
            Assert.AreEqual(1, database.GetAllViews().Count);
            Assert.AreEqual(view, database.GetAllViews()[0]);

            //no-op
            changed = view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=> { }, null, "1");
            Assert.IsFalse(changed);
            changed = view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=> { }, null, "2");

            //no-op
            Assert.IsTrue(changed);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        private RevisionInternal PutDoc(Database db, IDictionary<string, object> props)
        {
            var rev = new RevisionInternal(props, db);
            var status = new Status();
            rev = db.PutRevision(rev, null, false, status);
            Assert.IsTrue(status.IsSuccessful());
            return rev;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        private void PutDocViaUntitledDoc(Database db, IDictionary<string, object> props)
        {
            var document = db.CreateDocument();
            document.PutProperties(props);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual IList<RevisionInternal> PutDocs(Database db)
        {
            var result = new AList<RevisionInternal>();

            var dict2 = new Dictionary<string, object>();
            dict2["_id"] = "22222";
            dict2["key"] = "two";
            result.AddItem(PutDoc(db, dict2));

            var dict4 = new Dictionary<string, object>();
            dict4["_id"] = "44444";
            dict4["key"] = "four";
            result.AddItem(PutDoc(db, dict4));

            var dict1 = new Dictionary<string, object>();
            dict1["_id"] = "11111";
            dict1["key"] = "one";
            result.AddItem(PutDoc(db, dict1));

            var dict3 = new Dictionary<string, object>();
            dict3["_id"] = "33333";
            dict3["key"] = "three";
            result.AddItem(PutDoc(db, dict3));

            var dict5 = new Dictionary<string, object>();
            dict5["_id"] = "55555";
            dict5["key"] = "five";
            result.AddItem(PutDoc(db, dict5));

            return result;
        }

        // http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual IList<RevisionInternal> PutLinkedDocs(Database db)
        {
            var result = new AList<RevisionInternal>();

            var dict1 = new Dictionary<string, object>();
            dict1["_id"] = "11111";
            result.AddItem(PutDoc(db, dict1));

            var dict2 = new Dictionary<string, object>();
            dict2["_id"] = "22222";
            dict2["value"] = "hello";
            dict2["ancestors"] = new string[] { "11111" };
            result.AddItem(PutDoc(db, dict2));

            var dict3 = new Dictionary<string, object>();
            dict3["_id"] = "33333";
            dict3["value"] = "world";
            dict3["ancestors"] = new string[] { "22222", "11111" };
            result.AddItem(PutDoc(db, dict3));

            return result;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void PutNDocs(Database db, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var doc = new Dictionary<string, object>();
                doc.Put("_id", string.Format("{0}", i));

                var key = new AList<string>();
                for (int j = 0; j < 256; j++)
                {
                    key.AddItem("key");
                }
                key.AddItem(string.Format("key-{0}", i));
                doc["key"] = key;

                PutDocViaUntitledDoc(db, doc);
            }
        }

        public static View CreateView(Database db)
        {
            var view = db.GetView("aview");
            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=>
                {
                    Assert.IsNotNull(document["_id"]);
                    Assert.IsNotNull(document["_rev"]);
                    if (document["key"] != null)
                    {
                        emitter(document["key"], null);
                    }
                }, null, "1");
            return view;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewIndex()
        {
            int numTimesMapFunctionInvoked = 0;
            var dict1 = new Dictionary<string, object>();
            dict1["key"] = "one";

            var dict2 = new Dictionary<string, object>();
            dict2["key"] = "two";

            var dict3 = new Dictionary<string, object>();
            dict3["key"] = "three";

            var dictX = new Dictionary<string, object>();
            dictX["clef"] = "quatre";

            var rev1 = PutDoc(database, dict1);
            var rev2 = PutDoc(database, dict2);
            var rev3 = PutDoc(database, dict3);
            PutDoc(database, dictX);

            var view = database.GetView("aview");
            var numTimesInvoked = 0;

            MapDelegate mapBlock = (document, emitter) =>
            {
                numTimesInvoked += 1;

                Assert.IsNotNull(document["_id"]);
                Assert.IsNotNull(document["_rev"]);

                if (document.ContainsKey("key") && document["key"] != null)
                {
                    emitter(document["key"], null);
                }
            };
            view.SetMap(mapBlock, "1");

            Assert.AreEqual(1, view.Id);
            Assert.IsTrue(view.IsStale);
            view.UpdateIndex();

            IList<IDictionary<string, object>> dumpResult = view.Dump();
            Log.V(Tag, "View dump: " + dumpResult);
            Assert.AreEqual(3, dumpResult.Count);
            Assert.AreEqual("\"one\"", dumpResult[0]["key"]);
            Assert.AreEqual(1, dumpResult[0]["seq"]);
            Assert.AreEqual("\"two\"", dumpResult[2]["key"]);
            Assert.AreEqual(2, dumpResult[2]["seq"]);
            Assert.AreEqual("\"three\"", dumpResult[1]["key"]);
            Assert.AreEqual(3, dumpResult[1]["seq"]);

            //no-op reindex
            Assert.IsFalse(view.IsStale);
            view.UpdateIndex();

            // Now add a doc and update a doc:
            var threeUpdated = new RevisionInternal(rev3.GetDocId(), rev3.GetRevId(), false, database);
            numTimesMapFunctionInvoked = numTimesInvoked;

            var newdict3 = new Dictionary<string, object>();
            newdict3["key"] = "3hree";
            threeUpdated.SetProperties(newdict3);

            Status status = new Status();
            rev3 = database.PutRevision(threeUpdated, rev3.GetRevId(), false, status);
            Assert.IsTrue(status.IsSuccessful());

            // Reindex again:
            Assert.IsTrue(view.IsStale);
            view.UpdateIndex();

            // Make sure the map function was only invoked one more time (for the document that was added)
            Assert.AreEqual(numTimesInvoked, numTimesMapFunctionInvoked + 1);

            var dict4 = new Dictionary<string, object>();
            dict4["key"] = "four";
            var rev4 = PutDoc(database, dict4);
            var twoDeleted = new RevisionInternal(rev2.GetDocId(), rev2.GetRevId(), true, database);
            database.PutRevision(twoDeleted, rev2.GetRevId(), false, status);
            Assert.IsTrue(status.IsSuccessful());

            // Reindex again:
            Assert.IsTrue(view.IsStale);
            view.UpdateIndex();
            dumpResult = view.Dump();
            Log.V(Tag, "View dump: " + dumpResult);
            Assert.AreEqual(3, dumpResult.Count);
            Assert.AreEqual("\"one\"", dumpResult[2]["key"]);
            Assert.AreEqual(1, dumpResult[2]["seq"]);
            Assert.AreEqual("\"3hree\"", dumpResult[0]["key"]);
            Assert.AreEqual(5, dumpResult[0]["seq"]);
            Assert.AreEqual("\"four\"", dumpResult[1]["key"]);
            Assert.AreEqual(6, dumpResult[1]["seq"]);

            // Now do a real query:
            IList<QueryRow> rows = view.QueryWithOptions(null).ToList();
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("one", rows[2].Key);
            Assert.AreEqual(rev1.GetDocId(), rows[2].DocumentId);
            Assert.AreEqual("3hree", rows[0].Key);
            Assert.AreEqual(rev3.GetDocId(), rows[0].DocumentId);
            Assert.AreEqual("four", rows[1].Key);
            Assert.AreEqual(rev4.GetDocId(), rows[1].DocumentId);
            view.DeleteIndex();
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewQuery()
        {
            PutDocs(database);
            var view = CreateView(database);
            view.UpdateIndex();

            // Query all rows:
            QueryOptions options = new QueryOptions();
            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();

            var expectedRows = new AList<object>();

            var dict5 = new Dictionary<string, object>();
            dict5["id"] = "55555";
            dict5["key"] = "five";
            expectedRows.AddItem(dict5);

            var dict4 = new Dictionary<string, object>();
            dict4["id"] = "44444";
            dict4["key"] = "four";
            expectedRows.AddItem(dict4);

            var dict1 = new Dictionary<string, object>();
            dict1["id"] = "11111";
            dict1["key"] = "one";
            expectedRows.AddItem(dict1);

            var dict3 = new Dictionary<string, object>();
            dict3["id"] = "33333";
            dict3["key"] = "three";
            expectedRows.AddItem(dict3);

            var dict2 = new Dictionary<string, object>();
            dict2["id"] = "22222";
            dict2["key"] = "two";
            expectedRows.AddItem(dict2);
            Assert.AreEqual(5, rows.Count);
            Assert.AreEqual(dict5["key"], rows[0].Key);
            Assert.AreEqual(dict4["key"], rows[1].Key);
            Assert.AreEqual(dict1["key"], rows[2].Key);
            Assert.AreEqual(dict3["key"], rows[3].Key);
            Assert.AreEqual(dict2["key"], rows[4].Key);

            // Start/end key query:
            options = new QueryOptions();
            options.SetStartKey("a");
            options.SetEndKey("one");

            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new AList<object>();
            expectedRows.AddItem(dict5);
            expectedRows.AddItem(dict4);
            expectedRows.AddItem(dict1);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(dict5["key"], rows[0].Key);
            Assert.AreEqual(dict4["key"], rows[1].Key);
            Assert.AreEqual(dict1["key"], rows[2].Key);

            // Start/end query without inclusive end:
            options.SetInclusiveEnd(false);
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new AList<object>();
            expectedRows.AddItem(dict5);
            expectedRows.AddItem(dict4);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(dict5["key"], rows[0].Key);
            Assert.AreEqual(dict4["key"], rows[1].Key);

            // Reversed:
            options.SetDescending(true);
            options.SetStartKey("o");
            options.SetEndKey("five");
            options.SetInclusiveEnd(true);
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new AList<object>();
            expectedRows.AddItem(dict4);
            expectedRows.AddItem(dict5);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(dict4["key"], rows[0].Key);
            Assert.AreEqual(dict5["key"], rows[1].Key);

            // Reversed, no inclusive end:
            options.SetInclusiveEnd(false);
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new AList<object>();
            expectedRows.AddItem(dict4);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(dict4["key"], rows[0].Key);

            // Specific keys:
            options = new QueryOptions();
            var keys = new AList<object>();
            keys.AddItem("two");
            keys.AddItem("four");
            options.SetKeys(keys);
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new AList<object>();
            expectedRows.AddItem(dict4);
            expectedRows.AddItem(dict2);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(dict4["key"], rows[0].Key);
            Assert.AreEqual(dict2["key"], rows[1].Key);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestAllDocsQuery()
        {
            var docs = PutDocs(database);
            var expectedRow = new AList<QueryRow>();
            foreach (RevisionInternal rev in docs)
            {
                var value = new Dictionary<string, object>();
                value.Put("rev", rev.GetRevId());
                value.Put("_conflicts", new AList<string>());

                var queryRow = new QueryRow(rev.GetDocId(), 0, rev.GetDocId(), value, null);
                queryRow.Database = database;
                expectedRow.AddItem(queryRow);
            }

            var options = new QueryOptions();
            var allDocs = database.GetAllDocs(options);
            var expectedRows = new AList<QueryRow>();
            expectedRows.AddItem(expectedRow[2]);
            expectedRows.AddItem(expectedRow[0]);
            expectedRows.AddItem(expectedRow[3]);
            expectedRows.AddItem(expectedRow[1]);
            expectedRows.AddItem(expectedRow[4]);

            var expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
            //CollectionAssert.AreEqual(expectedQueryResult, allDocs);
            AssertPropertiesAreEqual(expectedQueryResult, allDocs);

            // Start/end key query:
            options = new QueryOptions();
            options.SetStartKey("2");
            options.SetEndKey("44444");
            allDocs = database.GetAllDocs(options);
            expectedRows = new AList<QueryRow>();
            expectedRows.AddItem(expectedRow[0]);
            expectedRows.AddItem(expectedRow[3]);
            expectedRows.AddItem(expectedRow[1]);
            expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
            Assert.AreEqual(expectedQueryResult, allDocs);

            // Start/end query without inclusive end:
            options.SetInclusiveEnd(false);
            allDocs = database.GetAllDocs(options);
            expectedRows = new AList<QueryRow>();
            expectedRows.AddItem(expectedRow[0]);
            expectedRows.AddItem(expectedRow[3]);
            expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
            Assert.AreEqual(expectedQueryResult, allDocs);

            // Get all documents: with default QueryOptions
            options = new QueryOptions();
            allDocs = database.GetAllDocs(options);
            expectedRows = new AList<QueryRow>();
            expectedRows.AddItem(expectedRow[2]);
            expectedRows.AddItem(expectedRow[0]);
            expectedRows.AddItem(expectedRow[3]);
            expectedRows.AddItem(expectedRow[1]);
            expectedRows.AddItem(expectedRow[4]);
            expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
            Assert.AreEqual(expectedQueryResult, allDocs);

            // Get specific documents:
            options = new QueryOptions();
            IList<object> docIds = new AList<object>();
            QueryRow expected2 = expectedRow[2];
            docIds.AddItem(expected2.Document.Id);
            options.SetKeys(docIds);
            allDocs = database.GetAllDocs(options);
            expectedRows = new AList<QueryRow>();
            expectedRows.AddItem(expected2);
            expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
            Assert.AreEqual(expectedQueryResult, allDocs);
        }

        private IDictionary<string, object> CreateExpectedQueryResult(IList<QueryRow> rows, int offset)
        {
            var result = new Dictionary<string, object>();
            result["rows"] = rows;
            result["total_rows"] = rows.Count;
            result["offset"] = offset;
            return result;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewReduce()
        {
            var docProperties1 = new Dictionary<string, object>();
            docProperties1["_id"] = "CD";
            docProperties1["cost"] = 8.99;
            PutDoc(database, docProperties1);

            var docProperties2 = new Dictionary<string, object>();
            docProperties2["_id"] = "App";
            docProperties2["cost"] = 1.95;
            PutDoc(database, docProperties2);

            IDictionary<string, object> docProperties3 = new Dictionary<string, object>();
            docProperties3["_id"] = "Dessert";
            docProperties3["cost"] = 6.50;
            PutDoc(database, docProperties3);

            View view = database.GetView("totaler");
            view.SetMapReduce((document, emitter) => {
                Assert.IsNotNull (document.Get ("_id"));
                Assert.IsNotNull (document.Get ("_rev"));
                object cost = document.Get ("cost");
                if (cost != null) {
                    emitter (document.Get ("_id"), cost);
                }
            }, (IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce)=> {
                return View.TotalValues(values.ToList());
                }, "1");


            view.UpdateIndex();

            IList<IDictionary<string, object>> dumpResult = view.Dump();
            Log.V(Tag, "View dump: " + dumpResult);
            Assert.AreEqual(3, dumpResult.Count);
            Assert.AreEqual("\"App\"", dumpResult[0]["key"]);
            Assert.AreEqual("1.95", dumpResult[0]["value"]);
            Assert.AreEqual(2, dumpResult[0]["seq"]);
            Assert.AreEqual("\"CD\"", dumpResult[1]["key"]);
            Assert.AreEqual("8.99", dumpResult[1]["value"]);
            Assert.AreEqual(1, dumpResult[1]["seq"]);
            Assert.AreEqual("\"Dessert\"", dumpResult[2]["key"]);
            Assert.AreEqual("6.5", dumpResult[2]["value"]);
            Assert.AreEqual(3, dumpResult[2]["seq"]);
            QueryOptions options = new QueryOptions();
            options.SetReduce(true);

            IList<QueryRow> reduced = view.QueryWithOptions(options).ToList();
            Assert.AreEqual(1, reduced.Count);
            object value = reduced[0].Value;
            double numberValue = (double)value;
            Assert.IsTrue(Math.Abs(numberValue - 17.44) < 0.001);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestIndexUpdateMode()
        {
            View view = CreateView(database);
            Query query = view.CreateQuery();

            query.IndexUpdateMode = IndexUpdateMode.Before;
            int numRowsBefore = query.Run().Count;
            Assert.AreEqual(0, numRowsBefore);

            // do a query and force re-indexing, number of results should be +4
            PutNDocs(database, 1);
            query.IndexUpdateMode = IndexUpdateMode.Before;
            Assert.AreEqual(1, query.Run().Count);

            // do a query without re-indexing, number of results should be the same
            PutNDocs(database, 4);
            query.IndexUpdateMode = IndexUpdateMode.Never;
            Assert.AreEqual(1, query.Run().Count);

            // do a query and force re-indexing, number of results should be +4
            query.IndexUpdateMode = IndexUpdateMode.Before;
            Assert.AreEqual(5, query.Run().Count);

            // do a query which will kick off an async index
            PutNDocs(database, 1);
            query.IndexUpdateMode = IndexUpdateMode.After;
            query.Run();

            // wait until indexing is (hopefully) done
            try
            {
                Sharpen.Thread.Sleep(1 * 1000);
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
            Assert.AreEqual(6, query.Run().Count);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewGrouped()
        {
            IDictionary<string, object> docProperties1 = new Dictionary<string, object>();
            docProperties1["_id"] = "1";
            docProperties1["artist"] = "Gang Of Four";
            docProperties1["album"] = "Entertainment!";
            docProperties1["track"] = "Ether";
            docProperties1["time"] = 231;
            PutDoc(database, docProperties1);

            IDictionary<string, object> docProperties2 = new Dictionary<string, object>();
            docProperties2["_id"] = "2";
            docProperties2["artist"] = "Gang Of Four";
            docProperties2["album"] = "Songs Of The Free";
            docProperties2["track"] = "I Love A Man In Uniform";
            docProperties2["time"] = 248;
            PutDoc(database, docProperties2);

            IDictionary<string, object> docProperties3 = new Dictionary<string, object>();
            docProperties3["_id"] = "3";
            docProperties3["artist"] = "Gang Of Four";
            docProperties3["album"] = "Entertainment!";
            docProperties3["track"] = "Natural's Not In It";
            docProperties3["time"] = 187;
            PutDoc(database, docProperties3);

            IDictionary<string, object> docProperties4 = new Dictionary<string, object>();
            docProperties4["_id"] = "4";
            docProperties4["artist"] = "PiL";
            docProperties4["album"] = "Metal Box";
            docProperties4["track"] = "Memories";
            docProperties4["time"] = 309;
            PutDoc(database, docProperties4);

            IDictionary<string, object> docProperties5 = new Dictionary<string, object>();
            docProperties5["_id"] = "5";
            docProperties5["artist"] = "Gang Of Four";
            docProperties5["album"] = "Entertainment!";
            docProperties5["track"] = "Not Great Men";
            docProperties5["time"] = 187;
            PutDoc(database, docProperties5);

            View view = database.GetView("grouper");
            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=>
            {
                    IList<object> key = new AList<object>();
                    key.AddItem(document["artist"]);
                    key.AddItem(document["album"]);
                    key.AddItem(document["track"]);
                    emitter(key, document["time"]);
            }, (IEnumerable<object> keys, IEnumerable<object> values, bool rereduce) => {
                return View.TotalValues(values.ToList());
            }, "1");
                
            view.UpdateIndex();
            QueryOptions options = new QueryOptions();
            options.SetReduce(true);

            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();
            IList<IDictionary<string, object>> expectedRows = new AList<IDictionary<string, object>>();
            IDictionary<string, object> row1 = new Dictionary<string, object>();
            row1["key"] = null;
            row1["value"] = 1162.0;
            expectedRows.AddItem(row1);
            Assert.AreEqual(row1["key"], rows[0].Key);
            Assert.AreEqual(row1["value"], rows[0].Value);

            //now group
            options.SetGroup(true);
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new AList<IDictionary<string, object>>();
            row1 = new Dictionary<string, object>();
            IList<string> key1 = new AList<string>();
            key1.AddItem("Gang Of Four");
            key1.AddItem("Entertainment!");
            key1.AddItem("Ether");
            row1["key"] = key1;
            row1["value"] = 231.0;
            expectedRows.AddItem(row1);

            IDictionary<string, object> row2 = new Dictionary<string, object>();
            IList<string> key2 = new AList<string>();
            key2.AddItem("Gang Of Four");
            key2.AddItem("Entertainment!");
            key2.AddItem("Natural's Not In It");
            row2["key"] = key2;
            row2["value"] = 187.0;
            expectedRows.AddItem(row2);

            IDictionary<string, object> row3 = new Dictionary<string, object>();
            IList<string> key3 = new AList<string>();
            key3.AddItem("Gang Of Four");
            key3.AddItem("Entertainment!");
            key3.AddItem("Not Great Men");
            row3["key"] = key3;
            row3["value"] = 187.0;
            expectedRows.AddItem(row3);

            IDictionary<string, object> row4 = new Dictionary<string, object>();
            IList<string> key4 = new AList<string>();
            key4.AddItem("Gang Of Four");
            key4.AddItem("Songs Of The Free");
            key4.AddItem("I Love A Man In Uniform");
            row4["key"] = key4;
            row4["value"] = 248.0;
            expectedRows.AddItem(row4);

            IDictionary<string, object> row5 = new Dictionary<string, object>();
            IList<string> key5 = new AList<string>();
            key5.AddItem("PiL");
            key5.AddItem("Metal Box");
            key5.AddItem("Memories");
            row5["key"] = key5;
            row5["value"] = 309.0;
            expectedRows.AddItem(row5);
            Assert.AreEqual(row1["key"], ((JArray)rows[0].Key).Values<String>().ToList());
            Assert.AreEqual(row1["value"], rows[0].Value);
            Assert.AreEqual(row2["key"], ((JArray)rows[1].Key).Values<String>().ToList());
            Assert.AreEqual(row2["value"], rows[1].Value);
            Assert.AreEqual(row3["key"], ((JArray)rows[2].Key).Values<String>().ToList());
            Assert.AreEqual(row3["value"], rows[2].Value);
            Assert.AreEqual(row4["key"], ((JArray)rows[3].Key).Values<String>().ToList());
            Assert.AreEqual(row4["value"], rows[3].Value);
            Assert.AreEqual(row5["key"], ((JArray)rows[4].Key).Values<String>().ToList());
            Assert.AreEqual(row5["value"], rows[4].Value);

            //group level 1
            options.SetGroupLevel(1);
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new AList<IDictionary<string, object>>();
            row1 = new Dictionary<string, object>();
            key1 = new AList<string>();
            key1.AddItem("Gang Of Four");
            row1["key"] = key1;
            row1["value"] = 853.0;

            expectedRows.AddItem(row1);
            row2 = new Dictionary<string, object>();
            key2 = new AList<string>();
            key2.AddItem("PiL");
            row2["key"] = key2;
            row2["value"] = 309.0;
            expectedRows.AddItem(row2);
            Assert.AreEqual(row1["key"], ((JArray)rows[0].Key).Values<String>().ToList());
            Assert.AreEqual(row1["value"], rows[0].Value);
            Assert.AreEqual(row2["key"], ((JArray)rows[1].Key).Values<String>().ToList());
            Assert.AreEqual(row2["value"], rows[1].Value);

            //group level 2
            options.SetGroupLevel(2);
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new AList<IDictionary<string, object>>();
            row1 = new Dictionary<string, object>();
            key1 = new AList<string>();
            key1.AddItem("Gang Of Four");
            key1.AddItem("Entertainment!");
            row1["key"] = key1;
            row1["value"] = 605.0;
            expectedRows.AddItem(row1);
            row2 = new Dictionary<string, object>();
            key2 = new AList<string>();
            key2.AddItem("Gang Of Four");
            key2.AddItem("Songs Of The Free");
            row2["key"] = key2;
            row2["value"] = 248.0;
            expectedRows.AddItem(row2);
            row3 = new Dictionary<string, object>();
            key3 = new AList<string>();
            key3.AddItem("PiL");
            key3.AddItem("Metal Box");
            row3["key"] = key3;
            row3["value"] = 309.0;
            expectedRows.AddItem(row3);
            Assert.AreEqual(row1["key"], ((JArray)rows[0].Key).Values<String>().ToList());
            Assert.AreEqual(row1["value"], rows[0].Value);
            Assert.AreEqual(row2["key"], ((JArray)rows[1].Key).Values<String>().ToList());
            Assert.AreEqual(row2["value"], rows[1].Value);
            Assert.AreEqual(row3["key"], ((JArray)rows[2].Key).Values<String>().ToList());
            Assert.AreEqual(row3["value"], rows[2].Value);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewGroupedStrings()
        {
            IDictionary<string, object> docProperties1 = new Dictionary<string, object>();
            docProperties1["name"] = "Alice";
            PutDoc(database, docProperties1);
            IDictionary<string, object> docProperties2 = new Dictionary<string, object>();
            docProperties2["name"] = "Albert";
            PutDoc(database, docProperties2);
            IDictionary<string, object> docProperties3 = new Dictionary<string, object>();
            docProperties3["name"] = "Naomi";
            PutDoc(database, docProperties3);
            IDictionary<string, object> docProperties4 = new Dictionary<string, object>();
            docProperties4["name"] = "Jens";
            PutDoc(database, docProperties4);
            IDictionary<string, object> docProperties5 = new Dictionary<string, object>();
            docProperties5["name"] = "Jed";
            PutDoc(database, docProperties5);

            View view = database.GetView("default/names");
            view.SetMapReduce((document, emitter) =>
            {
                string name = (string)document["name"];
                if (name != null)
                {
                    emitter(Sharpen.Runtime.Substring(name, 0, 1), 1);
                }
            }, (keys, values, rereduce) => View.TotalValues(values.ToList()), "1.0");

            view.UpdateIndex();
            QueryOptions options = new QueryOptions();
            options.SetGroupLevel(1);

            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();
            IList<IDictionary<string, object>> expectedRows = new AList<IDictionary<string, object>>();
            IDictionary<string, object> row1 = new Dictionary<string, object>();
            row1["key"] = "A";
            row1["value"] = 2;
            expectedRows.AddItem(row1);

            IDictionary<string, object> row2 = new Dictionary<string, object>();
            row2["key"] = "J";
            row2["value"] = 2;
            expectedRows.AddItem(row2);

            IDictionary<string, object> row3 = new Dictionary<string, object>();
            row3["key"] = "N";
            row3["value"] = 1;
            expectedRows.AddItem(row3);

            Assert.AreEqual(row1["key"], rows[0].Key);
            Assert.AreEqual(row1["value"], rows[0].Value);
            Assert.AreEqual(row2["key"], rows[1].Key);
            Assert.AreEqual(row2["value"], rows[1].Value);
            Assert.AreEqual(row3["key"], rows[2].Key);
            Assert.AreEqual(row3["value"], rows[2].Value);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewCollation()
        {
            IList<object> list1 = new AList<object>();
            list1.AddItem("a");
            IList<object> list2 = new AList<object>();
            list2.AddItem("b");
            IList<object> list3 = new AList<object>();
            list3.AddItem("b");
            list3.AddItem("c");
            IList<object> list4 = new AList<object>();
            list4.AddItem("b");
            list4.AddItem("c");
            list4.AddItem("a");
            IList<object> list5 = new AList<object>();
            list5.AddItem("b");
            list5.AddItem("d");
            IList<object> list6 = new AList<object>();
            list6.AddItem("b");
            list6.AddItem("d");
            list6.AddItem("e");

            // Based on CouchDB's "view_collation.js" test
            IList<object> testKeys = new AList<object>();
            testKeys.AddItem(null);
            testKeys.AddItem(false);
            testKeys.AddItem(true);
            testKeys.AddItem(0);
            testKeys.AddItem(2.5);
            testKeys.AddItem(10);
            testKeys.AddItem(" ");
            testKeys.AddItem("_");
            testKeys.AddItem("~");
            testKeys.AddItem("a");
            testKeys.AddItem("A");
            testKeys.AddItem("aa");
            testKeys.AddItem("b");
            testKeys.AddItem("B");
            testKeys.AddItem("ba");
            testKeys.AddItem("bb");
            testKeys.AddItem(list1);
            testKeys.AddItem(list2);
            testKeys.AddItem(list3);
            testKeys.AddItem(list4);
            testKeys.AddItem(list5);
            testKeys.AddItem(list6);

            int i = 0;
            foreach (object key in testKeys)
            {
                IDictionary<string, object> docProperties = new Dictionary<string, object>();
                docProperties.Put("_id", Sharpen.Extensions.ToString(i++));
                docProperties["name"] = key;
                PutDoc(database, docProperties);
            }

            View view = database.GetView("default/names");
            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter) => 
            emitter(document["name"], null), null, "1.0");

            QueryOptions options = new QueryOptions();
            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();
            i = 0;
            foreach (QueryRow row in rows)
            {
                Assert.AreEqual(testKeys[i++], row.Key);
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewCollationRaw()
        {
            IList<object> list1 = new AList<object>();
            list1.AddItem("a");
            IList<object> list2 = new AList<object>();
            list2.AddItem("b");
            IList<object> list3 = new AList<object>();
            list3.AddItem("b");
            list3.AddItem("c");
            IList<object> list4 = new AList<object>();
            list4.AddItem("b");
            list4.AddItem("c");
            list4.AddItem("a");
            IList<object> list5 = new AList<object>();
            list5.AddItem("b");
            list5.AddItem("d");
            IList<object> list6 = new AList<object>();
            list6.AddItem("b");
            list6.AddItem("d");
            list6.AddItem("e");

            // Based on CouchDB's "view_collation.js" test
            IList<object> testKeys = new AList<object>();
            testKeys.AddItem(0);
            testKeys.AddItem(2.5);
            testKeys.AddItem(10);
            testKeys.AddItem(false);
            testKeys.AddItem(null);
            testKeys.AddItem(true);
            testKeys.AddItem(list1);
            testKeys.AddItem(list2);
            testKeys.AddItem(list3);
            testKeys.AddItem(list4);
            testKeys.AddItem(list5);
            testKeys.AddItem(list6);
            testKeys.AddItem(" ");
            testKeys.AddItem("A");
            testKeys.AddItem("B");
            testKeys.AddItem("_");
            testKeys.AddItem("a");
            testKeys.AddItem("aa");
            testKeys.AddItem("b");
            testKeys.AddItem("ba");
            testKeys.AddItem("bb");
            testKeys.AddItem("~");

            int i = 0;
            foreach (object key in testKeys)
            {
                IDictionary<string, object> docProperties = new Dictionary<string, object>();
                docProperties.Put("_id", Sharpen.Extensions.ToString(i++));
                docProperties["name"] = key;
                PutDoc(database, docProperties);
            }

            View view = database.GetView("default/names");
            view.SetMapReduce((document, emitter) => 
            emitter(document["name"], null), null, "1.0");

            view.Collation = ViewCollation.Raw;

            QueryOptions options = new QueryOptions();

            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();

            i = 0;
            foreach (QueryRow row in rows)
            {
                Assert.AreEqual(testKeys[i++], row.Key);
            }
            database.Close();
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestLargerViewQuery()
        {
            PutNDocs(database, 4);
            View view = CreateView(database);
            view.UpdateIndex();

            // Query all rows:
            QueryOptions options = new QueryOptions();
            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();
            Assert.IsNotNull(rows);
            Assert.IsTrue(rows.Count > 0);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestViewLinkedDocs()
        {
            PutLinkedDocs(database);
            View view = database.GetView("linked");
            view.SetMapReduce((document, emitter) =>
            {
                if (document.ContainsKey("value"))
                {
                    emitter(new object[] { document["value"], 0 }, null);
                }
                if (document.ContainsKey("ancestors"))
                {
                    IList<object> ancestors = (IList<object>)document["ancestors"];
                    for (int i = 0; i < ancestors.Count; i++)
                    {
                        IDictionary<string, object> value = new Dictionary<string, object>();
                        value["_id"] = ancestors[i];
                        emitter(new object[] { document["value"], i + 1 }, value);
                    }
                }
            }, null, "1.0");

            view.UpdateIndex();
            QueryOptions options = new QueryOptions();
            options.SetIncludeDocs(true);

            // required for linked documents
            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();
            Assert.IsNotNull(rows);
            Assert.AreEqual(5, rows.Count);

            object[][] expected = new object[][] { 
                new object[] { "22222", "hello", 0, null, "22222" }, 
                new object[] { "22222", "hello", 1, "11111", "11111" }, 
                new object[] { "33333", "world", 0, null, "33333" }, 
                new object[] { "33333", "world", 1, "22222", "22222" }, 
                new object[] { "33333", "world", 2, "11111", "11111" } };

            for (int i = 0; i < rows.Count; i++)
            {
                QueryRow row = rows[i];
                IDictionary<string, object> rowAsJson = row.AsJSONDictionary();
                Log.D(Tag, string.Empty + rowAsJson);
                IList<object> key = (IList<object>)rowAsJson["key"];
                IDictionary<string, object> doc = (IDictionary<string, object>)rowAsJson.Get("doc"
                    );
                string id = (string)rowAsJson["id"];
                Assert.AreEqual(expected[i][0], id);
                Assert.AreEqual(2, key.Count);
                Assert.AreEqual(expected[i][1], key[0]);
                Assert.AreEqual(expected[i][2], key[1]);
                if (expected[i][3] == null)
                {
                    Assert.IsNull(row.Value);
                }
                else
                {
                    Assert.AreEqual(expected[i][3], ((IDictionary<string, object>)row.Value)["_id"]);
                }
                Assert.AreEqual(expected[i][4], doc["_id"]);
            }
        }

        [Test]
        public void TestViewUpdateIndexWithLiveQuery()
        {
            var view = database.GetView("TestViewUpdateWithLiveQuery");
            MapDelegate mapBlock = (document, emitter) => emitter(document["name"], null);
            view.SetMap(mapBlock, "1.0");

            var rowCountAlwaysOne = true;
            var liveQuery = view.CreateQuery().ToLiveQuery();
            liveQuery.Changed += (sender, e) => 
            {
                var count = e.Rows.Count;
                if (count > 0) 
                {
                    rowCountAlwaysOne = rowCountAlwaysOne && (count == 1);
                }
            };

            liveQuery.Start();

            var properties = new Dictionary<string, object>();
            properties.Put("name", "test");
            var doc = database.CreateDocument();
            var rev = doc.PutProperties(properties);
            for (var i = 0; i < 50; i++) {
                rev = rev.CreateRevision(properties);
            }

            // Sleep to ensure that the LiveQuery is done all of its async operations.
            Thread.Sleep(20000);

            liveQuery.Stop();

            Assert.IsTrue(rowCountAlwaysOne);
        }
    }
}
