// 
// IJoinRouter.cs
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
    /// An interface representing a portion of an <see cref="IQuery"/> that can accept JOIN
    /// as its next clause
    /// </summary>
    public interface IJoinRouter
    {
        #region Public Methods

        /// <summary>
        /// Create and appends the list of JOINS to the current <see cref="IQuery"/>
        /// </summary>
        /// <param name="joins">The join clauses to add</param>
        /// <returns>The query with the join statement, for further processing</returns>
        [NotNull]
        IJoin Join(params IJoin[] joins);

        #endregion
    }
}
