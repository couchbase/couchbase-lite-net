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

using Org.Apache.Http;
using Org.Apache.Http.Message;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>Interface for formatting elements of the HEAD section of an HTTP message.
	/// 	</summary>
	/// <remarks>
	/// Interface for formatting elements of the HEAD section of an HTTP message.
	/// This is the complement to
	/// <see cref="LineParser">LineParser</see>
	/// .
	/// There are individual methods for formatting a request line, a
	/// status line, or a header line. The formatting does <i>not</i> include the
	/// trailing line break sequence CR-LF.
	/// Instances of this interface are expected to be stateless and thread-safe.
	/// <p>
	/// The formatted lines are returned in memory, the formatter does not depend
	/// on any specific IO mechanism.
	/// In order to avoid unnecessary creation of temporary objects,
	/// a buffer can be passed as argument to all formatting methods.
	/// The implementation may or may not actually use that buffer for formatting.
	/// If it is used, the buffer will first be cleared by the
	/// <code>formatXXX</code> methods.
	/// The argument buffer can always be re-used after the call. The buffer
	/// returned as the result, if it is different from the argument buffer,
	/// MUST NOT be modified.
	/// </p>
	/// </remarks>
	/// <since>4.0</since>
	public interface LineFormatter
	{
		/// <summary>Formats a protocol version.</summary>
		/// <remarks>
		/// Formats a protocol version.
		/// This method does <i>not</i> follow the general contract for
		/// <code>buffer</code> arguments.
		/// It does <i>not</i> clear the argument buffer, but appends instead.
		/// The returned buffer can always be modified by the caller.
		/// Because of these differing conventions, it is not named
		/// <code>formatProtocolVersion</code>.
		/// </remarks>
		/// <param name="buffer">a buffer to which to append, or <code>null</code></param>
		/// <param name="version">the protocol version to format</param>
		/// <returns>
		/// a buffer with the formatted protocol version appended.
		/// The caller is allowed to modify the result buffer.
		/// If the <code>buffer</code> argument is not <code>null</code>,
		/// the returned buffer is the argument buffer.
		/// </returns>
		CharArrayBuffer AppendProtocolVersion(CharArrayBuffer buffer, ProtocolVersion version
			);

		/// <summary>Formats a request line.</summary>
		/// <remarks>Formats a request line.</remarks>
		/// <param name="buffer">
		/// a buffer available for formatting, or
		/// <code>null</code>.
		/// The buffer will be cleared before use.
		/// </param>
		/// <param name="reqline">the request line to format</param>
		/// <returns>the formatted request line</returns>
		CharArrayBuffer FormatRequestLine(CharArrayBuffer buffer, RequestLine reqline);

		/// <summary>Formats a status line.</summary>
		/// <remarks>Formats a status line.</remarks>
		/// <param name="buffer">
		/// a buffer available for formatting, or
		/// <code>null</code>.
		/// The buffer will be cleared before use.
		/// </param>
		/// <param name="statline">the status line to format</param>
		/// <returns>the formatted status line</returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		CharArrayBuffer FormatStatusLine(CharArrayBuffer buffer, StatusLine statline);

		/// <summary>Formats a header.</summary>
		/// <remarks>
		/// Formats a header.
		/// Due to header continuation, the result may be multiple lines.
		/// In order to generate well-formed HTTP, the lines in the result
		/// must be separated by the HTTP line break sequence CR-LF.
		/// There is <i>no</i> trailing CR-LF in the result.
		/// <br/>
		/// See the class comment for details about the buffer argument.
		/// </remarks>
		/// <param name="buffer">
		/// a buffer available for formatting, or
		/// <code>null</code>.
		/// The buffer will be cleared before use.
		/// </param>
		/// <param name="header">the header to format</param>
		/// <returns>
		/// a buffer holding the formatted header, never <code>null</code>.
		/// The returned buffer may be different from the argument buffer.
		/// </returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		CharArrayBuffer FormatHeader(CharArrayBuffer buffer, Header header);
	}
}
