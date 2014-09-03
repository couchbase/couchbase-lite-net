// 
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
//using System;
using System.Collections.Generic;
using System.Text;
using Apache.Http.Client;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
    /// <exclude></exclude>
    public sealed class Puller : Replication, ChangeTrackerClient
    {
        private const int MaxOpenHttpConnections = 16;

        private const int MaxRevsToGetInBulk = 50;

        public const int MaxNumberOfAttsSince = 50;

        protected internal bool canBulkGet;

        protected internal bool caughtUp;

        protected internal Batcher<RevisionInternal> downloadsToInsert;

        protected internal IList<RevisionInternal> revsToPull;

        protected internal IList<RevisionInternal> deletedRevsToPull;

        protected internal IList<RevisionInternal> bulkRevsToPull;

        protected internal ChangeTracker changeTracker;

        protected internal SequenceMap pendingSequences;

        protected internal volatile int httpConnectionCount;

        /// <summary>Constructor</summary>
        [InterfaceAudience.Private]
        public Puller(Database db, Uri remote, bool continuous, ScheduledExecutorService 
            workExecutor) : this(db, remote, continuous, null, workExecutor)
        {
        }

        /// <summary>Constructor</summary>
        [InterfaceAudience.Private]
        public Puller(Database db, Uri remote, bool continuous, HttpClientFactory clientFactory
            , ScheduledExecutorService workExecutor) : base(db, remote, continuous, clientFactory
            , workExecutor)
        {
        }

        // Maximum number of revs to fetch in a single bulk request
        // Maximum number of revision IDs to pass in an "?atts_since=" query param
        [InterfaceAudience.Public]
        public override bool IsPull()
        {
            return true;
        }

        [InterfaceAudience.Public]
        public override bool ShouldCreateTarget()
        {
            return false;
        }

        [InterfaceAudience.Public]
        public override void SetCreateTarget(bool createTarget)
        {
        }

        [InterfaceAudience.Public]
        public override void Stop()
        {
            if (!running)
            {
                return;
            }
            if (changeTracker != null)
            {
                Log.D(Log.TagSync, "%s: stopping changetracker", this, changeTracker);
                changeTracker.SetClient(null);
                // stop it from calling my changeTrackerStopped()
                changeTracker.Stop();
                changeTracker = null;
                if (!continuous)
                {
                    Log.V(Log.TagSync, "%s | %s : puller.stop() calling asyncTaskFinished()", this, Sharpen.Thread
                        .CurrentThread());
                    AsyncTaskFinished(1);
                }
            }
            // balances asyncTaskStarted() in beginReplicating()
            lock (this)
            {
                revsToPull = null;
                deletedRevsToPull = null;
                bulkRevsToPull = null;
            }
            base.Stop();
            if (downloadsToInsert != null)
            {
                downloadsToInsert.Flush();
            }
        }

        [InterfaceAudience.Private]
        public override void BeginReplicating()
        {
            if (downloadsToInsert == null)
            {
                int capacity = 200;
                int delay = 1000;
                downloadsToInsert = new Batcher<RevisionInternal>(workExecutor, capacity, delay, 
                    new _BatchProcessor_131(this));
            }
            if (pendingSequences == null)
            {
                pendingSequences = new SequenceMap();
                if (GetLastSequence() != null)
                {
                    // Prime _pendingSequences so its checkpointedValue will reflect the last known seq:
                    long seq = pendingSequences.AddValue(GetLastSequence());
                    pendingSequences.RemoveSequence(seq);
                    System.Diagnostics.Debug.Assert((pendingSequences.GetCheckpointedValue().Equals(GetLastSequence
                        ())));
                }
            }
            Log.W(Log.TagSync, "%s: starting ChangeTracker with since=%s", this, lastSequence
                );
            changeTracker = new ChangeTracker(remote, continuous ? ChangeTracker.ChangeTrackerMode
                .LongPoll : ChangeTracker.ChangeTrackerMode.OneShot, true, lastSequence, this);
            changeTracker.SetAuthenticator(GetAuthenticator());
            Log.W(Log.TagSync, "%s: started ChangeTracker %s", this, changeTracker);
            if (filterName != null)
            {
                changeTracker.SetFilterName(filterName);
                if (filterParams != null)
                {
                    changeTracker.SetFilterParams(filterParams);
                }
            }
            changeTracker.SetDocIDs(documentIDs);
            changeTracker.SetRequestHeaders(requestHeaders);
            if (!continuous)
            {
                Log.V(Log.TagSync, "%s | %s: beginReplicating() calling asyncTaskStarted()", this
                    , Sharpen.Thread.CurrentThread());
                AsyncTaskStarted();
            }
            changeTracker.SetUsePOST(ServerIsSyncGatewayVersion("0.93"));
            changeTracker.Start();
        }

        private sealed class _BatchProcessor_131 : BatchProcessor<RevisionInternal>
        {
            public _BatchProcessor_131(Puller _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Process(IList<RevisionInternal> inbox)
            {
                this._enclosing.InsertDownloads(inbox);
            }

            private readonly Puller _enclosing;
        }

        [InterfaceAudience.Private]
        protected internal override void Stopped()
        {
            downloadsToInsert = null;
            base.Stopped();
        }

        // Got a _changes feed entry from the ChangeTracker.
        [InterfaceAudience.Private]
        public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
        {
            string lastSequence = change.Get("seq").ToString();
            string docID = (string)change.Get("id");
            if (docID == null)
            {
                return;
            }
            if (!Database.IsValidDocumentId(docID))
            {
                Log.W(Log.TagSync, "%s: Received invalid doc ID from _changes: %s", this, change);
                return;
            }
            bool deleted = (change.ContainsKey("deleted") && ((bool)change.Get("deleted")).Equals
                (true));
            IList<IDictionary<string, object>> changes = (IList<IDictionary<string, object>>)
                change.Get("changes");
            foreach (IDictionary<string, object> changeDict in changes)
            {
                string revID = (string)changeDict.Get("rev");
                if (revID == null)
                {
                    continue;
                }
                PulledRevision rev = new PulledRevision(docID, revID, deleted, db);
                rev.SetRemoteSequenceID(lastSequence);
                Log.D(Log.TagSync, "%s: adding rev to inbox %s", this, rev);
                Log.V(Log.TagSync, "%s: changeTrackerReceivedChange() incrementing changesCount by 1"
                    , this);
                // this is purposefully done slightly different than the ios version
                AddToChangesCount(1);
                AddToInbox(rev);
            }
            while (revsToPull != null && revsToPull.Count > 1000)
            {
                try
                {
                    Sharpen.Thread.Sleep(500);
                }
                catch (Exception)
                {
                }
            }
        }

        // <-- TODO: why is this here?
        [InterfaceAudience.Private]
        public void ChangeTrackerStopped(ChangeTracker tracker)
        {
            Log.W(Log.TagSync, "%s: ChangeTracker %s stopped", this, tracker);
            if (error == null && tracker.GetLastError() != null)
            {
                SetError(tracker.GetLastError());
            }
            changeTracker = null;
            if (batcher != null)
            {
                Log.D(Log.TagSync, "%s: calling batcher.flush().  batcher.count() is %d", this, batcher
                    .Count());
                batcher.Flush();
            }
            if (!IsContinuous())
            {
                // the asyncTaskFinished needs to run on the work executor
                // in order to fix https://github.com/couchbase/couchbase-lite-java-core/issues/91
                // basically, bad things happen when this runs on ChangeTracker thread.
                workExecutor.Submit(new _Runnable_239(this));
            }
        }

        private sealed class _Runnable_239 : Runnable
        {
            public _Runnable_239(Puller _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Run()
            {
                Log.V(Log.TagSync, "%s | %s: changeTrackerStopped() calling asyncTaskFinished()", 
                    this, Sharpen.Thread.CurrentThread());
                this._enclosing.AsyncTaskFinished(1);
            }

            private readonly Puller _enclosing;
        }

        // balances -asyncTaskStarted in -startChangeTracker
        [InterfaceAudience.Private]
        public HttpClient GetHttpClient()
        {
            HttpClient httpClient = this.clientFactory.GetHttpClient();
            return httpClient;
        }

        /// <summary>Process a bunch of remote revisions from the _changes feed at once</summary>
        [InterfaceAudience.Private]
        protected internal override void ProcessInbox(RevisionList inbox)
        {
            if (canBulkGet == null)
            {
                canBulkGet = ServerIsSyncGatewayVersion("0.81");
            }
            // Ask the local database which of the revs are not known to it:
            string lastInboxSequence = ((PulledRevision)inbox[inbox.Count - 1]).GetRemoteSequenceID
                ();
            int numRevisionsRemoved = 0;
            try
            {
                // findMissingRevisions is the local equivalent of _revs_diff. it looks at the
                // array of revisions in ‚Äòinbox‚Äô and removes the ones that already exist. So whatever‚Äôs left in ‚Äòinbox‚Äô
                // afterwards are the revisions that need to be downloaded.
                numRevisionsRemoved = db.FindMissingRevisions(inbox);
            }
            catch (SQLException e)
            {
                Log.E(Log.TagSync, string.Format("%s failed to look up local revs", this), e);
                inbox = null;
            }
            //introducing this to java version since inbox may now be null everywhere
            int inboxCount = 0;
            if (inbox != null)
            {
                inboxCount = inbox.Count;
            }
            if (numRevisionsRemoved > 0)
            {
                Log.V(Log.TagSync, "%s: processInbox() setting changesCount to: %s", this, GetChangesCount
                    () - numRevisionsRemoved);
                // May decrease the changesCount, to account for the revisions we just found out we don‚Äôt need to get.
                AddToChangesCount(-1 * numRevisionsRemoved);
            }
            if (inboxCount == 0)
            {
                // Nothing to do. Just bump the lastSequence.
                Log.W(Log.TagSync, "%s no new remote revisions to fetch", this);
                long seq = pendingSequences.AddValue(lastInboxSequence);
                pendingSequences.RemoveSequence(seq);
                SetLastSequence(pendingSequences.GetCheckpointedValue());
                return;
            }
            Log.V(Log.TagSync, "%s: fetching %s remote revisions...", this, inboxCount);
            // Dump the revs into the queue of revs to pull from the remote db:
            lock (this)
            {
                int numBulked = 0;
                for (int i = 0; i < inbox.Count; i++)
                {
                    PulledRevision rev = (PulledRevision)inbox[i];
                    //TODO: add support for rev isConflicted
                    if (canBulkGet || (rev.GetGeneration() == 1 && !rev.IsDeleted()))
                    {
                        // &&!rev.isConflicted)
                        //optimistically pull 1st-gen revs in bulk
                        if (bulkRevsToPull == null)
                        {
                            bulkRevsToPull = new AList<RevisionInternal>(100);
                        }
                        bulkRevsToPull.AddItem(rev);
                        ++numBulked;
                    }
                    else
                    {
                        QueueRemoteRevision(rev);
                    }
                    rev.SetSequence(pendingSequences.AddValue(rev.GetRemoteSequenceID()));
                }
            }
            PullRemoteRevisions();
        }

        /// <summary>Add a revision to the appropriate queue of revs to individually GET</summary>
        [InterfaceAudience.Private]
        protected internal void QueueRemoteRevision(RevisionInternal rev)
        {
            if (rev.IsDeleted())
            {
                if (deletedRevsToPull == null)
                {
                    deletedRevsToPull = new AList<RevisionInternal>(100);
                }
                deletedRevsToPull.AddItem(rev);
            }
            else
            {
                if (revsToPull == null)
                {
                    revsToPull = new AList<RevisionInternal>(100);
                }
                revsToPull.AddItem(rev);
            }
        }

        /// <summary>
        /// Start up some HTTP GETs, within our limit on the maximum simultaneous number
        /// <p/>
        /// The entire method is not synchronized, only the portion pulling work off the list
        /// Important to not hold the synchronized block while we do network access
        /// </summary>
        [InterfaceAudience.Private]
        public void PullRemoteRevisions()
        {
            //find the work to be done in a synchronized block
            IList<RevisionInternal> workToStartNow = new AList<RevisionInternal>();
            IList<RevisionInternal> bulkWorkToStartNow = new AList<RevisionInternal>();
            lock (this)
            {
                while (httpConnectionCount + workToStartNow.Count < MaxOpenHttpConnections)
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
                        Sharpen.Collections.AddAll(bulkWorkToStartNow, bulkRevsToPull.SubList(0, nBulk));
                        bulkRevsToPull.SubList(0, nBulk).Clear();
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
            foreach (RevisionInternal work in workToStartNow)
            {
                PullRemoteRevision(work);
            }
        }

        /// <summary>Fetches the contents of a revision from the remote db, including its parent revision ID.
        ///     </summary>
        /// <remarks>
        /// Fetches the contents of a revision from the remote db, including its parent revision ID.
        /// The contents are stored into rev.properties.
        /// </remarks>
        [InterfaceAudience.Private]
        public void PullRemoteRevision(RevisionInternal rev)
        {
            Log.D(Log.TagSync, "%s: pullRemoteRevision with rev: %s", this, rev);
            Log.V(Log.TagSync, "%s | %s: pullRemoteRevision() calling asyncTaskStarted()", this
                , Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            ++httpConnectionCount;
            // Construct a query. We want the revision history, and the bodies of attachments that have
            // been added since the latest revisions we have locally.
            // See: http://wiki.apache.org/couchdb/HTTP_Document_API#Getting_Attachments_With_a_Document
            StringBuilder path = new StringBuilder("/" + URLEncoder.Encode(rev.GetDocId()) + 
                "?rev=" + URLEncoder.Encode(rev.GetRevId()) + "&revs=true&attachments=true");
            IList<string> knownRevs = KnownCurrentRevIDs(rev);
            if (knownRevs == null)
            {
                Log.W(Log.TagSync, "knownRevs == null, something is wrong, possibly the replicator has shut down"
                    );
                Log.V(Log.TagSync, "%s | %s: pullRemoteRevision() calling asyncTaskFinished()", this
                    , Sharpen.Thread.CurrentThread());
                AsyncTaskFinished(1);
                --httpConnectionCount;
                return;
            }
            if (knownRevs.Count > 0)
            {
                path.Append("&atts_since=");
                path.Append(JoinQuotedEscaped(knownRevs));
            }
            //create a final version of this variable for the log statement inside
            //FIXME find a way to avoid this
            string pathInside = path.ToString();
            SendAsyncMultipartDownloaderRequest("GET", pathInside, null, db, new _RemoteRequestCompletionBlock_439
                (this, rev));
        }

        private sealed class _RemoteRequestCompletionBlock_439 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_439(Puller _enclosing, RevisionInternal rev)
            {
                this._enclosing = _enclosing;
                this.rev = rev;
            }

            public void OnCompletion(object result, Exception e)
            {
                try
                {
                    if (e != null)
                    {
                        Log.E(Log.TagSync, "Error pulling remote revision", e);
                        this._enclosing.SetError(e);
                        this._enclosing.RevisionFailed();
                        Log.D(Log.TagSync, "%s: pullRemoteRevision() updating completedChangesCount from %d  ->  due to error pulling remote revision"
                            , this, this._enclosing.GetCompletedChangesCount(), this._enclosing.GetCompletedChangesCount
                            () + 1);
                        this._enclosing.AddToCompletedChangesCount(1);
                    }
                    else
                    {
                        IDictionary<string, object> properties = (IDictionary<string, object>)result;
                        PulledRevision gotRev = new PulledRevision(properties, this._enclosing.db);
                        gotRev.SetSequence(rev.GetSequence());
                        // Add to batcher ... eventually it will be fed to -insertDownloads:.
                        Log.V(Log.TagSync, "%s | %s: pullRemoteRevision.sendAsyncMultipartDownloaderRequest() calling asyncTaskStarted()"
                            , this, Sharpen.Thread.CurrentThread());
                        this._enclosing.AsyncTaskStarted();
                        // TODO: [gotRev.body compact];
                        Log.D(Log.TagSync, "%s: pullRemoteRevision add rev: %s to batcher", this, gotRev);
                        this._enclosing.downloadsToInsert.QueueObject(gotRev);
                    }
                }
                finally
                {
                    Log.V(Log.TagSync, "%s | %s: pullRemoteRevision.sendAsyncMultipartDownloaderRequest() calling asyncTaskFinished()"
                        , this, Sharpen.Thread.CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
                // Note that we've finished this task; then start another one if there
                // are still revisions waiting to be pulled:
                --this._enclosing.httpConnectionCount;
                this._enclosing.PullRemoteRevisions();
            }

            private readonly Puller _enclosing;

            private readonly RevisionInternal rev;
        }

        // Get a bunch of revisions in one bulk request. Will use _bulk_get if possible.
        protected internal void PullBulkRevisions(IList<RevisionInternal> bulkRevs)
        {
            int nRevs = bulkRevs.Count;
            if (nRevs == 0)
            {
                return;
            }
            Log.V(Log.TagSync, "%s bulk-fetching %d remote revisions...", this, nRevs);
            Log.V(Log.TagSync, "%s bulk-fetching remote revisions: %s", this, bulkRevs);
            if (!canBulkGet)
            {
                PullBulkWithAllDocs(bulkRevs);
                return;
            }
            Log.V(Log.TagSync, "%s: POST _bulk_get", this);
            IList<RevisionInternal> remainingRevs = new AList<RevisionInternal>(bulkRevs);
            Log.V(Log.TagSync, "%s | %s: pullBulkRevisions() calling asyncTaskStarted()", this
                , Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            ++httpConnectionCount;
            BulkDownloader dl;
            try
            {
                dl = new BulkDownloader(workExecutor, clientFactory, remote, bulkRevs, db, this.requestHeaders
                    , new _BulkDownloaderDocumentBlock_506(this, remainingRevs), new _RemoteRequestCompletionBlock_537
                    (this, remainingRevs));
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
            dl.SetAuthenticator(GetAuthenticator());
            remoteRequestExecutor.Execute(dl);
        }

        private sealed class _BulkDownloaderDocumentBlock_506 : BulkDownloader.BulkDownloaderDocumentBlock
        {
            public _BulkDownloaderDocumentBlock_506(Puller _enclosing, IList<RevisionInternal
                > remainingRevs)
            {
                this._enclosing = _enclosing;
                this.remainingRevs = remainingRevs;
            }

            public void OnDocument(IDictionary<string, object> props)
            {
                RevisionInternal rev;
                if (props.Get("_id") != null)
                {
                    rev = new RevisionInternal(props, this._enclosing.db);
                }
                else
                {
                    rev = new RevisionInternal((string)props.Get("id"), (string)props.Get("rev"), false
                        , this._enclosing.db);
                }
                int pos = remainingRevs.IndexOf(rev);
                if (pos > -1)
                {
                    rev.SetSequence(remainingRevs[pos].GetSequence());
                    remainingRevs.Remove(pos);
                }
                else
                {
                    Log.W(Log.TagSync, "%s : Received unexpected rev rev", this);
                }
                if (props.Get("_id") != null)
                {
                    this._enclosing.QueueDownloadedRevision(rev);
                }
                else
                {
                    Status status = this._enclosing.StatusFromBulkDocsResponseItem(props);
                    this._enclosing.error = new CouchbaseLiteException(status);
                    this._enclosing.RevisionFailed();
                    this._enclosing.completedChangesCount.GetAndIncrement();
                }
            }

            private readonly Puller _enclosing;

            private readonly IList<RevisionInternal> remainingRevs;
        }

        private sealed class _RemoteRequestCompletionBlock_537 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_537(Puller _enclosing, IList<RevisionInternal
                > remainingRevs)
            {
                this._enclosing = _enclosing;
                this.remainingRevs = remainingRevs;
            }

            public void OnCompletion(object result, Exception e)
            {
                if (e != null)
                {
                    this._enclosing.SetError(e);
                    this._enclosing.RevisionFailed();
                    this._enclosing.completedChangesCount.AddAndGet(remainingRevs.Count);
                }
                Log.V(Log.TagSync, "%s | %s: pullBulkRevisions.RemoteRequestCompletionBlock() calling asyncTaskFinished()"
                    , this, Sharpen.Thread.CurrentThread());
                this._enclosing.AsyncTaskFinished(1);
                --this._enclosing.httpConnectionCount;
                this._enclosing.PullRemoteRevisions();
            }

            private readonly Puller _enclosing;

            private readonly IList<RevisionInternal> remainingRevs;
        }

        // Get as many revisions as possible in one _all_docs request.
        // This is compatible with CouchDB, but it only works for revs of generation 1 without attachments.
        protected internal void PullBulkWithAllDocs(IList<RevisionInternal> bulkRevs)
        {
            // http://wiki.apache.org/couchdb/HTTP_Bulk_Document_API
            Log.V(Log.TagSync, "%s | %s: pullBulkWithAllDocs() calling asyncTaskStarted()", this
                , Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            ++httpConnectionCount;
            IList<RevisionInternal> remainingRevs = new AList<RevisionInternal>(bulkRevs);
            ICollection<string> keys = CollectionUtils.Transform(bulkRevs, new _Functor_578()
                );
            IDictionary<string, object> body = new Dictionary<string, object>();
            body.Put("keys", keys);
            SendAsyncRequest("POST", "/_all_docs?include_docs=true", body, new _RemoteRequestCompletionBlock_591
                (this, bulkRevs, remainingRevs));
        }

        private sealed class _Functor_578 : CollectionUtils.Functor<RevisionInternal, string
            >
        {
            public _Functor_578()
            {
            }

            public string Invoke(RevisionInternal rev)
            {
                return rev.GetDocId();
            }
        }

        private sealed class _RemoteRequestCompletionBlock_591 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_591(Puller _enclosing, IList<RevisionInternal
                > bulkRevs, IList<RevisionInternal> remainingRevs)
            {
                this._enclosing = _enclosing;
                this.bulkRevs = bulkRevs;
                this.remainingRevs = remainingRevs;
            }

            public void OnCompletion(object result, Exception e)
            {
                IDictionary<string, object> res = (IDictionary<string, object>)result;
                if (e != null)
                {
                    this._enclosing.SetError(e);
                    this._enclosing.RevisionFailed();
                    this._enclosing.completedChangesCount.AddAndGet(bulkRevs.Count);
                }
                else
                {
                    // Process the resulting rows' documents.
                    // We only add a document if it doesn't have attachments, and if its
                    // revID matches the one we asked for.
                    IList<IDictionary<string, object>> rows = (IList<IDictionary<string, object>>)res
                        .Get("rows");
                    Log.V(Log.TagSync, "%s checking %d bulk-fetched remote revisions", this, rows.Count
                        );
                    foreach (IDictionary<string, object> row in rows)
                    {
                        IDictionary<string, object> doc = (IDictionary<string, object>)row.Get("doc");
                        if (doc != null && doc.Get("_attachments") == null)
                        {
                            RevisionInternal rev = new RevisionInternal(doc, this._enclosing.db);
                            int pos = remainingRevs.IndexOf(rev);
                            if (pos > -1)
                            {
                                rev.SetSequence(remainingRevs[pos].GetSequence());
                                remainingRevs.Remove(pos);
                                this._enclosing.QueueDownloadedRevision(rev);
                            }
                        }
                    }
                }
                // Any leftover revisions that didn't get matched will be fetched individually:
                if (remainingRevs.Count > 0)
                {
                    Log.V(Log.TagSync, "%s bulk-fetch didn't work for %d of %d revs; getting individually"
                        , this, remainingRevs.Count, bulkRevs.Count);
                    foreach (RevisionInternal rev in remainingRevs)
                    {
                        this._enclosing.QueueRemoteRevision(rev);
                    }
                    this._enclosing.PullRemoteRevisions();
                }
                // Note that we've finished this task:
                Log.V(Log.TagSync, "%s | %s: pullBulkWithAllDocs() calling asyncTaskFinished()", 
                    this, Sharpen.Thread.CurrentThread());
                this._enclosing.AsyncTaskFinished(1);
                --this._enclosing.httpConnectionCount;
                // Start another task if there are still revisions waiting to be pulled:
                this._enclosing.PullRemoteRevisions();
            }

            private readonly Puller _enclosing;

            private readonly IList<RevisionInternal> bulkRevs;

            private readonly IList<RevisionInternal> remainingRevs;
        }

        // This invokes the tranformation block if one is installed and queues the resulting CBL_Revision
        private void QueueDownloadedRevision(RevisionInternal rev)
        {
            if (revisionBodyTransformationBlock != null)
            {
                // Add 'file' properties to attachments pointing to their bodies:
                foreach (KeyValuePair<string, IDictionary<string, object>> entry in ((IDictionary
                    <string, IDictionary<string, object>>)rev.GetProperties().Get("_attachments")).EntrySet
                    ())
                {
                    string name = entry.Key;
                    IDictionary<string, object> attachment = entry.Value;
                    Sharpen.Collections.Remove(attachment, "file");
                    if (attachment.Get("follows") != null && attachment.Get("data") == null)
                    {
                        string filePath = db.FileForAttachmentDict(attachment).AbsolutePath;
                        if (filePath != null)
                        {
                            attachment.Put("file", filePath);
                        }
                    }
                }
                RevisionInternal xformed = TransformRevision(rev);
                if (xformed == null)
                {
                    Log.V(Log.TagSync, "%s: Transformer rejected revision %s", this, rev);
                    pendingSequences.RemoveSequence(rev.GetSequence());
                    lastSequence = pendingSequences.GetCheckpointedValue();
                    return;
                }
                rev = xformed;
                // Clean up afterwards
                IDictionary<string, object> attachments = (IDictionary<string, object>)rev.GetProperties
                    ().Get("_attachments");
                foreach (KeyValuePair<string, IDictionary<string, object>> entry_1 in ((IDictionary
                    <string, IDictionary<string, object>>)rev.GetProperties().Get("_attachments")).EntrySet
                    ())
                {
                    IDictionary<string, object> attachment = entry_1.Value;
                    Sharpen.Collections.Remove(attachment, "file");
                }
            }
            //TODO: rev.getBody().compact();
            Log.V(Log.TagSync, "%s | %s: queueDownloadedRevision() calling asyncTaskStarted()"
                , this, Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            downloadsToInsert.QueueObject(rev);
        }

        /// <summary>This will be called when _revsToInsert fills up:</summary>
        [InterfaceAudience.Private]
        public void InsertDownloads(IList<RevisionInternal> downloads)
        {
            Log.I(Log.TagSync, this + " inserting " + downloads.Count + " revisions...");
            long time = Runtime.CurrentTimeMillis();
            downloads.Sort(GetRevisionListComparator());
            db.BeginTransaction();
            bool success = false;
            try
            {
                foreach (RevisionInternal rev in downloads)
                {
                    long fakeSequence = rev.GetSequence();
                    IList<string> history = Database.ParseCouchDBRevisionHistory(rev.GetProperties());
                    if (history.IsEmpty() && rev.GetGeneration() > 1)
                    {
                        Log.W(Log.TagSync, "%s: Missing revision history in response for: %s", this, rev);
                        SetError(new CouchbaseLiteException(Status.UpstreamError));
                        RevisionFailed();
                        continue;
                    }
                    Log.V(Log.TagSync, "%s: inserting %s %s", this, rev.GetDocId(), history);
                    // Insert the revision
                    try
                    {
                        db.ForceInsert(rev, history, remote);
                    }
                    catch (CouchbaseLiteException e)
                    {
                        if (e.GetCBLStatus().GetCode() == Status.Forbidden)
                        {
                            Log.I(Log.TagSync, "%s: Remote rev failed validation: %s", this, rev);
                        }
                        else
                        {
                            Log.W(Log.TagSync, "%s: failed to write %s: status=%s", this, rev, e.GetCBLStatus
                                ().GetCode());
                            RevisionFailed();
                            SetError(new HttpResponseException(e.GetCBLStatus().GetCode(), null));
                            continue;
                        }
                    }
                    // Mark this revision's fake sequence as processed:
                    pendingSequences.RemoveSequence(fakeSequence);
                }
                Log.V(Log.TagSync, "%s: finished inserting %d revisions", this, downloads.Count);
                success = true;
            }
            catch (SQLException e)
            {
                Log.E(Log.TagSync, this + ": Exception inserting revisions", e);
            }
            finally
            {
                db.EndTransaction(success);
                Log.D(Log.TagSync, "%s | %s: insertDownloads() calling asyncTaskFinished() with value: %d"
                    , this, Sharpen.Thread.CurrentThread(), downloads.Count);
                AsyncTaskFinished(downloads.Count);
            }
            // Checkpoint:
            SetLastSequence(pendingSequences.GetCheckpointedValue());
            long delta = Runtime.CurrentTimeMillis() - time;
            Log.V(Log.TagSync, "%s: inserted %d revs in %d milliseconds", this, downloads.Count
                , delta);
            int newCompletedChangesCount = GetCompletedChangesCount() + downloads.Count;
            Log.D(Log.TagSync, "%s insertDownloads() updating completedChangesCount from %d -> %d "
                , this, GetCompletedChangesCount(), newCompletedChangesCount);
            AddToCompletedChangesCount(downloads.Count);
        }

        [InterfaceAudience.Private]
        private IComparer<RevisionInternal> GetRevisionListComparator()
        {
            return new _IComparer_757();
        }

        private sealed class _IComparer_757 : IComparer<RevisionInternal>
        {
            public _IComparer_757()
            {
            }

            public int Compare(RevisionInternal reva, RevisionInternal revb)
            {
                return Misc.TDSequenceCompare(reva.GetSequence(), revb.GetSequence());
            }
        }

        [InterfaceAudience.Private]
        internal IList<string> KnownCurrentRevIDs(RevisionInternal rev)
        {
            if (db != null)
            {
                return db.GetAllRevisionsOfDocumentID(rev.GetDocId(), true).GetAllRevIds();
            }
            return null;
        }

        [InterfaceAudience.Private]
        public string JoinQuotedEscaped(IList<string> strings)
        {
            if (strings.Count == 0)
            {
                return "[]";
            }
            byte[] json = null;
            try
            {
                json = Manager.GetObjectMapper().WriteValueAsBytes(strings);
            }
            catch (Exception e)
            {
                Log.W(Log.TagSync, "Unable to serialize json", e);
            }
            return URLEncoder.Encode(Sharpen.Runtime.GetStringForBytes(json));
        }

        [InterfaceAudience.Public]
        public override bool GoOffline()
        {
            Log.D(Log.TagSync, "%s: goOffline() called, stopping changeTracker: %s", this, changeTracker
                );
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

    /// <summary>A revision received from a remote server during a pull.</summary>
    /// <remarks>A revision received from a remote server during a pull. Tracks the opaque remote sequence ID.
    ///     </remarks>
    internal class PulledRevision : RevisionInternal
    {
        public PulledRevision(Body body, Database database) : base(body, database)
        {
        }

        public PulledRevision(string docId, string revId, bool deleted, Database database
            ) : base(docId, revId, deleted, database)
        {
        }

        public PulledRevision(IDictionary<string, object> properties, Database database) : 
            base(properties, database)
        {
        }

        protected internal string remoteSequenceID;

        public virtual string GetRemoteSequenceID()
        {
            return remoteSequenceID;
        }

        public virtual void SetRemoteSequenceID(string remoteSequenceID)
        {
            this.remoteSequenceID = remoteSequenceID;
        }
    }
}
