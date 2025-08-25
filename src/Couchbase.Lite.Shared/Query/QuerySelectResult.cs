// 
// QuerySelectResult.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using System;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query;

internal sealed class QuerySelectResult : ISelectResultAs, ISelectResultFrom
{
    private const string Tag = nameof(QuerySelectResult);

    internal readonly IExpression Expression;
    private string? _alias;

    internal QuerySelectResult(IExpression expression)
    {
        Expression = expression;
    }

    public ISelectResult As(string alias)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(alias), alias);
        _alias = alias;
        return this;
    }

    public ISelectResult From(string alias)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(alias), alias);
        Misc.TryCast<IExpression, QueryTypeExpression>(Expression).From(alias);
        return this;
    }

    public object? ToJSON()
    {
        var json = Misc.TryCast<IExpression, QueryExpression>(Expression).ConvertToJSON();
        if (!String.IsNullOrEmpty(_alias))
            json = new[] { "AS", json, _alias };
        return json;
    }
}