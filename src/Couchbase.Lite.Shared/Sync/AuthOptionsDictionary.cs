// 
// AuthOptionsDictionary.cs
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
using System.Collections.Generic;
using System.Security;

namespace Couchbase.Lite.Sync;

/// <summary>
/// The type of authentication credentials that an <see cref="AuthOptionsDictionary"/>
/// holds
/// </summary>
internal enum AuthType
{
    /// <summary>
    /// HTTP Basic (RFC 2617)
    /// </summary>
    HttpBasic,

    /// <summary>
    /// TLS client cert
    /// </summary>
    ClientCert = 4
}

/// <summary>
/// A container that stores login information for authenticating with
/// a remote endpoint
/// </summary>
internal sealed class AuthOptionsDictionary : OptionsDictionary
{
    private const string PasswordKey = "password";
    private const string TypeKey = "type";
    private const string UsernameKey = "username";

    private AuthType _authType;

    /// <summary>
    /// Gets or sets the password for the credentials (not applicable in all cases)
    /// </summary>
    public string? Password
    {
        get => this[PasswordKey] as string;
        set => this[PasswordKey] = value;
    }

    /// <summary>
    /// Gets or sets the password for the credentials (not applicable in all cases)
    /// </summary>
    public SecureString? PasswordSecureString
    {
        get => this[PasswordKey] as SecureString;
        set => this[PasswordKey] = value;
    }

    /// <summary>
    /// Gets or sets the type of authentication to be used
    /// </summary>
    public AuthType Type
    {
        get => _authType;
        set {
            _authType = value;
            this[TypeKey] = value == AuthType.HttpBasic ? "Basic" : "Client Cert";
        }
    }

    /// <summary>
    /// Gets or sets the username to be used
    /// </summary>
    public string? Username
    {
        get => this[UsernameKey] as string;
        set => this[UsernameKey] = value;
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    public AuthOptionsDictionary()
    {
        Type = AuthType.HttpBasic;
        Username = String.Empty;
        Password = String.Empty;
        PasswordSecureString = new();
    }

    internal AuthOptionsDictionary(AuthOptionsDictionary other)
    {
        Password = other.Password;
        PasswordSecureString = other.PasswordSecureString;
        Type = other.Type;
        Username = other.Username;
    }

    internal AuthOptionsDictionary(Dictionary<string, object?> raw) : base(raw)
    {
            
    }

    internal override bool KeyIsRequired(string key) => 
        key is TypeKey or UsernameKey or PasswordKey;

    internal override bool Validate(string key, object? value)
    {
        switch (key) {
            case TypeKey:
                if (value is not string str) {
                    return false;
                }
                
                return str is "Basic" or "Client Cert";
            default:
                return true;
        }
    }
}