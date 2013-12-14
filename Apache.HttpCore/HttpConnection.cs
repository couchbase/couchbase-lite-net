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

using System;
using Org.Apache.Http;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>A generic HTTP connection, useful on client and server side.</summary>
	/// <remarks>A generic HTTP connection, useful on client and server side.</remarks>
	/// <since>4.0</since>
	public interface HttpConnection : IDisposable
	{
		/// <summary>Closes this connection gracefully.</summary>
		/// <remarks>
		/// Closes this connection gracefully.
		/// This method will attempt to flush the internal output
		/// buffer prior to closing the underlying socket.
		/// This method MUST NOT be called from a different thread to force
		/// shutdown of the connection. Use
		/// <see cref="Shutdown()">shutdown</see>
		/// instead.
		/// </remarks>
		/// <exception cref="System.IO.IOException"></exception>
		void Close();

		/// <summary>Checks if this connection is open.</summary>
		/// <remarks>Checks if this connection is open.</remarks>
		/// <returns>true if it is open, false if it is closed.</returns>
		bool IsOpen();

		/// <summary>Checks whether this connection has gone down.</summary>
		/// <remarks>
		/// Checks whether this connection has gone down.
		/// Network connections may get closed during some time of inactivity
		/// for several reasons. The next time a read is attempted on such a
		/// connection it will throw an IOException.
		/// This method tries to alleviate this inconvenience by trying to
		/// find out if a connection is still usable. Implementations may do
		/// that by attempting a read with a very small timeout. Thus this
		/// method may block for a small amount of time before returning a result.
		/// It is therefore an <i>expensive</i> operation.
		/// </remarks>
		/// <returns>
		/// <code>true</code> if attempts to use this connection are
		/// likely to succeed, or <code>false</code> if they are likely
		/// to fail and this connection should be closed
		/// </returns>
		bool IsStale();

		/// <summary>Sets the socket timeout value.</summary>
		/// <remarks>Sets the socket timeout value.</remarks>
		/// <param name="timeout">timeout value in milliseconds</param>
		void SetSocketTimeout(int timeout);

		/// <summary>Returns the socket timeout value.</summary>
		/// <remarks>Returns the socket timeout value.</remarks>
		/// <returns>
		/// positive value in milliseconds if a timeout is set,
		/// <code>0</code> if timeout is disabled or <code>-1</code> if
		/// timeout is undefined.
		/// </returns>
		int GetSocketTimeout();

		/// <summary>Force-closes this connection.</summary>
		/// <remarks>
		/// Force-closes this connection.
		/// This is the only method of a connection which may be called
		/// from a different thread to terminate the connection.
		/// This method will not attempt to flush the transmitter's
		/// internal buffer prior to closing the underlying socket.
		/// </remarks>
		/// <exception cref="System.IO.IOException"></exception>
		void Shutdown();

		/// <summary>Returns a collection of connection metrics.</summary>
		/// <remarks>Returns a collection of connection metrics.</remarks>
		/// <returns>HttpConnectionMetrics</returns>
		HttpConnectionMetrics GetMetrics();
	}
}
