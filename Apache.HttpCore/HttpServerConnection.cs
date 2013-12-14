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
	/// A server-side HTTP connection, which can be used for receiving
	/// requests and sending responses.
	/// </summary>
	/// <remarks>
	/// A server-side HTTP connection, which can be used for receiving
	/// requests and sending responses.
	/// </remarks>
	/// <since>4.0</since>
	public interface HttpServerConnection : HttpConnection
	{
		/// <summary>Receives the request line and all headers available from this connection.
		/// 	</summary>
		/// <remarks>
		/// Receives the request line and all headers available from this connection.
		/// The caller should examine the returned request and decide if to receive a
		/// request entity as well.
		/// </remarks>
		/// <returns>
		/// a new HttpRequest object whose request line and headers are
		/// initialized.
		/// </returns>
		/// <exception cref="HttpException">in case of HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		IHttpRequest ReceiveRequestHeader();

		/// <summary>
		/// Receives the next request entity available from this connection and attaches it to
		/// an existing request.
		/// </summary>
		/// <remarks>
		/// Receives the next request entity available from this connection and attaches it to
		/// an existing request.
		/// </remarks>
		/// <param name="request">the request to attach the entity to.</param>
		/// <exception cref="HttpException">in case of HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		void ReceiveRequestEntity(HttpEntityEnclosingRequest request);

		/// <summary>Sends the response line and headers of a response over this connection.</summary>
		/// <remarks>Sends the response line and headers of a response over this connection.</remarks>
		/// <param name="response">the response whose headers to send.</param>
		/// <exception cref="HttpException">in case of HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		void SendResponseHeader(HttpResponse response);

		/// <summary>Sends the response entity of a response over this connection.</summary>
		/// <remarks>Sends the response entity of a response over this connection.</remarks>
		/// <param name="response">the response whose entity to send.</param>
		/// <exception cref="HttpException">in case of HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		void SendResponseEntity(HttpResponse response);

		/// <summary>Sends all pending buffered data over this connection.</summary>
		/// <remarks>Sends all pending buffered data over this connection.</remarks>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		void Flush();
	}
}
