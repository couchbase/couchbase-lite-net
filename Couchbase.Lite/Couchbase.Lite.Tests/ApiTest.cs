//
// ApiTest.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
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
using Couchbase.Lite;
using Sharpen;
using Couchbase.Lite.Util;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using System.Security.Permissions;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
	/// <summary>Created by andrey on 12/3/13.</summary>
	/// <remarks>Created by andrey on 12/3/13.</remarks>
    public class ApiTest : LiteTestCase
	{
        public static Task CreateDocumentsAsync(Database db, int n)
		{
            return db.RunAsync((database)=>
                {
                    database.BeginTransaction();
                    ApiTest.CreateDocuments(database, n);
                    database.EndTransaction(true);
                });
		}

  		public static void CreateDocuments(Database db, int numberOfDocsToCreate)
		{
			//TODO should be changed to use db.runInTransaction
			for (int i = 0; i < numberOfDocsToCreate; i++)
			{
                var properties = new Dictionary<String, Object>();
                properties["testName"] = "testDatabase";
                properties["sequence"] = i;
				CreateDocumentWithProperties(db, properties);
			}
		}

        public static Document CreateDocumentWithProperties(Database db, IDictionary<String, Object> properties)
		{
            var doc = db.CreateDocument();

			Assert.IsNotNull(doc);
			Assert.IsNull(doc.CurrentRevisionId);
			Assert.IsNull(doc.CurrentRevision);
			Assert.IsNotNull("Document has no ID", doc.Id);

			// 'untitled' docs are no longer untitled (8/10/12)
			try
			{
				doc.PutProperties(properties);
			}
			catch (Exception e)
			{
				Log.E(Tag, "Error creating document", e);
                Assert.IsTrue( false, "can't create new document in db:" + db.Name +
                    " with properties:" + properties.Aggregate(new StringBuilder(" >>> "), (str, kvp)=> { str.AppendFormat("'{0}:{1}' ", kvp.Key, kvp.Value); return str; }, str=>str.ToString()));
			}

			Assert.IsNotNull(doc.Id);
			Assert.IsNotNull(doc.CurrentRevisionId);
			Assert.IsNotNull(doc.UserProperties);
			Assert.AreEqual(db.GetDocument(doc.Id), doc);

			return doc;
		}

        /// <exception cref="System.Exception"></exception>
        public void RunLiveQuery(String methodNameToCall)
        {
            var db = StartDatabase();

            var doneSignal = new CountDownLatch(11); // FIXME.ZJG: Not sure why, but now Changed is only called once.

            // 11 corresponds to startKey = 23; endKey = 33
            // run a live query
            var view = db.GetView("vu");
            view.SetMap((document, emitter) => emitter (document ["sequence"], 1), "1");

            var query = view.CreateQuery().ToLiveQuery();
            query.StartKey = 23;
            query.EndKey = 33;

            Log.I(Tag, "Created  " + query);

            // these are the keys that we expect to see in the livequery change listener callback
            var expectedKeys = new HashSet<Int64>();
            for (var i = 23; i < 34; i++)
            {
                expectedKeys.AddItem(i);
            }

            // install a change listener which decrements countdown latch when it sees a new
            // key from the list of expected keys
            EventHandler<QueryChangeEventArgs> handler = (sender, e) => {
                var rows = e.Rows;
                foreach(var row in rows)
                {
                    if (expectedKeys.Contains((Int64)row.Key))
                    {
                        Console.WriteLine(Tag + " doneSignal decremented " + doneSignal.Count);
                        doneSignal.CountDown();
                    }
                }
            };

            query.Changed += handler;

            // create the docs that will cause the above change listener to decrement countdown latch
            var createTask = CreateDocumentsAsync(db, n: 50);
            createTask.Wait(TimeSpan.FromSeconds(5));
            if (methodNameToCall.Equals("start"))
            {
                // start the livequery running asynchronously
                query.Start();
            }
            else
            {
                Assert.IsNull(query.Rows);

                query.Run();

                // this will block until the query completes
                Assert.IsNotNull(query.Rows);
            }

            // wait for the doneSignal to be finished
            var success = doneSignal.Await(TimeSpan.FromSeconds(5));
            Assert.IsTrue(success, "Done signal timed out live query never ran");

            // stop the livequery since we are done with it
            query.Changed -= handler;
            query.Stop();
        }

		//SERVER & DOCUMENTS
		/// <exception cref="System.IO.IOException"></exception>
        [Test]
        public void TestAPIManager()
		{
            //StartDatabase();
			Manager manager = this.manager;
			Assert.IsTrue(manager != null);

            foreach (string dbName in manager.AllDatabaseNames)
			{
				Database db = manager.GetDatabase(dbName);
				Log.I(Tag, "Database '" + dbName + "':" + db.DocumentCount + " documents");
			}

			var options = new ManagerOptions();
			options.ReadOnly = true;

            var roManager = new Manager(new DirectoryInfo(manager.Directory), options);
			Assert.IsTrue(roManager != null);

            var nonExistantDatabase = roManager.GetDatabase("foo");
			Assert.IsNull(nonExistantDatabase);

            var dbNames = manager.AllDatabaseNames;
            Assert.IsFalse(dbNames.Contains<String>("foo"));
			Assert.IsTrue(dbNames.Contains(DefaultTestDb));
		}

        [Test]
        public void TestCreateDocument()
		{
            var properties = new Dictionary<String, Object>();
			properties["testName"] = "testCreateDocument";
            properties["tag"] = 1337L;

            var db = manager.GetExistingDatabase(DefaultTestDb);
			var doc = CreateDocumentWithProperties(db, properties);
            var docID = doc.Id;
			Assert.IsTrue(docID.Length > 10, "Invalid doc ID: " + docID);

            var currentRevisionID = doc.CurrentRevisionId;
			Assert.IsTrue(currentRevisionID.Length > 10, "Invalid doc revision: " + docID);
			Assert.AreEqual(doc.UserProperties, properties);
			Assert.AreEqual(db.GetDocument(docID), doc);

            db.DocumentCache.EvictAll();
			
            // so we can load fresh copies
            var doc2 = db.GetExistingDocument(docID);
			Assert.AreEqual(doc2.Id, docID);
			Assert.AreEqual(doc2.CurrentRevisionId, currentRevisionID);
			Assert.IsNull(db.GetExistingDocument("b0gus"));
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestDatabaseCompaction()
		{
            var properties = new Dictionary<String, Object>();
			properties["testName"] = "testDatabaseCompaction";
			properties["tag"] = 1337;

            var db = manager.GetExistingDatabase(DefaultTestDb);
            var doc = CreateDocumentWithProperties(db, properties);
            var rev1 = doc.CurrentRevision;
            var properties2 = new Dictionary<String, Object>(properties);
			properties2["tag"] = 4567;

            var rev2 = rev1.CreateRevision(properties2);

            db.Compact();

            var fetchedDoc = database.GetDocument(doc.Id);
            var revisions = fetchedDoc.RevisionHistory;

			foreach (SavedRevision revision in revisions)
			{
				if (revision.Id.Equals(rev1))
				{
                    Assert.IsFalse(revision.PropertiesAvailable);
				}
				if (revision.Id.Equals(rev2))
				{
					Assert.IsTrue(revision.PropertiesAvailable);
				}
			}
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestCreateRevisions()
		{
			var properties = new Dictionary<String, Object>();
			properties["testName"] = "testCreateRevisions";
			properties["tag"] = 1337;
            var db = StartDatabase();

			var doc = CreateDocumentWithProperties(db, properties);
			var rev1 = doc.CurrentRevision;
			Assert.IsTrue(rev1.Id.StartsWith("1-"));
			Assert.AreEqual(1, rev1.Sequence);
            Assert.AreEqual(0, rev1.Attachments.Count());

			// Test -createRevisionWithProperties:
            var properties2 = new Dictionary<String, Object>(properties);
			properties2["tag"] = 4567;
            var rev2 = rev1.CreateRevision(properties2);
			Assert.IsNotNull(rev2, "Put failed");
            Assert.IsTrue(doc.CurrentRevisionId.StartsWith("2-"), "Document revision ID is still " + doc.CurrentRevisionId);
			Assert.AreEqual(rev2.Id, doc.CurrentRevisionId);
            Assert.IsNotNull(rev2.PropertiesAvailable);
            Assert.AreEqual(rev2.UserProperties, properties2);
			Assert.AreEqual(rev2.Document, doc);
			Assert.AreEqual(rev2.GetProperty("_id"), doc.Id);
			Assert.AreEqual(rev2.GetProperty("_rev"), rev2.Id);
			
            // Test -createRevision:
            var newRev = rev2.CreateRevision();
			Assert.IsNull(newRev.Id);
			Assert.AreEqual(newRev.Parent, rev2);
			Assert.AreEqual(newRev.ParentId, rev2.Id);

            var listRevs = new AList<SavedRevision>();
            listRevs.Add(rev1);
            listRevs.Add(rev2);
			Assert.AreEqual(newRev.RevisionHistory, listRevs);
			Assert.AreEqual(newRev.Properties, rev2.Properties);
			Assert.AreEqual(newRev.UserProperties, rev2.UserProperties);

			var userProperties = new Dictionary<String, Object>();
			userProperties["because"] = "NoSQL";
			newRev.SetUserProperties(userProperties);
			Assert.AreEqual(newRev.UserProperties, userProperties);

			var expectProperties = new Dictionary<String, Object>();
			expectProperties["because"] = "NoSQL";
			expectProperties["_id"] = doc.Id;
			expectProperties["_rev"] = rev2.Id;
			Assert.AreEqual(newRev.Properties, expectProperties);

			var rev3 = newRev.Save();
			Assert.IsNotNull(rev3);
			Assert.AreEqual(rev3.UserProperties, newRev.UserProperties);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestCreateNewRevisions()
		{
			var properties = new Dictionary<String, Object>();
			properties["testName"] = "testCreateRevisions";
			properties["tag"] = 1337;
            var db = StartDatabase();

			var doc = db.CreateDocument();
            var newRev = doc.CreateRevision();
            var newRevDocument = newRev.Document;
			Assert.AreEqual(doc, newRevDocument);
			Assert.AreEqual(db, newRev.Database);
			Assert.IsNull(newRev.ParentId);
			Assert.IsNull(newRev.Parent);

			var expectProperties = new Dictionary<String, Object>();
			expectProperties["_id"] = doc.Id;
			Assert.AreEqual(expectProperties, newRev.Properties);
			Assert.IsTrue(!newRev.IsDeletion);
			Assert.AreEqual(newRev.Sequence, 0);

			//ios support another approach to set properties::
			//newRev.([@"testName"] = @"testCreateRevisions";
			//newRev[@"tag"] = @1337;
			newRev.SetUserProperties(properties);
			Assert.AreEqual(newRev.UserProperties, properties);

			var rev1 = newRev.Save();
			Assert.IsNotNull(rev1, "Save 1 failed");
			Assert.AreEqual(doc.CurrentRevision, rev1);
			Assert.IsNotNull(rev1.Id.StartsWith("1-"));
            Assert.AreEqual(1, rev1.Sequence);
			Assert.IsNull(rev1.ParentId);
			Assert.IsNull(rev1.Parent);

			newRev = rev1.CreateRevision();
			newRevDocument = newRev.Document;
			Assert.AreEqual(doc, newRevDocument);
			Assert.AreEqual(db, newRev.Database);
			Assert.AreEqual(rev1.Id, newRev.ParentId);
			Assert.AreEqual(rev1, newRev.Parent);
			Assert.AreEqual(rev1.Properties, newRev.Properties);
			Assert.AreEqual(rev1.UserProperties, newRev.UserProperties);
            Assert.IsTrue(!newRev.IsDeletion);

			// we can't add/modify one property as on ios. need  to add separate method?
			// newRev[@"tag"] = @4567;
			properties["tag"] = 4567;
			newRev.SetUserProperties(properties);
			var rev2 = newRev.Save();
			Assert.IsNotNull(rev2, "Save 2 failed");
			Assert.AreEqual(doc.CurrentRevision, rev2);
            Assert.IsTrue(rev2.Id.StartsWith("2-"));
			Assert.AreEqual(2, rev2.Sequence);
			Assert.AreEqual(rev1.Id, rev2.ParentId);
			Assert.AreEqual(rev1, rev2.Parent);
            Assert.IsTrue(doc.CurrentRevisionId.StartsWith("2-"), "Document revision ID is still " + doc.CurrentRevisionId);

			// Add a deletion/tombstone revision:
			newRev = doc.CreateRevision();
			Assert.AreEqual(rev2.Id, newRev.ParentId);
			Assert.AreEqual(rev2, newRev.Parent);

            newRev.IsDeletion = true;
			var rev3 = newRev.Save();
			Assert.IsNotNull(rev3, "Save 3 failed");
			Assert.AreEqual(doc.CurrentRevision, rev3);
            Assert.IsTrue (rev3.Id.StartsWith ("3-", StringComparison.Ordinal), "Unexpected revID " + rev3.Id);
			Assert.AreEqual(3, rev3.Sequence);
			Assert.IsTrue(rev3.IsDeletion);
            Assert.IsTrue(doc.Deleted);

            var doc2 = db.GetDocument(doc.Id);
			Assert.AreEqual(doc, doc2);
			Assert.IsNull(db.GetExistingDocument(doc.Id));
		}

		//API_SaveMultipleDocuments on IOS
		//API_SaveMultipleUnsavedDocuments on IOS
		//API_DeleteMultipleDocuments commented on IOS
		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestDeleteDocument()
		{
            var properties = new Dictionary<String, Object>();
			properties["testName"] = "testDeleteDocument";

            var db = manager.GetExistingDatabase(DefaultTestDb);
            var doc = CreateDocumentWithProperties(db, properties);
			
            Assert.IsTrue(!doc.Deleted);
			Assert.IsTrue(!doc.CurrentRevision.IsDeletion);

            doc.Delete();

            Assert.IsTrue(doc.Deleted);
			Assert.IsNotNull(doc.CurrentRevision.IsDeletion);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPurgeDocument()
		{
            var properties = new Dictionary<String, Object>();
			properties["testName"] = "testPurgeDocument";

            var db = manager.GetExistingDatabase(DefaultTestDb);
            var doc = CreateDocumentWithProperties(db, properties);
			Assert.IsNotNull(doc);

			doc.Purge();

            var redoc = db.DocumentCache[doc.Id];
			Assert.IsNull(redoc);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestAllDocuments()
		{
            var db = manager.GetExistingDatabase(DefaultTestDb); //StartDatabase();

            const int docsCount = 5;
            CreateDocuments(db, numberOfDocsToCreate: docsCount);

			// clear the cache so all documents/revisions will be re-fetched:
            db.DocumentCache.EvictAll();
			
            Log.I(Tag, "----- all documents -----");

            var query = db.CreateAllDocumentsQuery();
			//query.prefetch = YES;
			
            Log.I(Tag, "Getting all documents: " + query);

            var rows = query.Run();

            Assert.AreEqual(docsCount, rows.Count);

            var n = 0;
            foreach (var row in rows)
			{
				Log.I(Tag, "    --> " + row);

                var doc = row.Document;

				Assert.IsNotNull(doc, "Couldn't get doc from query");
				Assert.IsNotNull(doc.CurrentRevision.PropertiesAvailable, "QueryRow should have preloaded revision contents");

				Log.I(Tag, "        Properties =" + doc.Properties);

                Assert.IsNotNull(doc.Properties, "Couldn't get doc properties");
                Assert.AreEqual("testDatabase", doc.GetProperty("testName"));
				
                n++;
			}

            Assert.AreEqual(n, docsCount);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestLocalDocs()
		{
            var db = manager.GetExistingDatabase(DefaultTestDb);

            var properties = new Dictionary<String, Object>();
			properties["foo"] = "bar";

			var props = db.GetExistingLocalDocument("dock");
            Assert.IsNull(props);

            db.PutLocalDocument("dock" , properties);
			
            props = db.GetExistingLocalDocument("dock");
			Assert.AreEqual(props["foo"], "bar");
			
            var newProperties = new Dictionary<String, Object>();
			newProperties["FOOO"] = "BARRR";
            db.PutLocalDocument("dock", newProperties);
			
            props = db.GetExistingLocalDocument("dock");
            Assert.IsFalse(props.ContainsKey("foo"));
			Assert.AreEqual(props["FOOO"], "BARRR");
            Assert.IsNotNull(db.DeleteLocalDocument("dock"), "Couldn't delete local doc");
			
            props = db.GetExistingLocalDocument("dock");
			Assert.IsNull(props);
			Assert.IsNotNull(!db.DeleteLocalDocument("dock"),"Second delete should have failed");
		}

		//TODO issue: deleteLocalDocument should return error.code( see ios)
		// HISTORY
		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestHistory()
		{
			var properties = new Dictionary<String, Object>();
			properties["testName"] = "test06_History";
            properties["tag"] = 1L;
            var db = StartDatabase();

			var doc = CreateDocumentWithProperties(db, properties);
			var rev1ID = doc.CurrentRevisionId;
			Log.I(Tag, "1st revision: " + rev1ID);
            Assert.IsTrue (rev1ID.StartsWith ("1-", StringComparison.Ordinal), "1st revision looks wrong: " + rev1ID);
			Assert.AreEqual(doc.UserProperties, properties);

            properties = new Dictionary<String, Object>(doc.Properties);
			properties["tag"] = 2;
			Assert.IsNotNull(!properties.Equals(doc.Properties));
			Assert.IsNotNull(doc.PutProperties(properties));

			var rev2ID = doc.CurrentRevisionId;
			Log.I(Tag, "rev2ID" + rev2ID);
            Assert.IsTrue(rev2ID.StartsWith("2-", StringComparison.Ordinal), "2nd revision looks wrong:" + rev2ID);

            var revisions = doc.RevisionHistory.ToList();
            Log.I(Tag, "Revisions = " + revisions);
            Assert.AreEqual(revisions.Count, 2);

			var rev1 = revisions[0];
			Assert.AreEqual(rev1.Id, rev1ID);

			var gotProperties = rev1.Properties;
			Assert.AreEqual(1, gotProperties["tag"]);

			var rev2 = revisions[1];
			Assert.AreEqual(rev2.Id, rev2ID);
			Assert.AreEqual(rev2, doc.CurrentRevision);

			gotProperties = rev2.Properties;
			Assert.AreEqual(2, gotProperties["tag"]);

            var tmp = new AList<SavedRevision>();
			tmp.Add(rev2);
			Assert.AreEqual(doc.ConflictingRevisions, tmp);
			Assert.AreEqual(doc.LeafRevisions, tmp);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestConflict()
		{
			var prop = new Dictionary<String, Object>();
			prop["foo"] = "bar";

            var db = StartDatabase();

			var doc = CreateDocumentWithProperties(db, prop);

			var rev1 = doc.CurrentRevision;

            var properties = new Dictionary<String, Object>(doc.Properties);
            properties["tag"] = 2;

			var rev2a = doc.PutProperties(properties);
            properties = new Dictionary<String, Object>(rev1.Properties);
            properties["tag"] = 3;
			var newRev = rev1.CreateRevision();
            newRev.SetProperties(properties);
            var rev2b = newRev.Save(allowConflict: true);
			Assert.IsNotNull(rev2b, "Failed to create a a conflict");

            var confRevs = new AList<SavedRevision>();
			confRevs.AddItem(rev2b);
			confRevs.AddItem(rev2a);
			Assert.AreEqual(doc.ConflictingRevisions, confRevs);
			Assert.AreEqual(doc.LeafRevisions, confRevs);

			SavedRevision defaultRev;
			SavedRevision otherRev;
            if (String.CompareOrdinal (rev2a.Id, rev2b.Id) > 0)
            {
				defaultRev = rev2a;
				otherRev = rev2b;
			}
			else
			{
				defaultRev = rev2b;
				otherRev = rev2a;
			}
            Assert.AreEqual(doc.CurrentRevision, defaultRev);

			var query = db.CreateAllDocumentsQuery();
            query.AllDocsMode = AllDocsMode.ShowConflicts;
			var rows = query.Run();
			Assert.AreEqual(rows.Count, 1);

			var row = rows.GetRow(0);
            var revs = row.GetConflictingRevisions().ToList();
            Assert.AreEqual(2, revs.Count);
            Assert.AreEqual(revs[0], defaultRev);
            Assert.AreEqual(revs[1], otherRev);
		}

		//ATTACHMENTS
		/// <exception cref="System.Exception"></exception>
		/// <exception cref="System.IO.IOException"></exception>
        [Test]
        public void TestAttachments()
		{
			var properties = new Dictionary<String, Object>();
			properties["testName"] = "testAttachments";
			var db = manager.GetExistingDatabase(DefaultTestDb);

			var doc = CreateDocumentWithProperties(db, properties);
			var rev = doc.CurrentRevision;
			Assert.AreEqual(rev.Attachments.Count(), 0);
            Assert.AreEqual(rev.AttachmentNames.Count(), 0);
            Assert.IsNull(rev.GetAttachment("index.html"));

			var content = "This is a test attachment!";
            var body = new ByteArrayInputStream(Runtime.GetBytesForString(content).ToArray());

			var rev2 = doc.CreateRevision();
            rev2.SetAttachment("index.html", "text/plain; charset=utf-8", body);

			var rev3 = rev2.Save();

			Assert.IsNotNull(rev3);
			Assert.AreEqual(rev3.Attachments.Count(), 1);
            Assert.AreEqual(rev3.AttachmentNames.Count(), 1);

            var attach = rev3.GetAttachment("index.html");
			Assert.IsNotNull(attach);
			Assert.AreEqual(doc, attach.Document);
			Assert.AreEqual("index.html", attach.Name);

			var attNames = new AList<string>();
			attNames.AddItem("index.html");
			Assert.AreEqual(rev3.AttachmentNames, attNames);
			Assert.AreEqual("text/plain; charset=utf-8", attach.ContentType);

            var attachmentContent = Encoding.UTF8.GetString(attach.Content.ToArray());
            Assert.AreEqual(content, attachmentContent);
            Assert.AreEqual(Runtime.GetBytesForString(content).ToArray().Length, attach.Length);

			var newRev = rev3.CreateRevision();
			newRev.RemoveAttachment(attach.Name);
			var rev4 = newRev.Save();
			Assert.IsNotNull(rev4);
            Assert.AreEqual(0, rev4.AttachmentNames.Count());
		}

		//CHANGE TRACKING
		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestChangeTracking()
		{
            var doneSignal = new CountDownLatch(5);
            var db = StartDatabase();

            db.Changed += (sender, e) => doneSignal.CountDown ();

            var task = CreateDocumentsAsync(db, 5);

			// We expect that the changes reported by the server won't be notified, because those revisions
			// are already cached in memory.
            var success = doneSignal.Await(TimeSpan.FromSeconds(10));
			Assert.IsTrue(success);
			Assert.AreEqual(5, db.GetLastSequenceNumber());

            // Give transaction time to complete.
            System.Threading.Thread.Sleep(500);
            Assert.IsTrue(task.Status.HasFlag(TaskStatus.RanToCompletion));
		}

		//VIEWS
		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestCreateView()
		{
            var db = StartDatabase();

			var view = db.GetView("vu");
			Assert.IsNotNull(view);
			Assert.AreEqual(db, view.Database);
			Assert.AreEqual("vu", view.Name);
			Assert.IsNull(view.Map);
			Assert.IsNull(view.Reduce);

            view.SetMap((document, emitter) => emitter (document.Get ("sequence"), null), "1");
			Assert.IsNotNull(view.Map != null);

            CreateDocuments(db, numberOfDocsToCreate: 50);

            var query = view.CreateQuery();
			query.StartKey=23;
			query.EndKey=33;
            Assert.AreEqual(db, query.Database);

			var rows = query.Run();
			Assert.IsNotNull(rows);
            Assert.AreEqual(11, rows.Count);

        	var expectedKey = 23;
            foreach (var row in rows)
			{
				Assert.AreEqual(expectedKey, row.Key);
				Assert.AreEqual(expectedKey + 1, row.SequenceNumber);
				++expectedKey;
			}
		}

		//API_RunSlowView commented on IOS
		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestValidation()
		{
            var db = StartDatabase();

            db.SetValidation("uncool", (newRevision, context)=>
                {
                    {
                        if (newRevision.GetProperty("groovy") == null)
                        {
                            context.Reject("uncool");
                            return false;
                        }
                        return true;
                    }
                });
			var properties = new Dictionary<String, Object>();
			properties["groovy"] = "right on";
			properties["foo"] = "bar";

			var doc = db.CreateDocument();
			Assert.IsNotNull(doc.PutProperties(properties));

			properties = new Dictionary<String, Object>();
			properties["foo"] = "bar";
			doc = db.CreateDocument();

			try
			{
				Assert.IsNull(doc.PutProperties(properties));
			}
			catch (CouchbaseLiteException e)
			{
				//TODO
                Assert.AreEqual(StatusCode.Forbidden, e.GetCBLStatus().GetCode());
			}
		}

		//            assertEquals(e.getLocalizedMessage(), "forbidden: uncool"); //TODO: Not hooked up yet
		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestViewWithLinkedDocs()
		{
            var db = StartDatabase();

            const int numberOfDocs = 50;
			var docs = new Document[50];
            var lastDocID = String.Empty;

            for (var i = 0; i < numberOfDocs; i++)
			{
				var properties = new Dictionary<String, Object>();
				properties["sequence"] = i;
				properties["prev"] = lastDocID;

                var doc = CreateDocumentWithProperties(db, properties);
				docs[i] = doc;
				lastDocID = doc.Id;
			}

            var query = db.SlowQuery((document, emitter)=> emitter (document ["sequence"], new object[] { "_id", document ["prev"] }));
            query.StartKey = 23;
            query.EndKey = 33;
            query.Prefetch = true;

			var rows = query.Run();
			Assert.IsNotNull(rows);
			Assert.AreEqual(rows.Count, 11);

			var rowNumber = 23;
            foreach (var row in rows)
			{
				Assert.AreEqual(row.Key, rowNumber);

				var prevDoc = docs[rowNumber];
				Assert.AreEqual(row.DocumentId, prevDoc.Id);
				Assert.AreEqual(row.Document, prevDoc);

				++rowNumber;
			}
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestLiveQueryRun()
		{
			RunLiveQuery("run");
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestLiveQueryStart()
		{
			RunLiveQuery("start");
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestAsyncViewQuery()
		{
			var doneSignal = new CountDownLatch(1);
            var db = StartDatabase();

			View view = db.GetView("vu");
            view.SetMap((document, emitter) => emitter (document ["sequence"], null), "1");

            const int kNDocs = 50;
			CreateDocuments(db, kNDocs);

			var query = view.CreateQuery();
			query.StartKey=23;
			query.EndKey=33;

            var task = query.RunAsync().ContinueWith((resultTask) => 
            {
                Log.I (LiteTestCase.Tag, "Async query finished!");
                var rows = resultTask.Result;

                Assert.IsNotNull (rows);
                Assert.AreEqual (rows.Count, 11);

                var expectedKey = 23;
                for (IEnumerator<QueryRow> it = rows; it.MoveNext ();) {
                    var row = it.Current;
                    Assert.AreEqual (row.Document.Database, db);
                    Assert.AreEqual (row.Key, expectedKey);
                    ++expectedKey;
                }
                doneSignal.CountDown ();
            });

			Log.I(Tag, "Waiting for async query to finish...");

            var success = task.Wait(TimeSpan.FromSeconds(10));
			Assert.IsTrue(success, "Done signal timed out..StartKey=ry never ran");
		}

		/// <summary>
		/// Make sure that a database's map/Counte functions are shared with the shadow database instance
		/// running in the background server.
		/// </summary>
		/// <remarks>
		/// Make sure that a database's map/reduce functionDatabaseared with the shadow database instance
		/// running in the background server.
		/// </remarks>
		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestSharedMapBlocks()
		{
            var path = new DirectoryInfo(Path.Combine(GetRootDirectory().FullName, "API_SharedMapBlocks"));
            var mgr = new Manager(path, Manager.DefaultOptions);
            var db = mgr.GetDatabase("db");

			db.Open();
            db.SetFilter("phil", (r, p) => true);
            db.SetValidation("val", (p1, p2) => true);

            var view = db.GetView("view");
            var ok = view.SetMapReduce((p1, p2)=>{ return; }, (a, b, c) => { return null; }, null);

            Assert.IsNotNull(ok, "Couldn't set map/reduce");

            var map = view.Map;
            var reduce = view.Reduce;
            var filter = db.GetFilter("phil");
            var validation = db.GetValidation("val");
            var result = mgr.RunAsync("db", (database)=>
                {
                    Assert.IsNotNull(database);
                    View serverView = database.GetExistingView("view");
                    Assert.IsNotNull(serverView);
                    Assert.AreEqual(database.GetFilter("phil"), filter);
                    Assert.AreEqual(database.GetValidation("val"), validation);
                    Assert.AreEqual(serverView.Map, map);
                    Assert.AreEqual(serverView.Reduce, reduce);
                    return true;
                });
            result.Wait(TimeSpan.FromSeconds(5));
			// blocks until async task has run
			db.Close();
			mgr.Close();
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestChangeUUID()
		{
            var mgr = new Manager(new DirectoryInfo(Path.Combine(GetRootDirectory().FullName, "ChangeUUID")), Manager.DefaultOptions);
            var db = mgr.GetDatabase("db");

			db.Open();

            var pub = db.PublicUUID();
            var priv = db.PrivateUUID();
			Assert.IsTrue(pub.Length > 10);
			Assert.IsTrue(priv.Length > 10);
            Assert.IsTrue(db.ReplaceUUIDs(), "replaceUUIDs failed");
			Assert.IsFalse(pub.Equals(db.PublicUUID()));
			Assert.IsFalse(priv.Equals(db.PrivateUUID()));
			mgr.Close();
		}
	}
}
