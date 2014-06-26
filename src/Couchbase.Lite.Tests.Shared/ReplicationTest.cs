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


namespace Couchbase.Lite.Replicator
{
	public class ReplicationTest : LiteTestCase
	{
        public const string Tag = "ReplicationTest";

        private CountDownLatch ReplicationWatcherThread(Replication replication)
        {
            var started = replication.IsRunning;
            var doneSignal = new CountDownLatch(1);

            Task.Factory.StartNew(()=>
            {
                var done = false;
                while (!done)
                {
                    if (!started) {
                        started = replication.IsRunning;
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
                        Thread.Sleep(1000);
                    }
                    catch (Exception e)
                    {
                        Runtime.PrintStackTrace(e);
                    }
                }
                doneSignal.CountDown();
            });

            return doneSignal;
        }

        private class ReplicationObserver 
        {
            private bool replicationFinished = false;

            private readonly CountDownLatch doneSignal;

            internal ReplicationObserver(CountDownLatch doneSignal)
            {
                this.doneSignal = doneSignal;
            }

            public void Changed(object sender, ReplicationChangeEventArgs args)
            {
                Replication replicator = args.Source;
                Log.D(Tag, replicator + " changed: " + replicator.CompletedChangesCount + " / " + replicator.ChangesCount);

                if (replicator.CompletedChangesCount < 0)
                {
                    var msg = replicator + ": replicator.CompletedChangesCount < 0";
                    Log.D(Tag, msg);
                    // throw new RuntimeException(msg);
                }

                if (replicator.ChangesCount < 0)
                {
                    var msg = replicator + ": replicator.ChangesCount < 0";
                    Log.D(Tag, msg);
                    throw new RuntimeException(msg);
                }

                if (replicator.CompletedChangesCount > replicator.ChangesCount)
                {
                    var msg = "replicator.CompletedChangesCount : " + replicator.CompletedChangesCount +
                        " > replicator.ChangesCount : " + replicator.ChangesCount;
                    Log.D(Tag, msg);
                    // throw new RuntimeException(msg);
                }

                if (!replicator.IsRunning)
                {
                    this.replicationFinished = true;
                    string msg = "ReplicationFinishedObserver.changed called, set replicationFinished to true";
                    Log.D(Tag, msg);
                    this.doneSignal.CountDown();
                    System.Threading.Thread.Sleep(5000);
                }
                else
                {
                    string msg = string.Format("ReplicationFinishedObserver.changed called, but replicator still running, so ignore it");
                    Log.D(ReplicationTest.Tag, msg);
                }
            }

            internal virtual bool IsReplicationFinished()
            {
                return this.replicationFinished;
            }
        }

        private void RunReplication(Replication replication)
        {
            var replicationDoneSignal = new CountDownLatch(1);
            var observer = new ReplicationObserver(replicationDoneSignal);
            replication.Changed += observer.Changed;
            replication.Start();

            var replicationDoneSignalPolling = ReplicationWatcherThread(replication);

            Log.D(Tag, "Waiting for replicator to finish.");

            try
            {
                var success = replicationDoneSignal.Await(TimeSpan.FromSeconds(10));
                Assert.IsTrue(success);
                success = replicationDoneSignalPolling.Await(TimeSpan.FromSeconds(10));
                Assert.IsTrue(success);

                Log.D(Tag, "replicator finished");
            }
            catch (Exception e)
            {
                Runtime.PrintStackTrace(e);
            }

            replication.Changed -= observer.Changed;
        }

        private void WorkaroundSyncGatewayRaceCondition() {
            Thread.Sleep(5 * 1000);
        }

        [SetUp]
        public void Setup()
        {
            Log.V(Tag, "------");
        }

		/// <exception cref="System.Exception"></exception>
        [Test]
		public void TestPusher()
		{
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

			const bool continuous = false;
			var repl = database.CreatePushReplication(remote);
            repl.Continuous = continuous;

			// Check the replication's properties:
			Assert.AreEqual(database, repl.LocalDatabase);
			Assert.AreEqual(remote, repl.RemoteUrl);
			Assert.IsFalse(repl.IsPull);
            Assert.IsFalse(repl.Continuous);
            //Assert.IsTrue(repl.CreateTarget);
			Assert.IsNull(repl.Filter);
			Assert.IsNull(repl.FilterParams);
			// TODO: CAssertNil(r1.doc_ids);
			// TODO: CAssertNil(r1.headers);
			// Check that the replication hasn't started running:
			Assert.IsFalse(repl.IsRunning);
            Assert.AreEqual((int)repl.Status, (int)ReplicationStatus.Stopped);
			Assert.AreEqual(0, repl.CompletedChangesCount);
			Assert.AreEqual(0, repl.ChangesCount);
			Assert.IsNull(repl.LastError);
            RunReplication(repl);
			// make sure doc1 is there
			// TODO: make sure doc2 is there (refactoring needed)
            var replicationUrlTrailing = new Uri(string.Format("{0}/", remote));
			var pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
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
                    responseTask.Wait(TimeSpan.FromSeconds(10));
                    response = responseTask.Result;
                    var statusLine = response.StatusCode;
                    Assert.IsTrue(statusLine == HttpStatusCode.OK);
                    if (statusLine == HttpStatusCode.OK)
                    {
                        var responseStringTask = response.Content.ReadAsStringAsync();
                        responseStringTask.Wait(TimeSpan.FromSeconds(10));
                        responseString = responseStringTask.Result;
                        Assert.IsTrue(responseString.Contains(doc1Id));
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

            //Closes the connection.
            Log.D(Tag, "Waiting for http request to finish");
            try
            {
                var result = httpRequestDoneSignal.Await(TimeSpan.FromSeconds(10));
                Assert.IsTrue(result, "Could not retrieve the new doc from the sync gateway.");
                Log.D(Tag, "http request finished");
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
            Log.D(Tag, "testPusher() finished");
        }

		/// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPusherDeletedDoc()
		{
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
            ((Pusher)repl).CreateTarget = true;
			RunReplication(repl);
			// make sure doc1 is deleted
			var replicationUrlTrailing = new Uri(string.Format ("{0}/", remote));
			var pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
			Log.D(Tag, "Send http request to " + pathToDoc);
			var httpRequestDoneSignal = new CountDownLatch(1);
			Task.Factory.StartNew(async ()=>
                {
                    var httpclient = new HttpClient();
                    try
                    {
						var getDocResponse = await httpclient.GetAsync(pathToDoc.ToString());
                        var statusLine = getDocResponse.StatusCode;
                        Log.D(ReplicationTest.Tag, "statusLine " + statusLine);
                        Assert.AreEqual(HttpStatusCode.NotFound, statusLine.GetStatusCode());                        
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
                });
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
			var docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
            var doc2Id = string.Format("doc2-{0}", docIdTimestamp);
            AddDocWithId(doc1Id, "attachment.png");
            AddDocWithId(doc2Id, "attachment2.png");

			// workaround for https://github.com/couchbase/sync_gateway/issues/228
            Sharpen.Thread.Sleep(3000);
            DoPullReplication();
            Sharpen.Thread.Sleep(3000);

			Log.D(Tag, "Fetching doc1 via id: " + doc1Id);
            var doc1 = database.GetExistingDocument(doc1Id);
			Assert.IsNotNull(doc1);
            Assert.IsNotNull(doc1.CurrentRevisionId);
            Assert.IsTrue(doc1.CurrentRevisionId.StartsWith("1-"));
            Assert.IsNotNull(doc1.Properties);
            Assert.AreEqual(1, doc1.GetProperty("foo"));

            Log.D(Tag, "Fetching doc2 via id: " + doc2Id);
            var doc2 = database.GetExistingDocument(doc2Id);
			Assert.IsNotNull(doc2);
            Assert.IsNotNull(doc2.CurrentRevisionId);
            Assert.IsTrue(doc2.CurrentRevisionId.StartsWith("1-"));
            Assert.IsNotNull(doc2.Properties);
            Assert.AreEqual(1, doc2.GetProperty("foo"));
            Log.D(Tag, "testPuller() finished");
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
		public void TestPullerWithLiveQuery()
		{
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
                var httpclient = new HttpClient(); //CouchbaseLiteHttpClientFactory.Instance.GetHttpClient();
                HttpResponseMessage response;

                try
                {
                    var request = new HttpRequestMessage();
                    request.Headers.Add("Accept", "*/*");

                    var postTask = httpclient.PutAsync(pathToDoc1.AbsoluteUri, new StringContent(docJson, Encoding.UTF8, "application/json"));
                    postTask.Wait();

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

                httpRequestDoneSignal.CountDown();
            });

			Log.D(Tag, "Waiting for http request to finish");
			try
			{
				httpRequestDoneSignal.Await(TimeSpan.FromSeconds(10));
				Log.D(Tag, "http request finished");
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
		public void TestGetReplicator()
		{
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
            Thread.Sleep(1000);

            Assert.IsFalse(replicator.IsRunning);
			activeReplicators = new Replication[database.ActiveReplicators.Count];
			database.ActiveReplicators.CopyTo(activeReplicators, 0);
			Assert.AreEqual(0, activeReplicators.Length);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
		public void TestGetReplicatorWithAuth()
		{
            var email = "jchris@couchbase.com";
            var accessToken = "fake_access_token";
            var remoteUrl = GetReplicationURL().ToString();
            FacebookAuthorizer.RegisterAccessToken(accessToken, email, remoteUrl);

            var url = GetReplicationURLWithoutCredentials();
            Replication replicator = database.CreatePushReplication(url);
            replicator.Authorizer = new FacebookAuthorizer(email);

			Assert.IsNotNull(replicator);
            Assert.IsNotNull(replicator.Authorizer);
            Assert.IsTrue(replicator.Authorizer is FacebookAuthorizer);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
		public void TestRunReplicationWithError()
		{
            var mockHttpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = mockHttpClientFactory;

            var mockHttpHandler = (MockHttpRequestHandler)mockHttpClientFactory.HttpHandler;
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
            var email = "jchris@couchbase.com";
            var accessToken = "fake_access_token";
            var remoteUrl = GetReplicationURL().ToString();
            FacebookAuthorizer.RegisterAccessToken(accessToken, email, remoteUrl);

            var replicator = database.CreatePullReplication(GetReplicationURL());
            replicator.Authorizer = new FacebookAuthorizer(email);

            RunReplication(replicator);

            Assert.IsNotNull(replicator.LastError);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
		public void TestGoOffline()
		{
			var remote = GetReplicationURL();
			var repl = database.CreatePullReplication(remote);
			repl.Continuous = true;
            repl.Start();
			repl.GoOffline();
            Assert.IsTrue(repl.Status == ReplicationStatus.Offline);
		}


		/// <exception cref="System.Exception"></exception>
        [Test]
		public virtual void TestBuildRelativeURLString()
		{
			string dbUrlString = "http://10.0.0.3:4984/todos/";
			Replication replicator = new Pusher(null, new Uri(dbUrlString), false, null);
			string relativeUrlString = replicator.BuildRelativeURLString("foo");
			string expected = "http://10.0.0.3:4984/todos/foo";
			NUnit.Framework.Assert.AreEqual(expected, relativeUrlString);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
		public virtual void TestBuildRelativeURLStringWithLeadingSlash()
		{
			string dbUrlString = "http://10.0.0.3:4984/todos/";
			Replication replicator = new Pusher(null, new Uri(dbUrlString), false, null);
			string relativeUrlString = replicator.BuildRelativeURLString("/foo");
			string expected = "http://10.0.0.3:4984/todos/foo";
			NUnit.Framework.Assert.AreEqual(expected, relativeUrlString);
		}

		/// <exception cref="System.Exception"></exception>
        [Test]
		public void TestChannels()
		{
            Uri remote = GetReplicationURL();
            Replication replicator = database.CreatePullReplication(remote);

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
            Uri fakeRemoteURL = new Uri("http://couchbase.com/no_such_db");
            Replication r1 = database.CreatePullReplication(fakeRemoteURL);

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
            var mockHttpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = mockHttpClientFactory;

            var mockHttpHandler = (MockHttpRequestHandler)mockHttpClientFactory.HttpHandler;
            mockHttpHandler.AddResponderThrowExceptionAllRequests();

            Uri remote = GetReplicationURL();
            Replication puller = database.CreatePullReplication(remote);
            var headers = new Dictionary<string, string>();
            headers["foo"] = "bar";
            puller.Headers = headers;
            RunReplication(puller);
            Assert.IsNotNull(puller.LastError);

            var foundFooHeader = false;
            var requests = mockHttpHandler.GetCapturedRequests();

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
            var httpClientFactory = new MockHttpClientFactory();
            var httpHandler = (MockHttpRequestHandler) httpClientFactory.HttpHandler; 
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

            var foundRevsDiff = false;
            var capturedRequests = httpHandler.GetCapturedRequests();
            foreach (var httpRequest in capturedRequests) 
            {
                var uriString = httpRequest.RequestUri.ToString();
                if (uriString.EndsWith("_revs_diff"))
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
            // Create a document with two conflicting edits.
            var doc = database.CreateDocument();
            var rev1 = doc.CreateRevision().Save();
            var rev2a = rev1.CreateRevision().Save();
            var rev2b = rev1.CreateRevision().Save(true);

            // Push the conflicts to the remote DB.
            var pusher = database.CreatePushReplication(GetReplicationURL());
            RunReplication(pusher);

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
            var docs = new JArray();
            docs.Add(rev3aBody);
            docs.Add(rev3bBody);
            requestBody.Put("docs", docs);

            // Make the _bulk_docs request.
            var client = new HttpClient();
            var bulkDocsUrl = GetReplicationURL().ToString() + "/_bulk_docs";
            var request = new HttpRequestMessage(HttpMethod.Post, bulkDocsUrl);
            request.Headers.Add("Accept", "*/*");
            request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            var response = client.SendAsync(request).Result;

            // Check the response to make sure everything worked as it should.
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            var rawResponse = response.Content.ReadAsStringAsync().Result;
            var resultArray = Manager.GetObjectMapper().ReadValue<JArray>(rawResponse);
            Assert.AreEqual(2, resultArray.Count);
            foreach (var value in resultArray.Values<JObject>())
            {
                Assert.IsNull(value["error"]);
            }

            WorkaroundSyncGatewayRaceCondition();

            // Pull the remote changes.
            Replication puller = database.CreatePullReplication(GetReplicationURL());
            RunReplication(puller);

            // Make sure the conflict was resolved locally.
            Assert.AreEqual(1, doc.ConflictingRevisions.Count());
        }

	}
}
