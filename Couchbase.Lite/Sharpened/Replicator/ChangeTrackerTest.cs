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
using System.Net;
using Apache.Http;
using Apache.Http.Client;
using Apache.Http.Impl.Client;
using Couchbase.Lite;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
	public class ChangeTrackerTest : LiteTestCase
	{
		public const string Tag = "ChangeTracker";

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTracker()
		{
			CountDownLatch changeTrackerFinishedSignal = new CountDownLatch(1);
			Uri testURL = GetReplicationURL();
			ChangeTrackerClient client = new _ChangeTrackerClient_31(changeTrackerFinishedSignal
				);
			ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.OneShot, false, 0, client);
			changeTracker.Start();
			try
			{
				bool success = changeTrackerFinishedSignal.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(success);
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
		}

		private sealed class _ChangeTrackerClient_31 : ChangeTrackerClient
		{
			public _ChangeTrackerClient_31(CountDownLatch changeTrackerFinishedSignal)
			{
				this.changeTrackerFinishedSignal = changeTrackerFinishedSignal;
			}

			public void ChangeTrackerStopped(ChangeTracker tracker)
			{
				changeTrackerFinishedSignal.CountDown();
			}

			public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
			{
				object seq = change.Get("seq");
			}

			public HttpClient GetHttpClient()
			{
				return new DefaultHttpClient();
			}

			private readonly CountDownLatch changeTrackerFinishedSignal;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTrackerLongPoll()
		{
			ChangeTrackerTestWithMode(ChangeTracker.ChangeTrackerMode.LongPoll);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void FailingTestChangeTrackerContinuous()
		{
			CountDownLatch changeTrackerFinishedSignal = new CountDownLatch(1);
			CountDownLatch changeReceivedSignal = new CountDownLatch(1);
			Uri testURL = GetReplicationURL();
			ChangeTrackerClient client = new _ChangeTrackerClient_72(changeTrackerFinishedSignal
				, changeReceivedSignal);
			ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.Continuous, false, 0, client);
			changeTracker.Start();
			try
			{
				bool success = changeReceivedSignal.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(success);
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
			changeTracker.Stop();
			try
			{
				bool success = changeTrackerFinishedSignal.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(success);
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
		}

		private sealed class _ChangeTrackerClient_72 : ChangeTrackerClient
		{
			public _ChangeTrackerClient_72(CountDownLatch changeTrackerFinishedSignal, CountDownLatch
				 changeReceivedSignal)
			{
				this.changeTrackerFinishedSignal = changeTrackerFinishedSignal;
				this.changeReceivedSignal = changeReceivedSignal;
			}

			public void ChangeTrackerStopped(ChangeTracker tracker)
			{
				changeTrackerFinishedSignal.CountDown();
			}

			public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
			{
				object seq = change.Get("seq");
				changeReceivedSignal.CountDown();
			}

			public HttpClient GetHttpClient()
			{
				return new DefaultHttpClient();
			}

			private readonly CountDownLatch changeTrackerFinishedSignal;

			private readonly CountDownLatch changeReceivedSignal;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void ChangeTrackerTestWithMode(ChangeTracker.ChangeTrackerMode mode
			)
		{
			CountDownLatch changeTrackerFinishedSignal = new CountDownLatch(1);
			CountDownLatch changeReceivedSignal = new CountDownLatch(1);
			Uri testURL = GetReplicationURL();
			ChangeTrackerClient client = new _ChangeTrackerClient_119(changeTrackerFinishedSignal
				, changeReceivedSignal);
			ChangeTracker changeTracker = new ChangeTracker(testURL, mode, false, 0, client);
			changeTracker.Start();
			try
			{
				bool success = changeReceivedSignal.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(success);
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
			changeTracker.Stop();
			try
			{
				bool success = changeTrackerFinishedSignal.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(success);
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
		}

		private sealed class _ChangeTrackerClient_119 : ChangeTrackerClient
		{
			public _ChangeTrackerClient_119(CountDownLatch changeTrackerFinishedSignal, CountDownLatch
				 changeReceivedSignal)
			{
				this.changeTrackerFinishedSignal = changeTrackerFinishedSignal;
				this.changeReceivedSignal = changeReceivedSignal;
			}

			public void ChangeTrackerStopped(ChangeTracker tracker)
			{
				changeTrackerFinishedSignal.CountDown();
			}

			public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
			{
				object seq = change.Get("seq");
				NUnit.Framework.Assert.AreEqual("*:1", seq.ToString());
				changeReceivedSignal.CountDown();
			}

			public HttpClient GetHttpClient()
			{
				CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
				mockHttpClient.SetResponder("_changes", new _Responder_136());
				return mockHttpClient;
			}

			private sealed class _Responder_136 : CustomizableMockHttpClient.Responder
			{
				public _Responder_136()
				{
				}

				/// <exception cref="System.IO.IOException"></exception>
				public HttpResponse Execute(HttpRequestMessage httpUriRequest)
				{
					string json = "{\"results\":[\n" + "{\"seq\":\"*:1\",\"id\":\"doc1-138\",\"changes\":[{\"rev\":\"1-82d\"}]}],\n"
						 + "\"last_seq\":\"*:50\"}";
					return CustomizableMockHttpClient.GenerateHttpResponseObject(json);
				}
			}

			private readonly CountDownLatch changeTrackerFinishedSignal;

			private readonly CountDownLatch changeReceivedSignal;
		}

		public virtual void TestChangeTrackerWithConflictsIncluded()
		{
			Uri testURL = GetReplicationURL();
			ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.LongPoll, true, 0, null);
			NUnit.Framework.Assert.AreEqual("_changes?feed=longpoll&limit=50&heartbeat=300000&style=all_docs&since=0"
				, changeTracker.GetChangesFeedPath());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTrackerWithFilterURL()
		{
			Uri testURL = GetReplicationURL();
			ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.LongPoll, false, 0, null);
			// set filter
			changeTracker.SetFilterName("filter");
			// build filter map
			IDictionary<string, object> filterMap = new Dictionary<string, object>();
			filterMap.Put("param", "value");
			// set filter map
			changeTracker.SetFilterParams(filterMap);
			NUnit.Framework.Assert.AreEqual("_changes?feed=longpoll&limit=50&heartbeat=300000&since=0&filter=filter&param=value"
				, changeTracker.GetChangesFeedPath());
		}

		public virtual void TestChangeTrackerWithDocsIds()
		{
			Uri testURL = GetReplicationURL();
			ChangeTracker changeTrackerDocIds = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.LongPoll, false, 0, null);
			IList<string> docIds = new AList<string>();
			docIds.AddItem("doc1");
			docIds.AddItem("doc2");
			changeTrackerDocIds.SetDocIDs(docIds);
			string docIdsEncoded = URLEncoder.Encode("[\"doc1\",\"doc2\"]");
			string expectedFeedPath = string.Format("_changes?feed=longpoll&limit=50&heartbeat=300000&since=0&filter=_doc_ids&doc_ids=%s"
				, docIdsEncoded);
			string changesFeedPath = changeTrackerDocIds.GetChangesFeedPath();
			NUnit.Framework.Assert.AreEqual(expectedFeedPath, changesFeedPath);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTrackerBackoffExceptions()
		{
			CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
			mockHttpClient.AddResponderThrowExceptionAllRequests();
			TestChangeTrackerBackoff(mockHttpClient);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTrackerBackoffInvalidJson()
		{
			CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
			mockHttpClient.AddResponderReturnInvalidChangesFeedJson();
			TestChangeTrackerBackoff(mockHttpClient);
		}

		/// <exception cref="System.Exception"></exception>
		private void TestChangeTrackerBackoff(CustomizableMockHttpClient mockHttpClient)
		{
			Uri testURL = GetReplicationURL();
			CountDownLatch changeTrackerFinishedSignal = new CountDownLatch(1);
			ChangeTrackerClient client = new _ChangeTrackerClient_234(changeTrackerFinishedSignal
				, mockHttpClient);
			ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.LongPoll, false, 0, client);
			changeTracker.Start();
			// sleep for a few seconds
			Sharpen.Thread.Sleep(5 * 1000);
			// make sure we got less than 10 requests in those 10 seconds (if it was hammering, we'd get a lot more)
			NUnit.Framework.Assert.IsTrue(mockHttpClient.GetCapturedRequests().Count < 25);
			NUnit.Framework.Assert.IsTrue(changeTracker.backoff.GetNumAttempts() > 0);
			mockHttpClient.ClearResponders();
			mockHttpClient.AddResponderReturnEmptyChangesFeed();
			// at this point, the change tracker backoff should cause it to sleep for about 3 seconds
			// and so lets wait 3 seconds until it wakes up and starts getting valid responses
			Sharpen.Thread.Sleep(3 * 1000);
			// now find the delta in requests received in a 2s period
			int before = mockHttpClient.GetCapturedRequests().Count;
			Sharpen.Thread.Sleep(2 * 1000);
			int after = mockHttpClient.GetCapturedRequests().Count;
			// assert that the delta is high, because at this point the change tracker should
			// be hammering away
			NUnit.Framework.Assert.IsTrue((after - before) > 25);
			// the backoff numAttempts should have been reset to 0
			NUnit.Framework.Assert.IsTrue(changeTracker.backoff.GetNumAttempts() == 0);
			changeTracker.Stop();
			try
			{
				bool success = changeTrackerFinishedSignal.Await(300, TimeUnit.Seconds);
				NUnit.Framework.Assert.IsTrue(success);
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
		}

		private sealed class _ChangeTrackerClient_234 : ChangeTrackerClient
		{
			public _ChangeTrackerClient_234(CountDownLatch changeTrackerFinishedSignal, CustomizableMockHttpClient
				 mockHttpClient)
			{
				this.changeTrackerFinishedSignal = changeTrackerFinishedSignal;
				this.mockHttpClient = mockHttpClient;
			}

			public void ChangeTrackerStopped(ChangeTracker tracker)
			{
				Log.V(ChangeTrackerTest.Tag, "changeTrackerStopped");
				changeTrackerFinishedSignal.CountDown();
			}

			public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
			{
				object seq = change.Get("seq");
				Log.V(ChangeTrackerTest.Tag, "changeTrackerReceivedChange: " + seq.ToString());
			}

			public HttpClient GetHttpClient()
			{
				return mockHttpClient;
			}

			private readonly CountDownLatch changeTrackerFinishedSignal;

			private readonly CustomizableMockHttpClient mockHttpClient;
		}
	}
}
