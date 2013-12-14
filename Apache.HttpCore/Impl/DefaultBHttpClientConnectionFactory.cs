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
using System.Net.Sockets;
using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.Entity;
using Org.Apache.Http.IO;
using Org.Apache.Http.Impl;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>
	/// Default factory for
	/// <see cref="Org.Apache.Http.HttpClientConnection">Org.Apache.Http.HttpClientConnection
	/// 	</see>
	/// s.
	/// </summary>
	/// <since>4.3</since>
	public class DefaultBHttpClientConnectionFactory : HttpConnectionFactory<DefaultBHttpClientConnection
		>
	{
		public static readonly Org.Apache.Http.Impl.DefaultBHttpClientConnectionFactory Instance
			 = new Org.Apache.Http.Impl.DefaultBHttpClientConnectionFactory();

		private readonly ConnectionConfig cconfig;

		private readonly ContentLengthStrategy incomingContentStrategy;

		private readonly ContentLengthStrategy outgoingContentStrategy;

		private readonly HttpMessageWriterFactory<IHttpRequest> requestWriterFactory;

		private readonly HttpMessageParserFactory<HttpResponse> responseParserFactory;

		public DefaultBHttpClientConnectionFactory(ConnectionConfig cconfig, ContentLengthStrategy
			 incomingContentStrategy, ContentLengthStrategy outgoingContentStrategy, HttpMessageWriterFactory
			<IHttpRequest> requestWriterFactory, HttpMessageParserFactory<HttpResponse> responseParserFactory
			) : base()
		{
			this.cconfig = cconfig != null ? cconfig : ConnectionConfig.Default;
			this.incomingContentStrategy = incomingContentStrategy;
			this.outgoingContentStrategy = outgoingContentStrategy;
			this.requestWriterFactory = requestWriterFactory;
			this.responseParserFactory = responseParserFactory;
		}

		public DefaultBHttpClientConnectionFactory(ConnectionConfig cconfig, HttpMessageWriterFactory
			<IHttpRequest> requestWriterFactory, HttpMessageParserFactory<HttpResponse> responseParserFactory
			) : this(cconfig, null, null, requestWriterFactory, responseParserFactory)
		{
		}

		public DefaultBHttpClientConnectionFactory(ConnectionConfig cconfig) : this(cconfig
			, null, null, null, null)
		{
		}

		public DefaultBHttpClientConnectionFactory() : this(null, null, null, null, null)
		{
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual DefaultBHttpClientConnection CreateConnection(Socket socket)
		{
			DefaultBHttpClientConnection conn = new DefaultBHttpClientConnection(this.cconfig
				.GetBufferSize(), this.cconfig.GetFragmentSizeHint(), ConnSupport.CreateDecoder(
				this.cconfig), ConnSupport.CreateEncoder(this.cconfig), this.cconfig.GetMessageConstraints
				(), this.incomingContentStrategy, this.outgoingContentStrategy, this.requestWriterFactory
				, this.responseParserFactory);
			conn.Bind(socket);
			return conn;
		}
	}
}
#endif