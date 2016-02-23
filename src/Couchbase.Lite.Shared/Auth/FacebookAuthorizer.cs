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

namespace Couchbase.Lite.Auth
{
    internal class FacebookAuthorizer : Authorizer
    {
        internal const string LoginParameterAccessToken = "access_token";
        internal const string QueryParameter = "facebookAccessToken";
        internal const string QueryParameterEmail = "email";

        private readonly static ConcurrentDictionary<string[], string> _AccessTokens =
            new ConcurrentDictionary<string[], string>(new StringArrayComparer());

        private readonly string _emailAddress;

        public FacebookAuthorizer(string emailAddress)
        {
            _emailAddress = emailAddress;
        }

        public override string UserInfo { get { return null; } }

        public override string Scheme { get { return null; } }

        public override bool UsesCookieBasedLogin { get { return true; } }

        public override IDictionary<string, string> LoginParametersForSite(Uri site)
        {
            IDictionary<string, string> loginParameters = new Dictionary<string, string>();
            string accessToken = TokenForSite(site);
            if (accessToken != null)
            {
                loginParameters[LoginParameterAccessToken] = accessToken;
                return loginParameters;
            }
            else
            {
                return null;
            }
        }

        public override string LoginPathForSite(Uri site)
        {
            return new Uri(site.AbsolutePath + "/_facebook").AbsoluteUri;
        }

        public static bool RegisterAccessToken(string accessToken, string email, Uri
             origin)
        {
            var key = new[] { email, origin.Host };
            Log.D(Database.TAG, "FacebookAuthorizer registering key: " + key);
            _AccessTokens.AddOrUpdate(key, k => accessToken, (k, v) => accessToken);
            return true;
        }

        public string TokenForSite(Uri site)
        {
            var key = new[] { _emailAddress, site.Host };
            Log.D(Database.TAG, "FacebookAuthorizer looking up key: " + key + " from list of access tokens");

            var accessToken = default(string);
            if (!_AccessTokens.TryGetValue(key, out accessToken)) {
                return null;
            }

            return accessToken;
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
