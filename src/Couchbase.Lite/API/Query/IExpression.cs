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
        IExpression Add(object expression);

        /// <summary>
        /// Logically "ands" the given expression with the current expression
        /// </summary>
        /// <param name="expression">The expression to "and"</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression And(object expression);

        /// <summary>
        /// Determines if the result is between the current and given expressions
        /// </summary>
        /// <param name="expression">The expression to use as one of the bounds for
        /// the between operation</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Between(object expression);

        /// <summary>
        /// Concatenates the current and given expressions
        /// </summary>
        /// <param name="expression">The expression to concatenate with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Concat(object expression);

        /// <summary>
        /// Matehematically divides the current and given expressions
        /// </summary>
        /// <param name="expression">The expression to divide</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Divide(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// and current expression are equal
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression EqualTo(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is greater than the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression GreaterThan(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is greater than or equal to the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression GreaterThanOrEqualTo(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is equal to the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Is(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is not equal to the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression IsNot(object expression);

        /// <summary>
        /// Gets an expression representing if the current expression is null
        /// </summary>
        /// <returns>The expression representing the new operation</returns>
        IExpression IsNull();

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is less than the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression LessThan(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is less than or equal to the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression LessThanOrEqualTo(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is "LIKE" the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Like(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression full text matches the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Match(object expression);

        /// <summary>
        /// Returns an modulo math expression using the current and given expressions
        /// as operands
        /// </summary>
        /// <param name="expression">The expression to mod with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Modulo(object expression);

        /// <summary>
        /// Returns a multiply expression using the current and given expressions as 
        /// operands
        /// </summary>
        /// <param name="expression">The expression to multiply with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Multiply(object expression);

        /// <summary>
        /// Determines if the result is not between the current and given expressions
        /// </summary>
        /// <param name="expression">The expression to use as one of the bounds for
        /// the between operation</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotBetween(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// and current expression are not equal
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotEqualTo(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is not greater than the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotGreaterThan(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is not greater than or equal to the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotGreaterThanOrEqualTo(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is less than the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotLessThan(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is less than or equal to the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotLessThanOrEqualTo(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression is "NOT LIKE" the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotLike(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression doesn't full text match the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotMatch(object expression);

        /// <summary>
        /// Gets an expression representing if the current expression is not null
        /// </summary>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotNull(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression doesn't regex match the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression NotRegex(object expression);

        /// <summary>
        /// Logically "ors" the given expression with the current expression
        /// </summary>
        /// <param name="expression">The expression to "and"</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Or(object expression);

        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression regex matches the current one
        /// </summary>
        /// <param name="expression">The expression to compare with the current one</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Regex(object expression);

        /// <summary>
        /// Mathematically subtracts the given expression to the current expression
        /// </summary>
        /// <param name="expression">The expression to subtract</param>
        /// <returns>The expression representing the new operation</returns>
        IExpression Subtract(object expression);

        #endregion
    }
}
