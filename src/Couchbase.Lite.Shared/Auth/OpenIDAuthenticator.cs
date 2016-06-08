//
//  OpenIDAuthenticator.cs
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

//  https://openid.net/connect/
//  JWT = JSON Web Token = https://jwt.io/introduction/ or https://tools.ietf.org/html/rfc7519
//  https://github.com/couchbase/sync_gateway/wiki/OIDC-Notes
using Couchbase.Lite.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Couchbase.Lite.Auth
{
    internal sealed class OpenIDAuthenticator : IOIDCAuthenticator, ICustomHeadersAuthorizer, ISessionCookieAuthorizer
    {
        private const string Tag = nameof(OpenIDAuthenticator);

        private string _authCode;
        private string _idToken;
        private string _refreshToken;

        public string AuthorizationHeaderValue
        {
            get {
                if(_token != null) {
                    return $"Bearer {_token}";
                }

                return null;
            }
        }

        public OpenIDAuthenticator(string authCode, string idToken, string refreshToken)
        {
            _authCode = authCode;
            _idToken = idToken;
            _refreshToken = refreshToken;
        }

        public string Scheme
        {
            get {
                throw new NotImplementedException();
            }
        }

        public string UserInfo
        {
            get {
                throw new NotImplementedException();
            }
        }

        public bool UsesCookieBasedLogin
        {
            get {
                throw new NotImplementedException();
            }
        }

        public Uri LoginUri
        {
            get; private set;
        }

        public IDictionary<string, string> LoginParametersForSite(Uri site)
        {
            LoginUri = null;
            if(_idToken != null) {
                return null;
            }

            if(_refreshToken != null) {
                return new Dictionary<string, string> {
                    ["token"] = _refreshToken
                };
            }

            return new Dictionary<string, string>();
        }

        public string LoginPathForSite(Uri site)
        {
            // If we have no token, we need to POST to /db/_oidc to get the auth challenge -- the server
            // will return a 401 status with a WWW-Authenticate header giving the OP's login URL.
            if(_idToken != null) {
                return null;
            }

            if(_refreshToken != null) {
                return $"_oidc_refresh?refresh_token={Uri.EscapeUriString(_refreshToken)}";
            }

            if(_authCode != null) {
                return $"_oidc_callback?code={Uri.EscapeUriString(_authCode)}";
            }

            return "_oidc_challenge";
        }

        public bool AuthorizeRequest(HttpRequestMessage message)
        {
            if(_idToken != null) {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _idToken);
                return true;
            }

            return false;
        }

        public IList LoginRequestForSite(Uri site)
        {
            throw new NotImplementedException();
        }

        public void ProcessLoginResponse(IDictionary<string, object> jsonResponse, IDictionary<string, string> headers, ref Exception error, Action<bool, Exception> continuation)
        {
            if(_refreshToken != null || _authCode != null) {
                if(error == null) {
                    // Generated or refreshed ID token:
                    // TODO: Should I look to see whether the server set a session cookie?
                    var idToken = jsonResponse.GetCast<string>("id_token");
                    if(idToken != null) {
                        _idToken = idToken;
                        _refreshToken = jsonResponse.GetCast<string>("refresh_token");
                    } else {
                        Log.To.Sync.W(Tag, "Server didn't return a refreshed ID token");
                        error = new CouchbaseLiteException("Server didn't return a refreshed ID token", StatusCode.UpStreamError);
                    }
                }
            } else {
                // Login challenge:
                if(error == null || Misc.IsUnauthorizedError(error)) {
                    var login = default(string);
                    var challenge = error.Data["AuthChallenge"].AsDictionary<string, string>();
                    if(challenge?.Get("Scheme") == "OIDC") {
                        login = challenge.Get("login");
                    }

                    if(login != null) {
                        Log.To.Sync.I(Tag, "Got OpenID Conect login URL: {0}", login);
                        var uri = default(Uri);
                        if(Uri.TryCreate(login, UriKind.Absolute, out uri)) {
                            LoginUri = uri;
                        } else {
                            Log.To.Sync.W(Tag, "Invalid OpenID Connect login received: {0}", login);
                            error = new CouchbaseLiteException("Invalid OpenID Connect login received", StatusCode.UpStreamError);
                        }
                    } else {
                        Log.To.Sync.W(Tag, "Server didn't provide an OpenID login url");
                        error = new CouchbaseLiteException("Server didn't provide an OpenID login url", StatusCode.UpStreamError);
                    }
                }
            }
        }
    }
}
