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
using Org.Apache.Http;
using Org.Apache.Http.IO;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>Input stream that cuts off after a defined number of bytes.</summary>
	/// <remarks>
	/// Input stream that cuts off after a defined number of bytes. This class
	/// is used to receive content of HTTP messages where the end of the content
	/// entity is determined by the value of the <code>Content-Length header</code>.
	/// Entities transferred using this stream can be maximum
	/// <see cref="long.MaxValue">long.MaxValue</see>
	/// long.
	/// <p>
	/// Note that this class NEVER closes the underlying stream, even when close
	/// gets called.  Instead, it will read until the "end" of its limit on
	/// close, which allows for the seamless execution of subsequent HTTP 1.1
	/// requests, while not requiring the client to remember to read the entire
	/// contents of the response.
	/// </remarks>
	/// <since>4.0</since>
	public class ContentLengthInputStream : InputStream
	{
		private const int BufferSize = 2048;

		/// <summary>The maximum number of bytes that can be read from the stream.</summary>
		/// <remarks>
		/// The maximum number of bytes that can be read from the stream. Subsequent
		/// read operations will return -1.
		/// </remarks>
		private readonly long contentLength;

		/// <summary>The current position</summary>
		private long pos = 0;

		/// <summary>True if the stream is closed.</summary>
		/// <remarks>True if the stream is closed.</remarks>
		private bool closed = false;

		/// <summary>Wrapped input stream that all calls are delegated to.</summary>
		/// <remarks>Wrapped input stream that all calls are delegated to.</remarks>
		private SessionInputBuffer @in = null;

		/// <summary>
		/// Wraps a session input buffer and cuts off output after a defined number
		/// of bytes.
		/// </summary>
		/// <remarks>
		/// Wraps a session input buffer and cuts off output after a defined number
		/// of bytes.
		/// </remarks>
		/// <param name="in">The session input buffer</param>
		/// <param name="contentLength">
		/// The maximum number of bytes that can be read from
		/// the stream. Subsequent read operations will return -1.
		/// </param>
		public ContentLengthInputStream(SessionInputBuffer @in, long contentLength) : base
			()
		{
			this.@in = Args.NotNull(@in, "Session input buffer");
			this.contentLength = Args.NotNegative(contentLength, "Content length");
		}

		/// <summary>
		/// <p>Reads until the end of the known length of content.</p>
		/// <p>Does not close the underlying socket input, but instead leaves it
		/// primed to parse the next response.</p>
		/// </summary>
		/// <exception cref="System.IO.IOException">If an IO problem occurs.</exception>
		public override void Close()
		{
			if (!closed)
			{
				try
				{
					if (pos < contentLength)
					{
						byte[] buffer = new byte[BufferSize];
						while (Read(buffer) >= 0)
						{
						}
					}
				}
				finally
				{
					// close after above so that we don't throw an exception trying
					// to read after closed!
					closed = true;
				}
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override int Available()
		{
			if (this.@in is BufferInfo)
			{
				int len = ((BufferInfo)this.@in).Length();
				return Math.Min(len, (int)(this.contentLength - this.pos));
			}
			else
			{
				return 0;
			}
		}

		/// <summary>Read the next byte from the stream</summary>
		/// <returns>The next byte or -1 if the end of stream has been reached.</returns>
		/// <exception cref="System.IO.IOException">If an IO problem occurs</exception>
		/// <seealso cref="System.IO.InputStream.Read()">System.IO.InputStream.Read()</seealso>
		public override int Read()
		{
			if (closed)
			{
				throw new IOException("Attempted read from closed stream.");
			}
			if (pos >= contentLength)
			{
				return -1;
			}
			int b = this.@in.Read();
			if (b == -1)
			{
				if (pos < contentLength)
				{
					throw new ConnectionClosedException("Premature end of Content-Length delimited message body (expected: "
						 + contentLength + "; received: " + pos);
				}
			}
			else
			{
				pos++;
			}
			return b;
		}

		/// <summary>
		/// Does standard
		/// <see cref="System.IO.InputStream.Read(byte[], int, int)">System.IO.InputStream.Read(byte[], int, int)
		/// 	</see>
		/// behavior, but
		/// also notifies the watcher when the contents have been consumed.
		/// </summary>
		/// <param name="b">The byte array to fill.</param>
		/// <param name="off">Start filling at this position.</param>
		/// <param name="len">The number of bytes to attempt to read.</param>
		/// <returns>
		/// The number of bytes read, or -1 if the end of content has been
		/// reached.
		/// </returns>
		/// <exception cref="System.IO.IOException">Should an error occur on the wrapped stream.
		/// 	</exception>
		public override int Read(byte[] b, int off, int len)
		{
			if (closed)
			{
				throw new IOException("Attempted read from closed stream.");
			}
			if (pos >= contentLength)
			{
				return -1;
			}
			int chunk = len;
			if (pos + len > contentLength)
			{
				chunk = (int)(contentLength - pos);
			}
			int count = this.@in.Read(b, off, chunk);
			if (count == -1 && pos < contentLength)
			{
				throw new ConnectionClosedException("Premature end of Content-Length delimited message body (expected: "
					 + contentLength + "; received: " + pos);
			}
			if (count > 0)
			{
				pos += count;
			}
			return count;
		}

		/// <summary>Read more bytes from the stream.</summary>
		/// <remarks>Read more bytes from the stream.</remarks>
		/// <param name="b">The byte array to put the new data in.</param>
		/// <returns>The number of bytes read into the buffer.</returns>
		/// <exception cref="System.IO.IOException">If an IO problem occurs</exception>
		/// <seealso cref="System.IO.InputStream.Read(byte[])">System.IO.InputStream.Read(byte[])
		/// 	</seealso>
		public override int Read(byte[] b)
		{
			return Read(b, 0, b.Length);
		}

		/// <summary>Skips and discards a number of bytes from the input stream.</summary>
		/// <remarks>Skips and discards a number of bytes from the input stream.</remarks>
		/// <param name="n">The number of bytes to skip.</param>
		/// <returns>
		/// The actual number of bytes skipped. &lt;= 0 if no bytes
		/// are skipped.
		/// </returns>
		/// <exception cref="System.IO.IOException">If an error occurs while skipping bytes.</exception>
		/// <seealso cref="System.IO.InputStream.Skip(long)">System.IO.InputStream.Skip(long)
		/// 	</seealso>
		public override long Skip(long n)
		{
			if (n <= 0)
			{
				return 0;
			}
			byte[] buffer = new byte[BufferSize];
			// make sure we don't skip more bytes than are
			// still available
			long remaining = Math.Min(n, this.contentLength - this.pos);
			// skip and keep track of the bytes actually skipped
			long count = 0;
			while (remaining > 0)
			{
				int l = Read(buffer, 0, (int)Math.Min(BufferSize, remaining));
				if (l == -1)
				{
					break;
				}
				count += l;
				remaining -= l;
			}
			return count;
		}
	}
}
