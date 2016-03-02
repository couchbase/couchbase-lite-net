//
// Misc.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

using Couchbase.Lite;
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{

    /// <summary>
    /// An event handler that sends a typed sender instead of object
    /// </summary>
    public delegate void TypedEventHandler<TSenderType, TArgType>(TSenderType sender, TArgType args);

    internal static class Misc
    {
        internal static readonly DateTime Epoch = new DateTime(1970, 1, 1);
        public static DateTime CreateDate(long milliSecondsSinceEpoch)
        {
            return Epoch.AddMilliseconds(milliSecondsSinceEpoch);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, string tag, string message)
        {
            return CreateExceptionAndLog(domain, StatusCode.Exception, tag, message);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, StatusCode code, string tag, string message)
        {
            domain.E(tag, "{0}, throwing CouchbaseLiteException", message);
            return new CouchbaseLiteException(message, code);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, StatusCode code, string tag, 
            string format, params object[] args)
        {
            var message = String.Format(format, args);
            return CreateExceptionAndLog(domain, code, tag, message);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, Exception inner, string tag, string message)
        {
            return CreateExceptionAndLog(domain, inner, StatusCode.Exception, tag, message);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, Exception inner, 
            StatusCode code, string tag, string message)
        {
            domain.E(tag, String.Format("{0}, throwing CouchbaseLiteException)", 
                message), inner);
            return new CouchbaseLiteException(message, inner) { Code = code };
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, Exception inner, 
            string tag, string format, params object[] args)
        {
            var message = String.Format(format, args);
            return CreateExceptionAndLog(domain, inner, tag, message);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, Exception inner, 
            StatusCode code, string tag, string format, params object[] args)
        {
            var message = String.Format(format, args);
            return CreateExceptionAndLog(domain, inner, code, tag, message);
        }

        public static Exception Flatten(Exception inE)
        {
            var ae = inE as AggregateException;
            if (ae == null) {
                return inE;
            }

            return ae.Flatten().InnerException;
        }

        public static void SafeDispose<T>(ref T obj) where T : class, IDisposable
        {
            var tmp = obj;
            obj = null;

            if (tmp != null) {
                tmp.Dispose();
            }
        }

        public static string CreateGUID()
        {
            var sb = new StringBuilder(Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('='));
           
            // URL-safe character set per RFC 4648 sec. 5:
            sb.Replace('/', '_');
            sb.Replace('+', '-');

            // prefix a '-' to make it more clear where this string came from and prevent having a leading
            // '_' character:
            sb.Insert(0, '-');
            return sb.ToString();
        }

        public static string HexSHA1Digest(IEnumerable<Byte> input)
        {
            MessageDigest md;
            try {
                md = MessageDigest.GetInstance("SHA-1");
            } catch (NotSupportedException) {
                Log.To.NoDomain.E("Misc", "SHA-1 digest is unavailable.");
                return null;
            }

            byte[] sha1hash;
            var inputArray = input.ToArray();

            md.Update(inputArray, 0, inputArray.Count());
            sha1hash = md.Digest();
            
            return ConvertToHex(sha1hash);
        }

        public static string ConvertToHex(byte[] data, int numBytes = -1)
        {
            StringBuilder buf = new StringBuilder();
            if (numBytes == -1) {
                numBytes = data.Length;
            }

            for (int i = 0; i < numBytes; i++)
            {
                int halfbyte = (data[i] >> 4) & unchecked((0x0F));
                int two_halfs = 0;
                do
                {
                    if ((0 <= halfbyte) && (halfbyte <= 9))
                    {
                        buf.Append((char)('0' + halfbyte));
                    }
                    else
                    {
                        buf.Append((char)('a' + (halfbyte - 10)));
                    }
                    halfbyte = data[i] & unchecked((0x0F));
                }
                while (two_halfs++ < 1);
            }
            return buf.ToString();
        }

        public static int TDSequenceCompare(long a, long b, bool ascending = true)
        {
            long diff = ascending ? a - b : b - a;
            return diff > 0 ? 1 : (diff < 0 ? -1 : 0);
        }

        public static string UnquoteString(string param)
        {
            return param.Replace("\"", string.Empty);
        }

        public static bool IsTransientNetworkError(Exception e)
        {
            var error = Misc.Flatten(e);

            if (error is IOException
                || error is TimeoutException
                || error is SocketException) {
                return true;
            }

            var we = error as WebException;
            if (we == null) {
                return false;
            }

            if (we.Status == WebExceptionStatus.ConnectFailure || we.Status == WebExceptionStatus.Timeout ||
               we.Status == WebExceptionStatus.ConnectionClosed || we.Status == WebExceptionStatus.RequestCanceled) {
                return true;
            }

            if (we.Response == null) {
                return false;
            }

            return IsTransientError(((HttpWebResponse)we.Response).StatusCode);
        }

        public static bool IsTransientError(HttpResponseMessage response)
        {
            if (response == null) {
                return false;
            }

            return IsTransientError(response.StatusCode);
        }

        public static bool IsTransientError(HttpStatusCode status)
        {
            return status == HttpStatusCode.InternalServerError || 
                status == HttpStatusCode.BadGateway || 
                status == HttpStatusCode.ServiceUnavailable || 
                status == HttpStatusCode.GatewayTimeout;
        }

        /// <summary>Like equals, but works even if either/both are null</summary>
        /// <param name="obj1">object1 being compared</param>
        /// <param name="obj2">object2 being compared</param>
        /// <returns>
        /// true if both are non-null and obj1.equals(obj2), or true if both are null.
        /// otherwise return false.
        /// </returns>
        public static bool IsEqual(object obj1, object obj2)
        {
            if (obj1 != null)
            {
                return (obj2 != null) && obj1.Equals(obj2);
            }
            else
            {
                return obj2 == null;
            }
        }

        public static bool PropertiesEqual(IDictionary<string, object> prop1, IDictionary<string, object> prop2)
        {
            if (prop1 == null || prop2 == null)
            {
                return prop1 == prop2;
            }

            if (prop1.Count != prop2.Count)
            {
                return false;
            }

            foreach(var key in prop1.Keys)
            {
                if (!prop2.ContainsKey(key))
                {
                    return false;
                }

                object obj1 = prop1[key];
                object obj2 = prop2[key];

                bool isValueEqual;
                if (obj1 is IDictionary<string, object> && obj2 is IDictionary<string, object>) {
                    isValueEqual = PropertiesEqual((IDictionary<string, object>)obj1, (IDictionary<string, object>)obj2);
                } 
                else 
                {
                    isValueEqual = obj1.Equals(obj2);
                }

                if (!isValueEqual)
                {
                    return false;
                }
            }

            return true;
        }
    }
        
}
