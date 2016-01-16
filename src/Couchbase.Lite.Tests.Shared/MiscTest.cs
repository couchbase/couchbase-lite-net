//
// MiscTest.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/*
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/

using Couchbase.Lite;
using NUnit.Framework;
using Sharpen;
using Couchbase.Lite.Util;
using System.Net.Http;
using System.Collections.Generic;
using System;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Store;

namespace Couchbase.Lite
{
    public class MiscTest : LiteTestCase
    {
        const string Tag = "MiscTest";

        public MiscTest(string storageType) : base(storageType) {}

        [Test]
        public void TestServerVersionParsing()
        {
            var oldVersion = new RemoteServerVersion("Couchbase Sync Gateway/1.1.0");
            Assert.IsTrue(oldVersion.IsSyncGateway);
            Assert.AreEqual("Couchbase Sync Gateway", oldVersion.Name);
            Assert.AreEqual("1.1.0", oldVersion.Version);
            Assert.AreEqual(String.Empty, oldVersion.Branch);
            Assert.AreEqual(String.Empty, oldVersion.Commit);

            var nonSGVersion = new RemoteServerVersion("CouchDB/1.6.1");
            Assert.IsFalse(nonSGVersion.IsSyncGateway);
            Assert.AreEqual("CouchDB", nonSGVersion.Name);
            Assert.AreEqual("1.6.1", nonSGVersion.Version);
            Assert.AreEqual(String.Empty, nonSGVersion.Branch);
            Assert.AreEqual(String.Empty, nonSGVersion.Commit);

            var newVersion = new RemoteServerVersion("Couchbase Sync Gateway/1.2 branch/fix/server_header commit/5bfcf79");
            Assert.IsTrue(newVersion.IsSyncGateway);
            Assert.AreEqual("Couchbase Sync Gateway", newVersion.Name);
            Assert.AreEqual("1.2", newVersion.Version);
            Assert.AreEqual("fix/server_header", newVersion.Branch);
            Assert.AreEqual("5bfcf79", newVersion.Commit);
        }

        [Test]
        public void TestFacebookAuthorizer()
        {
            const string token = "pyrzqxgl";
            var site = new Uri("https://example.com/database");
            const string email = "jimbo@example.com";

            // Register and retrieve the sample token:
            var auth = new FacebookAuthorizer(email);
            Assert.IsTrue(FacebookAuthorizer.RegisterAccessToken(token, email, site));
            var gotToken = auth.TokenForSite(site);
            Assert.AreEqual(token, gotToken);

            // Register and retrieve the sample token:
            gotToken = auth.TokenForSite(new Uri("HttpS://example.com:443/some/other/path"));
            Assert.AreEqual(token, gotToken);
            Assert.AreEqual(new Dictionary<string, string> { { "access_token", token } }, 
                auth.LoginParametersForSite(site));
        }

        [Test]
        public void TestUnquoteString()
        {
            string testString = "attachment; filename=\"attach\"";
            string expected = "attachment; filename=attach";
            string result = Misc.UnquoteString(testString);
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestForestDBViewNameEscaping()
        {
            var invalidName = "#@vuName!!/crazy:–¼";
            var escapedName = ForestDBViewStore.ViewNameToFilename(invalidName);
            Assert.AreEqual("@23@40vuName@21@21@2fcrazy@3a–¼.viewindex", escapedName);

            var unescapedName = ForestDBViewStore.FileNameToViewName(escapedName);
            Assert.AreEqual(invalidName, unescapedName);
        }

        [Test]
        public void TestTransientRetryHandler()
        {
            Assert.Inconclusive("Need to implement a scriptable http service, like Square's MockWebServer.");

            // Arrange
            var handler = new TransientErrorRetryHandler(new HttpClientHandler());
            var client = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/foo");

            // Act
            //HttpResponseMessage response = null;

            try {
                client.SendAsync(request).ContinueWith(t => {
                    Assert.Pass();
                });
            } catch (HttpRequestException e) {
                Log.E(Tag, "Transient exception not handled", e);
                Assert.Fail("Transient exception not handled");
            }
        }
    }
}
