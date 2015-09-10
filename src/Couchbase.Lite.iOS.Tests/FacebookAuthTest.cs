//
// FacebookAuthTest.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
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
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using NUnit.Framework;
using Accounts;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Couchbase.Lite.Tests;


namespace Couchbase.Lite
{
    public class FacebookAuthTest : LiteTestCase
    {
        const string Tag = "FacebookAuthTest";

        [Test]
        public void TestFacebookAuth()
        {
            var sg = new SyncGateway("http", GetReplicationServer());
            using (var remoteDb = sg.CreateDatabase("facebook")) {
                remoteDb.DisableGuestAccess();
                var doneEvent = new ManualResetEvent(false);

                var accountStore = new ACAccountStore();
                var accountType = accountStore.FindAccountType(ACAccountType.Facebook);

                var options = new AccountStoreOptions();
                options.FacebookAppId = FacebookAppId;
                options.SetPermissions(ACFacebookAudience.Friends, new [] { "email" });

                var success = true;
                ACAccount account = null;
                accountStore.RequestAccess(accountType, options, (result, error) =>
                {
                    success = result;
                    if (success) {
                        var accounts = accountStore.FindAccounts(accountType);
                        account = accounts != null && accounts.Length > 0 ? accounts[0] : null;
                    }
                    else {
                        Log.W(Tag, "Facebook Login needed. Go to Settings > Facebook and login.");
                        Log.E(Tag, "Facebook Request Access Error : " + error);
                    }
                    doneEvent.Set();
                });

                doneEvent.WaitOne(TimeSpan.FromSeconds(30));

                Assert.IsTrue(success);
                Assert.IsNotNull(account);

                var token = account.Credential.OAuthToken;
                Assert.IsNotNull(token);
                Assert.IsTrue(token.Length > 0);

                var url = remoteDb.RemoteUri;

                var cookieStore = new CookieStore(manager.Directory);
                var httpClientFactory = new CouchbaseLiteHttpClientFactory(cookieStore);
                manager.DefaultHttpClientFactory = httpClientFactory;
                Replication replicator = database.CreatePushReplication(url);
                replicator.Authenticator = AuthenticatorFactory.CreateFacebookAuthenticator(token);

                Assert.IsNotNull(replicator);
                Assert.IsNotNull(replicator.Authenticator);
                Assert.IsTrue(replicator.Authenticator is TokenAuthenticator);

                CreateDocuments(database, 20);

                RunReplication(replicator);

                var urlStr = url.ToString();
                urlStr = urlStr.EndsWith("/") ? urlStr : urlStr + "/";
                //var cookies = httpClientFactory.GetCookieContainer().GetCookies(new Uri(urlStr));
                //Assert.IsTrue(cookies.Count == 1);
                //Assert.AreEqual("SyncGatewaySession", cookies[0].Name);
            }
        }
    }
}
