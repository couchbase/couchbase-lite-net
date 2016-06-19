//
//  PeerToPeerTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
//#define USE_AUTH

using System;
using System.Collections.Generic;
using System.Threading;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Listener;
using Couchbase.Lite.Listener.Tcp;
using Couchbase.Lite.Util;
using Mono.Zeroconf.Providers.Bonjour;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Couchbase.Lite.Security;
using Couchbase.Lite.Tests;
using System.Text;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class PeerToPeerTest : LiteTestCase
    {
        private const string TAG = "PeerToPeerTest";
        private const string LISTENER_DB_NAME = "listy";
        private const int DOCUMENT_COUNT = 50;
        private const int MIN_ATTACHMENT_LENGTH = 4000;
        private const int MAX_ATTACHMENT_LENGTH = 100000;

        private Database _listenerDB;
        private CouchbaseLiteTcpListener _listener;
        private Uri _listenerDBUri;
        private ushort _port = 59840;
        private AuthenticationSchemes _authScheme;
        private Random _rng = new Random(DateTime.Now.Millisecond);

        public PeerToPeerTest(string storageType) : base(storageType) {}

        protected override void SetUp()
        {
            base.SetUp();

            _listenerDB = EnsureEmptyDatabase(LISTENER_DB_NAME);
        }

        protected override void TearDown()
        {
            _listener?.Stop();

            base.TearDown();
        }

        [Test]
        public void TestExternalReplicationStart()
        {
            var sg = new SyncGateway(GetReplicationProtocol(), GetReplicationServer());
            var tmp = EnsureEmptyDatabase("test_db");
            tmp.Close();

            SetupListener(false);
            CreateDocuments(database, 10);
            using (var remoteDb = sg.CreateDatabase("external_replication_test")) {
                var request = WebRequest.Create("http://localhost:" + _port + "/_replicate");
                request.ContentType = "application/json";
                request.Method = "POST";
                var body = String.Format("{{\"source\":\"{0}\",\"target\":\"{1}\"}}", database.Name, remoteDb.RemoteUri);
                var bytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = bytes.Length;
                request.GetRequestStream().Write(bytes, 0, bytes.Length);

                var response = (HttpWebResponse)request.GetResponse();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                request = WebRequest.Create("http://localhost:" + _port + "/_replicate");
                request.ContentType = "application/json";
                request.Method = "POST";
                body = String.Format("{{\"source\":\"{0}\",\"target\":\"test_db\",\"create_target\":true}}", remoteDb.RemoteUri);
                bytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = bytes.Length;
                request.GetRequestStream().Write(bytes, 0, bytes.Length);

                response = (HttpWebResponse)request.GetResponse();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                var createdDb = manager.GetExistingDatabase("test_db");
                Assert.IsNotNull(createdDb);
                Assert.AreEqual(10, createdDb.GetDocumentCount());
            }
        }

        [Test]
        public void TestSsl()
        {
            var cert = X509Manager.GetPersistentCertificate("127.0.0.1", "123abc", System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "unit_test.pfx"));
            var sslListener = new CouchbaseLiteTcpListener(manager, 59841, CouchbaseLiteTcpOptions.UseTLS, cert);
            sslListener.Start();

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = 
                (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
            {
                // If the certificate is a valid, signed certificate, return true.
                if (sslPolicyErrors == SslPolicyErrors.None || sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch)
                {
                    return true;
                }

                // If there are errors in the certificate chain, look at each error to determine the cause.
                if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
                {
                    if (chain != null && chain.ChainStatus != null)
                    {
                        foreach (X509ChainStatus status in chain.ChainStatus)
                        {
                            if ((certificate.Subject == certificate.Issuer) &&
                                (status.Status == X509ChainStatusFlags.UntrustedRoot))
                            {
                                // Self-signed certificates with an untrusted root are valid. 
                                continue;
                            }
                            else
                            {
                                if (status.Status != X509ChainStatusFlags.NoError)
                                {
                                    // If there are any other errors in the certificate chain, the certificate is invalid,
                                    // so the method returns false.
                                    return false;
                                }
                            }
                        }
                    }

                    // When processing reaches this line, the only errors in the certificate chain are 
                    // untrusted root errors for self-signed certificates. These certificates are valid
                    // for default Exchange server installations, so return true.
                    return true;
                }
                else
                {
                    // In all other cases, return false.
                    return false;
                }
            };

            try {
                var request = (HttpWebRequest)WebRequest.Create("https://127.0.0.1:59841/");
                var response = (HttpWebResponse)request.GetResponse();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:59841/");
                Assert.Throws<WebException>(() => response = (HttpWebResponse)request.GetResponse());
            } finally {
                sslListener.Stop();
            }
        }

        [Test]
        public void TestListenerRequestsAreExternal()
        {
            var fakeListener = new CouchbaseLiteMockTcpListener(59842);
            fakeListener.ContextGenerator = context =>
            {
                var internalContext = new CouchbaseListenerTcpContext(context.Request, context.Response, manager);
                var uriBuilder = new UriBuilder("http", context.Request.LocalEndPoint.Address.ToString(),
                    context.Request.LocalEndPoint.Port);
                uriBuilder.UserName = context.User != null && context.User.Identity != null ? context.User.Identity.Name : null;
                internalContext.Sender = uriBuilder.Uri;
                return internalContext;
            };
            _listenerDBUri = new Uri("http://127.0.0.1:59842/" + LISTENER_DB_NAME);
            fakeListener.Start();
            try {
                CreateDocs(database, false);
                var repl = CreateReplication(database, true);
                var allChangesExternal = true;
                _listenerDB.Changed += (sender, e) => 
                {
                    allChangesExternal = allChangesExternal && e.IsExternal;
                };

                RunReplication(repl);
                VerifyDocs(_listenerDB, false);
                Assert.IsTrue(allChangesExternal);
            } finally {
                fakeListener.Stop();
            }
        }

        [Test]
        public void TestBrowser()
        {
            #if __ANDROID__
            if(global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.JellyBean) {
                Assert.Inconclusive("PeerToPeer requires API level 16, but found {0}", global::Android.OS.Build.VERSION.Sdk);
            }
            #endif

            Log.Domains.All.Level = Log.LogLevel.None;
            Log.Domains.Discovery.Level = Log.LogLevel.Debug;

            //Use a short timeout to speed up the test since it is performed locally
            //Android will get stuck in DNSProcessResult which hangs indefinitely if
            //no results are found (Which will happen if registration is aborted between
            //the resolve reply and query record steps)
            ServiceParams.Timeout = TimeSpan.FromSeconds(3); 
            var mre1 = new ManualResetEventSlim();
            var mre2 = new ManualResetEventSlim();
            CouchbaseLiteServiceBrowser browser = new CouchbaseLiteServiceBrowser(new ServiceBrowser());
            browser.ServiceResolved += (sender, e) => {
                Log.To.Discovery.I(TAG, "Discovered service: {0}", e.Service.Name);
                if(e.Service.Name == TAG) {
                    mre1.Set();
                }
            };

            browser.ServiceRemoved += (o, args) => {
                Log.To.Discovery.I(TAG, "Service destroyed: {0}", args.Service.Name);
                if(args.Service.Name == TAG) {
                    mre2.Set();
                }
            };
            browser.Start();

            CouchbaseLiteServiceBroadcaster broadcaster = new CouchbaseLiteServiceBroadcaster(new RegisterService(), 59840);
            broadcaster.Name = TAG;
            broadcaster.Start();
            Assert.IsTrue(mre1.Wait(TimeSpan.FromSeconds(10)));

            //FIXME.JHB:  Why does Linux hate this part sporadically?
            broadcaster.Dispose();
            var success = mre2.Wait(TimeSpan.FromSeconds(10));
            browser.Dispose();
            Assert.IsTrue(success);
            mre1.Dispose();
            mre2.Dispose();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPush(bool secure)
        {
            SetupListener(secure);
            try {
                CreateDocs(database, false);
                var repl = CreateReplication(database, true);
                RunReplication(repl);
                VerifyDocs(_listenerDB, false);
            } finally {
                _listener.Stop();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPull(bool secure)
        {
            SetupListener(secure);
            try {
                CreateDocs(_listenerDB, false);
                var repl = CreateReplication(database, false);
                repl.Continuous = true;
                var allChangesExternal = true;
                database.Changed += (sender, e) => 
                {
                    allChangesExternal = allChangesExternal && e.IsExternal;
                };

                RunReplication(repl);
                VerifyDocs(database, false);
                Assert.IsTrue(allChangesExternal);
            } finally {
                _listener.Stop();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPushWithAttachment(bool secure)
        {
            SetupListener(secure);
            try {
                CreateDocs(database, true);
                var repl = CreateReplication(database, true);
                RunReplication(repl);
                Assert.IsNull(repl.LastError, "Error during replication");
                VerifyDocs(_listenerDB, true);
            } finally {
                _listener.Stop();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPullWithAttachment(bool secure)
        {
            SetupListener(secure);
            try {
                CreateDocs(_listenerDB, true);
                var repl = CreateReplication(database, false);
                RunReplication(repl);
                Assert.IsNull(repl.LastError, "Error during replication");
                VerifyDocs(database, true);
            } finally {
                _listener.Stop();
            }
        }

        private Replication CreateReplication(Database db, bool push)
        {
            Replication repl = null;
            if (push) {
                repl = db.CreatePushReplication(_listenerDBUri);
            } else {
                repl = db.CreatePullReplication(_listenerDBUri);
            }

            if (_authScheme == AuthenticationSchemes.Basic) {
                repl.Authenticator = new BasicAuthenticator("bob", "slack");
            } else if (_authScheme == AuthenticationSchemes.Digest) {
                repl.Authenticator = new DigestAuthenticator("bob", "slack");
            }

            return repl;
        }

        private void SetupListener(bool secure)
        {
            var opts = CouchbaseLiteTcpOptions.Default;
            if (_authScheme == AuthenticationSchemes.Basic) {
                opts |= CouchbaseLiteTcpOptions.AllowBasicAuth;
            }

            if (secure) {
                var cert = X509Manager.GetPersistentCertificate("127.0.0.1", "123abc", System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "unit_test.pfx"));
                _listenerDBUri = new Uri(String.Format("https://localhost:{0}/{1}/", _port, LISTENER_DB_NAME));
                _listener = new CouchbaseLiteTcpListener(manager, _port, opts | CouchbaseLiteTcpOptions.UseTLS, cert);  
            } else {
                _listenerDBUri = new Uri(String.Format("http://localhost:{0}/{1}/", _port, LISTENER_DB_NAME));
                _listener = new CouchbaseLiteTcpListener(manager, _port, opts); 
            }

            if (_authScheme != AuthenticationSchemes.None) {
                _listener.SetPasswords(new Dictionary<string, string> { { "bob", "slack" } });
            }

            _listener.Start();
        }

        private void CreateDocs(Database db, bool withAttachments)
        {
            WriteDebug("Creating {0} documents in {1}", DOCUMENT_COUNT, db.Name);
            db.RunInTransaction(() =>
            {
                for(int i = 1; i <= DOCUMENT_COUNT; i++) {
                    var doc = db.GetDocument(String.Format("doc-{0}", i));
                    var rev = doc.CreateRevision();
                    rev.SetUserProperties(new Dictionary<string, object> {
                        { "index", i },
                        { "bar", false }
                    });

                    if(withAttachments) {
                        int length = (int)(MIN_ATTACHMENT_LENGTH + _rng.Next() / 
                            (double)Int32.MaxValue * (MAX_ATTACHMENT_LENGTH - MIN_ATTACHMENT_LENGTH));
                        var data = new byte[length];
                        _rng.NextBytes(data);
                        rev.SetAttachment("README", "application/octet-stream", data);
                    }

                    Assert.DoesNotThrow(() => rev.Save());
                }

                return true;
            });
        }

        private void VerifyDocs(Database db, bool withAttachments)
        {
            for (int i = 1; i <= DOCUMENT_COUNT; i++) {
                var doc = db.GetExistingDocument(String.Format("doc-{0}", i));
                Assert.IsNotNull(doc);
                Assert.AreEqual(i, doc.UserProperties["index"]);
                Assert.AreEqual(false, doc.UserProperties["bar"]);
                if (withAttachments) {
                    Assert.IsNotNull(doc.CurrentRevision.GetAttachment("README"));
                }
            }

            Assert.AreEqual(DOCUMENT_COUNT, db.GetDocumentCount());
        }
    }
}

