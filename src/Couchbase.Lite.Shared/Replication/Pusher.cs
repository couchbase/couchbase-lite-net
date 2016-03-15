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
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Replicator
{
    internal sealed class Pusher : Replication
    {

        #region Constants

        private const string TAG = "Pusher";

        #endregion

        #region Variables

        private bool _creatingTarget;
        private bool _observing;
        private bool _dontSendMultipart;
        private FilterDelegate _filter;
        private SortedDictionary<long, int> _pendingSequences;
        private long _maxPendingSequence;

        #endregion

        #region Properties

        protected override bool IsSafeToStop
        {
            get
            {
                return Batcher == null || Batcher.Count() == 0;
            }
        }

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
        internal static int FindCommonAncestor(RevisionInternal rev, IList<string> possibleRevIDs)
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

            var parsed = RevisionInternal.ParseRevId(ancestorID);
            return parsed.Item1;
        }

        #endregion

        #region Private Methods

        private MultipartWriter GetMultipartWriter(RevisionInternal rev, string boundary)
        {
            // Find all the attachments with "follows" instead of a body, and put 'em in a multipart stream.
            // It's important to scan the _attachments entries in the same order in which they will appear
            // in the JSON, because CouchDB expects the MIME bodies to appear in that same order
            var bodyStream = default(MultipartWriter);
            var attachments = rev.GetAttachments();
            foreach (var a in attachments) {
                var attachment = a.Value.AsDictionary<string, object>();
                if (attachment != null && attachment.GetCast<bool>("follows")) {
                    if (bodyStream == null) {
                        // Create the HTTP multipart stream:
                        bodyStream = new MultipartWriter("multipart/related", boundary);
                        bodyStream.SetNextPartHeaders(new Dictionary<string, string> { 
                            { "Content-Type", "application/json" } 
                        });

                        // Use canonical JSON encoder so that _attachments keys will be written in the
                        // same order that this for loop is processing the attachments.
                        var json = Manager.GetObjectMapper().WriteValueAsBytes(rev.GetProperties(), true);
                        if (CanSendCompressedRequests) {
                            bodyStream.AddGZippedData(json);
                        } else {
                            bodyStream.AddData(json);
                        }
                    }

                    // Add attachment as another MIME part:
                    var disposition = String.Format("attachment; filename={0}", Misc.QuoteString(a.Key));
                    var contentType = attachment.GetCast<string>("type");
                    var contentEncoding = attachment.GetCast<string>("encoding");
                    bodyStream.SetNextPartHeaders(new NonNullDictionary<string, string> {
                        { "Content-Disposition", disposition },
                        { "Content-Type", contentType },
                        { "Content-Encoding", contentEncoding }
                    });

                    var attachmentObj = default(AttachmentInternal);
                    try {
                        attachmentObj = LocalDatabase.AttachmentForDict(attachment, a.Key);
                    } catch(CouchbaseLiteException) {
                        return null;
                    }

                    bodyStream.AddStream(attachmentObj.ContentStream, attachmentObj.Length);
                }
            }

            return bodyStream;
        }


        private void StopObserving()
        {
            var localDb = LocalDatabase;
            if (_observing) {
                _observing = false;
                if (localDb != null) {
                    localDb.Changed -= OnChanged;
                }
            }
        }

        private void OnChanged(Object sender, DatabaseChangeEventArgs args)
        {
            var changes = args.Changes;
            foreach (DocumentChange change in changes)
            {
                // Skip revisions that originally came from the database I'm syncing to:
                var source = change.SourceUrl;
                if (source != null && source.Equals(RemoteUrl)) {
                    return;
                }

                var rev = change.AddedRevision;
                if (LocalDatabase.RunFilter(_filter, FilterParams, rev)) {
                    AddToInbox(rev);
                }
            }
        }

        private void AddPending(RevisionInternal revisionInternal)
        {
            lock(_pendingSequences)
            {
                var seq = revisionInternal.GetSequence();
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
                var seq = revisionInternal.GetSequence();
                var wasFirst = (_pendingSequences.Count > 0 && seq == _pendingSequences.ElementAt(0).Key);
                if (!_pendingSequences.ContainsKey(seq))
                {
                    Log.W(TAG, "Remove Pending: Sequence " + seq + " not in set, for rev " + revisionInternal);
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
            if (numDocsToSend == 0)
            {
                return;
            }

            Log.V(TAG, string.Format("{0}: POSTing " + numDocsToSend + " revisions to _bulk_docs: {1}", this, docsToSend));

            var bulkDocsBody = new Dictionary<string, object>();
            bulkDocsBody["docs"] = docsToSend;
            bulkDocsBody["new_edits"] = false;
            SafeAddToChangesCount(numDocsToSend);

            SendAsyncRequest(HttpMethod.Post, "/_bulk_docs", bulkDocsBody, (result, e) => {
                if (e == null)
                {
                    var failedIds = new HashSet<string>();
                    // _bulk_docs response is really an array not a dictionary
                    var items = result.AsList<object>();
                    foreach(var item in items)
                    {
                        var itemObject = item.AsDictionary<string, object>();
                        var status = StatusFromBulkDocsResponseItem(itemObject);
                        if (!status.IsSuccessful)
                        {
                            // One of the docs failed to save.
                            Log.W(TAG, "_bulk_docs got an error: " + item);

                            // 403/Forbidden means validation failed; don't treat it as an error
                            // because I did my job in sending the revision. Other statuses are
                            // actual replication errors.
                            if (status.Code != StatusCode.Forbidden)
                            {
                                var docId = itemObject.GetCast<string>("id");
                                failedIds.Add(docId);
                            }
                        }
                    }

                    // Remove from the pending list all the revs that didn't fail:
                    foreach (var revisionInternal in revChanges)
                    {
                        if (!failedIds.Contains(revisionInternal.GetDocId()))
                        {
                            RemovePending(revisionInternal);
                        }
                    }
                }

                if (e != null) 
                {
                    LastError = e;
                    RevisionFailed();
                } 
                else 
                {
                    Log.V(TAG, string.Format("POSTed to _bulk_docs: {0}", docsToSend));
                }
                SafeAddToCompletedChangesCount(numDocsToSend);
            });
        }

        private bool UploadMultipartRevision(RevisionInternal revision)
        {
            MultipartContent multiPart = null;
            var revProps = revision.GetProperties();

            var attachments = revProps.Get("_attachments").AsDictionary<string,object>();
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
                        } catch (IOException e) {
                            throw new ArgumentException("Not able to serialize revision properties into a multipart request content.", e);
                        }
                    }

                    var blobStore = LocalDatabase.Attachments;
                    var base64Digest = (string)attachment.Get("digest");

                    var blobKey = new BlobKey(base64Digest);
                    var inputStream = blobStore.BlobStreamForKey(blobKey);

                    if (inputStream == null) {
                        Log.W(TAG, "Unable to find blob file for blobKey: " + blobKey + " - Skipping upload of multipart revision.");
                        multiPart = null;
                    } else {
                        string contentType = null;
                        if (attachment.ContainsKey("content_type")) {
                            contentType = (string)attachment.Get("content_type");
                        } else {
                            if (attachment.ContainsKey("content-type")) {
                                var message = string.Format("Found attachment that uses content-type"
                                              + " field name instead of content_type (see couchbase-lite-android"
                                              + " issue #80): " + attachment);
                                Log.W(TAG, message);
                            }
                        }

                        var content = new StreamContent(inputStream);
                        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") {
                            FileName = attachmentKey
                        };
                        content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");

                        multiPart.Add(content);
                    }
                }
            }

            if (multiPart == null) {
                return false;
            }

            var path = string.Format("/{0}?new_edits=false", revision.GetDocId());

            // TODO: need to throttle these requests
            Log.D(TAG, "Uploading multipart request.  Revision: " + revision);
            SafeAddToChangesCount(1);
            SendAsyncMultipartRequest(HttpMethod.Put, path, multiPart, (result, e) => 
            {
                if (e != null) {
                    var httpError = e as HttpResponseException;
                    if (httpError != null) {
                        if (httpError.StatusCode == System.Net.HttpStatusCode.UnsupportedMediaType) {
                            _dontSendMultipart = true;
                            UploadJsonRevision(revision);
                        }
                    } else {
                        Log.E (TAG, "Exception uploading multipart request", e);
                        LastError = e;
                        RevisionFailed();
                    }
                } else {
                    Log.D (TAG, "Uploaded multipart request.  Result: " + result);
                    SafeIncrementCompletedChangesCount();
                    RemovePending(revision);
                }
            });

            return true;
        }
            
        // Uploads the revision as JSON instead of multipart.
        private void UploadJsonRevision(RevisionInternal originalRev)
        {
            // Expand all attachments inline:
            var rev = originalRev.CopyWithDocID(originalRev.GetDocId(), originalRev.GetRevId());
            try {
                LocalDatabase.ExpandAttachments(rev, 0, false, false);
            } catch(Exception e) {
                LastError = e;
                RevisionFailed();
                return;
            }

            var path = string.Format("/{0}?new_edits=false", Uri.EscapeUriString(rev.GetDocId()));
            SendAsyncRequest(HttpMethod.Put, path, rev.GetProperties(), (result, e) =>
            {
                if (e != null) 
                {
                    LastError = e;
                    RevisionFailed();
                } 
                else 
                {
                    Log.V(TAG, "Sent {0} (JSON), response={1}", rev, result);
                    SafeIncrementCompletedChangesCount();
                    RemovePending (rev);
                }
            });
        }

        #endregion

        #region Overrides

        public override IEnumerable<String> DocIds { get; set; }

        public override IDictionary<String, String> Headers { get; set; }

        public override Boolean CreateTarget { get; set; }

        public override bool IsPull { get { return false; } }

        protected internal override void MaybeCreateRemoteDB()
        {
            if (!CreateTarget)
            {
                return;
            }

            _creatingTarget = true;

            Log.V(TAG, "Remote db might not exist; creating it...");

            SendAsyncRequest(HttpMethod.Put, String.Empty, null, (result, e) =>
            {
                _creatingTarget = false;
                if (e is HttpResponseException && ((HttpResponseException)e).StatusCode.GetStatusCode() != StatusCode.PreconditionFailed)
                {
                    // this is fatal: no db to push to!
                    Log.E(TAG, "Failed to create remote db", e);
                    LastError = e;
                    Stop();
                }
                else
                {
                    Log.V(TAG, "Created remote db");
                    CreateTarget = false;
                    BeginReplicating();
                }
            });
        }

        internal override void BeginReplicating()
        {
            Log.D(TAG, "beginReplicating() called");

            // If we're still waiting to create the remote db, do nothing now. (This method will be
            // re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
            if (_creatingTarget) {
                Log.D(TAG, "creatingTarget == true, doing nothing");
                return;
            }

            _pendingSequences = new SortedDictionary<long, int>();
            try {
                _maxPendingSequence = Int64.Parse(LastSequence);
            } catch (Exception e) {
                Log.W(TAG, "Error converting lastSequence: " + LastSequence + " to long. Using 0", e);
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
                Log.W(TAG, string.Format("{0}: No ReplicationFilter registered for filter '{1}'; ignoring", this, Filter));
            }

            // Process existing changes since the last push:
            long lastSequenceLong = 0;
            if (LastSequence != null) {
                lastSequenceLong = long.Parse(LastSequence);
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
            var changes = LocalDatabase.ChangesSince(lastSequenceLong, options, _filter, FilterParams);
            if (changes.Count > 0) {
                Batcher.QueueObjects(changes);
                Batcher.Flush();
            }

            if (Continuous) {
                if (changes.Count == 0) {
                    FireTrigger(ReplicationTrigger.WaitingForChanges);
                }
            } else {
                if(changes.Count == 0) {
                    FireTrigger(ReplicationTrigger.StopGraceful);
                }
            }
        }

        protected override void StopGraceful()
        {
            StopObserving();
            base.StopGraceful();
        }

        protected override void PerformGoOnline()
        {
            base.PerformGoOnline();

            CheckSession();
        }

        protected override void PerformGoOffline()
        {
            base.PerformGoOffline();
            StopObserving();
        }

        internal override void ProcessInbox(RevisionList inbox)
        {
            if (Status == ReplicationStatus.Offline) {
                Log.V(TAG, "Offline, so skipping inbox process");
                return;
            }

            if(_requests.Count > ManagerOptions.Default.MaxOpenHttpConnections) {
                Task.Delay(1000).ContinueWith(t => ProcessInbox(inbox), CancellationToken.None, TaskContinuationOptions.None, WorkExecutor.Scheduler);
                return;
            }

            // Generate a set of doc/rev IDs in the JSON format that _revs_diff wants:
            // <http://wiki.apache.org/couchdb/HttpPostRevsDiff>
            var diffs = new Dictionary<String, IList<String>>();
            var inboxCount = inbox.Count;
            foreach (var rev in inbox) {
                var docID = rev.GetDocId();
                var revs = diffs.Get(docID);
                if (revs == null) {
                    revs = new List<String>();
                    diffs[docID] = revs;
                }
                revs.Add(rev.GetRevId());
                AddPending(rev);
            }

            // Call _revs_diff on the target db:
            Log.D(TAG, "posting to /_revs_diff: {0}", String.Join(Environment.NewLine, new[] { Manager.GetObjectMapper().WriteValueAsString(diffs) }));
            SendAsyncRequest(HttpMethod.Post, "/_revs_diff", diffs, (response, e) =>
            {
                try {
                    if(!LocalDatabase.IsOpen) {
                        return;
                    }

                    var results = response.AsDictionary<string, object>();

                    Log.D(TAG, "/_revs_diff response: {0}\r\n{1}", response, results);

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
                            // Go through the list of local changes again, selecting the ones the destination server
                            // said were missing and mapping them to a JSON dictionary in the form _bulk_docs wants:
                            var docsToSend = new List<object> ();
                            var revsToSend = new RevisionList();
                            foreach (var rev in inbox) {
                                // Is this revision in the server's 'missing' list?
                                IDictionary<string, object> properties = null;
                                var revResults = results.Get(rev.GetDocId()).AsDictionary<string, object>(); 
                                if (revResults == null) {
                                    //SafeIncrementCompletedChangesCount();
                                    continue;
                                }

                                var revs = revResults.Get("missing").AsList<string>();
                                if (revs == null || !revs.Any( id => id.Equals(rev.GetRevId(), StringComparison.OrdinalIgnoreCase))) {
                                    RemovePending(rev);
                                    //SafeIncrementCompletedChangesCount();
                                    continue;
                                }

                                // Get the revision's properties:
                                var contentOptions = DocumentContentOptions.IncludeAttachments;
                                if (!_dontSendMultipart && RevisionBodyTransformationFunction == null)
                                {
                                    contentOptions |= DocumentContentOptions.BigAttachmentsFollow;
                                }

                                RevisionInternal loadedRev;
                                try {
                                    loadedRev = LocalDatabase.LoadRevisionBody (rev);
                                    if(loadedRev == null) {
                                        throw new CouchbaseLiteException("DB is closed", StatusCode.DbError);
                                    }

                                    properties = new Dictionary<string, object>(rev.GetProperties());
                                } catch (Exception e1) {
                                    Log.W(TAG, String.Format("{0} Couldn't get local contents of", rev), e1);
                                    RevisionFailed();
                                    continue;
                                }

                                var populatedRev = TransformRevision(loadedRev);
                                IList<string> possibleAncestors = null;
                                if (revResults.ContainsKey("possible_ancestors")) {
                                    possibleAncestors = revResults["possible_ancestors"].AsList<string>();
                                }

                                properties = new Dictionary<string, object>(populatedRev.GetProperties());

                                try {
                                    var history = LocalDatabase.GetRevisionHistory(populatedRev, possibleAncestors);
                                    if(history == null) {
                                        throw new CouchbaseLiteException("DB closed", StatusCode.DbError);
                                    }

                                    properties["_revisions"] = Database.MakeRevisionHistoryDict(history);
                                } catch(Exception e1) {
                                    Log.W(TAG, "Error getting revision history", e1);
                                    RevisionFailed();
                                    continue;
                                }

                                populatedRev.SetProperties(properties);
                                if(properties.GetCast<bool>("_removed")) {
                                    RemovePending(rev);
                                    continue;
                                }

                                // Strip any attachments already known to the target db:
                                if (properties.ContainsKey("_attachments")) {
                                    // Look for the latest common ancestor and stuf out older attachments:
                                    var minRevPos = FindCommonAncestor(populatedRev, possibleAncestors);
                                    try {
                                        LocalDatabase.ExpandAttachments(populatedRev, minRevPos + 1, !_dontSendMultipart, false);
                                    } catch(Exception ex) {
                                        Log.W(TAG, "Error expanding attachments!", ex);
                                        RevisionFailed();
                                        continue;
                                    }

                                    properties = populatedRev.GetProperties();
                                    if (!_dontSendMultipart && UploadMultipartRevision(populatedRev)) {
                                        continue;
                                    }
                                }

                                if (properties == null || !properties.ContainsKey("_id")) {
                                    throw new InvalidOperationException("properties must contain a document _id");
                                }

                                // Add the _revisions list:
                                revsToSend.Add(rev);

                                //now add it to the docs to send
                                docsToSend.Add (properties);
                            }

                            UploadBulkDocs(docsToSend, revsToSend);
                        } else {
                            foreach (var revisionInternal in inbox) {
                                RemovePending(revisionInternal);
                            }

                            SafeAddToCompletedChangesCount(inbox.Count);
                        }
                    }
                } catch (Exception ex) {
                    Log.E(TAG, "Unhandled exception in Pusher.ProcessInbox", ex);
                }
            });
        }

        #endregion
    }
}