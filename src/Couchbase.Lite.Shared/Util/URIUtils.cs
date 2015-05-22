//
// URIUtils.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Text;

using Sharpen;
using System.Linq;
using System.IO;

namespace Couchbase.Lite.Util
{
    internal static class URIUtils
    {

        #region Constants

        private const int NotFound = -1;
        private const string Utf8Encoding = "UTF-8";
        private const string NotHierarchical = "This isn't a hierarchical URI.";
        private static readonly char[] HexDigits = "0123456789ABCDEF".ToCharArray();

        #endregion

        #region Public Methods

        public static string Decode(string s)
        {
            if (s == null) {
                return null;
            }

            try {
                return Uri.UnescapeDataString(s);
            } catch (UnsupportedEncodingException e) {
                // This is highly unlikely since we always use UTF-8 encoding.
                throw new CouchbaseLiteException(e, StatusCode.Exception);
            }
        }
            
        public static string GetQueryParameter(Uri uri, string key)
        {
            if (uri.IsAbsoluteUri) {
                throw new NotSupportedException(NotHierarchical);
            }
            if (key == null) {
                throw new ArgumentNullException("key");
            }

            string query = uri.GetQuery();
            if (query == null) {
                return null;
            }

            string encodedKey = Encode(key, null);
            int length = query.Length;
            int start = 0;
            do {
                int nextAmpersand = query.IndexOf('&', start);
                int end = nextAmpersand != -1 ? nextAmpersand : length;
                int separator = query.IndexOf('=', start);
                if (separator > end || separator == -1) {
                    separator = end;
                }
                if (separator - start == encodedKey.Length && query.RegionMatches(true, start, encodedKey
                    , 0, encodedKey.Length)) {
                    if (separator == end) {
                        return string.Empty;
                    } else {
                        string encodedValue = Sharpen.Runtime.Substring(query, separator + 1, end);
                        return Decode(encodedValue, true, Sharpen.Extensions.GetEncoding(Utf8Encoding));
                    }
                }
                // Move start to end of name.
                if (nextAmpersand != -1) {
                    start = nextAmpersand + 1;
                } else {
                    break;
                }
            } while (true);

            return null;
        }
            
        public static string Encode(string s)
        {
            return Encode(s, null);
        }
            
        public static string Encode(string s, string allow)
        {
            if (s == null) {
                return null;
            }

            // Lazily-initialized buffers.
            StringBuilder encoded = null;
            int oldLength = s.Length;
            // This loop alternates between copying over allowed characters and
            // encoding in chunks. This results in fewer method calls and
            // allocations than encoding one character at a time.
            int current = 0;
            while (current < oldLength) {
                // Start in "copying" mode where we copy over allowed chars.
                // Find the next character which needs to be encoded.
                int nextToEncode = current;
                while (nextToEncode < oldLength && IsAllowed(s[nextToEncode], allow)) {
                    nextToEncode++;
                }
                // If there's nothing more to encode...
                if (nextToEncode == oldLength) {
                    if (current == 0) {
                        // We didn't need to encode anything!
                        return s;
                    }
                    else {
                        // Presumably, we've already done some encoding.
                        encoded.AppendRange(s, current, oldLength);
                        return encoded.ToString();
                    }
                }
                if (encoded == null) {
                    encoded = new StringBuilder();
                }
                if (nextToEncode > current) {
                    // Append allowed characters leading up to this point.
                    encoded.AppendRange(s, current, nextToEncode);
                }
                // assert nextToEncode == current
                // Switch to "encoding" mode.
                // Find the next allowed character.
                current = nextToEncode;
                int nextAllowed = current + 1;
                while (nextAllowed < oldLength && !IsAllowed(s[nextAllowed], allow)) {
                    nextAllowed++;
                }
                // Convert the substring to bytes and encode the bytes as
                // '%'-escaped octets.
                string toEncode = Sharpen.Runtime.Substring(s, current, nextAllowed);
                byte[] bytes = Sharpen.Runtime.GetBytesForString(toEncode, Utf8Encoding).ToArray();
                int bytesLength = bytes.Length;
                for (int i = 0; i < bytesLength; i++) {
                    encoded.Append('%');
                    encoded.Append(HexDigits[(bytes[i] & unchecked((int)(0xf0))) >> 4]);
                    encoded.Append(HexDigits[bytes[i] & unchecked((int)(0xf))]);
                }
                current = nextAllowed;
            }

            // Encoded could still be null at this point if s is empty.
            return encoded == null ? s : encoded.ToString();
        }

        public static string Decode(string s, bool convertPlus, Encoding charset)
        {
            if (s.IndexOf('%') == -1 && (!convertPlus || s.IndexOf('+') == -1)) {
                return s;
            }

            StringBuilder result = new StringBuilder(s.Length);
            ByteArrayOutputStream @out = new ByteArrayOutputStream();
            for (int i = 0; i < s.Length;) {
                char c = s[i];
                if (c == '%') {
                    do {
                        if (i + 2 >= s.Length) {
                            throw new ArgumentException("Incomplete % sequence at: " + i);
                        }
                        int d1 = HexToInt(s[i + 1]);
                        int d2 = HexToInt(s[i + 2]);
                        if (d1 == -1 || d2 == -1) {
                            throw new ArgumentException("Invalid % sequence " + Sharpen.Runtime.Substring(s, 
                                i, i + 3) + " at " + i);
                        }
                        @out.Write(unchecked((byte)((d1 << 4) + d2)));
                        i += 3;
                    } while (i < s.Length && s[i] == '%');
                    result.Append(charset.GetString(@out.ToByteArray()));
                    @out.Reset();
                }
                else {
                    if (convertPlus && c == '+') {
                        c = ' ';
                    }
                    result.Append(c);
                    i++;
                }
            }

            return result.ToString();
        }

        public static int HexToInt(char c)
        {
            if ('0' <= c && c <= '9') {
                return c - '0';
            } else {
                if ('a' <= c && c <= 'f') {
                    return 10 + (c - 'a');
                }
                else {
                    if ('A' <= c && c <= 'F') {
                        return 10 + (c - 'A');
                    }
                    else {
                        return -1;
                    }
                }
            }
        }

        public static Uri AppendPath(this Uri uri, string path)
        {
            if (uri == null) return null;

            var newUri = new UriBuilder(uri);
            newUri.Path = Path.Combine(newUri.Path.TrimEnd('/'), path.TrimStart('/'));

            var newUriStr = new Uri(Uri.UnescapeDataString(newUri.Uri.AbsoluteUri));
            return newUriStr;
        }

        #endregion

        #region Private Methods 
            
        private static bool IsAllowed(char c, string allow)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                 || "_-!.~'()*".IndexOf(c) != NotFound || (allow != null && allow.IndexOf(c) != 
                NotFound);
        }
            

        #endregion

    }
}
