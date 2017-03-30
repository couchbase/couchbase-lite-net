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
using Couchbase.Lite.Views;
using Couchbase.Lite.Storage.SQLCipher;
using FluentAssertions;
using System.Globalization;

namespace Couchbase.Lite
{
    [NUnit.Framework.TestFixture("ForestDB")]
    public class ViewsTest : LiteTestCase
    {
        public const string Tag = "Views";

        private Replication pull;
        private LiveQuery query;

        public ViewsTest(string storageType) : base(storageType) {}

        [NUnit.Framework.Test]
        public void TestReduceWithSkipAndLimit()
        {
            var view = database.GetView("reduceme");
            view.SetMapReduce((doc, emit) =>
            {
                emit(new[] { (long)doc["sequence"] / 5, doc["_id"] }, doc["sequence"]);
            }, BuiltinReduceFunctions.Sum, "1");

            CreateDocuments(database, 20);

            var query = view.CreateQuery();
            query.Skip = 2;
            query.Limit = 2;
            query.GroupLevel = 1;
            var result = query.Run();
            result.Should().HaveCount(2, "because that was the set limit");
            result.ElementAt(0).ValueAs<int>().Should().Be(10+11+12+13+14, "because the sum function should add");
            result.ElementAt(1).ValueAs<int>().Should().Be(15+16+17+18+19, "because the sum function should add");

            query.Skip = 3;
            query.Limit = 2;
            result = query.Run();
            result.Should().HaveCount(1, "because after skipping 3 only one should remain");
            result.ElementAt(0).ValueAs<int>().Should().Be(15 + 16 + 17 + 18 + 19, "because the sum function should add");

            query.Skip = 0;
            query.Limit = 10;
            result = query.Run();
            result.Should().HaveCount(4, "because even though the limit is 10, there are only four rows");
            result.ElementAt(0).ValueAs<int>().Should().Be(0 + 1 + 2 + 3 + 4, "because the sum function should add");
            result.ElementAt(1).ValueAs<int>().Should().Be(5 + 6 + 7 + 8 + 9, "because the sum function should add");
            result.ElementAt(2).ValueAs<int>().Should().Be(10 + 11 + 12 + 13 + 14, "because the sum function should add");
            result.ElementAt(3).ValueAs<int>().Should().Be(15 + 16 + 17 + 18 + 19, "because the sum function should add");
        }

        [NUnit.Framework.Test]
        public void TestDeleteViews()
        {
            var views = database.GetAllViews();
            foreach(var v in views) {
                v.Delete();
            }

            database.GetAllViews().Should().BeEmpty("because we should start with no views");
            database.GetExistingView("viewToDelete").Should().BeNull("because there should be no existing views");

            var view = database.GetView("viewToDelete");
            view.Should().NotBeNull("because GetView creates non-existent views");
            view.Database.Should().Be(view.Database, "because that is the database it was created from");
            view.Name.Should().BeEquivalentTo("viewToDelete", "because that is the name it was given");
            view.Map.Should().BeNull("beacuse no map has been assigned yet");
            database.GetExistingView("viewToDelete").Should().Be(view, "because after a view is created subsequent calls should return the same instance");

            // Note: ForestDB view storage created view db when constructor is called
            // but SQLite view storage does not
            if(_storageType == StorageEngineTypes.ForestDB) {
                database.GetAllViews().Should().HaveCount(1, "because there should only be one view created so far");
            } else {
                database.GetAllViews().Should().HaveCount(0, "because the view should not be fully created until the map is set");
            }

            view.SetMap((doc, emit) =>
            {
                // no-op 
            }, "1");


            database.GetAllViews().Should().HaveCount(1, "because there should only be one view created so far");
            database.GetAllViews()[0].Should().Be(view, "because only one instance of the view should exist");
            view.Delete();
            database.GetAllViews().Should().BeEmpty("because the view was deleted");

            var nullView = database.GetExistingView("viewToDelete").Should().BeNull("because the view was deleted");
        }

        [NUnit.Framework.Test]
        public void TestQueryParams()
        {
            CreateDocuments(database, 50);
            var view = database.GetView("test_query_params");
            view.SetMap((document, emit) => {
                emit(document["sequence"], document["_id"]);
            }, "1");

            var query = view.CreateQuery();
            var completionWait = new WaitAssert();
            int expected = 50;
            query.Completed += (sender, e) => {
                completionWait.RunAssert(() => {
                    e.ErrorInfo.Should().BeNull("because there should not be an error completing the query");
                    e.Rows.Should().HaveCount(expected, "because the query should return the correct number of results");
                });
            };

            query.RunAsync();
            completionWait.WaitForResult(TimeSpan.FromSeconds(5));

            //query.InclusiveStart = false;
            //query.InclusiveEnd = false;
            completionWait = new WaitAssert();
            //expected = 48;

            query.RunAsync();
            completionWait.WaitForResult(TimeSpan.FromSeconds(500));

            query.InclusiveStart = true;
            query.InclusiveEnd = true;

            var allDocsQuery = database.CreateAllDocumentsQuery();
            allDocsQuery.AllDocsMode = AllDocsMode.BySequence;
            var allDocs = allDocsQuery.Run();
            allDocs.Should().HaveCount(50, "because there are 50 documents");
            var delete = true;
            foreach(var row in allDocs) {
                if(delete) {
                    row.Document.Delete();
                }

                delete = !delete;
            }

            allDocsQuery.AllDocsMode = AllDocsMode.AllDocs;
            allDocsQuery.Run().Should().HaveCount(25, "because there are 25 documents after deleting half");

            allDocsQuery.AllDocsMode = AllDocsMode.IncludeDeleted;
            allDocsQuery.Run().Should().HaveCount(50, "because there are 50 documents when deleted ones are included");

            allDocsQuery.AllDocsMode = AllDocsMode.BySequence;
            allDocs = allDocsQuery.Run();
            allDocs.FirstOrDefault().Should().NotBeNull().And.Match<QueryRow>(x => x.SequenceNumber == 2, "because the first non-deleted document by sequence has a sequence number of 2");
        }

        [NUnit.Framework.Test]
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
                docSequence.Should().BeGreaterThan(lastSequence, "because the results should be in order by sequence");
            }

            var allDocsOrderByResults = from row in database.AsQueryable()
                                        orderby row.DocumentId
                                        select row.DocumentId;

            var lastId = default(string);
            foreach(var docId in allDocsOrderByResults) {
                if(lastId != null) {
                    docId.CompareTo(lastId).Should().BeGreaterOrEqualTo(0, "because the results should be in order by document ID");
                }

                lastId = docId;
            }

            allDocsOrderByResults = from row in database.AsQueryable()
                                    orderby row.DocumentId descending
                                    select row.DocumentId;

            lastId = null;
            foreach(var docId in allDocsOrderByResults) {
                if(lastId != null) {
                    docId.CompareTo(lastId).Should().BeLessOrEqualTo(0, "because the results should be in descending order by document ID");
                }

                lastId = docId;
            }

            var query = from row in database.AsQueryable()
                        where row.DocumentProperties.ContainsKey("testName")
                        select (long)row.DocumentProperties["sequence"];

            query.Should().Equal(new[] { 0L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L }, "because the results of the LINQ query should be correct");
            query.Reverse().Should().Equal(new[] { 9L, 8L, 7L, 6L, 5L, 4L, 3L, 2L, 1L, 0L }, "because the results should be reversable");
            query.Skip(5).Should().Equal(new[] { 5L, 6L, 7L, 8L, 9L }, "because the results should be skippable");
            query.Take(5).Should().Equal(new[] { 0L, 1L, 2L, 3L, 4L }, "because the results should be limitable");

            var reduce = query.Aggregate((x, y) => x + y);
            reduce.Should().Be(45, "because the results should be reducable");

            var multipleQuery = from row in database.AsQueryable()
                        where row.DocumentProperties.ContainsKey("testName")
                        select new object[]{ (long)row.DocumentProperties["sequence"], (string)row.DocumentProperties["testName"] };

            multipleQuery.ToArray()[0].Should().Equal(new object[] { 0L, "testDatabase" });
        }

        [NUnit.Framework.Test]
        public void TestEmitNullKey()
        {
            var view = database.GetView("vu");
            view.Should().NotBeNull("because the database should create views on demand");
            view.SetMap((doc, emit) =>
            {
                // null key -> ignored
                emit(null, null);
            }, "1");

            view.Should().Match<View>(v => v.Map != null && view.TotalRows == 0, "because the view should start empty");

            // insert 1 doc
            PutDoc(database, new Dictionary<string, object> {
                ["_id"] = "11111"
            });

            // regular query
            var testQuery = view.CreateQuery();
            testQuery.Should().NotBeNull("because queries should never return null");
            testQuery.Run().Should().NotBeNull().And.HaveCount(0, "because the view should ignore null keys");

            // query with null key. it should be ignored.
            testQuery.Keys = new string[] { null };
            testQuery.Run().Should().NotBeNull().And.HaveCount(0, "because the view should ignore null keys");
        }

        [NUnit.Framework.Test]
        public void TestCustomFilter()
        {
            var view = database.GetView("vu");
            view.SetMap((doc, emit) =>
            {
                emit(doc["name"], doc["skin"]);
            }, "1");

            view.Map.Should().NotBeNull("because it was just set");

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

            query.Run().Should().HaveCount(2, "because only the values of two rows end in 'y'");
            rows.Select(x => x.Value).Should().BeEquivalentTo(new[] { "furry", "scaly" }, "because those two values end in 'y'");

            query = view.CreateQuery();
            query.PostFilter = row => (row.Value as string).EndsWith("y");
            query.Limit = 1;
            rows = query.Run();
            rows.Should().HaveCount(1, "because the query was limited to 1 row");
            rows.ElementAt(0).Value.Should().Be("furry", "because that is the first result");

            query.Limit = 0;
            query.Run().Should().BeEmpty("because the limit is 0");

            query = view.CreateQuery();
            query.PostFilter = row => (row.Value as string).EndsWith("y");
            query.Skip = 1;
            rows = query.Run();
            rows.Should().HaveCount(1, "because there are two rows and one was skipped");
            rows.ElementAt(0).Value.Should().Be("scaly", "because that is the second result");
        }

        [NUnit.Framework.Test]
        public void TestPrefixMatchingString()
        {
            PutDocs(database);
            var view = CreateView(database);
            view.UpdateIndex_Internal().Code.Should().Be(StatusCode.Ok, "because this index update should succeed");

            // Keys with prefix "f":
            var options = new QueryOptions();
            options.StartKey = "f";
            options.EndKey = "f";
            options.PrefixMatchLevel = 1;
            var rows = RowsToDicts(view.QueryWithOptions(options));
            rows.Should().HaveCount(2, "because only two keys start with 'f'");
            rows[0].Should().ContainKey("id").WhichValue.Should().Be("55555", "because five comes before four in the alphabet");
            rows[0].Should().ContainKey("key").WhichValue.Should().Be("five", "because five comes before four in the alphabet");
            rows[1].Should().ContainKey("id").WhichValue.Should().Be("44444", "because four comes after five in the alphabet");
            rows[1].Should().ContainKey("key").WhichValue.Should().Be("four", "because four comes after five in the alphabet");


            // ...descending:
            options.Descending = true;
            rows = RowsToDicts(view.QueryWithOptions(options));
            rows.Should().HaveCount(2, "because only two keys start with 'f'");
            rows[1].Should().ContainKey("id").WhichValue.Should().Be("55555", "because five comes before four in the alphabet");
            rows[1].Should().ContainKey("key").WhichValue.Should().Be("five", "because five comes before four in the alphabet");
            rows[0].Should().ContainKey("id").WhichValue.Should().Be("44444", "because four comes after five in the alphabet");
            rows[0].Should().ContainKey("key").WhichValue.Should().Be("four", "because four comes after five in the alphabet");
        }

        [NUnit.Framework.Test]
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

            view.UpdateIndex_Internal().Code.Should().Be(StatusCode.Ok, "because the index update should succeed");

            // Keys starting with "one":
            var options = new QueryOptions();
            options.StartKey = options.EndKey = new List<object> { "one" };
            options.PrefixMatchLevel = 1;
            var rows = RowsToDicts(view.QueryWithOptions(options));
         
            rows.Should().HaveCount(2, "because only two rows start with 'one'");
            rows[0].Should().ContainKey("id").WhichValue.Should().Be("11111");
            rows[0].Should().ContainKey("key").WhichValue.AsList<object>().Should().Equal(new List<object> { "one", 111L }, (left, right) =>
            {
                if(left is string) {
                    return ((string)left).Equals((string)right);
                }

                return ((IConvertible)left).ToInt64(CultureInfo.InvariantCulture) == (long)right;
            }, "because 111 comes before 11111");
            rows[1].Should().ContainKey("id").WhichValue.Should().Be("11111");
            rows[1].Should().ContainKey("key").WhichValue.AsList<object>().Should().Equal(new List<object> { "one", 11111L }, (left, right) =>
            {
                if(left is string) {
                    return ((string)left).Equals((string)right);
                }

                return ((IConvertible)left).ToInt64(CultureInfo.InvariantCulture) == (long)right;
            },"because 111 comes before 11111");

            options.Descending = true;
            rows = RowsToDicts(view.QueryWithOptions(options));
            rows[1].Should().ContainKey("id").WhichValue.Should().Be("11111");
            rows[1].Should().ContainKey("key").WhichValue.AsList<object>().Should().Equal(new List<object> { "one", 111L }, (left, right) =>
            {
                if(left is string) {
                    return ((string)left).Equals((string)right);
                }

                return ((IConvertible)left).ToInt64(CultureInfo.InvariantCulture) == (long)right;
            },
                 "because 111 comes before 11111");
            rows[0].Should().ContainKey("id").WhichValue.Should().Be("11111");
            rows[0].Should().ContainKey("key").WhichValue.AsList<object>().Should().Equal(new List<object> { "one", 11111L }, (left, right) =>
            {
                if(left is string) {
                    return ((string)left).Equals((string)right);
                }

                return ((IConvertible)left).ToInt64(CultureInfo.InvariantCulture) == (long)right;
            },
                 "because 111 comes before 11111");
        }

        #if !NET_3_5
        [NUnit.Framework.Test]
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
                gotVu.LastSequenceIndexed.Should().Be(expectCount * 500);
                rows.Count.Should().Be(expectCount);
            };

            Action queryAction2 = () =>
            {
                var db = manager.GetDatabase(database.Name);
                var gotVu = db.GetView("prefix/vu2");
                var queryObj = gotVu.CreateQuery();
                queryObj.Keys = new object[] { new object[] { "sequence", "FAKE" } };
                var rows = queryObj.Run();
                gotVu.LastSequenceIndexed.Should().Be(expectCount * 500);
                rows.Count.Should().Be(expectCount * 500);
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

        [NUnit.Framework.Test] 
        public async Task TestIssue490()
        {
            var sg = new CouchDB("http", GetReplicationServer());
            using (var remoteDb = sg.CreateDatabase("issue490")) {

                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                CreateFilteredDocuments(database, 30);
                CreateNonFilteredDocuments (database, 10);
                RunReplication(push);
                push.ChangesCount.Should().Be(40, "because there should be 40 documents");
                push.CompletedChangesCount.Should().Be(40, "because there should be 40 documents");
                push.LastError.Should().BeNull("because the push should succeed");
                database.GetDocumentCount().Should().Be(40, "because there should be 40 documents");

                for (int i = 0; i <= 5; i++) {
                    pull = database.CreatePullReplication(remoteDb.RemoteUri);
                    pull.Continuous = true;
                    pull.Start ();
                    await Task.Delay(1000);
                    CallToView ();
                    await Task.Delay(2000);
                    RecreateDatabase ();
                }
            }

        }

        private void RecreateDatabase ()
        {
            database.Delete ();
            database = Manager.SharedInstance.GetDatabase ("test");
        }

        private void CallToView ()
        {
            var the_view = database.GetView ("testView");
            the_view.SetMap ((document, emit) => {
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
            db.RunInTransaction(() =>
            {
                for(int i = 0; i < n; i++) {
                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add("testName", "unimportant");
                    properties.Add("sequence", i);
                    CreateDocumentWithProperties(db, properties);
                }

                return true;
            });
        }

        private void CreateFilteredDocuments(Database db, int n)
        {
            db.RunInTransaction(() =>
            {
                for(int i = 0; i < n; i++) {
                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add("testName", "important");
                    properties.Add("sequence", i);
                    CreateDocumentWithProperties(db, properties);
                }

                return true;
            });
        }

        [NUnit.Framework.Test]
        public void TestViewValueIsEntireDoc()
        {
            var view = database.GetView("vu");
            view.SetMap((doc, emit) => emit(doc.CblID(), doc), "0.1");
            CreateDocuments(database, 10);
            var rows = view.CreateQuery().Run();
            foreach (var row in rows) {
                row.Value.AsDictionary<string, object>().Should().NotBeNull()
                    .And.Subject.CblID().Should().Be(row.Key as string);
            }
        }

        [NUnit.Framework.Test]
        public void TestLiveQueryUpdateWhenOptionsChanged()
        {
            var view = database.GetView("vu");
            view.SetMap((doc, emit) =>
                emit(doc.Get("sequence"), null), "1");

            CreateDocuments(database, 5);

            var query = view.CreateQuery();
            query.Run().Should().HaveCount(5).And.Subject.Select(x => ExtensionMethods.CastOrDefault<long>(x.Key)).Should().Equal(0L, 1L, 2L, 3L, 4L);

            var liveQuery = view.CreateQuery().ToLiveQuery();
            var are = new AutoResetEvent(false);
            liveQuery.Changed += (sender, e) => are.Set();
            liveQuery.Start();
            are.WaitOne(1000, true).Should().BeTrue();
            liveQuery.Rows.Should().HaveCount(5).And.Subject.Select(x => ExtensionMethods.CastOrDefault<long>(x.Key)).Should().Equal(0L, 1L, 2L, 3L, 4L);

            liveQuery.StartKey = 2;
            liveQuery.QueryOptionsChanged();
            are.WaitOne(1000, true).Should().BeTrue();
            liveQuery.Rows.Should().HaveCount(3).And.Subject.Select(x => ExtensionMethods.CastOrDefault<long>(x.Key)).Should().Equal(2L, 3L, 4L);

            liveQuery.Stop();
        }

        [NUnit.Framework.Test]
        public void TestQueryDefaultIndexUpdateMode()
        {
            View view = database.GetView("aview");
            Query query = view.CreateQuery();
            query.IndexUpdateMode.Should().Be(IndexUpdateMode.Before, "because that is the default");
        }

        [NUnit.Framework.Test]
        public void TestViewCreation()
        {
            database.GetExistingView("aview").Should().BeNull("because the view should not exist yet");
            var view = database.GetView("aview");
            view.Should().NotBeNull("because the view should be created via GetView");
            view.Database.Should().BeSameAs(database, "because the view should reference the DB it was created from");
            view.Name.Should().Be("aview", "because that was the name it was given");
            view.Map.Should().BeNull("because the map has not been assigned yet");
            view.Should().BeSameAs(database.GetExistingView("aview"), "because the next call should return the same view");

            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=> { }, null, "1")
                .Should().BeTrue("because otherwise setting the map function failed");

            database.GetAllViews().Should().HaveCount(1, "because the view should be in the all view collection now")
                .And.Subject.AsList<View>()[0].Should().BeSameAs(view, "because the returned view should be the same as the existing one");

            //no-op
            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=> { }, null, "1")
                .Should().BeFalse("beacuse the version number did not change");


            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter) => { }, null, "2")
                .Should().BeTrue("because the version number changed");
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
                    document.CblID().Should().NotBeNull("because nothing should enter the map function without an ID");
                    document.CblRev().Should().NotBeNull("because nothing should enter the map function without a rev ID");
                    if (document["key"] != null)
                    {
                        emitter(document["key"], null);
                    }
                }, null, "1");
            return view;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
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

                document.CblID().Should().NotBeNull("because nothing should enter the map function without an ID");
                document.CblRev().Should().NotBeNull("because nothing should enter the map function without a rev ID");

                if (document.ContainsKey("key") && document["key"] != null)
                {
                    emitter(document["key"], null);
                }
            };
            view.SetMap(mapBlock, "1");


            view.IsStale.Should().BeTrue("beacuse the view has not been updated yet");
            view.UpdateIndex_Internal();

            IList<IDictionary<string, object>> dumpResult = view.Storage.Dump().ToList();
            WriteDebug("View dump: " + dumpResult);
            dumpResult.Should().HaveCount(3, "beacuse three items have the 'key' property");
            dumpResult[0].Should().Contain("key", "\"one\"")
                .And.Subject.AsDictionary<string, object>().Should().Contain("seq", 1L);
            dumpResult[1].Should().Contain("key", "\"three\"")
                .And.Subject.AsDictionary<string, object>().Should().Contain("seq", 3L);
            dumpResult[2].Should().Contain("key", "\"two\"")
                .And.Subject.AsDictionary<string, object>().Should().Contain("seq", 2L);

            //no-op reindex
            view.IsStale.Should().BeFalse("beacuse the view was updated");
            view.UpdateIndex_Internal();

            // Now add a doc and update a doc:
            var threeUpdated = new RevisionInternal(rev3.DocID, rev3.RevID, false);
            numTimesMapFunctionInvoked = numTimesInvoked;

            var newdict3 = new Dictionary<string, object>();
            newdict3["key"] = "3hree";
            threeUpdated.SetProperties(newdict3);

            rev3 = database.PutRevision(threeUpdated, rev3.RevID, false);

            // Reindex again:
            view.IsStale.Should().BeTrue("beacuse a new document was added but the view was not updated");
            view.UpdateIndex_Internal();
            
            numTimesInvoked.Should().Be(numTimesMapFunctionInvoked + 1, "because we only added one document");

            var dict4 = new Dictionary<string, object>();
            dict4["key"] = "four";
            var rev4 = PutDoc(database, dict4);
            var twoDeleted = new RevisionInternal(rev2.DocID, rev2.RevID, true);
            database.PutRevision(twoDeleted, rev2.RevID, false);

            // Reindex again:
            view.IsStale.Should().BeTrue("because documents were updated but the view was not");
            view.UpdateIndex_Internal();
            dumpResult = view.Storage.Dump().ToList();
            WriteDebug("View dump: " + dumpResult);
            dumpResult.Should().HaveCount(3, "beacuse three items have the 'key' property");
            dumpResult[0].Should().Contain("key", "\"3hree\"")
                .And.Subject.AsDictionary<string, object>().Should().Contain("seq", 5L);
            dumpResult[1].Should().Contain("key", "\"four\"")
                .And.Subject.AsDictionary<string, object>().Should().Contain("seq", 6L);
            dumpResult[2].Should().Contain("key", "\"one\"")
                .And.Subject.AsDictionary<string, object>().Should().Contain("seq", 1L);

            // Now do a real query:
            IList<QueryRow> rows = view.QueryWithOptions(null).ToList();
            rows.Should().HaveCount(3, "because there are only three documents with the 'key' property");
            rows[0].Key.As<string>().Should().Be("3hree", "because the ordering should be correct");
            rows[0].DocumentId.Should().Be(rev3.DocID, "because the row should have the correct document ID");
            rows[1].Key.As<string>().Should().Be("four", "because the ordering should be correct");
            rows[1].DocumentId.Should().Be(rev4.DocID, "because the row should have the correct document ID");
            rows[2].Key.As<string>().Should().Be("one", "because the ordering should be correct");
            rows[2].DocumentId.Should().Be(rev1.DocID, "because the row should have the correct document ID");
            view.DeleteIndex();
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
        public void TestViewQuery()
        {
            PutDocs(database);
            var view = CreateView(database);
            view.UpdateIndex_Internal();

            // Query all rows:
            QueryOptions options = new QueryOptions();
            var dict5 = new Dictionary<string, object>();
            dict5["id"] = "55555";
            dict5["key"] = "five";

            var dict4 = new Dictionary<string, object>();
            dict4["id"] = "44444";
            dict4["key"] = "four";

            var dict1 = new Dictionary<string, object>();
            dict1["id"] = "11111";
            dict1["key"] = "one";

            var dict3 = new Dictionary<string, object>();
            dict3["id"] = "33333";
            dict3["key"] = "three";

            var dict2 = new Dictionary<string, object>();
            dict2["id"] = "22222";
            dict2["key"] = "two";
            view.QueryWithOptions(options).Select(x => x.Key)
                .Should().Equal(new[] { dict5["key"], dict4["key"], dict1["key"], dict3["key"], dict2["key"] }, String.Equals,
                "because the rows should be in the correct order");
            
            // Start/end key query:
            options = new QueryOptions();
            options.StartKey = "a";
            options.EndKey = "one";

            view.QueryWithOptions(options).Select(x => x.Key)
                .Should().Equal(new[] { dict5["key"], dict4["key"], dict1["key"] }, String.Equals,
                "because only three rows fall in the range of 'a' to 'one'");

            // Start/end query without inclusive end:
            options.InclusiveEnd = false;
            view.QueryWithOptions(options).Select(x => x.Key)
                .Should().Equal(new[] { dict5["key"], dict4["key"] }, String.Equals,
                "because the last row should be excluded");
           
            // Reversed:
            options.Descending = true;
            options.StartKey = "o";
            options.EndKey = "five";
            options.InclusiveEnd = true;
            view.QueryWithOptions(options).Select(x => x.Key)
                .Should().Equal(new[] { dict4["key"], dict5["key"] }, String.Equals,
                "because the results should be reversed and only contain applicable rows");

            // Reversed, no inclusive end:
            options.InclusiveEnd = false;
            view.QueryWithOptions(options).Select(x => x.Key)
                .Should().Equal(new[] { dict4["key"] }, String.Equals,
                "because the last row should be excluded");

            // Specific keys: (note that rows should be in same order as input keys, not sorted)
            options = new QueryOptions();
            var keys = new List<object>();
            keys.Add("two");
            keys.Add("four");
            options.Keys = keys;
            view.QueryWithOptions(options).Select(x => x.Key)
                .Should().Equal(new[] { dict2["key"], dict4["key"] }, String.Equals,
                "because the results should be the specified keys in the specified order");
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
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
            var expected = new[] { new[] { "green", "model1" }, new[] { "red", "model1" },
                new[] { "red", "model2" }, new[] { "yellow", "model2" } };

            view.QueryWithOptions(options).Select(x => x.Key.AsList<object>())
                .Should().Equal(expected, (left, right) => {
                    return left.Cast<string>().SequenceEqual(right);
                },
                "because the rows should contain the correct data in the correct order");

            // Start/end key query:
            options = new QueryOptions();
            options.StartKey = "a";
            options.EndKey = new List<object> { "red", new Dictionary<string, object>() };
            expected = new[] { new[] { "green", "model1" }, new[] { "red", "model1" },
                new[] { "red", "model2" } };
            view.QueryWithOptions(options).Select(x => x.Key.AsList<object>())
                .Should().Equal(expected, (left, right) => {
                    return left.Cast<string>().SequenceEqual(right);
                },
                "because only three rows should fall in the given range");

            // Start/end query without inclusive end:
            options.EndKey = new List<object> { "red", "model1" };
            options.InclusiveEnd = false;
            expected = new[] { new[] { "green", "model1" } };
            view.QueryWithOptions(options).Select(x => x.Key.AsList<object>())
                .Should().Equal(expected, (left, right) => {
                    return left.Cast<string>().SequenceEqual(right);
                },
                "because only two rows fall into the given range and the last row is excluded");

            // Reversed:
            options = new QueryOptions();
            options.StartKey = new List<object> { "red", new Dictionary<string, object>() };
            options.EndKey = new List<object> { "green", "model1" };
            options.Descending = true;
            expected = new[] { new[] { "red", "model2" }, new[] { "red", "model1" },
                new[] { "green", "model1" } };
            view.QueryWithOptions(options).Select(x => x.Key.AsList<object>())
                .Should().Equal(expected, (left, right) => {
                    return left.Cast<string>().SequenceEqual(right);
                },
                "because the three applicable rows should be reversed now");

            // Reversed, no inclusive end:
            options.InclusiveEnd = false;
            expected = new[] { new[] { "red", "model2" }, new[] { "red", "model1" } };
            view.QueryWithOptions(options).Select(x => x.Key.AsList<object>())
                .Should().Equal(expected, (left, right) => {
                    return left.Cast<string>().SequenceEqual(right);
                },
                "because the last row should be excluded");

            // Specific keys:
            options = new QueryOptions();
            IList<object> keys = new List<object>();
            keys.Add(new object[] { "red", "model2" });
            keys.Add(new object[] { "red", "model1" });
            options.Keys = keys;
            view.QueryWithOptions(options).Select(x => x.Key.AsList<object>())
                .Should().Equal(expected, (left, right) => {
                    return left.Cast<string>().SequenceEqual(right);
                },
                "because the given keys should appear in the given order");
        }

        [NUnit.Framework.Test]
        public void TestLiveQueryStartEndKey()
        {
            var view = CreateView(database);

            var query = view.CreateQuery();
            query.StartKey = "one";
            query.EndKey = "one\uFEFF";
            var liveQuery = query.ToLiveQuery();
            liveQuery.StartKey.Should().NotBeNull("because it was set on the creating query");
            liveQuery.EndKey.Should().NotBeNull("because it was set on the creating query");
            var are = new AutoResetEvent(false);
            liveQuery.Start();
            Sleep(2000);
            liveQuery.Rows.Should().HaveCount(0, "because no documents have been added yet");
            liveQuery.Changed += (sender, args) =>
            {
                if(args.Rows.SequenceNumber >= 4) {
                    are.Set();
                }
            };
            PutDocs(database);
            are.WaitOne(2000).Should().BeTrue("because otherwise the live query timed out");
            liveQuery.Rows.Should().HaveCount(1, "because one document was added in the given range");
        }

        [NUnit.Framework.Test]
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

            var wa = new WaitAssert();
            query.Changed += (sender, e) => {
                if(e.Rows.Count < expectedRowBase.Count) {
                    return;
                }

                wa.RunAssert(() => 
                {
                    var enumerator = expectedRowBase.OrderBy(x => x["key"]).GetEnumerator();
                    foreach(var row in e.Rows) {
                        enumerator.MoveNext();
                        row.DocumentId.Should().Be((string)enumerator.Current["id"]);
                        row.Key.As<string>().Should().Be((string)enumerator.Current["key"]);
                        row.Value.AsDictionary<string, object>().Should().Contain("rev", enumerator.Current["value"].AsDictionary<string, object>()["rev"].ToString());
                    }
                });
            };

            wa.WaitForResult(TimeSpan.FromSeconds(100));
            wa = new WaitAssert();
            

            var dict6 = new Dictionary<string, object> {
                ["key"] = "six",
                ["_id"] = "66666"
            };
            var rev6 = PutDoc(database, dict6);
            expectedRowBase.Add(new Dictionary<string, object> {
                { "id", "66666" },
                { "key", "66666" },
                { "value", new Dictionary<string, object> {
                        { "rev", rev6.RevID }
                    }
                }
            });
            wa.WaitForResult(TimeSpan.FromSeconds(10));
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
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
                            { "rev", rev.RevID.ToString() }
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
            database.GetDocument("44444", null, true).RevID.Should().Be(docs[1].RevID, "because the revision ID should be consistent");

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

            var e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                row["id"].Should().Be(e.Current["id"]);
                row["key"].Should().Be(e.Current["key"]);
                row["value"].AsDictionary<string, object>().Should().Equal(e.Current["value"].AsDictionary<string, object>());
            }

            // Limit:
            options.Limit = 1;
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[2] };
            e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                row["id"].Should().Be(e.Current["id"]);
                row["key"].Should().Be(e.Current["key"]);
                row["value"].AsDictionary<string, object>().Should().Equal(e.Current["value"].AsDictionary<string, object>());
            }

            // Limit+Skip:
            options.Skip = 2;
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[3] };
            e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                row["id"].Should().Be(e.Current["id"]);
                row["key"].Should().Be(e.Current["key"]);
                row["value"].AsDictionary<string, object>().Should().Equal(e.Current["value"].AsDictionary<string, object>());
            }

            // Start/end key query:
            options = new QueryOptions();
            options.StartKey = "2";
            options.EndKey = "44444";
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[0], expectedRowBase[3], expectedRowBase[1] };
            e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                row["id"].Should().Be(e.Current["id"]);
                row["key"].Should().Be(e.Current["key"]);
                row["value"].AsDictionary<string, object>().Should().Equal(e.Current["value"].AsDictionary<string, object>());
            }

            // Start/end query without inclusive end:
            options.InclusiveEnd = false;
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[0], expectedRowBase[3] };
            e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                row["id"].Should().Be(e.Current["id"]);
                row["key"].Should().Be(e.Current["key"]);
                row["value"].AsDictionary<string, object>().Should().Equal(e.Current["value"].AsDictionary<string, object>());
            }

            // Get zero specific documents:
            options = new QueryOptions();
            options.Keys = new List<object>();
            database.GetAllDocs(options).Should().BeNullOrEmpty("because the list of keys is empty");

            // Get specific documents:
            options = new QueryOptions();
            options.Keys = new List<object> {
                expectedRowBase[2].GetCast<string>("id"),
                expectedRowBase[3].GetCast<string>("id")
            };
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedRowBase[2], expectedRowBase[3] };
            e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                row["id"].Should().Be(e.Current["id"]);
                row["key"].Should().Be(e.Current["key"]);
                row["value"].AsDictionary<string, object>().Should().Equal(e.Current["value"].AsDictionary<string, object>());
            }

            // Delete a document:
            var del = docs[0];
            del = new RevisionInternal(del.DocID, del.RevID, true);
            del = database.PutRevision(del, del.RevID, false);

            // Get deleted doc, and one bogus one:
            options = new QueryOptions();
            options.Keys = new List<object> { "BOGUS", expectedRowBase[0].GetCast<string>("id") };
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>> {
                new Dictionary<string, object> {
                    { "key", "BOGUS" },
                    { "error", "not_found" }
                },
                new Dictionary<string, object> {
                    { "id", del.DocID },
                    { "key", del.DocID },
                    { "value", new Dictionary<string, object> {
                            { "rev", del.RevID.ToString() },
                            { "deleted", true }
                        }
                    }
                }
            };
            e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                if(e.Current.ContainsKey("id")) {
                    row["id"].Should().Be(e.Current["id"]);
                    row["value"].AsDictionary<string, object>().Should().Equal(e.Current["value"].AsDictionary<string, object>());
                } else {
                    row["error"].Should().Be(e.Current["error"]);
                }

                row["key"].Should().Be(e.Current["key"]);
                
            }

            // Get conflicts:
            options = new QueryOptions();
            options.AllDocsMode = AllDocsMode.ShowConflicts;
            allDocs = database.GetAllDocs(options);
            var curRevId = docs[1].RevID;
            var expectedConflict1 = new Dictionary<string, object> {
                { "id", "44444" },
                { "key", "44444" },
                { "value", new Dictionary<string, object> {
                        { "rev", curRevId.ToString() },
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

            e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                row["id"].Should().Be(e.Current["id"]);
                row["key"].Should().Be(e.Current["key"]);
                var leftVal = row["value"].AsDictionary<string, object>();
                var rightVal = e.Current["value"].AsDictionary<string, object>();
                leftVal.Should().Contain("rev", rightVal["rev"]);
                if(rightVal.ContainsKey("_conflicts")) {
                    leftVal["_conflicts"].AsList<string>().Should().Equal(rightVal["_conflicts"].AsList<string>());
                }
            }

            // Get _only_ conflicts:
            options.AllDocsMode = AllDocsMode.OnlyConflicts;
            allDocs = database.GetAllDocs(options);
            expectedRows = new List<IDictionary<string, object>>() { expectedConflict1 };
            e = expectedRows.GetEnumerator();
            foreach(var row in RowsToDicts(allDocs)) {
                e.MoveNext();
                row["id"].Should().Be(e.Current["id"]);
                row["key"].Should().Be(e.Current["key"]);
                var leftVal = row["value"].AsDictionary<string, object>();
                var rightVal = e.Current["value"].AsDictionary<string, object>();
                leftVal.Should().Contain("rev", rightVal["rev"]);
                if(rightVal.ContainsKey("_conflicts")) {
                    leftVal["_conflicts"].AsList<string>().Should().Equal(rightVal["_conflicts"].AsList<string>());
                }
            }
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
        [NUnit.Framework.Test]
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
                document.CblID().Should().NotBeNull("because no document should reach the map function without an ID");
                document.CblRev().Should().NotBeNull("because no document should reach the map function without a rev ID");
                object cost = document.Get ("cost");
                if (cost != null) {
                    emitter (document.CblID(), cost);
                }
            }, BuiltinReduceFunctions.Sum, "1");


            view.UpdateIndex_Internal();

            IList<IDictionary<string, object>> dumpResult = view.Storage.Dump().ToList();
            WriteDebug("View dump: " + dumpResult);
            dumpResult.Should().HaveCount(3, "because there should be three results from the view");
            dumpResult[0].Should().Contain("key", "\"App\"")
                .And.Subject.Should().Contain("val", "1.95")
                .And.Subject.Should().Contain("seq", 2L, "because the row should have correct information");

            dumpResult[1].Should().Contain("key", "\"CD\"")
                .And.Subject.Should().Contain("val", "8.99")
                .And.Subject.Should().Contain("seq", 1L, "because the row should have correct information");

            dumpResult[2].Should().Contain("key", "\"Dessert\"")
                .And.Subject.Should().Contain("val", "6.5")
                .And.Subject.Should().Contain("seq", 3L, "because the row should have correct information");

            QueryOptions options = new QueryOptions();
            options.Reduce = true;

            IList<QueryRow> reduced = view.QueryWithOptions(options).ToList();
            reduced.Should().HaveCount(1).And.Subject.First().Value.As<double>().Should().BeApproximately(17.44, 0.0001, "because the row information should be correct");
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
        public void TestIndexUpdateMode()
        {
            View view = CreateView(database);
            Query query = view.CreateQuery();

            query.IndexUpdateMode = IndexUpdateMode.Before;
            int numRowsBefore = query.Run().Count;
            query.Run().Should().HaveCount(0, "because no documents have been added yet");

            // do a query and force re-indexing, number of results should be +4
            PutNDocs(database, 1);
            query.IndexUpdateMode = IndexUpdateMode.Before;
            query.Run().Should().HaveCount(1, "because a document was added and the index update mode is default");

            // do a query without re-indexing, number of results should be the same
            PutNDocs(database, 4);
            query.IndexUpdateMode = IndexUpdateMode.Never;
            query.Run().Should().HaveCount(1, "because documents added and the view is set not to update");

            // do a query and force re-indexing, number of results should be +4
            query.IndexUpdateMode = IndexUpdateMode.Before;
            query.Run().Should().HaveCount(5, "because the view update mode was restored to default");

            // do a query which will kick off an async index
            PutNDocs(database, 1);
            query.IndexUpdateMode = IndexUpdateMode.After;
            query.Run().Count.Should().BeOneOf(new[] { 5, 6 }, "because the update mode is async");

            // wait until indexing is (hopefully) done
            Sleep(1000);
            query.Run().Should().HaveCount(6, "because the async update should be finished");
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
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

            var rows = view.QueryWithOptions(options);
            IList<IDictionary<string, object>> expectedRows = new List<IDictionary<string, object>>();
            IDictionary<string, object> row1 = new Dictionary<string, object>();
            row1["key"] = null;
            row1["value"] = 1162.0;
            expectedRows.Add(row1);
            rows.Should().Equal(expectedRows, (left, right) =>
            {
                return Object.Equals(left.Key, right["key"]) &&
                left.Value.Equals(right["value"]);
            }, "because otherwise the query is incorrect");

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
            rows.Should().Equal(expectedRows, (left, right) =>
            {
                return left.Key.AsList<string>().SequenceEqual(right["key"].AsList<string>()) && 
                left.Value.Equals(right["value"]);
            }, "because otherwise the query is incorrect");

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
            rows.Should().Equal(expectedRows, (left, right) =>
            {
                return left.Key.AsList<object>().Cast<string>().SequenceEqual(right["key"].AsList<string>()) &&
                left.Value.Equals(right["value"]);
            }, "because otherwise the query is incorrect");

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
            rows.Should().Equal(expectedRows, (left, right) =>
            {
                return left.Key.AsList<object>().Cast<string>().SequenceEqual(right["key"].AsList<string>()) &&
                left.Value.Equals(right["value"]);
            }, "because otherwise the query is incorrect");
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
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
            row1["value"] = 2.0;
            expectedRows.Add(row1);

            IDictionary<string, object> row2 = new Dictionary<string, object>();
            row2["key"] = "J";
            row2["value"] = 2.0;
            expectedRows.Add(row2);

            IDictionary<string, object> row3 = new Dictionary<string, object>();
            row3["key"] = "N";
            row3["value"] = 1.0;
            expectedRows.Add(row3);

            rows.Should().Equal(expectedRows, (left, right) =>
            {
                return left.Key.Equals(right["key"]) &&
                left.Value.Equals(right["value"]);
            }, "because otherwise the query is incorrect");
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
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
                testKeys[i++].Should().Be(row.Key, "because otherwise the row information is incorrect");
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [NUnit.Framework.Test]
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
                testKeys[i++].Should().Be(row.Key, "because otherwise the row information is incorrect");
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
            rows.Should().NotBeNullOrEmpty("beacuse otherwise the query failed");
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
            rows.Should().HaveCount(5, "because otherwise the query has an incorrect number of rows");

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
                id.Should().Be((string)expected[i][0]);
                key.Count.Should().Be(2);
                key[0].Should().Be(expected[i][1]);
                key[1].Should().Be(expected[i][2]);
                if (expected[i][3] == null)
                {
                    row.Value.Should().BeNull();
                }
                else
                {
                    ((IDictionary<string, object>)row.Value).CblID().Should().Be((string)expected[i][3]);
                }
                doc.CblID().Should().Be((string)expected[i][4]);
            }
        }

        [NUnit.Framework.Test]
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

            rowCountAlwaysOne.Should().BeTrue("because each revision should replace the last");
        }

        [NUnit.Framework.Test]
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

            query.Rows.Should().BeNull("because the query has not started yet");
            query.Start();

            var gotExpectedQueryResult = new CountdownEvent(1);
            query.Changed += (sender, e) => 
            {
                e.Error.Should().BeNull("because no error should occur");
                if (e.Rows.Count == 1 && Convert.ToInt32(e.Rows.ElementAt(0).Value) == numDocs)
                {
                    gotExpectedQueryResult.Signal();
                }
            };

            gotExpectedQueryResult.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("because otherwise the query timed out");
            query.Stop();

            query1.Start();

            CreateDocumentsAsync(database, numDocs + 5); //10 + 10 + 5

            gotExpectedQueryResult = new CountdownEvent(1);
            query1.Changed += (sender, e) =>
            {
                e.Error.Should().BeNull("because no error should occur");
                if (e.Rows.Count == 1 && Convert.ToInt32(e.Rows.ElementAt(0).Value) == (2 * numDocs) + 5)
                {
                    gotExpectedQueryResult.Signal();
                }
            };

            gotExpectedQueryResult.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("because otherwise the query timed out");
            query1.Stop();
            database.GetDocumentCount().Should().Be((2 * numDocs) + 5, "because otherwise the document count is wrong");
        }

        [NUnit.Framework.Test]
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
            rows.Should().HaveCount(0, "because design documents should not be indexed");
        }

        [NUnit.Framework.Test]
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
            rows.Should().HaveCount(1).And.Subject.First().Key.ShouldBeEquivalentTo(33547239, "because numeric keys should stay numeric");
        }
            
        [NUnit.Framework.Test]
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

            rows.Should().HaveCount(2, "because there are two documents in the given range");
            rows.Select(x => x.DocumentId).Should().Equal(new[] { "11112", "33333" }, "because the row document IDs should be correct");
            rows.Select(x => x.Key).Cast<string>().Should().Equal(new[] { "one", "three" }, "because the row keys should be correct");

            options = new QueryOptions();
            options.EndKey = "one";
            options.EndKeyDocId = "11111";
            rows = view.QueryWithOptions(options).ToList<QueryRow>();

            rows.Should().HaveCount(3, "because there are three documents in the given range");
            rows.Select(x => x.DocumentId).Should().Equal(new[] { "55555", "44444", "11111" }, "because the row document IDs should be correct");
            rows.Select(x => x.Key).Cast<string>().Should().Equal(new[] { "five", "four", "one" }, "because the row keys should be correct");

            options.StartKey = "one";
            options.StartKeyDocId = "11111";
            rows = view.QueryWithOptions(options).ToList<QueryRow>();

            rows.Should().HaveCount(1, "because there is one document in the given range");
            rows.First().DocumentId.Should().Be("11111", "because the document ID should be correct");
            rows.First().Key.As<string>().Should().Be("one", "because the key should be correct");
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

        [NUnit.Framework.Test]
        public void TestViewWithConflict() {
            // Create doc and add some revs
            var doc = database.CreateDocument();
            var rev1 = CreateTestRevisionNoConflicts(doc, "1");
            rev1.Should().NotBeNull("because the revision should be created successfully");
            var rev2a = CreateTestRevisionNoConflicts(doc, "2a");
            rev2a.Should().NotBeNull("because the revision should be created successfully"); ;
            var rev3 = CreateTestRevisionNoConflicts(doc, "3");
            rev3.Should().NotBeNull("because the revision should be created successfully");

            // index the view
            var view = CreateView(database);
            var rows = view.CreateQuery().Run();
            rows.Should().HaveCount(1).And.Subject.First().Key.Should().Be("3", "because there should only be one current revision in the query");

            // Create a conflict
            var rev2bUnsaved = rev1.CreateRevision();
            var props = new Dictionary<string, object>() 
            {
                {"key", "2b"}
            };
            rev2bUnsaved.SetUserProperties(props);
            var rev2b = rev2bUnsaved.Save(true);
            rev2b.Should().NotBeNull("because the revision should be created successfully");

            // re-run query
            view.UpdateIndex_Internal();
            rows = view.CreateQuery().Run();

            // we should only see one row, with key=3.
            // if we see key=2b then it's a bug.

            rows.Should().HaveCount(1).And.Subject.First().Key.Should().Be("3", "because there should only be one current revision in the query");
        }

        [NUnit.Framework.Test]
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
            view.TotalRows.Should().Be(50, "because despite multiple queries the results should be accurate");
            view.LastSequenceIndexed.Should().Be(50, "because despite multiple queries the results should be accurate");

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
            view.TotalRows.Should().Be(60, "because despite multiple queries the results should be accurate");
            view.LastSequenceIndexed.Should().Be(60, "because despite multiple queries the results should be accurate");
        }

        [NUnit.Framework.Test]
        public void TestMapConflicts()
        {
            var view = database.GetView("vu");
            view.Should().NotBeNull("because the view should be successfully created");
            view.SetMap((d, emit) =>
                emit(d.CblID(), d.Get("_conflicts")), "1");

            var doc = CreateDocumentWithProperties(database, new Dictionary<string, object> { { "foo", "bar" } });
            var rev1 = doc.CurrentRevision;
            var properties = rev1.Properties;
            properties["tag"] = "1";
            var rev2a = doc.PutProperties(properties);

            // No conflicts:
            var query = view.CreateQuery();
            var rows = query.Run();
            rows.Should().HaveCount(1).And.Subject.First().Should().Match<QueryRow>(x => x.Key.Equals(doc.Id) && x.Value == null);

            // Create a conflict revision:
            properties["tag"] = "2";
            var newRev = rev1.CreateRevision();
            newRev.SetProperties(properties);
            var rev2b = newRev.Save(true);

            rows = query.Run();
            rows.Should().HaveCount(1).And.Subject.First().Should().Match<QueryRow>(x => x.Key.Equals(doc.Id) 
                && x.Value.AsList<string>().First().Equals(rev2a.Id));

            // Create another conflict revision:
            properties["tag"] = "3";
            newRev = rev1.CreateRevision();
            newRev.SetProperties(properties);
            newRev.Save(true);

            rows = query.Run();
            rows.Should().HaveCount(1).And.Subject.First().Key.Should().Be(doc.Id, "because the query results should be correct");
            rows.First().Value.AsList<string>().Should().BeEquivalentTo(new[] { rev2a.Id, rev2b.Id });
        }

        [NUnit.Framework.Test]
        public void TestViewWithDocDeletion()
        {
            TestViewWithDocRemoval(false);
        }

        [NUnit.Framework.Test]
        public void TestViewWithDocPurge()
        {
            TestViewWithDocRemoval(true);
        }

        private void TestViewWithDocRemoval(bool purge)
        {
            var view = database.GetView("vu");
            view.Should().NotBeNull("because the view should be created successfully");
            view.SetMap((doc, emit) =>
            {
                var type = doc.GetCast<string>("type");
                if(type == "task") {
                    var date = doc.Get("created_at");
                    var listId = doc.Get("list_id");
                    emit(new[] { listId, date }, doc);
                }
            }, "1");

            view.Map.Should().NotBeNull("because the map was just set");
            view.TotalRows.Should().Be(0, "because no documents exist yet");

            const string insertListId = "list1";

            var doc1 = CreateDocumentWithProperties(database, new Dictionary<string, object> {
                { "_id", "doc1" },
                { "type", "task" },
                { "created_at", DateTime.Now },
                { "list_id", insertListId }
            });
            doc1.Should().NotBeNull("because the document should have been created successfully");

            var doc2 = CreateDocumentWithProperties(database, new Dictionary<string, object> {
                { "_id", "doc2" },
                { "type", "task" },
                { "created_at", DateTime.Now },
                { "list_id", insertListId }
            });
            doc2.Should().NotBeNull("because the document should have been created successfully");

            var doc3 = CreateDocumentWithProperties(database, new Dictionary<string, object> {
                { "_id", "doc3" },
                { "type", "task" },
                { "created_at", DateTime.Now },
                { "list_id", insertListId }
            });
            doc3.Should().NotBeNull("because the document should have been created successfully");

            var query = view.CreateQuery();
            query.Descending = true;
            query.StartKey = new object[] { insertListId, new Dictionary<string, object>() };
            query.EndKey = new[] { insertListId };

            var rows = query.Run();
            rows.Select(x => x.DocumentId).Should().Equal(new[] { doc3.Id, doc2.Id, doc1.Id });
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
            rows.Select(x => x.DocumentId).Should().Equal(new[] { doc1.Id, doc3.Id });

            // Check descending query result:
            query.Descending = true;
            query.StartKey = new object[] { insertListId, new Dictionary<string, object>() };
            query.EndKey = new[] { insertListId };
            rows = query.Run();
            Trace.WriteLine(String.Format("Descending query: rows = {0}", rows));
            rows.Select(x => x.DocumentId).Should().Equal(new[] { doc3.Id, doc1.Id });
        }

        [NUnit.Framework.Test] // Issue 710
        public void TestViewsInTransaction ()
        {
            if (_storageType != "SQLite") {
                return; // Invalid for ForestDB
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

            ((SqliteViewStore)foo.Storage).ViewID.Should().Be(1, "because the view ID should be properly set");
            ((SqliteViewStore)bar.Storage).ViewID.Should().Be(2, "because the view ID should be properly set");
        }

        private IList<IDictionary<string, object>> RowsToDicts(IEnumerable<QueryRow> allDocs)
        {
            allDocs.Should().NotBeNull("because otherwise RowsToDicts is invalid");
            var rows = new List<IDictionary<string, object>>();
            foreach (var row in allDocs) {
                rows.Add(row.AsJSONDictionary());
            }

            return rows;
        }
    }
}
