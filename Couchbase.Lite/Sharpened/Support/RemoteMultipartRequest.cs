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
using Couchbase.Lite.Support;
using Org.Apache.Http.Client;
using Org.Apache.Http.Client.Methods;
using Org.Apache.Http.Entity.Mime;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class RemoteMultipartRequest : RemoteRequest
	{
		private MultipartEntity multiPart;

		public RemoteMultipartRequest(ScheduledExecutorService workExecutor, HttpClientFactory
			 clientFactory, string method, Uri url, MultipartEntity multiPart, RemoteRequestCompletionBlock
			 onCompletion) : base(workExecutor, clientFactory, method, url, null, onCompletion
			)
		{
			this.multiPart = multiPart;
		}

		public override void Run()
		{
			HttpClient httpClient = clientFactory.GetHttpClient();
			PreemptivelySetAuthCredentials(httpClient);
			IHttpUriRequest request = null;
			if (Sharpen.Runtime.EqualsIgnoreCase(method, "PUT"))
			{
				HttpPut putRequest = new HttpPut(url.ToExternalForm());
				putRequest.SetEntity(multiPart);
				request = putRequest;
			}
			else
			{
				if (Sharpen.Runtime.EqualsIgnoreCase(method, "POST"))
				{
					HttpPost postRequest = new HttpPost(url.ToExternalForm());
					postRequest.SetEntity(multiPart);
					request = postRequest;
				}
				else
				{
					throw new ArgumentException("Invalid request method: " + method);
				}
			}
			request.AddHeader("Accept", "*/*");
			ExecuteRequest(httpClient, request);
		}
	}
}
