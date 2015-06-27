//
// IAuthenticator.cs
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
using System.Net.Http;

namespace Couchbase.Lite.Auth
{
    /// <summary>
    /// An interface describing an object that can perform authentication
    /// </summary>
    public interface IAuthenticator
    {
        /// <summary>
        /// Gets info about the user, if applicable
        /// </summary>
        string UserInfo { get; }

        /// <summary>
        /// Get the authentication scheme, if applicable
        /// </summary>
        string Scheme { get; }

        /// <summary>
        /// Gets whether or not this login method uses cookies
        /// </summary>
        bool UsesCookieBasedLogin { get; }

        /// <summary>
        /// Gets the login path for a particular site
        /// </summary>
        /// <returns>The login path</returns>
        /// <param name="site">The site uri</param>
        string LoginPathForSite(Uri site);

        /// <summary>
        /// Gets the authentication headers for a particular site, based on the
        /// authentication info contained
        /// </summary>
        /// <returns>The authentication headers</returns>
        /// <param name="site">The uri of the site</param>
        IDictionary<string, string> LoginParametersForSite(Uri site);

    }

    /// <summary>
    /// A specialized IAuthenticator that will handle a challenge response scenario
    /// (for example, Digest authentication)
    /// </summary>
    public interface IChallengeResponseAuthenticator : IAuthenticator
    {
        /// <summary>
        /// Creates a response for a challenge which will be placed into the 'Authorization'
        /// HTTP header
        /// </summary>
        /// <returns>The challenge, including the scheme (i.e. Digest xxxx)</returns>
        /// <param name="response">The 401 Unauthorized message that was received</param>
        string ResponseFromChallenge(HttpResponseMessage response);

        /// <summary>
        /// Setup the authenticator to make a request (some auth mechanisms
        /// differ depending on the request URI and even HTTP method)
        /// </summary>
        /// <param name="request">Request.</param>
        void PrepareWithRequest(HttpRequestMessage request);
    }

}
