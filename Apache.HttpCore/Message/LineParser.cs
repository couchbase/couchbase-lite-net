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
	/// <summary>Interface for parsing lines in the HEAD section of an HTTP message.</summary>
	/// <remarks>
	/// Interface for parsing lines in the HEAD section of an HTTP message.
	/// There are individual methods for parsing a request line, a
	/// status line, or a header line.
	/// The lines to parse are passed in memory, the parser does not depend
	/// on any specific IO mechanism.
	/// Instances of this interface are expected to be stateless and thread-safe.
	/// </remarks>
	/// <since>4.0</since>
	public interface LineParser
	{
		/// <summary>Parses the textual representation of a protocol version.</summary>
		/// <remarks>
		/// Parses the textual representation of a protocol version.
		/// This is needed for parsing request lines (last element)
		/// as well as status lines (first element).
		/// </remarks>
		/// <param name="buffer">a buffer holding the protocol version to parse</param>
		/// <param name="cursor">
		/// the parser cursor containing the current position and
		/// the bounds within the buffer for the parsing operation
		/// </param>
		/// <returns>the parsed protocol version</returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		ProtocolVersion ParseProtocolVersion(CharArrayBuffer buffer, ParserCursor cursor);

		/// <summary>Checks whether there likely is a protocol version in a line.</summary>
		/// <remarks>
		/// Checks whether there likely is a protocol version in a line.
		/// This method implements a <i>heuristic</i> to check for a
		/// likely protocol version specification. It does <i>not</i>
		/// guarantee that
		/// <see cref="ParseProtocolVersion(Org.Apache.Http.Util.CharArrayBuffer, ParserCursor)
		/// 	">ParseProtocolVersion(Org.Apache.Http.Util.CharArrayBuffer, ParserCursor)</see>
		/// would not
		/// detect a parse error.
		/// This can be used to detect garbage lines before a request
		/// or status line.
		/// </remarks>
		/// <param name="buffer">a buffer holding the line to inspect</param>
		/// <param name="cursor">
		/// the cursor at which to check for a protocol version, or
		/// negative for "end of line". Whether the check tolerates
		/// whitespace before or after the protocol version is
		/// implementation dependent.
		/// </param>
		/// <returns>
		/// <code>true</code> if there is a protocol version at the
		/// argument index (possibly ignoring whitespace),
		/// <code>false</code> otherwise
		/// </returns>
		bool HasProtocolVersion(CharArrayBuffer buffer, ParserCursor cursor);

		/// <summary>Parses a request line.</summary>
		/// <remarks>Parses a request line.</remarks>
		/// <param name="buffer">a buffer holding the line to parse</param>
		/// <param name="cursor">
		/// the parser cursor containing the current position and
		/// the bounds within the buffer for the parsing operation
		/// </param>
		/// <returns>the parsed request line</returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		RequestLine ParseRequestLine(CharArrayBuffer buffer, ParserCursor cursor);

		/// <summary>Parses a status line.</summary>
		/// <remarks>Parses a status line.</remarks>
		/// <param name="buffer">a buffer holding the line to parse</param>
		/// <param name="cursor">
		/// the parser cursor containing the current position and
		/// the bounds within the buffer for the parsing operation
		/// </param>
		/// <returns>the parsed status line</returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		StatusLine ParseStatusLine(CharArrayBuffer buffer, ParserCursor cursor);

		/// <summary>Creates a header from a line.</summary>
		/// <remarks>
		/// Creates a header from a line.
		/// The full header line is expected here. Header continuation lines
		/// must be joined by the caller before invoking this method.
		/// </remarks>
		/// <param name="buffer">
		/// a buffer holding the full header line.
		/// This buffer MUST NOT be re-used afterwards, since
		/// the returned object may reference the contents later.
		/// </param>
		/// <returns>
		/// the header in the argument buffer.
		/// The returned object MAY be a wrapper for the argument buffer.
		/// The argument buffer MUST NOT be re-used or changed afterwards.
		/// </returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		Header ParseHeader(CharArrayBuffer buffer);
	}
}
