//
// AttachmentsTest.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
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
/*
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
using Couchbase.Lite.Storage;
using Couchbase.Lite.Util;
using Newtonsoft.Json;
using System.IO;
using Couchbase.Lite.Store;
using System.Net;
using System.Threading;

namespace Couchbase.Lite
{
    public class AttachmentsTest : LiteTestCase
    {
        public const string Tag = "Attachments";

        [Test]
        public void TestUpgradeMD5()
        {
            var store = database.Storage as SqliteCouchStore;
            if (store == null) {
                Assert.Inconclusive("This test is only valid for a SQLite based store, since any others will be too new to see this issue");
            }

            try {
                HttpWebRequest.Create("http://localhost:5984/").GetResponse();
            } catch(Exception) {
                Assert.Inconclusive("Apache CouchDB not running");
            }

            var dbName = "a" + Misc.CreateGUID();
            var putRequest = HttpWebRequest.Create("http://localhost:5984/" + dbName);
            putRequest.Method = "PUT";
            var response = (HttpWebResponse)putRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);


            // The API prevents new insertions with MD5 hashes, so we need to insert this bypassing the API
            // to simulate a legacy document
            var engine = store.StorageEngine;
            var docName = "doc" + Convert.ToString(DateTime.UtcNow.ToMillisecondsSinceEpoch());
            var contentVals = new ContentValues();
            contentVals["docid"] = docName;
            engine.Insert("docs", null, contentVals);

            contentVals = new ContentValues();
            contentVals["doc_id"] = 1;
            contentVals["revid"] = "1-1153b140e4c8674e2e6425c94de860a0";
            contentVals["current"] = false;
            contentVals["deleted"] = false;
            contentVals["no_attachments"] = true;
            string json = "{\"foo\":false}";
            contentVals["json"] = Encoding.UTF8.GetBytes(json);
            engine.Insert("revs", null, contentVals);

            contentVals = new ContentValues();
            contentVals["doc_id"] = 1;
            contentVals["revid"] = "2-bb71ce0da1de19f848177525c4ae5a8b";
            contentVals["current"] = false;
            contentVals["deleted"] = false;
            contentVals["no_attachments"] = false;
            contentVals["parent"] = 1;
            json = "{\"foo\":false,\"_attachments\":{\"attachment\":{\"content_type\":\"image/png\",\"revpos\":2," +
                "\"digest\":\"md5-ks1IBwCXbuY7VWAO9CkEjA==\",\"length\":519173,\"stub\":true}}}";
            contentVals["json"] = Encoding.UTF8.GetBytes(json);
            engine.Insert("revs", null, contentVals);

            contentVals = new ContentValues();
            contentVals["doc_id"] = 1;
            contentVals["revid"] = "3-a020d6aae370ab5cbc136c477f4e5928";
            contentVals["current"] = true;
            contentVals["deleted"] = false;
            contentVals["no_attachments"] = false;
            contentVals["parent"] = 2;
            json = "{\"foo\":true,\"_attachments\":{\"attachment\":{\"content_type\":\"image/png\",\"revpos\":2," +
                "\"digest\":\"md5-ks1IBwCXbuY7VWAO9CkEjA==\",\"length\":519173,\"stub\":true}}}";
            contentVals["json"] = Encoding.UTF8.GetBytes(json);
            engine.Insert("revs", null, contentVals);

            var attachmentStream = (InputStream)GetAsset("attachment.png");
            var fileStream = File.OpenWrite(Path.Combine(database.AttachmentStorePath, "92CD480700976EE63B55600EF429048C.blob"));
            attachmentStream.Wrapped.CopyTo(fileStream);
            attachmentStream.Dispose();
            fileStream.Dispose();

            var baseEndpoint = String.Format("http://localhost:5984/{0}/{1}", dbName, docName);
            var endpoint = baseEndpoint;
            var docContent = Encoding.UTF8.GetBytes("{\"foo\":false}");
            putRequest = HttpWebRequest.Create(endpoint);
            putRequest.Method = "PUT";
            putRequest.ContentType = "application/json";
            putRequest.GetRequestStream().Write(docContent, 0, docContent.Length);
            response = (HttpWebResponse)putRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);


            attachmentStream = (InputStream)GetAsset("attachment.png");
            var baos = new MemoryStream();
            attachmentStream.Wrapped.CopyTo(baos);
            attachmentStream.Dispose();
            endpoint = baseEndpoint + "/attachment?rev=1-1153b140e4c8674e2e6425c94de860a0";
            docContent = baos.ToArray();
            baos.Dispose();

            putRequest = HttpWebRequest.Create(endpoint);
            putRequest.Method = "PUT";
            putRequest.ContentType = "image/png";
            putRequest.GetRequestStream().Write(docContent, 0, docContent.Length);
            response = (HttpWebResponse)putRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            endpoint = baseEndpoint + "?rev=2-bb71ce0da1de19f848177525c4ae5a8b";
            docContent = Encoding.UTF8.GetBytes("{\"foo\":true,\"_attachments\":{\"attachment\":{\"content_type\":\"image/png\",\"revpos\":2,\"digest\":\"md5-ks1IBwCXbuY7VWAO9CkEjA==\",\"length\":519173,\"stub\":true}}}");
            putRequest = HttpWebRequest.Create(endpoint);
            putRequest.Method = "PUT";
            putRequest.ContentType = "application/json";
            putRequest.GetRequestStream().Write(docContent, 0, docContent.Length);
            response = (HttpWebResponse)putRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            var pull = database.CreatePullReplication(new Uri("http://localhost:5984/" + dbName));
            pull.Continuous = true;
            pull.Start();

            endpoint = baseEndpoint + "?rev=3-a020d6aae370ab5cbc136c477f4e5928";
            docContent = Encoding.UTF8.GetBytes("{\"foo\":false,\"_attachments\":{\"attachment\":{\"content_type\":\"image/png\",\"revpos\":2,\"digest\":\"md5-ks1IBwCXbuY7VWAO9CkEjA==\",\"length\":519173,\"stub\":true}}}");
            putRequest = HttpWebRequest.Create(endpoint);
            putRequest.Method = "PUT";
            putRequest.ContentType = "application/json";
            putRequest.GetRequestStream().Write(docContent, 0, docContent.Length);
            response = (HttpWebResponse)putRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            Thread.Sleep(1000);
            while (pull.Status == ReplicationStatus.Active) {
                Thread.Sleep(500);
            }

            var doc = database.GetExistingDocument(docName);
            Assert.AreEqual("4-a91f8875144c6162874371c07a08ea17", doc.CurrentRevisionId);
            var attachment = doc.CurrentRevision.Attachments.ElementAtOrDefault(0);
            Assert.IsNotNull(attachment);
            var attachmentsDict = doc.GetProperty("_attachments").AsDictionary<string, object>();
            var attachmentDict = attachmentsDict.Get("attachment").AsDictionary<string, object>();
            Assert.AreEqual("md5-ks1IBwCXbuY7VWAO9CkEjA==", attachmentDict["digest"]);

            var deleteRequest = HttpWebRequest.Create(baseEndpoint + "/attachment?rev=4-a91f8875144c6162874371c07a08ea17");
            deleteRequest.Method = "DELETE";
            response = (HttpWebResponse)deleteRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            attachmentStream = (InputStream)GetAsset("attachment2.png");
            baos = new MemoryStream();
            attachmentStream.Wrapped.CopyTo(baos);
            attachmentStream.Dispose();
            endpoint = baseEndpoint + "/attachment?rev=5-4737cb66c6a7ef1b11e872cb6fa4d51a";
            docContent = baos.ToArray();
            baos.Dispose();

            putRequest = HttpWebRequest.Create(endpoint);
            putRequest.Method = "PUT";
            putRequest.ContentType = "image/png";
            putRequest.GetRequestStream().Write(docContent, 0, docContent.Length);
            response = (HttpWebResponse)putRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            Thread.Sleep(1000);
            while (pull.Status == ReplicationStatus.Active) {
                Thread.Sleep(500);
            }

            doc = database.GetExistingDocument(docName);
            Assert.AreEqual("6-e3a7423a9a9de094a0d12d7f3b44634c", doc.CurrentRevisionId);
            attachment = doc.CurrentRevision.Attachments.ElementAtOrDefault(0);
            Assert.IsNotNull(attachment);
            attachmentsDict = doc.GetProperty("_attachments").AsDictionary<string, object>();
            attachmentDict = attachmentsDict.Get("attachment").AsDictionary<string, object>();
            Assert.AreEqual("sha1-9ijdmMf0mK7c11WQPw7DBQcX5pE=", attachmentDict["digest"]);

            deleteRequest = HttpWebRequest.Create("http://localhost:5984/" + dbName);
            deleteRequest.Method = "DELETE";
            response = (HttpWebResponse)deleteRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestAttachments()
        {
            const string testAttachmentName = "test_attachment";
            var attachments = database.Attachments;
            Assert.AreEqual(0, attachments.Count());
            Assert.AreEqual(0, attachments.AllKeys().Count());

            var attach1 = Encoding.UTF8.GetBytes("This is the body of attach1");
            var props = new Dictionary<string, object> {
                { "foo", 1 },
                { "bar", false },
                { "_attachments", CreateAttachmentsDict(attach1, testAttachmentName, "text/plain", false) }
            };

            Status status = new Status();
            RevisionInternal rev1 = database.PutRevision(new RevisionInternal(props), null, false, status);
            Assert.AreEqual(StatusCode.Created, status.Code);

            var att = database.GetAttachmentForRevision(rev1, testAttachmentName, status);
            Assert.IsNotNull(att, "Couldn't get attachment:  Status {0}", status.Code);
            Assert.AreEqual(attach1, att.Content);
            Assert.AreEqual("text/plain", att.ContentType);
            Assert.AreEqual(AttachmentEncoding.None, att.Encoding);

            var itemDict = new Dictionary<string, object> {
                { "content_type", "text/plain" },
                { "digest", "sha1-gOHUOBmIMoDCrMuGyaLWzf1hQTE=" },
                { "length", 27 },
                { "stub", true },
                { "revpos", 1 }
            };
            var attachmentDict = new Dictionary<string, object> {
                { testAttachmentName, itemDict }
            };
            var gotRev1 = database.GetDocument(rev1.GetDocId(), rev1.GetRevId(), true);
            AssertDictionariesAreEqual(attachmentDict, gotRev1.GetAttachments());

            itemDict.Remove("stub");
            itemDict["data"] = Convert.ToBase64String(attach1);
            gotRev1 = database.GetDocument(rev1.GetDocId(), rev1.GetRevId(), true);
            var expandedRev = gotRev1.CopyWithDocID(rev1.GetDocId(), rev1.GetRevId());
            Assert.IsTrue(database.ExpandAttachments(expandedRev, 0, false, true, status));
            AssertDictionariesAreEqual(attachmentDict, expandedRev.GetAttachments());

            // Add a second revision that doesn't update the attachment:
            props = new Dictionary<string, object> {
                { "_id", rev1.GetDocId() },
                { "foo", 2 },
                { "bazz", false },
                { "_attachments", CreateAttachmentsStub(testAttachmentName) }
            };
            var rev2 = database.PutRevision(new RevisionInternal(props), rev1.GetRevId(), status);
            Assert.AreEqual(StatusCode.Created, status.Code);

            // Add a third revision of the same document:
            var attach2 = Encoding.UTF8.GetBytes("<html>And this is attach2</html>");
            props = new Dictionary<string, object> {
                { "_id", rev2.GetDocId() },
                { "foo", 2 },
                { "bazz", false },
                { "_attachments", CreateAttachmentsDict(attach2, testAttachmentName, "text/html", false) }
            };
            var rev3 = database.PutRevision(new RevisionInternal(props), rev2.GetRevId(), status);
            Assert.AreEqual(StatusCode.Created, status.Code);

            // Check the second revision's attachment
            att = database.GetAttachmentForRevision(rev2, testAttachmentName, status);
            Assert.IsNotNull(att, "Couldn't get attachment:  Status {0}", status.Code);
            Assert.AreEqual(attach1, att.Content);
            Assert.AreEqual("text/plain", att.ContentType);
            Assert.AreEqual(AttachmentEncoding.None, att.Encoding);

            expandedRev = rev2.CopyWithDocID(rev2.GetDocId(), rev2.GetRevId());
            Assert.IsTrue(database.ExpandAttachments(expandedRev, 2, false, true, status));
            AssertDictionariesAreEqual(new Dictionary<string, object> { 
                { testAttachmentName, new Dictionary<string, object> { 
                        { "stub", true }, 
                        { "revpos", 1 } 
                    }
                }
            }, expandedRev.GetAttachments());

            // Check the 3rd revision's attachment:
            att = database.GetAttachmentForRevision(rev3, testAttachmentName, status);
            Assert.IsNotNull(att, "Couldn't get attachment:  Status {0}", status.Code);
            Assert.AreEqual(attach2, att.Content);
            Assert.AreEqual("text/html", att.ContentType);
            Assert.AreEqual(AttachmentEncoding.None, att.Encoding);

            expandedRev = rev3.CopyWithDocID(rev3.GetDocId(), rev3.GetRevId());
            Assert.IsTrue(database.ExpandAttachments(expandedRev, 2, false, true, status));
            attachmentDict = new Dictionary<string, object> { 
                { testAttachmentName, new Dictionary<string, object> {
                        { "content_type", "text/html" },
                        { "data", "PGh0bWw+QW5kIHRoaXMgaXMgYXR0YWNoMjwvaHRtbD4=" },
                        { "digest", "sha1-s14XRTXlwvzYfjo1t1u0rjB+ZUA=" },
                        { "length", 32 },
                        { "revpos", 3 }
                    }
                }
            };
            AssertDictionariesAreEqual(attachmentDict, expandedRev.GetAttachments());

            // Examine the attachment store:
            Assert.AreEqual(2, attachments.Count());
            Assert.AreEqual(new HashSet<BlobKey> { BlobStore.KeyForBlob(attach1), BlobStore.KeyForBlob(attach2) }, attachments.AllKeys());
            database.Compact();
            Assert.AreEqual(1, attachments.Count());
            Assert.AreEqual(new HashSet<BlobKey> { BlobStore.KeyForBlob(attach2) }, attachments.AllKeys());
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPutLargeAttachment()
        {
            /*const string testAttachmentName = "test_attachment";
            var attachments = database.Attachments;
            attachments.DeleteBlobs();
            Assert.AreEqual(0, attachments.Count());

            var status = new Status();
            var rev1Properties = new Dictionary<string, object>();
            rev1Properties["foo"] = 1;
            rev1Properties["bar"] = false;

            database.BeginTransaction();
            var largeAttachment = new StringBuilder();
            for (int i = 0; i < Database.BigAttachmentLength; i++)
            {
                largeAttachment.Append("big attachment!");
            }

            var attach1 = Encoding.UTF8.GetBytes(largeAttachment.ToString());
            rev1Properties["_attachments"] = CreateAttachmentsDict(attach1, testAttachmentName, "text/plain", false);
            var rev1 = database.PutRevision(new RevisionInternal(rev1Properties), null, false, status);
            Assert.AreEqual(StatusCode.Created, status.Code);

            database.InsertAttachmentForSequenceWithNameAndType(
                new ByteArrayInputStream(attach1), rev1.GetSequence(), 
                testAttachmentName, "text/plain", rev1.GetGeneration());
            database.EndTransaction(true);
            var attachment = database.GetAttachmentForRevision(rev1, testAttachmentName, status);
            Assert.AreEqual("text/plain", attachment.ContentType);
            var data = attachment.Content.ToArray();
            Assert.IsTrue(Arrays.Equals(attach1, data));

            const DocumentContentOptions contentOptions = DocumentContentOptions.IncludeAttachments | DocumentContentOptions.BigAttachmentsFollow;
            var gotRev1 = database.GetDocumentWithIDAndRev(rev1.GetDocId(), rev1.GetRevId(), contentOptions, status);
            var attachmentDictForSequence = gotRev1.GetAttachments();
            var innerDict = attachmentDictForSequence[testAttachmentName].AsDictionary<string, object>();
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

            //attachment.Dispose();

            var rev1WithAttachments = database.GetDocumentWithIDAndRev(
                rev1.GetDocId(), rev1.GetRevId(), contentOptions);
            
            var rev1WithAttachmentsProperties = rev1WithAttachments.GetProperties();
            var rev2Properties = new Dictionary<string, object>();
            rev2Properties.Put("_id", rev1WithAttachmentsProperties["_id"]);
            rev2Properties["foo"] = 2;
            database.BeginTransaction();
            var newRev = new RevisionInternal(rev2Properties);
            var rev2 = database.PutRevision(newRev, rev1WithAttachments.GetRevId(), false, status);
            Assert.AreEqual(StatusCode.Created, status.Code);
            //database.CopyAttachmentNamedFromSequenceToSequence(
            //    testAttachmentName, rev1WithAttachments.GetSequence(), rev2.GetSequence());
            database.EndTransaction(true);

            // Check the 2nd revision's attachment:
            var rev2FetchedAttachment = database.GetAttachmentForRevision(rev2, testAttachmentName, status);
            Assert.AreEqual(attachment.Length, rev2FetchedAttachment.Length);
            //AssertPropertiesAreEqual(attachment.Metadata, rev2FetchedAttachment.Metadata);
            Assert.AreEqual(attachment.ContentType, rev2FetchedAttachment.ContentType);
            //rev2FetchedAttachment.Dispose();

            // Add a third revision of the same document:
            var rev3Properties = new Dictionary<string, object>();
            rev3Properties.Put("_id", rev2.GetProperties().Get("_id"));
            rev3Properties["foo"] = 3;
            rev3Properties["baz"] = false;
            var rev3 = new RevisionInternal(rev3Properties);
            rev3 = database.PutRevision(rev3, rev2.GetRevId(), false, status);

            Assert.AreEqual(StatusCode.Created, status.Code);
            var attach3 = Encoding.UTF8.GetBytes("<html><blink>attach3</blink></html>");
            database.InsertAttachmentForSequenceWithNameAndType(
                new ByteArrayInputStream(attach3), rev3.GetSequence(), 
                testAttachmentName, "text/html", rev3.GetGeneration());

            // Check the 3rd revision's attachment:
            var rev3FetchedAttachment = database.GetAttachmentForRevision(rev3, testAttachmentName, status);
            data = rev3FetchedAttachment.Content.ToArray();
            Assert.IsTrue(Arrays.Equals(attach3, data));
            Assert.AreEqual("text/html", rev3FetchedAttachment.ContentType);
            //rev3FetchedAttachment.Dispose();

            // TODO: why doesn't this work?
            // Assert.assertEquals(attach3.length, rev3FetchedAttachment.getLength());
            var blobKeys = database.Attachments.AllKeys();
            Assert.AreEqual(2, blobKeys.Count);
            database.Compact();
            blobKeys = database.Attachments.AllKeys();
            Assert.AreEqual(1, blobKeys.Count);*/
        }

        [Test]
        public virtual void TestPutAttachment()
        {
            const string testAttachmentName = "test_attachment";
            var attachments = database.Attachments;
            attachments.DeleteBlobs();
            Assert.AreEqual(0, attachments.Count());

            // Put a revision that includes an _attachments dict:
            var attach1 = Encoding.UTF8.GetBytes("This is the body of attach1");
            var base64 = Convert.ToBase64String(attach1);
            var attachment = new Dictionary<string, object>();
            attachment["content_type"] = "text/plain";
            attachment["data"] = base64;

            IDictionary<string, object> attachmentDict = new Dictionary<string, object>();
            attachmentDict[testAttachmentName] = attachment;
            var properties = new Dictionary<string, object>();
            properties["foo"] = 1;
            properties["bar"] = false;
            properties["_attachments"] = attachmentDict;

            var rev1 = database.PutRevision(new RevisionInternal(properties), null, false);

            // Examine the attachment store:
            Assert.AreEqual(1, attachments.Count());
            
            // Get the revision:
            var gotRev1 = database.GetDocument(rev1.GetDocId(), 
                rev1.GetRevId(), true);
            var gotAttachmentDict = gotRev1.GetPropertyForKey("_attachments").AsDictionary<string, object>();
            gotAttachmentDict[testAttachmentName] = gotAttachmentDict[testAttachmentName].AsDictionary<string, object>();

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
            var attachv2 = Encoding.UTF8.GetBytes("Replaced body of attach");
            var writer = new BlobStoreWriter(database.Attachments);
            writer.AppendData(attachv2);
            writer.Finish();
            var gotExpectedErrorCode = false;
            try
            {
                database.UpdateAttachment(testAttachmentName, writer, "application/foo", 
                    AttachmentEncoding.None, rev1.GetDocId(), null);
            }
            catch (CouchbaseLiteException e)
            {
                gotExpectedErrorCode = (e.CBLStatus.Code == StatusCode.Conflict);
            }
            Assert.IsTrue(gotExpectedErrorCode);
            gotExpectedErrorCode = false;
            
            try
            {
                database.UpdateAttachment(testAttachmentName, new BlobStoreWriter(database.Attachments), "application/foo", 
                    AttachmentEncoding.None, rev1.GetDocId(), "1-bogus");
            }
            catch (CouchbaseLiteException e)
            {
                gotExpectedErrorCode = (e.CBLStatus.Code == StatusCode.Conflict);
            }

            Assert.IsTrue(gotExpectedErrorCode);
            gotExpectedErrorCode = false;
            RevisionInternal rev2 = null;
            try
            {
                rev2 = database.UpdateAttachment(testAttachmentName, writer, "application/foo",
                    AttachmentEncoding.None, rev1.GetDocId(), rev1.GetRevId());
            }
            catch (CouchbaseLiteException)
            {
                gotExpectedErrorCode = true;
            }
            Assert.IsFalse(gotExpectedErrorCode);
            Assert.AreEqual(rev1.GetDocId(), rev2.GetDocId());
            Assert.AreEqual(2, rev2.GetGeneration());
            // Get the updated revision:
            RevisionInternal gotRev2 = database.GetDocument(rev2.GetDocId(), rev2
                .GetRevId(), true);
            attachmentDict = gotRev2.GetProperties().Get("_attachments").AsDictionary<string, object>();
            attachmentDict[testAttachmentName] = attachmentDict[testAttachmentName].AsDictionary<string, object>();
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
                database.UpdateAttachment("nosuchattach", null, "application/foo",
                    AttachmentEncoding.None, rev2.GetDocId(), rev2.GetRevId());
            }
            catch (CouchbaseLiteException e)
            {
                gotExpectedErrorCode = (e.CBLStatus.Code == StatusCode.AttachmentNotFound);
            }
            Assert.IsTrue(gotExpectedErrorCode);
            gotExpectedErrorCode = false;
            try
            {
                database.UpdateAttachment("nosuchattach", null, null, 
                    AttachmentEncoding.None, "nosuchdoc", "nosuchrev");
            }
            catch (CouchbaseLiteException e)
            {
                gotExpectedErrorCode = (e.CBLStatus.Code == StatusCode.NotFound);
            }
            Assert.IsTrue(gotExpectedErrorCode);
            RevisionInternal rev3 = database.UpdateAttachment(testAttachmentName, null, null,
                AttachmentEncoding.None, rev2.GetDocId(), rev2.GetRevId());
            Assert.AreEqual(rev2.GetDocId(), rev3.GetDocId());
            Assert.AreEqual(3, rev3.GetGeneration());
            // Get the updated revision:
            RevisionInternal gotRev3 = database.GetDocument(rev3.GetDocId(), rev3
                .GetRevId(), true);
            attachmentDict = gotRev3.GetProperties().Get("_attachments").AsDictionary<string, object>();
            Assert.IsNull(attachmentDict);
            database.Close();
        }

        [Test]
        public void TestStreamAttachmentBlobStoreWriter()
        {
            var attachments = database.Attachments;
            var blobWriter = new BlobStoreWriter(attachments);
            var testBlob = "foo";
            blobWriter.AppendData(Encoding.UTF8.GetBytes(testBlob));
            blobWriter.Finish();

            var sha1Base64Digest = "sha1-C+7Hteo/D9vJXQ3UfzxbwnXaijM=";
            Assert.AreEqual(blobWriter.SHA1DigestString(), sha1Base64Digest);

            // install it
            blobWriter.Install();
            // look it up in blob store and make sure it's there
            var blobKey = new BlobKey(sha1Base64Digest);
            var blob = attachments.BlobForKey(blobKey);
            Assert.IsTrue(Arrays.Equals(Encoding.UTF8.GetBytes(testBlob).ToArray(), blob));
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

            var attachBodyBytes = Encoding.UTF8.GetBytes("attach body");
            var attachment = new Attachment(new MemoryStream(attachBodyBytes), "text/plain");
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
                // Cleanup
                attachmentRetrieved.Dispose();
            }
            // Cleanup.
            attachment.Dispose();
        }

        [Test]
        public void TestAttachmentDisappearsAfterSave()
        {
            var doc = database.CreateDocument();
            var content = "This is a test attachment!";
            var body = Encoding.UTF8.GetBytes(content);
            var rev = doc.CreateRevision();
            rev.SetAttachment("index.html", "text/plain; charset=utf-8", body);
            rev.Save();

            // make sure the doc's latest revision has the attachment
            var attachments = doc.CurrentRevision.GetProperty("_attachments").AsDictionary<string, object>();
            Assert.IsNotNull(attachments);
            Assert.AreEqual(1, attachments.Count);

            var rev2 = doc.CreateRevision();
            rev2.Properties.Add("foo", "bar");
            rev2.Save();
            attachments = rev2.GetProperty("_attachments").AsDictionary<string, object>();
            Assert.IsNotNull(attachments);
            Assert.AreEqual(1, attachments.Count);
        }
    }
}
