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
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Replicator;
using Couchbase.Lite.Tests;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Couchbase.Lite.Internal;

#if NET_3_5
using System.Net.Couchbase;
#else
using System.Net;
#endif

namespace Couchbase.Lite
{
    public class ChangeTrackerTest : LiteTestCase
    {
        public const string TAG = "ChangeTracker";
        private SyncGateway _sg;

        public ChangeTrackerTest(string storageType) : base(storageType) {}

        private class ChangeTrackerTestClient : IChangeTrackerClient
        {

            #region IChangeTrackerClient implementation

            public delegate void ChangeTrackerStoppedDelegate(ChangeTracker tracker, ErrorResolution resolution);

            public delegate void ChangeTrackerReceivedChangeDelegate(IDictionary<string, object> change);

            public MockHttpClientFactory HttpClientFactory { get; set; }

            public MockHttpRequestHandler HttpRequestHandler { get { return HttpClientFactory.HttpHandler; } }

            public MessageProcessingHandler Handler { get { return HttpClientFactory.Handler; } }

            public ChangeTrackerStoppedDelegate StoppedDelegate { get; set; }

            public ChangeTrackerReceivedChangeDelegate ReceivedChangeDelegate { get; set; }

            private CountdownEvent stoppedSignal;
            private CountdownEvent changedSignal;
            private AutoResetEvent caughtUpSignal;

            public ChangeTrackerTestClient(CountdownEvent stoppedSignal, CountdownEvent changedSignal, 
                AutoResetEvent caughtUpSignal = null)
            {
                this.stoppedSignal = stoppedSignal;
                this.changedSignal = changedSignal;
                this.caughtUpSignal = caughtUpSignal;
                HttpClientFactory = new MockHttpClientFactory();
            }

            public void ChangeTrackerCaughtUp(ChangeTracker tracker)
            {
                if (caughtUpSignal != null) {
                    caughtUpSignal.Set();
                }
            }

            public void ChangeTrackerFinished(ChangeTracker tracker)
            {
                
            }

            public void ChangeTrackerStopped(ChangeTracker tracker, ErrorResolution resolution)
            {
                if (StoppedDelegate != null) 
                {
                    StoppedDelegate(tracker, resolution);
                }

                if (stoppedSignal != null)
                {
                    stoppedSignal.Signal();
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
                    changedSignal.Signal();
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

            public CookieContainer GetCookieStore()
            {
                return HttpClientFactory.GetCookieContainer();
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

        private Boolean IsSyncGateway(Uri remote) 
        {
            return (remote.Port == 4984 || remote.Port == 4985);
        }

        private void ChangeTrackerTestWithMode(ChangeTrackerMode mode)
        {
            var changeTrackerFinishedSignal = new CountdownEvent(1);
            var changeReceivedSignal = new CountdownEvent(1);
            var client = new ChangeTrackerTestClient(changeTrackerFinishedSignal, changeReceivedSignal);

            client.ReceivedChangeDelegate = (IDictionary<string, object> change) =>
            {
                Assert.IsTrue(change.ContainsKey("seq"));
                Assert.AreEqual("1", change["seq"]);
            };

            var handler = client.HttpRequestHandler;

            handler.SetResponder("_changes", (request) => 
            {
                var json = "{\"results\":[\n" +
                    "{\"seq\":\"1\",\"id\":\"doc1-138\",\"changes\":[{\"rev\":\"1-82d\"}]}],\n" +
                    "\"last_seq\":\"*:50\"}";
                return MockHttpRequestHandler.GenerateHttpResponseMessage(System.Net.HttpStatusCode.OK, null, json);
            });

            var testUrl = GetReplicationURL();
            var scheduler = new SingleTaskThreadpoolScheduler();
            var remoteSession = new RemoteSession(new RemoteSessionContructorOptions {
                WorkExecutor = new TaskFactory(scheduler),
                BaseUrl = testUrl
            });
            remoteSession.SetupHttpClientFactory(client.HttpClientFactory, database);
            remoteSession.Setup(new ReplicationOptions());
            var changeTracker = ChangeTrackerFactory.Create(new ChangeTrackerOptions {
                DatabaseUri = testUrl,
                Mode = mode,
                IncludeConflicts = false,
                Client = client,
                RetryStrategy = new ExponentialBackoffStrategy(2),
                WorkExecutor = new TaskFactory(scheduler),
                RemoteSession = remoteSession
            });

            changeTracker.ActiveOnly = true;
            changeTracker.Start();

            var success = changeReceivedSignal.Wait(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);

            changeTracker.Stop(ErrorResolution.Ignore);

            success = changeTrackerFinishedSignal.Wait(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
        }

        private void TestChangeTrackerBackoff(MockHttpClientFactory httpClientFactory)
        {
            var changeTrackerFinishedSignal = new CountdownEvent(1);
            var client = new ChangeTrackerTestClient(changeTrackerFinishedSignal, null);
            client.HttpClientFactory = httpClientFactory;

            var testUrl = GetReplicationURL();
            var scheduler = new SingleTaskThreadpoolScheduler();
            var opts = new RemoteSessionContructorOptions {
                BaseUrl = testUrl,
                WorkExecutor = new TaskFactory(scheduler)
            };
            var remoteSession = new RemoteSession (opts);
            remoteSession.SetupHttpClientFactory (client.HttpClientFactory, database);
            remoteSession.Setup (new ReplicationOptions ());
            var changeTracker = ChangeTrackerFactory.Create(new ChangeTrackerOptions {
                DatabaseUri = testUrl,
                Mode = ChangeTrackerMode.LongPoll,
                IncludeConflicts = true,
                Client = client,
                RetryStrategy = new ExponentialBackoffStrategy(2),
                WorkExecutor = new TaskFactory(scheduler),
                RemoteSession = remoteSession
            });
            changeTracker.Continuous = true;

            changeTracker.Start();

            // sleep for a few seconds
            Sleep(5 * 1000);

            // make sure we got less than 10 requests in those 8 seconds (if it was hammering, we'd get a lot more)
            var handler = client.HttpRequestHandler;
            Assert.Less(handler.CapturedRequests.Count, 10);

            handler.ClearResponders();
            handler.AddResponderReturnEmptyChangesFeed();
            handler.ResponseDelayMilliseconds = 100;

            // at this point, the change tracker backoff should cause it to sleep for about 3 seconds
            // and so lets wait 3 seconds until it wakes up and starts getting valid responses
            Sleep(10 * 1000);

            // now find the delta in requests received in a 2s period
            int before = handler.CapturedRequests.Count;
            Sleep(2 * 1000);
            int after = handler.CapturedRequests.Count;

            changeTracker.Stop(ErrorResolution.Ignore);

            // assert that the delta is high, because at this point the change tracker should
            // be hammering away
            Assert.Greater((after - before), 10);

            // the backoff numAttempts should have been reset to 0
            Assert.IsTrue(changeTracker.Backoff.NumAttempts == 0);

            var success = changeTrackerFinishedSignal.Wait(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
        }

        private MockHttpRequestHandler.HttpResponseDelegate RunChangeTrackerTransientErrorDefaultResponder() {
            MockHttpRequestHandler.HttpResponseDelegate responder = (request) =>
            {
                var json = "{\"results\":[\n" +
                    "{\"seq\":\"1\",\"id\":\"doc1-138\",\"changes\":[{\"rev\":\"1-82d\"}]}],\n" +
                    "\"last_seq\":\"*:50\"}";
                return MockHttpRequestHandler.GenerateHttpResponseMessage(System.Net.HttpStatusCode.OK, null, json);
            };
            return responder;
        }

        private void RunChangeTrackerTransientError(
            ChangeTrackerMode mode,
            Int32 errorCode,
            string statusMessage,
            Int32 numExpectedChangeCallbacks) 
        {
            var changeTrackerFinishedSignal = new CountdownEvent(1);
            var changeReceivedSignal = new CountdownEvent(numExpectedChangeCallbacks);
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

            var handler = client.HttpRequestHandler;
            handler.SetResponder("_changes", chainResponder);

            var testUrl = GetReplicationURL();
            var scheduler = new SingleTaskThreadpoolScheduler();
            var remoteSession = new RemoteSession(new RemoteSessionContructorOptions {
                WorkExecutor = new TaskFactory(scheduler),
                BaseUrl = testUrl
            });
            remoteSession.SetupHttpClientFactory(client.HttpClientFactory, database);
            remoteSession.Setup(new ReplicationOptions());
            var changeTracker = ChangeTrackerFactory.Create(new ChangeTrackerOptions {
                DatabaseUri = testUrl,
                Mode = mode,
                IncludeConflicts = false,
                Client = client,
                RetryStrategy = new ExponentialBackoffStrategy(2),
                WorkExecutor = new TaskFactory(scheduler),
                RemoteSession = remoteSession
            });

            changeTracker.Start();

            var success = changeReceivedSignal.Wait(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);

            changeTracker.Stop(ErrorResolution.Ignore);

            success = changeTrackerFinishedSignal.Wait(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
        }

        private void TestLiveChangeTracker(Func<Uri, IChangeTrackerClient, ChangeTracker> generator, bool continuous)
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled"))) {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var changedEvent = new CountdownEvent(4);
            var caughtUpEvent = new AutoResetEvent(false);
            var stoppedEvent = new CountdownEvent(1);
            var client = new ChangeTrackerTestClient(stoppedEvent, changedEvent, caughtUpEvent);
            client.HttpRequestHandler.DefaultFail = false;
            using (var remoteDb = _sg.CreateDatabase("web_socket_scratch")) {
                var tracker = generator(remoteDb.RemoteUri, client);
                tracker.Start();
                Assert.IsTrue(caughtUpEvent.WaitOne(200000));

                if (continuous) {
                    remoteDb.AddDocument("newdoc", new Dictionary<string, object> { { "foo", "bar" } });
                    Assert.IsTrue(changedEvent.Wait(20000));

                    tracker.Stop(ErrorResolution.Ignore);
                    Assert.IsTrue(stoppedEvent.Wait(20000));
                }
            }
        }

        [Test]
        public void TestWebSocketChangeTrackerFallback()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled"))) {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var signal = new CountdownEvent(1);
            var client = new ChangeTrackerTestClient(signal, null);
            using (var remoteDb = _sg.CreateDatabase("web_socket_scratch")) {
                var remoteSession = new RemoteSession(new RemoteSessionContructorOptions {
                    BaseUrl = remoteDb.RemoteUri
                });
                remoteSession.SetupHttpClientFactory(null, database);
                remoteSession.Setup(new ReplicationOptions());
                var tracker = new BadWebSocketChangeTracker(new ChangeTrackerOptions {
                    DatabaseUri = remoteDb.RemoteUri,
                    IncludeConflicts = false,
                    Client = client,
                    RetryStrategy = new ExponentialBackoffStrategy(2),
                    RemoteSession = remoteSession
                });

                tracker.Start();
                Assert.IsTrue(signal.Wait(TimeSpan.FromSeconds(20)));
                Assert.IsFalse(tracker.CanConnect);
            }
        }

        [Test]
        public void TestLiveChangeTrackerWebSocket()
        {
            TestLiveChangeTracker((uri, client) => 
            {
                var remoteSession = new RemoteSession(new RemoteSessionContructorOptions {
                    BaseUrl = uri
                });
                remoteSession.SetupHttpClientFactory(null, database);
                remoteSession.Setup(new ReplicationOptions());
                var tracker = ChangeTrackerFactory.Create(new ChangeTrackerOptions {
                    DatabaseUri = uri,
                    Mode = ChangeTrackerMode.WebSocket,
                    IncludeConflicts = false,
                    Client = client,
                    RetryStrategy = new ExponentialBackoffStrategy(2),
                    RemoteSession = remoteSession
                });
                tracker.Continuous = true;
                return tracker;
            }, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestLiveChangeTrackerOneShot(bool continuous)
        {
            TestLiveChangeTracker((uri, client) => 
            {
                var remoteSession = new RemoteSession(new RemoteSessionContructorOptions {
                    BaseUrl = uri
                });
                remoteSession.SetupHttpClientFactory(null, database);
                remoteSession.Setup(new ReplicationOptions());
                var tracker = ChangeTrackerFactory.Create(new ChangeTrackerOptions {
                    DatabaseUri = uri,
                    Mode = ChangeTrackerMode.OneShot,
                    IncludeConflicts = false,
                    Client = client,
                    RetryStrategy = new ExponentialBackoffStrategy(2),
                    RemoteSession = remoteSession
                });
                tracker.Continuous = continuous;
                tracker.PollInterval = TimeSpan.FromSeconds(5);
                return tracker;
            }, continuous);
        }
            
        [Test]
        public void TestLiveChangeTrackerLongPoll()
        {
            TestLiveChangeTracker((uri, client) => 
            {
                var remoteSession = new RemoteSession(new RemoteSessionContructorOptions {
                    BaseUrl = uri
                });
                remoteSession.SetupHttpClientFactory(null, database);
                remoteSession.Setup(new ReplicationOptions());
                var tracker = ChangeTrackerFactory.Create(new ChangeTrackerOptions {
                    DatabaseUri = uri,
                    Mode = ChangeTrackerMode.OneShot,
                    IncludeConflicts = false,
                    Client = client,
                    RetryStrategy = new ExponentialBackoffStrategy(2),
                    RemoteSession = remoteSession
                });
                tracker.Continuous = true;
                return tracker;
            }, true);
        }

        [Test]
        public void TestChangeTrackerGiveUp()
        {
            var factory = new MockHttpClientFactory();
            var httpHandler = (MockHttpRequestHandler)factory.HttpHandler;
            httpHandler.AddResponderThrowExceptionAllRequests();
            var changeTrackerFinishedSignal = new CountdownEvent(1);
            var client = new ChangeTrackerTestClient(changeTrackerFinishedSignal, null);
            client.HttpClientFactory = factory;


            var testUrl = GetReplicationURL();
            var scheduler = new SingleTaskThreadpoolScheduler();
            var remoteSession = new RemoteSession (new RemoteSessionContructorOptions {
                BaseUrl = testUrl,
                WorkExecutor = new TaskFactory (scheduler)
            });
            remoteSession.SetupHttpClientFactory (client.HttpClientFactory, database);
            remoteSession.Setup(new ReplicationOptions());
            var changeTracker = ChangeTrackerFactory.Create(new ChangeTrackerOptions {
                DatabaseUri = testUrl,
                Mode = ChangeTrackerMode.OneShot,
                IncludeConflicts = true,
                Client = client,
                RetryStrategy = new ExponentialBackoffStrategy(2),
                WorkExecutor = new TaskFactory(scheduler),
                RemoteSession = remoteSession
            });


            changeTracker.Start();
            Assert.IsTrue(changeTrackerFinishedSignal.Wait(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void TestChangeTrackerOneShot()
        {
            ChangeTrackerTestWithMode(ChangeTrackerMode.OneShot);
        }

        [Test]
        public void TestChangeTrackerLongPoll() 
        {
            ChangeTrackerTestWithMode(ChangeTrackerMode.LongPoll);
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
            var errorCode = System.Net.HttpStatusCode.ServiceUnavailable;
            var statusMessage = "Transient Error";
            var numExpectedChangeCallbacks = 2;
            RunChangeTrackerTransientError(ChangeTrackerMode.LongPoll, (Int32)errorCode, statusMessage, numExpectedChangeCallbacks);
        }

        [Test]
        public void TestChangeTrackerRecoverableIOException()
        {
            var errorCode = -1;
            var statusMessage = (string)null;
            var numExpectedChangeCallbacks = 2;
            RunChangeTrackerTransientError(ChangeTrackerMode.LongPoll, (Int32)errorCode, statusMessage, numExpectedChangeCallbacks);
        }

        [Test]
        public void TestChangeTrackerNonRecoverableError()
        {
            var errorCode = System.Net.HttpStatusCode.NotFound;
            var statusMessage = "Not Found";
            var numExpectedChangeCallbacks = 1;
            RunChangeTrackerTransientError(ChangeTrackerMode.LongPoll, (Int32)errorCode, statusMessage, numExpectedChangeCallbacks);
        }

        [Test]
        public void TestChangeTrackerWithDocsIds()
        {
            var testURL = GetReplicationURL();
            var remoteSession = new RemoteSession(new RemoteSessionContructorOptions {
                BaseUrl = testURL
            });
            remoteSession.SetupHttpClientFactory(null, database);
            remoteSession.Setup(new ReplicationOptions());
            var changeTracker = ChangeTrackerFactory.Create(new ChangeTrackerOptions {
                DatabaseUri = testURL,
                Mode = ChangeTrackerMode.LongPoll,
                IncludeConflicts = false,
                Client = new ChangeTrackerTestClient(null, null),
                RetryStrategy = new ExponentialBackoffStrategy(2),
                RemoteSession = remoteSession
            });

            var docIds = new List<string>();
            docIds.Add("doc1");
            docIds.Add("doc2");
            changeTracker.DocIDs = docIds;

            var parameters = changeTracker.GetChangesFeedParams();
            Assert.AreEqual("_doc_ids", parameters["filter"]);
            AssertEnumerablesAreEqual(docIds, (IEnumerable)parameters["doc_ids"]);
        }
          
        protected override void SetUp()
        {
            base.SetUp();
            _sg = new SyncGateway(GetReplicationProtocol(), GetReplicationServer());
        }
    }
}
