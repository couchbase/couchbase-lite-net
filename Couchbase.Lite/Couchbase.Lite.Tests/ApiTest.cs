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
			ManagerOptions options = new ManagerOptions();
			options.ReadOnly = true;
			Manager roManager = new Manager(new FilePath(manager.Directory), options);
			NUnit.Framework.Assert.IsTrue(roManager != null);
			Database db_1 = roManager.GetDatabase("foo");
			NUnit.Framework.Assert.IsNull(db_1);
            var dbNames = manager.AllDatabaseNames;
			NUnit.Framework.Assert.IsFalse(dbNames.Contains("foo"));
			NUnit.Framework.Assert.IsTrue(dbNames.Contains(DefaultTestDb));
		}

		public virtual void TestCreateDocument()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testCreateDocument");
			properties.Put("tag", 1337);
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			string docID = doc.Id;
			NUnit.Framework.Assert.IsTrue("Invalid doc ID: " + docID, docID.Length > 10);
			string currentRevisionID = doc.CurrentRevisionId;
			NUnit.Framework.Assert.IsTrue("Invalid doc revision: " + docID, currentRevisionID
				.Length > 10);
			NUnit.Framework.Assert.AreEqual(doc.UserProperties, properties);
			NUnit.Framework.Assert.AreEqual(db.GetDocument(docID), doc);
			db.ClearDocumentCache();
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
			properties.Put("testName", "testDatabaseCompaction");
			properties.Put("tag", 1337);
			Document doc = CreateDocumentWithProperties(database, properties);
			SavedRevision rev1 = doc.CurrentRevision;
			IDictionary<string, object> properties2 = new Dictionary<string, object>(properties
				);
			properties2.Put("tag", 4567);
			SavedRevision rev2 = rev1.CreateRevision(properties2);
			database.Compact();
			Document fetchedDoc = database.GetDocument(doc.Id);
			IList<SavedRevision> revisions = fetchedDoc.GetRevisionHistory();
			foreach (SavedRevision revision in revisions)
			{
				if (revision.Id.Equals(rev1))
				{
					NUnit.Framework.Assert.IsFalse(revision.ArePropertiesAvailable());
				}
				if (revision.Id.Equals(rev2))
				{
					NUnit.Framework.Assert.IsTrue(revision.ArePropertiesAvailable());
				}
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateRevisions()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testCreateRevisions");
			properties.Put("tag", 1337);
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			SavedRevision rev1 = doc.CurrentRevision;
			NUnit.Framework.Assert.IsTrue(rev1.Id.StartsWith("1-"));
			NUnit.Framework.Assert.AreEqual(1, rev1.GetSequence());
			NUnit.Framework.Assert.AreEqual(0, rev1.GetAttachments().Count);
			// Test -createRevisionWithProperties:
			IDictionary<string, object> properties2 = new Dictionary<string, object>(properties
				);
			properties2.Put("tag", 4567);
			SavedRevision rev2 = rev1.CreateRevision(properties2);
			NUnit.Framework.Assert.IsNotNull("Put failed", rev2);
			NUnit.Framework.Assert.IsTrue("Document revision ID is still " + doc.GetCurrentRevisionId
				(), doc.CurrentRevisionId.StartsWith("2-"));
			NUnit.Framework.Assert.AreEqual(rev2.Id, doc.CurrentRevisionId);
			NUnit.Framework.Assert.IsNotNull(rev2.ArePropertiesAvailable());
			NUnit.Framework.Assert.AreEqual(rev2.UserProperties, properties2);
			NUnit.Framework.Assert.AreEqual(rev2.GetDocument(), doc);
			NUnit.Framework.Assert.AreEqual(rev2.GetProperty("_id"), doc.Id);
			NUnit.Framework.Assert.AreEqual(rev2.GetProperty("_rev"), rev2.Id);
			// Test -createRevision:
			UnsavedRevision newRev = rev2.CreateRevision();
			NUnit.Framework.Assert.IsNull(newRev.Id);
			NUnit.Framework.Assert.AreEqual(newRev.GetParentRevision(), rev2);
			NUnit.Framework.Assert.AreEqual(newRev.GetParentRevisionId(), rev2.Id);
			IList<SavedRevision> listRevs = new AList<SavedRevision>();
			listRevs.AddItem(rev1);
			listRevs.AddItem(rev2);
			NUnit.Framework.Assert.AreEqual(newRev.GetRevisionHistory(), listRevs);
			NUnit.Framework.Assert.AreEqual(newRev.Properties, rev2.Properties);
			NUnit.Framework.Assert.AreEqual(newRev.UserProperties, rev2.GetUserProperties
				());
			IDictionary<string, object> userProperties = new Dictionary<string, object>();
			userProperties.Put("because", "NoSQL");
			newRev.SetUserProperties(userProperties);
			NUnit.Framework.Assert.AreEqual(newRev.UserProperties, userProperties);
			IDictionary<string, object> expectProperties = new Dictionary<string, object>();
			expectProperties.Put("because", "NoSQL");
			expectProperties.Put("_id", doc.Id);
			expectProperties.Put("_rev", rev2.Id);
			NUnit.Framework.Assert.AreEqual(newRev.Properties, expectProperties);
			SavedRevision rev3 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev3);
			NUnit.Framework.Assert.AreEqual(rev3.UserProperties, newRev.GetUserProperties
				());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateNewRevisions()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testCreateRevisions");
			properties.Put("tag", 1337);
			Database db = StartDatabase();
			Document doc = db.CreateDocument();
			UnsavedRevision newRev = doc.CreateRevision();
			Document newRevDocument = newRev.GetDocument();
			NUnit.Framework.Assert.AreEqual(doc, newRevDocument);
			NUnit.Framework.Assert.AreEqual(db, newRev.Database);
			NUnit.Framework.Assert.IsNull(newRev.GetParentRevisionId());
			NUnit.Framework.Assert.IsNull(newRev.GetParentRevision());
			IDictionary<string, object> expectProperties = new Dictionary<string, object>();
			expectProperties.Put("_id", doc.Id);
			NUnit.Framework.Assert.AreEqual(expectProperties, newRev.Properties);
			NUnit.Framework.Assert.IsTrue(!newRev.IsDeletion());
			NUnit.Framework.Assert.AreEqual(newRev.GetSequence(), 0);
			//ios support another approach to set properties::
			//newRev.([@"testName"] = @"testCreateRevisions";
			//newRev[@"tag"] = @1337;
			newRev.SetUserProperties(properties);
			NUnit.Framework.Assert.AreEqual(newRev.UserProperties, properties);
			SavedRevision rev1 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull("Save 1 failed", rev1);
			NUnit.Framework.Assert.AreEqual(doc.CurrentRevision, rev1);
			NUnit.Framework.Assert.IsNotNull(rev1.Id.StartsWith("1-"));
			NUnit.Framework.Assert.AreEqual(1, rev1.GetSequence());
			NUnit.Framework.Assert.IsNull(rev1.GetParentRevisionId());
			NUnit.Framework.Assert.IsNull(rev1.GetParentRevision());
			newRev = rev1.CreateRevision();
			newRevDocument = newRev.GetDocument();
			NUnit.Framework.Assert.AreEqual(doc, newRevDocument);
			NUnit.Framework.Assert.AreEqual(db, newRev.Database);
			NUnit.Framework.Assert.AreEqual(rev1.Id, newRev.GetParentRevisionId());
			NUnit.Framework.Assert.AreEqual(rev1, newRev.GetParentRevision());
			NUnit.Framework.Assert.AreEqual(rev1.Properties, newRev.Properties);
			NUnit.Framework.Assert.AreEqual(rev1.UserProperties, newRev.GetUserProperties
				());
			NUnit.Framework.Assert.IsNotNull(!newRev.IsDeletion());
			// we can't add/modify one property as on ios. need  to add separate method?
			// newRev[@"tag"] = @4567;
			properties.Put("tag", 4567);
			newRev.SetUserProperties(properties);
			SavedRevision rev2 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull("Save 2 failed", rev2);
			NUnit.Framework.Assert.AreEqual(doc.CurrentRevision, rev2);
			NUnit.Framework.Assert.IsNotNull(rev2.Id.StartsWith("2-"));
			NUnit.Framework.Assert.AreEqual(2, rev2.GetSequence());
			NUnit.Framework.Assert.AreEqual(rev1.Id, rev2.GetParentRevisionId());
			NUnit.Framework.Assert.AreEqual(rev1, rev2.GetParentRevision());
			NUnit.Framework.Assert.IsNotNull("Document revision ID is still " + doc.GetCurrentRevisionId
				(), doc.CurrentRevisionId.StartsWith("2-"));
			// Add a deletion/tombstone revision:
			newRev = doc.CreateRevision();
			NUnit.Framework.Assert.AreEqual(rev2.Id, newRev.GetParentRevisionId());
			NUnit.Framework.Assert.AreEqual(rev2, newRev.GetParentRevision());
			newRev.SetIsDeletion(true);
			SavedRevision rev3 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull("Save 3 failed", rev3);
			NUnit.Framework.Assert.AreEqual(doc.CurrentRevision, rev3);
			NUnit.Framework.Assert.IsNotNull("Unexpected revID " + rev3.Id, rev3.Id
				.StartsWith("3-"));
			NUnit.Framework.Assert.AreEqual(3, rev3.GetSequence());
			NUnit.Framework.Assert.IsTrue(rev3.IsDeletion());
			NUnit.Framework.Assert.IsTrue(doc.IsDeleted());
			db.DocumentCount;
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
			properties.Put("testName", "testDeleteDocument");
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			NUnit.Framework.Assert.IsTrue(!doc.IsDeleted());
			NUnit.Framework.Assert.IsTrue(!doc.CurrentRevision.IsDeletion());
			NUnit.Framework.Assert.IsTrue(doc.Delete());
			NUnit.Framework.Assert.IsTrue(doc.IsDeleted());
			NUnit.Framework.Assert.IsNotNull(doc.CurrentRevision.IsDeletion());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPurgeDocument()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testPurgeDocument");
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			NUnit.Framework.Assert.IsNotNull(doc);
			NUnit.Framework.Assert.IsNotNull(doc.Purge());
			Document redoc = db.GetCachedDocument(doc.Id);
			NUnit.Framework.Assert.IsNull(redoc);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestAllDocuments()
		{
			Database db = StartDatabase();
			int kNDocs = 5;
			CreateDocuments(db, kNDocs);
			// clear the cache so all documents/revisions will be re-fetched:
			db.ClearDocumentCache();
			Log.I(Tag, "----- all documents -----");
			Query query = db.CreateAllDocumentsQuery();
			//query.prefetch = YES;
			Log.I(Tag, "Getting all documents: " + query);
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.AreEqual(rows.Count, kNDocs);
			int n = 0;
			for (IEnumerator<QueryRow> it = rows; it.MoveNext(); )
			{
				QueryRow row = it.Current();
				Log.I(Tag, "    --> " + row);
				Document doc = row.GetDocument();
				NUnit.Framework.Assert.IsNotNull("Couldn't get doc from query", doc);
				NUnit.Framework.Assert.IsNotNull("QueryRow should have preloaded revision contents"
					, doc.CurrentRevision.ArePropertiesAvailable());
				Log.I(Tag, "        Properties =" + doc.Properties);
				NUnit.Framework.Assert.IsNotNull("Couldn't get doc properties", doc.GetProperties
					());
				NUnit.Framework.Assert.AreEqual(doc.GetProperty("testName"), "testDatabase");
				n++;
			}
			NUnit.Framework.Assert.AreEqual(n, kNDocs);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestLocalDocs()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("foo", "bar");
			Database db = StartDatabase();
			IDictionary<string, object> props = db.GetExistingLocalDocument("dock");
			NUnit.Framework.Assert.IsNull(props);
			NUnit.Framework.Assert.IsNotNull("Couldn't put new local doc", db.PutLocalDocument
				(properties, "dock"));
			props = db.GetExistingLocalDocument("dock");
			NUnit.Framework.Assert.AreEqual(props.Get("foo"), "bar");
			IDictionary<string, object> newProperties = new Dictionary<string, object>();
			newProperties.Put("FOOO", "BARRR");
			NUnit.Framework.Assert.IsNotNull("Couldn't update local doc", db.PutLocalDocument
				(newProperties, "dock"));
			props = db.GetExistingLocalDocument("dock");
			NUnit.Framework.Assert.IsNull(props.Get("foo"));
			NUnit.Framework.Assert.AreEqual(props.Get("FOOO"), "BARRR");
			NUnit.Framework.Assert.IsNotNull("Couldn't delete local doc", db.DeleteLocalDocument
				("dock"));
			props = db.GetExistingLocalDocument("dock");
			NUnit.Framework.Assert.IsNull(props);
			NUnit.Framework.Assert.IsNotNull("Second delete should have failed", !db.DeleteLocalDocument
				("dock"));
		}

		//TODO issue: deleteLocalDocument should return error.code( see ios)
		// HISTORY
		/// <exception cref="System.Exception"></exception>
		public virtual void TestHistory()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "test06_History");
			properties.Put("tag", 1);
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			string rev1ID = doc.CurrentRevisionId;
			Log.I(Tag, "1st revision: " + rev1ID);
			NUnit.Framework.Assert.IsNotNull("1st revision looks wrong: " + rev1ID, rev1ID.StartsWith
				("1-"));
			NUnit.Framework.Assert.AreEqual(doc.UserProperties, properties);
			properties = new Dictionary<string, object>();
			properties.PutAll(doc.Properties);
			properties.Put("tag", 2);
			NUnit.Framework.Assert.IsNotNull(!properties.Equals(doc.Properties));
			NUnit.Framework.Assert.IsNotNull(doc.PutProperties(properties));
			string rev2ID = doc.CurrentRevisionId;
			Log.I(Tag, "rev2ID" + rev2ID);
			NUnit.Framework.Assert.IsNotNull("2nd revision looks wrong:" + rev2ID, rev2ID.StartsWith
				("2-"));
			IList<SavedRevision> revisions = doc.GetRevisionHistory();
			Log.I(Tag, "Revisions = " + revisions);
			NUnit.Framework.Assert.AreEqual(revisions.Count, 2);
			SavedRevision rev1 = revisions[0];
			NUnit.Framework.Assert.AreEqual(rev1.Id, rev1ID);
			IDictionary<string, object> gotProperties = rev1.Properties;
			NUnit.Framework.Assert.AreEqual(1, gotProperties.Get("tag"));
			SavedRevision rev2 = revisions[1];
			NUnit.Framework.Assert.AreEqual(rev2.Id, rev2ID);
			NUnit.Framework.Assert.AreEqual(rev2, doc.CurrentRevision);
			gotProperties = rev2.Properties;
			NUnit.Framework.Assert.AreEqual(2, gotProperties.Get("tag"));
			IList<SavedRevision> tmp = new AList<SavedRevision>();
			tmp.AddItem(rev2);
			NUnit.Framework.Assert.AreEqual(doc.GetConflictingRevisions(), tmp);
			NUnit.Framework.Assert.AreEqual(doc.GetLeafRevisions(), tmp);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestConflict()
		{
			IDictionary<string, object> prop = new Dictionary<string, object>();
			prop.Put("foo", "bar");
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, prop);
			SavedRevision rev1 = doc.CurrentRevision;
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.PutAll(doc.Properties);
			properties.Put("tag", 2);
			SavedRevision rev2a = doc.PutProperties(properties);
			properties = new Dictionary<string, object>();
			properties.PutAll(rev1.Properties);
			properties.Put("tag", 3);
			UnsavedRevision newRev = rev1.CreateRevision();
			newRev.SetProperties(properties);
			bool allowConflict = true;
			SavedRevision rev2b = newRev.Save(allowConflict);
			NUnit.Framework.Assert.IsNotNull("Failed to create a a conflict", rev2b);
			IList<SavedRevision> confRevs = new AList<SavedRevision>();
			confRevs.AddItem(rev2b);
			confRevs.AddItem(rev2a);
			NUnit.Framework.Assert.AreEqual(doc.GetConflictingRevisions(), confRevs);
			NUnit.Framework.Assert.AreEqual(doc.GetLeafRevisions(), confRevs);
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
			query.SetAllDocsMode(Query.AllDocsMode.ShowConflicts);
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.AreEqual(rows.Count, 1);
			QueryRow row = rows.GetRow(0);
			IList<SavedRevision> revs = row.GetConflictingRevisions();
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
			properties.Put("testName", "testAttachments");
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			SavedRevision rev = doc.CurrentRevision;
			NUnit.Framework.Assert.AreEqual(rev.GetAttachments().Count, 0);
			NUnit.Framework.Assert.AreEqual(rev.GetAttachmentNames().Count, 0);
			NUnit.Framework.Assert.IsNull(rev.GetAttachment("index.html"));
			string content = "This is a test attachment!";
			ByteArrayInputStream body = new ByteArrayInputStream(Sharpen.Runtime.GetBytesForString
				(content));
			UnsavedRevision rev2 = doc.CreateRevision();
			rev2.SetAttachment("index.html", "text/plain; charset=utf-8", body);
			SavedRevision rev3 = rev2.Save();
			NUnit.Framework.Assert.IsNotNull(rev3);
			NUnit.Framework.Assert.AreEqual(rev3.GetAttachments().Count, 1);
			NUnit.Framework.Assert.AreEqual(rev3.GetAttachmentNames().Count, 1);
			Attachment attach = rev3.GetAttachment("index.html");
			NUnit.Framework.Assert.IsNotNull(attach);
			NUnit.Framework.Assert.AreEqual(doc, attach.GetDocument());
			NUnit.Framework.Assert.AreEqual("index.html", attach.Name);
			IList<string> attNames = new AList<string>();
			attNames.AddItem("index.html");
			NUnit.Framework.Assert.AreEqual(rev3.GetAttachmentNames(), attNames);
			NUnit.Framework.Assert.AreEqual("text/plain; charset=utf-8", attach.GetContentType
				());
			NUnit.Framework.Assert.AreEqual(IOUtils.ToString(attach.GetContent(), "UTF-8"), content
				);
			NUnit.Framework.Assert.AreEqual(Sharpen.Runtime.GetBytesForString(content).Length
				, attach.GetLength());
			UnsavedRevision newRev = rev3.CreateRevision();
			newRev.RemoveAttachment(attach.Name);
			SavedRevision rev4 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev4);
			NUnit.Framework.Assert.AreEqual(0, rev4.GetAttachmentNames().Count);
		}

		//CHANGE TRACKING
		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTracking()
		{
			CountDownLatch doneSignal = new CountDownLatch(1);
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
            NUnit.Framework.Assert.AreEqual(db, query.GetDatabase());
            Query query = view.CreateQuery();
			query.StartKey=23;
			query.EndKey=33;
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.IsNotNull(rows);
            NUnit.Framework.Assert.AreEqual(11, rows.Count);
        	int expectedKey = 23;
			for (IEnumerator<QueryRow> it = rows; it.MoveNext(); )
			{
				QueryRow row = it.Current();
				NUnit.Framework.Assert.AreEqual(expectedKey, row.GetKey());
				NUnit.Framework.Assert.AreEqual(expectedKey + 1, row.GetSequenceNumber());
				++expectedKey;
			}
		}

		//API_RunSlowView commented on IOS
		/// <exception cref="System.Exception"></exception>
		public virtual void TestValidation()
		{
			Database db = StartDatabase();
            db.SetValidation("uncool", (Revision newRevision, ValidationContext context)=>
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
			properties.Put("groovy", "right on");
			properties.Put("foo", "bar");
			Document doc = db.CreateDocument();
			NUnit.Framework.Assert.IsNotNull(doc.PutProperties(properties));
			properties = new Dictionary<string, object>();
			properties.Put("foo", "bar");
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
			Document[] docs = new Document[50];
			string lastDocID = string.Empty;
			for (int i = 0; i < kNDocs; i++)
			{
				IDictionary<string, object> properties = new Dictionary<string, object>();
				properties.Put("sequence", i);
				properties.Put("prev", lastDocID);
                Document doc = CreateDocumentWithProperties(db, properties);
				docs[i] = doc;
				lastDocID = doc.Id;
			}
            var query=db.SlowQuery((document, emitter)=>
                {
                            emitter(document.Get("sequence"), new object[] { "_id", document.Get("prev")});
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
				QueryRow row = it.Current();
				NUnit.Framework.Assert.AreEqual(row.GetKey(), rowNumber);
				Document prevDoc = docs[rowNumber];
				NUnit.Framework.Assert.AreEqual(row.GetDocumentId(), prevDoc.Id);
				NUnit.Framework.Assert.AreEqual(row.GetDocument(), prevDoc);
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
			CountDownLatch doneSignal = new CountDownLatch(11);
			// 11 corresponds to startKey=23; endKey=33
			// run a live query
			View view = db.GetView("vu");
            view.SetMap((IDictionary<string, object> document, EmitDelegate emitter)=> 
                {
                    emitter(document.Get("sequence"), 1);
                }, "1");
			LiveQuery query = view.CreateQuery().ToLiveQuery();
			query.StartKey=23;
			query.EndKey=33;
			Log.I(Tag, "Created  " + query);
			// these are the keys that we expect to see in the livequery change listener callback
			ICollection<int> expectedKeys = new HashSet<int>();
			for (int i = 23; i < 34; i++)
			{
				expectedKeys.AddItem(i);
			}
			// install a change listener which decrements countdown latch when it sees a new
			// key from the list of expected keys
            query.Changed  += (sender, e) =>
                {
                    var rows = e.Rows;
                    for (IEnumerator<QueryRow> it = rows; it.MoveNext(); )
                    {
                        QueryRow row = it.Current();
                        if (expectedKeys.Contains(row.Key))
                        {
                            expectedKeys.Remove(row.Key);
                            doneSignal.CountDown();
                        }
                    }
                };
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
				NUnit.Framework.Assert.IsNull(query.GetRows());
				query.Run();
				// this will block until the query completes
				NUnit.Framework.Assert.IsNotNull(query.GetRows());
			}
			// wait for the doneSignal to be finished
			bool success = doneSignal.Await(300, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue("Done signal timed out, live query never ran", success
				);
			// stop the livequery since we are done with it
			query.RemoveChangeListener(changeListener);
			query.Stop();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestAsyncViewQuery()
		{
			CountDownLatch doneSignal = new CountDownLatch(1);
			Database db = StartDatabase();
			View view = db.GetView("vu");
            view.SetMap((IDictionary<string, object> document, EmitDelegate emitter)=> {
                emitter(document.Get("sequence"), null);
            }, "1");
			int kNDocs = 50;
			CreateDocuments(db, kNDocs);
			Query query = view.CreateQuery();
			query.StartKey=23;
			query.EndKey=33;
            query.RunAsync((rows, error) => {
                Log.I (LiteTestCase.Tag, "Async query finished!");
                NUnit.Framework.Assert.IsNotNull (rows);
                NUnit.Framework.Assert.IsNull (error);
                NUnit.Framework.Assert.AreEqual (rows.Count, 11);
                int expectedKey = 23;
                for (IEnumerator<QueryRow> it = rows; it.MoveNext ();) {
                    QueryRow row = it.Current ();
                    NUnit.Framework.Assert.AreEqual (row.GetDocument ().Database, db);
                    NUnit.Framework.Assert.AreEqual (row.GetKey (), expectedKey);
                    ++expectedKey;
                }
                doneSignal.CountDown ();
            });
			Log.I(Tag, "Waiting for async query to finish...");
			bool success = doneSignal.Await(300, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue("Done signal timed out..StartKey=ry never ran", success
				);
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
			Manager mgr = new Manager(new FilePath(GetRootDirectory(), "API_SharedMapBlocks")
				, Manager.DefaultOptions);
			Database db = mgr.GetDatabase("db");
			db.Open();
            db.SetFilter("phil", (r, p) => true);
            db.SetValidation("val", (p1, p2) => true);
			View view = db.GetView("view");
            bool ok = view.SetMapReduce((p1, p2)=>{ return; }, (a, b, c) => { return null; }, true);
			NUnit.Framework.Assert.IsNotNull("Couldn't set map/reduce", ok);
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
			result.Get();
			// blocks until async task has run
			db.Close();
			mgr.Close();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeUUID()
		{
			Manager mgr = new Manager(new FilePath(GetRootDirectory(), "ChangeUUID"), Manager
				.DefaultOptions);
			Database db = mgr.GetDatabase("db");
			db.Open();
			strReduceb = db.PublicUUID();
			string priv = db.PrivateUUID();
			NUnit.Framework.Assert.IsTrue(pub.Length > 10);
			NUnit.Framework.Assert.IsTrue(priv.Length > 10);
			NUnit.Framework.Assert.IsTrue("replaceUUIDs failed", db.ReplaceUUIDs());
			NUnit.Framework.Assert.IsFalse(pub.Equals(db.PublicUUID()));
			NUnit.Framework.Assert.IsFalse(priv.Equals(db.PrivateUUID()));
			mgr.Close();
		}
	}
}
