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
using Couchbase.Lite.Util;
using Sharpen;
using System.Text;

namespace Couchbase.Lite.Auth
{
    internal static class DigestCalculator
    {
        private const string TAG = "DigestCalculator";

        public static IDictionary<string, string> ParseIntoComponents(string rawHeader)
        {
            var components = new Dictionary<string, string>();
            var authHeaderValue = AuthenticationHeaderValue.Parse(rawHeader);
            if (authHeaderValue.Scheme != "Digest") {
                Log.W(TAG, "Invalid scheme {0}", authHeaderValue.Scheme);
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
            MessageDigest ha1md5 = MessageDigest.GetInstance("md5");
            MessageDigest ha2md5 = MessageDigest.GetInstance("md5");
            MessageDigest responsemd5 = MessageDigest.GetInstance("md5");

            var ha1Str = String.Format("{0}:{1}:{2}", components.Get("username"), components.Get("realm"), components.Get("password"));
            ha1md5.Update(Encoding.UTF8.GetBytes(ha1Str));

            var ha1 = BitConverter.ToString(ha1md5.Digest()).Replace("-", "").ToLowerInvariant();
            var ha2Str = String.Format("{0}:{1}", components.Get("method"), components.Get("uri"));
            ha2md5.Update(Encoding.UTF8.GetBytes(ha2Str));
            var ha2 = BitConverter.ToString(ha2md5.Digest()).Replace("-", "").ToLowerInvariant();

            var responseStr = String.Format("{0}:{1}:{2}:{3}:{4}:{5}", ha1, components.Get("nonce"), components.Get("nc"), 
                components.Get("cnonce"), components.Get("qop"), ha2);
            responsemd5.Update(Encoding.UTF8.GetBytes(responseStr));
            return BitConverter.ToString(responsemd5.Digest()).Replace("-", "").ToLowerInvariant();
        }

        public static string Calculate(IDictionary<string, string> components, Func<string, MessageDigest, bool> passwordDigestBlock)
        {
            MessageDigest ha1md5 = MessageDigest.GetInstance("md5");
            MessageDigest ha2md5 = MessageDigest.GetInstance("md5");
            MessageDigest responsemd5 = MessageDigest.GetInstance("md5");

            var ha1Str = String.Format("{0}:{1}:", components.Get("username"), components.Get("realm"));
            ha1md5.Update(Encoding.UTF8.GetBytes(ha1Str));
            if (!passwordDigestBlock(components.Get("username"), ha1md5)) {
                Log.W(TAG, "No password entered from passwordDigestBlock");
                return null;
            }

            var ha1 = BitConverter.ToString(ha1md5.Digest()).Replace("-", "").ToLowerInvariant();
            var ha2Str = String.Format("{0}:{1}", components.Get("method"), components.Get("uri"));
            ha2md5.Update(Encoding.UTF8.GetBytes(ha2Str));
            var ha2 = BitConverter.ToString(ha2md5.Digest()).Replace("-", "").ToLowerInvariant();

            var responseStr = String.Format("{0}:{1}:{2}:{3}:{4}:{5}", ha1, components.Get("nonce"), components.Get("nc"), 
                components.Get("cnonce"), components.Get("qop"), ha2);
            responsemd5.Update(Encoding.UTF8.GetBytes(responseStr));
            return BitConverter.ToString(responsemd5.Digest()).Replace("-", "").ToLowerInvariant();
        }
    }
}

