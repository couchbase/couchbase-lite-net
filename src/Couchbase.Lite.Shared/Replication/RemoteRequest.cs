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
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Linq;
using System.Net.Http.Headers;

namespace Couchbase.Lite.Replicator
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

        protected internal IAuthenticator Authenticator { get; set; }

//        public event EventHandler<RemoteRequestEventArgs> WillComplete;

        public event EventHandler<RemoteRequestEventArgs> Complete;

//        public event EventHandler<RemoteRequestEventArgs> HasCompleted;

        private int retryCount;

        protected Database db;

        protected internal HttpRequestMessage requestMessage;

        protected internal IDictionary<string, object> requestHeaders;

        protected CancellationTokenSource _tokenSource;

        protected Task request;

        public RemoteRequest(TaskFactory workExecutor, IHttpClientFactory clientFactory, string method, Uri url, object body, Database db, IDictionary<string, object>requestHeaders, CancellationTokenSource tokenSource = null)
        {
            this.clientFactory = clientFactory;
            this.method = method;
            this.url = url;
            this.body = body;
            this.workExecutor = workExecutor;
            this.requestHeaders = requestHeaders;
            this.db = db;
            this.requestMessage = CreateConcreteRequest();
            _tokenSource = tokenSource == null 
                ? new CancellationTokenSource() 
                : CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token);
            Log.V(Tag, "RemoteRequest created, url: {0}", url);
        }

        public virtual void Run()
        {
            Log.V(Tag, "{0}: RemoteRequest run() called, url: {1}".Fmt(this, url));

            HttpClient httpClient = null;
            try
            {
                httpClient = clientFactory.GetHttpClient();

                //var manager = httpClient.GetConnectionManager();
                var authHeader = AuthUtils.GetAuthenticationHeaderValue(Authenticator, requestMessage.RequestUri);
                if (authHeader != null)
                {
                    httpClient.DefaultRequestHeaders.Authorization = authHeader;
                }

                requestMessage.Headers.Add("Accept", "multipart/related, application/json");           
                AddRequestHeaders(requestMessage);

                SetBody(requestMessage);

                ExecuteRequest(httpClient, requestMessage);

                Log.V(Tag, "{0}: RemoteRequest run() finished, url: {1}".Fmt(this, url));
            }
            finally
            {
                if (httpClient != null)
                {
                    httpClient.Dispose();
                }
            }
        }

        public virtual void Abort()
        {
            if (requestMessage != null)
            {
                _tokenSource.Cancel();
            }
            else
            {
                Log.W(Tag, "{0}: Unable to abort request since underlying request is null", this.ToString());
            }
        }

        public virtual HttpRequestMessage GetRequest()
        {
            return requestMessage;
        }

        protected internal void AddRequestHeaders(HttpRequestMessage request)
        {
            foreach (string requestHeaderKey in requestHeaders.Keys)
            {
                request.Headers.Add(requestHeaderKey, requestHeaders[requestHeaderKey].ToString());
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
            var newRequest = new HttpRequestMessage(httpMethod, url.AbsoluteUri);
            return newRequest;
        }

        protected internal void SetBody(HttpRequestMessage request)
        {
            // set body if appropriate
            if (body != null)
            {
                byte[] bodyBytes = null;
                try
                {
                    bodyBytes = Manager.GetObjectMapper().WriteValueAsBytes(body).ToArray();
                }
                catch (Exception e)
                {
                    Log.E(Tag, "Error serializing body of request", e);
                }
                var entity = new ByteArrayContent(bodyBytes);
                entity.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Content = entity;
            }
            else
            {
                Log.W(Tag + ".SetBody", "No body found for this request to {0}", request.RequestUri);
            }
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
            request.ContinueWith((t)=> Task.Delay(RetryDelayMs, _tokenSource.Token), _tokenSource.Token, TaskContinuationOptions.AttachedToParent, workExecutor.Scheduler)
                .ContinueWith((t)=> Run(), _tokenSource.Token, TaskContinuationOptions.LongRunning, workExecutor.Scheduler);
            retryCount += 1;
            Log.D(Tag, "Will retry in {0} ms", RetryDelayMs);
            return true;
        }

        protected internal virtual void ExecuteRequest(HttpClient httpClient, HttpRequestMessage req)
        {
            object fullBody = null;
            Exception error = null;
            HttpResponseMessage response = null;
            try
            {
                Log.V(Tag, "{0}: RemoteRequest executeRequest() called, url: {1}".Fmt(this, url));
                if (request.IsCanceled)
                {
                    Log.V(Tag, "RemoteRequest has already been aborted");
                    RespondWithResult(fullBody, new Exception(string.Format("{0}: Request {1} has been aborted", this, requestMessage)), response);
                    return;
                }
                Log.V(Tag, "{0}: RemoteRequest calling httpClient.execute", this);
                response = httpClient.SendAsync(requestMessage, _tokenSource.Token).Result;
                Log.V(Tag, "{0}: RemoteRequest called httpClient.execute", this);
                var status = response.StatusCode;
                if (Misc.IsTransientError(status) && RetryRequest())
                {
                    return;
                }
                if ((int)status.GetStatusCode() >= 300)
                {
                    Log.E(Tag, "Got error status: {0} for {1}.  Reason: {2}", status.GetStatusCode(), requestMessage, response.ReasonPhrase);
                    error = new HttpResponseException(status);
                }
                else
                {
                    var temp = response.Content;
                    if (temp != null)
                    {
                        Stream stream = null;
                        try
                        {
                            stream = temp.ReadAsStreamAsync().Result;
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
                Log.E(Tag, "io exception", e);
                error = e;
                // Treat all IOExceptions as transient, per:
                // http://hc.apache.org/httpclient-3.x/exception-handling.html
                Log.V(Tag, "RemoteRequest calling RetryRequest()", this);
                if (RetryRequest())
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Log.E(Tag, "ExecuteRequest() Exception", e);
                error = e;
            }
            Log.V(Tag, "RemoteRequest calling respondWithResult.", error);
            RespondWithResult(fullBody, error, response);
        }

        public void RespondWithResult(object result, Exception error, HttpResponseMessage response)
        {
            Log.D(Tag + ".RespondWithREsult", "Firing Completed event.");
            OnEvent(Complete, result, error);
        }
    }
}
