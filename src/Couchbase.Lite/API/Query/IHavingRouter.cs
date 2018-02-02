// 
// IHavingRouter.cs
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
    /// An interface representing a portion of a query that can take HAVING
    /// as its next step
    /// </summary>
    public interface IHavingRouter
    {
        #region Public Methods

        /// <summary>
        /// Adds the given expression as a HAVING clause onto this query
        /// </summary>
        /// <param name="expression">The expression to use in the HAVING clause</param>
        /// <returns>The query with the new clause for further processing</returns>
        [NotNull]
        IHaving Having(IExpression expression);

        #endregion
    }
}
