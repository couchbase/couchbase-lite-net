// 
// SessionAuthenticator.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Globalization;
using System.Net;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// A class that will authenticate using a session cookie.  This can be used for things like
    /// Sync Gateway admin created sessions, or implicit authentication flow (e.g. OpenID Connect
    /// where the authentication is done already)
    /// </summary>
    public sealed class SessionAuthenticator : Authenticator
    {
        #region Properties

        /// <summary>
        /// Gets the name of the cookie to store the session in
        /// </summary>
        public string CookieName { get; }

        /// <summary>
        /// Gets the optional expiration date for this session
        /// </summary>
        public DateTimeOffset? Expires { get; }

        /// <summary>
        /// Gets the session ID to set as the cookie value
        /// </summary>
        public string SessionID { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sessionID"><see cref="SessionID"/></param>
        /// <param name="expires"><see cref="Expires"/></param>
        /// <param name="cookieName"><see cref="CookieName"/></param>
        public SessionAuthenticator(string sessionID, DateTimeOffset? expires, string cookieName)
        {
            SessionID = sessionID;
            Expires = expires;
            CookieName = cookieName;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sessionID"><see cref="SessionID"/></param>
        /// <param name="expires">An ISO-8601 string representing a date for <see cref="Expires"/></param>
        /// <param name="cookieName"><see cref="CookieName"/></param>
        public SessionAuthenticator(string sessionID, string expires, string cookieName)
        {
            if (DateTimeOffset.TryParseExact(expires, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresDate)) {
                Expires = expiresDate;
            }

            SessionID = sessionID;
            CookieName = cookieName;
        }

        #endregion

        #region Overrides

        internal override void Authenticate(ReplicatorOptionsDictionary options)
        {
            var cookie = new Cookie(CookieName, SessionID);
            if (Expires.HasValue) {
                cookie.Expires = Expires.Value.DateTime;
            }
            
            options.Cookies.Add(cookie);
        }

        #endregion
    }
}
