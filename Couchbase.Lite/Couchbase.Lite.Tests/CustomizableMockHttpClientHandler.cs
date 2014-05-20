//
// CustomizableMockHttpClient.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Couchbase.Lite;
using Couchbase.Lite.Replicator;
using Sharpen;
using System.Net.Http;
using Couchbase.Lite.Support;
using System.Runtime.Serialization;
using System;
using System.Threading.Tasks;

namespace Couchbase.Lite.Replicator
{
	public class CustomizableMockHttpClientFactory : IHttpClientFactory
	{
		public IDictionary<string, string> Headers 
		{
			get { return handler.Headers; }
			set { handler.Headers = value; }
		}

		public CustomizableMockHttpClientFactory()
		{
			handler = new CustomizableMockHttpClientHandler();
			Headers = new Dictionary<string,string>();
		}

		#region IHttpClientFactory implementation
		HttpClient client;
		CustomizableMockHttpClientHandler handler;

		public HttpClient GetHttpClient ()
		{
			return client = new HttpClient(HttpHandler);
		}

		public HttpClientHandler HttpHandler {
			get {
				return handler;
			}
		}

		#endregion


	}
	public class CustomizableMockHttpClientHandler : HttpClientHandler
	{
		private IDictionary<string, CustomizableMockHttpClientHandler.Responder> responders;

		private IList<HttpRequestMessage> capturedRequests = Collections.SynchronizedList(new AList<HttpRequestMessage>());

        public CustomizableMockHttpClientHandler()
		{
			// tests can register custom responders per url.  the key is the URL pattern to match,
			// the value is the responder that should handle that request.
			// capture all request so that the test can verify expected requests were received.
			responders = new Dictionary<string, CustomizableMockHttpClientHandler.Responder>();
			AddDefaultResponders();
		}

		internal void SetResponder(string urlPattern, CustomizableMockHttpClientHandler.Responder responder)
		{
			responders[urlPattern] = responder;
		}

		public void AddDefaultResponders()
		{
			responders.Put("_revs_diff", new _Responder_49());
			responders.Put("_bulk_docs", new _Responder_56());
			responders.Put("_local", new _Responder_63());
		}

		private sealed class _Responder_49 : CustomizableMockHttpClientHandler.Responder
		{
			public _Responder_49()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponseMessage Execute(HttpRequestMessage httpUriRequest)
			{
				return CustomizableMockHttpClientHandler.FakeRevsDiff(httpUriRequest
					);
			}
		}

		private sealed class _Responder_56 : CustomizableMockHttpClientHandler.Responder
		{
			public _Responder_56()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponseMessage Execute(HttpRequestMessage httpUriRequest)
			{
				return CustomizableMockHttpClientHandler.FakeBulkDocs(httpUriRequest
					);
			}
		}

		private sealed class _Responder_63 : CustomizableMockHttpClientHandler.Responder
		{
			public _Responder_63()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponseMessage Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClientHandler.FakeLocalDocumentUpdate
					(httpUriRequest);
			}
		}

		public virtual void AddResponderFailAllRequests(int statusCode)
		{
			SetResponder("*", new _Responder_73(statusCode));
		}

		private sealed class _Responder_73 : CustomizableMockHttpClientHandler.Responder
		{
			public _Responder_73(int statusCode)
			{
				this.statusCode = statusCode;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponseMessage Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClientHandler.EmptyResponseWithStatusCode
					(statusCode);
			}

			private readonly int statusCode;
		}

		public virtual void AddResponderThrowExceptionAllRequests()
		{
			SetResponder("*", new _Responder_82());
		}

		private sealed class _Responder_82 : CustomizableMockHttpClientHandler.Responder
		{
			public _Responder_82()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponseMessage Execute(HttpRequestMessage httpUriRequest)
			{
				throw new IOException("Test IOException");
			}
		}

		public virtual IList<HttpRequestMessage> GetCapturedRequests()
		{
			return capturedRequests;
		}

		public IDictionary<string,string> Headers { get; set; }

		/// <exception cref="System.IO.IOException"></exception>
		public virtual HttpResponseMessage Execute(HttpRequestMessage httpUriRequest)
		{
			capturedRequests.AddItem(httpUriRequest);

			foreach (var header in Headers)
			{
				httpUriRequest.Headers.Add(header.Key, header.Value);
			}

			foreach (string urlPattern in responders.Keys)
			{
				if (urlPattern.Equals("*") || httpUriRequest.RequestUri.PathAndQuery.Contains(urlPattern))
				{
					var responder = responders[urlPattern];
					return responder.Execute(httpUriRequest);
				}
			}
			throw new RuntimeException("No responders matched for url pattern: " + httpUriRequest.RequestUri.PathAndQuery);
		}

		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
		{
			return Task.FromResult(Execute(request));
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Apache.Http.Client.ClientProtocolException"></exception>
		public static HttpResponseMessage FakeLocalDocumentUpdate(HttpRequestMessage httpUriRequest
			)
		{
			throw new IOException("Throw exception on purpose for purposes of testSaveRemoteCheckpointNoResponse()");
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Apache.Http.Client.ClientProtocolException"></exception>
		public static HttpResponseMessage FakeBulkDocs(HttpRequestMessage httpUriRequest)
		{
			var jsonMap = GetJsonMapFromRequest(httpUriRequest);
			var responseList = new AList<IDictionary<string, object>>();
			var docs = (ArrayList)jsonMap["docs"];
			foreach (IDictionary<string, object> doc in docs)
			{
				IDictionary<string, object> responseListItem = new Dictionary<string, object>();
				responseListItem.Put("id", doc["_id"]);
				responseListItem.Put("rev", doc["_rev"]);
				responseList.AddItem(responseListItem);
			}
			var response = GenerateHttpWebResponseObject(responseList);
			return response;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static HttpResponseMessage FakeRevsDiff(HttpRequestMessage httpUriRequest)
		{
			IDictionary<string, object> jsonMap = GetJsonMapFromRequest(httpUriRequest);
			IDictionary<string, object> responseMap = new Dictionary<string, object>();
			foreach (string key in jsonMap.Keys)
			{
				ArrayList value = (ArrayList)jsonMap[key];
				IDictionary<string, object> missingMap = new Dictionary<string, object>();
				missingMap["missing"] = value;
				responseMap[key] = missingMap;
			}
			var response = GenerateHttpWebResponseObject(responseMap);
			return response;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static HttpResponseMessage GenerateHttpWebResponseObject(object o)
		{
			var response = new FakeHttpWebResponse();
			return response;
//			BasicStatusLine statusLine = new BasicStatusLine(HttpVersion.Http11, 200, "OK");
//			HttpWebResponse response = responseFactory.NewHttpWebResponse(statusLine, null);
//			byte[] responseBytes = Manager.GetObjectMapper().WriteValueAsBytes(o);
//			response.SetEntity(new ByteArrayEntity(responseBytes));
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static HttpResponseMessage GenerateHttpWebResponseObject(string responseJson)
		{
//			DefaultHttpWebResponseFactory responseFactory = new DefaultHttpWebResponseFactory();
//			BasicStatusLine statusLine = new BasicStatusLine(HttpVersion.Http11, 200, "OK");
//			HttpWebResponse response = responseFactory.NewHttpWebResponse(statusLine, null);
//			byte[] responseBytes = Sharpen.Runtime.GetBytesForString(responseJson);
//			response.SetEntity(new ByteArrayEntity(responseBytes));
//			return response;
			var response = new FakeHttpWebResponse();
			return response;
		}

		public static HttpResponseMessage  EmptyResponseWithStatusCode(int statusCode)
		{
//			DefaultHttpWebResponseFactory responseFactory = new DefaultHttpWebResponseFactory();
//			BasicStatusLine statusLine = new BasicStatusLine(HttpVersion.Http11, statusCode, 
//				string.Empty);
//			HttpWebResponse response = responseFactory.NewHttpWebResponse(statusLine, null);
//			return response;
			var response = new FakeHttpWebResponse();
			return response;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private static IDictionary<string, object> GetJsonMapFromRequest(HttpRequestMessage httpUriRequest)
		{
			var streamTask = httpUriRequest.Content.ReadAsStreamAsync();
			streamTask.Wait(TimeSpan.FromSeconds(5));
			return Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(streamTask.Result);
		}

		/// <exception cref="System.IO.IOException"></exception>
//		public virtual HttpWebResponse Execute(HttpRequestMessage httpUriRequest, HttpContext
//			 httpContext)
//		{
//			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
//				);
//		}
//
//		/// <exception cref="System.IO.IOException"></exception>
//		public virtual HttpWebResponse Execute(HttpHost httpHost, HttpWebRequest httpRequest
//			)
//		{
//			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
//				);
//		}
//
//		/// <exception cref="System.IO.IOException"></exception>
//		public virtual HttpWebResponse Execute(HttpHost httpHost, HttpWebRequest httpRequest
//			, HttpContext httpContext)
//		{
//			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
//				);
//		}
//
//		/// <exception cref="System.IO.IOException"></exception>
//		public virtual T Execute<T, _T1>(HttpRequestMessage httpUriRequest, ResponseHandler
//			<_T1> responseHandler) where _T1:T
//		{
//			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
//				);
//		}
//
//		/// <exception cref="System.IO.IOException"></exception>
//		public virtual T Execute<T, _T1>(HttpRequestMessage httpUriRequest, ResponseHandler
//			<_T1> responseHandler, HttpContext httpContext) where _T1:T
//		{
//			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
//				);
//		}
//
//		/// <exception cref="System.IO.IOException"></exception>
//		public virtual T Execute<T, _T1>(HttpHost httpHost, HttpWebRequest httpRequest, ResponseHandler
//			<_T1> responseHandler) where _T1:T
//		{
//			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
//				);
//		}
//
//		/// <exception cref="System.IO.IOException"></exception>
//		public virtual T Execute<T, _T1>(HttpHost httpHost, HttpWebRequest httpRequest, ResponseHandler
//			<_T1> responseHandler, HttpContext httpContext) where _T1:T
//		{
//			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
//				);
//		}

		internal interface Responder
		{
			/// <exception cref="System.IO.IOException"></exception>
			HttpResponseMessage Execute(HttpRequestMessage httpUriRequest);
		}
	}
}
