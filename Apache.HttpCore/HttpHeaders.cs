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

using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>Constants enumerating the HTTP headers.</summary>
	/// <remarks>
	/// Constants enumerating the HTTP headers. All headers defined in RFC1945 (HTTP/1.0), RFC2616 (HTTP/1.1), and RFC2518
	/// (WebDAV) are listed.
	/// </remarks>
	/// <since>4.1</since>
	public sealed class HttpHeaders
	{
		private HttpHeaders()
		{
		}

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.1</summary>
		public const string Accept = "Accept";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.2</summary>
		public const string AcceptCharset = "Accept-Charset";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.3</summary>
		public const string AcceptEncoding = "Accept-Encoding";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.4</summary>
		public const string AcceptLanguage = "Accept-Language";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.5</summary>
		public const string AcceptRanges = "Accept-Ranges";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.6</summary>
		public const string Age = "Age";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.1, RFC 2616 (HTTP/1.1) Section 14.7</summary>
		public const string Allow = "Allow";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.2, RFC 2616 (HTTP/1.1) Section 14.8</summary>
		public const string Authorization = "Authorization";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.9</summary>
		public const string CacheControl = "Cache-Control";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.10</summary>
		public const string Connection = "Connection";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.3, RFC 2616 (HTTP/1.1) Section 14.11</summary>
		public const string ContentEncoding = "Content-Encoding";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.12</summary>
		public const string ContentLanguage = "Content-Language";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.4, RFC 2616 (HTTP/1.1) Section 14.13</summary>
		public const string ContentLength = "Content-Length";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.14</summary>
		public const string ContentLocation = "Content-Location";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.15</summary>
		public const string ContentMd5 = "Content-MD5";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.16</summary>
		public const string ContentRange = "Content-Range";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.5, RFC 2616 (HTTP/1.1) Section 14.17</summary>
		public const string ContentType = "Content-Type";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.6, RFC 2616 (HTTP/1.1) Section 14.18</summary>
		public const string Date = "Date";

		/// <summary>RFC 2518 (WevDAV) Section 9.1</summary>
		public const string Dav = "Dav";

		/// <summary>RFC 2518 (WevDAV) Section 9.2</summary>
		public const string Depth = "Depth";

		/// <summary>RFC 2518 (WevDAV) Section 9.3</summary>
		public const string Destination = "Destination";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.19</summary>
		public const string Etag = "ETag";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.20</summary>
		public const string Expect = "Expect";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.7, RFC 2616 (HTTP/1.1) Section 14.21</summary>
		public const string Expires = "Expires";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.8, RFC 2616 (HTTP/1.1) Section 14.22</summary>
		public const string From = "From";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.23</summary>
		public const string Host = "Host";

		/// <summary>RFC 2518 (WevDAV) Section 9.4</summary>
		public const string If = "If";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.24</summary>
		public const string IfMatch = "If-Match";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.9, RFC 2616 (HTTP/1.1) Section 14.25</summary>
		public const string IfModifiedSince = "If-Modified-Since";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.26</summary>
		public const string IfNoneMatch = "If-None-Match";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.27</summary>
		public const string IfRange = "If-Range";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.28</summary>
		public const string IfUnmodifiedSince = "If-Unmodified-Since";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.10, RFC 2616 (HTTP/1.1) Section 14.29</summary>
		public const string LastModified = "Last-Modified";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.11, RFC 2616 (HTTP/1.1) Section 14.30</summary>
		public const string Location = "Location";

		/// <summary>RFC 2518 (WevDAV) Section 9.5</summary>
		public const string LockToken = "Lock-Token";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.31</summary>
		public const string MaxForwards = "Max-Forwards";

		/// <summary>RFC 2518 (WevDAV) Section 9.6</summary>
		public const string Overwrite = "Overwrite";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.12, RFC 2616 (HTTP/1.1) Section 14.32</summary>
		public const string Pragma = "Pragma";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.33</summary>
		public const string ProxyAuthenticate = "Proxy-Authenticate";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.34</summary>
		public const string ProxyAuthorization = "Proxy-Authorization";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.35</summary>
		public const string Range = "Range";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.13, RFC 2616 (HTTP/1.1) Section 14.36</summary>
		public const string Referer = "Referer";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.37</summary>
		public const string RetryAfter = "Retry-After";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.14, RFC 2616 (HTTP/1.1) Section 14.38</summary>
		public const string Server = "Server";

		/// <summary>RFC 2518 (WevDAV) Section 9.7</summary>
		public const string StatusUri = "Status-URI";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.39</summary>
		public const string Te = "TE";

		/// <summary>RFC 2518 (WevDAV) Section 9.8</summary>
		public const string Timeout = "Timeout";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.40</summary>
		public const string Trailer = "Trailer";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.41</summary>
		public const string TransferEncoding = "Transfer-Encoding";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.42</summary>
		public const string Upgrade = "Upgrade";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.15, RFC 2616 (HTTP/1.1) Section 14.43</summary>
		public const string UserAgent = "User-Agent";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.44</summary>
		public const string Vary = "Vary";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.45</summary>
		public const string Via = "Via";

		/// <summary>RFC 2616 (HTTP/1.1) Section 14.46</summary>
		public const string Warning = "Warning";

		/// <summary>RFC 1945 (HTTP/1.0) Section 10.16, RFC 2616 (HTTP/1.1) Section 14.47</summary>
		public const string WwwAuthenticate = "WWW-Authenticate";
	}
}
