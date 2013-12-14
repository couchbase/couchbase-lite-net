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
using Org.Apache.Http.Config;
using Org.Apache.Http.IO;
using Org.Apache.Http.Impl;
using Org.Apache.Http.Impl.IO;
using Org.Apache.Http.Message;
using Org.Apache.Http.Params;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>
	/// HTTP response parser that obtain its input from an instance
	/// of
	/// <see cref="Org.Apache.Http.IO.SessionInputBuffer">Org.Apache.Http.IO.SessionInputBuffer
	/// 	</see>
	/// .
	/// </summary>
	/// <since>4.2</since>
	public class DefaultHttpResponseParser : AbstractMessageParser<HttpResponse>
	{
		private readonly HttpResponseFactory responseFactory;

		private readonly CharArrayBuffer lineBuf;

		/// <summary>Creates an instance of this class.</summary>
		/// <remarks>Creates an instance of this class.</remarks>
		/// <param name="buffer">the session input buffer.</param>
		/// <param name="lineParser">the line parser.</param>
		/// <param name="responseFactory">
		/// the factory to use to create
		/// <see cref="Org.Apache.Http.HttpResponse">Org.Apache.Http.HttpResponse</see>
		/// s.
		/// </param>
		/// <param name="params">HTTP parameters.</param>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) useDefaultHttpResponseParser(Org.Apache.Http.IO.SessionInputBuffer, Org.Apache.Http.Message.LineParser, Org.Apache.Http.HttpResponseFactory, Org.Apache.Http.Config.MessageConstraints)"
			)]
		public DefaultHttpResponseParser(SessionInputBuffer buffer, LineParser lineParser
			, HttpResponseFactory responseFactory, HttpParams @params) : base(buffer, lineParser
			, @params)
		{
			this.responseFactory = Args.NotNull(responseFactory, "Response factory");
			this.lineBuf = new CharArrayBuffer(128);
		}

		/// <summary>Creates new instance of DefaultHttpResponseParser.</summary>
		/// <remarks>Creates new instance of DefaultHttpResponseParser.</remarks>
		/// <param name="buffer">the session input buffer.</param>
		/// <param name="lineParser">
		/// the line parser. If <code>null</code>
		/// <see cref="Org.Apache.Http.Message.BasicLineParser.Instance">Org.Apache.Http.Message.BasicLineParser.Instance
		/// 	</see>
		/// will be used
		/// </param>
		/// <param name="responseFactory">
		/// the response factory. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.DefaultHttpResponseFactory.Instance">Org.Apache.Http.Impl.DefaultHttpResponseFactory.Instance
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
		public DefaultHttpResponseParser(SessionInputBuffer buffer, LineParser lineParser
			, HttpResponseFactory responseFactory, MessageConstraints constraints) : base(buffer
			, lineParser, constraints)
		{
			this.responseFactory = responseFactory != null ? responseFactory : DefaultHttpResponseFactory
				.Instance;
			this.lineBuf = new CharArrayBuffer(128);
		}

		/// <since>4.3</since>
		public DefaultHttpResponseParser(SessionInputBuffer buffer, MessageConstraints constraints
			) : this(buffer, null, null, constraints)
		{
		}

		/// <since>4.3</since>
		public DefaultHttpResponseParser(SessionInputBuffer buffer) : this(buffer, null, 
			null, MessageConstraints.Default)
		{
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		protected internal override HttpResponse ParseHead(SessionInputBuffer sessionBuffer
			)
		{
			this.lineBuf.Clear();
			int i = sessionBuffer.ReadLine(this.lineBuf);
			if (i == -1)
			{
				throw new NoHttpResponseException("The target server failed to respond");
			}
			//create the status line from the status string
			ParserCursor cursor = new ParserCursor(0, this.lineBuf.Length());
			StatusLine statusline = lineParser.ParseStatusLine(this.lineBuf, cursor);
			return this.responseFactory.NewHttpResponse(statusline, null);
		}
	}
}
