// 
//  ArrayExpression.cs
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
    /// A class containing methods for generating queries that operate on
    /// array types
    /// </summary>
    public static class ArrayExpression
    {
        /// <summary>
        /// Returns the start of an expression that will evaluate if any elements
        /// inside of an array match a given predicate
        /// 
        /// Usage:  <code>ArrayExpression.Any("x").In(Expression.Property("prop")).Satisfies(ArrayExpression.Variable("x").EqualTo(42))</code>
        /// </summary>
        /// <param name="variable">The name to assign to the variable that will be used later
        /// via <see cref="Variable"/></param>
        /// <returns>The first portion of the completed expression for further modification</returns>
        [NotNull]
        public static IArrayExpressionIn Any(IVariableExpression variable) => new QueryTernaryExpression("ANY", variable);

        /// <summary>
        /// Returns the start of an expression that will evaluate the following:
        /// 1. The array is not empty (has "any" elements)
        /// 2. Every element in the array matches a given predicate ("every" element matches)
        /// 
        /// Usage:  <code>ArrayExpression.AnyAndEvery("x").In(Expression.Property("prop")).Satisfies(ArrayExpression.Variable("x").EqualTo(42))</code>
        /// </summary>
        /// <param name="variable">The name to assign to the variable that will be used later
        /// via <see cref="Variable"/></param>
        /// <returns>The first portion of the completed expression for further modification</returns>
        [NotNull]
        public static IArrayExpressionIn AnyAndEvery(IVariableExpression variable) => new QueryTernaryExpression("ANY AND EVERY", variable);

        /// <summary>
        /// Returns the start of an expression that will evaluate if every element inside
        /// of an array matches a given predicate (note: That means that an empty array will
        /// return <c>true</c> because "all zero" elements match)
        /// 
        /// Usage:  <code>ArrayExpression.Every("x").In(Expression.Property("prop")).Satisfies(ArrayExpression.Variable("x").EqualTo(42))</code>
        /// </summary>
        /// <param name="variable">The name to assign to the variable that will be used later
        /// via <see cref="Variable"/></param>
        /// <returns>The first portion of the completed expression for further modification</returns>
        [NotNull]
        public static IArrayExpressionIn Every(IVariableExpression variable) => new QueryTernaryExpression("EVERY", variable);

        /// <summary>
        /// Returns an expression representing the value of a named variable
        /// assigned by earlier calls to <see cref="Any"/> and family.
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <returns>An expression representing the value of a named variable</returns>
        [NotNull]
        public static IVariableExpression Variable(string name) => new QueryTypeExpression(name, ExpressionType.Variable);
    }
}