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
using Apache.Http.Client;
using Apache.Http.Impl.Client;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
    public class RemoteMultipartDownloaderRequest : RemoteRequest
    {
        private Database db;

        public RemoteMultipartDownloaderRequest(ScheduledExecutorService workExecutor, HttpClientFactory
             clientFactory, string method, Uri url, object body, Database db, IDictionary<string
            , object> requestHeaders, RemoteRequestCompletionBlock onCompletion) : base(workExecutor
            , clientFactory, method, url, body, db, requestHeaders, onCompletion)
        {
            this.db = db;
        }

        public override void Run()
        {
            HttpClient httpClient = clientFactory.GetHttpClient();
            PreemptivelySetAuthCredentials(httpClient);
            request.AddHeader("Accept", "*/*");
            AddRequestHeaders(request);
            ExecuteRequest(httpClient, request);
        }

        protected internal override void ExecuteRequest(HttpClient httpClient, HttpRequestMessage
             request)
        {
            object fullBody = null;
            Exception error = null;
            HttpResponse response = null;
            try
            {
                if (request.IsAborted())
                {
                    RespondWithResult(fullBody, new Exception(string.Format("%s: Request %s has been aborted"
                        , this, request)), response);
                    return;
                }
                response = httpClient.Execute(request);
                try
                {
                    // add in cookies to global store
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
                if (status.GetStatusCode() >= 300)
                {
                    Log.E(Log.TagRemoteRequest, "Got error status: %d for %s.  Reason: %s", status.GetStatusCode
                        (), request, status.GetReasonPhrase());
                    error = new HttpResponseException(status.GetStatusCode(), status.GetReasonPhrase(
                        ));
                }
                else
                {
                    HttpEntity entity = response.GetEntity();
                    Header contentTypeHeader = entity.GetContentType();
                    InputStream inputStream = null;
                    if (contentTypeHeader != null && contentTypeHeader.GetValue().Contains("multipart/related"
                        ))
                    {
                        try
                        {
                            MultipartDocumentReader reader = new MultipartDocumentReader(response, db);
                            reader.SetContentType(contentTypeHeader.GetValue());
                            inputStream = entity.GetContent();
                            int bufLen = 1024;
                            byte[] buffer = new byte[bufLen];
                            int numBytesRead = 0;
                            while ((numBytesRead = inputStream.Read(buffer)) != -1)
                            {
                                if (numBytesRead != bufLen)
                                {
                                    byte[] bufferToAppend = Arrays.CopyOfRange(buffer, 0, numBytesRead);
                                    reader.AppendData(bufferToAppend);
                                }
                                else
                                {
                                    reader.AppendData(buffer);
                                }
                            }
                            reader.Finish();
                            fullBody = reader.GetDocumentProperties();
                            RespondWithResult(fullBody, error, response);
                        }
                        finally
                        {
                            try
                            {
                                inputStream.Close();
                            }
                            catch (IOException)
                            {
                            }
                        }
                    }
                    else
                    {
                        if (entity != null)
                        {
                            try
                            {
                                inputStream = entity.GetContent();
                                fullBody = Manager.GetObjectMapper().ReadValue<object>(inputStream);
                                RespondWithResult(fullBody, error, response);
                            }
                            finally
                            {
                                try
                                {
                                    inputStream.Close();
                                }
                                catch (IOException)
                                {
                                }
                            }
                        }
                    }
                }
            }
            catch (IOException e)
            {
                Log.E(Log.TagRemoteRequest, "%s: io exception", e, this);
                error = e;
                RespondWithResult(fullBody, e, response);
            }
            catch (Exception e)
            {
                Log.E(Log.TagRemoteRequest, "%s: executeRequest() Exception: ", e, this);
                error = e;
                RespondWithResult(fullBody, e, response);
            }
            finally
            {
                Log.E(Log.TagRemoteRequest, "%s: executeRequest() finally", this);
            }
        }
    }
}
