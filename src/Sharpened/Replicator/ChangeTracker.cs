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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Apache.Http;
using Apache.Http.Auth;
using Apache.Http.Client;
using Apache.Http.Client.Methods;
using Apache.Http.Client.Protocol;
using Apache.Http.Entity;
using Apache.Http.Impl.Auth;
using Apache.Http.Impl.Client;
using Apache.Http.Protocol;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using Org.Codehaus.Jackson;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
    /// <summary>
    /// Reads the continuous-mode _changes feed of a database, and sends the
    /// individual change entries to its client's changeTrackerReceivedChange()
    /// </summary>
    /// <exclude></exclude>
    public class ChangeTracker : Runnable
    {
        private Uri databaseURL;

        private ChangeTrackerClient client;

        private ChangeTracker.ChangeTrackerMode mode;

        private object lastSequenceID;

        private bool includeConflicts;

        private Sharpen.Thread thread;

        private bool running = false;

        private HttpRequestMessage request;

        private string filterName;

        private IDictionary<string, object> filterParams;

        private IList<string> docIDs;

        private Exception error;

        protected internal IDictionary<string, object> requestHeaders;

        protected internal ChangeTrackerBackoff backoff;

        private bool usePOST;

        private int heartBeatSeconds;

        private int limit;

        private Authenticator authenticator;

        public enum ChangeTrackerMode
        {
            OneShot,
            LongPoll,
            Continuous
        }

        public ChangeTracker(Uri databaseURL, ChangeTracker.ChangeTrackerMode mode, bool 
            includeConflicts, object lastSequenceID, ChangeTrackerClient client)
        {
            // does not work, do not use it.
            this.databaseURL = databaseURL;
            this.mode = mode;
            this.includeConflicts = includeConflicts;
            this.lastSequenceID = lastSequenceID;
            this.client = client;
            this.requestHeaders = new Dictionary<string, object>();
            this.heartBeatSeconds = 300;
            this.limit = 50;
        }

        public virtual void SetFilterName(string filterName)
        {
            this.filterName = filterName;
        }

        public virtual void SetFilterParams(IDictionary<string, object> filterParams)
        {
            this.filterParams = filterParams;
        }

        public virtual void SetClient(ChangeTrackerClient client)
        {
            this.client = client;
        }

        public virtual string GetDatabaseName()
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

        public virtual string GetFeed()
        {
            switch (mode)
            {
                case ChangeTracker.ChangeTrackerMode.OneShot:
                {
                    return "normal";
                }

                case ChangeTracker.ChangeTrackerMode.LongPoll:
                {
                    return "longpoll";
                }

                case ChangeTracker.ChangeTrackerMode.Continuous:
                {
                    return "continuous";
                }
            }
            return "normal";
        }

        public virtual long GetHeartbeatMilliseconds()
        {
            return heartBeatSeconds * 1000;
        }

        public virtual string GetChangesFeedPath()
        {
            if (usePOST)
            {
                return "_changes";
            }
            string path = "_changes?feed=";
            path += GetFeed();
            if (mode == ChangeTracker.ChangeTrackerMode.LongPoll)
            {
                path += string.Format("&limit=%s", limit);
            }
            path += string.Format("&heartbeat=%s", GetHeartbeatMilliseconds());
            if (includeConflicts)
            {
                path += "&style=all_docs";
            }
            if (lastSequenceID != null)
            {
                path += "&since=" + URLEncoder.Encode(lastSequenceID.ToString());
            }
            if (docIDs != null && docIDs.Count > 0)
            {
                filterName = "_doc_ids";
                filterParams = new Dictionary<string, object>();
                filterParams.Put("doc_ids", docIDs);
            }
            if (filterName != null)
            {
                path += "&filter=" + URLEncoder.Encode(filterName);
                if (filterParams != null)
                {
                    foreach (string filterParamKey in filterParams.Keys)
                    {
                        object value = filterParams.Get(filterParamKey);
                        if (!(value is string))
                        {
                            try
                            {
                                value = Manager.GetObjectMapper().WriteValueAsString(value);
                            }
                            catch (IOException e)
                            {
                                throw new ArgumentException(e);
                            }
                        }
                        path += "&" + URLEncoder.Encode(filterParamKey) + "=" + URLEncoder.Encode(value.ToString
                            ());
                    }
                }
            }
            return path;
        }

        public virtual Uri GetChangesFeedURL()
        {
            string dbURLString = databaseURL.ToExternalForm();
            if (!dbURLString.EndsWith("/"))
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
                Log.E(Log.TagChangeTracker, this + ": Changes feed ULR is malformed", e);
            }
            return result;
        }

        /// <summary>Set Authenticator for BASIC Authentication</summary>
        public virtual void SetAuthenticator(Authenticator authenticator)
        {
            this.authenticator = authenticator;
        }

        public virtual void Run()
        {
            running = true;
            HttpClient httpClient;
            if (client == null)
            {
                // This is a race condition that can be reproduced by calling cbpuller.start() and cbpuller.stop()
                // directly afterwards.  What happens is that by the time the Changetracker thread fires up,
                // the cbpuller has already set this.client to null.  See issue #109
                Log.W(Log.TagChangeTracker, "%s: ChangeTracker run() loop aborting because client == null"
                    , this);
                return;
            }
            if (mode == ChangeTracker.ChangeTrackerMode.Continuous)
            {
                // there is a failing unit test for this, and from looking at the code the Replication
                // object will never use Continuous mode anyway.  Explicitly prevent its use until
                // it is demonstrated to actually work.
                throw new RuntimeException("ChangeTracker does not correctly support continuous mode"
                    );
            }
            httpClient = client.GetHttpClient();
            backoff = new ChangeTrackerBackoff();
            while (running)
            {
                Uri url = GetChangesFeedURL();
                if (usePOST)
                {
                    HttpPost postRequest = new HttpPost(url.ToString());
                    postRequest.SetHeader("Content-Type", "application/json");
                    StringEntity entity;
                    try
                    {
                        entity = new StringEntity(ChangesFeedPOSTBody());
                    }
                    catch (UnsupportedEncodingException e)
                    {
                        throw new RuntimeException(e);
                    }
                    postRequest.SetEntity(entity);
                    request = postRequest;
                }
                else
                {
                    request = new HttpGet(url.ToString());
                }
                AddRequestHeaders(request);
                // Perform BASIC Authentication if needed
                bool isUrlBasedUserInfo = false;
                // If the URL contains user info AND if this a DefaultHttpClient then preemptively set the auth credentials
                string userInfo = url.GetUserInfo();
                if (userInfo != null)
                {
                    isUrlBasedUserInfo = true;
                }
                else
                {
                    if (authenticator != null)
                    {
                        AuthenticatorImpl auth = (AuthenticatorImpl)authenticator;
                        userInfo = auth.AuthUserInfo();
                    }
                }
                if (userInfo != null)
                {
                    if (userInfo.Contains(":") && !userInfo.Trim().Equals(":"))
                    {
                        string[] userInfoElements = userInfo.Split(":");
                        string username = isUrlBasedUserInfo ? URIUtils.Decode(userInfoElements[0]) : userInfoElements
                            [0];
                        string password = isUrlBasedUserInfo ? URIUtils.Decode(userInfoElements[1]) : userInfoElements
                            [1];
                        Credentials credentials = new UsernamePasswordCredentials(username, password);
                        if (httpClient is DefaultHttpClient)
                        {
                            DefaultHttpClient dhc = (DefaultHttpClient)httpClient;
                            MessageProcessingHandler preemptiveAuth = new _MessageProcessingHandler_285(credentials
                                );
                            dhc.AddRequestInterceptor(preemptiveAuth, 0);
                        }
                    }
                    else
                    {
                        Log.W(Log.TagChangeTracker, "RemoteRequest Unable to parse user info, not setting credentials"
                            );
                    }
                }
                try
                {
                    string maskedRemoteWithoutCredentials = GetChangesFeedURL().ToString();
                    maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
                        , "://---:---@");
                    Log.V(Log.TagChangeTracker, "%s: Making request to %s", this, maskedRemoteWithoutCredentials
                        );
                    HttpResponse response = httpClient.Execute(request);
                    StatusLine status = response.GetStatusLine();
                    if (status.GetStatusCode() >= 300 && !Utils.IsTransientError(status))
                    {
                        Log.E(Log.TagChangeTracker, "%s: Change tracker got error %d", this, status.GetStatusCode
                            ());
                        this.error = new HttpResponseException(status.GetStatusCode(), status.GetReasonPhrase
                            ());
                        Stop();
                    }
                    HttpEntity entity = response.GetEntity();
                    InputStream input = null;
                    if (entity != null)
                    {
                        try
                        {
                            input = entity.GetContent();
                            if (mode == ChangeTracker.ChangeTrackerMode.LongPoll)
                            {
                                // continuous replications
                                IDictionary<string, object> fullBody = Manager.GetObjectMapper().ReadValue<IDictionary
                                    >(input);
                                bool responseOK = ReceivedPollResponse(fullBody);
                                if (mode == ChangeTracker.ChangeTrackerMode.LongPoll && responseOK)
                                {
                                    Log.V(Log.TagChangeTracker, "%s: Starting new longpoll", this);
                                    backoff.ResetBackoff();
                                    continue;
                                }
                                else
                                {
                                    Log.W(Log.TagChangeTracker, "%s: Change tracker calling stop (LongPoll)", this);
                                    Stop();
                                }
                            }
                            else
                            {
                                // one-shot replications
                                JsonFactory jsonFactory = Manager.GetObjectMapper().GetJsonFactory();
                                JsonParser jp = jsonFactory.CreateJsonParser(input);
                                while (jp.NextToken() != JsonToken.StartArray)
                                {
                                }
                                // ignore these tokens
                                while (jp.NextToken() == JsonToken.StartObject)
                                {
                                    IDictionary<string, object> change = (IDictionary)Manager.GetObjectMapper().ReadValue
                                        <IDictionary>(jp);
                                    if (!ReceivedChange(change))
                                    {
                                        Log.W(Log.TagChangeTracker, "Received unparseable change line from server: %s", change
                                            );
                                    }
                                }
                                Log.W(Log.TagChangeTracker, "%s: Change tracker calling stop (OneShot)", this);
                                Stop();
                                break;
                            }
                            backoff.ResetBackoff();
                        }
                        finally
                        {
                            try
                            {
                                entity.ConsumeContent();
                            }
                            catch (IOException)
                            {
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!running && e is IOException)
                    {
                    }
                    else
                    {
                        // in this case, just silently absorb the exception because it
                        // frequently happens when we're shutting down and have to
                        // close the socket underneath our read.
                        Log.E(Log.TagChangeTracker, this + ": Exception in change tracker", e);
                    }
                    backoff.SleepAppropriateAmountOfTime();
                }
            }
            Log.V(Log.TagChangeTracker, "%s: Change tracker run loop exiting", this);
        }

        private sealed class _MessageProcessingHandler_285 : MessageProcessingHandler
        {
            public _MessageProcessingHandler_285(Credentials credentials)
            {
                this.credentials = credentials;
            }

            /// <exception cref="Apache.Http.HttpException"></exception>
            /// <exception cref="System.IO.IOException"></exception>
            public void Process(HttpWebRequest request, HttpContext context)
            {
                AuthState authState = (AuthState)context.GetAttribute(ClientContext.TargetAuthState
                    );
                if (authState.GetAuthScheme() == null)
                {
                    authState.SetAuthScheme(new BasicScheme());
                    authState.SetCredentials(credentials);
                }
            }

            private readonly Credentials credentials;
        }

        public virtual bool ReceivedChange(IDictionary<string, object> change)
        {
            object seq = change.Get("seq");
            if (seq == null)
            {
                return false;
            }
            //pass the change to the client on the thread that created this change tracker
            if (client != null)
            {
                client.ChangeTrackerReceivedChange(change);
            }
            lastSequenceID = seq;
            return true;
        }

        public virtual bool ReceivedPollResponse(IDictionary<string, object> response)
        {
            IList<IDictionary<string, object>> changes = (IList)response.Get("results");
            if (changes == null)
            {
                return false;
            }
            foreach (IDictionary<string, object> change in changes)
            {
                if (!ReceivedChange(change))
                {
                    return false;
                }
            }
            return true;
        }

        public virtual void SetUpstreamError(string message)
        {
            Log.W(Log.TagChangeTracker, "Server error: %s", message);
            this.error = new Exception(message);
        }

        public virtual bool Start()
        {
            this.error = null;
            string maskedRemoteWithoutCredentials = databaseURL.ToExternalForm();
            maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
                , "://---:---@");
            thread = new Sharpen.Thread(this, "ChangeTracker-" + maskedRemoteWithoutCredentials
                );
            thread.Start();
            return true;
        }

        public virtual void Stop()
        {
            Log.D(Log.TagChangeTracker, "%s: Changed tracker asked to stop", this);
            running = false;
            thread.Interrupt();
            if (request != null)
            {
                request.Abort();
            }
            Stopped();
        }

        public virtual void Stopped()
        {
            Log.D(Log.TagChangeTracker, "%s: Change tracker in stopped", this);
            if (client != null)
            {
                client.ChangeTrackerStopped(this);
            }
            client = null;
        }

        internal virtual void SetRequestHeaders(IDictionary<string, object> requestHeaders
            )
        {
            this.requestHeaders = requestHeaders;
        }

        private void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in requestHeaders.Keys)
            {
                request.AddHeader(requestHeaderKey, requestHeaders.Get(requestHeaderKey).ToString
                    ());
            }
        }

        public virtual Exception GetLastError()
        {
            return error;
        }

        public virtual bool IsRunning()
        {
            return running;
        }

        public virtual void SetDocIDs(IList<string> docIDs)
        {
            this.docIDs = docIDs;
        }

        public virtual string ChangesFeedPOSTBody()
        {
            IDictionary<string, object> postBodyMap = ChangesFeedPOSTBodyMap();
            try
            {
                return Manager.GetObjectMapper().WriteValueAsString(postBodyMap);
            }
            catch (IOException e)
            {
                throw new RuntimeException(e);
            }
        }

        public virtual bool IsUsePOST()
        {
            return usePOST;
        }

        public virtual void SetUsePOST(bool usePOST)
        {
            this.usePOST = usePOST;
        }

        public virtual IDictionary<string, object> ChangesFeedPOSTBodyMap()
        {
            if (!usePOST)
            {
                return null;
            }
            if (docIDs != null && docIDs.Count > 0)
            {
                filterName = "_doc_ids";
                filterParams = new Dictionary<string, object>();
                filterParams.Put("doc_ids", docIDs);
            }
            IDictionary<string, object> post = new Dictionary<string, object>();
            post.Put("feed", GetFeed());
            post.Put("heartbeat", GetHeartbeatMilliseconds());
            if (includeConflicts)
            {
                post.Put("style", "all_docs");
            }
            else
            {
                post.Put("style", null);
            }
            if (lastSequenceID != null)
            {
                try
                {
                    post.Put("since", long.Parse(lastSequenceID.ToString()));
                }
                catch (FormatException)
                {
                    post.Put("since", lastSequenceID.ToString());
                }
            }
            if (mode == ChangeTracker.ChangeTrackerMode.LongPoll && limit > 0)
            {
                post.Put("limit", limit);
            }
            else
            {
                post.Put("limit", null);
            }
            if (filterName != null)
            {
                post.Put("filter", filterName);
                post.PutAll(filterParams);
            }
            return post;
        }
    }
}
