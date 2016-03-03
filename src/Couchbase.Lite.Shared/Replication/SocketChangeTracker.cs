//
// ChangeTracker.cs
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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal
{
    /// <summary>
    /// Reads the continuous-mode _changes feed of a database, and sends the
    /// individual change entries to its client's changeTrackerReceivedChange()
    /// </summary>
    internal class SocketChangeTracker : ChangeTracker
    {
        private static readonly string Tag = typeof(SocketChangeTracker).Name;

        private DateTime _startTime;
        private readonly object stopMutex = new object();
        private HttpRequestMessage Request;
        private CancellationTokenSource tokenSource;
        CancellationTokenSource changesFeedRequestTokenSource;
        private CouchbaseLiteHttpClient _httpClient;



        public SocketChangeTracker(Uri databaseURL, ChangeTrackerMode mode, bool includeConflicts, 
            object lastSequenceID, IChangeTrackerClient client, TaskFactory workExecutor = null)
            : base(databaseURL, mode, includeConflicts, lastSequenceID, client, workExecutor)
        {
            tokenSource = new CancellationTokenSource();
        }

        public void Run()
        {
            IsRunning = true;

            var clientCopy = Client;
            if (clientCopy == null)
            {
                // This is a race condition that can be reproduced by calling cbpuller.start() and cbpuller.stop()
                // directly afterwards.  What happens is that by the time the Changetracker thread fires up,
                // the cbpuller has already set this.client to null.  See issue #109
                Log.To.ChangeTracker.W(Tag, "ChangeTracker run() loop aborting because client == null");
                return;
            }

            if (tokenSource.IsCancellationRequested) {
                tokenSource.Dispose();
                tokenSource = new CancellationTokenSource();
            }

            _startTime = DateTime.Now;
            if (Request != null)
            {
                Request.Dispose();
                Request = null;
            }

            var url = ChangesFeedUrl;
            if(_usePost) {
                Request = new HttpRequestMessage(HttpMethod.Post, url);
                var body = GetChangesFeedPostBody().ToArray();
                Request.Content = new ByteArrayContent(body);
                Request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            } else {
                Request = new HttpRequestMessage(HttpMethod.Get, url);
            }
            AddRequestHeaders(Request);

            Log.To.ChangeTracker.V(Tag, "Making request to {0}", new SecureLogUri(url));
            if (tokenSource.Token.IsCancellationRequested) {
                return;
            }
                
            try {
                changesFeedRequestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token);
                _httpClient.Authenticator = Authenticator;
                var info = _httpClient.SendAsync(
                    Request, 
                    HttpCompletionOption.ResponseHeadersRead,
                    changesFeedRequestTokenSource.Token
                );

                info.ContinueWith(ChangeFeedResponseHandler, changesFeedRequestTokenSource.Token, 
                    TaskContinuationOptions.LongRunning, 
                    TaskScheduler.Default);
            }
            catch (Exception e)
            {
                if (Misc.IsTransientNetworkError(e)) {
                    Log.To.ChangeTracker.I(Tag, "Connection error #{0}, retrying in {1}ms: {2}", backoff.NumAttempts,
                        backoff.GetSleepTime(), e);
                    backoff.SleepAppropriateAmountOfTime();
                    if (IsRunning) {
                        Run();
                    }
                } else {
                    Log.To.ChangeTracker.I(Tag, "Can't connect; giving up: {0}", e);
                    Error = e;
                    Stop();
                }
            }
        }

        private bool ResponseFailed(Task<HttpResponseMessage> responseTask)
        {
            if (responseTask.IsCompleted) {
                var response = responseTask.Result;
                if (response == null)
                    return true;

                return ResponseFailed(response);
            }

            var err = Misc.Flatten(responseTask.Exception);
            var statusCode = Misc.GetStatusCode(err as WebException);
            if (_usePost && statusCode.HasValue && statusCode.Value == HttpStatusCode.MethodNotAllowed) {
                // Remote doesn't allow POST _changes, retry as GET
                _usePost = false;
                Log.To.ChangeTracker.I(Tag, "Remote server doesn't support POST _changes, " +
                    "retrying as GET");
                WorkExecutor.StartNew(Run);
                return true;
            }

            return false;
        }

        private bool ResponseFailed(HttpResponseMessage response)
        {
            var status = response.StatusCode;
            if ((Int32)status >= 300) {
                if (_usePost && status == HttpStatusCode.MethodNotAllowed) {
                    // Remote doesn't allow POST _changes, retry as GET
                    _usePost = false;
                    Log.To.ChangeTracker.I(Tag, "Remote server ({0}) doesn't support POST _changes, " +
                        "retrying as GET", ServerType);
                    WorkExecutor.StartNew(Run);
                    return true;
                }

                if (Misc.IsTransientError(status) && Continuous) {
                    Log.To.ChangeTracker.I(Tag, "{0} transient error ({1}) detected, sleeping...", this,
                        status);
                    backoff.SleepAppropriateAmountOfTime();
                    Log.To.ChangeTracker.I(Tag, "{0} retrying...", this);
                    WorkExecutor.StartNew(Run);
                    return true;
                }

                var msg = response.Content != null 
                    ? String.Format("Change tracker got error with status code: {0}", status)
                    : String.Format("Change tracker got error with status code: {0} and null response content", status);
                Log.To.ChangeTracker.E(Tag, msg);
                Error = new CouchbaseLiteException (msg, new Status (status.GetStatusCode ()));
                Stop();
                response.Dispose();
                return true;
            }

            return false;
        }

        private Task ChangeFeedResponseHandler(Task<HttpResponseMessage> responseTask)
        {
            Misc.SafeDispose(ref changesFeedRequestTokenSource);
            if (ResponseFailed(responseTask)) {
                return Task.FromResult(false);
            }

            var response = responseTask.Result;
            UpdateServerType(response);

            if (response.Content == null) {
                throw Misc.CreateExceptionAndLog(Log.To.ChangeTracker, response.StatusCode.GetStatusCode(), Tag,
                    "Got empty change tracker response");
            }
                        
            Log.To.ChangeTracker.D(Tag, "Getting stream from change tracker response");
            return response.Content.ReadAsStreamAsync().ContinueWith(t =>
            {
                try {
                    ProcessResponseStream(t);
                    backoff.ResetBackoff();
                } catch (Exception e) {
                    if(!Continuous) {
                        Log.To.ChangeTracker.I(Tag, "Non continuous change tracker caught an exception, " +
                            "stopping...", e);
                        Error = e;
                        WorkExecutor.StartNew(Stop);
                        return;
                    }

                    Log.To.ChangeTracker.I(Tag, 
                        String.Format("{0} exception during changes feed processing, sleeping...", this), e);
                    backoff.SleepAppropriateAmountOfTime();
                    Log.To.ChangeTracker.I(Tag, "{0} retrying...", this);
                    WorkExecutor.StartNew(Run);
                } finally {
                    response.Dispose();
                }
            });
        }

        public bool ReceivedPollResponse(IJsonSerializer jsonReader, ref bool timedOut)
        {
            bool started = false;
            var start = DateTime.Now;
            try {
                while (jsonReader.Read()) {
                    _pauseWait.Wait();
                    if (jsonReader.CurrentToken == JsonToken.StartArray) {
                            timedOut = true;
                        started = true;
                    } else if (jsonReader.CurrentToken == JsonToken.EndArray) {
                        started = false;
                    } else if (started) {
                        IDictionary<string, object> change;
                        try {
                            change = jsonReader.DeserializeNextObject();
                        } catch(Exception e) {
                            var ex = e as CouchbaseLiteException;
                            if (ex == null || ex.Code != StatusCode.BadJson) {
                                Log.To.ChangeTracker.W(Tag, "Failure during change tracker JSON parsing", e);
                                throw;
                            }
                                
                            return false;
                        }

                        if (!ReceivedChange(change)) {
                                Log.To.ChangeTracker.W(Tag, "{0} received unparseable change line from server: {1}", 
                                    this, new SecureLogJsonString(change, LogMessageSensitivity.PotentiallyInsecure));
                            return false;
                        }

                        timedOut = false;
                    }
                }
            } catch (CouchbaseLiteException e) {
                var elapsed = DateTime.Now - start;
                timedOut = timedOut && elapsed.TotalSeconds >= 30;
                if (e.CBLStatus.Code == StatusCode.BadJson && timedOut) {
                    return false;
                }

                throw;
            }

            return true;
        }

        public override bool Start()
        {
            if (IsRunning) {
                return false;
            }

            Log.To.ChangeTracker.I(Tag, "Starting {0}...", this);
            _httpClient = Client.GetHttpClient();
            Error = null;
            WorkExecutor.StartNew(Run);
            Log.To.ChangeTracker.I(Tag, "Started {0}", this);

            return true;
        }

        public override void Stop()
        {
            // Lock to prevent multiple calls to Stop() method from different
            // threads (eg. one from ChangeTracker itself and one from any other
            // consumers).
            lock(stopMutex)
            {
                if (!IsRunning) {
                    return;
                }

                Log.To.ChangeTracker.I(Tag, "Stopping {0}...", this);

                IsRunning = false;
                Misc.SafeDispose(ref _httpClient);

                var feedTokenSource = changesFeedRequestTokenSource;
                if (feedTokenSource != null && !feedTokenSource.IsCancellationRequested) {
                    try {
                        feedTokenSource.Cancel();
                    } catch (ObjectDisposedException) {
                        Log.To.ChangeTracker.W(Tag, "Race condition on changesFeedRequestTokenSource detected");
                    } catch (AggregateException e) {
                        if (e.InnerException is ObjectDisposedException) {
                            Log.To.ChangeTracker.W(Tag, "Race condition on changesFeedRequestTokenSource detected");
                        } else {
                            throw;
                        }
                    }
                }

                Stopped();
            }
        }

        protected override void Stopped()
        {
            var client = Client;
            Client = null;
            if (client != null) {
                Log.To.ChangeTracker.V(Tag, "{0} posting stopped to client", this);
                client.ChangeTrackerStopped(this);
            }

            Log.To.ChangeTracker.D(Tag, "change tracker client should be null now");
        }

        private void ProcessResponseStream(Task<Stream> t)
        {
            Log.To.ChangeTracker.D(Tag, "Got stream from change tracker response");
            bool beforeFirstItem = true;
            bool responseOK = false;
            using (var jsonReader = Manager.GetObjectMapper().StartIncrementalParse(t.Result)) {
                responseOK = ReceivedPollResponse(jsonReader, ref beforeFirstItem);
            }

            Log.To.ChangeTracker.V(Tag, "{0} Finished polling", this);

            if (responseOK) {
                backoff.ResetBackoff();
                if (Mode == ChangeTrackerMode.Continuous) {
                    Stop();
                } else {
                    var client = Client;
                    if (!_caughtUp && client != null) {
                        client.ChangeTrackerCaughtUp(this);
                        _caughtUp = true;
                    }

                    if (Continuous) {
                        if (PollInterval.TotalMilliseconds > 30) {
                            Log.To.ChangeTracker.I(Tag, "{0} next poll of _changes feed in {1} sec", this, PollInterval.TotalSeconds);
                            Task.Delay(PollInterval).ContinueWith(_ => WorkExecutor.StartNew(Run));
                        } else if (Mode == ChangeTrackerMode.OneShot) {
                            Mode = ChangeTrackerMode.LongPoll;
                            WorkExecutor.StartNew(Run);
                        }
                    } else {
                        if (client != null) {
                            client.ChangeTrackerFinished(this);
                        }
                    }
                }
            } else {
                backoff.SleepAppropriateAmountOfTime();
                if (beforeFirstItem) {
                    var elapsed = DateTime.Now - _startTime;
                    Log.To.ChangeTracker.W(Tag, "{0} longpoll connection closed (by proxy?) after {0} sec", 
                        this, elapsed.TotalSeconds);

                    // Looks like the connection got closed by a proxy (like AWS' load balancer) while the
                    // server was waiting for a change to send, due to lack of activity.
                    // Lower the heartbeat time to work around this, and reconnect:
                    long newTicks = (long)(elapsed.Ticks * 0.75);
                    Heartbeat = new TimeSpan(newTicks);
                    backoff.ResetBackoff();
                    WorkExecutor.StartNew(Run);
                } else {
                    Log.To.ChangeTracker.W(Tag, "{0} Received improper _changes feed response", this);
                    if (Continuous) {
                        Log.To.ChangeTracker.I(Tag, "{0} sleeping...", this);
                        backoff.SleepAppropriateAmountOfTime();
                        Log.To.ChangeTracker.I(Tag, "{0} retrying...", this);
                        WorkExecutor.StartNew(Run);
                    } else {
                        Log.To.ChangeTracker.I(Tag, "{0} stopping...", this);
                        WorkExecutor.StartNew(Stop);
                    }
                }
            }
        }

        private void ProcessOneShotStream(Task<Stream> t)
        {
            using (var jsonReader = Manager.GetObjectMapper().StartIncrementalParse(t.Result)) {
                bool timedOut = false;
                ReceivedPollResponse(jsonReader, ref timedOut);
            }

            Stopped();
        }

        private void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in RequestHeaders.Keys)  {
                request.Headers.Add(requestHeaderKey, RequestHeaders.Get(requestHeaderKey));
            }
        }
    }
}
