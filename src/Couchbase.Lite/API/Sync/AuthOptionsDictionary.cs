// 
// AuthOptionsDictionary.cs
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
using System.Collections.Generic;

namespace Couchbase.Lite.Sync
{
    public enum AuthType
    {
        HttpBasic
    }

    public sealed class AuthOptionsDictionary : OptionsDictionary
    {
        #region Constants

        private static readonly string[] AuthTypes = {
            "Basic", "Session", "OpenID Connect", "Facebook", "Client Cert"
        };

        private const string PasswordKey = "password";
        private const string TypeKey = "type";
        private const string UsernameKey = "username";

        #endregion

        #region Variables

        private AuthType _authType;

        #endregion

        #region Properties

        public string Password
        {
            get => this[PasswordKey] as string;
            set => this[PasswordKey] = value;
        }

        public AuthType Type
        {
            get => _authType;
            set {
                _authType = value;
                this[TypeKey] = AuthTypes[(int)value];
            }
        }

        public string Username
        {
            get => this[UsernameKey] as string;
            set => this[UsernameKey] = value;
        }

        #endregion

        #region Constructors

        public AuthOptionsDictionary()
        {
            Type = AuthType.HttpBasic;
            Username = String.Empty;
            Password = String.Empty;
        }

        internal AuthOptionsDictionary(Dictionary<string, object> raw) : base(raw)
        {
            
        }

        #endregion

        #region Overrides

        protected override bool KeyIsRequired(string key)
        {
            return key == TypeKey || key == UsernameKey || key == PasswordKey;
        }

        protected override bool Validate(string key, object value)
        {
            switch (key) {
                case TypeKey:
                    return Array.IndexOf(AuthTypes, value as string) != -1;
                default:
                    return true;
            }
        }

        #endregion
    }
}
