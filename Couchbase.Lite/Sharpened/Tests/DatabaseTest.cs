//
// DatabaseTest.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
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
using Couchbase.Lite;
using Couchbase.Lite.Replicator;
using Sharpen;

namespace Couchbase.Lite
{
	public class DatabaseTest : LiteTestCase
	{
		/// <exception cref="System.Exception"></exception>
		public virtual void TestPruneRevsToMaxDepth()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testDatabaseCompaction");
			properties.Put("tag", 1337);
			Document doc = CreateDocumentWithProperties(database, properties);
			SavedRevision rev = doc.GetCurrentRevision();
			database.SetMaxRevTreeDepth(1);
			for (int i = 0; i < 10; i++)
			{
				IDictionary<string, object> properties2 = new Dictionary<string, object>(properties
					);
				properties2.Put("tag", i);
				rev = rev.CreateRevision(properties2);
			}
			int numPruned = database.PruneRevsToMaxDepth(1);
			NUnit.Framework.Assert.AreEqual(9, numPruned);
			Document fetchedDoc = database.GetDocument(doc.GetId());
			IList<SavedRevision> revisions = fetchedDoc.GetRevisionHistory();
			NUnit.Framework.Assert.AreEqual(1, revisions.Count);
			numPruned = database.PruneRevsToMaxDepth(1);
			NUnit.Framework.Assert.AreEqual(0, numPruned);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPruneRevsToMaxDepthViaCompact()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testDatabaseCompaction");
			properties.Put("tag", 1337);
			Document doc = CreateDocumentWithProperties(database, properties);
			SavedRevision rev = doc.GetCurrentRevision();
			database.SetMaxRevTreeDepth(1);
			for (int i = 0; i < 10; i++)
			{
				IDictionary<string, object> properties2 = new Dictionary<string, object>(properties
					);
				properties2.Put("tag", i);
				rev = rev.CreateRevision(properties2);
			}
			database.Compact();
			Document fetchedDoc = database.GetDocument(doc.GetId());
			IList<SavedRevision> revisions = fetchedDoc.GetRevisionHistory();
			NUnit.Framework.Assert.AreEqual(1, revisions.Count);
		}

		/// <summary>
		/// When making inserts in a transaction, the change notifications should
		/// be batched into a single change notification (rather than a change notification
		/// for each insert)
		/// </summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeListenerNotificationBatching()
		{
			int numDocs = 50;
			AtomicInteger atomicInteger = new AtomicInteger(0);
			CountDownLatch countDownLatch = new CountDownLatch(1);
			database.AddChangeListener(new _ChangeListener_78(atomicInteger));
			database.RunInTransaction(new _TransactionalTask_85(this, numDocs, countDownLatch
				));
			bool success = countDownLatch.Await(30, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(success);
			NUnit.Framework.Assert.AreEqual(1, atomicInteger.Get());
		}

		private sealed class _ChangeListener_78 : Database.ChangeListener
		{
			public _ChangeListener_78(AtomicInteger atomicInteger)
			{
				this.atomicInteger = atomicInteger;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				atomicInteger.IncrementAndGet();
			}

			private readonly AtomicInteger atomicInteger;
		}

		private sealed class _TransactionalTask_85 : TransactionalTask
		{
			public _TransactionalTask_85(DatabaseTest _enclosing, int numDocs, CountDownLatch
				 countDownLatch)
			{
				this._enclosing = _enclosing;
				this.numDocs = numDocs;
				this.countDownLatch = countDownLatch;
			}

			public bool Run()
			{
				LiteTestCase.CreateDocuments(this._enclosing.database, numDocs);
				countDownLatch.CountDown();
				return true;
			}

			private readonly DatabaseTest _enclosing;

			private readonly int numDocs;

			private readonly CountDownLatch countDownLatch;
		}

		/// <summary>
		/// When making inserts outside of a transaction, there should be a change notification
		/// for each insert (no batching)
		/// </summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeListenerNotification()
		{
			int numDocs = 50;
			AtomicInteger atomicInteger = new AtomicInteger(0);
			CountDownLatch countDownLatch = new CountDownLatch(1);
			database.AddChangeListener(new _ChangeListener_112(atomicInteger));
			CreateDocuments(database, numDocs);
			NUnit.Framework.Assert.AreEqual(numDocs, atomicInteger.Get());
		}

		private sealed class _ChangeListener_112 : Database.ChangeListener
		{
			public _ChangeListener_112(AtomicInteger atomicInteger)
			{
				this.atomicInteger = atomicInteger;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				atomicInteger.IncrementAndGet();
			}

			private readonly AtomicInteger atomicInteger;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestGetActiveReplications()
		{
			Uri remote = GetReplicationURL();
			Replication replication = (Replication)database.CreatePullReplication(remote);
			NUnit.Framework.Assert.AreEqual(0, database.GetAllReplications().Count);
			NUnit.Framework.Assert.AreEqual(0, database.GetActiveReplications().Count);
			replication.Start();
			NUnit.Framework.Assert.AreEqual(1, database.GetAllReplications().Count);
			NUnit.Framework.Assert.AreEqual(1, database.GetActiveReplications().Count);
			CountDownLatch replicationDoneSignal = new CountDownLatch(1);
			LiteTestCase.ReplicationFinishedObserver replicationFinishedObserver = new LiteTestCase.ReplicationFinishedObserver
				(replicationDoneSignal);
			replication.AddChangeListener(replicationFinishedObserver);
			bool success = replicationDoneSignal.Await(60, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(success);
			NUnit.Framework.Assert.AreEqual(1, database.GetAllReplications().Count);
			NUnit.Framework.Assert.AreEqual(0, database.GetActiveReplications().Count);
		}
	}
}
