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
using System.IO;
using System.Net;






using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;

using Couchbase.Lite.Util;
using NUnit.Framework;

using Sharpen;
using System.Threading.Tasks;
using System.Net.Http;

namespace Couchbase.Lite.Replicator
{
	public class ReplicationTest : LiteTestCase
	{
		public const string Tag = "Replicator";

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPusher()
		{
			CountDownLatch replicationDoneSignal = new CountDownLatch(1);
			Uri remote = GetReplicationURL();
			string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
			// Create some documents:
			IDictionary<string, object> documentProperties = new Dictionary<string, object>();
			string doc1Id = string.Format("doc1-%s", docIdTimestamp);
			documentProperties["_id"] = doc1Id;
			documentProperties["foo"] = 1;
			documentProperties["bar"] = false;
			Body body = new Body(documentProperties);
			RevisionInternal rev1 = new RevisionInternal(body, database);
			Status status = new Status();
			rev1 = database.PutRevision(rev1, null, false, status);
            NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			documentProperties.Put("_rev", rev1.GetRevId());
			documentProperties["UPDATED"] = true;
			RevisionInternal rev2 = database.PutRevision(new RevisionInternal(documentProperties
				, database), rev1.GetRevId(), false, status);
            NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			documentProperties = new Dictionary<string, object>();
			string doc2Id = string.Format("doc2-%s", docIdTimestamp);
			documentProperties["_id"] = doc2Id;
			documentProperties["baz"] = 666;
			documentProperties["fnord"] = true;
			database.PutRevision(new RevisionInternal(documentProperties, database), null, false
				, status);
            NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			bool continuous = false;
			Replication repl = database.CreatePushReplication(remote);
            repl.Continuous = continuous;
			repl.SetCreateTarget(true);
			// Check the replication's properties:
			NUnit.Framework.Assert.AreEqual(database, repl.GetLocalDatabase());
			NUnit.Framework.Assert.AreEqual(remote, repl.GetRemoteUrl());
			NUnit.Framework.Assert.IsFalse(repl.IsPull());
			NUnit.Framework.Assert.IsFalse(repl.IsContinuous());
			NUnit.Framework.Assert.IsTrue(repl.ShouldCreateTarget());
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
			// TODO: make sure doc2 is there (refactoring needed)
			Uri replicationUrlTrailing = new Uri(string.Format("%s/", remote.ToString()
				));
			Uri pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
			Log.D(Tag, "Send http request to " + pathToDoc);
			CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
            var getDocTask = Task.Factory.StartNew(()=>
                {
                    var httpclient = new HttpClient();
                    HttpResponse response;
                    string responseString = null;
                    try
                    {
                        response = httpclient.Execute(new HttpGet(pathToDoc.ToString()));
                        StatusLine statusLine = response.GetStatusLine();
                        NUnit.Framework.Assert.IsTrue(statusLine.GetStatusCode() == HttpStatusCode.OK);
                        if (statusLine.GetStatusCode() == HttpStatusCode.OK)
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
                });
			//Closes the connection.
            getDocTask.Start();
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
			Log.D(Tag, "testPusher() finished");
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
			documentProperties["_id"] = doc1Id;
			documentProperties["foo"] = 1;
			documentProperties["bar"] = false;
			Body body = new Body(documentProperties);
			RevisionInternal rev1 = new RevisionInternal(body, database);
			Status status = new Status();
			rev1 = database.PutRevision(rev1, null, false, status);
            NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			documentProperties.Put("_rev", rev1.GetRevId());
			documentProperties["UPDATED"] = true;
			documentProperties["_deleted"] = true;
			RevisionInternal rev2 = database.PutRevision(new RevisionInternal(documentProperties
				, database), rev1.GetRevId(), false, status);
			NUnit.Framework.Assert.IsTrue(status.GetCode() >= 200 && status.GetCode() < 300);
			Replication repl = database.CreatePushReplication(remote);
			((Pusher)repl).SetCreateTarget(true);
			RunReplication(repl);
			// make sure doc1 is deleted
			Uri replicationUrlTrailing = new Uri(string.Format("%s/", remote.ToString()
				));
			Uri pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
			Log.D(Tag, "Send http request to " + pathToDoc);
			CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
            var getDocTask = Task.Factory.StartNew(()=>
                {
                    var httpclient = new HttpClient();
                    HttpResponseMessage response;
                    string responseString = null;
                    try
                    {
                        var responseTask = httpclient.GetAsync(pathToDoc.ToString());
                        responseTask.Wait();
                        response = responseTask.Result;
                        var statusLine = response.StatusCode;
                        Log.D(ReplicationTest.Tag, "statusLine " + statusLine);
                        NUnit.Framework.Assert.AreEqual(HttpStatusCode.NotFound, statusLine.GetStatusCode()
                        );
                    }
                    catch (ProtocolViolationException e)
                    {
                        NUnit.Framework.Assert.IsNull("Got ClientProtocolException: " + e.Message, e);
                    }
                    catch (IOException e)
                    {
                        NUnit.Framework.Assert.IsNull("Got IOException: " + e.Message, e);
                    }
                    finally
                    {
                        httpRequestDoneSignal.CountDown();
                    }
                });
            getDocTask.Start();
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

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPuller()
		{
			string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
			string doc1Id = string.Format("doc1-%s", docIdTimestamp);
			string doc2Id = string.Format("doc2-%s", docIdTimestamp);
			AddDocWithId(doc1Id, "attachment.png");
			AddDocWithId(doc2Id, "attachment2.png");
			// workaround for https://github.com/couchbase/sync_gateway/issues/228
			Sharpen.Thread.Sleep(1000);
			DoPullReplication();
			Log.D(Tag, "Fetching doc1 via id: " + doc1Id);
			RevisionInternal doc1 = database.GetDocumentWithIDAndRev(doc1Id, null, EnumSet.NoneOf
				<TDContentOptions>());
			NUnit.Framework.Assert.IsNotNull(doc1);
			NUnit.Framework.Assert.IsTrue(doc1.GetRevId().StartsWith("1-"));
			NUnit.Framework.Assert.AreEqual(1, doc1.Properties.Get("foo"));
			Log.D(Tag, "Fetching doc2 via id: " + doc2Id);
			RevisionInternal doc2 = database.GetDocumentWithIDAndRev(doc2Id, null, EnumSet.NoneOf
				<TDContentOptions>());
			NUnit.Framework.Assert.IsNotNull(doc2);
			NUnit.Framework.Assert.IsTrue(doc2.GetRevId().StartsWith("1-"));
			NUnit.Framework.Assert.AreEqual(1, doc2.Properties.Get("foo"));
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
		}

		private void DoPullReplication()
		{
			Uri remote = GetReplicationURL();
			CountDownLatch replicationDoneSignal = new CountDownLatch(1);
			Replication repl = (Replication)database.CreatePullReplication(remote);
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
				InputStream attachmentStream = GetAsset(attachmentName);
				ByteArrayOutputStream baos = new ByteArrayOutputStream();
				IOUtils.Copy(attachmentStream, baos);
				string attachmentBase64 = Base64.EncodeBytes(baos.ToByteArray());
				docJson = string.Format("{\"foo\":1,\"bar\":false, \"_attachments\": { \"i_use_couchdb.png\": { \"content_type\": \"image/png\", \"data\": \"%s\" } } }"
					, attachmentBase64);
			}
			else
			{
				docJson = "{\"foo\":1,\"bar\":false}";
			}
			// push a document to server
			Uri replicationUrlTrailingDoc1 = new Uri(string.Format("%s/%s", GetReplicationURL
				().ToString(), docId));
			Uri pathToDoc1 = new Uri(replicationUrlTrailingDoc1, docId);
			Log.D(Tag, "Send http request to " + pathToDoc1);
			CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
            var getDocTask = Task.Factory.StartNew(()=>
                {
                    HttpClient httpclient = new HttpClient();
                    HttpResponseMessage response;
                    string responseString = null;
                    try
                    {
                        var postTask = httpclient.PostAsJsonAsync(pathToDoc1.AbsoluteUri, docJson);
                        postTask.Wait();
                        response = postTask.Result;
                        var statusLine = response.StatusCode;
                        Log.D(ReplicationTest.Tag, "Got response: " + statusLine);
                        NUnit.Framework.Assert.IsTrue(statusLine.GetStatusCode() == HttpStatusCode.Created);
                    }
                    catch (ProtocolViolationException e)
                    {
                        NUnit.Framework.Assert.IsNull("Got ClientProtocolException: " + e.Message, e);
                    }
                    catch (IOException e)
                    {
                        NUnit.Framework.Assert.IsNull("Got IOException: " + e.Message, e);
                    }
                    httpRequestDoneSignal.CountDown();
                });
            getDocTask.Start();
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

		/// <exception cref="System.Exception"></exception>
		public virtual void TestGetReplicator()
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["source"] = DefaultTestDb;
			properties.Put("target", GetReplicationURL().ToString());
			Replication replicator = manager.GetReplicator(properties);
			NUnit.Framework.Assert.IsNotNull(replicator);
			NUnit.Framework.Assert.AreEqual(GetReplicationURL().ToString(), replicator.
				GetRemoteUrl().ToString());
			NUnit.Framework.Assert.IsTrue(!replicator.IsPull());
			NUnit.Framework.Assert.IsFalse(replicator.IsContinuous());
			NUnit.Framework.Assert.IsFalse(replicator.IsRunning());
			// start the replicator
			replicator.Start();
			// now lets lookup existing replicator and stop it
			properties["cancel"] = true;
			Replication activeReplicator = manager.GetReplicator(properties);
			activeReplicator.Stop();
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

		private void RunReplication(Replication replication)
		{
			CountDownLatch replicationDoneSignal = new CountDownLatch(1);
			ReplicationTest.ReplicationObserver replicationObserver = new ReplicationTest.ReplicationObserver
				(this, replicationDoneSignal);
			replication.AddChangeListener(replicationObserver);
			replication.Start();
			CountDownLatch replicationDoneSignalPolling = ReplicationWatcherThread(replication
				);
			Log.D(Tag, "Waiting for replicator to finish");
			try
			{
				bool success = replicationDoneSignal.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(success);
				success = replicationDoneSignalPolling.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(success);
				Log.D(Tag, "replicator finished");
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
		}

		private CountDownLatch ReplicationWatcherThread(Replication replication)
		{
			CountDownLatch doneSignal = new CountDownLatch(1);
			new Sharpen.Thread(new _Runnable_482(replication, doneSignal)).Start();
			return doneSignal;
		}

		private sealed class _Runnable_482 : Runnable
		{
			public _Runnable_482(Replication replication, CountDownLatch doneSignal)
			{
				this.replication = replication;
				this.doneSignal = doneSignal;
			}

			public void Run()
			{
				bool started = false;
				bool done = false;
				while (!done)
				{
					if (replication.IsRunning())
					{
						started = true;
					}
					bool statusIsDone = (replication.GetStatus() == Replication.ReplicationStatus.ReplicationStopped
						 || replication.GetStatus() == Replication.ReplicationStatus.ReplicationIdle);
					if (started && statusIsDone)
					{
						done = true;
					}
					try
					{
						Sharpen.Thread.Sleep(500);
					}
					catch (Exception e)
					{
						Sharpen.Runtime.PrintStackTrace(e);
					}
				}
				doneSignal.CountDown();
			}

			private readonly Replication replication;

			private readonly CountDownLatch doneSignal;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestRunReplicationWithError()
		{
            var mockHttpClientFactory = new AlwaysFailingClientFactory();
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

		/// <exception cref="System.Exception"></exception>
		public virtual void TestReplicatorErrorStatus()
		{
			// register bogus fb token
			IDictionary<string, object> facebookTokenInfo = new Dictionary<string, object>();
			facebookTokenInfo.Put("email", "jchris@couchbase.com");
			facebookTokenInfo.Put("remote_url", GetReplicationURL().ToString());
			facebookTokenInfo["access_token"] = "fake_access_token";
			string destUrl = string.Format("/_facebook_token", DefaultTestDb);
			IDictionary<string, object> result = (IDictionary<string, object>)SendBody("POST"
                , destUrl, facebookTokenInfo, (int)StatusCode.Ok, null);
			Log.V(Tag, string.Format("result %s", result));
			// start a replicator
			IDictionary<string, object> properties = GetPullReplicationParsedJson();
			Replication replicator = manager.GetReplicator(properties);
			replicator.Start();
			bool foundError = false;
			for (int i = 0; i < 10; i++)
			{
				// wait a few seconds
				Sharpen.Thread.Sleep(5 * 1000);
				// expect an error since it will try to contact the sync gateway with this bogus login,
				// and the sync gateway will reject it.
                AList<object> activeTasks = (AList<object>)Send("GET", "/_active_tasks", StatusCode.Ok
					, null);
				Log.D(Tag, "activeTasks: " + activeTasks);
				IDictionary<string, object> activeTaskReplication = (IDictionary<string, object>)
					activeTasks[0];
				foundError = (activeTaskReplication.Get("error") != null);
				if (foundError == true)
				{
					break;
				}
			}
			NUnit.Framework.Assert.IsTrue(foundError);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestFetchRemoteCheckpointDoc()
		{
			HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_583();
			Log.D("TEST", "testFetchRemoteCheckpointDoc() called");
			string dbUrlString = "http://fake.test-url.com:4984/fake/";
			Uri remote = new Uri(dbUrlString);
			database.SetLastSequence("1", remote, true);
			// otherwise fetchRemoteCheckpoint won't contact remote
			Replication replicator = new Pusher(database, remote, false, mockHttpClientFactory
				, manager.GetWorkExecutor());
			CountDownLatch doneSignal = new CountDownLatch(1);
			ReplicationTest.ReplicationObserver replicationObserver = new ReplicationTest.ReplicationObserver
				(this, doneSignal);
            replicator.Changed += replicationObserver.Changed;
			replicator.FetchRemoteCheckpointDoc();
			Log.D(Tag, "testFetchRemoteCheckpointDoc() Waiting for replicator to finish");
			try
			{
				bool succeeded = doneSignal.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(succeeded);
				Log.D(Tag, "testFetchRemoteCheckpointDoc() replicator finished");
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
			string errorMessage = "Since we are passing in a mock http client that always throws "
				 + "errors, we expect the replicator to be in an error state";
			NUnit.Framework.Assert.IsNotNull(errorMessage, replicator.GetLastError());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestGoOffline()
		{
			Uri remote = GetReplicationURL();
			CountDownLatch replicationDoneSignal = new CountDownLatch(1);
			Replication repl = database.CreatePullReplication(remote);
			repl.Continuous = true;
            repl.Start();
			repl.GoOffline();
			NUnit.Framework.Assert.IsTrue(repl.GetStatus() == Replication.ReplicationStatus.ReplicationOffline
				);
		}

		internal class ReplicationObserver 
		{
			public bool replicationFinished = false;

			private CountDownLatch doneSignal;

			internal ReplicationObserver(ReplicationTest _enclosing, CountDownLatch doneSignal
				)
			{
				this._enclosing = _enclosing;
				this.doneSignal = doneSignal;
			}

            public virtual void Changed(object sender, Replication.ReplicationChangeEventArgs args)
			{
                Replication replicator = args.Source;
				if (!replicator.IsRunning())
				{
					this.replicationFinished = true;
					string msg = string.Format("myobserver.update called, set replicationFinished to: %b"
						, this.replicationFinished);
					Log.D(ReplicationTest.Tag, msg);
					this.doneSignal.CountDown();
				}
				else
				{
					string msg = string.Format("myobserver.update called, but replicator still running, so ignore it"
						);
					Log.D(ReplicationTest.Tag, msg);
				}
			}

			internal virtual bool IsReplicationFinished()
			{
				return this.replicationFinished;
			}

			private readonly ReplicationTest _enclosing;
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
		public virtual void TestChannelsMore()
		{
			Database db = StartDatabase();
			Uri fakeRemoteURL = new Uri("http://couchbase.com/no_such_db");
			Replication r1 = db.CreatePullReplication(fakeRemoteURL);
			NUnit.Framework.Assert.IsTrue(r1.GetChannels().IsEmpty());
			r1.SetFilter("foo/bar");
			NUnit.Framework.Assert.IsTrue(r1.GetChannels().IsEmpty());
			IDictionary<string, object> filterParams = new Dictionary<string, object>();
			filterParams["a"] = "b";
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
			HttpClientFactory mockHttpClientFactory = new _HttpClientFactory_741(mockHttpClient
				);
			Uri remote = GetReplicationURL();
			manager.SetDefaultHttpClientFactory(mockHttpClientFactory);
			Replication puller = database.CreatePullReplication(remote);
			IDictionary<string, object> headers = new Dictionary<string, object>();
			headers["foo"] = "bar";
			puller.SetHeaders(headers);
			puller.Start();
			Sharpen.Thread.Sleep(2000);
			puller.Stop();
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
	}
}
