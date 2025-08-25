// 
// QueryBuilder.cs
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
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Query;

/// <summary>
/// A factory class for generating the initial portion of a query
/// </summary>
public static class QueryBuilder
{
    private const string Tag = nameof(QueryBuilder);

    /// <summary>
    /// Selects the given property path from the query under construction
    /// </summary>
    /// <param name="results">The results to select</param>
    /// <returns>The initial SELECT portion of the query</returns>
    public static ISelect Select(params ISelectResult[] results)
    {
        CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(results), results);
        return new Select(results, false);
    }


    /// <summary>
    /// Selects only the distinct results of the query
    /// </summary>
    /// <param name="results">The results to select</param>
    /// <returns>The initial SELECT portion of the query</returns>
    public static ISelect SelectDistinct(params ISelectResult[] results)
    {
        CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(results), results);
        return new Select(results, true);
    }
}