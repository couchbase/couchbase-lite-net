// 
// QueryCompoundExpression.cs
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
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Util;
using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class for creating <see cref="IExpression"/> instances that represent functions
    /// </summary>
    public static partial class Function
    {
        #region Constants

        private const string Tag = nameof(Function);

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a function that will get the absolute value of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the absolute value of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Abs([NotNull]IExpression expression) => 
            new QueryCompoundExpression("ABS()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will get the inverse cosine of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the inverse cosine of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Acos([NotNull]IExpression expression) => 
            new QueryCompoundExpression("ACOS()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will get the inverse sin of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the inverse sin of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Asin([NotNull]IExpression expression) => 
            new QueryCompoundExpression("ASIN()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will get the inverse tangent of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the  inverse tangent of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Atan([NotNull]IExpression expression) => 
            new QueryCompoundExpression("ATAN()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will get the arctangent of the point expressed by
        /// expressions calculating X and Y of the point for the formula
        /// </summary>
        /// <param name="expressionX">An expression or literal to evaluate to get the X coordinate to use</param>
        /// <param name="expressionY">An expression or literal to evaluate to get the Y coordinate to use</param>
        /// <returns>A function that will get the arctangent of the point in question</returns>
        [NotNull]
        [ContractAnnotation("expressionX:null => halt;expressionY:null => halt")]
        public static IExpression Atan2([NotNull]IExpression expressionX, [NotNull]IExpression expressionY) => 
            new QueryCompoundExpression("ATAN2()", 
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expressionX), expressionX), 
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expressionY), expressionY));

        /// <summary>
        /// Creates a function that will calculate the average of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the average</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Avg([NotNull]IExpression expression) => 
            new QueryCompoundExpression("AVG()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will get the ceiling value of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the ceiling value of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Ceil([NotNull]IExpression expression) => 
            new QueryCompoundExpression("CEIL()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will calculate if a given string is inside of another
        /// in question
        /// </summary>
        /// <param name="expression">The string or expression that evaluates to a string to search</param>
        /// <param name="substring">The string or expression that evaluates to a string to search for</param>
        /// <returns>A function that will return true if the string contains the other, or false if it does not</returns>
        [NotNull]
        [ContractAnnotation("expression:null => halt;substring:null => halt")]
        public static IExpression Contains([NotNull]IExpression expression, [NotNull]IExpression substring) => 
            new QueryCompoundExpression("CONTAINS()", 
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression),
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(substring), substring));

        /// <summary>
        /// Creates a function that will get the cosine of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the cosine of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Cos([NotNull]IExpression expression) => 
            new QueryCompoundExpression("COS()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will count the occurrences of 
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the count</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Count([NotNull]IExpression expression) => 
            new QueryCompoundExpression("COUNT()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will convert a numeric expression to degrees from radians
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the value of the expression in question expressed in degrees</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Degrees([NotNull]IExpression expression) => 
            new QueryCompoundExpression("DEGREES()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

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
        [ContractAnnotation("null => halt")]
        public static IExpression Exp([NotNull]IExpression expression) => 
            new QueryCompoundExpression("EXP()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will get the floor value of the expression
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the floor value of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Floor([NotNull]IExpression expression) => 
            new QueryCompoundExpression("FLOOR()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that gets the length of a string
        /// in question
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result (must be or evaluate to a string)</param>
        /// <returns>The length of the string in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Length([NotNull]IExpression expression) => 
            new QueryCompoundExpression("LENGTH()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that gets the natural log of the numerical expression
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that gets the natural log of the expression</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Ln([NotNull]IExpression expression) => 
            new QueryCompoundExpression("LN()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that gets the base 10 log of the numerical expression
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that gets the base 10 log of the expression</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Log([NotNull]IExpression expression) => 
            new QueryCompoundExpression("LOG()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that converts a string to lower case
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that converts a string to lower case</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Lower([NotNull]IExpression expression) => 
            new QueryCompoundExpression("LOWER()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that removes whitespace from the beginning of a string
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that removes whitespace from the beginning of a string</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Ltrim([NotNull]IExpression expression) => 
            new QueryCompoundExpression("LTRIM()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will calculate the max value of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the max value</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Max([NotNull]IExpression expression) => 
            new QueryCompoundExpression("MAX()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will convert a numeric input representing
        /// milliseconds since the Unix epoch into a full ISO8601 date and time
        /// string in the device local time zone.
        /// </summary>
        /// <param name="expression">The expression to take data from when converting</param>
        /// <returns>A function that will convert the timestamp to a string</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression MillisToString([NotNull]IExpression expression) => 
            new QueryCompoundExpression("MILLIS_TO_STR()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will convert a numeric input representing
        /// milliseconds since the Unix epoch into a full ISO8601 date and time
        /// string in UTC time.
        /// </summary>
        /// <param name="expression">The expression to take data from when converting</param>
        /// <returns>A function that will convert the timestamp to a string</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression MillisToUTC([NotNull]IExpression expression) => 
            new QueryCompoundExpression("MILLIS_TO_UTC()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will calculate the min value of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the min value</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Min([NotNull]IExpression expression) => 
            new QueryCompoundExpression("MIN()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

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
        [ContractAnnotation("b:null => halt;exponent:null => halt")]
        public static IExpression Power([NotNull]IExpression b, [NotNull]IExpression exponent) => 
            new QueryCompoundExpression("POWER()",
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(b), b),
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(exponent), exponent));

        /// <summary>
        /// Creates a function that will convert a numeric expression to radians from degrees
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will get the value of the expression in question expressed in radians</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Radians([NotNull]IExpression expression) => 
            new QueryCompoundExpression("RADIANS()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will round the given expression
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will round the expression (using midpoint rounding)</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Round([NotNull]IExpression expression) => 
            new QueryCompoundExpression("ROUND()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will round the given expression to the number of digits indicated
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <param name="digits">The number of digits to round to</param>
        /// <returns>A function that will round the expression (using midpoint rounding)</returns>
        [NotNull]
        [ContractAnnotation("expression:null => halt;digits:null => halt")]
        public static IExpression Round([NotNull]IExpression expression, [NotNull]IExpression digits) => 
            new QueryCompoundExpression("ROUND()",
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression),
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(digits), digits));

        /// <summary>
        /// Creates a function that removes whitespace from the end of a string
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that removes whitespace from the end of a string</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Rtrim([NotNull]IExpression expression) => 
            new QueryCompoundExpression("RTRIM()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that returns the sign (positive, negative, or neither) of
        /// the expression in question
        /// </summary>
        /// <param name="expression">The numeric expression to evaluate</param>
        /// <returns>A function that returns the sign of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Sign([NotNull]IExpression expression) => 
            new QueryCompoundExpression("SIGN()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that returns the sin of the expression in question
        /// </summary>
        /// <param name="expression">The numeric expression to evaluate</param>
        /// <returns>A function that returns the sin of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Sin([NotNull]IExpression expression) => 
            new QueryCompoundExpression("SIN()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that returns the square root of the expression in question
        /// </summary>
        /// <param name="expression">The numeric expression to evaluate</param>
        /// <returns>A function that returns the square root of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Sqrt([NotNull]IExpression expression) => 
            new QueryCompoundExpression("SQRT()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will convert an ISO8601 datetime string
        /// into the number of milliseconds since the unix epoch.
        /// </summary>
        /// <param name="expression">The expression to take data from when converting</param>
        /// <returns>A function that will convert the string to a timestamp</returns>
        /// <remarks>
        /// Valid date strings must start with a date in the form YYYY-MM-DD (time
        /// only strings are not supported).
        ///
        /// Times can be of the form HH:MM, HH:MM:SS, or HH:MM:SS.FFF.  Leading zero is
        /// not optional (i.e. 02 is ok, 2 is not).  Hours are in 24-hour format.  FFF
        /// represents milliseconds, and *trailing* zeros are optional (i.e. 5 == 500).
        ///
        /// Time zones can be in one of three forms:
        /// (+/-)HH:MM
        /// (+/-)HHMM
        /// Z (which represents UTC)
        ///
        /// No time zone present will default to the device local time zone
        /// </remarks>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression StringToMillis([NotNull]IExpression expression) => 
            new QueryCompoundExpression("STR_TO_MILLIS()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will convert an ISO8601 datetime string
        /// into a full ISO8601 UTC datetime string.
        /// </summary>
        /// <param name="expression">The expression to take data from when converting</param>
        /// <returns>A function that will convert the string to a timestamp</returns>
        /// <remarks>
        /// Valid date strings must start with a date in the form YYYY-MM-DD (time
        /// only strings are not supported).
        ///
        /// Times can be of the form HH:MM, HH:MM:SS, or HH:MM:SS.FFF.  Leading zero is
        /// not optional (i.e. 02 is ok, 2 is not).  Hours are in 24-hour format.  FFF
        /// represents milliseconds, and *trailing* zeros are optional (i.e. 5 == 500).
        ///
        /// Time zones can be in one of three forms:
        /// (+/-)HH:MM
        /// (+/-)HHMM
        /// Z (which represents UTC)
        ///
        /// No time zone present will default to the device local time zone
        /// </remarks>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression StringToUTC([NotNull]IExpression expression) => 
            new QueryCompoundExpression("STR_TO_UTC()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will calculate the sum of the
        /// expression in question across the results in a particular query
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will calculate the sum</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Sum([NotNull]IExpression expression) => 
            new QueryCompoundExpression("SUM()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that returns the tangent of the expression in question
        /// </summary>
        /// <param name="expression">The numeric expression to evaluate</param>
        /// <returns>A function that returns the tangent of the expression in question</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Tan([NotNull]IExpression expression) => 
            new QueryCompoundExpression("TAN()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that removes whitespace from the start and end of a string
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that removes whitespace from the start and end of a string</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Trim([NotNull]IExpression expression) => 
            new QueryCompoundExpression("TRIM()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will truncate the given expression (i.e remove all the
        /// digits after the decimal place)
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that will truncate the expressoin</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Trunc([NotNull]IExpression expression) => 
            new QueryCompoundExpression("TRUNC()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates a function that will truncate the given expression to the number of digits indicated
        /// in question
        /// </summary>
        /// <param name="expression">The numeric expression to take data from when calculating 
        /// the result</param>
        /// <param name="digits">The number of digits to truncate to</param>
        /// <returns>A function that will truncate the expression</returns>
        [NotNull]
        [ContractAnnotation("expression:null => halt;digits:null => halt")]
        public static IExpression Trunc([NotNull]IExpression expression, [NotNull]IExpression digits) => 
            new QueryCompoundExpression("TRUNC()",
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression),
                CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(digits), digits));

        /// <summary>
        /// Creates a function that converts a string to upper case
        /// </summary>
        /// <param name="expression">The expression to take data from when calculating
        /// the result</param>
        /// <returns>A function that converts a string to upper case</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        public static IExpression Upper([NotNull]IExpression expression) => 
            new QueryCompoundExpression("UPPER()", CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        #endregion
    }
}
