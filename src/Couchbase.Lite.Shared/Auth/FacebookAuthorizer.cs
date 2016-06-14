//
// FacebookAuthorizer.cs
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
using System.Linq;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using System.Collections.Concurrent;
using System.Text;
using System.Collections;
using System.Net.Http.Headers;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Auth
{
    internal class FacebookAuthorizer : Authorizer, ISessionCookieAuthorizer
    {
        private static readonly string Tag = typeof(FacebookAuthorizer).Name;
        private const string LoginParameterAccessToken = "access_token";
        private const string QueryParameter = "facebookAccessToken";
        private const string QueryParameterEmail = "email";

        private readonly static ConcurrentDictionary<string[], string> _AccessTokens =
            new ConcurrentDictionary<string[], string>(new StringArrayComparer());

        private readonly string _emailAddress;

        public override string UserInfo
        {
            get {
                throw new NotImplementedException();
            }
        }

        public override string Scheme
        {
            get {
                throw new NotImplementedException();
            }
        }

        public override bool UsesCookieBasedLogin
        {
            get {
                throw new NotImplementedException();
            }
        }

        public FacebookAuthorizer(string emailAddress)
        {
            if(emailAddress == null) {
                Log.To.Sync.E(Tag, "Null email address in constructor, throwing...");
                throw new ArgumentNullException("emailAddress");
            }

            _emailAddress = emailAddress;
        }

        public static FacebookAuthorizer FromUri(Uri uri)
        {
            var facebookAccessToken = URIUtils.GetQueryParameter(uri, QueryParameter);

            if(facebookAccessToken != null && !StringEx.IsNullOrWhiteSpace(facebookAccessToken)) {
                var email = URIUtils.GetQueryParameter(uri, QueryParameterEmail);
                var authorizer = new FacebookAuthorizer(email);
                Uri remoteWithQueryRemoved = null;

                try {
                    remoteWithQueryRemoved = new UriBuilder(uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath).Uri;
                } catch(UriFormatException e) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, e, Tag,
                        "Invalid URI format for remote endpoint");
                }

                RegisterAccessToken(facebookAccessToken, email, remoteWithQueryRemoved);
                return authorizer;
            }

            return null;
        }

        public static bool RegisterAccessToken(string accessToken, string email, Uri
             origin)
        {
            var key = new[] { email, origin.Host };
            Log.To.Sync.I(Tag, "Registering Facebook key [{0}, {1}]", 
                new SecureLogString(email, LogMessageSensitivity.PotentiallyInsecure),
                origin.Host);
            _AccessTokens.AddOrUpdate(key, k => accessToken, (k, v) => accessToken);
            return true;
        }

        public string GetToken()
        {
            var key = new[] { _emailAddress, RemoteUrl.Host };
            Log.To.Sync.V(Tag, "Searching for Facebook key [{0}, {1}]",
                new SecureLogString(_emailAddress, LogMessageSensitivity.PotentiallyInsecure),
                RemoteUrl.Host);

            var accessToken = default(string);
            if (!_AccessTokens.TryGetValue(key, out accessToken)) {
                return null;
            }

            return accessToken;
        }

        public override string ToString()
        {
            var sb = new StringBuilder("[FacebookAuthorizer (");
            foreach (var pair in _AccessTokens) {
                if (pair.Key[0] == _emailAddress) {
                    sb.AppendFormat("key={0} value={1}, ", 
                        new SecureLogJsonString(pair.Key, LogMessageSensitivity.PotentiallyInsecure), 
                        new SecureLogString(pair.Value, LogMessageSensitivity.Insecure));
                }
            }

            sb.Remove(sb.Length - 2, 2);
            sb.Append(")]");
            return sb.ToString();
        }

        public IList LoginRequest()
        {
            var token = GetToken();
            if(token == null) {
                return null;
            }

            return new ArrayList { "POST", RemoteUrl.AbsolutePath + "_facebook", new Dictionary<string, string> {
                [LoginParameterAccessToken] = token
            }};
        }

        public bool ProcessLoginResponse(IDictionary<string, object> jsonResponse, HttpRequestHeaders headers, Exception error, Action<bool, Exception> continuation)
        {
            return false;
        }

        public override string LoginPathForSite(Uri site)
        {
            throw new NotImplementedException();
        }

        public override IDictionary<string, string> LoginParametersForSite(Uri site)
        {
            throw new NotImplementedException();
        }

        private class StringArrayComparer : IEqualityComparer<string[]>
        {
            #region IEqualityComparer

            public bool Equals(string[] x, string[] y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(string[] obj)
            {
                int hc = obj.Length;
                for (int i = 0; i < obj.Length; ++i) {
                    hc = unchecked(hc * 17 + obj[i].GetHashCode());
                }

                return hc;
            }

            #endregion


        }
    }
}
