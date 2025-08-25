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

using LiteCore;
using LiteCore.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#if !NET8_0_OR_GREATER
using Couchbase.Lite.Util;
#endif

namespace Couchbase.Lite.Internal.Query;

internal sealed class NQuery : QueryBase
{
    private const string Tag = nameof(NQuery);

    private readonly string _sqlppExpression;

    internal NQuery(string sqlppExpression, Database database)
    {
        Database = database;
        _sqlppExpression = sqlppExpression;

        // Catch N1QL compile error sooner
        Compile();
    }

    public override unsafe IResultSet Execute()
    {
        if (Database == null) {
            throw new InvalidOperationException(CouchbaseLiteErrorMessage.InvalidQueryDBNull);
        }

        if(_c4Query == null) {
            WriteLog.To.Query.E(Tag, "Native query object null in Execute(), returning no results...");
            return new NullResultSet();
        }

        var e = LiteCoreBridge.CheckTyped(err =>
        {
            if (DisposalWatchdog.IsDisposed) {
                return null;
            }

            return NativeSafe.c4query_run(_c4Query, FLSlice.Null, err);
        });

        if (e == null) {
            return new NullResultSet();
        }

        return new QueryResultSet(this, Database!.ThreadSafety, e, ColumnNames);
    }

    public override string Explain()
    {
        const string defaultString = "(Unable to explain)";
        if (_c4Query == null) {
            WriteLog.To.Query.W(Tag, "Native query object null in Explain(), returning...");
            return defaultString;
        }

        DisposalWatchdog.CheckDisposed();
        return NativeSafe.c4query_explain(_c4Query) ?? defaultString;
    }

    protected override unsafe void CreateQuery()
    {
        _c4Query ??= LiteCoreBridge.CheckTyped(err =>
        {
            Debug.Assert(Database?.C4db != null);
            return NativeSafe.c4query_new2(Database!.C4db!, C4QueryLanguage.N1QLQuery, _sqlppExpression, null, err);
        })!;
    }

    protected override Dictionary<string, int> CreateColumnNames(C4QueryWrapper query)
    {
        var map = new Dictionary<string, int>();

        var columnCnt = NativeSafe.c4query_columnCount(query);
        for (int i = 0; i < columnCnt; i++) {
            var titleStr = NativeSafe.c4query_columnTitle(query, (uint)i).CreateString();
            if(titleStr == null) {
                throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Null column title in query!");
            }

            if (!map.TryAdd(titleStr, i)) {
                throw new CouchbaseLiteException(C4ErrorCode.InvalidQuery,
                    String.Format(CouchbaseLiteErrorMessage.DuplicateSelectResultName, titleStr));
            }

        }

        return map;
    }

    private void Compile()
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        DisposalWatchdog.CheckDisposed();
        CreateQuery();
    }
}