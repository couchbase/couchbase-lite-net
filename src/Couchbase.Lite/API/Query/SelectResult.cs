// 
// SelectResultFactory.cs
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
    /// A class for generating instances of <see cref="ISelectResult"/>.  This *will*
    /// be expanded on in the near future.
    /// </summary>
    public static class SelectResult
    {
        /// <summary>
        /// Creates an instance based on the given expression
        /// </summary>
        /// <param name="expression">The expression describing what to select from the
        /// query (e.g. <see cref="Lite.Query.Expression.Property(string)"/>)</param>
        /// <returns></returns>
        public static ISelectResult Expression(IExpression expression)
        {
            return new QuerySelectResult(expression);
        }
    }
}