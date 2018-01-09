// 
// IOrderByRouter.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
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

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface representing a portion of a query that can be routed
    /// to an ORDER BY portion
    /// </summary>
    public interface IOrderByRouter
    {
        #region Public Methods

        /// <summary>
        /// Routes this IExpression to the next ORDER BY portion of the query
        /// </summary>
        /// <param name="orderings">An array of order by operations to consider in the 
        /// ORDER BY portion of the query</param>
        /// <returns>The next ORDER BY portion of the query</returns>
        [NotNull]
        IOrderBy OrderBy(params IOrdering[] orderings);

        #endregion
    }
}
