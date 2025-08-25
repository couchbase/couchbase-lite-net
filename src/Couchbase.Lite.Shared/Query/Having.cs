// 
// Having.cs
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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query;

internal sealed class Having : LimitedQuery, IHaving
{
    private const string Tag = nameof(IndexBuilder);

    private readonly IExpression _expression;

    internal Having(XQuery source, IExpression expression)
    {
        Copy(source);

        _expression = expression;
        HavingImpl = this;
    }

    public object? ToJSON() => (_expression as QueryExpression)?.ConvertToJSON();

    public IOrderBy OrderBy(params IOrdering[] orderings)
    {
        CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(orderings), orderings);
        ValidateParams(orderings);
        return new QueryOrderBy(this, orderings);
    }
}