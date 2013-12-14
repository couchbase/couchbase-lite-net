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

using Org.Apache.Http.Concurrent;
using Org.Apache.Http.Pool;
using Sharpen;

namespace Org.Apache.Http.Pool
{
	/// <summary>
	/// <tt>ConnPool</tt> represents a shared pool connections can be leased from
	/// and released back to.
	/// </summary>
	/// <remarks>
	/// <tt>ConnPool</tt> represents a shared pool connections can be leased from
	/// and released back to.
	/// </remarks>
	/// <?></?>
	/// <?></?>
	/// <since>4.2</since>
	public interface ConnPool<T, E>
	{
		/// <summary>
		/// Attempts to lease a connection for the given route and with the given
		/// state from the pool.
		/// </summary>
		/// <remarks>
		/// Attempts to lease a connection for the given route and with the given
		/// state from the pool.
		/// </remarks>
		/// <param name="route">route of the connection.</param>
		/// <param name="state">
		/// arbitrary object that represents a particular state
		/// (usually a security principal or a unique token identifying
		/// the user whose credentials have been used while establishing the connection).
		/// May be <code>null</code>.
		/// </param>
		/// <param name="callback">operation completion callback.</param>
		/// <returns>future for a leased pool entry.</returns>
		Future<E> Lease(T route, object state, FutureCallback<E> callback);

		/// <summary>Releases the pool entry back to the pool.</summary>
		/// <remarks>Releases the pool entry back to the pool.</remarks>
		/// <param name="entry">pool entry leased from the pool</param>
		/// <param name="reusable">
		/// flag indicating whether or not the released connection
		/// is in a consistent state and is safe for further use.
		/// </param>
		void Release(E entry, bool reusable);
	}
}
