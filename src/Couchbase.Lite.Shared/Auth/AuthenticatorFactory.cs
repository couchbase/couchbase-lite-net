//
// AuthenticatorFactory.cs
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

namespace Couchbase.Lite.Auth
{
    /// <summary>
    /// A factory class for creating IAuthenticator objects
    /// </summary>
    public class AuthenticatorFactory
    {
        
        /// <summary>
        /// Creates an authenticator that handles an OpenID authentication flow
        /// </summary>
        /// <param name="manager">The manager associated with the replication to be performed</param>
        /// <param name="callback">The login callback to use</param>
        /// <returns>An initialized authenticator object</returns>
        public static IAuthenticator CreateOpenIDAuthenticator(Manager manager, OIDCCallback callback)
        {
            return new OpenIDAuthenticator(manager, callback);
        }

        /// <summary>
        /// Creates an object for handling HTTP Basic authentication
        /// </summary>
        /// <returns>The authenticator</returns>
        /// <param name="username">The username to use</param>
        /// <param name="password">The password to use</param>
        public static IAuthenticator CreateBasicAuthenticator(string username, string password)
        {
            return new BasicAuthenticator(username, password);
        }

        /// <summary>
        /// Creates an object for handling HTTP Digest authentication (experimental)
        /// </summary>
        /// <param name="username">The username to use</param>
        /// <param name="password">The password to use</param>
        /// <returns>The authenticator</returns>
        public static IAuthenticator CreateDigestAuthenticator(string username, string password)
        {
            return new DigestAuthenticator(username, password);
        }

        /// <summary>
        /// Creates an object for handling Facebook authentication
        /// </summary>
        /// <returns>The authenticator</returns>
        /// <param name="token">The facebook auth token</param>
        public static IAuthenticator CreateFacebookAuthenticator(string token)
        {
            var parameters = new Dictionary<string, string>();
            parameters["access_token"] = token;
            return new TokenAuthenticator("_facebook", parameters);
        }

        /// <summary>
        /// Creates an object for handling Persona authentication
        /// </summary>
        /// <returns>The authenticator</returns>
        /// <param name="assertion">The assertion object created by Persona</param>
        /// <param name="email">The email used in the assertion</param>
        public static IAuthenticator CreatePersonaAuthenticator(string assertion, string email)
        {
            var parameters = new Dictionary<string, string>();
            parameters["access_token"] = assertion;
            return new TokenAuthenticator("_persona", parameters);
        }

        internal static IAuthenticator CreateFromUri(Uri uri)
        {
            return (IAuthenticator)FacebookAuthorizer.FromUri(uri) 
                ?? (IAuthenticator)PersonaAuthorizer.FromUri(uri) 
                ?? BasicAuthenticator.FromUri(uri);
        }
    }
}
