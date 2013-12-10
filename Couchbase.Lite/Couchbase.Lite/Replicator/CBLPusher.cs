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
using Couchbase;
using Couchbase.Replicator;
using Couchbase.Support;
using Couchbase.Util;
using Org.Apache.Http.Client;
using Org.Apache.Http.Entity.Mime;
using Org.Apache.Http.Entity.Mime.Content;
using Sharpen;

namespace Couchbase.Replicator
{
	public class CBLPusher : CBLReplicator, Observer
	{
		private bool createTarget;

		private bool observing;

		private CBLFilterBlock filter;

		public CBLPusher(CBLDatabase db, Uri remote, bool continuous, ScheduledExecutorService
			 workExecutor) : this(db, remote, continuous, null, workExecutor)
		{
		}

		public CBLPusher(CBLDatabase db, Uri remote, bool continuous, HttpClientFactory clientFactory
			, ScheduledExecutorService workExecutor) : base(db, remote, continuous, clientFactory
			, workExecutor)
		{
			createTarget = false;
			observing = false;
		}

		public virtual bool IsCreateTarget()
		{
			return createTarget;
		}

		public virtual void SetCreateTarget(bool createTarget)
		{
			this.createTarget = createTarget;
		}

		public virtual void SetFilter(CBLFilterBlock filter)
		{
			this.filter = filter;
		}

		public override bool IsPush()
		{
			return true;
		}

		public override void MaybeCreateRemoteDB()
		{
			if (!createTarget)
			{
				return;
			}
			Log.V(CBLDatabase.Tag, "Remote db might not exist; creating it...");
			SendAsyncRequest("PUT", string.Empty, null, new _CBLRemoteRequestCompletionBlock_73
				(this));
		}

		private sealed class _CBLRemoteRequestCompletionBlock_73 : CBLRemoteRequestCompletionBlock
		{
			public _CBLRemoteRequestCompletionBlock_73(CBLPusher _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void OnCompletion(object result, Exception e)
			{
				if (e != null && e is HttpResponseException && ((HttpResponseException)e).GetStatusCode
					() != 412)
				{
					Log.V(CBLDatabase.Tag, "Unable to create remote db (normal if using sync gateway)"
						);
				}
				else
				{
					Log.V(CBLDatabase.Tag, "Created remote db");
				}
				this._enclosing.createTarget = false;
				this._enclosing.BeginReplicating();
			}

			private readonly CBLPusher _enclosing;
		}

		public override void BeginReplicating()
		{
			// If we're still waiting to create the remote db, do nothing now. (This method will be
			// re-invoked after that request finishes; see maybeCreateRemoteDB() above.)
			if (createTarget)
			{
				return;
			}
			if (filterName != null)
			{
				filter = db.GetFilterNamed(filterName);
			}
			if (filterName != null && filter == null)
			{
				Log.W(CBLDatabase.Tag, string.Format("%s: No CBLFilterBlock registered for filter '%s'; ignoring"
					, this, filterName));
			}
			// Process existing changes since the last push:
			long lastSequenceLong = 0;
			if (lastSequence != null)
			{
				lastSequenceLong = long.Parse(lastSequence);
			}
			CBLRevisionList changes = db.ChangesSince(lastSequenceLong, null, filter);
			if (changes.Count > 0)
			{
				ProcessInbox(changes);
			}
			// Now listen for future changes (in continuous mode):
			if (continuous)
			{
				observing = true;
				db.AddObserver(this);
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
				db.DeleteObserver(this);
				AsyncTaskFinished(1);
			}
		}

		public virtual void Update(Observable observable, object data)
		{
			//make sure this came from where we expected
			if (observable == db)
			{
				IDictionary<string, object> change = (IDictionary<string, object>)data;
				// Skip revisions that originally came from the database I'm syncing to:
				Uri source = (Uri)change.Get("source");
				if (source != null && source.Equals(remote.ToExternalForm()))
				{
					return;
				}
				CBLRevision rev = (CBLRevision)change.Get("rev");
				if (rev != null && ((filter == null) || filter.Filter(rev)))
				{
					AddToInbox(rev);
				}
			}
		}

		public override void ProcessInbox(CBLRevisionList inbox)
		{
			long lastInboxSequence = inbox[inbox.Count - 1].GetSequence();
			// Generate a set of doc/rev IDs in the JSON format that _revs_diff wants:
			IDictionary<string, IList<string>> diffs = new Dictionary<string, IList<string>>(
				);
			foreach (CBLRevision rev in inbox)
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
			AsyncTaskStarted();
			SendAsyncRequest("POST", "/_revs_diff", diffs, new _CBLRemoteRequestCompletionBlock_172
				(this, inbox, lastInboxSequence));
		}

		private sealed class _CBLRemoteRequestCompletionBlock_172 : CBLRemoteRequestCompletionBlock
		{
			public _CBLRemoteRequestCompletionBlock_172(CBLPusher _enclosing, CBLRevisionList
				 inbox, long lastInboxSequence)
			{
				this._enclosing = _enclosing;
				this.inbox = inbox;
				this.lastInboxSequence = lastInboxSequence;
			}

			public void OnCompletion(object response, Exception e)
			{
				IDictionary<string, object> results = (IDictionary<string, object>)response;
				if (e != null)
				{
					this._enclosing.error = e;
					this._enclosing.Stop();
				}
				else
				{
					if (results.Count != 0)
					{
						// Go through the list of local changes again, selecting the ones the destination server
						// said were missing and mapping them to a JSON dictionary in the form _bulk_docs wants:
						IList<object> docsToSend = new AList<object>();
						foreach (CBLRevision rev in inbox)
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
										EnumSet<CBLDatabase.TDContentOptions> contentOptions = EnumSet.Of(CBLDatabase.TDContentOptions
											.TDIncludeAttachments, CBLDatabase.TDContentOptions.TDBigAttachmentsFollow);
										CBLStatus status = this._enclosing.db.LoadRevisionBody(rev, contentOptions);
										if (!status.IsSuccessful())
										{
											Log.W(CBLDatabase.Tag, string.Format("%s: Couldn't get local contents of %s", this
												, rev));
										}
										else
										{
											properties = new Dictionary<string, object>(rev.GetProperties());
										}
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
						IDictionary<string, object> bulkDocsBody = new Dictionary<string, object>();
						bulkDocsBody.Put("docs", docsToSend);
						bulkDocsBody.Put("new_edits", false);
						Log.I(CBLDatabase.Tag, string.Format("%s: Sending %d revisions", this, numDocsToSend
							));
						Log.V(CBLDatabase.Tag, string.Format("%s: Sending %s", this, inbox));
						this._enclosing.SetChangesTotal(this._enclosing.GetChangesTotal() + numDocsToSend
							);
						this._enclosing.AsyncTaskStarted();
						this._enclosing.SendAsyncRequest("POST", "/_bulk_docs", bulkDocsBody, new _CBLRemoteRequestCompletionBlock_236
							(this, inbox, lastInboxSequence, numDocsToSend));
					}
					else
					{
						// If none of the revisions are new to the remote, just bump the lastSequence:
						this._enclosing.SetLastSequence(string.Format("%d", lastInboxSequence));
					}
				}
				this._enclosing.AsyncTaskFinished(1);
			}

			private sealed class _CBLRemoteRequestCompletionBlock_236 : CBLRemoteRequestCompletionBlock
			{
				public _CBLRemoteRequestCompletionBlock_236(_CBLRemoteRequestCompletionBlock_172 
					_enclosing, CBLRevisionList inbox, long lastInboxSequence, int numDocsToSend)
				{
					this._enclosing = _enclosing;
					this.inbox = inbox;
					this.lastInboxSequence = lastInboxSequence;
					this.numDocsToSend = numDocsToSend;
				}

				public void OnCompletion(object result, Exception e)
				{
					if (e != null)
					{
						this._enclosing._enclosing.error = e;
					}
					else
					{
						Log.V(CBLDatabase.Tag, string.Format("%s: Sent %s", this, inbox));
						this._enclosing._enclosing.SetLastSequence(string.Format("%d", lastInboxSequence)
							);
					}
					this._enclosing._enclosing.SetChangesProcessed(this._enclosing._enclosing.GetChangesProcessed
						() + numDocsToSend);
					this._enclosing._enclosing.AsyncTaskFinished(1);
				}

				private readonly _CBLRemoteRequestCompletionBlock_172 _enclosing;

				private readonly CBLRevisionList inbox;

				private readonly long lastInboxSequence;

				private readonly int numDocsToSend;
			}

			private readonly CBLPusher _enclosing;

			private readonly CBLRevisionList inbox;

			private readonly long lastInboxSequence;
		}

		private bool UploadMultipartRevision(CBLRevision revision)
		{
			MultipartEntity multiPart = null;
			IDictionary<string, object> revProps = revision.GetProperties();
			revProps.Put("_revisions", db.GetRevisionHistoryDict(revision));
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
							string json = CBLServer.GetObjectMapper().WriteValueAsString(revProps);
							Encoding utf8charset = Sharpen.Extensions.GetEncoding("UTF-8");
							multiPart.AddPart("param1", new StringBody(json, "application/json", utf8charset)
								);
						}
						catch (IOException e)
						{
							throw new ArgumentException(e);
						}
					}
					CBLBlobStore blobStore = this.db.GetAttachments();
					string base64Digest = (string)attachment.Get("digest");
					CBLBlobKey blobKey = new CBLBlobKey(base64Digest);
					InputStream inputStream = blobStore.BlobStreamForKey(blobKey);
					if (inputStream == null)
					{
						Log.W(CBLDatabase.Tag, "Unable to find blob file for blobKey: " + blobKey + " - Skipping upload of multipart revision."
							);
						multiPart = null;
					}
					else
					{
						// workaround for issue #80 - it was looking at the "content_type" field instead of "content-type".
						// fix is backwards compatible in case any code is using content_type.
						string contentType = null;
						if (attachment.ContainsKey("content_type"))
						{
							contentType = (string)attachment.Get("content_type");
							Log.W(CBLDatabase.Tag, "Found attachment that uses content_type field name instead of content-type: "
								 + attachment);
						}
						else
						{
							if (attachment.ContainsKey("content-type"))
							{
								contentType = (string)attachment.Get("content-type");
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
			Log.D(CBLDatabase.Tag, "Uploadeding multipart request.  Revision: " + revision);
			AsyncTaskStarted();
			SendAsyncMultipartRequest("PUT", path, multiPart, new _CBLRemoteRequestCompletionBlock_322
				(this));
			return true;
		}

		private sealed class _CBLRemoteRequestCompletionBlock_322 : CBLRemoteRequestCompletionBlock
		{
			public _CBLRemoteRequestCompletionBlock_322(CBLPusher _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void OnCompletion(object result, Exception e)
			{
				if (e != null)
				{
					Log.E(CBLDatabase.Tag, "Exception uploading multipart request", e);
					this._enclosing.error = e;
				}
				else
				{
					Log.D(CBLDatabase.Tag, "Uploaded multipart request.  Result: " + result);
				}
				this._enclosing.AsyncTaskFinished(1);
			}

			private readonly CBLPusher _enclosing;
		}
	}
}
