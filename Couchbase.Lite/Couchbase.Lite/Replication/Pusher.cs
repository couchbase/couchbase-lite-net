/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

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

namespace Couchbase.Lite.Replicator
{
    internal class Pusher : Replication
	{
		private bool observing;

		private Couchbase.Lite.Database.FilterDelegate filter;

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

        public override IEnumerable<String> Channels { get; set; }

        public override IEnumerable<String> DocIds { get; set; }

        public override Dictionary<String, String> Headers { get; set; }

        public override Boolean CreateTarget { get; set; }

        public override bool IsPull { get { return false; } }

        #endregion

		internal override void MaybeCreateRemoteDB()
		{
            if (!CreateTarget)
            {
                return;
            }
            Log.V(Database.Tag, "Remote db might not exist; creating it...");
            SendAsyncRequest(HttpMethod.Put, String.Empty, null, (result, e) => 
                {
                    if (e is HttpException && ((HttpException)e).ErrorCode != 412) {
                        Log.V (Database.Tag, "Unable to create remote db (normal if using sync gateway)");
                    } else {
                        Log.V (Database.Tag, "Created remote db");
                    }
                    CreateTarget = false;
                    BeginReplicating ();
                });
		}

        internal override void BeginReplicating()
		{
            // If we're still waiting to create the remote db, do nothing now. (This method will be
            // re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
            if (CreateTarget)
            {
                return;
            }
            if (Filter != null)
            {
                filter = LocalDatabase.GetFilter(Filter);
            }
            if (Filter != null && filter == null)
            {
                Log.W(Database.Tag, string.Format("%s: No ReplicationFilter registered for filter '%s'; ignoring"
                    , this, Filter));
            }
            // Process existing changes since the last push:
            long lastSequenceLong = 0;
            if (LastSequence != null)
            {
                lastSequenceLong = long.Parse(LastSequence);
            }

            var changes = LocalDatabase.ChangesSince(lastSequenceLong, null, filter);
            if (changes.Count > 0)
            {
                ProcessInbox(changes);
            }
            // Now listen for future changes (in continuous mode):
            if (continuous)
            {
                observing = true;
                LocalDatabase.Changed += OnChanged;
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
				observing = false;
                LocalDatabase.Changed -= OnChanged;
				AsyncTaskFinished(1);
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
            var diffs = new Dictionary<String, IList<String>>();
            foreach (var rev in inbox)
            {
                var docID = rev.GetDocId();
                var revs = diffs.Get(docID);
                if (revs == null)
                {
                    revs = new AList<String>();
                    diffs.Put(docID, revs);
                }
                revs.AddItem(rev.GetRevId());
            }
            // Call _revs_diff on the target db:
            AsyncTaskStarted();
            SendAsyncRequest(HttpMethod.Post, "/_revs_diff", diffs, (response, e) => {
                IDictionary<string, object> results = (IDictionary<string, object>)response;
                if (e != null) {
                    LastError = e;
                    Stop ();
                } else {
                    if (results.Count != 0) {
                        // Go through the list of local changes again, selecting the ones the destination server
                        // said were missing and mapping them to a JSON dictionary in the form _bulk_docs wants:
                        var docsToSend = new AList<object> ();

                        foreach (var rev in inbox) {
                            IDictionary<string, object> properties = null;
                            var resultDoc = (IDictionary<String, Object>)results.Get (rev.GetDocId ());
                            if (resultDoc != null) {
                                var revs = (IList<String>)resultDoc.Get ("missing");
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
                                            throw new RuntimeException (e1);
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
                        var bulkDocsBody = new Dictionary<String, Object> ();

                        bulkDocsBody.Put ("docs", docsToSend);
                        bulkDocsBody.Put ("new_edits", false);

                        Log.I (Database.Tag, string.Format ("{0}: Sending {1} revisions", this, numDocsToSend));
                        Log.V (Database.Tag, string.Format ("{0}: Sending {1}", this, inbox));
                        ChangesCount += numDocsToSend;

                        AsyncTaskStarted ();
                        SendAsyncRequest (HttpMethod.Post, "/_bulk_docs", bulkDocsBody, (result, ex) => {
                            if (e != null) {
                                LastError = e;
                            } else {
                                Log.V (Database.Tag, string.Format ("%s: Sent %s", this, inbox));
                                LastSequence = string.Format ("{0}", lastInboxSequence);
                            }
                            CompletedChangesCount  += numDocsToSend;
                            AsyncTaskFinished (1);
                        });
                    } else {
                        // If none of the revisions are new to the remote, just bump the lastSequence:
                        LastSequence = string.Format ("{0}", lastInboxSequence);
                    }
                }
                AsyncTaskFinished (1);
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

                    var blobStore = LocalDatabase.GetAttachments();
                    var base64Digest = (string)attachment.Get("digest");

                    var blobKey = new BlobKey(base64Digest);
                    var inputStream = blobStore.BlobStreamForKey(blobKey);

                    if (inputStream == null)
                    {
                        Log.W(Database.Tag, "Unable to find blob file for blobKey: " + blobKey + " - Skipping upload of multipart revision.");
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
                                Log.W(Database.Tag, message);
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
            Log.D(Database.Tag, "Uploadeding multipart request.  Revision: " + revision);

            AsyncTaskStarted();
            SendAsyncMultipartRequest(HttpMethod.Put, path, multiPart, (result, e) => {
                if (e != null) {
                    Log.E (Database.Tag, "Exception uploading multipart request", e);
                    LastError = e;
                } else {
                    Log.D (Database.Tag, "Uploaded multipart request.  Result: " + result);
                }
                AsyncTaskFinished (1);
            });
            return true;
		}
	}
}
