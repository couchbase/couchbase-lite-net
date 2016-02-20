//
// BlobKey.cs
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
using System.IO;
using System.Linq;

using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    /// <summary>Key identifying a data blob.</summary>
    /// <remarks>Key identifying a data blob. This happens to be a SHA-1 digest.</remarks>
    internal sealed class BlobKey
    {
        private const string TAG = "BlobKey";

        public byte[] Bytes { get; set; }

        public BlobKey()
        {
        }

        public BlobKey(byte[] bytes)
        {
            Bytes = bytes;
        }

        /// <summary>Constructor</summary>
        /// <param name="base64Digest">
        /// string with base64'd digest, with leading "sha1-" attached.
        /// eg, "sha1-LKJ32423JK..."
        /// </param>
        public BlobKey(string base64Digest) : this(DecodeBase64Digest(base64Digest))
        {
        }

        public string Base64Digest()
        {
            return string.Format("sha1-{0}", Convert.ToBase64String(Bytes));
        }

        public static byte[] ConvertFromHex(string s)
        {
            return Enumerable.Range(0, s.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(s.Substring(x, 2), 16))
                .ToArray();
        }

        /// <summary>
        /// Decode base64'd digest into a byte array that is suitable for use
        /// as a blob key.
        /// </summary>
        /// <remarks>
        /// Decode base64'd digest into a byte array that is suitable for use
        /// as a blob key.
        /// </remarks>
        /// <param name="base64Digest">
        /// string with base64'd digest, with leading "sha1-" attached.
        /// eg, "sha1-LKJ32423JK..."
        /// </param>
        /// <returns>a byte[] blob key</returns>
        private static byte[] DecodeBase64Digest(string base64Digest)
        {
            const string expectedPrefix = "sha1-";
            var prefixLength = expectedPrefix.Length;
            if (!base64Digest.StartsWith(expectedPrefix, StringComparison.Ordinal)) {
                return Enumerable.Repeat<byte>(0, 20).ToArray(); // MD5 is no longer valid
            }

            base64Digest = base64Digest.Remove(0, prefixLength);
            byte[] bytes;
            try {
                bytes = StringUtils.ConvertFromUnpaddedBase64String(base64Digest);
            } catch (IOException e) {
                throw new ArgumentException(String.Format("{0} is not a valid Base64 digest.", base64Digest), e.Message);
            }

            return bytes;
        }
            
        public override bool Equals(object o)
        {
            if (!(o is BlobKey)) {
                return false;
            }

            BlobKey oBlobKey = (BlobKey)o;

            if (Bytes == null || oBlobKey.Bytes == null) {
                return false;
            }
            
            if (Bytes.Length != oBlobKey.Bytes.Length) {
                return false;
            }

            for (int i = 0; i < Bytes.Length; i++) {
                if (!Bytes[i].Equals (oBlobKey.Bytes[i])) {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = 1;
            foreach(var item in Bytes) {
                hashCode = 31 * hashCode + item.GetHashCode();
            }
            return hashCode;
        }

        public override string ToString()
        {
            return BitConverter.ToString(Bytes).Replace("-", String.Empty);
        }


    }
}
