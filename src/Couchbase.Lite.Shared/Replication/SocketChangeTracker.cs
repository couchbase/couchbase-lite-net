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

        private readonly object stopMutex = new object();
        private HttpRequestMessage Request;
        private CancellationTokenSource tokenSource;
        CancellationTokenSource changesFeedRequestTokenSource;
        private CouchbaseLiteHttpClient _httpClient;
        private IChangeTrackerResponseLogic _responseLogic;


        public SocketChangeTracker(Uri databaseURL, ChangeTrackerMode mode, bool includeConflicts, 
            object lastSequenceID, IChangeTrackerClient client, int retryCount, TaskFactory workExecutor = null)
            : base(databaseURL, mode, includeConflicts, lastSequenceID, client, retryCount, workExecutor)
        {
            tokenSource = new CancellationTokenSource();
            _responseLogic = ChangeTrackerResponseLogicFactory.CreateLogic(this);
            _responseLogic.Heartbeat = Heartbeat;
            _responseLogic.OnCaughtUp = () => Misc.IfNotNull(Client, c => c.ChangeTrackerCaughtUp(this));
            _responseLogic.OnChangeFound = (change) =>
            {
                if (!ReceivedChange(change)) {
                    Log.To.ChangeTracker.W(Tag, "Received unparseable change from server {0}", new LogJsonString(change));
                }
            };

            _responseLogic.OnFinished = e => RetryOrStopIfNecessary(e);
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
                RetryOrStopIfNecessary(e);
            }
        }

        private void PerformRetry(bool log)
        {
            if(Mode == ChangeTrackerMode.OneShot && PollInterval == TimeSpan.Zero) {
                Mode = ChangeTrackerMode.LongPoll;
            }

            if(PollInterval > TimeSpan.Zero) {
                if (log) {
                    Log.To.ChangeTracker.I(Tag, "{0} retrying in {1} seconds (PollInterval)...", 
                        this, PollInterval.TotalSeconds);
                }
                Task.Delay(PollInterval).ContinueWith(t1 => Run(), WorkExecutor.Scheduler);
            } else {
                if (log) {
                    Log.To.ChangeTracker.I(Tag, "{0} retrying NOW...", this);
                }
                WorkExecutor.StartNew(Run);
            }
        }

        private ContinuationAction RetryOrStopIfNecessary(Exception e)
        {
            var err = Misc.Flatten(e);
            if (err == null) {
                // No error occurred, keep going if continuous
                if (Continuous) {
                    PerformRetry(false);
                    return ContinuationAction.Retry;
                } else {
                    WorkExecutor.StartNew(Stop);
                    return ContinuationAction.Stop;
                }
            }

            string statusCode;
            if (Misc.IsTransientNetworkError(e, out statusCode)) {
                // Transient error occurred in a replication -> RETRY or STOP
                if (!Continuous && backoff.ReachedLimit) {
                    // Give up for non-continuous
                    Log.To.ChangeTracker.I(Tag, "{0} transient error ({1}) detected, giving up NOW...", this,
                        statusCode);
                    return ContinuationAction.Stop;
                } 

                // Keep retrying for continuous
                Log.To.ChangeTracker.I(Tag, "{0} transient error ({1}) detected, sleeping for {2}ms...", this,
                    statusCode, backoff.GetSleepTime().TotalMilliseconds);

                backoff.DelayAppropriateAmountOfTime().ContinueWith(t => PerformRetry(true));
                return ContinuationAction.Retry;
            } 

            if (String.IsNullOrEmpty(statusCode)) {
                Log.To.ChangeTracker.I(Tag, String.Format
                    ("{0} got an exception, stopping NOW...", this), err);
            } else {
                
                Log.To.ChangeTracker.I(Tag, String.Format
                    ("{0} got a non-transient error ({1}), stopping NOW...", this, statusCode));
            }

            // Non-transient error occurred in a continuous replication -> STOP
            WorkExecutor.StartNew(Stop);
            return ContinuationAction.Stop;
        }

        private ContinuationAction RetryOrStopIfNecessary(HttpStatusCode statusCode)
        {
            if ((int)statusCode >= 200 && (int)statusCode <= 299) {
                return ContinuationAction.NoAction;
            }

            if (!Continuous) {
                WorkExecutor.StartNew(Stop);
                return ContinuationAction.Stop;
            }

            if (!Misc.IsTransientError(statusCode)) {
                //
                Log.To.ChangeTracker.I(Tag, String.Format
                    ("{0} got a non-transient error ({1}), stopping NOW...", this, statusCode));
                WorkExecutor.StartNew(Stop);
                return ContinuationAction.Stop;
            }

            Log.To.ChangeTracker.I(Tag, "{0} transient error ({1}) detected, sleeping for {2}ms...", this,
                statusCode, backoff.GetSleepTime().TotalMilliseconds);
            backoff.DelayAppropriateAmountOfTime().ContinueWith(t => PerformRetry(true));

            return ContinuationAction.Retry;
        }

        private bool RetryIfFailedPost(Exception e)
        {
            if (!_usePost) {
                return false;
            }

            var statusCode = Misc.GetStatusCode(e as WebException);
            return RetryIfFailedPost(statusCode);
        }

        private bool RetryIfFailedPost(HttpStatusCode? statusCode)
        {
            if (!statusCode.HasValue || statusCode.Value != HttpStatusCode.MethodNotAllowed) {
                return false;
            }

            _usePost = false;
            Log.To.ChangeTracker.I(Tag, "Remote server doesn't support POST _changes, " +
                "retrying as GET");
            WorkExecutor.StartNew(Run);
            return true;
        }

        private bool ResponseFailed(Task<HttpResponseMessage> responseTask)
        {
            if (responseTask.Status == TaskStatus.RanToCompletion) {
                var response = responseTask.Result;
                if (response == null)
                    return true;

                return ResponseFailed(response);
            }

            var err = Misc.Flatten(responseTask.Exception);
            if(RetryIfFailedPost(err)) {
                return true;
            } else {
                RetryOrStopIfNecessary(err);
            }

            return true;
        }

        private bool ResponseFailed(HttpResponseMessage response)
        {
            var status = response.StatusCode;
            if ((Int32)status >= 300) {
                if (RetryIfFailedPost(status)) {
                    return true;
                }
   
                if (RetryOrStopIfNecessary(status) == ContinuationAction.NoAction) {
                    var msg = response.Content != null 
                        ? String.Format("Change tracker got error with status code: {0}", status)
                        : String.Format("Change tracker got error with status code: {0} and null response content", status);
                    Log.To.ChangeTracker.E(Tag, msg);
                    Error = new CouchbaseLiteException (msg, new Status (status.GetStatusCode ()));
                    Stop();
                }

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
            return response.Content.ReadAsStreamAsync().ContinueWith((Task<Stream> t) =>
            {
                try {
                    var result = _responseLogic.ProcessResponseStream(t.Result);
                    backoff.ResetBackoff();
                    if(result == ChangeTrackerResponseCode.ChangeHeartbeat) {
                        Heartbeat = _responseLogic.Heartbeat;
                        WorkExecutor.StartNew(Run);
                    }
                } catch (Exception e) {
                    RetryOrStopIfNecessary(e);
                } finally {
                    response.Dispose();
                }
            });
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

        private void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in RequestHeaders.Keys)  {
                request.Headers.Add(requestHeaderKey, RequestHeaders.Get(requestHeaderKey));
            }
        }
    }
}
