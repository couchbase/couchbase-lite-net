// 
// IExpression.cs
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
    /// An interface representing an abstract expression that can act on
    /// a given piece of data
    /// </summary>
    public interface IExpression
    {
        #region Public Methods

        /// <summary>
        /// Mathematically adds the given expression to the current expression
        /// </summary>
        /// <param name="expression">The expression to add</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Add(IExpression expression);

        /// <summary>
        /// Logically "ands" the given expression with the current expression
        /// </summary>
        /// <param name="expression">The expression to "and"</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression And(IExpression expression);

        /// <summary>
        /// Determines if the result is between the two given expressions
        /// </summary>
        /// <param name="expression1">The expression to use as the first bound</param>
        /// <param name="expression2">The expression to use as the second bound</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Between(IExpression expression1, IExpression expression2);

        /// <summary>
        /// Collates the previous expression using the given collation instance (normally 
        /// this is used directly after <see cref="Expression.Property(string)"/> when
        /// it is part of a <see cref="IWhereRouter.Where(IExpression)"/> or 
        /// <see cref="IOrderByRouter.OrderBy(IOrdering[])"/>)
        /// </summary>
        /// <param name="collation">The collation instance to use when collating</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Collate(ICollation collation);

        /// <summary>
        /// Matehematically divides the current and given expressions
        /// </summary>
        /// <param name="expression">The expression to divide</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Divide(IExpression expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// and current expression are equal
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression EqualTo(IExpression expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is greater than the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression GreaterThan(IExpression expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is greater than or equal to the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression GreaterThanOrEqualTo(IExpression expression);

        /// <summary>
        /// Returns an expression to test whether or not the given expression is contained
        /// in the given list of expressions
        /// </summary>
        /// <param name="expressions">The list of expressions to check</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression In(params IExpression[] expressions);

        /// <summary>
        /// Returns an expression to test whether or not the given expression is
        /// the same as the current current expression
        /// </summary>
        /// <param name="expression">The expression to compare to</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Is(IExpression expression);

        /// <summary>
        /// Returns an expression to test whether or not the given expression is
        /// NOT the same as the current current expression
        /// </summary>
        /// <param name="expression">The expression to compare to</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression IsNot(IExpression expression);

        /// <summary>
        /// Gets an expression representing if the current expression is null
        /// or missing (i.e. does not have a value)
        /// </summary>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression IsNullOrMissing();

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is less than the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression LessThan(IExpression expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is less than or equal to the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression LessThanOrEqualTo(IExpression expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is "LIKE" the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Like(IExpression expression);

        /// <summary>
        /// Returns an modulo math expression using the current and given expressions
        /// as operands
        /// </summary>
        /// <param name="expression">The expression to mod with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Modulo(IExpression expression);

        /// <summary>
        /// Returns a multiply expression using the current and given expressions as 
        /// operands
        /// </summary>
        /// <param name="expression">The expression to multiply with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Multiply(IExpression expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// and current expression are not equal
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression NotEqualTo(IExpression expression);

        /// <summary>
        /// Gets an expression representing if the current expression is neither null
        /// nor missing (i.e. has a value)
        /// </summary>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression NotNullOrMissing();

        /// <summary>
        /// Logically "ors" the given expression with the current expression
        /// </summary>
        /// <param name="expression">The expression to "and"</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Or(IExpression expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression regex matches the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Regex(IExpression expression);

        /// <summary>
        /// Mathematically subtracts the given expression to the current expression
        /// </summary>
        /// <param name="expression">The expression to subtract</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Subtract(IExpression expression);

        #endregion
    }
}
