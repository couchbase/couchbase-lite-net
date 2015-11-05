//
//  CookieParser.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.Net;
using System.Diagnostics;
using System.Globalization;

#if !NET_3_5
using StringEx = System.String;
#else
using Cookie = System.Net.Couchbase.Cookie;
#endif

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// A class for parsing HTTP cookies
    /// </summary>
    public static class CookieParser
    {

        #region Constants

        private const string ExpiresToken = "expires";
        private const string MaxAgeToken = "max-age";
        private const string DomainToken = "domain";
        private const string PathToken = "path";
        private const string SecureToken = "secure";
        private const string HttpOnlyToken = "httponly";

        private const char SEGMENT_SEPARATOR = ';';
        private const char NAME_VALUE_SEPARATOR = '=';

        private static readonly string[] dateFormats = new string[] 
        {
            // "r", // RFC 1123, required output format but too strict for input
            "ddd, d MMM yyyy H:m:s 'UTC'",
            "ddd, d MMM yyyy H:m:s 'GMT'", // RFC 1123 (r, except it allows both 1 and 01 for date and time)
            "ddd, d MMM yyyy H:m:s", // RFC 1123, no zone - assume GMT
            "d MMM yyyy H:m:s 'GMT'", // RFC 1123, no day-of-week
            "d MMM yyyy H:m:s", // RFC 1123, no day-of-week, no zone
            "ddd, d MMM yy H:m:s 'GMT'", // RFC 1123, short year
            "ddd, d MMM yy H:m:s", // RFC 1123, short year, no zone
            "d MMM yy H:m:s 'GMT'", // RFC 1123, no day-of-week, short year
            "d MMM yy H:m:s", // RFC 1123, no day-of-week, short year, no zone

            "dddd, d'-'MMM'-'yy H:m:s 'GMT'", // RFC 850
            "dddd, d'-'MMM'-'yy H:m:s", // RFC 850 no zone
            "ddd MMM d H:m:s yyyy", // ANSI C's asctime() format

            "ddd, d MMM yyyy H:m:s zzz", // RFC 5322
            "ddd, d MMM yyyy H:m:s", // RFC 5322 no zone
            "d MMM yyyy H:m:s zzz", // RFC 5322 no day-of-week
            "d MMM yyyy H:m:s", // RFC 5322 no day-of-week, no zone
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to parse the given cookie
        /// </summary>
        /// <returns><c>true</c>, if the cookie was parsed, <c>false</c> otherwise.</returns>
        /// <param name="value">The value to parse.</param>
        /// <param name="domain">The cookie domain.</param>
        /// <param name="cookie">Stores the cookie on success.</param>
        public static bool TryParse(string value, string domain, out Cookie cookie)
        {
            cookie = null;
            if (value == null) {
                return false;
            }

            var segments = value.Split(SEGMENT_SEPARATOR);
            if (segments.Length == 0) {
                return false;
            }

            var tmpCookie = new Cookie();
            tmpCookie.Domain = domain;
            tmpCookie.Path = "/";

            foreach (string segment in segments) {
                if (!ParseSegment(segment, tmpCookie)) {
                    return false;
                }
            }

            if (tmpCookie.Name == null || tmpCookie.Value == null) {
                return false;
            }

            cookie = tmpCookie;
            return true;
        }

        #endregion

        #region Private Methods

        private static bool ParseSegment(string segment, Cookie cookie)
        {
            if (segment == null || segment.Trim().Length == 0) {
                return true;
            }

            var nameValue = segment.Split(NAME_VALUE_SEPARATOR);
            string name = nameValue[0].Trim();

            if (String.Equals(name, ExpiresToken, StringComparison.OrdinalIgnoreCase)) {
                string value = GetSegmentValue(nameValue, null);
                DateTimeOffset expires;
                if (DateTimeOffset.TryParseExact(value, dateFormats, DateTimeFormatInfo.InvariantInfo,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out expires))
                {
                    cookie.Expires = expires.DateTime;
                    return true;
                }
                return false;
            }

            if (String.Equals(name, MaxAgeToken, StringComparison.OrdinalIgnoreCase))
            {
                string value = GetSegmentValue(nameValue, null);
                int maxAge;
                if (Int32.TryParse(value, out maxAge))
                {
                    var offset = new TimeSpan(0, 0, maxAge);
                    cookie.Expires = DateTime.Now.Add(offset);
                    return true;
                }
                return false;
            }

            if (String.Equals(name, DomainToken, StringComparison.OrdinalIgnoreCase))
            {
                cookie.Domain = GetSegmentValue(nameValue, null);
                return true;
            }

            if (String.Equals(name, PathToken, StringComparison.OrdinalIgnoreCase))
            {
                cookie.Path = GetSegmentValue(nameValue, "/");
                return true;
            }

            if (String.Equals(name, SecureToken, StringComparison.OrdinalIgnoreCase))
            {
                string value = GetSegmentValue(nameValue, null);
                if (!StringEx.IsNullOrWhiteSpace(value))
                {
                    return false;
                }
                cookie.Secure = true;
                return true;
            }

            if (String.Equals(name, HttpOnlyToken, StringComparison.OrdinalIgnoreCase)) {
                string value = GetSegmentValue(nameValue, null);
                if (!StringEx.IsNullOrWhiteSpace(value)) {
                    return false;
                }
                cookie.HttpOnly = true;
                return true;
            }

            string cookieValue = GetSegmentValue(nameValue, null);
            if (StringEx.IsNullOrWhiteSpace(cookieValue)) {
                return false;
            }

            cookie.Name = name;
            cookie.Value = cookieValue;

            return true;
        }

        private static string GetSegmentValue(string[] nameValuePair, string defaultValue)
        {
            Debug.Assert(nameValuePair != null);
            return nameValuePair.Length > 1 ? nameValuePair[1].Trim('"') : defaultValue;
        }

        #endregion
    }
}

