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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;

using NUnit.Framework;

using Sharpen;

using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using Couchbase.Lite.Tests;
using Newtonsoft.Json.Linq;
using System.Threading;
using Couchbase.Lite.Listener.Tcp;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;

#if NET_3_5
using WebRequest = System.Net.Couchbase.WebRequest;
using HttpWebRequest = System.Net.Couchbase.HttpWebRequest;
using HttpWebResponse = System.Net.Couchbase.HttpWebResponse;
using WebException = System.Net.Couchbase.WebException;
#endif


namespace Couchbase.Lite
{
    public class ReplicationTest : LiteTestCase
    {
        private const string Tag = "ReplicationTest";
        private const string TEMP_DB_NAME = "testing_tmp";
        private SyncGateway _sg;
        private static int _dbCounter = 0;
        private HttpClient _httpClient = new HttpClient();

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
                var replicator = args.Source;
                if (replicator.LastError != null && doneSignal.CurrentCount > 0) {
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

        private void CreatePullAndTest(int docCount, RemoteDatabase db, Action<Replication> tester)
        {
            db.AddDocuments(20, false);

            var pull = database.CreatePullReplication(db.RemoteUri);
            RunReplication(pull);

            Log.D("TestPullManyDocuments", "Document count at end {0}", database.DocumentCount);
            tester(pull);
        }

        protected override void SetUp()
        {
            base.SetUp();

            _sg = new SyncGateway(GetReplicationProtocol(), GetReplicationServer());
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
            r1.Options[ReplicationOptionsDictionary.REMOTE_UUID_KEY] = "cafebabe";
            var check1 = r1.RemoteCheckpointDocID();

            var r2 = database.CreatePullReplication(new Uri("http://alice.local:44444/db"));
            r2.Options = r1.Options;
            var check2 = r2.RemoteCheckpointDocID();

            Assert.AreEqual(check1, check2);
            Assert.IsTrue(r1.HasSameSettingsAs(r2));

            var r3 = database.CreatePullReplication(r2.RemoteUrl);
            r3.Options = r1.Options;
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

                    Assert.AreEqual(100, database.DocumentCount);
                } finally {
                    StopReplication(pull1);
                }
            }
        }

        [Test]
        public void TestPullerChangedEvent()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());
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
                Assert.IsTrue(doneEvent.WaitOne(TimeSpan.FromSeconds(60)));
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

                Assert.IsTrue(doneEvent.WaitOne(TimeSpan.FromSeconds(60)));
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
                Thread.Sleep(1000);

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
                Assert.AreEqual(3, database.DocumentCount);
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
                Assert.AreEqual(3, database.DocumentCount);
            }
        }

        // Reproduces issue #167
        // https://github.com/couchbase/couchbase-lite-android/issues/167
        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPushPurgedDoc()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
                    var msg = "changes: {0} completed changes: {1}".Fmt(changesCount, completedChangesCount);
                    Log.D(Tag, msg);
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
                properties.Put("testName2", "update doc");

                var unsavedRevision = doc.CreateRevision();
                unsavedRevision.SetUserProperties(properties);
                unsavedRevision.Save();

                // but then immediately purge it
                doc.Purge();
                pusher.Start();

                // wait for a while to give the replicator a chance to push it
                // (it should not actually push anything)
                Thread.Sleep(5 * 1000);

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


        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPusher()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remote = remoteDb.RemoteUri;
                var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());

                // Create some documents:
                var documentProperties = new Dictionary<string, object>();
                var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
                documentProperties["_id"] = doc1Id;
                documentProperties["foo"] = 1;
                documentProperties["bar"] = false;

                var body = new Body(documentProperties);
                var rev1 = new RevisionInternal(body);
                var status = new Status();
                rev1 = database.PutRevision(rev1, null, false, status);
                Assert.AreEqual(StatusCode.Created, status.Code);

                documentProperties.Put("_rev", rev1.GetRevId());
                documentProperties["UPDATED"] = true;
                database.PutRevision(new RevisionInternal(documentProperties), rev1.GetRevId(), false, status);
                Assert.AreEqual(StatusCode.Created, status.Code);

                documentProperties = new Dictionary<string, object>();
                var doc2Id = string.Format("doc2-{0}", docIdTimestamp);
                documentProperties["_id"] = doc2Id;
                documentProperties["baz"] = 666;
                documentProperties["fnord"] = true;

                database.PutRevision(new RevisionInternal(documentProperties), null, false, status);
                Assert.AreEqual(StatusCode.Created, status.Code);

                var doc2 = database.GetDocument(doc2Id);
                var doc2UnsavedRev = doc2.CreateRevision();
                var attachmentStream = GetAsset("attachment.png");
                doc2UnsavedRev.SetAttachment("attachment_testPusher.png", "image/png", attachmentStream);
                var doc2Rev = doc2UnsavedRev.Save();
                doc2UnsavedRev.Dispose();
                attachmentStream.Dispose();

                Assert.IsNotNull(doc2Rev);

                const bool continuous = false;
                var repl = database.CreatePushReplication(remote);
                repl.Continuous = continuous;
                if (!IsSyncGateway(remote)) {
                    repl.CreateTarget = true;
                }

                // Check the replication's properties:
                Assert.AreEqual(database, repl.LocalDatabase);
                Assert.AreEqual(remote, repl.RemoteUrl);
                Assert.IsFalse(repl.IsPull);
                Assert.IsFalse(repl.Continuous);
                Assert.IsNull(repl.Filter);
                Assert.IsNull(repl.FilterParams);
                Assert.IsNull(repl.DocIds);
                // TODO: CAssertNil(r1.headers); still not null!
                // Check that the replication hasn't started running:
                Assert.IsFalse(repl.IsRunning);
                Assert.AreEqual(ReplicationStatus.Stopped, repl.Status);
                Assert.AreEqual(0, repl.CompletedChangesCount);
                Assert.AreEqual(0, repl.ChangesCount);
                Assert.IsNull(repl.LastError);

                RunReplication(repl);

                // TODO: Verify the foloowing 2 asserts. ChangesCount and CompletedChangesCount
                // should already be reset when the replicator stopped.
                Assert.IsNull(repl.LastError);
                Assert.IsTrue(repl.ChangesCount >= 2);
                Assert.IsTrue(repl.CompletedChangesCount >= 2);


                remoteDb.VerifyDocumentExists(doc1Id);

                // Add doc3
                documentProperties = new Dictionary<string, object>();
                var doc3Id = string.Format("doc3-{0}", docIdTimestamp);
                var doc3 = database.GetDocument(doc3Id);
                documentProperties["bat"] = 677;
                doc3.PutProperties(documentProperties);

                // re-run push replication
                var repl2 = database.CreatePushReplication(remote);
                repl2.Continuous = continuous;
                if (!IsSyncGateway(remote)) {
                    repl2.CreateTarget = true;
                }

                var repl2CheckedpointId = repl2.RemoteCheckpointDocID();

                RunReplication(repl2);

                Assert.IsNull(repl2.LastError);

                Thread.Sleep(1000);

                // make sure trhe doc has been added
                remoteDb.VerifyDocumentExists(doc3Id);

                Assert.AreEqual(repl2.LastSequence, database.LastSequenceWithCheckpointId(repl2CheckedpointId));

                System.Threading.Thread.Sleep(2000);
                var json = GetRemoteDoc(remote, repl2CheckedpointId);
                var remoteLastSequence = (string)json["lastSequence"];
                Assert.AreEqual(repl2.LastSequence, remoteLastSequence);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestPusherDeletedDoc()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remote = remoteDb.RemoteUri;
                var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());

                // Create some documentsConvert
                var documentProperties = new Dictionary<string, object>();
                var doc1Id = string.Format("doc1-{0}", docIdTimestamp);
                documentProperties["_id"] = doc1Id;
                documentProperties["foo"] = 1;
                documentProperties["bar"] = false;

                var body = new Body(documentProperties);
                var rev1 = new RevisionInternal(body);
                var status = new Status();
                rev1 = database.PutRevision(rev1, null, false, status);
                Assert.AreEqual(StatusCode.Created, status.Code);

                documentProperties["_rev"] = rev1.GetRevId();
                documentProperties["UPDATED"] = true;
                documentProperties["_deleted"] = true;
                database.PutRevision(new RevisionInternal(documentProperties), rev1.GetRevId(), false, status);
                Assert.IsTrue((int)status.Code >= 200 && (int)status.Code < 300);

                var repl = database.CreatePushReplication(remote);
                if (!IsSyncGateway(remote)) {
                    ((Pusher)repl).CreateTarget = true;
                }

                RunReplication(repl);

                Assert.IsNull(repl.LastError);

                // make sure doc1 is deleted
                var replicationUrlTrailing = new Uri(string.Format("{0}/", remote));
                var pathToDoc = new Uri(replicationUrlTrailing, doc1Id);
                Log.D(Tag, "Send http request to " + pathToDoc);
                var httpRequestDoneSignal = new CountDownLatch(1);
                using (var httpclient = new HttpClient()) {
                    try {
                        var getDocResponse = httpclient.GetAsync(pathToDoc.ToString()).Result;
                        var statusLine = getDocResponse.StatusCode;
                        Log.D(ReplicationTest.Tag, "statusLine " + statusLine);
                        Assert.AreEqual(Couchbase.Lite.StatusCode.NotFound, statusLine.GetStatusCode());                        
                    }
                    catch (ProtocolViolationException e) {
                        Assert.IsNull(e, "Got ClientProtocolException: " + e.Message);
                    }
                    catch (IOException e) {
                        Assert.IsNull(e, "Got IOException: " + e.Message);
                    }
                    finally {
                        httpRequestDoneSignal.CountDown();
                    }
                    Log.D(Tag, "Waiting for http request to finish");
                    try {
                        httpRequestDoneSignal.Await(TimeSpan.FromSeconds(10));
                        Log.D(Tag, "http request finished");
                    }
                    catch (Exception e) {
                        Runtime.PrintStackTrace(e);
                    }
                }
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

                var docName = "doc" + Convert.ToString(DateTime.UtcNow.ToMillisecondsSinceEpoch());
                var endpoint = remoteDb.RemoteUri.AppendPath(docName);
                var docContent = Encoding.UTF8.GetBytes("{\"foo\":false}");
                var putRequest = new HttpRequestMessage(HttpMethod.Put, endpoint);
                putRequest.Content = new StringContent("{\"foo\":false}");
                putRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = _httpClient.SendAsync(putRequest).Result;
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);


                var attachmentStream = (InputStream)GetAsset("attachment.png");
                var baos = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(baos);
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

                Thread.Sleep(1000);
                while (pull.Status == ReplicationStatus.Active) {
                    Thread.Sleep(500);
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            // Even though this test is passed, there is a runtime exception
            // thrown regarding the replication's number of changes count versus
            // number of completed changes count. Investigation is required.
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            string doc1Id = string.Format("doc1-{0}", docIdTimestamp);
            string doc2Id = string.Format("doc2-{0}", docIdTimestamp);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                remoteDb.AddDocument(doc1Id, "attachment2.png");
                remoteDb.AddDocument(doc2Id, "attachment2.png");

                View view = database.GetView("testPullerWithLiveQueryView");
                view.SetMapReduce((document, emitter) =>
                {
                    if (document.Get("_id") != null) {
                        emitter(document.Get("_id"), null);
                    }
                }, null, "1");

                LiveQuery allDocsLiveQuery = view.CreateQuery().ToLiveQuery();
                allDocsLiveQuery.Changed += (sender, e) =>
                {
                    int numTimesCalled = 0;
                    if (e.Error != null) {
                        throw new RuntimeException(e.Error);
                    }
                    if (numTimesCalled++ > 0) {
                        Assert.IsTrue(e.Rows.Count > 0);
                    }
                    Log.D(Database.TAG, "rows " + e.Rows);
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

                Thread.Sleep(2000);
            }

            Thread.Sleep(1000);
        }

        /// <exception cref="System.IO.IOException"></exception>
        private Dictionary<string,object> GetDocWithId(string docId, string attachmentName)
        {
            Dictionary<string,object> docBody;
            if (attachmentName != null)
            {
                // add attachment to document
                var attachmentStream = (InputStream)GetAsset(attachmentName);
                var baos = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(baos);
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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

                var activeReplicators = new Replication[database.ActiveReplicators.Count];
                database.ActiveReplicators.CopyTo(activeReplicators, 0);
                Assert.AreEqual(1, activeReplicators.Length);
                Assert.AreEqual(replicator, activeReplicators[0]);

                replicator.Stop();

                mre.Reset();
                Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(10)), "Timed out waiting for replicator to stop");
                Assert.IsFalse(replicator.IsRunning);
                Thread.Sleep(500);
                activeReplicators = new Replication[database.ActiveReplicators.Count];
                database.ActiveReplicators.CopyTo(activeReplicators, 0);
                Assert.AreEqual(0, activeReplicators.Length);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestGetReplicatorWithAuth()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
                Thread.Sleep(1000);
                PutReplicationOffline(repl);
                Assert.IsTrue(repl.Status == ReplicationStatus.Offline);

                StopReplication(repl);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public virtual void TestBuildRelativeURLString()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var dbUrlString = "http://10.0.0.3:4984/todos/";
            var replicator = new Pusher(database, new Uri(dbUrlString), false, null);
            string relativeUrlString = replicator.BuildRelativeURLString("foo");
            string expected = "http://10.0.0.3:4984/todos/foo";
            Assert.AreEqual(expected, relativeUrlString);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public virtual void TestBuildRelativeURLStringWithLeadingSlash()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var dbUrlString = "http://10.0.0.3:4984/todos/";
            var replicator = new Pusher(database, new Uri(dbUrlString), false, null);
            string relativeUrlString = replicator.BuildRelativeURLString("/foo");
            string expected = "http://10.0.0.3:4984/todos/foo";
            Assert.AreEqual(expected, relativeUrlString);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public virtual void TestAppendPathURLString([Values("http://10.0.0.3:4984/connect-2014", "http://10.0.0.3:4984/connect-2014/")] String baseUri, [Values("/_bulk_get?revs=true&attachments=true", "_bulk_get?revs=true&attachments=true")] String newPath)
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var remote = new Uri("http://couchbase.com/no_such_db");
            var replicator = database.CreatePullReplication(remote);

            var channels = new List<string>();
            channels.AddItem("chan1");
            channels.AddItem("chan2");
            replicator.Channels = channels;
            Assert.AreEqual(channels, replicator.Channels);

            replicator.Channels = null;
            Assert.IsTrue(replicator.Channels.ToList().Count == 0);
        }

        /// <exception cref="System.UriFormatException"></exception>
        [Test]
        public virtual void TestChannelsMore()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
            filterParams.Put("a", "b");
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
            filterParams.Put("channels", "NBC,MTV");
            Assert.AreEqual(filterParams, r1.FilterParams);
                        
            r1.Channels = null;
            Assert.AreEqual(r1.Filter, null);
            Assert.AreEqual(null, r1.FilterParams);
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestHeaders()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var mockHttpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = mockHttpClientFactory;

            var mockHttpHandler = mockHttpClientFactory.HttpHandler;
            mockHttpHandler.AddResponderThrowExceptionAllRequests();

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remote = remoteDb.RemoteUri;
                var puller = database.CreatePullReplication(remote);
                var headers = new Dictionary<string, string>();
                headers["foo"] = "bar";
                puller.Headers = headers;
                RunReplication(puller);
                Assert.IsNotNull(puller.LastError);

                var foundFooHeader = false;
                var requests = mockHttpHandler.CapturedRequests;

                foreach (var request in requests) {
                    var requestHeaders = request.Headers.GetValues("foo");
                    foreach (var requestHeader in requestHeaders) {
                        foundFooHeader = true;
                        Assert.AreEqual("bar", requestHeader);
                    }
                }
                Assert.IsTrue(foundFooHeader);
                Thread.Sleep(1000);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestAllLeafRevisionsArePushed()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var httpClientFactory = new MockHttpClientFactory();
            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderRevDiffsAllMissing();
            httpHandler.AddResponderFakeLocalDocumentUpdate404();
            httpHandler.ResponseDelayMilliseconds = 250;
            manager.DefaultHttpClientFactory = httpClientFactory;

            var doc = database.CreateDocument();
            var rev1a = doc.CreateRevision().Save();
            var rev2a = rev1a.CreateRevision().Save();
            var rev3a = rev2a.CreateRevision().Save();

            // delete the branch we've been using, then create a new one to replace it
            var rev4a = rev3a.DeleteDocument();
            var unsaved = rev1a.CreateRevision();
            unsaved.SetUserProperties(new Dictionary<string, object> { { "foo", "bar" } });
            var rev2b = unsaved.Save(true);

            Assert.AreEqual(rev2b.Id, doc.CurrentRevisionId);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                // sync with remote DB -- should push both leaf revisions
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);
                Assert.IsNull(pusher.LastError);

                var foundRevsDiff = false;
                var capturedRequests = httpHandler.CapturedRequests;
                foreach (var httpRequest in capturedRequests) {
                    var uriString = httpRequest.RequestUri.ToString();
                    if (uriString.EndsWith("_revs_diff", StringComparison.Ordinal)) {
                        foundRevsDiff = true;
                        var jsonMap = MockHttpRequestHandler.GetJsonMapFromRequest(httpRequest);
                        var revisionIds = ((JArray)jsonMap.Get(doc.Id)).Values<string>().ToList();
                        Assert.AreEqual(2, revisionIds.Count);
                        Assert.IsTrue(revisionIds.Contains(rev4a.Id));
                        Assert.IsTrue(revisionIds.Contains(rev2b.Id));
                    }
                }

                Assert.IsTrue(foundRevsDiff);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestRemoteConflictResolution()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            // Create a document with two conflicting edits.
            var doc = database.CreateDocument();
            var rev1 = doc.CreateRevision().Save();
            var rev2a = CreateRevisionWithRandomProps(rev1, false);
            var rev2b = CreateRevisionWithRandomProps(rev1, true);

            // make sure we can query the db to get the conflict
            var allDocsQuery = database.CreateAllDocumentsQuery();
            allDocsQuery.AllDocsMode = allDocsQuery.AllDocsMode = AllDocsMode.OnlyConflicts;

            var rows = allDocsQuery.Run();
            Assert.AreEqual(1, rows.Count);
            Assert.IsTrue(rows.Aggregate(false, (found, row) => found |= row.Document.Id.Equals(doc.Id)));

            // Push the conflicts to the remote DB.
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                RunReplication(pusher);
                Assert.IsNull(pusher.LastError);

                var rev3aBody = new JObject();
                rev3aBody.Put("_id", doc.Id);
                rev3aBody.Put("_rev", rev2a.Id);

                // Then, delete rev 2b.
                var rev3bBody = new JObject();
                rev3bBody.Put("_id", doc.Id);
                rev3bBody.Put("_rev", rev2b.Id);
                rev3bBody.Put("_deleted", true);

                // Combine into one _bulk_docs request.
                var requestBody = new JObject();
                var docs = new JArray { rev3aBody, rev3bBody };
                requestBody.Put("docs", docs);

                // Make the _bulk_docs request.
                using (var client = new HttpClient()) {
                    var bulkDocsUrl = remoteDb.RemoteUri + "/_bulk_docs";
                    var request = new HttpRequestMessage(HttpMethod.Post, bulkDocsUrl);
                    //request.Headers.Add("Accept", "*/*");
                    request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

                    var response = client.SendAsync(request).Result;
                    // Check the response to make sure everything worked as it should.
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                    var rawResponse = response.Content.ReadAsStringAsync().Result;
                    var resultArray = Manager.GetObjectMapper().ReadValue<JArray>(rawResponse);
                    Assert.AreEqual(2, resultArray.Count);

                    foreach (var value in resultArray.Values<JObject>()) {
                        var err = (string)value["error"];
                        Assert.IsNull(err);
                    }

                    Thread.Sleep(200);

                    // Pull the remote changes.
                    var puller = database.CreatePullReplication(remoteDb.RemoteUri);
                    RunReplication(puller);
                    Assert.IsNull(puller.LastError);

                    // Make sure the conflict was resolved locally.
                    Assert.AreEqual(1, doc.ConflictingRevisions.Count());
                }
            }
        }

        [Test]
        public void TestSetAndDeleteCookies()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
        public void TestPushReplicationCanMissDocs()
        {
            Assert.Inconclusive("Not sure this is a valid test.");
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            Assert.AreEqual(0, database.LastSequenceNumber);

            var properties1 = new Dictionary<string, object>();
            properties1["doc1"] = "testPushReplicationCanMissDocs";
            CreateDocumentWithProperties(database, properties1);

            var properties2 = new Dictionary<string, object>();
            properties2["doc2"] = "testPushReplicationCanMissDocs";
            var doc2 = CreateDocumentWithProperties(database, properties2);

            var doc2UnsavedRev = doc2.CreateRevision();
            var attachmentStream = GetAsset("attachment.png");
            doc2UnsavedRev.SetAttachment("attachment.png", "image/png", attachmentStream);
            var doc2Rev = doc2UnsavedRev.Save();
            Assert.IsNotNull(doc2Rev);

            var httpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = httpClientFactory;

            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderFakeLocalDocumentUpdate404();

            var json = "{\"error\":\"not_found\",\"reason\":\"missing\"}";
            MockHttpRequestHandler.HttpResponseDelegate bulkDocsResponder = (request) =>
            {
                return MockHttpRequestHandler.GenerateHttpResponseMessage(HttpStatusCode.NotFound, null, json);
            };
            httpHandler.SetResponder("_bulk_docs", bulkDocsResponder);

            MockHttpRequestHandler.HttpResponseDelegate doc2Responder = (request) =>
            {
                var responseObject = new Dictionary<string, object>();
                responseObject["id"] = doc2.Id;
                responseObject["ok"] = true;
                responseObject["rev"] = doc2.CurrentRevisionId;
                return  MockHttpRequestHandler.GenerateHttpResponseMessage(responseObject);
            };
            httpHandler.SetResponder(doc2.Id, doc2Responder);

            var replicationDoneSignal = new CountdownEvent(1);
            var observer = new ReplicationObserver(replicationDoneSignal);
            var pusher = database.CreatePushReplication(GetReplicationURL());
            pusher.Changed += observer.Changed;
            pusher.Start();

            var success = replicationDoneSignal.Wait(TimeSpan.FromSeconds(5));
            Assert.IsTrue(success);

            Assert.IsNotNull(pusher.LastError);

            System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(500));

            var localLastSequence = database.LastSequenceWithCheckpointId(pusher.RemoteCheckpointDocID());

            Log.D(Tag, "dtabase.lastSequenceWithCheckpointId(): " + localLastSequence);
            Log.D(Tag, "doc2.getCUrrentRevision().getSequence(): " + doc2.CurrentRevision.Sequence);

            // Since doc1 failed, the database should _not_ have had its lastSequence bumped to doc2's sequence number.
            // If it did, it's bug: github.com/couchbase/couchbase-lite-java-core/issues/95
            Assert.IsFalse(doc2.CurrentRevision.Sequence.ToString().Equals(localLastSequence));
            Assert.IsNull(localLastSequence);
            Assert.IsTrue(doc2.CurrentRevision.Sequence > 0);
        }

        [Test]
        public void TestCheckServerCompatVersion()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var replicator = database.CreatePushReplication(new Uri("http://fake.test-url.com:4984/fake/"));
            Assert.IsFalse(replicator.CheckServerCompatVersion("0.01"));

            replicator.ServerType = "Couchbase Sync Gateway/1.00";
            Assert.IsTrue(replicator.CheckServerCompatVersion("1.00"));
            Assert.IsFalse(replicator.CheckServerCompatVersion("2.00"));
        }

        [Test]
        public void TestPusherFindCommonAncestor()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
            Assert.AreEqual(0, Pusher.FindCommonAncestor(rev, new List<string>()));
            Assert.AreEqual(0, Pusher.FindCommonAncestor(rev, (new [] {"3-noway", "1-nope"}).ToList()));
            Assert.AreEqual(1, Pusher.FindCommonAncestor(rev, (new [] {"3-noway", "1-first"}).ToList()));
            Assert.AreEqual(2, Pusher.FindCommonAncestor(rev, (new [] {"3-noway", "2-second", "1-first"}).ToList()));
        }

        [Test]
        public void TestPushFilteredByDocId()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
                pusher.DocIds = new List<string> { doc1.GetProperty("_id") as string };

                var doc2 = database.CreateDocument();
                doc2.PutProperties(new Dictionary<string, object> {
                    { "doc2", "Foo" }
                });

                pusher.Start();
                Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Replication timed out");
                Assert.AreEqual(1, changesCount);
            }
        }

        [Test]
        public void TestPushManyNewDocuments()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }
            var numDocs = 100;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Continuous = true;

                var changesCount = 0;
                var completedChangesCount = 0;
                var replicationCaughtUpSignal = new CountdownEvent(1);
                pusher.Changed += (sender, e) =>
                {
                    changesCount = e.ChangesCount;
                    completedChangesCount = e.CompletedChangesCount;
                    var msg = "changes: {0} completed changes: {1}".Fmt(changesCount, completedChangesCount);
                    Log.D(Tag, msg);
                    if (changesCount == completedChangesCount
                    && changesCount == numDocs
                    && replicationCaughtUpSignal.CurrentCount > 0) {
                        replicationCaughtUpSignal.Signal();
                    }
                };
                pusher.Start();

                for (var i = 0; i < numDocs; i++) {
                    var properties = new Dictionary<string, object>() {
                        { "type", "session" },
                        { "id", "session-" + i },
                        { "title", "title" + i },
                        { "speaker", "TBD" },
                        { "time", "2014-10-07T16:20:00" },
                        { "Track", "Case Study" }
                    };

                    var doc = database.CreateDocument();
                    var rev = doc.PutProperties(properties);
                    Assert.IsNotNull(rev);
                }

                Assert.IsTrue(replicationCaughtUpSignal.Wait(TimeSpan.FromSeconds(80)));
                Assert.AreEqual(numDocs, completedChangesCount);

                // Workaroud: sleep to ensure that all of the pending replication tasks 
                // (e.g. saving checkpoint) are all drained to prevent accessing to 
                // the database after the manager is closed during the test tearing 
                // down state.
                Log.V(Tag, "Wait for a few seconds to ensure all pending replication tasks are drained ...");
                StopReplication(pusher);
            }
        }

        [Test]
        public void TestContinuousPushReplicationGoesIdle() 
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            // make sure we are starting empty
            Assert.AreEqual(0, database.LastSequenceNumber);

            // add docs
            var properties1 = new Dictionary<string, object>();
            properties1["doc1"] = "testContinuousPushReplicationGoesIdle";
            CreateDocumentWithProperties(database, properties1);

            var httpClientFactory = new MockHttpClientFactory();
            manager.DefaultHttpClientFactory = httpClientFactory;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var firstPusher = database.CreatePushReplication(remoteDb.RemoteUri);
                firstPusher.Continuous = true;
                var checkpointId = firstPusher.RemoteCheckpointDocID();

                var httpHandler = httpClientFactory.HttpHandler; 
                MockHttpRequestHandler.HttpResponseDelegate localResponder = (request) =>
                {
                    var id = String.Format("_local/{0}", checkpointId);
                    var json = "{" + String.Format("\"id\":\"{0}\",\"ok\":true,\"rev\":\"0-2\"", id) + "}";
                    return MockHttpRequestHandler.GenerateHttpResponseMessage(HttpStatusCode.Created, "OK", json);
                };
                httpHandler.SetResponder("_local", localResponder);

                var replicationIdleSignal = new CountdownEvent(1);
                var replicationIdleObserver = new ReplicationIdleObserver(replicationIdleSignal);
                firstPusher.Changed += replicationIdleObserver.Changed;
                firstPusher.Start();

                var success = replicationIdleSignal.Wait(TimeSpan.FromSeconds(30));
                Assert.IsTrue(success);
                StopReplication(firstPusher);

                // the last sequence should be "1" at this point.  we will use this later
                var lastSequence = database.LastSequenceWithCheckpointId(checkpointId);
                Assert.AreEqual("1", lastSequence);

                // start a second continuous replication
                var secondPusher = database.CreatePushReplication(remoteDb.RemoteUri);
                secondPusher.Continuous = true;
                var secondPusherCheckpointId = secondPusher.RemoteCheckpointDocID();
                Assert.AreEqual(checkpointId, secondPusherCheckpointId);

                // when this goes to fetch the checkpoint, return the last sequence from the previous replication
                localResponder = (request) =>
                {
                    var id = String.Format("_local/{0}", secondPusherCheckpointId);
                    var json = String.Format("{{\"id\":\"{0}\",\"ok\":true,\"rev\":\"0-2\",\"lastSequence\":\"{1}\"}}", id, lastSequence);
                    return MockHttpRequestHandler.GenerateHttpResponseMessage(HttpStatusCode.Created, "OK", json);
                };
                httpHandler.SetResponder("_local", localResponder);

                // start second replication
                replicationIdleSignal = new CountdownEvent(1);
                replicationIdleObserver = new ReplicationIdleObserver(replicationIdleSignal);
                secondPusher.Changed += replicationIdleObserver.Changed;
                secondPusher.Start();

                // wait until we get an IDLE event
                success = replicationIdleSignal.Wait(TimeSpan.FromSeconds(30));
                Assert.IsTrue(success);
                StopReplication(secondPusher);
            }
        }

        /**
        * Verify that running a continuous push replication will emit a change while
        * in an error state when run against a mock server that returns 500 Internal Server
        * errors on every request.
        */
        [Test]
        public void TestContinuousReplicationErrorNotification() {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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

                var success = signal.Wait(TimeSpan.FromSeconds(30));
                Assert.IsTrue(success);

                StopReplication(pusher);
            }
        }

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestContinuousPusherWithAttachment()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var remote = remoteDb.RemoteUri;

                var pusher = database.CreatePushReplication(remote);
                pusher.Continuous = true;
                pusher.Start();

                var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());
                var doc1Id = string.Format("doc1-{0}", docIdTimestamp);

                var document = database.GetDocument(doc1Id);
                var values = new Dictionary<string,object> {
                    { "type" , "attachment_test" },
                };

                document.PutProperties(values);

                long expectedLength = 0;
                document.Update((r) =>
                {
                    var attachmentStream = (InputStream)GetAsset("attachment2.png");
                    var memoryStream = new MemoryStream();
                    attachmentStream.Wrapped.CopyTo(memoryStream);
                    expectedLength = memoryStream.Length;

                    r.SetAttachment("content", "application/octet-stream", memoryStream.ToArray());
                    return true;
                });

                // Make sure it has time to push the document
                Thread.Sleep(5000);

                // make sure the doc has been added
                remoteDb.VerifyDocumentExists(doc1Id);

                Thread.Sleep(2000);
                var json = GetRemoteDocById(remote, doc1Id);

                var attachments = json["_attachments"].AsDictionary<string,object>();
                var content = attachments["content"].AsDictionary<string,object>();
                var lengthAsStr = content["length"];
                var length = Convert.ToInt64(lengthAsStr);
                Assert.AreEqual(expectedLength, length);

                Log.D(Tag, "TestContinuousPusherWithAttachment() finished");

                StopReplication(pusher);
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            // Create a bunch (InboxCapacity * 2) local documents
            var numDocsToSend = Replication.INBOX_CAPACITY * 2;
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

            var httpClientFactory = new MockHttpClientFactory();
            var httpHandler = httpClientFactory.HttpHandler; 
            httpHandler.AddResponderFakeLocalDocumentUpdate404();
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
                Thread.Sleep(500);

                var localLastSequence = database.LastSequenceWithCheckpointId(checkpointId);
                if (expectError) {
                    Assert.Null(localLastSequence);
                }
                else {
                    Assert.IsNotNull(localLastSequence);
                }
            }
        }

        // Failed : https://github.com/couchbase/couchbase-lite-net/issues/320
        [Test]
        public void TestPushReplicationRecoverableError()
        {
            Assert.Inconclusive("Not sure this is a valid test.");
            var statusCode = 503;
            var statusMessage = "Transient Error";
            var expectError = false;
            RunPushReplicationWithTransientError(statusCode, statusMessage, expectError);
        }

        // Failed : https://github.com/couchbase/couchbase-lite-net/issues/320
        [Test]
        public void TestPushReplicationRecoverableIOException() {
            Assert.Inconclusive("Not sure this is a valid test.");
            var statusCode = -1; // code to tell it to throw an IOException
            string statusMessage = null;
            var expectError = false;
            RunPushReplicationWithTransientError(statusCode, statusMessage, expectError);
        }

        [Test]
        public void TestPushReplicationNonRecoverableError()
        {
            Assert.Inconclusive("Not sure this is a valid test.");
            var statusCode = 404;
            var statusMessage = "NOT FOUND";
            var expectError = true;
            RunPushReplicationWithTransientError(statusCode, statusMessage, expectError);
        }

        [Test]
        public void TestPushUpdatedDocWithoutReSendingAttachments() 
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            Assert.AreEqual(0, database.LastSequenceNumber);

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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            Assert.AreEqual(0, database.LastSequenceNumber);

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
            Assert.IsNotNull(rev2);
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
                    if (request.Method == HttpMethod.Put) {
                        var isMultipartContent = (request.Content is MultipartContent);
                        if (count == 0) {
                            Assert.IsTrue(isMultipartContent);
                        }
                        else {
                            Assert.IsFalse(isMultipartContent);
                        }
                        count++;
                    }
                }
            }
        }

        [Test]
        public void TestPushPullDocumentWithAttachment()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
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
                Thread.Sleep(500);

                var db = manager.GetDatabase("tmp");

                var puller = db.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(puller);

                doc = db.GetExistingDocument(docId);
                Assert.IsNotNull(doc);

                attachment = doc.CurrentRevision.GetAttachment("photo");
                Assert.IsNotNull(attachment);
                Assert.AreEqual(attachmentLength, attachment.Length);
                Assert.IsNotNull(attachment.Content);
            }
        }

        [Test, Category("issue348")]
        public void TestConcurrentPushPullAndLiveQueryWithFilledDatabase()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            // Create remote docs.
            const int docsToCreate = 50;

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var docList = remoteDb.AddDocuments(docsToCreate, true);

                // Create local docs
                for (int i = 0; i < docsToCreate; i++) {
                    var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());
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
                    Log.D(Tag, "Puller Changed: {0}/{1}/{2}", puller.Status, puller.ChangesCount, puller.CompletedChangesCount);
                    if (puller.Status != ReplicationStatus.Stopped)
                        return;
                    Log.D(Tag, "Puller Completed Changes after stopped: {0}", puller.CompletedChangesCount);
                };
                int numDocsBeforePull = database.DocumentCount;
                View view = database.GetView("testPullerWithLiveQueryView");
                view.SetMapReduce((document, emitter) =>
                {
                    if (document.Get("_id") != null) {
                        emitter(document.Get("_id"), null);
                    }
                }, null, "1");

                LiveQuery allDocsLiveQuery = view.CreateQuery().ToLiveQuery();
                int numTimesCalled = 0;
                allDocsLiveQuery.Changed += (sender, e) =>
                {
                    if (e.Error != null) {
                        throw new RuntimeException(e.Error);
                    }
                    if (numTimesCalled++ > 0 && e.Rows.Count > 0) {
                        Assert.IsTrue(e.Rows.Count > numDocsBeforePull, "e.Rows.Count ({0}) <= numDocsBeforePull ({1})".Fmt(e.Rows.Count, numDocsBeforePull));
                    }
                    Log.D(Tag, "rows {0} / times called {1}", e.Rows.Count, numTimesCalled);
                    foreach (var row in e.Rows) {
                        if (docList.Contains(row.DocumentId)) {
                            mre.Signal();
                            docList.Remove(row.DocumentId);
                        }
                    }

                    Log.D(Tag, "Remaining docs to be found: {0}", mre.CurrentCount);
                };

                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Start();

                // the first time this is called back, the rows will be empty.
                // but on subsequent times we should expect to get a non empty
                // row set.
                allDocsLiveQuery.Start();
                puller.Start();

                Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(60)), "Replication Timeout");

                StopReplication(pusher);
                StopReplication(puller);
                allDocsLiveQuery.Stop();  
            }
        }

        [Test, Category("issue348")]
        public void TestPushPullAndLiveQueryWithFilledDatabase()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            const int docsToCreate = 50;
            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var docList = remoteDb.AddDocuments(docsToCreate, true);
                var pusher = database.CreatePushReplication(remoteDb.RemoteUri);
                pusher.Start();

                var mre = new CountdownEvent(docsToCreate);
                var puller = database.CreatePullReplication(remoteDb.RemoteUri);
                puller.Changed += (sender, e) =>
                {
                    Log.D(Tag, "Puller Changed: {0}/{1}/{2}", puller.Status, puller.ChangesCount, puller.CompletedChangesCount);
                    if (puller.Status != ReplicationStatus.Stopped)
                        return;
                    Log.D(Tag, "Puller Completed Changes after stopped: {0}", puller.CompletedChangesCount);
                };
                int numDocsBeforePull = database.DocumentCount;
                View view = database.GetView("testPullerWithLiveQueryView");
                view.SetMapReduce((document, emitter) =>
                {
                    if (document.Get("_id") != null) {
                        emitter(document.Get("_id"), null);
                    }
                }, null, "1");

                LiveQuery allDocsLiveQuery = view.CreateQuery().ToLiveQuery();
                int numTimesCalled = 0;
                allDocsLiveQuery.Changed += (sender, e) =>
                {
                    if (e.Error != null) {
                        throw new RuntimeException(e.Error);
                    }
                    if (numTimesCalled++ > 0 && e.Rows.Count > 0) {
                        Assert.IsTrue(e.Rows.Count > numDocsBeforePull, "e.Rows.Count ({0}) <= numDocsBeforePull ({1})".Fmt(e.Rows.Count, numDocsBeforePull));
                    }
                    Log.D(Tag, "rows {0} / times called {1}", e.Rows.Count, numTimesCalled);
                    foreach (var row in e.Rows) {
                        if (docList.Contains(row.DocumentId)) {
                            mre.Signal();
                            docList.Remove(row.DocumentId);
                        }
                    }

                    Log.D(Tag, "Remaining docs to be found: {0}", mre.CurrentCount);
                };

                // the first time this is called back, the rows will be empty.
                // but on subsequent times we should expect to get a non empty
                // row set.
                allDocsLiveQuery.Start();
                puller.Start();

                Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(60)), "Replication Timeout");

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
                var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());
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

                    Log.D("ReplicationTest", "New replication status {0}", e.Source.Status);
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

                docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());
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

                    Log.D("ReplicationTest", "New replication status {0}", e.Source.Status);
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
            var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());
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
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var fakeFactory = new MockHttpClientFactory(false);
            FlowControl flow = new FlowControl(new FlowItem[]
            {
                new FunctionRunner<HttpResponseMessage>(() => {
                    Thread.Sleep(7000);
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
            ManagerOptions.Default.RequestTimeout = TimeSpan.FromSeconds(5);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                CreatePullAndTest(20, remoteDb, (repl) => Assert.AreEqual(20, database.DocumentCount, "Didn't recover from the error"));
            }
        }

        [Test]
        public void TestBulkPullPermanentExceptionSurrender() {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            var fakeFactory = new MockHttpClientFactory(false);
            FlowControl flow = new FlowControl(new FlowItem[]
            {
                new ExceptionThrower(new TaskCanceledException()) { ExecutionCount = -1 },
            });

            fakeFactory.HttpHandler.SetResponder("_bulk_get", (request) => 
                flow.ExecuteNext<HttpResponseMessage>());
            manager.DefaultHttpClientFactory = fakeFactory;
            ManagerOptions.Default.RequestTimeout = TimeSpan.FromSeconds(5);

            using (var remoteDb = _sg.CreateDatabase(TempDbName())) {
                CreatePullAndTest(20, remoteDb, repl => Assert.IsTrue(database.DocumentCount < 20, "Somehow got all the docs"));
            }
        }

        [Test]
        public void TestFailedBulkGetDoesntChangeLastSequence()
        {
            if (!Boolean.Parse((string)Runtime.Properties["replicationTestsEnabled"]))
            {
                Assert.Inconclusive("Replication tests disabled.");
                return;
            }

            string firstBulkGet = null;
            using(var remoteDb = _sg.CreateDatabase(TempDbName())) {
                var fakeFactory = new MockHttpClientFactory(false);
                fakeFactory.HttpHandler.SetResponder("_bulk_get", (request) =>
                {
                    var str = request.Content.ReadAsStringAsync().Result;
                    if (firstBulkGet == null || firstBulkGet.Equals(str)) {
                        Log.D(Tag, "Rejecting this bulk get because it looks like the first batch");
                        firstBulkGet = str;
                        throw new OperationCanceledException();
                    }

                    Log.D(Tag, "Letting this bulk get through");
                    return new RequestCorrectHttpMessage();
                });

                int gotSequence = 0;
                fakeFactory.HttpHandler.SetResponder("doc", request =>
                {
                    Regex r = new Regex("doc[0-9]+");
                    var m = r.Match(request.RequestUri.PathAndQuery);
                    if(m.Success) {
                        var str = m.Captures[0].Value;
                        var converted = Int32.Parse(str.Substring(3)) + 3;
                        if(gotSequence == 0 || converted - gotSequence == 1) {
                            gotSequence = converted;
                        }
                    }

                    return new RequestCorrectHttpMessage();
                });
                
                manager.DefaultHttpClientFactory = fakeFactory;
                Manager.DefaultOptions.MaxRevsToGetInBulk = 10;
                Manager.DefaultOptions.MaxOpenHttpConnections = 8;
                Manager.DefaultOptions.RequestTimeout = TimeSpan.FromSeconds(5);

                CreatePullAndTest((int)(Manager.DefaultOptions.MaxRevsToGetInBulk * 1.5), remoteDb, repl =>
                {
                    Log.D(Tag, "Document count increased to {0} with last sequence '{1}'", database.DocumentCount, repl.LastSequence);
                    Assert.IsTrue(database.DocumentCount > 0, "Didn't get docs from second bulk get batch");
                    Assert.AreEqual(gotSequence, Int32.Parse(repl.LastSequence), "LastSequence was advanced");
                });

                Thread.Sleep(500);
                fakeFactory.HttpHandler.ClearResponders();
                var pull = database.CreatePullReplication(remoteDb.RemoteUri);
                RunReplication(pull);
                Assert.AreEqual(pull.ChangesCount, pull.CompletedChangesCount);
                Assert.AreNotEqual(pull.LastSequence, "0");
            }
        }

    }
}