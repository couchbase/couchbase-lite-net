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
	/// ResponseConnControl is responsible for adding <code>Connection</code> header
	/// to the outgoing responses, which is essential for managing persistence of
	/// <code>HTTP/1.0</code> connections.
	/// </summary>
	/// <remarks>
	/// ResponseConnControl is responsible for adding <code>Connection</code> header
	/// to the outgoing responses, which is essential for managing persistence of
	/// <code>HTTP/1.0</code> connections. This interceptor is recommended for
	/// server side protocol processors.
	/// </remarks>
	/// <since>4.0</since>
	public class ResponseConnControl : HttpResponseInterceptor
	{
		public ResponseConnControl() : base()
		{
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Process(HttpResponse response, HttpContext context)
		{
			Args.NotNull(response, "HTTP response");
			HttpCoreContext corecontext = HttpCoreContext.Adapt(context);
			// Always drop connection after certain type of responses
			int status = response.GetStatusLine().GetStatusCode();
			if (status == HttpStatus.ScBadRequest || status == HttpStatus.ScRequestTimeout ||
				 status == HttpStatus.ScLengthRequired || status == HttpStatus.ScRequestTooLong 
				|| status == HttpStatus.ScRequestUriTooLong || status == HttpStatus.ScServiceUnavailable
				 || status == HttpStatus.ScNotImplemented)
			{
				response.SetHeader(HTTP.ConnDirective, HTTP.ConnClose);
				return;
			}
			Header _explicit = response.GetFirstHeader(HTTP.ConnDirective);
			if (_explicit != null && Sharpen.Runtime.EqualsIgnoreCase(HTTP.ConnClose, _explicit
				.GetValue()))
			{
				// Connection persistence _explicitly disabled
				return;
			}
			// Always drop connection for HTTP/1.0 responses and below
			// if the content body cannot be correctly delimited
			HttpEntity entity = response.GetEntity();
			if (entity != null)
			{
				ProtocolVersion ver = response.GetStatusLine().GetProtocolVersion();
				if (entity.GetContentLength() < 0 && (!entity.IsChunked() || ver.LessEquals(HttpVersion
					.Http10)))
				{
					response.SetHeader(HTTP.ConnDirective, HTTP.ConnClose);
					return;
				}
			}
			// Drop connection if requested by the client or request was <= 1.0
			IHttpRequest request = corecontext.GetRequest();
			if (request != null)
			{
				Header header = request.GetFirstHeader(HTTP.ConnDirective);
				if (header != null)
				{
					response.SetHeader(HTTP.ConnDirective, header.GetValue());
				}
				else
				{
					if (request.GetProtocolVersion().LessEquals(HttpVersion.Http10))
					{
						response.SetHeader(HTTP.ConnDirective, HTTP.ConnClose);
					}
				}
			}
		}
	}
}
