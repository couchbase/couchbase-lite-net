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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Apache.Http;
using Apache.Http.Client;
using Apache.Http.Client.Methods;
using Apache.Http.Entity;
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
			return new _HttpClientFactory_117(mockHttpClient);
		}

		private sealed class _HttpClientFactory_117 : HttpClientFactory
		{
			public _HttpClientFactory_117(CustomizableMockHttpClient mockHttpClient)
			{
				this.mockHttpClient = mockHttpClient;
			}

			public HttpClient GetHttpClient()
			{
				return mockHttpClient;
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
			HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_143(mockHttpClient
				);
			Uri remote = GetReplicationURL();
			manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
			Replication pusher = database.CreatePushReplication(remote);
			pusher.SetContinuous(true);
			CountDownLatch replicationCaughtUpSignal = new CountDownLatch(1);
			pusher.AddChangeListener(new _ChangeListener_158(replicationCaughtUpSignal));
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
			NUnit.Framework.Assert.IsTrue(doc.Purge());
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

		private sealed class _HttpClientFactory_143 : HttpClientFactory
		{
			public _HttpClientFactory_143(CustomizableMockHttpClient mockHttpClient)
			{
				this.mockHttpClient = mockHttpClient;
			}

			public HttpClient GetHttpClient()
			{
				return mockHttpClient;
			}

			private readonly CustomizableMockHttpClient mockHttpClient;
		}

		private sealed class _ChangeListener_158 : Replication.ChangeListener
		{
			public _ChangeListener_158(CountDownLatch replicationCaughtUpSignal)
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
			// TODO: CAssertNil(r1.doc_ids);
			// TODO: CAssertNil(r1.headers);
			// Check that the replication hasn't started running:
			NUnit.Framework.Assert.IsFalse(repl.IsRunning());
			NUnit.Framework.Assert.AreEqual(Replication.ReplicationStatus.ReplicationStopped, 
				repl.GetStatus());
			NUnit.Framework.Assert.AreEqual(0, repl.GetCompletedChangesCount());
			NUnit.Framework.Assert.AreEqual(0, repl.GetChangesCount());
			NUnit.Framework.Assert.IsNull(repl.GetLastError());
			RunReplication(repl);
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
			RunReplication(repl2);
			// make sure the doc has been added
			VerifyRemoteDocExists(remote, doc3Id);
			Log.D(Tag, "testPusher() finished");
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		private string CreateDocumentsForPushReplication(string docIdTimestamp)
		{
			string doc1Id;
			string doc2Id;
			// Create some documents:
			IDictionary<string, object> documentProperties = new Dictionary<string, object>();
			doc1Id = string.Format("doc1-%s", docIdTimestamp);
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
			RevisionInternal rev2 = database.PutRevision(new RevisionInternal(documentProperties
				, database), rev1.GetRevId(), false, status);
			NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
			documentProperties = new Dictionary<string, object>();
			doc2Id = string.Format("doc2-%s", docIdTimestamp);
			documentProperties.Put("_id", doc2Id);
			documentProperties.Put("baz", 666);
			documentProperties.Put("fnord", true);
			database.PutRevision(new RevisionInternal(documentProperties, database), null, false
				, status);
			NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
			return doc1Id;
		}

		private bool IsSyncGateway(Uri remote)
		{
			return (remote.Port == 4984 || remote.Port == 4984);
		}

		/// <exception cref="System.UriFormatException"></exception>
		private void VerifyRemoteDocExists(Uri remote, string doc1Id)
		{
			Uri replicationUrlTrailing = new Uri(string.Format("%s/", remote.ToExternalForm()
				));
			Uri pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
			Log.D(Tag, "Send http request to " + pathToDoc);
			CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
			BackgroundTask getDocTask = new _BackgroundTask_331(pathToDoc, doc1Id, httpRequestDoneSignal
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

		private sealed class _BackgroundTask_331 : BackgroundTask
		{
			public _BackgroundTask_331(Uri pathToDoc, string doc1Id, CountDownLatch httpRequestDoneSignal
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
		/// 	</summary>
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
			((Pusher)repl).SetCreateTarget(true);
			RunReplication(repl);
			// make sure doc1 is deleted
			Uri replicationUrlTrailing = new Uri(string.Format("%s/", remote.ToExternalForm()
				));
			Uri pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
			Log.D(Tag, "Send http request to " + pathToDoc);
			CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
			BackgroundTask getDocTask = new _BackgroundTask_466(pathToDoc, httpRequestDoneSignal
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

		private sealed class _BackgroundTask_466 : BackgroundTask
		{
			public _BackgroundTask_466(Uri pathToDoc, CountDownLatch httpRequestDoneSignal)
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
			view.SetMapReduce(new _Mapper_606(), null, "1");
			LiveQuery allDocsLiveQuery = view.CreateQuery().ToLiveQuery();
			allDocsLiveQuery.AddChangeListener(new _ChangeListener_616(numDocsBeforePull));
			// the first time this is called back, the rows will be empty.
			// but on subsequent times we should expect to get a non empty
			// row set.
			allDocsLiveQuery.Start();
			DoPullReplication();
			allDocsLiveQuery.Stop();
		}

		private sealed class _Mapper_606 : Mapper
		{
			public _Mapper_606()
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

		private sealed class _ChangeListener_616 : LiveQuery.ChangeListener
		{
			public _ChangeListener_616(int numDocsBeforePull)
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
			BackgroundTask getDocTask = new _BackgroundTask_694(pathToDoc1, docJson, httpRequestDoneSignal
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

		private sealed class _BackgroundTask_694 : BackgroundTask
		{
			public _BackgroundTask_694(Uri pathToDoc1, string docJson, CountDownLatch httpRequestDoneSignal
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
			replicator.Start();
			// now lets lookup existing replicator and stop it
			properties.Put("cancel", true);
			Replication activeReplicator = manager.GetReplicator(properties);
			activeReplicator.Stop();
			// wait for replication to finish
			bool didNotTimeOut = replicationDoneSignal.Await(30, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(didNotTimeOut);
			NUnit.Framework.Assert.IsFalse(activeReplicator.IsRunning());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestGetReplicatorWithAuth()
		{
			IDictionary<string, object> properties = GetPushReplicationParsedJson();
			Replication replicator = manager.GetReplicator(properties);
			NUnit.Framework.Assert.IsNotNull(replicator);
			NUnit.Framework.Assert.IsNotNull(replicator.GetAuthorizer());
			NUnit.Framework.Assert.IsTrue(replicator.GetAuthorizer() is FacebookAuthorizer);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestRunReplicationWithError()
		{
			HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_788();
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

		private sealed class _HttpClientFactory_788 : HttpClientFactory
		{
			public _HttpClientFactory_788()
			{
			}

			public HttpClient GetHttpClient()
			{
				CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
				int statusCode = 500;
				mockHttpClient.AddResponderFailAllRequests(statusCode);
				return mockHttpClient;
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
			replicator.GoOffline();
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
			Replication replicator = new Pusher(null, new Uri(dbUrlString), false, null);
			string relativeUrlString = replicator.BuildRelativeURLString("foo");
			string expected = "http://10.0.0.3:4984/todos/foo";
			NUnit.Framework.Assert.AreEqual(expected, relativeUrlString);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestBuildRelativeURLStringWithLeadingSlash()
		{
			string dbUrlString = "http://10.0.0.3:4984/todos/";
			Replication replicator = new Pusher(null, new Uri(dbUrlString), false, null);
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
			HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_966(mockHttpClient
				);
			Uri remote = GetReplicationURL();
			manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
			Replication puller = database.CreatePullReplication(remote);
			IDictionary<string, object> headers = new Dictionary<string, object>();
			headers.Put("foo", "bar");
			puller.SetHeaders(headers);
			RunReplication(puller);
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

		private sealed class _HttpClientFactory_966 : HttpClientFactory
		{
			public _HttpClientFactory_966(CustomizableMockHttpClient mockHttpClient)
			{
				this.mockHttpClient = mockHttpClient;
			}

			public HttpClient GetHttpClient()
			{
				return mockHttpClient;
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
			HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_1009(mockHttpClient
				);
			manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
			Document doc = database.CreateDocument();
			SavedRevision rev1a = doc.CreateRevision().Save();
			SavedRevision rev2a = rev1a.CreateRevision().Save();
			SavedRevision rev3a = rev2a.CreateRevision().Save();
			// delete the branch we've been using, then create a new one to replace it
			SavedRevision rev4a = rev3a.DeleteDocument();
			SavedRevision rev2b = rev1a.CreateRevision().Save(true);
			NUnit.Framework.Assert.AreEqual(rev2b.GetId(), doc.GetCurrentRevisionId());
			// sync with remote DB -- should push both leaf revisions
			Replication push = database.CreatePushReplication(GetReplicationURL());
			RunReplication(push);
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

		private sealed class _HttpClientFactory_1009 : HttpClientFactory
		{
			public _HttpClientFactory_1009(CustomizableMockHttpClient mockHttpClient)
			{
				this.mockHttpClient = mockHttpClient;
			}

			public HttpClient GetHttpClient()
			{
				return mockHttpClient;
			}

			private readonly CustomizableMockHttpClient mockHttpClient;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestRemoteConflictResolution()
		{
			// Create a document with two conflicting edits.
			Document doc = database.CreateDocument();
			SavedRevision rev1 = doc.CreateRevision().Save();
			SavedRevision rev2a = rev1.CreateRevision().Save();
			SavedRevision rev2b = rev1.CreateRevision().Save(true);
			// Push the conflicts to the remote DB.
			Replication push = database.CreatePushReplication(GetReplicationURL());
			RunReplication(push);
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
			// create a push replication
			Replication pusher = database.CreatePushReplication(remote);
			Log.D(Database.Tag, "created pusher: " + pusher);
			pusher.SetContinuous(true);
			pusher.Start();
			for (int i = 0; i < 5; i++)
			{
				Log.D(Database.Tag, "testOnlineOfflinePusher, i: " + i);
				// put the replication offline
				PutReplicationOffline(pusher);
				// add a document
				string docFieldName = "testOnlineOfflinePusher" + i;
				string docFieldVal = "foo" + i;
				IDictionary<string, object> properties = new Dictionary<string, object>();
				properties.Put(docFieldName, docFieldVal);
				CreateDocumentWithProperties(database, properties);
				// add a response listener to wait for a bulk_docs request from the pusher
				CountDownLatch gotBulkDocsRequest = new CountDownLatch(1);
				CustomizableMockHttpClient.ResponseListener bulkDocsListener = new _ResponseListener_1145
					(gotBulkDocsRequest);
				mockHttpClient.AddResponseListener(bulkDocsListener);
				// put the replication online, which should trigger it to send outgoing bulk_docs request
				PutReplicationOnline(pusher);
				// wait until we get a bulk docs request
				Log.D(Database.Tag, "waiting for bulk docs request");
				bool succeeded = gotBulkDocsRequest.Await(120, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(succeeded);
				Log.D(Database.Tag, "got bulk docs request, verifying captured requests");
				mockHttpClient.RemoveResponseListener(bulkDocsListener);
				// workaround bug https://github.com/couchbase/couchbase-lite-android/issues/219
				Sharpen.Thread.Sleep(2000);
				// make sure that doc was pushed out in a bulk docs request
				bool foundExpectedDoc = false;
				IList<HttpWebRequest> capturedRequests = mockHttpClient.GetCapturedRequests();
				foreach (HttpWebRequest capturedRequest in capturedRequests)
				{
					Log.D(Database.Tag, "captured request: " + capturedRequest);
					if (capturedRequest is HttpPost)
					{
						HttpPost capturedPostRequest = (HttpPost)capturedRequest;
						Log.D(Database.Tag, "capturedPostRequest: " + capturedPostRequest.GetURI().GetPath
							());
						if (capturedPostRequest.GetURI().GetPath().EndsWith("_bulk_docs"))
						{
							ArrayList docs = CustomizableMockHttpClient.ExtractDocsFromBulkDocsPost(capturedRequest
								);
							NUnit.Framework.Assert.AreEqual(1, docs.Count);
							IDictionary<string, object> doc = (IDictionary)docs[0];
							Log.D(Database.Tag, "doc from captured request: " + doc);
							Log.D(Database.Tag, "docFieldName: " + docFieldName);
							Log.D(Database.Tag, "expected docFieldVal: " + docFieldVal);
							Log.D(Database.Tag, "actual doc.get(docFieldName): " + doc.Get(docFieldName));
							NUnit.Framework.Assert.AreEqual(docFieldVal, doc.Get(docFieldName));
							foundExpectedDoc = true;
						}
					}
				}
				NUnit.Framework.Assert.IsTrue(foundExpectedDoc);
				mockHttpClient.ClearCapturedRequests();
			}
		}

		private sealed class _ResponseListener_1145 : CustomizableMockHttpClient.ResponseListener
		{
			public _ResponseListener_1145(CountDownLatch gotBulkDocsRequest)
			{
				this.gotBulkDocsRequest = gotBulkDocsRequest;
			}

			public void ResponseSent(HttpRequestMessage httpUriRequest, HttpResponse response
				)
			{
				if (httpUriRequest.GetURI().GetPath().EndsWith("_bulk_docs"))
				{
					gotBulkDocsRequest.CountDown();
				}
			}

			private readonly CountDownLatch gotBulkDocsRequest;
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

		/// <exception cref="System.Exception"></exception>
		private void PutReplicationOffline(Replication replication)
		{
			CountDownLatch wentOffline = new CountDownLatch(1);
			Replication.ChangeListener offlineChangeListener = new _ChangeListener_1260(wentOffline
				);
			replication.AddChangeListener(offlineChangeListener);
			replication.GoOffline();
			bool succeeded = wentOffline.Await(30, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(succeeded);
			replication.RemoveChangeListener(offlineChangeListener);
		}

		private sealed class _ChangeListener_1260 : Replication.ChangeListener
		{
			public _ChangeListener_1260(CountDownLatch wentOffline)
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
			Replication.ChangeListener onlineChangeListener = new _ChangeListener_1281(wentOnline
				);
			replication.AddChangeListener(onlineChangeListener);
			replication.GoOnline();
			bool succeeded = wentOnline.Await(30, TimeUnit.Seconds);
			NUnit.Framework.Assert.IsTrue(succeeded);
			replication.RemoveChangeListener(onlineChangeListener);
		}

		private sealed class _ChangeListener_1281 : Replication.ChangeListener
		{
			public _ChangeListener_1281(CountDownLatch wentOnline)
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
