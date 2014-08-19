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
using Apache.Http;
using Apache.Http.Auth;
using Apache.Http.Client;
using Apache.Http.Client.Methods;
using Apache.Http.Client.Protocol;
using Apache.Http.Conn;
using Apache.Http.Entity;
using Apache.Http.Impl.Auth;
using Apache.Http.Impl.Client;
using Apache.Http.Protocol;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
    public class RemoteRequest : Runnable
    {
        private const int MaxRetries = 2;

        private const int RetryDelayMs = 10 * 1000;

        protected internal ScheduledExecutorService workExecutor;

        protected internal readonly HttpClientFactory clientFactory;

        protected internal string method;

        protected internal Uri url;

        protected internal object body;

        protected internal Authenticator authenticator;

        protected internal RemoteRequestCompletionBlock onPreCompletion;

        protected internal RemoteRequestCompletionBlock onCompletion;

        protected internal RemoteRequestCompletionBlock onPostCompletion;

        private int retryCount;

        private Database db;

        protected internal HttpRequestMessage request;

        protected internal IDictionary<string, object> requestHeaders;

        public RemoteRequest(ScheduledExecutorService workExecutor, HttpClientFactory clientFactory
            , string method, Uri url, object body, Database db, IDictionary<string, object> 
            requestHeaders, RemoteRequestCompletionBlock onCompletion)
        {
            this.clientFactory = clientFactory;
            this.method = method;
            this.url = url;
            this.body = body;
            this.onCompletion = onCompletion;
            this.workExecutor = workExecutor;
            this.requestHeaders = requestHeaders;
            this.db = db;
            this.request = CreateConcreteRequest();
            Log.V(Log.TagSync, "%s: RemoteRequest created, url: %s", this, url);
        }

        public virtual void Run()
        {
            Log.V(Log.TagSync, "%s: RemoteRequest run() called, url: %s", this, url);
            HttpClient httpClient = clientFactory.GetHttpClient();
            ClientConnectionManager manager = httpClient.GetConnectionManager();
            PreemptivelySetAuthCredentials(httpClient);
            request.AddHeader("Accept", "multipart/related, application/json");
            AddRequestHeaders(request);
            SetBody(request);
            ExecuteRequest(httpClient, request);
            Log.V(Log.TagSync, "%s: RemoteRequest run() finished, url: %s", this, url);
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

        protected internal virtual void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in requestHeaders.Keys)
            {
                request.AddHeader(requestHeaderKey, requestHeaders.Get(requestHeaderKey).ToString
                    ());
            }
        }

        public virtual void SetOnPostCompletion(RemoteRequestCompletionBlock onPostCompletion
            )
        {
            this.onPostCompletion = onPostCompletion;
        }

        public virtual void SetOnPreCompletion(RemoteRequestCompletionBlock onPreCompletion
            )
        {
            this.onPreCompletion = onPreCompletion;
        }

        protected internal virtual HttpRequestMessage CreateConcreteRequest()
        {
            HttpRequestMessage request = null;
            if (Sharpen.Runtime.EqualsIgnoreCase(method, "GET"))
            {
                request = new HttpGet(url.ToExternalForm());
            }
            else
            {
                if (Sharpen.Runtime.EqualsIgnoreCase(method, "PUT"))
                {
                    request = new HttpPut(url.ToExternalForm());
                }
                else
                {
                    if (Sharpen.Runtime.EqualsIgnoreCase(method, "POST"))
                    {
                        request = new HttpPost(url.ToExternalForm());
                    }
                }
            }
            return request;
        }

        protected internal virtual void SetBody(HttpRequestMessage request)
        {
            // set body if appropriate
            if (body != null && request is HttpEntityEnclosingRequestBase)
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

        /// <summary>Set Authenticator for BASIC Authentication</summary>
        public virtual void SetAuthenticator(Authenticator authenticator)
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
        protected internal virtual bool RetryRequest()
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

        protected internal virtual void ExecuteRequest(HttpClient httpClient, HttpRequestMessage
             request)
        {
            object fullBody = null;
            Exception error = null;
            HttpResponse response = null;
            try
            {
                Log.V(Log.TagSync, "%s: RemoteRequest executeRequest() called, url: %s", this, url
                    );
                if (request.IsAborted())
                {
                    Log.V(Log.TagSync, "%s: RemoteRequest has already been aborted", this);
                    RespondWithResult(fullBody, new Exception(string.Format("%s: Request %s has been aborted"
                        , this, request)), response);
                    return;
                }
                Log.V(Log.TagSync, "%s: RemoteRequest calling httpClient.execute", this);
                response = httpClient.Execute(request);
                Log.V(Log.TagSync, "%s: RemoteRequest called httpClient.execute", this);
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
                Log.V(Log.TagSync, "%s: RemoteRequest calling retryRequest()", this);
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
            Log.V(Log.TagSync, "%s: RemoteRequest calling respondWithResult.  error: %s", this
                , error);
            RespondWithResult(fullBody, error, response);
        }

        protected internal virtual void PreemptivelySetAuthCredentials(HttpClient httpClient
            )
        {
            bool isUrlBasedUserInfo = false;
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
                        MessageProcessingHandler preemptiveAuth = new _MessageProcessingHandler_286(credentials
                            );
                        dhc.AddRequestInterceptor(preemptiveAuth, 0);
                    }
                }
                else
                {
                    Log.W(Log.TagRemoteRequest, "RemoteRequest Unable to parse user info, not setting credentials"
                        );
                }
            }
        }

        private sealed class _MessageProcessingHandler_286 : MessageProcessingHandler
        {
            public _MessageProcessingHandler_286(Credentials credentials)
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

        public virtual void RespondWithResult(object result, Exception error, HttpResponse
             response)
        {
            if (workExecutor != null)
            {
                workExecutor.Submit(new _Runnable_307(this, response, error, result));
            }
            else
            {
                // don't let this crash the thread
                Log.E(Log.TagRemoteRequest, "Work executor was null!");
            }
        }

        private sealed class _Runnable_307 : Runnable
        {
            public _Runnable_307(RemoteRequest _enclosing, HttpResponse response, Exception error
                , object result)
            {
                this._enclosing = _enclosing;
                this.response = response;
                this.error = error;
                this.result = result;
            }

            public void Run()
            {
                try
                {
                    if (this._enclosing.onPreCompletion != null)
                    {
                        this._enclosing.onPreCompletion.OnCompletion(response, error);
                    }
                    this._enclosing.onCompletion.OnCompletion(result, error);
                    if (this._enclosing.onPostCompletion != null)
                    {
                        this._enclosing.onPostCompletion.OnCompletion(response, error);
                    }
                }
                catch (Exception e)
                {
                    Log.E(Log.TagRemoteRequest, "RemoteRequestCompletionBlock throw Exception", e);
                }
            }

            private readonly RemoteRequest _enclosing;

            private readonly HttpResponse response;

            private readonly Exception error;

            private readonly object result;
        }
    }
}
