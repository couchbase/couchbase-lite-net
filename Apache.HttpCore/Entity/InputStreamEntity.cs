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
using System.IO;
using Org.Apache.Http.Entity;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>
	/// A streamed, non-repeatable entity that obtains its content from
	/// an
	/// <see cref="System.IO.InputStream">System.IO.InputStream</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public class InputStreamEntity : AbstractHttpEntity
	{
		private readonly InputStream content;

		private readonly long length;

		/// <summary>Creates an entity with an unknown length.</summary>
		/// <remarks>
		/// Creates an entity with an unknown length.
		/// Equivalent to
		/// <code>new InputStreamEntity(instream, -1)</code>
		/// .
		/// </remarks>
		/// <param name="instream">input stream</param>
		/// <exception cref="System.ArgumentException">
		/// if
		/// <code>instream</code>
		/// is
		/// <code>null</code>
		/// </exception>
		/// <since>4.3</since>
		public InputStreamEntity(InputStream instream) : this(instream, -1)
		{
		}

		/// <summary>Creates an entity with a specified content length.</summary>
		/// <remarks>Creates an entity with a specified content length.</remarks>
		/// <param name="instream">input stream</param>
		/// <param name="length">
		/// of the input stream,
		/// <code>-1</code>
		/// if unknown
		/// </param>
		/// <exception cref="System.ArgumentException">
		/// if
		/// <code>instream</code>
		/// is
		/// <code>null</code>
		/// </exception>
		public InputStreamEntity(InputStream instream, long length) : this(instream, length
			, null)
		{
		}

		/// <summary>Creates an entity with a content type and unknown length.</summary>
		/// <remarks>
		/// Creates an entity with a content type and unknown length.
		/// Equivalent to
		/// <code>new InputStreamEntity(instream, -1, contentType)</code>
		/// .
		/// </remarks>
		/// <param name="instream">input stream</param>
		/// <param name="contentType">content type</param>
		/// <exception cref="System.ArgumentException">
		/// if
		/// <code>instream</code>
		/// is
		/// <code>null</code>
		/// </exception>
		/// <since>4.3</since>
		public InputStreamEntity(InputStream instream, ContentType contentType) : this(instream
			, -1, contentType)
		{
		}

		/// <param name="instream">input stream</param>
		/// <param name="length">
		/// of the input stream,
		/// <code>-1</code>
		/// if unknown
		/// </param>
		/// <param name="contentType">
		/// for specifying the
		/// <code>Content-Type</code>
		/// header, may be
		/// <code>null</code>
		/// </param>
		/// <exception cref="System.ArgumentException">
		/// if
		/// <code>instream</code>
		/// is
		/// <code>null</code>
		/// </exception>
		/// <since>4.2</since>
		public InputStreamEntity(InputStream instream, long length, ContentType contentType
			) : base()
		{
			this.content = Args.NotNull(instream, "Source input stream");
			this.length = length;
			if (contentType != null)
			{
				SetContentType(contentType.ToString());
			}
		}

		public override bool IsRepeatable()
		{
			return false;
		}

		/// <returns>
		/// the content length or
		/// <code>-1</code>
		/// if unknown
		/// </returns>
		public override long GetContentLength()
		{
			return this.length;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override InputStream GetContent()
		{
			return this.content;
		}

		/// <summary>
		/// Writes bytes from the
		/// <code>InputStream</code>
		/// this entity was constructed
		/// with to an
		/// <code>OutputStream</code>
		/// .  The content length
		/// determines how many bytes are written.  If the length is unknown (
		/// <code>-1</code>
		/// ), the
		/// stream will be completely consumed (to the end of the stream).
		/// </summary>
		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream outstream)
		{
			Args.NotNull(outstream, "Output stream");
			InputStream instream = this.content;
			try
			{
				byte[] buffer = new byte[OutputBufferSize];
				int l;
				if (this.length < 0)
				{
					// consume until EOF
					while ((l = instream.Read(buffer)) != -1)
					{
						outstream.Write(buffer, 0, l);
					}
				}
				else
				{
					// consume no more than length
					long remaining = this.length;
					while (remaining > 0)
					{
						l = instream.Read(buffer, 0, (int)Math.Min(OutputBufferSize, remaining));
						if (l == -1)
						{
							break;
						}
						outstream.Write(buffer, 0, l);
						remaining -= l;
					}
				}
			}
			finally
			{
				instream.Close();
			}
		}

		public override bool IsStreaming()
		{
			return true;
		}
	}
}
