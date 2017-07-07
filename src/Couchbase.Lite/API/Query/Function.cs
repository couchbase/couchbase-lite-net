// 
// QueryFunction.cs
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
using Couchbase.Lite.Internal.Query;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class for creating <see cref="IFunction"/> instances
    /// </summary>
    public static class Function
    {
        #region Public Methods

        /// <summary>
        /// Creates a function that will calculate the average of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the average</returns>
        public static IFunction Avg(object expression)
        {
            return new QueryFunction("AVG()", expression);
        }

        /// <summary>
        /// Creates a function that will count the occurrences of 
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the count</returns>
        public static IFunction Count(object expression)
        {
            return new QueryFunction("COUNT()", expression);
        }

        /// <summary>
        /// Creates a function that will calculate the max value of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the max value</returns>
        public static IFunction Max(object expression)
        {
            return new QueryFunction("MAX()", expression);
        }

        /// <summary>
        /// Creates a function that will calculate the min value of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the min value</returns>
        public static IFunction Min(object expression)
        {
            return new QueryFunction("MIN()", expression);
        }

        /// <summary>
        /// Creates a function that will calculate the sum of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the sum</returns>
        public static IFunction Sum(object expression)
        {
            return new QueryFunction("SUM()", expression);
        }

        #endregion
    }
}
