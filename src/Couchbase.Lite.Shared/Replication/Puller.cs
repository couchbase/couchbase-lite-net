//
// Puller.cs
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
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using System.Web;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Couchbase.Lite.Replicator
{
    internal class Puller : Replication, IChangeTrackerClient
	{
		private const int MaxOpenHttpConnections = 16;

        readonly string Tag = "Puller";

        protected internal Batcher<IList<Object>> downloadsToInsert;

		protected internal IList<RevisionInternal> revsToPull;

		protected internal ChangeTracker changeTracker;

		protected internal SequenceMap pendingSequences;

		protected internal volatile int httpConnectionCount;

        private readonly Object locker = new object ();

        /// <summary>Constructor</summary>
        internal Puller(Database db, Uri remote, bool continuous, TaskFactory workExecutor)
            : this(db, remote, continuous, null, workExecutor) { }

		/// <summary>Constructor</summary>
        internal Puller(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor) 
            : base(db, remote, continuous, clientFactory, workExecutor) {  }

        #region implemented abstract members of Replication

        public HttpClientHandler HttpHandler { get { return clientFactory.HttpHandler; } }

        public override IEnumerable<string> DocIds { get; set; }

        public override IDictionary<string, string> Headers 
        {
            get { return clientFactory.Headers; } 
            set { clientFactory.Headers = value; } 
        }

        #endregion

        public override bool IsPull { get { return true; } }

        public override bool CreateTarget { get { return false; } set { return; /* No-op intended. Only used in Pusher. */ } }

        internal override void BeginReplicating()
		{
			if (downloadsToInsert == null)
			{
				const int capacity = 200;
				const int delay = 1000;
                downloadsToInsert = new Batcher<IList<Object>> (WorkExecutor, capacity, delay, InsertRevisions);
			}

			pendingSequences = new SequenceMap();

            Log.W(Tag, this + ": starting ChangeTracker with since=" + LastSequence);

            var mode = Continuous 
                       ? ChangeTracker.ChangeTrackerMode.LongPoll 
                       : ChangeTracker.ChangeTrackerMode.OneShot;

            changeTracker = new ChangeTracker(RemoteUrl, mode, LastSequence, true, this, WorkExecutor);

            Log.W(Tag, this + ": started ChangeTracker " + changeTracker);

            if (Filter != null)
			{
                changeTracker.SetFilterName(Filter);
                if (FilterParams != null)
				{
                    changeTracker.SetFilterParams(FilterParams.ToDictionary(kvp=>kvp.Key, kvp=>(Object)kvp.Value));
				}
			}
			if (!continuous)
			{
				AsyncTaskStarted();
			}
			changeTracker.Start();
		}

		public override void Stop()
		{
            if (!IsRunning)
			{
				return;
			}
			if (changeTracker != null)
			{
                Log.D(Tag, this + ": stopping changetracker " + changeTracker);

				changeTracker.SetClient(null);
				// stop it from calling my changeTrackerStopped()
				changeTracker.Stop();
				changeTracker = null;
                if (!Continuous)
                {   
                    Log.D(Tag, this + "|" + Sharpen.Thread.CurrentThread() + ": stop() calling asyncTaskFinished()");
					AsyncTaskFinished(1);
				}
			}
			// balances asyncTaskStarted() in beginReplicating()
            lock (locker)
			{
				revsToPull = null;
			}
            base.Stop();
		}

        internal override void Stopped()
		{
            if (downloadsToInsert != null)
            {
                downloadsToInsert.Flush();
                //downloadsToInsert = null;
            }
			base.Stopped();
		}

		// Got a _changes feed entry from the ChangeTracker.
		public void ChangeTrackerReceivedChange(IDictionary<string, object> change)
		{
            var lastSequence = change.Get("seq").ToString();
            var docID = (string)change.Get("id");
            if (docID == null)
            {
                return;
            }

            if (!LocalDatabase.IsValidDocumentId(docID))
            {
                Log.W(Tag, string.Format("{0}: Received invalid doc ID from _changes: {1}", this, change));
                return;
            }

            var deleted = (change.ContainsKey("deleted") && ((bool)change.Get("deleted")).Equals(true));

            var changesContainer = change.Get("changes") as JContainer;
            var changes = changesContainer.ToArray();
            foreach (var changeObj in changes)
            {
                var changeDict = changeObj.ToObject<IDictionary<string, object>>();
                var revID = (string)changeDict.Get("rev");
                if (revID == null)
                {
                    continue;
                }
                var rev = new PulledRevision(docID, revID, deleted, LocalDatabase);
                rev.SetRemoteSequenceID(lastSequence);
                Log.D(Tag, this + ": adding rev to inbox " + rev);
                AddToInbox(rev);
            }
            ChangesCount += changes.Length;
            while (revsToPull != null && revsToPull.Count > 1000)
            {
                try
                {
                    // Presumably we are letting 1 or more other threads do something while we wait.
                    Sharpen.Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    Log.W(Tag, "Swalling exception while sleeping after receiving changetracker changes.", e);
                    // swallow
                }
            }
		}

		// <-- TODO: why is this here?
		public void ChangeTrackerStopped(ChangeTracker tracker)
		{
            Log.W(Tag, this + ": ChangeTracker " + tracker + " stopped");
            if (LastError == null && tracker.GetLastError() != null)
            {
                LastError = tracker.GetLastError();
            }
            changeTracker = null;
            if (Batcher != null)
            {
                Log.D(Tag, this + ": calling batcher.flush().  batcher.count() is " + Batcher.Count());

                Batcher.Flush();
            }
            if (!Continuous)
            {
                var t = Task.Factory.StartNew(() => 
                { 
                    Task inner =Task.Factory.StartNew(() => {}); 
                    return inner; 
                });

                WorkExecutor.StartNew(() =>
                {
                    AsyncTaskFinished(1);
                });
            }
		}

        public HttpClient GetHttpClient()
		{
            return clientFactory.GetHttpClient();
		}

		/// <summary>Process a bunch of remote revisions from the _changes feed at once</summary>
        internal override void ProcessInbox(RevisionList inbox)
		{
            Debug.Assert(inbox != null);

            // Ask the local database which of the revs are not known to it:
            //Log.w(Database.TAG, String.format("%s: Looking up %s", this, inbox));
            var lastInboxSequence = ((PulledRevision)inbox[inbox.Count - 1]).GetRemoteSequenceID
                                       ();
            var total = ChangesCount - inbox.Count;
            if (!LocalDatabase.FindMissingRevisions(inbox))
            {
                Log.W(Tag, string.Format("{0} failed to look up local revs", this));
                inbox = null;
            }

            //introducing this to java version since inbox may now be null everywhere
            var inboxCount = 0;
            if (inbox != null)
            {
                inboxCount = inbox.Count;
            }
            ChangesCount = total + inboxCount;

            if (inboxCount == 0)
            {
                // Nothing to do. Just bump the lastSequence.
                Log.W(Tag, string.Format("{0} no new remote revisions to fetch", this));

                var seq = pendingSequences.AddValue(lastInboxSequence);
                pendingSequences.RemoveSequence(seq);
                LastSequence = pendingSequences.GetCheckpointedValue();
                return;
            }

            Log.V(Tag, this + " fetching " + inboxCount + " remote revisions...");
            //Log.v(Database.TAG, String.format("%s fetching remote revisions %s", this, inbox));
            // Dump the revs into the queue of revs to pull from the remote db:
            lock (locker) {
                if (revsToPull == null) {
                    revsToPull = new AList<RevisionInternal> (200);
                }
                for (int i = 0; i < inbox.Count; i++) {
                    var rev = (PulledRevision)inbox [i];
                    // FIXME add logic here to pull initial revs in bulk
                    rev.SetSequence (pendingSequences.AddValue (rev.GetRemoteSequenceID ()));
                    revsToPull.AddItem (rev);
                }
            }
            PullRemoteRevisions();
		}

		/// <summary>
		/// Start up some HTTP GETs, within our limit on the maximum simultaneous number
		/// The entire method is not synchronized, only the portion pulling work off the list
		/// Important to not hold the synchronized block while we do network access
		/// </summary>
		public void PullRemoteRevisions()
		{
            Log.D(Tag, this + ": pullRemoteRevisions() with revsToPull size: " + revsToPull.Count);

            //find the work to be done in a synchronized block
            var workToStartNow = new AList<RevisionInternal>();
            lock (locker)
            {
                while (httpConnectionCount + workToStartNow.Count < MaxOpenHttpConnections && revsToPull != null && revsToPull.Count > 0)
                {
                    var work = revsToPull.Remove(0);
                    Log.D(Tag, this + ": add " + work + " to workToStartNow");
                    workToStartNow.AddItem(work);
                }
            }
            //actually run it outside the synchronized block
            foreach (var rev in workToStartNow)
            {
                PullRemoteRevision(rev);
            }
		}

		/// <summary>Fetches the contents of a revision from the remote db, including its parent revision ID.
		/// 	</summary>
		/// <remarks>
		/// Fetches the contents of a revision from the remote db, including its parent revision ID.
		/// The contents are stored into rev.properties.
		/// </remarks>
        internal void PullRemoteRevision(RevisionInternal rev)
		{
            Log.D(Tag, this + "|" + Thread.CurrentThread() + ": pullRemoteRevision with rev: " + rev);
            Log.D(Tag, this + "|" + Thread.CurrentThread() + ": pullRemoteRevision() calling asyncTaskStarted()");

            AsyncTaskStarted();

            httpConnectionCount++;

            // Construct a query. We want the revision history, and the bodies of attachments that have
            // been added since the latest revisions we have locally.
            // See: http://wiki.apache.org/couchdb/HTTP_Document_API#Getting_Attachments_With_a_Document
            var path = new StringBuilder("/" + HttpUtility.UrlEncode(rev.GetDocId()) + "?rev=" + HttpUtility.UrlEncode(rev.GetRevId()) + "&revs=true&attachments=true");
            var knownRevs = KnownCurrentRevIDs(rev);
            if (knownRevs == null)
            {
                //this means something is wrong, possibly the replicator has shut down
                Log.D(Tag, this + "|" + Thread.CurrentThread() + ": pullRemoteRevision() calling asyncTaskFinished()");
                AsyncTaskFinished(1);
                httpConnectionCount--;
                return;
            }

            if (knownRevs.Count > 0)
            {
                path.Append("&atts_since=");
                path.Append(JoinQuotedEscaped(knownRevs));
            }

            //create a final version of this variable for the log statement inside
            //FIXME find a way to avoid this
            var pathInside = path.ToString();
            SendAsyncMultipartDownloaderRequest(HttpMethod.Get, pathInside, null, LocalDatabase, (result, e) => 
                {
                    try 
                    {
                        // OK, now we've got the response revision:
                        Log.D (Tag, this + ": pullRemoteRevision got response for rev: " + rev);
                        if (result != null)
                        {
                            var properties = ((JObject)result).ToObject<IDictionary<string, object>>();
                            var history = Database.ParseCouchDBRevisionHistory (properties);

                            if (history != null) 
                            {
                                rev.SetProperties (properties);
                                // Add to batcher ... eventually it will be fed to -insertRevisions:.
                                var toInsert = new AList<object> ();
                                toInsert.AddItem (rev);
                                toInsert.AddItem (history);
                                Log.D (Tag, this + ": pullRemoteRevision add rev: " + rev + " to batcher");
                                downloadsToInsert.QueueObject (toInsert);
                                Log.D (Tag, this + "|" + Thread.CurrentThread() + ": pullRemoteRevision.onCompletion() calling asyncTaskStarted()");
                                AsyncTaskStarted ();
                            } 
                            else 
                            {
                                Log.W (Tag, this + ": Missing revision history in response from " + pathInside);
                                CompletedChangesCount += 1;
                            }
                        } 
                        else 
                        {
                            if (e != null) 
                            {
                                Log.E (Tag, "Error pulling remote revision", e);
                                LastError = e;
                            }
                            CompletedChangesCount += 1;
                        }
                    }
                    finally
                    {
                        Log.D (Tag, this + "|" + Thread.CurrentThread() + ": pullRemoteRevision.onCompletion() calling asyncTaskFinished()");
                        AsyncTaskFinished (1);
                    }
                    
                    // Note that we've finished this task; then start another one if there
                    // are still revisions waiting to be pulled:
                    --httpConnectionCount;
                    PullRemoteRevisions ();
                });
		}

		/// <summary>This will be called when _revsToInsert fills up:</summary>
        public void InsertRevisions(IList<IList<Object>> revs)
		{
            Log.I(Tag, this + " inserting " + revs.Count + " revisions...");
            //Log.v(Database.TAG, String.format("%s inserting %s", this, revs));

            revs.Sort(new RevisionComparer());

            if (LocalDatabase == null)
            {
                AsyncTaskFinished(revs.Count);
                return;
            }

            LocalDatabase.BeginTransaction();

            var success = false;
            try
            {
                foreach (var revAndHistory in revs)
                {
                    var rev = (PulledRevision)revAndHistory[0];
                    var fakeSequence = rev.GetSequence();
                    var history = (IList<String>)revAndHistory[1];

                    // Insert the revision:
                    try
                    {
                        LocalDatabase.ForceInsert(rev, history, RemoteUrl);
                    }
                    catch (CouchbaseLiteException e)
                    {
                        if (e.GetCBLStatus().GetCode() == StatusCode.Forbidden)
                        {
                            Log.I(Tag, this + ": Remote rev failed validation: " + rev);
                        }
                        else
                        {
                            Log.W(Tag, this + " failed to write " + rev + ": status=" + e.GetCBLStatus().GetCode());
                            LastError = e;
                            continue;
                        }
                    }
                    pendingSequences.RemoveSequence(fakeSequence);
                }

                Log.W(Tag, this + " finished inserting " + revs.Count + " revisions");

                LastSequence = pendingSequences.GetCheckpointedValue();
                success = true;
            }
            catch (SQLException e)
            {
                Log.E(Tag, this + ": Exception inserting revisions", e);
            }
            finally
            {
                LocalDatabase.EndTransaction(success);
                Log.D(Tag, this + "|" + Thread.CurrentThread() + ": insertRevisions() calling asyncTaskFinished()");
                AsyncTaskFinished(revs.Count);
            }
            CompletedChangesCount += revs.Count;
		}

        private sealed class RevisionComparer : IComparer<IList<Object>>
		{
            public RevisionComparer()
			{
			}

            public int Compare(IList<Object> list1, IList<Object> list2)
			{
                var reva = (RevisionInternal)list1[0];
                var revb = (RevisionInternal)list2[0];
				return Misc.TDSequenceCompare(reva.GetSequence(), revb.GetSequence());
			}
		}

        private IList<String> KnownCurrentRevIDs(RevisionInternal rev)
		{
            if (LocalDatabase != null)
			{
                return LocalDatabase.GetAllRevisionsOfDocumentID(rev.GetDocId(), true).GetAllRevIds();
			}
			return null;
		}

		public string JoinQuotedEscaped(IList<string> strings)
		{
			if (strings.Count == 0)
			{
				return "[]";
			}

            IEnumerable<Byte> json = null;

			try
			{
                json = Manager.GetObjectMapper().WriteValueAsBytes(strings);
			}
			catch (Exception e)
			{
				Log.W(Tag, "Unable to serialize json", e);
			}

			return HttpUtility.UrlEncode(Runtime.GetStringForBytes(json));
		}

        public Boolean GoOffline()
        {
            Log.D(Tag, this + " goOffline() called, stopping changeTracker: " + changeTracker);
            if (!base.GoOffline())
            {
                return false;
            }
            if (changeTracker != null)
            {
                changeTracker.Stop();
            }
            return true;
        }
	}

}
