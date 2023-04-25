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
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryBase : IQuery, IStoppable
    {
        #region Constants

        private const string Tag = nameof(QueryBase);

        #endregion

        #region Variables

        protected readonly DisposalWatchdog _disposalWatchdog = new DisposalWatchdog(nameof(IQuery));
        protected unsafe C4Query* _c4Query;
        protected List<QueryResultSet> _history = new List<QueryResultSet>();
        protected Parameters _queryParameters;
        protected Dictionary<ListenerToken, LiveQuerier?> _listenerTokens = new Dictionary<ListenerToken, LiveQuerier?>();
        protected int _observingCount;

        #endregion

        #region Properties

        public Database? Database { get; set; }

        public Collection? Collection { get; set; }

        public Parameters Parameters
        {
            get => _queryParameters;
            set
            {
                _queryParameters = value.Freeze();
                SetParameters(_queryParameters.ToString());
            }
        }

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
            Collection?.Database?.RemoveActiveStoppable(this);
            foreach (var t in _listenerTokens) {
                var token = t.Key;
                var querier = t.Value;
                querier?.StopObserver(token);
                querier?.Dispose();
            }

            _listenerTokens.Clear();
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

        #endregion

        #region IChangeObservable

        public ListenerToken AddChangeListener(TaskScheduler? scheduler, EventHandler<QueryChangedEventArgs> handler)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(handler), handler);
            _disposalWatchdog.CheckDisposed();

            if (Interlocked.Increment(ref _observingCount) == 1) {
                Collection?.Database?.AddActiveStoppable(this);
            }

            var cbHandler = new CouchbaseEventHandler<QueryChangedEventArgs>(handler, scheduler);
            var listenerToken = CreateLiveQuerier(cbHandler);

            return listenerToken;
        }

        public ListenerToken AddChangeListener(EventHandler<QueryChangedEventArgs> handler) => AddChangeListener(null, handler);

        #endregion

        #region IChangeObservableRemovable

        public void RemoveChangeListener(ListenerToken token)
        {
            _disposalWatchdog.CheckDisposed();
            _listenerTokens[token]?.StopObserver(token);
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

        protected unsafe ListenerToken CreateLiveQuerier(CouchbaseEventHandler<QueryChangedEventArgs> cbEventHandler)
        {
            CreateQuery();
            LiveQuerier? liveQuerier = null;
            if (_c4Query != null) {
                liveQuerier = new LiveQuerier(this);
                liveQuerier.CreateLiveQuerier(_c4Query);
            }
            
            liveQuerier?.StartObserver(cbEventHandler);
            var token = new ListenerToken(cbEventHandler, ListenerTokenType.Query, this);
            _listenerTokens.Add(token, liveQuerier);
            return token;
        }

        #endregion
    }
}
