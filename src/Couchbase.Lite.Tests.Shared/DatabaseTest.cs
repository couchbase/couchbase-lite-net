//
// DatabaseTest.cs
//
// Author:
//  Pasin Suriyentrakorn <pasin@couchbase.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
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
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Couchbase.Lite.Storage.SQLCipher;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class DatabaseTest : LiteTestCase
    {
        const String TooLongName = "a11111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111110";

        public DatabaseTest(string storageType) : base(storageType) {}

        [Test]
        public void TestAllDocumentsPrefixMatch()
        {
            CreateDocumentWithProperties(database, new Dictionary<string, object> { { "_id", "three" } });
            CreateDocumentWithProperties(database, new Dictionary<string, object> { { "_id", "four" } });
            CreateDocumentWithProperties(database, new Dictionary<string, object> { { "_id", "five" } });
            CreateDocumentWithProperties(database, new Dictionary<string, object> { { "_id", "eight" } });
            CreateDocumentWithProperties(database, new Dictionary<string, object> { { "_id", "fifteen" } });

            database.DocumentCache.Clear();

            var query = database.CreateAllDocumentsQuery();
            var rows = default(QueryEnumerator);

            // Set prefixMatchLevel = 1, no startKey, ascending:
            query.Descending = false;
            query.EndKey = "f";
            query.PrefixMatchLevel = 1;
            rows = query.Run();
            Assert.AreEqual(4, rows.Count);
            CollectionAssert.AreEqual(new[] { "eight", "fifteen", "five", "four" }, rows.Select(x => x.Key));

            // Set prefixMatchLevel = 1, ascending:
            query.Descending = false;
            query.StartKey = "f";
            query.EndKey = "f";
            query.PrefixMatchLevel = 1;
            rows = query.Run();
            Assert.AreEqual(3, rows.Count);
            CollectionAssert.AreEqual(new[] { "fifteen", "five", "four" }, rows.Select(x => x.Key));

            // Set prefixMatchLevel = 1, descending:
            query.Descending = true;
            query.StartKey = "f";
            query.EndKey = "f";
            query.PrefixMatchLevel = 1;
            rows = query.Run();
            Assert.AreEqual(3, rows.Count);
            CollectionAssert.AreEqual(new[] { "four", "five", "fifteen" }, rows.Select(x => x.Key));

            // Set prefixMatchLevel = 1, ascending, prefix = fi:
            query.Descending = false;
            query.StartKey = "fi";
            query.EndKey = "fi";
            query.PrefixMatchLevel = 1;
            rows = query.Run();
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "fifteen", "five" }, rows.Select(x => x.Key));

        }

        #if !NET_3_5
        [Test]
        public void TestParallelLibrary()
        {
            const int docCount = 200;

            Parallel.Invoke(() => {
                Parallel.For(0, docCount, i =>
                {
                    Assert.DoesNotThrow(() => database.GetExistingDocument(i.ToString()));
                });
            }, () => {
                Parallel.For(0, docCount, i =>
                {
                    Assert.DoesNotThrow(() => database.GetExistingDocument(i.ToString()));
                });
            });
        }

        #endif

        [Test]
        public void TestReadOnlyDb()
        {
            CreateDocuments(database, 10);
            database.Close();

            var options = new ManagerOptions();
            options.ReadOnly = true;
            var readOnlyManager = new Manager(new DirectoryInfo(manager.Directory), options);
            database = readOnlyManager.GetExistingDatabase(database.Name);
            Assert.IsNotNull(database);
            var e = Assert.Throws<CouchbaseLiteException>(() => CreateDocuments(database, 1));
            Assert.AreEqual(StatusCode.Forbidden, e.Code);
            database.Close();

            var dbOptions = new DatabaseOptions();
            dbOptions.ReadOnly = true;
            database = manager.OpenDatabase(database.Name, dbOptions);
            Assert.IsNotNull(database);
            e = Assert.Throws<CouchbaseLiteException>(() => CreateDocuments(database, 1));
            Assert.AreEqual(StatusCode.Forbidden, e.Code);
            database.Close();

            dbOptions.ReadOnly = false;
            database = manager.OpenDatabase(database.Name, dbOptions);
            Assert.DoesNotThrow(() => CreateDocuments(database, 1));
        }

        [Test]
        public void TestUpgradeDatabase()
        {
            // Install a canned database:
            using (var dbStream = GetAsset("ios120.zip")) {
                Assert.DoesNotThrow(() => manager.ReplaceDatabase("replacedb", dbStream, true));
            }

            // Open installed db with storageType set to this test's storage type:
            var options = new DatabaseOptions();
            options.StorageType = _storageType;
            var replacedb = default(Database);
            Assert.DoesNotThrow(() => replacedb = manager.OpenDatabase("replacedb", options));
            Assert.IsNotNull(replacedb);

            // Verify storage type matches what we requested:
            Assert.IsInstanceOf(database.Storage.GetType(), replacedb.Storage);

            // Test db contents:
            CheckRowsOfReplacedDB("replacedb", rows =>
            {
                Assert.AreEqual(1, rows.Count);
                var doc = rows.ElementAt(0).Document;
                Assert.AreEqual("doc1", doc.Id);
                Assert.AreEqual(2, doc.CurrentRevision.Attachments.Count());
                var att1 = doc.CurrentRevision.GetAttachment("attach1");
                Assert.IsNotNull(att1);
                Assert.AreEqual(att1.Length, att1.Content.Count());

                var att2 = doc.CurrentRevision.GetAttachment("attach2");
                Assert.IsNotNull(att2);
                Assert.AreEqual(att2.Length, att2.Content.Count());
            });

            // Close and re-open the db using SQLite storage type. Should fail if it used to be ForestDB:
            Assert.DoesNotThrow(() => replacedb.Close().Wait(15000));
            options.StorageType = StorageEngineTypes.SQLite;
            if (_storageType == StorageEngineTypes.SQLite) {
                Assert.DoesNotThrow(() => replacedb = manager.OpenDatabase("replacedb", options));
                Assert.IsNotNull(replacedb);
            } else {
                var e = Assert.Throws<CouchbaseLiteException>(() => replacedb = manager.OpenDatabase("replacedb", options));
                Assert.AreEqual(StatusCode.InvalidStorageType, e.Code);
            }
        }

        [Test]
        public void TestValidDatabaseNames([Values("foo", "try1", "foo-bar", "goofball99", TooLongName)] String testName)
        {
            // Arrange.
            // Act.
            if (testName.Length == 240) {
                testName = testName.Trim('0');
            }
            var result = Manager.IsValidDatabaseName(testName);

            // Assert.
            Assert.IsTrue(result);
        }

        [Test]
        public void TestInvalidDatabaseNames([Values("Foo", "1database", "", "foo;", TooLongName)] String testName)
        {
            // Arrange.
            // Act.
            var result = Manager.IsValidDatabaseName(testName);

            // Assert.
            Assert.IsFalse(result);
        }

        [Test]
        public void TestGetDatabaseNameFromPath() 
        {
            Assert.AreEqual("baz", FileDirUtils.GetDatabaseNameFromPath("foo/bar/baz.cblite"));
        }

        [Test]
        public void TestPruneRevsToMaxDepth()
        {
            var sqliteStorage = database.Storage as SqliteCouchStore;
            if (sqliteStorage == null) {
                Assert.Inconclusive("This test is only valid on a SQLite store");
            }

            var properties = new Dictionary<string, object>();
            properties.Add("testName", "testDatabaseCompaction");
            properties.Add("tag", 1337);

            var doc = CreateDocumentWithProperties(database, properties);
            var rev = doc.CurrentRevision;
            database.SetMaxRevTreeDepth(1);

            for (int i = 0; i < 10; i++)
            {
                var properties2 = new Dictionary<string, object>(properties);
                properties2["tag"] = i;
                rev = rev.CreateRevision(properties2);
            }

            var numPruned = sqliteStorage.PruneRevsToMaxDepth(1);
            Assert.AreEqual(10, numPruned);

            var fetchedDoc = database.GetDocument(doc.Id);
            var revisions = fetchedDoc.RevisionHistory.ToList();
            Assert.AreEqual(1, revisions.Count);

            numPruned = sqliteStorage.PruneRevsToMaxDepth(1);
            Assert.AreEqual(0, numPruned);
        }

        [Test]
        public void TestPruneRevsToMaxDepthViaCompact()
        {
            var properties = new Dictionary<string, object>();
            properties.Add("testName", "testDatabaseCompaction");
            properties.Add("tag", 1337);

            var doc = CreateDocumentWithProperties(database, properties);
            var rev = doc.CurrentRevision;
            database.SetMaxRevTreeDepth(1);

            for (int i = 0; i < 10; i++)
            {
                var properties2 = new Dictionary<string, object>(properties);
                properties2["tag"] = i;
                rev = rev.CreateRevision(properties2);
            }

            database.Compact();

            var fetchedDoc = database.GetDocument(doc.Id);
            var revisions = fetchedDoc.RevisionHistory.ToList();
            Assert.AreEqual(1, revisions.Count);
        }

        /// <summary>
        /// When making inserts in a transaction, the change notifications should
        /// be batched into a single change notification (rather than a change notification
        /// for each insert)
        /// </summary>
        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestChangeListenerNotificationBatching()
        {
            const int numDocs = 50;
            var atomicInteger = 0;
            var doneSignal = new CountdownEvent(1);

            database.Changed += (sender, e) => Interlocked.Increment (ref atomicInteger);

            database.RunInTransaction(() =>
            {
                CreateDocuments(database, numDocs);
                doneSignal.Signal();
                return true;
            });

            var success = doneSignal.Wait(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
            Assert.AreEqual(1, atomicInteger);
        }

        /// <summary>
        /// When making inserts outside of a transaction, there should be a change notification
        /// for each insert (no batching)
        /// </summary>
        [Test]
        public void TestChangeListenerNotification()
        {
            const int numDocs = 50;
            var atomicInteger = 0;

            database.Changed += (sender, e) => Interlocked.Increment (ref atomicInteger);
            CreateDocuments(database, numDocs);
            Assert.AreEqual(numDocs, atomicInteger);
        }

        /// <summary>
        /// When making inserts outside of a transaction, there should be a change notification
        /// for each insert (no batching)
        /// </summary>
        [Test]
        public void TestGetActiveReplications()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var remote = GetReplicationURL();
            var doneSignal = new ManualResetEvent(false);
            var replication = database.CreatePullReplication(remote);
            replication.Continuous = true;

            Func<Replication, bool> doneLogic = r =>
                replication.Status == ReplicationStatus.Active;
            
            replication.Changed += (sender, e) => {
                if (doneLogic(e.Source)) {
                    doneSignal.Set();
                }
            };
                
            Assert.AreEqual(0, database.AllReplications.ToList().Count);

            replication.Start();
            var passed = doneSignal.WaitOne(TimeSpan.FromSeconds(5));
            Assert.IsTrue(passed);
            Assert.AreEqual(1, database.AllReplications.Count());
        }

        [Test]
        public void TestUnsavedRevisionCacheRetainDocument()
        {
            var document = database.CreateDocument();

            database.DocumentCache.Remove(document.Id);

            Assert.IsNull(database.DocumentCache.Get(document.Id));

            var cachedDocument = default(WeakReference);
            database.UnsavedRevisionDocumentCache.TryGetValue(document.Id, out cachedDocument);
            Assert.IsTrue(cachedDocument.Target == document);

            var checkedDocument = database.GetDocument(document.Id);
            Assert.IsTrue(document == checkedDocument);
        }

        [Test]
        public void TestUnsavedRevisionCacheRemoveDocument()
        {
            var document = database.CreateDocument();

            var properties = new Dictionary<string, object>();
            properties.Add("test", "test");
            document.PutProperties(properties);

            var cachedDocument = default(WeakReference);
            database.UnsavedRevisionDocumentCache.TryGetValue(document.Id, out cachedDocument);
            Assert.IsNull(cachedDocument);

            var checkedDocument = database.GetDocument(document.Id);
            Assert.IsTrue(document == checkedDocument);
        }

        [Test]
        public void TestEncodeDocumentJSON() 
        {
            var sqliteStorage = database.Storage as SqliteCouchStore;
            if (sqliteStorage == null) {
                Assert.Inconclusive("This test is only valid on an SQLite store");
            }

            var props = new Dictionary<string, object>() 
            {
                {"_local_seq", ""}
            };

            var revisionInternal = new RevisionInternal(props);
            var encoded = sqliteStorage.EncodeDocumentJSON(revisionInternal);
            Assert.IsNotNull(encoded);
        }

        [Test]
        public void TestWinningRevIDOfDoc()
        {
            var sqliteStorage = database.Storage as SqliteCouchStore;
            if (sqliteStorage == null) {
                Assert.Inconclusive("This test is only valid on an SQLite store");
            }

            var properties = new Dictionary<string, object>() 
            {
                {"testName", "testCreateRevisions"},
                {"tag", 1337}
            };

            var properties2a = new Dictionary<string, object>() 
            {
                {"testName", "testCreateRevisions"},
                {"tag", 1338}
            };

            var properties2b = new Dictionary<string, object>()
            {
                {"testName", "testCreateRevisions"},
                {"tag", 1339}
            };

            var doc = database.CreateDocument();
            var newRev1 = doc.CreateRevision();
            newRev1.SetUserProperties(properties);
            var rev1 = newRev1.Save();

            ValueTypePtr<bool> outIsDeleted = false;
            ValueTypePtr<bool> outIsConflict = false;

            var docNumericId = sqliteStorage.GetDocNumericID(doc.Id);
            Assert.IsTrue(docNumericId != 0);
            Assert.AreEqual(rev1.Id, sqliteStorage.GetWinner(docNumericId, outIsDeleted, outIsConflict));
            Assert.IsFalse(outIsConflict);

            var newRev2a = rev1.CreateRevision();
            newRev2a.SetUserProperties(properties2a);
            var rev2a = newRev2a.Save();
            Assert.AreEqual(rev2a.Id, sqliteStorage.GetWinner(docNumericId, outIsDeleted, outIsConflict));
            Assert.IsFalse(outIsConflict);

            var newRev2b = rev1.CreateRevision();
            newRev2b.SetUserProperties(properties2b);
            newRev2b.Save(true);
            sqliteStorage.GetWinner(docNumericId, outIsDeleted, outIsConflict);
            Assert.IsTrue(outIsConflict);
        }

        private void CheckRowsOfReplacedDB(string dbName, Action<QueryEnumerator> onComplete)
        {
            var replacedb = default(Database);
            Assert.DoesNotThrow(() => replacedb = manager.OpenDatabase(dbName, null));
            Assert.IsNotNull(replacedb);

            var view = replacedb.GetView("myview");
            Assert.IsNotNull(view);
            view.SetMap((doc, emit) =>
            {
                emit(doc.Get("_id"), null);
            }, "1.0");

            var query = view.CreateQuery();
            Assert.IsNotNull(query);
            query.Prefetch = true;
            var rows = default(QueryEnumerator);
            Assert.DoesNotThrow(() => rows = query.Run());
            onComplete(rows);
        }
    }
}

