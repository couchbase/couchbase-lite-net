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
using Org.Apache.Http.IO;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>Implements chunked transfer coding.</summary>
	/// <remarks>
	/// Implements chunked transfer coding. The content is sent in small chunks.
	/// Entities transferred using this output stream can be of unlimited length.
	/// Writes are buffered to an internal buffer (2048 default size).
	/// <p>
	/// Note that this class NEVER closes the underlying stream, even when close
	/// gets called.  Instead, the stream will be marked as closed and no further
	/// output will be permitted.
	/// </remarks>
	/// <since>4.0</since>
	public class ChunkedOutputStream : OutputStream
	{
		private readonly SessionOutputBuffer @out;

		private readonly byte[] cache;

		private int cachePosition = 0;

		private bool wroteLastChunk = false;

		/// <summary>True if the stream is closed.</summary>
		/// <remarks>True if the stream is closed.</remarks>
		private bool closed = false;

		/// <summary>Wraps a session output buffer and chunk-encodes the output.</summary>
		/// <remarks>Wraps a session output buffer and chunk-encodes the output.</remarks>
		/// <param name="out">The session output buffer</param>
		/// <param name="bufferSize">The minimum chunk size (excluding last chunk)</param>
		/// <exception cref="System.IO.IOException">not thrown</exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use ChunkedOutputStream(int, Org.Apache.Http.IO.SessionOutputBuffer)"
			)]
		public ChunkedOutputStream(SessionOutputBuffer @out, int bufferSize) : this(bufferSize
			, @out)
		{
		}

		/// <summary>Wraps a session output buffer and chunks the output.</summary>
		/// <remarks>
		/// Wraps a session output buffer and chunks the output. The default buffer
		/// size of 2048 was chosen because the chunk overhead is less than 0.5%
		/// </remarks>
		/// <param name="out">the output buffer to wrap</param>
		/// <exception cref="System.IO.IOException">not thrown</exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use ChunkedOutputStream(int, Org.Apache.Http.IO.SessionOutputBuffer)"
			)]
		public ChunkedOutputStream(SessionOutputBuffer @out) : this(2048, @out)
		{
		}

		/// <summary>Wraps a session output buffer and chunk-encodes the output.</summary>
		/// <remarks>Wraps a session output buffer and chunk-encodes the output.</remarks>
		/// <param name="bufferSize">The minimum chunk size (excluding last chunk)</param>
		/// <param name="out">The session output buffer</param>
		public ChunkedOutputStream(int bufferSize, SessionOutputBuffer @out) : base()
		{
			// ----------------------------------------------------- Instance Variables
			this.cache = new byte[bufferSize];
			this.@out = @out;
		}

		/// <summary>Writes the cache out onto the underlying stream</summary>
		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual void FlushCache()
		{
			if (this.cachePosition > 0)
			{
				this.@out.WriteLine(Sharpen.Extensions.ToHexString(this.cachePosition));
				this.@out.Write(this.cache, 0, this.cachePosition);
				this.@out.WriteLine(string.Empty);
				this.cachePosition = 0;
			}
		}

		/// <summary>
		/// Writes the cache and bufferToAppend to the underlying stream
		/// as one large chunk
		/// </summary>
		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual void FlushCacheWithAppend(byte[] bufferToAppend, int off
			, int len)
		{
			this.@out.WriteLine(Sharpen.Extensions.ToHexString(this.cachePosition + len));
			this.@out.Write(this.cache, 0, this.cachePosition);
			this.@out.Write(bufferToAppend, off, len);
			this.@out.WriteLine(string.Empty);
			this.cachePosition = 0;
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual void WriteClosingChunk()
		{
			// Write the final chunk.
			this.@out.WriteLine("0");
			this.@out.WriteLine(string.Empty);
		}

		// ----------------------------------------------------------- Public Methods
		/// <summary>
		/// Must be called to ensure the internal cache is flushed and the closing
		/// chunk is written.
		/// </summary>
		/// <remarks>
		/// Must be called to ensure the internal cache is flushed and the closing
		/// chunk is written.
		/// </remarks>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		public virtual void Finish()
		{
			if (!this.wroteLastChunk)
			{
				FlushCache();
				WriteClosingChunk();
				this.wroteLastChunk = true;
			}
		}

		// -------------------------------------------- OutputStream Methods
		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(int b)
		{
			if (this.closed)
			{
				throw new IOException("Attempted write to closed stream.");
			}
			this.cache[this.cachePosition] = unchecked((byte)b);
			this.cachePosition++;
			if (this.cachePosition == this.cache.Length)
			{
				FlushCache();
			}
		}

		/// <summary>Writes the array.</summary>
		/// <remarks>
		/// Writes the array. If the array does not fit within the buffer, it is
		/// not split, but rather written out as one large chunk.
		/// </remarks>
		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(byte[] b)
		{
			Write(b, 0, b.Length);
		}

		/// <summary>Writes the array.</summary>
		/// <remarks>
		/// Writes the array. If the array does not fit within the buffer, it is
		/// not split, but rather written out as one large chunk.
		/// </remarks>
		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(byte[] src, int off, int len)
		{
			if (this.closed)
			{
				throw new IOException("Attempted write to closed stream.");
			}
			if (len >= this.cache.Length - this.cachePosition)
			{
				FlushCacheWithAppend(src, off, len);
			}
			else
			{
				System.Array.Copy(src, off, cache, this.cachePosition, len);
				this.cachePosition += len;
			}
		}

		/// <summary>Flushes the content buffer and the underlying stream.</summary>
		/// <remarks>Flushes the content buffer and the underlying stream.</remarks>
		/// <exception cref="System.IO.IOException"></exception>
		public override void Flush()
		{
			FlushCache();
			this.@out.Flush();
		}

		/// <summary>Finishes writing to the underlying stream, but does NOT close the underlying stream.
		/// 	</summary>
		/// <remarks>Finishes writing to the underlying stream, but does NOT close the underlying stream.
		/// 	</remarks>
		/// <exception cref="System.IO.IOException"></exception>
		public override void Close()
		{
			if (!this.closed)
			{
				this.closed = true;
				Finish();
				this.@out.Flush();
			}
		}
	}
}
