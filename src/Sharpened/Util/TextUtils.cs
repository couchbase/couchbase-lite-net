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
//using System.Collections;
using System.IO;
using System.Text;
using Apache.Http.Util;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
    public class TextUtils
    {
        // COPY: Partially copied from android.text.TextUtils
        /// <summary>Returns a string containing the tokens joined by delimiters.</summary>
        /// <remarks>Returns a string containing the tokens joined by delimiters.</remarks>
        /// <param name="tokens">
        /// an array objects to be joined. Strings will be formed from
        /// the objects by calling object.toString().
        /// </param>
        public static string Join(CharSequence delimiter, IEnumerable tokens)
        {
            StringBuilder sb = new StringBuilder();
            bool firstTime = true;
            foreach (object token in tokens)
            {
                if (firstTime)
                {
                    firstTime = false;
                }
                else
                {
                    sb.Append(delimiter);
                }
                sb.Append(token);
            }
            return sb.ToString();
        }

        /// <exception cref="System.IO.IOException"></exception>
        public static byte[] Read(InputStream @is)
        {
            int initialCapacity = 1024;
            ByteArrayBuffer byteArrayBuffer = new ByteArrayBuffer(initialCapacity);
            byte[] bytes = new byte[512];
            int offset = 0;
            int numRead = 0;
            while ((numRead = @is.Read(bytes, offset, bytes.Length - offset)) >= 0)
            {
                byteArrayBuffer.Append(bytes, 0, numRead);
                offset += numRead;
            }
            return byteArrayBuffer.ToByteArray();
        }
    }
}
