// 
// QueryGroupBy.cs
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
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query;

internal sealed class QueryGroupBy : LimitedQuery, IGroupBy
{
    private const string Tag = nameof(QueryGroupBy);

    private readonly IList<IExpression> _expressions;

    internal QueryGroupBy(IList<IExpression> expressions)
    {
        _expressions = expressions;
        GroupByImpl = this;
    }

    internal QueryGroupBy(XQuery query, IList<IExpression> expressions)
        : this(expressions)
    {
        Copy(query);
        GroupByImpl = this;
    }

    internal QueryGroupBy(IExpression expression)
    {
        _expressions = [expression];
        GroupByImpl = this;
    }

    public object ToJSON() =>
        _expressions.OfType<QueryExpression>()
            .Select(o => o.ConvertToJSON()).ToList();

    public IHaving Having(IExpression expression)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression);
        return new Having(this, expression);
    }

    public IOrderBy OrderBy(params IOrdering[] orderings)
    {
        CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(orderings), orderings);
        return new QueryOrderBy(this, orderings);
    }
}