//
//  DigestAuthHeaderValue.cs
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
using System.Net.Http.Headers;
using System.Collections.Generic;
using Sharpen;
using System.Text;
using System.Net;

namespace Couchbase.Lite.Listener.Tcp
{
    public sealed class DigestAuthHeaderValue
    {
        private readonly IDictionary<string, string> _components = new Dictionary<string, string>();
        private string _calculatedResponse = String.Empty;

        internal string Nonce {
            get {
                return _components.Get("nonce");
            }
        }

        internal int Nc {
            get {
                return ExtensionMethods.CastOrDefault<int>(_components.Get("nc"));
            }
        }

        public DigestAuthHeaderValue(HttpListenerContext context)
        {
            var headerValue = context.Request.Headers["Authorization"];
            _components["method"] = context.Request.HttpMethod;
            var authHeaderValue = AuthenticationHeaderValue.Parse(headerValue);
            if (authHeaderValue.Scheme != "Digest") {
                throw new InvalidOperationException(String.Format("Invalid digest type {0}", authHeaderValue.Scheme));
            }

            var authData = authHeaderValue.Parameter;
            var rawComponents = authData.Split(',');
            foreach (var rawComponent in rawComponents) {
                var firstEqual = rawComponent.IndexOf('=');
                _components[rawComponent.Substring(0, firstEqual).Trim()] = rawComponent.Substring(firstEqual + 1).Trim('"');
            }
        }

        internal bool ValidateAgainst(CouchbaseLiteServiceListener listener)
        {
            CalculateResponse(listener);
            return _calculatedResponse == _components.Get("response");
        }

        private void CalculateResponse(CouchbaseLiteServiceListener listener)
        {
            if (!String.IsNullOrEmpty(_calculatedResponse)) {
                return;
            }

            MessageDigest ha1md5 = MessageDigest.GetInstance("md5");
            MessageDigest ha2md5 = MessageDigest.GetInstance("md5");
            MessageDigest responsemd5 = MessageDigest.GetInstance("md5");

            var ha1Str = String.Format("{0}:{1}:", _components.Get("username"), _components.Get("realm"));
            ha1md5.Update(Encoding.UTF8.GetBytes(ha1Str));
            if (!listener.HashPasswordToDigest(_components.Get("username"), ha1md5)) {
                return;
            }

            var ha1 = BitConverter.ToString(ha1md5.Digest()).Replace("-", "").ToLowerInvariant();
            var ha2Str = String.Format("{0}:{1}", _components.Get("method"), _components.Get("uri"));
            ha2md5.Update(Encoding.UTF8.GetBytes(ha2Str));
            var ha2 = BitConverter.ToString(ha2md5.Digest()).Replace("-", "").ToLowerInvariant();

            var responseStr = String.Format("{0}:{1}:{2}:{3}:{4}:{5}", ha1, _components.Get("nonce"), _components.Get("nc"), 
                _components.Get("cnonce"), _components.Get("qop"), ha2);
            responsemd5.Update(Encoding.UTF8.GetBytes(responseStr));
            _calculatedResponse = BitConverter.ToString(responsemd5.Digest()).Replace("-", "").ToLowerInvariant();
        }
    }
}

