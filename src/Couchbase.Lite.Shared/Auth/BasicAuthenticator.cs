//
// BasicAuthenticator.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc
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
using System.Text;
using Couchbase.Lite.Util;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Couchbase.Lite.Auth
{

    /// <summary>
    /// An object that can authenticate using HTTP basic authentication
    /// </summary>
    public class BasicAuthenticator : ICredentialAuthorizer, ICustomHeadersAuthorizer
    {

        #region Constants

        private const string Tag = nameof(BasicAuthenticator);

        #endregion

        #region Variables

        private string _basicAuthorization;

        #endregion

        #region Properties
        #pragma warning disable 1591

        public string AuthorizationHeaderValue
        {
            get {
                if(_basicAuthorization == null) {
                    if(Credentials.UserName != null && Credentials.Password != null) {
                        var plaintext = $"{Credentials.UserName}:{Credentials.Password}";
                        var seekrit = Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
                        _basicAuthorization = $"Basic {seekrit}";
                    }
                }

                return _basicAuthorization;
            }
        }

        public NetworkCredential Credentials
        {
            get; private set;
        }

        // IAuthenticator
        public bool UsesCookieBasedLogin { get { return false; } }

        // IAuthenticator
        public string UserInfo
        {
            get
            {
                return AuthorizationHeaderValue?.Split(' ')?.ElementAt(1);
            }
        }

        // IAuthenticator
        public string Scheme { get { return "Basic"; } }

        #pragma warning restore 1591
        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="username">The username for the auth</param>
        /// <param name="password">The password for the auth</param>
        public BasicAuthenticator(string username, string password) 
        {
            Credentials = new NetworkCredential(username, password);
        }

        public BasicAuthenticator(NetworkCredential credentials)
        {
            Credentials = credentials;
        }

        #endregion

        #region Public Methods

        public static BasicAuthenticator FromUri(Uri uri)
        {
            var userInfo = uri?.UserInfo;
            if(!String.IsNullOrEmpty(userInfo)) {
                var parts = userInfo.Split(':');
                if(parts.Length != 2) {
                    Log.To.Sync.W(Tag, "Unable to parse user info from URL ({0}), not creating Authenticator...",
                        new SecureLogString(userInfo, LogMessageSensitivity.Insecure));
                    return null;
                }

                return new BasicAuthenticator(parts[0], parts[1]);
            }

            return null;
        }

        #endregion

        #region Overrides
#pragma warning disable 1591

        public override string ToString()
        {
            return String.Format("[BasicAuthenticator ({0}:{1})]", 
                new SecureLogString(Credentials.UserName, LogMessageSensitivity.PotentiallyInsecure), 
                new SecureLogString(Credentials.Password, LogMessageSensitivity.Insecure));
        }

        #endregion

        #region IAuthenticator

        public string LoginPathForSite(Uri site) 
        {
            return null;
        }
            
        public IDictionary<String, String> LoginParametersForSite(Uri site) 
        {
            // This method has different implementation from the iOS's.
            // It is safe to return NULL as the method is not called
            // when Basic Authenticator is used. Also theoretically, the
            // standard Basic Auth doesn't add any additional parameters
            // to the login url.
            return null;
        }

        public bool AuthorizeRequest(HttpRequestMessage message)
        {
            var auth = UserInfo;
            if(auth == null) {
                return false;
            }

            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            return true;
        }

        #pragma warning restore 1591
        #endregion

    }
}

