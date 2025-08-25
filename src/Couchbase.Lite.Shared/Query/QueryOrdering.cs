// 
// Ordering.cs
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

using Couchbase.Lite.Query;

namespace Couchbase.Lite.Internal.Query;

internal class QueryOrderBy : LimitedQuery, IOrderBy
{
    internal IList<IOrdering>? Orders { get; }

    internal QueryOrderBy(IList<IOrdering>? orderBy)
    {
        Orders = orderBy;
        OrderByImpl = this;
    }

    internal QueryOrderBy(XQuery query, IList<IOrdering> orderBy)
        : this(orderBy)
    {
        Copy(query);
        OrderByImpl = this;
    }

    public virtual object? ToJSON()
    {
        var obj = new List<object?>();
        if (Orders == null) {
            return obj;
        }

        obj.AddRange(Orders.OfType<QueryOrderBy>().Select(o => o.ToJSON()));
        return obj;
    }
}

internal sealed class SortOrder : QueryOrderBy, ISortOrder
{
    internal IExpression Expression { get; }

    internal bool IsAscending { get; private set; }

    internal SortOrder(IExpression expression) : base(null)
    {
        IsAscending = true;
        Expression = expression;
    }

    public override object? ToJSON()
    {
        var obj = new List<object?>();
        if (Expression is not QueryExpression exp) {
            return obj;
        }
        
        if (!IsAscending) {
            obj.Add("DESC");
        } else {
            return exp.ConvertToJSON();
        }

        obj.Add(exp.ConvertToJSON());

        return obj;
    }

    public IOrdering Ascending()
    {
        IsAscending = true;
        return this;
    }

    public IOrdering Descending()
    {
        IsAscending = false;
        return this;
    }
}