//
// ChangeTrackerTest.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc
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
using System.Net;

using Couchbase.Lite.Replicator;

using Couchbase.Lite.Util;
using Sharpen;

using System.Web;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Couchbase.Lite.Tests
{
	public class ChangeTrackerTest : LiteTestCase
	{
		public const string Tag = "ChangeTracker";

        private class ChangeTrackerTestClient : IChangeTrackerClient
        {
            #region IChangeTrackerClient implementation

            public delegate void ChangeTrackerStoppedDelegate(ChangeTracker tracker);

            public delegate void ChangeTrackerReceivedChangeDelegate(IDictionary<string, object> change);

            public MockHttpClientFactory HttpClientFactory { get; set; }

            public ChangeTrackerStoppedDelegate StoppedDelegate { get; set; }

            public ChangeTrackerReceivedChangeDelegate ReceivedChangeDelegate { get; set; }

            private CountDownLatch stoppedSignal;
            private CountDownLatch changedSignal;

            public ChangeTrackerTestClient(CountDownLatch stoppedSignal, CountDownLatch changedSignal)
            {
                this.stoppedSignal = stoppedSignal;
                this.changedSignal = changedSignal;
                HttpClientFactory = new MockHttpClientFactory();
            }

            public void ChangeTrackerStopped(ChangeTracker tracker)
            {
                if (StoppedDelegate != null) 
                {
                    StoppedDelegate(tracker);
                }

                if (stoppedSignal != null)
                {
                    stoppedSignal.CountDown();
                }
            }

            public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
            {
                if (ReceivedChangeDelegate != null) 
                {
                    ReceivedChangeDelegate(change);
                }

                if (changedSignal != null)
                {
                    changedSignal.CountDown();
                }
            }

            public void AddCookies(CookieCollection cookies)
            {
                HttpClientFactory.AddCookies(cookies);
            }

            public void DeleteCookie(Uri domain, string name)
            {
                HttpClientFactory.DeleteCookie(domain, name);
            }

            public CookieContainer GetCookieContainer()
            {
                return HttpClientFactory.GetCookieContainer();
            }

            #endregion

            #region IHttpClientFactory implementation

            public HttpClient GetHttpClient()
            {
                return HttpClientFactory.GetHttpClient();
            }

            public HttpClientHandler HttpHandler
            {
                get
                {
                    return HttpClientFactory.HttpHandler;
                }
            }

            public IDictionary<string, string> Headers
            {
                get
                {
                    return HttpClientFactory.Headers;
                }
                set
                {
                    HttpClientFactory.Headers = value;
                }
            }

            #endregion
        }

        private void ChangeTrackerTestWithMode(ChangeTracker.ChangeTrackerMode mode)
		{
            var changeTrackerFinishedSignal = new CountDownLatch(1);
            var changeReceivedSignal = new CountDownLatch(1);
            var client = new ChangeTrackerTestClient(changeTrackerFinishedSignal, changeReceivedSignal);

            client.ReceivedChangeDelegate = (IDictionary<string, object> change) =>
            {
                Assert.IsTrue(change.ContainsKey("seq"));
                Assert.AreEqual("1", change["seq"]);
            };

            var handler = (MockHttpRequestHandler) client.HttpHandler;

            handler.SetResponder("_changes", (request) => 
            {
                var json = "{\"results\":[\n" +
                    "{\"seq\":\"1\",\"id\":\"doc1-138\",\"changes\":[{\"rev\":\"1-82d\"}]}],\n" +
                    "\"last_seq\":\"*:50\"}";
                return MockHttpRequestHandler.GenerateHttpResponseMessage(HttpStatusCode.OK, null, json);
            });

            var testUrl = GetReplicationURL();
            var scheduler = new SingleThreadTaskScheduler();
            var changeTracker = new ChangeTracker(testUrl, mode, 0, false, client, new TaskFactory(scheduler));
            // TODO: Set Use POST
            changeTracker.Start();

            var success = changeReceivedSignal.Await(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);

            changeTracker.Stop();

            success = changeTrackerFinishedSignal.Await(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
		}

        private void TestChangeTrackerBackoff(MockHttpClientFactory httpClientFactory)
        {
            var changeTrackerFinishedSignal = new CountDownLatch(1);
            var client = new ChangeTrackerTestClient(changeTrackerFinishedSignal, null);
            client.HttpClientFactory = httpClientFactory;

            var testUrl = GetReplicationURL();
            var scheduler = new SingleThreadTaskScheduler();
            var changeTracker = new ChangeTracker(testUrl, ChangeTracker.ChangeTrackerMode.LongPoll, 
                0, false, client, new TaskFactory(scheduler));

            // TODO: Set Use POST
            //changeTracker.setUsePOST(isTestingAgainstSyncGateway());

            changeTracker.Start();

            // sleep for a few seconds
            Thread.Sleep(5 * 1000);

            // make sure we got less than 10 requests in those 10 seconds (if it was hammering, we'd get a lot more)
            var handler = (MockHttpRequestHandler) client.HttpHandler;
            Assert.IsTrue(handler.CapturedRequests.Count < 25);
            Assert.IsTrue(changeTracker.backoff.NumAttempts > 0);

            handler.ClearResponders();
            handler.AddResponderReturnEmptyChangesFeed();

            // at this point, the change tracker backoff should cause it to sleep for about 3 seconds
            // and so lets wait 3 seconds until it wakes up and starts getting valid responses
            Thread.Sleep(3 * 1000);

            // now find the delta in requests received in a 2s period
            int before = handler.CapturedRequests.Count;
            Thread.Sleep(2 * 1000);
            int after = handler.CapturedRequests.Count;

            // assert that the delta is high, because at this point the change tracker should
            // be hammering away
            Assert.IsTrue((after - before) > 25);

            // the backoff numAttempts should have been reset to 0
            Assert.IsTrue(changeTracker.backoff.NumAttempts == 0);

            changeTracker.Stop();

            var success = changeTrackerFinishedSignal.Await(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
        }

        private MockHttpRequestHandler.HttpResponseDelegate RunChangeTrackerTransientErrorDefaultResponder() {
            MockHttpRequestHandler.HttpResponseDelegate responder = (request) =>
            {
                var json = "{\"results\":[\n" +
                    "{\"seq\":\"1\",\"id\":\"doc1-138\",\"changes\":[{\"rev\":\"1-82d\"}]}],\n" +
                    "\"last_seq\":\"*:50\"}";
                return MockHttpRequestHandler.GenerateHttpResponseMessage(HttpStatusCode.OK, null, json);
            };
            return responder;
        }

        private void RunChangeTrackerTransientError(
            ChangeTracker.ChangeTrackerMode mode,
            Int32 errorCode,
            string statusMessage,
            Int32 numExpectedChangeCallbacks) 
        {
            var changeTrackerFinishedSignal = new CountDownLatch(1);
            var changeReceivedSignal = new CountDownLatch(numExpectedChangeCallbacks);
            var client = new ChangeTrackerTestClient(changeTrackerFinishedSignal, changeReceivedSignal);

            MockHttpRequestHandler.HttpResponseDelegate sentinal = RunChangeTrackerTransientErrorDefaultResponder();

            var responders = new List<MockHttpRequestHandler.HttpResponseDelegate>();
            responders.Add(RunChangeTrackerTransientErrorDefaultResponder());
            responders.Add(MockHttpRequestHandler.TransientErrorResponder(errorCode, statusMessage));

            MockHttpRequestHandler.HttpResponseDelegate chainResponder = (request) =>
            {
                if (responders.Count > 0) {
                    var responder = responders[0];
                    responders.RemoveAt(0);
                    return responder(request);
                }

                return sentinal(request);
            };

            var handler = (MockHttpRequestHandler) client.HttpHandler;
            handler.SetResponder("_changes", chainResponder);

            var testUrl = GetReplicationURL();
            var scheduler = new SingleThreadTaskScheduler();
            var changeTracker = new ChangeTracker(testUrl, mode, 0, false, client, new TaskFactory(scheduler));

            // TODO: Set Use POST
            //changeTracker.setUsePOST(isTestingAgainstSyncGateway());

            changeTracker.Start();

            var success = changeReceivedSignal.Await(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);

            changeTracker.Stop();

            success = changeTrackerFinishedSignal.Await(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
        }

        [Test]
        public void TestChangeTrackerOneShot()
        {
            ChangeTrackerTestWithMode(ChangeTracker.ChangeTrackerMode.OneShot);
        }

        [Test]
        public void TestChangeTrackerLongPoll() 
        {
            ChangeTrackerTestWithMode(ChangeTracker.ChangeTrackerMode.LongPoll);
        }

        [Test]
        public void TestChangeTrackerWithConflictsIncluded()
        {
            Uri testUrl = GetReplicationURL();
            var changeTracker = new ChangeTracker(testUrl, ChangeTracker.ChangeTrackerMode.LongPoll, 0, true, null);
            Assert.AreEqual("_changes?feed=longpoll&limit=50&heartbeat=300000&style=all_docs&since=0", 
                changeTracker.GetChangesFeedPath());
        }
            
        [Test]
		public void TestChangeTrackerWithFilterURL()
		{
            var testUrl = GetReplicationURL();
            var changeTracker = new ChangeTracker(testUrl, ChangeTracker.ChangeTrackerMode.LongPoll, 0, false, null);
			
            // set filter
			changeTracker.SetFilterName("filter");
			
            // build filter map
            var filterMap = new Dictionary<string, object>();
			filterMap["param"] = "value";

			// set filter map
			changeTracker.SetFilterParams(filterMap);
			Assert.AreEqual("_changes?feed=longpoll&limit=50&heartbeat=300000&since=0&filter=filter&param=value", 
                changeTracker.GetChangesFeedPath());
		}

        [Test]
        public void TestChangeTrackerBackoffExceptions()
        {
            var factory = new MockHttpClientFactory();
            var httpHandler = (MockHttpRequestHandler)factory.HttpHandler;
            httpHandler.AddResponderThrowExceptionAllRequests();
            TestChangeTrackerBackoff(factory);
        }

        [Test]
        public void TestChangeTrackerBackoffInvalidJson() 
        {
            var factory = new MockHttpClientFactory();
            var httpHandler = (MockHttpRequestHandler)factory.HttpHandler;
            httpHandler.AddResponderReturnInvalidChangesFeedJson();
            TestChangeTrackerBackoff(factory);
        }

        [Test]
        public void TestChangeTrackerRecoverableError()
        {
            var errorCode = HttpStatusCode.ServiceUnavailable;
            var statusMessage = "Transient Error";
            var numExpectedChangeCallbacks = 2;
            RunChangeTrackerTransientError(ChangeTracker.ChangeTrackerMode.LongPoll, (Int32)errorCode, statusMessage, numExpectedChangeCallbacks);
        }

        [Test]
        public void TestChangeTrackerRecoverableIOException()
        {
            var errorCode = -1;
            var statusMessage = (string)null;
            var numExpectedChangeCallbacks = 2;
            RunChangeTrackerTransientError(ChangeTracker.ChangeTrackerMode.LongPoll, (Int32)errorCode, statusMessage, numExpectedChangeCallbacks);
        }

        [Test]
        public void TestChangeTrackerNonRecoverableError()
        {
            var errorCode = HttpStatusCode.NotFound;
            var statusMessage = "Not Found";
            var numExpectedChangeCallbacks = 1;
            RunChangeTrackerTransientError(ChangeTracker.ChangeTrackerMode.LongPoll, (Int32)errorCode, statusMessage, numExpectedChangeCallbacks);
        }

        [Test]
		public void TestChangeTrackerWithDocsIds()
		{
            var testURL = GetReplicationURL();
            var changeTrackerDocIds = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
                .LongPoll, 0, false, null);

            var docIds = new List<string>();
			docIds.AddItem("doc1");
			docIds.AddItem("doc2");
			changeTrackerDocIds.SetDocIDs(docIds);

            var docIdsEncoded = HttpUtility.UrlEncode("[\"doc1\",\"doc2\"]");
            var expectedFeedPath = string.Format("_changes?feed=longpoll&limit=50&heartbeat=300000&since=0&filter=_doc_ids&doc_ids={0}", 
                docIdsEncoded);
			string changesFeedPath = changeTrackerDocIds.GetChangesFeedPath();
			Assert.AreEqual(expectedFeedPath, changesFeedPath);

            // TODO: Test POST BODY When ChangeTracker implements POST requests
            // https://github.com/couchbaselabs/couchbase-lite-net/issues/71
		}
	}
}
