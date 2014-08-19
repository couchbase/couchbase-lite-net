// 
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
//using Apache.Http;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
    public class Utils
    {
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

        public static bool IsTransientError(StatusLine status)
        {
            // TODO: in ios implementation, it considers others errors
            int code = status.GetStatusCode();
            if (code == 500 || code == 502 || code == 503 || code == 504)
            {
                return true;
            }
            return false;
        }

        /// <exception cref="Couchbase.Lite.Storage.SQLException"></exception>
        public static byte[] ByteArrayResultForQuery(SQLiteStorageEngine database, string
             query, string[] args)
        {
            byte[] result = null;
            Cursor cursor = database.RawQuery(query, args);
            if (cursor.MoveToNext())
            {
                result = cursor.GetBlob(0);
            }
            return result;
        }

        /// <summary>cribbed from http://stackoverflow.com/questions/9655181/convert-from-byte-array-to-hex-string-in-java
        ///     </summary>
        protected internal static readonly char[] hexArray = "0123456789abcdef".ToCharArray
            ();

        public static string BytesToHex(byte[] bytes)
        {
            char[] hexChars = new char[bytes.Length * 2];
            for (int j = 0; j < bytes.Length; j++)
            {
                int v = bytes[j] & unchecked((int)(0xFF));
                hexChars[j * 2] = hexArray[(int)(((uint)v) >> 4)];
                hexChars[j * 2 + 1] = hexArray[v & unchecked((int)(0x0F))];
            }
            return new string(hexChars);
        }
    }
}
