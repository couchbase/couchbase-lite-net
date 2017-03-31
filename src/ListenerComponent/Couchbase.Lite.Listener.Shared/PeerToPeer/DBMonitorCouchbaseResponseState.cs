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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using System;

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// This class will wait for the database to change before writing to and
    /// possibly closing the HTTP response
    /// </summary>
    internal sealed class DBMonitorCouchbaseResponseState : ICouchbaseResponseState
    {

        #region Constants

        private const string TAG = "DBMonitorCouchbaseResponseState";

        #endregion

        #region Variables

        private Timer _heartbeatTimer;
        private long _since;
        private ChangesOptions _options;
        private int _filled;

        #endregion

        #region Properties

        public ICouchbaseListenerContext Context { get; set; }

        public Database Db 
        {
            get {
                return _db;
            } set {
                _db = value;
            }
        }
        private Database _db;

        /// <summary>
        /// The changes feed mode being used to listen to the database
        /// </summary>
        public ChangesFeedMode ChangesFeedMode { get; set; }

        /// <summary>
        /// Whether or not to write the document properties along with the changes
        /// </summary>
        public bool ChangesIncludeDocs { get; set; }

        /// <summary>
        /// Whether or not to include conflict revisions in the changes
        /// </summary>
        public bool ChangesIncludeConflicts { get; set; }

        /// <summary>
        /// The options for retrieving data from the DB
        /// </summary>
        public DocumentContentOptions ContentOptions { get; set; }

        /// <summary>
        /// The delegate to filter the changes being written
        /// </summary>
        public FilterDelegate ChangesFilter { get; set; }

        /// <summary>
        /// The parameters used in the change filter
        /// </summary>
        /// <value>The filter parameters.</value>
        public IDictionary<string, object> FilterParams { get; set; }

        //ICouchbaseResponseState
        public CouchbaseLiteResponse Response { get; set; }

        //ICouchbaseResponseState
        public bool IsAsync { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        public DBMonitorCouchbaseResponseState() 
        {
            IsAsync = false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="response">The response to write to</param>
        public DBMonitorCouchbaseResponseState(CouchbaseLiteResponse response) : this()
        {
            Response = response;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Subscribes this object to the given database's <c>Changed</c> event for
        /// processing
        /// </summary>
        /// <param name="db">Db.</param>
        public bool SubscribeToDatabase(Database db, long since, ChangesOptions options)
        {
            if(db == null) {
                return false;
            }


            Db = db;
            IsAsync = true;
            _since = since;
            _options = options;
            if(ChangesFeedMode == ChangesFeedMode.LongPoll) {
                var currentChanges = Db.ChangesSince(since, options, ChangesFilter, FilterParams);
                IsAsync = !currentChanges.Any();
                if(!IsAsync) {
                    if(ChangesIncludeConflicts) {
                        Response.JsonBody = new Body(DatabaseMethods.ResponseBodyForChanges(currentChanges, since, options.Limit, this));
                    } else {
                        Response.JsonBody = new Body(DatabaseMethods.ResponseBodyForChanges(currentChanges, since, this));
                    }
                } else {
                    Db.Changed += DatabaseChanged;
                }
            } else {
                Db.Changed += DatabaseChanged;
            }

            return IsAsync;
        }

        /// <summary>
        /// Starts a timer for writing heartbeat messages to the client
        /// </summary>
        /// <param name="response">The message to write</param>
        /// <param name="interval">The interval at which to write the message (in milliseconds)</param>
        public void StartHeartbeat(string response, TimeSpan interval)
        {
            if (interval.TotalMilliseconds <= 0 || _heartbeatTimer != null) {
                return;
            }

            IsAsync = true;
            Response.WriteHeaders();
            Log.To.Router.V(TAG, "Starting heartbeat at intervals of {0}", interval);
            _heartbeatTimer = new Timer(SendHeartbeatResponse, Encoding.UTF8.GetBytes(response), interval, interval);
        }

        #endregion

        #region Private Methods

        // Attempts to write the heartbeat message to the client
        private void SendHeartbeatResponse(object state)
        {
            Log.To.Router.I(TAG, "Sending heartbeat to client");
            if (!Response.WriteData((byte[])state, false)) {
                Log.To.Router.W(TAG, "Failed to write heartbeat");
                if (_heartbeatTimer != null) {
                    _heartbeatTimer.Dispose();
                    _heartbeatTimer = null;
                }

                Terminate();
            }
        }

        // Processes a change in the subscribed database
        private void DatabaseChanged(object sender, DatabaseChangeEventArgs args)
        {
            if (ChangesFeedMode == ChangesFeedMode.LongPoll) {
                // Only send changes if it is the first VALID time (i.e. has at least one change
                // and hasn't started another write yet)
                var changes = Db.ChangesSince(_since, _options, ChangesFilter, FilterParams);
                if (changes.Count > 0 && Interlocked.CompareExchange(ref _filled, 1, 0) == 0) {
                    WriteChanges(changes);
                }

                return;
            } else if (Interlocked.CompareExchange(ref _filled, 1, 0) == 0) {
                // Backfill potentially missed revisions between the check for subscription need
                // and actual subscription
                WriteChanges(Db.ChangesSince(_since, _options, ChangesFilter, FilterParams));
                return;
            }

            var changesToSend = new RevisionList();
            foreach (var change in args.Changes) {
                var rev = change.AddedRevision;
                var winningRev = change.WinningRevisionId;

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
                            Db.LoadRevisionBody(rev);
                        }
                    }
                }

                if (!Db.RunFilter(ChangesFilter, FilterParams, rev)) {
                    continue;
                }

                changesToSend.Add(rev);
            }

            WriteChanges(changesToSend);
        }

        private void WriteChanges(RevisionList changes)
        {
            if(changes.Count > 0) {
                if(ChangesFeedMode == ChangesFeedMode.LongPoll) {
                    var body = new Body(DatabaseMethods.ResponseBodyForChanges(changes, 0, this));
                    Response.WriteData(body.AsJson(), true);
                    Terminate();
                } else {
                    foreach(var rev in changes) {
                        var written = Response.SendContinuousLine(DatabaseMethods.ChangesDictForRev(rev, this), ChangesFeedMode);
                        if(!written) {
                            Terminate();
                            break;
                        }
                    }
                }
            }
        }

        // Tear down this object because an error occurred
        private void Terminate()
        {
            var db = Interlocked.Exchange(ref _db, null);
            if (db == null) {
                return;
            }

            Log.To.Router.I(TAG, "Shutting down DBMonitorCouchbaseState");
            db.Changed -= DatabaseChanged;
            CouchbaseLiteRouter.ResponseFinished(this);
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        #endregion

    }
}

