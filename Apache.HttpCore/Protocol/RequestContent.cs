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
	/// <summary>RequestContent is the most important interceptor for outgoing requests.</summary>
	/// <remarks>
	/// RequestContent is the most important interceptor for outgoing requests.
	/// It is responsible for delimiting content length by adding
	/// <code>Content-Length</code> or <code>Transfer-Content</code> headers based
	/// on the properties of the enclosed entity and the protocol version.
	/// This interceptor is required for correct functioning of client side protocol
	/// processors.
	/// </remarks>
	/// <since>4.0</since>
	public class RequestContent : IHttpRequestInterceptor
	{
		private readonly bool overwrite;

		/// <summary>Default constructor.</summary>
		/// <remarks>
		/// Default constructor. The <code>Content-Length</code> or <code>Transfer-Encoding</code>
		/// will cause the interceptor to throw
		/// <see cref="Org.Apache.Http.ProtocolException">Org.Apache.Http.ProtocolException</see>
		/// if already present in the
		/// response message.
		/// </remarks>
		public RequestContent() : this(false)
		{
		}

		/// <summary>Constructor that can be used to fine-tune behavior of this interceptor.</summary>
		/// <remarks>Constructor that can be used to fine-tune behavior of this interceptor.</remarks>
		/// <param name="overwrite">
		/// If set to <code>true</code> the <code>Content-Length</code> and
		/// <code>Transfer-Encoding</code> headers will be created or updated if already present.
		/// If set to <code>false</code> the <code>Content-Length</code> and
		/// <code>Transfer-Encoding</code> headers will cause the interceptor to throw
		/// <see cref="Org.Apache.Http.ProtocolException">Org.Apache.Http.ProtocolException</see>
		/// if already present in the response message.
		/// </param>
		/// <since>4.2</since>
		public RequestContent(bool overwrite) : base()
		{
			this.overwrite = overwrite;
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Process(IHttpRequest request, HttpContext context)
		{
			Args.NotNull(request, "HTTP request");
			if (request is HttpEntityEnclosingRequest)
			{
				if (this.overwrite)
				{
					request.RemoveHeaders(HTTP.TransferEncoding);
					request.RemoveHeaders(HTTP.ContentLen);
				}
				else
				{
					if (request.ContainsHeader(HTTP.TransferEncoding))
					{
						throw new ProtocolException("Transfer-encoding header already present");
					}
					if (request.ContainsHeader(HTTP.ContentLen))
					{
						throw new ProtocolException("Content-Length header already present");
					}
				}
				ProtocolVersion ver = request.GetRequestLine().GetProtocolVersion();
				HttpEntity entity = ((HttpEntityEnclosingRequest)request).GetEntity();
				if (entity == null)
				{
					request.AddHeader(HTTP.ContentLen, "0");
					return;
				}
				// Must specify a transfer encoding or a content length
				if (entity.IsChunked() || entity.GetContentLength() < 0)
				{
					if (ver.LessEquals(HttpVersion.Http10))
					{
						throw new ProtocolException("Chunked transfer encoding not allowed for " + ver);
					}
					request.AddHeader(HTTP.TransferEncoding, HTTP.ChunkCoding);
				}
				else
				{
					request.AddHeader(HTTP.ContentLen, System.Convert.ToString(entity.GetContentLength
						()));
				}
				// Specify a content type if known
				if (entity.GetContentType() != null && !request.ContainsHeader(HTTP.ContentType))
				{
					request.AddHeader(entity.GetContentType());
				}
				// Specify a content encoding if known
				if (entity.GetContentEncoding() != null && !request.ContainsHeader(HTTP.ContentEncoding
					))
				{
					request.AddHeader(entity.GetContentEncoding());
				}
			}
		}
	}
}
