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
        public virtual void TestChangeTrackerOneShot()
        {
            ChangeTrackerTestWithMode(ChangeTracker.ChangeTrackerMode.OneShot, true);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestChangeTrackerLongPoll()
        {
            ChangeTrackerTestWithMode(ChangeTracker.ChangeTrackerMode.LongPoll, true);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void ChangeTrackerTestWithMode(ChangeTracker.ChangeTrackerMode mode
            , bool useMockReplicator)
        {
            CountDownLatch changeTrackerFinishedSignal = new CountDownLatch(1);
            CountDownLatch changeReceivedSignal = new CountDownLatch(1);
            Uri testURL = GetReplicationURL();
            ChangeTrackerClient client = new _ChangeTrackerClient_42(changeTrackerFinishedSignal
                , useMockReplicator, changeReceivedSignal);
            ChangeTracker changeTracker = new ChangeTracker(testURL, mode, false, 0, client);
            changeTracker.SetUsePOST(IsTestingAgainstSyncGateway());
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

        private sealed class _ChangeTrackerClient_42 : ChangeTrackerClient
        {
            public _ChangeTrackerClient_42(CountDownLatch changeTrackerFinishedSignal, bool useMockReplicator
                , CountDownLatch changeReceivedSignal)
            {
                this.changeTrackerFinishedSignal = changeTrackerFinishedSignal;
                this.useMockReplicator = useMockReplicator;
                this.changeReceivedSignal = changeReceivedSignal;
            }

            public void ChangeTrackerStopped(ChangeTracker tracker)
            {
                changeTrackerFinishedSignal.CountDown();
            }

            public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
            {
                object seq = change.Get("seq");
                if (useMockReplicator)
                {
                    NUnit.Framework.Assert.AreEqual("1", seq.ToString());
                }
                changeReceivedSignal.CountDown();
            }

            public HttpClient GetHttpClient()
            {
                if (useMockReplicator)
                {
                    CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
                    mockHttpClient.SetResponder("_changes", new _Responder_62());
                    return mockHttpClient;
                }
                else
                {
                    return new DefaultHttpClient();
                }
            }

            private sealed class _Responder_62 : CustomizableMockHttpClient.Responder
            {
                public _Responder_62()
                {
                }

                /// <exception cref="System.IO.IOException"></exception>
                public HttpResponse Execute(HttpRequestMessage httpUriRequest)
                {
                    string json = "{\"results\":[\n" + "{\"seq\":\"1\",\"id\":\"doc1-138\",\"changes\":[{\"rev\":\"1-82d\"}]}],\n"
                         + "\"last_seq\":\"*:50\"}";
                    return CustomizableMockHttpClient.GenerateHttpResponseObject(json);
                }
            }

            private readonly CountDownLatch changeTrackerFinishedSignal;

            private readonly bool useMockReplicator;

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
            string docIdsUnencoded = "[\"doc1\",\"doc2\"]";
            string docIdsEncoded = URLEncoder.Encode(docIdsUnencoded);
            string expectedFeedPath = string.Format("_changes?feed=longpoll&limit=50&heartbeat=300000&since=0&filter=_doc_ids&doc_ids=%s"
                , docIdsEncoded);
            string changesFeedPath = changeTrackerDocIds.GetChangesFeedPath();
            NUnit.Framework.Assert.AreEqual(expectedFeedPath, changesFeedPath);
            changeTrackerDocIds.SetUsePOST(true);
            IDictionary<string, object> postBodyMap = changeTrackerDocIds.ChangesFeedPOSTBodyMap
                ();
            NUnit.Framework.Assert.AreEqual("_doc_ids", postBodyMap.Get("filter"));
            NUnit.Framework.Assert.AreEqual(docIds, postBodyMap.Get("doc_ids"));
            string postBody = changeTrackerDocIds.ChangesFeedPOSTBody();
            NUnit.Framework.Assert.IsTrue(postBody.Contains(docIdsUnencoded));
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
        public virtual void TestChangeTrackerRecoverableError()
        {
            int errorCode = 503;
            string statusMessage = "Transient Error";
            int numExpectedChangeCallbacks = 2;
            RunChangeTrackerTransientError(ChangeTracker.ChangeTrackerMode.LongPoll, errorCode
                , statusMessage, numExpectedChangeCallbacks);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestChangeTrackerRecoverableIOException()
        {
            int errorCode = -1;
            // special code to tell it to throw an IOException
            string statusMessage = null;
            int numExpectedChangeCallbacks = 2;
            RunChangeTrackerTransientError(ChangeTracker.ChangeTrackerMode.LongPoll, errorCode
                , statusMessage, numExpectedChangeCallbacks);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestChangeTrackerNonRecoverableError()
        {
            int errorCode = 404;
            string statusMessage = "NOT FOUND";
            int numExpectedChangeCallbacks = 1;
            RunChangeTrackerTransientError(ChangeTracker.ChangeTrackerMode.LongPoll, errorCode
                , statusMessage, numExpectedChangeCallbacks);
        }

        /// <exception cref="System.Exception"></exception>
        private void RunChangeTrackerTransientError(ChangeTracker.ChangeTrackerMode mode, 
            int errorCode, string statusMessage, int numExpectedChangeCallbacks)
        {
            CountDownLatch changeTrackerFinishedSignal = new CountDownLatch(1);
            CountDownLatch changeReceivedSignal = new CountDownLatch(numExpectedChangeCallbacks
                );
            Uri testURL = GetReplicationURL();
            ChangeTrackerClient client = new _ChangeTrackerClient_197(changeTrackerFinishedSignal
                , changeReceivedSignal, errorCode, statusMessage);
            ChangeTracker changeTracker = new ChangeTracker(testURL, mode, false, 0, client);
            changeTracker.SetUsePOST(IsTestingAgainstSyncGateway());
            changeTracker.Start();
            try
            {
                bool success = changeReceivedSignal.Await(30, TimeUnit.Seconds);
                NUnit.Framework.Assert.IsTrue(success);
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
            changeTracker.Stop();
            try
            {
                bool success = changeTrackerFinishedSignal.Await(30, TimeUnit.Seconds);
                NUnit.Framework.Assert.IsTrue(success);
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
        }

        private sealed class _ChangeTrackerClient_197 : ChangeTrackerClient
        {
            public _ChangeTrackerClient_197(CountDownLatch changeTrackerFinishedSignal, CountDownLatch
                 changeReceivedSignal, int errorCode, string statusMessage)
            {
                this.changeTrackerFinishedSignal = changeTrackerFinishedSignal;
                this.changeReceivedSignal = changeReceivedSignal;
                this.errorCode = errorCode;
                this.statusMessage = statusMessage;
            }

            public void ChangeTrackerStopped(ChangeTracker tracker)
            {
                changeTrackerFinishedSignal.CountDown();
            }

            public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
            {
                changeReceivedSignal.CountDown();
            }

            public HttpClient GetHttpClient()
            {
                CustomizableMockHttpClient mockHttpClient = new CustomizableMockHttpClient();
                CustomizableMockHttpClient.Responder sentinal = this.DefaultChangesResponder();
                Queue<CustomizableMockHttpClient.Responder> responders = new List<CustomizableMockHttpClient.Responder
                    >();
                responders.AddItem(this.DefaultChangesResponder());
                responders.AddItem(CustomizableMockHttpClient.TransientErrorResponder(errorCode, 
                    statusMessage));
                ResponderChain responderChain = new ResponderChain(responders, sentinal);
                mockHttpClient.SetResponder("_changes", responderChain);
                return mockHttpClient;
            }

            private CustomizableMockHttpClient.Responder DefaultChangesResponder()
            {
                return new _Responder_222();
            }

            private sealed class _Responder_222 : CustomizableMockHttpClient.Responder
            {
                public _Responder_222()
                {
                }

                /// <exception cref="System.IO.IOException"></exception>
                public HttpResponse Execute(HttpRequestMessage httpUriRequest)
                {
                    string json = "{\"results\":[\n" + "{\"seq\":\"1\",\"id\":\"doc1-138\",\"changes\":[{\"rev\":\"1-82d\"}]}],\n"
                         + "\"last_seq\":\"*:50\"}";
                    return CustomizableMockHttpClient.GenerateHttpResponseObject(json);
                }
            }

            private readonly CountDownLatch changeTrackerFinishedSignal;

            private readonly CountDownLatch changeReceivedSignal;

            private readonly int errorCode;

            private readonly string statusMessage;
        }

        /// <exception cref="System.Exception"></exception>
        private void TestChangeTrackerBackoff(CustomizableMockHttpClient mockHttpClient)
        {
            Uri testURL = GetReplicationURL();
            CountDownLatch changeTrackerFinishedSignal = new CountDownLatch(1);
            ChangeTrackerClient client = new _ChangeTrackerClient_263(changeTrackerFinishedSignal
                , mockHttpClient);
            ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
                .LongPoll, false, 0, client);
            changeTracker.SetUsePOST(IsTestingAgainstSyncGateway());
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

        private sealed class _ChangeTrackerClient_263 : ChangeTrackerClient
        {
            public _ChangeTrackerClient_263(CountDownLatch changeTrackerFinishedSignal, CustomizableMockHttpClient
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

        // ChangeTrackerMode.Continuous mode does not work, do not use it.
        /// <exception cref="System.Exception"></exception>
        public virtual void FailingTestChangeTrackerContinuous()
        {
            CountDownLatch changeTrackerFinishedSignal = new CountDownLatch(1);
            CountDownLatch changeReceivedSignal = new CountDownLatch(1);
            Uri testURL = GetReplicationURL();
            ChangeTrackerClient client = new _ChangeTrackerClient_333(changeTrackerFinishedSignal
                , changeReceivedSignal);
            ChangeTracker changeTracker = new ChangeTracker(testURL, ChangeTracker.ChangeTrackerMode
                .Continuous, false, 0, client);
            changeTracker.SetUsePOST(IsTestingAgainstSyncGateway());
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

        private sealed class _ChangeTrackerClient_333 : ChangeTrackerClient
        {
            public _ChangeTrackerClient_333(CountDownLatch changeTrackerFinishedSignal, CountDownLatch
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
    }
}
