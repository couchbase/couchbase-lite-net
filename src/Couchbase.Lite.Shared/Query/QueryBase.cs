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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;
using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryBase : IQuery, IStoppable
    {
        #region Constants

        private const string Tag = nameof(QueryBase);

        #endregion

        #region Variables

        [NotNull] protected readonly DisposalWatchdog _disposalWatchdog = new DisposalWatchdog(nameof(IQuery));
        protected unsafe C4Query* _c4Query;
        [NotNull] protected List<QueryResultSet> _history = new List<QueryResultSet>();
        [NotNull] protected Parameters _queryParameters;
        protected List<LiveQuerier> _liveQueriers = new List<LiveQuerier>();
        protected Dictionary<ListenerToken, LiveQuerier> _listenerTokens = new Dictionary<ListenerToken, LiveQuerier>();
        protected int _observingCount;
        internal Dictionary<string, int> ColumnNames;

        #endregion

        #region Properties

        public Database Database { get; set; }

        public Parameters Parameters
        {
            get => _queryParameters;
            set
            {
                _queryParameters = value.Freeze();
                SetParameters(_queryParameters.ToString());
            }
        }

        internal string QueryExpression { get; set; }

        [NotNull]
        internal ThreadSafety ThreadSafety { get; set; } = new ThreadSafety();

        internal SerialQueue DispatchQueue { get; } = new SerialQueue();

        #endregion

        #region Properties - Json Query Expression

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

        #endregion

        #region Constructors

        public QueryBase()
        {
            _queryParameters = new Parameters(this);
        }

        ~QueryBase()
        {
            Dispose(true);
        }

        #endregion

        #region Public Methods

        public void Stop()
        {
            Database?.RemoveActiveStoppable(this);
            foreach (var querier in _liveQueriers) {
                if (querier != null)
                    querier.StopObserver();
            }

            _liveQueriers.Clear();
        }

        #endregion

        #region Internal Methods

        internal unsafe void CreateQuery()
        {
            if (_c4Query == null) {
                C4Query* query = (C4Query*)ThreadSafety.DoLockedBridge(err =>
                {
                    if (this is XQuery) {
                        QueryExpression = EncodeAsJSON();
                        WriteLog.To.Query.I(Tag, $"Query encoded as {QueryExpression}");
                        return Native.c4query_new2(Database.c4db, C4QueryLanguage.JSONQuery, QueryExpression, null, err);
                    } else { //NQuery - N1QL
                        return Native.c4query_new2(Database.c4db, C4QueryLanguage.N1QLQuery, QueryExpression, null, err);
                    }
                });

                _c4Query = query;
            }
        }

        internal unsafe void SetParameters(string parameters)
        {
            CreateQuery();
            if (_c4Query != null && !String.IsNullOrEmpty(parameters)) {
                Native.c4query_setParameters(_c4Query, parameters);
            }
        }

        internal unsafe Dictionary<string, int> CreateColumnNames(C4Query* query)
        {
            QueryDataSource fromImpl = null;
            if (this is XQuery) {
                fromImpl = FromImpl;
                Debug.Assert(fromImpl != null, "CreateColumnNames reached without a FROM clause received");
                ThreadSafety = fromImpl.ThreadSafety;
            }

            var map = new Dictionary<string, int>();

            var columnCnt = Native.c4query_columnCount(query);
            for (int i = 0; i < columnCnt; i++) {
                var titleStr = Native.c4query_columnTitle(query, (uint)i).CreateString();

                if (this is XQuery && titleStr.StartsWith("*")) {
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

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(false);
        }

        private unsafe void Dispose(bool finalizing)
        {
            if (!finalizing) {
                Stop();
                ThreadSafety.DoLocked(() =>
                {
                    foreach (var e in _history) {
                        e.Release();
                    }

                    _history.Clear();
                    foreach (var querier in _liveQueriers) {
                        if (querier != null)
                            querier.Dispose(finalizing);
                    }

                    _liveQueriers.Clear();
                    Native.c4query_release(_c4Query);
                    _c4Query = null;
                    _disposalWatchdog.Dispose();
                });
            } else {
                // Database is not valid inside finalizer, but thread safety
                // is guaranteed
                Native.c4query_release(_c4Query);
                _c4Query = null;
            }
        }

        #endregion

        #region IQuery

        public abstract unsafe IResultSet Execute();

        public abstract unsafe string Explain();

        public ListenerToken AddChangeListener(TaskScheduler scheduler, [NotNull] EventHandler<QueryChangedEventArgs> handler)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(handler), handler);
            _disposalWatchdog.CheckDisposed();

            if (Interlocked.Increment(ref _observingCount) == 1) {
                Database?.AddActiveStoppable(this);
            }

            var cbHandler = new CouchbaseEventHandler<QueryChangedEventArgs>(handler, scheduler);
            var listenerToken = new ListenerToken(cbHandler, "query");
            CreateLiveQuerier(listenerToken);

            return listenerToken;
        }

        public ListenerToken AddChangeListener([NotNull] EventHandler<QueryChangedEventArgs> handler)
        {
            return AddChangeListener(null, handler);
        }

        public unsafe void RemoveChangeListener(ListenerToken token)
        {
            _disposalWatchdog.CheckDisposed();
            _listenerTokens[token].StopObserver();
            _listenerTokens.Remove(token);
            if (Interlocked.Decrement(ref _observingCount) == 0) {
                Stop();
            }
        }

        #endregion

        #region Protected Methods

        protected string EncodeAsJSON()
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
                if (fromJson != null) {
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

        protected unsafe void CreateLiveQuerier(ListenerToken listenerToken)
        {
            CreateQuery();
            if (_c4Query != null) {
                var liveQuerier = new LiveQuerier(this);
                liveQuerier.CreateLiveQuerier(_c4Query, listenerToken);
                _liveQueriers.Add(liveQuerier);
                _listenerTokens.Add(listenerToken, liveQuerier);
                liveQuerier.StartObserver();
            }
        }

        #endregion
    }
}
