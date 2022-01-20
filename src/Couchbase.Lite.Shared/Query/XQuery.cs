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
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal class XQuery : QueryBase
    {
        #region Variables
        private const string Tag = nameof(XQuery);
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

        protected void ValidateParams<T>(T[] param, [CallerMemberName] string tag = null)
        {
            if (param.Length == 0) {
                var message = String.Format(CouchbaseLiteErrorMessage.ExpressionsMustContainOnePlusElement, tag);
                CBDebug.LogAndThrow(WriteLog.To.Query, new InvalidOperationException(message), Tag, message, true);
            }
        }

        #endregion

        #region Override Methods

        public override unsafe IResultSet Execute()
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

            var e = (C4QueryEnumerator*)fromImpl.ThreadSafety.DoLockedBridge(err =>
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

            if (ColumnNames == null)
                ColumnNames = CreateColumnNames(_c4Query);

            var retVal = new QueryResultSet(this, fromImpl.ThreadSafety, e, ColumnNames);
            _history.Add(retVal);
            return retVal;
        }

        public override unsafe string Explain()
        {
            _disposalWatchdog.CheckDisposed();

            // Used for debugging
            if (_c4Query == null) {
                Check();
            }

            return FromImpl?.ThreadSafety?.DoLocked(() => Native.c4query_explain(_c4Query)) ?? "(Unable to explain)";
        }

        protected override unsafe void Dispose(bool finalizing)
        {
            if (!finalizing) {
                Stop();
                FromImpl.ThreadSafety.DoLocked(() =>
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

        #region Private Methods

        private unsafe void Check()
        {
            var from = FromImpl;
            Debug.Assert(from != null, "Reached Check() without receiving a FROM clause!");

            from.ThreadSafety.DoLockedBridge(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return true;
                }

                CreateQuery();
                if (_c4Query == null) { 
                    return false;
                }

                if (ColumnNames == null) {
                    ColumnNames = CreateColumnNames(_c4Query);
                }

                return true;
            });
        }

        #endregion
    }
}
