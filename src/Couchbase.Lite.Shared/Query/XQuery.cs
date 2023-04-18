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
using System.Runtime.CompilerServices;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal class XQuery : QueryBase
    {
        #region Constants
        private const string Tag = nameof(XQuery);
        #endregion

        #region Variables
        private string _queryExpression;
        #endregion

        #region Properties

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

        #region Protected Methods

        protected void Copy(XQuery source)
        {
            Database = source.Database;
            Collection = source.Collection;
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
            if (Collection == null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.InvalidQueryDBNull);
            }

            Database = Collection.Database;

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

        protected override unsafe void CreateQuery()
        {
            if (_c4Query == null)
            {
                if (Database == null)
                    Database = Collection.Database;

                C4Query* query = (C4Query*)ThreadSafety.DoLockedBridge(err =>
                {
                    _queryExpression = EncodeAsJSON();
                    WriteLog.To.Query.I(Tag, $"Query encoded as {_queryExpression}");
                    return Native.c4query_new2(Database.c4db, C4QueryLanguage.JSONQuery, _queryExpression, null, err);
                });

                _c4Query = query;
            }
        }

        protected override unsafe Dictionary<string, int> CreateColumnNames(C4Query* query)
        {
            var fromImpl = FromImpl;
            Debug.Assert(fromImpl != null, "CreateColumnNames reached without a FROM clause received");
            ThreadSafety = fromImpl.ThreadSafety;

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

        #endregion

        #region Private Methods

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

        private unsafe void Check()
        {
            var from = FromImpl;
            Debug.Assert(from != null, "Reached Check() without receiving a FROM clause!");

            from.ThreadSafety.DoLockedBridge(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return true;
                }

                _queryExpression = EncodeAsJSON();
                WriteLog.To.Query.I(Tag, $"Query encoded as {_queryExpression}");

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
