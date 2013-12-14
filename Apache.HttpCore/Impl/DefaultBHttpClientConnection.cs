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
using Org.Apache.Http.Impl.IO;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>
	/// Default implementation of
	/// <see cref="Org.Apache.Http.HttpClientConnection">Org.Apache.Http.HttpClientConnection
	/// 	</see>
	/// .
	/// </summary>
	/// <since>4.3</since>
	public class DefaultBHttpClientConnection : BHttpConnectionBase, HttpClientConnection
	{
		private readonly HttpMessageParser<HttpResponse> responseParser;

		private readonly HttpMessageWriter<IHttpRequest> requestWriter;

		/// <summary>Creates new instance of DefaultBHttpClientConnection.</summary>
		/// <remarks>Creates new instance of DefaultBHttpClientConnection.</remarks>
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
		/// <see cref="Org.Apache.Http.Impl.Entity.LaxContentLengthStrategy.Instance">Org.Apache.Http.Impl.Entity.LaxContentLengthStrategy.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="outgoingContentStrategy">
		/// outgoing content length strategy. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.Entity.StrictContentLengthStrategy.Instance">Org.Apache.Http.Impl.Entity.StrictContentLengthStrategy.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="requestWriterFactory">
		/// request writer factory. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.IO.DefaultHttpRequestWriterFactory.Instance">Org.Apache.Http.Impl.IO.DefaultHttpRequestWriterFactory.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="responseParserFactory">
		/// response parser factory. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.IO.DefaultHttpResponseParserFactory.Instance">Org.Apache.Http.Impl.IO.DefaultHttpResponseParserFactory.Instance
		/// 	</see>
		/// will be used.
		/// </param>
        internal DefaultBHttpClientConnection(int buffersize, int fragmentSizeHint, CharsetDecoder
			 chardecoder, CharsetEncoder charencoder, MessageConstraints constraints, ContentLengthStrategy
			 incomingContentStrategy, ContentLengthStrategy outgoingContentStrategy, HttpMessageWriterFactory
			<IHttpRequest> requestWriterFactory, HttpMessageParserFactory<HttpResponse> responseParserFactory
			) : base(buffersize, fragmentSizeHint, chardecoder, charencoder, constraints, incomingContentStrategy
			, outgoingContentStrategy)
		{
			this.requestWriter = (requestWriterFactory != null ? requestWriterFactory : DefaultHttpRequestWriterFactory
				.Instance).Create(GetSessionOutputBuffer());
			this.responseParser = (responseParserFactory != null ? responseParserFactory : DefaultHttpResponseParserFactory
				.Instance).Create(GetSessionInputBuffer(), constraints);
		}

        internal DefaultBHttpClientConnection(int buffersize, CharsetDecoder chardecoder, CharsetEncoder
			 charencoder, MessageConstraints constraints) : this(buffersize, buffersize, chardecoder
			, charencoder, constraints, null, null, null, null)
		{
		}

		public DefaultBHttpClientConnection(int buffersize) : this(buffersize, buffersize
			, null, null, null, null, null, null, null)
		{
		}

		protected internal virtual void OnResponseReceived(HttpResponse response)
		{
		}

		protected internal virtual void OnRequestSubmitted(IHttpRequest request)
		{
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal override void Bind(Socket socket)
		{
			base.Bind(socket);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual bool IsResponseAvailable(int timeout)
		{
			EnsureOpen();
			try
			{
				return AwaitInput(timeout);
			}
			catch (SocketTimeoutException)
			{
				return false;
			}
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void SendRequestHeader(IHttpRequest request)
		{
			Args.NotNull(request, "HTTP request");
			EnsureOpen();
			this.requestWriter.Write(request);
			OnRequestSubmitted(request);
			IncrementRequestCount();
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void SendRequestEntity(HttpEntityEnclosingRequest request)
		{
			Args.NotNull(request, "HTTP request");
			EnsureOpen();
			HttpEntity entity = request.GetEntity();
			if (entity == null)
			{
				return;
			}
			OutputStream outstream = PrepareOutput(request);
			entity.WriteTo(outstream);
			outstream.Close();
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual HttpResponse ReceiveResponseHeader()
		{
			EnsureOpen();
			HttpResponse response = this.responseParser.Parse();
			OnResponseReceived(response);
			if (response.GetStatusLine().GetStatusCode() >= HttpStatus.ScOk)
			{
				IncrementResponseCount();
			}
			return response;
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void ReceiveResponseEntity(HttpResponse response)
		{
			Args.NotNull(response, "HTTP response");
			EnsureOpen();
			HttpEntity entity = PrepareInput(response);
			response.SetEntity(entity);
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