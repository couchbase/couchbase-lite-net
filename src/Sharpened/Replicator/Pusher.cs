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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Apache.Http.Client;
using Apache.Http.Entity.Mime;
using Apache.Http.Entity.Mime.Content;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
    /// <exclude></exclude>
    public sealed class Pusher : Replication, Database.ChangeListener
    {
        private bool createTarget;

        private bool creatingTarget;

        private bool observing;

        private ReplicationFilter filter;

        private bool dontSendMultipart = false;

        internal ICollection<long> pendingSequences;

        internal long maxPendingSequence;

        /// <summary>Constructor</summary>
        [InterfaceAudience.Private]
        public Pusher(Database db, Uri remote, bool continuous, ScheduledExecutorService 
            workExecutor) : this(db, remote, continuous, null, workExecutor)
        {
        }

        /// <summary>Constructor</summary>
        [InterfaceAudience.Private]
        public Pusher(Database db, Uri remote, bool continuous, HttpClientFactory clientFactory
            , ScheduledExecutorService workExecutor) : base(db, remote, continuous, clientFactory
            , workExecutor)
        {
            createTarget = false;
            observing = false;
        }

        [InterfaceAudience.Public]
        public override bool IsPull()
        {
            return false;
        }

        [InterfaceAudience.Public]
        public override bool ShouldCreateTarget()
        {
            return createTarget;
        }

        [InterfaceAudience.Public]
        public override void SetCreateTarget(bool createTarget)
        {
            this.createTarget = createTarget;
        }

        [InterfaceAudience.Public]
        public override void Stop()
        {
            StopObserving();
            base.Stop();
        }

        /// <summary>Adds a local revision to the "pending" set that are awaiting upload:</summary>
        [InterfaceAudience.Private]
        private void AddPending(RevisionInternal revisionInternal)
        {
            long seq = revisionInternal.GetSequence();
            pendingSequences.AddItem(seq);
            if (seq > maxPendingSequence)
            {
                maxPendingSequence = seq;
            }
        }

        /// <summary>Removes a revision from the "pending" set after it's been uploaded.</summary>
        /// <remarks>Removes a revision from the "pending" set after it's been uploaded. Advances checkpoint.
        ///     </remarks>
        [InterfaceAudience.Private]
        private void RemovePending(RevisionInternal revisionInternal)
        {
            long seq = revisionInternal.GetSequence();
            if (pendingSequences == null || pendingSequences.Count == 0)
            {
                Log.W(Log.TagSync, "%s: removePending() called w/ rev: %s, but pendingSequences empty"
                    , this, revisionInternal);
                return;
            }
            bool wasFirst = (seq == pendingSequences.First());
            if (!pendingSequences.Contains(seq))
            {
                Log.W(Log.TagSync, "%s: removePending: sequence %s not in set, for rev %s", this, 
                    seq, revisionInternal);
            }
            pendingSequences.Remove(seq);
            if (wasFirst)
            {
                // If I removed the first pending sequence, can advance the checkpoint:
                long maxCompleted;
                if (pendingSequences.Count == 0)
                {
                    maxCompleted = maxPendingSequence;
                }
                else
                {
                    maxCompleted = pendingSequences.First();
                    --maxCompleted;
                }
                SetLastSequence(System.Convert.ToString(maxCompleted));
            }
        }

        [InterfaceAudience.Private]
        internal override void MaybeCreateRemoteDB()
        {
            if (!createTarget)
            {
                return;
            }
            creatingTarget = true;
            Log.V(Log.TagSync, "Remote db might not exist; creating it...");
            Log.V(Log.TagSync, "%s | %s: maybeCreateRemoteDB() calling asyncTaskStarted()", this
                , Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            SendAsyncRequest("PUT", string.Empty, null, new _RemoteRequestCompletionBlock_152
                (this));
        }

        private sealed class _RemoteRequestCompletionBlock_152 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_152(Pusher _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void OnCompletion(object result, Exception e)
            {
                try
                {
                    this._enclosing.creatingTarget = false;
                    if (e != null && e is HttpResponseException && ((HttpResponseException)e).GetStatusCode
                        () != 412)
                    {
                        Log.E(Log.TagSync, this + ": Failed to create remote db", e);
                        this._enclosing.SetError(e);
                        this._enclosing.Stop();
                    }
                    else
                    {
                        // this is fatal: no db to push to!
                        Log.V(Log.TagSync, "%s: Created remote db", this);
                        this._enclosing.createTarget = false;
                        this._enclosing.BeginReplicating();
                    }
                }
                finally
                {
                    Log.V(Log.TagSync, "%s | %s: maybeCreateRemoteDB.sendAsyncRequest() calling asyncTaskFinished()"
                        , this, Sharpen.Thread.CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
            }

            private readonly Pusher _enclosing;
        }

        [InterfaceAudience.Private]
        public override void BeginReplicating()
        {
            Log.D(Log.TagSync, "%s: beginReplicating() called", this);
            // If we're still waiting to create the remote db, do nothing now. (This method will be
            // re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
            if (creatingTarget)
            {
                Log.D(Log.TagSync, "%s: creatingTarget == true, doing nothing", this);
                return;
            }
            pendingSequences = Sharpen.Collections.SynchronizedSortedSet(new TreeSet<long>());
            try
            {
                maxPendingSequence = long.Parse(lastSequence);
            }
            catch (FormatException)
            {
                Log.W(Log.TagSync, "Error converting lastSequence: %s to long.  Using 0", lastSequence
                    );
                maxPendingSequence = System.Convert.ToInt64(0);
            }
            if (filterName != null)
            {
                filter = db.GetFilter(filterName);
            }
            if (filterName != null && filter == null)
            {
                Log.W(Log.TagSync, "%s: No ReplicationFilter registered for filter '%s'; ignoring"
                    , this, filterName);
            }
            // Process existing changes since the last push:
            long lastSequenceLong = 0;
            if (lastSequence != null)
            {
                lastSequenceLong = long.Parse(lastSequence);
            }
            ChangesOptions options = new ChangesOptions();
            options.SetIncludeConflicts(true);
            RevisionList changes = db.ChangesSince(lastSequenceLong, options, filter);
            if (changes.Count > 0)
            {
                batcher.QueueObjects(changes);
                batcher.Flush();
            }
            // Now listen for future changes (in continuous mode):
            if (continuous)
            {
                observing = true;
                db.AddChangeListener(this);
            }
        }

        [InterfaceAudience.Private]
        private void StopObserving()
        {
            if (observing)
            {
                observing = false;
                db.RemoveChangeListener(this);
            }
        }

        [InterfaceAudience.Private]
        public void Changed(Database.ChangeEvent @event)
        {
            IList<DocumentChange> changes = @event.GetChanges();
            foreach (DocumentChange change in changes)
            {
                // Skip revisions that originally came from the database I'm syncing to:
                Uri source = change.GetSourceUrl();
                if (source != null && source.Equals(remote))
                {
                    return;
                }
                RevisionInternal rev = change.GetAddedRevision();
                IDictionary<string, object> paramsFixMe = null;
                // TODO: these should not be null
                if (GetLocalDatabase().RunFilter(filter, paramsFixMe, rev))
                {
                    AddToInbox(rev);
                }
            }
        }

        [InterfaceAudience.Private]
        protected internal override void ProcessInbox(RevisionList changes)
        {
            // Generate a set of doc/rev IDs in the JSON format that _revs_diff wants:
            // <http://wiki.apache.org/couchdb/HttpPostRevsDiff>
            IDictionary<string, IList<string>> diffs = new Dictionary<string, IList<string>>(
                );
            foreach (RevisionInternal rev in changes)
            {
                string docID = rev.GetDocId();
                IList<string> revs = diffs.Get(docID);
                if (revs == null)
                {
                    revs = new AList<string>();
                    diffs.Put(docID, revs);
                }
                revs.AddItem(rev.GetRevId());
                AddPending(rev);
            }
            // Call _revs_diff on the target db:
            Log.V(Log.TagSync, "%s: posting to /_revs_diff", this);
            Log.V(Log.TagSync, "%s | %s: processInbox() calling asyncTaskStarted()", this, Sharpen.Thread
                .CurrentThread());
            AsyncTaskStarted();
            SendAsyncRequest("POST", "/_revs_diff", diffs, new _RemoteRequestCompletionBlock_280
                (this, changes));
        }

        private sealed class _RemoteRequestCompletionBlock_280 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_280(Pusher _enclosing, RevisionList changes)
            {
                this._enclosing = _enclosing;
                this.changes = changes;
            }

            public void OnCompletion(object response, Exception e)
            {
                try
                {
                    Log.V(Log.TagSync, "%s: got /_revs_diff response");
                    IDictionary<string, object> results = (IDictionary<string, object>)response;
                    if (e != null)
                    {
                        this._enclosing.SetError(e);
                        this._enclosing.RevisionFailed();
                    }
                    else
                    {
                        if (results.Count != 0)
                        {
                            // Go through the list of local changes again, selecting the ones the destination server
                            // said were missing and mapping them to a JSON dictionary in the form _bulk_docs wants:
                            IList<object> docsToSend = new AList<object>();
                            RevisionList revsToSend = new RevisionList();
                            foreach (RevisionInternal rev in changes)
                            {
                                // Is this revision in the server's 'missing' list?
                                IDictionary<string, object> properties = null;
                                IDictionary<string, object> revResults = (IDictionary<string, object>)results.Get
                                    (rev.GetDocId());
                                if (revResults == null)
                                {
                                    continue;
                                }
                                IList<string> revs = (IList<string>)revResults.Get("missing");
                                if (revs == null || !revs.Contains(rev.GetRevId()))
                                {
                                    this._enclosing.RemovePending(rev);
                                    continue;
                                }
                                // Get the revision's properties:
                                EnumSet<Database.TDContentOptions> contentOptions = EnumSet.Of(Database.TDContentOptions
                                    .TDIncludeAttachments);
                                if (!this._enclosing.dontSendMultipart && this._enclosing.revisionBodyTransformationBlock
                                     == null)
                                {
                                    contentOptions.AddItem(Database.TDContentOptions.TDBigAttachmentsFollow);
                                }
                                RevisionInternal loadedRev;
                                try
                                {
                                    loadedRev = this._enclosing.db.LoadRevisionBody(rev, contentOptions);
                                    properties = new Dictionary<string, object>(rev.GetProperties());
                                }
                                catch (CouchbaseLiteException)
                                {
                                    Log.W(Log.TagSync, "%s Couldn't get local contents of %s", rev, this._enclosing);
                                    this._enclosing.RevisionFailed();
                                    continue;
                                }
                                RevisionInternal populatedRev = this._enclosing.TransformRevision(loadedRev);
                                IList<string> possibleAncestors = (IList<string>)revResults.Get("possible_ancestors"
                                    );
                                properties = new Dictionary<string, object>(populatedRev.GetProperties());
                                IDictionary<string, object> revisions = this._enclosing.db.GetRevisionHistoryDictStartingFromAnyAncestor
                                    (populatedRev, possibleAncestors);
                                properties.Put("_revisions", revisions);
                                populatedRev.SetProperties(properties);
                                // Strip any attachments already known to the target db:
                                if (properties.ContainsKey("_attachments"))
                                {
                                    // Look for the latest common ancestor and stub out older attachments:
                                    int minRevPos = Couchbase.Lite.Replicator.Pusher.FindCommonAncestor(populatedRev, 
                                        possibleAncestors);
                                    Database.StubOutAttachmentsInRevBeforeRevPos(populatedRev, minRevPos + 1, false);
                                    properties = populatedRev.GetProperties();
                                    if (!this._enclosing.dontSendMultipart && this._enclosing.UploadMultipartRevision
                                        (populatedRev))
                                    {
                                        continue;
                                    }
                                }
                                if (properties == null || !properties.ContainsKey("_id"))
                                {
                                    throw new InvalidOperationException("properties must contain a document _id");
                                }
                                revsToSend.AddItem(rev);
                                docsToSend.AddItem(properties);
                            }
                            //TODO: port this code from iOS
                            // Post the revisions to the destination:
                            this._enclosing.UploadBulkDocs(docsToSend, revsToSend);
                        }
                        else
                        {
                            // None of the revisions are new to the remote
                            foreach (RevisionInternal revisionInternal in changes)
                            {
                                this._enclosing.RemovePending(revisionInternal);
                            }
                        }
                    }
                }
                finally
                {
                    Log.V(Log.TagSync, "%s | %s: processInbox.sendAsyncRequest() calling asyncTaskFinished()"
                        , this, Sharpen.Thread.CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
            }

            private readonly Pusher _enclosing;

            private readonly RevisionList changes;
        }

        /// <summary>Post the revisions to the destination.</summary>
        /// <remarks>
        /// Post the revisions to the destination. "new_edits":false means that the server should
        /// use the given _rev IDs instead of making up new ones.
        /// </remarks>
        [InterfaceAudience.Private]
        protected internal void UploadBulkDocs(IList<object> docsToSend, RevisionList changes
            )
        {
            int numDocsToSend = docsToSend.Count;
            if (numDocsToSend == 0)
            {
                return;
            }
            Log.V(Log.TagSync, "%s: POSTing " + numDocsToSend + " revisions to _bulk_docs: %s"
                , this, docsToSend);
            AddToChangesCount(numDocsToSend);
            IDictionary<string, object> bulkDocsBody = new Dictionary<string, object>();
            bulkDocsBody.Put("docs", docsToSend);
            bulkDocsBody.Put("new_edits", false);
            Log.V(Log.TagSync, "%s | %s: uploadBulkDocs() calling asyncTaskStarted()", this, 
                Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            SendAsyncRequest("POST", "/_bulk_docs", bulkDocsBody, new _RemoteRequestCompletionBlock_414
                (this, changes, numDocsToSend));
        }

        private sealed class _RemoteRequestCompletionBlock_414 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_414(Pusher _enclosing, RevisionList changes, 
                int numDocsToSend)
            {
                this._enclosing = _enclosing;
                this.changes = changes;
                this.numDocsToSend = numDocsToSend;
            }

            public void OnCompletion(object result, Exception e)
            {
                try
                {
                    if (e == null)
                    {
                        ICollection<string> failedIDs = new HashSet<string>();
                        // _bulk_docs response is really an array, not a dictionary!
                        IList<IDictionary<string, object>> items = (IList)result;
                        foreach (IDictionary<string, object> item in items)
                        {
                            Status status = this._enclosing.StatusFromBulkDocsResponseItem(item);
                            if (status.IsError())
                            {
                                // One of the docs failed to save.
                                Log.W(Log.TagSync, "%s: _bulk_docs got an error: %s", item, this);
                                // 403/Forbidden means validation failed; don't treat it as an error
                                // because I did my job in sending the revision. Other statuses are
                                // actual replication errors.
                                if (status.GetCode() != Status.Forbidden)
                                {
                                    string docID = (string)item.Get("id");
                                    failedIDs.AddItem(docID);
                                }
                            }
                        }
                        // TODO - port from iOS
                        // NSURL* url = docID ? [_remote URLByAppendingPathComponent: docID] : nil;
                        // error = CBLStatusToNSError(status, url);
                        // Remove from the pending list all the revs that didn't fail:
                        foreach (RevisionInternal revisionInternal in changes)
                        {
                            if (!failedIDs.Contains(revisionInternal.GetDocId()))
                            {
                                this._enclosing.RemovePending(revisionInternal);
                            }
                        }
                    }
                    if (e != null)
                    {
                        this._enclosing.SetError(e);
                        this._enclosing.RevisionFailed();
                    }
                    else
                    {
                        Log.V(Log.TagSync, "%s: POSTed to _bulk_docs", this._enclosing);
                    }
                    this._enclosing.AddToCompletedChangesCount(numDocsToSend);
                }
                finally
                {
                    Log.V(Log.TagSync, "%s | %s: uploadBulkDocs.sendAsyncRequest() calling asyncTaskFinished()"
                        , this, Sharpen.Thread.CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
            }

            private readonly Pusher _enclosing;

            private readonly RevisionList changes;

            private readonly int numDocsToSend;
        }

        [InterfaceAudience.Private]
        private bool UploadMultipartRevision(RevisionInternal revision)
        {
            MultipartEntity multiPart = null;
            IDictionary<string, object> revProps = revision.GetProperties();
            // TODO: refactor this to
            IDictionary<string, object> attachments = (IDictionary<string, object>)revProps.Get
                ("_attachments");
            foreach (string attachmentKey in attachments.Keys)
            {
                IDictionary<string, object> attachment = (IDictionary<string, object>)attachments
                    .Get(attachmentKey);
                if (attachment.ContainsKey("follows"))
                {
                    if (multiPart == null)
                    {
                        multiPart = new MultipartEntity();
                        try
                        {
                            string json = Manager.GetObjectMapper().WriteValueAsString(revProps);
                            Encoding utf8charset = Sharpen.Extensions.GetEncoding("UTF-8");
                            multiPart.AddPart("param1", new StringBody(json, "application/json", utf8charset)
                                );
                        }
                        catch (IOException e)
                        {
                            throw new ArgumentException(e);
                        }
                    }
                    BlobStore blobStore = this.db.GetAttachments();
                    string base64Digest = (string)attachment.Get("digest");
                    BlobKey blobKey = new BlobKey(base64Digest);
                    InputStream inputStream = blobStore.BlobStreamForKey(blobKey);
                    if (inputStream == null)
                    {
                        Log.W(Log.TagSync, "Unable to find blob file for blobKey: %s - Skipping upload of multipart revision."
                            , blobKey);
                        multiPart = null;
                    }
                    else
                    {
                        string contentType = null;
                        if (attachment.ContainsKey("content_type"))
                        {
                            contentType = (string)attachment.Get("content_type");
                        }
                        else
                        {
                            if (attachment.ContainsKey("content-type"))
                            {
                                Log.W(Log.TagSync, "Found attachment that uses content-type" + " field name instead of content_type (see couchbase-lite-android"
                                     + " issue #80): %s", attachment);
                            }
                        }
                        multiPart.AddPart(attachmentKey, new InputStreamBody(inputStream, contentType, attachmentKey
                            ));
                    }
                }
            }
            if (multiPart == null)
            {
                return false;
            }
            string path = string.Format("/%s?new_edits=false", revision.GetDocId());
            Log.D(Log.TagSync, "Uploading multipart request.  Revision: %s", revision);
            AddToChangesCount(1);
            Log.V(Log.TagSync, "%s | %s: uploadMultipartRevision() calling asyncTaskStarted()"
                , this, Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            SendAsyncMultipartRequest("PUT", path, multiPart, new _RemoteRequestCompletionBlock_542
                (this, revision));
            // Server doesn't like multipart, eh? Fall back to JSON.
            //status 415 = "bad_content_type"
            return true;
        }

        private sealed class _RemoteRequestCompletionBlock_542 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_542(Pusher _enclosing, RevisionInternal revision
                )
            {
                this._enclosing = _enclosing;
                this.revision = revision;
            }

            public void OnCompletion(object result, Exception e)
            {
                try
                {
                    if (e != null)
                    {
                        if (e is HttpResponseException)
                        {
                            if (((HttpResponseException)e).GetStatusCode() == 415)
                            {
                                this._enclosing.dontSendMultipart = true;
                                this._enclosing.UploadJsonRevision(revision);
                            }
                        }
                        else
                        {
                            Log.E(Log.TagSync, "Exception uploading multipart request", e);
                            this._enclosing.SetError(e);
                            this._enclosing.RevisionFailed();
                        }
                    }
                    else
                    {
                        Log.V(Log.TagSync, "Uploaded multipart request.");
                        this._enclosing.RemovePending(revision);
                    }
                }
                finally
                {
                    this._enclosing.AddToCompletedChangesCount(1);
                    Log.V(Log.TagSync, "%s | %s: uploadMultipartRevision() calling asyncTaskFinished()"
                        , this, Sharpen.Thread.CurrentThread());
                    this._enclosing.AsyncTaskFinished(1);
                }
            }

            private readonly Pusher _enclosing;

            private readonly RevisionInternal revision;
        }

        // Fallback to upload a revision if uploadMultipartRevision failed due to the server's rejecting
        // multipart format.
        private void UploadJsonRevision(RevisionInternal rev)
        {
            // Get the revision's properties:
            if (!db.InlineFollowingAttachmentsIn(rev))
            {
                error = new CouchbaseLiteException(Status.BadAttachment);
                RevisionFailed();
                return;
            }
            Log.V(Log.TagSync, "%s | %s: uploadJsonRevision() calling asyncTaskStarted()", this
                , Sharpen.Thread.CurrentThread());
            AsyncTaskStarted();
            string path = string.Format("/%s?new_edits=false", URIUtils.Encode(rev.GetDocId()
                ));
            SendAsyncRequest("PUT", path, rev.GetProperties(), new _RemoteRequestCompletionBlock_594
                (this, rev));
        }

        private sealed class _RemoteRequestCompletionBlock_594 : RemoteRequestCompletionBlock
        {
            public _RemoteRequestCompletionBlock_594(Pusher _enclosing, RevisionInternal rev)
            {
                this._enclosing = _enclosing;
                this.rev = rev;
            }

            public void OnCompletion(object result, Exception e)
            {
                if (e != null)
                {
                    this._enclosing.SetError(e);
                    this._enclosing.RevisionFailed();
                }
                else
                {
                    Log.V(Log.TagSync, "%s: Sent %s (JSON), response=%s", this, rev, result);
                    this._enclosing.RemovePending(rev);
                }
                Log.V(Log.TagSync, "%s | %s: uploadJsonRevision() calling asyncTaskFinished()", this
                    , Sharpen.Thread.CurrentThread());
                this._enclosing.AsyncTaskFinished(1);
            }

            private readonly Pusher _enclosing;

            private readonly RevisionInternal rev;
        }

        // Given a revision and an array of revIDs, finds the latest common ancestor revID
        // and returns its generation #. If there is none, returns 0.
        private static int FindCommonAncestor(RevisionInternal rev, IList<string> possibleRevIDs
            )
        {
            if (possibleRevIDs == null || possibleRevIDs.Count == 0)
            {
                return 0;
            }
            IList<string> history = Database.ParseCouchDBRevisionHistory(rev.GetProperties());
            //rev is missing _revisions property
            System.Diagnostics.Debug.Assert((history != null));
            bool changed = history.RetainAll(possibleRevIDs);
            string ancestorID = history.Count == 0 ? null : history[0];
            if (ancestorID == null)
            {
                return 0;
            }
            int generation = Database.ParseRevIDNumber(ancestorID);
            return generation;
        }
    }
}
