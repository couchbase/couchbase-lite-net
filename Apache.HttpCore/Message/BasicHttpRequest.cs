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
using Org.Apache.Http.Message;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>
	/// Basic implementation of
	/// <see cref="Org.Apache.Http.IHttpRequest">Org.Apache.Http.IHttpRequest</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public class BasicHttpRequest : AbstractHttpMessage, IHttpRequest
	{
		private readonly string method;

		private readonly string uri;

		private RequestLine requestline;

		/// <summary>
		/// Creates an instance of this class using the given request method
		/// and URI.
		/// </summary>
		/// <remarks>
		/// Creates an instance of this class using the given request method
		/// and URI.
		/// </remarks>
		/// <param name="method">request method.</param>
		/// <param name="uri">request URI.</param>
		public BasicHttpRequest(string method, string uri) : base()
		{
			this.method = Args.NotNull(method, "Method name");
			this.uri = Args.NotNull(uri, "Request URI");
			this.requestline = null;
		}

		/// <summary>
		/// Creates an instance of this class using the given request method, URI
		/// and the HTTP protocol version.
		/// </summary>
		/// <remarks>
		/// Creates an instance of this class using the given request method, URI
		/// and the HTTP protocol version.
		/// </remarks>
		/// <param name="method">request method.</param>
		/// <param name="uri">request URI.</param>
		/// <param name="ver">HTTP protocol version.</param>
		public BasicHttpRequest(string method, string uri, ProtocolVersion ver) : this(new 
			BasicRequestLine(method, uri, ver))
		{
		}

		/// <summary>Creates an instance of this class using the given request line.</summary>
		/// <remarks>Creates an instance of this class using the given request line.</remarks>
		/// <param name="requestline">request line.</param>
		public BasicHttpRequest(RequestLine requestline) : base()
		{
			this.requestline = Args.NotNull(requestline, "Request line");
			this.method = requestline.GetMethod();
			this.uri = requestline.GetUri();
		}

		/// <summary>Returns the HTTP protocol version to be used for this request.</summary>
		/// <remarks>Returns the HTTP protocol version to be used for this request.</remarks>
		/// <seealso cref="BasicHttpRequest(string, string)">BasicHttpRequest(string, string)
		/// 	</seealso>
		public override ProtocolVersion GetProtocolVersion()
		{
			return GetRequestLine().GetProtocolVersion();
		}

		/// <summary>Returns the request line of this request.</summary>
		/// <remarks>Returns the request line of this request.</remarks>
		/// <seealso cref="BasicHttpRequest(string, string)">BasicHttpRequest(string, string)
		/// 	</seealso>
		public virtual RequestLine GetRequestLine()
		{
			if (this.requestline == null)
			{
				this.requestline = new BasicRequestLine(this.method, this.uri, HttpVersion.Http11
					);
			}
			return this.requestline;
		}

		public override string ToString()
		{
			return this.method + " " + this.uri + " " + this.headergroup;
		}
	}
}
