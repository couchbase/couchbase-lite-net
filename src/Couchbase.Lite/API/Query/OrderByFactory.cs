// 
// OrderByFactory.cs
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

using System.Linq;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A factory class for generating <see cref="ISortOrder"/> objects
    /// </summary>
    public static class OrderByFactory
    {
        #region Public Methods

        /// <summary>
        /// Creates an object that will sort based on the given expression
        /// </summary>
        /// <param name="expression">The expression to use when sorting</param>
        /// <returns>The object that will perform the sort</returns>
        public static ISortOrder Expression(IExpression expression)
        {
            return new SortOrder(expression);
        }

        public static ISortOrder Property(string name)
        {
            return Expression(ExpressionFactory.Property(name));
        }

        public static IOrderBy OrderBy(params IOrderBy[] orderBy)
        {
            return new OrderBy(orderBy);
        }

        #endregion
    }
}
