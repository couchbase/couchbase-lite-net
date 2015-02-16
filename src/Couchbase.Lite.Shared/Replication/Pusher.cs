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
using System.IO;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Diagnostics;

namespace Couchbase.Lite.Replicator
{
    internal sealed class Pusher : Replication
    {
        const string Tag = "Pusher";

        private bool creatingTarget;

        private bool observing;

        private bool dontSendMultipart;

        private FilterDelegate filter;

        private SortedSet<long> pendingSequences;

        private long maxPendingSequence;

        /// <summary>Constructor</summary>
        public Pusher(Database db, Uri remote, bool continuous, TaskFactory workExecutor) 
        : this(db, remote, continuous, null, workExecutor) { }

        /// <summary>Constructor</summary>
        public Pusher(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor) 
        : base(db, remote, continuous, clientFactory, workExecutor)
        {
                CreateTarget = false;
                observing = false;
        }

        #region implemented abstract members of Replication

        public override IEnumerable<String> DocIds { get; set; }

        public override IDictionary<String, String> Headers { get; set; }

        public override Boolean CreateTarget { get; set; }

        public override bool IsPull { get { return false; } }

        #endregion

        protected internal override void MaybeCreateRemoteDB()
        {
            if (!CreateTarget)
            {
                return;
            }
				
            creatingTarget = true;

            Log.V(Tag, "Remote db might not exist; creating it...");
            Log.D(Tag, "maybeCreateRemoteDB() calling asyncTaskStarted()");

			AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Put, String.Empty, null, (result, e) =>
            {
                try
                {
                    creatingTarget = false;
                    if (e != null && e is HttpResponseException && ((HttpResponseException)e).StatusCode.GetStatusCode() != StatusCode.PreconditionFailed)
                    {
                        // this is fatal: no db to push to!
                        Log.E(Tag, "Failed to create remote db", e);
                        SetLastError(e);
                        Stop();
                    }
                    else
                    {
                        Log.V(Tag, "Created remote db");
                        CreateTarget = false;
                        BeginReplicating();
                    }
                }
                finally
                {
                    Log.D(Tag, "maybeCreateRemoteDB.onComplete() calling asyncTaskFinished()");
                    AsyncTaskFinished(1);
                }
            });
        }

        internal override void BeginReplicating()
        {
            Log.D(Tag, "beginReplicating() called");

            // If we're still waiting to create the remote db, do nothing now. (This method will be
            // re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
            if (creatingTarget)
            {
                Log.D(Tag, "creatingTarget == true, doing nothing");
                return;
            }

            pendingSequences = new SortedSet<long>();
            try
            {
                maxPendingSequence = Int64.Parse(LastSequence);
            }
            catch (Exception e)
            {
                Log.W(Tag, "Error converting lastSequence: " + LastSequence + " to long. Using 0");
                maxPendingSequence = 0;
            }

            if (Filter != null)
            {
                filter = LocalDatabase.GetFilter(Filter);
            }

            if (Filter != null && filter == null)
            {
                Log.W(Tag, string.Format("{0}: No ReplicationFilter registered for filter '{1}'; ignoring"
                    , this, Filter));
            }

            // Process existing changes since the last push:
            long lastSequenceLong = 0;
            if (LastSequence != null)
            {
                lastSequenceLong = long.Parse(LastSequence);
            }

            var options = new ChangesOptions();
            options.SetIncludeConflicts(true);
            var changes = LocalDatabase.ChangesSince(lastSequenceLong, options, filter);
            if (changes.Count > 0)
            {
                Batcher.QueueObjects(changes);
                Batcher.Flush();
            }

            // Now listen for future changes (in continuous mode):
            if (continuous)
            {
                observing = true;
                LocalDatabase.Changed += OnChanged;
            }
        }

        // prevents stopped() from being called when other tasks finish
        public override void Stop()
        {
            StopObserving();
            base.Stop();
        }

        private void StopObserving()
        {
            if (observing)
            {
                observing = false;
                LocalDatabase.Changed -= OnChanged;
            }
        }

        internal void OnChanged(Object sender, DatabaseChangeEventArgs args)
        {
            var changes = args.Changes;
            foreach (DocumentChange change in changes)
            {
                // Skip revisions that originally came from the database I'm syncing to:
                var source = change.SourceUrl;
                if (source != null && source.Equals(RemoteUrl))
                {
                    return;
                }

                var rev = change.AddedRevision;
                IDictionary<String, Object> paramsFixMe = null;

                // TODO: these should not be null
                if (LocalDatabase.RunFilter(filter, paramsFixMe, rev))
                {
                    AddToInbox(rev);
                }
            }
        }

        private void AddPending(RevisionInternal revisionInternal)
        {
            lock(pendingSequences)
            {
                var seq = revisionInternal.GetSequence();
                pendingSequences.Add(seq);
                if (seq > maxPendingSequence)
                {
                    maxPendingSequence = seq;
                }
            }
        }

        private void RemovePending(RevisionInternal revisionInternal)
        {
            lock (pendingSequences)
            {
                var seq = revisionInternal.GetSequence();
                var wasFirst = (seq == pendingSequences.FirstOrDefault());
                if (!pendingSequences.Contains(seq))
                {
                    Log.W(Tag, "Remove Pending: Sequence " + seq + " not in set, for rev " + revisionInternal);
                }

                pendingSequences.Remove(seq);
                if (wasFirst)
                {
                    // If removing the first pending sequence, can advance the checkpoint:
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
                    LastSequence = maxCompleted.ToString();
                }
            }
        }

        internal override void ProcessInbox(RevisionList inbox)
        {
            if (!online)
            {
                Log.V(Tag, "Offline, so skipping inbox process");
                return;
            }

            // Generate a set of doc/rev IDs in the JSON format that _revs_diff wants:
            // <http://wiki.apache.org/couchdb/HttpPostRevsDiff>
            var diffs = new Dictionary<String, IList<String>>();
            foreach (var rev in inbox)
            {
                var docID = rev.GetDocId();
                var revs = diffs.Get(docID);
                if (revs == null)
                {
                    revs = new List<String>();
                    diffs[docID] = revs;
                }
                revs.AddItem(rev.GetRevId());
                AddPending(rev);
            }

            // Call _revs_diff on the target db:
            Log.D(Tag, "processInbox() calling asyncTaskStarted()");
            Log.D(Tag, "posting to /_revs_diff: {0}", String.Join(Environment.NewLine, Manager.GetObjectMapper().WriteValueAsString(diffs)));

            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Post, "/_revs_diff", diffs, (response, e) =>
            {
                try {
                    var results = response.AsDictionary<string, object>();

                    Log.D(Tag, "/_revs_diff response: {0}\r\n{1}", response, results);

                    if (e != null) 
                    {
                        SetLastError(e);
                        RevisionFailed();
                    } else {
                        if (results.Count != 0) 
                        {
                            // Go through the list of local changes again, selecting the ones the destination server
                            // said were missing and mapping them to a JSON dictionary in the form _bulk_docs wants:
                            var docsToSend = new List<object> ();
                            var revsToSend = new RevisionList();
                            foreach (var rev in inbox)
                            {
                                // Is this revision in the server's 'missing' list?
                                IDictionary<string, object> properties = null;
                                var revResults = results.Get(rev.GetDocId()).AsDictionary<string, object>();

                                if (revResults == null)
                                {
                                    continue;
                                }

                                var revs = ((JArray)revResults.Get("missing")).Values<String>().ToList();
								if (revs == null || !revs.Any( id => id.Equals(rev.GetRevId(), StringComparison.OrdinalIgnoreCase)))
                                {
                                    RemovePending(rev);
                                    continue;
                                }

                                // Get the revision's properties:
                                var contentOptions = DocumentContentOptions.IncludeAttachments;

                                if (!dontSendMultipart && revisionBodyTransformationFunction == null)
                                {
                                    contentOptions |= DocumentContentOptions.BigAttachmentsFollow;
                                }


                                RevisionInternal loadedRev;
                                try {
                                    loadedRev = LocalDatabase.LoadRevisionBody (rev, contentOptions);
                                    properties = new Dictionary<string, object>(rev.GetProperties());
                                } catch (CouchbaseLiteException e1) {
                                    Log.W(Tag, string.Format("{0} Couldn't get local contents of {1}", rev, this), e1);
                                    RevisionFailed();
                                    continue;
                                }

                                var populatedRev = TransformRevision(loadedRev);

                                IList<string> possibleAncestors = null;
                                if (revResults.ContainsKey("possible_ancestors"))
                                {
                                    possibleAncestors = revResults["possible_ancestors"].AsList<string>();
                                }

                                properties = new Dictionary<string, object>(populatedRev.GetProperties());
                                var revisions = LocalDatabase.GetRevisionHistoryDictStartingFromAnyAncestor(populatedRev, possibleAncestors);
                                properties["_revisions"] = revisions;
                                populatedRev.SetProperties(properties);

                                // Strip any attachments already known to the target db:
                                if (properties.ContainsKey("_attachments")) 
                                {
                                    // Look for the latest common ancestor and stuf out older attachments:
                                    var minRevPos = FindCommonAncestor(populatedRev, possibleAncestors);

                                    Database.StubOutAttachmentsInRevBeforeRevPos(populatedRev, minRevPos + 1, false);

                                    properties = populatedRev.GetProperties();

                                    if (!dontSendMultipart && UploadMultipartRevision(populatedRev)) 
                                    {
                                        SafeIncrementCompletedChangesCount();
                                        continue;
                                    }
                                }

                                if (properties == null || !properties.ContainsKey("_id"))
                                {
                                    throw new InvalidOperationException("properties must contain a document _id");
                                }
                                // Add the _revisions list:
                                revsToSend.Add(rev);

                                //now add it to the docs to send
                                docsToSend.AddItem (properties);
                            }

                            UploadBulkDocs(docsToSend, revsToSend);
                        } 
                        else 
                        {
                            foreach (var revisionInternal in inbox)
                            {
                                RemovePending(revisionInternal);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.E(Tag, "Unhandled exception in Pusher.ProcessInbox", ex);
                }
                finally
                {
                    Log.D(Tag, "processInbox() calling AsyncTaskFinished()");
                    AsyncTaskFinished(1);
                }
            });
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

            Log.V(Tag, string.Format("{0}: POSTing " + numDocsToSend + " revisions to _bulk_docs: {1}", this, docsToSend));

            SafeAddToChangesCount(numDocsToSend);

            var bulkDocsBody = new Dictionary<string, object>();
            bulkDocsBody["docs"] = docsToSend;
            bulkDocsBody["new_edits"] = false;

            AsyncTaskStarted ();

            SendAsyncRequest(HttpMethod.Post, "/_bulk_docs", bulkDocsBody, (result, e) => {
                try
                {
                    if (e == null)
                    {
                        var failedIds = new HashSet<string>();
                        // _bulk_docs response is really an array not a dictionary
                        var items = ((JArray)result).ToList();
                        foreach(var item in items)
                        {
                            var itemObject = item.AsDictionary<string, object>();
                            var status = StatusFromBulkDocsResponseItem(itemObject);
                            if (!status.IsSuccessful)
                            {
                                // One of the docs failed to save.
                                Log.W(Tag, "_bulk_docs got an error: " + item);

                                // 403/Forbidden means validation failed; don't treat it as an error
                                // because I did my job in sending the revision. Other statuses are
                                // actual replication errors.
                                if (status.GetCode() != StatusCode.Forbidden)
                                {
                                    var docId = (string)item["id"];
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
                        SetLastError(e);
                        RevisionFailed();
                    } 
                    else 
                    {
                        Log.V(Tag, string.Format("POSTed to _bulk_docs: {0}", docsToSend));
                    }
                    SafeAddToCompletedChangesCount(numDocsToSend);
                }
                finally
                {
                    Log.D(Tag, "ProcessInbox() after _bulk_docs() calling AsyncTaskFinished()");
                    AsyncTaskFinished(1);
                }
            });
        }

        private new static Status StatusFromBulkDocsResponseItem(IDictionary<string, object> item)
        {
            try
            {
                if (!item.ContainsKey("error"))
                {
                    return new Status(StatusCode.Ok);
                }

                var errorStr = (string)item["error"];
                if (string.IsNullOrWhiteSpace(errorStr))
                {
                    return new Status(StatusCode.Ok);
                }

                if (item.ContainsKey("status"))
                {
                    var status = (Int64)item["status"];
                    if (status >= 400)
                    {
                        return new Status((StatusCode)status);
                    }
                }

                // If no 'status' present, interpret magic hardcoded CouchDB error strings:
                if (string.Equals(errorStr, "unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    return new Status(StatusCode.Unauthorized);
                }
                else if (string.Equals(errorStr, "forbidding", StringComparison.OrdinalIgnoreCase))
                {
                    return new Status(StatusCode.Forbidden);
                }
                else if (string.Equals(errorStr, "conflict", StringComparison.OrdinalIgnoreCase))
                {
                    return new Status(StatusCode.Conflict);
                }
                else
                {
                    return new Status(StatusCode.UpStreamError);
                }

            }
            catch (Exception e)
            {
                Log.E(Tag, "Exception getting status from " + item, e);
            }

            return new Status(StatusCode.Ok);
        }

        private bool UploadMultipartRevision(RevisionInternal revision)
        {
            MultipartContent multiPart = null;
            var revProps = revision.GetProperties();

            var attachments = revProps.Get("_attachments").AsDictionary<string,object>();
            foreach (var attachmentKey in attachments.Keys)
            {
                var attachment = attachments.Get(attachmentKey).AsDictionary<string,object>();
                if (attachment.ContainsKey("follows"))
                {
                    if (multiPart == null)
                    {
                        multiPart = new MultipartContent("related");
                        try
                        {
                            var json = Manager.GetObjectMapper().WriteValueAsString(revProps);
                            var utf8charset = Encoding.UTF8;
                            //multiPart.Add(new StringContent(json, utf8charset, "application/json"), "param1");

                            var jsonContent = new StringContent(json, utf8charset, "application/json");
                            //jsonContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                            multiPart.Add(jsonContent);
                        }
                        catch (IOException e)
                        {
                            throw new ArgumentException("Not able to serialize revision properties into a multipart request content.", e);
                        }
                    }

                    var blobStore = LocalDatabase.Attachments;
                    var base64Digest = (string)attachment.Get("digest");

                    var blobKey = new BlobKey(base64Digest);
                    var inputStream = blobStore.BlobStreamForKey(blobKey);

                    if (inputStream == null)
                    {
                        Log.W(Tag, "Unable to find blob file for blobKey: " + blobKey + " - Skipping upload of multipart revision.");
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
                                var message = string.Format("Found attachment that uses content-type"
                                    + " field name instead of content_type (see couchbase-lite-android"
                                    + " issue #80): " + attachment);
                                Log.W(Tag, message);
                            }
                        }

                        var content = new StreamContent(inputStream);
                        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                        {
                            FileName = Path.GetFileName(blobStore.PathForKey(blobKey))
                        };
                        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                        multiPart.Add(content);
                    }
                }
            }

            if (multiPart == null)
            {
                return false;
            }

            var path = string.Format("/{0}?new_edits=false", revision.GetDocId());

            // TODO: need to throttle these requests
            Log.D(Tag, "Uploading multipart request.  Revision: " + revision);
            Log.D(Tag, "uploadMultipartRevision() calling asyncTaskStarted()");

            SafeAddToChangesCount(1);
            AsyncTaskStarted();

            SendAsyncMultipartRequest(HttpMethod.Put, path, multiPart, (result, e) => {
                try
                {
                    if (e != null)
                    {
                        var httpError = e as HttpResponseException;
                        if (httpError != null)
                        {
                            if (httpError.StatusCode == System.Net.HttpStatusCode.UnsupportedMediaType)
                            {
                                dontSendMultipart = true;
                                UploadJsonRevision(revision);
                            }
                        }
                        else
                        {
                            Log.E (Tag, "Exception uploading multipart request", e);
                            SetLastError(e);
                            RevisionFailed();
                        }
                    }
                    else
                    {
                        Log.D (Tag, "Uploaded multipart request.  Result: " + result);
                        RemovePending(revision);
                    }
                }
                finally
                {
                    Log.D(Tag, "uploadMultipartRevision() calling asyncTaskFinished()");
                    // TODO: calling addToCompleteChangesCount(1)
                    AsyncTaskFinished (1);
                }
            });

            return true;
        }

        /// <summary>
        /// Uploads the revision as JSON instead of multipart.
        /// </summary>
        /// <remarks>
        /// Fallback to upload a revision if UploadMultipartRevision failed due to the server's rejecting
        /// multipart format.
        /// </remarks>
        /// <param name="rev">Rev.</param>
        private void UploadJsonRevision(RevisionInternal rev)
        {
            // Get the revision's properties:
            if (!LocalDatabase.InlineFollowingAttachmentsIn(rev))
            {
                SetLastError(new CouchbaseLiteException(StatusCode.BadAttachment));
                RevisionFailed();
                return;
            }

            Log.V(Tag, "UploadJsonRevision() calling AsyncTaskStarted()");
            AsyncTaskStarted();

            var path = string.Format("/{0}?new_edits=false", Uri.EscapeUriString(rev.GetDocId()));
            SendAsyncRequest(HttpMethod.Put, path, rev.GetProperties(), (result, e) =>
            {
                if (e != null) 
                {
                    SetLastError(e);
                    RevisionFailed();
                } 
                else 
                {
                    Log.V(Tag, "Sent {0} (JSON), response={1}", rev, result);
                    RemovePending (rev);
                }
                Log.V(Tag, "UploadJsonRevision() calling AsyncTaskFinished()");
                AsyncTaskFinished (1);
            });
        }

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
        internal static Int32 FindCommonAncestor(RevisionInternal rev, IList<string> possibleRevIDs)
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

            var generation = Database.ParseRevIDNumber(ancestorID);
            return generation;
        }
    }
}
