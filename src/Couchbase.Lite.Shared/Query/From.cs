// 
//  From.cs
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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query;

internal sealed class From : LimitedQuery, IFrom
{
    private const string Tag = nameof(From);

    internal From(XQuery query, IDataSource impl)
    {
        Copy(query);

        FromImpl = Misc.TryCast<IDataSource, QueryDataSource>(impl);
        Collection = (impl as DatabaseSource)?.Collection;
    }

    public object? ToJSON() => FromImpl?.ToJSON();

    public IGroupBy GroupBy(params IExpression[] expressions)
    {
        CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(expressions), expressions);
        ValidateParams(expressions);
        return new QueryGroupBy(this, expressions);
    }

    public IJoins Join(params IJoin[] joins)
    {
        CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(joins), joins);
        ValidateParams(joins);
        return new QueryJoin(this, joins);
    }

    public IOrderBy OrderBy(params IOrdering[] orderings)
    {
        CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(orderings), orderings);
        ValidateParams(orderings);
        return new QueryOrderBy(this, orderings);
    }

    public IWhere Where(IExpression expression)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression);
        return new Where(this, expression);
    }
}