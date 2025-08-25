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

namespace Couchbase.Lite.Internal.Query;

internal class XQuery : QueryBase
{
    private const string Tag = nameof(XQuery);

    private string? _queryExpression;

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


        var e = LiteCoreBridge.CheckTyped(err =>
        {
            if (DisposalWatchdog.IsDisposed) {
                return null;
            }

            using var paramJson = Parameters.FLEncode();
            Check();
            return NativeSafe.c4query_run(_c4Query!, (FLSlice)paramJson, err);
        });

        if (e == null) {
            return new NullResultSet();
        }

        return new QueryResultSet(this, fromImpl.ThreadSafety, e, ColumnNames);
    }

    public override string Explain()
    {
        const string defaultVal = "(Unable to explain)";
        DisposalWatchdog.CheckDisposed();

        // Used for debugging
        Check();

        return NativeSafe.c4query_explain(_c4Query!) ?? defaultVal;
    }

    protected override unsafe void CreateQuery()
    {
        if(_c4Query != null) {
            return;
        }

        if (Database == null) {
            Debug.Assert(Collection != null);
            Database = Collection!.Database;
        }

        _c4Query = LiteCoreBridge.CheckTyped(err =>
        {
            Debug.Assert(Database?.C4db != null);
            _queryExpression = EncodeAsJSON();
            WriteLog.To.Query.I(Tag, $"Query encoded as {_queryExpression}");
            return NativeSafe.c4query_new2(Database!.C4db!, C4QueryLanguage.JSONQuery, _queryExpression, null, err);
        })!;
    }

    protected override Dictionary<string, int> CreateColumnNames(C4QueryWrapper query)
    {
        var fromImpl = CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(FromImpl), FromImpl);
        ThreadSafety = fromImpl.ThreadSafety;

        var map = new Dictionary<string, int>();

        var columnCnt = NativeSafe.c4query_columnCount(query);
        for (var i = 0; i < columnCnt; i++) {
            var titleStr = CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, 
                "titleStr", NativeSafe.c4query_columnTitle(query, (uint)i).CreateString());

            if (titleStr.StartsWith("*")) {
                titleStr = CBDebug.MustNotBeNull(WriteLog.To.Query, Tag,
                    "fromImpl.ColumnName", fromImpl.ColumnName);
            }

            if (!map.TryAdd(titleStr, i)) {
                throw new CouchbaseLiteException(C4ErrorCode.InvalidQuery,
                    String.Format(CouchbaseLiteErrorMessage.DuplicateSelectResultName, titleStr));
            }
        }

        return map;
    }

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

            var joinJson = CBDebug.MustNotBeNull(WriteLog.To.Query, Tag,
                "JoinImpl", JoinImpl.ToJSON() as IList<object>);
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

    private void Check()
    {
        var from = FromImpl;
        Debug.Assert(from != null, "Reached Check() without receiving a FROM clause!");

        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        DisposalWatchdog.CheckDisposed();
        _queryExpression = EncodeAsJSON();
        WriteLog.To.Query.I(Tag, $"Query encoded as {_queryExpression}");

        CreateQuery();
    }
}