using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using JetBrains.Annotations;
using LiteCore.Interop;
using System;
using System.Collections.Generic;

namespace Couchbase.Lite.Internal.Query
{
    internal class N1QLQuery : XQuery
    {
        private const string Tag = nameof(N1QLQuery);
        private string _n1qlQueryExpression = "";

        [NotNull]
        internal ThreadSafety ThreadSafety { get; } = new ThreadSafety();

        public N1QLQuery(string n1qlQueryExpression, Database database) : base()
        {
            Database = database;
            _n1qlQueryExpression = n1qlQueryExpression;
        }

        public override unsafe IResultSet Execute()
        {
            if (Database == null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.InvalidQueryDBNull);
            }

            var options = C4QueryOptions.Default;
            var paramN1ql = _n1qlQueryExpression.FLEncode();

            var e = (C4QueryEnumerator*)ThreadSafety.DoLockedBridge(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return null;
                }

                if (_c4Query == null) {
                    Check();
                }

                var localOpts = options;
                return NativeRaw.c4query_run(_c4Query, &localOpts, FLSlice.Null, err);
            });

            paramN1ql.Dispose();

            if (e == null) {
                return new NullResultSet();
            }

            var retVal = new QueryResultSet(this, ThreadSafety, e, _columnNames);
            _history.Add(retVal);
            return retVal;
        }

        protected override unsafe void Dispose(bool finalizing)
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

        private unsafe void Check()
        {
            ThreadSafety.DoLockedBridge(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return true;
                }

                var query = Native.c4query_new2(Database.c4db, C4QueryLanguage.N1QLQuery, _n1qlQueryExpression, null, err);
                if (query == null) {
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
            var map = new Dictionary<string, int>();
            var columnCnt = Native.c4query_columnCount(query);
            for (int i = 0; i < columnCnt; i++) {
                var titleStr = Native.c4query_columnTitle(query, (uint)i).CreateString();
                if (map.ContainsKey(titleStr)) {
                    throw new CouchbaseLiteException(C4ErrorCode.InvalidQuery,
                        String.Format(CouchbaseLiteErrorMessage.DuplicateSelectResultName, titleStr));
                }

                map.Add(titleStr, i);
            }

            return map;
        }
    }
}
