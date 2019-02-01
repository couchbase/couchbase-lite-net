// 
// IWhereRouter.cs
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
    /// An interface representing a portion of a query that can be routed to
    /// a WHERE portion of the query.
    /// </summary>
    public interface IWhereRouter
    {
        #region Public Methods

        /// <summary>
        /// Routes this portion of the query to the next WHERE portion of the
        /// query
        /// </summary>
        /// <param name="expression">The expression to evaluate in the WHERE portion</param>
        /// <returns>The next WHERE portion of the query</returns>
        [NotNull]
        IWhere Where([NotNull]IExpression expression);

        #endregion
    }
}
