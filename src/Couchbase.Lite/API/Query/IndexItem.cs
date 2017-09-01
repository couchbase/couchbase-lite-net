// 
// IndexItem.cs
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
    /// A factory class for creating <see cref="IValueIndexItem"/> instances
    /// </summary>
    public static class ValueIndexItem
    {
        /// <summary>
        /// Creates a value index item based on a given <see cref="IExpression"/>
        /// </summary>
        /// <param name="expression">The expression to base the index item on</param>
        /// <returns>The created index item</returns>
        public static IValueIndexItem Expression(IExpression expression)
        {
            return new QueryIndexItem(expression);
        }
    }

    /// <summary>
    /// A factory class for creating <see cref="IFTSIndexItem"/> instances
    /// </summary>
    public static class FTSIndexItem
    {
        /// <summary>
        /// Creates an FTS index item based on a given <see cref="IExpression"/>
        /// </summary>
        /// <param name="expression">The expression to base the index item on</param>
        /// <returns>The created index item</returns>
        public static IFTSIndexItem Expression(IExpression expression)
        {
            return new QueryIndexItem(expression);
        }
    }
}