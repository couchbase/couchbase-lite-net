/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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

namespace Couchbase.Lite.Replicator
{
    public class CustomizableMockHttpClient : HttpMessageInvoker
	{
		private IDictionary<string, CustomizableMockHttpClient.Responder> responders;

		private IList<HttpWebRequest> capturedRequests = Sharpen.Collections.SynchronizedList
			(new AList<HttpWebRequest>());

        public CustomizableMockHttpClient() : base(new HttpClientHandler())
		{
			// tests can register custom responders per url.  the key is the URL pattern to match,
			// the value is the responder that should handle that request.
			// capture all request so that the test can verify expected requests were received.
			responders = new Dictionary<string, CustomizableMockHttpClient.Responder>();
			AddDefaultResponders();
		}

		public virtual void SetResponder(string urlPattern, CustomizableMockHttpClient.Responder
			 responder)
		{
			responders[urlPattern] = responder;
		}

		public virtual void AddDefaultResponders()
		{
			responders.Put("_revs_diff", new _Responder_49());
			responders.Put("_bulk_docs", new _Responder_56());
			responders.Put("_local", new _Responder_63());
		}

		private sealed class _Responder_49 : CustomizableMockHttpClient.Responder
		{
			public _Responder_49()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.FakeRevsDiff(httpUriRequest
					);
			}
		}

		private sealed class _Responder_56 : CustomizableMockHttpClient.Responder
		{
			public _Responder_56()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.FakeBulkDocs(httpUriRequest
					);
			}
		}

		private sealed class _Responder_63 : CustomizableMockHttpClient.Responder
		{
			public _Responder_63()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.FakeLocalDocumentUpdate
					(httpUriRequest);
			}
		}

		public virtual void AddResponderFailAllRequests(int statusCode)
		{
			SetResponder("*", new _Responder_73(statusCode));
		}

		private sealed class _Responder_73 : CustomizableMockHttpClient.Responder
		{
			public _Responder_73(int statusCode)
			{
				this.statusCode = statusCode;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.EmptyResponseWithStatusCode
					(statusCode);
			}

			private readonly int statusCode;
		}

		public virtual void AddResponderThrowExceptionAllRequests()
		{
			SetResponder("*", new _Responder_82());
		}

		private sealed class _Responder_82 : CustomizableMockHttpClient.Responder
		{
			public _Responder_82()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				throw new IOException("Test IOException");
			}
		}

		public virtual IList<HttpWebRequest> GetCapturedRequests()
		{
			return capturedRequests;
		}

		public virtual HttpParams GetParams()
		{
			return null;
		}

		public virtual ClientConnectionManager GetConnectionManager()
		{
			return null;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual HttpResponse Execute(HttpRequestMessage httpUriRequest)
		{
			capturedRequests.AddItem(httpUriRequest);
			foreach (string urlPattern in responders.Keys)
			{
				if (urlPattern.Equals("*") || httpUriRequest.GetURI().GetPath().Contains(urlPattern
					))
				{
					CustomizableMockHttpClient.Responder responder = responders[urlPattern];
					return responder.Execute(httpUriRequest);
				}
			}
			throw new RuntimeException("No responders matched for url pattern: " + httpUriRequest
				.GetURI().GetPath());
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Apache.Http.Client.ClientProtocolException"></exception>
		public static HttpResponse FakeLocalDocumentUpdate(HttpRequestMessage httpUriRequest
			)
		{
			throw new IOException("Throw exception on purpose for purposes of testSaveRemoteCheckpointNoResponse()"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Apache.Http.Client.ClientProtocolException"></exception>
		public static HttpResponse FakeBulkDocs(HttpRequestMessage httpUriRequest)
		{
			IDictionary<string, object> jsonMap = GetJsonMapFromRequest((HttpPost)httpUriRequest
				);
			IList<IDictionary<string, object>> responseList = new AList<IDictionary<string, object
				>>();
			AList<IDictionary<string, object>> docs = (ArrayList)jsonMap["docs"];
			foreach (IDictionary<string, object> doc in docs)
			{
				IDictionary<string, object> responseListItem = new Dictionary<string, object>();
				responseListItem.Put("id", doc["_id"]);
				responseListItem.Put("rev", doc["_rev"]);
				responseList.AddItem(responseListItem);
			}
			HttpResponse response = GenerateHttpResponseObject(responseList);
			return response;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static HttpResponse FakeRevsDiff(HttpRequestMessage httpUriRequest)
		{
			IDictionary<string, object> jsonMap = GetJsonMapFromRequest((HttpPost)httpUriRequest
				);
			IDictionary<string, object> responseMap = new Dictionary<string, object>();
			foreach (string key in jsonMap.Keys)
			{
				ArrayList value = (ArrayList)jsonMap[key];
				IDictionary<string, object> missingMap = new Dictionary<string, object>();
				missingMap["missing"] = value;
				responseMap[key] = missingMap;
			}
			HttpResponse response = GenerateHttpResponseObject(responseMap);
			return response;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static HttpResponse GenerateHttpResponseObject(object o)
		{
			DefaultHttpResponseFactory responseFactory = new DefaultHttpResponseFactory();
			BasicStatusLine statusLine = new BasicStatusLine(HttpVersion.Http11, 200, "OK");
			HttpResponse response = responseFactory.NewHttpResponse(statusLine, null);
			byte[] responseBytes = Manager.GetObjectMapper().WriteValueAsBytes(o);
			response.SetEntity(new ByteArrayEntity(responseBytes));
			return response;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static HttpResponse GenerateHttpResponseObject(string responseJson)
		{
			DefaultHttpResponseFactory responseFactory = new DefaultHttpResponseFactory();
			BasicStatusLine statusLine = new BasicStatusLine(HttpVersion.Http11, 200, "OK");
			HttpResponse response = responseFactory.NewHttpResponse(statusLine, null);
			byte[] responseBytes = Sharpen.Runtime.GetBytesForString(responseJson);
			response.SetEntity(new ByteArrayEntity(responseBytes));
			return response;
		}

		public static HttpResponse EmptyResponseWithStatusCode(int statusCode)
		{
			DefaultHttpResponseFactory responseFactory = new DefaultHttpResponseFactory();
			BasicStatusLine statusLine = new BasicStatusLine(HttpVersion.Http11, statusCode, 
				string.Empty);
			HttpResponse response = responseFactory.NewHttpResponse(statusLine, null);
			return response;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private static IDictionary<string, object> GetJsonMapFromRequest(HttpPost httpUriRequest
			)
		{
			HttpPost post = (HttpPost)httpUriRequest;
			InputStream @is = post.GetEntity().GetContent();
			return Manager.GetObjectMapper().ReadValue<IDictionary>(@is);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual HttpResponse Execute(HttpRequestMessage httpUriRequest, HttpContext
			 httpContext)
		{
			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual HttpResponse Execute(HttpHost httpHost, HttpWebRequest httpRequest
			)
		{
			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual HttpResponse Execute(HttpHost httpHost, HttpWebRequest httpRequest
			, HttpContext httpContext)
		{
			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual T Execute<T, _T1>(HttpRequestMessage httpUriRequest, ResponseHandler
			<_T1> responseHandler) where _T1:T
		{
			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual T Execute<T, _T1>(HttpRequestMessage httpUriRequest, ResponseHandler
			<_T1> responseHandler, HttpContext httpContext) where _T1:T
		{
			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual T Execute<T, _T1>(HttpHost httpHost, HttpWebRequest httpRequest, ResponseHandler
			<_T1> responseHandler) where _T1:T
		{
			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual T Execute<T, _T1>(HttpHost httpHost, HttpWebRequest httpRequest, ResponseHandler
			<_T1> responseHandler, HttpContext httpContext) where _T1:T
		{
			throw new RuntimeException("Mock Http Client does not know how to handle this request.  It should be fixed"
				);
		}

		internal interface Responder
		{
			/// <exception cref="System.IO.IOException"></exception>
			HttpResponse Execute(HttpRequestMessage httpUriRequest);
		}
	}
}
