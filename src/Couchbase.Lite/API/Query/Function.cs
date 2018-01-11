// 
// QueryCompoundExpression.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class for creating <see cref="IExpression"/> instances that represent functions
    /// </summary>
    public static class Function
    {
        #region Public Methods

        /// <summary>
        /// Creates a function that will get the absolute value of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the absolute value of the expression in question</returns>
        [NotNull]
        public static IExpression Abs(object expression) => new QueryCompoundExpression("ABS()", expression);

        /// <summary>
        /// Creates a function that will get the inverse cosine of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the inverse cosine of the expression in question</returns>
        [NotNull]
        public static IExpression Acos(object expression) => new QueryCompoundExpression("ACOS()", expression);

        /// <summary>
        /// Creates a function that will get the inverse sin of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the inverse sin of the expression in question</returns>
        [NotNull]
        public static IExpression Asin(object expression) => new QueryCompoundExpression("ASIN()", expression);

        /// <summary>
        /// Creates a function that will get the inverse tangent of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the  inverse tangent of the expression in question</returns>
        [NotNull]
        public static IExpression Atan(object expression) => new QueryCompoundExpression("ATAN()", expression);

        /// <summary>
        /// Creates a function that will get the arctangent of the point expressed by
        /// expressions calculating X and Y of the point for the formula
        /// </summary>
        /// <param name="expressionX">An expression or literal to evaluate to get the X coordinate to use</param>
        /// <param name="expressionY">An expression or literal to evaluate to get the Y coordinate to use</param>
        /// <returns>A function that will get the arctangent of the point in question</returns>
        [NotNull]
        public static IExpression Atan2(object expressionX, object expressionY) => new QueryCompoundExpression("ATAN2()", expressionX, expressionY);

        /// <summary>
        /// Creates a function that will calculate the average of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the average</returns>
        [NotNull]
        public static IExpression Avg(object expression) => new QueryCompoundExpression("AVG()", expression);

        /// <summary>
        /// Creates a function that will get the ceiling value of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the ceiling value of the expression in question</returns>
        [NotNull]
        public static IExpression Ceil(object expression) => new QueryCompoundExpression("CEIL()", expression);

        /// <summary>
        /// Creates a function that will calculate if a given string is inside of another
        /// in question
        /// </summary>
        /// <param name="expression">The string or expression that evaluates to a string to search</param>
        /// <param name="substring">The string or expression that evaluates to a string to search for</param>
        /// <returns>A function that will return true if the string contains the other, or false if it does not</returns>
        [NotNull]
        public static IExpression Contains(object expression, object substring) => new QueryCompoundExpression("CONTAINS()", expression, substring);

        /// <summary>
        /// Creates a function that will get the cosine of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the cosine of the expression in question</returns>
        [NotNull]
        public static IExpression Cos(object expression) => new QueryCompoundExpression("COS()", expression);

        /// <summary>
        /// Creates a function that will count the occurrences of 
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the count</returns>
        [NotNull]
        public static IExpression Count(object expression) => new QueryCompoundExpression("COUNT()", expression);

        /// <summary>
        /// Creates a function that will convert a numeric expression to degrees from radians
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the value of the expression in question expressed in degrees</returns>
        [NotNull]
        public static IExpression Degrees(object expression) => new QueryCompoundExpression("DEGREES()", expression);

        /// <summary>
        /// Creates a function that will return the value of the mathemetical constant 'e'
        /// </summary>
        /// <returns>The value of 'e'</returns>
        [NotNull]
        public static IExpression E() => new QueryCompoundExpression("E()");

        /// <summary>
        /// Returns the mathematical constant 'e' raised to the given power
        /// </summary>
        /// <param name="expression">The numerical expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the mathematical constant 'e' raised to the given power</returns>
        [NotNull]
        public static IExpression Exp(object expression) => new QueryCompoundExpression("EXP()", expression);

        /// <summary>
        /// Creates a function that will get the floor value of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the floor value of the expression in question</returns>
        [NotNull]
        public static IExpression Floor(object expression) => new QueryCompoundExpression("FLOOR()", expression);

        /// <summary>
        /// Creates a function that gets the length of a string
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result (must be or evaluate to a string)</param>
        /// <returns>The length of the string in question</returns>
        [NotNull]
        public static IExpression Length(object expression) => new QueryCompoundExpression("LENGTH()", expression);

        /// <summary>
        /// Creates a function that gets the natural log of the numerical expression
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that gets the natural log of the expression</returns>
        [NotNull]
        public static IExpression Ln(object expression) => new QueryCompoundExpression("LN()", expression);

        /// <summary>
        /// Creates a function that gets the base 10 log of the numerical expression
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that gets the base 10 log of the expression</returns>
        [NotNull]
        public static IExpression Log(object expression) => new QueryCompoundExpression("LOG()", expression);

        /// <summary>
        /// Creates a function that converts a string to lower case
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that converts a string to lower case</returns>
        [NotNull]
        public static IExpression Lower(object expression) => new QueryCompoundExpression("LOWER()", expression);

        /// <summary>
        /// Creates a function that removes whitespace from the beginning of a string
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that removes whitespace from the beginning of a string</returns>
        [NotNull]
        public static IExpression Ltrim(object expression) => new QueryCompoundExpression("LTRIM()", expression);

        /// <summary>
        /// Creates a function that will calculate the max value of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the max value</returns>
        [NotNull]
        public static IExpression Max(object expression) => new QueryCompoundExpression("MAX()", expression);

        /// <summary>
        /// Creates a function that will calculate the min value of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the min value</returns>
        [NotNull]
        public static IExpression Min(object expression) => new QueryCompoundExpression("MIN()", expression);

        /// <summary>
        /// Creates a function that will return the value of the mathemetical constant 'π'
        /// </summary>
        /// <returns>The value of 'π'</returns>
        [NotNull]
        public static IExpression Pi() => new QueryCompoundExpression("PI()");

        /// <summary>
        /// Creates a function that will raise the given numeric expression
        /// to an expression that determines the exponent
        /// </summary>
        /// <param name="b">A numeric literal or expression that provides the base</param>
        /// <param name="exponent">A numeric literal or expression that provides the exponent</param>
        /// <returns>A function that will raise the base to the given exponent</returns>
        [NotNull]
        public static IExpression Power(object b, object exponent) => new QueryCompoundExpression("POWER()", b, exponent);

        /// <summary>
        /// Creates a function that will convert a numeric expression to radians from degrees
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the value of the expression in question expressed in radians</returns>
        [NotNull]
        public static IExpression Radians(object expression) => new QueryCompoundExpression("RADIANS()", expression);

        /// <summary>
        /// Creates a function that will round the given expression
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will round the expression (using midpoint rounding)</returns>
        [NotNull]
        public static IExpression Round(object expression) => new QueryCompoundExpression("ROUND()", expression);

        /// <summary>
        /// Creates a function that will round the given expression to the number of digits indicated
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <param name="digits">The number of digits to round to</param>
        /// <returns>A function that will round the expression (using midpoint rounding)</returns>
        [NotNull]
        public static IExpression Round(object expression, int digits) => new QueryCompoundExpression("ROUND()", expression, digits);

        /// <summary>
        /// Creates a function that removes whitespace from the end of a string
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that removes whitespace from the end of a string</returns>
        [NotNull]
        public static IExpression Rtrim(object expression) => new QueryCompoundExpression("RTRIM()", expression);

        /// <summary>
        /// Creates a function that returns the sign (positive, negative, or neither) of
        /// the expression in question
        /// </summary>
        /// <param name="expression">The numeric expression to evaluate</param>
        /// <returns>A function that returns the sign of the expression in question</returns>
        [NotNull]
        public static IExpression Sign(object expression) => new QueryCompoundExpression("SIGN()", expression);

        /// <summary>
        /// Creates a function that returns the sin of the expression in question
        /// </summary>
        /// <param name="expression">The numeric expression to evaluate</param>
        /// <returns>A function that returns the sin of the expression in question</returns>
        [NotNull]
        public static IExpression Sin(object expression) => new QueryCompoundExpression("SIN()", expression);

        /// <summary>
        /// Creates a function that returns the square root of the expression in question
        /// </summary>
        /// <param name="expression">The numeric expression to evaluate</param>
        /// <returns>A function that returns the square root of the expression in question</returns>
        [NotNull]
        public static IExpression Sqrt(object expression) => new QueryCompoundExpression("SQRT()", expression);

        /// <summary>
        /// Creates a function that will calculate the sum of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the sum</returns>
        [NotNull]
        public static IExpression Sum(object expression) => new QueryCompoundExpression("SUM()", expression);

        /// <summary>
        /// Creates a function that returns the tangent of the expression in question
        /// </summary>
        /// <param name="expression">The numeric expression to evaluate</param>
        /// <returns>A function that returns the tangent of the expression in question</returns>
        [NotNull]
        public static IExpression Tan(object expression) => new QueryCompoundExpression("TAN()", expression);

        /// <summary>
        /// Creates a function that removes whitespace from the start and end of a string
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that removes whitespace from the start and end of a string</returns>
        [NotNull]
        public static IExpression Trim(object expression) => new QueryCompoundExpression("TRIM()", expression);

        /// <summary>
        /// Creates a function that will truncate the given expression (i.e remove all the
        /// digits after the decimal place)
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will truncate the expressoin</returns>
        [NotNull]
        public static IExpression Trunc(object expression) => new QueryCompoundExpression("TRUNC()", expression);

        /// <summary>
        /// Creates a function that will truncate the given expression to the number of digits indicated
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating 
        /// the result</param>
        /// <param name="digits">The number of digits to truncate to</param>
        /// <returns>A function that will truncate the expression</returns>
        [NotNull]
        public static IExpression Trunc(object expression, int digits) => new QueryCompoundExpression("TRUNC()", expression, digits);

        /// <summary>
        /// Creates a function that converts a string to upper case
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that converts a string to upper case</returns>
        [NotNull]
        public static IExpression Upper(object expression) => new QueryCompoundExpression("UPPER()", expression);

        #endregion
    }
}
