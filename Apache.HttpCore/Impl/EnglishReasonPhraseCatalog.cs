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
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>English reason phrases for HTTP status codes.</summary>
	/// <remarks>
	/// English reason phrases for HTTP status codes.
	/// All status codes defined in RFC1945 (HTTP/1.0), RFC2616 (HTTP/1.1), and
	/// RFC2518 (WebDAV) are supported.
	/// </remarks>
	/// <since>4.0</since>
	public class EnglishReasonPhraseCatalog : ReasonPhraseCatalog
	{
		/// <summary>The default instance of this catalog.</summary>
		/// <remarks>
		/// The default instance of this catalog.
		/// This catalog is thread safe, so there typically
		/// is no need to create other instances.
		/// </remarks>
		public static readonly Org.Apache.Http.Impl.EnglishReasonPhraseCatalog Instance = 
			new Org.Apache.Http.Impl.EnglishReasonPhraseCatalog();

		/// <summary>Restricted default constructor, for derived classes.</summary>
		/// <remarks>
		/// Restricted default constructor, for derived classes.
		/// If you need an instance of this class, use
		/// <see cref="Instance">INSTANCE</see>
		/// .
		/// </remarks>
		protected internal EnglishReasonPhraseCatalog()
		{
		}

		// static array with english reason phrases defined below
		// no body
		/// <summary>Obtains the reason phrase for a status code.</summary>
		/// <remarks>Obtains the reason phrase for a status code.</remarks>
		/// <param name="status">the status code, in the range 100-599</param>
		/// <param name="loc">ignored</param>
		/// <returns>the reason phrase, or <code>null</code></returns>
		public virtual string GetReason(int status, CultureInfo loc)
		{
			Args.Check(status >= 100 && status < 600, "Unknown category for status code " + status
				);
			int category = status / 100;
			int subcode = status - 100 * category;
			string reason = null;
			if (ReasonPhrases[category].Length > subcode)
			{
				reason = ReasonPhrases[category][subcode];
			}
			return reason;
		}

		/// <summary>Reason phrases lookup table.</summary>
		/// <remarks>Reason phrases lookup table.</remarks>
		private static readonly string[][] ReasonPhrases = new string[][] { null, new string
			[3], new string[8], new string[8], new string[25], new string[8] };

		// 1xx
		// 2xx
		// 3xx
		// 4xx
		// 5xx
		/// <summary>Stores the given reason phrase, by status code.</summary>
		/// <remarks>
		/// Stores the given reason phrase, by status code.
		/// Helper method to initialize the static lookup table.
		/// </remarks>
		/// <param name="status">the status code for which to define the phrase</param>
		/// <param name="reason">the reason phrase for this status code</param>
		private static void SetReason(int status, string reason)
		{
			int category = status / 100;
			int subcode = status - 100 * category;
			ReasonPhrases[category][subcode] = reason;
		}

		static EnglishReasonPhraseCatalog()
		{
			// ----------------------------------------------------- Static Initializer
			// HTTP 1.0 Server status codes -- see RFC 1945
			SetReason(HttpStatus.ScOk, "OK");
			SetReason(HttpStatus.ScCreated, "Created");
			SetReason(HttpStatus.ScAccepted, "Accepted");
			SetReason(HttpStatus.ScNoContent, "No Content");
			SetReason(HttpStatus.ScMovedPermanently, "Moved Permanently");
			SetReason(HttpStatus.ScMovedTemporarily, "Moved Temporarily");
			SetReason(HttpStatus.ScNotModified, "Not Modified");
			SetReason(HttpStatus.ScBadRequest, "Bad Request");
			SetReason(HttpStatus.ScUnauthorized, "Unauthorized");
			SetReason(HttpStatus.ScForbidden, "Forbidden");
			SetReason(HttpStatus.ScNotFound, "Not Found");
			SetReason(HttpStatus.ScInternalServerError, "Internal Server Error");
			SetReason(HttpStatus.ScNotImplemented, "Not Implemented");
			SetReason(HttpStatus.ScBadGateway, "Bad Gateway");
			SetReason(HttpStatus.ScServiceUnavailable, "Service Unavailable");
			// HTTP 1.1 Server status codes -- see RFC 2048
			SetReason(HttpStatus.ScContinue, "Continue");
			SetReason(HttpStatus.ScTemporaryRedirect, "Temporary Redirect");
			SetReason(HttpStatus.ScMethodNotAllowed, "Method Not Allowed");
			SetReason(HttpStatus.ScConflict, "Conflict");
			SetReason(HttpStatus.ScPreconditionFailed, "Precondition Failed");
			SetReason(HttpStatus.ScRequestTooLong, "Request Too Long");
			SetReason(HttpStatus.ScRequestUriTooLong, "Request-URI Too Long");
			SetReason(HttpStatus.ScUnsupportedMediaType, "Unsupported Media Type");
			SetReason(HttpStatus.ScMultipleChoices, "Multiple Choices");
			SetReason(HttpStatus.ScSeeOther, "See Other");
			SetReason(HttpStatus.ScUseProxy, "Use Proxy");
			SetReason(HttpStatus.ScPaymentRequired, "Payment Required");
			SetReason(HttpStatus.ScNotAcceptable, "Not Acceptable");
			SetReason(HttpStatus.ScProxyAuthenticationRequired, "Proxy Authentication Required"
				);
			SetReason(HttpStatus.ScRequestTimeout, "Request Timeout");
			SetReason(HttpStatus.ScSwitchingProtocols, "Switching Protocols");
			SetReason(HttpStatus.ScNonAuthoritativeInformation, "Non Authoritative Information"
				);
			SetReason(HttpStatus.ScResetContent, "Reset Content");
			SetReason(HttpStatus.ScPartialContent, "Partial Content");
			SetReason(HttpStatus.ScGatewayTimeout, "Gateway Timeout");
			SetReason(HttpStatus.ScHttpVersionNotSupported, "Http Version Not Supported");
			SetReason(HttpStatus.ScGone, "Gone");
			SetReason(HttpStatus.ScLengthRequired, "Length Required");
			SetReason(HttpStatus.ScRequestedRangeNotSatisfiable, "Requested Range Not Satisfiable"
				);
			SetReason(HttpStatus.ScExpectationFailed, "Expectation Failed");
			// WebDAV Server-specific status codes
			SetReason(HttpStatus.ScProcessing, "Processing");
			SetReason(HttpStatus.ScMultiStatus, "Multi-Status");
			SetReason(HttpStatus.ScUnprocessableEntity, "Unprocessable Entity");
			SetReason(HttpStatus.ScInsufficientSpaceOnResource, "Insufficient Space On Resource"
				);
			SetReason(HttpStatus.ScMethodFailure, "Method Failure");
			SetReason(HttpStatus.ScLocked, "Locked");
			SetReason(HttpStatus.ScInsufficientStorage, "Insufficient Storage");
			SetReason(HttpStatus.ScFailedDependency, "Failed Dependency");
		}
	}
}
