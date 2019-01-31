// 
// QueryExpression.cs
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

using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryExpression : IExpression
    {
        #region Constants

        protected static readonly string[] MissingValue = { "MISSING" };

        #endregion

        #region Variables

        private object _serialized;

        #endregion

        #region Public Methods

        public static object EncodeToJSON([NotNull]IList expressions)
        {
            return EncodeExpressions(expressions);
        }

        public IExpression Match([NotNull]IExpression expression) => GetOperator(BinaryOpType.Matches, expression);

        public IExpression NotIn([NotNull]params IExpression[] expressions) => Expression.Negated(In(expressions));

        #endregion

        #region Protected Methods

        protected void Reset()
        {
            _serialized = null;
        }

        protected abstract object ToJSON();

        #endregion

        #region Internal Methods

        internal object ConvertToJSON()
        {
            return _serialized ?? (_serialized = ToJSON());
        }

        #endregion

        #region Private Methods

        private static IList EncodeExpressions(IList expressions)
        {
            var result = new List<object>();
            foreach (var r in expressions) {
                object jsonObj;
                var arr = r as IList;
                if (arr != null) {
                    jsonObj = arr;
                } else {
                    QueryExpression expr;
                    var str = r as string;
                    if (str != null) {
                        expr = new QueryTypeExpression(str, ExpressionType.KeyPath);
                    } else {
                        expr = r as QueryExpression;
                        if (expr == null) {
                            throw new InvalidOperationException("Expressions must either be IExpression or string");
                        }
                    }

                    jsonObj = expr.ConvertToJSON();
                }
                
                result.Add(jsonObj);
            }

            return result;
        }

        [NotNull]
        private QueryExpression GetBetweenExpression(IExpression expression)
        {
            switch (expression) {
                case QueryTypeExpression e:
                    return e;
                case QueryConstantExpressionBase e:
                    return e;
                default:
                    throw new ArgumentException(
                        $"Invalid expression value for expression1 of Between ({expression.GetType().Name})");
            }
        }

        [NotNull]
        private QueryExpression GetOperator(BinaryOpType type, IExpression expression)
        {
            var lhs = this;
            var rhs = Misc.TryCast<IExpression, QueryExpression>(expression);

            return new QueryBinaryExpression(lhs, rhs, type);
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return $"[{GetType().Name}] => {JsonConvert.SerializeObject(ConvertToJSON())}";
        }

        #endregion

        #region IExpression

        public IExpression Add(IExpression expression) => GetOperator(BinaryOpType.Add, expression);

        public IExpression And(IExpression expression) => new QueryCompoundExpression("AND", this, expression);

        public IExpression Between(IExpression expression1, IExpression expression2)
        {
            if (!(this is QueryTypeExpression lhs)) {
                throw new NotSupportedException();
            }

            var exp1 = GetBetweenExpression(expression1);
            var exp2 = GetBetweenExpression(expression2);

            var rhs = new QueryTypeExpression(new[] { exp1, exp2 });
            return new QueryBinaryExpression(lhs, rhs, BinaryOpType.Between);
        }

        public IExpression Collate(ICollation collation)
        {
            var col = Misc.TryCast<ICollation, QueryCollation>(collation);
            col = new QueryCollation(col);
            col.SetOperand(this);
            return col;
        }

        public IExpression Divide(IExpression expression) => GetOperator(BinaryOpType.Divide, expression);

        public IExpression EqualTo(IExpression expression) => GetOperator(BinaryOpType.EqualTo, expression);

        public IExpression GreaterThan(IExpression expression) => GetOperator(BinaryOpType.GreaterThan, expression);

        public IExpression GreaterThanOrEqualTo(IExpression expression) => GetOperator(BinaryOpType.GreaterThanOrEqualTo, expression);

        public IExpression In(params IExpression[] expressions)
        {
            if (!(this is QueryTypeExpression lhs)) {
                throw new NotSupportedException();
            }

            var rhs = new QueryTypeExpression(expressions);
            return new QueryBinaryExpression(lhs, rhs, BinaryOpType.In);
        }

        public IExpression Is(IExpression expression) => GetOperator(BinaryOpType.Is, expression);

        public IExpression IsNot(IExpression expression) => GetOperator(BinaryOpType.IsNot, expression);

        public IExpression IsNullOrMissing() => new QueryUnaryExpression(this, UnaryOpType.Null)
            .Or(new QueryUnaryExpression(this, UnaryOpType.Missing));

        public IExpression LessThan(IExpression expression) => GetOperator(BinaryOpType.LessThan, expression);

        public IExpression LessThanOrEqualTo(IExpression expression) => GetOperator(BinaryOpType.LessThanOrEqualTo, expression);

        public IExpression Like(IExpression expression) => GetOperator(BinaryOpType.Like, expression);

        public IExpression Modulo(IExpression expression) => GetOperator(BinaryOpType.Modulus, expression);

        public IExpression Multiply(IExpression expression) => GetOperator(BinaryOpType.Multiply, expression);

        public IExpression NotEqualTo(IExpression expression) => GetOperator(BinaryOpType.NotEqualTo, expression);

        public IExpression NotNullOrMissing() => Expression.Not(IsNullOrMissing());

        public IExpression Or(IExpression expression) => new QueryCompoundExpression("OR", this, expression);

        public IExpression Regex(IExpression expression) => GetOperator(BinaryOpType.RegexLike, expression);

        public IExpression Subtract(IExpression expression) => GetOperator(BinaryOpType.Subtract, expression);

        #endregion
    }
}
