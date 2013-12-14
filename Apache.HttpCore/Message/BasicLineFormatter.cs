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
	/// The formatted lines are returned in memory, the formatter does not depend
	/// on any specific IO mechanism.
	/// Instances of this interface are expected to be stateless and thread-safe.
	/// </remarks>
	/// <since>4.0</since>
	public class BasicLineFormatter : LineFormatter
	{
		/// <summary>A default instance of this class, for use as default or fallback.</summary>
		/// <remarks>
		/// A default instance of this class, for use as default or fallback.
		/// Note that
		/// <see cref="BasicLineFormatter">BasicLineFormatter</see>
		/// is not a singleton, there can
		/// be many instances of the class itself and of derived classes.
		/// The instance here provides non-customized, default behavior.
		/// </remarks>
		[System.ObsoleteAttribute(@"(4.3) use Instance")]
		[Obsolete]
		public static readonly Org.Apache.Http.Message.BasicLineFormatter Default = new Org.Apache.Http.Message.BasicLineFormatter
			();

		public static readonly Org.Apache.Http.Message.BasicLineFormatter Instance = new 
			Org.Apache.Http.Message.BasicLineFormatter();

		public BasicLineFormatter() : base()
		{
		}

		/// <summary>Obtains a buffer for formatting.</summary>
		/// <remarks>Obtains a buffer for formatting.</remarks>
		/// <param name="charBuffer">a buffer already available, or <code>null</code></param>
		/// <returns>
		/// the cleared argument buffer if there is one, or
		/// a new empty buffer that can be used for formatting
		/// </returns>
		protected internal virtual CharArrayBuffer InitBuffer(CharArrayBuffer charBuffer)
		{
			CharArrayBuffer buffer = charBuffer;
			if (buffer != null)
			{
				buffer.Clear();
			}
			else
			{
				buffer = new CharArrayBuffer(64);
			}
			return buffer;
		}

		/// <summary>Formats a protocol version.</summary>
		/// <remarks>Formats a protocol version.</remarks>
		/// <param name="version">the protocol version to format</param>
		/// <param name="formatter">
		/// the formatter to use, or
		/// <code>null</code> for the
		/// <see cref="Instance">default</see>
		/// </param>
		/// <returns>the formatted protocol version</returns>
		public static string FormatProtocolVersion(ProtocolVersion version, LineFormatter
			 formatter)
		{
			return (formatter != null ? formatter : Org.Apache.Http.Message.BasicLineFormatter
				.Instance).AppendProtocolVersion(null, version).ToString();
		}

		// non-javadoc, see interface LineFormatter
		public virtual CharArrayBuffer AppendProtocolVersion(CharArrayBuffer buffer, ProtocolVersion
			 version)
		{
			Args.NotNull(version, "Protocol version");
			// can't use initBuffer, that would clear the argument!
			CharArrayBuffer result = buffer;
			int len = EstimateProtocolVersionLen(version);
			if (result == null)
			{
				result = new CharArrayBuffer(len);
			}
			else
			{
				result.EnsureCapacity(len);
			}
			result.Append(version.GetProtocol());
			result.Append('/');
			result.Append(Sharpen.Extensions.ToString(version.GetMajor()));
			result.Append('.');
			result.Append(Sharpen.Extensions.ToString(version.GetMinor()));
			return result;
		}

		/// <summary>Guesses the length of a formatted protocol version.</summary>
		/// <remarks>
		/// Guesses the length of a formatted protocol version.
		/// Needed to guess the length of a formatted request or status line.
		/// </remarks>
		/// <param name="version">the protocol version to format, or <code>null</code></param>
		/// <returns>
		/// the estimated length of the formatted protocol version,
		/// in characters
		/// </returns>
		protected internal virtual int EstimateProtocolVersionLen(ProtocolVersion version
			)
		{
			return version.GetProtocol().Length + 4;
		}

		// room for "HTTP/1.1"
		/// <summary>Formats a request line.</summary>
		/// <remarks>Formats a request line.</remarks>
		/// <param name="reqline">the request line to format</param>
		/// <param name="formatter">
		/// the formatter to use, or
		/// <code>null</code> for the
		/// <see cref="Instance">default</see>
		/// </param>
		/// <returns>the formatted request line</returns>
		public static string FormatRequestLine(RequestLine reqline, LineFormatter formatter
			)
		{
			return (formatter != null ? formatter : Org.Apache.Http.Message.BasicLineFormatter
				.Instance).FormatRequestLine(null, reqline).ToString();
		}

		// non-javadoc, see interface LineFormatter
		public virtual CharArrayBuffer FormatRequestLine(CharArrayBuffer buffer, RequestLine
			 reqline)
		{
			Args.NotNull(reqline, "Request line");
			CharArrayBuffer result = InitBuffer(buffer);
			DoFormatRequestLine(result, reqline);
			return result;
		}

		/// <summary>Actually formats a request line.</summary>
		/// <remarks>
		/// Actually formats a request line.
		/// Called from
		/// <see cref="FormatRequestLine(Org.Apache.Http.RequestLine, LineFormatter)">FormatRequestLine(Org.Apache.Http.RequestLine, LineFormatter)
		/// 	</see>
		/// .
		/// </remarks>
		/// <param name="buffer">
		/// the empty buffer into which to format,
		/// never <code>null</code>
		/// </param>
		/// <param name="reqline">the request line to format, never <code>null</code></param>
		protected internal virtual void DoFormatRequestLine(CharArrayBuffer buffer, RequestLine
			 reqline)
		{
			string method = reqline.GetMethod();
			string uri = reqline.GetUri();
			// room for "GET /index.html HTTP/1.1"
			int len = method.Length + 1 + uri.Length + 1 + EstimateProtocolVersionLen(reqline
				.GetProtocolVersion());
			buffer.EnsureCapacity(len);
			buffer.Append(method);
			buffer.Append(' ');
			buffer.Append(uri);
			buffer.Append(' ');
			AppendProtocolVersion(buffer, reqline.GetProtocolVersion());
		}

		/// <summary>Formats a status line.</summary>
		/// <remarks>Formats a status line.</remarks>
		/// <param name="statline">the status line to format</param>
		/// <param name="formatter">
		/// the formatter to use, or
		/// <code>null</code> for the
		/// <see cref="Instance">default</see>
		/// </param>
		/// <returns>the formatted status line</returns>
		public static string FormatStatusLine(StatusLine statline, LineFormatter formatter
			)
		{
			return (formatter != null ? formatter : Org.Apache.Http.Message.BasicLineFormatter
				.Instance).FormatStatusLine(null, statline).ToString();
		}

		// non-javadoc, see interface LineFormatter
		public virtual CharArrayBuffer FormatStatusLine(CharArrayBuffer buffer, StatusLine
			 statline)
		{
			Args.NotNull(statline, "Status line");
			CharArrayBuffer result = InitBuffer(buffer);
			DoFormatStatusLine(result, statline);
			return result;
		}

		/// <summary>Actually formats a status line.</summary>
		/// <remarks>
		/// Actually formats a status line.
		/// Called from
		/// <see cref="FormatStatusLine(Org.Apache.Http.StatusLine, LineFormatter)">FormatStatusLine(Org.Apache.Http.StatusLine, LineFormatter)
		/// 	</see>
		/// .
		/// </remarks>
		/// <param name="buffer">
		/// the empty buffer into which to format,
		/// never <code>null</code>
		/// </param>
		/// <param name="statline">the status line to format, never <code>null</code></param>
		protected internal virtual void DoFormatStatusLine(CharArrayBuffer buffer, StatusLine
			 statline)
		{
			int len = EstimateProtocolVersionLen(statline.GetProtocolVersion()) + 1 + 3 + 1;
			// room for "HTTP/1.1 200 "
			string reason = statline.GetReasonPhrase();
			if (reason != null)
			{
				len += reason.Length;
			}
			buffer.EnsureCapacity(len);
			AppendProtocolVersion(buffer, statline.GetProtocolVersion());
			buffer.Append(' ');
			buffer.Append(Sharpen.Extensions.ToString(statline.GetStatusCode()));
			buffer.Append(' ');
			// keep whitespace even if reason phrase is empty
			if (reason != null)
			{
				buffer.Append(reason);
			}
		}

		/// <summary>Formats a header.</summary>
		/// <remarks>Formats a header.</remarks>
		/// <param name="header">the header to format</param>
		/// <param name="formatter">
		/// the formatter to use, or
		/// <code>null</code> for the
		/// <see cref="Instance">default</see>
		/// </param>
		/// <returns>the formatted header</returns>
		public static string FormatHeader(Header header, LineFormatter formatter)
		{
			return (formatter != null ? formatter : Org.Apache.Http.Message.BasicLineFormatter
				.Instance).FormatHeader(null, header).ToString();
		}

		// non-javadoc, see interface LineFormatter
		public virtual CharArrayBuffer FormatHeader(CharArrayBuffer buffer, Header header
			)
		{
			Args.NotNull(header, "Header");
			CharArrayBuffer result;
			if (header is FormattedHeader)
			{
				// If the header is backed by a buffer, re-use the buffer
				result = ((FormattedHeader)header).GetBuffer();
			}
			else
			{
				result = InitBuffer(buffer);
				DoFormatHeader(result, header);
			}
			return result;
		}

		// formatHeader
		/// <summary>Actually formats a header.</summary>
		/// <remarks>
		/// Actually formats a header.
		/// Called from
		/// <see cref="FormatHeader(Org.Apache.Http.Header, LineFormatter)">FormatHeader(Org.Apache.Http.Header, LineFormatter)
		/// 	</see>
		/// .
		/// </remarks>
		/// <param name="buffer">
		/// the empty buffer into which to format,
		/// never <code>null</code>
		/// </param>
		/// <param name="header">the header to format, never <code>null</code></param>
		protected internal virtual void DoFormatHeader(CharArrayBuffer buffer, Header header
			)
		{
			string name = header.GetName();
			string value = header.GetValue();
			int len = name.Length + 2;
			if (value != null)
			{
				len += value.Length;
			}
			buffer.EnsureCapacity(len);
			buffer.Append(name);
			buffer.Append(": ");
			if (value != null)
			{
				buffer.Append(value);
			}
		}
	}
}
