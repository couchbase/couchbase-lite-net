// 
// Select.cs
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

using System.Linq;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query;

internal sealed class Select : XQuery, ISelect
{
    private const string Tag = nameof(Select);

    internal QuerySelectResult[] SelectResults { get; }

    public Select(ISelectResult[] selects, bool distinct)
    {
        SelectResults = selects.OfType<QuerySelectResult>().ToArray();
        SelectImpl = this;
        Distinct = distinct;
    }

    public object ToJSON() => SelectResults.Select(o => o.ToJSON()).ToList();

    public IFrom From(IDataSource dataSource)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(dataSource), dataSource);
        return new From(this, dataSource);
    }

    public IJoin Join(params IJoin[] joins)
    {
        CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(joins), joins);
        return new QueryJoin(this, joins);
    }
}