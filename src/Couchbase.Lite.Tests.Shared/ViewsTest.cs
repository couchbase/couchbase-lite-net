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
/*
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal;
using Couchbase.Lite.Tests;
using Couchbase.Lite.Util;
using Couchbase.Lite.Views;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Couchbase.Lite.Revisions;
using Couchbase.Lite.Storage.SQLCipher;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class ViewsTest : LiteTestCase
    {
        public const string Tag = "Views";

        private Replication pull;
        private LiveQuery query;

        public ViewsTest(string storageType) : base(storageType) {}

        [Test]
        public void TestDeleteViews()
        {
            var views = database.GetAllViews();
            foreach(var v in views) {
                v.Delete();
            }

            Assert.AreEqual(0, database.GetAllViews().Count);
            Assert.IsNull(database.GetExistingView("viewToDelete"));

            var view = database.GetView("viewToDelete");
            Assert.IsNotNull(view);
            Assert.AreEqual(database, view.Database);
            Assert.AreEqual("viewToDelete", view.Name);
            Assert.IsNull(view.Map);
            Assert.AreEqual(view, database.GetExistingView("viewToDelete"));

            // Note: ForestDB view storage created view db when constructor is called
            // but SQLite view storage does not
            if(_storageType == StorageEngineTypes.ForestDB) {
                Assert.AreEqual(1, database.GetAllViews().Count);
            } else {
                Assert.AreEqual(0, database.GetAllViews().Count);
            }

            view.SetMap((doc, emit) =>
            {
                // no-op 
            }, "1");

            Assert.AreEqual(1, database.GetAllViews().Count);
            Assert.AreEqual(view, database.GetAllViews()[0]);
            view.Delete();
            Assert.AreEqual(0, database.GetAllViews().Count);

            var nullView = database.GetExistingView("viewToDelete");
            Assert.IsNull(nullView);
        }

        [Test]
        public void TestQueryParams()
        {
            CreateDocuments(database, 50);
            var view = database.GetView("test_query_params");
            view.SetMap((document, emit) => {
                emit(document["sequence"], document["_id"]);
            }, "1");

            var query = view.CreateQuery();
            var completionWait = new WaitAssert();
            query.Completed += (sender, e) => {
                Assert.IsNull(e.ErrorInfo);
                Assert.IsNotNull(e.Rows);
                completionWait.RunAssert(() => Assert.AreEqual(50, e.Rows.Count));
            };

            query.RunAsync();
            completionWait.WaitForResult(TimeSpan.FromSeconds(5));

            query.InclusiveStart = false;
            query.InclusiveEnd = false;
            completionWait = new WaitAssert();
            query.Completed += (sender, e) => {
                Assert.IsNull(e.ErrorInfo);
                Assert.IsNotNull(e.Rows);
                completionWait.RunAssert(() => Assert.AreEqual(48, e.Rows.Count));
            };

            query.RunAsync();
            completionWait.WaitForResult(TimeSpan.FromSeconds(5));

            query.InclusiveStart = true;
            query.InclusiveEnd = true;

            var allDocsQuery = database.CreateAllDocumentsQuery();
            allDocsQuery.AllDocsMode = AllDocsMode.BySequence;
            var allDocs = allDocsQuery.Run();
            Assert.AreEqual(50, allDocs.Count);
            var delete = true;
            foreach(var row in allDocs) {
                if(delete) {
                    row.Document.Delete();
                }

                delete = !delete;
            }

            allDocsQuery.AllDocsMode = AllDocsMode.AllDocs;
            allDocs = allDocsQuery.Run();
            Assert.AreEqual(25, allDocs.Count);

            allDocsQuery.AllDocsMode = AllDocsMode.IncludeDeleted;
            Assert.AreEqual(50, allDocsQuery.Run().Count);

            allDocsQuery.AllDocsMode = AllDocsMode.BySequence;
            allDocs = allDocsQuery.Run();
            Assert.AreEqual(2, allDocs.First().SequenceNumber);
        }

        [Test]
        public void TestLinq ()
        {
            CreateDocuments (database, 10);
            database.CreateDocument ().PutProperties (new Dictionary<string, object> {
                ["foo"] = "bar",
                ["sequence"] = 100
            });

            var allDocsResult = from row in database.AsQueryable()
                                select row.SequenceNumber;

            var lastSequence = -1L;
            foreach(var docSequence in allDocsResult) {
                Assert.Greater(docSequence, lastSequence);
            }

            var allDocsOrderByResults = from row in database.AsQueryable()
                                        orderby row.DocumentId
                                        select row.DocumentId;

            var lastId = default(string);
            foreach(var docId in allDocsOrderByResults) {
                if(lastId != null) {
                    Assert.IsTrue(docId.CompareTo(lastId) >= 0);
                }

                lastId = docId;
            }

            allDocsOrderByResults = from row in database.AsQueryable()
                                    orderby row.DocumentId descending
                                    select row.DocumentId;

            lastId = null;
            foreach(var docId in allDocsOrderByResults) {
                if(lastId != null) {
                    Assert.IsTrue(docId.CompareTo(lastId) <= 0);
                }

                lastId = docId;
            }

            var query = from row in database.AsQueryable()
                        where row.DocumentProperties.ContainsKey("testName")
                        select (long)row.DocumentProperties["sequence"];

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, query);
            CollectionAssert.AreEqual(new[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }, query.Reverse());
            CollectionAssert.AreEqual(new[] { 5, 6, 7, 8, 9 }, query.Skip(5));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, query.Take(5));

            var reduce = query.Aggregate((x, y) => x + y);
            Assert.AreEqual(45, reduce);
        }

        [Test]
        public void TestEmitNullKey()
        {
            var view = database.GetView("vu");
            Assert.IsNotNull(view);
            view.SetMap((doc, emit) =>
            {
                // null key -> ignored
                emit(null, null);
            }, "1");

            Assert.IsNotNull(view.Map);
            Assert.AreEqual(0, view.TotalRows);

            // insert 1 doc
            var props = new Dictionary<string, object> {
                { "_id", "11111" }
            };
            Assert.DoesNotThrow(() => PutDoc(database, props));

            // regular query
            var testQuery = view.CreateQuery();
            Assert.IsNotNull(testQuery);
            var e = testQuery.Run();
            Assert.IsNotNull(e);
            Assert.AreEqual(0, e.Count);

            // query with null key. it should be ignored.
            testQuery.Keys = new string[] { null };
            e = testQuery.Run();
            Assert.IsNotNull(e);
            Assert.AreEqual(0, e.Count);
        }

        [Test]
        public void TestCustomFilter()
        {
            var view = database.GetView("vu");
            view.SetMap((doc, emit) =>
            {
                emit(doc["name"], doc["skin"]);
            }, "1");

            Assert.IsNotNull(view.Map);

            database.RunInTransaction(() =>
            {
                CreateDocumentWithProperties(database, new Dictionary<string, object> {
                    { "name", "Barry" },
                    { "skin", "none" }
                });
                CreateDocumentWithProperties(database, new Dictionary<string, object> {
                    { "name", "Terry" },
                    { "skin", "furry" }
                });
                CreateDocumentWithProperties(database, new Dictionary<string, object> {
                    { "name", "Wanda" },
                    { "skin", "scaly" }
                });

                return true;
            });

            var query = view.CreateQuery();
            query.PostFilter = row => (row.Value as string).EndsWith("y");
            var rows = query.Run();

            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "furry", "scaly" }, rows.Select(x => x.Value));

            query = view.CreateQuery();
            query.PostFilter = row => (row.Value as string).EndsWith("y");
            query.Limit = 1;
            rows = query.Run();
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("furry", rows.ElementAt(0).Value);

            query.Limit = 0;
            rows = query.Run();
            Assert.AreEqual(0, rows.Count);

            query = view.CreateQuery();
            query.PostFilter = row => (row.Value as string).EndsWith("y");
            query.Skip = 1;
            rows = query.Run();
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("scaly", rows.ElementAt(0).Value);
        }

        [Test]
        public void TestPrefixMatchingString()
        {
            PutDocs(database);
            var view = CreateView(database);
            Assert.AreEqual(StatusCode.Ok, view.UpdateIndex_Internal().Code);

            // Keys with prefix "f":
            var options = new QueryOptions();
            options.StartKey = "f";
            options.EndKey = "f";
            options.PrefixMatchLevel = 1;
            var rows = RowsToDicts(view.QueryWithOptions(options));
            Assert.AreEqual(2, rows.Count);
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "id", "55555" },
                { "key", "five" }
            }, rows[0]);
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "id", "44444" },
                { "key", "four" }
            }, rows[1]);

            // ...descending:
            options.Descending = true;
            rows = RowsToDicts(view.QueryWithOptions(options));
            Assert.AreEqual(2, rows.Count);
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "id", "55555" },
                { "key", "five" }
            }, rows[1]);
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "id", "44444" },
                { "key", "four" }
            }, rows[0]);
        }

        [Test]
        public void TestPrefixMatchingArray()
        {
            PutDocs(database);
            var view = database.GetView("view");
            view.SetMap((doc, emit) =>
            {
                var i = doc.CblID();
                emit(new List<object> { doc.Get("key"), Int32.Parse(i) }, null);
                emit(new List<object> { doc.Get("key"), Int32.Parse(i) / 100 }, null);
            }, "1");

            Assert.AreEqual(StatusCode.Ok, view.UpdateIndex_Internal().Code);

            // Keys starting with "one":
            var options = new QueryOptions();
            options.StartKey = options.EndKey = new List<object> { "one" };
            options.PrefixMatchLevel = 1;
            var rows = RowsToDicts(view.QueryWithOptions(options));
            Assert.AreEqual(2, rows.Count);
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "id", "11111" },
                { "key", new List<object> { "one", 111 } }
            }, rows[0]);
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "id", "11111" },
                { "key", new List<object> { "one", 11111 } }
            }, rows[1]);

            options.Descending = true;
            rows = RowsToDicts(view.QueryWithOptions(options));
            Assert.AreEqual(2, rows.Count);
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "id", "11111" },
                { "key", new List<object> { "one", 111 } }
            }, rows[1]);
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "id", "11111" },
                { "key", new List<object> { "one", 11111 } }
            }, rows[0]);
        }

        #if !NET_3_5
        [Test]
        public void TestParallelViewQueries()
        {
            var vu = database.GetView("prefix/vu");
            vu.SetMap((doc, emit) =>
            {
                emit(new object[] { "sequence", doc["sequence"] }, null);
            }, "1");

            var vu2 = database.GetView("prefix/vu2");
            vu2.SetMap((doc, emit) =>
            {
                emit(new object[] { "sequence", "FAKE" }, null);
            }, "1");

            CreateDocuments(database, 500);

            var expectCount = 1;
            Action<int> queryAction = x =>
            {
                var db = manager.GetDatabase(database.Name);
                var gotVu = db.GetView("prefix/vu");
                var queryObj = gotVu.CreateQuery();
                queryObj.Keys = new object[] { new object[] { "sequence", x } };
                var rows = queryObj.Run();
                Assert.AreEqual(expectCount * 500, gotVu.LastSequenceIndexed);
                Assert.AreEqual(expectCount, rows.Count);
            };

            Action queryAction2 = () =>
            {
                var db = manager.GetDatabase(database.Name);
                var gotVu = db.GetView("prefix/vu2");
                var queryObj = gotVu.CreateQuery();
                queryObj.Keys = new object[] { new object[] { "sequence", "FAKE" } };
                var rows = queryObj.Run();
                Assert.AreEqual(expectCount * 500, gotVu.LastSequenceIndexed);
                Assert.AreEqual(expectCount * 500, rows.Count);
            };

            Parallel.Invoke(() => queryAction(42), () => queryAction(184), 
                () => queryAction(256), queryAction2, () => queryAction(412));

            CreateDocuments(database, 500);
            expectCount = 2;

            Parallel.Invoke(() => queryAction(42), () => queryAction(184), 
                () => queryAction(256), queryAction2, () => queryAction(412));

            vu.Delete();

            vu = database.GetView("prefix/vu");
            vu.SetMap((doc, emit) =>
            {
                emit(new object[] { "sequence", doc["sequence"] }, null);
            }, "1");

            Parallel.Invoke(() => queryAction(42), () => queryAction(184), 
                () => queryAction(256), queryAction2, () => queryAction(412));

            vu2.Delete();
            vu2 = database.GetView("prefix/vu2");
            vu2.SetMap((doc, emit) =>
            {
                emit(new object[] { "sequence", "FAKE" }, null);
            }, "1");

            Parallel.Invoke(() => queryAction(42), () => queryAction(184), 
                () => queryAction(256), queryAction2, () => queryAction(412));
        }
        #endif

        [Test] 
        public void TestIssue490()
        {
            var sg = new CouchDB("http", GetReplicationServer());
            using (var remoteDb = sg.CreateDatabase("issue490")) {

                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                CreateFilteredDocuments(database, 30);
                CreateNonFilteredDocuments (database, 10);
                RunReplication(push);
                Assert.IsTrue(push.ChangesCount==40);
                Assert.IsTrue(push.CompletedChangesCount==40);

                Assert.IsNull(push.LastError);
                Assert.AreEqual(40, database.GetDocumentCount());

                for (int i = 0; i <= 5; i++) {
                    pull = database.CreatePullReplication(remoteDb.RemoteUri);
                    pull.Continuous = true;
                    pull.Start ();
                    Task.Delay (1000).Wait();
                    CallToView ();
                    Task.Delay (2000).Wait();
                    RecreateDatabase ();
                }
            }

        }

        private void RecreateDatabase ()
        {
            query.Stop ();
            pull.Stop ();
            //database.Manager.ForgetDatabase(database);
            database.Delete ();
            database = Manager.SharedInstance.GetDatabase ("test");
        }

        private void CallToView ()
        {
            var the_view = database.GetView ("testView");
            the_view.SetMap (delegate(IDictionary<string, object> document, EmitDelegate emit) {
                try{
                    emit (null, document);
                } catch(Exception ex){
                    Debug.WriteLine(ex);
                }
            }, "0.1.2");
            query = the_view.CreateQuery ().ToLiveQuery();
            query.Changed += delegate(object sender, QueryChangeEventArgs e) {
                Debug.WriteLine("changed!");
            };
            query.Start ();
        }

        private void CreateNonFilteredDocuments(Database db, int n)
        {
            //TODO should be changed to use db.runInTransaction
            for (int i = 0; i < n; i++)
            {
                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties.Add("testName", "unimportant");
                properties.Add("sequence", i);
                CreateDocumentWithProperties(db, properties);
            }
        }

        private void CreateFilteredDocuments(Database db, int n)
        {
            //TODO should be changed to use db.runInTransaction
            for (int i = 0; i < n; i++)
            {
                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties.Add("testName", "important");
                properties.Add("sequence", i);
                CreateDocumentWithProperties(db, properties);
            }
        }

        [Test]
        public void TestViewValueIsEntireDoc()
        {
            var view = database.GetView("vu");
            view.SetMap((doc, emit) => emit(doc.CblID(), doc), "0.1");
            CreateDocuments(database, 10);
            var rows = view.CreateQuery().Run();
            foreach (var row in rows) {
                Assert.IsNotNull(row.Value);
                var dict = row.Value.AsDictionary<string, object>();
                Assert.IsNotNull(dict);
                Assert.AreEqual(row.Key, dict.CblID());
            }
        }

        [Test]
        public void TestLiveQueryUpdateWhenOptionsChanged()
        {
            var view = database.GetView("vu");
            view.SetMap((doc, emit) =>
                emit(doc.Get("sequence"), null), "1");

            CreateDocuments(database, 5);

            var query = view.CreateQuery();
            var result = query.Run();
            Assert.AreEqual(5, result.Count);

            int expectedKey = 0;
            foreach (var row in result) {
                Assert.AreEqual(expectedKey++, row.Key);
            }

            var liveQuery = view.CreateQuery().ToLiveQuery();
            var changeCount = 0;
            liveQuery.Changed += (sender, e) => changeCount++;
            liveQuery.Start();
            Sleep(1000);

            Assert.AreEqual(1, changeCount);
            Assert.AreEqual(5, liveQuery.Rows.Count);
            expectedKey = 0;
            foreach (var row in liveQuery.Rows) {
                Assert.AreEqual(expectedKey++, row.Key);
            }

            liveQuery.StartKey = 2;
            liveQuery.QueryOptionsChanged();
            Sleep(1000);

            Assert.AreEqual(2, changeCount);
            Assert.AreEqual(3, liveQuery.Rows.Count);
            expectedKey = 2;
            foreach (var row in liveQuery.Rows) {
                Assert.AreEqual(expectedKey++, row.Key);
            }

            liveQuery.Stop();
        }

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
            var rev = new RevisionInternal(props);
            rev = db.PutRevision(rev, null, false);
            return rev;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        private void PutDocViaUntitledDoc(Database db, IDictionary<string, object> props)
        {
            var document = db.CreateDocument();
            document.PutProperties(props);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        private IList<RevisionInternal> PutDocs(Database db)
        {
            var result = new List<RevisionInternal>();

            var dict2 = new Dictionary<string, object>();
            dict2["_id"] = "22222";
            dict2["key"] = "two";
            result.Add(PutDoc(db, dict2));

            var dict4 = new Dictionary<string, object>();
            dict4["_id"] = "44444";
            dict4["key"] = "four";
            result.Add(PutDoc(db, dict4));

            var dict1 = new Dictionary<string, object>();
            dict1["_id"] = "11111";
            dict1["key"] = "one";
            result.Add(PutDoc(db, dict1));

            var dict3 = new Dictionary<string, object>();
            dict3["_id"] = "33333";
            dict3["key"] = "three";
            result.Add(PutDoc(db, dict3));

            var dict5 = new Dictionary<string, object>();
            dict5["_id"] = "55555";
            dict5["key"] = "five";
            result.Add(PutDoc(db, dict5));

            return result;
        }

        // http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        private IList<RevisionInternal> PutLinkedDocs(Database db)
        {
            var result = new List<RevisionInternal>();

            var dict1 = new Dictionary<string, object>();
            dict1["_id"] = "11111";
            result.Add(PutDoc(db, dict1));

            var dict2 = new Dictionary<string, object>();
            dict2["_id"] = "22222";
            dict2["value"] = "hello";
            dict2["ancestors"] = new string[] { "11111" };
            result.Add(PutDoc(db, dict2));

            var dict3 = new Dictionary<string, object>();
            dict3["_id"] = "33333";
            dict3["value"] = "world";
            dict3["ancestors"] = new string[] { "22222", "11111" };
            result.Add(PutDoc(db, dict3));

            return result;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void PutNDocs(Database db, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var doc = new Dictionary<string, object>();
                doc["_id"] = string.Format("{0}", i);

                var key = new List<string>();
                for (int j = 0; j < 256; j++)
                {
                    key.Add("key");
                }
                key.Add(string.Format("key-{0}", i));
                doc["key"] = key;

                PutDocViaUntitledDoc(db, doc);
            }
        }

        public static View CreateView(Database db)
        {
            var view = db.GetView("aview");
            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=>
                {
                    Assert.IsNotNull(document.CblID());
                    Assert.IsNotNull(document.CblRev());
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

                Assert.IsNotNull(document.CblID());
                Assert.IsNotNull(document.CblRev());

                if (document.ContainsKey("key") && document["key"] != null)
                {
                    emitter(document["key"], null);
                }
            };
            view.SetMap(mapBlock, "1");


            //Assert.AreEqual(1, view.Id);
            Assert.IsTrue(view.IsStale);
            view.UpdateIndex_Internal();

            IList<IDictionary<string, object>> dumpResult = view.Storage.Dump().ToList();
            WriteDebug("View dump: " + dumpResult);
            Assert.AreEqual(3, dumpResult.Count);
            Assert.AreEqual("\"one\"", dumpResult[0]["key"]);
            Assert.AreEqual(1, dumpResult[0]["seq"]);
            Assert.AreEqual("\"two\"", dumpResult[2]["key"]);
            Assert.AreEqual(2, dumpResult[2]["seq"]);
            Assert.AreEqual("\"three\"", dumpResult[1]["key"]);
            Assert.AreEqual(3, dumpResult[1]["seq"]);

            //no-op reindex
            Assert.IsFalse(view.IsStale);
            view.UpdateIndex_Internal();

            // Now add a doc and update a doc:
            var threeUpdated = new RevisionInternal(rev3.DocID, rev3.RevID, false);
            numTimesMapFunctionInvoked = numTimesInvoked;

            var newdict3 = new Dictionary<string, object>();
            newdict3["key"] = "3hree";
            threeUpdated.SetProperties(newdict3);

            rev3 = database.PutRevision(threeUpdated, rev3.RevID, false);

            // Reindex again:
            Assert.IsTrue(view.IsStale);
            view.UpdateIndex_Internal();

            // Make sure the map function was only invoked one more time (for the document that was added)
            Assert.AreEqual(numTimesMapFunctionInvoked + 1, numTimesInvoked);

            var dict4 = new Dictionary<string, object>();
            dict4["key"] = "four";
            var rev4 = PutDoc(database, dict4);
            var twoDeleted = new RevisionInternal(rev2.DocID, rev2.RevID, true);
            database.PutRevision(twoDeleted, rev2.RevID, false);

            // Reindex again:
            Assert.IsTrue(view.IsStale);
            view.UpdateIndex_Internal();
            dumpResult = view.Storage.Dump().ToList();
            WriteDebug("View dump: " + dumpResult);
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
            Assert.AreEqual(rev1.DocID, rows[2].DocumentId);
            Assert.AreEqual("3hree", rows[0].Key);
            Assert.AreEqual(rev3.DocID, rows[0].DocumentId);
            Assert.AreEqual("four", rows[1].Key);
            Assert.AreEqual(rev4.DocID, rows[1].DocumentId);
            view.DeleteIndex();
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewQuery()
        {
            PutDocs(database);
            var view = CreateView(database);
            view.UpdateIndex_Internal();

            // Query all rows:
            QueryOptions options = new QueryOptions();
            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();

            var expectedRows = new List<object>();

            var dict5 = new Dictionary<string, object>();
            dict5["id"] = "55555";
            dict5["key"] = "five";
            expectedRows.Add(dict5);

            var dict4 = new Dictionary<string, object>();
            dict4["id"] = "44444";
            dict4["key"] = "four";
            expectedRows.Add(dict4);

            var dict1 = new Dictionary<string, object>();
            dict1["id"] = "11111";
            dict1["key"] = "one";
            expectedRows.Add(dict1);

            var dict3 = new Dictionary<string, object>();
            dict3["id"] = "33333";
            dict3["key"] = "three";
            expectedRows.Add(dict3);

            var dict2 = new Dictionary<string, object>();
            dict2["id"] = "22222";
            dict2["key"] = "two";
            expectedRows.Add(dict2);
            Assert.AreEqual(5, rows.Count);
            Assert.AreEqual(dict5["key"], rows[0].Key);
            Assert.AreEqual(dict4["key"], rows[1].Key);
            Assert.AreEqual(dict1["key"], rows[2].Key);
            Assert.AreEqual(dict3["key"], rows[3].Key);
            Assert.AreEqual(dict2["key"], rows[4].Key);

            // Start/end key query:
            options = new QueryOptions();
            options.StartKey = "a";
            options.EndKey = "one";

            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new List<object>();
            expectedRows.Add(dict5);
            expectedRows.Add(dict4);
            expectedRows.Add(dict1);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(dict5["key"], rows[0].Key);
            Assert.AreEqual(dict4["key"], rows[1].Key);
            Assert.AreEqual(dict1["key"], rows[2].Key);

            // Start/end query without inclusive end:
            options.InclusiveEnd = false;
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new List<object>();
            expectedRows.Add(dict5);
            expectedRows.Add(dict4);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(dict5["key"], rows[0].Key);
            Assert.AreEqual(dict4["key"], rows[1].Key);

            // Reversed:
            options.Descending = true;
            options.StartKey = "o";
            options.EndKey = "five";
            options.InclusiveEnd = true;
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new List<object>();
            expectedRows.Add(dict4);
            expectedRows.Add(dict5);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(dict4["key"], rows[0].Key);
            Assert.AreEqual(dict5["key"], rows[1].Key);

            // Reversed, no inclusive end:
            options.InclusiveEnd = false;
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new List<object>();
            expectedRows.Add(dict4);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(dict4["key"], rows[0].Key);

            // Specific keys: (note that rows should be in same order as input keys, not sorted)
            options = new QueryOptions();
            var keys = new List<object>();
            keys.Add("two");
            keys.Add("four");
            options.Keys = keys;
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new List<object>();
            expectedRows.Add(dict4);
            expectedRows.Add(dict2);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(dict2["key"], rows[0].Key);
            Assert.AreEqual(dict4["key"], rows[1].Key);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestViewQueryWithDictSentinel()
        {
            var key1 = new List<string>();
            key1.Add("red");
            key1.Add("model1");
            var dict1 = new Dictionary<string, object>();
            dict1.Add("id", "11");
            dict1.Add("key", key1);
            PutDoc(database, dict1);

            var key2 = new List<string>();
            key2.Add("red");
            key2.Add("model2");
            var dict2 = new Dictionary<string, object>();
            dict2.Add("id", "12");
            dict2.Add("key", key2);
            PutDoc(database, dict2);

            var key3 = new List<string>();
            key3.Add("green");
            key3.Add("model1");
            var dict3 = new Dictionary<string, object>();
            dict3.Add("id", "21");
            dict3.Add("key", key3);
            PutDoc(database, dict3);

            var key4 = new List<string>();
            key4.Add("yellow");
            key4.Add("model2");
            var dict4 = new Dictionary<string, object>();
            dict4.Add("id", "31");
            dict4.Add("key", key4);
            PutDoc(database, dict4);

            var view = CreateView(database);

            view.UpdateIndex_Internal();

            // Query all rows:
            QueryOptions options = new QueryOptions();
            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();

            Assert.AreEqual(4, rows.Count);
            Assert.AreEqual(new object[] { "green", "model1" }, ((JArray)rows[0].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "red", "model1" }, ((JArray)rows[1].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "red", "model2" }, ((JArray)rows[2].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "yellow", "model2" }, ((JArray)rows[3].Key).ToObject<object[]>());

            // Start/end key query:
            options = new QueryOptions();
            options.StartKey = "a";
            options.EndKey = new List<object> { "red", new Dictionary<string, object>() };
            rows = view.QueryWithOptions(options).ToList();
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(new object[] { "green", "model1" }, ((JArray)rows[0].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "red", "model1" }, ((JArray)rows[1].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "red", "model2" }, ((JArray)rows[2].Key).ToObject<object[]>());

            // Start/end query without inclusive end:
            options.EndKey = new List<object> { "red", "model1" };
            options.InclusiveEnd = false;
            rows = view.QueryWithOptions(options).ToList();
            Assert.AreEqual(1, rows.Count); //1
            Assert.AreEqual(new object[] { "green", "model1" }, ((JArray)rows[0].Key).ToObject<object[]>());

            // Reversed:
            options = new QueryOptions();
            options.StartKey = new List<object> { "red", new Dictionary<string, object>() };
            options.EndKey = new List<object> { "green", "model1" };
            options.Descending = true;
            rows = view.QueryWithOptions(options).ToList();
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(new object[] { "red", "model2" }, ((JArray)rows[0].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "red", "model1" }, ((JArray)rows[1].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "green", "model1" }, ((JArray)rows[2].Key).ToObject<object[]>());

            // Reversed, no inclusive end:
            options.InclusiveEnd = false;
            rows = view.QueryWithOptions(options).ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(new object[] { "red", "model2" }, ((JArray)rows[0].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "red", "model1" }, ((JArray)rows[1].Key).ToObject<object[]>());

            // Specific keys:
            options = new QueryOptions();
            IList<object> keys = new List<object>();
            keys.Add(new object[] { "red", "model2" });
            keys.Add(new object[] { "red", "model1" });
            options.Keys = keys;
            rows = view.QueryWithOptions(options).ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(new object[] { "red", "model2" }, ((JArray)rows[0].Key).ToObject<object[]>());
            Assert.AreEqual(new object[] { "red", "model1" }, ((JArray)rows[1].Key).ToObject<object[]>());
        }

        [Test]
        public void TestLiveQueryStartEndKey()
        {
            var view = CreateView(database);

            var query = view.CreateQuery();
            query.StartKey = "one";
            query.EndKey = "one\uFEFF";
            var liveQuery = query.ToLiveQuery();
            Assert.IsNotNull(liveQuery.StartKey);
            Assert.IsNotNull(liveQuery.EndKey);

            liveQuery.Start();
            Sleep(2000);
            Assert.AreEqual(0, liveQuery.Rows.Count);

            PutDocs(database);
            Sleep(2000);
            Assert.AreEqual(1, liveQuery.Rows.Count);
        }

        [Test]
        public void TestAllDocsLiveQuery()
        {
            var query = database.CreateAllDocumentsQuery().ToLiveQuery();
            query.Start();
            var docs = PutDocs(database);
            var expectedRowBase = new List<IDictionary<string, object>>(docs.Count);
            foreach (RevisionInternal rev in docs)
            {
                expectedRowBase.Add(new Dictionary<string, object> {
                    { "id", rev.DocID },
                    { "key", rev.DocID },
                    { "value", new Dictionary<string, object> {
                            { "rev", rev.RevID }
                        }
                    }
                });
            }

            var mre = new AutoResetEvent(false);

            query.Changed += (sender, e) => {
                if(e.Rows.Count < expectedRowBase.Count) {
                    return;
                }

                AssertEnumerablesAreEqual(expectedRowBase, e.Rows);
                mre.Set();
            };

            Assert.IsTrue(mre.WaitOne(TimeSpan.FromSeconds(30)), "Live query timed out");

            expectedRowBase.Add(new Dictionary<string, object> {
                { "id", "66666" },
                { "key", "66666" },
                { "value", new Dictionary<string, object> {
                        { "rev", "1-abcdef" }
                    }
                }
            });

            var dict6 = new Dictionary<string, object>();
            dict6.SetDocRevID("66666", "1-adcdef");
            dict6["key"] = "six";
            PutDoc(database, dict6);

            Assert.IsTrue(mre.WaitOne(TimeSpan.FromSeconds(30)), "Live query timed out");
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestAllDocsQuery()
        {
            var docs = PutDocs(database);
            var expectedRowBase = new List<IDictionary<string, object>>(docs.Count);
            foreach (RevisionInternal rev in docs)
            {
                expectedRowBase.Add(new Dictionary<string, object> {
                    { "id", rev.DocID },
                    { "key", rev.DocID },
                    { "value", new Dictionary<string, object> {
                            { "rev", rev.RevID }
                        }
                    }
                });
            }

            // Create a conflict, won by the old revision:
            var props = new Dictionary<string, object> {
                { "_id", "44444" },
                { "_rev", "1-00" }, // lower revID, will lose conflict
                { "key", "40ur" }
            };

            var leaf2 = new RevisionInternal(props);
            database.ForceInsert(leaf2, null, null);
            Assert.AreEqual(docs[1].RevID, database.GetDocument("44444", null, true).RevID);

            // Query all rows:
            var options = new QueryOptions();
            var allDocs = database.GetAllDocs(options);
            var expectedRows = new List<IDictionary<string, object>> {
                expectedRowBase[2],
                expectedRowBase[0],
                expectedRowBase[3],
                expectedRowBase[1],
                expectedRowBase[4]
            };

            Assert.AreEqual(expectedRows, RowsToDicts(allDocs));

            // Limit:
            options.Limit = 1;
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[2] };
            Assert.AreEqual(expectedRows, RowsToDicts(allDocs));

            // Limit+Skip:
            options.Skip = 2;
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[3] };
            Assert.AreEqual(expectedRows, RowsToDicts(allDocs));

            // Start/end key query:
            options = new QueryOptions();
            options.StartKey = "2";
            options.EndKey = "44444";
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[0], expectedRowBase[3], expectedRowBase[1] };
            Assert.AreEqual(expectedRows, RowsToDicts(allDocs));

            // Start/end query without inclusive end:
            options.InclusiveEnd = false;
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[0], expectedRowBase[3] };
            Assert.AreEqual(expectedRows, RowsToDicts(allDocs));

            // Get zero specific documents:
            options = new QueryOptions();
            options.Keys = new List<object>();
            allDocs = database.GetAllDocs(options);
            Assert.IsTrue(allDocs == null || !allDocs.Any());

            // Get specific documents:
            options = new QueryOptions();
            options.Keys = new List<object> {
                expectedRowBase[2].GetCast<string>("id"),
                expectedRowBase[3].GetCast<string>("id")
            };
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[2], expectedRowBase[3] };
            Assert.AreEqual(expectedRows, RowsToDicts(allDocs));

            // Delete a document:
            var del = docs[0];
            del = new RevisionInternal(del.DocID, del.RevID, true);
            del = database.PutRevision(del, del.RevID, false);

            // Get deleted doc, and one bogus one:
            options = new QueryOptions();
            options.Keys = new List<object> { "BOGUS", expectedRowBase[0].GetCast<string>("id") };
            allDocs = database.GetAllDocs(options);
            var expectedResult = new List<IDictionary<string, object>> {
                new Dictionary<string, object> {
                    { "key", "BOGUS" },
                    { "error", "not_found" }
                },
                new Dictionary<string, object> {
                    { "id", del.DocID },
                    { "key", del.DocID },
                    { "value", new Dictionary<string, object> {
                            { "rev", del.RevID },
                            { "deleted", true }
                        }
                    }
                }
            };
            Assert.AreEqual(expectedResult, RowsToDicts(allDocs));

            // Get conflicts:
            options = new QueryOptions();
            options.AllDocsMode = AllDocsMode.ShowConflicts;
            allDocs = database.GetAllDocs(options);
            var curRevId = docs[1].RevID;
            var expectedConflict1 = new Dictionary<string, object> {
                { "id", "44444" },
                { "key", "44444" },
                { "value", new Dictionary<string, object> {
                        { "rev", curRevId },
                        { "_conflicts", new List<string> {
                                curRevId.ToString(), "1-00"
                            }
                        }
                    }
                }
            };

            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[2], expectedRowBase[3], expectedConflict1,
                expectedRowBase[4]
            };
                
            Assert.AreEqual(expectedRows, RowsToDicts(allDocs));

            // Get _only_ conflicts:
            options.AllDocsMode = AllDocsMode.OnlyConflicts;
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedConflict1 };
            Assert.AreEqual(expectedRows, RowsToDicts(allDocs));
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
                Assert.IsNotNull (document.CblID());
                Assert.IsNotNull (document.CblRev());
                object cost = document.Get ("cost");
                if (cost != null) {
                    emitter (document.CblID(), cost);
                }
            }, BuiltinReduceFunctions.Sum, "1");


            view.UpdateIndex_Internal();

            IList<IDictionary<string, object>> dumpResult = view.Storage.Dump().ToList();
            WriteDebug("View dump: " + dumpResult);
            Assert.AreEqual(3, dumpResult.Count);
            Assert.AreEqual("\"App\"", dumpResult[0]["key"]);
            Assert.AreEqual("1.95", dumpResult[0]["val"]);
            Assert.AreEqual(2, dumpResult[0]["seq"]);
            Assert.AreEqual("\"CD\"", dumpResult[1]["key"]);
            Assert.AreEqual("8.99", dumpResult[1]["val"]);
            Assert.AreEqual(1, dumpResult[1]["seq"]);
            Assert.AreEqual("\"Dessert\"", dumpResult[2]["key"]);
            Assert.AreEqual("6.5", dumpResult[2]["val"]);
            Assert.AreEqual(3, dumpResult[2]["seq"]);
            QueryOptions options = new QueryOptions();
            options.Reduce = true;

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
                Sleep(1 * 1000);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception during TestIndexUpdateMode", e);
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
            view.SetMapReduce((document, emitter) =>
            {
                    IList<object> key = new List<object>();
                    key.Add(document["artist"]);
                    key.Add(document["album"]);
                    key.Add(document["track"]);
                    emitter(key, document["time"]);
            }, BuiltinReduceFunctions.Sum, "1");
                
            view.UpdateIndex_Internal();
            QueryOptions options = new QueryOptions();
            options.Reduce = true;

            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();
            IList<IDictionary<string, object>> expectedRows = new List<IDictionary<string, object>>();
            IDictionary<string, object> row1 = new Dictionary<string, object>();
            row1["key"] = null;
            row1["value"] = 1162.0;
            expectedRows.Add(row1);
            Assert.AreEqual(row1["key"], rows[0].Key);
            Assert.AreEqual(row1["value"], rows[0].Value);

            //now group
            options.Group = true;
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new List<IDictionary<string, object>>();
            row1 = new Dictionary<string, object>();
            IList<string> key1 = new List<string>();
            key1.Add("Gang Of Four");
            key1.Add("Entertainment!");
            key1.Add("Ether");
            row1["key"] = key1;
            row1["value"] = 231.0;
            expectedRows.Add(row1);

            IDictionary<string, object> row2 = new Dictionary<string, object>();
            IList<string> key2 = new List<string>();
            key2.Add("Gang Of Four");
            key2.Add("Entertainment!");
            key2.Add("Natural's Not In It");
            row2["key"] = key2;
            row2["value"] = 187.0;
            expectedRows.Add(row2);

            IDictionary<string, object> row3 = new Dictionary<string, object>();
            IList<string> key3 = new List<string>();
            key3.Add("Gang Of Four");
            key3.Add("Entertainment!");
            key3.Add("Not Great Men");
            row3["key"] = key3;
            row3["value"] = 187.0;
            expectedRows.Add(row3);

            IDictionary<string, object> row4 = new Dictionary<string, object>();
            IList<string> key4 = new List<string>();
            key4.Add("Gang Of Four");
            key4.Add("Songs Of The Free");
            key4.Add("I Love A Man In Uniform");
            row4["key"] = key4;
            row4["value"] = 248.0;
            expectedRows.Add(row4);

            IDictionary<string, object> row5 = new Dictionary<string, object>();
            IList<string> key5 = new List<string>();
            key5.Add("PiL");
            key5.Add("Metal Box");
            key5.Add("Memories");
            row5["key"] = key5;
            row5["value"] = 309.0;
            expectedRows.Add(row5);
            Assert.AreEqual(row1["key"], rows[0].Key.AsList<string>());
            Assert.AreEqual(row1["value"], rows[0].Value);
            Assert.AreEqual(row2["key"], rows[1].Key.AsList<string>());
            Assert.AreEqual(row2["value"], rows[1].Value);
            Assert.AreEqual(row3["key"], rows[2].Key.AsList<string>());
            Assert.AreEqual(row3["value"], rows[2].Value);
            Assert.AreEqual(row4["key"], rows[3].Key.AsList<string>());
            Assert.AreEqual(row4["value"], rows[3].Value);
            Assert.AreEqual(row5["key"], rows[4].Key.AsList<string>());
            Assert.AreEqual(row5["value"], rows[4].Value);

            //group level 1
            options.GroupLevel = 1;
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new List<IDictionary<string, object>>();
            row1 = new Dictionary<string, object>();
            key1 = new List<string>();
            key1.Add("Gang Of Four");
            row1["key"] = key1;
            row1["value"] = 853.0;

            expectedRows.Add(row1);
            row2 = new Dictionary<string, object>();
            key2 = new List<string>();
            key2.Add("PiL");
            row2["key"] = key2;
            row2["value"] = 309.0;
            expectedRows.Add(row2);
            Assert.AreEqual(row1["key"], rows[0].Key.AsList<object>());
            Assert.AreEqual(row1["value"], rows[0].Value);
            Assert.AreEqual(row2["key"], rows[1].Key.AsList<object>());
            Assert.AreEqual(row2["value"], rows[1].Value);

            //group level 2
            options.GroupLevel = 2;
            rows = view.QueryWithOptions(options).ToList();
            expectedRows = new List<IDictionary<string, object>>();
            row1 = new Dictionary<string, object>();
            key1 = new List<string>();
            key1.Add("Gang Of Four");
            key1.Add("Entertainment!");
            row1["key"] = key1;
            row1["value"] = 605.0;
            expectedRows.Add(row1);
            row2 = new Dictionary<string, object>();
            key2 = new List<string>();
            key2.Add("Gang Of Four");
            key2.Add("Songs Of The Free");
            row2["key"] = key2;
            row2["value"] = 248.0;
            expectedRows.Add(row2);
            row3 = new Dictionary<string, object>();
            key3 = new List<string>();
            key3.Add("PiL");
            key3.Add("Metal Box");
            row3["key"] = key3;
            row3["value"] = 309.0;
            expectedRows.Add(row3);
            Assert.AreEqual(row1["key"], rows[0].Key.AsList<object>());
            Assert.AreEqual(row1["value"], rows[0].Value);
            Assert.AreEqual(row2["key"], rows[1].Key.AsList<object>());
            Assert.AreEqual(row2["value"], rows[1].Value);
            Assert.AreEqual(row3["key"], rows[2].Key.AsList<object>());
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
                    emitter(name.Substring(0, 1), 1);
                }
            }, BuiltinReduceFunctions.Sum, "1.0");

            view.UpdateIndex_Internal();
            QueryOptions options = new QueryOptions();
            options.GroupLevel = 1;

            IList<QueryRow> rows = view.QueryWithOptions(options).ToList();
            IList<IDictionary<string, object>> expectedRows = new List<IDictionary<string, object>>();
            IDictionary<string, object> row1 = new Dictionary<string, object>();
            row1["key"] = "A";
            row1["value"] = 2;
            expectedRows.Add(row1);

            IDictionary<string, object> row2 = new Dictionary<string, object>();
            row2["key"] = "J";
            row2["value"] = 2;
            expectedRows.Add(row2);

            IDictionary<string, object> row3 = new Dictionary<string, object>();
            row3["key"] = "N";
            row3["value"] = 1;
            expectedRows.Add(row3);

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
            IList<object> list1 = new List<object>();
            list1.Add("a");
            IList<object> list2 = new List<object>();
            list2.Add("b");
            IList<object> list3 = new List<object>();
            list3.Add("b");
            list3.Add("c");
            IList<object> list4 = new List<object>();
            list4.Add("b");
            list4.Add("c");
            list4.Add("a");
            IList<object> list5 = new List<object>();
            list5.Add("b");
            list5.Add("d");
            IList<object> list6 = new List<object>();
            list6.Add("b");
            list6.Add("d");
            list6.Add("e");

            // Based on CouchDB's "view_collation.js" test
            IList<object> testKeys = new List<object>();
            testKeys.Add(null);
            testKeys.Add(false);
            testKeys.Add(true);
            testKeys.Add(0);
            testKeys.Add(2.5);
            testKeys.Add(10);
            testKeys.Add(" ");
            testKeys.Add("_");
            testKeys.Add("~");
            testKeys.Add("a");
            testKeys.Add("A");
            testKeys.Add("aa");
            testKeys.Add("b");
            testKeys.Add("B");
            testKeys.Add("ba");
            testKeys.Add("bb");
            testKeys.Add(list1);
            testKeys.Add(list2);
            testKeys.Add(list3);
            testKeys.Add(list4);
            testKeys.Add(list5);
            testKeys.Add(list6);

            int i = 0;
            foreach (object key in testKeys)
            {
                IDictionary<string, object> docProperties = new Dictionary<string, object>();
                docProperties["_id"] = (i++).ToString();
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
            IList<object> list1 = new List<object>();
            list1.Add("a");
            IList<object> list2 = new List<object>();
            list2.Add("b");
            IList<object> list3 = new List<object>();
            list3.Add("b");
            list3.Add("c");
            IList<object> list4 = new List<object>();
            list4.Add("b");
            list4.Add("c");
            list4.Add("a");
            IList<object> list5 = new List<object>();
            list5.Add("b");
            list5.Add("d");
            IList<object> list6 = new List<object>();
            list6.Add("b");
            list6.Add("d");
            list6.Add("e");

            // Based on CouchDB's "view_collation.js" test
            IList<object> testKeys = new List<object>();
            testKeys.Add(0);
            testKeys.Add(2.5);
            testKeys.Add(10);
            testKeys.Add(false);
            testKeys.Add(null);
            testKeys.Add(true);
            testKeys.Add(list1);
            testKeys.Add(list2);
            testKeys.Add(list3);
            testKeys.Add(list4);
            testKeys.Add(list5);
            testKeys.Add(list6);
            testKeys.Add(" ");
            testKeys.Add("A");
            testKeys.Add("B");
            testKeys.Add("_");
            testKeys.Add("a");
            testKeys.Add("aa");
            testKeys.Add("b");
            testKeys.Add("ba");
            testKeys.Add("bb");
            testKeys.Add("~");

            int i = 0;
            foreach (object key in testKeys)
            {
                IDictionary<string, object> docProperties = new Dictionary<string, object>();
                docProperties["_id"] = (i++).ToString();
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
            view.UpdateIndex_Internal();

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

            view.UpdateIndex_Internal();
            QueryOptions options = new QueryOptions();
            options.IncludeDocs = true;

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
                WriteDebug(string.Empty + rowAsJson);
                IList<object> key = (IList<object>)rowAsJson["key"];
                IDictionary<string, object> doc = (IDictionary<string, object>)rowAsJson.Get("doc");
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
                    Assert.AreEqual(expected[i][3], ((IDictionary<string, object>)row.Value).CblID());
                }
                Assert.AreEqual(expected[i][4], doc.CblID());
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
            properties["name"] = "test";
            SavedRevision rev = null;
            database.RunInTransaction(() =>
            {
                var doc = database.CreateDocument();
                rev = doc.PutProperties(properties);
                return true;
            });
            for (var i = 0; i < 50; i++) {
                rev = rev.CreateRevision(properties);
            }
            // Sleep to ensure that the LiveQuery is done all of its async operations.
            Sleep(8000);

            liveQuery.Stop();

            Assert.IsTrue(rowCountAlwaysOne);
        }

        [Test]
        public void TestRunLiveQueriesWithReduce()
        {
            var view = database.GetView("vu");
            view.SetMapReduce((document, emit) => emit(document["sequence"], 1), 
                BuiltinReduceFunctions.Sum, "1");

            var query = view.CreateQuery().ToLiveQuery();

            var view1 = database.GetView("vu1");
            view1.SetMapReduce((document, emit) => emit(document["sequence"], 1), 
                BuiltinReduceFunctions.Sum, "1");

            var query1 = view1.CreateQuery().ToLiveQuery();

            const Int32 numDocs = 10;
            CreateDocumentsAsync(database, numDocs);

            Assert.IsNull(query.Rows);
            query.Start();

            var gotExpectedQueryResult = new CountdownEvent(1);
            query.Changed += (sender, e) => 
            {
                Assert.IsNull(e.Error);
                if (e.Rows.Count == 1 && Convert.ToInt32(e.Rows.ElementAt(0).Value) == numDocs)
                {
                    gotExpectedQueryResult.Signal();
                }
            };

            var success = gotExpectedQueryResult.Wait(TimeSpan.FromSeconds(60));
            Assert.IsTrue(success);
            query.Stop();

            query1.Start();

            CreateDocumentsAsync(database, numDocs + 5); //10 + 10 + 5

            var gotExpectedQuery1Result = new CountdownEvent(1);
            query1.Changed += (sender, e) => 
            {
                Assert.IsNull(e.Error);
                if (e.Rows.Count == 1 && Convert.ToInt32(e.Rows.ElementAt(0).Value) == (2 * numDocs) + 5)
                {
                    gotExpectedQuery1Result.Signal();
                }
            };

            success = gotExpectedQuery1Result.Wait(TimeSpan.FromSeconds(10));
            Assert.IsTrue(success);
            query1.Stop();

            Assert.AreEqual((2 * numDocs) + 5, database.GetDocumentCount());
        }

        [Test]
        public void TestViewIndexSkipsDesignDocs() 
        {
            var view = CreateView(database);

            var designDoc = new Dictionary<string, object>() 
            {
                {"_id", "_design/test"},
                {"key", "value"}
            };
            PutDoc(database, designDoc);

            view.UpdateIndex_Internal();
            var rows = view.QueryWithOptions(null);
            Assert.AreEqual(0, rows.Count());
        }

        [Test]
        public void TestViewNumericKeys() {
            var dict = new Dictionary<string, object>()
            { 
                {"_id", "22222"},
                {"referenceNumber", 33547239},
                {"title", "this is the title"}

            };
            PutDoc(database, dict);

            var view = CreateView(database);
            view.SetMap((document, emit) =>
            {
                if (document.ContainsKey("referenceNumber"))
                {
                    emit(document["referenceNumber"], document);
                }
            }, "1");

            var query = view.CreateQuery();
            query.StartKey = 33547239;
            query.EndKey = 33547239;
            var rows = query.Run();
            Assert.AreEqual(1, rows.Count());
            Assert.AreEqual(33547239, rows.ElementAt(0).Key);
        }
            
        [Test]
        public void TestViewQueryStartKeyDocID()
        {
            PutDocs(database);

            var result = new List<RevisionInternal>();

            var dict = new Dictionary<string, object>() 
            {
                {"_id", "11112"},
                {"key", "one"}
            };
            result.Add(PutDoc(database, dict));
            var view = CreateView(database);
            view.UpdateIndex_Internal();

            var options = new QueryOptions();
            options.StartKey = "one";
            options.StartKeyDocId = "11112";
            options.EndKey = "three";
            var rows = view.QueryWithOptions(options).ToList<QueryRow>();

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("11112", rows[0].DocumentId);
            Assert.AreEqual("one", rows[0].Key);
            Assert.AreEqual("33333", rows[1].DocumentId);
            Assert.AreEqual("three", rows[1].Key);

            options = new QueryOptions();
            options.EndKey = "one";
            options.EndKeyDocId = "11111";
            rows = view.QueryWithOptions(options).ToList<QueryRow>();

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("55555", rows[0].DocumentId);
            Assert.AreEqual("five", rows[0].Key);
            Assert.AreEqual("44444", rows[1].DocumentId);
            Assert.AreEqual("four", rows[1].Key);
            Assert.AreEqual("11111", rows[2].DocumentId);
            Assert.AreEqual("one", rows[2].Key);

            options.StartKey = "one";
            options.StartKeyDocId = "11111";
            rows = view.QueryWithOptions(options).ToList<QueryRow>();

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("11111", rows[0].DocumentId);
            Assert.AreEqual("one", rows[0].Key);
        }

        private SavedRevision CreateTestRevisionNoConflicts(Document doc, string val) {
            var unsavedRev = doc.CreateRevision();
            var props = new Dictionary<string, object>() 
            {
                {"key", val}
            };
            unsavedRev.SetUserProperties(props);
            return unsavedRev.Save();
        }

        [Test]
        public void TestViewWithConflict() {
            // Create doc and add some revs
            var doc = database.CreateDocument();
            var rev1 = CreateTestRevisionNoConflicts(doc, "1");
            Assert.IsNotNull(rev1);
            var rev2a = CreateTestRevisionNoConflicts(doc, "2a");
            Assert.IsNotNull(rev2a);
            var rev3 = CreateTestRevisionNoConflicts(doc, "3");
            Assert.IsNotNull(rev3);

            // index the view
            var view = CreateView(database);
            var rows = view.CreateQuery().Run();
            Assert.AreEqual(1, rows.Count);
            var row = rows.ElementAt(0);
            Assert.AreEqual(row.Key, "3");

            // TODO: Why is this null?
            //Assert.IsNotNull(row.DocumentRevisionId);

            // Create a conflict
            var rev2bUnsaved = rev1.CreateRevision();
            var props = new Dictionary<string, object>() 
            {
                {"key", "2b"}
            };
            rev2bUnsaved.SetUserProperties(props);
            var rev2b = rev2bUnsaved.Save(true);
            Assert.IsNotNull(rev2b);

            // re-run query
            view.UpdateIndex_Internal();
            rows = view.CreateQuery().Run();

            // we should only see one row, with key=3.
            // if we see key=2b then it's a bug.
            Assert.AreEqual(1, rows.Count);
            row = rows.ElementAt(0);
            Assert.AreEqual(row.Key, "3");
        }

        [Test]
        public void TestMultipleQueriesOnSameView()
        {
            var view = database.GetView("view1");
            view.SetMapReduce((doc, emit) =>
            {
                emit(doc["jim"], doc.CblID());
            }, (keys, vals, rereduce) => 
            {
                return keys.Count();
            }, "1");
            var query1 = view.CreateQuery().ToLiveQuery();
            query1.Start();

            var query2 = view.CreateQuery().ToLiveQuery();
            query2.Start();

            var docIdTimestamp = Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
            for(int i = 0; i < 50; i++) {
                database.GetDocument(string.Format("doc{0}-{1}", i, docIdTimestamp)).PutProperties(new Dictionary<string, object> { {
                        "jim",
                        "borden"
                    } });
            }

            Sleep(5000);
            Assert.AreEqual(50, view.TotalRows);
            Assert.AreEqual(50, view.LastSequenceIndexed);

            query1.Stop();
            for(int i = 50; i < 60; i++) {
                database.GetDocument(string.Format("doc{0}-{1}", i, docIdTimestamp)).PutProperties(new Dictionary<string, object> { {
                        "jim",
                        "borden"
                    } });
                if (i == 55) {
                    query1.Start();
                }
            }

            Sleep(5000);
            Assert.AreEqual(60, view.TotalRows);
            Assert.AreEqual(60, view.LastSequenceIndexed);
        }

        [Test]
        public void TestMapConflicts()
        {
            var view = database.GetView("vu");
            Assert.IsNotNull(view);
            view.SetMap((d, emit) =>
                emit(d.CblID(), d.Get("_conflicts")), "1");

            var doc = CreateDocumentWithProperties(database, new Dictionary<string, object> { { "foo", "bar" } });
            var rev1 = doc.CurrentRevision;
            var properties = rev1.Properties;
            properties["tag"] = "1";
            var rev2a = default(SavedRevision);
            Assert.DoesNotThrow(() => rev2a = doc.PutProperties(properties));

            // No conflicts:
            var query = view.CreateQuery();
            var rows = query.Run();
            Assert.AreEqual(1, rows.Count);
            var row = rows.ElementAt(0);
            Assert.AreEqual(doc.Id, row.Key);
            Assert.IsNull(row.Value);

            // Create a conflict revision:
            properties["tag"] = "2";
            var newRev = rev1.CreateRevision();
            newRev.SetProperties(properties);
            var rev2b = default(SavedRevision);
            Assert.DoesNotThrow(() => rev2b = newRev.Save(true));

            rows = query.Run();
            Assert.AreEqual(1, rows.Count);
            row = rows.ElementAt(0);
            Assert.AreEqual(doc.Id, row.Key);
            var conflicts = new List<string> { rev2a.Id };
            CollectionAssert.AreEqual(conflicts, row.ValueAs<IEnumerable<string>>());

            // Create another conflict revision:
            properties["tag"] = "3";
            newRev = rev1.CreateRevision();
            newRev.SetProperties(properties);
            Assert.DoesNotThrow(() => newRev.Save(true));

            rows = query.Run();
            Assert.AreEqual(1, rows.Count);
            row = rows.ElementAt(0);
            Assert.AreEqual(doc.Id, row.Key);
            conflicts = new List<string> { rev2a.Id, rev2b.Id };
            CollectionAssert.AreEquivalent(conflicts, row.ValueAs<IEnumerable<string>>());
        }

        [Test]
        public void TestViewWithDocDeletion()
        {
            TestViewWithDocRemoval(false);
        }

        [Test]
        public void TestViewWithDocPurge()
        {
            TestViewWithDocRemoval(true);
        }

        private void TestViewWithDocRemoval(bool purge)
        {
            var view = database.GetView("vu");
            Assert.IsNotNull(view);
            view.SetMap((doc, emit) =>
            {
                var type = doc.GetCast<string>("type");
                if(type == "task") {
                    var date = doc.Get("created_at");
                    var listId = doc.Get("list_id");
                    emit(new[] { listId, date }, doc);
                }
            }, "1");

            Assert.IsNotNull(view.Map);
            Assert.AreEqual(0, view.TotalRows);

            const string insertListId = "list1";

            var doc1 = CreateDocumentWithProperties(database, new Dictionary<string, object> {
                { "_id", "doc1" },
                { "type", "task" },
                { "created_at", DateTime.Now },
                { "list_id", insertListId }
            });
            Assert.IsNotNull(doc1);

            var doc2 = CreateDocumentWithProperties(database, new Dictionary<string, object> {
                { "_id", "doc2" },
                { "type", "task" },
                { "created_at", DateTime.Now },
                { "list_id", insertListId }
            });
            Assert.IsNotNull(doc2);

            var doc3 = CreateDocumentWithProperties(database, new Dictionary<string, object> {
                { "_id", "doc3" },
                { "type", "task" },
                { "created_at", DateTime.Now },
                { "list_id", insertListId }
            });
            Assert.IsNotNull(doc3);

            var query = view.CreateQuery();
            query.Descending = true;
            query.StartKey = new object[] { insertListId, new Dictionary<string, object>() };
            query.EndKey = new[] { insertListId };

            var rows = default(QueryEnumerator);
            Assert.DoesNotThrow(() => rows = query.Run());
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(doc3.Id, rows.ElementAt(0).DocumentId);
            Assert.AreEqual(doc2.Id, rows.ElementAt(1).DocumentId);
            Assert.AreEqual(doc1.Id, rows.ElementAt(2).DocumentId);

            if (purge) {
                doc2.Purge();
            } else {
                doc2.Delete();
            }

            // Check ascending query result:
            query.Descending = false;
            query.StartKey = new[] { insertListId };
            query.EndKey = new object[] { insertListId, new Dictionary<string, object>() };
            rows = query.Run();
            Trace.WriteLine(String.Format("Ascending query: rows = {0}", rows));
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(doc1.Id, rows.ElementAt(0).DocumentId);
            Assert.AreEqual(doc3.Id, rows.ElementAt(1).DocumentId);

            // Check descending query result:
            query.Descending = true;
            query.StartKey = new object[] { insertListId, new Dictionary<string, object>() };
            query.EndKey = new[] { insertListId };
            rows = query.Run();
            Trace.WriteLine(String.Format("Descending query: rows = {0}", rows));
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(doc3.Id, rows.ElementAt(0).DocumentId);
            Assert.AreEqual(doc1.Id, rows.ElementAt(1).DocumentId);
        }

        [Test] // Issue 710
        public void TestViewsInTransaction ()
        {
            if (_storageType != "SQLite") {
                Assert.Inconclusive ("Only valid for a SQLite backing store");
            }

            var foo = default (View);
            var bar = default (View);
            database.RunInTransaction (() => {
                foo = database.GetView ("foo");
                foo.SetMap ((document, emit) => {
                    emit ("test", null);
                }, "1");

                bar = database.GetView ("bar");
                bar.SetMapReduce ((document, emit) => {
                    emit ("test", null);
                }, BuiltinReduceFunctions.Count, "1");

                return true;
            });

            Assert.AreEqual (1, ((SqliteViewStore)foo.Storage).ViewID);
            Assert.AreEqual (2, ((SqliteViewStore)bar.Storage).ViewID);
        }

        private IList<IDictionary<string, object>> RowsToDicts(IEnumerable<QueryRow> allDocs)
        {
            Assert.IsNotNull(allDocs);
            var rows = new List<IDictionary<string, object>>();
            foreach (var row in allDocs) {
                rows.Add(row.AsJSONDictionary());
            }

            return rows;
        }
    }
}
