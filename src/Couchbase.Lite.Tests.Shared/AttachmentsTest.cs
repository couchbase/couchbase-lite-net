//
// AttachmentsTest.cs
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

using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;
using NUnit.Framework;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;
using Newtonsoft.Json.Linq;

namespace Couchbase.Lite
{
	public class AttachmentsTest : LiteTestCase
	{
		public const string Tag = "Attachments";

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestAttachments()
		{
            var testAttachmentName = "test_attachment";
            var attachments = database.Attachments;
			Assert.AreEqual(0, attachments.Count());
            Assert.AreEqual(0, attachments.AllKeys().Count());
			
            var rev1Properties = new Dictionary<string, object>();
			rev1Properties["foo"] = 1;
			rev1Properties["bar"] = false;

            var status = new Status();
            var rev1 = database.PutRevision(
                new RevisionInternal(rev1Properties, database), null, false, status);
			Assert.AreEqual(StatusCode.Created, status.GetCode());

            var attach1 = Runtime.GetBytesForString(
                "This is the body of attach1").ToArray();
			database.InsertAttachmentForSequenceWithNameAndType(
                new ByteArrayInputStream(attach1), 
                rev1.GetSequence(), 
                testAttachmentName, 
                "text/plain", 
                rev1.GetGeneration());

            var attachment = database.GetAttachmentForSequence(
                rev1.GetSequence(), testAttachmentName);
            Assert.AreEqual("text/plain", attachment.ContentType);
            var data = attachment.Content.ToArray();
			Assert.IsTrue(Arrays.Equals(attach1, data));

            // Workaround :
            // Not closing the content stream will cause Sharing Violation
            // Exception when trying to get the same attachment going forward.
            attachment.ContentStream.Close();

            var innerDict = new Dictionary<string, object>();
			innerDict["content_type"] = "text/plain";
			innerDict["digest"] = "sha1-gOHUOBmIMoDCrMuGyaLWzf1hQTE=";
            innerDict["length"] = 27;
			innerDict["stub"] = true;
			innerDict["revpos"] = 1;

            var attachmentDict = new Dictionary<string, object>();
			attachmentDict[testAttachmentName] = innerDict;
            var attachmentDictForSequence = database.GetAttachmentsDictForSequenceWithContent
                (rev1.GetSequence(), DocumentContentOptions.None);
            Assert.AreEqual(1, attachmentDictForSequence.Count);
            AssertPropertiesAreEqual(
                (IDictionary<string, object>)attachmentDict.Get(testAttachmentName), 
                (IDictionary<string, object>)attachmentDictForSequence.Get(testAttachmentName));

            var gotRev1 = database.GetDocumentWithIDAndRev(rev1.GetDocId(), 
                rev1.GetRevId(), DocumentContentOptions.None);
            var gotRev1Properties = (IDictionary<string, object>)gotRev1.GetProperties();
            var gotRev1AttachmentDict = ((JObject)gotRev1Properties.Get("_attachments"))
                .ToObject<Dictionary<string, object>>();
            Assert.AreEqual(1, gotRev1AttachmentDict.Count);
            AssertPropertiesAreEqual(
                (IDictionary<string, object>)attachmentDict.Get(testAttachmentName), 
                ((JObject)gotRev1AttachmentDict.Get(testAttachmentName))
                .ToObject<IDictionary<string, object>>());

			// Check the attachment dict, with attachments included:
			Collections.Remove(innerDict, "stub");
            innerDict.Put("data", Convert.ToBase64String(attach1));
			attachmentDictForSequence = database.GetAttachmentsDictForSequenceWithContent(
                rev1.GetSequence(), DocumentContentOptions.IncludeAttachments);
            Assert.AreEqual(1, attachmentDictForSequence.Count);
            AssertPropertiesAreEqual(
                (IDictionary<string, object>)attachmentDict.Get(testAttachmentName), 
                (IDictionary<string, object>)attachmentDictForSequence.Get(testAttachmentName));

			gotRev1 = database.GetDocumentWithIDAndRev(
                rev1.GetDocId(), rev1.GetRevId(), DocumentContentOptions.IncludeAttachments);
            gotRev1Properties = (IDictionary<string, object>)gotRev1.GetProperties();
            gotRev1AttachmentDict = ((JObject)gotRev1Properties.Get("_attachments"))
                .ToObject<Dictionary<String, Object>>();
            AssertPropertiesAreEqual(
                (IDictionary<string, object>)attachmentDict.Get(testAttachmentName), 
                ((JObject)gotRev1AttachmentDict.Get(testAttachmentName))
                .ToObject<IDictionary<string, object>>());

			// Add a second revision that doesn't update the attachment:
            var rev2Properties = new Dictionary<string, object>();
			rev2Properties.Put("_id", rev1.GetDocId());
			rev2Properties["foo"] = 2;
			rev2Properties["bazz"] = false;
            var rev2 = database.PutRevision(new RevisionInternal(rev2Properties, 
				database), rev1.GetRevId(), false, status);
			Assert.AreEqual(StatusCode.Created, status.GetCode());
			database.CopyAttachmentNamedFromSequenceToSequence(
                testAttachmentName, rev1.GetSequence(), rev2.GetSequence());

			// Add a third revision of the same document:
            var rev3Properties = new Dictionary<string, object>();
			rev3Properties.Put("_id", rev2.GetDocId());
			rev3Properties["foo"] = 2;
			rev3Properties["bazz"] = false;
            var rev3 = database.PutRevision(new RevisionInternal(
                rev3Properties, database), rev2.GetRevId(), false, status);
			Assert.AreEqual(StatusCode.Created, status.GetCode());

            var attach2 = Runtime.GetBytesForString("<html>And this is attach2</html>").ToArray();
            database.InsertAttachmentForSequenceWithNameAndType(
                new ByteArrayInputStream(attach2), rev3.GetSequence(), 
                testAttachmentName, "text/html", rev2.GetGeneration());

			// Check the 2nd revision's attachment:
            var attachment2 = database.GetAttachmentForSequence(rev2.GetSequence(), testAttachmentName);
            Assert.AreEqual("text/plain", attachment2.ContentType);
            data = attachment2.Content.ToArray();
            Assert.IsTrue(Arrays.Equals(attach1, data));

            // Workaround :
            // Not closing the content stream will cause Sharing Violation
            // Exception when trying to get the same attachment going forward.
            attachment2.ContentStream.Close();

			// Check the 3rd revision's attachment:
            Attachment attachment3 = database.GetAttachmentForSequence(rev3.GetSequence(), testAttachmentName);
            Assert.AreEqual("text/html", attachment3.ContentType);
            data = attachment3.Content.ToArray();
            Assert.IsTrue(Arrays.Equals(attach2, data));

            // Workaround :
            // Not closing the content stream will cause Sharing Violation
            // Exception when trying to get the same attachment going forward.
            attachment3.ContentStream.Close();

			// Examine the attachment store:
			Assert.AreEqual(2, attachments.Count());
            var expected = new HashSet<BlobKey>();
			expected.AddItem(BlobStore.KeyForBlob(attach1));
			expected.AddItem(BlobStore.KeyForBlob(attach2));
            Assert.AreEqual(expected.Count, attachments.AllKeys().Count());
            foreach(var key in attachments.AllKeys()) {
                Assert.IsTrue(expected.Contains(key));
            }

            database.Compact();

			// This clears the body of the first revision
			Assert.AreEqual(1, attachments.Count());
            var expected2 = new HashSet<BlobKey>();
			expected2.AddItem(BlobStore.KeyForBlob(attach2));
            Assert.AreEqual(expected2.Count, attachments.AllKeys().Count());
            foreach(var key in attachments.AllKeys()) {
                Assert.IsTrue(expected2.Contains(key));
            }
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
		public void TestPutLargeAttachment()
		{
            var testAttachmentName = "test_attachment";
            var attachments = database.Attachments;
			attachments.DeleteBlobs();
			Assert.AreEqual(0, attachments.Count());

            var status = new Status();
            var rev1Properties = new Dictionary<string, object>();
			rev1Properties["foo"] = 1;
			rev1Properties["bar"] = false;
            var rev1 = database.PutRevision(new RevisionInternal(rev1Properties, 
				database), null, false, status);
			Assert.AreEqual(StatusCode.Created, status.GetCode());
            var largeAttachment = new StringBuilder();
            for (int i = 0; i < Database.BigAttachmentLength; i++)
			{
				largeAttachment.Append("big attachment!");
			}

            var attach1 = Runtime.GetBytesForString(largeAttachment.ToString()).ToArray();
			database.InsertAttachmentForSequenceWithNameAndType(
                new ByteArrayInputStream(attach1), rev1.GetSequence(), 
                testAttachmentName, "text/plain", rev1.GetGeneration());
            var attachment = database.GetAttachmentForSequence(rev1.GetSequence(), testAttachmentName);
            Assert.AreEqual("text/plain", attachment.ContentType);
            var data = attachment.Content.ToArray();
			Assert.IsTrue(Arrays.Equals(attach1, data));

            const DocumentContentOptions contentOptions = DocumentContentOptions.IncludeAttachments | DocumentContentOptions.BigAttachmentsFollow;
            var attachmentDictForSequence = database.GetAttachmentsDictForSequenceWithContent(rev1.GetSequence(), contentOptions);
            var innerDict = (IDictionary<string, object>)attachmentDictForSequence[testAttachmentName];
			if (innerDict.ContainsKey("stub"))
			{
                if (((bool)innerDict["stub"]))
                {
                    throw new RuntimeException("Expected attachment dict 'stub' key to be true");
                } else {
                    throw new RuntimeException("Expected attachment dict to have 'stub' key");
                }
			}
			if (!innerDict.ContainsKey("follows"))
			{
				throw new RuntimeException("Expected attachment dict to have 'follows' key");
			}

            // Workaround :
            // Not closing the content stream will cause Sharing Violation
            // Exception when trying to get the same attachment going forward.
            attachment.ContentStream.Close();

            var rev1WithAttachments = database.GetDocumentWithIDAndRev(
                rev1.GetDocId(), rev1.GetRevId(), contentOptions);
			
            var rev1WithAttachmentsProperties = rev1WithAttachments.GetProperties();
            var rev2Properties = new Dictionary<string, object>();
			rev2Properties.Put("_id", rev1WithAttachmentsProperties["_id"]);
			rev2Properties["foo"] = 2;
            var newRev = new RevisionInternal(rev2Properties, database);
            var rev2 = database.PutRevision(newRev, rev1WithAttachments.GetRevId(), false, status);
			Assert.AreEqual(StatusCode.Created, status.GetCode());

            database.CopyAttachmentNamedFromSequenceToSequence(
                testAttachmentName, rev1WithAttachments.GetSequence(), rev2.GetSequence());

			// Check the 2nd revision's attachment:
            var rev2FetchedAttachment = database.GetAttachmentForSequence(rev2.GetSequence(), testAttachmentName);
            Assert.AreEqual(attachment.Length, rev2FetchedAttachment.Length);
            AssertPropertiesAreEqual(attachment.Metadata, rev2FetchedAttachment.Metadata);
            Assert.AreEqual(attachment.ContentType, rev2FetchedAttachment.ContentType);

			// Add a third revision of the same document:
            var rev3Properties = new Dictionary<string, object>();
            rev3Properties.Put("_id", rev2.GetProperties().Get("_id"));
			rev3Properties["foo"] = 3;
			rev3Properties["baz"] = false;
            var rev3 = new RevisionInternal(rev3Properties, database);
			rev3 = database.PutRevision(rev3, rev2.GetRevId(), false, status);

			Assert.AreEqual(StatusCode.Created, status.GetCode());
            var attach3 = Runtime.GetBytesForString("<html><blink>attach3</blink></html>").ToArray();
			database.InsertAttachmentForSequenceWithNameAndType(
                new ByteArrayInputStream(attach3), rev3.GetSequence(), 
                testAttachmentName, "text/html", rev3.GetGeneration());

			// Check the 3rd revision's attachment:
            var rev3FetchedAttachment = database.GetAttachmentForSequence(
                rev3.GetSequence(), testAttachmentName);
            data = rev3FetchedAttachment.Content.ToArray();
			Assert.IsTrue(Arrays.Equals(attach3, data));
            Assert.AreEqual("text/html", rev3FetchedAttachment.ContentType);

			// TODO: why doesn't this work?
			// Assert.assertEquals(attach3.length, rev3FetchedAttachment.getLength());
			ICollection<BlobKey> blobKeys = database.Attachments.AllKeys();
			Assert.AreEqual(2, blobKeys.Count);
			database.Compact();
			blobKeys = database.Attachments.AllKeys();
			Assert.AreEqual(1, blobKeys.Count);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        /*
        public virtual void TestPutAttachment()
		{
            var testAttachmentName = "test_attachment";
            var attachments = database.Attachments;
			attachments.DeleteBlobs();
			Assert.AreEqual(0, attachments.Count());

			// Put a revision that includes an _attachments dict:
            var attach1 = Runtime.GetBytesForString("This is the body of attach1").ToArray();
            var base64 = Convert.ToBase64String(attach1);
            var attachment = new Dictionary<string, object>();
			attachment["content_type"] = "text/plain";
			attachment["data"] = base64;

            var attachmentDict = new Dictionary<string, object>();
			attachmentDict[testAttachmentName] = attachment;
            var properties = new Dictionary<string, object>();
			properties["foo"] = 1;
			properties["bar"] = false;
			properties["_attachments"] = attachmentDict;

            var rev1 = database.PutRevision(new RevisionInternal(properties, database), null, false);

			// Examine the attachment store:
			Assert.AreEqual(1, attachments.Count());
			
            // Get the revision:
            var gotRev1 = database.GetDocumentWithIDAndRev(rev1.GetDocId(), 
                rev1.GetRevId(), EnumSet.NoneOf<TDContentOptions>());
            var gotAttachmentDict = (IDictionary<string, object>)gotRev1.
                GetProperties().Get("_attachments");
            var innerDict = new Dictionary<string, object>();
			innerDict["content_type"] = "text/plain";
			innerDict["digest"] = "sha1-gOHUOBmIMoDCrMuGyaLWzf1hQTE=";
			innerDict["length"] = 27;
			innerDict["stub"] = true;
			innerDict["revpos"] = 1;
            var expectAttachmentDict = new Dictionary<string, object>();
			expectAttachmentDict[testAttachmentName] = innerDict;
			Assert.AreEqual(expectAttachmentDict, gotAttachmentDict);

			// Update the attachment directly:
            var attachv2 = Runtime.GetBytesForString("Replaced body of attach").ToArray();
            var gotExpectedErrorCode = false;
			try
			{
				database.UpdateAttachment(testAttachmentName, new ByteArrayInputStream(attachv2), 
					"application/foo", rev1.GetDocId(), null);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedErrorCode = (e.GetCBLStatus().GetCode() == StatusCode.Conflict);
			}
			Assert.IsTrue(gotExpectedErrorCode);
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

			Assert.IsTrue(gotExpectedErrorCode);
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
			Assert.IsFalse(gotExpectedErrorCode);
			Assert.AreEqual(rev1.GetDocId(), rev2.GetDocId());
			Assert.AreEqual(2, rev2.GetGeneration());
			// Get the updated revision:
			RevisionInternal gotRev2 = database.GetDocumentWithIDAndRev(rev2.GetDocId(), rev2
				.GetRevId(), EnumSet.NoneOf<TDContentOptions>());
			attachmentDict = (IDictionary<string, object>)gotRev2.Properties.Get("_attachments"
				);
			innerDict = new Dictionary<string, object>();
			innerDict["content_type"] = "application/foo";
			innerDict["digest"] = "sha1-mbT3208HI3PZgbG4zYWbDW2HsPk=";
			innerDict["length"] = 23;
			innerDict["stub"] = true;
			innerDict["revpos"] = 2;
			expectAttachmentDict[testAttachmentName] = innerDict;
			Assert.AreEqual(expectAttachmentDict, attachmentDict);
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
			Assert.IsTrue(gotExpectedErrorCode);
			gotExpectedErrorCode = false;
			try
			{
				database.UpdateAttachment("nosuchattach", null, null, "nosuchdoc", "nosuchrev");
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedErrorCode = (e.GetCBLStatus().GetCode() == StatusCode.NotFound);
			}
			Assert.IsTrue(gotExpectedErrorCode);
			RevisionInternal rev3 = database.UpdateAttachment(testAttachmentName, null, null, 
				rev2.GetDocId(), rev2.GetRevId());
			Assert.AreEqual(rev2.GetDocId(), rev3.GetDocId());
			Assert.AreEqual(3, rev3.GetGeneration());
			// Get the updated revision:
			RevisionInternal gotRev3 = database.GetDocumentWithIDAndRev(rev3.GetDocId(), rev3
				.GetRevId(), EnumSet.NoneOf<TDContentOptions>());
			attachmentDict = (IDictionary<string, object>)gotRev3.Properties.Get("_attachments"
				);
			Assert.IsNull(attachmentDict);
			database.Close();
        }*/

        [Test]
		public void TestStreamAttachmentBlobStoreWriter()
		{
            var attachments = database.Attachments;
            var blobWriter = new BlobStoreWriter(attachments);
            var testBlob = "foo";
            blobWriter.AppendData(Runtime.GetBytesForString(testBlob));
			blobWriter.Finish();

            var sha1Base64Digest = "sha1-C+7Hteo/D9vJXQ3UfzxbwnXaijM=";
			Assert.AreEqual(blobWriter.SHA1DigestString(), sha1Base64Digest);
			Assert.AreEqual(blobWriter.MD5DigestString(), "md5-rL0Y20zC+Fzt72VPzMSk2A==");

			// install it
			blobWriter.Install();
			// look it up in blob store and make sure it's there
            var blobKey = new BlobKey(sha1Base64Digest);
            var blob = attachments.BlobForKey(blobKey);
			Assert.IsTrue(Arrays.Equals(Runtime.GetBytesForString(testBlob).ToArray(), blob));
		}

		/// <summary>https://github.com/couchbase/couchbase-lite-android/issues/134</summary>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
        [Test]
		public void TestGetAttachmentBodyUsingPrefetch()
		{
			// add a doc with an attachment
            var doc = database.CreateDocument();
            var rev = doc.CreateRevision();

            var properties = new Dictionary<string, object>();
			properties["foo"] = "bar";
			rev.SetUserProperties(properties);

            var attachBodyBytes = Runtime.GetBytesForString("attach body").ToArray();
            var attachment = new Attachment(new ByteArrayInputStream(attachBodyBytes), "text/plain");
			string attachmentName = "test_attachment.txt";
			rev.AddAttachment(attachment, attachmentName);
            rev.Save();

			// do query that finds that doc with prefetch

            var view = database.GetView("aview");
            view.SetMapReduce((IDictionary<string, object> document, EmitDelegate emitter)=>
                {
                    var id = (string)document["_id"];
                    emitter(id, null);
                }, null, "1");

			// try to get the attachment
            var query = view.CreateQuery();
			query.Prefetch=true;
            var results = query.Run();
            while (results.MoveNext())
			{
                var row = results.Current;
				// This returns the revision just fine, but the sequence number
				// is set to 0.
                var revision = row.Document.CurrentRevision;
                //var attachments = revision.AttachmentNames.ToList();

				// This returns an Attachment object which looks ok, except again
				// its sequence number is 0. The metadata property knows about
				// the length and mime type of the attachment. It also says
				// "stub" -> "true".
                var attachmentRetrieved = revision.GetAttachment(attachmentName);
                var inputStream = attachmentRetrieved.ContentStream;
                Assert.IsNotNull(inputStream);
                var attachmentDataRetrieved = attachmentRetrieved.Content.ToArray();
                var attachmentDataRetrievedString = Runtime.GetStringForBytes(attachmentDataRetrieved);
                var attachBodyString = Sharpen.Runtime.GetStringForBytes(attachBodyBytes);
				Assert.AreEqual(attachBodyString, attachmentDataRetrievedString);
			}
		}
	}
}
