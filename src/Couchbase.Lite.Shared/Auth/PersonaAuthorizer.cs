//
// PersonaAuthorizer.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
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
using System.IO;
using System.Text;

using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using System.Collections.Concurrent;
using System.Collections;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Auth
{
    internal class PersonaAuthorizer : ISessionCookieAuthorizer
    {
        private const string QueryParameter = "personaAssertion";
        private static readonly string Tag = nameof(PersonaAuthorizer);
        private static readonly ConcurrentDictionary<string, string> Assertions =
            new ConcurrentDictionary<string, string>();

        public string Email { get; private set; }

        public string UserInfo
        {
            get {
                throw new NotImplementedException();
            }
        }

        public string Scheme
        {
            get {
                throw new NotImplementedException();
            }
        }

        public bool UsesCookieBasedLogin
        {
            get {
                throw new NotImplementedException();
            }
        }

        public PersonaAuthorizer(string emailAddress)
        {
            if(emailAddress == null) {
                Log.To.Sync.E(Tag, "null email address in constructor, throwing...");
                throw new ArgumentNullException(nameof(emailAddress));
            }

            Email = emailAddress;
        }

        public static PersonaAuthorizer FromUri(Uri uri)
        {
            var personaAssertion = URIUtils.GetQueryParameter(uri, QueryParameter);

            if(personaAssertion != null && !StringEx.IsNullOrWhiteSpace(personaAssertion)) {
                var email = RegisterAssertion(personaAssertion);
                var authorizer = new PersonaAuthorizer(email);
                return authorizer;
            }

            return null;
        }

        public static string RegisterAssertion(string assertion)
        {
            var email = default(string);
            var origin = default(string);
            var exp = default(DateTime);
            if(!ParseAssertion(assertion, out email, out origin, out exp)) {
                Log.To.Sync.W(Tag, "Unable to parse assertion {0}, returning null...", new SecureLogString(assertion,
                    LogMessageSensitivity.Insecure));
                return null;
            }

            // Normalize the origin URL string:
            var originURL = default(Uri);
            if(!Uri.TryCreate(origin, UriKind.RelativeOrAbsolute, out originURL)) {
                Log.To.Sync.W(Tag, "Unable to parse assertion origin {0}, returning null...", origin);
                return null;
            }

            origin = originURL.GetLeftPart(UriPartial.Authority);

            var key = $"{email}:{origin}";
            Assertions[key] = assertion;
            return email;
        }

        public static string GetAssertion(string email, Uri site)
        {
            var key = $"{email}:{site.GetLeftPart(UriPartial.Authority)}";
            var assertion = default(string);
            return Assertions.TryGetValue(key, out assertion) ? assertion : null;
        }

        public string GetAssertion(Uri site)
        {
            var assertion = GetAssertion(Email, site);
            if(assertion == null) {
                Log.To.Sync.W(Tag, "No assertion found for {0}", new SecureLogUri(site));
                return null;
            }

            var email = default(string);
            var origin = default(string);
            var exp = default(DateTime);
            if(!ParseAssertion(assertion, out email, out origin, out exp) || exp == Misc.Epoch) {
                Log.To.Sync.W(Tag, "Assertion invalid or expired {0}", new SecureLogString(assertion,
                    LogMessageSensitivity.Insecure));
                return null;
            }

            return assertion;
        }

        public IList LoginRequestForSite(Uri site)
        {
            var assertion = GetAssertion(site);
            if(assertion == null) {
                return null;
            }

            return new ArrayList { "POST", site.AbsolutePath + "_persona", assertion };
        }

        public void ProcessLoginResponse(IDictionary<string, object> jsonResponse, IDictionary<string, string> headers, Exception error, Action<bool, Exception> continuation)
        {
            // No-op
        }

        private static IDictionary<string, object> DecodeComponent(IList<string> components, int index)
        {
            var bodyData = StringUtils.ConvertFromUnpaddedBase64String(components[index]);
            if(bodyData == null) {
                return null;
            }

            return Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(bodyData);
        }

        internal static bool ParseAssertion(string assertion, out string email, out string origin, out DateTime exp)
        {
            email = null;
            origin = null;
            exp = Misc.Epoch;

            // https://github.com/mozilla/id-specs/blob/prod/browserid/index.md
            // http://self-issued.info/docs/draft-jones-json-web-token-04.html
            var components = assertion.Split('.');
            if(components.Length < 4) {
                return false;
            }

            var body = DecodeComponent(components, 1);
            var principal = body?.Get("principal")?.AsDictionary<string, object>();
            email = principal?.GetCast<string>("email");

            body = DecodeComponent(components, 3);
            origin = body?.GetCast<string>("aud");
            var expNum = default(long);
            if(body != null && body.TryGetValue("exp", out expNum)) {
                exp = Misc.OffsetFromEpoch(TimeSpan.FromMilliseconds(expNum));
            }

            return email != null && origin != null && exp != Misc.Epoch;
        }

        public string LoginPathForSite(Uri site)
        {
            throw new NotImplementedException();
        }

        public IDictionary<string, string> LoginParametersForSite(Uri site)
        {
            throw new NotImplementedException();
        }
    }
}