// 
// Expression.cs
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
    /// A factory for unary IExpression operators
    /// </summary>
    public static class Expression
    {
        #region Public Methods

        /// <summary>
        /// Returns the start of an expression that will evaluate if any elements
        /// inside of an array match a given predicate
        /// 
        /// Usage:  <code>Expression.Any("x").In(Expression.Property("prop")).Satisfies(Expression.Variable("x").EqualTo(42))</code>
        /// </summary>
        /// <param name="variable">The name to assign to the variable that will be used later
        /// via <see cref="Variable(string)"/></param>
        /// <returns>The first portion of the completed expression for further modification</returns>
        public static IExpressionIn Any(string variable)
        {
            return new QueryTernaryExpression("ANY", variable);
        }

        /// <summary>
        /// Returns the start of an expression that will evaluate the following:
        /// 1. The array is not empty (has "any" elements)
        /// 2. Every element in the array matches a given predicate ("every" element matches)
        /// 
        /// Usage:  <code>Expression.AnyAndEvery("x").In(Expression.Property("prop")).Satisfies(Expression.Variable("x").EqualTo(42))</code>
        /// </summary>
        /// <param name="variable">The name to assign to the variable that will be used later
        /// via <see cref="Variable(string)"/></param>
        /// <returns>The first portion of the completed expression for further modification</returns>
        public static IExpressionIn AnyAndEvery(string variable)
        {
            return new QueryTernaryExpression("ANY AND EVERY", variable);
        }

        /// <summary>
        /// Returns the start of an expression that will evaluate if every element inside
        /// of an array matches a given predicate (note: That means that an empty array will
        /// return <c>true</c> because "all zero" elements match)
        /// 
        /// Usage:  <code>Expression.Every("x").In(Expression.Property("prop")).Satisfies(Expression.Variable("x").EqualTo(42))</code>
        /// </summary>
        /// <param name="variable">The name to assign to the variable that will be used later
        /// via <see cref="Variable(string)"/></param>
        /// <returns>The first portion of the completed expression for further modification</returns>
        public static IExpressionIn Every(string variable)
        {
            return new QueryTernaryExpression("EVERY", variable);
        }

        /// <summary>
        /// Creates an object that can generate expressions for retrieving metadata about
        /// a result
        /// </summary>
        /// <returns>An object that can generate expressions for retrieving metadata about
        /// a result</returns>
        public static IMeta Meta()
        {
            return new QueryMeta();
        }

        /// <summary>
        /// Returns an expression representing the negated result of an expression
        /// </summary>
        /// <param name="expression">The expression to evaluate</param>
        /// <returns>The negated result of the expression</returns>
        public static IExpression Negated(IExpression expression)
        {
            return new QueryCompoundExpression("NOT", expression);
        }

        /// <summary>
        /// Returns an expression representing the negated result of an expression
        /// </summary>
        /// <param name="expression">The expression to evaluate</param>
        /// <returns>The negated result of the expression</returns>
        public static IExpression Not(IExpression expression)
        {
            return Negated(expression);
        }

        /// <summary>
        /// Gets an expression representing a positional parameter (as set in
        /// <see cref="IQuery.Parameters"/>) for use in a query
        /// </summary>
        /// <param name="index">The position of the parameter in the parameter list</param>
        /// <returns>The expression representing the parameter</returns>
        public static IExpression Parameter(int index)
        {
            return new QueryTypeExpression(index.ToString(), ExpressionType.Parameter);
        }

        /// <summary>
        /// Gets an expression representing a named parameter (as set in
        /// <see cref="IQuery.Parameters"/>) for use in a query
        /// </summary>
        /// <param name="name">The name of the parameter in the parameter set</param>
        /// <returns>The expression representing the parameter</returns>
        public static IExpression Parameter(string name)
        {
            return new QueryTypeExpression(name, ExpressionType.Parameter);
        }

        /// <summary>
        /// Returns an expression representing the value of a named property
        /// </summary>
        /// <param name="property">The name of the property to fetch</param>
        /// <returns>An expression representing the value of a named property</returns>
        public static IPropertyExpression Property(string property)
        {
            return new QueryTypeExpression(property, ExpressionType.KeyPath);
        }

        /// <summary>
        /// Returns an expression representing the value of a named variable
        /// assigned by earlier calls to <see cref="Any(string)"/> and family.
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <returns>An expression representing the value of a named variable</returns>
        public static IPropertyExpression Variable(string name)
        {
            return new QueryTypeExpression(name, ExpressionType.Variable);
        }

        #endregion
    }
}
