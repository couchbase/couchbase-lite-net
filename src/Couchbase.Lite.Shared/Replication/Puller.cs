//
// Puller.cs
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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Replicator
{
    internal sealed class Puller : Replication, IChangeTrackerClient
    {

        #region Constants

        internal const int MAX_ATTS_SINCE = 50;
        internal const int CHANGE_TRACKER_RESTART_DELAY_MS = 10000;
        private const string TAG = "Puller";

        #endregion

        #region Variables

        //TODO: Socket change tracker
        //private bool caughtUp;

        private bool _canBulkGet;
        private Batcher<RevisionInternal> _downloadsToInsert;
        private IList<RevisionInternal> _revsToPull;
        private IList<RevisionInternal> _deletedRevsToPull;
        private IList<RevisionInternal> _bulkRevsToPull;
        private ChangeTracker _changeTracker;
        private SequenceMap _pendingSequences;
        private volatile int _httpConnectionCount;
        private readonly object _locker = new object ();
        private List<Task> _pendingBulkDownloads = new List<Task>();

        #endregion

        #region Constructors

        internal Puller(Database db, Uri remote, bool continuous, TaskFactory workExecutor)
            : this(db, remote, continuous, null, workExecutor) { }

        internal Puller(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor) 
            : base(db, remote, continuous, clientFactory, workExecutor) {  }

        #endregion

        #region Private Methods

        private void PauseOrResume()
        {
            var pending = 0;
            if(Batcher != null) {
                pending += Batcher.Count();
            }

            if(_pendingSequences != null) {
                pending += _pendingSequences.Count;
            }

            if(_changeTracker != null) {
                _changeTracker.Paused = pending >= 200;
            }
        }

        private void StartChangeTracker()
        {
            Log.D(TAG, "starting ChangeTracker with since = " + LastSequence);

            var mode = Continuous 
                ? ChangeTrackerMode.LongPoll 
                : ChangeTrackerMode.OneShot;

            _changeTracker = new ChangeTracker(RemoteUrl, mode, LastSequence, true, this, WorkExecutor);
            _changeTracker.Authenticator = Authenticator;
            if(DocIds != null) {
                if(ServerType != null && ServerType.StartsWith("CouchDB")) {
                    _changeTracker.SetDocIDs(DocIds.ToList());
                } else {
                    Log.W(TAG, "DocIds parameter only supported on CouchDB");
                }
            }       

            if (Filter != null) {
                _changeTracker.SetFilterName(Filter);
                if (FilterParams != null) {
                    _changeTracker.SetFilterParams(FilterParams.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
                }
            }

            _changeTracker.UsePost = CheckServerCompatVersion("0.93");
            _changeTracker.Start();
        }


        private void ProcessChangeTrackerStopped(ChangeTracker tracker)
        {
            if (Continuous) {
                if (_stateMachine.State == ReplicationState.Offline) {
                    // in this case, we don't want to do anything here, since
                    // we told the change tracker to go offline ..
                    Log.D(TAG, "Change tracker stopped because we are going offline");
                } else if (_stateMachine.State == ReplicationState.Stopping || _stateMachine.State == ReplicationState.Stopped) {
                    Log.D(TAG, "Change tracker stopped because replicator is stopping or stopped.");
                } else {
                    // otherwise, try to restart the change tracker, since it should
                    // always be running in continuous replications
                    const string msg = "Change tracker stopped during continuous replication";
                    Log.E(TAG, msg);
                    LastError = new Exception(msg);
                    FireTrigger(ReplicationTrigger.WaitingForChanges);
                    Log.D(TAG, "Scheduling change tracker restart in {0} ms", CHANGE_TRACKER_RESTART_DELAY_MS);
                    Task.Delay(CHANGE_TRACKER_RESTART_DELAY_MS).ContinueWith(t =>
                    {
                        // the replication may have been stopped by the time this scheduled fires
                        // so we need to check the state here.
                        if(_stateMachine.IsInState(ReplicationState.Running)) {
                            Log.D(TAG, "Still runing, restarting change tracker");
                            StartChangeTracker();
                        } else {
                            Log.D(TAG, "No longer running, not restarting change tracker");
                        }
                    });
                }
            } else {
                if (LastError == null && tracker.Error != null) {
                    LastError = tracker.Error;
                }

                FireTrigger(ReplicationTrigger.StopGraceful);
            }
        }

        private void FinishStopping()
        {
            StopRemoteRequests();
            lock (_locker) {
                _revsToPull = null;
                _deletedRevsToPull = null;
                _bulkRevsToPull = null;
            }

            if (_downloadsToInsert != null) {
                _downloadsToInsert.FlushAll();
            }

            FireTrigger(ReplicationTrigger.StopImmediate);
        }

        private void ReplicationChanged(object sender, ReplicationChangeEventArgs args)
        {
            if (args.Source.CompletedChangesCount < args.Source.ChangesCount) {
                return;
            }

            Changed -= ReplicationChanged;
            FinishStopping();
        }

        private string JoinQuotedEscaped(IList<string> strings)
        {
            if (strings.Count == 0) {
                return "[]";
            }

            IEnumerable<Byte> json = null;

            try {
                json = Manager.GetObjectMapper().WriteValueAsBytes(strings);
            } catch (Exception e) {
                Log.W(TAG, "Unable to serialize json", e);
            }

            return Uri.EscapeUriString(Runtime.GetStringForBytes(json));
        }

        private void QueueRemoteRevision(RevisionInternal rev)
        {
            if (rev.IsDeleted()) {
                if (_deletedRevsToPull == null) {
                    _deletedRevsToPull = new List<RevisionInternal>(100);
                }

                _deletedRevsToPull.AddItem(rev);
            } else {
                if (_revsToPull == null) {
                    _revsToPull = new List<RevisionInternal>(100);
                }

                _revsToPull.AddItem(rev);
            }
        }

        /// <summary>
        /// Start up some HTTP GETs, within our limit on the maximum simultaneous number
        /// The entire method is not synchronized, only the portion pulling work off the list
        /// Important to not hold the synchronized block while we do network access
        /// </summary>
        private void PullRemoteRevisions()
        {
            //find the work to be done in a synchronized block
            var workToStartNow = new List<RevisionInternal>();
            var bulkWorkToStartNow = new List<RevisionInternal>();
            lock (_locker)
            {
                while (LocalDatabase != null && _httpConnectionCount + bulkWorkToStartNow.Count + workToStartNow.Count < ManagerOptions.Default.MaxOpenHttpConnections)
                {
                    int nBulk = 0;
                    if (_bulkRevsToPull != null) {
                        nBulk = Math.Min(_bulkRevsToPull.Count, ManagerOptions.Default.MaxRevsToGetInBulk);
                    }

                    if (nBulk == 1) {
                        // Rather than pulling a single revision in 'bulk', just pull it normally:
                        QueueRemoteRevision(_bulkRevsToPull[0]);
                        _bulkRevsToPull.Remove(0);
                        nBulk = 0;
                    }

                    if (nBulk > 0) {
                        // Prefer to pull bulk revisions:
                        var range = new Couchbase.Lite.Util.ArraySegment<RevisionInternal>(_bulkRevsToPull.ToArray(), 0, nBulk);
                        bulkWorkToStartNow.AddRange(range);
                        _bulkRevsToPull.RemoveAll(range);
                    } else {
                        // Prefer to pull an existing revision over a deleted one:
                        IList<RevisionInternal> queue = _revsToPull;
                        if (queue == null || queue.Count == 0) {
                            queue = _deletedRevsToPull;
                            if (queue == null || queue.Count == 0) {
                                break; // both queues are empty
                            }
                        }

                        workToStartNow.AddItem(queue[0]);
                        queue.Remove(0);
                    }
                }
            }

            //actually run it outside the synchronized block
            if (bulkWorkToStartNow.Count > 0) {
                PullBulkRevisions(bulkWorkToStartNow);
            }

            foreach (var rev in workToStartNow) {
                PullRemoteRevision(rev);
            }
        }

        // Get a bunch of revisions in one bulk request. Will use _bulk_get if possible.
        private void PullBulkRevisions(IList<RevisionInternal> bulkRevs)
        {
            var nRevs = bulkRevs == null ? 0 : bulkRevs.Count;
            if (nRevs == 0) {
                return;
            }

            Log.D(TAG, "{0} bulk-fetching {1} remote revisions...", this, nRevs);
            Log.V(TAG, "{0} bulk-fetching remote revisions: {1}", this, bulkRevs);

            if (!_canBulkGet) {
                PullBulkWithAllDocs(bulkRevs);
                return;
            }

            Log.V(TAG, "POST _bulk_get");
            var remainingRevs = new List<RevisionInternal>(bulkRevs);
            ++_httpConnectionCount;
            BulkDownloader dl;
            try
            {
                dl = new BulkDownloader(WorkExecutor, ClientFactory, RemoteUrl, bulkRevs, LocalDatabase, RequestHeaders);
                dl.DocumentDownloaded += (sender, args) =>
                {
                    var props = args.DocumentProperties;

                    var rev = props.Get ("_id") != null 
                        ? new RevisionInternal (props) 
                        : new RevisionInternal (props.GetCast<string> ("id"), props.GetCast<string> ("rev"), false);


                    var pos = remainingRevs.IndexOf(rev);
                    if (pos > -1) {
                        rev.SetSequence(remainingRevs[pos].GetSequence());
                        remainingRevs.RemoveAt(pos);
                    } else {
                        Log.W(TAG, "Received unexpected rev {0}; ignoring", rev);
                        return;
                    }

                    if (props.GetCast<string>("_id") != null) {
                        // Add to batcher ... eventually it will be fed to -insertRevisions:.
                        QueueDownloadedRevision(rev);
                    } else {
                        var status = StatusFromBulkDocsResponseItem(props);
                        LastError = new CouchbaseLiteException(status.Code);
                        RevisionFailed();
                        SafeIncrementCompletedChangesCount();
                    }
                };

                dl.Complete += (sender, args) => 
                {
                    if (args != null && args.Error != null) {
                        RevisionFailed();
                        if(remainingRevs.Count == 0) {
                            LastError = args.Error;
                        }

                    } else if(remainingRevs.Count > 0) {
                        Log.W(TAG, "{0} revs not returned from _bulk_get: {1}",
                            remainingRevs.Count, remainingRevs);
                        for(int i = 0; i < remainingRevs.Count; i++) {
                            var rev = remainingRevs[i];
                            if(ShouldRetryDownload(rev.GetDocId())) {
                                _bulkRevsToPull.Add(remainingRevs[i]);
                            } else {
                                LastError = args.Error;
                                SafeIncrementCompletedChangesCount();
                            }
                        }
                    }

                    SafeAddToCompletedChangesCount(remainingRevs.Count);

                    --_httpConnectionCount;

                    PullRemoteRevisions();
                };
            } catch (Exception) {
                return;
            }

            dl.Authenticator = Authenticator;
            var t = WorkExecutor.StartNew(dl.Run, CancellationTokenSource.Token, TaskCreationOptions.LongRunning, WorkExecutor.Scheduler);
            t.ConfigureAwait(false).GetAwaiter().OnCompleted(() => _pendingBulkDownloads.Remove(t));
            _pendingBulkDownloads.Add(t);
        }

        private bool ShouldRetryDownload(string docId)
        {
            var localDoc = LocalDatabase.GetExistingLocalDocument(docId);
            if (localDoc == null)
            {
                LocalDatabase.PutLocalDocument(docId, new Dictionary<string, object>
                {
                    {"retryCount", 1}
                });
                return true;
            }

            var retryCount = (long)localDoc["retryCount"];
            if (retryCount >= ManagerOptions.Default.MaxRetries)
            {
                PruneFailedDownload(docId);
                return false;
            }

            localDoc["retryCount"] = (long)localDoc["retryCount"] + 1;
            LocalDatabase.PutLocalDocument(docId, localDoc);
            return true;
        }

        private void PruneFailedDownload(string docId)
        {
            LocalDatabase.DeleteLocalDocument(docId);
        }

        // This invokes the tranformation block if one is installed and queues the resulting Revision
        private void QueueDownloadedRevision(RevisionInternal rev)
        {
            if (RevisionBodyTransformationFunction != null)
            {
                // Add 'file' properties to attachments pointing to their bodies:
                foreach (var entry in rev.GetProperties().Get("_attachments").AsDictionary<string,object>())
                {
                    var attachment = entry.Value as IDictionary<string, object>;
                    attachment.Remove("file");

                    if (attachment.Get("follows") != null && attachment.Get("data") == null)
                    {
                        var filePath = LocalDatabase.FileForAttachmentDict(attachment).AbsolutePath;
                        if (filePath != null)
                        {
                            attachment["file"] = filePath;
                        }
                    }
                }
                var xformed = TransformRevision(rev);
                if (xformed == null)
                {
                    Log.V(TAG, "Transformer rejected revision {0}", rev);
                    _pendingSequences.RemoveSequence(rev.GetSequence());
                    LastSequence = _pendingSequences.GetCheckpointedValue();
                    PauseOrResume();
                    return;
                }

                rev = xformed;

                var attachments = (IDictionary<string, IDictionary<string, object>>)rev.GetProperties().Get("_attachments");
                foreach (var entry in attachments.EntrySet())
                {
                    var attachment = entry.Value;
                    attachment.Remove("file");
                }
            }

            //TODO: rev.getBody().compact();

            if (_downloadsToInsert != null) {
                _downloadsToInsert.QueueObject(rev);
            }
            else {
                Log.I(TAG, "downloadsToInsert is null");
            }
        }

        // Get as many revisions as possible in one _all_docs request.
        // This is compatible with CouchDB, but it only works for revs of generation 1 without attachments.
        private void PullBulkWithAllDocs(IList<RevisionInternal> bulkRevs)
        {
            // http://wiki.apache.org/couchdb/HTTP_Bulk_Document_API
            ++_httpConnectionCount;

            var remainingRevs = new List<RevisionInternal>(bulkRevs);
            var keys = bulkRevs.Select(rev => rev.GetDocId()).ToArray();
            var body = new Dictionary<string, object>();
            body.Put("keys", keys);

            SendAsyncRequest(HttpMethod.Post, "/_all_docs?include_docs=true", body, (result, e) =>
            {
                var res = result.AsDictionary<string, object>();
                if (e != null) {
                    LastError = e;
                    RevisionFailed();
                    SafeAddToCompletedChangesCount(bulkRevs.Count);
                } else {
                    // Process the resulting rows' documents.
                    // We only add a document if it doesn't have attachments, and if its
                    // revID matches the one we asked for.
                    var rows = res.Get ("rows").AsList<IDictionary<string, object>>();
                    Log.V (TAG, "Checking {0} bulk-fetched remote revisions", rows.Count);

                    foreach (var row in rows) {
                        var doc = row.Get ("doc").AsDictionary<string, object>();
                        if (doc != null && doc.Get ("_attachments") == null)
                        {
                            var rev = new RevisionInternal (doc);
                            var pos = remainingRevs.IndexOf (rev);
                            if (pos > -1) 
                            {
                                rev.SetSequence(remainingRevs[pos].GetSequence());
                                remainingRevs.Remove (pos);
                                QueueDownloadedRevision (rev);
                            }
                        }
                    }
                }

                // Any leftover revisions that didn't get matched will be fetched individually:
                if (remainingRevs.Count > 0) 
                {
                    Log.V (TAG, "Bulk-fetch didn't work for {0} of {1} revs; getting individually", remainingRevs.Count, bulkRevs.Count);
                    foreach (var rev in remainingRevs) 
                    {
                        QueueRemoteRevision (rev);
                    }
                    PullRemoteRevisions ();
                } 

                --_httpConnectionCount;

                // Start another task if there are still revisions waiting to be pulled:
                PullRemoteRevisions();
            });
        }

        /// <summary>Fetches the contents of a revision from the remote db, including its parent revision ID.
        ///     </summary>
        /// <remarks>
        /// Fetches the contents of a revision from the remote db, including its parent revision ID.
        /// The contents are stored into rev.properties.
        /// </remarks>
        private void PullRemoteRevision(RevisionInternal rev)
        {
            Log.D(TAG, "PullRemoteRevision with rev: {0}", rev);
            _httpConnectionCount++;

            // Construct a query. We want the revision history, and the bodies of attachments that have
            // been added since the latest revisions we have locally.
            // See: http://wiki.apache.org/couchdb/HTTP_Document_API#Getting_Attachments_With_a_Document
            var path = new StringBuilder("/" + Uri.EscapeUriString(rev.GetDocId()) + "?rev=" + Uri.EscapeUriString(rev.GetRevId()) + "&revs=true&attachments=true");
            var tmp = LocalDatabase.GetPossibleAncestors(rev, MAX_ATTS_SINCE, true);
            var knownRevs = tmp == null ? null : tmp.ToList();
            if (knownRevs == null) {
                //this means something is wrong, possibly the replicator has shut down
                _httpConnectionCount--;
                return;
            }

            if (knownRevs.Count > 0) {
                path.Append("&atts_since=");
                path.Append(JoinQuotedEscaped(knownRevs));
            }

            //create a final version of this variable for the log statement inside
            //FIXME find a way to avoid this
            var pathInside = path.ToString();
            SendAsyncMultipartDownloaderRequest(HttpMethod.Get, pathInside, null, LocalDatabase, (result, e) => 
            {
                // OK, now we've got the response revision:
                Log.D (TAG, "PullRemoteRevision got response for rev: " + rev);

                if (e != null) {
                    Log.E (TAG, "Error pulling remote revision", e);
                    LastError = e;
                    RevisionFailed();
                    Log.D(TAG, "PullRemoteRevision updating completedChangesCount from " + 
                        CompletedChangesCount + " -> " + (CompletedChangesCount + 1) 
                        + " due to error pulling remote revision");
                    SafeIncrementCompletedChangesCount();
                } else {
                    var properties = result.AsDictionary<string, object>();
                    var gotRev = new PulledRevision(properties);
                    gotRev.SetSequence(rev.GetSequence());
                    Log.D(TAG, "PullRemoteRevision add rev: " + gotRev + " to batcher");

                    if (_downloadsToInsert != null) {
                        _downloadsToInsert.QueueObject(gotRev);
                    } else {
                        Log.E (TAG, "downloadsToInsert is null");
                    }
                }

                // Note that we've finished this task; then start another one if there
                // are still revisions waiting to be pulled:
                --_httpConnectionCount;
                PullRemoteRevisions ();
            });
        }

        /// <summary>This will be called when _revsToInsert fills up:</summary>
        private void InsertDownloads(IList<RevisionInternal> downloads)
        {
            Log.V(TAG, "Inserting {0} revisions...", downloads.Count);
            var time = DateTime.UtcNow;
            downloads.Sort(new RevisionComparer());

            if (LocalDatabase == null) {
                return;
            }

            try {
                var success = LocalDatabase.RunInTransaction(() =>
                {
                    foreach (var rev in downloads) {
                        var fakeSequence = rev.GetSequence();
                        rev.SetSequence(0L);
                        var history = Database.ParseCouchDBRevisionHistory(rev.GetProperties());
                        if (history.Count == 0 && rev.GetGeneration() > 1) {
                            Log.W(TAG, "Missing revision history in response for: {0}", rev);
                            LastError = new CouchbaseLiteException(StatusCode.UpStreamError);
                            RevisionFailed();
                            continue;
                        }

                        Log.V(TAG, String.Format("Inserting {0} {1}", rev.GetDocId(), Manager.GetObjectMapper().WriteValueAsString(history)));

                        // Insert the revision:
                        try {
                            LocalDatabase.ForceInsert(rev, history, RemoteUrl);
                        } catch (CouchbaseLiteException e) {
                            if (e.Code == StatusCode.Forbidden) {
                                Log.I(TAG, "Remote rev failed validation: " + rev);
                            } else if(e.Code == StatusCode.DbBusy) {
                                // abort transaction; RunInTransaction will retry
                                return false;
                            } else {
                                Log.W(TAG, " failed to write {0}: status={1}", rev, e.Code);
                                RevisionFailed();
                                LastError = e;
                                continue;
                            }
                        } catch (Exception e) {
                            Log.E(TAG, "Exception inserting downloads.", e);
                            throw;
                        }

                        _pendingSequences.RemoveSequence(fakeSequence);
                    }

                    Log.D(TAG, " Finished inserting " + downloads.Count + " revisions");

                    return true;
                });

                Log.V(TAG, "Finished inserting {0} revisions. Success == {1}", downloads.Count, success);
            } catch (Exception e) {
                Log.E(TAG, "Exception inserting revisions", e);
            }

            // Checkpoint:
            LastSequence = _pendingSequences.GetCheckpointedValue();

            var delta = (DateTime.UtcNow - time).TotalMilliseconds;
            Log.D(TAG, "Inserted {0} revs in {1} milliseconds", downloads.Count, delta);
            var newCompletedChangesCount = CompletedChangesCount + downloads.Count;
            Log.D(TAG, "InsertDownloads() updating CompletedChangesCount from {0} -> {1}", CompletedChangesCount, newCompletedChangesCount);
            SafeAddToCompletedChangesCount(downloads.Count);
            PauseOrResume();
        }

        #endregion

        #region Overrides

        public override bool CreateTarget { get { return false; } set { return; /* No-op intended. Only used in Pusher. */ } }

        public override bool IsPull { get { return true; } }

        public override IEnumerable<string> DocIds { get; set; }

        public override IDictionary<string, string> Headers 
        {
            get { return clientFactory.Headers; } 
            set { clientFactory.Headers = value; } 
        }

        protected override void StopGraceful()
        {
            var changeTrackerCopy = _changeTracker;
            if (changeTrackerCopy != null) {
                Log.D(TAG, "stopping changetracker " + _changeTracker);

                changeTrackerCopy.SetClient(null);
                // stop it from calling my changeTrackerStopped()
                changeTrackerCopy.Stop();
                _changeTracker = null;
            }

            base.StopGraceful();

            if (CompletedChangesCount == ChangesCount) {
                FinishStopping();
            } else {
                Changed += ReplicationChanged;
            }
        }

        protected override void PerformGoOffline()
        {
            base.PerformGoOffline();
            if (_changeTracker != null) {
                _changeTracker.Stop();
            }

            StopRemoteRequests();
        }

        protected override void PerformGoOnline()
        {
            base.PerformGoOnline();

            BeginReplicating();
        }

        internal override void ProcessInbox(RevisionList inbox)
        {
            if (Status == ReplicationStatus.Offline) {
                Log.D(TAG, "Offline, so skipping inbox process");
                return;
            }

            Debug.Assert(inbox != null);
            if (!_canBulkGet) {
                _canBulkGet = CheckServerCompatVersion("0.81");
            }

            // Ask the local database which of the revs are not known to it:
            var lastInboxSequence = ((PulledRevision)inbox[inbox.Count - 1]).GetRemoteSequenceID();
            var numRevisionsRemoved = 0;
            try {
                // findMissingRevisions is the local equivalent of _revs_diff. it looks at the
                // array of revisions in inbox and removes the ones that already exist. So whatever's left in inbox
                // afterwards are the revisions that need to be downloaded.
                numRevisionsRemoved = LocalDatabase.FindMissingRevisions(inbox);
            } catch (Exception e) {
                Log.W(TAG, "Failed to look up local revs", e);
                inbox = null;
            }

            var inboxCount = 0;
            if (inbox != null) {
                inboxCount = inbox.Count;
            }

            if (numRevisionsRemoved > 0)
            {
                // Some of the revisions originally in the inbox aren't missing; treat those as processed:
                SafeAddToCompletedChangesCount(numRevisionsRemoved);
            }

            if (inboxCount == 0) {
                // Nothing to do. Just bump the lastSequence.
                Log.V(TAG, string.Format("{0} no new remote revisions to fetch", this));

                var seq = _pendingSequences.AddValue(lastInboxSequence);
                _pendingSequences.RemoveSequence(seq);
                LastSequence = _pendingSequences.GetCheckpointedValue();
                PauseOrResume();
                return;
            }

            Log.V(TAG, "Queuing {0} remote revisions...", inboxCount);

            // Dump the revs into the queue of revs to pull from the remote db:
            lock (_locker) {
                int numBulked = 0;
                for (int i = 0; i < inboxCount; i++) {
                    var rev = (PulledRevision)inbox[i];
                    //TODO: add support for rev isConflicted
                    if (_canBulkGet || (rev.GetGeneration() == 1 && !rev.IsDeleted() && !rev.IsConflicted)) {
                        //optimistically pull 1st-gen revs in bulk
                        if (_bulkRevsToPull == null) {
                            _bulkRevsToPull = new List<RevisionInternal>(100);
                        }

                        _bulkRevsToPull.AddItem(rev);
                        ++numBulked;
                    } else {
                        QueueRemoteRevision(rev);
                    }
                    rev.SetSequence(_pendingSequences.AddValue(rev.GetRemoteSequenceID()));
                }

                Log.D(TAG, "Queued {0} remote revisions from seq={1} ({2} in bulk, {3} individually)", inboxCount, 
                    ((PulledRevision)inbox[0]).GetRemoteSequenceID(), numBulked, inboxCount - numBulked);
            }

            PullRemoteRevisions();
            PauseOrResume();
        }

        internal override void BeginReplicating()
        {
            Log.D(TAG, string.Format("Using MaxOpenHttpConnections({0}), MaxRevsToGetInBulk({1})", 
                ManagerOptions.Default.MaxOpenHttpConnections, ManagerOptions.Default.MaxRevsToGetInBulk));

            if (_downloadsToInsert == null) {
                const int capacity = 200;
                const int delay = 1000;
                _downloadsToInsert = new Batcher<RevisionInternal>(WorkExecutor, capacity, delay, InsertDownloads);
            }

            if (_pendingSequences == null) {
                _pendingSequences = new SequenceMap();
                if (LastSequence != null) {
                    // Prime _pendingSequences so its checkpointedValue will reflect the last known seq:
                    var seq = _pendingSequences.AddValue(LastSequence);
                    _pendingSequences.RemoveSequence(seq);
                    Debug.Assert((_pendingSequences.GetCheckpointedValue().Equals(LastSequence)));
                }
            }

            StartChangeTracker();
        }

        internal override void Stopping()
        {
            _downloadsToInsert = null;
            base.Stopping();
        }

        #endregion

        #region IChangeTrackerClient

        public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
        {
            var lastSequence = change.Get("seq").ToString();
            var docID = (string)change.Get("id");
            if (docID == null) {
                return;
            }

            if (!LocalDatabase.IsValidDocumentId(docID)) {
                if (!docID.StartsWith("_user/", StringComparison.InvariantCultureIgnoreCase)) {
                    Log.W(TAG, string.Format("{0}: Received invalid doc ID from _changes: {1} ({2})", this, docID, Manager.GetObjectMapper().WriteValueAsString(change)));
                }

                return;
            }

            var deleted = change.GetCast<bool>("deleted");
            var changes = change.Get("changes").AsList<object>();
            SafeAddToChangesCount(changes.Count);

            foreach (var changeObj in changes) {
                var changeDict = changeObj.AsDictionary<string, object>();
                var revID = changeDict.GetCast<string>("rev");
                if (revID == null) {
                    continue;
                }

                var rev = new PulledRevision(docID, revID, deleted, LocalDatabase);
                rev.SetRemoteSequenceID(lastSequence);
                if (changes.Count > 1) {
                    rev.IsConflicted = true;
                }

                Log.D(TAG, "Adding rev to inbox " + rev);
                AddToInbox(rev);
            }

            PauseOrResume();

            while (_revsToPull != null && _revsToPull.Count > 1000) {
                try {
                    // Presumably we are letting 1 or more other threads do something while we wait.
                    Thread.Sleep(500);
                }
                catch (Exception e) {
                    Log.W(TAG, "Swalling exception while sleeping after receiving changetracker changes.", e);
                    // swallow
                }
            }
        }

        public void ChangeTrackerStopped(ChangeTracker tracker)
        {
            WorkExecutor.StartNew(() => ProcessChangeTrackerStopped(tracker));
        }

        public HttpClient GetHttpClient(bool longPoll)
        {
            var client = ClientFactory.GetHttpClient(longPoll);
            var challengeResponseAuth = Authenticator as IChallengeResponseAuthenticator;
            if (challengeResponseAuth != null) {
                var authHandler = ClientFactory.Handler as DefaultAuthHandler;
                if (authHandler != null) {
                    authHandler.Authenticator = challengeResponseAuth;
                }
            }

            return client;
        }

        #endregion

        #region Nested Classes

        private sealed class RevisionComparer : IComparer<RevisionInternal>
        {
            public RevisionComparer() { }

            public int Compare(RevisionInternal reva, RevisionInternal revb)
            {
                return Misc.TDSequenceCompare(reva != null ? reva.GetSequence() : -1L, revb != null ? revb.GetSequence() : -1L);
            }
        }


        #endregion
    }

}