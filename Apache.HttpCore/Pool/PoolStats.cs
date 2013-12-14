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

using System.Text;
using Sharpen;

namespace Org.Apache.Http.Pool
{
	/// <summary>Pool statistics.</summary>
	/// <remarks>
	/// Pool statistics.
	/// <p>
	/// The total number of connections in the pool is equal to
	/// <code>available</code>
	/// plus
	/// <code>leased</code>
	/// .
	/// </p>
	/// </remarks>
	/// <since>4.2</since>
	public class PoolStats
	{
		private readonly int leased;

		private readonly int pending;

		private readonly int available;

		private readonly int max;

		public PoolStats(int leased, int pending, int free, int max) : base()
		{
			this.leased = leased;
			this.pending = pending;
			this.available = free;
			this.max = max;
		}

		/// <summary>
		/// Gets the number of persistent connections tracked by the connection manager currently being used to execute
		/// requests.
		/// </summary>
		/// <remarks>
		/// Gets the number of persistent connections tracked by the connection manager currently being used to execute
		/// requests.
		/// <p>
		/// The total number of connections in the pool is equal to
		/// <code>available</code>
		/// plus
		/// <code>leased</code>
		/// .
		/// </p>
		/// </remarks>
		/// <returns>the number of persistent connections.</returns>
		public virtual int GetLeased()
		{
			return this.leased;
		}

		/// <summary>Gets the number of connection requests being blocked awaiting a free connection.
		/// 	</summary>
		/// <remarks>
		/// Gets the number of connection requests being blocked awaiting a free connection. This can happen only if there
		/// are more worker threads contending for fewer connections.
		/// </remarks>
		/// <returns>the number of connection requests being blocked awaiting a free connection.
		/// 	</returns>
		public virtual int GetPending()
		{
			return this.pending;
		}

		/// <summary>Gets the number idle persistent connections.</summary>
		/// <remarks>
		/// Gets the number idle persistent connections.
		/// <p>
		/// The total number of connections in the pool is equal to
		/// <code>available</code>
		/// plus
		/// <code>leased</code>
		/// .
		/// </p>
		/// </remarks>
		/// <returns>number idle persistent connections.</returns>
		public virtual int GetAvailable()
		{
			return this.available;
		}

		/// <summary>Gets the maximum number of allowed persistent connections.</summary>
		/// <remarks>Gets the maximum number of allowed persistent connections.</remarks>
		/// <returns>the maximum number of allowed persistent connections.</returns>
		public virtual int GetMax()
		{
			return this.max;
		}

		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append("[leased: ");
			buffer.Append(this.leased);
			buffer.Append("; pending: ");
			buffer.Append(this.pending);
			buffer.Append("; available: ");
			buffer.Append(this.available);
			buffer.Append("; max: ");
			buffer.Append(this.max);
			buffer.Append("]");
			return buffer.ToString();
		}
	}
}
