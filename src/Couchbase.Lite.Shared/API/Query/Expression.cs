// 
// Expression.cs
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

using System;
using System.Collections;
using System.Collections.Generic;

using Couchbase.Lite.Internal.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A factory for unary IExpression operators
    /// </summary>
    public static class Expression
    {
        #region Public Methods

        /// <summary>
        /// Returns an expression to represent '*' in things like COUNT(*) and
        /// SELECT *
        /// </summary>
        /// <returns>The expression representing '*'</returns>
        [NotNull]
        public static IPropertyExpression All() => new QueryTypeExpression("", ExpressionType.KeyPath);

        [NotNull]
        public static IExpression Array(IList value) => new QueryCollectionExpression(value);

        /// <summary>
        /// Returns an expression to represent a fixed <see cref="bool"/> value
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <returns>An expression representing the fixed value</returns>
        [NotNull]
        public static IExpression Boolean(bool value) => new QueryConstantExpression<bool>(value);

        /// <summary>
        /// Returns an expression to represent a fixed <see cref="DateTimeOffset"/> value
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <returns>An expression representing the fixed value</returns>
        [NotNull]
        public static IExpression Date(DateTimeOffset value) => new QueryConstantExpression<string>(value.ToString("o")); //#1052 workaround

        [NotNull]
        public static IExpression Dictionary(IDictionary<string, object> value) => new QueryCollectionExpression(value);

        /// <summary>
        /// Returns an expression to represent a fixed <see cref="double"/> value
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <returns>An expression representing the fixed value</returns>
        [NotNull]
        public static IExpression Double(double value) => new QueryConstantExpression<double>(value);

        /// <summary>
        /// Returns an expression to represent a fixed <see cref="Single"/> value
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <returns>An expression representing the fixed value</returns>
        [NotNull]
        public static IExpression Float(float value) => new QueryConstantExpression<float>(value);

        /// <summary>
        /// Returns an expression to represent a fixed <see cref="Int32"/> value
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <returns>An expression representing the fixed value</returns>
        [NotNull]
        public static IExpression Int(int value) => new QueryConstantExpression<int>(value);

        /// <summary>
        /// Returns an expression to represent a fixed <see cref="Int64"/> value
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <returns>An expression representing the fixed value</returns>
        [NotNull]
        public static IExpression Long(long value) => new QueryConstantExpression<long>(value);

        /// <summary>
        /// Returns an expression representing the negated result of an expression
        /// </summary>
        /// <param name="expression">The expression to evaluate</param>
        /// <returns>The negated result of the expression</returns>
        [NotNull]
        public static IExpression Negated(IExpression expression) => new QueryCompoundExpression("NOT", expression);

        /// <summary>
        /// Returns an expression representing the negated result of an expression
        /// </summary>
        /// <param name="expression">The expression to evaluate</param>
        /// <returns>The negated result of the expression</returns>
        [NotNull]
        public static IExpression Not(IExpression expression) => Negated(expression);

        /// <summary>
        /// Gets an expression representing a named parameter (as set in
        /// <see cref="IQuery.Parameters"/>) for use in a query
        /// </summary>
        /// <param name="name">The name of the parameter in the parameter set</param>
        /// <returns>The expression representing the parameter</returns>
        [NotNull]
        public static IExpression Parameter(string name) => new QueryTypeExpression(name, ExpressionType.Parameter);

        /// <summary>
        /// Returns an expression representing the value of a named property
        /// </summary>
        /// <param name="property">The name of the property to fetch</param>
        /// <returns>An expression representing the value of a named property</returns>
        [NotNull]
        public static IPropertyExpression Property(string property) => new QueryTypeExpression(property, ExpressionType.KeyPath);

        /// <summary>
        /// Returns an expression to represent a fixed <see cref="string"/> value
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <returns>An expression representing the fixed value</returns>
        [NotNull]
        public static IExpression String(string value) => new QueryConstantExpression<string>(value);

        /// <summary>
        /// Returns an expression to represent a fixed <see cref="Object"/> value.  It must be one
        /// of the allowed types (i.e. the ones allowed in other methods such as <see cref="String"/>
        /// </summary>
        /// <param name="value">The value to use</param>
        /// <returns>An expression representing the fixed value</returns>
        [NotNull]
        public static IExpression Value(object value) => new QueryConstantExpression<object>(value);

        #endregion
    }
}
