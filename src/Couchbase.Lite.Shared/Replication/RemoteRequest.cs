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
using System.IO;
using System.Net;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.ComponentModel;
using System.Security;

namespace Couchbase.Lite.Replication
{
    internal class RemoteRequest
    {
        const string Tag = "RemoteRequest";

        private const int MaxRetries = 2;

        private const int RetryDelayMs = 10 * 1000;

        protected internal TaskFactory workExecutor;

        protected internal readonly IHttpClientFactory clientFactory;

        protected internal string method;

        protected internal Uri url;

        protected internal object body;

        protected internal IAuthenticator authenticator;

        public event EventHandler<RemoteRequestEventArgs> WillComplete;

        public event EventHandler<RemoteRequestEventArgs> Complete;

        public event EventHandler<RemoteRequestEventArgs> HasCompleted;

        private int retryCount;

        private Database db;

        protected internal HttpRequestMessage request;

        protected internal IDictionary<string, object> requestHeaders;

        public RemoteRequest(TaskFactory workExecutor, IHttpClientFactory clientFactory, string method, Uri url, object body, Database db, IDictionary<string, object>requestHeaders)
        {
            this.clientFactory = clientFactory;
            this.method = method;
            this.url = url;
            this.body = body;
            this.workExecutor = workExecutor;
            this.requestHeaders = requestHeaders;
            this.db = db;
            this.request = CreateConcreteRequest();
            Log.V(Tag, "%s: RemoteRequest created, url: %s", this, url);
        }

        public void Run()
        {
            Log.V(Tag, "{0}: RemoteRequest run() called, url: {1}".Fmt(this, url));

            var httpClient = clientFactory.GetHttpClient();
            
            //var manager = httpClient.GetConnectionManager();
            PreemptivelySetAuthCredentials(httpClient);

            request.Headers.Add("Accept", "multipart/related, application/json");           
            AddRequestHeaders(request);
            
            SetBody(request);
            
            ExecuteRequest(httpClient, request);
            
            Log.V(Tag, "{0}: RemoteRequest run() finished, url: {1}".Fmt(this, url));
        }

        public virtual void Abort()
        {
            if (request != null)
            {
                request.Abort();
            }
            else
            {
                Log.W(Log.TagRemoteRequest, "%s: Unable to abort request since underlying request is null"
                    , this);
            }
        }

        public virtual HttpRequestMessage GetRequest()
        {
            return request;
        }

        protected internal void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in requestHeaders.Keys)
            {
                request.Headers.Add(requestHeaderKey, requestHeaders.Get(requestHeaderKey).ToString());
            }
        }

        public void OnEvent(EventHandler<RemoteRequestEventArgs> evt, Object result, Exception error)
        {
            if (evt == null)
                return;
            var args = new RemoteRequestEventArgs(result, error);
            evt(this, args);
        }

        protected HttpRequestMessage CreateConcreteRequest()
        {
            var httpMethod = new HttpMethod(method);
            var newRequest = new HttpRequestMessage(httpMethod, url.AbsoluteUri);;
            return newRequest;
        }

        protected internal void SetBody(HttpRequestMessage request)
        {
            // set body if appropriate
            if (body != null && request.Content is HttpContent)//HttpEntityEnclosingRequestBase)
            {
                byte[] bodyBytes = null;
                try
                {
                    bodyBytes = Manager.GetObjectMapper().WriteValueAsBytes(body);
                }
                catch (Exception e)
                {
                    Log.E(Log.TagRemoteRequest, "Error serializing body of request", e);
                }
                ByteArrayEntity entity = new ByteArrayEntity(bodyBytes);
                entity.SetContentType("application/json");
                ((HttpEntityEnclosingRequestBase)request).SetEntity(entity);
            }
        }

        /// <summary>Set IAuthenticator for BASIC Authentication</summary>
        public void SetAuthenticator(IAuthenticator authenticator)
        {
            this.authenticator = authenticator;
        }

        /// <summary>
        /// Retry this remote request, unless we've already retried MAX_RETRIES times
        /// NOTE: This assumes all requests are idempotent, since even though we got an error back, the
        /// request might have succeeded on the remote server, and by retrying we'd be issuing it again.
        /// </summary>
        /// <remarks>
        /// Retry this remote request, unless we've already retried MAX_RETRIES times
        /// NOTE: This assumes all requests are idempotent, since even though we got an error back, the
        /// request might have succeeded on the remote server, and by retrying we'd be issuing it again.
        /// PUT and POST requests aren't generally idempotent, but the ones sent by the replicator are.
        /// </remarks>
        /// <returns>true if going to retry the request, false otherwise</returns>
        protected internal bool RetryRequest()
        {
            if (retryCount >= MaxRetries)
            {
                return false;
            }
            workExecutor.Schedule(this, RetryDelayMs, TimeUnit.Milliseconds);
            retryCount += 1;
            Log.D(Log.TagRemoteRequest, "Will retry in %d ms", RetryDelayMs);
            return true;
        }

        protected internal void ExecuteRequest(HttpClient httpClient, HttpRequestMessage req)
        {
            object fullBody = null;
            Exception error = null;
            HttpWebResponse response = null;
            try
            {
                Log.V(Tag, "{0}: RemoteRequest executeRequest() called, url: {1}".Fmt(this, url));
                if (request.IsAborted)
                {
                    Log.V(Tag, "%s: RemoteRequest has already been aborted", this);
                    RespondWithResult(fullBody, new Exception(string.Format("%s: Request %s has been aborted"
                        , this, request)), response);
                    return;
                }
                Log.V(Tag, "%s: RemoteRequest calling httpClient.execute", this);
                response = httpClient.Execute(request);
                Log.V(Tag, "%s: RemoteRequest called httpClient.execute", this);
                // add in cookies to global store
                try
                {
                    if (httpClient is DefaultHttpClient)
                    {
                        DefaultHttpClient defaultHttpClient = (DefaultHttpClient)httpClient;
                        this.clientFactory.AddCookies(defaultHttpClient.GetCookieStore().GetCookies());
                    }
                }
                catch (Exception e)
                {
                    Log.E(Log.TagRemoteRequest, "Unable to add in cookies to global store", e);
                }
                StatusLine status = response.GetStatusLine();
                if (Utils.IsTransientError(status) && RetryRequest())
                {
                    return;
                }
                if (status.GetStatusCode() >= 300)
                {
                    Log.E(Log.TagRemoteRequest, "Got error status: %d for %s.  Reason: %s", status.GetStatusCode
                        (), request, status.GetReasonPhrase());
                    error = new HttpResponseException(status.GetStatusCode(), status.GetReasonPhrase(
                        ));
                }
                else
                {
                    HttpEntity temp = response.GetEntity();
                    if (temp != null)
                    {
                        InputStream stream = null;
                        try
                        {
                            stream = temp.GetContent();
                            fullBody = Manager.GetObjectMapper().ReadValue<object>(stream);
                        }
                        finally
                        {
                            try
                            {
                                stream.Close();
                            }
                            catch (IOException)
                            {
                            }
                        }
                    }
                }
            }
            catch (IOException e)
            {
                Log.E(Log.TagRemoteRequest, "io exception", e);
                error = e;
                // Treat all IOExceptions as transient, per:
                // http://hc.apache.org/httpclient-3.x/exception-handling.html
                Log.V(Tag, "%s: RemoteRequest calling retryRequest()", this);
                if (RetryRequest())
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Log.E(Log.TagRemoteRequest, "%s: executeRequest() Exception: ", e, this);
                error = e;
            }
            Log.V(Tag, "%s: RemoteRequest calling respondWithResult.  error: %s", this
                , error);
            RespondWithResult(fullBody, error, response);
        }

        protected internal void PreemptivelySetAuthCredentials(HttpClient httpClient)
        {
            var isUrlBasedUserInfo = false;
            var userInfo = url.UserInfo;
            if (userInfo != null)
            {
                isUrlBasedUserInfo = true;
            }
            else
            {
                if (authenticator != null)
                {
                    var auth = authenticator;
                    userInfo = auth.UserInfo;
                }
            }
            if (userInfo != null)
            {
                if (userInfo.Contains(":") && !userInfo.Trim().Equals(":"))
                {
                    var userInfoElements = userInfo.Split(":");
                    var username = isUrlBasedUserInfo ? URIUtils.Decode(userInfoElements[0]) : userInfoElements[0];
                    var password = isUrlBasedUserInfo ? URIUtils.Decode(userInfoElements[1]) : userInfoElements[1];
                    var authHandler = clientFactory.Handler.InnerHandler as HttpClientHandler;
                    authHandler.Credentials = new NetworkCredential(username, password);
                }
                else
                {
                    Log.W(Tag, "RemoteRequest Unable to parse user info, not setting credentials");
                }
            }
        }

        public void RespondWithResult(object result, Exception error, HttpWebResponse response)
        {
            if (workExecutor != null)
            {
                workExecutor.StartNew(()=>
                {
                    try
                    {
                        OnCompletion(WillComplete, response, error);
                        OnCompletion(Complete, result, error);
                        OnCompletion(HasCompleted, response, error);
                    }
                    catch (Exception e)
                    {
                        Log.E(Tag, "RemoteRequestCompletionBlock throw Exception", e);
                    }
                });
            }
            else
            {
                // don't let this crash the thread
                Log.E(Tag, "Work executor was null!");
            }
        }
    }
}
