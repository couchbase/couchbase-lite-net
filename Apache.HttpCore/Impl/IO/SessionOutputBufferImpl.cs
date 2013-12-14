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
#if DefaultBHttpClientConnectionFactory
using System;
using System.IO;
using Org.Apache.Http.IO;
using Org.Apache.Http.Impl.IO;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>
	/// Abstract base class for session output buffers that stream data to
	/// an arbitrary
	/// <see cref="System.IO.OutputStream">System.IO.OutputStream</see>
	/// . This class buffers small chunks of
	/// output data in an internal byte array for optimal output performance.
	/// </p>
	/// <see cref="WriteLine(Org.Apache.Http.Util.CharArrayBuffer)">WriteLine(Org.Apache.Http.Util.CharArrayBuffer)
	/// 	</see>
	/// and
	/// <see cref="WriteLine(string)">WriteLine(string)</see>
	/// methods
	/// of this class use CR-LF as a line delimiter.
	/// </summary>
	/// <since>4.3</since>
	public class SessionOutputBufferImpl : SessionOutputBuffer, BufferInfo
	{
		private static readonly byte[] Crlf = new byte[] { HTTP.Cr, HTTP.Lf };

		private readonly HttpTransportMetricsImpl metrics;

		private readonly ByteArrayBuffer buffer;

		private readonly int fragementSizeHint;

		private readonly CharsetEncoder encoder;

		private OutputStream outstream;

		private ByteBuffer bbuf;

		/// <summary>Creates new instance of SessionOutputBufferImpl.</summary>
		/// <remarks>Creates new instance of SessionOutputBufferImpl.</remarks>
		/// <param name="metrics">HTTP transport metrics.</param>
		/// <param name="buffersize">buffer size. Must be a positive number.</param>
		/// <param name="fragementSizeHint">
		/// fragment size hint defining a minimal size of a fragment
		/// that should be written out directly to the socket bypassing the session buffer.
		/// Value <code>0</code> disables fragment buffering.
		/// </param>
		/// <param name="charencoder">
		/// charencoder to be used for encoding HTTP protocol elements.
		/// If <code>null</code> simple type cast will be used for char to byte conversion.
		/// </param>
        internal SessionOutputBufferImpl(HttpTransportMetricsImpl metrics, int buffersize, 
			int fragementSizeHint, CharsetEncoder charencoder) : base()
		{
			Args.Positive(buffersize, "Buffer size");
			Args.NotNull(metrics, "HTTP transport metrcis");
			this.metrics = metrics;
			this.buffer = new ByteArrayBuffer(buffersize);
			this.fragementSizeHint = fragementSizeHint >= 0 ? fragementSizeHint : 0;
			this.encoder = charencoder;
		}

		public SessionOutputBufferImpl(HttpTransportMetricsImpl metrics, int buffersize) : 
			this(metrics, buffersize, buffersize, null)
		{
		}

		public virtual void Bind(OutputStream outstream)
		{
			this.outstream = outstream;
		}

		public virtual bool IsBound()
		{
			return this.outstream != null;
		}

		public virtual int Capacity()
		{
			return this.buffer.Capacity();
		}

		public virtual int Length()
		{
			return this.buffer.Length();
		}

		public virtual int Available()
		{
			return Capacity() - Length();
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void StreamWrite(byte[] b, int off, int len)
		{
			Asserts.NotNull(outstream, "Output stream");
			this.outstream.Write(b, off, len);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void FlushStream()
		{
			if (this.outstream != null)
			{
				this.outstream.Flush();
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void FlushBuffer()
		{
			int len = this.buffer.Length();
			if (len > 0)
			{
				StreamWrite(this.buffer.Buffer(), 0, len);
				this.buffer.Clear();
				this.metrics.IncrementBytesTransferred(len);
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Flush()
		{
			FlushBuffer();
			FlushStream();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Write(byte[] b, int off, int len)
		{
			if (b == null)
			{
				return;
			}
			// Do not want to buffer large-ish chunks
			// if the byte array is larger then MIN_CHUNK_LIMIT
			// write it directly to the output stream
			if (len > this.fragementSizeHint || len > this.buffer.Capacity())
			{
				// flush the buffer
				FlushBuffer();
				// write directly to the out stream
				StreamWrite(b, off, len);
				this.metrics.IncrementBytesTransferred(len);
			}
			else
			{
				// Do not let the buffer grow unnecessarily
				int freecapacity = this.buffer.Capacity() - this.buffer.Length();
				if (len > freecapacity)
				{
					// flush the buffer
					FlushBuffer();
				}
				// buffer
				this.buffer.Append(b, off, len);
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Write(byte[] b)
		{
			if (b == null)
			{
				return;
			}
			Write(b, 0, b.Length);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Write(int b)
		{
			if (this.fragementSizeHint > 0)
			{
				if (this.buffer.IsFull())
				{
					FlushBuffer();
				}
				this.buffer.Append(b);
			}
			else
			{
				FlushBuffer();
				this.outstream.Write(b);
			}
		}

		/// <summary>
		/// Writes characters from the specified string followed by a line delimiter
		/// to this session buffer.
		/// </summary>
		/// <remarks>
		/// Writes characters from the specified string followed by a line delimiter
		/// to this session buffer.
		/// <p>
		/// This method uses CR-LF as a line delimiter.
		/// </remarks>
		/// <param name="s">the line.</param>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void WriteLine(string s)
		{
			if (s == null)
			{
				return;
			}
			if (s.Length > 0)
			{
				if (this.encoder == null)
				{
					for (int i = 0; i < s.Length; i++)
					{
						Write(s[i]);
					}
				}
				else
				{
					CharBuffer cbuf = CharBuffer.Wrap(s);
					WriteEncoded(cbuf);
				}
			}
			Write(Crlf);
		}

		/// <summary>
		/// Writes characters from the specified char array followed by a line
		/// delimiter to this session buffer.
		/// </summary>
		/// <remarks>
		/// Writes characters from the specified char array followed by a line
		/// delimiter to this session buffer.
		/// <p>
		/// This method uses CR-LF as a line delimiter.
		/// </remarks>
		/// <param name="charbuffer">the buffer containing chars of the line.</param>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void WriteLine(CharArrayBuffer charbuffer)
		{
			if (charbuffer == null)
			{
				return;
			}
			if (this.encoder == null)
			{
				int off = 0;
				int remaining = charbuffer.Length();
				while (remaining > 0)
				{
					int chunk = this.buffer.Capacity() - this.buffer.Length();
					chunk = Math.Min(chunk, remaining);
					if (chunk > 0)
					{
						this.buffer.Append(charbuffer, off, chunk);
					}
					if (this.buffer.IsFull())
					{
						FlushBuffer();
					}
					off += chunk;
					remaining -= chunk;
				}
			}
			else
			{
				CharBuffer cbuf = CharBuffer.Wrap(charbuffer.Buffer(), 0, charbuffer.Length());
				WriteEncoded(cbuf);
			}
			Write(Crlf);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void WriteEncoded(CharBuffer cbuf)
		{
			if (!cbuf.HasRemaining())
			{
				return;
			}
			if (this.bbuf == null)
			{
				this.bbuf = ByteBuffer.Allocate(1024);
			}
			this.encoder.Reset();
			while (cbuf.HasRemaining())
			{
				CoderResult result = this.encoder.Encode(cbuf, this.bbuf, true);
				HandleEncodingResult(result);
			}
			CoderResult result_1 = this.encoder.Flush(this.bbuf);
			HandleEncodingResult(result_1);
			this.bbuf.Clear();
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void HandleEncodingResult(CoderResult result)
		{
			if (result.IsError())
			{
				result.ThrowException();
			}
			this.bbuf.Flip();
			while (this.bbuf.HasRemaining())
			{
				Write(this.bbuf.Get());
			}
			this.bbuf.Compact();
		}

		public virtual HttpTransportMetrics GetMetrics()
		{
			return this.metrics;
		}
	}
}
#endif