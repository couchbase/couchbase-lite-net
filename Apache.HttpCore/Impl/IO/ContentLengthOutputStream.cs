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

using System.IO;
using Org.Apache.Http.IO;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>Output stream that cuts off after a defined number of bytes.</summary>
	/// <remarks>
	/// Output stream that cuts off after a defined number of bytes. This class
	/// is used to send content of HTTP messages where the end of the content entity
	/// is determined by the value of the <code>Content-Length header</code>.
	/// Entities transferred using this stream can be maximum
	/// <see cref="long.MaxValue">long.MaxValue</see>
	/// long.
	/// <p>
	/// Note that this class NEVER closes the underlying stream, even when close
	/// gets called.  Instead, the stream will be marked as closed and no further
	/// output will be permitted.
	/// </remarks>
	/// <since>4.0</since>
	public class ContentLengthOutputStream : OutputStream
	{
		/// <summary>Wrapped session output buffer.</summary>
		/// <remarks>Wrapped session output buffer.</remarks>
		private readonly SessionOutputBuffer @out;

		/// <summary>The maximum number of bytes that can be written the stream.</summary>
		/// <remarks>
		/// The maximum number of bytes that can be written the stream. Subsequent
		/// write operations will be ignored.
		/// </remarks>
		private readonly long contentLength;

		/// <summary>Total bytes written</summary>
		private long total = 0;

		/// <summary>True if the stream is closed.</summary>
		/// <remarks>True if the stream is closed.</remarks>
		private bool closed = false;

		/// <summary>
		/// Wraps a session output buffer and cuts off output after a defined number
		/// of bytes.
		/// </summary>
		/// <remarks>
		/// Wraps a session output buffer and cuts off output after a defined number
		/// of bytes.
		/// </remarks>
		/// <param name="out">The session output buffer</param>
		/// <param name="contentLength">
		/// The maximum number of bytes that can be written to
		/// the stream. Subsequent write operations will be ignored.
		/// </param>
		/// <since>4.0</since>
		public ContentLengthOutputStream(SessionOutputBuffer @out, long contentLength) : 
			base()
		{
			this.@out = Args.NotNull(@out, "Session output buffer");
			this.contentLength = Args.NotNegative(contentLength, "Content length");
		}

		/// <summary><p>Does not close the underlying socket output.</p></summary>
		/// <exception cref="System.IO.IOException">If an I/O problem occurs.</exception>
		public override void Close()
		{
			if (!this.closed)
			{
				this.closed = true;
				this.@out.Flush();
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Flush()
		{
			this.@out.Flush();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(byte[] b, int off, int len)
		{
			if (this.closed)
			{
				throw new IOException("Attempted write to closed stream.");
			}
			if (this.total < this.contentLength)
			{
				long max = this.contentLength - this.total;
				int chunk = len;
				if (chunk > max)
				{
					chunk = (int)max;
				}
				this.@out.Write(b, off, chunk);
				this.total += chunk;
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(byte[] b)
		{
			Write(b, 0, b.Length);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(int b)
		{
			if (this.closed)
			{
				throw new IOException("Attempted write to closed stream.");
			}
			if (this.total < this.contentLength)
			{
				this.@out.Write(b);
				this.total++;
			}
		}
	}
}
