// 
// ReplicatorOptionsDictionary.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Sync
{
    public sealed class ReplicatorOptionsDictionary : OptionsDictionary
    {
        #region Constants

        private const string AuthOption = "auth";
        private const string HeadersKey = "headers";

        #endregion

        #region Properties

        public AuthOptionsDictionary Auth
        {
            get => this.GetCast<AuthOptionsDictionary>(AuthOption);
            set => this[AuthOption] = value;
        }

        public IDictionary<string, object> Headers
        {
            get => this.GetCast<IDictionary<string, object>>(HeadersKey);
            private set => this[HeadersKey] = value;
        }

        #endregion

        #region Constructors

        public ReplicatorOptionsDictionary()
        {
            Headers = new Dictionary<string, object>();
        }

        #endregion

        #region Overrides

        protected override void FreezeInternal()
        {
            if (Auth == null) {
                return;
            }

            Auth.Freeze();

            // HACK: Until the underlying implementation accepts creds, use headers
            var cipher = Encoding.UTF8.GetBytes($"{Auth.Username}:{Auth.Password}");
            var encodedVal = Convert.ToBase64String(cipher);
            var authHeaderValue = $"Basic {encodedVal}";
            Headers["Authorization"] = authHeaderValue;

            Remove(AuthOption);
        }

        protected override bool KeyIsRequired(string key)
        {
            return false;
        }

        protected override bool Validate(string key, object value)
        {
            switch (key) {
                case AuthOption:
                    return value is AuthOptionsDictionary;
                default:
                    return true;
            }
        }

        #endregion
    }
}
