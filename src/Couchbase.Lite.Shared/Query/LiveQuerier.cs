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
using System.Runtime.InteropServices;
using System.Threading;

using JetBrains.Annotations;
using Couchbase.Lite.Internal.Logging;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed unsafe class LiveQuerier : IDisposable
    {
        #region Constants

        private const string Tag = nameof(LiveQuerier);

        private static readonly C4QueryObserverCallback QueryCallback = QueryObserverCallback;

        #endregion

        #region Variables

        [NotNull] private readonly Event<QueryChangedEventArgs> _changed = new Event<QueryChangedEventArgs>();
        private C4QueryObserver* _queryObserver;
        private bool _disposed;
        private GCHandle _contextHandle;
        private QueryBase _queryBase;
        private int _startedObserving;

        #endregion

        #region Constructor

        internal LiveQuerier(QueryBase queryBase)
        {
            _queryBase = queryBase;
        }

        ~LiveQuerier()
        {
            // Very rare for this to happen since CreateLiveQuerier will create a strong
            // GCHandle that lasts until Dispose is called
            Dispose(true);
        }

        #endregion

        #region Internal Methods

        internal unsafe LiveQuerier CreateLiveQuerier(C4Query* c4Query)
        {
            _queryBase.ThreadSafety.DoLocked(() =>
            {
                if(_disposed) {
                    throw new ObjectDisposedException(nameof(LiveQuerier));
                }

                if(_queryObserver != null) {
                    return;
                }

                _contextHandle = GCHandle.Alloc(this);
                _queryObserver = NativeRaw.c4queryobs_create(c4Query, QueryCallback, GCHandle.ToIntPtr(_contextHandle).ToPointer());
            });

            return this;
        }

        internal Event<QueryChangedEventArgs> StartObserver(CouchbaseEventHandler<QueryChangedEventArgs> cbEventHandler)
        {
            if (Interlocked.Increment(ref _startedObserving) == 1) {
                _changed.Add(cbEventHandler);

                // CBL-5272: This call needs to be guarded with the db exclusive lock to avoid bad
                // interaction with multiple concurrent LiveQueries being created with different objects
                DatabaseThreadSafety().DoLocked(() => NativeRaw.c4queryobs_setEnabled(_queryObserver, true));
            }

            return _changed;
        }

        internal void StopObserver(ListenerToken listenerToken)
        {
            if (Interlocked.Decrement(ref _startedObserving) == 0) {
                _changed.Remove(listenerToken);
                NativeRaw.c4queryobs_setEnabled(_queryObserver, false);
            }
        }

        internal void Dispose(bool finalizing)
        {
            if (!finalizing) {
                _queryBase.DispatchQueue.DispatchSync(() =>
                {
                    if (_disposed) {
                        return;
                    }

                    /* Stops an observer and frees the resources it's using. It is safe to pass NULL to this call. */
                    Native.c4queryobs_free(_queryObserver);
                    _queryObserver = null;
                    _disposed = true;
                    if (_contextHandle.IsAllocated) {
                        _contextHandle.Free();
                    }
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
            var obj = GCHandle.FromIntPtr((IntPtr)context).Target as LiveQuerier;
            if (obj == null) {
                WriteLog.To.Query.E(Tag, "Failed to retrieve LiveQuerier from GCHandle");
                return;
            }

            obj.QueryObserverCalled(obs, query);
        }

        private unsafe void QueryObserverCalled(C4QueryObserver* obs, C4Query* query)
        {
            _queryBase.DispatchQueue.DispatchSync(() =>
            {
                if(_disposed) {
                    return;
                }

                Exception exp = null;
                var newEnum = GetResults();

                if (newEnum != null) {
                    _changed.Fire(this, new QueryChangedEventArgs(newEnum, exp));
                }
            });
        }

        private QueryResultSet GetResults()
        {
            var newEnum = (C4QueryEnumerator*)_queryBase.ThreadSafety.DoLockedBridge(err =>
            {
                return NativeRaw.c4queryobs_getEnumerator(_queryObserver, true, err);
            });

            if (newEnum != null) {
                return new QueryResultSet(_queryBase, _queryBase.ThreadSafety, newEnum, _queryBase.ColumnNames);
            }

            return null;
        }

        private ThreadSafety DatabaseThreadSafety() => _queryBase?.Database?.ThreadSafety ??
            throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Database ThreadSafety was lost");

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
