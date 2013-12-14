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

using Org.Apache.Http.Params;
using Sharpen;

namespace Org.Apache.Http.Params
{
	/// <summary>Defines parameter names for protocol execution in HttpCore.</summary>
	/// <remarks>Defines parameter names for protocol execution in HttpCore.</remarks>
	/// <since>4.0</since>
	[System.ObsoleteAttribute(@"(4.3) use configuration classes provided 'org.apache.http.config' and 'org.apache.http.client.config'"
		)]
	public abstract class CoreProtocolPNames
	{
		/// <summary>
		/// Defines the
		/// <see cref="Org.Apache.Http.ProtocolVersion">Org.Apache.Http.ProtocolVersion</see>
		/// used per default.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="Org.Apache.Http.ProtocolVersion">Org.Apache.Http.ProtocolVersion</see>
		/// .
		/// </p>
		/// </summary>
		public const string ProtocolVersion = "http.protocol.version";

		/// <summary>Defines the charset to be used for encoding HTTP protocol elements.</summary>
		/// <remarks>
		/// Defines the charset to be used for encoding HTTP protocol elements.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="string">string</see>
		/// .
		/// </p>
		/// </remarks>
		public const string HttpElementCharset = "http.protocol.element-charset";

		/// <summary>Defines the charset to be used per default for encoding content body.</summary>
		/// <remarks>
		/// Defines the charset to be used per default for encoding content body.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="string">string</see>
		/// .
		/// </p>
		/// </remarks>
		public const string HttpContentCharset = "http.protocol.content-charset";

		/// <summary>Defines the content of the <code>User-Agent</code> header.</summary>
		/// <remarks>
		/// Defines the content of the <code>User-Agent</code> header.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="string">string</see>
		/// .
		/// </p>
		/// </remarks>
		public const string UserAgent = "http.useragent";

		/// <summary>Defines the content of the <code>Server</code> header.</summary>
		/// <remarks>
		/// Defines the content of the <code>Server</code> header.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="string">string</see>
		/// .
		/// </p>
		/// </remarks>
		public const string OriginServer = "http.origin-server";

		/// <summary>
		/// Defines whether responses with an invalid <code>Transfer-Encoding</code>
		/// header should be rejected.
		/// </summary>
		/// <remarks>
		/// Defines whether responses with an invalid <code>Transfer-Encoding</code>
		/// header should be rejected.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="bool">bool</see>
		/// .
		/// </p>
		/// </remarks>
		public const string StrictTransferEncoding = "http.protocol.strict-transfer-encoding";

		/// <summary>
		/// <p>
		/// Activates 'Expect: 100-Continue' handshake for the
		/// entity enclosing methods.
		/// </summary>
		/// <remarks>
		/// <p>
		/// Activates 'Expect: 100-Continue' handshake for the
		/// entity enclosing methods. The purpose of the 'Expect: 100-Continue'
		/// handshake is to allow a client that is sending a request message with
		/// a request body to determine if the origin server is willing to
		/// accept the request (based on the request headers) before the client
		/// sends the request body.
		/// </p>
		/// <p>
		/// The use of the 'Expect: 100-continue' handshake can result in
		/// a noticeable performance improvement for entity enclosing requests
		/// (such as POST and PUT) that require the target server's
		/// authentication.
		/// </p>
		/// <p>
		/// 'Expect: 100-continue' handshake should be used with
		/// caution, as it may cause problems with HTTP servers and
		/// proxies that do not support HTTP/1.1 protocol.
		/// </p>
		/// This parameter expects a value of type
		/// <see cref="bool">bool</see>
		/// .
		/// </remarks>
		public const string UseExpectContinue = "http.protocol.expect-continue";

		/// <summary>
		/// <p>
		/// Defines the maximum period of time in milliseconds the client should spend
		/// waiting for a 100-continue response.
		/// </summary>
		/// <remarks>
		/// <p>
		/// Defines the maximum period of time in milliseconds the client should spend
		/// waiting for a 100-continue response.
		/// </p>
		/// This parameter expects a value of type
		/// <see cref="int">int</see>
		/// .
		/// </remarks>
		public const string WaitForContinue = "http.protocol.wait-for-continue";

		/// <summary>
		/// <p>
		/// Defines the action to perform upon receiving a malformed input.
		/// </summary>
		/// <remarks>
		/// <p>
		/// Defines the action to perform upon receiving a malformed input. If the input byte sequence
		/// is not legal for this charset then the input is said to be malformed
		/// </p>
		/// This parameter expects a value of type
		/// <see cref="Sharpen.CodingErrorAction">Sharpen.CodingErrorAction</see>
		/// </remarks>
		/// <since>4.2</since>
		public const string HttpMalformedInputAction = "http.malformed.input.action";

		/// <summary>
		/// <p>
		/// Defines the action to perform upon receiving an unmappable input.
		/// </summary>
		/// <remarks>
		/// <p>
		/// Defines the action to perform upon receiving an unmappable input. If the input byte sequence
		/// is legal but cannot be mapped to a valid Unicode character then the input is said to be
		/// unmappable
		/// </p>
		/// This parameter expects a value of type
		/// <see cref="Sharpen.CodingErrorAction">Sharpen.CodingErrorAction</see>
		/// </remarks>
		/// <since>4.2</since>
		public const string HttpUnmappableInputAction = "http.unmappable.input.action";
	}
}
