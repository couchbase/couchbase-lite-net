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

using Org.Apache.Http;
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>
	/// Interface for deciding whether a connection can be re-used for
	/// subsequent requests and should be kept alive.
	/// </summary>
	/// <remarks>
	/// Interface for deciding whether a connection can be re-used for
	/// subsequent requests and should be kept alive.
	/// <p>
	/// Implementations of this interface must be thread-safe. Access to shared
	/// data must be synchronized as methods of this interface may be executed
	/// from multiple threads.
	/// </remarks>
	/// <since>4.0</since>
	public interface ConnectionReuseStrategy
	{
		/// <summary>Decides whether a connection can be kept open after a request.</summary>
		/// <remarks>
		/// Decides whether a connection can be kept open after a request.
		/// If this method returns <code>false</code>, the caller MUST
		/// close the connection to correctly comply with the HTTP protocol.
		/// If it returns <code>true</code>, the caller SHOULD attempt to
		/// keep the connection open for reuse with another request.
		/// <br/>
		/// One can use the HTTP context to retrieve additional objects that
		/// may be relevant for the keep-alive strategy: the actual HTTP
		/// connection, the original HTTP request, target host if known,
		/// number of times the connection has been reused already and so on.
		/// <br/>
		/// If the connection is already closed, <code>false</code> is returned.
		/// The stale connection check MUST NOT be triggered by a
		/// connection reuse strategy.
		/// </remarks>
		/// <param name="response">The last response received over that connection.</param>
		/// <param name="context">
		/// the context in which the connection is being
		/// used.
		/// </param>
		/// <returns>
		/// <code>true</code> if the connection is allowed to be reused, or
		/// <code>false</code> if it MUST NOT be reused
		/// </returns>
		bool KeepAlive(HttpResponse response, HttpContext context);
	}
}
