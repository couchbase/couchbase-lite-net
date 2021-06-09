// 
//  QueryBase.cs
// 
//  Copyright (c) 2021 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryBase : IQuery, IStoppable
    {
        #region Constants

        private const string Tag = nameof(QueryBase);
        private static readonly TimeSpan DefaultLiveQueryUpdateInterval = TimeSpan.FromMilliseconds(200);

        #endregion

        #region Variables

        [NotNull] private readonly Event<QueryChangedEventArgs> _changed = new Event<QueryChangedEventArgs>();
        [NotNull] protected readonly DisposalWatchdog _disposalWatchdog = new DisposalWatchdog(nameof(IQuery));
        private readonly TimeSpan _updateInterval;
        protected unsafe C4Query* _c4Query;
        protected Dictionary<string, int> _columnNames;
        private ListenerToken _databaseChangedToken;
        [NotNull] protected List<QueryResultSet> _history = new List<QueryResultSet>();
        private DateTime _lastUpdatedAt;
        private int _observingCount = 0;
        [NotNull] private Parameters _queryParameters = new Parameters();
        private AtomicBool _willUpdate = false;
        private bool _updating = false, _stopping = false;

        #endregion

        #region Properties

        public Database Database { get; set; }

        public Parameters Parameters
        {
            get => _queryParameters;
            set
            {
                _queryParameters = value.Freeze();
                Update();
            }
        }

        #endregion

        #region Constructors

        public QueryBase()
        {
            _updateInterval = DefaultLiveQueryUpdateInterval;
        }

        ~QueryBase()
        {
            Dispose(true);
        }

        #endregion

        #region Public Methods
        public void Stop()
        {
            if (_updating) {
                _stopping = true;
            } else {
                Stopped();
            }
        }
        #endregion

        #region Private Methods

        private void OnDatabaseChanged(object sender, DatabaseChangedEventArgs e)
        {
            if (_willUpdate)
            {
                return;
            }

            // External updates should poll less frequently
            var updateInterval = _updateInterval;

            var updateDelay = _lastUpdatedAt + updateInterval - DateTime.Now;
            UpdateAfter(updateDelay);
        }

        private void Stopped()
        {
            Database?.RemoveActiveStoppable(this);
            Database?.RemoveChangeListener(_databaseChangedToken);
            _stopping = false;
        }

        private void Update()
        {
            if (!_willUpdate)
                return;

            _updating = true;

            WriteLog.To.Query.I(Tag, $"{this}: Querying...");
            var oldEnum = _history.LastOrDefault();
            QueryResultSet newEnum = null;
            Exception error = null;
            if (oldEnum == null) {
                try {
                    var result = Execute();
                    if (result is NullResultSet) {
                        return;
                    }

                    newEnum = (QueryResultSet)result;
                } catch (Exception e) {
                    error = e;
                }
            } else {
                newEnum = oldEnum.Refresh();
                if (newEnum != null) {
                    _history.Add(newEnum);
                }
            }

            _updating = false;
            _willUpdate.Set(false);

            if (_stopping) {
                Stopped();
                return;
            }

            _lastUpdatedAt = DateTime.Now;

            var changed = true;
            if (newEnum != null) {
                if (oldEnum != null) {
                    WriteLog.To.Query.I(Tag, $"{this}: Changed!");
                }
            } else if (error != null) {
                WriteLog.To.Query.E(Tag, $"{this}: Update failed: {error}");
            } else {
                changed = false;
                WriteLog.To.Query.V(Tag, $"{this}: ...no change");
            }

            if (changed) {
                _changed.Fire(this, new QueryChangedEventArgs(newEnum, error));
            }
        }

        private async void UpdateAfter(TimeSpan updateDelay)
        {
            if (_willUpdate.Set(true)) {
                return;
            }

            if (updateDelay > TimeSpan.Zero) {
                await Task.Delay(updateDelay).ConfigureAwait(false);
            }

            if (_willUpdate) {
                Update();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(false);
        }

        protected abstract void Dispose(bool finalizing);

        #endregion

        #region IQuery

        public abstract unsafe IResultSet Execute();

        public abstract unsafe string Explain();

        public ListenerToken AddChangeListener(TaskScheduler scheduler, [NotNull] EventHandler<QueryChangedEventArgs> handler)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(handler), handler);
            _disposalWatchdog.CheckDisposed();

            var cbHandler = new CouchbaseEventHandler<QueryChangedEventArgs>(handler, scheduler);
            _changed.Add(cbHandler);

            if (Interlocked.Increment(ref _observingCount) == 1) {
                Database?.AddActiveStoppable(this);
                if (Database != null) {
                    _databaseChangedToken = Database.AddChangeListener(OnDatabaseChanged);
                } else {
                    WriteLog.To.Query.W(Tag, @"Attempting to add a change listener onto a query with a null Database.  
                                        Changed events will not continue to fire");
                }

                UpdateAfter(new TimeSpan(0));
            }

            return new ListenerToken(cbHandler, "query");
        }

        public ListenerToken AddChangeListener([NotNull] EventHandler<QueryChangedEventArgs> handler)
        {
            return AddChangeListener(null, handler);
        }

        public void RemoveChangeListener(ListenerToken token)
        {
            _disposalWatchdog.CheckDisposed();
            _changed.Remove(token);
            if (Interlocked.Decrement(ref _observingCount) == 0) {
                Stop();
            }

            _willUpdate.Set(false);
        }

        #endregion
    }
}
