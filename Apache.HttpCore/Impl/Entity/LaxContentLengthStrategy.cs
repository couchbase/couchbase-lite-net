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
using Org.Apache.Http.Entity;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.Entity
{
	/// <summary>The lax implementation of the content length strategy.</summary>
	/// <remarks>
	/// The lax implementation of the content length strategy. This class will ignore
	/// unrecognized transfer encodings and malformed <code>Content-Length</code>
	/// header values.
	/// <p/>
	/// This class recognizes "chunked" and "identitiy" transfer-coding only.
	/// </remarks>
	/// <since>4.0</since>
	public class LaxContentLengthStrategy : ContentLengthStrategy
	{
		public static readonly Org.Apache.Http.Impl.Entity.LaxContentLengthStrategy Instance
			 = new Org.Apache.Http.Impl.Entity.LaxContentLengthStrategy();

		private readonly int implicitLen;

		/// <summary>
		/// Creates <tt>LaxContentLengthStrategy</tt> instance with the given length used per default
		/// when content length is not explicitly specified in the message.
		/// </summary>
		/// <remarks>
		/// Creates <tt>LaxContentLengthStrategy</tt> instance with the given length used per default
		/// when content length is not explicitly specified in the message.
		/// </remarks>
		/// <param name="implicitLen">implicit content length.</param>
		/// <since>4.2</since>
		public LaxContentLengthStrategy(int implicitLen) : base()
		{
			this.implicitLen = implicitLen;
		}

		/// <summary>Creates <tt>LaxContentLengthStrategy</tt> instance.</summary>
		/// <remarks>
		/// Creates <tt>LaxContentLengthStrategy</tt> instance.
		/// <see cref="Org.Apache.Http.Entity.ContentLengthStrategy.Identity">Org.Apache.Http.Entity.ContentLengthStrategy.Identity
		/// 	</see>
		/// is used per default when content length is not explicitly specified in the message.
		/// </remarks>
		public LaxContentLengthStrategy() : this(Identity)
		{
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		public override long DetermineLength(HttpMessage message)
		{
			Args.NotNull(message, "HTTP message");
			Header transferEncodingHeader = message.GetFirstHeader(HTTP.TransferEncoding);
			// We use Transfer-Encoding if present and ignore Content-Length.
			// RFC2616, 4.4 item number 3
			if (transferEncodingHeader != null)
			{
				HeaderElement[] encodings;
				try
				{
					encodings = transferEncodingHeader.GetElements();
				}
				catch (ParseException px)
				{
					throw new ProtocolException("Invalid Transfer-Encoding header value: " + transferEncodingHeader
						, px);
				}
				// The chunked encoding must be the last one applied RFC2616, 14.41
				int len = encodings.Length;
				if (Sharpen.Runtime.EqualsIgnoreCase(HTTP.IdentityCoding, transferEncodingHeader.
					GetValue()))
				{
					return Identity;
				}
				else
				{
					if ((len > 0) && (Sharpen.Runtime.EqualsIgnoreCase(HTTP.ChunkCoding, encodings[len
						 - 1].GetName())))
					{
						return Chunked;
					}
					else
					{
						return Identity;
					}
				}
			}
			Header contentLengthHeader = message.GetFirstHeader(HTTP.ContentLen);
			if (contentLengthHeader != null)
			{
				long contentlen = -1;
				Header[] headers = message.GetHeaders(HTTP.ContentLen);
				for (int i = headers.Length - 1; i >= 0; i--)
				{
					Header header = headers[i];
					try
					{
						contentlen = long.Parse(header.GetValue());
						break;
					}
					catch (FormatException)
					{
					}
				}
				// See if we can have better luck with another header, if present
				if (contentlen >= 0)
				{
					return contentlen;
				}
				else
				{
					return Identity;
				}
			}
			return this.implicitLen;
		}
	}
}
