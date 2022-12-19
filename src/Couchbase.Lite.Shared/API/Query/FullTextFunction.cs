// 
//  FullTextFunction.cs
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

using Couchbase.Lite.Internal.Query;

using JetBrains.Annotations;
using System;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class that generates functions for use on the results of a full-text search
    /// </summary>
    public static class FullTextFunction
    {
        /// <summary>
        /// Create a full-text matching function that will check for matches between given 
        /// full text index name and the given query expression
        /// </summary>
        /// <param name="indexName">The name of the full-text index to perform the
        /// check against</param>
        /// <param name="query">The query expression to perform the check against</param>
        /// <returns>A function that will perform the matching</returns>
        [NotNull]
        [Obsolete("Use Match(index, query) instead")]
        public static IExpression Match(string indexName, string query) =>
            Match(Expression.FullTextIndex(indexName), query);

        /// <summary>
        /// Creates a full-text ranking value function indicating how well the current
        /// query result matches the full-text query when performing the match comparison.
        /// </summary>
        /// <param name="indexName">The FTS index name to use when performing the calculation</param>
        /// <returns>A function that will perform the ranking</returns>
        [NotNull]
        [Obsolete("Use Rank(index) instead")]
        public static IExpression Rank(string indexName) => Rank(Expression.FullTextIndex(indexName));

        /// <summary>
        /// Create a full-text matching function that will check for matches between given 
        /// full text index name and the given query expression
        /// </summary>
        /// <param name="index">The full-text index to perform the
        /// check against</param>
        /// <param name="query">The query expression to perform the check against</param>
        /// <returns>A function that will perform the matching</returns>
        [NotNull]
        public static IExpression Match(IIndexExpression index, string query) =>
            new QueryCompoundExpression("MATCH()", Expression.String(index.ToString()), Expression.String(query));

        /// <summary>
        /// Creates a full-text ranking value function indicating how well the current
        /// query result matches the full-text query when performing the match comparison.
        /// </summary>
        /// <param name="index">The FTS index to use when performing the calculation</param>
        /// <returns>A function that will perform the ranking</returns>
        [NotNull]
        public static IExpression Rank(IIndexExpression index) => new QueryCompoundExpression("RANK()", Expression.String(index.ToString()));
    }
}