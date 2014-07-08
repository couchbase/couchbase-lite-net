// 
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
//using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>Created by andrey on 12/3/13.</summary>
	/// <remarks>Created by andrey on 12/3/13.</remarks>
	public class ApiTest : LiteTestCase
	{
		private int changeCount = 0;

		//SERVER & DOCUMENTS
		/// <exception cref="System.Exception"></exception>
		public virtual void TestAPIManager()
		{
			Manager manager = this.manager;
			NUnit.Framework.Assert.IsTrue(manager != null);
			foreach (string dbName in manager.GetAllDatabaseNames())
			{
				Database db = manager.GetDatabase(dbName);
				Log.I(Tag, "Database '" + dbName + "':" + db.GetDocumentCount() + " documents");
			}
			ManagerOptions options = new ManagerOptions();
			options.SetReadOnly(true);
			Manager roManager = new Manager(new LiteTestContext(), options);
			NUnit.Framework.Assert.IsTrue(roManager != null);
			Database db_1 = roManager.GetDatabase("foo");
			NUnit.Framework.Assert.IsNull(db_1);
			IList<string> dbNames = manager.GetAllDatabaseNames();
			NUnit.Framework.Assert.IsFalse(dbNames.Contains("foo"));
			NUnit.Framework.Assert.IsTrue(dbNames.Contains(DefaultTestDb));
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestCreateDocument()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testCreateDocument");
			properties.Put("tag", 1337);
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			string docID = doc.GetId();
			NUnit.Framework.Assert.IsTrue("Invalid doc ID: " + docID, docID.Length > 10);
			string currentRevisionID = doc.GetCurrentRevisionId();
			NUnit.Framework.Assert.IsTrue("Invalid doc revision: " + docID, currentRevisionID
				.Length > 10);
			NUnit.Framework.Assert.AreEqual(doc.GetUserProperties(), properties);
			NUnit.Framework.Assert.AreEqual(db.GetDocument(docID), doc);
			db.ClearDocumentCache();
			// so we can load fresh copies
			Document doc2 = db.GetExistingDocument(docID);
			NUnit.Framework.Assert.AreEqual(doc2.GetId(), docID);
			NUnit.Framework.Assert.AreEqual(doc2.GetCurrentRevisionId(), currentRevisionID);
			NUnit.Framework.Assert.IsNull(db.GetExistingDocument("b0gus"));
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestDeleteDatabase()
		{
			Database deleteme = manager.GetDatabase("deleteme");
			NUnit.Framework.Assert.IsTrue(deleteme.Exists());
			string dbPath = deleteme.GetPath();
			NUnit.Framework.Assert.IsTrue(new FilePath(dbPath).Exists());
			NUnit.Framework.Assert.IsTrue(new FilePath(Sharpen.Runtime.Substring(dbPath, 0, dbPath
				.LastIndexOf('.'))).Exists());
			deleteme.Delete();
			NUnit.Framework.Assert.IsFalse(deleteme.Exists());
			NUnit.Framework.Assert.IsFalse(new FilePath(dbPath).Exists());
			NUnit.Framework.Assert.IsFalse(new FilePath(dbPath + "-journal").Exists());
			NUnit.Framework.Assert.IsFalse(new FilePath(Sharpen.Runtime.Substring(dbPath, 0, 
				dbPath.LastIndexOf('.'))).Exists());
			deleteme.Delete();
			// delete again, even though already deleted
			Database deletemeFetched = manager.GetExistingDatabase("deleteme");
			NUnit.Framework.Assert.IsNull(deletemeFetched);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestDatabaseCompaction()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testDatabaseCompaction");
			properties.Put("tag", 1337);
			Document doc = CreateDocumentWithProperties(database, properties);
			SavedRevision rev1 = doc.GetCurrentRevision();
			IDictionary<string, object> properties2 = new Dictionary<string, object>(properties
				);
			properties2.Put("tag", 4567);
			SavedRevision rev2 = rev1.CreateRevision(properties2);
			database.Compact();
			Document fetchedDoc = database.GetDocument(doc.GetId());
			IList<SavedRevision> revisions = fetchedDoc.GetRevisionHistory();
			foreach (SavedRevision revision in revisions)
			{
				if (revision.GetId().Equals(rev1))
				{
					NUnit.Framework.Assert.IsFalse(revision.ArePropertiesAvailable());
				}
				if (revision.GetId().Equals(rev2))
				{
					NUnit.Framework.Assert.IsTrue(revision.ArePropertiesAvailable());
				}
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestDocumentCache()
		{
			Database db = StartDatabase();
			Document doc = db.CreateDocument();
			UnsavedRevision rev1 = doc.CreateRevision();
			IDictionary<string, object> rev1Properties = new Dictionary<string, object>();
			rev1Properties.Put("foo", "bar");
			rev1.SetUserProperties(rev1Properties);
			SavedRevision savedRev1 = rev1.Save();
			string documentId = savedRev1.GetDocument().GetId();
			// getting the document puts it in cache
			Document docRev1 = db.GetExistingDocument(documentId);
			UnsavedRevision rev2 = docRev1.CreateRevision();
			IDictionary<string, object> rev2Properties = rev2.GetProperties();
			rev2Properties.Put("foo", "baz");
			rev2.SetUserProperties(rev2Properties);
			rev2.Save();
			Document docRev2 = db.GetExistingDocument(documentId);
			NUnit.Framework.Assert.AreEqual("baz", docRev2.GetProperty("foo"));
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateRevisions()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testCreateRevisions");
			properties.Put("tag", 1337);
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			NUnit.Framework.Assert.IsFalse(doc.IsDeleted());
			SavedRevision rev1 = doc.GetCurrentRevision();
			NUnit.Framework.Assert.IsTrue(rev1.GetId().StartsWith("1-"));
			NUnit.Framework.Assert.AreEqual(1, rev1.GetSequence());
			NUnit.Framework.Assert.AreEqual(0, rev1.GetAttachments().Count);
			// Test -createRevisionWithProperties:
			IDictionary<string, object> properties2 = new Dictionary<string, object>(properties
				);
			properties2.Put("tag", 4567);
			SavedRevision rev2 = rev1.CreateRevision(properties2);
			NUnit.Framework.Assert.IsNotNull("Put failed", rev2);
			NUnit.Framework.Assert.IsTrue("Document revision ID is still " + doc.GetCurrentRevisionId
				(), doc.GetCurrentRevisionId().StartsWith("2-"));
			NUnit.Framework.Assert.AreEqual(rev2.GetId(), doc.GetCurrentRevisionId());
			NUnit.Framework.Assert.IsNotNull(rev2.ArePropertiesAvailable());
			NUnit.Framework.Assert.AreEqual(rev2.GetUserProperties(), properties2);
			NUnit.Framework.Assert.AreEqual(rev2.GetDocument(), doc);
			NUnit.Framework.Assert.AreEqual(rev2.GetProperty("_id"), doc.GetId());
			NUnit.Framework.Assert.AreEqual(rev2.GetProperty("_rev"), rev2.GetId());
			// Test -createRevision:
			UnsavedRevision newRev = rev2.CreateRevision();
			NUnit.Framework.Assert.IsNull(newRev.GetId());
			NUnit.Framework.Assert.AreEqual(newRev.GetParent(), rev2);
			NUnit.Framework.Assert.AreEqual(newRev.GetParentId(), rev2.GetId());
			NUnit.Framework.Assert.AreEqual(doc.GetCurrentRevision(), rev2);
			NUnit.Framework.Assert.IsFalse(doc.IsDeleted());
			IList<SavedRevision> listRevs = new AList<SavedRevision>();
			listRevs.AddItem(rev1);
			listRevs.AddItem(rev2);
			NUnit.Framework.Assert.AreEqual(newRev.GetRevisionHistory(), listRevs);
			NUnit.Framework.Assert.AreEqual(newRev.GetProperties(), rev2.GetProperties());
			NUnit.Framework.Assert.AreEqual(newRev.GetUserProperties(), rev2.GetUserProperties
				());
			IDictionary<string, object> userProperties = new Dictionary<string, object>();
			userProperties.Put("because", "NoSQL");
			newRev.SetUserProperties(userProperties);
			NUnit.Framework.Assert.AreEqual(newRev.GetUserProperties(), userProperties);
			IDictionary<string, object> expectProperties = new Dictionary<string, object>();
			expectProperties.Put("because", "NoSQL");
			expectProperties.Put("_id", doc.GetId());
			expectProperties.Put("_rev", rev2.GetId());
			NUnit.Framework.Assert.AreEqual(newRev.GetProperties(), expectProperties);
			SavedRevision rev3 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev3);
			NUnit.Framework.Assert.AreEqual(rev3.GetUserProperties(), newRev.GetUserProperties
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
			NUnit.Framework.Assert.AreEqual(db, newRev.GetDatabase());
			NUnit.Framework.Assert.IsNull(newRev.GetParentId());
			NUnit.Framework.Assert.IsNull(newRev.GetParent());
			IDictionary<string, object> expectProperties = new Dictionary<string, object>();
			expectProperties.Put("_id", doc.GetId());
			NUnit.Framework.Assert.AreEqual(expectProperties, newRev.GetProperties());
			NUnit.Framework.Assert.IsTrue(!newRev.IsDeletion());
			NUnit.Framework.Assert.AreEqual(newRev.GetSequence(), 0);
			//ios support another approach to set properties::
			//newRev.([@"testName"] = @"testCreateRevisions";
			//newRev[@"tag"] = @1337;
			newRev.SetUserProperties(properties);
			NUnit.Framework.Assert.AreEqual(newRev.GetUserProperties(), properties);
			SavedRevision rev1 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull("Save 1 failed", rev1);
			NUnit.Framework.Assert.AreEqual(doc.GetCurrentRevision(), rev1);
			NUnit.Framework.Assert.IsNotNull(rev1.GetId().StartsWith("1-"));
			NUnit.Framework.Assert.AreEqual(1, rev1.GetSequence());
			NUnit.Framework.Assert.IsNull(rev1.GetParentId());
			NUnit.Framework.Assert.IsNull(rev1.GetParent());
			newRev = rev1.CreateRevision();
			newRevDocument = newRev.GetDocument();
			NUnit.Framework.Assert.AreEqual(doc, newRevDocument);
			NUnit.Framework.Assert.AreEqual(db, newRev.GetDatabase());
			NUnit.Framework.Assert.AreEqual(rev1.GetId(), newRev.GetParentId());
			NUnit.Framework.Assert.AreEqual(rev1, newRev.GetParent());
			NUnit.Framework.Assert.AreEqual(rev1.GetProperties(), newRev.GetProperties());
			NUnit.Framework.Assert.AreEqual(rev1.GetUserProperties(), newRev.GetUserProperties
				());
			NUnit.Framework.Assert.IsNotNull(!newRev.IsDeletion());
			// we can't add/modify one property as on ios. need  to add separate method?
			// newRev[@"tag"] = @4567;
			properties.Put("tag", 4567);
			newRev.SetUserProperties(properties);
			SavedRevision rev2 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull("Save 2 failed", rev2);
			NUnit.Framework.Assert.AreEqual(doc.GetCurrentRevision(), rev2);
			NUnit.Framework.Assert.IsNotNull(rev2.GetId().StartsWith("2-"));
			NUnit.Framework.Assert.AreEqual(2, rev2.GetSequence());
			NUnit.Framework.Assert.AreEqual(rev1.GetId(), rev2.GetParentId());
			NUnit.Framework.Assert.AreEqual(rev1, rev2.GetParent());
			NUnit.Framework.Assert.IsNotNull("Document revision ID is still " + doc.GetCurrentRevisionId
				(), doc.GetCurrentRevisionId().StartsWith("2-"));
			// Add a deletion/tombstone revision:
			newRev = doc.CreateRevision();
			NUnit.Framework.Assert.AreEqual(rev2.GetId(), newRev.GetParentId());
			NUnit.Framework.Assert.AreEqual(rev2, newRev.GetParent());
			newRev.SetIsDeletion(true);
			SavedRevision rev3 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull("Save 3 failed", rev3);
			NUnit.Framework.Assert.IsNotNull("Unexpected revID " + rev3.GetId(), rev3.GetId()
				.StartsWith("3-"));
			NUnit.Framework.Assert.AreEqual(3, rev3.GetSequence());
			NUnit.Framework.Assert.IsTrue(rev3.IsDeletion());
			NUnit.Framework.Assert.IsTrue(doc.IsDeleted());
			NUnit.Framework.Assert.IsNull(doc.GetCurrentRevision());
			IList<SavedRevision> leafRevs = new AList<SavedRevision>();
			leafRevs.AddItem(rev3);
			NUnit.Framework.Assert.AreEqual(doc.GetLeafRevisions(), leafRevs);
			db.GetDocumentCount();
			Document doc2 = db.GetDocument(doc.GetId());
			NUnit.Framework.Assert.AreEqual(doc, doc2);
			NUnit.Framework.Assert.IsNull(db.GetExistingDocument(doc.GetId()));
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
			NUnit.Framework.Assert.IsTrue(!doc.GetCurrentRevision().IsDeletion());
			NUnit.Framework.Assert.IsTrue(doc.Delete());
			NUnit.Framework.Assert.IsTrue(doc.IsDeleted());
			NUnit.Framework.Assert.IsNull(doc.GetCurrentRevision());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPurgeDocument()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testPurgeDocument");
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			NUnit.Framework.Assert.IsNotNull(doc);
			doc.Purge();
			Document redoc = db.GetCachedDocument(doc.GetId());
			NUnit.Framework.Assert.IsNull(redoc);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestDeleteDocumentViaTombstoneRevision()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("testName", "testDeleteDocument");
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, properties);
			NUnit.Framework.Assert.IsTrue(!doc.IsDeleted());
			NUnit.Framework.Assert.IsTrue(!doc.GetCurrentRevision().IsDeletion());
			IDictionary<string, object> props = new Dictionary<string, object>(doc.GetProperties
				());
			props.Put("_deleted", true);
			SavedRevision deletedRevision = doc.PutProperties(props);
			NUnit.Framework.Assert.IsTrue(doc.IsDeleted());
			NUnit.Framework.Assert.IsTrue(deletedRevision.IsDeletion());
			NUnit.Framework.Assert.IsNull(doc.GetCurrentRevision());
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
			NUnit.Framework.Assert.AreEqual(rows.GetCount(), kNDocs);
			int n = 0;
			for (IEnumerator<QueryRow> it = rows; it.HasNext(); )
			{
				QueryRow row = it.Next();
				Log.I(Tag, "    --> " + row);
				Document doc = row.GetDocument();
				NUnit.Framework.Assert.IsNotNull("Couldn't get doc from query", doc);
				NUnit.Framework.Assert.IsNotNull("QueryRow should have preloaded revision contents"
					, doc.GetCurrentRevision().ArePropertiesAvailable());
				Log.I(Tag, "        Properties =" + doc.GetProperties());
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
				("dock", properties));
			props = db.GetExistingLocalDocument("dock");
			NUnit.Framework.Assert.AreEqual(props.Get("foo"), "bar");
			IDictionary<string, object> newProperties = new Dictionary<string, object>();
			newProperties.Put("FOOO", "BARRR");
			NUnit.Framework.Assert.IsNotNull("Couldn't update local doc", db.PutLocalDocument
				("dock", newProperties));
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
			string rev1ID = doc.GetCurrentRevisionId();
			Log.I(Tag, "1st revision: " + rev1ID);
			NUnit.Framework.Assert.IsNotNull("1st revision looks wrong: " + rev1ID, rev1ID.StartsWith
				("1-"));
			NUnit.Framework.Assert.AreEqual(doc.GetUserProperties(), properties);
			properties = new Dictionary<string, object>();
			properties.PutAll(doc.GetProperties());
			properties.Put("tag", 2);
			NUnit.Framework.Assert.IsNotNull(!properties.Equals(doc.GetProperties()));
			NUnit.Framework.Assert.IsNotNull(doc.PutProperties(properties));
			string rev2ID = doc.GetCurrentRevisionId();
			Log.I(Tag, "rev2ID" + rev2ID);
			NUnit.Framework.Assert.IsNotNull("2nd revision looks wrong:" + rev2ID, rev2ID.StartsWith
				("2-"));
			IList<SavedRevision> revisions = doc.GetRevisionHistory();
			Log.I(Tag, "Revisions = " + revisions);
			NUnit.Framework.Assert.AreEqual(revisions.Count, 2);
			SavedRevision rev1 = revisions[0];
			NUnit.Framework.Assert.AreEqual(rev1.GetId(), rev1ID);
			IDictionary<string, object> gotProperties = rev1.GetProperties();
			NUnit.Framework.Assert.AreEqual(1, gotProperties.Get("tag"));
			SavedRevision rev2 = revisions[1];
			NUnit.Framework.Assert.AreEqual(rev2.GetId(), rev2ID);
			NUnit.Framework.Assert.AreEqual(rev2, doc.GetCurrentRevision());
			gotProperties = rev2.GetProperties();
			NUnit.Framework.Assert.AreEqual(2, gotProperties.Get("tag"));
			IList<SavedRevision> tmp = new AList<SavedRevision>();
			tmp.AddItem(rev2);
			NUnit.Framework.Assert.AreEqual(doc.GetConflictingRevisions(), tmp);
			NUnit.Framework.Assert.AreEqual(doc.GetLeafRevisions(), tmp);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestHistoryAfterDocDeletion()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			string docId = "testHistoryAfterDocDeletion";
			properties.Put("tag", 1);
			Database db = StartDatabase();
			Document doc = db.GetDocument(docId);
			NUnit.Framework.Assert.AreEqual(docId, doc.GetId());
			doc.PutProperties(properties);
			string revID = doc.GetCurrentRevisionId();
			for (int i = 2; i < 6; i++)
			{
				properties.Put("tag", i);
				properties.Put("_rev", revID);
				doc.PutProperties(properties);
				revID = doc.GetCurrentRevisionId();
				Log.I(Tag, i + " revision: " + revID);
				NUnit.Framework.Assert.IsTrue("revision is not correct:" + revID + ", should be with prefix "
					 + i + "-", revID.StartsWith(i.ToString() + "-"));
				NUnit.Framework.Assert.AreEqual("Doc Id is not correct ", docId, doc.GetId());
			}
			// now delete the doc and clear it from the cache so we
			// make sure we are reading a fresh copy
			doc.Delete();
			database.ClearDocumentCache();
			// get doc from db with same ID as before, and the current rev should be null since the
			// last update was a deletion
			Document docPostDelete = db.GetDocument(docId);
			NUnit.Framework.Assert.IsNull(docPostDelete.GetCurrentRevision());
			// save a new revision
			properties = new Dictionary<string, object>();
			properties.Put("tag", 6);
			UnsavedRevision newRevision = docPostDelete.CreateRevision();
			newRevision.SetProperties(properties);
			SavedRevision newSavedRevision = newRevision.Save();
			// make sure the current revision of doc matches the rev we just saved
			NUnit.Framework.Assert.AreEqual(newSavedRevision, docPostDelete.GetCurrentRevision
				());
			// make sure the rev id is 7-
			NUnit.Framework.Assert.IsTrue(docPostDelete.GetCurrentRevisionId().StartsWith("7-"
				));
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestConflict()
		{
			IDictionary<string, object> prop = new Dictionary<string, object>();
			prop.Put("foo", "bar");
			Database db = StartDatabase();
			Document doc = CreateDocumentWithProperties(db, prop);
			SavedRevision rev1 = doc.GetCurrentRevision();
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.PutAll(doc.GetProperties());
			properties.Put("tag", 2);
			SavedRevision rev2a = doc.PutProperties(properties);
			properties = new Dictionary<string, object>();
			properties.PutAll(rev1.GetProperties());
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
			if (Sharpen.Runtime.CompareOrdinal(rev2a.GetId(), rev2b.GetId()) > 0)
			{
				defaultRev = rev2a;
				otherRev = rev2b;
			}
			else
			{
				defaultRev = rev2b;
				otherRev = rev2a;
			}
			NUnit.Framework.Assert.AreEqual(doc.GetCurrentRevision(), defaultRev);
			Query query = db.CreateAllDocumentsQuery();
			query.SetAllDocsMode(Query.AllDocsMode.ShowConflicts);
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.AreEqual(rows.GetCount(), 1);
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
			string attachmentName = "index.html";
			string content = "This is a test attachment!";
			Document doc = CreateDocWithAttachment(database, attachmentName, content);
			UnsavedRevision newRev = doc.GetCurrentRevision().CreateRevision();
			newRev.RemoveAttachment(attachmentName);
			SavedRevision rev4 = newRev.Save();
			NUnit.Framework.Assert.IsNotNull(rev4);
			NUnit.Framework.Assert.AreEqual(0, rev4.GetAttachmentNames().Count);
		}

		/// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/132</summary>
		/// <exception cref="System.Exception"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestUpdateDocWithAttachments()
		{
			string attachmentName = "index.html";
			string content = "This is a test attachment!";
			Document doc = CreateDocWithAttachment(database, attachmentName, content);
			SavedRevision latestRevision = doc.GetCurrentRevision();
			IDictionary<string, object> propertiesUpdated = new Dictionary<string, object>();
			propertiesUpdated.Put("propertiesUpdated", "testUpdateDocWithAttachments");
			UnsavedRevision newUnsavedRevision = latestRevision.CreateRevision();
			newUnsavedRevision.SetUserProperties(propertiesUpdated);
			SavedRevision newSavedRevision = newUnsavedRevision.Save();
			NUnit.Framework.Assert.IsNotNull(newSavedRevision);
			NUnit.Framework.Assert.AreEqual(1, newSavedRevision.GetAttachmentNames().Count);
			Attachment fetched = doc.GetCurrentRevision().GetAttachment(attachmentName);
			InputStream @is = fetched.GetContent();
			byte[] attachmentBytes = TextUtils.Read(@is);
			NUnit.Framework.Assert.AreEqual(content, Sharpen.Runtime.GetStringForBytes(attachmentBytes
				));
			NUnit.Framework.Assert.IsNotNull(fetched);
		}

		//CHANGE TRACKING
		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTracking()
		{
			CountDownLatch doneSignal = new CountDownLatch(1);
			Database db = StartDatabase();
			db.AddChangeListener(new _ChangeListener_607(doneSignal));
			CreateDocumentsAsync(db, 5);
			// We expect that the changes reported by the server won't be notified, because those revisions
			// are already cached in memory.
			bool success = doneSignal.Await(300, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(success);
			NUnit.Framework.Assert.AreEqual(5, db.GetLastSequenceNumber());
		}

		private sealed class _ChangeListener_607 : Database.ChangeListener
		{
			public _ChangeListener_607(CountDownLatch doneSignal)
			{
				this.doneSignal = doneSignal;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				doneSignal.CountDown();
			}

			private readonly CountDownLatch doneSignal;
		}

		//VIEWS
		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateView()
		{
			Database db = StartDatabase();
			View view = db.GetView("vu");
			NUnit.Framework.Assert.IsNotNull(view);
			NUnit.Framework.Assert.AreEqual(db, view.GetDatabase());
			NUnit.Framework.Assert.AreEqual("vu", view.GetName());
			NUnit.Framework.Assert.IsNull(view.GetMap());
			NUnit.Framework.Assert.IsNull(view.GetReduce());
			view.SetMap(new _Mapper_635(), "1");
			NUnit.Framework.Assert.IsNotNull(view.GetMap() != null);
			int kNDocs = 50;
			CreateDocuments(db, kNDocs);
			Query query = view.CreateQuery();
			NUnit.Framework.Assert.AreEqual(db, query.GetDatabase());
			query.SetStartKey(23);
			query.SetEndKey(33);
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.IsNotNull(rows);
			NUnit.Framework.Assert.AreEqual(11, rows.GetCount());
			int expectedKey = 23;
			for (IEnumerator<QueryRow> it = rows; it.HasNext(); )
			{
				QueryRow row = it.Next();
				NUnit.Framework.Assert.AreEqual(expectedKey, row.GetKey());
				NUnit.Framework.Assert.AreEqual(expectedKey + 1, row.GetSequenceNumber());
				++expectedKey;
			}
		}

		private sealed class _Mapper_635 : Mapper
		{
			public _Mapper_635()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("sequence"), null);
			}
		}

		//API_RunSlowView commented on IOS
		/// <exception cref="System.Exception"></exception>
		public virtual void TestValidation()
		{
			Database db = StartDatabase();
			db.SetValidation("uncool", new _Validator_671());
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
				NUnit.Framework.Assert.AreEqual(e.GetCBLStatus().GetCode(), Status.Forbidden);
			}
		}

		private sealed class _Validator_671 : Validator
		{
			public _Validator_671()
			{
			}

			public void Validate(Revision newRevision, ValidationContext context)
			{
				{
					if (newRevision.GetProperty("groovy") == null)
					{
						context.Reject("uncool");
					}
				}
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
				lastDocID = doc.GetId();
			}
			Query query = db.SlowQuery(new _Mapper_719());
			query.SetStartKey(23);
			query.SetEndKey(33);
			query.SetPrefetch(true);
			QueryEnumerator rows = query.Run();
			NUnit.Framework.Assert.IsNotNull(rows);
			NUnit.Framework.Assert.AreEqual(rows.GetCount(), 11);
			int rowNumber = 23;
			for (IEnumerator<QueryRow> it = rows; it.HasNext(); )
			{
				QueryRow row = it.Next();
				NUnit.Framework.Assert.AreEqual(row.GetKey(), rowNumber);
				Document prevDoc = docs[rowNumber];
				NUnit.Framework.Assert.AreEqual(row.GetDocumentId(), prevDoc.GetId());
				NUnit.Framework.Assert.AreEqual(row.GetDocument(), prevDoc);
				++rowNumber;
			}
		}

		private sealed class _Mapper_719 : Mapper
		{
			public _Mapper_719()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("sequence"), new object[] { "_id", document.Get("prev")
					 });
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
		public virtual void TestLiveQueryStartWaitForRows()
		{
			RunLiveQuery("startWaitForRows");
		}

		/// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/84</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestLiveQueryStop()
		{
			int kNDocs = 100;
			CountDownLatch doneSignal = new CountDownLatch(1);
			Database db = StartDatabase();
			// run a live query
			View view = db.GetView("vu");
			view.SetMap(new _Mapper_774(), "1");
			LiveQuery query = view.CreateQuery().ToLiveQuery();
			AtomicInteger atomicInteger = new AtomicInteger(0);
			// install a change listener which decrements countdown latch when it sees a new
			// key from the list of expected keys
			LiveQuery.ChangeListener changeListener = new _ChangeListener_786(atomicInteger, 
				kNDocs, doneSignal);
			query.AddChangeListener(changeListener);
			// create the docs that will cause the above change listener to decrement countdown latch
			Log.D(Database.Tag, "testLiveQueryStop: createDocumentsAsync()");
			CreateDocumentsAsync(db, kNDocs);
			Log.D(Database.Tag, "testLiveQueryStop: calling query.start()");
			query.Start();
			// wait until the livequery is called back with kNDocs docs
			Log.D(Database.Tag, "testLiveQueryStop: waiting for doneSignal");
			bool success = doneSignal.Await(45, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(success);
			Log.D(Database.Tag, "testLiveQueryStop: waiting for query.stop()");
			query.Stop();
			// after stopping the query, we should not get any more livequery callbacks, even
			// if we add more docs to the database and pause (to give time for potential callbacks)
			int numTimesCallbackCalled = atomicInteger.Get();
			Log.D(Database.Tag, "testLiveQueryStop: numTimesCallbackCalled is: " + numTimesCallbackCalled
				 + ".  Now adding docs");
			for (int i = 0; i < 10; i++)
			{
				CreateDocuments(db, 1);
				Log.D(Database.Tag, "testLiveQueryStop: add a document.  atomicInteger.get(): " +
					 atomicInteger.Get());
				NUnit.Framework.Assert.AreEqual(numTimesCallbackCalled, atomicInteger.Get());
				Sharpen.Thread.Sleep(200);
			}
			NUnit.Framework.Assert.AreEqual(numTimesCallbackCalled, atomicInteger.Get());
		}

		private sealed class _Mapper_774 : Mapper
		{
			public _Mapper_774()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("sequence"), null);
			}
		}

		private sealed class _ChangeListener_786 : LiveQuery.ChangeListener
		{
			public _ChangeListener_786(AtomicInteger atomicInteger, int kNDocs, CountDownLatch
				 doneSignal)
			{
				this.atomicInteger = atomicInteger;
				this.kNDocs = kNDocs;
				this.doneSignal = doneSignal;
			}

			public void Changed(LiveQuery.ChangeEvent @event)
			{
				Log.D(LiteTestCase.Tag, "changed called, atomicInteger.incrementAndGet");
				atomicInteger.IncrementAndGet();
				NUnit.Framework.Assert.IsNull(@event.GetError());
				if (@event.GetRows().GetCount() == kNDocs)
				{
					doneSignal.CountDown();
				}
			}

			private readonly AtomicInteger atomicInteger;

			private readonly int kNDocs;

			private readonly CountDownLatch doneSignal;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestLiveQueryRestart()
		{
		}

		// kick something off that will s
		/// <exception cref="System.Exception"></exception>
		public virtual void RunLiveQuery(string methodNameToCall)
		{
			Database db = StartDatabase();
			CountDownLatch doneSignal = new CountDownLatch(11);
			// 11 corresponds to startKey=23; endKey=33
			// run a live query
			View view = db.GetView("vu");
			view.SetMap(new _Mapper_845(), "1");
			LiveQuery query = view.CreateQuery().ToLiveQuery();
			query.SetStartKey(23);
			query.SetEndKey(33);
			Log.I(Tag, "Created  " + query);
			// these are the keys that we expect to see in the livequery change listener callback
			ICollection<int> expectedKeys = new HashSet<int>();
			for (int i = 23; i < 34; i++)
			{
				expectedKeys.AddItem(i);
			}
			// install a change listener which decrements countdown latch when it sees a new
			// key from the list of expected keys
			LiveQuery.ChangeListener changeListener = new _ChangeListener_864(expectedKeys, doneSignal
				);
			query.AddChangeListener(changeListener);
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
				if (methodNameToCall.Equals("startWaitForRows"))
				{
					query.Start();
					query.WaitForRows();
				}
				else
				{
					NUnit.Framework.Assert.IsNull(query.GetRows());
					query.Run();
					// this will block until the query completes
					NUnit.Framework.Assert.IsNotNull(query.GetRows());
				}
			}
			// wait for the doneSignal to be finished
			bool success = doneSignal.Await(300, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue("Done signal timed out, live query never ran", success
				);
			// stop the livequery since we are done with it
			query.RemoveChangeListener(changeListener);
			query.Stop();
		}

		private sealed class _Mapper_845 : Mapper
		{
			public _Mapper_845()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("sequence"), null);
			}
		}

		private sealed class _ChangeListener_864 : LiveQuery.ChangeListener
		{
			public _ChangeListener_864(ICollection<int> expectedKeys, CountDownLatch doneSignal
				)
			{
				this.expectedKeys = expectedKeys;
				this.doneSignal = doneSignal;
			}

			public void Changed(LiveQuery.ChangeEvent @event)
			{
				QueryEnumerator rows = @event.GetRows();
				for (IEnumerator<QueryRow> it = rows; it.HasNext(); )
				{
					QueryRow row = it.Next();
					if (expectedKeys.Contains(row.GetKey()))
					{
						expectedKeys.Remove(row.GetKey());
						doneSignal.CountDown();
					}
				}
			}

			private readonly ICollection<int> expectedKeys;

			private readonly CountDownLatch doneSignal;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestAsyncViewQuery()
		{
			CountDownLatch doneSignal = new CountDownLatch(1);
			Database db = StartDatabase();
			View view = db.GetView("vu");
			view.SetMap(new _Mapper_911(), "1");
			int kNDocs = 50;
			CreateDocuments(db, kNDocs);
			Query query = view.CreateQuery();
			query.SetStartKey(23);
			query.SetEndKey(33);
			query.RunAsync(new _QueryCompleteListener_925(db, doneSignal));
			Log.I(Tag, "Waiting for async query to finish...");
			bool success = doneSignal.Await(300, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue("Done signal timed out, async query never ran", success
				);
		}

		private sealed class _Mapper_911 : Mapper
		{
			public _Mapper_911()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("sequence"), null);
			}
		}

		private sealed class _QueryCompleteListener_925 : Query.QueryCompleteListener
		{
			public _QueryCompleteListener_925(Database db, CountDownLatch doneSignal)
			{
				this.db = db;
				this.doneSignal = doneSignal;
			}

			public void Completed(QueryEnumerator rows, Exception error)
			{
				Log.I(LiteTestCase.Tag, "Async query finished!");
				NUnit.Framework.Assert.IsNotNull(rows);
				NUnit.Framework.Assert.IsNull(error);
				NUnit.Framework.Assert.AreEqual(rows.GetCount(), 11);
				int expectedKey = 23;
				for (IEnumerator<QueryRow> it = rows; it.HasNext(); )
				{
					QueryRow row = it.Next();
					NUnit.Framework.Assert.AreEqual(row.GetDocument().GetDatabase(), db);
					NUnit.Framework.Assert.AreEqual(row.GetKey(), expectedKey);
					++expectedKey;
				}
				doneSignal.CountDown();
			}

			private readonly Database db;

			private readonly CountDownLatch doneSignal;
		}

		/// <summary>
		/// Make sure that a database's map/reduce functions are shared with the shadow database instance
		/// running in the background server.
		/// </summary>
		/// <remarks>
		/// Make sure that a database's map/reduce functions are shared with the shadow database instance
		/// running in the background server.
		/// </remarks>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestSharedMapBlocks()
		{
			Manager mgr = new Manager(new LiteTestContext("API_SharedMapBlocks"), Manager.DefaultOptions
				);
			Database db = mgr.GetDatabase("db");
			db.Open();
			db.SetFilter("phil", new _ReplicationFilter_959());
			db.SetValidation("val", new _Validator_966());
			View view = db.GetView("view");
			bool ok = view.SetMapReduce(new _Mapper_973(), new _Reducer_978(), "1");
			NUnit.Framework.Assert.IsNotNull("Couldn't set map/reduce", ok);
			Mapper map = view.GetMap();
			Reducer reduce = view.GetReduce();
			ReplicationFilter filter = db.GetFilter("phil");
			Validator validation = db.GetValidation("val");
			Future result = mgr.RunAsync("db", new _AsyncTask_993(filter, validation, map, reduce
				));
			result.Get();
			// blocks until async task has run
			db.Close();
			mgr.Close();
		}

		private sealed class _ReplicationFilter_959 : ReplicationFilter
		{
			public _ReplicationFilter_959()
			{
			}

			public bool Filter(SavedRevision revision, IDictionary<string, object> @params)
			{
				return true;
			}
		}

		private sealed class _Validator_966 : Validator
		{
			public _Validator_966()
			{
			}

			public void Validate(Revision newRevision, ValidationContext context)
			{
			}
		}

		private sealed class _Mapper_973 : Mapper
		{
			public _Mapper_973()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
			}
		}

		private sealed class _Reducer_978 : Reducer
		{
			public _Reducer_978()
			{
			}

			public object Reduce(IList<object> keys, IList<object> values, bool rereduce)
			{
				return null;
			}
		}

		private sealed class _AsyncTask_993 : AsyncTask
		{
			public _AsyncTask_993(ReplicationFilter filter, Validator validation, Mapper map, 
				Reducer reduce)
			{
				this.filter = filter;
				this.validation = validation;
				this.map = map;
				this.reduce = reduce;
			}

			public void Run(Database database)
			{
				NUnit.Framework.Assert.IsNotNull(database);
				View serverView = database.GetExistingView("view");
				NUnit.Framework.Assert.IsNotNull(serverView);
				NUnit.Framework.Assert.AreEqual(database.GetFilter("phil"), filter);
				NUnit.Framework.Assert.AreEqual(database.GetValidation("val"), validation);
				NUnit.Framework.Assert.AreEqual(serverView.GetMap(), map);
				NUnit.Framework.Assert.AreEqual(serverView.GetReduce(), reduce);
			}

			private readonly ReplicationFilter filter;

			private readonly Validator validation;

			private readonly Mapper map;

			private readonly Reducer reduce;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeUUID()
		{
			Manager mgr = new Manager(new LiteTestContext("ChangeUUID"), Manager.DefaultOptions
				);
			Database db = mgr.GetDatabase("db");
			db.Open();
			string pub = db.PublicUUID();
			string priv = db.PrivateUUID();
			NUnit.Framework.Assert.IsTrue(pub.Length > 10);
			NUnit.Framework.Assert.IsTrue(priv.Length > 10);
			NUnit.Framework.Assert.IsTrue("replaceUUIDs failed", db.ReplaceUUIDs());
			NUnit.Framework.Assert.IsFalse(pub.Equals(db.PublicUUID()));
			NUnit.Framework.Assert.IsFalse(priv.Equals(db.PrivateUUID()));
			mgr.Close();
		}

		/// <summary>https://github.com/couchbase/couchbase-lite-android/issues/220</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestMultiDocumentUpdate()
		{
			int numberOfDocuments = 10;
			int numberOfUpdates = 10;
			Document[] docs = new Document[numberOfDocuments];
			for (int j = 0; j < numberOfDocuments; j++)
			{
				IDictionary<string, object> prop = new Dictionary<string, object>();
				prop.Put("foo", "bar");
				prop.Put("toogle", true);
				Document document = CreateDocumentWithProperties(database, prop);
				docs[j] = document;
			}
			AtomicInteger numDocsUpdated = new AtomicInteger(0);
			AtomicInteger numExceptions = new AtomicInteger(0);
			for (int j_1 = 0; j_1 < numberOfDocuments; j_1++)
			{
				Document doc = docs[j_1];
				for (int k = 0; k < numberOfUpdates; k++)
				{
					IDictionary<string, object> contents = new Hashtable(doc.GetProperties());
					bool wasChecked = (bool)contents.Get("toogle");
					//toggle value of check property
					contents.Put("toogle", !wasChecked);
					try
					{
						doc.PutProperties(contents);
						numDocsUpdated.IncrementAndGet();
					}
					catch (CouchbaseLiteException cblex)
					{
						Log.E(Tag, "Document update failed", cblex);
						numExceptions.IncrementAndGet();
					}
				}
			}
			NUnit.Framework.Assert.AreEqual(numberOfDocuments * numberOfUpdates, numDocsUpdated
				.Get());
			NUnit.Framework.Assert.AreEqual(0, numExceptions.Get());
		}

		/// <summary>https://github.com/couchbase/couchbase-lite-android/issues/220</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void FailingTestMultiDocumentUpdateInTransaction()
		{
			int numberOfDocuments = 10;
			int numberOfUpdates = 10;
			Document[] docs = new Document[numberOfDocuments];
			database.RunInTransaction(new _TransactionalTask_1086(this, numberOfDocuments, docs
				));
			AtomicInteger numDocsUpdated = new AtomicInteger(0);
			AtomicInteger numExceptions = new AtomicInteger(0);
			database.RunInTransaction(new _TransactionalTask_1104(this, numberOfDocuments, docs
				, numberOfUpdates, numDocsUpdated, numExceptions));
			//toggle value of check property
			NUnit.Framework.Assert.AreEqual(numberOfDocuments * numberOfUpdates, numDocsUpdated
				.Get());
			NUnit.Framework.Assert.AreEqual(0, numExceptions.Get());
		}

		private sealed class _TransactionalTask_1086 : TransactionalTask
		{
			public _TransactionalTask_1086(ApiTest _enclosing, int numberOfDocuments, Document
				[] docs)
			{
				this._enclosing = _enclosing;
				this.numberOfDocuments = numberOfDocuments;
				this.docs = docs;
			}

			public bool Run()
			{
				for (int j = 0; j < numberOfDocuments; j++)
				{
					IDictionary<string, object> prop = new Dictionary<string, object>();
					prop.Put("foo", "bar");
					prop.Put("toogle", true);
					Document document = LiteTestCase.CreateDocumentWithProperties(this._enclosing.database
						, prop);
					docs[j] = document;
				}
				return true;
			}

			private readonly ApiTest _enclosing;

			private readonly int numberOfDocuments;

			private readonly Document[] docs;
		}

		private sealed class _TransactionalTask_1104 : TransactionalTask
		{
			public _TransactionalTask_1104(ApiTest _enclosing, int numberOfDocuments, Document
				[] docs, int numberOfUpdates, AtomicInteger numDocsUpdated, AtomicInteger numExceptions
				)
			{
				this._enclosing = _enclosing;
				this.numberOfDocuments = numberOfDocuments;
				this.docs = docs;
				this.numberOfUpdates = numberOfUpdates;
				this.numDocsUpdated = numDocsUpdated;
				this.numExceptions = numExceptions;
			}

			public bool Run()
			{
				for (int j = 0; j < numberOfDocuments; j++)
				{
					Document doc = docs[j];
					SavedRevision lastSavedRevision = null;
					for (int k = 0; k < numberOfUpdates; k++)
					{
						if (lastSavedRevision != null)
						{
							NUnit.Framework.Assert.AreEqual(lastSavedRevision.GetId(), doc.GetCurrentRevisionId
								());
						}
						IDictionary<string, object> contents = new Hashtable(doc.GetProperties());
						Document docLatest = this._enclosing.database.GetDocument(doc.GetId());
						bool wasChecked = (bool)contents.Get("toogle");
						contents.Put("toogle", !wasChecked);
						try
						{
							lastSavedRevision = doc.PutProperties(contents);
							numDocsUpdated.IncrementAndGet();
						}
						catch (CouchbaseLiteException cblex)
						{
							Log.E(LiteTestCase.Tag, "Document update failed", cblex);
							numExceptions.IncrementAndGet();
						}
					}
				}
				return true;
			}

			private readonly ApiTest _enclosing;

			private readonly int numberOfDocuments;

			private readonly Document[] docs;

			private readonly int numberOfUpdates;

			private readonly AtomicInteger numDocsUpdated;

			private readonly AtomicInteger numExceptions;
		}
	}
}
