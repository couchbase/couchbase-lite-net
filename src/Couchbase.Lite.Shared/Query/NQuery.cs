// 
//  NQuery.cs
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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using LiteCore.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class NQuery : QueryBase
    {
        #region Constants
        private const string Tag = nameof(NQuery);
        #endregion

        #region Variables
        private string _n1qlQueryExpression;
        #endregion

        #region Constructors
        internal NQuery(string n1qlQueryExpression, Database database) : base()
        {
            Database = database;
            _n1qlQueryExpression = n1qlQueryExpression;

            // Catch N1QL compile error sooner
            Compile();
        }
        #endregion

        #region Override Methods

        public override unsafe IResultSet Execute()
        {
            if (Database == null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.InvalidQueryDBNull);
            }

            var options = C4QueryOptions.Default;
            var e = (C4QueryEnumerator*)ThreadSafety.DoLockedBridge(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return null;
                }

                var localOpts = options;
                return NativeRaw.c4query_run(_c4Query, &localOpts, FLSlice.Null, err);
            });

            if (e == null) {
                return new NullResultSet();
            }

            var retVal = new QueryResultSet(this, ThreadSafety, e, ColumnNames);
            _history.Add(retVal);
            return retVal;
        }

        public override unsafe string Explain()
        {
            _disposalWatchdog.CheckDisposed();
            return ThreadSafety?.DoLocked(() => Native.c4query_explain(_c4Query)) ?? "(Unable to explain)";
        }

        protected override unsafe void CreateQuery()
        {
            if (_c4Query == null) {
                Debug.Assert(Database != null);
                C4Query* query = (C4Query*)ThreadSafety.DoLockedBridge(err =>
                {
                    return Native.c4query_new2(Database.c4db, C4QueryLanguage.N1QLQuery, _n1qlQueryExpression, null, err);
                });

                _c4Query = query;
            }
        }

        protected override unsafe Dictionary<string, int> CreateColumnNames(C4Query* query)
        {
            var map = new Dictionary<string, int>();

            var columnCnt = Native.c4query_columnCount(query);
            for (int i = 0; i < columnCnt; i++) {
                var titleStr = Native.c4query_columnTitle(query, (uint)i).CreateString();
                if(titleStr == null) {
                    throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Null column title in query!");
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

        #region Private Methods

        private unsafe void Compile()
        {
            if (_c4Query != null)
                return;

            ThreadSafety.DoLockedBridge(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return true;
                }

                CreateQuery();
                if (_c4Query == null) {
                    return false;
                }

                return true;
            });
        }

        #endregion
    }
}
