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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System.Threading;
using System.Data;
using Newtonsoft.Json;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Replicator
{
    internal sealed class Puller : Replication, IChangeTrackerClient
    {
        private const int MaxOpenHttpConnections = 16;

        private const int MaxRevsToGetInBulk = 50;

        internal const int MaxNumberOfAttsSince = 50;

        readonly string Tag = "Puller";

        internal bool canBulkGet;

        internal bool caughtUp;

        internal Batcher<RevisionInternal> downloadsToInsert;

        internal IList<RevisionInternal> revsToPull;

        internal IList<RevisionInternal> deletedRevsToPull;

        internal IList<RevisionInternal> bulkRevsToPull;

        internal ChangeTracker changeTracker;

        internal SequenceMap pendingSequences;

        internal volatile int httpConnectionCount;

        private readonly Object locker = new object ();

        /// <summary>Constructor</summary>
        internal Puller(Database db, Uri remote, bool continuous, TaskFactory workExecutor)
            : this(db, remote, continuous, null, workExecutor) { }

        /// <summary>Constructor</summary>
        internal Puller(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor) 
            : base(db, remote, continuous, clientFactory, workExecutor) {  }

        #region implemented abstract members of Replication

        public override IEnumerable<string> DocIds { get; set; }

        public override IDictionary<string, string> Headers 
        {
            get { return clientFactory.Headers; } 
            set { clientFactory.Headers = value; } 
        }

        #endregion

        // Maximum number of revs to fetch in a single bulk request
        // Maximum number of revision IDs to pass in an "?atts_since=" query param
        public override bool IsPull { get { return true; } }

        public override bool CreateTarget { get { return false; } set { return; /* No-op intended. Only used in Pusher. */ } }

        internal override void BeginReplicating()
        {
            if (downloadsToInsert == null)
            {
                const int capacity = 200;
                const int delay = 1000;
                downloadsToInsert = new Batcher<RevisionInternal> (WorkExecutor, capacity, delay, InsertDownloads);
            }

            if (pendingSequences == null)
            {
                pendingSequences = new SequenceMap();
                if (LastSequence != null)
                {
                    // Prime _pendingSequences so its checkpointedValue will reflect the last known seq:
                    var seq = pendingSequences.AddValue(LastSequence);
                    pendingSequences.RemoveSequence(seq);
                    Debug.Assert((pendingSequences.GetCheckpointedValue().Equals(LastSequence)));
                }
            }

            Log.D(Tag, "starting ChangeTracker with since = " + LastSequence);

            var mode = Continuous 
                       ? ChangeTrackerMode.LongPoll 
                       : ChangeTrackerMode.OneShot;

            changeTracker = new ChangeTracker(RemoteUrl, mode, LastSequence, true, this, WorkExecutor);
            changeTracker.Authenticator = Authenticator;

            if (Filter != null)
            {
                changeTracker.SetFilterName(Filter);
                if (FilterParams != null)
                {
                    changeTracker.SetFilterParams(FilterParams.ToDictionary(kvp=>kvp.Key, kvp=>(Object)kvp.Value));
                }
            }
            if (!continuous)
            {
                Log.D(Tag, "BeginReplicating() calling asyncTaskStarted()");
                AsyncTaskStarted();
            }

            changeTracker.UsePost = CheckServerCompatVersion("0.93");
            changeTracker.Start();
        }

        public override void Stop()
        {
            if (!IsRunning)
            {
                return;
            }
            if (changeTracker != null)
            {
                Log.D(Tag, "stopping changetracker " + changeTracker);

                changeTracker.SetClient(null);
                // stop it from calling my changeTrackerStopped()
                changeTracker.Stop();
                changeTracker = null;
                if (!Continuous)
                {   
                    Log.D(Tag, "stop() calling asyncTaskFinished()");
                    AsyncTaskFinished(1);
                }
            }
            // balances asyncTaskStarted() in beginReplicating()
            lock (locker)
            {
                revsToPull = null;
                deletedRevsToPull = null;
                bulkRevsToPull = null;
            }

            base.Stop();

            if (downloadsToInsert != null)
            {
                downloadsToInsert.FlushAll();
            }
        }

        internal override void Stopping()
        {
//            if (downloadsToInsert != null)
//            {
//                downloadsToInsert.Flush();
                downloadsToInsert = null;
//            }
            base.Stopping();
        }

        // Got a _changes feed entry from the ChangeTracker.
        public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
        {
            var lastSequence = change.Get("seq").ToString();
            var docID = (string)change.Get("id");
            if (docID == null)
            {
                return;
            }

            if (!LocalDatabase.IsValidDocumentId(docID))
            {
                if (!docID.StartsWith ("_user/", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.W(Tag, string.Format("{0}: Received invalid doc ID from _changes: {1} ({2})", this, docID, JsonConvert.SerializeObject(change)));
                }
                return;
            }

            var deleted = (change.ContainsKey("deleted") && ((bool)change.Get("deleted")).Equals(true));

            var changesContainer = change.Get("changes") as JContainer;
            var changes = changesContainer.ToArray();
            foreach (var changeObj in changes)
            {
                var changeDict = changeObj.ToObject<IDictionary<string, object>>();
                var revID = (string)changeDict.Get("rev");
                if (revID == null)
                {
                    continue;
                }

                var rev = new PulledRevision(docID, revID, deleted, LocalDatabase);
                rev.SetRemoteSequenceID(lastSequence);

                Log.D(Tag, "adding rev to inbox " + rev);
                Log.V(Tag, "ChangeTrackerReceivedChange() incrementing changesCount by 1");

                SafeAddToChangesCount(changes.Length);

                AddToInbox(rev);
            }

            while (revsToPull != null && revsToPull.Count > 1000)
            {
                try
                {
                    // Presumably we are letting 1 or more other threads do something while we wait.
                    Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    Log.W(Tag, "Swalling exception while sleeping after receiving changetracker changes.", e);
                    // swallow
                }
            }
        }

        // The change tracker reached EOF or an error.
        public void ChangeTrackerStopped(ChangeTracker tracker)
        {
            Log.V(Tag, "ChangeTracker " + tracker + " stopped");
            if (LastError == null && tracker.Error != null)
            {
                SetLastError(tracker.Error);
            }
            changeTracker = null;
            if (Batcher != null)
            {
                Log.D(Tag, "calling batcher.flush().  batcher.count() is " + Batcher.Count());

                Batcher.FlushAll();
            }
            if (!Continuous)
            {
                WorkExecutor.StartNew(() =>
                {
                    AsyncTaskFinished(1);
                });
            }
        }

        public HttpClient GetHttpClient()
        {
            return clientFactory.GetHttpClient();
        }
            
        /// <summary>Process a bunch of remote revisions from the _changes feed at once</summary>
        internal override void ProcessInbox(RevisionList inbox)
        {
            if (!online)
            {
                Log.V(Tag, "Offline, so skipping inbox process");
                return;
            }

            Debug.Assert(inbox != null);

            if (!canBulkGet)
            {
                canBulkGet = CheckServerCompatVersion("0.81");
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
                Log.E(Tag, "Failed to look up local revs", e);
                inbox = null;
            }

            //introducing this to java version since inbox may now be null everywhere
            var inboxCount = 0;
            if (inbox != null)
            {
                inboxCount = inbox.Count;
            }

            if (numRevisionsRemoved > 0)
            {
                Log.V(Tag, "processInbox() setting changesCount to: " + (ChangesCount - numRevisionsRemoved));
                SafeAddToChangesCount(-numRevisionsRemoved);
            }

            if (inboxCount == 0)
            {
                // Nothing to do. Just bump the lastSequence.
                Log.W(Tag, string.Format("{0} no new remote revisions to fetch", this));

                var seq = pendingSequences.AddValue(lastInboxSequence);
                pendingSequences.RemoveSequence(seq);
                LastSequence = pendingSequences.GetCheckpointedValue();
                return;
            }

            Log.V(Tag, "fetching " + inboxCount + " remote revisions...");

            // Dump the revs into the queue of revs to pull from the remote db:
            lock (locker) {
                int numBulked = 0;
                for (int i = 0; i < inbox.Count; i++) {
                    var rev = (PulledRevision)inbox [i];
                    //TODO: add support for rev isConflicted
                    if (canBulkGet || (rev.GetGeneration() == 1 && !rev.IsDeleted()))
                    {
                        // &&!rev.isConflicted)
                        //optimistically pull 1st-gen revs in bulk
                        if (bulkRevsToPull == null)
                        {
                            bulkRevsToPull = new List<RevisionInternal>(100);
                        }
                        bulkRevsToPull.AddItem(rev);
                        ++numBulked;
                    }
                    else
                    {
                        QueueRemoteRevision(rev);
                    }
                    rev.SetSequence (pendingSequences.AddValue (rev.GetRemoteSequenceID ()));
                }
            }

            PullRemoteRevisions();
        }

        /// <summary>Add a revision to the appropriate queue of revs to individually GET</summary>
        internal void QueueRemoteRevision(RevisionInternal rev)
        {
            if (rev.IsDeleted())
            {
                if (deletedRevsToPull == null)
                {
                    deletedRevsToPull = new List<RevisionInternal>(100);
                }
                deletedRevsToPull.AddItem(rev);
            }
            else
            {
                if (revsToPull == null)
                {
                    revsToPull = new List<RevisionInternal>(100);
                }
                revsToPull.AddItem(rev);
            }
        }

        /// <summary>
        /// Start up some HTTP GETs, within our limit on the maximum simultaneous number
        /// The entire method is not synchronized, only the portion pulling work off the list
        /// Important to not hold the synchronized block while we do network access
        /// </summary>
        public void PullRemoteRevisions()
        {
            //find the work to be done in a synchronized block
            var workToStartNow = new List<RevisionInternal>();
            var bulkWorkToStartNow = new List<RevisionInternal>();
            lock (locker)
            {
                while (httpConnectionCount + bulkWorkToStartNow.Count + workToStartNow.Count < MaxOpenHttpConnections)
                {
                    int nBulk = 0;
                    if (bulkRevsToPull != null)
                    {
                        nBulk = (bulkRevsToPull.Count < MaxRevsToGetInBulk) ? bulkRevsToPull.Count : MaxRevsToGetInBulk;
                    }
                    if (nBulk == 1)
                    {
                        // Rather than pulling a single revision in 'bulk', just pull it normally:
                        QueueRemoteRevision(bulkRevsToPull[0]);
                        bulkRevsToPull.Remove(0);
                        nBulk = 0;
                    }
                    if (nBulk > 0)
                    {
                        // Prefer to pull bulk revisions:
                        var range = new Couchbase.Lite.Util.ArraySegment<RevisionInternal>(bulkRevsToPull.ToArray(), 0, nBulk);
                        bulkWorkToStartNow.AddRange(range) ;
                        bulkRevsToPull.RemoveAll(range);
                    }
                    else
                    {
                        // Prefer to pull an existing revision over a deleted one:
                        IList<RevisionInternal> queue = revsToPull;
                        if (queue == null || queue.Count == 0)
                        {
                            queue = deletedRevsToPull;
                            if (queue == null || queue.Count == 0)
                            {
                                break;
                            }
                        }
                        // both queues are empty
                        workToStartNow.AddItem(queue[0]);
                        queue.Remove(0);
                    }
                }
            }
            //actually run it outside the synchronized block
            if (bulkWorkToStartNow.Count > 0)
            {
                PullBulkRevisions(bulkWorkToStartNow);
            }

            foreach (var rev in workToStartNow)
            {
                PullRemoteRevision(rev);
            }
        }

        // Get a bunch of revisions in one bulk request. Will use _bulk_get if possible.
        internal void PullBulkRevisions(IList<RevisionInternal> bulkRevs)
        {
            var nRevs = bulkRevs.Count;
            if (nRevs == 0)
            {
                return;
            }

            Log.V(Tag, "{0} bulk-fetching {1} remote revisions...", this, nRevs);
            Log.V(Tag, "{0} bulk-fetching remote revisions: {1}", this, bulkRevs);

            if (!canBulkGet)
            {
                PullBulkWithAllDocs(bulkRevs);
                return;
            }

            Log.V(Tag, "POST _bulk_get");
            var remainingRevs = new List<RevisionInternal>(bulkRevs);

            Log.V(Tag, "PullBulkRevisions() calling AsyncTaskStarted()");
            AsyncTaskStarted();

            ++httpConnectionCount;
            BulkDownloader dl;
            try
            {
                dl = new BulkDownloader(WorkExecutor, clientFactory, RemoteUrl, bulkRevs, LocalDatabase, RequestHeaders);
                // , new _BulkDownloaderDocumentBlock_506(this, remainingRevs), new _RemoteRequestCompletionBlock_537(this, remainingRevs)
                // TODO: add event handles for completion and documentdownloaded.
                dl.DocumentDownloaded += (sender, e) =>
                {
                    var props = e.DocumentProperties;

                    var rev = props.Get ("_id") != null 
                        ? new RevisionInternal (props) 
                        : new RevisionInternal ((string)props.Get ("id"), (string)props.Get ("rev"), false);

                    Log.D(Tag, "Document downloaded! {0}", rev);

                    var pos = remainingRevs.IndexOf(rev);
                    if (pos > -1)
                    {
                        rev.SetSequence(remainingRevs[pos].GetSequence());
                        remainingRevs.Remove(pos);
                    }
                    else
                    {
                        Log.W(Tag, "Received unexpected rev rev");
                    }
                    if (props.Get("_id") != null)
                    {
                        QueueDownloadedRevision(rev);
                    }
                    else
                    {
                        var status = StatusFromBulkDocsResponseItem(props);
                        SetLastError(new CouchbaseLiteException(status.GetCode()));
                        RevisionFailed();
                        SafeIncrementCompletedChangesCount();
                    }
                };

                dl.Complete += (sender, args) => 
                {
                    if (args != null && args.Error != null)
                    {
                        SetLastError(args.Error);
                        RevisionFailed();
                        SafeAddToCompletedChangesCount(remainingRevs.Count);
                    }

                    Log.V(Tag, "PullBulkRevisions.Completion event handler calling AsyncTaskFinished()");
                    AsyncTaskFinished(1);

                    --httpConnectionCount;

                    PullRemoteRevisions();
                };
            }
            catch (Exception)
            {
                // Got a revision!
                // Find the matching revision in 'remainingRevs' and get its sequence:
                // Add to batcher ... eventually it will be fed to -insertRevisions:.
                // The entire _bulk_get is finished:
                // Note that we've finished this task:
                // Start another task if there are still revisions waiting to be pulled:
                return;
            }
            dl.Authenticator = Authenticator;
            WorkExecutor.StartNew(dl.Run, CancellationTokenSource.Token, TaskCreationOptions.LongRunning, WorkExecutor.Scheduler);
//            dl.Run();
        }

        // This invokes the tranformation block if one is installed and queues the resulting Revision
        private void QueueDownloadedRevision(RevisionInternal rev)
        {
            if (revisionBodyTransformationFunction != null)
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
                    Log.V(Tag, "Transformer rejected revision {0}", rev);
                    pendingSequences.RemoveSequence(rev.GetSequence());
                    LastSequence = pendingSequences.GetCheckpointedValue();
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
            Log.V(Tag, "QueueDownloadedRevision() calling AsyncTaskStarted()");
            AsyncTaskStarted();

            downloadsToInsert.QueueObject(rev);
        }

        // Get as many revisions as possible in one _all_docs request.
        // This is compatible with CouchDB, but it only works for revs of generation 1 without attachments.
        internal void PullBulkWithAllDocs(IList<RevisionInternal> bulkRevs)
        {
            // http://wiki.apache.org/couchdb/HTTP_Bulk_Document_API
            Log.V(Tag, "PullBulkWithAllDocs() calling AsyncTaskStarted()");
            AsyncTaskStarted();

            ++httpConnectionCount;

            var remainingRevs = new List<RevisionInternal>(bulkRevs);
            var keys = bulkRevs.Select(rev => rev.GetDocId()).ToArray();
            var body = new Dictionary<string, object>();
            body.Put("keys", keys);

            SendAsyncRequest(HttpMethod.Post, "/_all_docs?include_docs=true", body, (result, e) =>
            {
                var res = result.AsDictionary<string, object>();
                if (e != null) {
                    SetLastError(e);
                    RevisionFailed();
                    SafeAddToCompletedChangesCount(bulkRevs.Count);
                } else {
                    // Process the resulting rows' documents.
                    // We only add a document if it doesn't have attachments, and if its
                    // revID matches the one we asked for.
                    var rows = res.Get ("rows").AsList<IDictionary<string, object>>();
                    Log.V (Tag, "Checking {0} bulk-fetched remote revisions", rows.Count);

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
                    Log.V (Tag, "Bulk-fetch didn't work for {0} of {1} revs; getting individually", remainingRevs.Count, bulkRevs.Count);
                    foreach (var rev in remainingRevs) 
                    {
                        QueueRemoteRevision (rev);
                    }
                    PullRemoteRevisions ();
                }

                // Note that we've finished this task:
                Log.V (Tag, "PullBulkWithAllDocs() calling AsyncTaskFinished()");
                AsyncTaskFinished (1);

                --httpConnectionCount;

                // Start another task if there are still revisions waiting to be pulled:
                PullRemoteRevisions();
            });
        }


        private new static Status StatusFromBulkDocsResponseItem(IDictionary<string, object> item)
        {
            try {
                if (!item.ContainsKey ("error")) {
                    return new Status (StatusCode.Ok);
                }

                var errorStr = (string)item.Get ("error");
                if (StringEx.IsNullOrWhiteSpace(errorStr)) {
                    return new Status (StatusCode.Ok);
                }

                // 'status' property is nonstandard; TouchDB returns it, others don't.
                var statusString = (string)item.Get ("status");
                var status = Convert.ToInt32 (statusString);
                if (status >= 400) {
                    return new Status ((StatusCode)status);
                }

                // If no 'status' present, interpret magic hardcoded CouchDB error strings:
                if (errorStr.Equals ("unauthorized", StringComparison.InvariantCultureIgnoreCase)) {
                    return new Status (StatusCode.Unauthorized);
                }

                if (errorStr.Equals ("forbidden", StringComparison.InvariantCultureIgnoreCase)) {
                    return new Status (StatusCode.Forbidden);
                }

                if (errorStr.Equals ("conflict", StringComparison.InvariantCultureIgnoreCase)) {
                    return new Status (StatusCode.Conflict);
                }

                return new Status (StatusCode.UpStreamError);
            }
            catch (Exception e)
            {
                Log.E(Database.Tag, "Exception getting status from " + item, e);
            }
            return new Status(StatusCode.Ok);
        }

        /// <summary>Fetches the contents of a revision from the remote db, including its parent revision ID.
        ///     </summary>
        /// <remarks>
        /// Fetches the contents of a revision from the remote db, including its parent revision ID.
        /// The contents are stored into rev.properties.
        /// </remarks>
        internal void PullRemoteRevision(RevisionInternal rev)
        {
            Log.D(Tag, "PullRemoteRevision with rev: {0}", rev);

            Log.D(Tag, "PullRemoteRevision() calling AsyncTaskStarted()");
            AsyncTaskStarted();

            httpConnectionCount++;

            // Construct a query. We want the revision history, and the bodies of attachments that have
            // been added since the latest revisions we have locally.
            // See: http://wiki.apache.org/couchdb/HTTP_Document_API#Getting_Attachments_With_a_Document
            var path = new StringBuilder("/" + Uri.EscapeUriString(rev.GetDocId()) + "?rev=" + Uri.EscapeUriString(rev.GetRevId()) + "&revs=true&attachments=true");
            var knownRevs = KnownCurrentRevIDs(rev);
            if (knownRevs == null)
            {
                //this means something is wrong, possibly the replicator has shut down
                Log.D(Tag, "PullRemoteRevision() calling AsyncTaskFinished()");
                AsyncTaskFinished(1);

                httpConnectionCount--;
                return;
            }

            if (knownRevs.Count > 0)
            {
                path.Append("&atts_since=");
                path.Append(JoinQuotedEscaped(knownRevs));
            }

            //create a final version of this variable for the log statement inside
            //FIXME find a way to avoid this
            var pathInside = path.ToString();
            SendAsyncMultipartDownloaderRequest(HttpMethod.Get, pathInside, null, LocalDatabase, (result, e) => 
            {
                try 
                {
                    // OK, now we've got the response revision:
                    Log.D (Tag, "PullRemoteRevision got response for rev: " + rev);

                    if (e != null)
                    {
                        Log.E (Tag, "Error pulling remote revision", e);
                        SetLastError(e);
                        RevisionFailed();
                        Log.D(Tag, "PullRemoteRevision updating completedChangesCount from " + 
                            CompletedChangesCount + " -> " + (CompletedChangesCount + 1) 
                            + " due to error pulling remote revision");
                        SafeIncrementCompletedChangesCount();
                    }
                    else
                    {
                        var properties = result.AsDictionary<string, object>();
                        var gotRev = new PulledRevision(properties);
                        gotRev.SetSequence(rev.GetSequence());
                        AsyncTaskStarted ();
                        Log.D(Tag, "PullRemoteRevision add rev: " + gotRev + " to batcher");
                        downloadsToInsert.QueueObject(gotRev);
                    }
                }
                finally
                {
                    Log.D (Tag, "PullRemoteRevision.onCompletion() calling AsyncTaskFinished()");
                    AsyncTaskFinished (1);
                }

                // Note that we've finished this task; then start another one if there
                // are still revisions waiting to be pulled:
                --httpConnectionCount;
                PullRemoteRevisions ();
            });
        }

        /// <summary>This will be called when _revsToInsert fills up:</summary>
        public void InsertDownloads(IList<RevisionInternal> downloads)
        {
            Log.D(Tag, "Inserting " + downloads.Count + " revisions...");

            var time = DateTime.UtcNow;

            downloads.Sort(new RevisionComparer());

            if (LocalDatabase == null)
            {
                AsyncTaskFinished(downloads.Count);
                return;
            }

            try
            {
                var success = LocalDatabase.RunInTransaction(() =>
                {
                    foreach (var rev in downloads)
                    {
                        var fakeSequence = rev.GetSequence();
                        var history = Database.ParseCouchDBRevisionHistory(rev.GetProperties());
                        if (history.Count == 0 && rev.GetGeneration() > 1) 
                        {
                            Log.W(Tag, String.Format("{0}: Missing revision history in response for: {1}", this, rev));
                            SetLastError(new CouchbaseLiteException(StatusCode.UpStreamError));
                            RevisionFailed();
                            continue;
                        }

                        Log.D(Tag, String.Format("{0}: inserting {1} {2}", this, rev.GetDocId(), history));

                        // Insert the revision:
                        try
                        {
                            LocalDatabase.ForceInsert(rev, history, RemoteUrl);
                        }
                        catch (CouchbaseLiteException e)
                        {
                            if (e.GetCBLStatus().GetCode() == StatusCode.Forbidden)
                            {
                                Log.I(Tag, "Remote rev failed validation: " + rev);
                            }
                            else
                            {
                                Log.W(Tag, " failed to write " + rev + ": status=" + e.GetCBLStatus().GetCode());
                                RevisionFailed();
                                SetLastError(e);
                                continue;
                            }
                        }
                        catch (Exception e)
                        {
                            Log.E(Tag, "Exception inserting downloads.", e);
                            throw;
                        }
                        pendingSequences.RemoveSequence(fakeSequence);
                    }

                    Log.D(Tag, " Finished inserting " + downloads.Count + " revisions");

                    return true;
                });
                Log.D(Tag, "Finished inserting {0}. Success == {1}", downloads.Count, success);
            }
            catch (Exception e)
            {
                Log.E(Tag, "Exception inserting revisions", e);
            }
            finally
            {
                Log.D(Tag, "InsertRevisions() calling AsyncTaskFinished()");
                AsyncTaskFinished(downloads.Count);
            }

            // Checkpoint:
            LastSequence = pendingSequences.GetCheckpointedValue();

            var delta = (DateTime.UtcNow - time).TotalMilliseconds;
            Log.D(Tag, "inserted {0} revs in {1} milliseconds", downloads.Count, delta);

            var newCompletedChangesCount = CompletedChangesCount + downloads.Count;
            Log.D(Tag, "InsertDownloads() updating CompletedChangesCount from {0} -> {1}", CompletedChangesCount, newCompletedChangesCount);

            SafeAddToCompletedChangesCount(downloads.Count);

            Log.D(Tag, "InsertDownloads updating completedChangesCount from " + newCompletedChangesCount + " -> " + CompletedChangesCount);
        }

        private sealed class RevisionComparer : IComparer<RevisionInternal>
        {
            public RevisionComparer() { }

            public int Compare(RevisionInternal reva, RevisionInternal revb)
            {
                return Misc.TDSequenceCompare(reva.GetSequence(), revb.GetSequence());
            }
        }

        private IList<String> KnownCurrentRevIDs(RevisionInternal rev)
        {
            if (LocalDatabase != null)
            {
                return LocalDatabase.GetAllRevisionsOfDocumentID(rev.GetDocId(), true).GetAllRevIds();
            }
            return null;
        }

        public string JoinQuotedEscaped(IList<string> strings)
        {
            if (strings.Count == 0)
            {
                return "[]";
            }

            IEnumerable<Byte> json = null;

            try
            {
                json = Manager.GetObjectMapper().WriteValueAsBytes(strings);
            }
            catch (Exception e)
            {
                Log.W(Tag, "Unable to serialize json", e);
            }

            return Uri.EscapeUriString(Runtime.GetStringForBytes(json));
        }

        internal override Boolean GoOffline()
        {
            Log.D(Tag, "goOffline() called, stopping changeTracker: " + changeTracker);
            if (!base.GoOffline())
            {
                return false;
            }
            if (changeTracker != null)
            {
                changeTracker.Stop();
            }
            return true;
        }
    }

}
