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
#if DefaultBHttpClientConnectionFactory
using System.IO;
using System.Net.Sockets;
using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.Entity;
using Org.Apache.Http.IO;
using Org.Apache.Http.Impl;
using Org.Apache.Http.Impl.Entity;
using Org.Apache.Http.Impl.IO;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>
	/// Default implementation of
	/// <see cref="Org.Apache.Http.HttpServerConnection">Org.Apache.Http.HttpServerConnection
	/// 	</see>
	/// .
	/// </summary>
	/// <since>4.3</since>
	public class DefaultBHttpServerConnection : BHttpConnectionBase, HttpServerConnection
	{
		private readonly HttpMessageParser<IHttpRequest> requestParser;

		private readonly HttpMessageWriter<HttpResponse> responseWriter;

		/// <summary>Creates new instance of DefaultBHttpServerConnection.</summary>
		/// <remarks>Creates new instance of DefaultBHttpServerConnection.</remarks>
		/// <param name="buffersize">buffer size. Must be a positive number.</param>
		/// <param name="fragmentSizeHint">fragment size hint.</param>
		/// <param name="chardecoder">
		/// decoder to be used for decoding HTTP protocol elements.
		/// If <code>null</code> simple type cast will be used for byte to char conversion.
		/// </param>
		/// <param name="charencoder">
		/// encoder to be used for encoding HTTP protocol elements.
		/// If <code>null</code> simple type cast will be used for char to byte conversion.
		/// </param>
		/// <param name="constraints">
		/// Message constraints. If <code>null</code>
		/// <see cref="Org.Apache.Http.Config.MessageConstraints.Default">Org.Apache.Http.Config.MessageConstraints.Default
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="incomingContentStrategy">
		/// incoming content length strategy. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.Entity.DisallowIdentityContentLengthStrategy.Instance
		/// 	">Org.Apache.Http.Impl.Entity.DisallowIdentityContentLengthStrategy.Instance</see>
		/// will be used.
		/// </param>
		/// <param name="outgoingContentStrategy">
		/// outgoing content length strategy. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.Entity.StrictContentLengthStrategy.Instance">Org.Apache.Http.Impl.Entity.StrictContentLengthStrategy.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="requestParserFactory">
		/// request parser factory. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.IO.DefaultHttpRequestParserFactory.Instance">Org.Apache.Http.Impl.IO.DefaultHttpRequestParserFactory.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="responseWriterFactory">
		/// response writer factory. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.IO.DefaultHttpResponseWriterFactory.Instance">Org.Apache.Http.Impl.IO.DefaultHttpResponseWriterFactory.Instance
		/// 	</see>
		/// will be used.
		/// </param>
        internal DefaultBHttpServerConnection(int buffersize, int fragmentSizeHint, CharsetDecoder
			 chardecoder, CharsetEncoder charencoder, MessageConstraints constraints, ContentLengthStrategy
			 incomingContentStrategy, ContentLengthStrategy outgoingContentStrategy, HttpMessageParserFactory
			<IHttpRequest> requestParserFactory, HttpMessageWriterFactory<HttpResponse> responseWriterFactory
			) : base(buffersize, fragmentSizeHint, chardecoder, charencoder, constraints, incomingContentStrategy
			 != null ? incomingContentStrategy : DisallowIdentityContentLengthStrategy.Instance
			, outgoingContentStrategy)
		{
			this.requestParser = (requestParserFactory != null ? requestParserFactory : DefaultHttpRequestParserFactory
				.Instance).Create(GetSessionInputBuffer(), constraints);
			this.responseWriter = (responseWriterFactory != null ? responseWriterFactory : DefaultHttpResponseWriterFactory
				.Instance).Create(GetSessionOutputBuffer());
		}

        internal DefaultBHttpServerConnection(int buffersize, CharsetDecoder chardecoder, CharsetEncoder
			 charencoder, MessageConstraints constraints) : this(buffersize, buffersize, chardecoder
			, charencoder, constraints, null, null, null, null)
		{
		}

		public DefaultBHttpServerConnection(int buffersize) : this(buffersize, buffersize
			, null, null, null, null, null, null, null)
		{
		}

		protected internal virtual void OnRequestReceived(IHttpRequest request)
		{
		}

		protected internal virtual void OnResponseSubmitted(HttpResponse response)
		{
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal override void Bind(Socket socket)
		{
			base.Bind(socket);
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual IHttpRequest ReceiveRequestHeader()
		{
			EnsureOpen();
			IHttpRequest request = this.requestParser.Parse();
			OnRequestReceived(request);
			IncrementRequestCount();
			return request;
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void ReceiveRequestEntity(HttpEntityEnclosingRequest request)
		{
			Args.NotNull(request, "HTTP request");
			EnsureOpen();
			HttpEntity entity = PrepareInput(request);
			request.SetEntity(entity);
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void SendResponseHeader(HttpResponse response)
		{
			Args.NotNull(response, "HTTP response");
			EnsureOpen();
			this.responseWriter.Write(response);
			OnResponseSubmitted(response);
			if (response.GetStatusLine().GetStatusCode() >= 200)
			{
				IncrementResponseCount();
			}
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void SendResponseEntity(HttpResponse response)
		{
			Args.NotNull(response, "HTTP response");
			EnsureOpen();
			HttpEntity entity = response.GetEntity();
			if (entity == null)
			{
				return;
			}
			OutputStream outstream = PrepareOutput(response);
			entity.WriteTo(outstream);
			outstream.Close();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Flush()
		{
			EnsureOpen();
			DoFlush();
		}
	}
}
#endif