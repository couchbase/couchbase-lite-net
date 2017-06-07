//
//  DigestCalculator.cs
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
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Sync
{
    internal static class DigestCalculator
    {
        private const string Tag = "DigestCalculator";

        public static IDictionary<string, string> ParseIntoComponents(string rawHeader)
        {
            var components = new Dictionary<string, string>();
            var authHeaderValue = AuthenticationHeaderValue.Parse(rawHeader);
            if (authHeaderValue.Scheme != "Digest") {
                Log.To.Sync.W(Tag, "Invalid scheme {0}", authHeaderValue.Scheme);
                return null;
            }

            var authData = authHeaderValue.Parameter;
            var rawComponents = authData.Split(',');
            foreach (var rawComponent in rawComponents) {
                var firstEqual = rawComponent.IndexOf('=');
                components[rawComponent.Substring(0, firstEqual).Trim()] = rawComponent.Substring(firstEqual + 1).Trim('"');
            }

            return components;
        }

        public static string Calculate(IDictionary<string, string> components)
        {
            var ha1md5 = MD5.Create();
            var ha2md5 = MD5.Create();
            var responsemd5 = MD5.Create();

            var ha1Str = String.Format("{0}:{1}:{2}", components.Get("username"), components.Get("realm"), components.Get("password"));
            var ha1Hash = ha1md5.ComputeHash(Encoding.UTF8.GetBytes(ha1Str));
            var ha1 = BitConverter.ToString(ha1Hash).Replace("-", "").ToLowerInvariant();

            var ha2Str = String.Format("{0}:{1}", components.Get("method"), components.Get("uri"));
            var ha2Hash = ha2md5.ComputeHash(Encoding.UTF8.GetBytes(ha2Str));
            var ha2 = BitConverter.ToString(ha2Hash).Replace("-", "").ToLowerInvariant();

            var responseStr = String.Format("{0}:{1}:{2}:{3}:{4}:{5}", ha1, components.Get("nonce"), components.Get("nc"),
                components.Get("cnonce"), components.Get("qop"), ha2);
            var responseHash = responsemd5.ComputeHash(Encoding.UTF8.GetBytes(responseStr));
            return BitConverter.ToString(responseHash).Replace("-", "").ToLowerInvariant();
        }
    }
}
