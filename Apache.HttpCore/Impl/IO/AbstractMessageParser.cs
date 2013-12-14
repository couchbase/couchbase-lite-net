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
using System.Collections.Generic;
using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.IO;
using Org.Apache.Http.Message;
using Org.Apache.Http.Params;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>
	/// Abstract base class for HTTP message parsers that obtain input from
	/// an instance of
	/// <see cref="Org.Apache.Http.IO.SessionInputBuffer">Org.Apache.Http.IO.SessionInputBuffer
	/// 	</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public abstract class AbstractMessageParser<T> : HttpMessageParser<T> where T:HttpMessage
	{
		private const int HeadLine = 0;

		private const int Headers = 1;

		private readonly SessionInputBuffer sessionBuffer;

		private readonly MessageConstraints messageConstraints;

		private readonly IList<CharArrayBuffer> headerLines;

		protected internal readonly LineParser lineParser;

		private int state;

		private T message;

		/// <summary>Creates an instance of AbstractMessageParser.</summary>
		/// <remarks>Creates an instance of AbstractMessageParser.</remarks>
		/// <param name="buffer">the session input buffer.</param>
		/// <param name="parser">the line parser.</param>
		/// <param name="params">HTTP parameters.</param>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use AbstractMessageParser{T}.AbstractMessageParser(Org.Apache.Http.IO.SessionInputBuffer, Org.Apache.Http.Message.LineParser, Org.Apache.Http.Config.MessageConstraints)"
			)]
		public AbstractMessageParser(SessionInputBuffer buffer, LineParser parser, HttpParams
			 @params) : base()
		{
			Args.NotNull(buffer, "Session input buffer");
			Args.NotNull(@params, "HTTP parameters");
			this.sessionBuffer = buffer;
			this.messageConstraints = HttpParamConfig.GetMessageConstraints(@params);
			this.lineParser = (parser != null) ? parser : BasicLineParser.Instance;
			this.headerLines = new AList<CharArrayBuffer>();
			this.state = HeadLine;
		}

		/// <summary>Creates new instance of AbstractMessageParser.</summary>
		/// <remarks>Creates new instance of AbstractMessageParser.</remarks>
		/// <param name="buffer">the session input buffer.</param>
		/// <param name="lineParser">
		/// the line parser. If <code>null</code>
		/// <see cref="Org.Apache.Http.Message.BasicLineParser.Instance">Org.Apache.Http.Message.BasicLineParser.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="constraints">
		/// the message constraints. If <code>null</code>
		/// <see cref="Org.Apache.Http.Config.MessageConstraints.Default">Org.Apache.Http.Config.MessageConstraints.Default
		/// 	</see>
		/// will be used.
		/// </param>
		/// <since>4.3</since>
		public AbstractMessageParser(SessionInputBuffer buffer, LineParser lineParser, MessageConstraints
			 constraints) : base()
		{
			this.sessionBuffer = Args.NotNull(buffer, "Session input buffer");
			this.lineParser = lineParser != null ? lineParser : BasicLineParser.Instance;
			this.messageConstraints = constraints != null ? constraints : MessageConstraints.
				Default;
			this.headerLines = new AList<CharArrayBuffer>();
			this.state = HeadLine;
		}

		/// <summary>
		/// Parses HTTP headers from the data receiver stream according to the generic
		/// format as given in Section 3.1 of RFC 822, RFC-2616 Section 4 and 19.3.
		/// </summary>
		/// <remarks>
		/// Parses HTTP headers from the data receiver stream according to the generic
		/// format as given in Section 3.1 of RFC 822, RFC-2616 Section 4 and 19.3.
		/// </remarks>
		/// <param name="inbuffer">Session input buffer</param>
		/// <param name="maxHeaderCount">
		/// maximum number of headers allowed. If the number
		/// of headers received from the data stream exceeds maxCount value, an
		/// IOException will be thrown. Setting this parameter to a negative value
		/// or zero will disable the check.
		/// </param>
		/// <param name="maxLineLen">
		/// maximum number of characters for a header line,
		/// including the continuation lines. Setting this parameter to a negative
		/// value or zero will disable the check.
		/// </param>
		/// <returns>array of HTTP headers</returns>
		/// <param name="parser">
		/// line parser to use. Can be <code>null</code>, in which case
		/// the default implementation of this interface will be used.
		/// </param>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException">in case of HTTP protocol violation
		/// 	</exception>
		public static Header[] ParseHeaders(SessionInputBuffer inbuffer, int maxHeaderCount
			, int maxLineLen, LineParser parser)
		{
			IList<CharArrayBuffer> headerLines = new AList<CharArrayBuffer>();
			return ParseHeaders(inbuffer, maxHeaderCount, maxLineLen, parser != null ? parser
				 : BasicLineParser.Instance, headerLines);
		}

		/// <summary>
		/// Parses HTTP headers from the data receiver stream according to the generic
		/// format as given in Section 3.1 of RFC 822, RFC-2616 Section 4 and 19.3.
		/// </summary>
		/// <remarks>
		/// Parses HTTP headers from the data receiver stream according to the generic
		/// format as given in Section 3.1 of RFC 822, RFC-2616 Section 4 and 19.3.
		/// </remarks>
		/// <param name="inbuffer">Session input buffer</param>
		/// <param name="maxHeaderCount">
		/// maximum number of headers allowed. If the number
		/// of headers received from the data stream exceeds maxCount value, an
		/// IOException will be thrown. Setting this parameter to a negative value
		/// or zero will disable the check.
		/// </param>
		/// <param name="maxLineLen">
		/// maximum number of characters for a header line,
		/// including the continuation lines. Setting this parameter to a negative
		/// value or zero will disable the check.
		/// </param>
		/// <param name="parser">line parser to use.</param>
		/// <param name="headerLines">
		/// List of header lines. This list will be used to store
		/// intermediate results. This makes it possible to resume parsing of
		/// headers in case of a
		/// <see cref="System.Threading.ThreadInterruptedException">System.Threading.ThreadInterruptedException
		/// 	</see>
		/// .
		/// </param>
		/// <returns>array of HTTP headers</returns>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException">in case of HTTP protocol violation
		/// 	</exception>
		/// <since>4.1</since>
		public static Header[] ParseHeaders(SessionInputBuffer inbuffer, int maxHeaderCount
			, int maxLineLen, LineParser parser, IList<CharArrayBuffer> headerLines)
		{
			Args.NotNull(inbuffer, "Session input buffer");
			Args.NotNull(parser, "Line parser");
			Args.NotNull(headerLines, "Header line list");
			CharArrayBuffer current = null;
			CharArrayBuffer previous = null;
			for (; ; )
			{
				if (current == null)
				{
					current = new CharArrayBuffer(64);
				}
				else
				{
					current.Clear();
				}
				int l = inbuffer.ReadLine(current);
				if (l == -1 || current.Length() < 1)
				{
					break;
				}
				// Parse the header name and value
				// Check for folded headers first
				// Detect LWS-char see HTTP/1.0 or HTTP/1.1 Section 2.2
				// discussion on folded headers
				if ((current.CharAt(0) == ' ' || current.CharAt(0) == '\t') && previous != null)
				{
					// we have continuation folded header
					// so append value
					int i = 0;
					while (i < current.Length())
					{
						char ch = current.CharAt(i);
						if (ch != ' ' && ch != '\t')
						{
							break;
						}
						i++;
					}
					if (maxLineLen > 0 && previous.Length() + 1 + current.Length() - i > maxLineLen)
					{
						throw new MessageConstraintException("Maximum line length limit exceeded");
					}
					previous.Append(' ');
					previous.Append(current, i, current.Length() - i);
				}
				else
				{
					headerLines.AddItem(current);
					previous = current;
					current = null;
				}
				if (maxHeaderCount > 0 && headerLines.Count >= maxHeaderCount)
				{
					throw new MessageConstraintException("Maximum header count exceeded");
				}
			}
			Header[] headers = new Header[headerLines.Count];
			for (int i_1 = 0; i_1 < headerLines.Count; i_1++)
			{
				CharArrayBuffer buffer = headerLines[i_1];
				try
				{
					headers[i_1] = parser.ParseHeader(buffer);
				}
				catch (ParseException ex)
				{
					throw new ProtocolException(ex.Message);
				}
			}
			return headers;
		}

		/// <summary>
		/// Subclasses must override this method to generate an instance of
		/// <see cref="Org.Apache.Http.HttpMessage">Org.Apache.Http.HttpMessage</see>
		/// based on the initial input from the session buffer.
		/// <p>
		/// Usually this method is expected to read just the very first line or
		/// the very first valid from the data stream and based on the input generate
		/// an appropriate instance of
		/// <see cref="Org.Apache.Http.HttpMessage">Org.Apache.Http.HttpMessage</see>
		/// .
		/// </summary>
		/// <param name="sessionBuffer">the session input buffer.</param>
		/// <returns>HTTP message based on the input from the session buffer.</returns>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">in case of HTTP protocol violation.
		/// 	</exception>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error.</exception>
		protected internal abstract T ParseHead(SessionInputBuffer sessionBuffer);

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		public virtual T Parse()
		{
			int st = this.state;
			switch (st)
			{
				case HeadLine:
				{
					try
					{
						this.message = ParseHead(this.sessionBuffer);
					}
					catch (ParseException px)
					{
						throw new ProtocolException(px.Message, px);
					}
					this.state = Headers;
					goto case Headers;
				}

				case Headers:
				{
					//$FALL-THROUGH$
					Header[] headers = Org.Apache.Http.Impl.IO.AbstractMessageParser.ParseHeaders(this
						.sessionBuffer, this.messageConstraints.GetMaxHeaderCount(), this.messageConstraints
						.GetMaxLineLength(), this.lineParser, this.headerLines);
					this.message.SetHeaders(headers);
					T result = this.message;
					this.message = null;
					this.headerLines.Clear();
					this.state = HeadLine;
					return result;
				}

				default:
				{
					throw new InvalidOperationException("Inconsistent parser state");
				}
			}
		}
	}
}
