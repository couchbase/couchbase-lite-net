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
using Org.Apache.Http;
using Org.Apache.Http.Message;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>Basic parser for lines in the head section of an HTTP message.</summary>
	/// <remarks>
	/// Basic parser for lines in the head section of an HTTP message.
	/// There are individual methods for parsing a request line, a
	/// status line, or a header line.
	/// The lines to parse are passed in memory, the parser does not depend
	/// on any specific IO mechanism.
	/// Instances of this class are stateless and thread-safe.
	/// Derived classes MUST maintain these properties.
	/// <p>
	/// Note: This class was created by refactoring parsing code located in
	/// various other classes. The author tags from those other classes have
	/// been replicated here, although the association with the parsing code
	/// taken from there has not been traced.
	/// </p>
	/// </remarks>
	/// <since>4.0</since>
	public class BasicLineParser : LineParser
	{
		/// <summary>A default instance of this class, for use as default or fallback.</summary>
		/// <remarks>
		/// A default instance of this class, for use as default or fallback.
		/// Note that
		/// <see cref="BasicLineParser">BasicLineParser</see>
		/// is not a singleton, there can
		/// be many instances of the class itself and of derived classes.
		/// The instance here provides non-customized, default behavior.
		/// </remarks>
		[System.ObsoleteAttribute(@"(4.3) use Instance")]
		[Obsolete]
		public static readonly Org.Apache.Http.Message.BasicLineParser Default = new Org.Apache.Http.Message.BasicLineParser
			();

		public static readonly Org.Apache.Http.Message.BasicLineParser Instance = new Org.Apache.Http.Message.BasicLineParser
			();

		/// <summary>A version of the protocol to parse.</summary>
		/// <remarks>
		/// A version of the protocol to parse.
		/// The version is typically not relevant, but the protocol name.
		/// </remarks>
		protected internal readonly ProtocolVersion protocol;

		/// <summary>Creates a new line parser for the given HTTP-like protocol.</summary>
		/// <remarks>Creates a new line parser for the given HTTP-like protocol.</remarks>
		/// <param name="proto">
		/// a version of the protocol to parse, or
		/// <code>null</code> for HTTP. The actual version
		/// is not relevant, only the protocol name.
		/// </param>
		public BasicLineParser(ProtocolVersion proto)
		{
			this.protocol = proto != null ? proto : HttpVersion.Http11;
		}

		/// <summary>Creates a new line parser for HTTP.</summary>
		/// <remarks>Creates a new line parser for HTTP.</remarks>
		public BasicLineParser() : this(null)
		{
		}

		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public static ProtocolVersion ParseProtocolVersion(string value, LineParser parser
			)
		{
			Args.NotNull(value, "Value");
			CharArrayBuffer buffer = new CharArrayBuffer(value.Length);
			buffer.Append(value);
			ParserCursor cursor = new ParserCursor(0, value.Length);
			return (parser != null ? parser : Org.Apache.Http.Message.BasicLineParser.Instance
				).ParseProtocolVersion(buffer, cursor);
		}

		// non-javadoc, see interface LineParser
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public virtual ProtocolVersion ParseProtocolVersion(CharArrayBuffer buffer, ParserCursor
			 cursor)
		{
			Args.NotNull(buffer, "Char array buffer");
			Args.NotNull(cursor, "Parser cursor");
			string protoname = this.protocol.GetProtocol();
			int protolength = protoname.Length;
			int indexFrom = cursor.GetPos();
			int indexTo = cursor.GetUpperBound();
			SkipWhitespace(buffer, cursor);
			int i = cursor.GetPos();
			// long enough for "HTTP/1.1"?
			if (i + protolength + 4 > indexTo)
			{
				throw new ParseException("Not a valid protocol version: " + buffer.Substring(indexFrom
					, indexTo));
			}
			// check the protocol name and slash
			bool ok = true;
			for (int j = 0; ok && (j < protolength); j++)
			{
				ok = (buffer.CharAt(i + j) == protoname[j]);
			}
			if (ok)
			{
				ok = (buffer.CharAt(i + protolength) == '/');
			}
			if (!ok)
			{
				throw new ParseException("Not a valid protocol version: " + buffer.Substring(indexFrom
					, indexTo));
			}
			i += protolength + 1;
			int period = buffer.IndexOf('.', i, indexTo);
			if (period == -1)
			{
				throw new ParseException("Invalid protocol version number: " + buffer.Substring(indexFrom
					, indexTo));
			}
			int major;
			try
			{
				major = System.Convert.ToInt32(buffer.SubstringTrimmed(i, period));
			}
			catch (FormatException)
			{
				throw new ParseException("Invalid protocol major version number: " + buffer.Substring
					(indexFrom, indexTo));
			}
			i = period + 1;
			int blank = buffer.IndexOf(' ', i, indexTo);
			if (blank == -1)
			{
				blank = indexTo;
			}
			int minor;
			try
			{
				minor = System.Convert.ToInt32(buffer.SubstringTrimmed(i, blank));
			}
			catch (FormatException)
			{
				throw new ParseException("Invalid protocol minor version number: " + buffer.Substring
					(indexFrom, indexTo));
			}
			cursor.UpdatePos(blank);
			return CreateProtocolVersion(major, minor);
		}

		// parseProtocolVersion
		/// <summary>Creates a protocol version.</summary>
		/// <remarks>
		/// Creates a protocol version.
		/// Called from
		/// <see cref="ParseProtocolVersion(string, LineParser)">ParseProtocolVersion(string, LineParser)
		/// 	</see>
		/// .
		/// </remarks>
		/// <param name="major">the major version number, for example 1 in HTTP/1.0</param>
		/// <param name="minor">the minor version number, for example 0 in HTTP/1.0</param>
		/// <returns>the protocol version</returns>
		protected internal virtual ProtocolVersion CreateProtocolVersion(int major, int minor
			)
		{
			return protocol.ForVersion(major, minor);
		}

		// non-javadoc, see interface LineParser
		public virtual bool HasProtocolVersion(CharArrayBuffer buffer, ParserCursor cursor
			)
		{
			Args.NotNull(buffer, "Char array buffer");
			Args.NotNull(cursor, "Parser cursor");
			int index = cursor.GetPos();
			string protoname = this.protocol.GetProtocol();
			int protolength = protoname.Length;
			if (buffer.Length() < protolength + 4)
			{
				return false;
			}
			// not long enough for "HTTP/1.1"
			if (index < 0)
			{
				// end of line, no tolerance for trailing whitespace
				// this works only for single-digit major and minor version
				index = buffer.Length() - 4 - protolength;
			}
			else
			{
				if (index == 0)
				{
					// beginning of line, tolerate leading whitespace
					while ((index < buffer.Length()) && HTTP.IsWhitespace(buffer.CharAt(index)))
					{
						index++;
					}
				}
			}
			// else within line, don't tolerate whitespace
			if (index + protolength + 4 > buffer.Length())
			{
				return false;
			}
			// just check protocol name and slash, no need to analyse the version
			bool ok = true;
			for (int j = 0; ok && (j < protolength); j++)
			{
				ok = (buffer.CharAt(index + j) == protoname[j]);
			}
			if (ok)
			{
				ok = (buffer.CharAt(index + protolength) == '/');
			}
			return ok;
		}

		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public static RequestLine ParseRequestLine(string value, LineParser parser)
		{
			Args.NotNull(value, "Value");
			CharArrayBuffer buffer = new CharArrayBuffer(value.Length);
			buffer.Append(value);
			ParserCursor cursor = new ParserCursor(0, value.Length);
			return (parser != null ? parser : Org.Apache.Http.Message.BasicLineParser.Instance
				).ParseRequestLine(buffer, cursor);
		}

		/// <summary>Parses a request line.</summary>
		/// <remarks>Parses a request line.</remarks>
		/// <param name="buffer">a buffer holding the line to parse</param>
		/// <returns>the parsed request line</returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		public virtual RequestLine ParseRequestLine(CharArrayBuffer buffer, ParserCursor 
			cursor)
		{
			Args.NotNull(buffer, "Char array buffer");
			Args.NotNull(cursor, "Parser cursor");
			int indexFrom = cursor.GetPos();
			int indexTo = cursor.GetUpperBound();
			try
			{
				SkipWhitespace(buffer, cursor);
				int i = cursor.GetPos();
				int blank = buffer.IndexOf(' ', i, indexTo);
				if (blank < 0)
				{
					throw new ParseException("Invalid request line: " + buffer.Substring(indexFrom, indexTo
						));
				}
				string method = buffer.SubstringTrimmed(i, blank);
				cursor.UpdatePos(blank);
				SkipWhitespace(buffer, cursor);
				i = cursor.GetPos();
				blank = buffer.IndexOf(' ', i, indexTo);
				if (blank < 0)
				{
					throw new ParseException("Invalid request line: " + buffer.Substring(indexFrom, indexTo
						));
				}
				string uri = buffer.SubstringTrimmed(i, blank);
				cursor.UpdatePos(blank);
				ProtocolVersion ver = ParseProtocolVersion(buffer, cursor);
				SkipWhitespace(buffer, cursor);
				if (!cursor.AtEnd())
				{
					throw new ParseException("Invalid request line: " + buffer.Substring(indexFrom, indexTo
						));
				}
				return CreateRequestLine(method, uri, ver);
			}
			catch (IndexOutOfRangeException)
			{
				throw new ParseException("Invalid request line: " + buffer.Substring(indexFrom, indexTo
					));
			}
		}

		// parseRequestLine
		/// <summary>Instantiates a new request line.</summary>
		/// <remarks>
		/// Instantiates a new request line.
		/// Called from
		/// <see cref="ParseRequestLine(string, LineParser)">ParseRequestLine(string, LineParser)
		/// 	</see>
		/// .
		/// </remarks>
		/// <param name="method">the request method</param>
		/// <param name="uri">the requested URI</param>
		/// <param name="ver">the protocol version</param>
		/// <returns>a new status line with the given data</returns>
		protected internal virtual RequestLine CreateRequestLine(string method, string uri
			, ProtocolVersion ver)
		{
			return new BasicRequestLine(method, uri, ver);
		}

		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public static StatusLine ParseStatusLine(string value, LineParser parser)
		{
			Args.NotNull(value, "Value");
			CharArrayBuffer buffer = new CharArrayBuffer(value.Length);
			buffer.Append(value);
			ParserCursor cursor = new ParserCursor(0, value.Length);
			return (parser != null ? parser : Org.Apache.Http.Message.BasicLineParser.Instance
				).ParseStatusLine(buffer, cursor);
		}

		// non-javadoc, see interface LineParser
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public virtual StatusLine ParseStatusLine(CharArrayBuffer buffer, ParserCursor cursor
			)
		{
			Args.NotNull(buffer, "Char array buffer");
			Args.NotNull(cursor, "Parser cursor");
			int indexFrom = cursor.GetPos();
			int indexTo = cursor.GetUpperBound();
			try
			{
				// handle the HTTP-Version
				ProtocolVersion ver = ParseProtocolVersion(buffer, cursor);
				// handle the Status-Code
				SkipWhitespace(buffer, cursor);
				int i = cursor.GetPos();
				int blank = buffer.IndexOf(' ', i, indexTo);
				if (blank < 0)
				{
					blank = indexTo;
				}
				int statusCode;
				string s = buffer.SubstringTrimmed(i, blank);
				for (int j = 0; j < s.Length; j++)
				{
					if (!char.IsDigit(s[j]))
					{
						throw new ParseException("Status line contains invalid status code: " + buffer.Substring
							(indexFrom, indexTo));
					}
				}
				try
				{
					statusCode = System.Convert.ToInt32(s);
				}
				catch (FormatException)
				{
					throw new ParseException("Status line contains invalid status code: " + buffer.Substring
						(indexFrom, indexTo));
				}
				//handle the Reason-Phrase
				i = blank;
				string reasonPhrase;
				if (i < indexTo)
				{
					reasonPhrase = buffer.SubstringTrimmed(i, indexTo);
				}
				else
				{
					reasonPhrase = string.Empty;
				}
				return CreateStatusLine(ver, statusCode, reasonPhrase);
			}
			catch (IndexOutOfRangeException)
			{
				throw new ParseException("Invalid status line: " + buffer.Substring(indexFrom, indexTo
					));
			}
		}

		// parseStatusLine
		/// <summary>Instantiates a new status line.</summary>
		/// <remarks>
		/// Instantiates a new status line.
		/// Called from
		/// <see cref="ParseStatusLine(string, LineParser)">ParseStatusLine(string, LineParser)
		/// 	</see>
		/// .
		/// </remarks>
		/// <param name="ver">the protocol version</param>
		/// <param name="status">the status code</param>
		/// <param name="reason">the reason phrase</param>
		/// <returns>a new status line with the given data</returns>
		protected internal virtual StatusLine CreateStatusLine(ProtocolVersion ver, int status
			, string reason)
		{
			return new BasicStatusLine(ver, status, reason);
		}

		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public static Header ParseHeader(string value, LineParser parser)
		{
			Args.NotNull(value, "Value");
			CharArrayBuffer buffer = new CharArrayBuffer(value.Length);
			buffer.Append(value);
			return (parser != null ? parser : Org.Apache.Http.Message.BasicLineParser.Instance
				).ParseHeader(buffer);
		}

		// non-javadoc, see interface LineParser
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public virtual Header ParseHeader(CharArrayBuffer buffer)
		{
			// the actual parser code is in the constructor of BufferedHeader
			return new BufferedHeader(buffer);
		}

		/// <summary>Helper to skip whitespace.</summary>
		/// <remarks>Helper to skip whitespace.</remarks>
		protected internal virtual void SkipWhitespace(CharArrayBuffer buffer, ParserCursor
			 cursor)
		{
			int pos = cursor.GetPos();
			int indexTo = cursor.GetUpperBound();
			while ((pos < indexTo) && HTTP.IsWhitespace(buffer.CharAt(pos)))
			{
				pos++;
			}
			cursor.UpdatePos(pos);
		}
	}
}
