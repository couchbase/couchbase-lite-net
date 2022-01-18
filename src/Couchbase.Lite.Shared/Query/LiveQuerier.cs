// 
//  LiveQuerier.cs
// 
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using LiteCore.Interop;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed unsafe class LiveQuerier : IDisposable
    {
        #region Constants

        private const string Tag = nameof(LiveQuerier);

        private static readonly C4QueryObserverCallback QueryCallback = QueryObserverCallback;

        #endregion

        #region Variables

        [NotNull] internal readonly Event<QueryChangedEventArgs> _changed = new Event<QueryChangedEventArgs>();
        private C4QueryObserver* _queryObserver; //uniqe long value
        private bool _disposed;
        private QueryBase _queryBase;
        private long _rowCount;
        private C4Query* _c4Query;

        #endregion

        #region Properties

        internal SerialQueue DispatchQueue { get; } = new SerialQueue();
        internal C4QueryObserver* QueryObserver => _queryObserver;
        internal long RowCount => _rowCount;

        #endregion

        #region Constructor

        internal LiveQuerier(QueryBase queryBase)
        {
            _queryBase = queryBase;
        }

        ~LiveQuerier()
        {
            Dispose(true);
        }

        #endregion

        #region Internal Methods

        internal unsafe LiveQuerier CreateLiveQuerier(C4Query* c4Query)
        {
            _c4Query = c4Query;
            _queryObserver = Native.c4queryobs_create(_c4Query, QueryCallback, GCHandle.ToIntPtr(_queryBase.Handle).ToPointer());
            return this;
        }

        internal void StartObserver(CouchbaseEventHandler<QueryChangedEventArgs> handler)
        {
            _changed.Add(handler);
            Native.c4queryobs_setEnabled(_queryObserver, true);
        }

        internal void StopObserver()
        {
            Native.c4queryobs_setEnabled(_queryObserver, false);
        }

        internal QueryResultSet GetResults()
        {
            C4Error error;
            var newEnum = (C4QueryEnumerator*)_queryBase.ThreadSafety.DoLockedBridge(err =>
            {
                return Native.c4queryobs_getEnumerator(_queryObserver, true, err);
            });

            if (newEnum != null) {
                _rowCount = Native.c4queryenum_getRowCount(newEnum, &error);
                return new QueryResultSet(_queryBase, _queryBase.ThreadSafety, newEnum, _queryBase.CreateColumnNames(_c4Query));
            }

            return null;
        }

            internal void Dispose(bool finalizing)
        {
            if (!finalizing) {
                DispatchQueue.DispatchSync(() =>
                {
                    if (_disposed) {
                        return;
                    }

                    StopObserver();
                    /* Stops an observer and frees the resources it's using. It is safe to pass NULL to this call. */
                    Native.c4queryobs_free(_queryObserver);
                    _queryObserver = null;
                    _disposed = true;
                });
            } else {
                Native.c4queryobs_free(_queryObserver);
                _queryObserver = null;
            }
        }

        #endregion

        #region Private Methods

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4QueryObserverCallback))]
        #endif
        private static void QueryObserverCallback(C4QueryObserver* obs, C4Query* query, void* context)
        {
            var obj = GCHandle.FromIntPtr((IntPtr)context).Target as QueryBase;
            obj.QueryObserverCalled(obs, query);
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
