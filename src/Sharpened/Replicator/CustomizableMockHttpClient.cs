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
using Apache.Http.Client;
using Apache.Http.Client.Methods;
using Apache.Http.Conn;
using Apache.Http.Entity;
using Apache.Http.Impl;
using Apache.Http.Message;
using Apache.Http.Params;
using Apache.Http.Protocol;
using Couchbase.Lite;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
	public class CustomizableMockHttpClient : HttpClient
	{
		private IDictionary<string, CustomizableMockHttpClient.Responder> responders;

		private IList<HttpWebRequest> capturedRequests = new CopyOnWriteArrayList<HttpWebRequest
			>();

		private long responseDelayMilliseconds;

		private int numberOfEntityConsumeCallbacks;

		private IList<CustomizableMockHttpClient.ResponseListener> responseListeners;

		public CustomizableMockHttpClient()
		{
			// tests can register custom responders per url.  the key is the URL pattern to match,
			// the value is the responder that should handle that request.
			// capture all request so that the test can verify expected requests were received.
			// if this is set, it will delay responses by this number of milliseconds
			// track the number of times consumeContent is called on HttpEntity returned in response (detect resource leaks)
			// users of this class can subscribe to activity by adding themselves as a response listener
			responders = new Dictionary<string, CustomizableMockHttpClient.Responder>();
			responseListeners = new AList<CustomizableMockHttpClient.ResponseListener>();
			AddDefaultResponders();
		}

		public virtual void SetResponder(string urlPattern, CustomizableMockHttpClient.Responder
			 responder)
		{
			responders.Put(urlPattern, responder);
		}

		public virtual void AddResponseListener(CustomizableMockHttpClient.ResponseListener
			 listener)
		{
			responseListeners.AddItem(listener);
		}

		public virtual void RemoveResponseListener(CustomizableMockHttpClient.ResponseListener
			 listener)
		{
			responseListeners.Remove(listener);
		}

		public virtual void SetResponseDelayMilliseconds(long responseDelayMilliseconds)
		{
			this.responseDelayMilliseconds = responseDelayMilliseconds;
		}

		public virtual void AddDefaultResponders()
		{
			AddResponderRevDiffsAllMissing();
			AddResponderFakeBulkDocs();
			AddResponderFakeLocalDocumentUpdateIOException();
		}

		public virtual void AddResponderFailAllRequests(int statusCode)
		{
			SetResponder("*", new _Responder_84(statusCode));
		}

		private sealed class _Responder_84 : CustomizableMockHttpClient.Responder
		{
			public _Responder_84(int statusCode)
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
			SetResponder("*", new _Responder_93());
		}

		private sealed class _Responder_93 : CustomizableMockHttpClient.Responder
		{
			public _Responder_93()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				throw new IOException("Test IOException");
			}
		}

		public virtual void AddResponderFakeLocalDocumentUpdate401()
		{
			responders.Put("_local", GetFakeLocalDocumentUpdate401());
		}

		public virtual CustomizableMockHttpClient.Responder GetFakeLocalDocumentUpdate401
			()
		{
			return new _Responder_106();
		}

		private sealed class _Responder_106 : CustomizableMockHttpClient.Responder
		{
			public _Responder_106()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				string json = "{\"error\":\"Unauthorized\",\"reason\":\"Login required\"}";
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.GenerateHttpResponseObject
					(401, "Unauthorized", json);
			}
		}

		public virtual void AddResponderFakeLocalDocumentUpdate404()
		{
			responders.Put("_local", GetFakeLocalDocumentUpdate404());
		}

		public virtual CustomizableMockHttpClient.Responder GetFakeLocalDocumentUpdate404
			()
		{
			return new _Responder_120();
		}

		private sealed class _Responder_120 : CustomizableMockHttpClient.Responder
		{
			public _Responder_120()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				string json = "{\"error\":\"not_found\",\"reason\":\"missing\"}";
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.GenerateHttpResponseObject
					(404, "NOT FOUND", json);
			}
		}

		public virtual void AddResponderFakeLocalDocumentUpdateIOException()
		{
			responders.Put("_local", new _Responder_130());
		}

		private sealed class _Responder_130 : CustomizableMockHttpClient.Responder
		{
			public _Responder_130()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.FakeLocalDocumentUpdateIOException
					(httpUriRequest);
			}
		}

		public virtual void AddResponderFakeBulkDocs()
		{
			responders.Put("_bulk_docs", FakeBulkDocsResponder());
		}

		public static CustomizableMockHttpClient.Responder FakeBulkDocsResponder()
		{
			return new _Responder_143();
		}

		private sealed class _Responder_143 : CustomizableMockHttpClient.Responder
		{
			public _Responder_143()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.FakeBulkDocs(httpUriRequest
					);
			}
		}

		public static CustomizableMockHttpClient.Responder TransientErrorResponder(int statusCode
			, string statusMsg)
		{
			return new _Responder_152(statusCode, statusMsg);
		}

		private sealed class _Responder_152 : CustomizableMockHttpClient.Responder
		{
			public _Responder_152(int statusCode, string statusMsg)
			{
				this.statusCode = statusCode;
				this.statusMsg = statusMsg;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				if (statusCode == -1)
				{
					throw new IOException("Fake IO Exception from transientErrorResponder");
				}
				else
				{
					return Couchbase.Lite.Replicator.CustomizableMockHttpClient.GenerateHttpResponseObject
						(statusCode, statusMsg, null);
				}
			}

			private readonly int statusCode;

			private readonly string statusMsg;
		}

		public virtual void AddResponderRevDiffsAllMissing()
		{
			responders.Put("_revs_diff", new _Responder_166());
		}

		private sealed class _Responder_166 : CustomizableMockHttpClient.Responder
		{
			public _Responder_166()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.FakeRevsDiff(httpUriRequest
					);
			}
		}

		public virtual void AddResponderRevDiffsSmartResponder()
		{
			CustomizableMockHttpClient.SmartRevsDiffResponder smartRevsDiffResponder = new CustomizableMockHttpClient.SmartRevsDiffResponder
				();
			this.AddResponseListener(smartRevsDiffResponder);
			responders.Put("_revs_diff", smartRevsDiffResponder);
		}

		public virtual void AddResponderReturnInvalidChangesFeedJson()
		{
			SetResponder("_changes", new _Responder_183());
		}

		private sealed class _Responder_183 : CustomizableMockHttpClient.Responder
		{
			public _Responder_183()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				string json = "{\"results\":[";
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.GenerateHttpResponseObject
					(json);
			}
		}

		public virtual void ClearResponders()
		{
			responders = new Dictionary<string, CustomizableMockHttpClient.Responder>();
		}

		public virtual void AddResponderReturnEmptyChangesFeed()
		{
			SetResponder("_changes", new _Responder_197());
		}

		private sealed class _Responder_197 : CustomizableMockHttpClient.Responder
		{
			public _Responder_197()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				string json = "{\"results\":[]}";
				return Couchbase.Lite.Replicator.CustomizableMockHttpClient.GenerateHttpResponseObject
					(json);
			}
		}

		public virtual IList<HttpWebRequest> GetCapturedRequests()
		{
			return capturedRequests;
		}

		public virtual void ClearCapturedRequests()
		{
			capturedRequests.Clear();
		}

		public virtual void RecordEntityConsumeCallback()
		{
			numberOfEntityConsumeCallbacks += 1;
		}

		public virtual int GetNumberOfEntityConsumeCallbacks()
		{
			return numberOfEntityConsumeCallbacks;
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
			DelayResponseIfNeeded();
			Log.D(Database.Tag, "execute() called with request: " + httpUriRequest.GetURI().GetPath
				());
			capturedRequests.AddItem(httpUriRequest);
			foreach (string urlPattern in responders.Keys)
			{
				if (urlPattern.Equals("*") || httpUriRequest.GetURI().GetPath().Contains(urlPattern
					))
				{
					CustomizableMockHttpClient.Responder responder = responders.Get(urlPattern);
					HttpResponse response = responder.Execute(httpUriRequest);
					NotifyResponseListeners(httpUriRequest, response);
					return response;
				}
			}
			throw new RuntimeException("No responders matched for url pattern: " + httpUriRequest
				.GetURI().GetPath());
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Apache.Http.Client.ClientProtocolException"></exception>
		public static HttpResponse FakeLocalDocumentUpdateIOException(HttpRequestMessage 
			httpUriRequest)
		{
			throw new IOException("Throw exception on purpose for purposes of testSaveRemoteCheckpointNoResponse()"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Apache.Http.Client.ClientProtocolException"></exception>
		public static HttpResponse FakeBulkDocs(HttpRequestMessage httpUriRequest)
		{
			Log.D(Database.Tag, "fakeBulkDocs() called");
			IDictionary<string, object> jsonMap = GetJsonMapFromRequest((HttpPost)httpUriRequest
				);
			IList<IDictionary<string, object>> responseList = new AList<IDictionary<string, object
				>>();
			AList<IDictionary<string, object>> docs = (ArrayList)jsonMap.Get("docs");
			foreach (IDictionary<string, object> doc in docs)
			{
				IDictionary<string, object> responseListItem = new Dictionary<string, object>();
				responseListItem.Put("id", doc.Get("_id"));
				responseListItem.Put("rev", doc.Get("_rev"));
				Log.D(Database.Tag, "id: " + doc.Get("_id"));
				Log.D(Database.Tag, "rev: " + doc.Get("_rev"));
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
				ArrayList value = (ArrayList)jsonMap.Get(key);
				IDictionary<string, object> missingMap = new Dictionary<string, object>();
				missingMap.Put("missing", value);
				responseMap.Put(key, missingMap);
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
			return GenerateHttpResponseObject(200, "OK", responseJson);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static HttpResponse GenerateHttpResponseObject(int statusCode, string statusString
			, string responseJson)
		{
			DefaultHttpResponseFactory responseFactory = new DefaultHttpResponseFactory();
			BasicStatusLine statusLine = new BasicStatusLine(HttpVersion.Http11, statusCode, 
				statusString);
			HttpResponse response = responseFactory.NewHttpResponse(statusLine, null);
			if (responseJson != null)
			{
				byte[] responseBytes = Sharpen.Runtime.GetBytesForString(responseJson);
				response.SetEntity(new ByteArrayEntity(responseBytes));
			}
			return response;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static HttpResponse GenerateHttpResponseObject(HttpEntity responseEntity)
		{
			DefaultHttpResponseFactory responseFactory = new DefaultHttpResponseFactory();
			BasicStatusLine statusLine = new BasicStatusLine(HttpVersion.Http11, 200, "OK");
			HttpResponse response = responseFactory.NewHttpResponse(statusLine, null);
			response.SetEntity(responseEntity);
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
		public static IDictionary<string, object> GetJsonMapFromRequest(HttpPost httpUriRequest
			)
		{
			HttpPost post = (HttpPost)httpUriRequest;
			InputStream @is = post.GetEntity().GetContent();
			return Manager.GetObjectMapper().ReadValue<IDictionary>(@is);
		}

		private void DelayResponseIfNeeded()
		{
			if (responseDelayMilliseconds > 0)
			{
				try
				{
					Sharpen.Thread.Sleep(responseDelayMilliseconds);
				}
				catch (Exception e)
				{
					Sharpen.Runtime.PrintStackTrace(e);
				}
			}
		}

		private void NotifyResponseListeners(HttpRequestMessage httpUriRequest, HttpResponse
			 response)
		{
			foreach (CustomizableMockHttpClient.ResponseListener listener in responseListeners)
			{
				listener.ResponseSent(httpUriRequest, response);
			}
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

		internal interface ResponseListener
		{
			void ResponseSent(HttpRequestMessage httpUriRequest, HttpResponse response);
		}

		public static ArrayList ExtractDocsFromBulkDocsPost(HttpWebRequest capturedRequest
			)
		{
			try
			{
				if (capturedRequest is HttpPost)
				{
					HttpPost capturedPostRequest = (HttpPost)capturedRequest;
					if (capturedPostRequest.GetURI().GetPath().EndsWith("_bulk_docs"))
					{
						ByteArrayEntity entity = (ByteArrayEntity)capturedPostRequest.GetEntity();
						InputStream contentStream = entity.GetContent();
						IDictionary<string, object> body = Manager.GetObjectMapper().ReadValue<IDictionary
							>(contentStream);
						ArrayList docs = (ArrayList)body.Get("docs");
						return docs;
					}
				}
			}
			catch (IOException e)
			{
				throw new RuntimeException(e);
			}
			return null;
		}

		internal class SmartRevsDiffResponder : CustomizableMockHttpClient.Responder, CustomizableMockHttpClient.ResponseListener
		{
			private IList<string> docIdsSeen = new AList<string>();

			public virtual void ResponseSent(HttpRequestMessage httpUriRequest, HttpResponse 
				response)
			{
				if (httpUriRequest is HttpPost)
				{
					HttpPost capturedPostRequest = (HttpPost)httpUriRequest;
					if (capturedPostRequest.GetURI().GetPath().EndsWith("_bulk_docs"))
					{
						ArrayList docs = ExtractDocsFromBulkDocsPost(capturedPostRequest);
						foreach (object docObject in docs)
						{
							IDictionary<string, object> doc = (IDictionary)docObject;
							docIdsSeen.AddItem((string)doc.Get("_id"));
						}
					}
				}
			}

			/// <summary>Fake _revs_diff responder</summary>
			/// <exception cref="System.IO.IOException"></exception>
			public virtual HttpResponse Execute(HttpRequestMessage httpUriRequest)
			{
				IDictionary<string, object> jsonMap = GetJsonMapFromRequest((HttpPost)httpUriRequest
					);
				IDictionary<string, object> responseMap = new Dictionary<string, object>();
				foreach (string key in jsonMap.Keys)
				{
					if (docIdsSeen.Contains(key))
					{
						// we were previously pushed this document, so lets not consider it missing
						// TODO: this only takes into account document id's, not rev-ids.
						Log.D(Database.Tag, "already saw " + key);
						continue;
					}
					ArrayList value = (ArrayList)jsonMap.Get(key);
					IDictionary<string, object> missingMap = new Dictionary<string, object>();
					missingMap.Put("missing", value);
					responseMap.Put(key, missingMap);
				}
				HttpResponse response = GenerateHttpResponseObject(responseMap);
				return response;
			}
		}
	}
}
