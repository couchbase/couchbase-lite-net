//
// ReplicationTest.cs
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
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;

using NUnit.Framework;

using Sharpen;

using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using Couchbase.Lite.Tests;
using Newtonsoft.Json.Linq;
using System.Threading;


namespace Couchbase.Lite
{
    public class ReplicationTest : LiteTestCase
    {
        new const string Tag = "ReplicationTest";

        private static CountdownEvent ReplicationWatcherThread(Replication replication)
        {
            var started = replication.IsRunning;
            var doneSignal = new CountdownEvent(1);

            Task.Factory.StartNew(()=>
            {
                var done = false;
                while (!done)
                {
                    if (replication.IsRunning)
                    {
                        started = true;
                    }

                    var statusIsDone = (
                        replication.Status == ReplicationStatus.Stopped 
                        || replication.Status == ReplicationStatus.Idle
                    );

                    if (started && statusIsDone)
                    {
                        done = true;
                    }

                    try
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                    catch (Exception e)
                    {
                        Runtime.PrintStackTrace(e);
                    }
                }
                doneSignal.Signal();
            });

            return doneSignal;
        }

        private void RunReplication(Replication replication)
        {
            var replicationDoneSignal = new CountDownLatch(1);
            var observer = new ReplicationObserver(replicationDoneSignal);
            replication.Changed += observer.Changed;
            replication.Start();

            var replicationDoneSignalPolling = ReplicationWatcherThread(replication);

            Log.D(Tag, "Waiting for replicator to finish.");

                var success = replicationDoneSignal.Await(TimeSpan.FromSeconds(15));
                Assert.IsTrue(success);
                success = replicationDoneSignalPolling.Wait(TimeSpan.FromSeconds(15));
                Assert.IsTrue(success);

                Log.D(Tag, "replicator finished");

            replication.Changed -= observer.Changed;
        }

        private void WorkaroundSyncGatewayRaceCondition() 
        {
            System.Threading.Thread.Sleep(2 * 1000);
        }

        private void PutReplicationOffline(Replication replication)
        {
            var doneEvent = new ManualResetEvent(false);
            replication.Changed += (object sender, ReplicationChangeEventArgs e) => 
            {
                if (!e.Source.online)
                {
                    doneEvent.Set();
                }
            };

            replication.GoOffline();

            var success = doneEvent.WaitOne(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
        }

        private Boolean IsSyncGateway(Uri remote) 
        {
            return (remote.Port == 4984 || remote.Port == 4985);
        }

        private void VerifyRemoteDocExists(Uri remote, string docId)
        {
            var replicationUrlTrailing = new Uri(string.Format("{0}/", remote));
            var pathToDoc = new Uri(replicationUrlTrailing, docId);
            Log.D(Tag, "Send http request to " + pathToDoc);

            var httpRequestDoneSignal = new CountDownLatch(1);
            Task.Factory.StartNew(() =>
            {
                var httpclient = new HttpClient();
                HttpResponseMessage response;
                string responseString = null;
                try
                {
                    var responseTask = httpclient.GetAsync(pathToDoc.ToString());
                    responseTask.Wait(TimeSpan.FromSeconds(1));
                    response = responseTask.Result;
                    var statusLine = response.StatusCode;
                    Assert.IsTrue(statusLine == HttpStatusCode.OK);
                    if (statusLine == HttpStatusCode.OK)
                    {
                        var responseStringTask = response.Content.ReadAsStringAsync();
                        responseStringTask.Wait(TimeSpan.FromSeconds(10));
                        responseString = responseStringTask.Result;
                        Assert.IsTrue(responseString.Contains(docId));
                        Log.D(ReplicationTest.Tag, "result: " + responseString);
                    }
                    else
                    {
                        var statusReason = response.ReasonPhrase;
                        response.Dispose();
                        throw new IOException(statusReason);
                    }
                }
                catch (ProtocolViolationException e)
                {
                    Assert.IsNull(e, "Got ClientProtocolException: " + e.Message);
                }
                catch (IOException e)
                {
                    Assert.IsNull(e, "Got IOException: " + e.Message);
                }

                httpRequestDoneSignal.CountDown();
            });

            var result = httpRequestDoneSignal.Await(TimeSpan.FromSeconds(10));
            Assert.IsTrue(result, "Could not retrieve the new doc from the sync gateway.");
        }

        private IDictionary<string, object> GetRemoteDoc(Uri remote, string checkpointId)
        {
            var url = new Uri(string.Format("{0}/_local/{1}", remote, checkpointId));
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = new HttpClient().SendAsync(request).Result;
            var result = response.Content.ReadAsStringAsync().Result;
            var json = Manager.GetObjectMapper().ReadValue<JObject>(result);
            return json.AsDictionary<string, object>();
        }

        [SetUp]
        public void Setup()
        {
            Log.V(Tag, "------");
        }

        // Reproduces issue #167
        // https://github.com/couchbase/couchbase-lite-android/issues/167
        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPushPurgedDoc()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var numBulkDocRequests = 0;
            HttpRequestMessage lastBulkDocsRequest = null;

            var doc = CreateDocumentWithProperties(
                database, 
                new Dictionary<string, object>
                {
                    {"testName", "testPurgeDocument"}
                }
            );
            Assert.IsNotNull(doc);

            var remote = GetReplicationURL();
            var factory = new MockHttpClientFactory();
            factory.HttpHandler.ClearResponders();
            factory.HttpHandler.AddResponderRevDiffsAllMissing();
            factory.HttpHandler.AddResponderFakeLocalDocumentUpdate404();
            factory.HttpHandler.AddResponderFakeBulkDocs();
            manager.DefaultHttpClientFactory = factory;

            var pusher = database.CreatePushReplication(remote);
            pusher.Continuous = true;

            var replicationCaughtUpSignal = new CountdownEvent(1);
            pusher.Changed += (sender, e) => 
            {
                var changesCount = e.Source.ChangesCount;
                var completedChangesCount = e.Source.CompletedChangesCount;
                var msg = "changes: {0} completed changes: {1}".Fmt(changesCount, completedChangesCount);
                Log.D(Tag, msg);
                if (changesCount == completedChangesCount 
                 && changesCount != 0
                 && replicationCaughtUpSignal.CurrentCount > 0)
                {
                    replicationCaughtUpSignal.Signal();
                }
            };
            pusher.Start();

            // wait until that doc is pushed
            var didNotTimeOut = replicationCaughtUpSignal.Wait(TimeSpan.FromSeconds(5));
            Assert.IsTrue(didNotTimeOut);

            // at this point, we should have captured exactly 1 bulk docs request
            numBulkDocRequests = 0;

            var handler = factory.HttpHandler;

            foreach (var capturedRequest in handler.CapturedRequests)
            {
                if (capturedRequest.Method == HttpMethod.Post && capturedRequest.RequestUri.AbsoluteUri.EndsWith("_bulk_docs", StringComparison.Ordinal))
                {
                    lastBulkDocsRequest = capturedRequest;
                    numBulkDocRequests++;
                }
            }

            Assert.AreEqual(1, numBulkDocRequests);

            // that bulk docs request should have the "start" key under its _revisions
            var jsonMap = MockHttpRequestHandler.GetJsonMapFromRequest(lastBulkDocsRequest);
            var docs = ((JArray)jsonMap.Get("docs")).ToObject<IList<IDictionary<string,object>>>();
            var onlyDoc = docs[0];
            var revisions = ((JObject)onlyDoc.Get("_revisions")).ToObject<IDictionary<string,object>>();
            Assert.IsTrue(revisions.ContainsKey("start"));

            // Reset for the next attempt.
            handler.ClearCapturedRequests();

            // now add a new revision, which will trigger the pusher to try to push it
            var properties = new Dictionary<string, object>();
            properties.Put("testName2", "update doc");

            var unsavedRevision = doc.CreateRevision();
            unsavedRevision.SetUserProperties(properties);
            unsavedRevision.Save();

            // but then immediately purge it
            doc.Purge();

            // wait for a while to give the replicator a chance to push it
            // (it should not actually push anything)
            System.Threading.Thread.Sleep(5 * 1000);

            // we should not have gotten any more _bulk_docs requests, because
            // the replicator should not have pushed anything else.
            // (in the case of the bug, it was trying to push the purged revision)
            numBulkDocRequests = 0;
            foreach (var capturedRequest in handler.CapturedRequests)
            {
                if (capturedRequest.Method == HttpMethod.Post && capturedRequest.RequestUri.AbsoluteUri.EndsWith("_bulk_docs", StringComparison.Ordinal))
                {
                    numBulkDocRequests++;
                }
            }

            Assert.AreEqual(1, numBulkDocRequests);
            pusher.Stop();
        }


        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPusher()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var remote = GetReplicationURL();
            var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());

            // Create some documents:
            var documentProperties = new Dictionary<string, object>();
            var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
            documentProperties["_id"] = doc1Id;
            documentProperties["foo"] = 1;
            documentProperties["bar"] = false;

            var body = new Body(documentProperties);
            var rev1 = new RevisionInternal(body, database);
            var status = new Status();
            rev1 = database.PutRevision(rev1, null, false, status);
            Assert.AreEqual(StatusCode.Created, status.GetCode());

            documentProperties.Put("_rev", rev1.GetRevId());
            documentProperties["UPDATED"] = true;
            database.PutRevision(new RevisionInternal(documentProperties, database), rev1.GetRevId(), false, status);
            Assert.AreEqual(StatusCode.Created, status.GetCode());

            documentProperties = new Dictionary<string, object>();
            var doc2Id = string.Format("doc2-{0}", docIdTimestamp);
            documentProperties["_id"] = doc2Id;
            documentProperties["baz"] = 666;
            documentProperties["fnord"] = true;

            database.PutRevision(new RevisionInternal(documentProperties, database), null, false, status);
            Assert.AreEqual(StatusCode.Created, status.GetCode());

            var doc2 = database.GetDocument(doc2Id);
            var doc2UnsavedRev = doc2.CreateRevision();
            var attachmentStream = GetAsset("attachment.png");
            doc2UnsavedRev.SetAttachment("attachment.png", "image/png", attachmentStream);
            var doc2Rev = doc2UnsavedRev.Save();

            Assert.IsNotNull(doc2Rev);

            const bool continuous = false;
            var repl = database.CreatePushReplication(remote);
            repl.Continuous = continuous;
            if (!IsSyncGateway(remote)) {
                repl.CreateTarget = true;
            }

            // Check the replication's properties:
            Assert.AreEqual(database, repl.LocalDatabase);
            Assert.AreEqual(remote, repl.RemoteUrl);
            Assert.IsFalse(repl.IsPull);
            Assert.IsFalse(repl.Continuous);
            Assert.IsNull(repl.Filter);
            Assert.IsNull(repl.FilterParams);
            Assert.IsNull(repl.DocIds);
            // TODO: CAssertNil(r1.headers); still not null!
            // Check that the replication hasn't started running:
            Assert.IsFalse(repl.IsRunning);
            Assert.AreEqual((int)repl.Status, (int)ReplicationStatus.Stopped);
            Assert.AreEqual(0, repl.CompletedChangesCount);
            Assert.AreEqual(0, repl.ChangesCount);
            Assert.IsNull(repl.LastError);

            RunReplication(repl);

            // TODO: Verify the foloowing 2 asserts. ChangesCount and CompletedChangesCount
            // should already be reset when the replicator stopped.
             Assert.IsTrue(repl.ChangesCount >= 2);
             Assert.IsTrue(repl.CompletedChangesCount >= 2);
            Assert.IsNull(repl.LastError);

            VerifyRemoteDocExists(remote, doc1Id);

            // Add doc3
            documentProperties = new Dictionary<string, object>();
            var doc3Id = string.Format("doc3-{0}", docIdTimestamp);
            var doc3 = database.GetDocument(doc3Id);
            documentProperties["bat"] = 677;
            doc3.PutProperties(documentProperties);

            // re-run push replication
            var repl2 = database.CreatePushReplication(remote);
            repl2.Continuous = continuous;
            if (!IsSyncGateway(remote))
            {
                repl2.CreateTarget = true;
            }

            var repl2CheckedpointId = repl2.RemoteCheckpointDocID();

            RunReplication(repl2);

            Assert.IsNull(repl2.LastError);

            // make sure trhe doc has been added
            VerifyRemoteDocExists(remote, doc3Id);

            Assert.AreEqual(repl2.LastSequence, database.LastSequenceWithCheckpointId(repl2CheckedpointId));

            System.Threading.Thread.Sleep(2000);
            var json = GetRemoteDoc(remote, repl2CheckedpointId);
            var remoteLastSequence = (string)json["lastSequence"];
            Assert.AreEqual(repl2.LastSequence, remoteLastSequence);

            Log.D(Tag, "testPusher() finished");
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPusherDeletedDoc()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var remote = GetReplicationURL();
            var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());

            // Create some documentsConvert
            var documentProperties = new Dictionary<string, object>();
            var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
            documentProperties["_id"] = doc1Id;
            documentProperties["foo"] = 1;
            documentProperties["bar"] = false;

            var body = new Body(documentProperties);
            var rev1 = new RevisionInternal(body, database);
            var status = new Status();
            rev1 = database.PutRevision(rev1, null, false, status);
            Assert.AreEqual(StatusCode.Created, status.GetCode());

            documentProperties["_rev"] = rev1.GetRevId();
            documentProperties["UPDATED"] = true;
            documentProperties["_deleted"] = true;
            database.PutRevision(new RevisionInternal(documentProperties, database), rev1.GetRevId(), false, status);
            Assert.IsTrue((int)status.GetCode() >= 200 && (int)status.GetCode() < 300);

            var repl = database.CreatePushReplication(remote);
            if (!IsSyncGateway(remote)) {
                ((Pusher)repl).CreateTarget = true;
            }

            RunReplication(repl);

            Assert.IsNull(repl.LastError);

            // make sure doc1 is deleted
            var replicationUrlTrailing = new Uri(string.Format ("{0}/", remote));
            var pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
            Log.D(Tag, "Send http request to " + pathToDoc);
            var httpRequestDoneSignal = new CountDownLatch(1);
                var httpclient = new HttpClient();
                try
                {
                    var getDocResponse = httpclient.GetAsync(pathToDoc.ToString()).Result;
                    var statusLine = getDocResponse.StatusCode;
                    Log.D(ReplicationTest.Tag, "statusLine " + statusLine);
                    Assert.AreEqual(Couchbase.Lite.StatusCode.NotFound, statusLine.GetStatusCode());                        
                }
                catch (ProtocolViolationException e)
                {
                    Assert.IsNull(e, "Got ClientProtocolException: " + e.Message);
                }
                catch (IOException e)
                {
                    Assert.IsNull(e, "Got IOException: " + e.Message);
                }
                finally
                {
                    httpRequestDoneSignal.CountDown();
                }
            Log.D(Tag, "Waiting for http request to finish");
            try
            {
                httpRequestDoneSignal.Await(TimeSpan.FromSeconds(10));
                Log.D(Tag, "http request finished");
            }
            catch (Exception e)
            {
                Runtime.PrintStackTrace(e);
            }
            Log.D(Tag, "testPusherDeletedDoc() finished");
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPuller()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());
            var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
            var doc2Id = string.Format("doc2-{0}", docIdTimestamp);

            Log.D(Tag, "Adding " + doc1Id + " directly to sync gateway");           
            AddDocWithId(doc1Id, "attachment.png");

            Log.D(Tag, "Adding " + doc2Id + " directly to sync gateway");
            AddDocWithId(doc2Id, "attachment2.png");

            DoPullReplication();
            Assert.IsNotNull(database);

            Log.D(Tag, "Fetching doc1 via id: " + doc1Id);
            var doc1 = database.GetExistingDocument(doc1Id);
            Log.D(Tag, "doc1" + doc1);
            Assert.IsNotNull(doc1);
            Assert.IsNotNull(doc1.CurrentRevisionId);
            Assert.IsTrue(doc1.CurrentRevisionId.StartsWith("1-", StringComparison.Ordinal));
            Assert.IsNotNull(doc1.Properties);
            Assert.AreEqual(1, doc1.GetProperty("foo"));

            Log.D(Tag, "Fetching doc2 via id: " + doc2Id);
            var doc2 = database.GetExistingDocument(doc2Id);
            Assert.IsNotNull(doc2);
            Assert.IsNotNull(doc2.CurrentRevisionId);
            Assert.IsTrue(doc2.CurrentRevisionId.StartsWith("1-", StringComparison.Ordinal));
            Assert.IsNotNull(doc2.Properties);
            Assert.AreEqual(1, doc2.GetProperty("foo"));
            Log.D(Tag, "testPuller() finished");
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPullerWithLiveQuery()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            // Even though this test is passed, there is a runtime exception
            // thrown regarding the replication's number of changes count versus
            // number of completed changes count. Investigation is required.
            Log.D(Database.Tag, "testPullerWithLiveQuery");
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            string doc1Id = string.Format("doc1-{0}", docIdTimestamp);
            string doc2Id = string.Format("doc2-{0}", docIdTimestamp);

            AddDocWithId(doc1Id, "attachment2.png");
            AddDocWithId(doc2Id, "attachment2.png");

            int numDocsBeforePull = database.DocumentCount;
            View view = database.GetView("testPullerWithLiveQueryView");
            view.SetMapReduce((document, emitter) => {
                if (document.Get ("_id") != null) {
                    emitter (document.Get ("_id"), null);
                }
            }, null, "1");

            LiveQuery allDocsLiveQuery = view.CreateQuery().ToLiveQuery();
            allDocsLiveQuery.Changed += (sender, e) => {
                int numTimesCalled = 0;
                if (e.Error != null)
                {
                    throw new RuntimeException(e.Error);
                }
                if (numTimesCalled++ > 0)
                {
                    NUnit.Framework.Assert.IsTrue(e.Rows.Count > numDocsBeforePull);
                }
                Log.D(Database.Tag, "rows " + e.Rows);
            };

            // the first time this is called back, the rows will be empty.
            // but on subsequent times we should expect to get a non empty
            // row set.
            allDocsLiveQuery.Start();
            DoPullReplication();
            allDocsLiveQuery.Stop();
        }

        private void DoPullReplication()
        {
            var remote = GetReplicationURL();
            var repl = database.CreatePullReplication(remote);
            repl.Continuous = false;
            RunReplication(repl);
            Assert.IsNull(repl.LastError);
        }

        /// <exception cref="System.IO.IOException"></exception>
        private void AddDocWithId(string docId, string attachmentName)
        {
            string docJson;
            if (attachmentName != null)
            {
                // add attachment to document
                var attachmentStream = (InputStream)GetAsset(attachmentName);
                var baos = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(baos);
                var attachmentBase64 = Convert.ToBase64String(baos.ToArray());
                docJson = String.Format("{{\"foo\":1,\"bar\":false, \"_attachments\": {{ \"i_use_couchdb.png\": {{ \"content_type\": \"image/png\", \"data\": \"{0}\" }} }} }}", attachmentBase64);
            }
            else
            {
                docJson = @"{""foo"":1,""bar"":false}";
            }

            // push a document to server
            var replicationUrlTrailingDoc1 = new Uri(string.Format("{0}/{1}", GetReplicationURL(), docId));
            var pathToDoc1 = new Uri(replicationUrlTrailingDoc1, docId);
            Log.D(Tag, "Send http request to " + pathToDoc1);
            CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
            Task.Factory.StartNew(() =>
            {
                HttpClient httpclient = null;
                try
                {
                    httpclient = new HttpClient();
                    HttpResponseMessage response;
                    var request = new HttpRequestMessage();
                    request.Headers.Add("Accept", "*/*");

                    var postTask = httpclient.PutAsync(pathToDoc1.AbsoluteUri, new StringContent(docJson, Encoding.UTF8, "application/json"));
                    response = postTask.Result;
                    var statusLine = response.StatusCode;
                    Log.D(ReplicationTest.Tag, "Got response: " + statusLine);
                    Assert.IsTrue(statusLine == HttpStatusCode.Created);
                }
                catch (ProtocolViolationException e)
                {
                    Assert.IsNull(e, "Got ClientProtocolException: " + e.Message);
                }
                catch (IOException e)
                {
                    Assert.IsNull(e, "Got IOException: " + e.Message);
                }
                finally
                {
                    httpclient.Dispose();
                }

                httpRequestDoneSignal.CountDown();
            });

            Log.D(Tag, "Waiting for http request to finish");
            try
            {
                Assert.IsTrue(httpRequestDoneSignal.Await(TimeSpan.FromSeconds(10)));
                Log.D(Tag, "http request finished");
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }

            WorkaroundSyncGatewayRaceCondition();
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestGetReplicator()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var replicationUrl = GetReplicationURL();
            var replicator = database.CreatePullReplication(replicationUrl);
            Assert.IsNotNull(replicator);
            Assert.IsTrue(replicator.IsPull);
            Assert.IsFalse(replicator.Continuous);
            Assert.IsFalse(replicator.IsRunning);

            replicator.Start();
            Assert.IsTrue(replicator.IsRunning);

            var activeReplicators = new Replication[database.ActiveReplicators.Count];
            database.ActiveReplicators.CopyTo(activeReplicators, 0);
            Assert.AreEqual(1, activeReplicators.Length);
            Assert.AreEqual(replicator, activeReplicators [0]);

            replicator.Stop();

            // Wait for a second to ensure that the replicator finishes
            // updating all status (esp Database.ActiveReplicator that will
            // be updated when receiving a Replication.Changed event which
            // is distached asynchronously when running tests.
            System.Threading.Thread.Sleep(1000);

            Assert.IsFalse(replicator.IsRunning);
            activeReplicators = new Replication[database.ActiveReplicators.Count];
            database.ActiveReplicators.CopyTo(activeReplicators, 0);
            Assert.AreEqual(0, activeReplicators.Length);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestGetReplicatorWithAuth()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var email = "jchris@couchbase.com";
            var accessToken = "fake_access_token";
            var remoteUrl = GetReplicationURL().ToString();
            FacebookAuthorizer.RegisterAccessToken(accessToken, email, remoteUrl);

            var url = GetReplicationURLWithoutCredentials();
            Replication replicator = database.CreatePushReplication(url);
            replicator.Authenticator = AuthenticatorFactory.CreateFacebookAuthenticator (accessToken);

            Assert.IsNotNull(replicator);
            Assert.IsNotNull(replicator.Authenticator);
            Assert.IsTrue(replicator.Authenticator is TokenAuthenticator);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestRunReplicationWithError()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var mockHttpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = mockHttpClientFactory;

            var mockHttpHandler = mockHttpClientFactory.HttpHandler;
            mockHttpHandler.AddResponderFailAllRequests(HttpStatusCode.InternalServerError);

            var dbUrlString = "http://fake.test-url.com:4984/fake/";
            var remote = new Uri(dbUrlString);
            var continuous = false;
            var r1 = new Pusher(database, remote, continuous, mockHttpClientFactory, new TaskFactory(new SingleThreadTaskScheduler()));
            Assert.IsFalse(r1.Continuous);
            RunReplication(r1);

            Assert.AreEqual(ReplicationStatus.Stopped, r1.Status);
            Assert.AreEqual(0, r1.CompletedChangesCount);
            Assert.AreEqual(0, r1.ChangesCount);
            Assert.IsNotNull(r1.LastError);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestReplicatorErrorStatus()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var email = "jchris@couchbase.com";
            var accessToken = "fake_access_token";
            var remoteUrl = GetReplicationURL().ToString();
            FacebookAuthorizer.RegisterAccessToken(accessToken, email, remoteUrl);

            var replicator = database.CreatePullReplication(GetReplicationURL());
            replicator.Authenticator = AuthenticatorFactory.CreateFacebookAuthenticator (accessToken);

            RunReplication(replicator);

            Assert.IsNotNull(replicator.LastError);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestGoOffline()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var remote = GetReplicationURL();
            var repl = database.CreatePullReplication(remote);
            repl.Continuous = true;
            repl.Start();
            PutReplicationOffline(repl);
            Assert.IsTrue(repl.Status == ReplicationStatus.Offline);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public virtual void TestBuildRelativeURLString()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var dbUrlString = "http://10.0.0.3:4984/todos/";
            var replicator = new Pusher(null, new Uri(dbUrlString), false, null);
            string relativeUrlString = replicator.BuildRelativeURLString("foo");
            string expected = "http://10.0.0.3:4984/todos/foo";
            Assert.AreEqual(expected, relativeUrlString);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public virtual void TestBuildRelativeURLStringWithLeadingSlash()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var dbUrlString = "http://10.0.0.3:4984/todos/";
            var replicator = new Pusher(null, new Uri(dbUrlString), false, null);
            string relativeUrlString = replicator.BuildRelativeURLString("/foo");
            string expected = "http://10.0.0.3:4984/todos/foo";
            Assert.AreEqual(expected, relativeUrlString);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestChannels()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var remote = GetReplicationURL();
            var replicator = database.CreatePullReplication(remote);

            var channels = new List<string>();
            channels.AddItem("chan1");
            channels.AddItem("chan2");
            replicator.Channels = channels;
            Assert.AreEqual(channels, replicator.Channels);

            replicator.Channels = null;
            Assert.IsTrue(replicator.Channels.ToList().Count == 0);
        }

        /// <exception cref="System.UriFormatException"></exception>
        [Test]
        public virtual void TestChannelsMore()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var fakeRemoteURL = new Uri("http://couchbase.com/no_such_db");
            var r1 = database.CreatePullReplication(fakeRemoteURL);

            Assert.IsTrue(!r1.Channels.Any());
            r1.Filter = "foo/bar";
            Assert.IsTrue(!r1.Channels.Any());

            var filterParams = new Dictionary<string, object>();
            filterParams.Put("a", "b");
            r1.FilterParams = filterParams;
            Assert.IsTrue(!r1.Channels.Any());

            r1.Channels = null;
            Assert.AreEqual("foo/bar", r1.Filter);
            Assert.AreEqual(filterParams, r1.FilterParams);

            var channels = new List<string>();
            channels.Add("NBC");
            channels.Add("MTV");
            r1.Channels = channels;
            Assert.AreEqual(channels, r1.Channels);
            Assert.AreEqual("sync_gateway/bychannel", r1.Filter);

            filterParams = new Dictionary<string, object>();
            filterParams.Put("channels", "NBC,MTV");
            Assert.AreEqual(filterParams, r1.FilterParams);
                        
            r1.Channels = null;
            Assert.AreEqual(r1.Filter, null);
            Assert.AreEqual(null, r1.FilterParams);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestHeaders()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var mockHttpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = mockHttpClientFactory;

            var mockHttpHandler = mockHttpClientFactory.HttpHandler;
            mockHttpHandler.AddResponderThrowExceptionAllRequests();

            var remote = GetReplicationURL();
            var puller = database.CreatePullReplication(remote);
            var headers = new Dictionary<string, string>();
            headers["foo"] = "bar";
            puller.Headers = headers;
            RunReplication(puller);
            Assert.IsNotNull(puller.LastError);

            var foundFooHeader = false;
            var requests = mockHttpHandler.CapturedRequests;

            foreach (var request in requests)
            {
                var requestHeaders = request.Headers.GetValues("foo");
                foreach (var requestHeader in requestHeaders)
                {
                    foundFooHeader = true;
                    Assert.AreEqual("bar", requestHeader);
                }
            }
            Assert.IsTrue(foundFooHeader);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestAllLeafRevisionsArePushed()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var httpClientFactory = new MockHttpClientFactory();
            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderRevDiffsAllMissing();
            httpHandler.AddResponderFakeLocalDocumentUpdate404();
            httpHandler.ResponseDelayMilliseconds = 250;
            manager.DefaultHttpClientFactory = httpClientFactory;

            var doc = database.CreateDocument();
            var rev1a = doc.CreateRevision().Save();
            var rev2a = rev1a.CreateRevision().Save();
            var rev3a = rev2a.CreateRevision().Save();

            // delete the branch we've been using, then create a new one to replace it
            var rev4a = rev3a.DeleteDocument();
            var rev2b = rev1a.CreateRevision().Save(true);

            Assert.AreEqual(rev2b.Id, doc.CurrentRevisionId);

            // sync with remote DB -- should push both leaf revisions
            var pusher = database.CreatePushReplication(GetReplicationURL());
            RunReplication(pusher);
            Assert.IsNull(pusher.LastError);

            var foundRevsDiff = false;
            var capturedRequests = httpHandler.CapturedRequests;
            foreach (var httpRequest in capturedRequests) 
            {
                var uriString = httpRequest.RequestUri.ToString();
                if (uriString.EndsWith("_revs_diff", StringComparison.Ordinal))
                {
                    foundRevsDiff = true;
                    var jsonMap = MockHttpRequestHandler.GetJsonMapFromRequest(httpRequest);
                    var revisionIds = ((JArray)jsonMap.Get(doc.Id)).Values<string>().ToList();
                    Assert.AreEqual(2, revisionIds.Count);
                    Assert.IsTrue(revisionIds.Contains(rev4a.Id));
                    Assert.IsTrue(revisionIds.Contains(rev2b.Id));
                }
            }

            Assert.IsTrue(foundRevsDiff);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestRemoteConflictResolution()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            // Create a document with two conflicting edits.
            var doc = database.CreateDocument();
            var rev1 = doc.CreateRevision().Save();
            var rev2a = CreateRevisionWithRandomProps(rev1, false);
            var rev2b = CreateRevisionWithRandomProps(rev1, true);

            // make sure we can query the db to get the conflict
            var allDocsQuery = database.CreateAllDocumentsQuery();
            allDocsQuery.AllDocsMode = allDocsQuery.AllDocsMode = AllDocsMode.OnlyConflicts;

            var rows = allDocsQuery.Run();
            Assert.AreEqual(1, rows.Count);
            Assert.IsTrue(rows.Aggregate(false, (found, row) => found |= row.Document.Id.Equals(doc.Id)));

            // Push the conflicts to the remote DB.
            var pusher = database.CreatePushReplication(GetReplicationURL());
            RunReplication(pusher);
            Assert.IsNull(pusher.LastError);

            var rev3aBody = new JObject();
            rev3aBody.Put("_id", doc.Id);
            rev3aBody.Put("_rev", rev2a.Id);

            // Then, delete rev 2b.
            var rev3bBody = new JObject();
            rev3bBody.Put("_id", doc.Id);
            rev3bBody.Put("_rev", rev2b.Id);
            rev3bBody.Put("_deleted", true);

            // Combine into one _bulk_docs request.
            var requestBody = new JObject();
            var docs = new JArray { rev3aBody, rev3bBody};
            requestBody.Put("docs", docs);

            // Make the _bulk_docs request.
            var client = new HttpClient();
            var bulkDocsUrl = GetReplicationURL () + "/_bulk_docs";
            var request = new HttpRequestMessage(HttpMethod.Post, bulkDocsUrl);
            //request.Headers.Add("Accept", "*/*");
            request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

            var response = client.SendAsync(request).Result;
            // Check the response to make sure everything worked as it should.
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            var rawResponse = response.Content.ReadAsStringAsync().Result;
            var resultArray = Manager.GetObjectMapper().ReadValue<JArray>(rawResponse);
            Assert.AreEqual(2, resultArray.Count);

            foreach (var value in resultArray.Values<JObject>())
            {
                var err = (string)value["error"];
                Assert.IsNull(err);
            }

            WorkaroundSyncGatewayRaceCondition();

            // Pull the remote changes.
            var puller = database.CreatePullReplication(GetReplicationURL());
            RunReplication(puller);
            Assert.IsNull(puller.LastError);

            // Make sure the conflict was resolved locally.
            Assert.AreEqual(1, doc.ConflictingRevisions.Count());
        }

        [Test]
        public void TestSetAndDeleteCookies()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var replicationUrl = GetReplicationURL();
            var puller = database.CreatePullReplication(replicationUrl);
            var cookieContainer = puller.CookieContainer;

            // Set
            var name = "foo";
            var value = "bar";
            var isSecure = false;
            var httpOnly = false;
            var domain = replicationUrl.Host;
            var path = replicationUrl.PathAndQuery;
            var expires = DateTime.Now.Add(TimeSpan.FromDays(1));
            puller.SetCookie(name, value, path, expires, isSecure, httpOnly);

            var cookies = cookieContainer.GetCookies(replicationUrl);
            Assert.AreEqual(1, cookies.Count);
            var cookie = cookies[0];
            Assert.AreEqual(name, cookie.Name);
            Assert.AreEqual(value, cookie.Value);
            Assert.AreEqual(domain, cookie.Domain);
            Assert.AreEqual(path, cookie.Path);
            Assert.AreEqual(expires, cookie.Expires);
            Assert.AreEqual(isSecure, cookie.Secure);
            Assert.AreEqual(httpOnly, cookie.HttpOnly);

            puller = database.CreatePullReplication(replicationUrl);
            cookieContainer = puller.CookieContainer;

            var name2 = "foo2";
            puller.SetCookie(name2, value, path, expires, isSecure, httpOnly);
            cookies = cookieContainer.GetCookies(replicationUrl);
            Assert.AreEqual(2, cookies.Count);

            // Delete
            puller.DeleteCookie(name2);
            Assert.AreEqual(1, cookieContainer.GetCookies(replicationUrl).Count);
            Assert.AreEqual(name, cookieContainer.GetCookies(replicationUrl)[0].Name);
        }

        [Test]
        public void TestPushReplicationCanMissDocs()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            Assert.AreEqual(0, database.LastSequenceNumber);

            var properties1 = new Dictionary<string, object>();
            properties1["doc1"] = "testPushReplicationCanMissDocs";
            var doc1 = CreateDocumentWithProperties(database, properties1);

            var properties2 = new Dictionary<string, object>();
            properties2["doc2"] = "testPushReplicationCanMissDocs";
            var doc2 = CreateDocumentWithProperties(database, properties2);

            var doc2UnsavedRev = doc2.CreateRevision();
            var attachmentStream = GetAsset("attachment.png");
            doc2UnsavedRev.SetAttachment("attachment.png", "image/png", attachmentStream);
            var doc2Rev = doc2UnsavedRev.Save();
            Assert.IsNotNull(doc2Rev);

            var httpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = httpClientFactory;

            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderFakeLocalDocumentUpdate404();

            var json = "{\"error\":\"not_found\",\"reason\":\"missing\"}";
            MockHttpRequestHandler.HttpResponseDelegate bulkDocsResponder = (request) =>
            {
                return MockHttpRequestHandler.GenerateHttpResponseMessage(HttpStatusCode.NotFound, null, json);
            };
            httpHandler.SetResponder("_bulk_docs", bulkDocsResponder);

            MockHttpRequestHandler.HttpResponseDelegate doc2Responder = (request) =>
            {
                var responseObject = new Dictionary<string, object>();
                responseObject["id"] = doc2.Id;
                responseObject["ok"] = true;
                responseObject["rev"] = doc2.CurrentRevisionId;
                return  MockHttpRequestHandler.GenerateHttpResponseMessage(responseObject);
            };
            httpHandler.SetResponder(doc2.Id, bulkDocsResponder);

            var replicationDoneSignal = new CountDownLatch(1);
            var observer = new ReplicationObserver(replicationDoneSignal);
            var pusher = database.CreatePushReplication(GetReplicationURL());
            pusher.Changed += observer.Changed;
            pusher.Start();

            var success = replicationDoneSignal.Await(TimeSpan.FromSeconds(5));
            Assert.IsTrue(success);

            Assert.IsNotNull(pusher.LastError);

            System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(500));

            var localLastSequence = database.LastSequenceWithCheckpointId(pusher.RemoteCheckpointDocID());

            Log.D(Tag, "dtabase.lastSequenceWithCheckpointId(): " + localLastSequence);
            Log.D(Tag, "doc2.getCUrrentRevision().getSequence(): " + doc2.CurrentRevision.Sequence);

            // Since doc1 failed, the database should _not_ have had its lastSequence bumped to doc2's sequence number.
            // If it did, it's bug: github.com/couchbase/couchbase-lite-java-core/issues/95
            Assert.IsFalse(doc2.CurrentRevision.Sequence.ToString().Equals(localLastSequence));
            Assert.IsNull(localLastSequence);
            Assert.IsTrue(doc2.CurrentRevision.Sequence > 0);
        }

        [Test]
        public void TestCheckServerCompatVersion()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var replicator = database.CreatePushReplication(GetReplicationURL());
            Assert.IsFalse(replicator.CheckServerCompatVersion("0.01"));

            replicator.ServerType = "Couchbase Sync Gateway/1.00";
            Assert.IsTrue(replicator.CheckServerCompatVersion("1.00"));
            Assert.IsFalse(replicator.CheckServerCompatVersion("2.00"));
        }

        [Test]
        public void TestPusherFindCommonAncestor()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var ids = new JArray();
            ids.Add("second");
            ids.Add("first");

            var revDict = new JObject();
            revDict["ids"] = ids;
            revDict["start"] = 2;

            var properties = new Dictionary<string, object>();
            properties["_revisions"] = revDict;

            var rev = new RevisionInternal(properties, database);
            Assert.AreEqual(Pusher.FindCommonAncestor(rev, new  List<string>()), 0);
            Assert.AreEqual(Pusher.FindCommonAncestor(rev, (new [] {"3-noway", "1-nope"}).ToList()), 0);
            Assert.AreEqual(Pusher.FindCommonAncestor(rev, (new [] {"3-noway", "1-first"}).ToList()), 1);
            Assert.AreEqual(Pusher.FindCommonAncestor(rev, (new [] {"3-noway", "2-second", "1-first"}).ToList()), 2);
        }

        [Test]
        public void TestPushManyNewDocuments()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var numDocs = 100;

            var pusher = database.CreatePushReplication(GetReplicationURL());
            pusher.Continuous = true;

            var changesCount = 0;
            var completedChangesCount = 0;
            var replicationCaughtUpSignal = new CountdownEvent(1);
            pusher.Changed += (sender, e) => 
            {
                changesCount = e.Source.ChangesCount;
                completedChangesCount = e.Source.CompletedChangesCount;
                var msg = "changes: {0} completed changes: {1}".Fmt(changesCount, completedChangesCount);
                Log.D(Tag, msg);
                if (changesCount == completedChangesCount 
                    && changesCount == numDocs
                    && replicationCaughtUpSignal.CurrentCount > 0)
                {
                    replicationCaughtUpSignal.Signal();
                }
            };
            pusher.Start();

            for (var i = 0; i < numDocs; i++)
            {
                var properties = new Dictionary<string, object>() 
                {
                    {"type", "session"},
                    {"id", "session-" + i},
                    {"title", "title" + i},
                    {"speaker", "TBD"},
                    {"time", "2014-10-07T16:20:00"},
                    {"Track", "Case Study"}
                };

                var doc = database.CreateDocument();
                var rev = doc.PutProperties(properties);
                Assert.IsNotNull(rev);
            }

            replicationCaughtUpSignal.Wait(TimeSpan.FromSeconds(8));
            Assert.AreEqual(numDocs, completedChangesCount);
        }
    }
}
