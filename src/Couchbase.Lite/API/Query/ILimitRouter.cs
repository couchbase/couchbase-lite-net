// 
// ILimitRouter.cs
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
    /// An interface representing a query that can accept LIMIT as its next clause
    /// </summary>
    public interface ILimitRouter
    {
        #region Public Methods

        /// <summary>
        /// Limits a query to the given count (ulong, parameter, etc)
        /// </summary>
        /// <param name="limit">The amount to limit the query to</param>
        /// <returns>The query for further processing</returns>
        [NotNull]
        ILimit Limit(object limit);

        /// <summary>
        /// Limits a query to the given count and also offsets it by
        /// a given count (ulong, parameter, etc)
        /// </summary>
        /// <param name="limit">The amount to limit the query to</param>
        /// <param name="offset">The amount to offset the query by</param>
        /// <returns>The query for further processing</returns>
        [NotNull]
        ILimit Limit(object limit, object offset);

        #endregion
    }
}
