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
using Org.Apache.Http.IO;
using Org.Apache.Http.Message;
using Org.Apache.Http.Params;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>
	/// Abstract base class for HTTP message writers that serialize output to
	/// an instance of
	/// <see cref="Org.Apache.Http.IO.SessionOutputBuffer">Org.Apache.Http.IO.SessionOutputBuffer
	/// 	</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public abstract class AbstractMessageWriter<T> : HttpMessageWriter<T> where T:HttpMessage
	{
		protected internal readonly SessionOutputBuffer sessionBuffer;

		protected internal readonly CharArrayBuffer lineBuf;

		protected internal readonly LineFormatter lineFormatter;

		/// <summary>Creates an instance of AbstractMessageWriter.</summary>
		/// <remarks>Creates an instance of AbstractMessageWriter.</remarks>
		/// <param name="buffer">the session output buffer.</param>
		/// <param name="formatter">the line formatter.</param>
		/// <param name="params">HTTP parameters.</param>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) useAbstractMessageWriter{T}.AbstractMessageWriter(Org.Apache.Http.IO.SessionOutputBuffer, Org.Apache.Http.Message.LineFormatter)"
			)]
		public AbstractMessageWriter(SessionOutputBuffer buffer, LineFormatter formatter, 
			HttpParams @params) : base()
		{
			Args.NotNull(buffer, "Session input buffer");
			this.sessionBuffer = buffer;
			this.lineBuf = new CharArrayBuffer(128);
			this.lineFormatter = (formatter != null) ? formatter : BasicLineFormatter.Instance;
		}

		/// <summary>Creates an instance of AbstractMessageWriter.</summary>
		/// <remarks>Creates an instance of AbstractMessageWriter.</remarks>
		/// <param name="buffer">the session output buffer.</param>
		/// <param name="formatter">
		/// the line formatter If <code>null</code>
		/// <see cref="Org.Apache.Http.Message.BasicLineFormatter.Instance">Org.Apache.Http.Message.BasicLineFormatter.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <since>4.3</since>
		public AbstractMessageWriter(SessionOutputBuffer buffer, LineFormatter formatter)
			 : base()
		{
			this.sessionBuffer = Args.NotNull(buffer, "Session input buffer");
			this.lineFormatter = (formatter != null) ? formatter : BasicLineFormatter.Instance;
			this.lineBuf = new CharArrayBuffer(128);
		}

		/// <summary>
		/// Subclasses must override this method to write out the first header line
		/// based on the
		/// <see cref="Org.Apache.Http.HttpMessage">Org.Apache.Http.HttpMessage</see>
		/// passed as a parameter.
		/// </summary>
		/// <param name="message">the message whose first line is to be written out.</param>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		protected internal abstract void WriteHeadLine(T message);

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		public virtual void Write(T message)
		{
			Args.NotNull(message, "HTTP message");
			WriteHeadLine(message);
			for (HeaderIterator it = message.HeaderIterator(); it.HasNext(); )
			{
				Header header = it.NextHeader();
				this.sessionBuffer.WriteLine(lineFormatter.FormatHeader(this.lineBuf, header));
			}
			this.lineBuf.Clear();
			this.sessionBuffer.WriteLine(this.lineBuf);
		}
	}
}
