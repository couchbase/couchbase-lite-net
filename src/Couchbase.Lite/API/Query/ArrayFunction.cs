// 
//  ArrayFunction.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using Couchbase.Lite.Internal.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class for generating query functions that operate on
    /// array types
    /// </summary>
    public static class ArrayFunction
    {
        #region Public Methods

        /// <summary>
        /// Creates a function that will query if the given array expression contains
        /// the given element
        /// </summary>
        /// <param name="expression">An expression that evaluates to an array (otherwise the query will
        /// fail)</param>
        /// <param name="value">The element to search for (either an expression or literal)</param>
        /// <returns>A function that will return true if the array contains the element, or false
        /// if it does not</returns>
        [NotNull]
        public static IExpression Contains(IExpression expression, IExpression value) => new QueryCompoundExpression("ARRAY_CONTAINS()", expression, value);

        /// <summary>
        /// Creates a function that will get the length of an array
        /// in question
        /// </summary>
        /// <param name="expression">The expression to usem when calculating (must evaluate to an array type)
        /// the result</param>
        /// <returns>A function that will get the length of the array in question</returns>
        [NotNull]
        public static IExpression Length(IExpression expression) => new QueryCompoundExpression("ARRAY_LENGTH()", expression);

        #endregion
    }
}