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

using System.Collections.Generic;
using System.IO;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
	public class AttachmentsTest : LiteTestCase
	{
		public const string Tag = "Attachments";

		/// <exception cref="System.Exception"></exception>
		public virtual void TestAttachments()
		{
			string testAttachmentName = "test_attachment";
			BlobStore attachments = database.GetAttachments();
			NUnit.Framework.Assert.AreEqual(0, attachments.Count());
			NUnit.Framework.Assert.AreEqual(new HashSet<object>(), attachments.AllKeys());
			Status status = new Status();
			IDictionary<string, object> rev1Properties = new Dictionary<string, object>();
			rev1Properties["foo"] = 1;
			rev1Properties["bar"] = false;
			RevisionInternal rev1 = database.PutRevision(new RevisionInternal(rev1Properties, 
				database), null, false, status);
			NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			byte[] attach1 = Sharpen.Runtime.GetBytesForString("This is the body of attach1");
			database.InsertAttachmentForSequenceWithNameAndType(new ByteArrayInputStream(attach1
				), rev1.GetSequence(), testAttachmentName, "text/plain", rev1.GetGeneration());
			NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			Attachment attachment = database.GetAttachmentForSequence(rev1.GetSequence(), testAttachmentName
				);
			NUnit.Framework.Assert.AreEqual("text/plain", attachment.GetContentType());
			byte[] data = IOUtils.ToByteArray(attachment.GetContent());
			NUnit.Framework.Assert.IsTrue(Arrays.Equals(attach1, data));
			IDictionary<string, object> innerDict = new Dictionary<string, object>();
			innerDict.Put("content_type", "text/plain");
			innerDict.Put("digest", "sha1-gOHUOBmIMoDCrMuGyaLWzf1hQTE=");
			innerDict["length"] = 27;
			innerDict["stub"] = true;
			innerDict["revpos"] = 1;
			IDictionary<string, object> attachmentDict = new Dictionary<string, object>();
			attachmentDict[testAttachmentName] = innerDict;
			IDictionary<string, object> attachmentDictForSequence = database.GetAttachmentsDictForSequenceWithContent
				(rev1.GetSequence(), EnumSet.NoneOf<TDContentOptions>());
			NUnit.Framework.Assert.AreEqual(attachmentDict, attachmentDictForSequence);
			RevisionInternal gotRev1 = database.GetDocumentWithIDAndRev(rev1.GetDocId(), rev1
				.GetRevId(), EnumSet.NoneOf<TDContentOptions>());
			IDictionary<string, object> gotAttachmentDict = (IDictionary<string, object>)gotRev1
				.Properties.Get("_attachments");
			NUnit.Framework.Assert.AreEqual(attachmentDict, gotAttachmentDict);
			// Check the attachment dict, with attachments included:
			Sharpen.Collections.Remove(innerDict, "stub");
			innerDict.Put("data", Base64.EncodeBytes(attach1));
			attachmentDictForSequence = database.GetAttachmentsDictForSequenceWithContent(rev1
				.GetSequence(), EnumSet.Of(TDContentOptions.TDIncludeAttachments));
			NUnit.Framework.Assert.AreEqual(attachmentDict, attachmentDictForSequence);
			gotRev1 = database.GetDocumentWithIDAndRev(rev1.GetDocId(), rev1.GetRevId(), EnumSet
				.Of(TDContentOptions.TDIncludeAttachments));
			gotAttachmentDict = (IDictionary<string, object>)gotRev1.Properties.Get("_attachments"
				);
			NUnit.Framework.Assert.AreEqual(attachmentDict, gotAttachmentDict);
			// Add a second revision that doesn't update the attachment:
			IDictionary<string, object> rev2Properties = new Dictionary<string, object>();
			rev2Properties.Put("_id", rev1.GetDocId());
			rev2Properties["foo"] = 2;
			rev2Properties["bazz"] = false;
			RevisionInternal rev2 = database.PutRevision(new RevisionInternal(rev2Properties, 
				database), rev1.GetRevId(), false, status);
			NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			database.CopyAttachmentNamedFromSequenceToSequence(testAttachmentName, rev1.GetSequence
				(), rev2.GetSequence());
			// Add a third revision of the same document:
			IDictionary<string, object> rev3Properties = new Dictionary<string, object>();
			rev3Properties.Put("_id", rev2.GetDocId());
			rev3Properties["foo"] = 2;
			rev3Properties["bazz"] = false;
			RevisionInternal rev3 = database.PutRevision(new RevisionInternal(rev3Properties, 
				database), rev2.GetRevId(), false, status);
			NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			byte[] attach2 = Sharpen.Runtime.GetBytesForString("<html>And this is attach2</html>"
				);
			database.InsertAttachmentForSequenceWithNameAndType(new ByteArrayInputStream(attach2
				), rev3.GetSequence(), testAttachmentName, "text/html", rev2.GetGeneration());
			// Check the 2nd revision's attachment:
			Attachment attachment2 = database.GetAttachmentForSequence(rev2.GetSequence(), testAttachmentName
				);
			NUnit.Framework.Assert.AreEqual("text/plain", attachment2.GetContentType());
			data = IOUtils.ToByteArray(attachment2.GetContent());
			NUnit.Framework.Assert.IsTrue(Arrays.Equals(attach1, data));
			// Check the 3rd revision's attachment:
			Attachment attachment3 = database.GetAttachmentForSequence(rev3.GetSequence(), testAttachmentName
				);
			NUnit.Framework.Assert.AreEqual("text/html", attachment3.GetContentType());
			data = IOUtils.ToByteArray(attachment3.GetContent());
			NUnit.Framework.Assert.IsTrue(Arrays.Equals(attach2, data));
			// Examine the attachment store:
			NUnit.Framework.Assert.AreEqual(2, attachments.Count());
			ICollection<BlobKey> expected = new HashSet<BlobKey>();
			expected.AddItem(BlobStore.KeyForBlob(attach1));
			expected.AddItem(BlobStore.KeyForBlob(attach2));
			NUnit.Framework.Assert.AreEqual(expected, attachments.AllKeys());
			status = database.Compact();
			// This clears the body of the first revision
			NUnit.Framework.Assert.AreEqual(StatusCode.Ok, status.GetCode());
			NUnit.Framework.Assert.AreEqual(1, attachments.Count());
			ICollection<BlobKey> expected2 = new HashSet<BlobKey>();
			expected2.AddItem(BlobStore.KeyForBlob(attach2));
			NUnit.Framework.Assert.AreEqual(expected2, attachments.AllKeys());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPutLargeAttachment()
		{
			string testAttachmentName = "test_attachment";
			BlobStore attachments = database.GetAttachments();
			attachments.DeleteBlobs();
			NUnit.Framework.Assert.AreEqual(0, attachments.Count());
			Status status = new Status();
			IDictionary<string, object> rev1Properties = new Dictionary<string, object>();
			rev1Properties["foo"] = 1;
			rev1Properties["bar"] = false;
			RevisionInternal rev1 = database.PutRevision(new RevisionInternal(rev1Properties, 
				database), null, false, status);
			NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			StringBuilder largeAttachment = new StringBuilder();
			for (int i = 0; i < Database.kBigAttachmentLength; i++)
			{
				largeAttachment.Append("big attachment!");
			}
			byte[] attach1 = Sharpen.Runtime.GetBytesForString(largeAttachment.ToString());
			database.InsertAttachmentForSequenceWithNameAndType(new ByteArrayInputStream(attach1
				), rev1.GetSequence(), testAttachmentName, "text/plain", rev1.GetGeneration());
			Attachment attachment = database.GetAttachmentForSequence(rev1.GetSequence(), testAttachmentName
				);
			NUnit.Framework.Assert.AreEqual("text/plain", attachment.GetContentType());
			byte[] data = IOUtils.ToByteArray(attachment.GetContent());
			NUnit.Framework.Assert.IsTrue(Arrays.Equals(attach1, data));
			EnumSet<TDContentOptions> contentOptions = EnumSet.Of(TDContentOptions
				.TDIncludeAttachments, TDContentOptions.TDBigAttachmentsFollow);
			IDictionary<string, object> attachmentDictForSequence = database.GetAttachmentsDictForSequenceWithContent
				(rev1.GetSequence(), contentOptions);
			IDictionary<string, object> innerDict = (IDictionary<string, object>)attachmentDictForSequence
				.Get(testAttachmentName);
			if (!innerDict.ContainsKey("stub"))
			{
				throw new RuntimeException("Expected attachment dict to have 'stub' key");
			}
			if (((bool)innerDict.Get("stub")) == false)
			{
				throw new RuntimeException("Expected attachment dict 'stub' key to be true");
			}
			if (!innerDict.ContainsKey("follows"))
			{
				throw new RuntimeException("Expected attachment dict to have 'follows' key");
			}
			RevisionInternal rev1WithAttachments = database.GetDocumentWithIDAndRev(rev1.GetDocId
				(), rev1.GetRevId(), contentOptions);
			// Map<String,Object> rev1PropertiesPrime = rev1WithAttachments.Properties;
			// rev1PropertiesPrime.put("foo", 2);
			IDictionary<string, object> rev1WithAttachmentsProperties = rev1WithAttachments.GetProperties
				();
			IDictionary<string, object> rev2Properties = new Dictionary<string, object>();
			rev2Properties.Put("_id", rev1WithAttachmentsProperties.Get("_id"));
			rev2Properties["foo"] = 2;
			RevisionInternal newRev = new RevisionInternal(rev2Properties, database);
			RevisionInternal rev2 = database.PutRevision(newRev, rev1WithAttachments.GetRevId
				(), false, status);
			NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			database.CopyAttachmentNamedFromSequenceToSequence(testAttachmentName, rev1WithAttachments
				.GetSequence(), rev2.GetSequence());
			// Check the 2nd revision's attachment:
			Attachment rev2FetchedAttachment = database.GetAttachmentForSequence(rev2.GetSequence
				(), testAttachmentName);
			NUnit.Framework.Assert.AreEqual(attachment.GetLength(), rev2FetchedAttachment.GetLength
				());
			NUnit.Framework.Assert.AreEqual(attachment.GetMetadata(), rev2FetchedAttachment.GetMetadata
				());
			NUnit.Framework.Assert.AreEqual(attachment.GetContentType(), rev2FetchedAttachment
				.GetContentType());
			// Add a third revision of the same document:
			IDictionary<string, object> rev3Properties = new Dictionary<string, object>();
			rev3Properties.Put("_id", rev2.Properties.Get("_id"));
			rev3Properties["foo"] = 3;
			rev3Properties["baz"] = false;
			RevisionInternal rev3 = new RevisionInternal(rev3Properties, database);
			rev3 = database.PutRevision(rev3, rev2.GetRevId(), false, status);
			NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			byte[] attach3 = Sharpen.Runtime.GetBytesForString("<html><blink>attach3</blink></html>"
				);
			database.InsertAttachmentForSequenceWithNameAndType(new ByteArrayInputStream(attach3
				), rev3.GetSequence(), testAttachmentName, "text/html", rev3.GetGeneration());
			// Check the 3rd revision's attachment:
			Attachment rev3FetchedAttachment = database.GetAttachmentForSequence(rev3.GetSequence
				(), testAttachmentName);
			data = IOUtils.ToByteArray(rev3FetchedAttachment.GetContent());
			NUnit.Framework.Assert.IsTrue(Arrays.Equals(attach3, data));
			NUnit.Framework.Assert.AreEqual("text/html", rev3FetchedAttachment.GetContentType
				());
			// TODO: why doesn't this work?
			// Assert.assertEquals(attach3.length, rev3FetchedAttachment.getLength());
			ICollection<BlobKey> blobKeys = database.GetAttachments().AllKeys();
			NUnit.Framework.Assert.AreEqual(2, blobKeys.Count);
			database.Compact();
			blobKeys = database.GetAttachments().AllKeys();
			NUnit.Framework.Assert.AreEqual(1, blobKeys.Count);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestPutAttachment()
		{
			string testAttachmentName = "test_attachment";
			BlobStore attachments = database.GetAttachments();
			attachments.DeleteBlobs();
			NUnit.Framework.Assert.AreEqual(0, attachments.Count());
			// Put a revision that includes an _attachments dict:
			byte[] attach1 = Sharpen.Runtime.GetBytesForString("This is the body of attach1");
			string base64 = Base64.EncodeBytes(attach1);
			IDictionary<string, object> attachment = new Dictionary<string, object>();
			attachment.Put("content_type", "text/plain");
			attachment["data"] = base64;
			IDictionary<string, object> attachmentDict = new Dictionary<string, object>();
			attachmentDict[testAttachmentName] = attachment;
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["foo"] = 1;
			properties["bar"] = false;
			properties["_attachments"] = attachmentDict;
			RevisionInternal rev1 = database.PutRevision(new RevisionInternal(properties, database
				), null, false);
			// Examine the attachment store:
			NUnit.Framework.Assert.AreEqual(1, attachments.Count());
			// Get the revision:
			RevisionInternal gotRev1 = database.GetDocumentWithIDAndRev(rev1.GetDocId(), rev1
				.GetRevId(), EnumSet.NoneOf<TDContentOptions>());
			IDictionary<string, object> gotAttachmentDict = (IDictionary<string, object>)gotRev1
				.Properties.Get("_attachments");
			IDictionary<string, object> innerDict = new Dictionary<string, object>();
			innerDict.Put("content_type", "text/plain");
			innerDict.Put("digest", "sha1-gOHUOBmIMoDCrMuGyaLWzf1hQTE=");
			innerDict["length"] = 27;
			innerDict["stub"] = true;
			innerDict["revpos"] = 1;
			IDictionary<string, object> expectAttachmentDict = new Dictionary<string, object>
				();
			expectAttachmentDict[testAttachmentName] = innerDict;
			NUnit.Framework.Assert.AreEqual(expectAttachmentDict, gotAttachmentDict);
			// Update the attachment directly:
			byte[] attachv2 = Sharpen.Runtime.GetBytesForString("Replaced body of attach");
			bool gotExpectedErrorCode = false;
			try
			{
				database.UpdateAttachment(testAttachmentName, new ByteArrayInputStream(attachv2), 
					"application/foo", rev1.GetDocId(), null);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedErrorCode = (e.GetCBLStatus().GetCode() == StatusCode.Conflict);
			}
			NUnit.Framework.Assert.IsTrue(gotExpectedErrorCode);
			gotExpectedErrorCode = false;
			try
			{
				database.UpdateAttachment(testAttachmentName, new ByteArrayInputStream(attachv2), 
					"application/foo", rev1.GetDocId(), "1-bogus");
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedErrorCode = (e.GetCBLStatus().GetCode() == StatusCode.Conflict);
			}
			NUnit.Framework.Assert.IsTrue(gotExpectedErrorCode);
			gotExpectedErrorCode = false;
			RevisionInternal rev2 = null;
			try
			{
				rev2 = database.UpdateAttachment(testAttachmentName, new ByteArrayInputStream(attachv2
					), "application/foo", rev1.GetDocId(), rev1.GetRevId());
			}
			catch (CouchbaseLiteException)
			{
				gotExpectedErrorCode = true;
			}
			NUnit.Framework.Assert.IsFalse(gotExpectedErrorCode);
			NUnit.Framework.Assert.AreEqual(rev1.GetDocId(), rev2.GetDocId());
			NUnit.Framework.Assert.AreEqual(2, rev2.GetGeneration());
			// Get the updated revision:
			RevisionInternal gotRev2 = database.GetDocumentWithIDAndRev(rev2.GetDocId(), rev2
				.GetRevId(), EnumSet.NoneOf<TDContentOptions>());
			attachmentDict = (IDictionary<string, object>)gotRev2.Properties.Get("_attachments"
				);
			innerDict = new Dictionary<string, object>();
			innerDict.Put("content_type", "application/foo");
			innerDict.Put("digest", "sha1-mbT3208HI3PZgbG4zYWbDW2HsPk=");
			innerDict["length"] = 23;
			innerDict["stub"] = true;
			innerDict["revpos"] = 2;
			expectAttachmentDict[testAttachmentName] = innerDict;
			NUnit.Framework.Assert.AreEqual(expectAttachmentDict, attachmentDict);
			// Delete the attachment:
			gotExpectedErrorCode = false;
			try
			{
				database.UpdateAttachment("nosuchattach", null, null, rev2.GetDocId(), rev2.GetRevId
					());
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedErrorCode = (e.GetCBLStatus().GetCode() == StatusCode.NotFound);
			}
			NUnit.Framework.Assert.IsTrue(gotExpectedErrorCode);
			gotExpectedErrorCode = false;
			try
			{
				database.UpdateAttachment("nosuchattach", null, null, "nosuchdoc", "nosuchrev");
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedErrorCode = (e.GetCBLStatus().GetCode() == StatusCode.NotFound);
			}
			NUnit.Framework.Assert.IsTrue(gotExpectedErrorCode);
			RevisionInternal rev3 = database.UpdateAttachment(testAttachmentName, null, null, 
				rev2.GetDocId(), rev2.GetRevId());
			NUnit.Framework.Assert.AreEqual(rev2.GetDocId(), rev3.GetDocId());
			NUnit.Framework.Assert.AreEqual(3, rev3.GetGeneration());
			// Get the updated revision:
			RevisionInternal gotRev3 = database.GetDocumentWithIDAndRev(rev3.GetDocId(), rev3
				.GetRevId(), EnumSet.NoneOf<TDContentOptions>());
			attachmentDict = (IDictionary<string, object>)gotRev3.Properties.Get("_attachments"
				);
			NUnit.Framework.Assert.IsNull(attachmentDict);
			database.Close();
		}

		public virtual void TestStreamAttachmentBlobStoreWriter()
		{
			BlobStore attachments = database.GetAttachments();
			BlobStoreWriter blobWriter = new BlobStoreWriter(attachments);
			string testBlob = "foo";
			blobWriter.AppendData(Sharpen.Runtime.GetBytesForString(new string(testBlob)));
			blobWriter.Finish();
			string sha1Base64Digest = "sha1-C+7Hteo/D9vJXQ3UfzxbwnXaijM=";
			NUnit.Framework.Assert.AreEqual(blobWriter.SHA1DigestString(), sha1Base64Digest);
			NUnit.Framework.Assert.AreEqual(blobWriter.MD5DigestString(), "md5-rL0Y20zC+Fzt72VPzMSk2A=="
				);
			// install it
			blobWriter.Install();
			// look it up in blob store and make sure it's there
			BlobKey blobKey = new BlobKey(sha1Base64Digest);
			byte[] blob = attachments.BlobForKey(blobKey);
			NUnit.Framework.Assert.IsTrue(Arrays.Equals(Sharpen.Runtime.GetBytesForString(testBlob
				, Sharpen.Extensions.GetEncoding("UTF-8")), blob));
		}

		/// <summary>https://github.com/couchbase/couchbase-lite-android/issues/134</summary>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestGetAttachmentBodyUsingPrefetch()
		{
			// add a doc with an attachment
			Document doc = database.CreateDocument();
			UnsavedRevision rev = doc.CreateRevision();
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["foo"] = "bar";
			rev.SetUserProperties(properties);
			byte[] attachBodyBytes = Sharpen.Runtime.GetBytesForString("attach body");
			Attachment attachment = new Attachment(new ByteArrayInputStream(attachBodyBytes), 
				"text/plain");
			string attachmentName = "test_attachment.txt";
			rev.AddAttachment(attachment, attachmentName);
			rev.Save();
			// do query that finds that doc with prefetch
			View view = database.GetView("aview");
            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=>
                {
                    string id = (string)document.Get("_id");
                    emitter.Emit(id, null);
                }, null, "1");
			// try to get the attachment
			Query query = view.CreateQuery();
			query.Prefetch=true;
			QueryEnumerator results = query.Run();
            while (results.MoveNext())
			{
				QueryRow row = results.Current();
				// This returns the revision just fine, but the sequence number
				// is set to 0.
				SavedRevision revision = row.GetDocument().CurrentRevision;
				IList<string> attachments = revision.GetAttachmentNames();
				// This returns an Attachment object which looks ok, except again
				// its sequence number is 0. The metadata property knows about
				// the length and mime type of the attachment. It also says
				// "stub" -> "true".
				Attachment attachmentRetrieved = revision.GetAttachment(attachmentName);
				// This throws a CouchbaseLiteException with StatusCode.NOT_FOUND.
				InputStream @is = attachmentRetrieved.GetContent();
				NUnit.Framework.Assert.IsNotNull(@is);
				byte[] attachmentDataRetrieved = TextUtils.Read(@is);
				string attachmentDataRetrievedString = Sharpen.Runtime.GetStringForBytes(attachmentDataRetrieved
					);
				string attachBodyString = Sharpen.Runtime.GetStringForBytes(attachBodyBytes);
				NUnit.Framework.Assert.AreEqual(attachBodyString, attachmentDataRetrievedString);
			}
		}
	}
}
