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
	/// <summary>The strict implementation of the content length strategy.</summary>
	/// <remarks>
	/// The strict implementation of the content length strategy. This class
	/// will throw
	/// <see cref="Org.Apache.Http.ProtocolException">Org.Apache.Http.ProtocolException</see>
	/// if it encounters an unsupported
	/// transfer encoding or a malformed <code>Content-Length</code> header
	/// value.
	/// <p>
	/// This class recognizes "chunked" and "identitiy" transfer-coding only.
	/// </remarks>
	/// <since>4.0</since>
	public class StrictContentLengthStrategy : ContentLengthStrategy
	{
		public static readonly Org.Apache.Http.Impl.Entity.StrictContentLengthStrategy Instance
			 = new Org.Apache.Http.Impl.Entity.StrictContentLengthStrategy();

		private readonly int implicitLen;

		/// <summary>
		/// Creates <tt>StrictContentLengthStrategy</tt> instance with the given length used per default
		/// when content length is not explicitly specified in the message.
		/// </summary>
		/// <remarks>
		/// Creates <tt>StrictContentLengthStrategy</tt> instance with the given length used per default
		/// when content length is not explicitly specified in the message.
		/// </remarks>
		/// <param name="implicitLen">implicit content length.</param>
		/// <since>4.2</since>
		public StrictContentLengthStrategy(int implicitLen) : base()
		{
			this.implicitLen = implicitLen;
		}

		/// <summary>Creates <tt>StrictContentLengthStrategy</tt> instance.</summary>
		/// <remarks>
		/// Creates <tt>StrictContentLengthStrategy</tt> instance.
		/// <see cref="Org.Apache.Http.Entity.ContentLengthStrategy.Identity">Org.Apache.Http.Entity.ContentLengthStrategy.Identity
		/// 	</see>
		/// is used per default when content length is not explicitly specified in the message.
		/// </remarks>
		public StrictContentLengthStrategy() : this(Identity)
		{
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		public override long DetermineLength(HttpMessage message)
		{
			Args.NotNull(message, "HTTP message");
			// Although Transfer-Encoding is specified as a list, in practice
			// it is either missing or has the single value "chunked". So we
			// treat it as a single-valued header here.
			Header transferEncodingHeader = message.GetFirstHeader(HTTP.TransferEncoding);
			if (transferEncodingHeader != null)
			{
				string s = transferEncodingHeader.GetValue();
				if (Sharpen.Runtime.EqualsIgnoreCase(HTTP.ChunkCoding, s))
				{
					if (message.GetProtocolVersion().LessEquals(HttpVersion.Http10))
					{
						throw new ProtocolException("Chunked transfer encoding not allowed for " + message
							.GetProtocolVersion());
					}
					return Chunked;
				}
				else
				{
					if (Sharpen.Runtime.EqualsIgnoreCase(HTTP.IdentityCoding, s))
					{
						return Identity;
					}
					else
					{
						throw new ProtocolException("Unsupported transfer encoding: " + s);
					}
				}
			}
			Header contentLengthHeader = message.GetFirstHeader(HTTP.ContentLen);
			if (contentLengthHeader != null)
			{
				string s = contentLengthHeader.GetValue();
				try
				{
					long len = long.Parse(s);
					if (len < 0)
					{
						throw new ProtocolException("Negative content length: " + s);
					}
					return len;
				}
				catch (FormatException)
				{
					throw new ProtocolException("Invalid content length: " + s);
				}
			}
			return this.implicitLen;
		}
	}
}
