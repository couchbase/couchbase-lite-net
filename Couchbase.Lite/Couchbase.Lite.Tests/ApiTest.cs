/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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

namespace Couchbase.Lite
{
	/// <summary>Created by andrey on 12/3/13.</summary>
	/// <remarks>Created by andrey on 12/3/13.</remarks>
	public class ApiTest : LiteTestCase
	{
		private int changeCount = 0;

		internal static void CreateDocumentsAsync(Database db, int n)
		{
            db.RunAsync((database)=>
                {
                    database.BeginTransaction();
                    ApiTest.CreateDocuments(database, n);
                    database.EndTransaction(true);
                });
		}

		internal static void CreateDocuments(Database db, int n)
		{
			//TODO should be changed to use db.runInTransaction
			for (int i = 0; i < n; i++)
			{
                var properties = new Dictionary<string, object>();
                properties["testName"] = "testDatabase";
                properties["sequence"] = i;
				CreateDocumentWithProperties(db, properties);
			}
		}

		internal static Document CreateDocumentWithProperties(Database db, IDictionary<string
			, object> properties)
		{
			Document doc = db.CreateDocument();
			NUnit.Framework.Assert.IsNotNull(doc);
			NUnit.Framework.Assert.IsNull(doc.CurrentRevisionId);
			NUnit.Framework.Assert.IsNull(doc.CurrentRevision);
			NUnit.Framework.Assert.IsNotNull("Document has no ID", doc.Id);
			// 'untitled' docs are no longer untitled (8/10/12)
			try
			{
				doc.PutProperties(properties);
			}
			catch (Exception e)
			{
				Log.E(Tag, "Error creating document", e);
                NUnit.Framework.Assert.IsTrue( false, "can't create new document in db:" + db.Name +
                    " with properties:" + properties.ToString());
			}
			NUnit.Framework.Assert.IsNotNull(doc.Id);
			NUnit.Framework.Assert.IsNotNull(doc.CurrentRevisionId);
			NUnit.Framework.Assert.IsNotNull(doc.UserProperties);
			NUnit.Framework.Assert.AreEqual(db.GetDocument(doc.Id), doc);
			return doc;
		}

		//SERVER & DOCUMENTS
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestAPIManager()
		{
			Manager manager = this.manager;
			NUnit.Framework.Assert.IsTrue(manager != null);
            foreach (string dbName in manager.AllDatabaseNames)
			{
				Database db = manager.GetDatabase(dbName);
				Log.I(Tag, "Database '" + dbName + "':" + db.DocumentCount + " documents");
			}
			var options = new ManagerOptions();
			options.ReadOnly = true;
            var roManager = new Manager(new DirectoryInfo(manager.Directory), options);
			NUnit.Framework.Assert.IsTrue(roManager != null);
			Database db_1 = roManager.GetDatabase("foo");
			NUnit.Framework.Assert.IsNull(db_1);
            var dbNames = manager.AllDatabaseNames;
            NUnit.Framework.Assert.IsFalse(dbNames.Contains<String>("foo"));
			NUnit.Framework.Assert.IsTrue(dbNames.Contains(DefaultTestDb));
		}

		public virtual void TestCreateDocument()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["testName"] = "testCreateDocument";
			properties["tag"] = 1337;
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			string docID = doc.Id;
			NUnit.Framework.Assert.IsTrue(docID.Length > 10, "Invalid doc ID: " + docID);
			string currentRevisionID = doc.CurrentRevisionId;
			NUnit.Framework.Assert.IsTrue(currentRevisionID
                .Length > 10, "Invalid doc revision: " + docID);
			NUnit.Framework.Assert.AreEqual(doc.UserProperties, properties);
			NUnit.Framework.Assert.AreEqual(db.GetDocument(docID), doc);
            db.DocumentCache.EvictAll();
			// so we can load fresh copies
			Document doc2 = db.GetExistingDocument(docID);
			NUnit.Framework.Assert.AreEqual(doc2.Id, docID);
			NUnit.Framework.Assert.AreEqual(doc2.CurrentRevisionId, currentRevisionID);
			NUnit.Framework.Assert.IsNull(db.GetExistingDocument("b0gus"));
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestDatabaseCompaction()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["testName"] = "testDatabaseCompaction";
			properties["tag"] = 1337;
			Document doc = CreateDocumentWithProperties(database, properties);
			SavedRevision rev1 = doc.CurrentRevision;
			IDictionary<string, object> properties2 = new Dictionary<string, object>(properties
				);
			properties2["tag"] = 4567;
			SavedRevision rev2 = rev1.CreateRevision(properties2);
			database.Compact();
			Document fetchedDoc = database.GetDocument(doc.Id);
            var revisions = fetchedDoc.RevisionHistory;
			foreach (SavedRevision revision in revisions)
			{
				if (revision.Id.Equals(rev1))
				{
                    NUnit.Framework.Assert.IsFalse(revision.PropertiesAvailable);
				}
				if (revision.Id.Equals(rev2))
				{
					NUnit.Framework.Assert.IsTrue(revision.PropertiesAvailable);
				}
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateRevisions()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["testName"] = "testCreateRevisions";
			properties["tag"] = 1337;
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			SavedRevision rev1 = doc.CurrentRevision;
			NUnit.Framework.Assert.IsTrue(rev1.Id.StartsWith("1-"));
			NUnit.Framework.Assert.AreEqual(1, rev1.Sequence);
            NUnit.Framework.Assert.AreEqual(0, rev1.Attachments.Count());
			// Test -createRevisionWithProperties:
            var properties2 = new Dictionary<string, object>(properties);
			properties2["tag"] = 4567;
            var rev2 = rev1.CreateRevision(properties2);
			NUnit.Framework.Assert.IsNotNull(rev2, "Put failed");
            NUnit.Framework.Assert.IsTrue(doc.CurrentRevisionId.StartsWith("2-"), "Document revision ID is still " + doc.CurrentRevisionId);
			NUnit.Framework.Assert.AreEqual(rev2.Id, doc.CurrentRevisionId);
            NUnit.Framework.Assert.IsNotNull(properties2);
            NUnit.Framework.Assert.AreEqual(rev2.PropertiesAvailable, rev2.UserProperties);
			NUnit.Framework.Assert.AreEqual(rev2.Document, doc);
			NUnit.Framework.Assert.AreEqual(rev2.GetProperty("_id"), doc.Id);
			NUnit.Framework.Assert.AreEqual(rev2.GetProperty("_rev"), rev2.Id);
			// Test -createRevision:
            var newRev = rev2.CreateRevision();
			NUnit.Framework.Assert.IsNull(newRev.Id);
			NUnit.Framework.Assert.AreEqual(newRev.Parent, rev2);
			NUnit.Framework.Assert.AreEqual(newRev.ParentId, rev2.Id);
            var listRevs = new AList<SavedRevision>();
            listRevs.Add(rev1);
            listRevs.Add(rev2);
			NUnit.Framework.Assert.AreEqual(newRev.RevisionHistory, listRevs);
			NUnit.Framework.Assert.AreEqual(newRev.Properties, rev2.Properties);
			NUnit.Framework.Assert.AreEqual(newRev.UserProperties, rev2.UserProperties);
			IDictionary<string, object> userProperties = new Dictionary<string, object>();
			userProperties["because"] = "NoSQL";
			newRev.SetUserProperties(userProperties);
			NUnit.Framework.Assert.AreEqual(newRev.UserProperties, userProperties);
			IDictionary<string, object> expectProperties = new Dictionary<string, object>();
			expectProperties["because"] = "NoSQL";
			expectProperties["_id"] = doc.Id;
			expectProperties["_rev"] = rev2.Id;
			NUnit.Framework.Assert.AreEqual(newRev.Properties, expectProperties);
			SavedRevision rev3 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev3);
			NUnit.Framework.Assert.AreEqual(rev3.UserProperties, newRev.UserProperties);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateNewRevisions()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["testName"] = "testCreateRevisions";
			properties["tag"] = 1337;
			Database db = StartDatabase();
			Document doc = db.CreateDocument();
            var newRev = doc.CreateRevision();
            var newRevDocument = newRev.Document;
			NUnit.Framework.Assert.AreEqual(doc, newRevDocument);
			NUnit.Framework.Assert.AreEqual(db, newRev.Database);
			NUnit.Framework.Assert.IsNull(newRev.ParentId);
			NUnit.Framework.Assert.IsNull(newRev.Parent);
			IDictionary<string, object> expectProperties = new Dictionary<string, object>();
			expectProperties["_id"] = doc.Id;
			NUnit.Framework.Assert.AreEqual(expectProperties, newRev.Properties);
			NUnit.Framework.Assert.IsTrue(!newRev.IsDeletion);
			NUnit.Framework.Assert.AreEqual(newRev.Sequence, 0);
			//ios support another approach to set properties::
			//newRev.([@"testName"] = @"testCreateRevisions";
			//newRev[@"tag"] = @1337;
			newRev.SetUserProperties(properties);
			NUnit.Framework.Assert.AreEqual(newRev.UserProperties, properties);
			SavedRevision rev1 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev1, "Save 1 failed");
			NUnit.Framework.Assert.AreEqual(doc.CurrentRevision, rev1);
			NUnit.Framework.Assert.IsNotNull(rev1.Id.StartsWith("1-"));
            NUnit.Framework.Assert.AreEqual(1, rev1.Sequence);
			NUnit.Framework.Assert.IsNull(rev1.ParentId);
			NUnit.Framework.Assert.IsNull(rev1.Parent);
			newRev = rev1.CreateRevision();
			newRevDocument = newRev.Document;
			NUnit.Framework.Assert.AreEqual(doc, newRevDocument);
			NUnit.Framework.Assert.AreEqual(db, newRev.Database);
			NUnit.Framework.Assert.AreEqual(rev1.Id, newRev.ParentId);
			NUnit.Framework.Assert.AreEqual(rev1, newRev.Parent);
			NUnit.Framework.Assert.AreEqual(rev1.Properties, newRev.Properties);
			NUnit.Framework.Assert.AreEqual(rev1.UserProperties, newRev.UserProperties);
            NUnit.Framework.Assert.IsTrue(!newRev.IsDeletion);
			// we can't add/modify one property as on ios. need  to add separate method?
			// newRev[@"tag"] = @4567;
			properties["tag"] = 4567;
			newRev.SetUserProperties(properties);
			SavedRevision rev2 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev2, "Save 2 failed");
			NUnit.Framework.Assert.AreEqual(doc.CurrentRevision, rev2);
            NUnit.Framework.Assert.IsTrue(rev2.Id.StartsWith("2-"));
			NUnit.Framework.Assert.AreEqual(2, rev2.Sequence);
			NUnit.Framework.Assert.AreEqual(rev1.Id, rev2.ParentId);
			NUnit.Framework.Assert.AreEqual(rev1, rev2.Parent);
            NUnit.Framework.Assert.IsTrue(doc.CurrentRevisionId.StartsWith("2-"), "Document revision ID is still " + doc.CurrentRevisionId);
			// Add a deletion/tombstone revision:
			newRev = doc.CreateRevision();
			NUnit.Framework.Assert.AreEqual(rev2.Id, newRev.ParentId);
			NUnit.Framework.Assert.AreEqual(rev2, newRev.Parent);
            newRev.IsDeletion = true;
			SavedRevision rev3 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev3, "Save 3 failed");
			NUnit.Framework.Assert.AreEqual(doc.CurrentRevision, rev3);
            NUnit.Framework.Assert.IsTrue (rev3.Id.StartsWith ("3-", StringComparison.Ordinal), "Unexpected revID " + rev3.Id);
			NUnit.Framework.Assert.AreEqual(3, rev3.Sequence);
			NUnit.Framework.Assert.IsTrue(rev3.IsDeletion);
            NUnit.Framework.Assert.IsTrue(doc.Deleted);

            Document doc2 = db.GetDocument(doc.Id);
			NUnit.Framework.Assert.AreEqual(doc, doc2);
			NUnit.Framework.Assert.IsNull(db.GetExistingDocument(doc.Id));
		}

		//API_SaveMultipleDocuments on IOS
		//API_SaveMultipleUnsavedDocuments on IOS
		//API_DeleteMultipleDocuments commented on IOS
		/// <exception cref="System.Exception"></exception>
		public virtual void TestDeleteDocument()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["testName"] = "testDeleteDocument";
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			NUnit.Framework.Assert.IsTrue(!doc.Deleted);
			NUnit.Framework.Assert.IsTrue(!doc.CurrentRevision.IsDeletion);
            doc.Delete();
            NUnit.Framework.Assert.IsTrue(doc.Deleted);
			NUnit.Framework.Assert.IsNotNull(doc.CurrentRevision.IsDeletion);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPurgeDocument()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["testName"] = "testPurgeDocument";
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			NUnit.Framework.Assert.IsNotNull(doc);
			doc.Purge();
            Document redoc = db.DocumentCache[doc.Id];
			NUnit.Framework.Assert.IsNull(redoc);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestAllDocuments()
		{
			Database db = StartDatabase();
			int kNDocs = 5;
			CreateDocuments(db, kNDocs);
			// clear the cache so all documents/revisions will be re-fetched:
            db.DocumentCache.EvictAll();
			Log.I(Tag, "----- all documents -----");
			Query query = db.CreateAllDocumentsQuery();
			//query.prefetch = YES;
			Log.I(Tag, "Getting all documents: " + query);
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.AreEqual(rows.Count, kNDocs);
			int n = 0;
			for (IEnumerator<QueryRow> it = rows; it.MoveNext(); )
			{
				QueryRow row = it.Current;
				Log.I(Tag, "    --> " + row);
				Document doc = row.Document;
				NUnit.Framework.Assert.IsNotNull(doc, "Couldn't get doc from query");
				NUnit.Framework.Assert.IsNotNull(doc.CurrentRevision.PropertiesAvailable, "QueryRow should have preloaded revision contents"
                    );
				Log.I(Tag, "        Properties =" + doc.Properties);
                NUnit.Framework.Assert.IsNotNull(doc.Properties, "Couldn't get doc properties");
				NUnit.Framework.Assert.AreEqual(doc.GetProperty("testName"), "testDatabase");
				n++;
			}
			NUnit.Framework.Assert.AreEqual(n, kNDocs);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestLocalDocs()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["foo"] = "bar";
			Database db = StartDatabase();
			IDictionary<string, object> props = db.GetExistingLocalDocument("dock");
            NUnit.Framework.Assert.IsNull(props);
            db.PutLocalDocument("dock" , properties);
			props = db.GetExistingLocalDocument("dock");
			NUnit.Framework.Assert.AreEqual(props["foo"], "bar");
			IDictionary<string, object> newProperties = new Dictionary<string, object>();
			newProperties["FOOO"] = "BARRR";
            db.PutLocalDocument("dock", newProperties);
			props = db.GetExistingLocalDocument("dock");
			NUnit.Framework.Assert.IsNull(props["foo"]);
			NUnit.Framework.Assert.AreEqual(props["FOOO"], "BARRR");
            NUnit.Framework.Assert.IsNotNull(db.DeleteLocalDocument("dock"), "Couldn't delete local doc");
			props = db.GetExistingLocalDocument("dock");
			NUnit.Framework.Assert.IsNull(props);
			NUnit.Framework.Assert.IsNotNull(!db.DeleteLocalDocument("dock"),"Second delete should have failed");
		}

		//TODO issue: deleteLocalDocument should return error.code( see ios)
		// HISTORY
		/// <exception cref="System.Exception"></exception>
		public virtual void TestHistory()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["testName"] = "test06_History";
			properties["tag"] = 1;
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			string rev1ID = doc.CurrentRevisionId;
			Log.I(Tag, "1st revision: " + rev1ID);
            NUnit.Framework.Assert.IsTrue (rev1ID.StartsWith ("1-", StringComparison.Ordinal), "1st revision looks wrong: " + rev1ID);
			NUnit.Framework.Assert.AreEqual(doc.UserProperties, properties);
            properties = new Dictionary<string, object>(doc.Properties);
			properties["tag"] = 2;
			NUnit.Framework.Assert.IsNotNull(!properties.Equals(doc.Properties));
			NUnit.Framework.Assert.IsNotNull(doc.PutProperties(properties));
			string rev2ID = doc.CurrentRevisionId;
			Log.I(Tag, "rev2ID" + rev2ID);
                NUnit.Framework.Assert.IsTrue(rev2ID.StartsWith("2-", StringComparison.Ordinal), "2nd revision looks wrong:" + rev2ID);
            var revisions = doc.RevisionHistory.ToList();
            Log.I(Tag, "Revisions = " + revisions);
            NUnit.Framework.Assert.AreEqual(revisions.Count, 2);
			SavedRevision rev1 = revisions[0];
			NUnit.Framework.Assert.AreEqual(rev1.Id, rev1ID);
			IDictionary<string, object> gotProperties = rev1.Properties;
			NUnit.Framework.Assert.AreEqual(1, gotProperties["tag"]);
			SavedRevision rev2 = revisions[1];
			NUnit.Framework.Assert.AreEqual(rev2.Id, rev2ID);
			NUnit.Framework.Assert.AreEqual(rev2, doc.CurrentRevision);
			gotProperties = rev2.Properties;
			NUnit.Framework.Assert.AreEqual(2, gotProperties["tag"]);
            var tmp = new AList<SavedRevision>();
			tmp.Add(rev2);
			NUnit.Framework.Assert.AreEqual(doc.ConflictingRevisions, tmp);
			NUnit.Framework.Assert.AreEqual(doc.LeafRevisions, tmp);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestConflict()
		{
			IDictionary<string, object> prop = new Dictionary<string, object>();
			prop["foo"] = "bar";
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, prop);
			SavedRevision rev1 = doc.CurrentRevision;
            var properties = new Dictionary<string, object>(doc.Properties);
			properties["tag"] = 2;
			SavedRevision rev2a = doc.PutProperties(properties);
            properties = new Dictionary<string, object>(rev1.Properties);
			properties["tag"] = 3;
			UnsavedRevision newRev = rev1.CreateRevision();
            newRev.SetProperties(properties);
			bool allowConflict = true;
            SavedRevision rev2b = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev2b, "Failed to create a a conflict");
            var confRevs = new AList<SavedRevision>();
			confRevs.AddItem(rev2b);
			confRevs.AddItem(rev2a);
			NUnit.Framework.Assert.AreEqual(doc.ConflictingRevisions, confRevs);
			NUnit.Framework.Assert.AreEqual(doc.LeafRevisions, confRevs);
			SavedRevision defaultRev;
			SavedRevision otherRev;
			if (Sharpen.Runtime.CompareOrdinal(rev2a.Id, rev2b.Id) > 0)
			{
				defaultRev = rev2a;
				otherRev = rev2b;
			}
			else
			{
				defaultRev = rev2b;
				otherRev = rev2a;
			}
			NUnit.Framework.Assert.AreEqual(doc.CurrentRevision, defaultRev);
			Query query = db.CreateAllDocumentsQuery();
            query.AllDocsMode = AllDocsMode.ShowConflicts;
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.AreEqual(rows.Count, 1);
			QueryRow row = rows.GetRow(0);
            IList<SavedRevision> revs = row.GetConflictingRevisions().ToList();
			NUnit.Framework.Assert.AreEqual(revs.Count, 2);
			NUnit.Framework.Assert.AreEqual(revs[0], defaultRev);
			NUnit.Framework.Assert.AreEqual(revs[1], otherRev);
		}

		//ATTACHMENTS
		/// <exception cref="System.Exception"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestAttachments()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["testName"] = "testAttachments";
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			SavedRevision rev = doc.CurrentRevision;
			NUnit.Framework.Assert.AreEqual(rev.Attachments.Count(), 0);
            NUnit.Framework.Assert.AreEqual(rev.AttachmentNames.Count(), 0);
            NUnit.Framework.Assert.IsNull(rev.GetAttachment("index.html"));
			string content = "This is a test attachment!";
            var body = new ByteArrayInputStream(Sharpen.Runtime.GetBytesForString(content).ToArray());
			UnsavedRevision rev2 = doc.CreateRevision();
            rev2.SetAttachment("index.html", "text/plain; charset=utf-8", body);
			SavedRevision rev3 = rev2.Save();
			NUnit.Framework.Assert.IsNotNull(rev3);
			NUnit.Framework.Assert.AreEqual(rev3.Attachments.Count(), 1);
            NUnit.Framework.Assert.AreEqual(rev3.AttachmentNames.Count(), 1);
            Attachment attach = rev3.GetAttachment("index.html");
			NUnit.Framework.Assert.IsNotNull(attach);
			NUnit.Framework.Assert.AreEqual(doc, attach.Document);
			NUnit.Framework.Assert.AreEqual("index.html", attach.Name);
			IList<string> attNames = new AList<string>();
			attNames.AddItem("index.html");
			NUnit.Framework.Assert.AreEqual(rev3.AttachmentNames, attNames);
			NUnit.Framework.Assert.AreEqual("text/plain; charset=utf-8", attach.ContentType);
            NUnit.Framework.Assert.AreEqual(Encoding.UTF8.GetString(attach.Content.ToArray()), content);
            NUnit.Framework.Assert.AreEqual(Sharpen.Runtime.GetBytesForString(content).ToArray().Length
                , attach.Length);
			UnsavedRevision newRev = rev3.CreateRevision();
			newRev.RemoveAttachment(attach.Name);
			SavedRevision rev4 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev4);
            NUnit.Framework.Assert.AreEqual(0, rev4.AttachmentNames.Count());
		}

		//CHANGE TRACKING
		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTracking()
		{
			var doneSignal = new CountDownLatch(1);
			Database db = StartDatabase();
            db.Changed += (sender, e) => doneSignal.CountDown();
			CreateDocumentsAsync(db, 5);
			// We expect that the changes reported by the server won't be notified, because those revisions
			// are already cached in memory.
			bool success = doneSignal.Await(300, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(success);
			NUnit.Framework.Assert.AreEqual(5, db.GetLastSequenceNumber());
		}

		//VIEWS
		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateView()
		{
			Database db = StartDatabase();
			View view = db.GetView("vu");
			NUnit.Framework.Assert.IsNotNull(view);
			NUnit.Framework.Assert.AreEqual(db, view.Database);
			NUnit.Framework.Assert.AreEqual("vu", view.Name);
			NUnit.Framework.Assert.IsNull(view.Map);
			NUnit.Framework.Assert.IsNull(view.Reduce);
            view.SetMap((document, emitter) => emitter (document.Get ("sequence"), null), "1");
			NUnit.Framework.Assert.IsNotNull(view.Map != null);
			int kNDocs = 50;
			CreateDocuments(db, kNDocs);
            Query query = view.CreateQuery();
			query.StartKey=23;
			query.EndKey=33;
            NUnit.Framework.Assert.AreEqual(db, query.Database);
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.IsNotNull(rows);
            NUnit.Framework.Assert.AreEqual(11, rows.Count);
        	int expectedKey = 23;
			for (IEnumerator<QueryRow> it = rows; it.MoveNext(); )
			{
				QueryRow row = it.Current;
				NUnit.Framework.Assert.AreEqual(expectedKey, row.Key);
				NUnit.Framework.Assert.AreEqual(expectedKey + 1, row.SequenceNumber);
				++expectedKey;
			}
		}

		//API_RunSlowView commented on IOS
		/// <exception cref="System.Exception"></exception>
		public virtual void TestValidation()
		{
			Database db = StartDatabase();
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
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["groovy"] = "right on";
			properties["foo"] = "bar";
			Document doc = db.CreateDocument();
			NUnit.Framework.Assert.IsNotNull(doc.PutProperties(properties));
			properties = new Dictionary<string, object>();
			properties["foo"] = "bar";
			doc = db.CreateDocument();
			try
			{
				NUnit.Framework.Assert.IsNull(doc.PutProperties(properties));
			}
			catch (CouchbaseLiteException e)
			{
				//TODO
				NUnit.Framework.Assert.AreEqual(e.GetCBLStatus().GetCode(), StatusCode.Forbidden);
			}
		}

		//            assertEquals(e.getLocalizedMessage(), "forbidden: uncool"); //TODO: Not hooked up yet
		/// <exception cref="System.Exception"></exception>
		public virtual void TestViewWithLinkedDocs()
		{
			Database db = StartDatabase();
			int kNDocs = 50;
			var docs = new Document[50];
			string lastDocID = string.Empty;
			for (int i = 0; i < kNDocs; i++)
			{
				IDictionary<string, object> properties = new Dictionary<string, object>();
				properties["sequence"] = i;
				properties["prev"] = lastDocID;
                Document doc = CreateDocumentWithProperties(db, properties);
				docs[i] = doc;
				lastDocID = doc.Id;
			}
            var query=db.SlowQuery((document, emitter)=>
                {
                            emitter(document["sequence"], new object[] { "_id", document["prev"]});
                 });
			query.StartKey=23;
			query.EndKey=33;
			query.Prefetch=true;
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.IsNotNull(rows);
			NUnit.Framework.Assert.AreEqual(rows.Count, 11);
			int rowNumber = 23;
			for (IEnumerator<QueryRow> it = rows; it.MoveNext(); )
			{
				QueryRow row = it.Current;
				NUnit.Framework.Assert.AreEqual(row.Key, rowNumber);
				Document prevDoc = docs[rowNumber];
				NUnit.Framework.Assert.AreEqual(row.DocumentId, prevDoc.Id);
				NUnit.Framework.Assert.AreEqual(row.Document, prevDoc);
				++rowNumber;
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestLiveQueryRun()
		{
			RunLiveQuery("run");
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestLiveQueryStart()
		{
			RunLiveQuery("start");
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void RunLiveQuery(string methodNameToCall)
		{
			Database db = StartDatabase();
			var doneSignal = new CountDownLatch(11);
			// 11 corresponds to startKey=23; endKey=33
			// run a live query
			View view = db.GetView("vu");
            view.SetMap((IDictionary<string, object> document, EmitDelegate emitter)=> 
                {
                    emitter(document["sequence"], 1);
                }, "1");
			LiveQuery query = view.CreateQuery().ToLiveQuery();
			query.StartKey=23;
			query.EndKey=33;
			Log.I(Tag, "Created  " + query);
			// these are the keys that we expect to see in the livequery change listener callback
            var expectedKeys = new HashSet<int>();
			for (int i = 23; i < 34; i++)
			{
				expectedKeys.AddItem(i);
			}
			// install a change listener which decrements countdown latch when it sees a new
			// key from the list of expected keys
            EventHandler<QueryChangeEventArgs> handler = (sender, e) =>
                {
                    var rows = e.Rows;
                for (var it = rows; it.MoveNext(); )
                    {
                    QueryRow row = it.Current;
                        if (expectedKeys.Contains(row.Key))
                        {
                        expectedKeys.Remove((int)row.Key);
                            doneSignal.CountDown();
                        }
                    }
            };
            query.Changed  += handler;
			// create the docs that will cause the above change listener to decrement countdown latch
			int kNDocs = 50;
			CreateDocumentsAsync(db, kNDocs);
			if (methodNameToCall.Equals("start"))
			{
				// start the livequery running asynchronously
				query.Start();
			}
			else
			{
				NUnit.Framework.Assert.IsNull(query.Rows);
				query.Run();
				// this will block until the query completes
				NUnit.Framework.Assert.IsNotNull(query.Rows);
			}
			// wait for the doneSignal to be finished
			bool success = doneSignal.Await(300, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(success, "Done signal timed out live query never ran");
			// stop the livequery since we are done with it
            query.Changed -= handler;
			query.Stop();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestAsyncViewQuery()
		{
			var doneSignal = new CountDownLatch(1);
			Database db = StartDatabase();
			View view = db.GetView("vu");
            view.SetMap((document, emitter) => emitter (document ["sequence"], null), "1");

            const int kNDocs = 50;
			CreateDocuments(db, kNDocs);

			Query query = view.CreateQuery();
			query.StartKey=23;
			query.EndKey=33;

            var task = query.RunAsync().ContinueWith((resultTask) => 
            {
                Log.I (LiteTestCase.Tag, "Async query finished!");
                var rows = resultTask.Result;

                NUnit.Framework.Assert.IsNotNull (rows);
                NUnit.Framework.Assert.AreEqual (rows.Count, 11);

                int expectedKey = 23;
                for (IEnumerator<QueryRow> it = rows; it.MoveNext ();) {
                    QueryRow row = it.Current;
                    NUnit.Framework.Assert.AreEqual (row.Document.Database, db);
                    NUnit.Framework.Assert.AreEqual (row.Key, expectedKey);
                    ++expectedKey;
                }
                doneSignal.CountDown ();
            });

			Log.I(Tag, "Waiting for async query to finish...");

            var success = task.Wait(300000);
			NUnit.Framework.Assert.IsTrue(success, "Done signal timed out..StartKey=ry never ran");
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
		public virtual void TestSharedMapBlocks()
		{
            var path = new DirectoryInfo(Path.Combine(GetRootDirectory().FullName, "API_SharedMapBlocks"));
            var mgr = new Manager(path, Manager.DefaultOptions);
            var db = mgr.GetDatabase("db");

			db.Open();
            db.SetFilter("phil", (r, p) => true);
            db.SetValidation("val", (p1, p2) => true);

            var view = db.GetView("view");
            var ok = view.SetMapReduce((p1, p2)=>{ return; }, (a, b, c) => { return null; }, null);

            NUnit.Framework.Assert.IsNotNull(ok, "Couldn't set map/reduce");

            var map = view.Map;
            var reduce = view.Reduce;
            var filter = db.GetFilter("phil");
            var validation = db.GetValidation("val");
            var result = mgr.RunAsync("db", (database)=>
                {
                    NUnit.Framework.Assert.IsNotNull(database);
                    View serverView = database.GetExistingView("view");
                    NUnit.Framework.Assert.IsNotNull(serverView);
                    NUnit.Framework.Assert.AreEqual(database.GetFilter("phil"), filter);
                    NUnit.Framework.Assert.AreEqual(database.GetValidation("val"), validation);
                    NUnit.Framework.Assert.AreEqual(serverView.Map, map);
                    NUnit.Framework.Assert.AreEqual(serverView.Reduce, reduce);
                    return true;
                });
            result.RunSynchronously();
			// blocks until async task has run
			db.Close();
			mgr.Close();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeUUID()
		{
            var mgr = new Manager(new DirectoryInfo(Path.Combine(GetRootDirectory().FullName, "ChangeUUID")), Manager.DefaultOptions);
            var db = mgr.GetDatabase("db");

			db.Open();

            var pub = db.PublicUUID();
            var priv = db.PrivateUUID();
			NUnit.Framework.Assert.IsTrue(pub.Length > 10);
			NUnit.Framework.Assert.IsTrue(priv.Length > 10);
            NUnit.Framework.Assert.IsTrue(db.ReplaceUUIDs(), "replaceUUIDs failed");
			NUnit.Framework.Assert.IsFalse(pub.Equals(db.PublicUUID()));
			NUnit.Framework.Assert.IsFalse(priv.Equals(db.PrivateUUID()));
			mgr.Close();
		}
	}
}
