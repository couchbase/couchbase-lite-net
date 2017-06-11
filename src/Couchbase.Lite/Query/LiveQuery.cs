// 
// LiveQuery.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query
{
    internal class LiveQuery : XQuery, ILiveQuery
    {
        #region Constants

        private static readonly TimeSpan DefaultLiveQueryUpdateInterval = TimeSpan.FromMilliseconds(200);
        private const string Tag = nameof(LiveQuery);

        #endregion

        #region Variables

        private readonly ThreadSafety _threadSafety = new ThreadSafety(true);

        public event EventHandler<LiveQueryChangedEventArgs> Changed;

        private QueryEnumerator _enum;
        private bool _forceReload;
        private Exception _lastError;
        private DateTime _lastUpdatedAt;

        private AtomicBool _observing = false;
        private IReadOnlyList<IQueryRow> _rows;
        private TimeSpan _updateInterval;
        private AtomicBool _willUpdate = false;

        #endregion

        #region Properties

        public Exception LastError
        {
            get => _threadSafety.LockedForRead(() => _lastError);
            private set => _threadSafety.LockedForWrite(() =>
            {
                if (_lastError != value) {
                    _lastError = value;
                    Task.Factory.StartNew(() => Changed?.Invoke(this, new LiveQueryChangedEventArgs(null, value)));
                }
            });
        }

        public IReadOnlyList<IQueryRow> Rows
        {
            get {
                Start();
                return _threadSafety.LockedForRead(() => _rows);
            }
            private set {
                _threadSafety.LockedForWrite(() =>_rows = value);
                Task.Factory.StartNew(() => Changed?.Invoke(this, new LiveQueryChangedEventArgs(value)));
            }
        }

        public TimeSpan UpdateInterval
        {
            get => _threadSafety.LockedForRead(() => _updateInterval);
            set => _threadSafety.LockedForWrite(() => _updateInterval = value);
        }

        #endregion

        #region Constructors

        internal LiveQuery(XQuery query)
        {
            UpdateInterval = DefaultLiveQueryUpdateInterval;
            Copy(query);
        }

        #endregion

        #region Private Methods

        private void OnDatabaseChanged(object sender, DatabaseChangedEventArgs e)
        {
            if (_willUpdate) {
                return;
            }

            // External updates should poll less frequently
            var updateInterval = _updateInterval;
            if (e.IsExternal) {
                updateInterval += updateInterval;
            }

            var updateDelay = _lastUpdatedAt + updateInterval - DateTime.Now;
            UpdateAfter(updateDelay);
        }

        private void Update()
        {
            Log.To.Query.I(Tag, $"{this}: Querying...");
            var oldEnum = _enum;
            QueryEnumerator newEnum = null;
            Exception error = null;
            if (oldEnum == null || _forceReload) {
                try {
                    newEnum = (QueryEnumerator) Run();
                } catch (Exception e) {
                    error = e;
                }
            } else {
                newEnum = oldEnum.Refresh();
            }

            _willUpdate.Set(false);
            _forceReload = false;
            _lastUpdatedAt = DateTime.Now;

            if (newEnum != null) {
                if (oldEnum != null) {
                    Log.To.Query.I(Tag, $"{this}: Changed!");
                }

                Misc.SafeSwap(ref _enum, newEnum);
                Rows = newEnum;
            } else if (error == null) {
                Log.To.Query.V(Tag, $"{this}: ...no change");
            } else {
                Log.To.Query.E(Tag, $"{this}: Update failed: {error}");
            }

            if (error != null || _lastError != null) {
                LastError = error;
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

        #region Overrides

        protected override void Dispose(bool finalizing)
        {
            Stop();
            Misc.SafeSwap(ref _enum, null);

            base.Dispose(finalizing);
        }

        #endregion

        #region ILiveQuery

        public void Start()
        {
            if (!_observing.Set(true)) {
                Database.Changed += OnDatabaseChanged;
                Update();
            }
        }

        public void Stop()
        {
            if (_observing.Set(false)) {
                Database.Changed -= OnDatabaseChanged;
            }

            _willUpdate.Set(false);
        }

        #endregion
    }
}
