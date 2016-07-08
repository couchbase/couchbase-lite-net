//
//  Authorizer.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Auth
{
    internal abstract class Authorizer : IAuthorizer
    {
        public Uri RemoteUrl { get; set; }

        public string Username { get; protected set; }

        public string LocalUUID { get; set; }

        public abstract string Scheme { get; }
        public abstract string UserInfo { get; }
        public abstract bool UsesCookieBasedLogin { get; }

        public abstract IDictionary<string, string> LoginParametersForSite(Uri site);
        public abstract string LoginPathForSite(Uri site);

        public virtual bool RemoveStoredCredentials()
        {
            return true;
        }
    }
}
