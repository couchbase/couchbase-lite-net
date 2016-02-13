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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class AttachmentsTest : LiteTestCase
    {
        private const string TAG = "AttachmentsTest";

        public AttachmentsTest(string storageType) : base(storageType) {}

        [Test]
        public void TestPutEncodedAttachment()
        {
            const string bodyString = "This is the body of attach1";
            var rev1 = PutDocument(null, bodyString, true);
            var resultDict = new Dictionary<string, object> { 
                { "attach", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "digest", "sha1-Wk8g89eb0Y+5DtvMKkf+/g90Mhc=" },
                        { "length", 27 },
                        { "encoded_length", 45 },
                        { "encoding", "gzip" },
                        { "stub", true },
                        { "revpos", 1 }
                    }
                }
            };
            AssertDictionariesAreEqual(resultDict, rev1.GetAttachments());

            // Examine the attachment store:
            Assert.AreEqual(1, database.Attachments.Count());

            // Get the revision:
            var gotRev1 = database.GetDocument(rev1.GetDocId(), rev1.GetRevId(), true);
            var attachmentsDict = gotRev1.GetAttachments();
            AssertDictionariesAreEqual(resultDict, attachmentsDict);

            // Expand it without decoding:
            var expandedRev = gotRev1.CopyWithDocID(gotRev1.GetDocId(), gotRev1.GetRevId());
            Assert.DoesNotThrow(() => database.ExpandAttachments(expandedRev, 0, false, false));
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(bodyString).Compress().ToArray());
            AssertDictionariesAreEqual(new Dictionary<string, object> { { "attach", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "digest", "sha1-Wk8g89eb0Y+5DtvMKkf+/g90Mhc=" },
                        { "length", 27 },
                        { "encoded_length", 45 },
                        { "encoding", "gzip" },
                        { "data", encoded },
                        { "revpos", 1 }
                    }
                }
            }, expandedRev.GetAttachments());

            // Expand it and decode:
            expandedRev = gotRev1.CopyWithDocID(gotRev1.GetDocId(), gotRev1.GetRevId());
            Assert.DoesNotThrow(() => database.ExpandAttachments(expandedRev, 0, false, true));
            encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(bodyString));
            AssertDictionariesAreEqual(new Dictionary<string, object> { { "attach", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "digest", "sha1-Wk8g89eb0Y+5DtvMKkf+/g90Mhc=" },
                        { "length", 27 },
                        { "data", encoded },
                        { "revpos", 1 }
                    }
                }
            }, expandedRev.GetAttachments());
        }

        [Test]
        public void TestFollowWithRevPos()
        {
            var attachInfo = new Dictionary<string, object> {
                { "content_type", "text/plain" },
                { "digest", "md5-DaUdFsLh8FKLbcBIDlU57g==" },
                { "follows", true },
                { "length", 51200 },
                { "revpos", 2 }
            };

            var attachment = default(AttachmentInternal);
            Assert.DoesNotThrow(() => attachment = new AttachmentInternal("attachment", attachInfo));
            var stub = attachment.AsStubDictionary();

            var expected = new Dictionary<string, object> {
                { "content_type", "text/plain" },
                { "digest", "sha1-AAAAAAAAAAAAAAAAAAAAAAAAAAA=" },
                { "stub", true },
                { "length", 51200 },
                { "revpos", 2 }
            };

            AssertDictionariesAreEqual(expected, stub);
        }

        [Test]
        public void TestIntermediateDeletedRevs()
        {
            // Put a revision that includes an _attachments dict:
            var attach1 = Encoding.UTF8.GetBytes("This is the body of attach1");
            var base64 = Convert.ToBase64String(attach1);

            var attachDict = new Dictionary<string, object> { 
                { "attach", new Dictionary<string, object> { 
                        { "content_type", "text/plain"},
                        { "data", base64 }
                    }
                }
            };

            IDictionary<string, object> props = new Dictionary<string, object> {
                { "_id", "X" },
                { "_attachments", attachDict }
            };
                
            var rev1 = default(RevisionInternal);
            Assert.DoesNotThrow(() => rev1 = database.PutRevision(new RevisionInternal(props), null, false));
            Assert.AreEqual(1L, rev1.GetAttachments().GetCast<IDictionary<string, object>>("attach").GetCast<long>("revpos"));

            props = new Dictionary<string, object> {
                { "_id", rev1.GetDocId() },
                { "_deleted", true }
            };

            var rev2 = default(RevisionInternal);
            Assert.DoesNotThrow(() => rev2 = database.PutRevision(new RevisionInternal(props), rev1.GetRevId()));
            Assert.IsTrue(rev2.IsDeleted());

            // Insert a revision several generations advanced but which hasn't changed the attachment:
            var rev3 = rev1.CopyWithDocID(rev1.GetDocId(), "3-3333");
            props = rev3.GetProperties();
            props["foo"] = "bar";
            rev3.SetProperties(props);
            rev3.MutateAttachments((name, att) =>
            {
                var nuAtt = new Dictionary<string, object>(att);
                nuAtt.Remove("data");
                nuAtt["stub"] = true;
                nuAtt["digest"] = "md5-deadbeef";
                return nuAtt;
            });

            var history = new List<string> { rev3.GetRevId(), rev2.GetRevId(), rev1.GetRevId() };
            Assert.DoesNotThrow(() => database.ForceInsert(rev3, history, null));
        }

        [Test]
        public void TestUpgradeMD5()
        {
            var store = database.Storage as SqliteCouchStore;
            if (store == null) {
                Assert.Inconclusive("This test is only valid for a SQLite based store, since any others will be too new to see this issue");
            }

            var authorizationHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("jim:borden")));
            using (var client = new HttpClient()) {
                var couchDbUri = String.Format("http://{0}:5984/", GetReplicationServer());

                try {
                    var request = new HttpRequestMessage(HttpMethod.Get, new Uri(couchDbUri));
                    client.SendAsync(request).Wait();
                } catch (Exception) {
                    Assert.Inconclusive("Apache CouchDB not running");
                }

                var dbName = "a" + Guid.NewGuid();
                var putRequest = new HttpRequestMessage(HttpMethod.Put, new Uri(couchDbUri + dbName));
                putRequest.Headers.Authorization = authorizationHeader;
                var response = client.SendAsync(putRequest).Result;
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

                var baseEndpoint = String.Format("http://{0}:5984/{1}/{2}", GetReplicationServer(), dbName, docName);
                var endpoint = baseEndpoint;
                putRequest = new HttpRequestMessage(HttpMethod.Put, new Uri(endpoint));
                putRequest.Headers.Authorization = authorizationHeader;
                putRequest.Content = new StringContent("{\"foo\":false}");
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = client.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);


                attachmentStream = (InputStream)GetAsset("attachment.png");
                var baos = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(baos);
                attachmentStream.Dispose();
                endpoint = baseEndpoint + "/attachment?rev=1-1153b140e4c8674e2e6425c94de860a0";

                putRequest = new HttpRequestMessage(HttpMethod.Put, new Uri(endpoint));
                putRequest.Content = new ByteArrayContent(baos.ToArray());
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                putRequest.Headers.Authorization = authorizationHeader;
                baos.Dispose();
                response = client.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                endpoint = baseEndpoint + "?rev=2-bb71ce0da1de19f848177525c4ae5a8b";
                putRequest = new HttpRequestMessage(HttpMethod.Put, new Uri(endpoint));
                putRequest.Content = new StringContent("{\"foo\":true,\"_attachments\":{\"attachment\":{\"content_type\":\"image/png\",\"revpos\":2,\"digest\":\"md5-ks1IBwCXbuY7VWAO9CkEjA==\",\"length\":519173,\"stub\":true}}}");
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                putRequest.Headers.Authorization = authorizationHeader;
                response = client.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                var pull = database.CreatePullReplication(new Uri(couchDbUri + dbName));
                pull.Continuous = true;
                pull.Start();

                endpoint = baseEndpoint + "?rev=3-a020d6aae370ab5cbc136c477f4e5928";
                putRequest = new HttpRequestMessage(HttpMethod.Put, new Uri(endpoint));
                putRequest.Content = new StringContent("{\"foo\":false,\"_attachments\":{\"attachment\":{\"content_type\":\"image/png\",\"revpos\":2,\"digest\":\"md5-ks1IBwCXbuY7VWAO9CkEjA==\",\"length\":519173,\"stub\":true}}}");
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                putRequest.Headers.Authorization = authorizationHeader;
                response = client.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                Sleep(1000);
                while (pull.Status == ReplicationStatus.Active) {
                    Sleep(500);
                }

                var doc = database.GetExistingDocument(docName);
                Assert.AreEqual("4-a91f8875144c6162874371c07a08ea17", doc.CurrentRevisionId);
                var attachment = doc.CurrentRevision.Attachments.ElementAtOrDefault(0);
                Assert.IsNotNull(attachment);
                var attachmentsDict = doc.GetProperty("_attachments").AsDictionary<string, object>();
                var attachmentDict = attachmentsDict.Get("attachment").AsDictionary<string, object>();
                Assert.AreEqual("md5-ks1IBwCXbuY7VWAO9CkEjA==", attachmentDict["digest"]);

                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, baseEndpoint + "/attachment?rev=4-a91f8875144c6162874371c07a08ea17");
                deleteRequest.Headers.Authorization = authorizationHeader;
                response = client.SendAsync(deleteRequest).Result;
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                attachmentStream = (InputStream)GetAsset("attachment2.png");
                baos = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(baos);
                attachmentStream.Dispose();
                endpoint = baseEndpoint + "/attachment?rev=5-4737cb66c6a7ef1b11e872cb6fa4d51a";

                putRequest = new HttpRequestMessage(HttpMethod.Put, endpoint);
                putRequest.Content = new ByteArrayContent(baos.ToArray());
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                putRequest.Headers.Authorization = authorizationHeader;
                baos.Dispose();
                response = client.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                Sleep(1000);
                while (pull.Status == ReplicationStatus.Active) {
                    Sleep(500);
                }

                doc = database.GetExistingDocument(docName);
                Assert.AreEqual("6-e3a7423a9a9de094a0d12d7f3b44634c", doc.CurrentRevisionId);
                attachment = doc.CurrentRevision.Attachments.ElementAtOrDefault(0);
                Assert.IsNotNull(attachment);
                attachmentsDict = doc.GetProperty("_attachments").AsDictionary<string, object>();
                attachmentDict = attachmentsDict.Get("attachment").AsDictionary<string, object>();
                Assert.AreEqual("sha1-9ijdmMf0mK7c11WQPw7DBQcX5pE=", attachmentDict["digest"]);

                deleteRequest = new HttpRequestMessage(HttpMethod.Delete, couchDbUri + dbName);
                deleteRequest.Headers.Authorization = authorizationHeader;
                response = client.SendAsync(deleteRequest).Result;
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
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
                
            RevisionInternal rev1 = database.PutRevision(new RevisionInternal(props), null, false);

            var att = database.GetAttachmentForRevision(rev1, testAttachmentName);
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
            Assert.DoesNotThrow(() => database.ExpandAttachments(expandedRev, 0, false, true));
            AssertDictionariesAreEqual(attachmentDict, expandedRev.GetAttachments());

            // Add a second revision that doesn't update the attachment:
            props = new Dictionary<string, object> {
                { "_id", rev1.GetDocId() },
                { "foo", 2 },
                { "bazz", false },
                { "_attachments", CreateAttachmentsStub(testAttachmentName) }
            };
            var rev2 = database.PutRevision(new RevisionInternal(props), rev1.GetRevId());

            // Add a third revision of the same document:
            var attach2 = Encoding.UTF8.GetBytes("<html>And this is attach2</html>");
            props = new Dictionary<string, object> {
                { "_id", rev2.GetDocId() },
                { "foo", 2 },
                { "bazz", false },
                { "_attachments", CreateAttachmentsDict(attach2, testAttachmentName, "text/html", false) }
            };
            var rev3 = database.PutRevision(new RevisionInternal(props), rev2.GetRevId());

            // Check the second revision's attachment
            att = database.GetAttachmentForRevision(rev2, testAttachmentName);
            Assert.AreEqual(attach1, att.Content);
            Assert.AreEqual("text/plain", att.ContentType);
            Assert.AreEqual(AttachmentEncoding.None, att.Encoding);

            expandedRev = rev2.CopyWithDocID(rev2.GetDocId(), rev2.GetRevId());
            Assert.DoesNotThrow(() => database.ExpandAttachments(expandedRev, 2, false, true));
            AssertDictionariesAreEqual(new Dictionary<string, object> { { testAttachmentName, new Dictionary<string, object> { 
                        { "stub", true }, 
                        { "revpos", 1 } 
                    }
                }
            }, expandedRev.GetAttachments());

            // Check the 3rd revision's attachment:
            att = database.GetAttachmentForRevision(rev3, testAttachmentName);
            Assert.AreEqual(attach2, att.Content);
            Assert.AreEqual("text/html", att.ContentType);
            Assert.AreEqual(AttachmentEncoding.None, att.Encoding);

            expandedRev = rev3.CopyWithDocID(rev3.GetDocId(), rev3.GetRevId());
            Assert.DoesNotThrow(() => database.ExpandAttachments(expandedRev, 2, false, true));
            attachmentDict = new Dictionary<string, object> { { testAttachmentName, new Dictionary<string, object> {
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

        [Test]
        public virtual void TestPutAttachment()
        {
            // Put a revision that includes an _attachments dict:
            var rev1 = PutDocument(null, "This is the body of attach1", false);
            var resultDict = new Dictionary<string, object> { 
                { "attach", new Dictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "digest", "sha1-gOHUOBmIMoDCrMuGyaLWzf1hQTE=" },
                        { "length", 27 },
                        { "stub", true },
                        { "revpos", 1 }
                    }
                }
            };
            AssertDictionariesAreEqual(resultDict, rev1.GetAttachments());

            // Examine the attachment store:
            Assert.AreEqual(1, database.Attachments.Count());

            // Get the revision:
            var gotRev1 = database.GetDocument(rev1.GetDocId(), rev1.GetRevId(), true);
            var attachmentDict = gotRev1.GetAttachments();
            AssertDictionariesAreEqual(resultDict, attachmentDict);

            // Update the attachment directly:
            var attachv2 = Encoding.UTF8.GetBytes("Replaced body of attach");

            var ex = Assert.Throws<CouchbaseLiteException>(() => database.UpdateAttachment("attach", BlobForData(database, attachv2), 
                "application/foo", AttachmentEncoding.None, rev1.GetDocId(), null, null));
            Assert.AreEqual(StatusCode.Conflict, ex.Code);

            ex = Assert.Throws<CouchbaseLiteException>(() => database.UpdateAttachment("attach", BlobForData(database, attachv2), 
                "application/foo", AttachmentEncoding.None, rev1.GetDocId(), "1-deadbeef", null));
            Assert.AreEqual(StatusCode.Conflict, ex.Code);

            var rev2 = default(RevisionInternal);
            Assert.DoesNotThrow(() => rev2 = database.UpdateAttachment("attach", BlobForData(database, attachv2), 
                "application/foo", AttachmentEncoding.None, rev1.GetDocId(), rev1.GetRevId(), null));
            Assert.AreEqual(rev1.GetDocId(), rev2.GetDocId());
            Assert.AreEqual(2, rev2.GetGeneration());

            // Get the updated revision:
            var gotRev2 = database.GetDocument(rev2.GetDocId(), rev2.GetRevId(), true);
            attachmentDict = gotRev2.GetAttachments();
            AssertDictionariesAreEqual(new Dictionary<string, object> { 
                { "attach", new Dictionary<string, object> {
                        { "content_type", "application/foo" },
                        { "digest", "sha1-mbT3208HI3PZgbG4zYWbDW2HsPk=" },
                        { "length", 23 },
                        { "stub", true },
                        { "revpos", 2 }
                    }
                }
            }, attachmentDict);
             
            var gotAttach = database.GetAttachmentForRevision(gotRev2, "attach");
            Assert.IsNotNull(gotAttach);
            Assert.AreEqual(attachv2, gotAttach.Content);

            // Delete the attachment:
            ex = Assert.Throws<CouchbaseLiteException>(() => database.UpdateAttachment("nosuchattach", null, 
                null, AttachmentEncoding.None, rev2.GetDocId(), rev2.GetRevId(), null));
            Assert.AreEqual(StatusCode.AttachmentNotFound, ex.Code);

            ex = Assert.Throws<CouchbaseLiteException>(() => database.UpdateAttachment("nosuchattach", null, 
                null, AttachmentEncoding.None, "nosuchdoc", "nosuchrev"));
            Assert.AreEqual(StatusCode.NotFound, ex.Code);

            var rev3 = default(RevisionInternal);
            Assert.DoesNotThrow(() => rev3 = database.UpdateAttachment("attach", null, 
                null, AttachmentEncoding.None, rev2.GetDocId(), rev2.GetRevId(), null));
            Assert.AreEqual(rev2.GetDocId(), rev3.GetDocId());
            Assert.AreEqual(3, rev3.GetGeneration());

            // Get the updated revision:
            var gotRev3 = database.GetDocument(rev3.GetDocId(), rev3.GetRevId(), true);
            Assert.IsNull(gotRev3.GetPropertyForKey("_attachments"));
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
            foreach(var row in results)
            {
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

        private RevisionInternal PutDocument(string docId, string attachment, bool compress)
        {
            var attachmentData = Encoding.UTF8.GetBytes(attachment);
            var encoding = default(string);
            var length = 0;
            if (compress) {
                length = attachmentData.Length;
                encoding = "gzip";
                attachmentData = attachmentData.Compress().ToArray();
            }

            var base64 = Convert.ToBase64String(attachmentData);
            var attachmentDict = new Dictionary<string, object> {
                { "attach", new NonNullDictionary<string, object> {
                        { "content_type", "text/plain" },
                        { "data", base64 },
                        { "encoding", encoding },
                        { "length", length }
                    }
                }
            };

            var props = new Dictionary<string, object> {
                { "_id", docId },
                { "foo", 1 },
                { "bar", false },
                { "_attachments", attachmentDict }
            };

            var rev = default(RevisionInternal);
            Assert.DoesNotThrow(() => rev = database.PutRevision(new RevisionInternal(props), null, false));
            return rev;
        }

        private BlobStoreWriter BlobForData(Database db, byte[] data)
        {
            var blob = db.AttachmentWriter;
            blob.AppendData(data);
            blob.Finish();
            return blob;
        }
    }
}