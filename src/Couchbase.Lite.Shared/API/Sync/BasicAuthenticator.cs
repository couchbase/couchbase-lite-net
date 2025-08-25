// 
//  BasicAuthenticator.cs
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

using System;
using System.Security;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Sync;

/// <summary>
/// An object that will authenticate a <see cref="Replicator"/> using
/// HTTP Basic authentication
/// </summary>
public sealed class BasicAuthenticator : Authenticator
{
    private const string Tag = nameof(BasicAuthenticator);

    /// <summary>
    /// Gets the username that this object holds
    /// </summary>
    public string Username { get; }

    /// <summary>
    /// Gets the password that this object holds
    /// </summary>
    public string Password { get; } = "";

    /// <summary>
    /// Gets the password that this object holds
    /// </summary>
    public SecureString PasswordSecureString { get; } = new SecureString();

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="username">The username to send through HTTP Basic authentication</param>
    /// <param name="password">The password to send through HTTP Basic authentication</param>
    public BasicAuthenticator(string username, string password)
    {
        Username = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(username), username);
        Password = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(password), password);
    }

    /// <summary>
    /// Construct a basic authenticator with username and password
    /// </summary>
    /// <param name="username">The username to send through HTTP Basic authentication</param>
    /// <param name="password">The password to send through HTTP Basic authentication</param>
    public BasicAuthenticator(string username, SecureString password)
    {
        Username = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(username), username);
        PasswordSecureString = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(password), password);
    }

    internal override void Authenticate(ReplicatorOptionsDictionary options)
    {
        var authDict = new AuthOptionsDictionary
        {
            Username = Username,
            Type = AuthType.HttpBasic
        };

        if (String.IsNullOrEmpty(Password)) {
            authDict.PasswordSecureString = PasswordSecureString;
        } else {
            authDict.Password = Password;
        }

        options.Auth = authDict;
    }
}