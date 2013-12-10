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
using Couchbase;
using Couchbase.Support;
using Couchbase.Util;
using Org.Apache.Http;
using Org.Apache.Http.Client;
using Org.Apache.Http.Client.Methods;
using Org.Apache.Http.Impl.Client;
using Sharpen;

namespace Couchbase.Support
{
	public class CBLRemoteMultipartDownloaderRequest : CBLRemoteRequest
	{
		private CBLDatabase db;

		public CBLRemoteMultipartDownloaderRequest(ScheduledExecutorService workExecutor, 
			HttpClientFactory clientFactory, string method, Uri url, object body, CBLDatabase
			 db, CBLRemoteRequestCompletionBlock onCompletion) : base(workExecutor, clientFactory
			, method, url, body, onCompletion)
		{
			this.db = db;
		}

		public override void Run()
		{
			HttpClient httpClient = clientFactory.GetHttpClient();
			PreemptivelySetAuthCredentials(httpClient);
			IHttpUriRequest request = CreateConcreteRequest();
			request.AddHeader("Accept", "*/*");
			ExecuteRequest(httpClient, request);
		}

		protected internal override void ExecuteRequest(HttpClient httpClient, IHttpUriRequest
			 request)
		{
			object fullBody = null;
			Exception error = null;
			try
			{
				HttpResponse response = httpClient.Execute(request);
				// add in cookies to global store
				DefaultHttpClient defaultHttpClient = (DefaultHttpClient)httpClient;
				new CBLHttpClientFactory().AddCookies(defaultHttpClient.GetCookieStore().GetCookies
					());
				StatusLine status = response.GetStatusLine();
				if (status.GetStatusCode() >= 300)
				{
					Log.E(CBLDatabase.Tag, "Got error " + Sharpen.Extensions.ToString(status.GetStatusCode
						()));
					Log.E(CBLDatabase.Tag, "Request was for: " + request.ToString());
					Log.E(CBLDatabase.Tag, "Status reason: " + status.GetReasonPhrase());
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
							CBLMultipartDocumentReader reader = new CBLMultipartDocumentReader(response, db);
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
							RespondWithResult(fullBody, error);
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
								fullBody = CBLServer.GetObjectMapper().ReadValue<object>(inputStream);
								RespondWithResult(fullBody, error);
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
			catch (ClientProtocolException e)
			{
				Log.E(CBLDatabase.Tag, "client protocol exception", e);
				error = e;
			}
			catch (IOException e)
			{
				Log.E(CBLDatabase.Tag, "io exception", e);
				error = e;
			}
		}
	}
}
