//
// TokenAuthenticator.cs
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
    /// An object that can verify authentication via token (like Facebook or Persona)
    /// </summary>
    public class TokenAuthenticator : IAuthenticator
    {

        #region Variables

        private readonly string _loginPath;
        private readonly IDictionary<string, string> _loginParams;

        #endregion

        #region Properties
        #pragma warning disable 1591

        // IAuthenticator
        public bool UsesCookieBasedLogin { get { return true; } }

        // IAuthenticator
        public string UserInfo { get { return null; } }

        // IAuthenticator
        public string Scheme { get { return null; } }

        #pragma warning restore 1591
        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loginPath">The login path to use</param>
        /// <param name="loginParams">The login headers to use</param>
        public TokenAuthenticator(string loginPath, IDictionary<String, String> loginParams) 
        {
            _loginPath = loginPath;
            _loginParams = loginParams;
        }

        #endregion

        #region IAuthenticator
        #pragma warning disable 1591

        public string LoginPathForSite(Uri site) 
        {
            var path = _loginPath;
            if (path != null && !path.StartsWith("/")) {
                path = "/" + path;
            }
            return path;
        }

        public IDictionary<string, string> LoginParametersForSite(Uri site) 
        {
            return _loginParams;
        }

        #pragma warning restore 1591
        #endregion
    }
}
