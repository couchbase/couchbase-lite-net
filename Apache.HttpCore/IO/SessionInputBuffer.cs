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
using Org.Apache.Http.IO;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.IO
{
	/// <summary>Session input buffer for blocking connections.</summary>
	/// <remarks>
	/// Session input buffer for blocking connections. This interface is similar to
	/// InputStream class, but it also provides methods for reading lines of text.
	/// <p>
	/// Implementing classes are also expected to manage intermediate data buffering
	/// for optimal input performance.
	/// </remarks>
	/// <since>4.0</since>
	public interface SessionInputBuffer
	{
		/// <summary>
		/// Reads up to <code>len</code> bytes of data from the session buffer into
		/// an array of bytes.
		/// </summary>
		/// <remarks>
		/// Reads up to <code>len</code> bytes of data from the session buffer into
		/// an array of bytes.  An attempt is made to read as many as
		/// <code>len</code> bytes, but a smaller number may be read, possibly
		/// zero. The number of bytes actually read is returned as an integer.
		/// <p> This method blocks until input data is available, end of file is
		/// detected, or an exception is thrown.
		/// <p> If <code>off</code> is negative, or <code>len</code> is negative, or
		/// <code>off+len</code> is greater than the length of the array
		/// <code>b</code>, then an <code>IndexOutOfBoundsException</code> is
		/// thrown.
		/// </remarks>
		/// <param name="b">the buffer into which the data is read.</param>
		/// <param name="off">
		/// the start offset in array <code>b</code>
		/// at which the data is written.
		/// </param>
		/// <param name="len">the maximum number of bytes to read.</param>
		/// <returns>
		/// the total number of bytes read into the buffer, or
		/// <code>-1</code> if there is no more data because the end of
		/// the stream has been reached.
		/// </returns>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		int Read(byte[] b, int off, int len);

		/// <summary>
		/// Reads some number of bytes from the session buffer and stores them into
		/// the buffer array <code>b</code>.
		/// </summary>
		/// <remarks>
		/// Reads some number of bytes from the session buffer and stores them into
		/// the buffer array <code>b</code>. The number of bytes actually read is
		/// returned as an integer.  This method blocks until input data is
		/// available, end of file is detected, or an exception is thrown.
		/// </remarks>
		/// <param name="b">the buffer into which the data is read.</param>
		/// <returns>
		/// the total number of bytes read into the buffer, or
		/// <code>-1</code> is there is no more data because the end of
		/// the stream has been reached.
		/// </returns>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		int Read(byte[] b);

		/// <summary>Reads the next byte of data from this session buffer.</summary>
		/// <remarks>
		/// Reads the next byte of data from this session buffer. The value byte is
		/// returned as an <code>int</code> in the range <code>0</code> to
		/// <code>255</code>. If no byte is available because the end of the stream
		/// has been reached, the value <code>-1</code> is returned. This method
		/// blocks until input data is available, the end of the stream is detected,
		/// or an exception is thrown.
		/// </remarks>
		/// <returns>
		/// the next byte of data, or <code>-1</code> if the end of the
		/// stream is reached.
		/// </returns>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		int Read();

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
		/// The choice of a char encoding and line delimiter sequence is up to the
		/// specific implementations of this interface.
		/// </remarks>
		/// <param name="buffer">the line buffer.</param>
		/// <returns>one line of characters</returns>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		int ReadLine(CharArrayBuffer buffer);

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
		/// The choice of a char encoding and line delimiter sequence is up to the
		/// specific implementations of this interface.
		/// </remarks>
		/// <returns>HTTP line as a string</returns>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		string ReadLine();

		/// <summary>
		/// Blocks until some data becomes available in the session buffer or the
		/// given timeout period in milliseconds elapses.
		/// </summary>
		/// <remarks>
		/// Blocks until some data becomes available in the session buffer or the
		/// given timeout period in milliseconds elapses. If the timeout value is
		/// <code>0</code> this method blocks indefinitely.
		/// </remarks>
		/// <param name="timeout">in milliseconds.</param>
		/// <returns>
		/// <code>true</code> if some data is available in the session
		/// buffer or <code>false</code> otherwise.
		/// </returns>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) do not use. This function should be provided at the connection level"
			)]
		bool IsDataAvailable(int timeout);

		/// <summary>
		/// Returns
		/// <see cref="HttpTransportMetrics">HttpTransportMetrics</see>
		/// for this session buffer.
		/// </summary>
		/// <returns>transport metrics.</returns>
		HttpTransportMetrics GetMetrics();
	}
}
