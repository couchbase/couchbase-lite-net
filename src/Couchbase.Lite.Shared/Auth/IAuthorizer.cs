//
//  IAuthorizer.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Couchbase.Lite.Auth
{
    internal interface IAuthorizer : IAuthenticator
    {
        Uri RemoteUrl { get; set; }

        string Username { get; }

        bool RemoveStoredCredentials();
    }

    internal interface ICredentialAuthorizer : IAuthorizer
    {
        NetworkCredential Credentials { get; }
    }

    internal interface ICustomHeadersAuthorizer : IAuthorizer
    {
        bool AuthorizeRequest(HttpRequestMessage request);

        string AuthorizationHeaderValue { get; }
    }

    internal interface ILoginAuthorizer : IAuthorizer
    {
        IList LoginRequest();

        void ProcessLoginResponse(IDictionary<string, object> jsonResponse, HttpRequestHeaders headers,
            Exception error, Action<bool, Exception> continuation);
    }

    internal interface ISessionCookieAuthorizer : ILoginAuthorizer
    {

    }
}
