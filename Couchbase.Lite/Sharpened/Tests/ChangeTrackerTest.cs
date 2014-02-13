//
// ChangeTrackerTest.cs
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

using System;
using System.Collections.Generic;
using System.Net;
using Apache.Http;
using Apache.Http.Client;
using Apache.Http.Impl.Client;
using Couchbase.Lite;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Threading;
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
				.OneShot, 0, client);
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
				.Continuous, 0, client);
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
			ChangeTracker changeTracker = new ChangeTracker(testURL, mode, 0, client);
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

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTrackerWithFilterURL()
		{
			Uri testURL = GetReplicationURL();
			ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.LongPoll, 0, null);
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
				.LongPoll, 0, null);
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
		public virtual void TestChangeTrackerBackoff()
		{
			Uri testURL = GetReplicationURL();
			CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
			mockHttpClient.AddResponderThrowExceptionAllRequests();
			ChangeTrackerClient client = new _ChangeTrackerClient_214(mockHttpClient);
			ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.LongPoll, 0, client);
			BackgroundTask task = new _BackgroundTask_235(changeTracker);
			task.Execute();
			try
			{
				// expected behavior:
				// when:
				//    mockHttpClient throws IOExceptions -> it should start high and then back off and numTimesExecute should be low
				for (int i = 0; i < 30; i++)
				{
					int numTimesExectutedAfter10seconds = 0;
					try
					{
						Sharpen.Thread.Sleep(1000);
						// take a snapshot of num times the http client was called after 10 seconds
						if (i == 10)
						{
							numTimesExectutedAfter10seconds = mockHttpClient.GetCapturedRequests().Count;
						}
						// take another snapshot after 20 seconds have passed
						if (i == 20)
						{
							// by now it should have backed off, so the delta between 10s and 20s should be small
							int delta = mockHttpClient.GetCapturedRequests().Count - numTimesExectutedAfter10seconds;
							NUnit.Framework.Assert.IsTrue(delta < 25);
						}
					}
					catch (Exception e)
					{
						Sharpen.Runtime.PrintStackTrace(e);
					}
				}
			}
			finally
			{
				changeTracker.Stop();
			}
		}

		private sealed class _ChangeTrackerClient_214 : ChangeTrackerClient
		{
			public _ChangeTrackerClient_214(CustomizableMockHttpClient mockHttpClient)
			{
				this.mockHttpClient = mockHttpClient;
			}

			public void ChangeTrackerStopped(ChangeTracker tracker)
			{
				Log.V(ChangeTrackerTest.Tag, "changeTrackerStopped");
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

			private readonly CustomizableMockHttpClient mockHttpClient;
		}

		private sealed class _BackgroundTask_235 : BackgroundTask
		{
			public _BackgroundTask_235(ChangeTracker changeTracker)
			{
				this.changeTracker = changeTracker;
			}

			public override void Run()
			{
				changeTracker.Start();
			}

			private readonly ChangeTracker changeTracker;
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestChangeTrackerInvalidJson()
		{
			Uri testURL = GetReplicationURL();
			CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
			mockHttpClient.AddResponderThrowExceptionAllRequests();
			ChangeTrackerClient client = new _ChangeTrackerClient_290(mockHttpClient);
			ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
				.LongPoll, 0, client);
			BackgroundTask task = new _BackgroundTask_311(changeTracker);
			task.Execute();
			try
			{
				// expected behavior:
				// when:
				//    mockHttpClient throws IOExceptions -> it should start high and then back off and numTimesExecute should be low
				for (int i = 0; i < 30; i++)
				{
					int numTimesExectutedAfter10seconds = 0;
					try
					{
						Sharpen.Thread.Sleep(1000);
						// take a snapshot of num times the http client was called after 10 seconds
						if (i == 10)
						{
							numTimesExectutedAfter10seconds = mockHttpClient.GetCapturedRequests().Count;
						}
						// take another snapshot after 20 seconds have passed
						if (i == 20)
						{
							// by now it should have backed off, so the delta between 10s and 20s should be small
							int delta = mockHttpClient.GetCapturedRequests().Count - numTimesExectutedAfter10seconds;
							NUnit.Framework.Assert.IsTrue(delta < 25);
						}
					}
					catch (Exception e)
					{
						Sharpen.Runtime.PrintStackTrace(e);
					}
				}
			}
			finally
			{
				changeTracker.Stop();
			}
		}

		private sealed class _ChangeTrackerClient_290 : ChangeTrackerClient
		{
			public _ChangeTrackerClient_290(CustomizableMockHttpClient mockHttpClient)
			{
				this.mockHttpClient = mockHttpClient;
			}

			public void ChangeTrackerStopped(ChangeTracker tracker)
			{
				Log.V(ChangeTrackerTest.Tag, "changeTrackerStopped");
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

			private readonly CustomizableMockHttpClient mockHttpClient;
		}

		private sealed class _BackgroundTask_311 : BackgroundTask
		{
			public _BackgroundTask_311(ChangeTracker changeTracker)
			{
				this.changeTracker = changeTracker;
			}

			public override void Run()
			{
				changeTracker.Start();
			}

			private readonly ChangeTracker changeTracker;
		}
	}
}
