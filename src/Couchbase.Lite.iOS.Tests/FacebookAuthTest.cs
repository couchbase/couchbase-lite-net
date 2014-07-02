using System;
using NUnit.Framework;
using MonoTouch.Accounts;
using System.Threading;
using Couchbase.Lite.Util;
using Couchbase.Lite.Auth;
using Sharpen;
using System.Threading.Tasks;
using Couchbase.Lite.Support;
using System.Net;

namespace Couchbase.Lite
{
    public class FacebookAuthTest : LiteTestCase
    {
        const string Tag = "FacebookAuthTest";
        const string APP_ID = "618392634906728";

        [Test]
        public void TestFacebookAuth()
        {
            var doneEvent = new ManualResetEvent(false);

            var accountStore = new ACAccountStore();

            var accountType = accountStore.FindAccountType(ACAccountType.Facebook);

            var options = new AccountStoreOptions();
            options.FacebookAppId = APP_ID;
            options.SetPermissions(ACFacebookAudience.Friends, new [] { "email" });

            var success = true;
            ACAccount account = null;
            accountStore.RequestAccess(accountType, options, (result, error) => 
            {
                success = result;
                if (success)
                {
                    var accounts = accountStore.FindAccounts(accountType);
                    account = accounts != null && accounts.Length > 0 ? accounts[0] : null;
                }
                else
                {
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

            var url = GetReplicationURLWithoutCredentials();

            var httpClientFactory = new CouchbaseLiteHttpClientFactory();
            manager.DefaultHttpClientFactory = httpClientFactory;
            Replication replicator = database.CreatePushReplication(url);
            replicator.Authenticator = AuthenticatorFactory.CreateFacebookAuthenticator(token);

            Assert.IsNotNull(replicator);
            Assert.IsNotNull(replicator.Authenticator);
            Assert.IsTrue(replicator.Authenticator is TokenAuthenticator);

            replicator.Start();

            doneEvent.Reset();
            var task = Task.Factory.StartNew(()=>
            {
                var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                while (DateTime.UtcNow < timeout)
                {
                    if (!replicator.active)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(10));
                }
                doneEvent.Set();
            });
            doneEvent.WaitOne(TimeSpan.FromSeconds(35));

            var urlStr = url.ToString();
            urlStr = urlStr.EndsWith("/") ? urlStr : urlStr + "/";
            var cookies = httpClientFactory.cookieStore.GetCookies(new Uri(urlStr));
            Assert.IsTrue(cookies.Count == 1);
            Assert.AreEqual("SyncGatewaySession", cookies[0].Name);
        }
    }
}
