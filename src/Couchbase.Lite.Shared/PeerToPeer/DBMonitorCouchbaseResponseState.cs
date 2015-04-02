//
//  DBMonitorCouchbaseResponseState.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;

using Couchbase.Lite.Replicator;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;

namespace Couchbase.Lite.PeerToPeer
{
    internal class DBMonitorCouchbaseResponseState : ICouchbaseResponseState
    {
        private const string TAG = "ICouchbaseResponseState";
        private Database _db;
        private Timer _heartbeatTimer;

        public CouchbaseLiteResponse Response { get; set; }

        public bool IsAsync { get; set; }

        public ChangesFeedMode ChangesMode { get; set; }

        public bool ChangesIncludeDocs { get; set; }

        public bool ChangesIncludeConflicts { get; set; }

        public FilterDelegate ChangesFilter { get; set; }

        public RevisionList Changes { get; set; }

        public DBMonitorCouchbaseResponseState() 
        {
            IsAsync = false;
        }

        public DBMonitorCouchbaseResponseState(CouchbaseLiteResponse response) : this()
        {
            Response = response;
        }

        public void SubscribeToDatabase(Database db)
        {
            if (db == null) {
                return;
            }

            IsAsync = true;
            _db = db;
            _db.Changed += DatabaseChanged;
        }

        public void StartHeartbeat(string response, double interval)
        {
            if (interval <= 0 || _heartbeatTimer != null) {
                return;
            }

            IsAsync = true;
            _heartbeatTimer = new Timer(SendHeartbeatResponse, Encoding.UTF8.GetBytes(response), 0, (int)interval);
        }

        private void SendHeartbeatResponse(object state)
        {
            Response.WriteData((byte[])state, false);
        }

        private void DatabaseChanged(object sender, DatabaseChangeEventArgs args)
        {
            foreach (var change in args.Changes) {
                var rev = change.AddedRevision;
                var winningRev = change.WinningRevision;

                if (!ChangesIncludeConflicts) {
                    if (winningRev == null) {
                        continue; // this change doesn't affect the winning rev ID, no need to send it
                    }

                    if (rev.Equals(winningRev)) {
                        // This rev made a _different_ rev current, so substitute that one.
                        // We need to emit the current sequence # in the feed, so put it in the rev.
                        // This isn't correct internally (this is an old rev so it has an older sequence)
                        // but consumers of the _changes feed don't care about the internal state.
                        if (ChangesIncludeDocs) {
                            _db.LoadRevisionBody(rev, DocumentContentOptions.None);
                        }
                    }
                }

                if (!_db.RunFilter(ChangesFilter, null, rev)) {
                    continue;
                }

                if (ChangesMode == ChangesFeedMode.LongPoll) {
                    Changes.Add(rev);
                } else {
                    Log.D(TAG, "Sending continuous change chunk");
                    DatabaseMethods.SendContinuousLine(DatabaseMethods.ChangesDictForRev(rev, this), this);
                }
            }

            if (ChangesMode == ChangesFeedMode.LongPoll && Changes.Count > 0) {
                Response.WriteHeaders();
                Response.JsonBody = new Body(DatabaseMethods.ResponseBodyForChanges(Changes, 0, this));
                Response.WriteToContext();
                CouchbaseLiteRouter.ResponseFinished(this);
            }
        }

       
    }
}

