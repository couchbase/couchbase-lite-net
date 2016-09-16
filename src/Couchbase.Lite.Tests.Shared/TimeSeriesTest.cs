//
// TimeSeriesTest.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using NUnit.Framework;
using Couchbase.Lite.Util;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using Couchbase.Lite.Tests;
using System.Linq;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class TimeSeriesTest : LiteTestCase
    {
        private const string ScratchDbName = "cbl_replicator_scratch";
        private static readonly DateTime FakeT0 = Misc.OffsetFromEpoch(TimeSpan.FromMilliseconds(100000));
        private TimeSeries _ts;
        private SyncGateway _sg;

        public TimeSeriesTest(string storageType) : base(storageType)
        {
        }

        protected override void SetUp()
        {
            base.SetUp();
            _sg = new SyncGateway(GetReplicationProtocol(), GetReplicationServer());
            Assert.DoesNotThrow(() => _ts = new TimeSeries(database, "tstest"), "Could not create ts");
        }

        protected override void TearDown()
        {
            _ts.Dispose();
            base.TearDown();
        }

        [Test]
        public void TestRemoteLogging()
        {
            var loggingDb = manager.GetDatabase("cbl-logging");
            var logger = new RemoteLogger(loggingDb, "remotelogging-test");
            Log.AddLogger(logger);
            var r = new Random();
            for (int i = 0; i < 15; i++) {
                
                var secs = r.NextDouble();
                Log.To.Database.W("RemoteLoggingTest", "A test warning message, waiting {0} sec", secs);
                Log.To.Database.I("RemoteLoggingTest", "A test info message, waiting {0} sec", secs);
                Log.To.Database.E("RemoteLoggingTest", "A test error message, waiting {0} sec", secs);
                Thread.Sleep(TimeSpan.FromSeconds(secs));
            }

            logger.Flush();
            logger.Dispose();

            Thread.Sleep(3000);

            Assert.AreEqual(1, loggingDb.GetDocumentCount());
            var doc = loggingDb.CreateAllDocumentsQuery().Run().First();
            var events = doc.Document.GetProperty("events").AsList<object>();
            Assert.GreaterOrEqual(events.Count, 45);
        }

        [Test]
        public void TestTimeSeries()
        {
            GenerateEventsSync();
            var i = 0;

            foreach (var row in database.CreateAllDocumentsQuery().Run()) {
                var t0 = Convert.ToUInt64(row.Document.GetProperty("t0"));
                Assert.Greater(t0, 0);
                var events = row.Document.GetProperty("events").AsList<IDictionary<string, object>>();
                Console.WriteLine("Doc {0}: {1} events starting at {2}", row.DocumentId, events.Count, t0);
                foreach (var evnt in events) {
                    Assert.AreEqual(i++, evnt.GetCast<int>("i"));
                    var dt = evnt.Get("dt");
                    if (dt != null) {
                        Assert.Greater(Convert.ToInt32(dt), 0);
                    }
                }
            }
        }

        [Test]
        public void TestPushTimeSeries()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled"))) {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(ScratchDbName)) {
                var tsPush = _ts.CreatePushReplication(remoteDb.RemoteUri, true);
                tsPush.Continuous = true;
                Console.WriteLine("Starting replication");

                var idleMre = new ManualResetEventSlim();
                var stoppedMre = new ManualResetEventSlim();
                tsPush.Changed += (sender, e) =>
                {
                    if(e.CompletedChangesCount == 10 &&e.Status == ReplicationStatus.Idle && 
                        e.Source.GetPendingDocumentIDs().Count == 0) {
                        idleMre.Set();
                    } else if(e.Status == ReplicationStatus.Stopped) {
                        stoppedMre.Set();
                    }
                };

                var waitHandle = GenerateEventsAsync();
                tsPush.Start();

                // Generate events:

                Console.WriteLine("Waiting for events...");
                Assert.IsTrue(waitHandle.WaitOne(TimeSpan.FromSeconds(10)), "Waiting for events timed out");
                Console.WriteLine("Waiting for replication to finish...");
                idleMre.Reset();
                Assert.IsTrue(idleMre.Wait(TimeSpan.FromSeconds(10)), "Timed out waiting for replication");
                idleMre.Dispose();
                Assert.IsNull(tsPush.LastError);

                // Stop the replication:
                tsPush.Stop();
                Console.WriteLine("Waiting for replication to stop");
                Assert.IsTrue(stoppedMre.Wait(TimeSpan.FromSeconds(10)), "Timed out waiting for replication to stop");
                stoppedMre.Dispose();
                Assert.IsNull(tsPush.LastError);

                // Did the docs get purged?
                Assert.AreEqual(0, database.GetDocumentCount());
            }
        }

        [Test]
        public void TestQueryTimeSeries()
        {
            var waitHandle = GenerateFakeEventsAsync();
            Assert.IsTrue(waitHandle.WaitOne(TimeSpan.FromSeconds(5)), "Timed out creating fake events");
            CheckQuery(DateTime.MinValue, DateTime.MaxValue, Tuple.Create(0, 9999, FakeT0, FakeT0.AddSeconds(4999)));
            CheckQuery(DateTime.MinValue, FakeT0 - TimeSpan.FromSeconds(1), 
                Tuple.Create(-1, -1, DateTime.MinValue, DateTime.MinValue));
            CheckQuery(FakeT0 + TimeSpan.FromSeconds(100), FakeT0 + TimeSpan.FromSeconds(200), 
                Tuple.Create(200, 401, FakeT0 + TimeSpan.FromSeconds(100), FakeT0 + TimeSpan.FromSeconds(200)));
            CheckQuery(FakeT0 + TimeSpan.FromSeconds(100), FakeT0 + TimeSpan.FromSeconds(2000),
                Tuple.Create(200, 4001, FakeT0 + TimeSpan.FromSeconds(100), FakeT0 + TimeSpan.FromSeconds(2000)));
            CheckQuery(FakeT0 + TimeSpan.FromSeconds(999), FakeT0 + TimeSpan.FromSeconds(1001),
                Tuple.Create(1998, 2003, FakeT0 + TimeSpan.FromSeconds(999), FakeT0 + TimeSpan.FromSeconds(1001)));
            CheckQuery(FakeT0 + TimeSpan.FromSeconds(999.5), FakeT0 + TimeSpan.FromSeconds(1001),
                Tuple.Create(2000, 2003, FakeT0 + TimeSpan.FromSeconds(1000), FakeT0 + TimeSpan.FromSeconds(1001)));
            CheckQuery(FakeT0 + TimeSpan.FromSeconds(1000), FakeT0 + TimeSpan.FromSeconds(1002),
                Tuple.Create(2000, 2005, FakeT0 + TimeSpan.FromSeconds(1000), FakeT0 + TimeSpan.FromSeconds(1002)));
            CheckQuery(FakeT0 + TimeSpan.FromSeconds(5555), DateTime.MinValue,
                Tuple.Create(-1, -1, DateTime.MinValue, DateTime.MinValue));
            CheckQuery(FakeT0 + TimeSpan.FromSeconds(5555), FakeT0 + TimeSpan.FromSeconds(999999),
                Tuple.Create(-1, -1, DateTime.MinValue, DateTime.MinValue));
        }

        private void CheckQuery(DateTime t0, DateTime t1, Tuple<int, int, DateTime, DateTime> expected)
        {
            var e = _ts.GetEventsInRange(t0, t1);
            int n = 0, i = -1;
            int i0 = -1, i1 = -1;
            var realT0 = DateTime.MinValue;
            var realT1 = DateTime.MinValue;
            var lastT = t0;
            foreach (var storedEvent in e) {
                i1 = storedEvent.GetCast<int>("i", -1);
                Assert.AreNotEqual(-1, i1);
                realT1 = storedEvent.GetCast<DateTime>("t");
                if (n++ == 0) {
                    i = i0 = i1;
                    realT0 = realT1;
                }

                Assert.AreEqual(i, i1);
                i++;
                Assert.GreaterOrEqual(realT1, lastT);
                Assert.LessOrEqual(realT1, t1);
                lastT = realT1;
            }

            if (t1 < DateTime.MaxValue) {
                Assert.IsTrue(realT1 <= t1);
            }

            Console.WriteLine("Query returned events {0:D4}-{1:D4} with time from {2} to {3}", i0, i1, realT0, realT1);
            Assert.AreEqual(expected.Item1, i0);
            Assert.AreEqual(expected.Item2, i1);
            Assert.AreEqual(expected.Item3, realT0);
            Assert.AreEqual(expected.Item4, realT1);
        }

        private WaitHandle GenerateFakeEventsAsync()
        {
            var mre = new ManualResetEventSlim();
            Task.Factory.StartNew(() =>
            {
                Console.WriteLine("Generating fake-time events...");
                var random = new Random();
                var t = FakeT0;
                for (int i = 0; i < 10000; i++) {
                    var r = random.Next();
                    _ts.AddEvent(new Dictionary<string, object> {
                        { "i", i },
                        { "random", r }
                    }, t);

                    if((i & 1) == 1) {
                        t = t.AddSeconds(1);
                    }
                }

                _ts.FlushAsync().ContinueWith(task => 
                {
                    mre.Set();
                    mre.Dispose();
                });
            }, TaskCreationOptions.LongRunning);

            return mre.WaitHandle;
        }

        // Generates 10000 events, one every 100µsec. Fulfils an expectation when they're all in the db.
        private WaitHandle GenerateEventsAsync()
        {
            var mre = new ManualResetEventSlim();
            Task.Factory.StartNew(() =>
            {
                GenerateEvents();
                _ts.FlushAsync().ContinueWith(t => 
                {
                    mre.Set();
                    mre.Dispose();
                });
            }, TaskCreationOptions.LongRunning);

            return mre.WaitHandle;
        }

        private void GenerateEventsSync()
        {
            GenerateEvents();
            _ts.Flush();
        }

        private void GenerateEvents()
        {
            Console.WriteLine("Generating events...");
            var sw = new Stopwatch();
            var random = new Random();
            for (int i = 0; i < 10000; i++) {
                sw.Start();
                while((sw.ElapsedTicks * 1000000) / Stopwatch.Frequency < 100) {
                    Thread.SpinWait(10);
                }
                sw.Reset();
                var r = random.Next();
                _ts.AddEvent(new Dictionary<string, object> {
                    { "i", i },
                    { "random", r }
                });
            }
        }
    }
}

