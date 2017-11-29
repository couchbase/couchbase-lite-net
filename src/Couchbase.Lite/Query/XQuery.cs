// 
// XQuery.cs
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal class XQuery : IQuery
    {
        #region Constants

        private const string Tag = nameof(XQuery);
        private static readonly TimeSpan DefaultLiveQueryUpdateInterval = TimeSpan.FromMilliseconds(200);

        #endregion

        #region Variables

        private unsafe C4Query* _c4Query;
        private Dictionary<string, int> _columnNames;
        private QueryParameters _queryParameters = new QueryParameters();
        private Event<QueryChangedEventArgs> _changed = new Event<QueryChangedEventArgs>();
        private readonly TimeSpan _updateInterval;
        private QueryResultSet _enum;
        private DateTime _lastUpdatedAt;
        private int _observingCount = 0;
        private AtomicBool _willUpdate = false;
        private ListenerToken _databaseChangedToken;

        #endregion

        #region Properties

        public Database Database { get; set; }

        public QueryParameters Parameters
        {
            get => _queryParameters;
            set {
                _queryParameters = value;
                Update();
            }
        }

        protected bool Distinct { get; set; }

        protected QueryDataSource FromImpl { get; set; }

        protected QueryGroupBy GroupByImpl { get; set; }

        protected Having HavingImpl { get; set; }

        protected QueryJoin JoinImpl { get; set; }

        protected object LimitValue { get; set; }

        protected QueryOrdering OrderByImpl { get; set; }

        protected Select SelectImpl { get; set; }

        protected object SkipValue { get; set; }

        protected QueryExpression WhereImpl { get; set; }

        #endregion

        #region Constructors

        public XQuery()
        {
            _updateInterval = DefaultLiveQueryUpdateInterval;
        }

        ~XQuery()
        {
            Dispose(true);
        }

        #endregion

        #region Protected Methods

        protected void Copy(XQuery source)
        {
            Database = source.Database;
            SelectImpl = source.SelectImpl;
            Distinct = source.Distinct;
            FromImpl = source.FromImpl;
            WhereImpl = source.WhereImpl;
            OrderByImpl = source.OrderByImpl;
            JoinImpl = source.JoinImpl;
            GroupByImpl = source.GroupByImpl;
            HavingImpl = source.HavingImpl;
            LimitValue = source.LimitValue;
            SkipValue = source.SkipValue;
        }

        protected virtual unsafe void Dispose(bool finalizing)
        {
            if (!finalizing) {
                Stop();
                Misc.SafeSwap(ref _enum, null);
                FromImpl.ThreadSafety.DoLocked(() => Native.c4query_free(_c4Query));
            }
            else {
                // Database is not valid inside finalizer, but thread safety
                // is guaranteed
                Native.c4query_free(_c4Query);
            }

            _c4Query = null;
        }

        #endregion

        #region Internal Methods

        internal unsafe string Explain()
        {
            // Used for debugging
            if (_c4Query == null) {
                Check();
            }

            return FromImpl.ThreadSafety.DoLocked(() => Native.c4query_explain(_c4Query));
        }

        #endregion

        #region Private Methods

        private unsafe void Check()
        {
            var jsonData = EncodeAsJSON();
            if (_columnNames == null) {
                _columnNames = CreateColumnNames();
            }

            Log.To.Query.I(Tag, $"Query encoded as {jsonData}");

            FromImpl.ThreadSafety.DoLockedBridge(err =>
            {
                var query = Native.c4query_new(Database.c4db, jsonData, err);
                if(query == null) {
                    return false;
                }

                Native.c4query_free(_c4Query);
                _c4Query = query;
                return true;
            });
        }

        private Dictionary<string, int> CreateColumnNames()
        {
            var map = new Dictionary<string, int>();
            var index = 0;
            var provisionKeyIndex = 0;
            foreach (var select in SelectImpl.SelectResults) {
                var name = select.ColumnName ?? $"${++provisionKeyIndex}";
                if (map.ContainsKey(name)) {
                    throw new CouchbaseLiteException(StatusCode.InvalidQuery, $"Duplicate select result named {name}");
                }

                map[name] = index;
                index++;
            }

            return map;
        }

        private string EncodeAsJSON()
        {
            var parameters = new Dictionary<string, object>();
            if (WhereImpl != null) {
                parameters["WHERE"] = WhereImpl.ConvertToJSON();
            }

            if (Distinct) {
                parameters["DISTINCT"] = true;
            }

            if (LimitValue != null) {
                if (LimitValue is QueryExpression e) {
                    parameters["LIMIT"] = e.ConvertToJSON();
                } else {
                    parameters["LIMIT"] = LimitValue;
                }
            }

            if (SkipValue != null) {
                if (SkipValue is QueryExpression e) {
                    parameters["OFFSET"] = e.ConvertToJSON();
                }
                else {
                    parameters["OFFSET"] = SkipValue;
                }
            }

            if (OrderByImpl != null) {
                parameters["ORDER_BY"] = OrderByImpl.ToJSON();
            }

            var selectParam = SelectImpl?.ToJSON();
            if (selectParam != null) {
                parameters["WHAT"] = selectParam;
            }

            if (JoinImpl != null) {
                var fromJson = FromImpl?.ToJSON();
                if (fromJson == null) {
                    throw new InvalidOperationException(
                        "The default database must have an alias in order to use a JOIN statement" +
                        " (Make sure your data source uses the As() function)");
                }

                var joinJson = JoinImpl.ToJSON() as IList<object>;
                Debug.Assert(joinJson != null);
                joinJson.Insert(0, fromJson);
                parameters["FROM"] = joinJson;
            } else {
                var fromJson = FromImpl?.ToJSON();
                if(fromJson != null) {
                    parameters["FROM"] = new[] { fromJson };
                }
            }
            
            if (GroupByImpl != null) {
                parameters["GROUP_BY"] = GroupByImpl.ToJSON();
            }
            
            if (HavingImpl != null) {
                parameters["HAVING"] = HavingImpl.ToJSON();
            }

            return JsonConvert.SerializeObject(parameters);
        }

        private void OnDatabaseChanged(object sender, DatabaseChangedEventArgs e)
        {
            if (_willUpdate) {
                return;
            }

            // External updates should poll less frequently
            var updateInterval = _updateInterval;

            var updateDelay = _lastUpdatedAt + updateInterval - DateTime.Now;
            UpdateAfter(updateDelay);
        }

        private void Stop()
        {
            Database?.ActiveLiveQueries?.Remove(this);
            if (_databaseChangedToken != null) {
                Database?.RemoveChangeListener(_databaseChangedToken);
            }
        }

        private void Update()
        {
            Log.To.Query.I(Tag, $"{this}: Querying...");
            var oldEnum = _enum;
            QueryResultSet newEnum = null;
            Exception error = null;
            if (oldEnum == null) {
                try {
                    newEnum = (QueryResultSet) Execute();
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

        #endregion

        #region IQuery

        public ListenerToken AddChangeListener(TaskScheduler scheduler, EventHandler<QueryChangedEventArgs> handler)
        {
            CBDebug.MustNotBeNull(Log.To.Query, Tag, nameof(handler), handler);

            var cbHandler = new CouchbaseEventHandler<QueryChangedEventArgs>(handler, scheduler);
            _changed.Add(cbHandler);

            if (Interlocked.Increment(ref _observingCount) == 1) {
                Database?.ActiveLiveQueries?.Add(this);
                _databaseChangedToken = Database?.AddChangeListener(null, OnDatabaseChanged);
                Update();
            }

            return new ListenerToken(cbHandler);
        }

        public void RemoveChangeListener(ListenerToken token)
        {
            CBDebug.MustNotBeNull(Log.To.Query, Tag, nameof(token), token);

            _changed.Remove(token);
            if (Interlocked.Decrement(ref _observingCount) == 0) {
                Stop();
            }

            _willUpdate.Set(false);
        }

        public unsafe IResultSet Execute()
        {
            if (Database == null) {
                throw new InvalidOperationException("Invalid query, Database == null");
            }

            if (SelectImpl == null || FromImpl == null) {
                throw new InvalidOperationException("Invalid query, missing Select or From");
            }


            if (_c4Query == null) {
                Check();
            }

            var options = C4QueryOptions.Default;
            var paramJson = ((QueryParameters) Parameters).ToString();

            var e = (C4QueryEnumerator*) FromImpl.ThreadSafety.DoLockedBridge(err =>
            {
                var localOpts = options;
                return Native.c4query_run(_c4Query, &localOpts, paramJson, err);
            });

            return new QueryResultSet(this, FromImpl.ThreadSafety, e, _columnNames);
        }

        #endregion
    }
}
