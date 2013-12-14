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

#if BASIC_HTTP_CONN_POOL
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Javax.Net;
using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.Impl;
using Org.Apache.Http.Params;
using Org.Apache.Http.Pool;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.Pool
{
	/// <summary>
	/// A very basic
	/// <see cref="Org.Apache.Http.Pool.ConnFactory{T, C}">Org.Apache.Http.Pool.ConnFactory&lt;T, C&gt;
	/// 	</see>
	/// implementation that creates
	/// <see cref="Org.Apache.Http.HttpClientConnection">Org.Apache.Http.HttpClientConnection
	/// 	</see>
	/// instances given a
	/// <see cref="Org.Apache.Http.HttpHost">Org.Apache.Http.HttpHost</see>
	/// instance.
	/// </summary>
	/// <seealso cref="Org.Apache.Http.HttpHost">Org.Apache.Http.HttpHost</seealso>
	/// <since>4.2</since>
	public class BasicConnFactory : ConnFactory<HttpHost, HttpClientConnection>
	{
		private readonly SocketFactory plainfactory;

		private readonly SSLSocketFactory sslfactory;

		private readonly int connectTimeout;

		private readonly SocketConfig sconfig;

		private readonly HttpConnectionFactory<HttpClientConnection> connFactory;

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) useBasicConnFactory(Javax.Net.SocketFactory, Sharpen.SSLSocketFactory, int, Org.Apache.Http.Config.SocketConfig, Org.Apache.Http.Config.ConnectionConfig) ."
			)]
		public BasicConnFactory(SSLSocketFactory sslfactory, HttpParams @params) : base()
		{
			Args.NotNull(@params, "HTTP params");
			this.plainfactory = null;
			this.sslfactory = sslfactory;
			this.connectTimeout = @params.GetIntParameter(CoreConnectionPNames.ConnectionTimeout
				, 0);
			this.sconfig = HttpParamConfig.GetSocketConfig(@params);
			this.connFactory = new DefaultBHttpClientConnectionFactory(HttpParamConfig.GetConnectionConfig
				(@params));
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) useBasicConnFactory(int, Org.Apache.Http.Config.SocketConfig, Org.Apache.Http.Config.ConnectionConfig) ."
			)]
		public BasicConnFactory(HttpParams @params) : this(null, @params)
		{
		}

		/// <since>4.3</since>
		public BasicConnFactory(SocketFactory plainfactory, SSLSocketFactory sslfactory, 
			int connectTimeout, SocketConfig sconfig, ConnectionConfig cconfig) : base()
		{
			this.plainfactory = plainfactory;
			this.sslfactory = sslfactory;
			this.connectTimeout = connectTimeout;
			this.sconfig = sconfig != null ? sconfig : SocketConfig.Default;
			this.connFactory = new DefaultBHttpClientConnectionFactory(cconfig != null ? cconfig
				 : ConnectionConfig.Default);
		}

		/// <since>4.3</since>
		public BasicConnFactory(int connectTimeout, SocketConfig sconfig, ConnectionConfig
			 cconfig) : this(null, null, connectTimeout, sconfig, cconfig)
		{
		}

		/// <since>4.3</since>
		public BasicConnFactory(SocketConfig sconfig, ConnectionConfig cconfig) : this(null
			, null, 0, sconfig, cconfig)
		{
		}

		/// <since>4.3</since>
		public BasicConnFactory() : this(null, null, 0, SocketConfig.Default, ConnectionConfig
			.Default)
		{
		}

		/// <exception cref="System.IO.IOException"></exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) no longer used.")]
		protected internal virtual HttpClientConnection Create(Socket socket, HttpParams 
			@params)
		{
			int bufsize = @params.GetIntParameter(CoreConnectionPNames.SocketBufferSize, 8 * 
				1024);
			DefaultBHttpClientConnection conn = new DefaultBHttpClientConnection(bufsize);
			conn.Bind(socket);
			return conn;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual HttpClientConnection Create(HttpHost host)
		{
			string scheme = host.GetSchemeName();
			Socket socket = null;
			if (Sharpen.Runtime.EqualsIgnoreCase("http", scheme))
			{
				socket = this.plainfactory != null ? this.plainfactory.CreateSocket() : new Socket
					();
			}
			if (Sharpen.Runtime.EqualsIgnoreCase("https", scheme))
			{
				socket = (this.sslfactory != null ? this.sslfactory : SSLSocketFactory.GetDefault
					()).CreateSocket();
			}
			if (socket == null)
			{
				throw new IOException(scheme + " scheme is not supported");
			}
			string hostname = host.GetHostName();
			int port = host.GetPort();
			if (port == -1)
			{
				if (Sharpen.Runtime.EqualsIgnoreCase(host.GetSchemeName(), "http"))
				{
					port = 80;
				}
				else
				{
					if (Sharpen.Runtime.EqualsIgnoreCase(host.GetSchemeName(), "https"))
					{
						port = 443;
					}
				}
			}
			socket.ReceiveTimeout = this.sconfig.GetSoTimeout();
			socket.Connect(new IPEndPoint(hostname, port), this.connectTimeout);
			socket.NoDelay = this.sconfig.IsTcpNoDelay();
			int linger = this.sconfig.GetSoLinger();
			if (linger >= 0)
			{
				socket.SetSoLinger(linger > 0, linger);
			}
			socket.SetKeepAlive(this.sconfig.IsSoKeepAlive());
			return this.connFactory.CreateConnection(socket);
		}
	}
}
#endif