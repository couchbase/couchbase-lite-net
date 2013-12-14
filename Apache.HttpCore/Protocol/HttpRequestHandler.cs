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

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// HttpRequestHandler represents a routine for processing of a specific group
	/// of HTTP requests.
	/// </summary>
	/// <remarks>
	/// HttpRequestHandler represents a routine for processing of a specific group
	/// of HTTP requests. Protocol handlers are designed to take care of protocol
	/// specific aspects, whereas individual request handlers are expected to take
	/// care of application specific HTTP processing. The main purpose of a request
	/// handler is to generate a response object with a content entity to be sent
	/// back to the client in response to the given request
	/// </remarks>
	/// <since>4.0</since>
	public interface HttpRequestHandler
	{
		/// <summary>
		/// Handles the request and produces a response to be sent back to
		/// the client.
		/// </summary>
		/// <remarks>
		/// Handles the request and produces a response to be sent back to
		/// the client.
		/// </remarks>
		/// <param name="request">the HTTP request.</param>
		/// <param name="response">the HTTP response.</param>
		/// <param name="context">the HTTP execution context.</param>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">
		/// in case of HTTP protocol violation or a processing
		/// problem.
		/// </exception>
		void Handle(IHttpRequest request, HttpResponse response, HttpContext context);
	}
}
