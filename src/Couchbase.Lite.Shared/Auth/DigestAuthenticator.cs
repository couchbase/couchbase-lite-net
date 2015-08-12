//
//  DigestAuthenticator.cs
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
using System.Linq;
using System.Net.Http;

using Sharpen;
using System.Threading;

namespace Couchbase.Lite.Auth
{

    /// <summary>
    /// An authenticator for performing HTTP Digest authentication (RFC 2617)
    /// </summary>
    public sealed class DigestAuthenticator : IChallengeResponseAuthenticator
    {

        #region Variables

        private IDictionary<string, string> _components;
        private int _nc;
        private readonly string _username;
        private readonly string _password;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new digest authenticator using the given name and password
        /// </summary>
        /// <param name="username">The username to authenticate with</param>
        /// <param name="password">The password to authenticate with</param>
        public DigestAuthenticator(string username, string password)
        {
            _username = username;
            _password = password;
        }

        #endregion

        #region IChallengeResponseAuthenticator
        #pragma warning disable 1591

        public string ResponseFromChallenge(HttpResponseMessage message)
        {
            var challenge = message.Headers.GetValues("WWW-Authenticate").First();
            _components = DigestCalculator.ParseIntoComponents(challenge);
            if (_components == null) {
                return null;
            }

            _components["username"] = _username;
            _components["password"] = _password;
            _components["uri"] = message.RequestMessage.RequestUri.PathAndQuery;
            _components["method"] = message.RequestMessage.Method.ToString();
            _components["cnonce"] = Misc.CreateGUID();
            return String.Format("{0} {1}", Scheme, UserInfo);
        }

        public void PrepareWithRequest(HttpRequestMessage request)
        {
            if (_components == null) {
                return;
            }

            _components["method"] = request.Method.ToString();
        }

        #endregion

        #region IAuthenticator

        public string LoginPathForSite(Uri site)
        {
            return null;
        }

        public IDictionary<string, string> LoginParametersForSite(Uri site)
        {
            return null;
        }

        public string UserInfo
        {
            get
            {
                if (_components == null) {
                    return null;
                }

                _components["nc"] = Interlocked.Increment(ref _nc).ToString();
                var response = DigestCalculator.Calculate(_components);
                return String.Format("username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", " +
                    "qop={4}, nc={5}, cnonce=\"{6}\", response=\"{7}\", opaque=\"0\"", _username,
                    _components.Get("realm"), _components.Get("nonce"), _components.Get("uri"), _components.Get("qop"),
                    _nc, _components.Get("cnonce"), response);
            }
        }

        public string Scheme
        {
            get {
                return "Digest";
            }
        }

        public bool UsesCookieBasedLogin
        {
            get {
                return false;
            }
        }

        #pragma warning restore 1591
        #endregion
        
    }
}

