// 
// IGroupByRouter.cs
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

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface representing a portion of a query which can take GROUP BY
    /// as its next step
    /// </summary>
    public interface IGroupByRouter
    {
        #region Public Methods

        /// <summary>
        /// Groups the current query by the given GROUP BY clauses
        /// </summary>
        /// <param name="groupBy">The clauses to group by</param>
        /// <returns>The query grouped by the given clauses for further processing</returns>
        IGroupBy GroupBy(params IGroupBy[] groupBy);

        #endregion
    }
}
