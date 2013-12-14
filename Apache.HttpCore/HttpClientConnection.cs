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
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>
	/// A client-side HTTP connection, which can be used for sending
	/// requests and receiving responses.
	/// </summary>
	/// <remarks>
	/// A client-side HTTP connection, which can be used for sending
	/// requests and receiving responses.
	/// </remarks>
	/// <since>4.0</since>
	public interface HttpClientConnection : HttpConnection
	{
		/// <summary>Checks if response data is available from the connection.</summary>
		/// <remarks>
		/// Checks if response data is available from the connection. May wait for
		/// the specified time until some data becomes available. Note that some
		/// implementations may completely ignore the timeout parameter.
		/// </remarks>
		/// <param name="timeout">the maximum time in milliseconds to wait for data</param>
		/// <returns>
		/// true if data is available; false if there was no data available
		/// even after waiting for <code>timeout</code> milliseconds.
		/// </returns>
		/// <exception cref="System.IO.IOException">if an error happens on the connection</exception>
		bool IsResponseAvailable(int timeout);

		/// <summary>Sends the request line and all headers over the connection.</summary>
		/// <remarks>Sends the request line and all headers over the connection.</remarks>
		/// <param name="request">the request whose headers to send.</param>
		/// <exception cref="HttpException">in case of HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		void SendRequestHeader(IHttpRequest request);

		/// <summary>Sends the request entity over the connection.</summary>
		/// <remarks>Sends the request entity over the connection.</remarks>
		/// <param name="request">the request whose entity to send.</param>
		/// <exception cref="HttpException">in case of HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		void SendRequestEntity(HttpEntityEnclosingRequest request);

		/// <summary>
		/// Receives the request line and headers of the next response available from
		/// this connection.
		/// </summary>
		/// <remarks>
		/// Receives the request line and headers of the next response available from
		/// this connection. The caller should examine the HttpResponse object to
		/// find out if it should try to receive a response entity as well.
		/// </remarks>
		/// <returns>
		/// a new HttpResponse object with status line and headers
		/// initialized.
		/// </returns>
		/// <exception cref="HttpException">in case of HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		HttpResponse ReceiveResponseHeader();

		/// <summary>
		/// Receives the next response entity available from this connection and
		/// attaches it to an existing HttpResponse object.
		/// </summary>
		/// <remarks>
		/// Receives the next response entity available from this connection and
		/// attaches it to an existing HttpResponse object.
		/// </remarks>
		/// <param name="response">the response to attach the entity to</param>
		/// <exception cref="HttpException">in case of HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		void ReceiveResponseEntity(HttpResponse response);

		/// <summary>Writes out all pending buffered data over the open connection.</summary>
		/// <remarks>Writes out all pending buffered data over the open connection.</remarks>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		void Flush();
	}
}
