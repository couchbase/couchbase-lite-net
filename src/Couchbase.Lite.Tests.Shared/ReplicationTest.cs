//
// ReplicationTest.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Tests;
using Couchbase.Lite.Util;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Net.Sockets;
using Couchbase.Lite.Revisions;
using System.Diagnostics;
using FluentAssertions;

#if NET_3_5
using WebRequest = System.Net.Couchbase.WebRequest;
using HttpWebRequest = System.Net.Couchbase.HttpWebRequest;
using HttpWebResponse = System.Net.Couchbase.HttpWebResponse;
using WebException = System.Net.Couchbase.WebException;
#endif


namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class ReplicationTest : LiteTestCase
    {
        private const string Tag = "ReplicationTest";
        private const string TEMP_DB_NAME = "testing_tmp";
        private SyncGateway _sg;
        private static int _dbCounter = 0;
        private HttpClient _httpClient = new HttpClient();

        public ReplicationTest(string storageType) : base(storageType) {}

        private class ReplicationIdleObserver 
        {
            private readonly CountdownEvent doneSignal;

            internal ReplicationIdleObserver(CountdownEvent doneSignal) {
                this.doneSignal = doneSignal;
            }
                
            public void Changed(object sender, ReplicationChangeEventArgs args) {
                var replicator = args.Source;
                if (replicator.Status == ReplicationStatus.Idle && doneSignal.CurrentCount > 0) {
                    doneSignal.Signal();
                }
            }
        }

        private class ReplicationErrorObserver 
        {
            private readonly CountdownEvent doneSignal;

            internal ReplicationErrorObserver(CountdownEvent doneSignal) {
                this.doneSignal = doneSignal;
            }

            public void Changed(object sender, ReplicationChangeEventArgs args) {
                if (args.LastError != null && doneSignal.CurrentCount > 0) {
                    doneSignal.Signal();
                }
            }
        }
 
        private string TempDbName()
        {
            return TEMP_DB_NAME + _dbCounter++;
        }

        private void PutReplicationOffline(Replication replication)
        {
            var doneEvent = new ManualResetEvent(false);
            replication.Changed += (object sender, ReplicationChangeEventArgs e) => 
            {
                if (e.Source.Status == ReplicationStatus.Offline) {
                    doneEvent.Set();
                }
            };

            replication.GoOffline();

            var success = doneEvent.WaitOne(TimeSpan.FromSeconds(30));
            Assert.IsTrue(success);
        }

        private Boolean IsSyncGateway(Uri remote) 
        {
            return (remote.Port == 4984 || remote.Port == 4985);
        }

        private IDictionary<string, object> GetRemoteDoc(Uri remote, string checkpointId)
        {
            var url = new Uri(string.Format("{0}/_local/{1}", remote, checkpointId));
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            using(var client = new HttpClient()) {
                var response = client.SendAsync(request).Result;
                var result = response.Content.ReadAsStringAsync().Result;
                var json = Manager.GetObjectMapper().ReadValue<JObject>(result);
                return json.AsDictionary<string, object>();
            }
        }

        private void CreatePullAndTest(int docCount, RemoteDatabase db, Action<Replication> tester, ReplicationOptions options = null)
        {
            if(options == null) {
                options = new ReplicationOptions();
            }

            db.AddDocuments(20, false);

            var pull = database.CreatePullReplication(db.RemoteUri);
            pull.ReplicationOptions = options;
            RunReplication(pull);

            WriteDebug("Document count at end {0}", database.GetDocumentCount());
            tester(pull);
        }

        private Stream ZipDatabase(Database db, string zipName)
        {
            var zipPath = Path.Combine(Path.GetTempPath(), zipName);
            if (File.Exists(zipPath)) {
                File.Delete(zipPath);
            }

            using (var outStream = new ZipOutputStream(File.OpenWrite(zipPath)) { IsStreamOwner = true }) {
                var fileName = Path.GetFileName(db.DbDirectory);
                CompressFolder(db.DbDirectory, outStream, db.DbDirectory.Length - fileName.Length);
            }

            return File.OpenRead(zipPath);
        }

        private void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset) {
            ZipEntry dirEntry = new ZipEntry(ZipEntry.CleanName(Path.GetFileName(path) + "/"));
            zipStream.PutNextEntry(dirEntry);
            zipStream.CloseEntry();

            string[] files = Directory.GetFiles(path);

            foreach (string filename in files) {

                FileInfo fi = new FileInfo(filename);

                string entryName = filename.Substring(folderOffset); // Makes the name in zip based on the folder
                entryName = ZipEntry.CleanName(entryName); // Removes drive from name and fixes slash direction
                ZipEntry newEntry = new ZipEntry(entryName);
                newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity
                newEntry.Size = fi.Length;

                zipStream.PutNextEntry(newEntry);

                // Zip the file in buffered chunks
                // the "using" will close the stream even if an exception occurs
                using (FileStream streamReader = File.OpenRead(filename)) {
                    streamReader.CopyTo(zipStream);
                }
                zipStream.CloseEntry();
            }
            string[ ] folders = Directory.GetDirectories(path);
            foreach (string folder in folders) {
                CompressFolder(folder, zipStream, folderOffset);
            }
        }

        protected override void SetUp()
        {
            base.SetUp();

            _sg = new SyncGateway(GetReplicationProtocol(), GetReplicationServer());
        }

        [Test]
        public void TestFailedRevisionDuringPull()
        {
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                remoteDb.AddDocuments(50, false);
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.ReplicationOptions.ReplicationRetryDelay = TimeSpan.FromSeconds(5);
                pull.Continuous = true;
                pull.Start();
                Thread.Sleep(500);
                pull.RevisionFailed();
                Thread.Sleep(20000);
                pull.Status.Should().Be(ReplicationStatus.Idle, "because the replication should not stop");
            }
        }

        [Test]
        public void TestRejectedDocument()
        {
            var push = database.CreatePushReplication(GetReplicationURL());
            var wait = new WaitAssert();
            push.Changed += (sender, e) => {
                var err = e.LastError as HttpResponseException;
                if(err != null) {
                    wait.RunAssert(() => err.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                                                                    "because the document should be rejected"));
                }

                var err2 = e.LastError as CouchbaseLiteException;
                if(err2 != null) {
                    wait.RunAssert(() => err2.Code.Should().Be(StatusCode.Forbidden,
                                                                    "because the document should be rejected"));
                }
            };

            database.CreateDocument().PutProperties(new Dictionary<string, object> {
                ["reject"] = false
            });

            database.CreateDocument().PutProperties(new Dictionary<string, object> {
                ["reject"] = true
            });

            push.Start();

            wait.WaitForResult(TimeSpan.FromSeconds(5));

            var docWithAttach = database.CreateDocument();
            docWithAttach.Update(rev => {
                rev.SetUserProperties(new Dictionary<string, object> {
                    ["reject"] = true
                });

                rev.SetAttachment("foo", "foo/bar", Enumerable.Repeat<byte>((byte)'a', 100000));
                return true;
            });

            wait = new WaitAssert();
            push.Start();

            wait.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Test]
        public void TestUnauthorized()
        {
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                remoteDb.DisableGuestAccess();
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                var wait = new WaitAssert();
                pull.Changed += (sender, e) => {
                    var err = e.LastError as HttpResponseException;
                    if(err != null) {
                        wait.RunAssert(() => Assert.AreEqual(HttpStatusCode.Unauthorized, err.StatusCode));
                    }
                };
                pull.Start();

                wait.WaitForResult(TimeSpan.FromSeconds(5));

                wait = new WaitAssert();
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                push.Changed += (sender, e) => {
                    var err = e.LastError as HttpResponseException;
                    if(err != null) {
                        wait.RunAssert(() => Assert.AreEqual(HttpStatusCode.Unauthorized, err.StatusCode));
                    }
                };
                push.Start();

                wait.WaitForResult(TimeSpan.FromSeconds(5));
            }
        }

        [Test]
        public void TestDeepRevTree()
        {
            const int NumRevisions = 200;
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                var doc = database.GetDocument("deep");
                var numRevisions = 0;
                for(numRevisions = 0; numRevisions < NumRevisions;) {
                    database.RunInTransaction(() =>
                    {
                        // Have to push the doc periodically, to make sure the server gets the whole
                        // history, since CBL will only remember the latest 20 revisions.
                        var batchSize = Math.Min(database.GetMaxRevTreeDepth(), NumRevisions - numRevisions);
                        Trace.WriteLine($"Adding revisions {numRevisions + 1} -- {numRevisions + batchSize} ...");
                        for(int i = 0; i < batchSize; i++) {
                            doc.Update(rev =>
                            {
                                var props = rev.UserProperties;
                                props["counter"] = ++numRevisions;
                                rev.SetUserProperties(props);
                                return true;
                            });
                        }

                        return true;
                    });

                    Trace.WriteLine("Pushing ...");
                    RunReplication(push);
                }

                Trace.WriteLine($"{Environment.NewLine}{Environment.NewLine}$$$$$$$$$$ PULLING TO DB2 $$$$$$$$$$");

                // Now create a second database and pull the remote db into it:
                var db2 = EnsureEmptyDatabase("db2");
                var pull = db2.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(pull);

                var doc2 = db2.GetExistingDocument("deep");
                Assert.AreEqual(db2.GetMaxRevTreeDepth(), doc2.RevisionHistory.Count());
                Assert.AreEqual(1, doc.ConflictingRevisions.Count());
                Trace.WriteLine($"{Environment.NewLine}{Environment.NewLine}$$$$$$$$$$ PUSHING 1 DOC FROM DB $$$$$$$$$$");

                // Now add a revision to the doc, push, and pull into db2:
                doc.Update(rev =>
                {
                    var props = rev.UserProperties;
                    props["counter"] = ++numRevisions;
                    rev.SetUserProperties(props);
                    return true;
                });
                RunReplication(push);
                Trace.WriteLine($"{Environment.NewLine}{Environment.NewLine}$$$$$$$$$$ PULLING 1 DOC INTO DB2 $$$$$$$$$$");
                RunReplication(pull);
                Assert.AreEqual(db2.GetMaxRevTreeDepth(), doc2.RevisionHistory.Count());
                Assert.AreEqual(1, doc.ConflictingRevisions.Count());
            }
        }

        [Test]
        public void TestRapidRestart()
        {
            var pull = database.CreatePullReplication(GetReplicationURL());
            pull.Start();
            RunReplication(pull);
            var pull2 = database.CreatePullReplication(GetReplicationURL());
            pull2.Continuous = true;
            pull.Stop();
            pull2.Start();
            Sleep(2000);
            Assert.AreNotEqual(ReplicationStatus.Stopped, pull2.Status);
            StopReplication(pull2);
        }

        [Test]
        public void TestRestartSpamming()
        {
            CreateDocuments(database, 100);
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                var pushMRE = new ManualResetEvent(false);
                var pullMRE = new ManualResetEvent(false);
                push.Changed += (sender, args) =>
                {
                    if(args.Status == ReplicationStatus.Idle && args.CompletedChangesCount == 100) {
                        pushMRE.Set();
                    }
                };
                push.Continuous = true;
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.Changed += (sender, args) =>
                {
                    if(args.Status == ReplicationStatus.Idle && args.CompletedChangesCount == 100) {
                        pullMRE.Set();
                    }
                };
                pull.Continuous = true;
                push.Start();
                pull.Start();
                Sleep(200);
                push.Restart();
                pull.Restart();
                push.Restart();
                pull.Restart();
                push.Restart();
                pull.Restart();
                push.Restart();
                pull.Restart();

                pushMRE.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue("because otherwise the pusher never caught up");
                pullMRE.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue("because otherwise the puller never caught up");
                push.Status.Should().Be(ReplicationStatus.Idle, "because the pusher should be idle since it finished its changes");
                pull.Status.Should().Be(ReplicationStatus.Idle, "because the puller should be idle since it finished its changes");
                StopReplication(push);
                StopReplication(pull);

                push.Restart();
                pull.Restart();
                push.Restart();
                pull.Restart();
                push.Restart();
                pull.Restart();
                push.Restart();
                pull.Restart();

                Sleep(1000);

                push.Status.Should().Be(ReplicationStatus.Idle, "because the pusher should still be idle");
                pull.Status.Should().Be(ReplicationStatus.Idle, "because the puller should still be idle");
                StopReplication(push);
                StopReplication(pull);
            }
        }

        [Test]
        public void TestStartSpamming()
        {
            Log.Domains.Sync.Level = Log.LogLevel.Debug;
            CreateDocuments(database, 100);
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                var pushMRE = new ManualResetEvent(false);
                var pullMRE = new ManualResetEvent(false);
                push.Changed += (sender, args) =>
                {
                    if(args.Status == ReplicationStatus.Idle && args.CompletedChangesCount == 100) {
                        pushMRE.Set();
                    }
                };
                push.Continuous = true;
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.Changed += (sender, args) =>
                {
                    if(args.Status == ReplicationStatus.Idle && args.CompletedChangesCount == 100) {
                        pullMRE.Set();
                    }
                };
                pull.Continuous = true;
                push.Start();
                pull.Start();
                Sleep(200);
                push.Start();
                pull.Start();
                push.Start();
                pull.Start();
                push.Start();
                pull.Start();
                push.Start();
                pull.Start();

                Assert.IsTrue(pushMRE.WaitOne(TimeSpan.FromSeconds(10)));
                Assert.IsTrue(pullMRE.WaitOne(TimeSpan.FromSeconds(10)));
                Assert.AreEqual(ReplicationStatus.Idle, push.Status);
                Assert.AreEqual(ReplicationStatus.Idle, pull.Status);
                StopReplication(push);
                StopReplication(pull);
            }
        }

        [Test]
        public void TestStopSpamming()
        {
            Log.Domains.Sync.Level = Log.LogLevel.Debug;
            CreateDocuments(database, 100);
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                push.Continuous = true;
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.Continuous = true;
                push.Start();
                pull.Start();
                Sleep(200);
                push.Stop();
                pull.Stop();
                push.Stop();
                pull.Stop();
                push.Stop();
                pull.Stop();
                StopReplication(push);
                StopReplication(pull);

                Assert.AreEqual(ReplicationStatus.Stopped, push.Status);
                Assert.AreEqual(ReplicationStatus.Stopped, pull.Status);
            }
        }

        [Test]
        public void TestStartStopSpamming()
        {
            Log.Domains.Sync.Level = Log.LogLevel.Debug;
            CreateDocuments(database, 100);
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                push.Continuous = true;
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.Continuous = true;
                push.Start();
                pull.Start();
                Sleep(200);
                push.Stop();
                pull.Stop();
                push.Start();
                pull.Start();
                push.Stop();
                pull.Stop();
                push.Start();
                pull.Start();
                push.Stop();
                pull.Stop();
                push.Start();
                pull.Start();
                StopReplication(push);
                StopReplication(pull);

                Assert.AreEqual(ReplicationStatus.Stopped, push.Status);
                Assert.AreEqual(ReplicationStatus.Stopped, pull.Status);

                push.Stop();
                pull.Stop();
                push.Start();
                pull.Start();
                push.Stop();
                pull.Stop();
                push.Start();
                pull.Start();
                push.Stop();
                pull.Stop();
                push.Start();
                pull.Start();
                StopReplication(push);
                StopReplication(pull);

                Assert.AreEqual(ReplicationStatus.Stopped, push.Status);
                Assert.AreEqual(ReplicationStatus.Stopped, pull.Status);
            }
        }

        [Test]
        public void TestStopRestartFlow()
        {
            Log.Domains.Sync.Level = Log.LogLevel.Debug;
            CreateDocuments(database, 100);
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                var pushMRE = new ManualResetEvent(false);
                var pullMRE = new ManualResetEvent(false);
                push.Changed += (sender, args) =>
                {
                    if(args.Status == ReplicationStatus.Idle && args.CompletedChangesCount == 100) {
                        pushMRE.Set();
                    }
                };
                push.Continuous = true;
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.Changed += (sender, args) =>
                {
                    if(args.Status == ReplicationStatus.Idle && args.CompletedChangesCount == 100) {
                        pullMRE.Set();
                    }
                };
                pull.Continuous = true;
                push.Start();
                pull.Start();
                Sleep(200);
                push.Stop();
                pull.Stop();
                push.Restart();
                pull.Restart();

                pushMRE.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue("because otherwise the pusher never caught up");
                pullMRE.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue("because otherwise the puller never caught up");
                push.Status.Should().Be(ReplicationStatus.Idle, "because the pusher should be idle since it finished its changes");
                pull.Status.Should().Be(ReplicationStatus.Idle, "because the puller should be idle since it finished its changes");
                StopReplication(push);
                StopReplication(pull);
            }
        }

        [Test]
        public void TestStartRestartFlow()
        {
            Log.Domains.Sync.Level = Log.LogLevel.Debug;
            CreateDocuments(database, 100);
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                var pushMRE = new ManualResetEvent(false);
                var pullMRE = new ManualResetEvent(false);
                push.Changed += (sender, args) =>
                {
                    if(args.Status == ReplicationStatus.Idle && args.CompletedChangesCount == 100) {
                        pushMRE.Set();
                    }
                };
                push.Continuous = true;
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.Changed += (sender, args) =>
                {
                    if(args.Status == ReplicationStatus.Idle && args.CompletedChangesCount == 100) {
                        pullMRE.Set();
                    }
                };
                pull.Continuous = true;
                push.Start();
                pull.Start();
                Sleep(200);
                push.Start();
                pull.Start();
                push.Restart();
                pull.Restart();

                pushMRE.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue("because otherwise the pusher never caught up");
                pullMRE.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue("because otherwise the puller never caught up");
                push.Status.Should().Be(ReplicationStatus.Idle, "because the pusher should be idle since it finished its changes");
                pull.Status.Should().Be(ReplicationStatus.Idle, "because the puller should be idle since it finished its changes");
                StopReplication(push);
                StopReplication(pull);
            }
        }

        [Test]
        public void TestCloseDatabaseWhileReplicating()
        {
            var signal = new CountdownEvent(1);
            var rso = new ReplicationStoppedObserver(signal);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                CreateDocuments(database, 1000);
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Changed += rso.Changed;
                pusher.Start();
                Sleep(500);
                database.Close().Wait(15000).Should().BeTrue("because otherwise the database close operation timed out");
                database.IsOpen.Should().BeFalse("because the database is now closed");
                database.Storage.Should().BeNull("because the database storage is null when a database is closed");
                signal.Wait(10000).Should().BeTrue("because otherwise the replication failed to stop");
            }
        }

        [Test]
        public void TestRestartReplicator()
        {
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                CreateDocuments(database, 10);
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Start();
                Sleep(500);
                pusher.Restart();
                Sleep(500);
                RunReplication(pusher);

                Assert.AreEqual(10, pusher.CompletedChangesCount);
                Assert.AreEqual(10, pusher.ChangesCount);
                Assert.IsNull(pusher.LastError);

                CreateDocuments(database, 10);
                RunReplication(pusher);

                Assert.AreEqual(20, pusher.CompletedChangesCount);
                Assert.AreEqual(20, pusher.ChangesCount);
                Assert.IsNull(pusher.LastError);
            }
        }

        [Test]
        public void TestReplicationWithReplacedDB()
        {
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                const int numPrePopulatedDocs = 100;
                Console.WriteLine("Creating {0} pre-populated documents", numPrePopulatedDocs);

                var prePopulateDB = EnsureEmptyDatabase("prepobdb");
                Assert.IsNotNull(prePopulateDB, "Couldn't create pre-populated DB");
                prePopulateDB.RunInTransaction(() =>
                {
                    for(int i = 1; i <= numPrePopulatedDocs; i++) {
                        var doc = prePopulateDB.GetDocument(String.Format("foo-doc-{0}", i));
                        Assert.DoesNotThrow(() => doc.PutProperties(new Dictionary<string, object> {
                            { "index", i },
                            { "foo", true }
                        }));
                    }

                    return true;
                });

                Console.WriteLine("Pushing pre-populated documents ...");
                var pusher = prePopulateDB.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);
                Assert.AreEqual(ReplicationStatus.Stopped, pusher.Status);
                Assert.AreEqual(numPrePopulatedDocs, pusher.CompletedChangesCount);
                Assert.AreEqual(numPrePopulatedDocs, pusher.ChangesCount);

                Console.WriteLine("Pulling pre-populated documents ...");
                var puller = prePopulateDB.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(puller);
                Assert.AreEqual(ReplicationStatus.Stopped, pusher.Status);
                Assert.AreEqual(numPrePopulatedDocs, puller.CompletedChangesCount);
                Assert.AreEqual(numPrePopulatedDocs, puller.ChangesCount);

                const int numNonPrepopulatedDocs = 100;
                var propertiesList = new List<IDictionary<string, object>>();
                for (int i = 1; i <= numNonPrepopulatedDocs; i++) {
                    propertiesList.Add(new Dictionary<string, object> {
                        { "_id", String.Format("bar-doc-{0}", i) },
                        { "index", i },
                        { "bar", true }
                    });
                }

                remoteDb.AddDocuments(propertiesList);

                prePopulateDB.Close();
                using (var zipStream = ZipDatabase(prePopulateDB, "prepopulated.zip")) {
                    manager.ReplaceDatabase("importdb", zipStream, true);
                }

                var importDb = manager.GetDatabase("importdb");
                pusher = importDb.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);
                Assert.AreEqual(ReplicationStatus.Stopped, pusher.Status);
                Assert.AreEqual(0, pusher.CompletedChangesCount);
                Assert.AreEqual(0, pusher.ChangesCount);

                puller = importDb.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(puller);
                Assert.AreEqual(ReplicationStatus.Stopped, pusher.Status);
                Assert.AreEqual(numNonPrepopulatedDocs, puller.CompletedChangesCount);
                Assert.AreEqual(numNonPrepopulatedDocs, puller.ChangesCount);
            }
        }

        [Test]
        public void TestPendingDocumentIDs()
        {
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var repl = database.CreatePushReplication(remoteDb.RemoteUri);
                Assert.IsNotNull(repl.GetPendingDocumentIDs());
                Assert.AreEqual(0, repl.GetPendingDocumentIDs().Count);

                database.RunInTransaction(() =>
                {
                    for(int i = 1; i <= 10; i++) {
                        var doc = database.GetDocument(String.Format("doc-{0}", i));
                        Assert.DoesNotThrow(() => {
                            doc.PutProperties(new Dictionary<string, object> {
                                { "index", i },
                                { "bar", false }
                            });
                        });
                    }

                    return true;
                });

                Assert.AreEqual(10, repl.GetPendingDocumentIDs().Count);
                Assert.IsTrue(repl.IsDocumentPending(database.GetDocument("doc-1")));

                repl.Start();
                Assert.AreEqual(10, repl.GetPendingDocumentIDs().Count);
                Assert.IsTrue(repl.IsDocumentPending(database.GetDocument("doc-1")));

                RunReplication(repl);
                Assert.IsNotNull(repl.GetPendingDocumentIDs());
                Assert.AreEqual(0, repl.GetPendingDocumentIDs().Count);
                Assert.IsFalse(repl.IsDocumentPending(database.GetDocument("doc-1")));

                database.RunInTransaction(() =>
                {
                    for(int i = 11; i <= 20; i++) {
                        var doc = database.GetDocument(String.Format("doc-{0}", i));
                        Assert.DoesNotThrow(() => {
                            doc.PutProperties(new Dictionary<string, object> {
                                { "index", i },
                                { "bar", false }
                            });
                        });
                    }

                    return true;
                });

                Sleep(1000);

                repl = database.CreatePushReplication(remoteDb.RemoteUri);
                Assert.AreEqual(10, repl.GetPendingDocumentIDs().Count);
                Assert.IsTrue(repl.IsDocumentPending(database.GetDocument("doc-11")));
                Assert.IsFalse(repl.IsDocumentPending(database.GetDocument("doc-1")));

                repl.Start();
                Assert.AreEqual(10, repl.GetPendingDocumentIDs().Count);
                Assert.IsTrue(repl.IsDocumentPending(database.GetDocument("doc-11")));
                Assert.IsFalse(repl.IsDocumentPending(database.GetDocument("doc-1")));

                repl = database.CreatePullReplication(remoteDb.RemoteUri);
                Assert.IsNull(repl.GetPendingDocumentIDs());

                repl.Start();
                Assert.IsNull(repl.GetPendingDocumentIDs());

                RunReplication(repl);
                Assert.IsNull(repl.GetPendingDocumentIDs());
            }
        }

        [Test]
        public void TestRemoteUUID()
        {
            var r1 = database.CreatePullReplication(new Uri("http://alice.local:55555/db"));
            r1.ReplicationOptions = new ReplicationOptions { RemoteUUID = "cafebabe" };
            var check1 = r1.RemoteCheckpointDocID();

            var r2 = database.CreatePullReplication(new Uri("http://alice.local:44444/db"));
            r2.ReplicationOptions = r1.ReplicationOptions;
            var check2 = r2.RemoteCheckpointDocID();

            Assert.AreEqual(check1, check2);
            Assert.IsTrue(r1.HasSameSettingsAs(r2));

            var r3 = database.CreatePullReplication(r2.RemoteUrl);
            r3.ReplicationOptions = r1.ReplicationOptions;
            r3.Filter = "Melitta";
            var check3 = r3.RemoteCheckpointDocID();

            Assert.AreNotEqual(check2, check3);
            Assert.IsFalse(r3.HasSameSettingsAs(r2));
        }

        [Test]
        public void TestConcurrentPullers()
        {
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                remoteDb.AddDocuments(100, false);

                var pull1 = database.CreatePullReplication(remoteDb.RemoteUri);
                pull1.Continuous = true;

                var pull2 = database.CreatePullReplication(remoteDb.RemoteUri);
                pull2.Continuous = false;

                pull1.Start();
                try {
                    RunReplication(pull2);
                    Assert.IsNull(pull1.LastError);
                    Assert.IsNull(pull2.LastError);

                    Assert.AreEqual(100, database.GetDocumentCount());
                } finally {
                    StopReplication(pull1);
                }
            }
        }

        [Test]
        public void TestPullerChangedEvent()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            Log.Domains.Sync.Level = Log.LogLevel.Debug;
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var docIdTimestamp = Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
                var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
                var doc2Id = string.Format("doc2-{0}", docIdTimestamp);           
                remoteDb.AddDocument(doc1Id, "attachment.png");
                remoteDb.AddDocument(doc2Id, "attachment2.png");

                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                List<ReplicationStatus> statusHistory = new List<ReplicationStatus>();

                pull.Changed += (sender, e) =>
                {
                    statusHistory.Add(e.Status);
                    if (e.ChangesCount > 0 && e.CompletedChangesCount != e.ChangesCount) {
                        Assert.AreEqual(ReplicationStatus.Active, e.Source.Status);
                    } 
                };

                RunReplication(pull);
                for (int i = 0; i < statusHistory.Count; i++) {
                    if (i == statusHistory.Count - 1) {
                        Assert.AreEqual(ReplicationStatus.Stopped, statusHistory[i]);
                    } else {
                        Assert.AreEqual(ReplicationStatus.Active, statusHistory[i]);
                    }
                }

                doc1Id = string.Format("doc3-{0}", docIdTimestamp);
                doc2Id = string.Format("doc4-{0}", docIdTimestamp);           
                remoteDb.AddDocument(doc1Id, "attachment.png");
                remoteDb.AddDocument(doc2Id, "attachment2.png");
                pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.Continuous = true;
                statusHistory.Clear();
                var doneEvent = new AutoResetEvent(false);

                pull.Changed += (sender, e) =>
                {
                    statusHistory.Add(e.Status);
                    if (e.Status == ReplicationStatus.Idle && e.ChangesCount == e.CompletedChangesCount) {
                        doneEvent.Set();
                    }
                };

                pull.Start();
                Assert.IsTrue(doneEvent.WaitOne(TimeSpan.FromSeconds(10)));
                Assert.IsNull(pull.LastError);

                for (int i = 0; i < statusHistory.Count; i++) {
                    if (i == statusHistory.Count - 1) {
                        Assert.AreEqual(ReplicationStatus.Idle, statusHistory[i]);
                    } else {
                        Assert.IsTrue(statusHistory[i] == ReplicationStatus.Active || statusHistory[i] == ReplicationStatus.Idle);
                    }
                }

                statusHistory.Clear();
                doc1Id = string.Format("doc5-{0}", docIdTimestamp);         
                remoteDb.AddDocument(doc1Id, "attachment.png");

                Assert.IsTrue(doneEvent.WaitOne(TimeSpan.FromSeconds(10)));
                Assert.IsNull(pull.LastError);
                statusHistory.Clear();
                StopReplication(pull);

                Assert.AreEqual(ReplicationStatus.Active, statusHistory.First());
                Assert.AreEqual(ReplicationStatus.Stopped, statusHistory.Last());
            }
        }

        [Test]
        public void TestPusherChangedEvent()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
                
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                CreateDocuments(database, 2);
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                List<ReplicationStatus> statusHistory = new List<ReplicationStatus>();

                push.Changed += (sender, e) =>
                {
                    statusHistory.Add(e.Status);
                    if (e.ChangesCount > 0 && e.CompletedChangesCount != e.ChangesCount) {
                        Assert.AreEqual(ReplicationStatus.Active, e.Status);
                    }

                    if (e.Status == ReplicationStatus.Stopped) {
                        Assert.AreNotEqual(0, e.CompletedChangesCount);
                        Assert.AreEqual(e.ChangesCount, e.CompletedChangesCount);
                    }
                };

                RunReplication(push);
                Sleep(1000);

                Assert.IsNull(push.LastError);
                foreach (var status in statusHistory.Take(statusHistory.Count - 1)) {
                    Assert.AreEqual(ReplicationStatus.Active, status);
                }

                Assert.AreEqual(ReplicationStatus.Stopped, statusHistory[statusHistory.Count - 1]);

                CreateDocuments(database, 2);
                push = database.CreatePushReplication(remoteDb.RemoteUri);
                push.Continuous = true;
                statusHistory.Clear();
                var doneEvent = new ManualResetEventSlim();

                push.Changed += (sender, e) =>
                {
                    statusHistory.Add(e.Status);
                    if(e.Status == ReplicationStatus.Idle && e.ChangesCount == e.CompletedChangesCount) {
                        doneEvent.Set();
                    }
                };

                push.Start();
                Assert.IsTrue(doneEvent.Wait(TimeSpan.FromSeconds(60)));
                Assert.IsNull(push.LastError);
                foreach (var status in statusHistory.Take(statusHistory.Count - 1)) {
                    Assert.AreEqual(ReplicationStatus.Active, status);
                }

                Assert.AreEqual(ReplicationStatus.Idle, statusHistory[statusHistory.Count - 1]);
                doneEvent.Reset();

                statusHistory.Clear();
                CreateDocuments(database, 1);

                Assert.IsTrue(doneEvent.Wait(TimeSpan.FromSeconds(60)));
                Assert.IsNull(push.LastError);
                foreach (var status in statusHistory.Take(statusHistory.Count - 1)) {
                    Assert.IsTrue(status == ReplicationStatus.Active || status == ReplicationStatus.Idle);
                }

                Assert.AreEqual(ReplicationStatus.Idle, statusHistory[statusHistory.Count - 1]);

                doneEvent.Reset();
                statusHistory.Clear();
                StopReplication(push);

                Assert.AreEqual(2, statusHistory.Count);
                Assert.AreEqual(ReplicationStatus.Active, statusHistory.First());
                Assert.AreEqual(ReplicationStatus.Stopped, statusHistory[1]);
            }
        }

        [Test] // Issue #449
        public void TestPushAttachmentToCouchDB()
        {
            var couchDb = new CouchDB(GetReplicationProtocol(), GetReplicationServer());
            using (var remoteDb = couchDb.CreateDatabase(TempDbName())) {

                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                CreateDocuments(database, 2);
                var attachDoc = database.CreateDocument();
                var newRev = attachDoc.CreateRevision();
                var newProps = newRev.UserProperties;
                newProps["foo"] = "bar";
                newRev.SetUserProperties(newProps);
                var attachmentStream = GetAsset("attachment.png");
                newRev.SetAttachment("attachment.png", "image/png", attachmentStream);
                newRev.Save();
                
                RunReplication(push);
                Assert.AreEqual(3, push.ChangesCount);
                Assert.AreEqual(3, push.CompletedChangesCount);
                attachDoc = database.GetExistingDocument(attachDoc.Id);
                attachDoc.Update(rev =>
                {
                    var props = rev.UserProperties;
                    props["extraminutes"] = "5";
                    rev.SetUserProperties(props);
                    return true;
                });

                push = database.CreatePushReplication(push.RemoteUrl);
                RunReplication(push);

                database.Close();
                database = EnsureEmptyDatabase(database.Name);
                var pull = database.CreatePullReplication(push.RemoteUrl);
                RunReplication(pull);
                Assert.AreEqual(3, database.GetDocumentCount());
                attachDoc = database.GetExistingDocument(attachDoc.Id);
                Assert.IsNotNull(attachDoc, "Failed to retrieve doc with attachment");
                Assert.IsNotNull(attachDoc.CurrentRevision.Attachments, "Failed to retrieve attachments on attachment doc");
                attachDoc.Update(rev =>
                {
                    var props = rev.UserProperties;
                    props["extraminutes"] = "10";
                    rev.SetUserProperties(props);
                    return true;
                });

                push = database.CreatePushReplication(pull.RemoteUrl);
                RunReplication(push);
                Assert.IsNull(push.LastError);
                Assert.AreEqual(1, push.ChangesCount);
                Assert.AreEqual(1, push.CompletedChangesCount);
                Assert.AreEqual(3, database.GetDocumentCount());
            }
        }

        // Reproduces issue #167
        // https://github.com/couchbase/couchbase-lite-android/issues/167
        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPushPurgedDoc()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var numBulkDocRequests = 0;
            HttpRequestMessage lastBulkDocsRequest = null;

            var doc = CreateDocumentWithProperties(
                database, 
                new Dictionary<string, object>
                {
                    {"testName", "testPurgeDocument"}
                }
            );
            Assert.IsNotNull(doc);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remote = remoteDb.RemoteUri;
                var factory = new MockHttpClientFactory();
                factory.HttpHandler.ClearResponders();
                factory.HttpHandler.AddResponderRevDiffsAllMissing();
                factory.HttpHandler.AddResponderFakeLocalDocumentUpdate404();
                factory.HttpHandler.AddResponderFakeBulkDocs();
                manager.DefaultHttpClientFactory = factory;

                var pusher = database.CreatePushReplication(remote);

                var replicationCaughtUpSignal = new CountdownEvent(1);
                pusher.Changed += (sender, e) =>
                {
                    var changesCount = e.Source.ChangesCount;
                    var completedChangesCount = e.Source.CompletedChangesCount;
                    var msg = String.Format("changes: {0} completed changes: {1}", changesCount, completedChangesCount);
                    WriteDebug(msg);
                    if (changesCount > 0 && changesCount == completedChangesCount
                    && replicationCaughtUpSignal.CurrentCount > 0) {
                        replicationCaughtUpSignal.Signal();
                    }
                };
                pusher.Start();

                // wait until that doc is pushed
                var didNotTimeOut = replicationCaughtUpSignal.Wait(TimeSpan.FromSeconds(15));
                Assert.IsTrue(didNotTimeOut);

                // at this point, we should have captured exactly 1 bulk docs request
                numBulkDocRequests = 0;

                var handler = factory.HttpHandler;

                foreach (var capturedRequest in handler.CapturedRequests) {
                    if (capturedRequest.Method == HttpMethod.Post && capturedRequest.RequestUri.AbsoluteUri.EndsWith("_bulk_docs", StringComparison.Ordinal)) {
                        lastBulkDocsRequest = capturedRequest;
                        numBulkDocRequests++;
                    }
                }

                Assert.AreEqual(1, numBulkDocRequests);

                // that bulk docs request should have the "start" key under its _revisions
                var jsonMap = MockHttpRequestHandler.GetJsonMapFromRequest(lastBulkDocsRequest);
                var docs = (jsonMap.Get("docs")).AsList<IDictionary<string,object>>();
                var onlyDoc = docs[0];
                var revisions = onlyDoc.Get("_revisions").AsDictionary<string,object>();
                Assert.IsTrue(revisions.ContainsKey("start"));

                // Reset for the next attempt.
                handler.ClearCapturedRequests();

                // now add a new revision, which will trigger the pusher to try to push it
                var properties = new Dictionary<string, object>();
                properties["testName2"] = "update doc";

                var unsavedRevision = doc.CreateRevision();
                unsavedRevision.SetUserProperties(properties);
                unsavedRevision.Save();

                // but then immediately purge it
                doc.Purge();
                pusher.Start();

                // wait for a while to give the replicator a chance to push it
                // (it should not actually push anything)
                Sleep(5 * 1000);

                // we should not have gotten any more _bulk_docs requests, because
                // the replicator should not have pushed anything else.
                // (in the case of the bug, it was trying to push the purged revision)
                numBulkDocRequests = 0;
                foreach (var capturedRequest in handler.CapturedRequests) {
                    if (capturedRequest.Method == HttpMethod.Post && capturedRequest.RequestUri.AbsoluteUri.EndsWith("_bulk_docs", StringComparison.Ordinal)) {
                        numBulkDocRequests++;
                    }
                }

                Assert.AreEqual(0, numBulkDocRequests);
                pusher.Stop();
            }
        }

        [Test]
        public void TestUpdateToServerSavesAttachment()
        {
            var couchDb = new CouchDB(GetReplicationProtocol(), GetReplicationServer());
            using (var remoteDb = couchDb.CreateDatabase(TempDbName())) {
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                pull.Continuous = true;
                pull.Start();

                var docName = "doc" + Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
                var endpoint = remoteDb.RemoteUri.AppendPath(docName);
                var docContent = Encoding.UTF8.GetBytes("{\"foo\":false}");
                var putRequest = new HttpRequestMessage(HttpMethod.Put, endpoint);
                putRequest.Content = new StringContent("{\"foo\":false}");
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = _httpClient.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);


                var attachmentStream = GetAsset("attachment.png");
                var baos = new MemoryStream();
                attachmentStream.CopyTo(baos);
                attachmentStream.Dispose();
                endpoint = endpoint.AppendPath("attachment?rev=1-1153b140e4c8674e2e6425c94de860a0");
                docContent = baos.ToArray();
                baos.Dispose();

                putRequest = new HttpRequestMessage(HttpMethod.Put, endpoint);
                putRequest.Content = new ByteArrayContent(docContent);
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                response = _httpClient.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                endpoint = remoteDb.RemoteUri.AppendPath(docName + "?rev=2-bb71ce0da1de19f848177525c4ae5a8b");
                const string docContentStr = "{\"foo\":true,\"_attachments\":{\"attachment\":{\"content_type\":\"image/png\",\"revpos\":2,\"digest\":\"md5-ks1IBwCXbuY7VWAO9CkEjA==\",\"length\":519173,\"stub\":true}}}";
                putRequest = new HttpRequestMessage(HttpMethod.Put, endpoint);
                putRequest.Content = new StringContent(docContentStr);
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = _httpClient.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                Sleep(1000);
                while (pull.Status == ReplicationStatus.Active) {
                    Sleep(500);
                }

                Assert.IsNull(pull.LastError);
                Assert.AreEqual(ReplicationStatus.Idle, pull.Status);
                var doc = database.GetExistingDocument(docName);
                Assert.IsNotNull(doc);
                Assert.IsNotNull(doc.CurrentRevision.Attachments);

                StopReplication(pull);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPullerWithLiveQuery()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            // Even though this test is passed, there is a runtime exception
            // thrown regarding the replication's number of changes count versus
            // number of completed changes count. Investigation is required.
            string docIdTimestamp = System.Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
            string doc1Id = string.Format("doc1-{0}", docIdTimestamp);
            string doc2Id = string.Format("doc2-{0}", docIdTimestamp);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                remoteDb.AddDocument(doc1Id, "attachment2.png");
                remoteDb.AddDocument(doc2Id, "attachment2.png");

                View view = database.GetView("testPullerWithLiveQueryView");
                view.SetMapReduce((document, emitter) =>
                {
                    if (document.CblID() != null) {
                        emitter(document.CblID(), null);
                    }
                }, null, "1");

                LiveQuery allDocsLiveQuery = view.CreateQuery().ToLiveQuery();
                allDocsLiveQuery.Changed += (sender, e) =>
                {
                    int numTimesCalled = 0;
                    if (e.Error != null) {
                        throw new ApplicationException("Fail", e.Error);
                    }
                    if (numTimesCalled++ > 0) {
                        Assert.IsTrue(e.Rows.Count > 0);
                    }
                    WriteDebug("rows " + e.Rows);
                };

                // the first time this is called back, the rows will be empty.
                // but on subsequent times we should expect to get a non empty
                // row set.
                allDocsLiveQuery.Start();
                var remote = remoteDb.RemoteUri;
                var repl = database.CreatePullReplication(remote);
                repl.Continuous = false;
                RunReplication(repl);
                Assert.IsNull(repl.LastError);

                allDocsLiveQuery.Stop();

                Sleep(2000);
            }

            Sleep(1000);
        }

        /// <exception cref="System.IO.IOException"></exception>
        private Dictionary<string,object> GetDocWithId(string docId, string attachmentName)
        {
            Dictionary<string,object> docBody;
            if (attachmentName != null)
            {
                // add attachment to document
                var attachmentStream = GetAsset(attachmentName);
                var baos = new MemoryStream();
                attachmentStream.CopyTo(baos);
                var attachmentBase64 = Convert.ToBase64String(baos.ToArray());
                docBody = new Dictionary<string,object> 
                {
                    {"foo", 1},
                    {"bar", false}, 
                    {
                        "_attachments", 
                        new Dictionary<string,object> 
                        {
                            {
                                "i_use_couchdb.png" , new Dictionary<string,object>
                                { 
                                    {"content_type", "image/png"}, 
                                    {"data", attachmentBase64 }
                                }
                            } 
                        }
                    }
                };
                attachmentStream.Dispose();
                baos.Dispose();
            }
            else
            {
                docBody = new Dictionary<string,object> 
                {
                    {"foo", 1},
                    {"bar", false}
                };
            }

            return docBody;
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestGetReplicator()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var replicationUrl = remoteDb.RemoteUri;
                var replicator = database.CreatePullReplication(replicationUrl);
                Assert.IsNotNull(replicator);
                Assert.IsTrue(replicator.IsPull);
                Assert.IsFalse(replicator.Continuous);
                Assert.IsFalse(replicator.IsRunning);

                ReplicationStatus lastStatus = replicator.Status;
                var mre = new ManualResetEventSlim();
                replicator.Changed += (sender, e) =>
                {
                    if (lastStatus != e.Source.Status) {
                        lastStatus = e.Source.Status;
                        mre.Set();
                    }
                };
                replicator.Start();
                Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(10)), "Timed out waiting for replicator to start");
                Assert.IsTrue(replicator.IsRunning);

                var activeReplicators = default(IList<Replication>);
                var got = database.ActiveReplicators.AcquireTemp(out activeReplicators);
                Assert.IsTrue(got);
                Assert.AreEqual(1, activeReplicators.Count);
                Assert.AreEqual(replicator, activeReplicators[0]);

                replicator.Stop();

                mre.Reset();
                Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(10)), "Timed out waiting for replicator to stop");
                Assert.IsFalse(replicator.IsRunning);
                Sleep(500);
                got = database.ActiveReplicators.AcquireTemp(out activeReplicators);
                Assert.IsTrue(got);
                Assert.AreEqual(0, activeReplicators.Count);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestGetReplicatorWithAuth()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            const string email = "jchris@couchbase.com";
            const string accessToken = "fake_access_token";

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remoteUrl = remoteDb.RemoteUri;
                FacebookAuthorizer.RegisterAccessToken(accessToken, email, remoteUrl);
                Replication replicator = database.CreatePushReplication(remoteDb.RemoteUri);
                replicator.Authenticator = AuthenticatorFactory.CreateFacebookAuthenticator(accessToken);

                Assert.IsNotNull(replicator);
                Assert.IsNotNull(replicator.Authenticator);
                Assert.IsTrue(replicator.Authenticator is TokenAuthenticator);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestRunReplicationWithError()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var mockHttpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = mockHttpClientFactory;

            var mockHttpHandler = mockHttpClientFactory.HttpHandler;
            mockHttpHandler.AddResponderFailAllRequests(HttpStatusCode.InternalServerError);

            var dbUrlString = "http://fake.test-url.com:4984/fake/";
            var remote = new Uri(dbUrlString);
            var continuous = false;
            var r1 = new Pusher(database, remote, continuous, mockHttpClientFactory, new TaskFactory(new SingleTaskThreadpoolScheduler()));
            Assert.IsFalse(r1.Continuous);
            RunReplication(r1);

            Assert.AreEqual(ReplicationStatus.Stopped, r1.Status);
            Assert.AreEqual(0, r1.CompletedChangesCount);
            Assert.AreEqual(0, r1.ChangesCount);
            Assert.IsNotNull(r1.LastError);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestReplicatorErrorStatus()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            const string email = "jchris@couchbase.com";
            const string accessToken = "fake_access_token";

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remoteUrl = remoteDb.RemoteUri;
                FacebookAuthorizer.RegisterAccessToken(accessToken, email, remoteUrl);
                var replicator = database.CreatePullReplication(remoteDb.RemoteUri);
                replicator.Authenticator = AuthenticatorFactory.CreateFacebookAuthenticator(accessToken);

                RunReplication(replicator);

                Assert.IsNotNull(replicator.LastError);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestGoOffline()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remote = remoteDb.RemoteUri;
                var repl = database.CreatePullReplication(remote);
                repl.Continuous = true;
                repl.Start();

                //Some platforms will fire an intial GoOnline event because of network status
                //change registration, so get that out of the way first
                Sleep(1000);
                PutReplicationOffline(repl);
                Assert.IsTrue(repl.Status == ReplicationStatus.Offline);

                StopReplication(repl);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public virtual void TestAppendPathURLString([Values("http://10.0.0.3:4984/connect-2014", "http://10.0.0.3:4984/connect-2014/")] String baseUri, [Values("/_bulk_get?revs=true&attachments=true", "_bulk_get?revs=true&attachments=true")] String newPath)
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var dbUrlString = new Uri(baseUri);
            var relativeUrlString = dbUrlString.AppendPath(newPath).AbsoluteUri;
            var expected = "http://10.0.0.3:4984/connect-2014/_bulk_get?revs=true&attachments=true";
            Assert.AreEqual(expected, relativeUrlString);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestChannels()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var remote = new Uri("http://couchbase.com/no_such_db");
            var replicator = database.CreatePullReplication(remote);

            var channels = new List<string>();
            channels.Add("chan1");
            channels.Add("chan2");
            replicator.Channels = channels;
            Assert.AreEqual(channels, replicator.Channels);

            replicator.Channels = null;
            Assert.IsTrue(replicator.Channels.ToList().Count == 0);
        }

        /// <exception cref="System.UriFormatException"></exception>
        [Test]
        public virtual void TestChannelsMore()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var fakeRemoteURL = new Uri("http://couchbase.com/no_such_db");
            var r1 = database.CreatePullReplication(fakeRemoteURL);

            Assert.IsTrue(!r1.Channels.Any());
            r1.Filter = "foo/bar";
            Assert.IsTrue(!r1.Channels.Any());

            var filterParams = new Dictionary<string, object>();
            filterParams["a"] = "b";
            r1.FilterParams = filterParams;
            Assert.IsTrue(!r1.Channels.Any());

            r1.Channels = null;
            Assert.AreEqual("foo/bar", r1.Filter);
            Assert.AreEqual(filterParams, r1.FilterParams);

            var channels = new List<string>();
            channels.Add("NBC");
            channels.Add("MTV");
            r1.Channels = channels;
            Assert.AreEqual(channels, r1.Channels);
            Assert.AreEqual("sync_gateway/bychannel", r1.Filter);

            filterParams = new Dictionary<string, object>();
            filterParams["channels"] = "NBC,MTV";
            Assert.AreEqual(filterParams, r1.FilterParams);
                        
            r1.Channels = null;
            Assert.AreEqual(r1.Filter, null);
            Assert.AreEqual(null, r1.FilterParams);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestHeaders()
        {
            if(!Boolean.Parse((string)GetProperty("replicationTestsEnabled"))) {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            database.GetDocument("doc1").PutProperties(new Dictionary<string, object> {
                ["jim"] = "borden"
            });

            database.GetDocument("doc2").PutProperties(new Dictionary<string, object> {
                ["foo"] = "bar"
            });

            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remote = remoteDb.RemoteUri;
                var puller = database.CreatePullReplication(remote);

                var mockHttpClientFactory = new MockHttpClientFactory(false);
                var mockHttpHandler = mockHttpClientFactory.HttpHandler;
                var pusher = database.CreatePushReplication(remote);
                pusher.ClientFactory = mockHttpClientFactory;
                pusher.Headers.Add("foo", "bar");
                pusher.Start();
                Sleep(2000);
                pusher.Stop();

                ValidateHttpHeaders(mockHttpHandler);
                mockHttpHandler.ClearCapturedRequests();

                mockHttpClientFactory = new MockHttpClientFactory(false);
                mockHttpHandler = mockHttpClientFactory.HttpHandler;
                puller.ClientFactory = mockHttpClientFactory;
                puller.Headers.Add("foo", "bar");
                puller.Start();
                Sleep(2000);
                puller.Stop();

                ValidateHttpHeaders(mockHttpHandler);
                mockHttpHandler.ClearCapturedRequests();

                mockHttpClientFactory = new MockHttpClientFactory(false);
                mockHttpHandler = mockHttpClientFactory.HttpHandler;
                pusher = database.CreatePushReplication(remote);
                pusher.ClientFactory = mockHttpClientFactory;
                pusher.Continuous = true;
                pusher.Headers.Add("foo", "bar");
                pusher.Start();
                Sleep(2000);
                pusher.Stop();

                ValidateHttpHeaders(mockHttpHandler);
                mockHttpHandler.ClearCapturedRequests();

                mockHttpClientFactory = new MockHttpClientFactory(false);
                mockHttpHandler = mockHttpClientFactory.HttpHandler;
                puller = database.CreatePullReplication(remote);
                puller.ClientFactory = mockHttpClientFactory;
                puller.Continuous = true;
                puller.Headers.Add("foo", "bar");
                puller.Start();
                Sleep(2000);
                puller.Stop();

                ValidateHttpHeaders(mockHttpHandler);
            }
        }

        private void ValidateHttpHeaders (MockHttpRequestHandler mockHttpHandler)
        {
            var fooheaderCount = 0;
            var requests = mockHttpHandler.CapturedRequests;

            requests.Should().NotBeEmpty("because there should be at least one request");
            foreach (var request in requests) {
                try {
                    var requestHeaders = request.Headers.GetValues("foo");
                    foreach(var requestHeader in requestHeaders) {
                        fooheaderCount++;
                        requestHeader.Should().Be("bar", "because otherwise the custom header is incorrect");
                    }
                } catch(InvalidOperationException) {
                    Assert.Fail("No custom header found");
                }
            }

            fooheaderCount.Should().Be(requests.Count, "because every request should have the custom header");
        }

        [Test]
        // Note that this should not happen anymore but this test will remain just to verify
        // the correct behavior if it does
        public void TestBulkGet404()
        {
            var factory = new MockHttpClientFactory(false);
            factory.HttpHandler.SetResponder("_changes", req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(@"{""results"":[{""seq"":3,""id"":""somedoc"",""changes"":
                [{""rev"":""2-cafebabe""}]},{""seq"":4,""id"":""otherdoc"",""changes"":[{""rev"":""5-bedbedbe""}]},
                {""seq"":5,""id"":""realdoc"",""changes"":[{""rev"":""1-12345abc""}]}]}");

                return response;
            });

            factory.HttpHandler.SetResponder("_bulk_get", req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("--67aac1bcad803590b9a9e1999fc539438b3363fab35a24c17990188b222f\r\n" +
                    "Content-Type: application/json; error=\"true\"\r\n\r\n" +
                    "{\"error\":\"not_found\",\"id\":\"somedoc\",\"reason\":\"missing\",\"rev\":\"2-cafebabe\",\"status\":404}\r\n" +
                    "--67aac1bcad803590b9a9e1999fc539438b3363fab35a24c17990188b222f\r\n" +
                    "Content-Type: application/json; error=\"true\"\r\n\r\n" +
                    "{\"error\":\"not_found\",\"id\":\"otherdoc\",\"reason\":\"missing\",\"rev\":\"5-bedbedbe\",\"status\":404}\r\n" +
                    "--67aac1bcad803590b9a9e1999fc539438b3363fab35a24c17990188b222f\r\n" +
                    "Content-Type: application/json\r\n\r\n" +
                    "{\"_id\":\"realdoc\",\"_rev\":\"1-12345abc\",\"channels\":[\"unit_test\"],\"foo\":\"bar\"}\r\n" +
                    "--67aac1bcad803590b9a9e1999fc539438b3363fab35a24c17990188b222f--");

                response.Content.Headers.Remove("Content-Type");
                response.Content.Headers.TryAddWithoutValidation("Content-Type", "multipart/mixed; boundary=\"67aac1bcad803590b9a9e1999fc539438b3363fab35a24c17990188b222f\"");
                return response;
            });
            manager.DefaultHttpClientFactory = factory;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var puller = database.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(puller);
                Assert.IsNotNull(puller.LastError);
                Assert.AreEqual(3, puller.ChangesCount);
                Assert.AreEqual(3, puller.CompletedChangesCount);
                Assert.AreEqual("5", puller.LastSequence);
            }
        }

        #if false
        [Test] // This test takes nearly 5 minutes to run, so only run when needed
        #endif
        public void TestLongRemovedChangesFeed()
        {
            var random = new Random();
            var changesFeed = new StringBuilder("{\"results\":[");
            const int limit = 100000;
            HashSet<string> removedIDSet = new HashSet<string>();
            for (var i = 1; i < limit; i++) {
                var removed = random.NextDouble() >= 0.5;
                if (removed) {
                    var removedID = Misc.CreateGUID();
                    changesFeed.AppendFormat("{{\"seq\":\"{0}\",\"id\":\"{1}\",\"removed\":[\"fake\"]," +
                        "\"changes\":[{{\"rev\":\"1-deadbeef\"}}]}},",
                        i, removedID);
                    removedIDSet.Add(removedID);
                } else {
                    changesFeed.AppendFormat("{{\"seq\":\"{0}\",\"id\":\"{1}\",\"changes\":[{{\"rev\":\"1-deadbeef\"}}]}},",
                        i, Misc.CreateGUID());
                }
            }

            changesFeed.AppendFormat("{{\"seq\":\"{0}\",\"id\":\"{1}\",\"changes\":[{{\"rev\":\"1-deadbeef\"}}]}}]," +
                "last_seq: \"{0}\"}}",
                limit, Misc.CreateGUID());

            var factory = new MockHttpClientFactory(false);
            factory.HttpHandler.SetResponder("_changes", req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var changesString = changesFeed.ToString();
                response.Content = new StringContent(changesString);

                return response;
            });

            factory.HttpHandler.SetResponder("_bulk_get", req =>
            {
                var contentStream = req.Content.ReadAsStreamAsync().Result;
                var content = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(contentStream);

                var responseBody = new StringBuilder("--67aac1bcad803590b9a9e1999fc539438b3363fab35a24c17990188b222f\r\n");
                foreach(var obj in content["docs"] as IEnumerable) {
                    var dict = obj.AsDictionary<string, object>();
                    var nonexistent = removedIDSet.Contains(dict.GetCast<string>("id"));
                    if(nonexistent) {
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError); // Just so we can know
                    } else {
                        responseBody.Append("Content-Type: application/json\r\n\r\n");
                        responseBody.AppendFormat("{{\"_id\":\"{0}\",\"_rev\":\"1-deadbeef\",\"foo\":\"bar\"}}\r\n", dict["id"]);
                    }

                    responseBody.Append("--67aac1bcad803590b9a9e1999fc539438b3363fab35a24c17990188b222f\r\n");
                }

                responseBody.Remove(responseBody.Length - 2, 2);
                responseBody.Append("--");

                var retVal = new HttpResponseMessage(HttpStatusCode.OK);
                var responseString = responseBody.ToString();
                retVal.Content = new StringContent(responseString);
                retVal.Content.Headers.Remove("Content-Type");
                retVal.Content.Headers.TryAddWithoutValidation("Content-Type", "multipart/mixed; boundary=\"67aac1bcad803590b9a9e1999fc539438b3363fab35a24c17990188b222f\"");

                return retVal;
            });

            manager.DefaultHttpClientFactory = factory;
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var puller = database.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(puller);
                Assert.AreEqual(ReplicationStatus.Stopped, puller.Status);
                Assert.AreNotEqual(limit, puller.ChangesCount);
                Assert.AreNotEqual(limit, puller.CompletedChangesCount);
                Assert.AreEqual(limit.ToString(), puller.LastSequence);
            }

            Sleep(1000);
        }

        [Test]
        public void TestSetAndDeleteCookies()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var replicationUrl = remoteDb.RemoteUri;
                var puller = database.CreatePullReplication(replicationUrl);
                var cookieContainer = puller.CookieContainer;

                // Set
                var name = "foo";
                var value = "bar";
                var isSecure = false;
                var httpOnly = false;
                var domain = replicationUrl.Host;
                var path = replicationUrl.PathAndQuery;
                var expires = DateTime.Now.Add(TimeSpan.FromDays(1));
                puller.SetCookie(name, value, path, expires, isSecure, httpOnly);

                var cookies = cookieContainer.GetCookies(replicationUrl);
                Assert.AreEqual(1, cookies.Count);
                var cookie = cookies[0];
                Assert.AreEqual(name, cookie.Name);
                Assert.AreEqual(value, cookie.Value);
                Assert.AreEqual(domain, cookie.Domain);
                Assert.AreEqual(path, cookie.Path);
                Assert.AreEqual(expires, cookie.Expires);
                Assert.AreEqual(isSecure, cookie.Secure);
                Assert.AreEqual(httpOnly, cookie.HttpOnly);

                puller = database.CreatePullReplication(replicationUrl);
                cookieContainer = puller.CookieContainer;

                var name2 = "foo2";
                puller.SetCookie(name2, value, path, expires, isSecure, httpOnly);
                cookies = cookieContainer.GetCookies(replicationUrl);
                Assert.AreEqual(2, cookies.Count);

                // Delete
                puller.DeleteCookie(name2);
                Assert.AreEqual(1, cookieContainer.GetCookies(replicationUrl).Count);
                Assert.AreEqual(name, cookieContainer.GetCookies(replicationUrl)[0].Name);
            }
        }

        [Test]
        public void TestCheckServerCompatVersion()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var replicator = database.CreatePushReplication(new Uri("http://fake.test-url.com:4984/fake/"));
            Assert.IsFalse(replicator.CheckServerCompatVersion("0.01"));

            replicator.ServerType = new RemoteServerVersion("Couchbase Sync Gateway/1.1.0");
            Assert.IsTrue(replicator.CheckServerCompatVersion("1.00"));
            Assert.IsFalse(replicator.CheckServerCompatVersion("2.00"));
        }

        [Test]
        public void TestPusherFindCommonAncestor()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var ids = new List<string>();
            ids.Add("second");
            ids.Add("first");

            var revDict = new Dictionary<string, object>();
            revDict["ids"] = ids;
            revDict["start"] = 2;

            var properties = new Dictionary<string, object>();
            properties["_revisions"] = revDict;

            var rev = new RevisionInternal(properties);
            Assert.AreEqual(0, Pusher.FindCommonAncestor(rev, new List<RevisionID>()));
            Assert.AreEqual(0, Pusher.FindCommonAncestor(rev, (new [] {"3-noway".AsRevID(), "1-nope".AsRevID() }).ToList()));
            Assert.AreEqual(1, Pusher.FindCommonAncestor(rev, (new [] {"3-noway".AsRevID(), "1-first".AsRevID() }).ToList()));
            Assert.AreEqual(2, Pusher.FindCommonAncestor(rev, (new [] {"3-noway".AsRevID(), "2-second".AsRevID(), "1-first".AsRevID() }).ToList()));
        }

        [Test]
        public void TestPushFilteredByDocId()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var countdown = new CountdownEvent(1);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                int changesCount = 0;
                pusher.Changed += (sender, e) =>
                {
                    if (e.Source.ChangesCount > 0 && countdown.CurrentCount > 0) {
                        changesCount = e.Source.ChangesCount;
                        countdown.Signal();
                    }
                };


                var doc1 = database.CreateDocument();
                doc1.PutProperties(new Dictionary<string, object> {
                    { "doc1", "Foo" }
                });
                pusher.DocIds = new List<string> { doc1.GetProperty<string>("_id") };

                var doc2 = database.CreateDocument();
                doc2.PutProperties(new Dictionary<string, object> {
                    { "doc2", "Foo" }
                });

                pusher.Start();
                Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Replication timed out");
                Assert.AreEqual(1, changesCount);
            }
        }

        /**
        * Verify that running a continuous push replication will emit a change while
        * in an error state when run against a mock server that returns 500 Internal Server
        * errors on every request.
        */
        [Test]
        public void TestContinuousReplicationErrorNotification() {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var httpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = httpClientFactory;

            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderThrowExceptionAllRequests();

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Continuous = true;

                var signal = new CountdownEvent(1);
                var observer = new ReplicationErrorObserver(signal);
                pusher.Changed += observer.Changed;
                pusher.Start();

                CreateDocuments(database, 10);

                var success = signal.Wait(TimeSpan.FromSeconds(30));
                Assert.IsTrue(success);

                StopReplication(pusher);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestContinuousPusherWithAttachment()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remote = remoteDb.RemoteUri;

                var pusher = database.CreatePushReplication(remote);
                pusher.Continuous = true;
                pusher.Start();

                var docIdTimestamp = Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
                var doc1Id = string.Format("doc1-{0}", docIdTimestamp);

                var document = database.GetDocument(doc1Id);
                var values = new Dictionary<string,object> {
                    { "type" , "attachment_test" },
                };

                document.PutProperties(values);

                Sleep(5000);

                long expectedLength = 0;
                document.Update((r) =>
                {
                    var attachmentStream = GetAsset("attachment2.png");
                    var memoryStream = new MemoryStream();
                    attachmentStream.CopyTo(memoryStream);
                    expectedLength = memoryStream.Length;

                    r.SetAttachment("content", "application/octet-stream", memoryStream.ToArray());
                    return true;
                });

                // Make sure it has time to push the document
                Sleep(5000);

                // make sure the doc has been added
                remoteDb.VerifyDocumentExists(doc1Id);

                Sleep(2000);
                var json = GetRemoteDocById(remote, doc1Id);

                try {
                    var attachments = json["_attachments"].AsDictionary<string,object>();
                    var content = attachments["content"].AsDictionary<string,object>();
                    var lengthAsStr = content["length"];
                    var length = Convert.ToInt64(lengthAsStr);
                    Assert.AreEqual(expectedLength, length);
                    WriteDebug("TestContinuousPusherWithAttachment() finished");
                } finally {
                    StopReplication(pusher);
                }
            }
        }  

        private IDictionary<string, object> GetRemoteDocById(Uri remote, string docId)
        {
            var replicationUrlTrailing = new Uri(string.Format("{0}/", remote));
            var pathToDoc = new Uri(replicationUrlTrailing, docId);
            var request = new HttpRequestMessage(HttpMethod.Get, pathToDoc);
            using(var client = new HttpClient()) {
                var response = client.SendAsync(request).Result;
                var result = response.Content.ReadAsStringAsync().Result;
                var json = Manager.GetObjectMapper().ReadValue<JObject>(result);
                return json.AsDictionary<string, object>();
            }
        }

        [Test]
        public void TestDifferentCheckpointsFilteredReplication() {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var pullerNoFilter = database.CreatePullReplication(new Uri("http://fake.test-url.com:4984/fake/"));
            var noFilterCheckpointDocId = pullerNoFilter.RemoteCheckpointDocID();

            var pullerWithFilter1 = database.CreatePullReplication(new Uri("http://fake.test-url.com:4984/fake/"));
            pullerWithFilter1.Filter = "foo/bar";
            pullerWithFilter1.DocIds = new List<string>() 
            { 
                "doc3", "doc1", "doc2"
            };
            pullerWithFilter1.FilterParams = new Dictionary<string, object>() 
            { 
                { "a", "aval" },
                { "b", "bval" }
            };
            var withFilterCheckpointDocId = pullerWithFilter1.RemoteCheckpointDocID();
            Assert.IsFalse(withFilterCheckpointDocId.Equals(noFilterCheckpointDocId));

            var pullerWithFilter2 = database.CreatePullReplication(new Uri("http://fake.test-url.com:4984/fake/"));
            pullerWithFilter2.Filter = "foo/bar";
            pullerWithFilter2.DocIds = new List<string>() 
            { 
                "doc2", "doc3", "doc1"
            };
            pullerWithFilter2.FilterParams = new Dictionary<string, object>() 
            { 
                { "b", "bval" },
                { "a", "aval" }
            };
            var withFilterCheckpointDocId2 = pullerWithFilter2.RemoteCheckpointDocID();
            Assert.IsTrue(withFilterCheckpointDocId.Equals(withFilterCheckpointDocId2));
        }

        [Test]
        public void TestPusherBatching()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            // Create a bunch (InboxCapacity * 2) local documents
            var numDocsToSend = Replication.InboxCapacity * 2;
            for (var i = 0; i < numDocsToSend; i++)
            {
                var properties = new Dictionary<string, object>();
                properties["testPusherBatching"] = i;
                var doc = database.CreateDocument();
                var rev = doc.PutProperties(properties);
                Assert.IsNotNull(rev);
            }

            // Kick off a one time push replication to a mock
            var httpClientFactory = new MockHttpClientFactory();
            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderFakeLocalDocumentUpdate404();
            manager.DefaultHttpClientFactory = httpClientFactory;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);
                Assert.IsNull(pusher.LastError);

                var numDocsSent = 0;

                // Verify that only INBOX_SIZE documents are included in any given bulk post request
                var capturedRequests = httpHandler.CapturedRequests;
                foreach (var request in capturedRequests) {
                    if (request.Method == HttpMethod.Post &&
                    request.RequestUri.AbsoluteUri.EndsWith("_bulk_docs", StringComparison.Ordinal)) {
                        var bytes = request.Content.ReadAsByteArrayAsync().Result;
                        var body = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(bytes.AsEnumerable());
                        var docs = (JArray)body["docs"];
                        numDocsSent += docs.Count;
                    }
                }

                Assert.AreEqual(numDocsToSend, numDocsSent);
            }
        }

        private void RunPushReplicationWithTransientError(Int32 errorCode, string statusMessage, Boolean expectError) 
        {
            var properties1 = new Dictionary<string, object>() 
            {
                {"doc1", "testPushReplicationTransientError"}
            };
            CreateDocumentWithProperties(database, properties1);

            var httpClientFactory = new MockHttpClientFactory(false);
            var httpHandler = httpClientFactory.HttpHandler; 
            manager.DefaultHttpClientFactory = httpClientFactory;

            MockHttpRequestHandler.HttpResponseDelegate sentinal = MockHttpRequestHandler.FakeBulkDocs;

            var responders = new List<MockHttpRequestHandler.HttpResponseDelegate>();
            responders.Add(MockHttpRequestHandler.TransientErrorResponder(errorCode, statusMessage));
            MockHttpRequestHandler.HttpResponseDelegate chainResponder = (request) =>
            {
                if (responders.Count > 0) {
                    var responder = responders[0];
                    responders.RemoveAt(0);
                    return responder(request);
                }

                return sentinal(request);
            };

            httpHandler.SetResponder("_bulk_docs", chainResponder);

            // Create a replication observer to wait until replication finishes
            var replicationDoneSignal = new CountdownEvent(1);
            var replicationFinishedObserver = new ReplicationObserver(replicationDoneSignal);
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Changed += replicationFinishedObserver.Changed;

                // save the checkpoint id for later usage
                var checkpointId = pusher.RemoteCheckpointDocID();

                // kick off the replication
                pusher.Start();

                // wait for it to finish
                var success = replicationDoneSignal.Wait(TimeSpan.FromSeconds(30));
                Assert.IsTrue(success);

                if (expectError) {
                    Assert.IsNotNull(pusher.LastError);
                }
                else {
                    Assert.IsNull(pusher.LastError);
                }

                // workaround for the fact that the replicationDoneSignal.Await() call could unblock before all
                // the statements in Replication.Stopped() have even had a chance to execute.
                Sleep(500);

                var localLastSequence = database.LastSequenceWithCheckpointId(checkpointId);
                if (expectError) {
                    Assert.Null(localLastSequence);
                }
                else {
                    Assert.IsNotNull(localLastSequence);
                }
            }
        }
            
        [Test]
        public void TestPushReplicationRecoverableError()
        {
            var statusCode = 503;
            var statusMessage = "Transient Error";
            var expectError = false;
            RunPushReplicationWithTransientError(statusCode, statusMessage, expectError);
        }

        [Test]
        public void TestPushReplicationRecoverableIOException() {
            var statusCode = -1; // code to tell it to throw an IOException
            string statusMessage = null;
            var expectError = false;
            RunPushReplicationWithTransientError(statusCode, statusMessage, expectError);
        }

        [Test]
        public void TestPushReplicationNonRecoverableError()
        {
            var statusCode = 404;
            var statusMessage = "NOT FOUND";
            var expectError = true;
            RunPushReplicationWithTransientError(statusCode, statusMessage, expectError);
        }

        [Test]
        public void TestPushUpdatedDocWithoutReSendingAttachments() 
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            Assert.AreEqual(0, database.GetLastSequenceNumber());

            var properties1 = new Dictionary<string, object>() {
                { "dynamic", 1 }
            };

            var doc = CreateDocumentWithProperties(database, properties1);
            var rev1 = doc.CurrentRevision;

            var unsavedRev2 = doc.CreateRevision();
            var attachmentStream = GetAsset("attachment.png");
            unsavedRev2.SetAttachment("attachment.png", "image/png", attachmentStream);
            var rev2 = unsavedRev2.Save();

            // Kick off a one time push replication to a mock
            var httpClientFactory = new MockHttpClientFactory();
            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderFakeLocalDocumentUpdate404();
            httpHandler.SetResponder(doc.Id, (request) => 
            {
                var content = new Dictionary<string, object>()
                {
                    {"id", doc.Id},
                    {"ok", true},
                    {"rev", doc.CurrentRevisionId}
                };
                return MockHttpRequestHandler.GenerateHttpResponseMessage(content);
            });
            manager.DefaultHttpClientFactory = httpClientFactory;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);
         
                httpHandler.ClearCapturedRequests();

                var oldDoc = database.GetDocument(doc.Id);
                var unsavedRev = oldDoc.CreateRevision();
                var props = new Dictionary<string, object>(oldDoc.UserProperties);
                props["dynamic"] = Convert.ToInt64(oldDoc.Properties["dynamic"]) + 1;
                unsavedRev.SetProperties(props);
                var savedRev = unsavedRev.Save();
                httpHandler.SetResponder(doc.Id, (request) =>
                {
                    var content = new Dictionary<string, object>() {
                        { "id", doc.Id },
                        { "ok", true },
                        { "rev", savedRev.Id }
                    };
                    return MockHttpRequestHandler.GenerateHttpResponseMessage(content);
                });

                httpHandler.SetResponder("_revs_diff", (request) =>
                {
                    var json = String.Format("{{\"{0}\":{{\"missing\":[\"{1}\"],\"possible_ancestors\":[\"{2},{3}\"]}}}}", doc.Id, savedRev.Id, rev1.Id, rev2.Id);
                    return MockHttpRequestHandler.GenerateHttpResponseMessage(HttpStatusCode.OK, "OK", json);
                });

                pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);

                foreach (var request in httpHandler.CapturedRequests) {
                    if (request.Method == HttpMethod.Put) {
                        var isMultipartContent = (request.Content is MultipartContent);
                        Assert.IsFalse(isMultipartContent);
                    }
                }
            }
        }

        [Test]
        public void TestServerDoesNotSupportMultipart() {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            database.GetLastSequenceNumber().Should().Be(0, "because the database is empty");

            var properties1 = new Dictionary<string, object>() {
                { "dynamic", 1 }
            };

            var doc = CreateDocumentWithProperties(database, properties1);
            var rev1 = doc.CurrentRevision;

            var unsavedRev2 = doc.CreateRevision();
            var attachmentStream = GetAsset("attachment.png");
            unsavedRev2.SetAttachment("attachment.png", "image/png", attachmentStream);
            var rev2 = unsavedRev2.Save();
            attachmentStream.Dispose();
            rev2.Should().NotBeNull("because otherwise the revision failed to save");
            unsavedRev2.Dispose();

            var httpClientFactory = new MockHttpClientFactory();
            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderFakeLocalDocumentUpdate404();

            var responders = new List<MockHttpRequestHandler.HttpResponseDelegate>();
            responders.Add((request) => 
            {
                var json = "{\"error\":\"Unsupported Media Type\",\"reason\":\"missing\"}";
                return MockHttpRequestHandler.GenerateHttpResponseMessage(HttpStatusCode.UnsupportedMediaType, 
                    "Unsupported Media Type", json);
            });

            responders.Add((request) => 
            {
                var props = new Dictionary<string, object>()
                {
                    {"id", doc.Id},
                    {"ok", true},
                    {"rev", doc.CurrentRevisionId}

                };
                return MockHttpRequestHandler.GenerateHttpResponseMessage(props);
            });

            MockHttpRequestHandler.HttpResponseDelegate chainResponder = (request) =>
            {
                if (responders.Count > 0) {
                    var responder = responders[0];
                    responders.RemoveAt(0);
                    return responder(request);
                }
                return null;
            };

            httpHandler.SetResponder(doc.Id, chainResponder);
            manager.DefaultHttpClientFactory = httpClientFactory;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);

                var count = 0;
                foreach (var request in httpHandler.CapturedRequests) {
                    if (request.Method == HttpMethod.Put && request.RequestUri.PathAndQuery.Contains(doc.Id)) {
                        var isMultipartContent = (request.Content is MultipartContent);
                        if (count == 0) {
                            isMultipartContent.Should().BeTrue("because the first attempt will try multipart");
                        }
                        else {
                            isMultipartContent.Should().BeFalse("because the second attempt will fall back to non-multipart");
                        }
                        count++;
                    }
                }
            }
        }

        [Test]
        public void TestPushPullDocumentWithAttachment()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var props = new Dictionary<string, object>()
            { 
                {"type", "post"},
                {"title", "This is a post."}
            };

            var doc = database.CreateDocument();
            doc.PutProperties(props);
            var docId = doc.Id;

            var unsavedRev = doc.CreateRevision();
            var attachmentStream = GetAsset("attachment.png");
            unsavedRev.SetAttachment("photo", "image/png", attachmentStream);
            var rev = unsavedRev.Save();
            var attachment = rev.GetAttachment("photo");
            var attachmentLength = attachment.Length;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);

                //In release mode this actually goes so fast that sync gateway doesn't
                //have time to store the document before we try to pull it
                Sleep(500);

                var db = manager.GetDatabase("tmp");

                var puller = db.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(puller);

                doc = db.GetExistingDocument(docId);
                Assert.IsNotNull(doc);

                attachment = doc.CurrentRevision.GetAttachment("photo");
                Assert.IsNotNull(attachment);
                Assert.AreEqual(attachmentLength, attachment.Length);
                Assert.IsNotNull(attachment.Content);
                db.Close();
            }
        }

        [Test, Category("issue348")]
        public void TestConcurrentPushPullAndLiveQueryWithFilledDatabase()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            // Create remote docs.
            const int docsToCreate = 100;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var docList = remoteDb.AddDocuments(docsToCreate, true);

                // Create local docs
                for (int i = 0; i < docsToCreate; i++) {
                    var docIdTimestamp = Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
                    var docId = string.Format("doc{0}-{1}", i, docIdTimestamp);
                
                    var docBody = GetDocWithId(docId, "attachment.png");
                    var doc = database.CreateDocument();
                    doc.PutProperties(docBody);
                    docList.Add(doc.Id);
                }

                var mre = new CountdownEvent(docsToCreate * 2);
                var puller = database.CreatePullReplication(remoteDb.RemoteUri);
                puller.Changed += (sender, e) =>
                {
                    WriteDebug("Puller Changed: {0}/{1}/{2}", puller.Status, puller.ChangesCount, puller.CompletedChangesCount);
                    if (puller.Status != ReplicationStatus.Stopped)
                        return;
                    WriteDebug("Puller Completed Changes after stopped: {0}", puller.CompletedChangesCount);
                };
                int numDocsBeforePull = database.GetDocumentCount();
                View view = database.GetView("testPullerWithLiveQueryView");
                view.SetMapReduce((document, emitter) =>
                {
                    if (document.CblID() != null) {
                        emitter(document.CblID(), null);
                    }
                }, null, "1");

                LiveQuery allDocsLiveQuery = view.CreateQuery().ToLiveQuery();
                int numTimesCalled = 0;
                allDocsLiveQuery.Changed += (sender, e) =>
                {
                    if (e.Error != null) {
                        throw new ApplicationException("Fail", e.Error);
                    }
                    if (numTimesCalled++ > 0 && e.Rows.Count > 0) {
                        Assert.IsTrue(e.Rows.Count > numDocsBeforePull, String.Format("e.Rows.Count ({0}) <= numDocsBeforePull ({1})", e.Rows.Count, numDocsBeforePull));
                    }
                    WriteDebug("rows {0} / times called {1}", e.Rows.Count, numTimesCalled);
                    foreach (var row in e.Rows) {
                        if (docList.Contains(row.DocumentId)) {
                            mre.Signal();
                            docList.Remove(row.DocumentId);
                        }
                    }

                    WriteDebug("Remaining docs to be found: {0}", mre.CurrentCount);
                };

                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Start();

                // the first time this is called back, the rows will be empty.
                // but on subsequent times we should expect to get a non empty
                // row set.
                allDocsLiveQuery.Start();
                puller.Start();

                Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(180)), "Replication Timeout");

                StopReplication(pusher);
                StopReplication(puller);
                allDocsLiveQuery.Stop();  
            }
        }

        [Test]
        public void TestPullReplicationWithUsername()
        {
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                remoteDb.DisableGuestAccess();
                var docIdTimestamp = Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
                var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
                var doc2Id = string.Format("doc2-{0}", docIdTimestamp);

                remoteDb.AddDocument(doc1Id, "attachment.png");
                remoteDb.AddDocument(doc2Id, "attachment2.png");

                var repl = database.CreatePullReplication(remoteDb.RemoteUri);
                repl.Authenticator = new BasicAuthenticator("jim", "borden");
                var wait = new CountdownEvent(1);
                repl.Changed += (sender, e) =>
                {
                    if(wait.CurrentCount == 0) {
                        return;
                    }

                    WriteDebug("New replication status {0}", e.Source.Status);
                    if((e.Source.Status == ReplicationStatus.Idle || e.Source.Status == ReplicationStatus.Stopped) &&
                        e.Source.ChangesCount > 0 && e.Source.CompletedChangesCount == e.Source.ChangesCount) {
                        wait.Signal();
                    }
                };
                repl.Start();

                Assert.IsTrue(wait.Wait(TimeSpan.FromSeconds(60)), "Pull replication timed out");
                Assert.IsNotNull(database.GetExistingDocument(doc1Id), "Didn't get doc1 from puller");
                Assert.IsNotNull(database.GetExistingDocument(doc2Id), "Didn't get doc2 from puller");
                Assert.IsNull(repl.LastError);
                repl.Stop();

                docIdTimestamp = Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
                doc1Id = string.Format("doc1-{0}", docIdTimestamp);
                doc2Id = string.Format("doc2-{0}", docIdTimestamp);
                remoteDb.AddDocument(doc1Id, "attachment.png");
                remoteDb.AddDocument(doc2Id, "attachment2.png");

                repl = database.CreatePullReplication(remoteDb.RemoteUri);
                repl.Changed += (sender, e) =>
                {
                    if(wait.CurrentCount == 0) {
                        return;
                    }

                    WriteDebug("New replication status {0}", e.Source.Status);
                    if((e.Source.Status == ReplicationStatus.Idle || e.Source.Status == ReplicationStatus.Stopped) &&
                        e.Source.CompletedChangesCount == e.Source.ChangesCount) {
                        wait.Signal();
                    }
                };

                repl.Authenticator = new BasicAuthenticator("jim", "bogus");
                wait.Reset(1);
                repl.Start();
                Assert.IsTrue(wait.Wait(TimeSpan.FromSeconds(60)), "Pull replication timed out");
                Assert.IsNull(database.GetExistingDocument(doc1Id), "Got rogue doc1 from puller");
                Assert.IsNull(database.GetExistingDocument(doc2Id), "Got rogue doc2 from puller");
                repl.Stop();
            }
            
        }

        [Test]
        public void TestReplicationCheckpointUniqueness()
        {
            var repl1 = database.CreatePullReplication(new Uri("http://fake.test-url.com:4984/fake/"));
            var repl2 = database.CreatePullReplication(new Uri("http://fake.test-url.com:4984/fake/"));

            Assert.AreEqual(repl1.RemoteCheckpointDocID(), repl2.RemoteCheckpointDocID());

            repl1 = database.CreatePullReplication(new Uri("http://fake.test-url.com:4984/fake/"));
            repl2 = database.CreatePullReplication(new Uri("http://fake.test-url.com:4984/fake/"));
            repl1.Channels = new List<string> { "A" };
            repl2.Channels = new List<string> { "A", "B" };

            Assert.AreNotEqual(repl1.RemoteCheckpointDocID(), repl2.RemoteCheckpointDocID());
        }

        [Test]
        [Category("issue_398")]
        public void TestPusherUsesFilterParams()
        {
            var docIdTimestamp = Convert.ToString((ulong)DateTime.UtcNow.TimeSinceEpoch().TotalMilliseconds);
            var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
            var doc2Id = string.Format("doc2-{0}", docIdTimestamp);

            var doc1 = database.GetDocument(doc1Id);
            doc1.PutProperties(new Dictionary<string, object> { { "foo", "bar" } });

            var doc2 = database.GetDocument(doc2Id);
            doc2.PutProperties(new Dictionary<string, object> { { "bar", "foo" } });

            var mre = new ManualResetEventSlim();
            IDictionary<string, object> gotParams = null;
            database.SetFilter("issue-398", (rev, parameters) =>
            {
                gotParams = parameters;
                if(!mre.IsSet) {
                    mre.Set();
                }

                return true;
            });

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                push.Filter = "issue-398";
                push.FilterParams = new Dictionary<string, object> { { "issue-398", "finished" } };
                push.Start();

                mre.Wait();
                Assert.AreEqual(push.FilterParams, gotParams);
            }
        }
            
        [Test]
        public void TestBulkPullTransientExceptionRecovery() {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var fakeFactory = new MockHttpClientFactory(false);
            FlowControl flow = new FlowControl(new FlowItem[]
            {
                new FunctionRunner<HttpResponseMessage>(() => {
                    Sleep(7000);
                    return new RequestCorrectHttpMessage();
                }) { ExecutionCount = 2 },
                new FunctionRunner<HttpResponseMessage>(() => {
                    fakeFactory.HttpHandler.ClearResponders();
                    return new RequestCorrectHttpMessage();
                }) { ExecutionCount = 1 }
            });

            fakeFactory.HttpHandler.SetResponder("_bulk_get", (request) =>
                flow.ExecuteNext<HttpResponseMessage>());
            manager.DefaultHttpClientFactory = fakeFactory;
#pragma warning disable 618
            ManagerOptions.Default.RequestTimeout = TimeSpan.FromSeconds(5);
#pragma warning restore 618

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                CreatePullAndTest(20, remoteDb, (repl) => Assert.AreEqual(20, database.GetDocumentCount(), "Didn't recover from the error"));
            }

            Thread.Sleep(1000);
        }

        [Test]
        public void TestBulkPullPermanentExceptionSurrender() {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            Log.Domains.Sync.Level = Log.LogLevel.Debug;
            var fakeFactory = new MockHttpClientFactory(false);
            FlowControl flow = new FlowControl(new FlowItem[]
            {
                new ExceptionThrower(new SocketException()) { ExecutionCount = -1 },
            });

            fakeFactory.HttpHandler.SetResponder("_bulk_get", (request) => 
                flow.ExecuteNext<HttpResponseMessage>());
            manager.DefaultHttpClientFactory = fakeFactory;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                CreatePullAndTest(20, remoteDb, repl => Assert.IsTrue(database.GetDocumentCount() < 20, "Somehow got all the docs"));
            }
        }

        [Test]
        public void TestFailedBulkGetDoesntChangeLastSequence()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            string firstBulkGet = null;
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var fakeFactory = new MockHttpClientFactory(false);
                fakeFactory.HttpHandler.SetResponder("_bulk_get", (request) =>
                {
                    var str = default(string);
                    if(request.Content is CompressedContent) {
                        var stream = request.Content.ReadAsStreamAsync().Result;
                        str = Encoding.UTF8.GetString(stream.ReadAllBytes());
                    } else {
                        str = request.Content.ReadAsStringAsync().Result;
                    }

                    if (firstBulkGet == null || firstBulkGet.Equals(str)) {
                        WriteDebug("Rejecting this bulk get because it looks like the first batch");
                        firstBulkGet = str;
                        throw new OperationCanceledException();
                    }

                    WriteDebug("Letting this bulk get through");
                    return new RequestCorrectHttpMessage();
                });

                int gotSequence = 0;
                fakeFactory.HttpHandler.SetResponder("doc", request =>
                {
                    Regex r = new Regex("doc[0-9]+");
                    var m = r.Match(request.RequestUri.PathAndQuery);
                    if(m.Success) {
                        var str = m.Captures[0].Value;
                        var converted = Int32.Parse(str.Substring(3)) + 4;
                        if(gotSequence == 0 || converted - gotSequence == 1) {
                            gotSequence = converted;
                        }
                    }

                    return new RequestCorrectHttpMessage();
                });
                
                manager.DefaultHttpClientFactory = fakeFactory;
#pragma warning disable 618
                Manager.DefaultOptions.MaxRevsToGetInBulk = 10;
                Manager.DefaultOptions.MaxOpenHttpConnections = 8;
                Manager.DefaultOptions.RequestTimeout = TimeSpan.FromSeconds(5);

                CreatePullAndTest((int)(Manager.DefaultOptions.MaxRevsToGetInBulk * 1.5), remoteDb, repl =>
                {
                    WriteDebug("Document count increased to {0} with last sequence '{1}'", database.GetDocumentCount(), repl.LastSequence);
                    Assert.IsTrue(database.GetDocumentCount() > 0, "Didn't get docs from second bulk get batch");
                    Assert.AreEqual(gotSequence, Int32.Parse(repl.LastSequence), "LastSequence was advanced");
                });
#pragma warning restore 618

                Sleep(500);
                fakeFactory.HttpHandler.ClearResponders();
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(pull);
                Assert.AreEqual(pull.ChangesCount, pull.CompletedChangesCount);
                Assert.AreNotEqual(pull.LastSequence, "0");
            }
        }

        [Test]
        public void TestRemovedRevision()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
                
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var doc = database.GetDocument("doc1");
                var unsaved = doc.CreateRevision();
                unsaved.SetUserProperties(new Dictionary<string, object> {
                    { "_removed", true }
                });

                Assert.DoesNotThrow(() => unsaved.Save());
                 
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Start();

                Assert.IsTrue(pusher.IsDocumentPending(doc));
                RunReplication(pusher);
                Assert.IsNull(pusher.LastError);
                Assert.AreEqual(0, pusher.ChangesCount);
                Assert.AreEqual(0, pusher.CompletedChangesCount);
                Assert.IsFalse(pusher.IsDocumentPending(doc));
            }
        }

        //Note: requires manual intervention (unplugging network cable, etc)
        public void TestReactToNetworkChange()
        {
            CreateDocuments(database, 10);
            var offlineEvent = new ManualResetEvent(false);
            var resumedEvent = new ManualResetEvent(false);
            var finishedEvent = new ManualResetEvent(false);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var push = database.CreatePushReplication(remoteDb.RemoteUri);
                push.Continuous = true;
                push.Changed += (sender, args) =>
                {
                    if (args.Status == ReplicationStatus.Offline) {
                        Console.WriteLine("Replication went offline");
                        offlineEvent.Set();
                    } else if (args.Status == ReplicationStatus.Active) {
                        Console.WriteLine("Replication resumed");
                        resumedEvent.Set();
                    } else if (args.Status == ReplicationStatus.Idle) {
                        Console.WriteLine("Replication finished");
                        finishedEvent.Set();
                    }
                };

                push.Start();

                // ***** PULL OUT NETWORK CABLE OR SOMETHING HERE ***** //
                Task.Delay(1000).ContinueWith(t => Console.WriteLine("***** Test will continue when network connectivity is lost... *****"));
                Assert.True(offlineEvent.WaitOne(TimeSpan.FromSeconds(60)));
                CreateDocuments(database, 10);

                // ***** UNDO THE ABOVE CHANGES AND RESTORE CONNECTIVITY ***** //
                Console.WriteLine("***** Test will continue when network connectivity is restored... *****");
                resumedEvent.Reset();
                Assert.True(resumedEvent.WaitOne(TimeSpan.FromSeconds(60)));
                finishedEvent.Reset();
                Assert.True(finishedEvent.WaitOne(TimeSpan.FromSeconds(15)));
                Assert.AreEqual(20, push.ChangesCount);
                Assert.AreEqual(20, push.CompletedChangesCount);
                push.Stop();
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                remoteDb.AddDocuments(10, false);
                var secondDb = manager.GetDatabase("foo");
                var pull = secondDb.CreatePullReplication(remoteDb.RemoteUri);
                pull.Continuous = true;
                pull.Changed += (sender, args) =>
                {
                    if (args.Status == ReplicationStatus.Offline) {
                        Console.WriteLine("Replication went offline");
                        offlineEvent.Set();
                    } else if (args.Status == ReplicationStatus.Active) {
                        Console.WriteLine("Replication resumed");
                        resumedEvent.Set();
                    } else if (args.Status == ReplicationStatus.Idle) {
                        Console.WriteLine("Replication finished");
                        finishedEvent.Set();
                    }
                };

                offlineEvent.Reset();
                pull.Start();
                // ***** PULL OUT NETWORK CABLE OR SOMETHING HERE ***** //
                Task.Delay(2000).ContinueWith(t => Console.WriteLine("***** Test will continue when network connectivity is lost... *****"));
                Assert.True(offlineEvent.WaitOne(TimeSpan.FromSeconds(60)));

                remoteDb.AddDocuments(10, false);

                // ***** UNDO THE ABOVE CHANGES AND RESTORE CONNECTIVITY ***** //
                Console.WriteLine("***** Test will continue when network connectivity is restored... *****");
                resumedEvent.Reset();
                Assert.True(resumedEvent.WaitOne(TimeSpan.FromSeconds(60)));
                finishedEvent.Reset();
                Assert.True(finishedEvent.WaitOne(TimeSpan.FromSeconds(15)));
                Assert.AreEqual(20, secondDb.GetDocumentCount());
                Assert.AreEqual(20, pull.ChangesCount);
                Assert.AreEqual(20, pull.CompletedChangesCount);
                pull.Stop();
            }
        }

        [Test]
        public void TestRemovedChangesFeed()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName(), "test_user", "1234")) {

                var doc = database.GetDocument("channel_walker");
                doc.PutProperties(new Dictionary<string, object> {
                    { "foo", "bar" },
                    { "channels", new List<object> { "unit_test" } }
                });

                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);

                doc.Update(rev =>
                {
                    var props = rev.UserProperties;
                    props["channels"] = new List<object> { "no_mans_land" };
                    props.Remove("foo");
                    rev.SetUserProperties(props);
                    return true;
                });

                pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);

                doc.Update(rev =>
                {
                    var props = rev.UserProperties;
                    props["magic"] = true;
                    rev.SetUserProperties(props);
                    return true;
                });

                pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);

                database.Delete();
                database = manager.GetDatabase(database.Name);

                remoteDb.DisableGuestAccess();
                var puller = database.CreatePullReplication(remoteDb.RemoteUri);
                puller.Authenticator = new BasicAuthenticator("test_user", "1234");
                RunReplication(puller);
                Assert.IsNull(puller.LastError);
                Assert.AreEqual(0, database.GetDocumentCount());
                Assert.DoesNotThrow(() => doc = database.GetExistingDocument("channel_walker"));
                Assert.IsNull(doc);
            }
        }

        [Test]
        public void TestReplicatorSeparateCookies()
        {
            using (var secondDb = manager.GetDatabase("cblitetest2"))
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var puller1 = database.CreatePullReplication(remoteDb.RemoteUri);
                puller1.SetCookie("whitechoco", "sweet", "/", DateTime.Now.AddSeconds(60), false, false);
                Assert.AreEqual(1, puller1.CookieContainer.Count);
                var pusher1 = database.CreatePushReplication(remoteDb.RemoteUri);
                Assert.AreEqual(1, pusher1.CookieContainer.Count);
                var puller2 = secondDb.CreatePullReplication(remoteDb.RemoteUri);
                Assert.AreEqual(0, puller2.CookieContainer.Count);

                puller1.SetCookie("whitechoco", "bitter sweet", "/", DateTime.Now.AddSeconds(60), false, false);
                Assert.AreEqual(1, puller1.CookieContainer.Count);
                Assert.AreEqual(1, pusher1.CookieContainer.Count);
                Assert.AreEqual(0, puller2.CookieContainer.Count);
            }
        }

        [Test]
        public void TestGatewayTemporarilyGoesOffline()
        {
            CreateDocuments(database, 10);
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Continuous = true;
                pusher.Start();
                _sg.SetOffline(remoteDb.Name);
                Sleep(15000);
                _sg.SetOnline(remoteDb.Name);

                while (pusher.Status == ReplicationStatus.Active) {
                    Thread.Sleep(100);
                }

                Assert.AreEqual(ReplicationStatus.Idle, pusher.Status);
                Assert.AreEqual("0", pusher.LastSequence);
                pusher.Stop();
            }
        }

        [Test]
        public void TestStopDoesntWait()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled"))) {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                remoteDb.AddDocuments(1000, false);
                var puller = database.CreatePullReplication(remoteDb.RemoteUri);
                var mre = new ManualResetEventSlim();
                puller.Changed += (sender, e) => 
                {
                    if(e.CompletedChangesCount > 0 && !mre.IsSet) {
                        mre.Set();
                    }
                };

                puller.Start();
                mre.Wait();
                puller.Stop();
                while (puller.Status != ReplicationStatus.Stopped) {
                    Sleep(200);
                }

                Assert.AreNotEqual(1000, puller.CompletedChangesCount);
                Assert.Greater(Int64.Parse(puller.LastSequence), 0);
                Assert.IsNull(puller.LastError);
            }
        }

        [Category("issue/606")]
        public void TestPushAndPurge()
        {
            if (!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            const int numDocs = 100;
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                for (int pass = 1; pass <= 2; ++pass) {
                    Console.WriteLine("Pass #{0}: Creating {1} documents", pass, numDocs);
                    database.RunInTransaction(() =>
                    {
                        for(int i = 1; i <= numDocs; i++) {
                            var doc = database.GetDocument(String.Format("doc-{0}", i));
                            Assert.DoesNotThrow(() => doc.PutProperties(new Dictionary<string, object> {
                                { "index", i },
                                { "bar", false }
                            }));
                        }

                        return true;
                    });

                    var repl = database.CreatePushReplication(remoteDb.RemoteUri);
                    repl.ReplicationOptions.AllNew = true;
                    repl.ReplicationOptions.PurgePushed = true;
                    RunReplication(repl);
                    Assert.IsNull(repl.LastError);
                    Assert.AreEqual(numDocs, repl.ChangesCount);
                    Assert.AreEqual(numDocs, repl.CompletedChangesCount);
                    Assert.AreEqual(0, database.GetDocumentCount());
                }
            }
        }

        [Category("issue/842")]
        [Test]
        public void TestSetCookieInHeader()
        {
            if(!Boolean.Parse((string)GetProperty("replicationTestsEnabled")))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                for (int i = 0; i < 2; i++) {
                    remoteDb.DisableGuestAccess();
                    var cookie = _sg.GenerateSessionCookie(remoteDb.Name, "jim", "borden", TimeSpan.FromSeconds(5));
                    var cookieStr = $"{cookie["cookie_name"]}={cookie["session_id"]}";
                    var repl = database.CreatePushReplication(remoteDb.RemoteUri);
                    repl.Continuous = true;
                    repl.Headers["Cookie"] = cookieStr;
                    RunReplication(repl);
                    Assert.IsNull(repl.LastError);

                    // Sleep for more than 10% of the TTL to trigger auto session refresh
                    // by SGW:
                    Sleep(1000);

                    // Create a document to push to SGW:
                    CreateDocumentWithProperties(database, new Dictionary<string, object> {["foo"] = "bar"});
                    int count = 0;
                    while (repl.CompletedChangesCount < 1) {
                        Sleep(500);
                        if (count++ > 5) {
                            Assert.Fail("Replication timed out");
                        }
                    }

                    StopReplication(repl);
                    _sg.DeleteSessionCookie(remoteDb.Name, "jim");
                }
            }
        }
    }
}