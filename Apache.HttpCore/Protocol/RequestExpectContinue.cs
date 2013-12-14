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
using Org.Apache.Http;
using Org.Apache.Http.Params;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// RequestExpectContinue is responsible for enabling the 'expect-continue'
	/// handshake by adding <code>Expect</code> header.
	/// </summary>
	/// <remarks>
	/// RequestExpectContinue is responsible for enabling the 'expect-continue'
	/// handshake by adding <code>Expect</code> header. This interceptor is
	/// recommended for client side protocol processors.
	/// </remarks>
	/// <since>4.0</since>
	public class RequestExpectContinue : IHttpRequestInterceptor
	{
		private readonly bool activeByDefault;

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use RequestExpectContinue(bool)")]
		public RequestExpectContinue() : this(false)
		{
		}

		/// <since>4.3</since>
		public RequestExpectContinue(bool activeByDefault) : base()
		{
			this.activeByDefault = activeByDefault;
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Process(IHttpRequest request, HttpContext context)
		{
			Args.NotNull(request, "HTTP request");
			if (!request.ContainsHeader(HTTP.ExpectDirective))
			{
				if (request is HttpEntityEnclosingRequest)
				{
					ProtocolVersion ver = request.GetRequestLine().GetProtocolVersion();
					HttpEntity entity = ((HttpEntityEnclosingRequest)request).GetEntity();
					// Do not send the expect header if request body is known to be empty
					if (entity != null && entity.GetContentLength() != 0 && !ver.LessEquals(HttpVersion
						.Http10))
					{
						bool active = request.GetParams().GetBooleanParameter(CoreProtocolPNames.UseExpectContinue
							, this.activeByDefault);
						if (active)
						{
							request.AddHeader(HTTP.ExpectDirective, HTTP.ExpectContinue);
						}
					}
				}
			}
		}
	}
}
