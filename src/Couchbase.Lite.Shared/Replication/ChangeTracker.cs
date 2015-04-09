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
using Newtonsoft.Json;

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

//        private Task runTask;

//        Task changesRequestTask;

        private readonly object stopMutex = new object();

        private HttpRequestMessage Request;

        private String filterName;

        private IDictionary<String, Object> filterParams;

        private IList<String> docIDs;

        internal ChangeTrackerBackoff backoff;

        protected internal IDictionary<string, object> RequestHeaders;

        private CancellationTokenSource tokenSource;

        CancellationTokenSource changesFeedRequestTokenSource;

        /// <summary>Set Authenticator for BASIC Authentication</summary>
        public IAuthenticator Authenticator { get; set; }

        public bool UsePost { get; set; }

        public Exception Error { get; private set; }

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

            var clientCopy = client;
            if (clientCopy == null)
            {
                // This is a race condition that can be reproduced by calling cbpuller.start() and cbpuller.stop()
                // directly afterwards.  What happens is that by the time the Changetracker thread fires up,
                // the cbpuller has already set this.client to null.  See issue #109
                Log.W(Tag, "ChangeTracker run() loop aborting because client == null");
                return;
            }

            if (tokenSource.IsCancellationRequested) {
                tokenSource.Dispose();
                tokenSource = new CancellationTokenSource();
            }

            backoff = new ChangeTrackerBackoff();

            while (IsRunning && !tokenSource.Token.IsCancellationRequested)
            {
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

                var maskedRemoteWithoutCredentials = url.ToString();
                maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@", "://---:---@");
                Log.V(Tag, "Making request to " + maskedRemoteWithoutCredentials);

                if (tokenSource.Token.IsCancellationRequested)
                {
                    break;
                }

                Task<HttpResponseMessage> changesRequestTask = null;
                Task<HttpResponseMessage> successHandler;
                Task<Boolean> errorHandler;

                HttpClient httpClient = null;
                try {
                    httpClient = clientCopy.GetHttpClient();
                    var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, Request.RequestUri);
                    if (authHeader != null)
                    {
                        httpClient.DefaultRequestHeaders.Authorization = authHeader;
                    }

                    changesFeedRequestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token);

                    var evt = new ManualResetEvent(false);

                    // We do this akward set of calls in order
                    // to help minimize the frequency of the error:
                    //
                    //   "Cannot re-call start of asynchronous method 
                    //    while a previous call is still in progress."
                    // 
                    // There's got to be a better way to deal with this.
                    var info = httpClient.SendAsync(
                        Request, 
                        changesFeedRequestTokenSource.Token
                    );
        
                    info.ContinueWith((t)=>
                        evt.Set()
                    );
                    evt.WaitOne(ManagerOptions.Default.RequestTimeout);

                    changesRequestTask = info;

                    successHandler = changesRequestTask.ContinueWith<HttpResponseMessage>(
                        ChangeFeedResponseHandler, 
                        changesFeedRequestTokenSource.Token, 
                        TaskContinuationOptions.LongRunning | TaskContinuationOptions.OnlyOnRanToCompletion, 
                        WorkExecutor.Scheduler
                    );

                    errorHandler = changesRequestTask.ContinueWith(t =>
                    {
                        if (t.IsCanceled) 
                        {
                            return false; // Not a real error.
                        }
                        var err = t.Exception.Flatten();
                        Log.D(Tag, "ChangeFeedResponseHandler faulted.", err.InnerException ?? err);
                        Error = err.InnerException ?? err;
                        backoff.SleepAppropriateAmountOfTime();
                        return true; // a real error.
                    }, changesFeedRequestTokenSource.Token, TaskContinuationOptions.OnlyOnFaulted, WorkExecutor.Scheduler);

                    try 
                    {
                        Task.WaitAll(new Task[] { successHandler, errorHandler }, (Int32)ManagerOptions.Default.RequestTimeout.TotalMilliseconds, changesFeedRequestTokenSource.Token);
                        Log.D(Tag, "Finished processing changes feed.");
                    } 
                    catch (Exception ex) {
                        var e = ex.InnerException ?? ex;
                        // Swallow TaskCancelledExceptions, which will always happen
                        // if either errorHandler or successHandler don't need to fire.
                        if (!(e is OperationCanceledException))
                            throw ex;
                    } 
                    finally 
                    {
                        if (changesRequestTask != null) 
                        {
                            if(changesRequestTask.IsCompleted)
                            {
                                changesRequestTask.Dispose();
                            }

                            changesRequestTask = null;
                        }

                        if (successHandler != null)
                        {
                            if(successHandler.IsCompleted)
                            {
                                successHandler.Dispose();
                            }

                            successHandler = null;
                        }

                        if (errorHandler != null) 
                        {
                            if(errorHandler.IsCompleted)
                            {
                                errorHandler.Dispose();
                            }

                            errorHandler = null;
                        }

                        if(Request != null)
                        {
                            Request.Dispose();
                            Request = null;
                        }

                        if(changesFeedRequestTokenSource != null)
                        {
                            changesFeedRequestTokenSource.Dispose();
                            changesFeedRequestTokenSource = null;
                        }
                    }
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
                finally
                {
                    if (httpClient != null)
                    {
                        httpClient.Dispose();
                    }

                    if (mode == ChangeTrackerMode.OneShot)
                    {
                        Stop();
                    }
                }
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
                return response;
            }
                
            switch (mode)
            {
                case ChangeTrackerMode.LongPoll:
                    {
                        if (response.Content == null) {
                            throw new CouchbaseLiteException("Got empty change tracker response", status.GetStatusCode());
                        }

                        var content = response.Content.ReadAsByteArrayAsync().Result;
                        IDictionary<string, object> fullBody;
                        try
                        {
                            fullBody = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(content) ?? new Dictionary<string, object>();
                        } 
                        catch (JsonSerializationException ex)
                        {
                            const string timeoutContent = "{\"results\":[";
                            if (!Encoding.UTF8.GetString(content).Trim().Equals(timeoutContent))
                                throw ex;
                            Log.V(Tag, "Timeout while waiting for changes.");
                            backoff.SleepAppropriateAmountOfTime();
                            return response;
                        }
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
            var changes = response.Get("results").AsList<IDictionary<string, object>>();
            if (changes == null)
            {
                return false;
            }
            foreach (var change in changes)
            {
                if (! ReceivedChange(change))
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

        Thread thread;

        public bool Start()
        {
            if (IsRunning)
            {
                return false;
            }

            this.Error = null;
            this.thread = new Thread(Run) { IsBackground = true, Name = "Change Tracker Thread" };
            thread.Start();

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

                var feedTokenSource = changesFeedRequestTokenSource;
                if (feedTokenSource != null && !feedTokenSource.IsCancellationRequested)
                {
                    try {
                        feedTokenSource.Cancel();
                    }catch(ObjectDisposedException) {
                        //FIXME Run() will often dispose this token source right out from under us since it
                        //is running on a separate thread.
                        Log.W(Tag, "Race condition on changesFeedRequestTokenSource detected");
                    }catch(AggregateException e) {
                        if (e.InnerException is ObjectDisposedException) {
                            Log.W(Tag, "Race condition on changesFeedRequestTokenSource detected");
                        } else {
                            throw;
                        }
                    }
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
                bodyParams["limit"] = LongPollModeLimit;
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
