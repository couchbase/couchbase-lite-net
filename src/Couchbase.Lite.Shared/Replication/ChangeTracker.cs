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
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using Sharpen;
using System.Text;

namespace Couchbase.Lite.Replicator
{
    internal enum ChangeTrackerMode
    {
        OneShot,
        LongPoll
    }


    /// <summary>
    /// Reads the continuous-mode _changes feed of a database, and sends the
    /// individual change entries to its client's changeTrackerReceivedChange()
    /// </summary>
    internal class ChangeTracker
    {
        const String Tag = "ChangeTracker";

        const Int32 LongPollModeLimit = 50;

        const Int32 HeartbeatMilliseconds = 300000;

        private Uri databaseURL;

        private IChangeTrackerClient client;

        private ChangeTrackerMode mode;

        private Object lastSequenceID;

        private Boolean includeConflicts;

        private TaskFactory WorkExecutor;

        private Task runTask;

        Task changesRequestTask;

        private readonly object stopMutex = new object();

        private HttpRequestMessage Request;

        private String filterName;

        private IDictionary<String, Object> filterParams;

        private IList<String> docIDs;

        private Exception Error;

        internal ChangeTrackerBackoff backoff;

        protected internal IDictionary<string, object> RequestHeaders;

        private readonly CancellationTokenSource tokenSource;

        /// <summary>Set Authenticator for BASIC Authentication</summary>
        public IAuthenticator Authenticator { get; set; }

        public bool UsePost { get; set; }

        public ChangeTracker(Uri databaseURL, ChangeTrackerMode mode, object lastSequenceID, 
            Boolean includeConflicts, IChangeTrackerClient client, TaskFactory workExecutor = null)
        {
            // does not work, do not use it.
            this.databaseURL = databaseURL;
            this.mode = mode;
            this.includeConflicts = includeConflicts;
            this.lastSequenceID = lastSequenceID;
            this.client = client;
            this.RequestHeaders = new Dictionary<string, object>();
            this.tokenSource = new CancellationTokenSource();
            WorkExecutor = workExecutor ?? Task.Factory;
        }

        public void SetFilterName(string filterName)
        {
            this.filterName = filterName;
        }

        public void SetFilterParams(IDictionary<String, Object> filterParams)
        {
            this.filterParams = filterParams;
        }

        public void SetClient(IChangeTrackerClient client)
        {
            this.client = client;
        }

        public string GetDatabaseName()
        {
            string result = null;
            if (databaseURL != null)
            {
                result = databaseURL.AbsolutePath;
                if (result != null)
                {
                    int pathLastSlashPos = result.LastIndexOf('/');
                    if (pathLastSlashPos > 0)
                    {
                        result = Sharpen.Runtime.Substring(result, pathLastSlashPos);
                    }
                }
            }
            return result;
        }

        public string GetChangesFeedPath()
        {
            if (UsePost)
            {
                return "_changes";
            }

            var path = new StringBuilder("_changes?feed=");
            path.Append(GetFeed());

            if (mode == ChangeTrackerMode.LongPoll)
            {
                path.Append(string.Format("&limit={0}", LongPollModeLimit));
            }
            path.Append(string.Format("&heartbeat={0}", HeartbeatMilliseconds));
            if (includeConflicts)
            {
                path.Append("&style=all_docs");
            }
            if (lastSequenceID != null)
            {
                path.Append("&since=");
                path.Append(Uri.EscapeUriString(lastSequenceID.ToString()));
            }
            if (docIDs != null && docIDs.Count > 0)
            {
                filterName = "_doc_ids";
                filterParams = new Dictionary<string, object>();
                filterParams.Put("doc_ids", docIDs);
            }
            if (filterName != null)
            {
                path.Append("&filter=");
                path.Append(Uri.EscapeUriString(filterName));
                if (filterParams != null)
                {
                    foreach (string filterParamKey in filterParams.Keys)
                    {
                        var value = filterParams.Get(filterParamKey);
                        if (!(value is string))
                        {
                            try
                            {
                                value = Manager.GetObjectMapper().WriteValueAsString(value);
                            }
                            catch (IOException e)
                            {
                                throw new InvalidOperationException("Unable to JSON-serialize a filter parameter value.", e);
                            }
                        }
                        path.Append("&");
                        path.Append(Uri.EscapeUriString(filterParamKey));
                        path.Append("=");
                        path.Append(Uri.EscapeUriString(value.ToString()));
                    }
                }
            }
            return path.ToString();
        }

        public Uri GetChangesFeedURL()
        {
            var dbURLString = databaseURL.ToString();
            if (!dbURLString.EndsWith ("/", StringComparison.Ordinal))
            {
                dbURLString += "/";
            }
            dbURLString += GetChangesFeedPath();

            Uri result = null;
            try
            {
                result = new Uri(dbURLString);
            }
            catch (UriFormatException e)
            {
                Log.E(Tag, "Changes feed ULR is malformed", e);
            }
            return result;
        }

        // TODO: Needs to refactored into smaller calls. Each continuation could be its own method, for example.
        public void Run()
        {
            IsRunning = true;

            if (client == null)
            {
                // This is a race condition that can be reproduced by calling cbpuller.start() and cbpuller.stop()
                // directly afterwards.  What happens is that by the time the Changetracker thread fires up,
                // the cbpuller has already set this.client to null.  See issue #109
                Log.W(Tag, "ChangeTracker run() loop aborting because client == null");
                return;
            }

            backoff = new ChangeTrackerBackoff();
            HttpClient httpClient = client.GetHttpClient();

            while (IsRunning && !tokenSource.Token.IsCancellationRequested)
            {
                if (changesRequestTask != null && !changesRequestTask.IsCanceled && !changesRequestTask.IsFaulted) 
                {
                    Thread.Sleep(500);
                    continue;
                }

                if (Request != null)
                {
                    Request.Dispose();
                    Request = null;
                }

                var url = GetChangesFeedURL();
                if (UsePost)
                {
                    Request = new HttpRequestMessage(HttpMethod.Post, url);
                    var body = GetChangesFeedPostBody();
                    Request.Content = new StringContent(body);
                    Request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }
                else
                {
                    Request = new HttpRequestMessage(HttpMethod.Get, url);
                }

                AddRequestHeaders(Request);

                var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, Request.RequestUri);
                if (authHeader != null)
                {
                    httpClient.DefaultRequestHeaders.Authorization = authHeader;
                }

                var maskedRemoteWithoutCredentials = url.ToString();
                maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@", "://---:---@");
                Log.V(Tag, "Making request to " + maskedRemoteWithoutCredentials);

                if (tokenSource.Token.IsCancellationRequested)
                    break;

                try {
                    var singleRequestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token);
                    var response = httpClient
                        .SendAsync(Request, HttpCompletionOption.ResponseHeadersRead, singleRequestTokenSource.Token)
                        .ContinueWith<HttpResponseMessage>(ChangeFeedResponseHandler, singleRequestTokenSource.Token, TaskContinuationOptions.LongRunning, WorkExecutor.Scheduler);
                    var result = response.Result;
                    //                    ChangeFeedResponseHandler(response);
                    if (result != null)
                    {
                        result.Dispose();
                    }
                    response.Dispose();
                    Request.Dispose();
                }
                catch (Exception e)
                {
                    if (!IsRunning && e.InnerException is IOException)
                    {
                        // swallow
                    }
                    else
                    {
                        // in this case, just silently absorb the exception because it
                        // frequently happens when we're shutting down and have to
                        // close the socket underneath our read.
                        Log.E(Tag, "Exception in change tracker", e);
                    }
                    backoff.SleepAppropriateAmountOfTime();
                }
//                var singleRequestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token);
//                var cTask = httpClient.SendAsync(Request, HttpCompletionOption.ResponseHeadersRead, singleRequestTokenSource.Token);
//                var bTask = cTask
//                    .ContinueWith<HttpResponseMessage>(t =>
//                    {
//                        if (!IsRunning)
//                        {
//                            return null;
//                            // swallow
//                        }
//                        if (t.IsFaulted && t.Exception.InnerException is IOException)
//                        {
//                            // in this case, just silently absorb the exception because it
//                            // frequently happens when we're shutting down and have to
//                            // close the socket underneath our read.
//                            Log.E(Tag, "Exception in change tracker", t.Exception);
//                            return null;
//                        }
//                        if (!singleRequestTokenSource.IsCancellationRequested && t.Exception != null)
//                        {
//                            var e = t.Exception.InnerException as WebException;
//                            var status = (HttpStatusCode)e.Status;
//                            if ((Int32)status >= 300 && !Misc.IsTransientError(status))
//                            {
//                                var response = t.Result;
//                                var msg = response.Content != null 
//                                    ? String.Format("Change tracker got error with status code: {0}", status)
//                                    : String.Format("Change tracker got error with status code: {0} and null response content", status);
//                                Log.E(Tag, msg);
//                                Error = new CouchbaseLiteException(msg, new Status(status.GetStatusCode()));
//                                Stop();
//                            }
//                            backoff.SleepAppropriateAmountOfTime();
//                        }
//                        return t.Result;
//                    }, singleRequestTokenSource.Token, TaskContinuationOptions.OnlyOnFaulted, WorkExecutor.Scheduler)
//                    .ContinueWith<HttpResponseMessage>(ChangeFeedResponseHandler, singleRequestTokenSource.Token, TaskContinuationOptions.OnlyOnRanToCompletion, WorkExecutor.Scheduler)
//                    .ContinueWith<HttpResponseMessage>(t => 
//                    {
//                        Log.D(Tag, "ChangeFeedResponseHandler finished.");
//                        singleRequestTokenSource.Token.ThrowIfCancellationRequested();
//                        if (t != null) t.Result.Dispose();
//                        return null;
//                    }, singleRequestTokenSource.Token, TaskContinuationOptions.OnlyOnRanToCompletion, WorkExecutor.Scheduler);
//                changesRequestTask = bTask;
//                    .ContinueWith((t) => 
//                    {
//                        Log.D(Tag, "ChangeFeedResponseHandler faulted.");
//                    }, singleRequestTokenSource.Token, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent, TaskScheduler.Default);
            }
            if (httpClient != null) 
            {
                httpClient.Dispose();
            }
        }

        HttpResponseMessage ChangeFeedResponseHandler(Task<HttpResponseMessage> responseTask)
        {
            var response = responseTask.Result;
            if (response == null)
                return null;
            var status = response.StatusCode;
            if ((Int32)status >= 300 && !Misc.IsTransientError(status))
            {
                var msg = response.Content != null 
                    ? String.Format("Change tracker got error with status code: {0}", status)
                    : String.Format("Change tracker got error with status code: {0} and null response content", status);
                Log.E(Tag, msg);
                Error = new CouchbaseLiteException (msg, new Status (status.GetStatusCode ()));
                Stop();
            }
                
            switch (mode)
            {
                case ChangeTrackerMode.LongPoll:
                    {
                        var content = response.Content.ReadAsByteArrayAsync().Result;
                        var fullBody = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(content.AsEnumerable());
                        var responseOK = ReceivedPollResponse(fullBody);
                        if (responseOK)
                        {
                            Log.V(Tag, "Starting new longpoll");
                            backoff.ResetBackoff();
                            return response;
                        }
                        else
                        {
                            Log.W(Tag, "Change tracker calling stop");
                            Stop();
                        }
                    }
                    break;
                default:
                    {
                        var content = response.Content.ReadAsByteArrayAsync().Result;
                        var results = Manager.GetObjectMapper().ReadValue<IDictionary<String, Object>>(content.AsEnumerable());
                        Log.D(Tag, "Received results from changes feed: {0}", results);
                        var resultsValue = results["results"] as JArray;
                        foreach (var item in resultsValue)
                        {
                            IDictionary<String, Object> change = null;
                            try
                            {
                                change = item.ToObject<IDictionary<String, Object>>();
                            }
                            catch (Exception)
                            {
                                Log.E(Tag, this + string.Format(": Received unparseable change line from server: {0}", change));
                            }
                            if (!ReceivedChange(change))
                            {
                                Log.W(Tag, this + string.Format(": Received unparseable change line from server: {0}", change));
                            }
                        }
                    
                        // As ReceivedChange() dispatches the change event to its client via WorkExecutor,
                        // to avoid the Stop() to be called before the client is done handling the change event,    
                        // we need to setup the Stop() call with the WorkExecutor as well 
                        // (Assuming that the WorkExecutor is a single thread executor).

                        WorkExecutor.StartNew(Stop);

                        return response;
                    }
            }

            backoff.ResetBackoff();
            return response;
        }

        public bool ReceivedChange(IDictionary<string, object> change)
        {
            var seq = change.Get("seq");
            if (seq == null)
            {
                return false;
            }

            //pass the change to the client on the thread that created this change tracker
            if (client != null)
            {
                Log.D(Tag, "changed tracker posting change");
                client.ChangeTrackerReceivedChange(change);
            }
            lastSequenceID = seq;
            return true;
        }

        public bool ReceivedPollResponse(IDictionary<string, object> response)
        {
            var changes = ((JArray)response.Get("results")).ToList();
            if (changes == null)
            {
                return false;
            }
            foreach (var change in changes)
            {
                var changeDict = change.ToObject<IDictionary<string, object>>();
                if (! ReceivedChange(changeDict))
                {
                    return false;
                }
            }
            return true;
        }

        public void SetUpstreamError(string message)
        {
            Log.W(Tag, this + string.Format(": Server error: {0}", message));
            this.Error = new Exception(message);
        }

        public bool Start()
        {
            this.Error = null;
            this.runTask = Task.Factory
                .StartNew(Run, tokenSource.Token, TaskCreationOptions.LongRunning, WorkExecutor.Scheduler)
                .ContinueWith(t =>
                {
                    Log.E(Tag, "Run task faulted", t.Exception);
                }, TaskContinuationOptions.OnlyOnFaulted);
            return true;
        }

        public void Stop()
        {
            // Lock to prevent multiple calls to Stop() method from different
            // threads (eg. one from ChangeTracker itself and one from any other
            // consumers).
            lock(stopMutex)
            {
                if (!IsRunning)
                {
                    return;
                }

                Log.D(Tag, "changed tracker asked to stop");

                IsRunning = false;

                if (Request != null)
                {
                    tokenSource.Cancel();
                }

                Stopped();
            }
        }

        public void Stopped()
        {
            Log.D(Tag, "change tracker in stopped");
            if (client != null)
            {
                Log.D(Tag, "posting stopped");
                client.ChangeTrackerStopped(this);
            }
            client = null;
            Log.D(Tag, "change tracker client should be null now");
        }

        public Exception GetLastError()
        {
            return Error;
        }

        public void SetDocIDs(IList<string> docIDs)
        {
            this.docIDs = docIDs;
        }

        public bool IsRunning
        {
            get; private set;
        }

        internal void SetRequestHeaders(IDictionary<String, Object> requestHeaders)
        {
            RequestHeaders = requestHeaders;
        }

        private void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in RequestHeaders.Keys)
            {
                request.Headers.Add(requestHeaderKey, RequestHeaders.Get(requestHeaderKey).ToString());
            }
        }

        private string GetFeed()
        {
            switch (mode)
            {
                case ChangeTrackerMode.LongPoll:
                    return "longpoll";
                default:
                    return "normal";
            }
        }

        internal IDictionary<string, object> GetChangesFeedParams()
        {
            if (!UsePost)
            {
                return null;
            }

            if (docIDs != null && docIDs.Count > 0)
            {
                filterName = "_doc_ids";
                filterParams = new Dictionary<string, object>();
                filterParams.Put("doc_ids", docIDs);
            }

            var bodyParams = new Dictionary<string, object>();
            bodyParams["feed"] = GetFeed();
            bodyParams["heartbeat"] = HeartbeatMilliseconds;

            if (includeConflicts) 
            {
                bodyParams["style"] = "all_docs";
            }
            else
            {
                bodyParams["style"] = null;
            }

            if (lastSequenceID != null)
            {
                Int64 sequenceAsLong;
                var success = Int64.TryParse(lastSequenceID.ToString(), out sequenceAsLong);
                bodyParams["since"] = success ? sequenceAsLong : lastSequenceID;
            }

            if (mode == ChangeTrackerMode.LongPoll)
            {
                bodyParams["limit"] = LongPollModeLimit > 0  //NOTE: This is to make tweaking this value easier.
                    ? LongPollModeLimit.ToString() 
                    : null;
            }

            if (docIDs != null && docIDs.Count > 0)
            {
                filterName = "_doc_ids";
                filterParams = new Dictionary<String, Object>();
                filterParams["doc_ids"] = docIDs;
            }

            if (filterName != null)
            {
                bodyParams["filter"] = filterName;
                bodyParams.PutAll(filterParams);
            }

            return bodyParams;
        }

        internal string GetChangesFeedPostBody()
        {
            var parameters = GetChangesFeedParams();
            var mapper = Manager.GetObjectMapper();
            var body = mapper.WriteValueAsString(parameters);
            return body;
        }
    }
}
