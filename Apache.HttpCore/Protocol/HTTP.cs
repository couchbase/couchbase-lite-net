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

using System;
using System.Text;
using Org.Apache.Http;
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>Constants and static helpers related to the HTTP protocol.</summary>
	/// <remarks>Constants and static helpers related to the HTTP protocol.</remarks>
	/// <since>4.0</since>
	public sealed class HTTP
	{
		public const int Cr = 13;

		public const int Lf = 10;

		public const int Sp = 32;

		public const int Ht = 9;

		/// <summary>HTTP header definitions</summary>
		public const string TransferEncoding = "Transfer-Encoding";

		public const string ContentLen = "Content-Length";

		public const string ContentType = "Content-Type";

		public const string ContentEncoding = "Content-Encoding";

		public const string ExpectDirective = "Expect";

		public const string ConnDirective = "Connection";

		public const string TargetHost = "Host";

		public const string UserAgent = "User-Agent";

		public const string DateHeader = "Date";

		public const string ServerHeader = "Server";

		/// <summary>HTTP expectations</summary>
		public const string ExpectContinue = "100-continue";

		/// <summary>HTTP connection control</summary>
		public const string ConnClose = "Close";

		public const string ConnKeepAlive = "Keep-Alive";

		/// <summary>Transfer encoding definitions</summary>
		public const string ChunkCoding = "chunked";

		public const string IdentityCoding = "identity";

		public static readonly Encoding DefContentCharset = Consts.Iso88591;

		public static readonly Encoding DefProtocolCharset = Consts.Ascii;

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string Utf8 = "UTF-8";

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string Utf16 = "UTF-16";

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string UsAscii = "US-ASCII";

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string Ascii = "ASCII";

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string Iso88591 = "ISO-8859-1";

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string DefaultContentCharset = Iso88591;

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string DefaultProtocolCharset = UsAscii;

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string OctetStreamType = "application/octet-stream";

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string PlainTextType = "text/plain";

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string CharsetParam = "; charset=";

		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		public const string DefaultContentType = OctetStreamType;

		// <US-ASCII CR, carriage return (13)>
		// <US-ASCII LF, linefeed (10)>
		// <US-ASCII SP, space (32)>
		// <US-ASCII HT, horizontal-tab (9)>
		public static bool IsWhitespace(char ch)
		{
			return ch == Sp || ch == Ht || ch == Cr || ch == Lf;
		}

		private HTTP()
		{
		}
	}
}
