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

using System.IO;
using Org.Apache.Http;
using Org.Apache.Http.Pool;
using Sharpen;

namespace Org.Apache.Http.Impl.Pool
{
	/// <summary>
	/// A very basic
	/// <see cref="Org.Apache.Http.Pool.PoolEntry{T, C}">Org.Apache.Http.Pool.PoolEntry&lt;T, C&gt;
	/// 	</see>
	/// implementation that represents an entry
	/// in a pool of blocking
	/// <see cref="Org.Apache.Http.HttpClientConnection">Org.Apache.Http.HttpClientConnection
	/// 	</see>
	/// s identified by
	/// an
	/// <see cref="Org.Apache.Http.HttpHost">Org.Apache.Http.HttpHost</see>
	/// instance.
	/// </summary>
	/// <seealso cref="Org.Apache.Http.HttpHost">Org.Apache.Http.HttpHost</seealso>
	/// <since>4.2</since>
	public class BasicPoolEntry : PoolEntry<HttpHost, HttpClientConnection>
	{
		public BasicPoolEntry(string id, HttpHost route, HttpClientConnection conn) : base
			(id, route, conn)
		{
		}

		public override void Close()
		{
			try
			{
				this.GetConnection().Close();
			}
			catch (IOException)
			{
			}
		}

		public override bool IsClosed()
		{
			return !this.GetConnection().IsOpen();
		}
	}
}
