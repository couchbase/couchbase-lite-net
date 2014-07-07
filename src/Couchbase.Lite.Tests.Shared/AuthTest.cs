//
// AuthTest.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//     Pasin Suriyentrakorn <pasin@couchbase.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 Couchbase Inc
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
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using Sharpen;
using NUnit.Framework;
using System.Net;
using System.Net.Http;

namespace Couchbase.Lite
{
    public class AuthTest : LiteTestCase
	{
        const string Tag = "AuthTest";

        [Test]
		public void TestParsePersonaAssertion()
		{
			try
			{
				Log.D(Database.Tag, "testParsePersonaAssertion");
				var sampleAssertion = "eyJhbGciOiJSUzI1NiJ9.eyJwdWJsaWMta2V5Ijp7ImFsZ29yaXRobSI6IkRTIiwieSI6ImNhNWJiYTYzZmI4MDQ2OGE0MjFjZjgxYTIzN2VlMDcwYTJlOTM4NTY0ODhiYTYzNTM0ZTU4NzJjZjllMGUwMDk0ZWQ2NDBlOGNhYmEwMjNkYjc5ODU3YjkxMzBlZGNmZGZiNmJiNTUwMWNjNTk3MTI1Y2NiMWQ1ZWQzOTVjZTMyNThlYjEwN2FjZTM1ODRiOWIwN2I4MWU5MDQ4NzhhYzBhMjFlOWZkYmRjYzNhNzNjOTg3MDAwYjk4YWUwMmZmMDQ4ODFiZDNiOTBmNzllYzVlNDU1YzliZjM3NzFkYjEzMTcxYjNkMTA2ZjM1ZDQyZmZmZjQ2ZWZiZDcwNjgyNWQiLCJwIjoiZmY2MDA0ODNkYjZhYmZjNWI0NWVhYjc4NTk0YjM1MzNkNTUwZDlmMWJmMmE5OTJhN2E4ZGFhNmRjMzRmODA0NWFkNGU2ZTBjNDI5ZDMzNGVlZWFhZWZkN2UyM2Q0ODEwYmUwMGU0Y2MxNDkyY2JhMzI1YmE4MWZmMmQ1YTViMzA1YThkMTdlYjNiZjRhMDZhMzQ5ZDM5MmUwMGQzMjk3NDRhNTE3OTM4MDM0NGU4MmExOGM0NzkzMzQzOGY4OTFlMjJhZWVmODEyZDY5YzhmNzVlMzI2Y2I3MGVhMDAwYzNmNzc2ZGZkYmQ2MDQ2MzhjMmVmNzE3ZmMyNmQwMmUxNyIsInEiOiJlMjFlMDRmOTExZDFlZDc5OTEwMDhlY2FhYjNiZjc3NTk4NDMwOWMzIiwiZyI6ImM1MmE0YTBmZjNiN2U2MWZkZjE4NjdjZTg0MTM4MzY5YTYxNTRmNGFmYTkyOTY2ZTNjODI3ZTI1Y2ZhNmNmNTA4YjkwZTVkZTQxOWUxMzM3ZTA3YTJlOWUyYTNjZDVkZWE3MDRkMTc1ZjhlYmY2YWYzOTdkNjllMTEwYjk2YWZiMTdjN2EwMzI1OTMyOWU0ODI5YjBkMDNiYmM3ODk2YjE1YjRhZGU1M2UxMzA4NThjYzM0ZDk2MjY5YWE4OTA0MWY0MDkxMzZjNzI0MmEzODg5NWM5ZDViY2NhZDRmMzg5YWYxZDdhNGJkMTM5OGJkMDcyZGZmYTg5NjIzMzM5N2EifSwicHJpbmNpcGFsIjp7ImVtYWlsIjoiamVuc0Btb29zZXlhcmQuY29tIn0sImlhdCI6MTM1ODI5NjIzNzU3NywiZXhwIjoxMzU4MzgyNjM3NTc3LCJpc3MiOiJsb2dpbi5wZXJzb25hLm9yZyJ9.RnDK118nqL2wzpLCVRzw1MI4IThgeWpul9jPl6ypyyxRMMTurlJbjFfs-BXoPaOem878G8-4D2eGWS6wd307k7xlPysevYPogfFWxK_eDHwkTq3Ts91qEDqrdV_JtgULC8c1LvX65E0TwW_GL_TM94g3CvqoQnGVxxoaMVye4ggvR7eOZjimWMzUuu4Lo9Z-VBHBj7XM0UMBie57CpGwH4_Wkv0V_LHZRRHKdnl9ISp_aGwfBObTcHG9v0P3BW9vRrCjihIn0SqOJQ9obl52rMf84GD4Lcy9NIktzfyka70xR9Sh7ALotW7rWywsTzMTu3t8AzMz2MJgGjvQmx49QA~eyJhbGciOiJEUzEyOCJ9.eyJleHAiOjEzNTgyOTY0Mzg0OTUsImF1ZCI6Imh0dHA6Ly9sb2NhbGhvc3Q6NDk4NC8ifQ.4FV2TrUQffDya0MOxOQlzJQbDNvCPF2sfTIJN7KOLvvlSFPknuIo5g";
				var result = PersonaAuthorizer.ParseAssertion(sampleAssertion);
				var email = (string)result.Get(PersonaAuthorizer.AssertionFieldEmail);
				var origin = (string)result.Get(PersonaAuthorizer.AssertionFieldOrigin);

				Assert.AreEqual(email, "jens@mooseyard.com");
				Assert.AreEqual(origin, "http://localhost:4984/");
				Assert.AreEqual(PersonaAuthorizer.RegisterAssertion(sampleAssertion), email);

				Uri originURL = new Uri(origin);
				var gotAssertion = PersonaAuthorizer.AssertionForEmailAndSite(email, originURL);
				Assert.AreEqual(gotAssertion, sampleAssertion);
				
                // variant form of URL
				originURL = new Uri("Http://LocalHost:4984/");
				gotAssertion = PersonaAuthorizer.AssertionForEmailAndSite(email, originURL);
				Assert.AreEqual(sampleAssertion, gotAssertion);

				var auth = new PersonaAuthorizer(email);
				Assert.AreEqual(email, auth.GetEmailAddress());
				Assert.AreEqual(null, auth.AssertionForSite(originURL));
			}
			catch (Exception e)
			{
				Assert.Fail(e.Message);
			}
		}

        [Test]
        public void TestAuthenticationFactory()
        {
            var basicAuth = AuthenticatorFactory.CreateBasicAuthenticator("username", "password");
            Assert.IsNotNull(basicAuth);
            Assert.IsTrue(basicAuth is BasicAuthenticator);

            var facebookAuth = AuthenticatorFactory.CreateFacebookAuthenticator("DUMMY_TOKEN");
            Assert.IsNotNull(facebookAuth);
            Assert.IsTrue(facebookAuth is TokenAuthenticator);

            var personalAuth = AuthenticatorFactory.CreatePersonaAuthenticator("DUMMY_ASSERTION", null);
            Assert.IsNotNull(personalAuth);
            Assert.IsTrue(personalAuth is TokenAuthenticator);
        }

        [Test]
        public void TestTokenAuthenticator() 
        {
            var loginPath = "_facebook";

            var parameters = new Dictionary<string, string>();
            parameters["access_token"] = "facebookaccesstoken";
            TokenAuthenticator tokenAuth = new TokenAuthenticator(loginPath, parameters);

            var tokenAuthParams = tokenAuth.LoginParametersForSite(null);
            Assert.IsNotNull(tokenAuthParams);
            Assert.AreEqual(tokenAuthParams.Count, parameters.Count);
            Assert.AreEqual(tokenAuthParams["access_token"], parameters["access_token"]);
            Assert.AreEqual(tokenAuth.LoginPathForSite(null), "/_facebook");
            Assert.IsTrue(tokenAuth.UsesCookieBasedLogin);
            Assert.IsNull(tokenAuth.AuthUserInfo);
        }

        [Test]
        public void TestBasicAuthenticator() 
        {
            var username = "username";
            var password = "password";
            var basicAuth = new BasicAuthenticator(username, password);

            Assert.IsNull(basicAuth.LoginParametersForSite(null));
            Assert.IsTrue(basicAuth.UsesCookieBasedLogin);
            Assert.AreEqual(basicAuth.AuthUserInfo, username + ":" + password);
        }

        [Test]
        public void TestGetCredentialsIfAvailable()
        {
            var username1 = "username1";
            var password1 = "password1";
            var auth = AuthenticatorFactory.CreateBasicAuthenticator(username1, password1);

            var credentials = AuthUtils.GetCredentialsIfAvailable(auth, null);
            Assert.IsNotNull(credentials);
            Assert.AreEqual(username1, ((NetworkCredential)credentials).UserName);
            Assert.AreEqual(password1, ((NetworkCredential)credentials).Password);

            var username2 = "username2";
            var password2 = "password2";
            var userinfo = username2 + ":" + password2;
            var uri = new Uri("http://" + userinfo + "@couchbase.com");
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
               
            credentials = AuthUtils.GetCredentialsIfAvailable(null, request);
            Assert.IsNotNull(credentials);
            Assert.AreEqual(username2, ((NetworkCredential)credentials).UserName);
            Assert.AreEqual(password2, ((NetworkCredential)credentials).Password);

            credentials = AuthUtils.GetCredentialsIfAvailable(auth, request);
            Assert.IsNotNull(credentials);
            Assert.AreEqual(username2, ((NetworkCredential)credentials).UserName);
            Assert.AreEqual(password2, ((NetworkCredential)credentials).Password);

            uri = new Uri("http://www.couchbase.com");
            request = new HttpRequestMessage(HttpMethod.Get, uri);
            credentials = AuthUtils.GetCredentialsIfAvailable(null, request);
            Assert.IsNull(credentials);

            auth = AuthenticatorFactory.CreateFacebookAuthenticator("1234");
            credentials = AuthUtils.GetCredentialsIfAvailable(null, null);
            Assert.IsNull(credentials);
        }
	}
}
