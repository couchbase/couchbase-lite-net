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
using System;
using NUnit.Framework;
using Couchbase.Lite.PeerToPeer;
using Couchbase.Lite.Util;
using System.Threading;

namespace Couchbase.Lite
{
    public class PeerToPeerTest : LiteTestCase
    {
        private const string TAG = "PeerToPeerTest";

        [Test]
        public void TestBrowser()
        {
            var mre = new ManualResetEventSlim();
            CouchbaseLiteServiceBrowser browser = new CouchbaseLiteServiceBrowser(null);
            browser.ServiceResolved += (sender, e) => {
                Log.D(TAG, "Discovered service: {0}", e.Service.Name);
                mre.Set();
            };
            browser.Start();

            CouchbaseLiteServiceBroadcaster broadcaster = new CouchbaseLiteServiceBroadcaster(null);
            broadcaster.Name = "Foo";
            broadcaster.Start();
            Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void TestListener()
        {
            var listener = new CouchbaseLiteServiceListener(Manager.SharedInstance, 59840);
            listener.Start();
            var db = Manager.SharedInstance.GetDatabase("db");
            var replication = db.CreatePullReplication(new Uri("http://localhost:59840/db"));
            replication.Start();

            Thread.Sleep(50000);
        }
    }
}

