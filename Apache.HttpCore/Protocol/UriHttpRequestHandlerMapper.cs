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
	/// <summary>Maintains a map of HTTP request handlers keyed by a request URI pattern.
	/// 	</summary>
	/// <remarks>
	/// Maintains a map of HTTP request handlers keyed by a request URI pattern.
	/// <br />
	/// Patterns may have three formats:
	/// <ul>
	/// <li><code>*</code></li>
	/// <li><code>*&lt;uri&gt;</code></li>
	/// <li><code>&lt;uri&gt;*</code></li>
	/// </ul>
	/// <br />
	/// This class can be used to map an instance of
	/// <see cref="HttpRequestHandler">HttpRequestHandler</see>
	/// matching a particular request URI. Usually the
	/// mapped request handler will be used to process the request with the
	/// specified request URI.
	/// </remarks>
	/// <since>4.3</since>
	public class UriHttpRequestHandlerMapper : HttpRequestHandlerMapper
	{
		private readonly UriPatternMatcher<HttpRequestHandler> matcher;

		protected internal UriHttpRequestHandlerMapper(UriPatternMatcher<HttpRequestHandler
			> matcher) : base()
		{
			// provided injected dependencies are thread-safe
			this.matcher = Args.NotNull(matcher, "Pattern matcher");
		}

		public UriHttpRequestHandlerMapper() : this(new UriPatternMatcher<HttpRequestHandler
			>())
		{
		}

		/// <summary>
		/// Registers the given
		/// <see cref="HttpRequestHandler">HttpRequestHandler</see>
		/// as a handler for URIs
		/// matching the given pattern.
		/// </summary>
		/// <param name="pattern">the pattern to register the handler for.</param>
		/// <param name="handler">the handler.</param>
		public virtual void Register(string pattern, HttpRequestHandler handler)
		{
			Args.NotNull(pattern, "Pattern");
			Args.NotNull(handler, "Handler");
			matcher.Register(pattern, handler);
		}

		/// <summary>Removes registered handler, if exists, for the given pattern.</summary>
		/// <remarks>Removes registered handler, if exists, for the given pattern.</remarks>
		/// <param name="pattern">the pattern to unregister the handler for.</param>
		public virtual void Unregister(string pattern)
		{
			matcher.Unregister(pattern);
		}

		/// <summary>
		/// Extracts request path from the given
		/// <see cref="Org.Apache.Http.IHttpRequest">Org.Apache.Http.IHttpRequest</see>
		/// </summary>
		protected internal virtual string GetRequestPath(IHttpRequest request)
		{
			string uriPath = request.GetRequestLine().GetUri();
			int index = uriPath.IndexOf("?");
			if (index != -1)
			{
				uriPath = Sharpen.Runtime.Substring(uriPath, 0, index);
			}
			else
			{
				index = uriPath.IndexOf("#");
				if (index != -1)
				{
					uriPath = Sharpen.Runtime.Substring(uriPath, 0, index);
				}
			}
			return uriPath;
		}

		/// <summary>Looks up a handler matching the given request URI.</summary>
		/// <remarks>Looks up a handler matching the given request URI.</remarks>
		/// <param name="request">the request</param>
		/// <returns>handler or <code>null</code> if no match is found.</returns>
		public virtual HttpRequestHandler Lookup(IHttpRequest request)
		{
			Args.NotNull(request, "HTTP request");
			return matcher.Lookup(GetRequestPath(request));
		}
	}
}
