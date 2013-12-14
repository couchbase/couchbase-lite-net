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

using System.Net.Sockets;
using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.Entity;
using Org.Apache.Http.IO;
using Org.Apache.Http.Impl;
using Sharpen;
#if DefaultBHttpClientConnectionFactory
namespace Org.Apache.Http.Impl
{
	/// <summary>
	/// Default factory for
	/// <see cref="Org.Apache.Http.HttpServerConnection">Org.Apache.Http.HttpServerConnection
	/// 	</see>
	/// s.
	/// </summary>
	/// <since>4.3</since>
	public class DefaultBHttpServerConnectionFactory : HttpConnectionFactory<DefaultBHttpServerConnection
		>
	{
		public static readonly Org.Apache.Http.Impl.DefaultBHttpServerConnectionFactory Instance
			 = new Org.Apache.Http.Impl.DefaultBHttpServerConnectionFactory();

		private readonly ConnectionConfig cconfig;

		private readonly ContentLengthStrategy incomingContentStrategy;

		private readonly ContentLengthStrategy outgoingContentStrategy;

		private readonly HttpMessageParserFactory<IHttpRequest> requestParserFactory;

		private readonly HttpMessageWriterFactory<HttpResponse> responseWriterFactory;

		public DefaultBHttpServerConnectionFactory(ConnectionConfig cconfig, ContentLengthStrategy
			 incomingContentStrategy, ContentLengthStrategy outgoingContentStrategy, HttpMessageParserFactory
			<IHttpRequest> requestParserFactory, HttpMessageWriterFactory<HttpResponse> responseWriterFactory
			) : base()
		{
			this.cconfig = cconfig != null ? cconfig : ConnectionConfig.Default;
			this.incomingContentStrategy = incomingContentStrategy;
			this.outgoingContentStrategy = outgoingContentStrategy;
			this.requestParserFactory = requestParserFactory;
			this.responseWriterFactory = responseWriterFactory;
		}

		public DefaultBHttpServerConnectionFactory(ConnectionConfig cconfig, HttpMessageParserFactory
			<IHttpRequest> requestParserFactory, HttpMessageWriterFactory<HttpResponse> responseWriterFactory
			) : this(cconfig, null, null, requestParserFactory, responseWriterFactory)
		{
		}

		public DefaultBHttpServerConnectionFactory(ConnectionConfig cconfig) : this(cconfig
			, null, null, null, null)
		{
		}

		public DefaultBHttpServerConnectionFactory() : this(null, null, null, null, null)
		{
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual DefaultBHttpServerConnection CreateConnection(Socket socket)
		{
			DefaultBHttpServerConnection conn = new DefaultBHttpServerConnection(this.cconfig
				.GetBufferSize(), this.cconfig.GetFragmentSizeHint(), ConnSupport.CreateDecoder(
				this.cconfig), ConnSupport.CreateEncoder(this.cconfig), this.cconfig.GetMessageConstraints
				(), this.incomingContentStrategy, this.outgoingContentStrategy, this.requestParserFactory
				, this.responseWriterFactory);
			conn.Bind(socket);
			return conn;
		}
	}
}
#endif