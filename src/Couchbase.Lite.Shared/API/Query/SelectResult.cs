﻿// 
// SelectResult.cs
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
/// A class for generating instances of <see cref="ISelectResult"/>.  This *will*
/// be expanded on in the near future.
/// </summary>
public static class SelectResult
{
    private const string Tag = nameof(SelectResult);

    /// <summary>
    /// Creates an instance based on the given expression
    /// </summary>
    /// <param name="expression">The expression describing what to select from the
    /// query (e.g. <see cref="Lite.Query.Expression.Property(string)"/>)</param>
    /// <returns>The instantiated instance</returns>
    public static ISelectResultAs Expression(IExpression expression) => 
        new QuerySelectResult(CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

    /// <summary>
    /// Creates an instanced based on a given property path
    /// </summary>
    /// <param name="property">The property path to select</param>
    /// <returns>The instantiated instance</returns>
    /// <remarks>Equivalent to <c>SelectResult.Expression(Expression.Property(property))</c></remarks>
    public static ISelectResultAs Property(string property) => new QuerySelectResult(Query.Expression.Property(property));

    /// <summary>
    /// Creates a select result instance that will return all the
    /// data in the retrieved document
    /// </summary>
    /// <returns>The instantiated instance</returns>
    public static ISelectResultFrom All() => new QuerySelectResult(Query.Expression.All());
}