// 
// ProxyAuthenticator.cs
// 
// Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Sync;

/// <summary>
/// A class for storing credentials for a proxy that needs authentication
/// </summary>
public sealed class ProxyAuthenticator
{
    /// <summary>
    /// Gets the username sent to the proxy
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    /// Gets the password sent to the proxy
    /// </summary>
    public string Password { get; init; }

    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="username">The username to send to the proxy</param>
    /// <param name="password">The password to send to the proxy</param>
    public ProxyAuthenticator(string username, string password)
    {
        Username = username;
        Password = password;
    }

    internal void Authenticate(ReplicatorOptionsDictionary options)
    {
        var authDict = new AuthOptionsDictionary
        {
            Username = Username,
            Password = Password
        };

        options.ProxyAuth = authDict;
    }
}