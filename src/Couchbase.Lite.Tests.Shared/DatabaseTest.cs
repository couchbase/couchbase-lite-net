﻿//
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
using System.Threading.Tasks;
using System.Threading;

namespace Couchbase.Lite
{
    public class DatabaseTest : LiteTestCase
    {
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
        public async void TestGetActiveReplications()
        {
            var remote = GetReplicationURL();
            var replication = database.CreatePullReplication(remote);

            Assert.AreEqual(0, database.AllReplications.ToList().Count);
            Assert.AreEqual(0, database.ActiveReplicators.Count);

            replication.Start();

            Assert.AreEqual(1, database.AllReplications.ToList().Count);
            Assert.AreEqual(1, database.ActiveReplicators.Count);

            // TODO: Port full ReplicationFinishedObserver
            var doneSignal = new CountdownEvent(1);
            var replicateTask = new TaskFactory().StartNew(()=>
                {
                    replication.Changed += (sender, e) => {
                        if (!replication.IsRunning) {
                            doneSignal.Signal();
                        }
                    };
                    return doneSignal.Wait(TimeSpan.FromSeconds(30));
                });
            var failed = await replicateTask;
            Assert.True(failed);
            Assert.AreEqual(1, database.AllReplications.ToList().Count);
            Assert.AreEqual(0, database.ActiveReplicators.Count);
        }
    }
}

