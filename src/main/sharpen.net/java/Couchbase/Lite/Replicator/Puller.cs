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
	public class Puller : Replication, ChangeTrackerClient
	{
		private const int MaxOpenHttpConnections = 16;

		protected internal Batcher<IList<object>> downloadsToInsert;

		protected internal IList<RevisionInternal> revsToPull;

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
				changeTracker.SetClient(null);
				// stop it from calling my changeTrackerStopped()
				changeTracker.Stop();
				changeTracker = null;
				if (!continuous)
				{
					AsyncTaskFinished(1);
				}
			}
			// balances asyncTaskStarted() in beginReplicating()
			lock (this)
			{
				revsToPull = null;
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
				downloadsToInsert = new Batcher<IList<object>>(workExecutor, capacity, delay, new 
					_BatchProcessor_111(this));
			}
			pendingSequences = new SequenceMap();
			Log.W(Database.Tag, this + " starting ChangeTracker with since=" + lastSequence);
			changeTracker = new ChangeTracker(remote, continuous ? ChangeTracker.ChangeTrackerMode
				.LongPoll : ChangeTracker.ChangeTrackerMode.OneShot, lastSequence, this);
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
				AsyncTaskStarted();
			}
			changeTracker.Start();
		}

		private sealed class _BatchProcessor_111 : BatchProcessor<IList<object>>
		{
			public _BatchProcessor_111(Puller _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Process(IList<IList<object>> inbox)
			{
				this._enclosing.InsertRevisions(inbox);
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
		public virtual void ChangeTrackerReceivedChange(IDictionary<string, object> change
			)
		{
			string lastSequence = change.Get("seq").ToString();
			string docID = (string)change.Get("id");
			if (docID == null)
			{
				return;
			}
			if (!Database.IsValidDocumentId(docID))
			{
				Log.W(Database.Tag, string.Format("%s: Received invalid doc ID from _changes: %s"
					, this, change));
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
				AddToInbox(rev);
			}
			SetChangesCount(GetChangesCount() + changes.Count);
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
		public virtual void ChangeTrackerStopped(ChangeTracker tracker)
		{
			Log.W(Database.Tag, this + ": ChangeTracker stopped");
			if (error == null && tracker.GetLastError() != null)
			{
				error = tracker.GetLastError();
			}
			changeTracker = null;
			if (batcher != null)
			{
				batcher.Flush();
			}
			if (!IsContinuous())
			{
				AsyncTaskFinished(1);
			}
		}

		// balances -asyncTaskStarted in -startChangeTracker
		[InterfaceAudience.Private]
		public virtual HttpClient GetHttpClient()
		{
			HttpClient httpClient = this.clientFactory.GetHttpClient();
			return httpClient;
		}

		/// <summary>Process a bunch of remote revisions from the _changes feed at once</summary>
		[InterfaceAudience.Private]
		protected internal override void ProcessInbox(RevisionList inbox)
		{
			// Ask the local database which of the revs are not known to it:
			//Log.w(Database.TAG, String.format("%s: Looking up %s", this, inbox));
			string lastInboxSequence = ((PulledRevision)inbox[inbox.Count - 1]).GetRemoteSequenceID
				();
			int total = GetChangesCount() - inbox.Count;
			if (!db.FindMissingRevisions(inbox))
			{
				Log.W(Database.Tag, string.Format("%s failed to look up local revs", this));
				inbox = null;
			}
			//introducing this to java version since inbox may now be null everywhere
			int inboxCount = 0;
			if (inbox != null)
			{
				inboxCount = inbox.Count;
			}
			if (GetChangesCount() != total + inboxCount)
			{
				SetChangesCount(total + inboxCount);
			}
			if (inboxCount == 0)
			{
				// Nothing to do. Just bump the lastSequence.
				Log.W(Database.Tag, string.Format("%s no new remote revisions to fetch", this));
				long seq = pendingSequences.AddValue(lastInboxSequence);
				pendingSequences.RemoveSequence(seq);
				SetLastSequence(pendingSequences.GetCheckpointedValue());
				return;
			}
			Log.V(Database.Tag, this + " fetching " + inboxCount + " remote revisions...");
			//Log.v(Database.TAG, String.format("%s fetching remote revisions %s", this, inbox));
			// Dump the revs into the queue of revs to pull from the remote db:
			lock (this)
			{
				if (revsToPull == null)
				{
					revsToPull = new AList<RevisionInternal>(200);
				}
				for (int i = 0; i < inbox.Count; i++)
				{
					PulledRevision rev = (PulledRevision)inbox[i];
					// FIXME add logic here to pull initial revs in bulk
					rev.SetSequence(pendingSequences.AddValue(rev.GetRemoteSequenceID()));
					revsToPull.AddItem(rev);
				}
			}
			PullRemoteRevisions();
		}

		/// <summary>
		/// Start up some HTTP GETs, within our limit on the maximum simultaneous number
		/// The entire method is not synchronized, only the portion pulling work off the list
		/// Important to not hold the synchronized block while we do network access
		/// </summary>
		[InterfaceAudience.Private]
		public virtual void PullRemoteRevisions()
		{
			//find the work to be done in a synchronized block
			IList<RevisionInternal> workToStartNow = new AList<RevisionInternal>();
			lock (this)
			{
				while (httpConnectionCount + workToStartNow.Count < MaxOpenHttpConnections && revsToPull
					 != null && revsToPull.Count > 0)
				{
					RevisionInternal work = revsToPull.Remove(0);
					workToStartNow.AddItem(work);
				}
			}
			//actually run it outside the synchronized block
			foreach (RevisionInternal work_1 in workToStartNow)
			{
				PullRemoteRevision(work_1);
			}
		}

		/// <summary>Fetches the contents of a revision from the remote db, including its parent revision ID.
		/// 	</summary>
		/// <remarks>
		/// Fetches the contents of a revision from the remote db, including its parent revision ID.
		/// The contents are stored into rev.properties.
		/// </remarks>
		[InterfaceAudience.Private]
		public virtual void PullRemoteRevision(RevisionInternal rev)
		{
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
				//this means something is wrong, possibly the replicator has shut down
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
			SendAsyncMultipartDownloaderRequest("GET", pathInside, null, db, new _RemoteRequestCompletionBlock_305
				(this, rev, pathInside));
		}

		private sealed class _RemoteRequestCompletionBlock_305 : RemoteRequestCompletionBlock
		{
			public _RemoteRequestCompletionBlock_305(Puller _enclosing, RevisionInternal rev, 
				string pathInside)
			{
				this._enclosing = _enclosing;
				this.rev = rev;
				this.pathInside = pathInside;
			}

			public void OnCompletion(object result, Exception e)
			{
				// OK, now we've got the response revision:
				if (result != null)
				{
					IDictionary<string, object> properties = (IDictionary<string, object>)result;
					IList<string> history = Database.ParseCouchDBRevisionHistory(properties);
					if (history != null)
					{
						rev.SetProperties(properties);
						// Add to batcher ... eventually it will be fed to -insertRevisions:.
						IList<object> toInsert = new AList<object>();
						toInsert.AddItem(rev);
						toInsert.AddItem(history);
						this._enclosing.downloadsToInsert.QueueObject(toInsert);
						this._enclosing.AsyncTaskStarted();
					}
					else
					{
						Log.W(Database.Tag, this + ": Missing revision history in response from " + pathInside
							);
						this._enclosing.SetCompletedChangesCount(this._enclosing.GetCompletedChangesCount
							() + 1);
					}
				}
				else
				{
					if (e != null)
					{
						Log.E(Database.Tag, "Error pulling remote revision", e);
						this._enclosing.error = e;
					}
					this._enclosing.SetCompletedChangesCount(this._enclosing.GetCompletedChangesCount
						() + 1);
				}
				// Note that we've finished this task; then start another one if there
				// are still revisions waiting to be pulled:
				this._enclosing.AsyncTaskFinished(1);
				--this._enclosing.httpConnectionCount;
				this._enclosing.PullRemoteRevisions();
			}

			private readonly Puller _enclosing;

			private readonly RevisionInternal rev;

			private readonly string pathInside;
		}

		/// <summary>This will be called when _revsToInsert fills up:</summary>
		[InterfaceAudience.Private]
		public virtual void InsertRevisions(IList<IList<object>> revs)
		{
			Log.I(Database.Tag, this + " inserting " + revs.Count + " revisions...");
			//Log.v(Database.TAG, String.format("%s inserting %s", this, revs));
			revs.Sort(new _IComparer_361());
			if (db == null)
			{
				AsyncTaskFinished(revs.Count);
				return;
			}
			db.BeginTransaction();
			bool success = false;
			try
			{
				foreach (IList<object> revAndHistory in revs)
				{
					PulledRevision rev = (PulledRevision)revAndHistory[0];
					long fakeSequence = rev.GetSequence();
					IList<string> history = (IList<string>)revAndHistory[1];
					// Insert the revision:
					try
					{
						db.ForceInsert(rev, history, remote);
					}
					catch (CouchbaseLiteException e)
					{
						if (e.GetCBLStatus().GetCode() == Status.Forbidden)
						{
							Log.I(Database.Tag, this + ": Remote rev failed validation: " + rev);
						}
						else
						{
							Log.W(Database.Tag, this + " failed to write " + rev + ": status=" + e.GetCBLStatus
								().GetCode());
							error = new HttpResponseException(e.GetCBLStatus().GetCode(), null);
							continue;
						}
					}
					pendingSequences.RemoveSequence(fakeSequence);
				}
				Log.W(Database.Tag, this + " finished inserting " + revs.Count + " revisions");
				SetLastSequence(pendingSequences.GetCheckpointedValue());
				success = true;
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, this + ": Exception inserting revisions", e);
			}
			finally
			{
				db.EndTransaction(success);
				AsyncTaskFinished(revs.Count);
			}
			SetCompletedChangesCount(GetCompletedChangesCount() + revs.Count);
		}

		private sealed class _IComparer_361 : IComparer<IList<object>>
		{
			public _IComparer_361()
			{
			}

			public int Compare(IList<object> list1, IList<object> list2)
			{
				RevisionInternal reva = (RevisionInternal)list1[0];
				RevisionInternal revb = (RevisionInternal)list2[0];
				return Misc.TDSequenceCompare(reva.GetSequence(), revb.GetSequence());
			}
		}

		[InterfaceAudience.Private]
		internal virtual IList<string> KnownCurrentRevIDs(RevisionInternal rev)
		{
			if (db != null)
			{
				return db.GetAllRevisionsOfDocumentID(rev.GetDocId(), true).GetAllRevIds();
			}
			return null;
		}

		[InterfaceAudience.Private]
		public virtual string JoinQuotedEscaped(IList<string> strings)
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
				Log.W(Database.Tag, "Unable to serialize json", e);
			}
			return URLEncoder.Encode(Sharpen.Runtime.GetStringForBytes(json));
		}

		[InterfaceAudience.Private]
		internal override bool GoOffline()
		{
			if (!base.GoOffline())
			{
				return false;
			}
			changeTracker.Stop();
			return true;
		}
	}

	/// <summary>A revision received from a remote server during a pull.</summary>
	/// <remarks>A revision received from a remote server during a pull. Tracks the opaque remote sequence ID.
	/// 	</remarks>
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
