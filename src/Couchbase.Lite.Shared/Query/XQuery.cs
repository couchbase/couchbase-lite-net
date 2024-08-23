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
using LiteCore;
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

        private string? _queryExpression;

        #endregion

        #region Properties

        protected bool Distinct { get; set; }

        protected QueryDataSource? FromImpl { get; set; }

        protected QueryGroupBy? GroupByImpl { get; set; }

        protected Having? HavingImpl { get; set; }

        protected QueryJoin? JoinImpl { get; set; }

        protected IExpression? LimitValue { get; set; }

        protected QueryOrderBy? OrderByImpl { get; set; }

        protected Select? SelectImpl { get; set; }

        protected IExpression? SkipValue { get; set; }

        protected QueryExpression? WhereImpl { get; set; }

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

        protected void ValidateParams<T>(T[] param, [CallerMemberName] string? tag = null)
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

            var paramJson = Parameters.FLEncode();

            var e = LiteCoreBridge.CheckTyped(err =>
            {
                if (_disposalWatchdog.IsDisposed) {
                    return null;
                }

                _c4Query = Check();
                return NativeSafe.c4query_run(_c4Query, (FLSlice)paramJson, err);
            });

            paramJson.Dispose();

            if (e == null) {
                return new NullResultSet();
            }

            return new QueryResultSet(this, fromImpl.ThreadSafety, e, ColumnNames);
        }

        public override unsafe string Explain()
        {
            const string defaultVal = "(Unable to explain)";
            _disposalWatchdog.CheckDisposed();

            // Used for debugging
            _c4Query = Check();

            return NativeSafe.c4query_explain(_c4Query!) ?? defaultVal;
        }

        protected override unsafe C4QueryWrapper CreateQuery()
        {
            if(_c4Query != null) {
                return _c4Query;
            }

            if (Database == null) {
                Debug.Assert(Collection != null);
                Database = Collection!.Database;
            }

            return LiteCoreBridge.CheckTyped(err =>
            {
                Debug.Assert(Database?.c4db != null);
                _queryExpression = EncodeAsJSON();
                WriteLog.To.Query.I(Tag, $"Query encoded as {_queryExpression}");
                return NativeSafe.c4query_new2(Database!.c4db!, C4QueryLanguage.JSONQuery, _queryExpression, null, err);
            })!;
        }

        protected override unsafe Dictionary<string, int> CreateColumnNames(C4QueryWrapper query)
        {
            var fromImpl = FromImpl;
            Debug.Assert(fromImpl != null, "CreateColumnNames reached without a FROM clause received");
            ThreadSafety = fromImpl!.ThreadSafety;

            var map = new Dictionary<string, int>();

            var columnCnt = NativeSafe.c4query_columnCount(query);
            for (int i = 0; i < columnCnt; i++) {
                var titleStr = NativeSafe.c4query_columnTitle(query, (uint)i).CreateString();
                Debug.Assert(titleStr != null);

                if (titleStr!.StartsWith("*")) {
                    titleStr = fromImpl.ColumnName;
                }

                if (map.ContainsKey(titleStr!)) {
                    throw new CouchbaseLiteException(C4ErrorCode.InvalidQuery,
                        String.Format(CouchbaseLiteErrorMessage.DuplicateSelectResultName, titleStr));
                }

                map.Add(titleStr!, i);
            }

            return map;
        }

        #endregion

        #region Private Methods

        private string EncodeAsJSON()
        {
            var parameters = new Dictionary<string, object?>();
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
                joinJson!.Insert(0, fromJson);
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

        private unsafe C4QueryWrapper Check()
        {
            var from = FromImpl;
            Debug.Assert(from != null, "Reached Check() without receiving a FROM clause!");

            return ThreadSafety.DoLocked(() =>
            {
                _disposalWatchdog.CheckDisposed();

                _queryExpression = EncodeAsJSON();
                WriteLog.To.Query.I(Tag, $"Query encoded as {_queryExpression}");

                return CreateQuery();
            });
        }

        #endregion
    }
}
