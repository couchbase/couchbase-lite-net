// 
//  XQuery.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

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

        [NotNull]private readonly Event<QueryChangedEventArgs> _changed = new Event<QueryChangedEventArgs>();
        [NotNull] private readonly DisposalWatchdog _disposalWatchdog = new DisposalWatchdog(nameof(IQuery));
        private readonly TimeSpan _updateInterval;

        private unsafe C4Query* _c4Query;
        private Dictionary<string, int> _columnNames;
        private ListenerToken _databaseChangedToken;
        [NotNull]private List<QueryResultSet> _history = new List<QueryResultSet>();
        private DateTime _lastUpdatedAt;
        private int _observingCount = 0;
        [NotNull]private Parameters _queryParameters = new Parameters();
        private AtomicBool _willUpdate = false;
        private bool _updating = false, _stopping = false;

        #endregion

        #region Properties

        public Database Database { get; set; }

        protected bool Distinct { get; set; }

        protected QueryDataSource FromImpl { get; set; }

        protected QueryGroupBy GroupByImpl { get; set; }

        protected Having HavingImpl { get; set; }

        protected QueryJoin JoinImpl { get; set; }

        protected IExpression LimitValue { get; set; }

        protected QueryOrderBy OrderByImpl { get; set; }

        protected Select SelectImpl { get; set; }

        protected IExpression SkipValue { get; set; }

        protected QueryExpression WhereImpl { get; set; }

        public Parameters Parameters
        {
            get => _queryParameters;
            set {
                _queryParameters = value.Freeze();
                Update();
            }
        }

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
                FromImpl.ThreadSafety.DoLocked(() =>
                {
                    foreach (var e in _history) {
                        e.Release();
                    }

                    _history.Clear();
                    Native.c4query_release(_c4Query);
                    _c4Query = null;
                    _disposalWatchdog.Dispose();
                });
            }
            else {
                // Database is not valid inside finalizer, but thread safety
                // is guaranteed
                Native.c4query_release(_c4Query);
                _c4Query = null;
            }
        }

        protected void ValidateParams<T>(T[] param, [CallerMemberName]string tag = null)
        {
            if (param.Length == 0) {
                var message = String.Format(CouchbaseLiteErrorMessage.ExpressionsMustContainOnePlusElement, tag);
                CBDebug.LogAndThrow(WriteLog.To.Query, new InvalidOperationException(message), Tag, message, true);
            }
        }

        #endregion

        internal void Stop()
        {
            if (_updating) {
                _stopping = true;
            } else {
                Stopped();
            }
        }

        #region Private Methods

        private unsafe void Check()
        {
            var from = FromImpl;
            Debug.Assert(from != null, "Reached Check() without receiving a FROM clause!");

            var jsonData = EncodeAsJSON();

            WriteLog.To.Query.I(Tag, $"Query encoded as {jsonData}");

            from.ThreadSafety.DoLockedBridge(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return true;
                }

                var query = Native.c4query_new(Database.c4db, jsonData, err);
                if(query == null) {
                    return false;
                }

                if (_columnNames == null) {
                    _columnNames = CreateColumnNames(query);
                }

                Native.c4query_release(_c4Query);
                _c4Query = query;
                return true;
            });
        }

        private unsafe Dictionary<string, int> CreateColumnNames(C4Query* query)
        {
            var fromImpl = FromImpl;
            Debug.Assert(fromImpl != null, "CreateColumnNames reached without a FROM clause received");

            var map = new Dictionary<string, int>();

            var columnCnt = Native.c4query_columnCount(query);
            for (int i = 0; i < columnCnt; i++) {
                var titleStr = Native.c4query_columnTitle(query, (uint)i).CreateString();

                if (titleStr.StartsWith("*")) {
                    titleStr = fromImpl.ColumnName;
                }

                if (map.ContainsKey(titleStr)) {
                    throw new CouchbaseLiteException(C4ErrorCode.InvalidQuery, 
                        String.Format(CouchbaseLiteErrorMessage.DuplicateSelectResultName, titleStr));
                }

                map.Add(titleStr, i);
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
                var e = Misc.TryCast<IExpression, QueryExpression>(LimitValue);
                parameters["LIMIT"] = e.ConvertToJSON();
            }

            if (SkipValue != null) {
                var e = Misc.TryCast<IExpression, QueryExpression>(SkipValue);
                parameters["OFFSET"] = e.ConvertToJSON();
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
                    throw new InvalidOperationException(CouchbaseLiteErrorMessage.NoAliasInJoin);
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

            _updating.Set(true);

            // External updates should poll less frequently
            var updateInterval = _updateInterval;

            var updateDelay = _lastUpdatedAt + updateInterval - DateTime.Now;
            UpdateAfter(updateDelay);
        }

        private void Stopped()
        {
            Database?.RemoveActiveLiveQuery(this);
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

                    newEnum = (QueryResultSet) result;
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

        #endregion

        #region IQuery

        public ListenerToken AddChangeListener(TaskScheduler scheduler, [NotNull]EventHandler<QueryChangedEventArgs> handler)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(handler), handler);
            _disposalWatchdog.CheckDisposed();

            var cbHandler = new CouchbaseEventHandler<QueryChangedEventArgs>(handler, scheduler);
            _changed.Add(cbHandler);

            if (Interlocked.Increment(ref _observingCount) == 1) {
                Database?.AddActiveLiveQuery(this);
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

        public ListenerToken AddChangeListener([NotNull]EventHandler<QueryChangedEventArgs> handler)
        {
            return AddChangeListener(null, handler);
        }

        public unsafe IResultSet Execute()
        {
            if (Database == null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.InvalidQueryDBNull);
            }

            var fromImpl = FromImpl;
            if (SelectImpl == null || fromImpl == null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.InvalidQueryMissingSelectOrFrom);
            }
            
            var options = C4QueryOptions.Default;
            var paramJson = Parameters.FLEncode();

            var e = (C4QueryEnumerator*) fromImpl.ThreadSafety.DoLockedBridge(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return null;
                }

                if (_c4Query == null) {
                    Check();
                }

                var localOpts = options;
                return NativeRaw.c4query_run(_c4Query, &localOpts, (FLSlice)paramJson, err);
            });

            paramJson.Dispose();

            if (e == null) {
                return new NullResultSet();
            }

            var retVal = new QueryResultSet(this, fromImpl.ThreadSafety, e, _columnNames);
            _history.Add(retVal);
            return retVal;
        }
        public unsafe string Explain()
        {
            _disposalWatchdog.CheckDisposed();

            // Used for debugging
            if (_c4Query == null) {
                Check();
            }

            return FromImpl?.ThreadSafety?.DoLocked(() => Native.c4query_explain(_c4Query)) ?? "(Unable to explain)";
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
