// 
// QueryFactory.cs
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
    /// A factory class for generating the initial portion of a query
    /// </summary>
    public static class QueryFactory
    {
        #region Public Methods

        /// <summary>
        /// Selects all the results of the query
        /// </summary>
        /// <returns>The initial SELECT portion of the query</returns>
        public static ISelect Select()
        {
            return Select(null);
        }

        /// <summary>
        /// Selects the given property path from the query under construction
        /// </summary>
        /// <param name="propertyPath">The property path to select</param>
        /// <returns>The initial SELECT portion of the query</returns>
        public static ISelect Select(string propertyPath)
        {
            return new Select(propertyPath, false);
        }

        /// <summary>
        /// Selects only the distinct results of the query
        /// </summary>
        /// <returns>The initial SELECT portion of the query</returns>
        public static ISelect SelectDistinct()
        {
            return SelectDistinct(null);
        }

        /// <summary>
        /// Selects only the distinct results of the query
        /// </summary>
        /// <param name="propertyPath">The property path to select</param>
        /// <returns>The initial SELECT portion of the query</returns>
        public static ISelect SelectDistinct(string propertyPath)
        {
            return new Select(propertyPath, true);
        }

        #endregion
    }
}
