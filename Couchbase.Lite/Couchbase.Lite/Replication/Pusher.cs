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

namespace Couchbase.Lite.Replicator
{
    internal class Pusher : Replication, IChangeTrackerClient
	{
		private bool shouldCreateTarget;

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
			shouldCreateTarget = false;
			observing = false;
		}

        #region implemented abstract members of Replication

        #region IChangeTrackerClient implementation

        public void ChangeTrackerReceivedChange (IDictionary<string, object> change)
        {
            throw new NotImplementedException ();
        }

        public void ChangeTrackerStopped (ChangeTracker tracker)
        {
            throw new NotImplementedException ();
        }

        #endregion

        #region IHttpClientFactory implementation

        public System.Net.Http.HttpClient GetHttpClient ()
        {
            throw new NotImplementedException ();
        }

        public System.Net.Http.HttpClientHandler HttpHandler {
            get {
                throw new NotImplementedException ();
            }
        }

        #endregion

        public override bool CreateTarget {
            get {
                throw new NotImplementedException ();
            }
            set {
                throw new NotImplementedException ();
            }
        }

        public override IEnumerable<string> Channels {
            get {
                throw new NotImplementedException ();
            }
            set {
                throw new NotImplementedException ();
            }
        }

        public override IEnumerable<string> DocIds {
            get {
                throw new NotImplementedException ();
            }
            set {
                throw new NotImplementedException ();
            }
        }

        public override Dictionary<string, string> Headers {
            get {
                throw new NotImplementedException ();
            }
            set {
                throw new NotImplementedException ();
            }
        }

        #endregion

        public override bool IsPull { get { return false; } }

		public bool ShouldCreateTarget()
		{
			return shouldCreateTarget;
		}

		public void SetCreateTarget(bool createTarget)
		{
			this.shouldCreateTarget = createTarget;
		}

		internal override void MaybeCreateRemoteDB()
		{
            throw new NotImplementedException();
//			if (!shouldCreateTarget)
//			{
//				return;
//			}
//			Log.V(Database.Tag, "Remote db might not exist; creating it...");
//			SendAsyncRequest("PUT", string.Empty, null, new _RemoteRequestCompletionBlock_82(
//				this));
		}

//		private sealed class _RemoteRequestCompletionBlock_82 : RemoteRequestCompletionBlock
//		{
//			public _RemoteRequestCompletionBlock_82(Pusher _enclosing)
//			{
//				this._enclosing = _enclosing;
//			}
//
//			public void OnCompletion(object result, Exception e)
//			{
//				if (e != null && e is HttpResponseException && ((HttpResponseException)e).GetStatusCode
//					() != 412)
//				{
//					Log.V(Database.Tag, "Unable to create remote db (normal if using sync gateway)");
//				}
//				else
//				{
//					Log.V(Database.Tag, "Created remote db");
//				}
//				this._enclosing.shouldCreateTarget = false;
//				this._enclosing.BeginReplicating();
//			}
//
//			private readonly Pusher _enclosing;
//		}

        internal override void BeginReplicating()
		{
            throw new NotImplementedException();
			// If we're still waiting to create the remote db, do nothing now. (This method will be
			// re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
//			if (shouldCreateTarget)
//			{
//				return;
//			}
//			if (filterName != null)
//			{
//				filter = db.GetFilter(filterName);
//			}
//			if (filterName != null && filter == null)
//			{
//				Log.W(Database.Tag, string.Format("%s: No FilterDelegate registered for filter '%s'; ignoring"
//					, this, filterName));
//			}
//			// Process existing changes since the last push:
//			long lastSequenceLong = 0;
//			if (lastSequence != null)
//			{
//				lastSequenceLong = long.Parse(lastSequence);
//			}
//			RevisionList changes = db.ChangesSince(lastSequenceLong, null, filter);
//			if (changes.Count > 0)
//			{
//				ProcessInbox(changes);
//			}
//			// Now listen for future changes (in continuous mode):
//			if (continuous)
//			{
//				observing = true;
//				db.AddChangeListener(this);
//				AsyncTaskStarted();
//			}
		}

		// prevents stopped() from being called when other tasks finish
		public override void Stop()
		{
			StopObserving();
			base.Stop();
		}

		private void StopObserving()
		{
            throw new NotImplementedException();
//			if (observing)
//			{
//				observing = false;
//				db.RemoveChangeListener(this);
//				AsyncTaskFinished(1);
//			}
		}

        public virtual void OnChanged(Object sender, Database.DatabaseChangeEventArgs args)
		{
            throw new NotImplementedException();
//			IList<DocumentChange> changes = @event.GetChanges();
//			foreach (DocumentChange change in changes)
//			{
//				// Skip revisions that originally came from the database I'm syncing to:
//				Uri source = change.GetSourceUrl();
//				if (source != null && source.Equals(remote))
//				{
//					return;
//				}
//				RevisionInternal rev = change.GetRevisionInternal();
//				if (rev != null && ((filter == null) || filter.Filter(rev, null)))
//				{
//					AddToInbox(rev);
//				}
//			}
		}

        internal override void ProcessInbox(RevisionList inbox)
		{
            throw new NotImplementedException();
//			long lastInboxSequence = inbox[inbox.Count - 1].GetSequence();
//			// Generate a set of doc/rev IDs in the JSON format that _revs_diff wants:
//			IDictionary<string, IList<string>> diffs = new Dictionary<string, IList<string>>(
//				);
//			foreach (RevisionInternal rev in inbox)
//			{
//				string docID = rev.GetDocId();
//				IList<string> revs = diffs.Get(docID);
//				if (revs == null)
//				{
//					revs = new AList<string>();
//					diffs.Put(docID, revs);
//				}
//				revs.AddItem(rev.GetRevId());
//			}
//			// Call _revs_diff on the target db:
//			AsyncTaskStarted();
//			SendAsyncRequest("POST", "/_revs_diff", diffs, new _RemoteRequestCompletionBlock_181
//				(this, inbox, lastInboxSequence));
		}

//		private sealed class _RemoteRequestCompletionBlock_181 : RemoteRequestCompletionBlock
//		{
//			public _RemoteRequestCompletionBlock_181(Pusher _enclosing, RevisionList inbox, long
//				 lastInboxSequence)
//			{
//				this._enclosing = _enclosing;
//				this.inbox = inbox;
//				this.lastInboxSequence = lastInboxSequence;
//			}
//
//			public void OnCompletion(object response, Exception e)
//			{
//				IDictionary<string, object> results = (IDictionary<string, object>)response;
//				if (e != null)
//				{
//					this._enclosing.error = e;
//					this._enclosing.Stop();
//				}
//				else
//				{
//					if (results.Count != 0)
//					{
//						// Go through the list of local changes again, selecting the ones the destination server
//						// said were missing and mapping them to a JSON dictionary in the form _bulk_docs wants:
//						IList<object> docsToSend = new AList<object>();
//						foreach (RevisionInternal rev in inbox)
//						{
//							IDictionary<string, object> properties = null;
//							IDictionary<string, object> resultDoc = (IDictionary<string, object>)results.Get(
//								rev.GetDocId());
//							if (resultDoc != null)
//							{
//								IList<string> revs = (IList<string>)resultDoc.Get("missing");
//								if (revs != null && revs.Contains(rev.GetRevId()))
//								{
//									//remote server needs this revision
//									// Get the revision's properties
//									if (rev.IsDeleted())
//									{
//										properties = new Dictionary<string, object>();
//										properties.Put("_id", rev.GetDocId());
//										properties.Put("_rev", rev.GetRevId());
//										properties.Put("_deleted", true);
//									}
//									else
//									{
//										// OPT: Shouldn't include all attachment bodies, just ones that have changed
//										EnumSet<TDContentOptions> contentOptions = EnumSet.Of(TDContentOptions
//											.TDIncludeAttachments, TDContentOptions.TDBigAttachmentsFollow);
//										try
//										{
//											this._enclosing.db.LoadRevisionBody(rev, contentOptions);
//										}
//										catch (CouchbaseLiteException e1)
//										{
//											throw new RuntimeException(e1);
//										}
//										properties = new Dictionary<string, object>(rev.GetProperties());
//									}
//									if (properties.ContainsKey("_attachments"))
//									{
//										if (this._enclosing.UploadMultipartRevision(rev))
//										{
//											continue;
//										}
//									}
//									if (properties != null)
//									{
//										// Add the _revisions list:
//										properties.Put("_revisions", this._enclosing.db.GetRevisionHistoryDict(rev));
//										//now add it to the docs to send
//										docsToSend.AddItem(properties);
//									}
//								}
//							}
//						}
//						// Post the revisions to the destination. "new_edits":false means that the server should
//						// use the given _rev IDs instead of making up new ones.
//						int numDocsToSend = docsToSend.Count;
//						IDictionary<string, object> bulkDocsBody = new Dictionary<string, object>();
//						bulkDocsBody.Put("docs", docsToSend);
//						bulkDocsBody.Put("new_edits", false);
//						Log.I(Database.Tag, string.Format("%s: Sending %d revisions", this, numDocsToSend
//							));
//						Log.V(Database.Tag, string.Format("%s: Sending %s", this, inbox));
//						this._enclosing.SetChangesCount(this._enclosing.GetChangesCount() + numDocsToSend
//							);
//						this._enclosing.AsyncTaskStarted();
//						this._enclosing.SendAsyncRequest("POST", "/_bulk_docs", bulkDocsBody, new _RemoteRequestCompletionBlock_247
//							(this, inbox, lastInboxSequence, numDocsToSend));
//					}
//					else
//					{
//						// If none of the revisions are new to the remote, just bump the lastSequence:
//						this._enclosing.SetLastSequence(string.Format("%d", lastInboxSequence));
//					}
//				}
//				this._enclosing.AsyncTaskFinished(1);
//			}
//
//			private sealed class _RemoteRequestCompletionBlock_247 : RemoteRequestCompletionBlock
//			{
//				public _RemoteRequestCompletionBlock_247(_RemoteRequestCompletionBlock_181 _enclosing
//					, RevisionList inbox, long lastInboxSequence, int numDocsToSend)
//				{
//					this._enclosing = _enclosing;
//					this.inbox = inbox;
//					this.lastInboxSequence = lastInboxSequence;
//					this.numDocsToSend = numDocsToSend;
//				}
//
//				public void OnCompletion(object result, Exception e)
//				{
//					if (e != null)
//					{
//						this._enclosing._enclosing.error = e;
//					}
//					else
//					{
//						Log.V(Database.Tag, string.Format("%s: Sent %s", this, inbox));
//						this._enclosing._enclosing.SetLastSequence(string.Format("%d", lastInboxSequence)
//							);
//					}
//					this._enclosing._enclosing.SetCompletedChangesCount(this._enclosing._enclosing.GetCompletedChangesCount
//						() + numDocsToSend);
//					this._enclosing._enclosing.AsyncTaskFinished(1);
//				}
//
//				private readonly _RemoteRequestCompletionBlock_181 _enclosing;
//
//				private readonly RevisionList inbox;
//
//				private readonly long lastInboxSequence;
//
//				private readonly int numDocsToSend;
//			}
//
//			private readonly Pusher _enclosing;
//
//			private readonly RevisionList inbox;
//
//			private readonly long lastInboxSequence;
//		}

		private bool UploadMultipartRevision(RevisionInternal revision)
		{
            throw new NotImplementedException();
//			MultipartEntity multiPart = null;
//			IDictionary<string, object> revProps = revision.GetProperties();
//			revProps.Put("_revisions", db.GetRevisionHistoryDict(revision));
//			IDictionary<string, object> attachments = (IDictionary<string, object>)revProps.Get
//				("_attachments");
//			foreach (string attachmentKey in attachments.Keys)
//			{
//				IDictionary<string, object> attachment = (IDictionary<string, object>)attachments
//					.Get(attachmentKey);
//				if (attachment.ContainsKey("follows"))
//				{
//					if (multiPart == null)
//					{
//						multiPart = new MultipartEntity();
//						try
//						{
//							string json = Manager.GetObjectMapper().WriteValueAsString(revProps);
//							Encoding utf8charset = Sharpen.Extensions.GetEncoding("UTF-8");
//							multiPart.AddPart("param1", new StringBody(json, "application/json", utf8charset)
//								);
//						}
//						catch (IOException e)
//						{
//							throw new ArgumentException(e);
//						}
//					}
//					BlobStore blobStore = this.db.GetAttachments();
//					string base64Digest = (string)attachment.Get("digest");
//					BlobKey blobKey = new BlobKey(base64Digest);
//					InputStream inputStream = blobStore.BlobStreamForKey(blobKey);
//					if (inputStream == null)
//					{
//						Log.W(Database.Tag, "Unable to find blob file for blobKey: " + blobKey + " - Skipping upload of multipart revision."
//							);
//						multiPart = null;
//					}
//					else
//					{
//						string contentType = null;
//						if (attachment.ContainsKey("content_type"))
//						{
//							contentType = (string)attachment.Get("content_type");
//						}
//						else
//						{
//							if (attachment.ContainsKey("content-type"))
//							{
//								string message = string.Format("Found attachment that uses content-type" + " field name instead of content_type (see couchbase-lite-android"
//									 + " issue #80): " + attachment);
//								Log.W(Database.Tag, message);
//							}
//						}
//						multiPart.AddPart(attachmentKey, new InputStreamBody(inputStream, contentType, attachmentKey
//							));
//					}
//				}
//			}
//			if (multiPart == null)
//			{
//				return false;
//			}
//			string path = string.Format("/%s?new_edits=false", revision.GetDocId());
//			// TODO: need to throttle these requests
//			Log.D(Database.Tag, "Uploadeding multipart request.  Revision: " + revision);
//			AsyncTaskStarted();
//			SendAsyncMultipartRequest("PUT", path, multiPart, new _RemoteRequestCompletionBlock_333
//				(this));
//			return true;
		}

//		private sealed class _RemoteRequestCompletionBlock_333 : RemoteRequestCompletionBlock
//		{
//			public _RemoteRequestCompletionBlock_333(Pusher _enclosing)
//			{
//				this._enclosing = _enclosing;
//			}
//
//			public void OnCompletion(object result, Exception e)
//			{
//				if (e != null)
//				{
//					Log.E(Database.Tag, "Exception uploading multipart request", e);
//					this._enclosing.error = e;
//				}
//				else
//				{
//					Log.D(Database.Tag, "Uploaded multipart request.  Result: " + result);
//				}
//				this._enclosing.AsyncTaskFinished(1);
//			}
//
//			private readonly Pusher _enclosing;
//		}
	}
}
