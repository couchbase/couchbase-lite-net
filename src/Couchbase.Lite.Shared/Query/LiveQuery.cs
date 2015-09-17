//
// LiveQuery.cs
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
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    /// <summary>
    /// A Couchbase Lite <see cref="Couchbase.Lite.View"/> <see cref="Couchbase.Lite.Query"/> that 
    /// automatically refreshes every time the <see cref="Couchbase.Lite.Database"/> changes 
    /// in a way that would affect the results.
    /// </summary>
    public sealed class LiveQuery : Query
    {
        #region Constants

        private const string TAG = "LiveQuery";
        private const int DEFAULT_QUERY_TIMEOUT = 90000; // milliseconds.
        private const double DEFAULT_UPDATE_INTERVAL = 0.5;

        #endregion

        #region Variables

        /// <summary>
        /// Adds or removed a <see cref="Couchbase.Lite.LiveQuery"/> change delegate that will be called 
        /// whenever the Database changes in a way that would affect the results of the 
        /// <see cref="Couchbase.Lite.Query"/>.
        /// </summary>
        public event EventHandler<QueryChangeEventArgs> Changed {
            add { _changed = (EventHandler<QueryChangeEventArgs>)Delegate.Combine(_changed, value); }
            remove { _changed = (EventHandler<QueryChangeEventArgs>)Delegate.Remove(_changed, value); }
        }
        private EventHandler<QueryChangeEventArgs> _changed;

        private QueryEnumerator _rows;
        private long _lastSequence;
        private long _isUpdatingAtSequence;
        private bool _willUpdate;
        private bool _updateAgain;
        private double _updateInterval = DEFAULT_UPDATE_INTERVAL;
        private volatile bool _observing;
        private bool _runningState;

        #endregion

        #region Properties

        //Properties
        /// <summary>
        /// Gets the results of the <see cref="Couchbase.Lite.Query"/>. 
        /// The value will be null until the initial <see cref="Couchbase.Lite.Query"/> completes.
        /// </summary>
        /// <value>The row results of the <see cref="Couchbase.Lite.Query"/>.</value>
        public QueryEnumerator Rows
        { 
            get {
                Start();
                // Have to return a copy because the enumeration has to start at item #0 every time
                return _rows == null ? null : new QueryEnumerator(_rows);
            }
        }

        /// <summary>
        /// Returns the last error, if any, that occured while executing 
        /// the <see cref="Couchbase.Lite.Query"/>, otherwise null.
        /// </summary>
        /// <value>The last error.</value>
        public Exception LastError { get; private set; }

        // If a query is running and the user calls Stop() on this query, the Task
        // will be used in order to cancel the query in progress.
        private Task UpdateQueryTask { get; set; }
        private CancellationTokenSource UpdateQueryTokenSource { get; set; }

        #endregion

        #region Constructors

        internal LiveQuery(Query query) : base(query.Database, query.View) { 
            StartKey = query.StartKey;
            EndKey = query.EndKey;
            Descending = query.Descending;
            EndKeyDocId = query.EndKeyDocId;
            StartKeyDocId = query.StartKeyDocId;
            Prefetch = query.Prefetch;
            Limit = query.Limit;
            GroupLevel = query.GroupLevel;
            IncludeDeleted = query.IncludeDeleted;
            InclusiveEnd = query.InclusiveEnd;
            IndexUpdateMode = query.IndexUpdateMode;
            Keys = query.Keys;
            MapOnly = query.MapOnly;
            Skip = query.Skip;
        }

        #endregion

        #region Public Methods

        /// <summary>Sends the query to the server and returns an enumerator over the result rows (Synchronous).
        ///     </summary>
        /// <remarks>
        /// Sends the query to the server and returns an enumerator over the result rows (Synchronous).
        /// Note: In a CBLLiveQuery you should add a ChangeListener and call start() instead.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public override QueryEnumerator Run()
        {
            while (true) {
                try {
                    if (UpdateQueryTask.Status != TaskStatus.Canceled || UpdateQueryTask.Status != TaskStatus.RanToCompletion) {
                        Log.W(TAG, "Run called white update query task still running.");
                    }
                    WaitForRows();
                    break;
                } catch (OperationCanceledException) { //TODO: Review
                    continue;
                } catch (Exception e) {
                    LastError = e;
                    throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
                }
            }

            return _rows == null ? null : new QueryEnumerator (_rows);
        }

        /// <summary>
        /// Used to indicate that the options of the live query have changed since its first
        /// run.
        /// </summary>
        public void QueryOptionsChanged()
        {
            OnViewChanged(null, null);
        }

        /// <summary>Starts observing database changes.</summary>
        /// <remarks>
        /// Starts the <see cref="Couchbase.Lite.LiveQuery"/> and begins observing <see cref="Couchbase.Lite.Database"/> 
        /// changes. When the <see cref="Couchbase.Lite.Database"/> changes in a way that would affect the results of 
        /// the <see cref="Couchbase.Lite.Query"/>, the <see cref="Rows"/> property will be updated and any 
        /// <see cref="Changed"/> delegates will be notified.  Accessing the <see cref="Rows"/>  property or adding a
        /// <see cref="Changed"/> delegate will automatically start the <see cref="Couchbase.Lite.LiveQuery"/>.
        /// </remarks>
        public void Start()
        {
            if (_runningState) {
                Log.D(TAG, "start() called, but runningState is already true.  Ignoring.");
                return;
            } else {
                Log.D(TAG, "start() called");
                _runningState = true;
            }

            if (!_observing) {
                _observing = true;
                Database.Changed += OnDatabaseChanged;
                if (View != null) {
                    View.Changed += OnViewChanged;
                }
            }

            Update();
        }

        /// <summary>
        /// Stops the <see cref="Couchbase.Lite.LiveQuery"/> and stops observing Database changes.
        /// </summary>
        public void Stop()
        {
            if (!_runningState) {
                Log.D(TAG, "stop() called, but runningState is already false.  Ignoring.");
                return;
            } else {
                Log.D(TAG, "stop() called");
                _runningState = false;
            }

            if (_observing) {
                Database.Changed -= OnDatabaseChanged;
                _observing = false;
            }

            // slight diversion from iOS version -- cancel the queryFuture
            // regardless of the willUpdate value, since there can be an update in flight
            // with willUpdate set to false.  was needed to make testLiveQueryStop() unit test pass.
            if (UpdateQueryTokenSource != null && UpdateQueryTokenSource.Token.CanBeCanceled) {
                UpdateQueryTokenSource.Cancel();
                Log.D(TAG, "canceled update query token Source");
            } else {
                Log.D(TAG, "not cancelling update query token source.");
            }

            _willUpdate = false;
        }

        /// <summary>
        /// Blocks until the intial <see cref="Couchbase.Lite.Query"/> completes.
        /// </summary>
        /// <remarks>
        /// If an error occurs while executing the <see cref="Couchbase.Lite.Query"/>, <see cref="LastError"/> 
        /// will contain the exception. Can be cancelled if results are not returned after <see cref="DEFAULT_QUERY_TIMEOUT"/> (90 seconds).
        /// </remarks>
        public void WaitForRows()
        {
            Start();
            while (true) {
                try {
                    //FIXME: JHB: This is painful.  The UpdateQueryTask will null itself once it finishes
                    //and it will swallow any exceptions it had.  Needs investigation.
                    var taskToWait = UpdateQueryTask;
                    if (taskToWait != null) {
                        taskToWait.Wait(DEFAULT_QUERY_TIMEOUT, UpdateQueryTokenSource.Token);
                        LastError = taskToWait.Exception;
                    }
                    break;
                } catch (OperationCanceledException e) { //TODO: Review
                    Log.D(TAG, "Got operation cancel exception waiting for rows", e);
                    continue;
                } catch (Exception e) {
                    Log.E(TAG, "Got interrupted exception waiting for rows", e);
                    LastError = e;
                }
            }
        }

        #endregion
       
        #region Private Methods

        private void OnDatabaseChanged (object sender, DatabaseChangeEventArgs e)
        {
            if (_willUpdate) {
                return;
            }

            var updateInterval = _updateInterval * 2;
            foreach (var change in e.Changes) {
                if (change.SourceUrl == null) {
                    updateInterval /= 2;
                    break;
                }
            }

            _willUpdate = true;
            Log.D(TAG, "Will update after {0} sec...", updateInterval);
            Task.Delay(TimeSpan.FromSeconds(updateInterval)).ContinueWith(t =>
            {
                if(_willUpdate) {
                    Update();
                }
            });
        }

        private void OnViewChanged(View sender, EventArgs e)
        {
            _lastSequence = 0;
            Update();
        }

        private void RunUpdateAfterQueryFinishes(Task updateQueryTask, CancellationTokenSource updateQueryTaskTokenSource) 
        {
            if (!_runningState) {
                Log.D(TAG, "ReRunUpdateAfterQueryFinishes() fired, but running state == false. Ignoring.");
                return; // NOTE: Assuming that we don't want to lose rows we already retrieved.
            }

            try {
                Log.D(TAG, "Waiting for Query to finish");
                updateQueryTask.Wait(DEFAULT_QUERY_TIMEOUT, updateQueryTaskTokenSource.Token);
                if (_runningState && !updateQueryTaskTokenSource.IsCancellationRequested) {
                    Log.D(TAG, "Running Update() since Query finished");
                    Update();
                } else {
                    Log.D(TAG, "Update() not called because either !runningState ({0}) or cancelled ({1})", _runningState, updateQueryTaskTokenSource.IsCancellationRequested);
                }
            } catch (Exception e)
            {
                Log.E(TAG, "Got an exception waiting for Update Query Task to finish", e);
            } finally {
                UpdateQueryTask = null;
            }
        }

        /// <summary>
        /// Implements the updating of the <see cref="Rows"/> collection.
        /// </summary>
        private void Update()
        {
            _willUpdate = false;
            long lastSequence = Database.LastSequenceNumber;
            if (_rows != null && _lastSequence >= lastSequence) {
                return; // db hasn't changed since last query
            }

            if (_isUpdatingAtSequence > 0) {
                // Update already in progress; only schedule another one if db has changed since
                if (_isUpdatingAtSequence < lastSequence) {
                    _isUpdatingAtSequence = lastSequence;
                    _updateAgain = true;
                    return;
                }
            }
				

            if (!_runningState) {
                Log.W(TAG, "update() called, but running state == false.  Ignoring.");
                return;
            }

            _updateAgain = false;
            _isUpdatingAtSequence = lastSequence;
            UpdateQueryTokenSource = new CancellationTokenSource();

            UpdateQueryTask = Task.Factory.StartNew<QueryEnumerator>(base.Run, UpdateQueryTokenSource.Token)
                .ContinueWith(UpdateFinished, Database.Manager.CapturedContext.Scheduler);
        }

        private void UpdateFinished(Task<QueryEnumerator> runTask)
        {
            _isUpdatingAtSequence = 0;

            if (UpdateQueryTokenSource.IsCancellationRequested)
                return;

            UpdateQueryTask = null;
            if (_updateAgain) {
                Update();
            }

            if (runTask.Status != TaskStatus.RanToCompletion) {
                Log.W(String.Format("Query Updated task did not run to completion ({0})", runTask.Status), runTask.Exception);
                return; // NOTE: Assuming that we don't want to lose rows we already retrieved.
            }

            _rows = runTask.Result; // NOTE: Should this be 'append' instead of 'replace' semantics? If append, use a concurrent collection.
            Log.D(TAG, "UpdateQueryTask results obtained.");
            LastError = runTask.Exception;

            var evt = _changed;
            if (evt == null)
                return; // No delegates were subscribed, so no work to be done.

            var args = new QueryChangeEventArgs (this, _rows, LastError);
            evt (this, args);
        }

        #endregion
    
    }

    /// <summary>
    /// Query change event arguments.
    /// </summary>
    public class QueryChangeEventArgs : EventArgs 
    {

        #region Properties
            
        //Properties
        /// <summary>
        /// Gets the LiveQuery that raised the event.
        /// </summary>
        /// <value>The LiveQuery that raised the event.</value>
        public LiveQuery Source { get; private set; }

        /// <summary>
        /// Gets the results of the Query.
        /// </summary>
        /// <value>The results of the Query.</value>
        public QueryEnumerator Rows { get; private set; }

        /// <summary>
        /// Returns the error, if any, that occured while executing 
        /// the <see cref="Couchbase.Lite.Query"/>, otherwise null.
        /// </summary>
        /// <value>The error.</value>
        public Exception Error { get; private set; }

        #endregion

        #region Constructors

        internal QueryChangeEventArgs (LiveQuery liveQuery, QueryEnumerator enumerator, Exception error)
        {
            Source = liveQuery;
            Rows = enumerator;
            Error = error;
        }

        #endregion
    }
        
}
