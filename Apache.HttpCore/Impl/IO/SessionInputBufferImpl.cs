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
using Org.Apache.Http.Config;
using Org.Apache.Http.IO;
using Org.Apache.Http.Impl.IO;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;
#if DefaultBHttpClientConnectionFactory
namespace Org.Apache.Http.Impl.IO
{
	/// <summary>
	/// Abstract base class for session input buffers that stream data from
	/// an arbitrary
	/// <see cref="System.IO.InputStream">System.IO.InputStream</see>
	/// . This class buffers input data in
	/// an internal byte array for optimal input performance.
	/// <p/>
	/// <see cref="ReadLine(Org.Apache.Http.Util.CharArrayBuffer)">ReadLine(Org.Apache.Http.Util.CharArrayBuffer)
	/// 	</see>
	/// and
	/// <see cref="ReadLine()">ReadLine()</see>
	/// methods of this
	/// class treat a lone LF as valid line delimiters in addition to CR-LF required
	/// by the HTTP specification.
	/// </summary>
	/// <since>4.3</since>
	public class SessionInputBufferImpl : SessionInputBuffer, BufferInfo
	{
		private readonly HttpTransportMetricsImpl metrics;

		private readonly byte[] buffer;

		private readonly ByteArrayBuffer linebuffer;

		private readonly int minChunkLimit;

		private readonly MessageConstraints constraints;

		private readonly CharsetDecoder decoder;

		private InputStream instream;

		private int bufferpos;

		private int bufferlen;

		private CharBuffer cbuf;

		/// <summary>Creates new instance of SessionInputBufferImpl.</summary>
		/// <remarks>Creates new instance of SessionInputBufferImpl.</remarks>
		/// <param name="metrics">HTTP transport metrics.</param>
		/// <param name="buffersize">buffer size. Must be a positive number.</param>
		/// <param name="minChunkLimit">
		/// size limit below which data chunks should be buffered in memory
		/// in order to minimize native method invocations on the underlying network socket.
		/// The optimal value of this parameter can be platform specific and defines a trade-off
		/// between performance of memory copy operations and that of native method invocation.
		/// If negative default chunk limited will be used.
		/// </param>
		/// <param name="constraints">
		/// Message constraints. If <code>null</code>
		/// <see cref="Org.Apache.Http.Config.MessageConstraints.Default">Org.Apache.Http.Config.MessageConstraints.Default
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="chardecoder">
		/// chardecoder to be used for decoding HTTP protocol elements.
		/// If <code>null</code> simple type cast will be used for byte to char conversion.
		/// </param>
        internal SessionInputBufferImpl(HttpTransportMetricsImpl metrics, int buffersize, int
			 minChunkLimit, MessageConstraints constraints, CharsetDecoder chardecoder)
		{
			Args.NotNull(metrics, "HTTP transport metrcis");
			Args.Positive(buffersize, "Buffer size");
			this.metrics = metrics;
			this.buffer = new byte[buffersize];
			this.bufferpos = 0;
			this.bufferlen = 0;
			this.minChunkLimit = minChunkLimit >= 0 ? minChunkLimit : 512;
			this.constraints = constraints != null ? constraints : MessageConstraints.Default;
			this.linebuffer = new ByteArrayBuffer(buffersize);
			this.decoder = chardecoder;
		}

		public SessionInputBufferImpl(HttpTransportMetricsImpl metrics, int buffersize) : 
			this(metrics, buffersize, buffersize, null, null)
		{
		}

		public virtual void Bind(InputStream instream)
		{
			this.instream = instream;
		}

		public virtual bool IsBound()
		{
			return this.instream != null;
		}

		public virtual int Capacity()
		{
			return this.buffer.Length;
		}

		public virtual int Length()
		{
			return this.bufferlen - this.bufferpos;
		}

		public virtual int Available()
		{
			return Capacity() - Length();
		}

		/// <exception cref="System.IO.IOException"></exception>
		private int StreamRead(byte[] b, int off, int len)
		{
			Asserts.NotNull(this.instream, "Input stream");
			return this.instream.Read(b, off, len);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual int FillBuffer()
		{
			// compact the buffer if necessary
			if (this.bufferpos > 0)
			{
				int len = this.bufferlen - this.bufferpos;
				if (len > 0)
				{
					System.Array.Copy(this.buffer, this.bufferpos, this.buffer, 0, len);
				}
				this.bufferpos = 0;
				this.bufferlen = len;
			}
			int l;
			int off = this.bufferlen;
			int len_1 = this.buffer.Length - off;
			l = StreamRead(this.buffer, off, len_1);
			if (l == -1)
			{
				return -1;
			}
			else
			{
				this.bufferlen = off + l;
				this.metrics.IncrementBytesTransferred(l);
				return l;
			}
		}

		public virtual bool HasBufferedData()
		{
			return this.bufferpos < this.bufferlen;
		}

		public virtual void Clear()
		{
			this.bufferpos = 0;
			this.bufferlen = 0;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual int Read()
		{
			int noRead;
			while (!HasBufferedData())
			{
				noRead = FillBuffer();
				if (noRead == -1)
				{
					return -1;
				}
			}
			return this.buffer[this.bufferpos++] & unchecked((int)(0xff));
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual int Read(byte[] b, int off, int len)
		{
			if (b == null)
			{
				return 0;
			}
			if (HasBufferedData())
			{
				int chunk = Math.Min(len, this.bufferlen - this.bufferpos);
				System.Array.Copy(this.buffer, this.bufferpos, b, off, chunk);
				this.bufferpos += chunk;
				return chunk;
			}
			// If the remaining capacity is big enough, read directly from the
			// underlying input stream bypassing the buffer.
			if (len > this.minChunkLimit)
			{
				int read = StreamRead(b, off, len);
				if (read > 0)
				{
					this.metrics.IncrementBytesTransferred(read);
				}
				return read;
			}
			else
			{
				// otherwise read to the buffer first
				while (!HasBufferedData())
				{
					int noRead = FillBuffer();
					if (noRead == -1)
					{
						return -1;
					}
				}
				int chunk = Math.Min(len, this.bufferlen - this.bufferpos);
				System.Array.Copy(this.buffer, this.bufferpos, b, off, chunk);
				this.bufferpos += chunk;
				return chunk;
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual int Read(byte[] b)
		{
			if (b == null)
			{
				return 0;
			}
			return Read(b, 0, b.Length);
		}

		private int LocateLF()
		{
			for (int i = this.bufferpos; i < this.bufferlen; i++)
			{
				if (this.buffer[i] == HTTP.Lf)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// Reads a complete line of characters up to a line delimiter from this
		/// session buffer into the given line buffer.
		/// </summary>
		/// <remarks>
		/// Reads a complete line of characters up to a line delimiter from this
		/// session buffer into the given line buffer. The number of chars actually
		/// read is returned as an integer. The line delimiter itself is discarded.
		/// If no char is available because the end of the stream has been reached,
		/// the value <code>-1</code> is returned. This method blocks until input
		/// data is available, end of file is detected, or an exception is thrown.
		/// <p>
		/// This method treats a lone LF as a valid line delimiters in addition
		/// to CR-LF required by the HTTP specification.
		/// </remarks>
		/// <param name="charbuffer">the line buffer.</param>
		/// <returns>one line of characters</returns>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual int ReadLine(CharArrayBuffer charbuffer)
		{
			Args.NotNull(charbuffer, "Char array buffer");
			int noRead = 0;
			bool retry = true;
			while (retry)
			{
				// attempt to find end of line (LF)
				int i = LocateLF();
				if (i != -1)
				{
					// end of line found.
					if (this.linebuffer.IsEmpty())
					{
						// the entire line is preset in the read buffer
						return LineFromReadBuffer(charbuffer, i);
					}
					retry = false;
					int len = i + 1 - this.bufferpos;
					this.linebuffer.Append(this.buffer, this.bufferpos, len);
					this.bufferpos = i + 1;
				}
				else
				{
					// end of line not found
					if (HasBufferedData())
					{
						int len = this.bufferlen - this.bufferpos;
						this.linebuffer.Append(this.buffer, this.bufferpos, len);
						this.bufferpos = this.bufferlen;
					}
					noRead = FillBuffer();
					if (noRead == -1)
					{
						retry = false;
					}
				}
				int maxLineLen = this.constraints.GetMaxLineLength();
				if (maxLineLen > 0 && this.linebuffer.Length() >= maxLineLen)
				{
					throw new MessageConstraintException("Maximum line length limit exceeded");
				}
			}
			if (noRead == -1 && this.linebuffer.IsEmpty())
			{
				// indicate the end of stream
				return -1;
			}
			return LineFromLineBuffer(charbuffer);
		}

		/// <summary>
		/// Reads a complete line of characters up to a line delimiter from this
		/// session buffer.
		/// </summary>
		/// <remarks>
		/// Reads a complete line of characters up to a line delimiter from this
		/// session buffer. The line delimiter itself is discarded. If no char is
		/// available because the end of the stream has been reached,
		/// <code>null</code> is returned. This method blocks until input data is
		/// available, end of file is detected, or an exception is thrown.
		/// <p>
		/// This method treats a lone LF as a valid line delimiters in addition
		/// to CR-LF required by the HTTP specification.
		/// </remarks>
		/// <returns>HTTP line as a string</returns>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		private int LineFromLineBuffer(CharArrayBuffer charbuffer)
		{
			// discard LF if found
			int len = this.linebuffer.Length();
			if (len > 0)
			{
				if (this.linebuffer.ByteAt(len - 1) == HTTP.Lf)
				{
					len--;
				}
				// discard CR if found
				if (len > 0)
				{
					if (this.linebuffer.ByteAt(len - 1) == HTTP.Cr)
					{
						len--;
					}
				}
			}
			if (this.decoder == null)
			{
				charbuffer.Append(this.linebuffer, 0, len);
			}
			else
			{
				ByteBuffer bbuf = ByteBuffer.Wrap(this.linebuffer.Buffer(), 0, len);
				len = AppendDecoded(charbuffer, bbuf);
			}
			this.linebuffer.Clear();
			return len;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private int LineFromReadBuffer(CharArrayBuffer charbuffer, int position)
		{
			int pos = position;
			int off = this.bufferpos;
			int len;
			this.bufferpos = pos + 1;
			if (pos > off && this.buffer[pos - 1] == HTTP.Cr)
			{
				// skip CR if found
				pos--;
			}
			len = pos - off;
			if (this.decoder == null)
			{
				charbuffer.Append(this.buffer, off, len);
			}
			else
			{
				ByteBuffer bbuf = ByteBuffer.Wrap(this.buffer, off, len);
				len = AppendDecoded(charbuffer, bbuf);
			}
			return len;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private int AppendDecoded(CharArrayBuffer charbuffer, ByteBuffer bbuf)
		{
			if (!bbuf.HasRemaining())
			{
				return 0;
			}
			if (this.cbuf == null)
			{
				this.cbuf = CharBuffer.Allocate(1024);
			}
			this.decoder.Reset();
			int len = 0;
			while (bbuf.HasRemaining())
			{
				CoderResult result = this.decoder.Decode(bbuf, this.cbuf, true);
				len += HandleDecodingResult(result, charbuffer, bbuf);
			}
			CoderResult result_1 = this.decoder.Flush(this.cbuf);
			len += HandleDecodingResult(result_1, charbuffer, bbuf);
			this.cbuf.Clear();
			return len;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private int HandleDecodingResult(CoderResult result, CharArrayBuffer charbuffer, 
			ByteBuffer bbuf)
		{
			if (result.IsError())
			{
				result.ThrowException();
			}
			this.cbuf.Flip();
			int len = this.cbuf.Remaining();
			while (this.cbuf.HasRemaining())
			{
				charbuffer.Append(this.cbuf.Get());
			}
			this.cbuf.Compact();
			return len;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual string ReadLine()
		{
			CharArrayBuffer charbuffer = new CharArrayBuffer(64);
			int l = ReadLine(charbuffer);
			if (l != -1)
			{
				return charbuffer.ToString();
			}
			else
			{
				return null;
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual bool IsDataAvailable(int timeout)
		{
			return HasBufferedData();
		}

		public virtual HttpTransportMetrics GetMetrics()
		{
			return this.metrics;
		}
	}
}
#endif