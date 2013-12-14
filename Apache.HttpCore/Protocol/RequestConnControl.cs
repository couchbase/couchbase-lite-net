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

using Org.Apache.Http;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// RequestConnControl is responsible for adding <code>Connection</code> header
	/// to the outgoing requests, which is essential for managing persistence of
	/// <code>HTTP/1.0</code> connections.
	/// </summary>
	/// <remarks>
	/// RequestConnControl is responsible for adding <code>Connection</code> header
	/// to the outgoing requests, which is essential for managing persistence of
	/// <code>HTTP/1.0</code> connections. This interceptor is recommended for
	/// client side protocol processors.
	/// </remarks>
	/// <since>4.0</since>
	public class RequestConnControl : IHttpRequestInterceptor
	{
		public RequestConnControl() : base()
		{
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Process(IHttpRequest request, HttpContext context)
		{
			Args.NotNull(request, "HTTP request");
			string method = request.GetRequestLine().GetMethod();
			if (Sharpen.Runtime.EqualsIgnoreCase(method, "CONNECT"))
			{
				return;
			}
			if (!request.ContainsHeader(HTTP.ConnDirective))
			{
				// Default policy is to keep connection alive
				// whenever possible
				request.AddHeader(HTTP.ConnDirective, HTTP.ConnKeepAlive);
			}
		}
	}
}
