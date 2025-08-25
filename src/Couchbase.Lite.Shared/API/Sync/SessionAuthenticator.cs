// 
//  SessionAuthenticator.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

using System.Net;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Sync;

/// <summary>
/// A class that will authenticate using a session cookie.  This can be used for things like
/// Sync Gateway admin created sessions, or implicit authentication flow (e.g. OpenID Connect
/// where the authentication is done already)
/// </summary>
public sealed class SessionAuthenticator : Authenticator
{
    private const string Tag = nameof(SessionAuthenticator);

    /// <summary>
    /// Gets the name of the cookie to store the session in
    /// </summary>
    public string CookieName { get; }

    /// <summary>
    /// Gets the session ID to set as the cookie value
    /// </summary>
    public string SessionID { get; }

    /// <summary>
    /// Constructor using the given cookie name
    /// </summary>
    /// <param name="sessionID"><see cref="SessionID"/></param>
    /// <param name="cookieName"><see cref="CookieName"/></param>
    public SessionAuthenticator(string sessionID, string cookieName)
    {
        SessionID = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(sessionID), sessionID);
        CookieName = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(cookieName), cookieName);
    }

    /// <summary>
    /// Constructor using the default cookie name for Sync Gateway ('SyncGatewaySession')
    /// </summary>
    /// <param name="sessionID"><see cref="SessionID"/></param>
    public SessionAuthenticator(string sessionID)
        // ReSharper disable once IntroduceOptionalParameters.Global
        : this(sessionID, "SyncGatewaySession")
    {
    }

    internal override void Authenticate(ReplicatorOptionsDictionary options)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(options), options);

        var cookie = new Cookie(CookieName, SessionID);
        options.Cookies.Add(cookie);
    }
}