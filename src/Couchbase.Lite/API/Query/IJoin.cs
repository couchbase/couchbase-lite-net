// 
// IJoin.cs
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
    /// An interface representing a query that has just received a JOIN
    /// clause
    /// </summary>
    public interface IJoin : IQuery, IWhereRouter, IOrderByRouter, ILimitRouter
    {}

    /// <summary>
    /// An interface representing an <see cref="IQuery"/> with a 
    /// partially constructed JOIN clause that has not yet received its ON clause
    /// </summary>
    public interface IJoinOn : IJoin
    {
        #region Public Methods

        /// <summary>
        /// Adds the ON clause to the current JOIN clause
        /// </summary>
        /// <param name="expression">The expression to use as the ON clause</param>
        /// <returns>The query for further processing</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        IJoin On(IExpression expression);

        #endregion
    }
}
