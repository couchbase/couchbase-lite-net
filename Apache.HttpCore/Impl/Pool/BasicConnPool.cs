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
using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.Impl.Pool;
using Org.Apache.Http.Params;
using Org.Apache.Http.Pool;
using Sharpen;

namespace Org.Apache.Http.Impl.Pool
{
	/// <summary>
	/// A very basic
	/// <see cref="Org.Apache.Http.Pool.ConnPool{T, E}">Org.Apache.Http.Pool.ConnPool&lt;T, E&gt;
	/// 	</see>
	/// implementation that
	/// represents a pool of blocking
	/// <see cref="Org.Apache.Http.HttpClientConnection">Org.Apache.Http.HttpClientConnection
	/// 	</see>
	/// connections
	/// identified by an
	/// <see cref="Org.Apache.Http.HttpHost">Org.Apache.Http.HttpHost</see>
	/// instance. Please note this pool
	/// implementation does not support complex routes via a proxy cannot
	/// differentiate between direct and proxied connections.
	/// </summary>
	/// <seealso cref="Org.Apache.Http.HttpHost">Org.Apache.Http.HttpHost</seealso>
	/// <since>4.2</since>
	public class BasicConnPool : AbstractConnPool<HttpHost, HttpClientConnection, BasicPoolEntry
		>
	{
		private static readonly AtomicLong Counter = new AtomicLong();

		public BasicConnPool(ConnFactory<HttpHost, HttpClientConnection> connFactory) : base
			(connFactory, 2, 20)
		{
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use BasicConnPool(Org.Apache.Http.Config.SocketConfig, Org.Apache.Http.Config.ConnectionConfig)"
			)]
		public BasicConnPool(HttpParams @params) : base(new BasicConnFactory(@params), 2, 
			20)
		{
		}

		/// <since>4.3</since>
		public BasicConnPool(SocketConfig sconfig, ConnectionConfig cconfig) : base(new BasicConnFactory
			(sconfig, cconfig), 2, 20)
		{
		}

		/// <since>4.3</since>
		public BasicConnPool() : base(new BasicConnFactory(SocketConfig.Default, ConnectionConfig
			.Default), 2, 20)
		{
		}

		protected internal override BasicPoolEntry CreateEntry(HttpHost host, HttpClientConnection
			 conn)
		{
			return new BasicPoolEntry(System.Convert.ToString(Counter.GetAndIncrement()), host
				, conn);
		}
	}
}
#endif
