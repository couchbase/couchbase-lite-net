//
// ExtensionMethods.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.IO;

namespace Couchbase.Lite
{
    internal delegate bool TryParseDelegate<T>(string s, out T value);

    internal static class ExtensionMethods
    {
        public static void PutAll<T, U> (this IDictionary<T, U> d, IDictionary<T, U> values)
        {
            foreach (var val in values) {
                d[val.Key] = val.Value;
            }
        }

        public static void Sort<T> (this IList<T> list, IComparer<T> comparer)
        {
            List<T> sorted = new List<T> (list);
            sorted.Sort (comparer);
            for (int i = 0; i < list.Count; i++) {
                list[i] = sorted[i];
            }
        }

        public static bool RegionMatches (this string str, int toOffset, string other, int ooffset, int len)
        {
            if (toOffset < 0 || ooffset < 0 || toOffset + len > str.Length || ooffset + len > other.Length) {
                return false;
            }

            return string.Compare (str, toOffset, other, ooffset, len) == 0;
        }

        public static string ReplaceAll (this string str, string regex, string replacement)
        {
            Regex rgx = new Regex (regex);

            if (replacement.IndexOfAny (new char[] { '\\','$' }) != -1) {
                // Back references not yet supported
                StringBuilder sb = new StringBuilder ();
                for (int n=0; n<replacement.Length; n++) {
                    char c = replacement [n];
                    if (c == '$') {
                        throw new NotSupportedException("Back references not supported");
                    }
                    if (c == '\\') {
                        c = replacement[++n];
                    }

                    sb.Append (c);
                }
                replacement = sb.ToString ();
            }

            return rgx.Replace (str, replacement);
        }
            
        public static TimeSpan TimeSinceEpoch(this DateTime dt)
        {
            return dt - Misc.Epoch;
        }

        public static U Get<T, U> (this IDictionary<T, U> d, T key)
        {
            U val = default(U);
            d.TryGetValue (key, out val);
            return val;
        }

        public static IEnumerable<byte> Decompress(this IEnumerable<byte> compressedData)
        {
            var realized = compressedData.ToArray();
            using (var ms = RecyclableMemoryStreamManager.SharedInstance.GetStream("Decompress", 
                realized, 0, realized.Length))
            using (var gs = new GZipStream(ms, CompressionMode.Decompress, false)) {
                return gs.ReadAllBytes();
            }
        }

        public static IEnumerable<byte> Compress(this IEnumerable<byte> data)
        {
            var array = data.ToArray();
            using (var ms = RecyclableMemoryStreamManager.SharedInstance.GetStream("Compress")) {
                using (var gs = new GZipStream(ms, CompressionMode.Compress, false)) {
                    gs.Write(array, 0, array.Length);
                }

                return ms.ToArray();
            }
        }

        public static bool TryGetValue<T>(this IDictionary<string, object> dic, string key, out T value)
        {
            value = default(T);
            object obj;
            if (!dic.TryGetValue(key, out obj)) {
                return false;
            }

            //If the types already match then things are easy
            if ((obj is T)) {
                value = (T)obj;
                return true;
            }

            try {
                //Take the slow route for things like boxed value types
                value = (T)Convert.ChangeType(value, typeof(T));
                return true;
            } catch(Exception) {
                return false;
            }
        }

        public static bool TryCast<T>(object obj, out T castVal)
        {
            //If the types already match then things are easy
            if (obj is T) {
                castVal = (T)obj;
                return true;
            }

            try {
                //Take the slow route for things like boxed value types
                castVal = (T)Convert.ChangeType(obj, typeof(T));
            } catch(Exception) {
                castVal = default(T);
                return false;
            }

            return true;
        }

        public static T CastOrDefault<T>(object obj, T defaultVal)
        {
            T retVal;
            if (obj != null && TryCast<T>(obj, out retVal)) {
                return retVal;
            }

            return defaultVal;
        }

        public static T CastOrDefault<T>(object obj)
        {
            return CastOrDefault<T>(obj, default(T));
        }

        public static T GetCast<T>(this IDictionary<string, object> collection, string key)
        {
            return collection.GetCast(key, default(T));
        }

        public static T GetCast<T>(this IDictionary<string, object> collection, string key, T defaultVal)
        {
            object value = collection.Get(key);
            return CastOrDefault<T>(value, defaultVal);
        }

        public static T? GetNullable<T>(this IDictionary<string, object> collection, string key) where T : struct
        {
            object value = collection.Get(key);
            return CastOrDefault<T>(value);
        }

        public static IEnumerable<T> AsSafeEnumerable<T>(this IEnumerable<T> source)
        {
            var e = ((IEnumerable)source).GetEnumerator();
            using (e as IDisposable)
            {
                while (e.MoveNext())
                {
                    yield return (T)e.Current;
                }
            }
        }

        internal static IDictionary<TKey,TValue> AsDictionary<TKey, TValue>(this object attachmentProps)
        {
            return Manager.GetObjectMapper().ConvertToDictionary<TKey, TValue>(attachmentProps);
        }

        internal static IList<TValue> AsList<TValue>(this object value)
        {
            return Manager.GetObjectMapper().ConvertToList<TValue>(value);
        }

        public static byte[] ReadAllBytes(this Stream stream)
        {
            var ms = stream as MemoryStream;
            var block = RecyclableMemoryStreamManager.SharedInstance.GetBlock();
            try {
                if (ms != null) {
                    return ms.GetBuffer().Take((int)ms.Length).ToArray();
                }

                ms = RecyclableMemoryStreamManager.SharedInstance.GetStream();
                var read = 0;
                while((read = stream.Read(block, 0, block.Length)) > 0) {
                    ms.Write(block, 0, read);
                }
                return ms.GetBuffer().Take((int)ms.Length).ToArray();
            } finally {
                ms?.Dispose();
                if (block != null) {
                    RecyclableMemoryStreamManager.SharedInstance.ReturnBlocks(new byte[][] { block }, "ReadAllBytes");
                }
            }
        }

        public static StatusCode GetStatusCode(this HttpStatusCode code)
        {
            var validVals = Enum.GetValues(typeof(StatusCode));
            foreach (StatusCode validVal in validVals)
            {
                if ((Int32)code == (Int32)validVal)
                {
                    return validVal;
                }
            }

            return StatusCode.Unknown;
        }

        public static AuthenticationHeaderValue GetAuthenticationHeader(this Uri uri, string scheme)
        {
            Debug.Assert(uri != null);

            var unescapedUserInfo = uri.UserEscaped
                ? Uri.UnescapeDataString(uri.UserInfo)
                : uri.UserInfo;

            var param = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(unescapedUserInfo));

            return new AuthenticationHeaderValue(scheme, param);
        }

        public static AuthenticationHeaderValue AsAuthenticationHeader(this string userinfo, string scheme)
        {
            Debug.Assert(userinfo != null);
            return new AuthenticationHeaderValue(scheme, userinfo);
        }
            
        #if NET_3_5

        public static IEnumerable<FileInfo> EnumerateFiles(this DirectoryInfo info, string searchPattern, SearchOption option) {
            foreach (var file in info.GetFiles(searchPattern, option)) {
                yield return file;
            }
        }

        public static IEnumerable<FileInfo> EnumerateFiles(this DirectoryInfo info) {
            foreach (var file in info.GetFiles()) {
                yield return file;
            }
        }

        public static Task<HttpListenerContext> GetContextAsync(this HttpListener listener)
        {
            return Task.Factory.FromAsync<HttpListenerContext>(listener.BeginGetContext, listener.EndGetContext, null);
        }

        #endif
            
    }
}
