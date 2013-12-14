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

using System.Net;
using Org.Apache.Http;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>RequestTargetHost is responsible for adding <code>Host</code> header.</summary>
	/// <remarks>
	/// RequestTargetHost is responsible for adding <code>Host</code> header. This
	/// interceptor is required for client side protocol processors.
	/// </remarks>
	/// <since>4.0</since>
	public class RequestTargetHost : IHttpRequestInterceptor
	{
		public RequestTargetHost() : base()
		{
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Process(IHttpRequest request, HttpContext context)
		{
			Args.NotNull(request, "HTTP request");
			HttpCoreContext corecontext = HttpCoreContext.Adapt(context);
			ProtocolVersion ver = request.GetRequestLine().GetProtocolVersion();
			string method = request.GetRequestLine().GetMethod();
			if (Sharpen.Runtime.EqualsIgnoreCase(method, "CONNECT") && ver.LessEquals(HttpVersion
				.Http10))
			{
				return;
			}
			if (!request.ContainsHeader(HTTP.TargetHost))
			{
				HttpHost targethost = corecontext.GetTargetHost();
				if (targethost == null)
				{
					HttpConnection conn = corecontext.GetConnection();
					if (conn is HttpInetConnection)
					{
						// Populate the context with a default HTTP host based on the
						// inet address of the target host
						IPAddress address = ((HttpInetConnection)conn).GetRemoteAddress();
						int port = ((HttpInetConnection)conn).GetRemotePort();
						if (address != null)
						{
							targethost = new HttpHost(address.GetHostName(), port);
						}
					}
					if (targethost == null)
					{
						if (ver.LessEquals(HttpVersion.Http10))
						{
							return;
						}
						else
						{
							throw new ProtocolException("Target host missing");
						}
					}
				}
				request.AddHeader(HTTP.TargetHost, targethost.ToHostString());
			}
		}
	}
}
