/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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

using System;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Org.Apache.Http;
using Org.Apache.Http.Auth;
using Org.Apache.Http.Client;
using Org.Apache.Http.Client.Methods;
using Org.Apache.Http.Client.Protocol;
using Org.Apache.Http.Conn;
using Org.Apache.Http.Entity;
using Org.Apache.Http.Impl.Client;
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class RemoteRequest : Runnable
	{
		protected internal ScheduledExecutorService workExecutor;

		protected internal readonly HttpClientFactory clientFactory;

		protected internal string method;

		protected internal Uri url;

		protected internal object body;

		protected internal RemoteRequestCompletionBlock onCompletion;

		public RemoteRequest(ScheduledExecutorService workExecutor, HttpClientFactory clientFactory
			, string method, Uri url, object body, RemoteRequestCompletionBlock onCompletion
			)
		{
			this.clientFactory = clientFactory;
			this.method = method;
			this.url = url;
			this.body = body;
			this.onCompletion = onCompletion;
			this.workExecutor = workExecutor;
		}

		public virtual void Run()
		{
			HttpClient httpClient = clientFactory.GetHttpClient();
			ClientConnectionManager manager = httpClient.GetConnectionManager();
			IHttpUriRequest request = CreateConcreteRequest();
			PreemptivelySetAuthCredentials(httpClient);
			request.AddHeader("Accept", "multipart/related, application/json");
			SetBody(request);
			ExecuteRequest(httpClient, request);
		}

		protected internal virtual IHttpUriRequest CreateConcreteRequest()
		{
			IHttpUriRequest request = null;
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

		private void SetBody(IHttpUriRequest request)
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
					Log.E(Database.Tag, "Error serializing body of request", e);
				}
				ByteArrayEntity entity = new ByteArrayEntity(bodyBytes);
				entity.SetContentType("application/json");
				((HttpEntityEnclosingRequestBase)request).SetEntity(entity);
			}
		}

		protected internal virtual void ExecuteRequest(HttpClient httpClient, IHttpUriRequest
			 request)
		{
			object fullBody = null;
			Exception error = null;
			try
			{
				HttpResponse response = httpClient.Execute(request);
				// add in cookies to global store
				DefaultHttpClient defaultHttpClient = (DefaultHttpClient)httpClient;
				new CouchbaseLiteHttpClientFactory().AddCookies(defaultHttpClient.GetCookieStore(
					).GetCookies());
				StatusLine status = response.GetStatusLine();
				if (status.GetStatusCode() >= 300)
				{
					Log.E(Database.Tag, "Got error " + Sharpen.Extensions.ToString(status.GetStatusCode
						()));
					Log.E(Database.Tag, "Request was for: " + request.ToString());
					Log.E(Database.Tag, "Status reason: " + status.GetReasonPhrase());
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
			catch (ClientProtocolException e)
			{
				Log.E(Database.Tag, "client protocol exception", e);
				error = e;
			}
			catch (IOException e)
			{
				Log.E(Database.Tag, "io exception", e);
				error = e;
			}
			RespondWithResult(fullBody, error);
		}

		protected internal virtual void PreemptivelySetAuthCredentials(HttpClient httpClient
			)
		{
			// if the URL contains user info AND if this a DefaultHttpClient
			// then preemptively set the auth credentials
			if (url.GetUserInfo() != null)
			{
				if (url.GetUserInfo().Contains(":") && !url.GetUserInfo().Trim().Equals(":"))
				{
					string[] userInfoSplit = url.GetUserInfo().Split(":");
					Credentials creds = new UsernamePasswordCredentials(URIUtils.Decode(userInfoSplit
						[0]), URIUtils.Decode(userInfoSplit[1]));
					if (httpClient is DefaultHttpClient)
					{
						DefaultHttpClient dhc = (DefaultHttpClient)httpClient;
						IHttpRequestInterceptor preemptiveAuth = new _IHttpRequestInterceptor_167(creds);
						dhc.AddRequestInterceptor(preemptiveAuth, 0);
					}
				}
				else
				{
					Log.W(Database.Tag, "RemoteRequest Unable to parse user info, not setting credentials"
						);
				}
			}
		}

		private sealed class _IHttpRequestInterceptor_167 : IHttpRequestInterceptor
		{
			public _IHttpRequestInterceptor_167(Credentials creds)
			{
				this.creds = creds;
			}

			/// <exception cref="Org.Apache.Http.HttpException"></exception>
			/// <exception cref="System.IO.IOException"></exception>
			public void Process(IHttpRequest request, HttpContext context)
			{
				AuthState authState = (AuthState)context.GetAttribute(ClientContext.TargetAuthState
					);
				CredentialsProvider credsProvider = (CredentialsProvider)context.GetAttribute(ClientContext
					.CredsProvider);
				HttpHost targetHost = (HttpHost)context.GetAttribute(ExecutionContext.HttpTargetHost
					);
				if (authState.GetAuthScheme() == null)
				{
					AuthScope authScope = new AuthScope(targetHost.GetHostName(), targetHost.GetPort(
						));
					authState.SetAuthScheme(new BasicScheme());
					authState.SetCredentials(creds);
				}
			}

			private readonly Credentials creds;
		}

		public virtual void RespondWithResult(object result, Exception error)
		{
			if (workExecutor != null)
			{
				workExecutor.Submit(new _Runnable_201(this, result, error));
			}
			else
			{
				// don't let this crash the thread
				Log.E(Database.Tag, "work executor was null!!!");
			}
		}

		private sealed class _Runnable_201 : Runnable
		{
			public _Runnable_201(RemoteRequest _enclosing, object result, Exception error)
			{
				this._enclosing = _enclosing;
				this.result = result;
				this.error = error;
			}

			public void Run()
			{
				try
				{
					this._enclosing.onCompletion.OnCompletion(result, error);
				}
				catch (Exception e)
				{
					Log.E(Database.Tag, "RemoteRequestCompletionBlock throw Exception", e);
				}
			}

			private readonly RemoteRequest _enclosing;

			private readonly object result;

			private readonly Exception error;
		}
	}
}
