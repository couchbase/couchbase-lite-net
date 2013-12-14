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
using Org.Apache.Http.Impl.IO;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>Implements chunked transfer coding.</summary>
	/// <remarks>
	/// Implements chunked transfer coding. The content is received in small chunks.
	/// Entities transferred using this input stream can be of unlimited length.
	/// After the stream is read to the end, it provides access to the trailers,
	/// if any.
	/// <p>
	/// Note that this class NEVER closes the underlying stream, even when close
	/// gets called.  Instead, it will read until the "end" of its chunking on
	/// close, which allows for the seamless execution of subsequent HTTP 1.1
	/// requests, while not requiring the client to remember to read the entire
	/// contents of the response.
	/// </remarks>
	/// <since>4.0</since>
	public class ChunkedInputStream : InputStream
	{
		private const int ChunkLen = 1;

		private const int ChunkData = 2;

		private const int ChunkCrlf = 3;

		private const int BufferSize = 2048;

		/// <summary>The session input buffer</summary>
		private readonly SessionInputBuffer @in;

		private readonly CharArrayBuffer buffer;

		private int state;

		/// <summary>The chunk size</summary>
		private int chunkSize;

		/// <summary>The current position within the current chunk</summary>
		private int pos;

		/// <summary>True if we've reached the end of stream</summary>
		private bool eof = false;

		/// <summary>True if this stream is closed</summary>
		private bool closed = false;

		private Header[] footers = new Header[] {  };

		/// <summary>Wraps session input stream and reads chunk coded input.</summary>
		/// <remarks>Wraps session input stream and reads chunk coded input.</remarks>
		/// <param name="in">The session input buffer</param>
		public ChunkedInputStream(SessionInputBuffer @in) : base()
		{
			this.@in = Args.NotNull(@in, "Session input buffer");
			this.pos = 0;
			this.buffer = new CharArrayBuffer(16);
			this.state = ChunkLen;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override int Available()
		{
			if (this.@in is BufferInfo)
			{
				int len = ((BufferInfo)this.@in).Length();
				return Math.Min(len, this.chunkSize - this.pos);
			}
			else
			{
				return 0;
			}
		}

		/// <summary><p> Returns all the data in a chunked stream in coalesced form.</summary>
		/// <remarks>
		/// <p> Returns all the data in a chunked stream in coalesced form. A chunk
		/// is followed by a CRLF. The method returns -1 as soon as a chunksize of 0
		/// is detected.</p>
		/// <p> Trailer headers are read automatically at the end of the stream and
		/// can be obtained with the getResponseFooters() method.</p>
		/// </remarks>
		/// <returns>
		/// -1 of the end of the stream has been reached or the next data
		/// byte
		/// </returns>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		public override int Read()
		{
			if (this.closed)
			{
				throw new IOException("Attempted read from closed stream.");
			}
			if (this.eof)
			{
				return -1;
			}
			if (state != ChunkData)
			{
				NextChunk();
				if (this.eof)
				{
					return -1;
				}
			}
			int b = @in.Read();
			if (b != -1)
			{
				pos++;
				if (pos >= chunkSize)
				{
					state = ChunkCrlf;
				}
			}
			return b;
		}

		/// <summary>Read some bytes from the stream.</summary>
		/// <remarks>Read some bytes from the stream.</remarks>
		/// <param name="b">The byte array that will hold the contents from the stream.</param>
		/// <param name="off">
		/// The offset into the byte array at which bytes will start to be
		/// placed.
		/// </param>
		/// <param name="len">the maximum number of bytes that can be returned.</param>
		/// <returns>
		/// The number of bytes returned or -1 if the end of stream has been
		/// reached.
		/// </returns>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		public override int Read(byte[] b, int off, int len)
		{
			if (closed)
			{
				throw new IOException("Attempted read from closed stream.");
			}
			if (eof)
			{
				return -1;
			}
			if (state != ChunkData)
			{
				NextChunk();
				if (eof)
				{
					return -1;
				}
			}
			int bytesRead = @in.Read(b, off, Math.Min(len, chunkSize - pos));
			if (bytesRead != -1)
			{
				pos += bytesRead;
				if (pos >= chunkSize)
				{
					state = ChunkCrlf;
				}
				return bytesRead;
			}
			else
			{
				eof = true;
				throw new TruncatedChunkException("Truncated chunk " + "( expected size: " + chunkSize
					 + "; actual size: " + pos + ")");
			}
		}

		/// <summary>Read some bytes from the stream.</summary>
		/// <remarks>Read some bytes from the stream.</remarks>
		/// <param name="b">The byte array that will hold the contents from the stream.</param>
		/// <returns>
		/// The number of bytes returned or -1 if the end of stream has been
		/// reached.
		/// </returns>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		public override int Read(byte[] b)
		{
			return Read(b, 0, b.Length);
		}

		/// <summary>Read the next chunk.</summary>
		/// <remarks>Read the next chunk.</remarks>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		private void NextChunk()
		{
			chunkSize = GetChunkSize();
			if (chunkSize < 0)
			{
				throw new MalformedChunkCodingException("Negative chunk size");
			}
			state = ChunkData;
			pos = 0;
			if (chunkSize == 0)
			{
				eof = true;
				ParseTrailerHeaders();
			}
		}

		/// <summary>
		/// Expects the stream to start with a chunksize in hex with optional
		/// comments after a semicolon.
		/// </summary>
		/// <remarks>
		/// Expects the stream to start with a chunksize in hex with optional
		/// comments after a semicolon. The line must end with a CRLF: "a3; some
		/// comment\r\n" Positions the stream at the start of the next line.
		/// </remarks>
		/// <exception cref="System.IO.IOException"></exception>
		private int GetChunkSize()
		{
			int st = this.state;
			switch (st)
			{
				case ChunkCrlf:
				{
					this.buffer.Clear();
					int bytesRead1 = this.@in.ReadLine(this.buffer);
					if (bytesRead1 == -1)
					{
						return 0;
					}
					if (!this.buffer.IsEmpty())
					{
						throw new MalformedChunkCodingException("Unexpected content at the end of chunk");
					}
					state = ChunkLen;
					goto case ChunkLen;
				}

				case ChunkLen:
				{
					//$FALL-THROUGH$
					this.buffer.Clear();
					int bytesRead2 = this.@in.ReadLine(this.buffer);
					if (bytesRead2 == -1)
					{
						return 0;
					}
					int separator = this.buffer.IndexOf(';');
					if (separator < 0)
					{
						separator = this.buffer.Length();
					}
					try
					{
						return System.Convert.ToInt32(this.buffer.SubstringTrimmed(0, separator), 16);
					}
					catch (FormatException)
					{
						throw new MalformedChunkCodingException("Bad chunk header");
					}
					goto default;
				}

				default:
				{
					throw new InvalidOperationException("Inconsistent codec state");
				}
			}
		}

		/// <summary>Reads and stores the Trailer headers.</summary>
		/// <remarks>Reads and stores the Trailer headers.</remarks>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		private void ParseTrailerHeaders()
		{
			try
			{
				this.footers = AbstractMessageParser.ParseHeaders(@in, -1, -1, null);
			}
			catch (HttpException ex)
			{
				IOException ioe = new MalformedChunkCodingException("Invalid footer: " + ex.Message
					);
				Sharpen.Extensions.InitCause(ioe, ex);
				throw ioe;
			}
		}

		/// <summary>
		/// Upon close, this reads the remainder of the chunked message,
		/// leaving the underlying socket at a position to start reading the
		/// next response without scanning.
		/// </summary>
		/// <remarks>
		/// Upon close, this reads the remainder of the chunked message,
		/// leaving the underlying socket at a position to start reading the
		/// next response without scanning.
		/// </remarks>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		public override void Close()
		{
			if (!closed)
			{
				try
				{
					if (!eof)
					{
						// read and discard the remainder of the message
						byte[] buff = new byte[BufferSize];
						while (Read(buff) >= 0)
						{
						}
					}
				}
				finally
				{
					eof = true;
					closed = true;
				}
			}
		}

		public virtual Header[] GetFooters()
		{
			return this.footers.Clone();
		}
	}
}
