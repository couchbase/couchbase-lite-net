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

using Org.Apache.Http.IO;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.IO
{
	/// <summary>Session output buffer for blocking connections.</summary>
	/// <remarks>
	/// Session output buffer for blocking connections. This interface is similar to
	/// OutputStream class, but it also provides methods for writing lines of text.
	/// <p>
	/// Implementing classes are also expected to manage intermediate data buffering
	/// for optimal output performance.
	/// </remarks>
	/// <since>4.0</since>
	public interface SessionOutputBuffer
	{
		/// <summary>
		/// Writes <code>len</code> bytes from the specified byte array
		/// starting at offset <code>off</code> to this session buffer.
		/// </summary>
		/// <remarks>
		/// Writes <code>len</code> bytes from the specified byte array
		/// starting at offset <code>off</code> to this session buffer.
		/// <p>
		/// If <code>off</code> is negative, or <code>len</code> is negative, or
		/// <code>off+len</code> is greater than the length of the array
		/// <code>b</code>, then an <tt>IndexOutOfBoundsException</tt> is thrown.
		/// </remarks>
		/// <param name="b">the data.</param>
		/// <param name="off">the start offset in the data.</param>
		/// <param name="len">the number of bytes to write.</param>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		void Write(byte[] b, int off, int len);

		/// <summary>
		/// Writes <code>b.length</code> bytes from the specified byte array
		/// to this session buffer.
		/// </summary>
		/// <remarks>
		/// Writes <code>b.length</code> bytes from the specified byte array
		/// to this session buffer.
		/// </remarks>
		/// <param name="b">the data.</param>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		void Write(byte[] b);

		/// <summary>Writes the specified byte to this session buffer.</summary>
		/// <remarks>Writes the specified byte to this session buffer.</remarks>
		/// <param name="b">the <code>byte</code>.</param>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		void Write(int b);

		/// <summary>
		/// Writes characters from the specified string followed by a line delimiter
		/// to this session buffer.
		/// </summary>
		/// <remarks>
		/// Writes characters from the specified string followed by a line delimiter
		/// to this session buffer.
		/// <p>
		/// The choice of a char encoding and line delimiter sequence is up to the
		/// specific implementations of this interface.
		/// </remarks>
		/// <param name="s">the line.</param>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		void WriteLine(string s);

		/// <summary>
		/// Writes characters from the specified char array followed by a line
		/// delimiter to this session buffer.
		/// </summary>
		/// <remarks>
		/// Writes characters from the specified char array followed by a line
		/// delimiter to this session buffer.
		/// <p>
		/// The choice of a char encoding and line delimiter sequence is up to the
		/// specific implementations of this interface.
		/// </remarks>
		/// <param name="buffer">the buffer containing chars of the line.</param>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		void WriteLine(CharArrayBuffer buffer);

		/// <summary>
		/// Flushes this session buffer and forces any buffered output bytes
		/// to be written out.
		/// </summary>
		/// <remarks>
		/// Flushes this session buffer and forces any buffered output bytes
		/// to be written out. The general contract of <code>flush</code> is
		/// that calling it is an indication that, if any bytes previously
		/// written have been buffered by the implementation of the output
		/// stream, such bytes should immediately be written to their
		/// intended destination.
		/// </remarks>
		/// <exception>
		/// IOException
		/// if an I/O error occurs.
		/// </exception>
		/// <exception cref="System.IO.IOException"></exception>
		void Flush();

		/// <summary>
		/// Returns
		/// <see cref="HttpTransportMetrics">HttpTransportMetrics</see>
		/// for this session buffer.
		/// </summary>
		/// <returns>transport metrics.</returns>
		HttpTransportMetrics GetMetrics();
	}
}
