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
#define USE_AUTH

using System;
using System.Collections.Generic;
using System.Threading;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Listener;
using Couchbase.Lite.Listener.Tcp;
using Couchbase.Lite.Util;
using Mono.Zeroconf.Providers.Bonjour;
using NUnit.Framework;

namespace Couchbase.Lite
{
    public class PeerToPeerTest : LiteTestCase
    {
        private const string TAG = "PeerToPeerTest";
        private const string LISTENER_DB_NAME = "listy";
        private const int DOCUMENT_COUNT = 100;
        private const int MIN_ATTACHMENT_LENGTH = 4000;
        private const int MAX_ATTACHMENT_LENGTH = 100000;

        private Database _listenerDB;
        private CouchbaseLiteTcpListener _listener;
        private Uri _listenerDBUri;
        private ushort _port = 60010;
        private Random _rng = new Random(DateTime.Now.Millisecond);

        [Test]
        public void TestBrowser()
        {
            #if __ANDROID__
            if(global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.JellyBean) {
                Assert.Inconclusive("PeerToPeer requires API level 16, but found {0}", global::Android.OS.Build.VERSION.Sdk);
            }
            #endif

            //Use a short timeout to speed up the test since it is performed locally
            //Android will get stuck in DNSProcessResult which hangs indefinitely if
            //no results are found (Which will happen if registration is aborted between
            //the resolve reply and query record steps)
            ServiceParams.Timeout = TimeSpan.FromSeconds(3); 
            var mre = new ManualResetEventSlim();
            CouchbaseLiteServiceBrowser browser = new CouchbaseLiteServiceBrowser(new ServiceBrowser());
            browser.ServiceResolved += (sender, e) => {
                Log.D(TAG, "Discovered service: {0}", e.Service.Name);
                if(e.Service.Name == TAG) {
                    mre.Set();
                }
            };

            browser.ServiceRemoved += (o, args) => {
                Log.D(TAG, "Service destroyed: {0}", args.Service.Name);
                if(args.Service.Name == TAG) {
                    mre.Set();
                }
            };
            browser.Start();

            CouchbaseLiteServiceBroadcaster broadcaster = new CouchbaseLiteServiceBroadcaster(new RegisterService(), 59840);
            broadcaster.Name = TAG;
            broadcaster.Start();
            Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(10)));

            //FIXME.JHB:  Why does Linux hate this part sporadically?
            mre.Reset();
            broadcaster.Dispose();
            var success = mre.Wait(TimeSpan.FromSeconds(10));
            browser.Dispose();
            Assert.IsTrue(success);
        }

        protected override void SetUp()
        {
            base.SetUp();

            _listenerDB = EnsureEmptyDatabase(LISTENER_DB_NAME);
            _listener = new CouchbaseLiteTcpListener(manager, _port);
            #if USE_AUTH
            _listener.SetPasswords(new Dictionary<string, string> { { "bob", "slack" } });
            #endif

            _listenerDBUri = new Uri("http://localhost:" + _port + "/" + LISTENER_DB_NAME);
            _listener.Start();
        }

        protected override void TearDown()
        {
            base.TearDown();

            _listener.Stop();
            _port++;
        }

        [Test]
        public void TestPush()
        {
            CreateDocs(database, false);
            var repl = CreateReplication(database, true);
            RunReplication(repl);
            VerifyDocs(_listenerDB, false);
        }

        [Test]
        public void TestPull()
        {
            CreateDocs(_listenerDB, false);
            var repl = CreateReplication(database, false);
            RunReplication(repl);
            VerifyDocs(database, false);
        }

        [Test]
        public void TestPushWithAttachment()
        {
            CreateDocs(database, true);
            var repl = CreateReplication(database, true);
            RunReplication(repl);
            VerifyDocs(_listenerDB, true);
        }

        [Test]
        public void TestPullWithAttachment()
        {
            CreateDocs(_listenerDB, true);
            var repl = CreateReplication(database, false);
            RunReplication(repl);
            VerifyDocs(database, true);
        }

        private Replication CreateReplication(Database db, bool push)
        {
            Replication repl = null;
            if (push) {
                repl = db.CreatePushReplication(_listenerDBUri);
            } else {
                repl = db.CreatePullReplication(_listenerDBUri);
            }

            #if USE_AUTH
            repl.Authenticator = new DigestAuthenticator("bob", "slack");
            #endif

            return repl;
        }

        private void CreateDocs(Database db, bool withAttachments)
        {
            Log.D(TAG, "Creating {0} documents in {1}", DOCUMENT_COUNT, db.Name);
            db.RunInTransaction(() =>
            {
                for(int i = 1; i <= DOCUMENT_COUNT; i++) {
                    var doc = database.GetDocument(String.Format("doc-{0}", i));
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
                var doc = database.GetDocument(String.Format("doc-{0}", i));
                Assert.AreEqual(i, doc.UserProperties["index"]);
                Assert.AreEqual(false, doc.UserProperties["bar"]);
                if (withAttachments) {
                    Assert.IsNotNull(doc.CurrentRevision.GetAttachment("README"));
                }
            }

            Assert.AreEqual(DOCUMENT_COUNT, database.DocumentCount);
        }

    }
}

