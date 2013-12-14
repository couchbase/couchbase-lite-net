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
	/// HTTP protocol interceptor is a routine that implements a specific aspect of
	/// the HTTP protocol.
	/// </summary>
	/// <remarks>
	/// HTTP protocol interceptor is a routine that implements a specific aspect of
	/// the HTTP protocol. Usually protocol interceptors are expected to act upon
	/// one specific header or a group of related headers of the incoming message
	/// or populate the outgoing message with one specific header or a group of
	/// related headers.
	/// <p>
	/// Protocol Interceptors can also manipulate content entities enclosed with messages.
	/// Usually this is accomplished by using the 'Decorator' pattern where a wrapper
	/// entity class is used to decorate the original entity.
	/// <p>
	/// Protocol interceptors must be implemented as thread-safe. Similarly to
	/// servlets, protocol interceptors should not use instance variables unless
	/// access to those variables is synchronized.
	/// </remarks>
	/// <since>4.0</since>
	public interface IHttpRequestInterceptor
	{
		/// <summary>Processes a request.</summary>
		/// <remarks>
		/// Processes a request.
		/// On the client side, this step is performed before the request is
		/// sent to the server. On the server side, this step is performed
		/// on incoming messages before the message body is evaluated.
		/// </remarks>
		/// <param name="request">the request to preprocess</param>
		/// <param name="context">the context for the request</param>
		/// <exception cref="HttpException">in case of an HTTP protocol violation</exception>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		void Process(IHttpRequest request, HttpContext context);
	}
}
