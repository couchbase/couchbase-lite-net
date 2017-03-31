// Pusher.cs
//
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Revisions;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Replicator
{
    internal sealed class Pusher : Replication
    {

        #region Constants

        private const string TAG = "Pusher";
        private const int EphemeralPurgeBatchSize = 100;
        private static readonly TimeSpan EphemeralPurgeDelay = TimeSpan.FromSeconds(1);

        #endregion

        #region Variables

        private bool _creatingTarget;
        private bool _observing;
        private bool _dontSendMultipart;
        private FilterDelegate _filter;
        private SortedDictionary<long, int> _pendingSequences;
        private long _maxPendingSequence;
        private Batcher<RevisionInternal> _purgeQueue;

        #endregion

        #region Properties

        protected override bool IsSafeToStop
        {
            get
            {
                return Batcher == null || Batcher.Count() == 0;
            }
        }

        //TODO: Why isn't this used?
        private bool CanSendCompressedRequests
        {
            get {
                return CheckServerCompatVersion("0.92");
            }
        }


        #endregion

        #region Constructors

        public Pusher(Database db, Uri remote, bool continuous, TaskFactory workExecutor) 
        : this(db, remote, continuous, null, workExecutor) { }
        
        public Pusher(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor) 
        : base(db, remote, continuous, clientFactory, workExecutor)
        {
                CreateTarget = false;
                _observing = false;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Finds the common ancestor.
        /// </summary>
        /// <remarks>
        /// Given a revision and an array of revIDs, finds the latest common ancestor revID
        /// and returns its generation #. If there is none, returns 0.
        /// </remarks>
        /// <returns>The common ancestor.</returns>
        /// <param name="rev">Rev.</param>
        /// <param name="possibleRevIDs">Possible rev I ds.</param>
        internal static int FindCommonAncestor(RevisionInternal rev, IList<RevisionID> possibleRevIDs)
        {
            if (possibleRevIDs == null || possibleRevIDs.Count == 0)
            {
                return 0;
            }

            var history = Database.ParseCouchDBRevisionHistory(rev.GetProperties());
            Debug.Assert(history != null);

            history = history.Intersect(possibleRevIDs).ToList();

            var ancestorID = history.Count == 0 
                ? null 
                : history[0];

            if (ancestorID == null)
            {
                return 0;
            }

            return ancestorID.Generation;
        }

        #endregion

        #region Private Methods

        private void StopObserving()
        {
            var localDb = LocalDatabase;
            if (_observing) {
                if (localDb != null) {
                    _observing = false;
                    localDb.Changed -= OnChanged;
                }
            }
        }

        private void OnChanged(Object sender, DatabaseChangeEventArgs args)
        {
            var changes = args.Changes;
            foreach (DocumentChange change in changes)
            {
                // Skip expired documents
                if (change.IsExpiration) {
                    return;
                }

                // Skip revisions that originally came from the database I'm syncing to:
                var source = change.SourceUrl;
                if (source != null && source.Equals(RemoteUrl)) {
                    return;
                }

                var rev = change.AddedRevision;
                if (LocalDatabase.RunFilter(_filter, FilterParams, rev)) {
                    Log.To.Sync.V(TAG, "{0} queuing {1} {2}", this, LocalDatabase.GetSequence(rev), rev);
                    AddToInbox(rev);
                }
            }
        }

        private void AddPending(RevisionInternal revisionInternal)
        {
            lock(_pendingSequences)
            {
                var seq = revisionInternal.Sequence;
                if (!_pendingSequences.ContainsKey(seq)) {
                    _pendingSequences.Add(seq, 0);
                }

                if (seq > _maxPendingSequence)
                {
                    _maxPendingSequence = seq;
                }
            }
        }

        private void RemovePending(RevisionInternal revisionInternal)
        {
            lock (_pendingSequences)
            {
                var seq = revisionInternal.Sequence;
                var wasFirst = (_pendingSequences.Count > 0 && seq == _pendingSequences.ElementAt(0).Key);
                if (!_pendingSequences.ContainsKey(seq)) {
                    Log.To.Sync.W(TAG, "Sequence {0} not in set, for rev {1}", seq, revisionInternal);
                }

                _pendingSequences.Remove(seq);
                if (wasFirst)
                {
                    // If removing the first pending sequence, can advance the checkpoint:
                    long maxCompleted;
                    if (_pendingSequences.Count == 0)
                    {
                        maxCompleted = _maxPendingSequence;
                    }
                    else
                    {
                        maxCompleted = _pendingSequences.ElementAt(0).Key;
                        --maxCompleted;
                    }
                    LastSequence = maxCompleted.ToString();
                }

                if (_purgeQueue != null) {
                    _purgeQueue.QueueObject(revisionInternal);
                }

                if (IsSafeToStop && _pendingSequences.Count == 0) {
                    FireTrigger(Continuous ? ReplicationTrigger.WaitingForChanges : ReplicationTrigger.StopGraceful);
                }
            }
        }

        private void UploadBulkDocs(IList<object> docsToSend, RevisionList revChanges)
        {
            // Post the revisions to the destination. "new_edits":false means that the server should
            // use the given _rev IDs instead of making up new ones.
            var numDocsToSend = docsToSend.Count;
            if (numDocsToSend == 0) {
                return;
            }

            Log.To.Sync.I(TAG, "{0} sending {1} revisions", this, numDocsToSend);
            Log.To.Sync.V(TAG, "{0} sending {1}", this, revChanges);

            var bulkDocsBody = new Dictionary<string, object>();
            bulkDocsBody["docs"] = docsToSend;
            bulkDocsBody["new_edits"] = false;
            SafeAddToChangesCount(numDocsToSend);

            _remoteSession.SendAsyncRequest(HttpMethod.Post, "/_bulk_docs", bulkDocsBody, (result, e) => {
                if (e == null) {
                    var failedIds = new HashSet<string>();
                    // _bulk_docs response is really an array not a dictionary
                    var items = result.AsList<object>();
                    foreach(var item in items) {
                        var itemObject = item.AsDictionary<string, object>();
                        var status = StatusFromBulkDocsResponseItem(itemObject);
                        if (!status.IsSuccessful) {
                            // One of the docs failed to save.
                            Log.To.Sync.I(TAG, "_bulk_docs got an error: " + item);

                            // 403/Forbidden means validation failed; don't treat it as an error
                            // because I did my job in sending the revision. Other statuses are
                            // actual replication errors.
                            if(status.Code != StatusCode.Forbidden) {
                                var docId = itemObject.GetCast<string>("id");
                                failedIds.Add(docId);
                            } else {
                                LastError = Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.Forbidden, TAG,
                                    $"{itemObject["id"]} was rejected by the endpoint with message: {itemObject["reason"]}");
                            }
                        }
                    }

                    // Remove from the pending list all the revs that didn't fail:
                    foreach (var revisionInternal in revChanges) {
                        if (!failedIds.Contains(revisionInternal.DocID)) {
                            RemovePending(revisionInternal);
                        }
                    }
                }

                if (e != null) {
                    LastError = e;
                    RevisionFailed();
                } else {
                    Log.To.Sync.V(TAG, "{0} sent {1}", this, revChanges);
                }

                SafeAddToCompletedChangesCount(numDocsToSend);
            });
        }

        private bool UploadMultipartRevision(RevisionInternal revision)
        {
            MultipartContent multiPart = null;
            var length = default(double);
            var revProps = revision.GetProperties();

            var attachments = revProps.Get("_attachments").AsDictionary<string,object>();
            if(attachments == null) {
                return false;
            }

            foreach (var attachmentKey in attachments.Keys) {
                var attachment = attachments.Get(attachmentKey).AsDictionary<string,object>();
                if (attachment.ContainsKey("follows")) {
                    if (multiPart == null) {
                        multiPart = new MultipartContent("related");
                        try {
                            var json = Manager.GetObjectMapper().WriteValueAsString(revProps);
                            var utf8charset = Encoding.UTF8;
                            //multiPart.Add(new StringContent(json, utf8charset, "application/json"), "param1");

                            var jsonContent = new StringContent(json, utf8charset, "application/json");
                            //jsonContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                            multiPart.Add(jsonContent);
                            length += json.Length;
                        } catch (Exception e) {
                            throw Misc.CreateExceptionAndLog(Log.To.Sync, e, TAG,
                                "Not able to serialize revision properties into a multipart request content.");
                        }
                    }

                    var blobStore = LocalDatabase.Attachments;
                    var base64Digest = (string)attachment.Get("digest");

                    var blobKey = new BlobKey(base64Digest);
                    var inputStream = blobStore.BlobStreamForKey(blobKey);

                    if (inputStream == null) {
                        Log.To.Sync.W(TAG, "Unable to find blob file for blobKey: {0} - Skipping upload of multipart revision.", blobKey);
                        multiPart = null;
                        length = 0;
                    } else {
                        string contentType = null;
                        if (attachment.ContainsKey("content_type")) {
                            contentType = (string)attachment.Get("content_type");
                        } else {
                            if (attachment.ContainsKey("content-type")) {
                                var message = string.Format("Found attachment that uses content-type"
                                              + " field name instead of content_type (see couchbase-lite-android"
                                              + " issue #80): " + attachment);
                                Log.To.Sync.W(TAG, message);
                            }
                        }

                        var content = new StreamContent(inputStream);
                        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") {
                            FileName = attachmentKey
                        };
                        if(!String.IsNullOrEmpty(contentType)) {
                            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                        }

                        multiPart.Add(content);
                        length += attachment.GetCast<double>("length");
                    }
                }
            }

            if (multiPart == null) {
                return false;
            }

            var path = string.Format("/{0}?new_edits=false", revision.DocID);

            // TODO: need to throttle these requests
            Log.To.Sync.D(TAG, "{0} uploading multipart request.  Revision: {1}", this, revision);
            SafeAddToChangesCount(1);
            _remoteSession.SendAsyncMultipartRequest(HttpMethod.Put, path, multiPart, (result, e) => 
            {
                if (e != null) {
                    var httpError = Misc.Flatten(e).FirstOrDefault(ex => ex is HttpResponseException) as HttpResponseException;
                    if (httpError != null) {
                        if(httpError.StatusCode == System.Net.HttpStatusCode.UnsupportedMediaType) {
                            _dontSendMultipart = true;
                            UploadJsonRevision(revision);
                        } else if(httpError.StatusCode == System.Net.HttpStatusCode.Forbidden) {
                            LastError = Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.Forbidden, TAG,
                                                                       $"{revision.DocID} was rejected by the endpoint with message");
                        }
                    } else {
                        LastError = e;
                        RevisionFailed();
                    }
                } else {
                    Log.To.Sync.V(TAG, "{0} sent multipart {1}", this, revision);
                    SafeIncrementCompletedChangesCount();
                    RemovePending(revision);
                }
            });

            Log.To.Sync.V(TAG, "{0} queuing revision (multipart, ~{1}kb)", this, length / 1024.0);
            return true;
        }
            
        // Uploads the revision as JSON instead of multipart.
        private void UploadJsonRevision(RevisionInternal originalRev)
        {
            // Expand all attachments inline:
            var rev = originalRev.Copy(originalRev.DocID, originalRev.RevID);
            try {
                LocalDatabase.ExpandAttachments(rev, 0, false, false);
            } catch(Exception e) {
                LastError = e;
                RevisionFailed();
                return;
            }

            var path = string.Format("/{0}?new_edits=false", Uri.EscapeUriString(rev.DocID));
            _remoteSession.SendAsyncRequest(HttpMethod.Put, path, rev.GetProperties(), (result, e) =>
            {
                if (e != null) 
                {
                    var httpError = Misc.Flatten(e).FirstOrDefault(ex => ex is HttpResponseException) as HttpResponseException;
                    if(httpError != null) {
                        LastError = Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.Forbidden, TAG,
                                                                       $"{rev.DocID} was rejected by the endpoint");
                    } else {
                        LastError = e;
                        RevisionFailed();
                    }
                } 
                else 
                {
                    Log.To.Sync.V(TAG, "{0} sent {1} (JSON), response={2}", this, rev, new LogJsonString(result));
                    SafeIncrementCompletedChangesCount();
                    RemovePending (rev);
                }
            });
        }

        private void UploadChanges(IList<RevisionInternal> changes, IDictionary<string, object> revsDiffResults)
        {

            // Go through the list of local changes again, selecting the ones the destination server
            // said were missing and mapping them to a JSON dictionary in the form _bulk_docs wants:
            var docsToSend = new List<object> ();
            var revsToSend = new RevisionList();
            IDictionary<string, object> revResults = null;
            foreach (var rev in changes) {
                // Is this revision in the server's 'missing' list?
                if (revsDiffResults != null) {
                    revResults = revsDiffResults.Get(rev.DocID).AsDictionary<string, object>(); 
                    if (revResults == null) {
                        continue;
                    }

                    var revs = revResults.Get("missing").AsList<string>();
                    if (revs == null || !revs.Any(id => id.Equals(rev.RevID.ToString()))) {
                        RemovePending(rev);
                        continue;
                    }
                }

                IDictionary<string, object> properties = null;
                RevisionInternal loadedRev;
                try {
                    loadedRev = LocalDatabase.LoadRevisionBody (rev);
                    if(loadedRev == null) {
                        throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.NotFound, TAG,
                            "Unable to load revision body");
                    }

                    properties = rev.GetProperties();
                } catch (Exception e1) {
                    Log.To.Sync.E(TAG, String.Format("Couldn't get local contents of {0}, marking revision failed",
                        rev), e1);
                    RevisionFailed();
                    continue;
                }

                if (properties.GetCast<bool> ("_removed")) {
                    RemovePending (rev);
                    continue;
                }

                var populatedRev = TransformRevision(loadedRev);
                var backTo = revResults?.Get("possible_ancestors")?.AsList<RevisionID>();

                try {
                    var history = LocalDatabase.GetRevisionHistory(populatedRev, backTo);
                    if(history == null) {
                        throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.DbError, TAG,
                            "Unable to load revision history");
                    }

                    properties["_revisions"] = TreeRevisionID.MakeRevisionHistoryDict(history);
                    populatedRev.SetPropertyForKey("_revisions", properties["_revisions"]);
                } catch(Exception e1) {
                    Log.To.Sync.E(TAG, "Error getting revision history, marking revision failed", e1);
                    RevisionFailed();
                    continue;
                }

                // Strip any attachments already known to the target db:
                if (properties.Get("_attachments") != null) {
                    // Look for the latest common ancestor and stuf out older attachments:
                    var minRevPos = FindCommonAncestor(populatedRev, backTo);
                    try {
                        LocalDatabase.ExpandAttachments(populatedRev, minRevPos + 1, !_dontSendMultipart, false);
                    } catch(Exception ex) {
                        Log.To.Sync.E(TAG, "Error expanding attachments, marking revision failed", ex);
                        RevisionFailed();
                        continue;
                    }

                    properties = populatedRev.GetProperties();
                    if (!_dontSendMultipart && UploadMultipartRevision(populatedRev)) {
                        continue;
                    }
                }

                if (properties == null || !properties.ContainsKey("_id")) {
                    throw Misc.CreateExceptionAndLog(Log.To.Sync, StatusCode.BadParam, TAG,
                        "properties must contain a document _id");
                }

                // Add the _revisions list:
                revsToSend.Add(rev);

                //now add it to the docs to send
                docsToSend.Add (properties);
            }

            UploadBulkDocs(docsToSend, revsToSend);
        }

        private void PurgeRevs(IList<RevisionInternal> revs)
        {

            Log.To.Sync.I(TAG, "Purging {0} docs ('purgePushed' option)", revs.Count);
            var toPurge = new Dictionary<string, IList<string>>();
            foreach(var rev in revs) {
                toPurge[rev.DocID] = new List<string> { rev.RevID.ToString() };
            }

            var localDb = LocalDatabase;
            if(localDb != null && localDb.IsOpen) {
                var storage = localDb.Storage;
                if(storage != null && storage.IsOpen) {
                    storage.PurgeRevisions(toPurge);
                } else {
                    Log.To.Sync.W(TAG, "{0} storage is closed, cannot purge...", localDb);
                }
            } else {
                Log.To.Sync.W(TAG, "Local database is closed or null, cannot purge...");
            }
        }

        #endregion

        #region Overrides

        public override IEnumerable<string> DocIds { get; set; }

        public override IDictionary<string, string> Headers {
            get {
                return _remoteSession.RequestHeaders;
            }
            set {
                _remoteSession.RequestHeaders = value;
            }
        }

        public override Boolean CreateTarget { get; set; }

        public override bool IsPull { get { return false; } }

        public override bool IsAttachmentPull { get { return false; } }

        protected internal override void MaybeCreateRemoteDB()
        {
            if (!CreateTarget) {
                return;
            }

            _creatingTarget = true;
            Log.To.Sync.I(TAG, "{0} remote db might not exist; creating it...", this);

            _remoteSession.SendAsyncRequest(HttpMethod.Put, String.Empty, null, (result, e) =>
            {
                _creatingTarget = false;
                if (e is HttpResponseException && ((HttpResponseException)e).StatusCode.GetStatusCode() != StatusCode.PreconditionFailed) {
                    
                    Log.To.Sync.I(TAG, String.Format("{0} failed to create remote db", this), e);
                    LastError = e;
                    Stop(); // this is fatal: no db to push to!
                } else {
                    Log.To.Sync.I(TAG, "{0} created remote db", this);
                    CreateTarget = false;
                    BeginReplicating();
                }
            });
        }

        internal override void BeginReplicating()
        {
            // If we're still waiting to create the remote db, do nothing now. (This method will be
            // re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
            if (_creatingTarget) {
                Log.To.Sync.D(TAG, "creatingTarget == true, doing nothing");
                return;
            }

            _pendingSequences = new SortedDictionary<long, int>();
            if (!Int64.TryParse(LastSequence, out _maxPendingSequence)) {
                Log.To.Sync.W(TAG, "{0} is not a valid last sequence, using 0", LastSequence);
                _maxPendingSequence = 0;
            }

            if (Filter != null) {
                _filter = LocalDatabase.GetFilter(Filter);
            } else {
                // If not filter function was provided, but DocIds were
                // specified, then only push the documents listed in the
                // DocIds property. It is assumed that if the users
                // specified both a filter name and doc ids that their
                // custom filter function will handle that. This is 
                // consistent with the iOS behavior.
                if (DocIds != null && DocIds.Any()) {
                    _filter = (rev, filterParams) => DocIds.Contains(rev.Document.Id);
                }
            }

            if (Filter != null && _filter == null) {
                Log.To.Sync.W(TAG, "{0}: No ReplicationFilter registered for filter '{1}'; ignoring", this, Filter);
            }

            // Process existing changes since the last push:
            long lastSequenceLong = 0;
            if (LastSequence != null) {
                lastSequenceLong = long.Parse(LastSequence);
            }
                
            if (ReplicationOptions.PurgePushed) {
                _purgeQueue = new Batcher<RevisionInternal>(new BatcherOptions<RevisionInternal> {
                    WorkExecutor = WorkExecutor,
                    Capacity = EphemeralPurgeBatchSize,
                    Delay = EphemeralPurgeDelay,
                    Processor = PurgeRevs,
                    TokenSource = CancellationTokenSource
                });
            }

            // Now listen for future changes (in continuous mode):
            // Note:  This needs to happen before adding the observer
            // or else there is a race condition.  
            // A document could be added between the call to
            // ChangesSince and adding the observer, which would result
            // in a document being skipped
            if (Continuous) {
                _observing = true;
                LocalDatabase.Changed += OnChanged;
            } 

            var options = ChangesOptions.Default;
            options.IncludeConflicts = true;
            var changes = LocalDatabase.ChangesSinceStreaming(lastSequenceLong, options, _filter, FilterParams);
            bool hasItems = changes.Any();
            foreach(var change in changes) {
                Batcher.QueueObject(change);
                if(Status == ReplicationStatus.Stopped) {
                    Batcher.Clear();
                    return;
                }
            }

            if (hasItems) {
                Batcher.Flush();
            }

            if (Continuous) {
                if (!hasItems) {
                    Log.To.Sync.V(TAG, "No changes to push, switching to idle...");
                    FireTrigger(ReplicationTrigger.WaitingForChanges);
                }
            } else {
                if(!hasItems) {
                    Log.To.Sync.V(TAG, "No changes to push, firing StopGraceful...");
                    FireTrigger(ReplicationTrigger.StopGraceful);
                }
            }
        }

        protected override void StopGraceful()
        {
            StopObserving();
            if (_purgeQueue != null) {
                _purgeQueue.FlushAll();
            }

            base.StopGraceful();
        }

        internal override void Stopping ()
        {
            StopObserving (); // Just in case
            base.Stopping ();
        }

        protected override void PerformGoOnline()
        {
            base.PerformGoOnline();

            Login();
        }

        protected override void PerformGoOffline()
        {
            base.PerformGoOffline();
            StopObserving();
        }

        internal override void ProcessInbox(RevisionList inbox)
        {
            if (Status == ReplicationStatus.Offline) {
                Log.To.Sync.I(TAG, "Offline, so skipping inbox process");
                return;
            }
                
            if (ReplicationOptions.AllNew) {
                // If 'allNew' option is set, upload new revs without checking first:
                foreach (var rev in inbox) {
                    AddPending(rev);
                }

                UploadChanges(inbox, null);
                return;
            }

            // Generate a set of doc/rev IDs in the JSON format that _revs_diff wants:
            // <http://wiki.apache.org/couchdb/HttpPostRevsDiff>
            var diffs = new Dictionary<String, IList<String>>();
            var inboxCount = inbox.Count;
            foreach (var rev in inbox) {
                var docID = rev.DocID;
                var revs = diffs.Get(docID);
                if (revs == null) {
                    revs = new List<String>();
                    diffs[docID] = revs;
                }
                revs.Add(rev.RevID.ToString());
                AddPending(rev);
            }

            // Call _revs_diff on the target db:
            Log.To.Sync.D(TAG, "posting to /_revs_diff: {0}", String.Join(Environment.NewLine, new[] { Manager.GetObjectMapper().WriteValueAsString(diffs) }));
            _remoteSession.SendAsyncRequest(HttpMethod.Post, "/_revs_diff", diffs, (response, e) =>
            {
                try {
                    if(!LocalDatabase.IsOpen) {
                        return;
                    }

                    var results = response.AsDictionary<string, object>();

                    Log.To.Sync.D(TAG, "/_revs_diff response: {0}\r\n{1}", response, results);

                    if (e != null) {
                        LastError = e;
                        for(int i = 0; i < inboxCount; i++) {
                            RevisionFailed();
                        }

                        if(Continuous) {
                            FireTrigger(ReplicationTrigger.WaitingForChanges);
                        } else {
                            FireTrigger(ReplicationTrigger.StopImmediate);
                        }
                    } else {
                        if (results.Count != 0)  {
                            UploadChanges(inbox, results);
                        } else {
                            foreach (var revisionInternal in inbox) {
                                RemovePending(revisionInternal);
                            }

                            //SafeAddToCompletedChangesCount(inbox.Count);
                        }
                    }
                } catch (Exception ex) {
                    Log.To.Sync.E(TAG, "Unhandled exception in Pusher.ProcessInbox, continuing...", ex);
                }
            });
        }

        public override string ToString()
        {
            return String.Format("Pusher {0}", ReplicatorID);
        }

        #endregion
    }
}