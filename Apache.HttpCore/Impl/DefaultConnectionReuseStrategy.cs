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
using Org.Apache.Http.Message;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>Default implementation of a strategy deciding about connection re-use.</summary>
	/// <remarks>
	/// Default implementation of a strategy deciding about connection re-use.
	/// The default implementation first checks some basics, for example
	/// whether the connection is still open or whether the end of the
	/// request entity can be determined without closing the connection.
	/// If these checks pass, the tokens in the <code>Connection</code> header will
	/// be examined. In the absence of a <code>Connection</code> header, the
	/// non-standard but commonly used <code>Proxy-Connection</code> header takes
	/// it's role. A token <code>close</code> indicates that the connection cannot
	/// be reused. If there is no such token, a token <code>keep-alive</code>
	/// indicates that the connection should be re-used. If neither token is found,
	/// or if there are no <code>Connection</code> headers, the default policy for
	/// the HTTP version is applied. Since <code>HTTP/1.1</code>, connections are
	/// re-used by default. Up until <code>HTTP/1.0</code>, connections are not
	/// re-used by default.
	/// </remarks>
	/// <since>4.0</since>
	public class DefaultConnectionReuseStrategy : ConnectionReuseStrategy
	{
		public static readonly Org.Apache.Http.Impl.DefaultConnectionReuseStrategy Instance
			 = new Org.Apache.Http.Impl.DefaultConnectionReuseStrategy();

		public DefaultConnectionReuseStrategy() : base()
		{
		}

		// see interface ConnectionReuseStrategy
		public virtual bool KeepAlive(HttpResponse response, HttpContext context)
		{
			Args.NotNull(response, "HTTP response");
			Args.NotNull(context, "HTTP context");
			// Check for a self-terminating entity. If the end of the entity will
			// be indicated by closing the connection, there is no keep-alive.
			ProtocolVersion ver = response.GetStatusLine().GetProtocolVersion();
			Header teh = response.GetFirstHeader(HTTP.TransferEncoding);
			if (teh != null)
			{
				if (!Sharpen.Runtime.EqualsIgnoreCase(HTTP.ChunkCoding, teh.GetValue()))
				{
					return false;
				}
			}
			else
			{
				if (CanResponseHaveBody(response))
				{
					Header[] clhs = response.GetHeaders(HTTP.ContentLen);
					// Do not reuse if not properly content-length delimited
					if (clhs.Length == 1)
					{
						Header clh = clhs[0];
						try
						{
							int contentLen = System.Convert.ToInt32(clh.GetValue());
							if (contentLen < 0)
							{
								return false;
							}
						}
						catch (FormatException)
						{
							return false;
						}
					}
					else
					{
						return false;
					}
				}
			}
			// Check for the "Connection" header. If that is absent, check for
			// the "Proxy-Connection" header. The latter is an unspecified and
			// broken but unfortunately common extension of HTTP.
			HeaderIterator hit = response.HeaderIterator(HTTP.ConnDirective);
			if (!hit.HasNext())
			{
				hit = response.HeaderIterator("Proxy-Connection");
			}
			// Experimental usage of the "Connection" header in HTTP/1.0 is
			// documented in RFC 2068, section 19.7.1. A token "keep-alive" is
			// used to indicate that the connection should be persistent.
			// Note that the final specification of HTTP/1.1 in RFC 2616 does not
			// include this information. Neither is the "Connection" header
			// mentioned in RFC 1945, which informally describes HTTP/1.0.
			//
			// RFC 2616 specifies "close" as the only connection token with a
			// specific meaning: it disables persistent connections.
			//
			// The "Proxy-Connection" header is not formally specified anywhere,
			// but is commonly used to carry one token, "close" or "keep-alive".
			// The "Connection" header, on the other hand, is defined as a
			// sequence of tokens, where each token is a header name, and the
			// token "close" has the above-mentioned additional meaning.
			//
			// To get through this mess, we treat the "Proxy-Connection" header
			// in exactly the same way as the "Connection" header, but only if
			// the latter is missing. We scan the sequence of tokens for both
			// "close" and "keep-alive". As "close" is specified by RFC 2068,
			// it takes precedence and indicates a non-persistent connection.
			// If there is no "close" but a "keep-alive", we take the hint.
			if (hit.HasNext())
			{
				try
				{
					TokenIterator ti = CreateTokenIterator(hit);
					bool keepalive = false;
					while (ti.HasNext())
					{
						string token = ti.NextToken();
						if (Sharpen.Runtime.EqualsIgnoreCase(HTTP.ConnClose, token))
						{
							return false;
						}
						else
						{
							if (Sharpen.Runtime.EqualsIgnoreCase(HTTP.ConnKeepAlive, token))
							{
								// continue the loop, there may be a "close" afterwards
								keepalive = true;
							}
						}
					}
					if (keepalive)
					{
						return true;
					}
				}
				catch (ParseException)
				{
					// neither "close" nor "keep-alive", use default policy
					// invalid connection header means no persistent connection
					// we don't have logging in HttpCore, so the exception is lost
					return false;
				}
			}
			// default since HTTP/1.1 is persistent, before it was non-persistent
			return !ver.LessEquals(HttpVersion.Http10);
		}

		/// <summary>Creates a token iterator from a header iterator.</summary>
		/// <remarks>
		/// Creates a token iterator from a header iterator.
		/// This method can be overridden to replace the implementation of
		/// the token iterator.
		/// </remarks>
		/// <param name="hit">the header iterator</param>
		/// <returns>the token iterator</returns>
		protected internal virtual TokenIterator CreateTokenIterator(HeaderIterator hit)
		{
			return new BasicTokenIterator(hit);
		}

		private bool CanResponseHaveBody(HttpResponse response)
		{
			int status = response.GetStatusLine().GetStatusCode();
			return status >= HttpStatus.ScOk && status != HttpStatus.ScNoContent && status !=
				 HttpStatus.ScNotModified && status != HttpStatus.ScResetContent;
		}
	}
}
