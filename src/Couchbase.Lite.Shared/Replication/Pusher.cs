//
// Pusher.cs
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
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Couchbase.Lite.Replicator
{
    internal class Pusher : Replication
	{
        readonly string Tag = "Pusher";

		private bool observing;

		private FilterDelegate filter;

		/// <summary>Constructor</summary>
        public Pusher(Database db, Uri remote, bool continuous, TaskFactory workExecutor) : this(db, remote, continuous, null, workExecutor)
		{
		}

		/// <summary>Constructor</summary>
        public Pusher(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory
            , TaskFactory workExecutor) : base(db, remote, continuous, clientFactory
			, workExecutor)
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

            Log.V(Tag, this + ": Remote db might not exist; creating it...");
            Log.D(Tag, this + "|" + Thread.CurrentThread() + ": maybeCreateRemoteDB() calling asyncTaskStarted()");
            SendAsyncRequest(HttpMethod.Put, String.Empty, null, (result, e) => 
                {
                    try
                    {
                        if (e is HttpResponseException && ((HttpResponseException)e).StatusCode.GetStatusCode() != StatusCode.PreconditionFailed) 
                        {
                            // this is fatal: no db to push to!
                            Log.E(Tag, this + ": Failed to create remote db", e);
                            LastError = e;
                            Stop();
                        } 
                        else 
                        {
                            Log.V (Tag, this + ": Created remote db");
                            CreateTarget = false;
                            BeginReplicating ();
                        }
                    } 
                    finally
                    {
                        Log.D(Tag, this + "|" +  Thread.CurrentThread() + ": maybeCreateRemoteDB.onComplete() calling asyncTaskFinished()");
                        AsyncTaskFinished(1);
                    }
                });
		}

        internal override void BeginReplicating()
		{
            // If we're still waiting to create the remote db, do nothing now. (This method will be
            // re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
            Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": beginReplicating() called");

            if (CreateTarget)
            {
                Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": creatingTarget == true, doing nothing");
                return;
            }
            else
            {
                Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": creatingTarget != true, continuing");
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
                Log.D(Tag, this + "|" + Thread.CurrentThread() + ": pusher.beginReplicating() calling asyncTaskStarted()");
                AsyncTaskStarted();
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
                try
                {
                    observing = false;
                    LocalDatabase.Changed -= OnChanged;
                }
                finally
                {
                    AsyncTaskFinished(1);
                }
			}
		}

        internal void OnChanged(Object sender, Database.DatabaseChangeEventArgs args)
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

        internal override void ProcessInbox(RevisionList inbox)
		{
            var lastInboxSequence = inbox[inbox.Count - 1].GetSequence();
            // Generate a set of doc/rev IDs in the JSON format that _revs_diff wants:
            // <http://wiki.apache.org/couchdb/HttpPostRevsDiff>
            var diffs = new Dictionary<String, IList<String>>();
            foreach (var rev in inbox)
            {
                var docID = rev.GetDocId();
                var revs = diffs.Get(docID);
                if (revs == null)
                {
                    revs = new AList<String>();
                    diffs[docID] = revs;
                }
                revs.AddItem(rev.GetRevId());
            }

            // Call _revs_diff on the target db:
            Log.D(Tag, this + "|" + Thread.CurrentThread() + ": processInbox() calling asyncTaskStarted()");
            Log.D(Tag, this + "|" + Thread.CurrentThread() + ": posting to /_revs_diff: " + string.Join(Environment.NewLine, diffs));

            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Post, "/_revs_diff", diffs, (response, e) => 
            {
                try {
                    Log.D(Tag, this + "|" + Thread.CurrentThread() + ": /_revs_diff response: " + response);

                    var responseData = (JObject)response;
                    var results = responseData.ToObject<IDictionary<string, object>>();

                    if (e != null) {
                        LastError = e;
                        RevisionFailed();
                        //Stop ();
                    } else {
                        if (results.Count != 0) {
                            // Go through the list of local changes again, selecting the ones the destination server
                            // said were missing and mapping them to a JSON dictionary in the form _bulk_docs wants:
                            var docsToSend = new AList<object> ();

                            foreach (var rev in inbox) {
                                IDictionary<string, object> properties = null;
                                var resultDocData = (JObject)results.Get (rev.GetDocId ());
                                var resultDoc = resultDocData.ToObject<IDictionary<String, Object>>();
                                if (resultDoc != null) {
                                    var revs = ((JArray)resultDoc.Get ("missing")).Values<String>().ToList();
                                    if (revs != null && revs.Contains (rev.GetRevId ())) {
                                        //remote server needs this revision
                                        // Get the revision's properties
                                        if (rev.IsDeleted ()) {
                                            properties = new Dictionary<string, object> ();
                                            properties.Put ("_id", rev.GetDocId ());
                                            properties.Put ("_rev", rev.GetRevId ());
                                            properties.Put ("_deleted", true);
                                        } else {
                                            // OPT: Shouldn't include all attachment bodies, just ones that have changed
                                            var contentOptions = EnumSet.Of (TDContentOptions.TDIncludeAttachments, TDContentOptions.TDBigAttachmentsFollow);
                                            try {
                                                LocalDatabase.LoadRevisionBody (rev, contentOptions);
                                            } catch (CouchbaseLiteException e1) {
                                                Log.W(Tag, string.Format("%s Couldn't get local contents of %s", rev, this));
                                                RevisionFailed();
                                                continue;
                                            }
                                            properties = new Dictionary<String, Object> (rev.GetProperties ());
                                        }
                                        if (properties.ContainsKey ("_attachments")) {
                                            if (UploadMultipartRevision (rev)) {
                                                continue;
                                            }
                                        }
                                        if (properties != null) {
                                            // Add the _revisions list:
                                            properties.Put ("_revisions", LocalDatabase.GetRevisionHistoryDict (rev));
                                            //now add it to the docs to send
                                            docsToSend.AddItem (properties);
                                        }
                                    }
                                }
                            }
                            // Post the revisions to the destination. "new_edits":false means that the server should
                            // use the given _rev IDs instead of making up new ones.
                            var numDocsToSend = docsToSend.Count;
                            if (numDocsToSend > 0)
                            {
                                var bulkDocsBody = new Dictionary<String, Object> ();
                                bulkDocsBody.Put ("docs", docsToSend);
                                bulkDocsBody.Put ("new_edits", false);

                                Log.V(Tag, string.Format("%s: POSTing " + numDocsToSend + " revisions to _bulk_docs: %s", this, docsToSend));

                                ChangesCount += numDocsToSend;

                                Log.D(Tag, this + "|" + Thread.CurrentThread() + ": processInbox-before_bulk_docs() calling asyncTaskStarted()");

                                AsyncTaskStarted ();
                                SendAsyncRequest (HttpMethod.Post, "/_bulk_docs", bulkDocsBody, (result, ex) => {
                                    try
                                    {
                                        if (ex != null) {
                                            LastError = ex;
                                            RevisionFailed();
                                        } else {
                                            Log.V(Tag, string.Format("%s: POSTed to _bulk_docs: %s", this, docsToSend));
                                            LastSequence = string.Format ("{0}", lastInboxSequence);
                                        }
                                        CompletedChangesCount  += numDocsToSend;
                                    }
                                    finally
                                    {
                                        AsyncTaskFinished (1);
                                    }
                                });
                            }
                        } else {
                            // If none of the revisions are new to the remote, just bump the lastSequence:
                            LastSequence = string.Format ("{0}", lastInboxSequence);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.E(Tag, "Unhandled exception in Pusher.ProcessInbox", ex);
                }
                finally
                {
                    Log.D(Tag, this + "|" + Thread.CurrentThread() + ": processInbox() calling asyncTaskFinished()");
                    AsyncTaskFinished (1);
                }
            });
		}

		private bool UploadMultipartRevision(RevisionInternal revision)
		{
            MultipartFormDataContent multiPart = null;
            var revProps = revision.GetProperties();
            revProps.Put("_revisions", LocalDatabase.GetRevisionHistoryDict(revision));

            var attachments = (IDictionary<string, object>)revProps.Get("_attachments");

            foreach (var attachmentKey in attachments.Keys)
            {
                var attachment = (IDictionary<String, Object>)attachments.Get(attachmentKey);
                if (attachment.ContainsKey("follows"))
                {
                    if (multiPart == null)
                    {
                        multiPart = new MultipartFormDataContent();
                        try
                        {
                            var json = Manager.GetObjectMapper().WriteValueAsString(revProps);
                            var utf8charset = Encoding.UTF8;
                            multiPart.Add(new StringContent(json, utf8charset, "application/json"), "param1");
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
                        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                        multiPart.Add(content, attachmentKey);
                    }
                }
            }

            if (multiPart == null)
            {
                return false;
            }

            var path = string.Format("/{0}?new_edits=false", revision.GetDocId());

            // TODO: need to throttle these requests
            Log.D(Tag, "Uploadeding multipart request.  Revision: " + revision);
            Log.D(Tag, this + "|" + Thread.CurrentThread() + ": uploadMultipartRevision() calling asyncTaskStarted()");

            AsyncTaskStarted();

            SendAsyncMultipartRequest(HttpMethod.Put, path, multiPart, (result, e) => {
                try
                {
                    if (e != null) 
                    {
                        Log.E (Tag, "Exception uploading multipart request", e);
                        LastError = e;
                        RevisionFailed();
                    }
                    else
                    {
                        Log.D (Tag, "Uploaded multipart request.  Result: " + result);
                    }
                }
                finally
                {
                    AsyncTaskFinished (1);
                }
            });
            return true;
		}
	}
}
