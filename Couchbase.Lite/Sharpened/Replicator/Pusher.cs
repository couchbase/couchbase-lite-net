/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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

		[InterfaceAudience.Private]
		internal override void MaybeCreateRemoteDB()
		{
			if (!createTarget)
			{
				return;
			}
			creatingTarget = true;
			Log.V(Database.Tag, "Remote db might not exist; creating it...");
			Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": maybeCreateRemoteDB() calling asyncTaskStarted()"
				);
			AsyncTaskStarted();
			SendAsyncRequest("PUT", string.Empty, null, new _RemoteRequestCompletionBlock_100
				(this));
		}

		private sealed class _RemoteRequestCompletionBlock_100 : RemoteRequestCompletionBlock
		{
			public _RemoteRequestCompletionBlock_100(Pusher _enclosing)
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
						Log.E(Database.Tag, this + ": Failed to create remote db", e);
						this._enclosing.SetError(e);
						this._enclosing.Stop();
					}
					else
					{
						// this is fatal: no db to push to!
						Log.V(Database.Tag, this + ": Created remote db");
						this._enclosing.createTarget = false;
						this._enclosing.BeginReplicating();
					}
				}
				finally
				{
					Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": maybeCreateRemoteDB.onComplete() calling asyncTaskFinished()"
						);
					this._enclosing.AsyncTaskFinished(1);
				}
			}

			private readonly Pusher _enclosing;
		}

		[InterfaceAudience.Private]
		public override void BeginReplicating()
		{
			// If we're still waiting to create the remote db, do nothing now. (This method will be
			// re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
			Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": beginReplicating() called"
				);
			if (creatingTarget)
			{
				Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": creatingTarget == true, doing nothing"
					);
				return;
			}
			else
			{
				Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": creatingTarget != true, continuing"
					);
			}
			if (filterName != null)
			{
				filter = db.GetFilter(filterName);
			}
			if (filterName != null && filter == null)
			{
				Log.W(Database.Tag, string.Format("%s: No ReplicationFilter registered for filter '%s'; ignoring"
					, this, filterName));
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
				Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": pusher.beginReplicating() calling asyncTaskStarted()"
					);
				AsyncTaskStarted();
			}
		}

		// prevents stopped() from being called when other tasks finish
		[InterfaceAudience.Private]
		private void StopObserving()
		{
			if (observing)
			{
				try
				{
					observing = false;
					db.RemoveChangeListener(this);
				}
				finally
				{
					Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": stopObserving() calling asyncTaskFinished()"
						);
					AsyncTaskFinished(1);
				}
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
		protected internal override void ProcessInbox(RevisionList inbox)
		{
			long lastInboxSequence = inbox[inbox.Count - 1].GetSequence();
			// Generate a set of doc/rev IDs in the JSON format that _revs_diff wants:
			// <http://wiki.apache.org/couchdb/HttpPostRevsDiff>
			IDictionary<string, IList<string>> diffs = new Dictionary<string, IList<string>>(
				);
			foreach (RevisionInternal rev in inbox)
			{
				string docID = rev.GetDocId();
				IList<string> revs = diffs.Get(docID);
				if (revs == null)
				{
					revs = new AList<string>();
					diffs.Put(docID, revs);
				}
				revs.AddItem(rev.GetRevId());
			}
			// Call _revs_diff on the target db:
			Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": processInbox() calling asyncTaskStarted()"
				);
			Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": posting to /_revs_diff: "
				 + diffs);
			AsyncTaskStarted();
			SendAsyncRequest("POST", "/_revs_diff", diffs, new _RemoteRequestCompletionBlock_226
				(this, inbox, lastInboxSequence));
		}

		private sealed class _RemoteRequestCompletionBlock_226 : RemoteRequestCompletionBlock
		{
			public _RemoteRequestCompletionBlock_226(Pusher _enclosing, RevisionList inbox, long
				 lastInboxSequence)
			{
				this._enclosing = _enclosing;
				this.inbox = inbox;
				this.lastInboxSequence = lastInboxSequence;
			}

			public void OnCompletion(object response, Exception e)
			{
				try
				{
					Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": /_revs_diff response: "
						 + response);
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
							foreach (RevisionInternal rev in inbox)
							{
								IDictionary<string, object> properties = null;
								IDictionary<string, object> resultDoc = (IDictionary<string, object>)results.Get(
									rev.GetDocId());
								if (resultDoc != null)
								{
									IList<string> revs = (IList<string>)resultDoc.Get("missing");
									if (revs != null && revs.Contains(rev.GetRevId()))
									{
										//remote server needs this revision
										// Get the revision's properties
										if (rev.IsDeleted())
										{
											properties = new Dictionary<string, object>();
											properties.Put("_id", rev.GetDocId());
											properties.Put("_rev", rev.GetRevId());
											properties.Put("_deleted", true);
										}
										else
										{
											// OPT: Shouldn't include all attachment bodies, just ones that have changed
											EnumSet<Database.TDContentOptions> contentOptions = EnumSet.Of(Database.TDContentOptions
												.TDIncludeAttachments, Database.TDContentOptions.TDBigAttachmentsFollow);
											try
											{
												this._enclosing.db.LoadRevisionBody(rev, contentOptions);
											}
											catch (CouchbaseLiteException)
											{
												string msg = string.Format("%s Couldn't get local contents of %s", rev, this._enclosing
													);
												Log.W(Database.Tag, msg);
												this._enclosing.RevisionFailed();
												continue;
											}
											properties = new Dictionary<string, object>(rev.GetProperties());
										}
										if (properties.ContainsKey("_attachments"))
										{
											if (this._enclosing.UploadMultipartRevision(rev))
											{
												continue;
											}
										}
										if (properties != null)
										{
											// Add the _revisions list:
											properties.Put("_revisions", this._enclosing.db.GetRevisionHistoryDict(rev));
											//now add it to the docs to send
											docsToSend.AddItem(properties);
										}
									}
								}
							}
							// Post the revisions to the destination. "new_edits":false means that the server should
							// use the given _rev IDs instead of making up new ones.
							int numDocsToSend = docsToSend.Count;
							if (numDocsToSend > 0)
							{
								IDictionary<string, object> bulkDocsBody = new Dictionary<string, object>();
								bulkDocsBody.Put("docs", docsToSend);
								bulkDocsBody.Put("new_edits", false);
								Log.V(Database.Tag, string.Format("%s: POSTing " + numDocsToSend + " revisions to _bulk_docs: %s"
									, this._enclosing, docsToSend));
								this._enclosing.SetChangesCount(this._enclosing.GetChangesCount() + numDocsToSend
									);
								Log.D(Database.Tag, this._enclosing + "|" + Sharpen.Thread.CurrentThread() + ": processInbox-before_bulk_docs() calling asyncTaskStarted()"
									);
								this._enclosing.AsyncTaskStarted();
								this._enclosing.SendAsyncRequest("POST", "/_bulk_docs", bulkDocsBody, new _RemoteRequestCompletionBlock_300
									(this, docsToSend, lastInboxSequence, numDocsToSend));
							}
						}
						else
						{
							// If none of the revisions are new to the remote, just bump the lastSequence:
							this._enclosing.SetLastSequence(string.Format("%d", lastInboxSequence));
						}
					}
				}
				finally
				{
					Log.D(Database.Tag, this._enclosing + "|" + Sharpen.Thread.CurrentThread() + ": processInbox() calling asyncTaskFinished()"
						);
					this._enclosing.AsyncTaskFinished(1);
				}
			}

			private sealed class _RemoteRequestCompletionBlock_300 : RemoteRequestCompletionBlock
			{
				public _RemoteRequestCompletionBlock_300(_RemoteRequestCompletionBlock_226 _enclosing
					, IList<object> docsToSend, long lastInboxSequence, int numDocsToSend)
				{
					this._enclosing = _enclosing;
					this.docsToSend = docsToSend;
					this.lastInboxSequence = lastInboxSequence;
					this.numDocsToSend = numDocsToSend;
				}

				public void OnCompletion(object result, Exception e)
				{
					try
					{
						if (e != null)
						{
							this._enclosing._enclosing.SetError(e);
							this._enclosing._enclosing.RevisionFailed();
						}
						else
						{
							Log.V(Database.Tag, string.Format("%s: POSTed to _bulk_docs: %s", this._enclosing
								._enclosing, docsToSend));
							this._enclosing._enclosing.SetLastSequence(string.Format("%d", lastInboxSequence)
								);
						}
						this._enclosing._enclosing.SetCompletedChangesCount(this._enclosing._enclosing.GetCompletedChangesCount
							() + numDocsToSend);
					}
					finally
					{
						Log.D(Database.Tag, this._enclosing._enclosing + "|" + Sharpen.Thread.CurrentThread
							() + ": processInbox-after_bulk_docs() calling asyncTaskFinished()");
						this._enclosing._enclosing.AsyncTaskFinished(1);
					}
				}

				private readonly _RemoteRequestCompletionBlock_226 _enclosing;

				private readonly IList<object> docsToSend;

				private readonly long lastInboxSequence;

				private readonly int numDocsToSend;
			}

			private readonly Pusher _enclosing;

			private readonly RevisionList inbox;

			private readonly long lastInboxSequence;
		}

		[InterfaceAudience.Private]
		private bool UploadMultipartRevision(RevisionInternal revision)
		{
			MultipartEntity multiPart = null;
			IDictionary<string, object> revProps = revision.GetProperties();
			revProps.Put("_revisions", db.GetRevisionHistoryDict(revision));
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
						Log.W(Database.Tag, "Unable to find blob file for blobKey: " + blobKey + " - Skipping upload of multipart revision."
							);
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
								string message = string.Format("Found attachment that uses content-type" + " field name instead of content_type (see couchbase-lite-android"
									 + " issue #80): " + attachment);
								Log.W(Database.Tag, message);
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
			// TODO: need to throttle these requests
			Log.D(Database.Tag, "Uploadeding multipart request.  Revision: " + revision);
			Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": uploadMultipartRevision() calling asyncTaskStarted()"
				);
			AsyncTaskStarted();
			SendAsyncMultipartRequest("PUT", path, multiPart, new _RemoteRequestCompletionBlock_411
				(this));
			// TODO:
			return true;
		}

		private sealed class _RemoteRequestCompletionBlock_411 : RemoteRequestCompletionBlock
		{
			public _RemoteRequestCompletionBlock_411(Pusher _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void OnCompletion(object result, Exception e)
			{
				try
				{
					if (e != null)
					{
						Log.E(Database.Tag, "Exception uploading multipart request", e);
						this._enclosing.SetError(e);
						this._enclosing.RevisionFailed();
					}
					else
					{
						Log.D(Database.Tag, "Uploaded multipart request.  Result: " + result);
					}
				}
				finally
				{
					Log.D(Database.Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": uploadMultipartRevision() calling asyncTaskFinished()"
						);
					this._enclosing.AsyncTaskFinished(1);
				}
			}

			private readonly Pusher _enclosing;
		}
	}
}
