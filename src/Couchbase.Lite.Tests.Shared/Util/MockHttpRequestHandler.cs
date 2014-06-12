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
            capturedRequests = new List<HttpRequestMessage>();
            AddDefaultResponders();
        }

        #endregion

        #region Instance Members

        public Int32 ResponseDelayMilliseconds { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<HttpResponseMessage>(() => 
            {
                Delay();

                capturedRequests.Add(request);

                foreach(var urlPattern in responders.Keys)
                {
                    if (urlPattern.Equals("*") || request.RequestUri.PathAndQuery.Contains(urlPattern))
                    {
                        HttpResponseDelegate responder = responders[urlPattern];
                        HttpResponseMessage message = responder(request);
                        NotifyResponseListeners(request, message);
                        return message;
                    }
                }

                throw new Exception("No responders matched for url pattern: " + request.RequestUri.PathAndQuery);
            });
        }

        public void SetResponder(string urlPattern, HttpResponseDelegate responder)
        {
            responders[urlPattern] = responder;
        }

        public IList<HttpRequestMessage> GetCapturedRequests() 
        {
            var snapshot = new List<HttpRequestMessage>(capturedRequests);
            return snapshot;
        }

        public void ClearCapturedRequests() {
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
                throw new IOException("Test IOException");
            };
            SetResponder("*", responder);
        }

        public void AddResponderFakeLocalDocumentUpdate404()
        {
            var json = "{\"error\":\"not_found\",\"reason\":\"missing\"}";
            HttpResponseDelegate responder = (request) => GenerateHttpResponseMessage(HttpStatusCode.NotFound, json);
            SetResponder("_local", responder);
        }

        public void AddResponderReturnInvalidChangesFeedJson() 
        {
            var json = "{\"results\":[";
            HttpResponseDelegate responder = (request) => GenerateHttpResponseMessage(HttpStatusCode.Accepted, json);
            SetResponder("_changes", responder);
        }

        public void AddResponderReturnEmptyChangesFeed() 
        {
            String json = "{\"results\":[]}";
            HttpResponseDelegate responder = (request) => GenerateHttpResponseMessage(HttpStatusCode.Accepted, json);
            SetResponder("_changes", responder);
        }

        public void clearResponders() {
            responders.Clear();
        }

        #endregion

        #region Non-Public Instance Members

        private IDictionary <string, HttpResponseDelegate> responders;

        private IList <HttpRequestMessage> capturedRequests;

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

        private static HttpResponseMessage GenerateHttpResponseMessage(IDictionary<string, object> content)
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

        private static HttpResponseMessage GenerateHttpResponseMessage(IList<IDictionary<string, object>> content)
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

        public static HttpResponseMessage GenerateHttpResponseMessage(HttpStatusCode statusCode, string responseJson)
        {
            var message = new HttpResponseMessage(statusCode);
            message.Content = new StringContent(responseJson);
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

        public static HttpResponseMessage FakeLocalDocumentUpdateIOException(HttpRequestMessage httpUriRequest) {
            throw new IOException("Throw exception on purpose for purposes of testSaveRemoteCheckpointNoResponse()");
        }

        #endregion
    }
}
