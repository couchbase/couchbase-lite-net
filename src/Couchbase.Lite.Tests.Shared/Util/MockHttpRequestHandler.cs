//
// MockHttpRequestHandler.cs
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
using System.Net.Http;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Couchbase.Lite.Tests
{
    public class MockHttpRequestHandler : HttpClientHandler
    {
        #region Global Delegates

        public delegate HttpResponseMessage HttpResponseDelegate(HttpRequestMessage request);

        #endregion

        #region Constructors

        public MockHttpRequestHandler()
        {
            responders = new Dictionary<string, HttpResponseDelegate>();
            CapturedRequests = new List<HttpRequestMessage>();
            AddDefaultResponders();
        }

        #endregion

        #region Instance Members

        public Int32 ResponseDelayMilliseconds { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Delay();
            var requestDeepCopy = CopyRequest(request);
            capturedRequests.Add(requestDeepCopy);

            HttpResponseDelegate responder;
            if(!responders.TryGetValue("*", out responder)) {
                foreach(var urlPattern in responders.Keys) {
                    if (request.RequestUri.PathAndQuery.Contains(urlPattern)) {
                        responder = responders[urlPattern];
                        break;
                    }
                }
            }

            if (responder != null) {
                HttpResponseMessage message = responder(request);
                Task<HttpResponseMessage> retVal = Task.FromResult<HttpResponseMessage>(message);
                NotifyResponseListeners(request, message);
                return retVal;
            } else {
                throw new Exception("No responders matched for url pattern: " + request.RequestUri.PathAndQuery);
            }
        }

        public void SetResponder(string urlPattern, HttpResponseDelegate responder)
        {
            responders[urlPattern] = responder;
        }

        public void ClearCapturedRequests() 
        {
            capturedRequests.Clear();
        }

        public void AddDefaultResponders()
        {
            AddResponderFakeBulkDocs();
            AddResponderRevDiffsAllMissing();
            AddResponderFakeLocalDocumentUpdateIOException();
        }

        public void AddResponderFakeBulkDocs()
        {
            HttpResponseDelegate responder = (request) => FakeBulkDocs(request);
            SetResponder("_bulk_docs", responder);
        }

        public void AddResponderRevDiffsAllMissing()
        {
            HttpResponseDelegate responder = (request) => FakeRevsDiff(request);
            SetResponder("_revs_diff", responder);
        }

        public void AddResponderFakeLocalDocumentUpdateIOException()
        {
            HttpResponseDelegate responder = (request) => FakeLocalDocumentUpdateIOException(request);
            SetResponder("_local", responder);
        }

        public void AddResponderFailAllRequests(HttpStatusCode statusCode) 
        {
            HttpResponseDelegate responder = (request) => new HttpResponseMessage(statusCode);
            SetResponder("*", responder);
        }

        public void AddResponderThrowExceptionAllRequests() 
        {
            HttpResponseDelegate responder = (request) => {
                throw new WebException("Test WebException", WebExceptionStatus.UnknownError);
            };
            SetResponder("*", responder);
        }

        public void AddResponderFakeLocalDocumentUpdate404()
        {
            var json = "{\"error\":\"not_found\",\"reason\":\"missing\"}";
            HttpResponseDelegate responder = (request) => GenerateHttpResponseMessage(HttpStatusCode.NotFound, null, json);
            SetResponder("_local", responder);
        }

        public void AddResponderReturnInvalidChangesFeedJson() 
        {
            var json = "{\"results\":[";
            HttpResponseDelegate responder = (request) => GenerateHttpResponseMessage(HttpStatusCode.Accepted, null, json);
            SetResponder("_changes", responder);
        }

        public void AddResponderReturnEmptyChangesFeed() 
        {
            var json = "{\"results\":[]}";
            HttpResponseDelegate responder = (request) => GenerateHttpResponseMessage(HttpStatusCode.Accepted, null, json);
            SetResponder("_changes", responder);
        }

        public void ClearResponders() {
            responders.Clear();
        }

        #endregion

        #region Non-Public Instance Members

        private IDictionary <string, HttpResponseDelegate> responders;

        private IList <HttpRequestMessage> capturedRequests;

        internal IList<HttpRequestMessage> CapturedRequests
        {
            private set { 
                capturedRequests = value; 
            }
            get {
                var snapshot = new List<HttpRequestMessage>(capturedRequests);
                return snapshot;
            }
        }

        private HttpRequestMessage CopyRequest(HttpRequestMessage request)
        {
            //The Windows version of HttpClient uncontrollably disposes the
            //HttpContent of an HttpRequestMessage once the message is sent
            //so we need to make a copy of it to store in the capturedRequests 
            //collection
            var retVal = new HttpRequestMessage(request.Method, request.RequestUri) {
                Version = request.Version
            };

            foreach (var header in request.Headers) {
                retVal.Headers.Add(header.Key, header.Value);
            }

            //Expand as needed
            if (request.Content is ByteArrayContent) {
                byte[] data = request.Content.ReadAsByteArrayAsync().Result;
                retVal.Content = new ByteArrayContent(data);
            } else {
                retVal.Content = request.Content;
            }

            return retVal;
        }

        private void Delay()
        {
            if (ResponseDelayMilliseconds > 0)
            {
                Thread.Sleep(ResponseDelayMilliseconds);
            }
        }

        private void NotifyResponseListeners(HttpRequestMessage request, HttpResponseMessage response)
        {
            // TODO:
        }

        #endregion

        #region Static Members

        public static IDictionary<string, object> GetJsonMapFromRequest(HttpRequestMessage request)
        {
            var bytesTask = request.Content.ReadAsByteArrayAsync();
            bytesTask.Wait(TimeSpan.FromSeconds(3));
            var value = Manager.GetObjectMapper().ReadValue<object>(bytesTask.Result);

            IDictionary<string, object> jsonMap = null;
            if (value is JObject)
            {
                jsonMap = ((JObject)value).ToObject<IDictionary<string, object>>();
            }
            else
            {
                jsonMap = (IDictionary<string, object>)value;
            }

            return jsonMap;
        }

        public static HttpResponseMessage GenerateHttpResponseMessage(IDictionary<string, object> content)
        {
            var message = new HttpResponseMessage(HttpStatusCode.OK);

            if (content != null)
            {
                var bytes = Manager.GetObjectMapper().WriteValueAsBytes<IDictionary<string, object>>(content).ToArray();
                var byteContent = new ByteArrayContent(bytes);
                message.Content = byteContent;
            }

            return message;
        }

        public static HttpResponseMessage GenerateHttpResponseMessage(IList<IDictionary<string, object>> content)
        {
            var message = new HttpResponseMessage(HttpStatusCode.OK);

            if (content != null)
            {
                var bytes = Manager.GetObjectMapper().WriteValueAsBytes(content).ToArray();
                var byteContent = new ByteArrayContent(bytes);
                message.Content = byteContent;
            }

            return message;
        }

        public static HttpResponseMessage GenerateHttpResponseMessage(HttpStatusCode statusCode, string statusMesg, string responseJson)
        {
            var message = new HttpResponseMessage(statusCode);

            if (statusMesg != null)
            {
                message.ReasonPhrase = statusMesg;
            }

            if (responseJson != null)
            {
                message.Content = new StringContent(responseJson);
            }

            return message;
        }

        public static HttpResponseMessage FakeBulkDocs(HttpRequestMessage request)
        {
            var jsonMap = GetJsonMapFromRequest(request);
            var responseList = new List<IDictionary<string, object>>();

            var docs = ((JArray)jsonMap["docs"]).ToList();

            foreach (JObject doc in docs)
            {
                IDictionary<string, object> responseListItem = new Dictionary<string, object>();
                responseListItem["id"] = doc["_id"];
                responseListItem["rev"] = doc["_rev"];
                responseList.Add(responseListItem);
            }

            var response = GenerateHttpResponseMessage(responseList);
            return response;
        }

        public static HttpResponseDelegate TransientErrorResponder(Int32 statusCode, string statusMesg)
        {
            HttpResponseDelegate responder = (request) =>
            {
                if (statusCode == -1)
                {
                    throw new IOException("Fake IO Exception from TransientErrorResponder");
                }
                return GenerateHttpResponseMessage((HttpStatusCode)statusCode, statusMesg, null);
            };

            return responder;
        }

        /// <summary>
        /// Transform Request JSON:
        /// {
        ///     "doc2-1384988871931": ["1-b52e6d59-4151-4802-92fb-7e34ceff1e92"],
        ///     "doc1-1384988871931": ["2-e776a593-6b61-44ee-b51a-0bdf205c9e13"]
        /// }
        /// 
        /// Into Response JSON:
        /// {
        ///     "doc1-1384988871931": {
        ///         "missing": ["2-e776a593-6b61-44ee-b51a-0bdf205c9e13"]
        ///     },
        ///     "doc2-1384988871931": {
        ///         "missing": ["1-b52e6d59-4151-4802-92fb-7e34ceff1e92"]
        ///     }
        /// }
        /// </summary>
        /// <returns>The revs diff.</returns>
        /// <param name="httpUriRequest">Http URI request.</param>
        public static HttpResponseMessage FakeRevsDiff(HttpRequestMessage request)
        {
            IDictionary<string, object> jsonMap = GetJsonMapFromRequest(request);
            IDictionary<string, object> responseMap = new Dictionary<string, object>();
            foreach (string key in jsonMap.Keys)
            {
                IDictionary<string, object> missingMap = new Dictionary<string, object>();
                missingMap["missing"] = jsonMap[key];
                responseMap[key] = missingMap;
            }

            var response = GenerateHttpResponseMessage(responseMap);
            return response;
        }

        public static HttpResponseMessage FakeLocalDocumentUpdateIOException(HttpRequestMessage httpUriRequest)
        {
            throw new IOException("Throw exception on purpose for purposes of testSaveRemoteCheckpointNoResponse()");
        }

        #endregion
    }
}
