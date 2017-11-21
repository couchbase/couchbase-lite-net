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
using System.Threading.Tasks;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class LiveQuery : ILiveQuery
    {
        #region Constants

        private static readonly TimeSpan DefaultLiveQueryUpdateInterval = TimeSpan.FromMilliseconds(200);
        private const string Tag = nameof(LiveQuery);

        #endregion

        #region Variables

        private readonly XQuery _query;
        private readonly TimeSpan _updateInterval;

        public event EventHandler<LiveQueryChangedEventArgs> Changed;

        private QueryResultSet _enum;
        private DateTime _lastUpdatedAt;
        private AtomicBool _observing = false;
        private AtomicBool _willUpdate = false;

        #endregion

        #region Properties

        public IParameters Parameters => _query.Parameters;

        #endregion

        #region Constructors

        internal LiveQuery(XQuery query)
        {
            _updateInterval = DefaultLiveQueryUpdateInterval;
            _query = query;
            _query?.Database?.ActiveLiveQueries.Add(this);
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
            QueryResultSet newEnum = null;
            Exception error = null;
            if (oldEnum == null) {
                try {
                    newEnum = (QueryResultSet) _query.Execute();
                } catch (Exception e) {
                    error = e;
                }
            } else {
                newEnum = oldEnum.Refresh();
            }

            _willUpdate.Set(false);
            _lastUpdatedAt = DateTime.Now;

            var changed = true;
            if (newEnum != null) {
                if (oldEnum != null) {
                    Log.To.Query.I(Tag, $"{this}: Changed!");
                }

                Misc.SafeSwap(ref _enum, newEnum);
            } else if (error != null) {
                Log.To.Query.E(Tag, $"{this}: Update failed: {error}");
            } else {
                changed = false;
                Log.To.Query.V(Tag, $"{this}: ...no change");
            }

            if (changed) {
                Changed?.Invoke(this, new LiveQueryChangedEventArgs(newEnum, error));
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
            Stop();
            Misc.SafeSwap(ref _enum, null);
            _query?.Database?.ActiveLiveQueries.Remove(this);
            _query?.Dispose();
        }

        #endregion

        #region ILiveQuery

        public void Run()
        {
            if (!_observing.Set(true)) {
                _query.Database.Changed += OnDatabaseChanged;
                Update();
            }
        }

        public void Stop()
        {
            if (_observing.Set(false)) {
                _query.Database.Changed -= OnDatabaseChanged;
            }

            _willUpdate.Set(false);
        }

        #endregion
    }
}
