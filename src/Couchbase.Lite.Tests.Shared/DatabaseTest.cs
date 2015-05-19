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
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Sharpen;
using System.Threading;
using Newtonsoft.Json.Linq;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    public class DatabaseTest : LiteTestCase
    {
        const String TooLongName = "a11111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111110";

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
            var properties = new Dictionary<string, object>();
            properties.Add("testName", "testDatabaseCompaction");
            properties.Add("tag", 1337);

            var doc = CreateDocumentWithProperties(database, properties);
            var rev = doc.CurrentRevision;
            database.MaxRevTreeDepth = 1;

            for (int i = 0; i < 10; i++)
            {
                var properties2 = new Dictionary<string, object>(properties);
                properties2["tag"] = i;
                rev = rev.CreateRevision(properties2);
            }

            var numPruned = database.PruneRevsToMaxDepth(1);
            Assert.AreEqual(10, numPruned);

            var fetchedDoc = database.GetDocument(doc.Id);
            var revisions = fetchedDoc.RevisionHistory.ToList();
            Assert.AreEqual(1, revisions.Count);

            numPruned = database.PruneRevsToMaxDepth(1);
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
            database.MaxRevTreeDepth = 1;

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
            var atomicInteger = new AtomicInteger(0);
            var doneSignal = new CountDownLatch(1);

            database.Changed += (sender, e) => atomicInteger.IncrementAndGet();

            database.RunInTransaction(() =>
            {
                CreateDocuments(database, numDocs);
                doneSignal.CountDown();
                return true;
            });

            var success = doneSignal.Await(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
            Assert.AreEqual(1, atomicInteger.Get());
        }

        /// <summary>
        /// When making inserts outside of a transaction, there should be a change notification
        /// for each insert (no batching)
        /// </summary>
        [Test]
        public void TestChangeListenerNotification()
        {
            const int numDocs = 50;
            var atomicInteger = new AtomicInteger(0);

            database.Changed += (sender, e) => atomicInteger.IncrementAndGet();
            CreateDocuments(database, numDocs);
            Assert.AreEqual(numDocs, atomicInteger.Get());
        }

        /// <summary>
        /// When making inserts outside of a transaction, there should be a change notification
        /// for each insert (no batching)
        /// </summary>
        [Test]
        public void TestGetActiveReplications()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var remote = GetReplicationURL();
            var doneSignal = new ManualResetEvent(false);
            var replication = database.CreatePullReplication(remote);
            replication.Changed += (sender, e) => {
                if (!replication.IsRunning) {
                    doneSignal.Set();
                }
            };

            Assert.AreEqual(0, database.AllReplications.ToList().Count);
            Assert.AreEqual(0, database.ActiveReplicators.Count);

            replication.Start();

            Assert.AreEqual(1, database.AllReplications.ToList().Count);
            Assert.AreEqual(1, database.ActiveReplicators.Count);

            // TODO: Port full ReplicationFinishedObserver
            var failed = doneSignal.WaitOne(TimeSpan.FromSeconds(60));

            Assert.True(failed);

            //Active replicators also get removed in a changed callback and sometimes the done
            //signal gets set before the changed event that removes the active replicator has a
            //chance to run
            Thread.Sleep(TimeSpan.FromMilliseconds(500)); 
            Assert.AreEqual(1, database.AllReplications.Count());
            Assert.AreEqual(0, database.ActiveReplicators.Count);
        }

        [Test]
        public void TestUnsavedRevisionCacheRetainDocument()
        {
            var document = database.CreateDocument();

            database.DocumentCache.Remove(document.Id);

            Assert.IsNull(database.DocumentCache.Get(document.Id));

            var cachedDocument = database.UnsavedRevisionDocumentCache.Get(document.Id);
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

            var cachedDocument = database.UnsavedRevisionDocumentCache.Get(document.Id);
            Assert.IsNull(cachedDocument);

            var checkedDocument = database.GetDocument(document.Id);
            Assert.IsTrue(document == checkedDocument);
        }

        [Test]
        public void TestStubOutAttachmentsInRevBeforeRevPos()
        {
            var hello = new JObject();
            hello["revpos"] = 1;
            hello["follows"] = true;

            var goodbye = new JObject();
            goodbye["revpos"] = 2;
            goodbye["data"] = "squeee";

            var attachments = new JObject();
            attachments["hello"] = hello;
            attachments["goodbye"] = goodbye;

            var properties = new Dictionary<string, object>();
            properties["_attachments"] = attachments;

            IDictionary<string, object> expected = null;

            var rev = new RevisionInternal(properties);
            Database.StubOutAttachmentsInRevBeforeRevPos(rev, 3, false);
            var checkAttachments = rev.GetProperties()["_attachments"].AsDictionary<string, object>();
            var result = (IDictionary<string, object>)checkAttachments["hello"];
            expected = new Dictionary<string, object>();
            expected["revpos"] = 1;
            expected["stub"] = true;
            AssertPropertiesAreEqual(expected, result);
            result = (IDictionary<string, object>)checkAttachments["goodbye"];
            expected = new Dictionary<string, object>();
            expected["revpos"] = 2;
            expected["stub"] = true;
            AssertPropertiesAreEqual(expected, result);

            rev = new RevisionInternal(properties);
            Database.StubOutAttachmentsInRevBeforeRevPos(rev, 2, false);
            checkAttachments = rev.GetProperties()["_attachments"].AsDictionary<string, object>();
            result = checkAttachments["hello"].AsDictionary<string, object>();
            expected = new Dictionary<string, object>();
            expected["revpos"] = 1;
            expected["stub"] = true;
            AssertPropertiesAreEqual(expected, result);
            result = checkAttachments["goodbye"].AsDictionary<string, object>();
            expected = goodbye.AsDictionary<string, object>();
            AssertPropertiesAreEqual(expected, result);

            rev = new RevisionInternal(properties);
            Database.StubOutAttachmentsInRevBeforeRevPos(rev, 1, false);
            checkAttachments = rev.GetProperties()["_attachments"].AsDictionary<string, object>();
            result = checkAttachments["hello"].AsDictionary<string, object>();
            expected = hello.AsDictionary<string, object>();
            AssertPropertiesAreEqual(expected, result);
            result = checkAttachments["goodbye"].AsDictionary<string, object>();
            expected = goodbye.AsDictionary<string, object>();
            AssertPropertiesAreEqual(expected, result);

            //Test the follows mode
            rev = new RevisionInternal(properties);
            Database.StubOutAttachmentsInRevBeforeRevPos(rev, 3, true);
            checkAttachments = rev.GetProperties()["_attachments"].AsDictionary<string, object>();
            result = checkAttachments["hello"].AsDictionary<string, object>();
            expected = new Dictionary<string, object>();
            expected["revpos"] = 1;
            expected["stub"] = true;
            AssertPropertiesAreEqual(expected, result);
            result = checkAttachments["goodbye"].AsDictionary<string, object>();
            expected = new Dictionary<string, object>();
            expected["revpos"] = 2;
            expected["stub"] = true;
            AssertPropertiesAreEqual(expected, result);

            rev = new RevisionInternal(properties);
            Database.StubOutAttachmentsInRevBeforeRevPos(rev, 2, true);
            checkAttachments = rev.GetProperties()["_attachments"].AsDictionary<string, object>();
            result = checkAttachments["hello"].AsDictionary<string, object>();
            expected = new Dictionary<string, object>();
            expected["revpos"] = 1;
            expected["stub"] = true;
            AssertPropertiesAreEqual(expected, result);
            result = checkAttachments["goodbye"].AsDictionary<string, object>();
            expected = new Dictionary<string, object>();
            expected["revpos"] = 2;
            expected["follows"] = true;
            AssertPropertiesAreEqual(expected, result);

            rev = new RevisionInternal(properties);
            Database.StubOutAttachmentsInRevBeforeRevPos(rev, 1, true);
            checkAttachments = rev.GetProperties()["_attachments"].AsDictionary<string, object>();
            result = checkAttachments["hello"].AsDictionary<string, object>();
            expected = new Dictionary<string, object>();
            expected["revpos"] = 1;
            expected["follows"] = true;
            AssertPropertiesAreEqual(expected, result);
            result = checkAttachments["goodbye"].AsDictionary<string, object>();
            expected = new Dictionary<string, object>();
            expected["revpos"] = 2;
            expected["follows"] = true;
            AssertPropertiesAreEqual(expected, result);
        }

        [Test]
        public void TestEncodeDocumentJSON() 
        {
            var props = new Dictionary<string, object>() 
            {
                {"_local_seq", ""}
            };

            var revisionInternal = new RevisionInternal(props);
            var encoded = database.EncodeDocumentJSON(revisionInternal);
            Assert.IsNotNull(encoded);
        }

        [Test]
        public void TestWinningRevIDOfDoc()
        {
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

            var outIsDeleted = new List<Boolean>();
            var outIsConflict = new List<Boolean>();

            var docNumericId = database.GetDocNumericID(doc.Id);
            Assert.IsTrue(docNumericId != 0);
            Assert.AreEqual(rev1.Id, database.WinningRevIDOfDoc(docNumericId, outIsDeleted, outIsConflict));
            Assert.IsTrue(outIsConflict.Count == 0);

            outIsDeleted = new List<Boolean>();
            outIsConflict = new List<Boolean>();
            var newRev2a = rev1.CreateRevision();
            newRev2a.SetUserProperties(properties2a);
            var rev2a = newRev2a.Save();
            Assert.AreEqual(rev2a.Id, database.WinningRevIDOfDoc(docNumericId, outIsDeleted, outIsConflict));
            Assert.IsTrue(outIsConflict.Count == 0);

            outIsDeleted = new List<Boolean>();
            outIsConflict = new List<Boolean>();
            var newRev2b = rev1.CreateRevision();
            newRev2b.SetUserProperties(properties2b);
            newRev2b.Save(true);
            database.WinningRevIDOfDoc(docNumericId, outIsDeleted, outIsConflict);
            Assert.IsTrue(outIsConflict.Count > 0);
        }
    }
}

