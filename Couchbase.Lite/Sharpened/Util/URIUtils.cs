/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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
using System.IO;
using System.Text;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
	public class URIUtils
	{
		/// <summary>Index of a component which was not found.</summary>
		/// <remarks>Index of a component which was not found.</remarks>
		private const int NotFound = -1;

		/// <summary>Default encoding.</summary>
		/// <remarks>Default encoding.</remarks>
		private const string Utf8Encoding = "UTF-8";

		/// <summary>
		/// Error message presented when a user tries to treat an opaque URI as
		/// hierarchical.
		/// </summary>
		/// <remarks>
		/// Error message presented when a user tries to treat an opaque URI as
		/// hierarchical.
		/// </remarks>
		private const string NotHierarchical = "This isn't a hierarchical URI.";

		// COPY: Partially copied from android.net.Uri
		// COPY: Partially copied from libcore.net.UriCodec
		public static string Decode(string s)
		{
			if (s == null)
			{
				return null;
			}
			try
			{
				return URLDecoder.Decode(s, Utf8Encoding);
			}
			catch (UnsupportedEncodingException e)
			{
				// This is highly unlikely since we always use UTF-8 encoding.
				throw new RuntimeException(e);
			}
		}

		/// <summary>Searches the query string for the first value with the given key.</summary>
		/// <remarks>Searches the query string for the first value with the given key.</remarks>
		/// <param name="key">which will be encoded</param>
		/// <exception cref="System.NotSupportedException">if this isn't a hierarchical URI</exception>
		/// <exception cref="System.ArgumentNullException">if key is null</exception>
		/// <returns>the decoded value or null if no parameter is found</returns>
		public static string GetQueryParameter(URI uri, string key)
		{
			if (uri.IsOpaque())
			{
				throw new NotSupportedException(NotHierarchical);
			}
			if (key == null)
			{
				throw new ArgumentNullException("key");
			}
			string query = uri.GetRawQuery();
			if (query == null)
			{
				return null;
			}
			string encodedKey = Encode(key, null);
			int length = query.Length;
			int start = 0;
			do
			{
				int nextAmpersand = query.IndexOf('&', start);
				int end = nextAmpersand != -1 ? nextAmpersand : length;
				int separator = query.IndexOf('=', start);
				if (separator > end || separator == -1)
				{
					separator = end;
				}
				if (separator - start == encodedKey.Length && query.RegionMatches(start, encodedKey
					, 0, encodedKey.Length))
				{
					if (separator == end)
					{
						return string.Empty;
					}
					else
					{
						string encodedValue = Sharpen.Runtime.Substring(query, separator + 1, end);
						return Decode(encodedValue, true, Sharpen.Extensions.GetEncoding(Utf8Encoding));
					}
				}
				// Move start to end of name.
				if (nextAmpersand != -1)
				{
					start = nextAmpersand + 1;
				}
				else
				{
					break;
				}
			}
			while (true);
			return null;
		}

		private static readonly char[] HexDigits = "0123456789ABCDEF".ToCharArray();

		/// <summary>
		/// Encodes characters in the given string as '%'-escaped octets
		/// using the UTF-8 scheme.
		/// </summary>
		/// <remarks>
		/// Encodes characters in the given string as '%'-escaped octets
		/// using the UTF-8 scheme. Leaves letters ("A-Z", "a-z"), numbers
		/// ("0-9"), and unreserved characters ("_-!.~'()*") intact. Encodes
		/// all other characters.
		/// </remarks>
		/// <param name="s">string to encode</param>
		/// <returns>
		/// an encoded version of s suitable for use as a URI component,
		/// or null if s is null
		/// </returns>
		public static string Encode(string s)
		{
			return Encode(s, null);
		}

		/// <summary>
		/// Encodes characters in the given string as '%'-escaped octets
		/// using the UTF-8 scheme.
		/// </summary>
		/// <remarks>
		/// Encodes characters in the given string as '%'-escaped octets
		/// using the UTF-8 scheme. Leaves letters ("A-Z", "a-z"), numbers
		/// ("0-9"), and unreserved characters ("_-!.~'()*") intact. Encodes
		/// all other characters with the exception of those specified in the
		/// allow argument.
		/// </remarks>
		/// <param name="s">string to encode</param>
		/// <param name="allow">
		/// set of additional characters to allow in the encoded form,
		/// null if no characters should be skipped
		/// </param>
		/// <returns>
		/// an encoded version of s suitable for use as a URI component,
		/// or null if s is null
		/// </returns>
		public static string Encode(string s, string allow)
		{
			if (s == null)
			{
				return null;
			}
			// Lazily-initialized buffers.
			StringBuilder encoded = null;
			int oldLength = s.Length;
			// This loop alternates between copying over allowed characters and
			// encoding in chunks. This results in fewer method calls and
			// allocations than encoding one character at a time.
			int current = 0;
			while (current < oldLength)
			{
				// Start in "copying" mode where we copy over allowed chars.
				// Find the next character which needs to be encoded.
				int nextToEncode = current;
				while (nextToEncode < oldLength && IsAllowed(s[nextToEncode], allow))
				{
					nextToEncode++;
				}
				// If there's nothing more to encode...
				if (nextToEncode == oldLength)
				{
					if (current == 0)
					{
						// We didn't need to encode anything!
						return s;
					}
					else
					{
						// Presumably, we've already done some encoding.
						encoded.AppendRange(s, current, oldLength);
						return encoded.ToString();
					}
				}
				if (encoded == null)
				{
					encoded = new StringBuilder();
				}
				if (nextToEncode > current)
				{
					// Append allowed characters leading up to this point.
					encoded.AppendRange(s, current, nextToEncode);
				}
				// assert nextToEncode == current
				// Switch to "encoding" mode.
				// Find the next allowed character.
				current = nextToEncode;
				int nextAllowed = current + 1;
				while (nextAllowed < oldLength && !IsAllowed(s[nextAllowed], allow))
				{
					nextAllowed++;
				}
				// Convert the substring to bytes and encode the bytes as
				// '%'-escaped octets.
				string toEncode = Sharpen.Runtime.Substring(s, current, nextAllowed);
				try
				{
					byte[] bytes = Sharpen.Runtime.GetBytesForString(toEncode, Utf8Encoding);
					int bytesLength = bytes.Length;
					for (int i = 0; i < bytesLength; i++)
					{
						encoded.Append('%');
						encoded.Append(HexDigits[(bytes[i] & unchecked((int)(0xf0))) >> 4]);
						encoded.Append(HexDigits[bytes[i] & unchecked((int)(0xf))]);
					}
				}
				catch (UnsupportedEncodingException e)
				{
					throw new Exception(e);
				}
				current = nextAllowed;
			}
			// Encoded could still be null at this point if s is empty.
			return encoded == null ? s : encoded.ToString();
		}

		/// <summary>Returns true if the given character is allowed.</summary>
		/// <remarks>Returns true if the given character is allowed.</remarks>
		/// <param name="c">character to check</param>
		/// <param name="allow">characters to allow</param>
		/// <returns>
		/// true if the character is allowed or false if it should be
		/// encoded
		/// </returns>
		private static bool IsAllowed(char c, string allow)
		{
			return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
				 || "_-!.~'()*".IndexOf(c) != NotFound || (allow != null && allow.IndexOf(c) != 
				NotFound);
		}

		// COPY: Copied from libcore.net.UriCodec
		/// <param name="convertPlus">true to convert '+' to ' '.</param>
		public static string Decode(string s, bool convertPlus, Encoding charset)
		{
			if (s.IndexOf('%') == -1 && (!convertPlus || s.IndexOf('+') == -1))
			{
				return s;
			}
			StringBuilder result = new StringBuilder(s.Length);
			ByteArrayOutputStream @out = new ByteArrayOutputStream();
			for (int i = 0; i < s.Length; )
			{
				char c = s[i];
				if (c == '%')
				{
					do
					{
						if (i + 2 >= s.Length)
						{
							throw new ArgumentException("Incomplete % sequence at: " + i);
						}
						int d1 = HexToInt(s[i + 1]);
						int d2 = HexToInt(s[i + 2]);
						if (d1 == -1 || d2 == -1)
						{
							throw new ArgumentException("Invalid % sequence " + Sharpen.Runtime.Substring(s, 
								i, i + 3) + " at " + i);
						}
						@out.Write(unchecked((byte)((d1 << 4) + d2)));
						i += 3;
					}
					while (i < s.Length && s[i] == '%');
					result.Append(new string(@out.ToByteArray(), charset));
					@out.Reset();
				}
				else
				{
					if (convertPlus && c == '+')
					{
						c = ' ';
					}
					result.Append(c);
					i++;
				}
			}
			return result.ToString();
		}

		// COPY: Copied from libcore.net.UriCodec
		/// <summary>
		/// Like
		/// <see cref="char.Digit(char, int)">char.Digit(char, int)</see>
		/// , but without support for non-ASCII
		/// characters.
		/// </summary>
		private static int HexToInt(char c)
		{
			if ('0' <= c && c <= '9')
			{
				return c - '0';
			}
			else
			{
				if ('a' <= c && c <= 'f')
				{
					return 10 + (c - 'a');
				}
				else
				{
					if ('A' <= c && c <= 'F')
					{
						return 10 + (c - 'A');
					}
					else
					{
						return -1;
					}
				}
			}
		}
	}
}
