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
	/// <summary>Constants enumerating the HTTP status codes.</summary>
	/// <remarks>
	/// Constants enumerating the HTTP status codes.
	/// All status codes defined in RFC1945 (HTTP/1.0), RFC2616 (HTTP/1.1), and
	/// RFC2518 (WebDAV) are listed.
	/// </remarks>
	/// <seealso cref="StatusLine">StatusLine</seealso>
	/// <since>4.0</since>
	public abstract class HttpStatus
	{
		/// <summary><tt>100 Continue</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScContinue = 100;

		/// <summary><tt>101 Switching Protocols</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScSwitchingProtocols = 101;

		/// <summary><tt>102 Processing</tt> (WebDAV - RFC 2518)</summary>
		public const int ScProcessing = 102;

		/// <summary><tt>200 OK</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScOk = 200;

		/// <summary><tt>201 Created</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScCreated = 201;

		/// <summary><tt>202 Accepted</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScAccepted = 202;

		/// <summary><tt>203 Non Authoritative Information</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScNonAuthoritativeInformation = 203;

		/// <summary><tt>204 No Content</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScNoContent = 204;

		/// <summary><tt>205 Reset Content</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScResetContent = 205;

		/// <summary><tt>206 Partial Content</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScPartialContent = 206;

		/// <summary>
		/// <tt>207 Multi-Status</tt> (WebDAV - RFC 2518) or <tt>207 Partial Update
		/// OK</tt> (HTTP/1.1 - draft-ietf-http-v11-spec-rev-01?)
		/// </summary>
		public const int ScMultiStatus = 207;

		/// <summary><tt>300 Mutliple Choices</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScMultipleChoices = 300;

		/// <summary><tt>301 Moved Permanently</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScMovedPermanently = 301;

		/// <summary><tt>302 Moved Temporarily</tt> (Sometimes <tt>Found</tt>) (HTTP/1.0 - RFC 1945)
		/// 	</summary>
		public const int ScMovedTemporarily = 302;

		/// <summary><tt>303 See Other</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScSeeOther = 303;

		/// <summary><tt>304 Not Modified</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScNotModified = 304;

		/// <summary><tt>305 Use Proxy</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScUseProxy = 305;

		/// <summary><tt>307 Temporary Redirect</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScTemporaryRedirect = 307;

		/// <summary><tt>400 Bad Request</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScBadRequest = 400;

		/// <summary><tt>401 Unauthorized</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScUnauthorized = 401;

		/// <summary><tt>402 Payment Required</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScPaymentRequired = 402;

		/// <summary><tt>403 Forbidden</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScForbidden = 403;

		/// <summary><tt>404 Not Found</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScNotFound = 404;

		/// <summary><tt>405 Method Not Allowed</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScMethodNotAllowed = 405;

		/// <summary><tt>406 Not Acceptable</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScNotAcceptable = 406;

		/// <summary><tt>407 Proxy Authentication Required</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScProxyAuthenticationRequired = 407;

		/// <summary><tt>408 Request Timeout</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScRequestTimeout = 408;

		/// <summary><tt>409 Conflict</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScConflict = 409;

		/// <summary><tt>410 Gone</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScGone = 410;

		/// <summary><tt>411 Length Required</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScLengthRequired = 411;

		/// <summary><tt>412 Precondition Failed</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScPreconditionFailed = 412;

		/// <summary><tt>413 Request Entity Too Large</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScRequestTooLong = 413;

		/// <summary><tt>414 Request-URI Too Long</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScRequestUriTooLong = 414;

		/// <summary><tt>415 Unsupported Media Type</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScUnsupportedMediaType = 415;

		/// <summary><tt>416 Requested Range Not Satisfiable</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScRequestedRangeNotSatisfiable = 416;

		/// <summary><tt>417 Expectation Failed</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScExpectationFailed = 417;

		/// <summary>Static constant for a 419 error.</summary>
		/// <remarks>
		/// Static constant for a 419 error.
		/// <tt>419 Insufficient Space on Resource</tt>
		/// (WebDAV - draft-ietf-webdav-protocol-05?)
		/// or <tt>419 Proxy Reauthentication Required</tt>
		/// (HTTP/1.1 drafts?)
		/// </remarks>
		public const int ScInsufficientSpaceOnResource = 419;

		/// <summary>Static constant for a 420 error.</summary>
		/// <remarks>
		/// Static constant for a 420 error.
		/// <tt>420 Method Failure</tt>
		/// (WebDAV - draft-ietf-webdav-protocol-05?)
		/// </remarks>
		public const int ScMethodFailure = 420;

		/// <summary><tt>422 Unprocessable Entity</tt> (WebDAV - RFC 2518)</summary>
		public const int ScUnprocessableEntity = 422;

		/// <summary><tt>423 Locked</tt> (WebDAV - RFC 2518)</summary>
		public const int ScLocked = 423;

		/// <summary><tt>424 Failed Dependency</tt> (WebDAV - RFC 2518)</summary>
		public const int ScFailedDependency = 424;

		/// <summary><tt>500 Server Error</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScInternalServerError = 500;

		/// <summary><tt>501 Not Implemented</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScNotImplemented = 501;

		/// <summary><tt>502 Bad Gateway</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScBadGateway = 502;

		/// <summary><tt>503 Service Unavailable</tt> (HTTP/1.0 - RFC 1945)</summary>
		public const int ScServiceUnavailable = 503;

		/// <summary><tt>504 Gateway Timeout</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScGatewayTimeout = 504;

		/// <summary><tt>505 HTTP Version Not Supported</tt> (HTTP/1.1 - RFC 2616)</summary>
		public const int ScHttpVersionNotSupported = 505;

		/// <summary><tt>507 Insufficient Storage</tt> (WebDAV - RFC 2518)</summary>
		public const int ScInsufficientStorage = 507;
		// --- 1xx Informational ---
		// --- 2xx Success ---
		// --- 3xx Redirection ---
		// --- 4xx Client Error ---
		// not used
		// public static final int SC_UNPROCESSABLE_ENTITY = 418;
		// --- 5xx Server Error ---
	}
}
