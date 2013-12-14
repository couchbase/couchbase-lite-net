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

using System.Globalization;
using Org.Apache.Http;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>
	/// After receiving and interpreting a request message, a server responds
	/// with an HTTP response message.
	/// </summary>
	/// <remarks>
	/// After receiving and interpreting a request message, a server responds
	/// with an HTTP response message.
	/// <pre>
	/// Response      = Status-Line
	/// *(( general-header
	/// | response-header
	/// | entity-header ) CRLF)
	/// CRLF
	/// [ message-body ]
	/// </pre>
	/// </remarks>
	/// <since>4.0</since>
	public interface HttpResponse : HttpMessage
	{
		/// <summary>Obtains the status line of this response.</summary>
		/// <remarks>
		/// Obtains the status line of this response.
		/// The status line can be set using one of the
		/// <see cref="SetStatusLine(StatusLine)">setStatusLine</see>
		/// methods,
		/// or it can be initialized in a constructor.
		/// </remarks>
		/// <returns>the status line, or <code>null</code> if not yet set</returns>
		StatusLine GetStatusLine();

		/// <summary>Sets the status line of this response.</summary>
		/// <remarks>Sets the status line of this response.</remarks>
		/// <param name="statusline">the status line of this response</param>
		void SetStatusLine(StatusLine statusline);

		/// <summary>Sets the status line of this response.</summary>
		/// <remarks>
		/// Sets the status line of this response.
		/// The reason phrase will be determined based on the current
		/// <see cref="GetLocale()">locale</see>
		/// .
		/// </remarks>
		/// <param name="ver">the HTTP version</param>
		/// <param name="code">the status code</param>
		void SetStatusLine(ProtocolVersion ver, int code);

		/// <summary>Sets the status line of this response with a reason phrase.</summary>
		/// <remarks>Sets the status line of this response with a reason phrase.</remarks>
		/// <param name="ver">the HTTP version</param>
		/// <param name="code">the status code</param>
		/// <param name="reason">the reason phrase, or <code>null</code> to omit</param>
		void SetStatusLine(ProtocolVersion ver, int code, string reason);

		/// <summary>Updates the status line of this response with a new status code.</summary>
		/// <remarks>Updates the status line of this response with a new status code.</remarks>
		/// <param name="code">the HTTP status code.</param>
		/// <exception cref="System.InvalidOperationException">if the status line has not be set
		/// 	</exception>
		/// <seealso cref="HttpStatus">HttpStatus</seealso>
		/// <seealso cref="SetStatusLine(StatusLine)">SetStatusLine(StatusLine)</seealso>
		/// <seealso cref="SetStatusLine(ProtocolVersion, int)">SetStatusLine(ProtocolVersion, int)
		/// 	</seealso>
		void SetStatusCode(int code);

		/// <summary>Updates the status line of this response with a new reason phrase.</summary>
		/// <remarks>Updates the status line of this response with a new reason phrase.</remarks>
		/// <param name="reason">
		/// the new reason phrase as a single-line string, or
		/// <code>null</code> to unset the reason phrase
		/// </param>
		/// <exception cref="System.InvalidOperationException">if the status line has not be set
		/// 	</exception>
		/// <seealso cref="SetStatusLine(StatusLine)">SetStatusLine(StatusLine)</seealso>
		/// <seealso cref="SetStatusLine(ProtocolVersion, int)">SetStatusLine(ProtocolVersion, int)
		/// 	</seealso>
		void SetReasonPhrase(string reason);

		/// <summary>Obtains the message entity of this response, if any.</summary>
		/// <remarks>
		/// Obtains the message entity of this response, if any.
		/// The entity is provided by calling
		/// <see cref="SetEntity(HttpEntity)">setEntity</see>
		/// .
		/// </remarks>
		/// <returns>
		/// the response entity, or
		/// <code>null</code> if there is none
		/// </returns>
		HttpEntity GetEntity();

		/// <summary>Associates a response entity with this response.</summary>
		/// <remarks>
		/// Associates a response entity with this response.
		/// <p/>
		/// Please note that if an entity has already been set for this response and it depends on
		/// an input stream (
		/// <see cref="HttpEntity.IsStreaming()">HttpEntity.IsStreaming()</see>
		/// returns <code>true</code>),
		/// it must be fully consumed in order to ensure release of resources.
		/// </remarks>
		/// <param name="entity">
		/// the entity to associate with this response, or
		/// <code>null</code> to unset
		/// </param>
		/// <seealso cref="HttpEntity.IsStreaming()">HttpEntity.IsStreaming()</seealso>
		/// <seealso cref="Org.Apache.Http.Util.EntityUtils.UpdateEntity(HttpResponse, HttpEntity)
		/// 	">Org.Apache.Http.Util.EntityUtils.UpdateEntity(HttpResponse, HttpEntity)</seealso>
		void SetEntity(HttpEntity entity);

		/// <summary>Obtains the locale of this response.</summary>
		/// <remarks>
		/// Obtains the locale of this response.
		/// The locale is used to determine the reason phrase
		/// for the
		/// <see cref="SetStatusCode(int)">status code</see>
		/// .
		/// It can be changed using
		/// <see cref="SetLocale(System.Globalization.CultureInfo)">setLocale</see>
		/// .
		/// </remarks>
		/// <returns>the locale of this response, never <code>null</code></returns>
		CultureInfo GetLocale();

		/// <summary>Changes the locale of this response.</summary>
		/// <remarks>Changes the locale of this response.</remarks>
		/// <param name="loc">the new locale</param>
		void SetLocale(CultureInfo loc);
	}
}
