// 
// IOrdering.cs
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
    /// An interface representing the ORDER BY portion of an <see cref="IQuery"/>
    /// </summary>
    public interface IOrdering : IQuery
    {}

    /// <summary>
    /// An interface representing the way that an <see cref="IOrdering"/> should be
    /// sorted
    /// </summary>
    public interface ISortOrder : IOrdering
    {
        #region Public Methods

        /// <summary>
        /// Returns an IExpression that will sort in ascending order
        /// </summary>
        /// <returns>An IExpression that will sort in ascending order</returns>
        IOrdering Ascending();

        /// <summary>
        /// Returns an IExpression that will sort in desecending order
        /// </summary>
        /// <returns>An IExpression that will sort in desecending order</returns>
        IOrdering Descending();

        #endregion
    }
}
