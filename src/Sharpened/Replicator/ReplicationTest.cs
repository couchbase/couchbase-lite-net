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
using System.Net;
using System.Text;
using Apache.Http;
using Apache.Http.Client;
using Apache.Http.Client.Methods;
using Apache.Http.Entity;
using Apache.Http.Entity.Mime;
using Apache.Http.Impl.Client;
using Apache.Http.Message;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Threading;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Org.Apache.Commons.IO;
using Org.Apache.Commons.IO.Output;
using Org.Json;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
    public class ReplicationTest : LiteTestCase
    {
        public const string Tag = "Replicator";

        /// <summary>
        /// Verify that running a one-shot push replication will complete when run against a
        /// mock server that returns 500 Internal Server errors on every request.
        /// </summary>
        /// <remarks>
        /// Verify that running a one-shot push replication will complete when run against a
        /// mock server that returns 500 Internal Server errors on every request.
        /// </remarks>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestOneShotReplicationErrorNotification()
        {
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderThrowExceptionAllRequests();
            Uri remote = GetReplicationURL();
            manager.SetDefaultHttpClientFactory(MockFactoryFactory(mockHttpClient));
            Replication pusher = database.CreatePushReplication(remote);
            RunReplication(pusher);
            NUnit.Framework.Assert.IsTrue(pusher.GetLastError() != null);
        }

        /// <summary>
        /// Verify that running a continuous push replication will emit a change while
        /// in an error state when run against a mock server that returns 500 Internal Server
        /// errors on every request.
        /// </summary>
        /// <remarks>
        /// Verify that running a continuous push replication will emit a change while
        /// in an error state when run against a mock server that returns 500 Internal Server
        /// errors on every request.
        /// </remarks>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestContinuousReplicationErrorNotification()
        {
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderThrowExceptionAllRequests();
            Uri remote = GetReplicationURL();
            manager.SetDefaultHttpClientFactory(MockFactoryFactory(mockHttpClient));
            Replication pusher = database.CreatePushReplication(remote);
            pusher.SetContinuous(true);
            // add replication observer
            CountDownLatch countDownLatch = new CountDownLatch(1);
            LiteTestCase.ReplicationErrorObserver replicationErrorObserver = new LiteTestCase.ReplicationErrorObserver
                (countDownLatch);
            pusher.AddChangeListener(replicationErrorObserver);
            // start replication
            pusher.Start();
            bool success = countDownLatch.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(success);
            pusher.Stop();
        }

        private HttpClientFactory MockFactoryFactory(CustomizableMockHttpClient mockHttpClient
            )
        {
            return new _HttpClientFactory_128(mockHttpClient);
        }

        private sealed class _HttpClientFactory_128 : HttpClientFactory
        {
            public _HttpClientFactory_128(CustomizableMockHttpClient mockHttpClient)
            {
                this.mockHttpClient = mockHttpClient;
            }

            public HttpClient GetHttpClient()
            {
                return mockHttpClient;
            }

            public void AddCookies(IList<Apache.Http.Cookie.Cookie> cookies)
            {
            }

            public void DeleteCookie(string name)
            {
            }

            public CookieStore GetCookieStore()
            {
                return null;
            }

            private readonly CustomizableMockHttpClient mockHttpClient;
        }

        // Reproduces issue #167
        // https://github.com/couchbase/couchbase-lite-android/issues/167
        /// <exception cref="System.Exception"></exception>
        public virtual void TestPushPurgedDoc()
        {
            int numBulkDocRequests = 0;
            HttpPost lastBulkDocsRequest = null;
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("testName", "testPurgeDocument");
            Document doc = CreateDocumentWithProperties(database, properties);
            NUnit.Framework.Assert.IsNotNull(doc);
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderRevDiffsAllMissing();
            mockHttpClient.SetResponseDelayMilliseconds(250);
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_169(mockHttpClient
                );
            Uri remote = GetReplicationURL();
            manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
            Replication pusher = database.CreatePushReplication(remote);
            pusher.SetContinuous(true);
            CountDownLatch replicationCaughtUpSignal = new CountDownLatch(1);
            pusher.AddChangeListener(new _ChangeListener_199(replicationCaughtUpSignal));
            pusher.Start();
            // wait until that doc is pushed
            bool didNotTimeOut = replicationCaughtUpSignal.Await(60, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(didNotTimeOut);
            // at this point, we should have captured exactly 1 bulk docs request
            numBulkDocRequests = 0;
            foreach (HttpWebRequest capturedRequest in mockHttpClient.GetCapturedRequests())
            {
                if (capturedRequest is HttpPost && ((HttpPost)capturedRequest).GetURI().ToString(
                    ).EndsWith("_bulk_docs"))
                {
                    lastBulkDocsRequest = (HttpPost)capturedRequest;
                    numBulkDocRequests += 1;
                }
            }
            NUnit.Framework.Assert.AreEqual(1, numBulkDocRequests);
            // that bulk docs request should have the "start" key under its _revisions
            IDictionary<string, object> jsonMap = CustomizableMockHttpClient.GetJsonMapFromRequest
                ((HttpPost)lastBulkDocsRequest);
            IList docs = (IList)jsonMap.Get("docs");
            IDictionary<string, object> onlyDoc = (IDictionary)docs[0];
            IDictionary<string, object> revisions = (IDictionary)onlyDoc.Get("_revisions");
            NUnit.Framework.Assert.IsTrue(revisions.ContainsKey("start"));
            // now add a new revision, which will trigger the pusher to try to push it
            properties = new Dictionary<string, object>();
            properties.Put("testName2", "update doc");
            UnsavedRevision unsavedRevision = doc.CreateRevision();
            unsavedRevision.SetUserProperties(properties);
            unsavedRevision.Save();
            // but then immediately purge it
            doc.Purge();
            // wait for a while to give the replicator a chance to push it
            // (it should not actually push anything)
            Sharpen.Thread.Sleep(5 * 1000);
            // we should not have gotten any more _bulk_docs requests, because
            // the replicator should not have pushed anything else.
            // (in the case of the bug, it was trying to push the purged revision)
            numBulkDocRequests = 0;
            foreach (HttpWebRequest capturedRequest_1 in mockHttpClient.GetCapturedRequests())
            {
                if (capturedRequest_1 is HttpPost && ((HttpPost)capturedRequest_1).GetURI().ToString
                    ().EndsWith("_bulk_docs"))
                {
                    numBulkDocRequests += 1;
                }
            }
            NUnit.Framework.Assert.AreEqual(1, numBulkDocRequests);
            pusher.Stop();
        }

        private sealed class _HttpClientFactory_169 : HttpClientFactory
        {
            public _HttpClientFactory_169(CustomizableMockHttpClient mockHttpClient)
            {
                this.mockHttpClient = mockHttpClient;
            }

            public HttpClient GetHttpClient()
            {
                return mockHttpClient;
            }

            public void AddCookies(IList<Apache.Http.Cookie.Cookie> cookies)
            {
            }

            public void DeleteCookie(string name)
            {
            }

            public CookieStore GetCookieStore()
            {
                return null;
            }

            private readonly CustomizableMockHttpClient mockHttpClient;
        }

        private sealed class _ChangeListener_199 : Replication.ChangeListener
        {
            public _ChangeListener_199(CountDownLatch replicationCaughtUpSignal)
            {
                this.replicationCaughtUpSignal = replicationCaughtUpSignal;
            }

            public void Changed(Replication.ChangeEvent @event)
            {
                int changesCount = @event.GetSource().GetChangesCount();
                int completedChangesCount = @event.GetSource().GetCompletedChangesCount();
                string msg = string.Format("changes: %d completed changes: %d", changesCount, completedChangesCount
                    );
                Log.D(ReplicationTest.Tag, msg);
                if (changesCount == completedChangesCount && changesCount != 0)
                {
                    replicationCaughtUpSignal.CountDown();
                }
            }

            private readonly CountDownLatch replicationCaughtUpSignal;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestPusher()
        {
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            string doc1Id;
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            Uri remote = GetReplicationURL();
            doc1Id = CreateDocumentsForPushReplication(docIdTimestamp);
            IDictionary<string, object> documentProperties;
            bool continuous = false;
            Replication repl = database.CreatePushReplication(remote);
            repl.SetContinuous(continuous);
            if (!IsSyncGateway(remote))
            {
                repl.SetCreateTarget(true);
                NUnit.Framework.Assert.IsTrue(repl.ShouldCreateTarget());
            }
            // Check the replication's properties:
            NUnit.Framework.Assert.AreEqual(database, repl.GetLocalDatabase());
            NUnit.Framework.Assert.AreEqual(remote, repl.GetRemoteUrl());
            NUnit.Framework.Assert.IsFalse(repl.IsPull());
            NUnit.Framework.Assert.IsFalse(repl.IsContinuous());
            NUnit.Framework.Assert.IsNull(repl.GetFilter());
            NUnit.Framework.Assert.IsNull(repl.GetFilterParams());
            NUnit.Framework.Assert.IsNull(repl.GetDocIds());
            // TODO: CAssertNil(r1.headers); still not null!
            // Check that the replication hasn't started running:
            NUnit.Framework.Assert.IsFalse(repl.IsRunning());
            NUnit.Framework.Assert.AreEqual(Replication.ReplicationStatus.ReplicationStopped, 
                repl.GetStatus());
            NUnit.Framework.Assert.AreEqual(0, repl.GetCompletedChangesCount());
            NUnit.Framework.Assert.AreEqual(0, repl.GetChangesCount());
            NUnit.Framework.Assert.IsNull(repl.GetLastError());
            RunReplication(repl);
            // since we pushed two documents, should expect the changes count to be >= 2
            NUnit.Framework.Assert.IsTrue(repl.GetChangesCount() >= 2);
            NUnit.Framework.Assert.IsTrue(repl.GetCompletedChangesCount() >= 2);
            NUnit.Framework.Assert.IsNull(repl.GetLastError());
            // make sure doc1 is there
            VerifyRemoteDocExists(remote, doc1Id);
            // add doc3
            documentProperties = new Dictionary<string, object>();
            string doc3Id = string.Format("doc3-%s", docIdTimestamp);
            Document doc3 = database.GetDocument(doc3Id);
            documentProperties.Put("bat", 677);
            doc3.PutProperties(documentProperties);
            // re-run push replication
            Replication repl2 = database.CreatePushReplication(remote);
            repl2.SetContinuous(continuous);
            if (!IsSyncGateway(remote))
            {
                repl2.SetCreateTarget(true);
            }
            string repl2CheckpointId = repl2.RemoteCheckpointDocID();
            RunReplication(repl2);
            NUnit.Framework.Assert.IsNull(repl2.GetLastError());
            // make sure the doc has been added
            VerifyRemoteDocExists(remote, doc3Id);
            // verify sequence stored in local db has been updated
            bool isPush = true;
            NUnit.Framework.Assert.AreEqual(repl2.GetLastSequence(), database.GetLastSequenceStored
                (repl2CheckpointId, isPush));
            // wait a few seconds in case reqeust to server to update checkpoint still in flight
            Sharpen.Thread.Sleep(2000);
            // verify that the _local doc remote checkpoint has been updated and it matches
            string pathToCheckpointDoc = string.Format("%s/_local/%s", remote.ToExternalForm(
                ), repl2CheckpointId);
            HttpResponse response = GetRemoteDoc(new Uri(pathToCheckpointDoc));
            IDictionary<string, object> json = ExtractJsonFromResponse(response);
            string remoteLastSequence = (string)json.Get("lastSequence");
            NUnit.Framework.Assert.AreEqual(repl2.GetLastSequence(), remoteLastSequence);
            Log.D(Tag, "testPusher() finished");
        }

        /// <exception cref="System.IO.IOException"></exception>
        private IDictionary<string, object> ExtractJsonFromResponse(HttpResponse response
            )
        {
            InputStream @is = response.GetEntity().GetContent();
            return Manager.GetObjectMapper().ReadValue<IDictionary>(@is);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        private string CreateDocumentsForPushReplication(string docIdTimestamp)
        {
            return CreateDocumentsForPushReplication(docIdTimestamp, "png");
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        private string CreateDocumentsForPushReplication(string docIdTimestamp, string attachmentType
            )
        {
            string doc1Id;
            string doc2Id;
            // Create some documents:
            IDictionary<string, object> doc1Properties = new Dictionary<string, object>();
            doc1Id = string.Format("doc1-%s", docIdTimestamp);
            doc1Properties.Put("_id", doc1Id);
            doc1Properties.Put("foo", 1);
            doc1Properties.Put("bar", false);
            Body body = new Body(doc1Properties);
            RevisionInternal rev1 = new RevisionInternal(body, database);
            Status status = new Status();
            rev1 = database.PutRevision(rev1, null, false, status);
            NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
            doc1Properties.Put("_rev", rev1.GetRevId());
            doc1Properties.Put("UPDATED", true);
            RevisionInternal rev2 = database.PutRevision(new RevisionInternal(doc1Properties, 
                database), rev1.GetRevId(), false, status);
            NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
            IDictionary<string, object> doc2Properties = new Dictionary<string, object>();
            doc2Id = string.Format("doc2-%s", docIdTimestamp);
            doc2Properties.Put("_id", doc2Id);
            doc2Properties.Put("baz", 666);
            doc2Properties.Put("fnord", true);
            database.PutRevision(new RevisionInternal(doc2Properties, database), null, false, 
                status);
            NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
            Document doc2 = database.GetDocument(doc2Id);
            UnsavedRevision doc2UnsavedRev = doc2.CreateRevision();
            if (attachmentType.Equals("png"))
            {
                InputStream attachmentStream = GetAsset("attachment.png");
                doc2UnsavedRev.SetAttachment("attachment.png", "image/png", attachmentStream);
            }
            else
            {
                if (attachmentType.Equals("txt"))
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < 1000; i++)
                    {
                        sb.Append("This is a large attachemnt.");
                    }
                    ByteArrayInputStream attachmentStream = new ByteArrayInputStream(Sharpen.Runtime.GetBytesForString
                        (sb.ToString()));
                    doc2UnsavedRev.SetAttachment("attachment.txt", "text/plain", attachmentStream);
                }
                else
                {
                    throw new RuntimeException("invalid attachment type: " + attachmentType);
                }
            }
            SavedRevision doc2Rev = doc2UnsavedRev.Save();
            NUnit.Framework.Assert.IsNotNull(doc2Rev);
            return doc1Id;
        }

        private bool IsSyncGateway(Uri remote)
        {
            return (remote.Port == 4984 || remote.Port == 4984);
        }

        /// <exception cref="System.UriFormatException"></exception>
        /// <exception cref="System.IO.IOException"></exception>
        private HttpResponse GetRemoteDoc(Uri pathToDoc)
        {
            HttpClient httpclient = new DefaultHttpClient();
            HttpResponse response = null;
            string responseString = null;
            response = httpclient.Execute(new HttpGet(pathToDoc.ToExternalForm()));
            StatusLine statusLine = response.GetStatusLine();
            if (statusLine.GetStatusCode() != HttpStatus.ScOk)
            {
                throw new RuntimeException("Did not get 200 status doing GET to URL: " + pathToDoc
                    );
            }
            return response;
        }

        /// <summary>TODO: 1.</summary>
        /// <remarks>
        /// TODO: 1. refactor to use getRemoteDoc
        /// TODO: 2. can just make synchronous http call, no need for background task
        /// </remarks>
        /// <param name="remote"></param>
        /// <param name="doc1Id"></param>
        /// <exception cref="System.UriFormatException">System.UriFormatException</exception>
        private void VerifyRemoteDocExists(Uri remote, string doc1Id)
        {
            Uri replicationUrlTrailing = new Uri(string.Format("%s/", remote.ToExternalForm()
                ));
            Uri pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
            Log.D(Tag, "Send http request to " + pathToDoc);
            CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
            BackgroundTask getDocTask = new _BackgroundTask_445(pathToDoc, doc1Id, httpRequestDoneSignal
                );
            //Closes the connection.
            getDocTask.Execute();
            Log.D(Tag, "Waiting for http request to finish");
            try
            {
                httpRequestDoneSignal.Await(300, TimeUnit.Seconds);
                Log.D(Tag, "http request finished");
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
        }

        private sealed class _BackgroundTask_445 : BackgroundTask
        {
            public _BackgroundTask_445(Uri pathToDoc, string doc1Id, CountDownLatch httpRequestDoneSignal
                )
            {
                this.pathToDoc = pathToDoc;
                this.doc1Id = doc1Id;
                this.httpRequestDoneSignal = httpRequestDoneSignal;
            }

            public override void Run()
            {
                HttpClient httpclient = new DefaultHttpClient();
                HttpResponse response;
                string responseString = null;
                try
                {
                    response = httpclient.Execute(new HttpGet(pathToDoc.ToExternalForm()));
                    StatusLine statusLine = response.GetStatusLine();
                    NUnit.Framework.Assert.IsTrue(statusLine.GetStatusCode() == HttpStatus.ScOk);
                    if (statusLine.GetStatusCode() == HttpStatus.ScOk)
                    {
                        ByteArrayOutputStream @out = new ByteArrayOutputStream();
                        response.GetEntity().WriteTo(@out);
                        @out.Close();
                        responseString = @out.ToString();
                        NUnit.Framework.Assert.IsTrue(responseString.Contains(doc1Id));
                        Log.D(ReplicationTest.Tag, "result: " + responseString);
                    }
                    else
                    {
                        response.GetEntity().GetContent().Close();
                        throw new IOException(statusLine.GetReasonPhrase());
                    }
                }
                catch (ClientProtocolException e)
                {
                    NUnit.Framework.Assert.IsNull("Got ClientProtocolException: " + e.GetLocalizedMessage
                        (), e);
                }
                catch (IOException e)
                {
                    NUnit.Framework.Assert.IsNull("Got IOException: " + e.GetLocalizedMessage(), e);
                }
                httpRequestDoneSignal.CountDown();
            }

            private readonly Uri pathToDoc;

            private readonly string doc1Id;

            private readonly CountDownLatch httpRequestDoneSignal;
        }

        /// <summary>Regression test for https://github.com/couchbase/couchbase-lite-java-core/issues/72
        ///     </summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestPusherBatching()
        {
            // create a bunch (INBOX_CAPACITY * 2) local documents
            int numDocsToSend = Replication.InboxCapacity * 2;
            for (int i = 0; i < numDocsToSend; i++)
            {
                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties.Put("testPusherBatching", i);
                CreateDocumentWithProperties(database, properties);
            }
            // kick off a one time push replication to a mock
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            HttpClientFactory mockHttpClientFactory = MockFactoryFactory(mockHttpClient);
            Uri remote = GetReplicationURL();
            manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
            Replication pusher = database.CreatePushReplication(remote);
            RunReplication(pusher);
            NUnit.Framework.Assert.IsNull(pusher.GetLastError());
            int numDocsSent = 0;
            // verify that only INBOX_SIZE documents are included in any given bulk post request
            IList<HttpWebRequest> capturedRequests = mockHttpClient.GetCapturedRequests();
            foreach (HttpWebRequest capturedRequest in capturedRequests)
            {
                if (capturedRequest is HttpPost)
                {
                    HttpPost capturedPostRequest = (HttpPost)capturedRequest;
                    if (capturedPostRequest.GetURI().GetPath().EndsWith("_bulk_docs"))
                    {
                        ArrayList docs = CustomizableMockHttpClient.ExtractDocsFromBulkDocsPost(capturedRequest
                            );
                        string msg = "# of bulk docs pushed should be <= INBOX_CAPACITY";
                        NUnit.Framework.Assert.IsTrue(msg, docs.Count <= Replication.InboxCapacity);
                        numDocsSent += docs.Count;
                    }
                }
            }
            NUnit.Framework.Assert.AreEqual(numDocsToSend, numDocsSent);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestPusherDeletedDoc()
        {
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            Uri remote = GetReplicationURL();
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            // Create some documents:
            IDictionary<string, object> documentProperties = new Dictionary<string, object>();
            string doc1Id = string.Format("doc1-%s", docIdTimestamp);
            documentProperties.Put("_id", doc1Id);
            documentProperties.Put("foo", 1);
            documentProperties.Put("bar", false);
            Body body = new Body(documentProperties);
            RevisionInternal rev1 = new RevisionInternal(body, database);
            Status status = new Status();
            rev1 = database.PutRevision(rev1, null, false, status);
            NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
            documentProperties.Put("_rev", rev1.GetRevId());
            documentProperties.Put("UPDATED", true);
            documentProperties.Put("_deleted", true);
            RevisionInternal rev2 = database.PutRevision(new RevisionInternal(documentProperties
                , database), rev1.GetRevId(), false, status);
            NUnit.Framework.Assert.IsTrue(status.GetCode() >= 200 && status.GetCode() < 300);
            Replication repl = database.CreatePushReplication(remote);
            if (!IsSyncGateway(remote))
            {
                repl.SetCreateTarget(true);
            }
            RunReplication(repl);
            NUnit.Framework.Assert.IsNull(repl.GetLastError());
            // make sure doc1 is deleted
            Uri replicationUrlTrailing = new Uri(string.Format("%s/", remote.ToExternalForm()
                ));
            Uri pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
            Log.D(Tag, "Send http request to " + pathToDoc);
            CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
            BackgroundTask getDocTask = new _BackgroundTask_584(pathToDoc, httpRequestDoneSignal
                );
            getDocTask.Execute();
            Log.D(Tag, "Waiting for http request to finish");
            try
            {
                httpRequestDoneSignal.Await(300, TimeUnit.Seconds);
                Log.D(Tag, "http request finished");
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
            Log.D(Tag, "testPusherDeletedDoc() finished");
        }

        private sealed class _BackgroundTask_584 : BackgroundTask
        {
            public _BackgroundTask_584(Uri pathToDoc, CountDownLatch httpRequestDoneSignal)
            {
                this.pathToDoc = pathToDoc;
                this.httpRequestDoneSignal = httpRequestDoneSignal;
            }

            public override void Run()
            {
                HttpClient httpclient = new DefaultHttpClient();
                HttpResponse response;
                string responseString = null;
                try
                {
                    response = httpclient.Execute(new HttpGet(pathToDoc.ToExternalForm()));
                    StatusLine statusLine = response.GetStatusLine();
                    Log.D(ReplicationTest.Tag, "statusLine " + statusLine);
                    NUnit.Framework.Assert.AreEqual(HttpStatus.ScNotFound, statusLine.GetStatusCode()
                        );
                }
                catch (ClientProtocolException e)
                {
                    NUnit.Framework.Assert.IsNull("Got ClientProtocolException: " + e.GetLocalizedMessage
                        (), e);
                }
                catch (IOException e)
                {
                    NUnit.Framework.Assert.IsNull("Got IOException: " + e.GetLocalizedMessage(), e);
                }
                finally
                {
                    httpRequestDoneSignal.CountDown();
                }
            }

            private readonly Uri pathToDoc;

            private readonly CountDownLatch httpRequestDoneSignal;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void FailingTestPullerGzipped()
        {
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            string doc1Id = string.Format("doc1-%s", docIdTimestamp);
            string attachmentName = "attachment.png";
            AddDocWithId(doc1Id, attachmentName, true);
            DoPullReplication();
            Log.D(Tag, "Fetching doc1 via id: " + doc1Id);
            Document doc1 = database.GetDocument(doc1Id);
            NUnit.Framework.Assert.IsNotNull(doc1);
            NUnit.Framework.Assert.IsTrue(doc1.GetCurrentRevisionId().StartsWith("1-"));
            NUnit.Framework.Assert.AreEqual(1, doc1.GetProperties().Get("foo"));
            Attachment attachment = doc1.GetCurrentRevision().GetAttachment(attachmentName);
            NUnit.Framework.Assert.IsTrue(attachment.GetLength() > 0);
            NUnit.Framework.Assert.IsTrue(attachment.GetGZipped());
            byte[] receivedBytes = TextUtils.Read(attachment.GetContent());
            InputStream attachmentStream = GetAsset(attachmentName);
            byte[] actualBytes = TextUtils.Read(attachmentStream);
            NUnit.Framework.Assert.AreEqual(actualBytes.Length, receivedBytes.Length);
            NUnit.Framework.Assert.AreEqual(actualBytes, receivedBytes);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestPuller()
        {
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            string doc1Id = string.Format("doc1-%s", docIdTimestamp);
            string doc2Id = string.Format("doc2-%s", docIdTimestamp);
            Log.D(Tag, "Adding " + doc1Id + " directly to sync gateway");
            AddDocWithId(doc1Id, "attachment.png", false);
            Log.D(Tag, "Adding " + doc2Id + " directly to sync gateway");
            AddDocWithId(doc2Id, "attachment2.png", false);
            DoPullReplication();
            NUnit.Framework.Assert.IsNotNull(database);
            Log.D(Tag, "Fetching doc1 via id: " + doc1Id);
            Document doc1 = database.GetDocument(doc1Id);
            Log.D(Tag, "doc1" + doc1);
            NUnit.Framework.Assert.IsNotNull(doc1);
            NUnit.Framework.Assert.IsNotNull(doc1.GetCurrentRevisionId());
            NUnit.Framework.Assert.IsTrue(doc1.GetCurrentRevisionId().StartsWith("1-"));
            NUnit.Framework.Assert.IsNotNull(doc1.GetProperties());
            NUnit.Framework.Assert.AreEqual(1, doc1.GetProperties().Get("foo"));
            Log.D(Tag, "Fetching doc2 via id: " + doc2Id);
            Document doc2 = database.GetDocument(doc2Id);
            NUnit.Framework.Assert.IsNotNull(doc2);
            NUnit.Framework.Assert.IsNotNull(doc2.GetCurrentRevisionId());
            NUnit.Framework.Assert.IsNotNull(doc2.GetProperties());
            NUnit.Framework.Assert.IsTrue(doc2.GetCurrentRevisionId().StartsWith("1-"));
            NUnit.Framework.Assert.AreEqual(1, doc2.GetProperties().Get("foo"));
            // update doc1 on sync gateway
            string docJson = string.Format("{\"foo\":2,\"bar\":true,\"_rev\":\"%s\",\"_id\":\"%s\"}"
                , doc1.GetCurrentRevisionId(), doc1.GetId());
            PushDocumentToSyncGateway(doc1.GetId(), docJson);
            // do another pull
            Log.D(Tag, "Doing 2nd pull replication");
            DoPullReplication();
            Log.D(Tag, "Finished 2nd pull replication");
            // make sure it has the latest properties
            Document doc1Fetched = database.GetDocument(doc1Id);
            NUnit.Framework.Assert.IsNotNull(doc1Fetched);
            NUnit.Framework.Assert.IsTrue(doc1Fetched.GetCurrentRevisionId().StartsWith("2-")
                );
            NUnit.Framework.Assert.AreEqual(2, doc1Fetched.GetProperties().Get("foo"));
            Log.D(Tag, "testPuller() finished");
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestPullerWithLiveQuery()
        {
            // This is essentially a regression test for a deadlock
            // that was happening when the LiveQuery#onDatabaseChanged()
            // was calling waitForUpdateThread(), but that thread was
            // waiting on connection to be released by the thread calling
            // waitForUpdateThread().  When the deadlock bug was present,
            // this test would trigger the deadlock and never finish.
            Log.D(Database.Tag, "testPullerWithLiveQuery");
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            string doc1Id = string.Format("doc1-%s", docIdTimestamp);
            string doc2Id = string.Format("doc2-%s", docIdTimestamp);
            AddDocWithId(doc1Id, "attachment2.png", false);
            AddDocWithId(doc2Id, "attachment2.png", false);
            int numDocsBeforePull = database.GetDocumentCount();
            View view = database.GetView("testPullerWithLiveQueryView");
            view.SetMapReduce(new _Mapper_724(), null, "1");
            LiveQuery allDocsLiveQuery = view.CreateQuery().ToLiveQuery();
            allDocsLiveQuery.AddChangeListener(new _ChangeListener_734(numDocsBeforePull));
            // the first time this is called back, the rows will be empty.
            // but on subsequent times we should expect to get a non empty
            // row set.
            allDocsLiveQuery.Start();
            DoPullReplication();
            allDocsLiveQuery.Stop();
        }

        private sealed class _Mapper_724 : Mapper
        {
            public _Mapper_724()
            {
            }

            public void Map(IDictionary<string, object> document, Emitter emitter)
            {
                if (document.Get("_id") != null)
                {
                    emitter.Emit(document.Get("_id"), null);
                }
            }
        }

        private sealed class _ChangeListener_734 : LiveQuery.ChangeListener
        {
            public _ChangeListener_734(int numDocsBeforePull)
            {
                this.numDocsBeforePull = numDocsBeforePull;
            }

            public void Changed(LiveQuery.ChangeEvent @event)
            {
                int numTimesCalled = 0;
                if (@event.GetError() != null)
                {
                    throw new RuntimeException(@event.GetError());
                }
                if (numTimesCalled++ > 0)
                {
                    NUnit.Framework.Assert.IsTrue(@event.GetRows().GetCount() > numDocsBeforePull);
                }
                Log.D(Database.Tag, "rows " + @event.GetRows());
            }

            private readonly int numDocsBeforePull;
        }

        private void DoPullReplication()
        {
            Uri remote = GetReplicationURL();
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            Replication repl = (Replication)database.CreatePullReplication(remote);
            repl.SetContinuous(false);
            Log.D(Tag, "Doing pull replication with: " + repl);
            RunReplication(repl);
            NUnit.Framework.Assert.IsNull(repl.GetLastError());
            Log.D(Tag, "Finished pull replication with: " + repl);
        }

        /// <exception cref="System.IO.IOException"></exception>
        private void AddDocWithId(string docId, string attachmentName, bool gzipped)
        {
            string docJson;
            if (attachmentName != null)
            {
                // add attachment to document
                InputStream attachmentStream = GetAsset(attachmentName);
                ByteArrayOutputStream baos = new ByteArrayOutputStream();
                IOUtils.Copy(attachmentStream, baos);
                if (gzipped == false)
                {
                    string attachmentBase64 = Base64.EncodeBytes(baos.ToByteArray());
                    docJson = string.Format("{\"foo\":1,\"bar\":false, \"_attachments\": { \"%s\": { \"content_type\": \"image/png\", \"data\": \"%s\" } } }"
                        , attachmentName, attachmentBase64);
                }
                else
                {
                    byte[] bytes = baos.ToByteArray();
                    string attachmentBase64 = Base64.EncodeBytes(bytes, Base64.Gzip);
                    docJson = string.Format("{\"foo\":1,\"bar\":false, \"_attachments\": { \"%s\": { \"content_type\": \"image/png\", \"data\": \"%s\", \"encoding\": \"gzip\", \"length\":%d } } }"
                        , attachmentName, attachmentBase64, bytes.Length);
                }
            }
            else
            {
                docJson = "{\"foo\":1,\"bar\":false}";
            }
            PushDocumentToSyncGateway(docId, docJson);
            WorkaroundSyncGatewayRaceCondition();
        }

        /// <exception cref="System.UriFormatException"></exception>
        private void PushDocumentToSyncGateway(string docId, string docJson)
        {
            // push a document to server
            Uri replicationUrlTrailingDoc1 = new Uri(string.Format("%s/%s", GetReplicationURL
                ().ToExternalForm(), docId));
            Uri pathToDoc1 = new Uri(replicationUrlTrailingDoc1, docId);
            Log.D(Tag, "Send http request to " + pathToDoc1);
            CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
            BackgroundTask getDocTask = new _BackgroundTask_813(pathToDoc1, docJson, httpRequestDoneSignal
                );
            getDocTask.Execute();
            Log.D(Tag, "Waiting for http request to finish");
            try
            {
                httpRequestDoneSignal.Await(300, TimeUnit.Seconds);
                Log.D(Tag, "http request finished");
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
        }

        private sealed class _BackgroundTask_813 : BackgroundTask
        {
            public _BackgroundTask_813(Uri pathToDoc1, string docJson, CountDownLatch httpRequestDoneSignal
                )
            {
                this.pathToDoc1 = pathToDoc1;
                this.docJson = docJson;
                this.httpRequestDoneSignal = httpRequestDoneSignal;
            }

            public override void Run()
            {
                HttpClient httpclient = new DefaultHttpClient();
                HttpResponse response;
                string responseString = null;
                try
                {
                    HttpPut post = new HttpPut(pathToDoc1.ToExternalForm());
                    StringEntity se = new StringEntity(docJson.ToString());
                    se.SetContentType(new BasicHeader("content_type", "application/json"));
                    post.SetEntity(se);
                    response = httpclient.Execute(post);
                    StatusLine statusLine = response.GetStatusLine();
                    Log.D(ReplicationTest.Tag, "Got response: " + statusLine);
                    NUnit.Framework.Assert.IsTrue(statusLine.GetStatusCode() == HttpStatus.ScCreated);
                }
                catch (ClientProtocolException e)
                {
                    NUnit.Framework.Assert.IsNull("Got ClientProtocolException: " + e.GetLocalizedMessage
                        (), e);
                }
                catch (IOException e)
                {
                    NUnit.Framework.Assert.IsNull("Got IOException: " + e.GetLocalizedMessage(), e);
                }
                httpRequestDoneSignal.CountDown();
            }

            private readonly Uri pathToDoc1;

            private readonly string docJson;

            private readonly CountDownLatch httpRequestDoneSignal;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestGetReplicator()
        {
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("source", DefaultTestDb);
            properties.Put("target", GetReplicationURL().ToExternalForm());
            IDictionary<string, object> headers = new Dictionary<string, object>();
            string coolieVal = "SyncGatewaySession=c38687c2696688a";
            headers.Put("Cookie", coolieVal);
            properties.Put("headers", headers);
            Replication replicator = manager.GetReplicator(properties);
            NUnit.Framework.Assert.IsNotNull(replicator);
            NUnit.Framework.Assert.AreEqual(GetReplicationURL().ToExternalForm(), replicator.
                GetRemoteUrl().ToExternalForm());
            NUnit.Framework.Assert.IsTrue(!replicator.IsPull());
            NUnit.Framework.Assert.IsFalse(replicator.IsContinuous());
            NUnit.Framework.Assert.IsFalse(replicator.IsRunning());
            NUnit.Framework.Assert.IsTrue(replicator.GetHeaders().ContainsKey("Cookie"));
            NUnit.Framework.Assert.AreEqual(replicator.GetHeaders().Get("Cookie"), coolieVal);
            // add replication observer
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            LiteTestCase.ReplicationFinishedObserver replicationFinishedObserver = new LiteTestCase.ReplicationFinishedObserver
                (replicationDoneSignal);
            replicator.AddChangeListener(replicationFinishedObserver);
            // start the replicator
            Log.D(Tag, "Starting replicator " + replicator);
            replicator.Start();
            // now lets lookup existing replicator and stop it
            Log.D(Tag, "Looking up replicator");
            properties.Put("cancel", true);
            Replication activeReplicator = manager.GetReplicator(properties);
            Log.D(Tag, "Found replicator " + activeReplicator + " and calling stop()");
            activeReplicator.Stop();
            Log.D(Tag, "called stop(), waiting for it to finish");
            // wait for replication to finish
            bool didNotTimeOut = replicationDoneSignal.Await(180, TimeUnit.Seconds);
            Log.D(Tag, "replicationDoneSignal.await done, didNotTimeOut: " + didNotTimeOut);
            NUnit.Framework.Assert.IsTrue(didNotTimeOut);
            NUnit.Framework.Assert.IsFalse(activeReplicator.IsRunning());
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestGetReplicatorWithAuth()
        {
            IDictionary<string, object> properties = GetPushReplicationParsedJson();
            Replication replicator = manager.GetReplicator(properties);
            NUnit.Framework.Assert.IsNotNull(replicator);
            NUnit.Framework.Assert.IsNotNull(replicator.GetAuthenticator());
            NUnit.Framework.Assert.IsTrue(replicator.GetAuthenticator() is FacebookAuthorizer
                );
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestRunReplicationWithError()
        {
            HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_913();
            string dbUrlString = "http://fake.test-url.com:4984/fake/";
            Uri remote = new Uri(dbUrlString);
            bool continuous = false;
            Replication r1 = new Puller(database, remote, continuous, mockHttpClientFactory, 
                manager.GetWorkExecutor());
            NUnit.Framework.Assert.IsFalse(r1.IsContinuous());
            RunReplication(r1);
            // It should have failed with a 404:
            NUnit.Framework.Assert.AreEqual(Replication.ReplicationStatus.ReplicationStopped, 
                r1.GetStatus());
            NUnit.Framework.Assert.AreEqual(0, r1.GetCompletedChangesCount());
            NUnit.Framework.Assert.AreEqual(0, r1.GetChangesCount());
            NUnit.Framework.Assert.IsNotNull(r1.GetLastError());
        }

        private sealed class _HttpClientFactory_913 : HttpClientFactory
        {
            public _HttpClientFactory_913()
            {
            }

            public HttpClient GetHttpClient()
            {
                CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
                int statusCode = 500;
                mockHttpClient.AddResponderFailAllRequests(statusCode);
                return mockHttpClient;
            }

            public void AddCookies(IList<Apache.Http.Cookie.Cookie> cookies)
            {
            }

            public void DeleteCookie(string name)
            {
            }

            public CookieStore GetCookieStore()
            {
                return null;
            }
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestReplicatorErrorStatus()
        {
            // register bogus fb token
            IDictionary<string, object> facebookTokenInfo = new Dictionary<string, object>();
            facebookTokenInfo.Put("email", "jchris@couchbase.com");
            facebookTokenInfo.Put("remote_url", GetReplicationURL().ToExternalForm());
            facebookTokenInfo.Put("access_token", "fake_access_token");
            string destUrl = string.Format("/_facebook_token", DefaultTestDb);
            IDictionary<string, object> result = (IDictionary<string, object>)SendBody("POST"
                , destUrl, facebookTokenInfo, Status.Ok, null);
            Log.V(Tag, string.Format("result %s", result));
            // run replicator and make sure it has an error
            IDictionary<string, object> properties = GetPullReplicationParsedJson();
            Replication replicator = manager.GetReplicator(properties);
            RunReplication(replicator);
            NUnit.Framework.Assert.IsNotNull(replicator.GetLastError());
            NUnit.Framework.Assert.IsTrue(replicator.GetLastError() is HttpResponseException);
            NUnit.Framework.Assert.AreEqual(401, ((HttpResponseException)replicator.GetLastError
                ()).GetStatusCode());
        }

        /// <summary>Test for the private goOffline() method, which still in "incubation".</summary>
        /// <remarks>
        /// Test for the private goOffline() method, which still in "incubation".
        /// This test is brittle because it depends on the following observed behavior,
        /// which will probably change:
        /// - the replication will go into an "idle" state after starting the change listener
        /// Which does not match: https://github.com/couchbase/couchbase-lite-android/wiki/Replicator-State-Descriptions
        /// The reason we need to wait for it to go into the "idle" state, is otherwise the following sequence happens:
        /// 1) Call replicator.start()
        /// 2) Call replicator.goOffline()
        /// 3) Does not cancel changetracker, because changetracker is still null
        /// 4) After getting the remote sequence from http://sg/_local/.., it starts the ChangeTracker
        /// 5) Now the changetracker is running even though we've told it to go offline.
        /// </remarks>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestGoOffline()
        {
            Uri remote = GetReplicationURL();
            Replication replicator = database.CreatePullReplication(remote);
            replicator.SetContinuous(true);
            // add replication "idle" observer - exploit the fact that during observation,
            // the replication will go into an "idle" state after starting the change listener.
            CountDownLatch countDownLatch = new CountDownLatch(1);
            LiteTestCase.ReplicationIdleObserver replicationObserver = new LiteTestCase.ReplicationIdleObserver
                (countDownLatch);
            replicator.AddChangeListener(replicationObserver);
            // add replication observer
            CountDownLatch countDownLatch2 = new CountDownLatch(1);
            LiteTestCase.ReplicationFinishedObserver replicationFinishedObserver = new LiteTestCase.ReplicationFinishedObserver
                (countDownLatch2);
            replicator.AddChangeListener(replicationFinishedObserver);
            replicator.Start();
            bool success = countDownLatch.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(success);
            PutReplicationOffline(replicator);
            NUnit.Framework.Assert.IsTrue(replicator.GetStatus() == Replication.ReplicationStatus
                .ReplicationOffline);
            replicator.Stop();
            bool success2 = countDownLatch2.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(success2);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestBuildRelativeURLString()
        {
            string dbUrlString = "http://10.0.0.3:4984/todos/";
            Replication replicator = new Pusher(database, new Uri(dbUrlString), false, null);
            string relativeUrlString = replicator.BuildRelativeURLString("foo");
            string expected = "http://10.0.0.3:4984/todos/foo";
            NUnit.Framework.Assert.AreEqual(expected, relativeUrlString);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestBuildRelativeURLStringWithLeadingSlash()
        {
            string dbUrlString = "http://10.0.0.3:4984/todos/";
            Replication replicator = new Pusher(database, new Uri(dbUrlString), false, null);
            string relativeUrlString = replicator.BuildRelativeURLString("/foo");
            string expected = "http://10.0.0.3:4984/todos/foo";
            NUnit.Framework.Assert.AreEqual(expected, relativeUrlString);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestChannels()
        {
            Uri remote = GetReplicationURL();
            Replication replicator = database.CreatePullReplication(remote);
            IList<string> channels = new AList<string>();
            channels.AddItem("chan1");
            channels.AddItem("chan2");
            replicator.SetChannels(channels);
            NUnit.Framework.Assert.AreEqual(channels, replicator.GetChannels());
            replicator.SetChannels(null);
            NUnit.Framework.Assert.IsTrue(replicator.GetChannels().IsEmpty());
        }

        /// <exception cref="System.UriFormatException"></exception>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestChannelsMore()
        {
            Database db = StartDatabase();
            Uri fakeRemoteURL = new Uri("http://couchbase.com/no_such_db");
            Replication r1 = db.CreatePullReplication(fakeRemoteURL);
            NUnit.Framework.Assert.IsTrue(r1.GetChannels().IsEmpty());
            r1.SetFilter("foo/bar");
            NUnit.Framework.Assert.IsTrue(r1.GetChannels().IsEmpty());
            IDictionary<string, object> filterParams = new Dictionary<string, object>();
            filterParams.Put("a", "b");
            r1.SetFilterParams(filterParams);
            NUnit.Framework.Assert.IsTrue(r1.GetChannels().IsEmpty());
            r1.SetChannels(null);
            NUnit.Framework.Assert.AreEqual("foo/bar", r1.GetFilter());
            NUnit.Framework.Assert.AreEqual(filterParams, r1.GetFilterParams());
            IList<string> channels = new AList<string>();
            channels.AddItem("NBC");
            channels.AddItem("MTV");
            r1.SetChannels(channels);
            NUnit.Framework.Assert.AreEqual(channels, r1.GetChannels());
            NUnit.Framework.Assert.AreEqual("sync_gateway/bychannel", r1.GetFilter());
            filterParams = new Dictionary<string, object>();
            filterParams.Put("channels", "NBC,MTV");
            NUnit.Framework.Assert.AreEqual(filterParams, r1.GetFilterParams());
            r1.SetChannels(null);
            NUnit.Framework.Assert.AreEqual(r1.GetFilter(), null);
            NUnit.Framework.Assert.AreEqual(null, r1.GetFilterParams());
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestHeaders()
        {
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderThrowExceptionAllRequests();
            HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_1106(mockHttpClient
                );
            Uri remote = GetReplicationURL();
            manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
            Replication puller = database.CreatePullReplication(remote);
            IDictionary<string, object> headers = new Dictionary<string, object>();
            headers.Put("foo", "bar");
            puller.SetHeaders(headers);
            RunReplication(puller);
            NUnit.Framework.Assert.IsNotNull(puller.GetLastError());
            bool foundFooHeader = false;
            IList<HttpWebRequest> requests = mockHttpClient.GetCapturedRequests();
            foreach (HttpWebRequest request in requests)
            {
                Header[] requestHeaders = request.GetHeaders("foo");
                foreach (Header requestHeader in requestHeaders)
                {
                    foundFooHeader = true;
                    NUnit.Framework.Assert.AreEqual("bar", requestHeader.GetValue());
                }
            }
            NUnit.Framework.Assert.IsTrue(foundFooHeader);
            manager.SetDefaultHttpClientFactory(null);
        }

        private sealed class _HttpClientFactory_1106 : HttpClientFactory
        {
            public _HttpClientFactory_1106(CustomizableMockHttpClient mockHttpClient)
            {
                this.mockHttpClient = mockHttpClient;
            }

            public HttpClient GetHttpClient()
            {
                return mockHttpClient;
            }

            public void AddCookies(IList<Apache.Http.Cookie.Cookie> cookies)
            {
            }

            public void DeleteCookie(string name)
            {
            }

            public CookieStore GetCookieStore()
            {
                return null;
            }

            private readonly CustomizableMockHttpClient mockHttpClient;
        }

        /// <summary>Regression test for issue couchbase/couchbase-lite-android#174</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestAllLeafRevisionsArePushed()
        {
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderRevDiffsAllMissing();
            mockHttpClient.SetResponseDelayMilliseconds(250);
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_1165(mockHttpClient
                );
            manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
            Document doc = database.CreateDocument();
            SavedRevision rev1a = doc.CreateRevision().Save();
            SavedRevision rev2a = CreateRevisionWithRandomProps(rev1a, false);
            SavedRevision rev3a = CreateRevisionWithRandomProps(rev2a, false);
            // delete the branch we've been using, then create a new one to replace it
            SavedRevision rev4a = rev3a.DeleteDocument();
            SavedRevision rev2b = CreateRevisionWithRandomProps(rev1a, true);
            NUnit.Framework.Assert.AreEqual(rev2b.GetId(), doc.GetCurrentRevisionId());
            // sync with remote DB -- should push both leaf revisions
            Replication push = database.CreatePushReplication(GetReplicationURL());
            RunReplication(push);
            NUnit.Framework.Assert.IsNull(push.GetLastError());
            // find the _revs_diff captured request and decode into json
            bool foundRevsDiff = false;
            IList<HttpWebRequest> captured = mockHttpClient.GetCapturedRequests();
            foreach (HttpWebRequest httpRequest in captured)
            {
                if (httpRequest is HttpPost)
                {
                    HttpPost httpPost = (HttpPost)httpRequest;
                    if (httpPost.GetURI().ToString().EndsWith("_revs_diff"))
                    {
                        foundRevsDiff = true;
                        IDictionary<string, object> jsonMap = CustomizableMockHttpClient.GetJsonMapFromRequest
                            (httpPost);
                        // assert that it contains the expected revisions
                        IList<string> revisionIds = (IList)jsonMap.Get(doc.GetId());
                        NUnit.Framework.Assert.AreEqual(2, revisionIds.Count);
                        NUnit.Framework.Assert.IsTrue(revisionIds.Contains(rev4a.GetId()));
                        NUnit.Framework.Assert.IsTrue(revisionIds.Contains(rev2b.GetId()));
                    }
                }
            }
            NUnit.Framework.Assert.IsTrue(foundRevsDiff);
        }

        private sealed class _HttpClientFactory_1165 : HttpClientFactory
        {
            public _HttpClientFactory_1165(CustomizableMockHttpClient mockHttpClient)
            {
                this.mockHttpClient = mockHttpClient;
            }

            public HttpClient GetHttpClient()
            {
                return mockHttpClient;
            }

            public void AddCookies(IList<Apache.Http.Cookie.Cookie> cookies)
            {
            }

            public void DeleteCookie(string name)
            {
            }

            public CookieStore GetCookieStore()
            {
                return null;
            }

            private readonly CustomizableMockHttpClient mockHttpClient;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestRemoteConflictResolution()
        {
            // Create a document with two conflicting edits.
            Document doc = database.CreateDocument();
            SavedRevision rev1 = doc.CreateRevision().Save();
            SavedRevision rev2a = CreateRevisionWithRandomProps(rev1, false);
            SavedRevision rev2b = CreateRevisionWithRandomProps(rev1, true);
            // make sure we can query the db to get the conflict
            Query allDocsQuery = database.CreateAllDocumentsQuery();
            allDocsQuery.SetAllDocsMode(Query.AllDocsMode.OnlyConflicts);
            QueryEnumerator rows = allDocsQuery.Run();
            bool foundDoc = false;
            NUnit.Framework.Assert.AreEqual(1, rows.GetCount());
            for (IEnumerator<QueryRow> it = rows; it.HasNext(); )
            {
                QueryRow row = it.Next();
                if (row.GetDocument().GetId().Equals(doc.GetId()))
                {
                    foundDoc = true;
                }
            }
            NUnit.Framework.Assert.IsTrue(foundDoc);
            // Push the conflicts to the remote DB.
            Replication push = database.CreatePushReplication(GetReplicationURL());
            RunReplication(push);
            NUnit.Framework.Assert.IsNull(push.GetLastError());
            // Prepare a bulk docs request to resolve the conflict remotely. First, advance rev 2a.
            JSONObject rev3aBody = new JSONObject();
            rev3aBody.Put("_id", doc.GetId());
            rev3aBody.Put("_rev", rev2a.GetId());
            // Then, delete rev 2b.
            JSONObject rev3bBody = new JSONObject();
            rev3bBody.Put("_id", doc.GetId());
            rev3bBody.Put("_rev", rev2b.GetId());
            rev3bBody.Put("_deleted", true);
            // Combine into one _bulk_docs request.
            JSONObject requestBody = new JSONObject();
            requestBody.Put("docs", new JSONArray(Arrays.AsList(rev3aBody, rev3bBody)));
            // Make the _bulk_docs request.
            HttpClient client = new DefaultHttpClient();
            string bulkDocsUrl = GetReplicationURL().ToExternalForm() + "/_bulk_docs";
            HttpPost request = new HttpPost(bulkDocsUrl);
            request.SetHeader("Content-Type", "application/json");
            string json = requestBody.ToString();
            request.SetEntity(new StringEntity(json));
            HttpResponse response = client.Execute(request);
            // Check the response to make sure everything worked as it should.
            NUnit.Framework.Assert.AreEqual(201, response.GetStatusLine().GetStatusCode());
            string rawResponse = IOUtils.ToString(response.GetEntity().GetContent());
            JSONArray resultArray = new JSONArray(rawResponse);
            NUnit.Framework.Assert.AreEqual(2, resultArray.Length());
            for (int i = 0; i < resultArray.Length(); i++)
            {
                NUnit.Framework.Assert.IsTrue(((JSONObject)resultArray.Get(i)).IsNull("error"));
            }
            WorkaroundSyncGatewayRaceCondition();
            // Pull the remote changes.
            Replication pull = database.CreatePullReplication(GetReplicationURL());
            RunReplication(pull);
            NUnit.Framework.Assert.IsNull(pull.GetLastError());
            // Make sure the conflict was resolved locally.
            NUnit.Framework.Assert.AreEqual(1, doc.GetConflictingRevisions().Count);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestOnlineOfflinePusher()
        {
            Uri remote = GetReplicationURL();
            // mock sync gateway
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            mockHttpClient.AddResponderRevDiffsSmartResponder();
            HttpClientFactory mockHttpClientFactory = MockFactoryFactory(mockHttpClient);
            manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
            // create a replication observer
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            LiteTestCase.ReplicationFinishedObserver replicationFinishedObserver = new LiteTestCase.ReplicationFinishedObserver
                (replicationDoneSignal);
            // create a push replication
            Replication pusher = database.CreatePushReplication(remote);
            Log.D(Database.Tag, "created pusher: " + pusher);
            pusher.AddChangeListener(replicationFinishedObserver);
            pusher.SetContinuous(true);
            pusher.Start();
            for (int i = 0; i < 5; i++)
            {
                Log.D(Database.Tag, "testOnlineOfflinePusher, i: " + i);
                string docFieldName = "testOnlineOfflinePusher" + i;
                // put the replication offline
                PutReplicationOffline(pusher);
                // add a response listener to wait for a bulk_docs request from the pusher
                CountDownLatch gotBulkDocsRequest = new CountDownLatch(1);
                CustomizableMockHttpClient.ResponseListener bulkDocsListener = new _ResponseListener_1334
                    (docFieldName, gotBulkDocsRequest);
                mockHttpClient.AddResponseListener(bulkDocsListener);
                // add a document
                string docFieldVal = "foo" + i;
                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties.Put(docFieldName, docFieldVal);
                CreateDocumentWithProperties(database, properties);
                // put the replication online, which should trigger it to send outgoing bulk_docs request
                PutReplicationOnline(pusher);
                // wait until we get a bulk docs request
                Log.D(Database.Tag, "waiting for bulk docs request with " + docFieldName);
                bool succeeded = gotBulkDocsRequest.Await(90, TimeUnit.Seconds);
                NUnit.Framework.Assert.IsTrue(succeeded);
                Log.D(Database.Tag, "got bulk docs request with " + docFieldName);
                mockHttpClient.RemoveResponseListener(bulkDocsListener);
                mockHttpClient.ClearCapturedRequests();
            }
            Log.D(Database.Tag, "calling pusher.stop()");
            pusher.Stop();
            Log.D(Database.Tag, "called pusher.stop()");
            // wait for replication to finish
            Log.D(Database.Tag, "waiting for replicationDoneSignal");
            bool didNotTimeOut = replicationDoneSignal.Await(90, TimeUnit.Seconds);
            Log.D(Database.Tag, "done waiting for replicationDoneSignal.  didNotTimeOut: " + 
                didNotTimeOut);
            NUnit.Framework.Assert.IsTrue(didNotTimeOut);
            NUnit.Framework.Assert.IsFalse(pusher.IsRunning());
        }

        private sealed class _ResponseListener_1334 : CustomizableMockHttpClient.ResponseListener
        {
            public _ResponseListener_1334(string docFieldName, CountDownLatch gotBulkDocsRequest
                )
            {
                this.docFieldName = docFieldName;
                this.gotBulkDocsRequest = gotBulkDocsRequest;
            }

            public void ResponseSent(HttpRequestMessage httpUriRequest, HttpResponse response
                )
            {
                if (httpUriRequest.GetURI().GetPath().EndsWith("_bulk_docs"))
                {
                    Log.D(ReplicationTest.Tag, "testOnlineOfflinePusher responselistener called with _bulk_docs"
                        );
                    ArrayList docs = CustomizableMockHttpClient.ExtractDocsFromBulkDocsPost(httpUriRequest
                        );
                    Log.D(ReplicationTest.Tag, "docs: " + docs);
                    foreach (object docObject in docs)
                    {
                        IDictionary<string, object> doc = (IDictionary)docObject;
                        if (doc.ContainsKey(docFieldName))
                        {
                            Log.D(ReplicationTest.Tag, "Found expected doc in _bulk_docs: " + doc);
                            gotBulkDocsRequest.CountDown();
                        }
                        else
                        {
                            Log.D(ReplicationTest.Tag, "Ignore doc in _bulk_docs: " + doc);
                        }
                    }
                }
            }

            private readonly string docFieldName;

            private readonly CountDownLatch gotBulkDocsRequest;
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-android/issues/247</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestPushReplicationRecoverableError()
        {
            int statusCode = 503;
            string statusMsg = "Transient Error";
            bool expectReplicatorError = false;
            RunPushReplicationWithTransientError(statusCode, statusMsg, expectReplicatorError
                );
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-android/issues/247</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestPushReplicationRecoverableIOException()
        {
            int statusCode = -1;
            // code to tell it to throw an IOException
            string statusMsg = null;
            bool expectReplicatorError = false;
            RunPushReplicationWithTransientError(statusCode, statusMsg, expectReplicatorError
                );
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-android/issues/247</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestPushReplicationNonRecoverableError()
        {
            int statusCode = 404;
            string statusMsg = "NOT FOUND";
            bool expectReplicatorError = true;
            RunPushReplicationWithTransientError(statusCode, statusMsg, expectReplicatorError
                );
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-android/issues/247</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void RunPushReplicationWithTransientError(int statusCode, string statusMsg
            , bool expectReplicatorError)
        {
            IDictionary<string, object> properties1 = new Dictionary<string, object>();
            properties1.Put("doc1", "testPushReplicationTransientError");
            CreateDocWithProperties(properties1);
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            CustomizableMockHttpClient.Responder sentinal = CustomizableMockHttpClient.FakeBulkDocsResponder
                ();
            Queue<CustomizableMockHttpClient.Responder> responders = new List<CustomizableMockHttpClient.Responder
                >();
            responders.AddItem(CustomizableMockHttpClient.TransientErrorResponder(statusCode, 
                statusMsg));
            ResponderChain responderChain = new ResponderChain(responders, sentinal);
            mockHttpClient.SetResponder("_bulk_docs", responderChain);
            // create a replication observer to wait until replication finishes
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            LiteTestCase.ReplicationFinishedObserver replicationFinishedObserver = new LiteTestCase.ReplicationFinishedObserver
                (replicationDoneSignal);
            // create replication and add observer
            manager.SetDefaultHttpClientFactory(MockFactoryFactory(mockHttpClient));
            Replication pusher = database.CreatePushReplication(GetReplicationURL());
            pusher.AddChangeListener(replicationFinishedObserver);
            // save the checkpoint id for later usage
            string checkpointId = pusher.RemoteCheckpointDocID();
            // kick off the replication
            pusher.Start();
            // wait for it to finish
            bool success = replicationDoneSignal.Await(60, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(success);
            Log.D(Tag, "replicationDoneSignal finished");
            if (expectReplicatorError == true)
            {
                NUnit.Framework.Assert.IsNotNull(pusher.GetLastError());
            }
            else
            {
                NUnit.Framework.Assert.IsNull(pusher.GetLastError());
            }
            // workaround for the fact that the replicationDoneSignal.wait() call will unblock before all
            // the statements in Replication.stopped() have even had a chance to execute.
            // (specifically the ones that come after the call to notifyChangeListeners())
            Sharpen.Thread.Sleep(500);
            string localLastSequence = database.LastSequenceWithCheckpointId(checkpointId);
            if (expectReplicatorError == true)
            {
                NUnit.Framework.Assert.IsNull(localLastSequence);
            }
            else
            {
                NUnit.Framework.Assert.IsNotNull(localLastSequence);
            }
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/95</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestPushReplicationCanMissDocs()
        {
            NUnit.Framework.Assert.AreEqual(0, database.GetLastSequenceNumber());
            IDictionary<string, object> properties1 = new Dictionary<string, object>();
            properties1.Put("doc1", "testPushReplicationCanMissDocs");
            Document doc1 = CreateDocWithProperties(properties1);
            IDictionary<string, object> properties2 = new Dictionary<string, object>();
            properties1.Put("doc2", "testPushReplicationCanMissDocs");
            Document doc2 = CreateDocWithProperties(properties2);
            UnsavedRevision doc2UnsavedRev = doc2.CreateRevision();
            InputStream attachmentStream = GetAsset("attachment.png");
            doc2UnsavedRev.SetAttachment("attachment.png", "image/png", attachmentStream);
            SavedRevision doc2Rev = doc2UnsavedRev.Save();
            NUnit.Framework.Assert.IsNotNull(doc2Rev);
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            mockHttpClient.SetResponder("_bulk_docs", new _Responder_1506());
            mockHttpClient.SetResponder(doc2.GetId(), new _Responder_1514(doc2));
            // create a replication obeserver to wait until replication finishes
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            LiteTestCase.ReplicationFinishedObserver replicationFinishedObserver = new LiteTestCase.ReplicationFinishedObserver
                (replicationDoneSignal);
            // create replication and add observer
            manager.SetDefaultHttpClientFactory(MockFactoryFactory(mockHttpClient));
            Replication pusher = database.CreatePushReplication(GetReplicationURL());
            pusher.AddChangeListener(replicationFinishedObserver);
            // save the checkpoint id for later usage
            string checkpointId = pusher.RemoteCheckpointDocID();
            // kick off the replication
            pusher.Start();
            // wait for it to finish
            bool success = replicationDoneSignal.Await(60, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(success);
            Log.D(Tag, "replicationDoneSignal finished");
            // we would expect it to have recorded an error because one of the docs (the one without the attachment)
            // will have failed.
            NUnit.Framework.Assert.IsNotNull(pusher.GetLastError());
            // workaround for the fact that the replicationDoneSignal.wait() call will unblock before all
            // the statements in Replication.stopped() have even had a chance to execute.
            // (specifically the ones that come after the call to notifyChangeListeners())
            Sharpen.Thread.Sleep(500);
            string localLastSequence = database.LastSequenceWithCheckpointId(checkpointId);
            Log.D(Tag, "database.lastSequenceWithCheckpointId(): " + localLastSequence);
            Log.D(Tag, "doc2.getCurrentRevision().getSequence(): " + doc2.GetCurrentRevision(
                ).GetSequence());
            string msg = "Since doc1 failed, the database should _not_ have had its lastSequence bumped"
                 + " to doc2's sequence number.  If it did, it's bug: github.com/couchbase/couchbase-lite-java-core/issues/95";
            NUnit.Framework.Assert.IsFalse(msg, System.Convert.ToString(doc2.GetCurrentRevision
                ().GetSequence()).Equals(localLastSequence));
            NUnit.Framework.Assert.IsNull(localLastSequence);
            NUnit.Framework.Assert.IsTrue(doc2.GetCurrentRevision().GetSequence() > 0);
        }

        private sealed class _Responder_1506 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1506()
            {
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                string json = "{\"error\":\"not_found\",\"reason\":\"missing\"}";
                return CustomizableMockHttpClient.GenerateHttpResponseObject(404, "NOT FOUND", json
                    );
            }
        }

        private sealed class _Responder_1514 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1514(Document doc2)
            {
                this.doc2 = doc2;
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                IDictionary<string, object> responseObject = new Dictionary<string, object>();
                responseObject.Put("id", doc2.GetId());
                responseObject.Put("ok", true);
                responseObject.Put("rev", doc2.GetCurrentRevisionId());
                return CustomizableMockHttpClient.GenerateHttpResponseObject(responseObject);
            }

            private readonly Document doc2;
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-android/issues/66</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestPushUpdatedDocWithoutReSendingAttachments()
        {
            NUnit.Framework.Assert.AreEqual(0, database.GetLastSequenceNumber());
            IDictionary<string, object> properties1 = new Dictionary<string, object>();
            properties1.Put("dynamic", 1);
            Document doc = CreateDocWithProperties(properties1);
            SavedRevision doc1Rev = doc.GetCurrentRevision();
            // Add attachment to document
            UnsavedRevision doc2UnsavedRev = doc.CreateRevision();
            InputStream attachmentStream = GetAsset("attachment.png");
            doc2UnsavedRev.SetAttachment("attachment.png", "image/png", attachmentStream);
            SavedRevision doc2Rev = doc2UnsavedRev.Save();
            NUnit.Framework.Assert.IsNotNull(doc2Rev);
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            // http://url/db/foo (foo==docid)
            mockHttpClient.SetResponder(doc.GetId(), new _Responder_1593(doc));
            // create replication and add observer
            manager.SetDefaultHttpClientFactory(MockFactoryFactory(mockHttpClient));
            Replication pusher = database.CreatePushReplication(GetReplicationURL());
            RunReplication(pusher);
            IList<HttpWebRequest> captured = mockHttpClient.GetCapturedRequests();
            foreach (HttpWebRequest httpRequest in captured)
            {
                // verify that there are no PUT requests with attachments
                if (httpRequest is HttpPut)
                {
                    HttpPut httpPut = (HttpPut)httpRequest;
                    HttpEntity entity = httpPut.GetEntity();
                }
            }
            //assertFalse("PUT request with updated doc properties contains attachment", entity instanceof MultipartEntity);
            mockHttpClient.ClearCapturedRequests();
            Document oldDoc = database.GetDocument(doc.GetId());
            UnsavedRevision aUnsavedRev = oldDoc.CreateRevision();
            IDictionary<string, object> prop = new Dictionary<string, object>();
            prop.PutAll(oldDoc.GetProperties());
            prop.Put("dynamic", (int)oldDoc.GetProperty("dynamic") + 1);
            aUnsavedRev.SetProperties(prop);
            SavedRevision savedRev = aUnsavedRev.Save();
            mockHttpClient.SetResponder(doc.GetId(), new _Responder_1630(doc, savedRev));
            string json = string.Format("{\"%s\":{\"missing\":[\"%s\"],\"possible_ancestors\":[\"%s\",\"%s\"]}}"
                , doc.GetId(), savedRev.GetId(), doc1Rev.GetId(), doc2Rev.GetId());
            mockHttpClient.SetResponder("_revs_diff", new _Responder_1642(mockHttpClient, json
                ));
            pusher = database.CreatePushReplication(GetReplicationURL());
            RunReplication(pusher);
            captured = mockHttpClient.GetCapturedRequests();
            foreach (HttpWebRequest httpRequest_1 in captured)
            {
                // verify that there are no PUT requests with attachments
                if (httpRequest_1 is HttpPut)
                {
                    HttpPut httpPut = (HttpPut)httpRequest_1;
                    HttpEntity entity = httpPut.GetEntity();
                    NUnit.Framework.Assert.IsFalse("PUT request with updated doc properties contains attachment"
                        , entity is MultipartEntity);
                }
            }
        }

        private sealed class _Responder_1593 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1593(Document doc)
            {
                this.doc = doc;
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                IDictionary<string, object> responseObject = new Dictionary<string, object>();
                responseObject.Put("id", doc.GetId());
                responseObject.Put("ok", true);
                responseObject.Put("rev", doc.GetCurrentRevisionId());
                return CustomizableMockHttpClient.GenerateHttpResponseObject(responseObject);
            }

            private readonly Document doc;
        }

        private sealed class _Responder_1630 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1630(Document doc, SavedRevision savedRev)
            {
                this.doc = doc;
                this.savedRev = savedRev;
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                IDictionary<string, object> responseObject = new Dictionary<string, object>();
                responseObject.Put("id", doc.GetId());
                responseObject.Put("ok", true);
                responseObject.Put("rev", savedRev.GetId());
                return CustomizableMockHttpClient.GenerateHttpResponseObject(responseObject);
            }

            private readonly Document doc;

            private readonly SavedRevision savedRev;
        }

        private sealed class _Responder_1642 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1642(CustomizableMockHttpClient mockHttpClient, string json)
            {
                this.mockHttpClient = mockHttpClient;
                this.json = json;
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                return CustomizableMockHttpClient.GenerateHttpResponseObject(json);
            }

            private readonly CustomizableMockHttpClient mockHttpClient;

            private readonly string json;
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/188</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestServerDoesNotSupportMultipart()
        {
            NUnit.Framework.Assert.AreEqual(0, database.GetLastSequenceNumber());
            IDictionary<string, object> properties1 = new Dictionary<string, object>();
            properties1.Put("dynamic", 1);
            Document doc = CreateDocWithProperties(properties1);
            SavedRevision doc1Rev = doc.GetCurrentRevision();
            // Add attachment to document
            UnsavedRevision doc2UnsavedRev = doc.CreateRevision();
            InputStream attachmentStream = GetAsset("attachment.png");
            doc2UnsavedRev.SetAttachment("attachment.png", "image/png", attachmentStream);
            SavedRevision doc2Rev = doc2UnsavedRev.Save();
            NUnit.Framework.Assert.IsNotNull(doc2Rev);
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            Queue<CustomizableMockHttpClient.Responder> responders = new List<CustomizableMockHttpClient.Responder
                >();
            //first http://url/db/foo (foo==docid)
            //Reject multipart PUT with response code 415
            responders.AddItem(new _Responder_1691());
            // second http://url/db/foo (foo==docid)
            // second call should be plain json, return good response
            responders.AddItem(new _Responder_1701(doc));
            ResponderChain responderChain = new ResponderChain(responders);
            mockHttpClient.SetResponder(doc.GetId(), responderChain);
            // create replication and add observer
            manager.SetDefaultHttpClientFactory(MockFactoryFactory(mockHttpClient));
            Replication pusher = database.CreatePushReplication(GetReplicationURL());
            RunReplication(pusher);
            IList<HttpWebRequest> captured = mockHttpClient.GetCapturedRequests();
            int entityIndex = 0;
            foreach (HttpWebRequest httpRequest in captured)
            {
                // verify that there are no PUT requests with attachments
                if (httpRequest is HttpPut)
                {
                    HttpPut httpPut = (HttpPut)httpRequest;
                    HttpEntity entity = httpPut.GetEntity();
                    if (entityIndex++ == 0)
                    {
                        NUnit.Framework.Assert.IsTrue("PUT request with attachment is not multipart", entity
                             is MultipartEntity);
                    }
                    else
                    {
                        NUnit.Framework.Assert.IsFalse("PUT request with attachment is multipart", entity
                             is MultipartEntity);
                    }
                }
            }
        }

        private sealed class _Responder_1691 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1691()
            {
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                string json = "{\"error\":\"Unsupported Media Type\",\"reason\":\"missing\"}";
                return CustomizableMockHttpClient.GenerateHttpResponseObject(415, "Unsupported Media Type"
                    , json);
            }
        }

        private sealed class _Responder_1701 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1701(Document doc)
            {
                this.doc = doc;
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                IDictionary<string, object> responseObject = new Dictionary<string, object>();
                responseObject.Put("id", doc.GetId());
                responseObject.Put("ok", true);
                responseObject.Put("rev", doc.GetCurrentRevisionId());
                return CustomizableMockHttpClient.GenerateHttpResponseObject(responseObject);
            }

            private readonly Document doc;
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/55</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestContinuousPushReplicationGoesIdle()
        {
            // make sure we are starting empty
            NUnit.Framework.Assert.AreEqual(0, database.GetLastSequenceNumber());
            // add docs
            IDictionary<string, object> properties1 = new Dictionary<string, object>();
            properties1.Put("doc1", "testContinuousPushReplicationGoesIdle");
            Document doc1 = CreateDocWithProperties(properties1);
            // create a mock http client that serves as a mocked out sync gateway
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            // replication to do initial sync up - has to be continuous replication so the checkpoint id
            // matches the next continuous replication we're gonna do later.
            manager.SetDefaultHttpClientFactory(MockFactoryFactory(mockHttpClient));
            Replication firstPusher = database.CreatePushReplication(GetReplicationURL());
            firstPusher.SetContinuous(true);
            string checkpointId = firstPusher.RemoteCheckpointDocID();
            // save the checkpoint id for later usage
            // intercept checkpoint PUT request and return a 201 response with expected json
            mockHttpClient.SetResponder("_local", new _Responder_1762(checkpointId));
            // start the continuous replication
            CountDownLatch replicationIdleSignal = new CountDownLatch(1);
            LiteTestCase.ReplicationIdleObserver replicationIdleObserver = new LiteTestCase.ReplicationIdleObserver
                (replicationIdleSignal);
            firstPusher.AddChangeListener(replicationIdleObserver);
            firstPusher.Start();
            // wait until we get an IDLE event
            bool successful = replicationIdleSignal.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(successful);
            StopReplication(firstPusher);
            // the last sequence should be "1" at this point.  we will use this later
            string lastSequence = database.LastSequenceWithCheckpointId(checkpointId);
            NUnit.Framework.Assert.AreEqual("1", lastSequence);
            // start a second continuous replication
            Replication secondPusher = database.CreatePushReplication(GetReplicationURL());
            secondPusher.SetContinuous(true);
            string secondPusherCheckpointId = secondPusher.RemoteCheckpointDocID();
            NUnit.Framework.Assert.AreEqual(checkpointId, secondPusherCheckpointId);
            // when this goes to fetch the checkpoint, return the last sequence from the previous replication
            mockHttpClient.SetResponder("_local", new _Responder_1793(secondPusherCheckpointId
                , lastSequence));
            // start second replication
            replicationIdleSignal = new CountDownLatch(1);
            replicationIdleObserver = new LiteTestCase.ReplicationIdleObserver(replicationIdleSignal
                );
            secondPusher.AddChangeListener(replicationIdleObserver);
            secondPusher.Start();
            // wait until we get an IDLE event
            successful = replicationIdleSignal.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(successful);
            StopReplication(secondPusher);
        }

        private sealed class _Responder_1762 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1762(string checkpointId)
            {
                this.checkpointId = checkpointId;
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                string id = string.Format("_local/%s", checkpointId);
                string json = string.Format("{\"id\":\"%s\",\"ok\":true,\"rev\":\"0-2\"}", id);
                return CustomizableMockHttpClient.GenerateHttpResponseObject(201, "OK", json);
            }

            private readonly string checkpointId;
        }

        private sealed class _Responder_1793 : CustomizableMockHttpClient.Responder
        {
            public _Responder_1793(string secondPusherCheckpointId, string lastSequence)
            {
                this.secondPusherCheckpointId = secondPusherCheckpointId;
                this.lastSequence = lastSequence;
            }

            /// <exception cref="System.IO.IOException"></exception>
            public HttpResponse Execute(HttpRequestMessage httpUriRequest)
            {
                string id = string.Format("_local/%s", secondPusherCheckpointId);
                string json = string.Format("{\"id\":\"%s\",\"ok\":true,\"rev\":\"0-2\",\"lastSequence\":\"%s\"}"
                    , id, lastSequence);
                return CustomizableMockHttpClient.GenerateHttpResponseObject(200, "OK", json);
            }

            private readonly string secondPusherCheckpointId;

            private readonly string lastSequence;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        private Document CreateDocWithProperties(IDictionary<string, object> properties1)
        {
            Document doc1 = database.CreateDocument();
            UnsavedRevision revUnsaved = doc1.CreateRevision();
            revUnsaved.SetUserProperties(properties1);
            SavedRevision rev = revUnsaved.Save();
            NUnit.Framework.Assert.IsNotNull(rev);
            return doc1;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void DisabledTestCheckpointingWithServerError()
        {
            string remoteCheckpointDocId;
            string lastSequenceWithCheckpointIdInitial;
            string lastSequenceWithCheckpointIdFinal;
            Uri remote = GetReplicationURL();
            // add docs
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            CreateDocumentsForPushReplication(docIdTimestamp);
            // do push replication against mock replicator that fails to save remote checkpoint
            CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
            mockHttpClient.AddResponderFakeLocalDocumentUpdate404();
            manager.SetDefaultHttpClientFactory(MockFactoryFactory(mockHttpClient));
            Replication pusher = database.CreatePushReplication(remote);
            remoteCheckpointDocId = pusher.RemoteCheckpointDocID();
            lastSequenceWithCheckpointIdInitial = database.LastSequenceWithCheckpointId(remoteCheckpointDocId
                );
            RunReplication(pusher);
            IList<HttpWebRequest> capturedRequests = mockHttpClient.GetCapturedRequests();
            foreach (HttpWebRequest capturedRequest in capturedRequests)
            {
                if (capturedRequest is HttpPost)
                {
                    HttpPost capturedPostRequest = (HttpPost)capturedRequest;
                }
            }
            // sleep to allow for any "post-finished" activities on the replicator related to checkpointing
            Sharpen.Thread.Sleep(2000);
            // make sure local checkpoint is not updated
            lastSequenceWithCheckpointIdFinal = database.LastSequenceWithCheckpointId(remoteCheckpointDocId
                );
            string msg = "since the mock replicator rejected the PUT to _local/remoteCheckpointDocId, we "
                 + "would expect lastSequenceWithCheckpointIdInitial == lastSequenceWithCheckpointIdFinal";
            NUnit.Framework.Assert.AreEqual(msg, lastSequenceWithCheckpointIdFinal, lastSequenceWithCheckpointIdInitial
                );
            Log.D(Tag, "replication done");
        }

        public virtual void TestServerIsSyncGatewayVersion()
        {
            Replication pusher = database.CreatePushReplication(GetReplicationURL());
            NUnit.Framework.Assert.IsFalse(pusher.ServerIsSyncGatewayVersion("0.01"));
            pusher.SetServerType("Couchbase Sync Gateway/0.93");
            NUnit.Framework.Assert.IsTrue(pusher.ServerIsSyncGatewayVersion("0.92"));
            NUnit.Framework.Assert.IsFalse(pusher.ServerIsSyncGatewayVersion("0.94"));
        }

        /// <exception cref="System.Exception"></exception>
        private void PutReplicationOffline(Replication replication)
        {
            CountDownLatch wentOffline = new CountDownLatch(1);
            Replication.ChangeListener offlineChangeListener = new _ChangeListener_1894(wentOffline
                );
            replication.AddChangeListener(offlineChangeListener);
            replication.GoOffline();
            bool succeeded = wentOffline.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(succeeded);
            replication.RemoveChangeListener(offlineChangeListener);
        }

        private sealed class _ChangeListener_1894 : Replication.ChangeListener
        {
            public _ChangeListener_1894(CountDownLatch wentOffline)
            {
                this.wentOffline = wentOffline;
            }

            public void Changed(Replication.ChangeEvent @event)
            {
                if (!@event.GetSource().online)
                {
                    wentOffline.CountDown();
                }
            }

            private readonly CountDownLatch wentOffline;
        }

        /// <exception cref="System.Exception"></exception>
        private void PutReplicationOnline(Replication replication)
        {
            CountDownLatch wentOnline = new CountDownLatch(1);
            Replication.ChangeListener onlineChangeListener = new _ChangeListener_1915(wentOnline
                );
            replication.AddChangeListener(onlineChangeListener);
            replication.GoOnline();
            bool succeeded = wentOnline.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(succeeded);
            replication.RemoveChangeListener(onlineChangeListener);
        }

        private sealed class _ChangeListener_1915 : Replication.ChangeListener
        {
            public _ChangeListener_1915(CountDownLatch wentOnline)
            {
                this.wentOnline = wentOnline;
            }

            public void Changed(Replication.ChangeEvent @event)
            {
                if (@event.GetSource().online)
                {
                    wentOnline.CountDown();
                }
            }

            private readonly CountDownLatch wentOnline;
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-android/issues/243</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestDifferentCheckpointsFilteredReplication()
        {
            Replication pullerNoFilter = database.CreatePullReplication(GetReplicationURL());
            string noFilterCheckpointDocId = pullerNoFilter.RemoteCheckpointDocID();
            Replication pullerWithFilter1 = database.CreatePullReplication(GetReplicationURL(
                ));
            pullerWithFilter1.SetFilter("foo/bar");
            IDictionary<string, object> filterParams = new Dictionary<string, object>();
            filterParams.Put("a", "aval");
            filterParams.Put("b", "bval");
            pullerWithFilter1.SetDocIds(Arrays.AsList("doc3", "doc1", "doc2"));
            pullerWithFilter1.SetFilterParams(filterParams);
            string withFilterCheckpointDocId = pullerWithFilter1.RemoteCheckpointDocID();
            NUnit.Framework.Assert.IsFalse(withFilterCheckpointDocId.Equals(noFilterCheckpointDocId
                ));
            Replication pullerWithFilter2 = database.CreatePullReplication(GetReplicationURL(
                ));
            pullerWithFilter2.SetFilter("foo/bar");
            filterParams = new Dictionary<string, object>();
            filterParams.Put("b", "bval");
            filterParams.Put("a", "aval");
            pullerWithFilter2.SetDocIds(Arrays.AsList("doc2", "doc3", "doc1"));
            pullerWithFilter2.SetFilterParams(filterParams);
            string withFilterCheckpointDocId2 = pullerWithFilter2.RemoteCheckpointDocID();
            NUnit.Framework.Assert.IsTrue(withFilterCheckpointDocId.Equals(withFilterCheckpointDocId2
                ));
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestSetReplicationCookie()
        {
            Uri replicationUrl = GetReplicationURL();
            Replication puller = database.CreatePullReplication(replicationUrl);
            string cookieName = "foo";
            string cookieVal = "bar";
            bool isSecure = false;
            bool httpOnly = false;
            // expiration date - 1 day from now
            Calendar cal = Calendar.GetInstance();
            cal.SetTime(new DateTime());
            int numDaysToAdd = 1;
            cal.Add(Calendar.Date, numDaysToAdd);
            DateTime expirationDate = cal.GetTime();
            // set the cookie
            puller.SetCookie(cookieName, cookieVal, string.Empty, expirationDate, isSecure, httpOnly
                );
            // make sure it made it into cookie store and has expected params
            CookieStore cookieStore = puller.GetClientFactory().GetCookieStore();
            IList<Apache.Http.Cookie.Cookie> cookies = cookieStore.GetCookies();
            NUnit.Framework.Assert.AreEqual(1, cookies.Count);
            Apache.Http.Cookie.Cookie cookie = cookies[0];
            NUnit.Framework.Assert.AreEqual(cookieName, cookie.GetName());
            NUnit.Framework.Assert.AreEqual(cookieVal, cookie.GetValue());
            NUnit.Framework.Assert.AreEqual(replicationUrl.GetHost(), cookie.GetDomain());
            NUnit.Framework.Assert.AreEqual(replicationUrl.AbsolutePath, cookie.GetPath());
            NUnit.Framework.Assert.AreEqual(expirationDate, cookie.GetExpiryDate());
            NUnit.Framework.Assert.AreEqual(isSecure, cookie.IsSecure());
            // add a second cookie
            string cookieName2 = "foo2";
            puller.SetCookie(cookieName2, cookieVal, string.Empty, expirationDate, isSecure, 
                false);
            NUnit.Framework.Assert.AreEqual(2, cookieStore.GetCookies().Count);
            // delete cookie
            puller.DeleteCookie(cookieName2);
            // should only have the original cookie left
            NUnit.Framework.Assert.AreEqual(1, cookieStore.GetCookies().Count);
            NUnit.Framework.Assert.AreEqual(cookieName, cookieStore.GetCookies()[0].GetName()
                );
        }

        /// <summary>
        /// Whenever posting information directly to sync gateway via HTTP, the client
        /// must pause briefly to give it a chance to achieve internal consistency.
        /// </summary>
        /// <remarks>
        /// Whenever posting information directly to sync gateway via HTTP, the client
        /// must pause briefly to give it a chance to achieve internal consistency.
        /// This is documented in https://github.com/couchbase/sync_gateway/issues/228
        /// </remarks>
        private void WorkaroundSyncGatewayRaceCondition()
        {
            try
            {
                Sharpen.Thread.Sleep(5 * 1000);
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
        }
    }
}
