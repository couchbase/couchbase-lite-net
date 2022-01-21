﻿// 
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
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;
using LiteCore.Interop;

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

        [NotNull]
        internal ThreadSafety ThreadSafety { get; set; } = new ThreadSafety();

        internal SerialQueue DispatchQueue { get; } = new SerialQueue();

        internal unsafe Dictionary<string, int> ColumnNames => CreateColumnNames(_c4Query);

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

        internal unsafe void SetParameters(string parameters)
        {
            CreateQuery();
            if (_c4Query != null && !String.IsNullOrEmpty(parameters)) {
                Native.c4query_setParameters(_c4Query, parameters);
            }
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

        public void RemoveChangeListener(ListenerToken token)
        {
            _disposalWatchdog.CheckDisposed();
            _listenerTokens[token].StopObserver();
            _listenerTokens.Remove(token);
            if (Interlocked.Decrement(ref _observingCount) == 0) {
                Stop();
            }
        }

        #endregion

        #region QueryBase

        protected abstract void CreateQuery();

        protected abstract unsafe Dictionary<string, int> CreateColumnNames(C4Query* query);

        #endregion

        #region Protected Methods

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
