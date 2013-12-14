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
	/// A factory for
	/// <see cref="HttpResponse">HttpResponse</see>
	/// objects.
	/// </summary>
	/// <since>4.0</since>
	public interface HttpResponseFactory
	{
		/// <summary>Creates a new response from status line elements.</summary>
		/// <remarks>Creates a new response from status line elements.</remarks>
		/// <param name="ver">the protocol version</param>
		/// <param name="status">the status code</param>
		/// <param name="context">
		/// the context from which to determine the locale
		/// for looking up a reason phrase to the status code, or
		/// <code>null</code> to use the default locale
		/// </param>
		/// <returns>the new response with an initialized status line</returns>
		HttpResponse NewHttpResponse(ProtocolVersion ver, int status, HttpContext context
			);

		/// <summary>Creates a new response from a status line.</summary>
		/// <remarks>Creates a new response from a status line.</remarks>
		/// <param name="statusline">the status line</param>
		/// <param name="context">
		/// the context from which to determine the locale
		/// for looking up a reason phrase if the status code
		/// is updated, or
		/// <code>null</code> to use the default locale
		/// </param>
		/// <returns>the new response with the argument status line</returns>
		HttpResponse NewHttpResponse(StatusLine statusline, HttpContext context);
	}
}
