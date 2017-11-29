// 
//  FullTextFunction.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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

namespace Couchbase.Lite.Query
{
    public static class FullTextFunction
    {
        /// <summary>
        /// Creates a full-text ranking value function indicating how well the current
        /// query result matches the full-text query when performing the match comparison.
        /// </summary>
        /// <param name="indexName">The FTS index name to use when performing the calculation</param>
        /// <returns>A function that will perform the ranking</returns>
        [NotNull]
        public static IExpression Rank(string indexName) => new QueryCompoundExpression("RANK()", indexName);
    }
}