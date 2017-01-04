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
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Couchbase.Lite.Revisions;


#if !NET_3_5
using System.Net;
using StringEx = System.String;
#else
using System.Net.Couchbase;
#endif

namespace Couchbase.Lite.Replicator
{
    internal sealed class Puller : Replication, IChangeTrackerClient
    {

        #region Constants

        // Maximum number of revision IDs to pass in an "?atts_since=" query param
        internal const int MaxAttsSince = 10;
        internal const int CHANGE_TRACKER_RESTART_DELAY_MS = 10000;
        private const string TAG = "Puller";

        #endregion

        #region Variables

        private bool _caughtUp;
        private bool _canBulkGet;
        private Batcher<RevisionInternal> _downloadsToInsert;
        private IList<RevisionInternal> _revsToPull;
        private IList<RevisionInternal> _deletedRevsToPull;
        private IList<RevisionInternal> _bulkRevsToPull;
        private ChangeTracker _changeTracker;
        private SequenceMap _pendingSequences;
        private readonly object _locker = new object ();

        #endregion

        #region Properties

        protected override bool IsSafeToStop
        {
            get
            {
                return (Batcher == null || Batcher.Count() == 0) && (Continuous || _changeTracker != null && !_changeTracker.IsRunning);
            }
        }

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
#if __IOS__ || __ANDROID__ || UNITY
                _changeTracker.Paused = pending >= 200;
#else
                _changeTracker.Paused = pending >= 2000;
#endif
            }
        }

        private void StartChangeTracker()
        {
            var mode = ChangeTrackerMode.OneShot;
            var pollInterval = ReplicationOptions.PollInterval;
            if (Continuous && pollInterval == TimeSpan.Zero && ReplicationOptions.UseWebSocket) {
                mode = ChangeTrackerMode.WebSocket;
            }

            _canBulkGet = mode == ChangeTrackerMode.WebSocket;
            Log.To.Sync.V(TAG, "{0} starting ChangeTracker: mode={0} since={1}", this, mode, LastSequence);
            var initialSync = LocalDatabase.IsOpen && LocalDatabase.GetDocumentCount() == 0;
            var changeTrackerOptions = new ChangeTrackerOptions {
                DatabaseUri = RemoteUrl,
                Mode = mode,
                IncludeConflicts = true,
                LastSequenceID = LastSequence,
                Client = this,
                RemoteSession = _remoteSession,
                RetryStrategy = ReplicationOptions.RetryStrategy,
                WorkExecutor = WorkExecutor,
                UsePost = CheckServerCompatVersion("0.9.3")
            };
            _changeTracker = ChangeTrackerFactory.Create(changeTrackerOptions);
            _changeTracker.ActiveOnly = initialSync;
            _changeTracker.Continuous = Continuous;
            _changeTracker.PollInterval = pollInterval;
            _changeTracker.Heartbeat = ReplicationOptions.Heartbeat;
            if(DocIds != null) {
                if(ServerType != null && ServerType.Name == "CouchDB") {
                    _changeTracker.DocIDs = DocIds.ToList();
                } else {
                    Log.To.Sync.W(TAG, "DocIds parameter only supported on CouchDB");
                }
            }       

            if (Filter != null) {
                _changeTracker.FilterName = Filter;
                if (FilterParams != null) {
                    _changeTracker.FilterParameters = FilterParams.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
            }

            _changeTracker.Start();
        }


        private void ProcessChangeTrackerStopped(ChangeTracker tracker, ErrorResolution resolution)
        {
            var webSocketTracker = tracker as WebSocketChangeTracker;
            if (webSocketTracker != null && !webSocketTracker.CanConnect) {
                ReplicationOptions.UseWebSocket = false;
                _canBulkGet = false;
                Log.To.Sync.I(TAG, "Server doesn't support web socket changes feed, switching " +
                "to regular HTTP");
                StartChangeTracker();
                return;
            }

            Log.To.Sync.I(TAG, "Change tracker for {0} stopped; error={1}", ReplicatorID, tracker.Error);
            if (LastError == null && tracker.Error != null) {
                LastError = tracker.Error;
            }

            Batcher.FlushAll();
            if(resolution == ErrorResolution.RetryLater) {
                Log.To.Sync.I(TAG, "Change tracked stopped, entering retry loop...");
                ScheduleRetryIfReady();
            } else if(resolution == ErrorResolution.GoOffline) {
                GoOffline();
            } else if(resolution == ErrorResolution.Stop) {
                if(Continuous || (ChangesCount == CompletedChangesCount && IsSafeToStop)) {
                    Log.To.Sync.V(TAG, "Change tracker stopped, firing StopGraceful...");
                    FireTrigger(ReplicationTrigger.StopGraceful);
                }
            }
        }

        private string JoinQuotedEscaped(IList<string> strings)
        {
            if (strings.Count == 0) {
                return "[]";
            }

            string json = null;

            try {
                json = Manager.GetObjectMapper().WriteValueAsString(strings);
            } catch (Exception e) {
                Log.To.Sync.E(TAG, "Unable to serialize json, returning null", e);
                return null;
            }

            return Uri.EscapeUriString(json);
        }

        private void QueueRemoteRevision(RevisionInternal rev)
        {
            if (rev.Deleted) {
                if (_deletedRevsToPull == null) {
                    _deletedRevsToPull = new List<RevisionInternal>(100);
                }

                _deletedRevsToPull.Add(rev);
            } else {
                if (_revsToPull == null) {
                    _revsToPull = new List<RevisionInternal>(100);
                }

                _revsToPull.Add(rev);
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
                while (LocalDatabase.IsOpen)
                {
                    int nBulk = 0;
                    if (_bulkRevsToPull != null) {
                        nBulk = Math.Min(_bulkRevsToPull.Count, ReplicationOptions.MaxRevsToGetInBulk);
                    }

                    if (nBulk == 1) {
                        // Rather than pulling a single revision in 'bulk', just pull it normally:
                        QueueRemoteRevision(_bulkRevsToPull[0]);
                        _bulkRevsToPull.RemoveAt(0);
                        nBulk = 0;
                    }

                    if (nBulk > 0) {
                        // Prefer to pull bulk revisions:
                        var range = new Couchbase.Lite.Util.ArraySegment<RevisionInternal>(_bulkRevsToPull.ToArray(), 0, nBulk);
                        bulkWorkToStartNow.AddRange(range);
                        foreach (var val in range) {
                            _bulkRevsToPull.Remove(val);
                        }
                    } else {
                        // Prefer to pull an existing revision over a deleted one:
                        IList<RevisionInternal> queue = _revsToPull;
                        if (queue == null || queue.Count == 0) {
                            queue = _deletedRevsToPull;
                            if (queue == null || queue.Count == 0) {
                                break; // both queues are empty
                            }
                        }

                        workToStartNow.Add(queue[0]);
                        queue.RemoveAt(0);
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

            Log.To.Sync.I(TAG, "{0} bulk-fetching {1} remote revisions...", ReplicatorID, nRevs);
            if(!_canBulkGet) {
                PullBulkWithAllDocs(bulkRevs);
                return;
            }

            Log.To.SyncPerf.I(TAG, "{0} bulk-getting {1} remote revisions...", ReplicatorID, nRevs);
            var remainingRevs = new List<RevisionInternal>(bulkRevs);
            BulkDownloader dl = new BulkDownloader(new BulkDownloaderOptions {
                Session = _remoteSession,
                DatabaseUri = RemoteUrl,
                Revisions = bulkRevs,
                Database = LocalDatabase,
                RetryStrategy = ReplicationOptions.RetryStrategy,
                CookieStore = CookieContainer
            });

            dl.DocumentDownloaded += (sender, args) =>
            {
                var props = args.DocumentProperties;

                var rev = props.CblID() != null
                    ? new RevisionInternal(props)
                    : new RevisionInternal(props.GetCast<string>("id"), props.GetCast<string>("rev").AsRevID(), false);


                var pos = remainingRevs.IndexOf(rev);
                if(pos > -1) {
                    rev.Sequence = remainingRevs[pos].Sequence;
                    remainingRevs.RemoveAt(pos);
                } else {
                    Log.To.Sync.W(TAG, "Received unexpected rev {0}; ignoring", rev);
                    return;
                }

                if(props.CblID() != null) {
                        // Add to batcher ... eventually it will be fed to -insertRevisions:.
                        QueueDownloadedRevision(rev);
                } else {
                    var status = StatusFromBulkDocsResponseItem(props);
                    Log.To.Sync.W(TAG, "Error downloading {0}", rev);
                    var error = new CouchbaseLiteException(status.Code);
                    LastError = error;
                    RevisionFailed();
                    SafeIncrementCompletedChangesCount();
                    if(IsDocumentError(error)) {
                        Log.To.Sync.W(TAG, $"Error is permanent, {rev} will NOT be downloaded!");
                        _pendingSequences.RemoveSequence(rev.Sequence);
                    } else {
                        Log.To.Sync.I(TAG, $"Will try again later to get {rev}");
                    }
                }
            };

            dl.Complete += (sender, args) =>
            {
                if(args != null && args.Error != null) {
                    RevisionFailed();
                    if(remainingRevs.Count == 0) {
                        LastError = args.Error;
                    }

                } else if(remainingRevs.Count > 0) {
                    Log.To.Sync.W(TAG, "{0} revs not returned from _bulk_get: {1}",
                        remainingRevs.Count, remainingRevs);
                    for(int i = 0; i < remainingRevs.Count; i++) {
                        var rev = remainingRevs[i];
                        if(ShouldRetryDownload(rev.DocID)) {
                            _bulkRevsToPull.Add(remainingRevs[i]);
                        } else {
                            LastError = args.Error;
                            SafeIncrementCompletedChangesCount();
                        }
                    }
                }

                SafeAddToCompletedChangesCount(remainingRevs.Count);
                LastSequence = _pendingSequences.GetCheckpointedValue();

                PullRemoteRevisions();
            };

            dl.Authenticator = Authenticator;
            WorkExecutor.StartNew(dl.Start, CancellationTokenSource.Token, TaskCreationOptions.None, WorkExecutor.Scheduler);
        }

        // Get as many revisions as possible in one _all_docs request.
        // This is compatible with CouchDB, but it only works for revs of generation 1 without attachments.
        private void PullBulkWithAllDocs(IList<RevisionInternal> bulkRevs)
        {
            // http://wiki.apache.org/couchdb/HTTP_Bulk_Document_API

            var remainingRevs = new List<RevisionInternal>(bulkRevs);
            var keys = bulkRevs.Select(rev => rev.DocID).ToArray();
            var body = new Dictionary<string, object>();
            body["keys"] = keys;

            _remoteSession.SendAsyncRequest(HttpMethod.Post, "/_all_docs?include_docs=true", body, (result, e) =>
            {
                var res = result.AsDictionary<string, object>();
                if(e != null) {
                    LastError = e;
                    RevisionFailed();
                    SafeAddToCompletedChangesCount(bulkRevs.Count);
                } else {
                    // Process the resulting rows' documents.
                    // We only add a document if it doesn't have attachments, and if its
                    // revID matches the one we asked for.
                    var rows = res.Get("rows").AsList<IDictionary<string, object>>();
                    Log.To.Sync.I(TAG, "{0} checking {1} bulk-fetched remote revisions", ReplicatorID, rows.Count);

                    foreach(var row in rows) {
                        var doc = row.Get("doc").AsDictionary<string, object>();
                        if(doc != null && doc.Get("_attachments") == null) {
                            var rev = new RevisionInternal(doc);
                            var pos = remainingRevs.IndexOf(rev);
                            if(pos > -1) {
                                rev.Sequence = remainingRevs[pos].Sequence;
                                remainingRevs.RemoveAt(pos);
                                QueueDownloadedRevision(rev);
                            }
                        }
                    }
                }

                // Any leftover revisions that didn't get matched will be fetched individually:
                if(remainingRevs.Count > 0) {
                    Log.To.Sync.I(TAG, "Bulk-fetch didn't work for {0} of {1} revs; getting individually for {2}", 
                        remainingRevs.Count, bulkRevs.Count, ReplicatorID);
                    foreach(var rev in remainingRevs) {
                        QueueRemoteRevision(rev);
                    }
                    PullRemoteRevisions();
                }

                // Start another task if there are still revisions waiting to be pulled:
                PullRemoteRevisions();
            });
        }


        private bool ShouldRetryDownload(string docId)
        {
            if (!LocalDatabase.IsOpen) {
                return false;
            }

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
            if (retryCount >= ReplicationOptions.RetryStrategy.MaxRetries)
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
            if (RevisionBodyTransformationFunction != null) {
                // Add 'file' properties to attachments pointing to their bodies:
                foreach (var entry in rev.GetProperties().Get("_attachments").AsDictionary<string,object>()) {
                    var attachment = entry.Value as IDictionary<string, object>;
                    attachment.Remove("file");

                    if (attachment.Get("follows") != null && attachment.Get("data") == null) {
                        var filePath = LocalDatabase.FileForAttachmentDict(attachment).AbsolutePath;
                        if (filePath != null) {
                            attachment["file"] = filePath;
                        }
                    }
                }

                var xformed = TransformRevision(rev);
                if (xformed == null) {
                    Log.To.Sync.I(TAG, "Transformer rejected revision {0}", rev);
                    _pendingSequences.RemoveSequence(rev.Sequence);
                    LastSequence = _pendingSequences.GetCheckpointedValue();
                    PauseOrResume();
                    return;
                }

                rev = xformed;

                var attachments = (IDictionary<string, IDictionary<string, object>>)rev.GetProperties().Get("_attachments");
                foreach (var entry in attachments) {
                    var attachment = entry.Value;
                    attachment.Remove("file");
                }
            }
                
            //TODO: rev.getBody().compact();

            if (_downloadsToInsert != null) {
                if(!_downloadsToInsert.QueueObject(rev)) {
                    Log.To.Sync.W(TAG, "{0} failed to queue {1} for download because it is already queued, marking completed...", this, rev);
                    SafeIncrementCompletedChangesCount();
                }
            }
            else {
                Log.To.Sync.W(TAG, "{0} is finished and cannot accept download requests", this);
            }
        }

        /// <summary>Fetches the contents of a revision from the remote db, including its parent revision ID.
        ///     </summary>
        /// <remarks>
        /// Fetches the contents of a revision from the remote db, including its parent revision ID.
        /// The contents are stored into rev.properties.
        /// </remarks>
        private void PullRemoteRevision(RevisionInternal rev)
        {
            // Construct a query. We want the revision history, and the bodies of attachments that have
            // been added since the latest revisions we have locally.
            // See: http://wiki.apache.org/couchdb/HTTP_Document_API#Getting_Attachments_With_a_Document
            var path = new StringBuilder($"/{Uri.EscapeUriString(rev.DocID)}?rev={Uri.EscapeUriString(rev.RevID.ToString())}&revs=true");

            var attachments = true;
            if(attachments) {
                // TODO: deferred attachments
                path.Append("&attachments=true");
            }

            // Include atts_since with a list of possible ancestor revisions of rev. If getting attachments,
            // this allows the server to skip the bodies of attachments that have not changed since the
            // local ancestor. The server can also trim the revision history it returns, to not extend past
            // the local ancestor (not implemented yet in SG but will be soon.)
            var knownRevs = default(IList<RevisionID>);
            ValueTypePtr<bool> haveBodies = false;
            try {
                knownRevs = LocalDatabase.Storage.GetPossibleAncestors(rev, MaxAttsSince, haveBodies)?.ToList();
            } catch(Exception e) {
                Log.To.Sync.W(TAG, "Error getting possible ancestors (probably database closed)", e);
            }

            if(knownRevs != null) {
                path.Append(haveBodies ? "&atts_since=" : "&revs_from=");
                path.Append(JoinQuotedEscaped(knownRevs.Select(x => x.ToString()).ToList()));
            } else {
                // If we don't have any revisions at all, at least tell the server how long a history we
                // can keep track of:
                var maxRevTreeDepth = LocalDatabase.GetMaxRevTreeDepth();
                if(rev.Generation > maxRevTreeDepth) {
                    path.AppendFormat("&revs_limit={0}", maxRevTreeDepth);
                }
            }

            var pathInside = path.ToString();
            Log.To.SyncPerf.I(TAG, "{0} getting {1}", this, rev);
            Log.To.Sync.V(TAG, "{0} GET {1}", this, new SecureLogString(pathInside, LogMessageSensitivity.PotentiallyInsecure));
            _remoteSession.SendAsyncMultipartDownloaderRequest(HttpMethod.Get, pathInside, null, LocalDatabase, (result, e) => 
            {
                // OK, now we've got the response revision:
                Log.To.SyncPerf.I(TAG, "{0} got {1}", this, rev);

                if (e != null) {
                    Log.To.Sync.I (TAG, String.Format("{0} error pulling remote revision", this), e);
                    LastError = e;
                    RevisionFailed();
                    SafeIncrementCompletedChangesCount();
                    if(IsDocumentError(e)) {
                        // Make sure this document is skipped because it is not available
                        // even though the server is functioning
                        _pendingSequences.RemoveSequence(rev.Sequence);
                        LastSequence = _pendingSequences.GetCheckpointedValue();
                    }
                } else {
                    var properties = result.AsDictionary<string, object>();
                    var gotRev = new PulledRevision(properties);
                    gotRev.Sequence = rev.Sequence;

                    if (_downloadsToInsert != null) {
                        if (!_downloadsToInsert.QueueObject (gotRev)) {
                            Log.To.Sync.W(TAG, "{0} failed to queue {1} for download because it is already queued, marking completed...", this, rev);
                            SafeIncrementCompletedChangesCount ();
                        }
                    } else {
                        Log.To.Sync.E (TAG, "downloadsToInsert is null");
                    }
                }

                // Note that we've finished this task; then start another one if there
                // are still revisions waiting to be pulled:
                PullRemoteRevisions ();
            });
        }

        private static bool IsDocumentError(Exception e) 
        {
            foreach (var inner in Misc.Flatten(e)) {
                if (IsDocumentError(inner as HttpResponseException) || IsDocumentError(inner as CouchbaseLiteException)) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDocumentError(HttpResponseException e)
        {
            if (e == null) {
                return false;
            }

            return e.StatusCode == System.Net.HttpStatusCode.NotFound ||
                e.StatusCode == System.Net.HttpStatusCode.Forbidden || e.StatusCode == System.Net.HttpStatusCode.Gone;
        }

        private static bool IsDocumentError(CouchbaseLiteException e)
        {
            if (e == null) {
                return false;
            }

            return e.Code == StatusCode.NotFound || e.Code == StatusCode.Forbidden;
        }

        /// <summary>This will be called when _revsToInsert fills up:</summary>
        private void InsertDownloads(IList<RevisionInternal> downloads)
        {
            Log.To.SyncPerf.I(TAG, "{0} inserting {1} revisions into db...", this, downloads.Count);
            Log.To.Sync.V(TAG, "{0} inserting {1} revisions...", this, downloads.Count);
            var time = DateTime.UtcNow;
            downloads.Sort(new RevisionComparer());
           
            if (!LocalDatabase.IsOpen) {
                return;
            }

            try {
                var success = LocalDatabase.RunInTransaction(() =>
                {
                    foreach (var rev in downloads) {
                        var fakeSequence = rev.Sequence;
                        rev.Sequence = 0L;
                        var history = Database.ParseCouchDBRevisionHistory(rev.GetProperties());
                        if ((history == null || history.Count == 0) && rev.Generation > 1) {
                            Log.To.Sync.W(TAG, "{0} missing revision history in response for: {0}", this, rev);
                            LastError = new CouchbaseLiteException(StatusCode.UpStreamError);
                            RevisionFailed();
                            continue;
                        }

                        Log.To.Sync.V(TAG, String.Format("Inserting {0} {1}", 
                            new SecureLogString(rev.DocID, LogMessageSensitivity.PotentiallyInsecure), 
                            new LogJsonString(history)));

                        // Insert the revision:
                        try {
                            LocalDatabase.ForceInsert(rev, history, RemoteUrl);
                        } catch (CouchbaseLiteException e) {
                            if (e.Code == StatusCode.Forbidden) {
                                Log.To.Sync.I(TAG, "{0} remote rev failed validation: {1}", this, rev);
                            } else if(e.Code == StatusCode.AttachmentError) {
                                // Revision with broken _attachments metadata (i.e. bogus revpos)
                                // should not stop replication. Warn and skip it.
                                Log.To.Sync.W(TAG, "{0} revision {1} has invalid attachment metadata: {2}",
                                    this, rev, new SecureLogJsonString(rev.GetAttachments(), LogMessageSensitivity.PotentiallyInsecure));
                            } else if(e.Code == StatusCode.DbBusy) {
                                Log.To.Sync.I(TAG, "Database is busy, will retry soon...");
                                // abort transaction; RunInTransaction will retry
                                return false;
                            } else {
                                Log.To.Sync.W(TAG, "{0} failed to write {1}: status={2}", this, rev, e.Code);
                                RevisionFailed();
                                LastError = e;
                                continue;
                            }
                        } catch (Exception e) {
                            throw Misc.CreateExceptionAndLog(Log.To.Sync, e, TAG,
                                "Error inserting downloads");
                        }

                        _pendingSequences.RemoveSequence(fakeSequence);
                    }

                    Log.To.Sync.V(TAG, "{0} finished inserting {1} revisions", this, downloads.Count);

                    return true;
                });

                Log.To.Sync.V(TAG, "Finished inserting {0} revisions. Success == {1}", downloads.Count, success);
            } catch (Exception e) {
                Log.To.Sync.E(TAG, "Exception inserting revisions, continuing...", e);
            }

            // Checkpoint:
            LastSequence = _pendingSequences.GetCheckpointedValue();

            var delta = (DateTime.UtcNow - time).TotalMilliseconds;
            Log.To.Sync.I(TAG, "Inserted {0} revs in {1} milliseconds", downloads.Count, delta);
            Log.To.SyncPerf.I(TAG, "Inserted {0} revs in {1} milliseconds", downloads.Count, delta);
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
            get { return ClientFactory.Headers; } 
            set { ClientFactory.Headers = value; } 
        }

        protected override void Retry()
        {
            if (_changeTracker != null) {
                _changeTracker.Stop(ErrorResolution.Ignore);
            }

            base.Retry();
        }

        protected override void StopGraceful()
        {
            var changeTrackerCopy = _changeTracker;
            if (changeTrackerCopy != null) {
                Log.D(TAG, "stopping changetracker " + _changeTracker);

                changeTrackerCopy.Client = null;
                // stop it from calling my changeTrackerStopped()
                changeTrackerCopy.Stop(ErrorResolution.Ignore);
                _changeTracker = null;
            }

            lock (_locker) {
                _revsToPull = null;
                _deletedRevsToPull = null;
                _bulkRevsToPull = null;
            }

            if (_downloadsToInsert != null) {
                _downloadsToInsert.FlushAll();
            }

            base.StopGraceful();
        }

        protected override void PerformGoOffline()
        {
            base.PerformGoOffline();
            if (_changeTracker != null) {
                _changeTracker.Stop(ErrorResolution.GoOffline);
            }

            _remoteSession.CancelRequests();
        }

        protected override void PerformGoOnline()
        {
            base.PerformGoOnline();

            BeginReplicating();
        }

        internal override void ProcessInbox(RevisionList inbox)
        {
            if (Status == ReplicationStatus.Offline) {
                Log.To.Sync.I(TAG, "{0} is offline, so skipping inbox process", this);
                return;
            }

            Debug.Assert(inbox != null);
            if (!_canBulkGet) {
                _canBulkGet = CheckServerCompatVersion("0.81");
            }

            Log.To.SyncPerf.I(TAG, "{0} processing {1} changes", this, inbox.Count);
            Log.To.Sync.V(TAG, "{0} looking up {1}", this, inbox);

            // Ask the local database which of the revs are not known to it:
            var lastInboxSequence = ((PulledRevision)inbox[inbox.Count - 1]).GetRemoteSequenceID();
            var numRevisionsRemoved = 0;
            try {
                // findMissingRevisions is the local equivalent of _revs_diff. it looks at the
                // array of revisions in inbox and removes the ones that already exist. So whatever's left in inbox
                // afterwards are the revisions that need to be downloaded.
                numRevisionsRemoved = LocalDatabase.Storage.FindMissingRevisions(inbox);
            } catch (Exception e) {
                Log.To.Sync.E(TAG, String.Format("{0} failed to look up local revs, aborting...", this), e);
                inbox = null;
            }

            var inboxCount = 0;
            if (inbox != null) {
                inboxCount = inbox.Count;
            }

            if (numRevisionsRemoved > 0)
            {
                // Some of the revisions originally in the inbox aren't missing; treat those as processed:
                Log.To.Sync.I (TAG, "{0} Removed {1} already present revisions", this, numRevisionsRemoved);
                SafeAddToCompletedChangesCount(numRevisionsRemoved);
            }

            if (inboxCount == 0) {
                // Nothing to do; just count all the revisions as processed.
                // Instead of adding and immediately removing the revs to _pendingSequences,
                // just do the latest one (equivalent but faster):
                Log.To.Sync.V(TAG, "{0} no new remote revisions to fetch", this);

                var seq = _pendingSequences.AddValue(lastInboxSequence);
                _pendingSequences.RemoveSequence(seq);
                LastSequence = _pendingSequences.GetCheckpointedValue();
                PauseOrResume();
                return;
            }

            Log.To.SyncPerf.I(TAG, "{0} queuing download requests for {1} revisions", this, inboxCount);
            Log.To.Sync.V(TAG, "{0} queuing remote revisions {1}", this, inbox);

            // Dump the revs into the queue of revs to pull from the remote db:
            lock (_locker) {
                int numBulked = 0;
                for (int i = 0; i < inboxCount; i++) {
                    var rev = (PulledRevision)inbox[i];
                    if (_canBulkGet || (rev.Generation == 1 && !rev.Deleted && !rev.IsConflicted)) {
                        //optimistically pull 1st-gen revs in bulk
                        if (_bulkRevsToPull == null) {
                            _bulkRevsToPull = new List<RevisionInternal>(100);
                        }

                        _bulkRevsToPull.Add(rev);
                        ++numBulked;
                    } else {
                        QueueRemoteRevision(rev);
                    }
                    rev.Sequence = _pendingSequences.AddValue(rev.GetRemoteSequenceID());
                }

                Log.To.Sync.I(TAG, "{4} queued {0} remote revisions from seq={1} ({2} in bulk, {3} individually)", inboxCount, 
                    ((PulledRevision)inbox[0]).GetRemoteSequenceID(), numBulked, inboxCount - numBulked, this);
            }

            PullRemoteRevisions();
            PauseOrResume();
        }

        internal override void BeginReplicating()
        {
            Log.To.Sync.I(TAG, "{2} will use MaxOpenHttpConnections({0}), MaxRevsToGetInBulk({1})", 
                ReplicationOptions.MaxOpenHttpConnections, 
                ReplicationOptions.MaxRevsToGetInBulk,
                this);

            if (_downloadsToInsert == null) {
                const int capacity = InboxCapacity * 2;
                TimeSpan delay = TimeSpan.FromSeconds(1);
                _downloadsToInsert = new Batcher<RevisionInternal>(new BatcherOptions<RevisionInternal> {
                    WorkExecutor = WorkExecutor,
                    Capacity = capacity,
                    Delay = delay,
                    Processor = InsertDownloads
                });
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

            _caughtUp = false;
            StartChangeTracker();
        }

        internal override void Stopping()
        {
            _downloadsToInsert = null;
            base.Stopping();
        }

        public override string ToString()
        {
            return String.Format("Puller {0}", ReplicatorID);
        }

        #endregion

        #region IChangeTrackerClient

        public void ChangeTrackerCaughtUp(ChangeTracker tracker)
        {
            if (!_caughtUp) {
                Log.To.Sync.I(TAG, "{0} caught up with changes", this);
                _caughtUp = true;
            }

            if (Continuous && ChangesCount == CompletedChangesCount) {
                FireTrigger (ReplicationTrigger.WaitingForChanges);
            }
        }

        public void ChangeTrackerFinished(ChangeTracker tracker)
        {
            ChangeTrackerCaughtUp(tracker);
        }

        public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
        {
            if (ServerType == null) {
                ServerType = _remoteSession.ServerType;
            }

            var lastSequence = change.Get("seq").ToString();
            var docID = (string)change.Get("id");
            if (docID == null) {
                Log.To.Sync.W (TAG, "{0} Change received with no id, ignoring...", this);
                return;
            }

            var removed = change.Get("removed") != null;
            if (removed) {
                Log.To.Sync.V(TAG, "Removed entry received, body may not be available");
            }

            if (!Document.IsValidDocumentId(docID)) {
                if (!docID.StartsWith("_user/", StringComparison.InvariantCultureIgnoreCase)) {
                    Log.To.Sync.W(TAG, "{0}: Received invalid doc ID from _changes: {1} ({2})", 
                        this, new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure), new LogJsonString(change));
                }

                return;
            }

            var deleted = change.GetCast<bool>("deleted");
            var changes = change.Get("changes").AsList<object>();
            SafeAddToChangesCount(changes.Count);

            foreach (var changeObj in changes) {
                var changeDict = changeObj.AsDictionary<string, object>();
                var revID = changeDict.GetCast<string>("rev").AsRevID();
                if (revID == null) {
                    Log.To.Sync.W (TAG, "{0} missing revID for entry, skipping...");
                    SafeIncrementCompletedChangesCount ();
                    continue;
                }

                var rev = new PulledRevision(docID, revID, deleted, LocalDatabase);
                rev.SetRemoteSequenceID(lastSequence);
                if (changes.Count > 1) {
                    rev.IsConflicted = true;
                }

                Log.To.Sync.D(TAG, "Adding rev to inbox " + rev);
                if(!AddToInbox(rev)) {
                    Log.To.Sync.W (TAG, "{0} Failed to add {1} to inbox, probably already added.  Marking completed", this, rev);
                    SafeIncrementCompletedChangesCount ();
                }
            }

            PauseOrResume();

            while (_revsToPull != null && _revsToPull.Count > 1000) {
                try {
                    // Presumably we are letting 1 or more other threads do something while we wait.
                    Thread.Sleep(500);
                }
                catch (Exception e) {
                    Log.To.Sync.W(TAG, "Swallowing exception while sleeping after receiving changetracker changes.", e);
                    // swallow
                }
            }
        }

        public void ChangeTrackerStopped(ChangeTracker tracker, ErrorResolution resolution)
        {
            WorkExecutor.StartNew(() => ProcessChangeTrackerStopped(tracker, resolution));
        }

        public CookieContainer GetCookieStore()
        {
            return CookieContainer;
        }

        #endregion

        #region Nested Classes

        private sealed class RevisionComparer : IComparer<RevisionInternal>
        {
            public RevisionComparer() { }

            public int Compare(RevisionInternal reva, RevisionInternal revb)
            {
                return Misc.TDSequenceCompare(reva != null ? reva.Sequence : -1L, revb != null ? revb.Sequence : -1L);
            }
        }


        #endregion
    }

}
