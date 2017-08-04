// 
// QueryExpression.cs
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
using System;
using System.Collections;
using System.Collections.Generic;

using Couchbase.Lite.Query;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryExpression : IExpression
    {
        #region Constants

        private static readonly string[] MissingValue = new[] {"MISSING"};

        #endregion

        #region Variables

        private object _serialized;

        #endregion

        #region Public Methods

        public static object EncodeToJSON(IList expressions)
        {
            return EncodeExpressions(expressions);
        }

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

        private QueryExpression GetOperator(BinaryOpType type, object expression)
        {
            var lhs = this;
            var rhs = expression as QueryExpression;
            if (rhs == null) {
                rhs = new QueryTypeExpression {
                    ConstantValue = expression
                };
            }

            return new QueryBinaryExpression(lhs, rhs, type);
        }

        #endregion

        #region IExpression

        public IExpression Add(object expression)
        {
            return GetOperator(BinaryOpType.Add, expression);
        }

        public IExpression And(object expression)
        {
            return new QueryCompoundExpression("AND", this, expression);
        }

        public IExpression Between(object expression1, object expression2)
        {
            var lhs = this as QueryTypeExpression;
            if (lhs == null) {
                throw new NotSupportedException();
            }

            var exp1 = expression1 as QueryTypeExpression;
            if (exp1 == null) {
                if (expression1 is QueryExpression) {
                    throw new ArgumentException("Invalid expression value");
                }

                exp1 = new QueryTypeExpression {
                    ConstantValue = expression1
                };
            }

            var exp2 = expression2 as QueryTypeExpression;
            if (exp2 == null) {
                if (expression2 is QueryExpression) {
                    throw new ArgumentException("Invalid expression value");
                }

                exp2 = new QueryTypeExpression {
                    ConstantValue = expression2
                };
            }

            var rhs = new QueryTypeExpression(new[] { exp1, exp2 });
            return new QueryBinaryExpression(lhs, rhs, BinaryOpType.Between);
        }

        public IExpression Collate(ICollation collation)
        {
            if (!(collation is QueryCollation col)) {
                throw new NotSupportedException("Custom ICollation not supported");
            }

            col.SetOperand(this);
            return col;
        }

        public IExpression Concat(object expression)
        {
            throw new NotSupportedException();
        }

        public IExpression Divide(object expression)
        {
            return GetOperator(BinaryOpType.Divide, expression);
        }

        public IExpression EqualTo(object expression)
        {
            return GetOperator(BinaryOpType.EqualTo, expression);
        }

        public IExpression GreaterThan(object expression)
        {
            return GetOperator(BinaryOpType.GreaterThan, expression);
        }

        public IExpression GreaterThanOrEqualTo(object expression)
        {
            return GetOperator(BinaryOpType.GreaterThanOrEqualTo, expression);
        }

        public IExpression In(params object[] expressions)
        {
            var lhs = this as QueryTypeExpression;
            if (lhs == null) {
                throw new NotSupportedException();
            }

            var rhs = new QueryTypeExpression(expressions);
            return new QueryBinaryExpression(lhs, rhs, BinaryOpType.In);
        }

        public IExpression Is(object expression)
        {
            return EqualTo(expression);
        }

        public IExpression IsNot(object expression)
        {
            return NotEqualTo(expression);
        }

        public IExpression IsNullOrMissing()
        {
            return new QueryUnaryExpression(this, UnaryOpType.Null)
                .Or(new QueryUnaryExpression(this, UnaryOpType.Missing));
        }

        public IExpression LessThan(object expression)
        {
            return GetOperator(BinaryOpType.LessThan, expression);
        }

        public IExpression LessThanOrEqualTo(object expression)
        {
            return GetOperator(BinaryOpType.LessThanOrEqualTo, expression);
        }

        public IExpression Like(object expression)
        {
            return GetOperator(BinaryOpType.Like, expression);
        }

        public IExpression Match(object expression)
        {
            return GetOperator(BinaryOpType.Matches, expression);
        }

        public IExpression Modulo(object expression)
        {
            return GetOperator(BinaryOpType.Modulus, expression);
        }

        public IExpression Multiply(object expression)
        {
            return GetOperator(BinaryOpType.Multiply, expression);
        }

        public IExpression NotBetween(object expression1, object expression2)
        {
            return Expression.Negated(Between(expression1, expression2));
        }

        public IExpression NotEqualTo(object expression)
        {
            return GetOperator(BinaryOpType.NotEqualTo, expression);
        }

        public IExpression NotGreaterThan(object expression)
        {
            return LessThanOrEqualTo(expression);
        }

        public IExpression NotGreaterThanOrEqualTo(object expression)
        {
            return LessThan(expression);
        }

        public IExpression NotIn(params object[] expressions)
        {
            return Expression.Negated(In(expressions));
        }

        public IExpression NotLessThan(object expression)
        {
            return GreaterThanOrEqualTo(expression);
        }

        public IExpression NotLessThanOrEqualTo(object expression)
        {
            return GreaterThan(expression);
        }

        public IExpression NotLike(object expression)
        {
            return Expression.Negated(Like(expression));
        }

        public IExpression NotMatch(object expression)
        {
            return Expression.Negated(Match(expression));
        }

        public IExpression NotNullOrMissing()
        {
            return IsNot(IsNullOrMissing());
        }

        public IExpression NotRegex(object expression)
        {
            return Expression.Negated(Regex(expression));
        }

        public IExpression Or(object expression)
        {
            return new QueryCompoundExpression("OR", this, expression);
        }

        public IExpression Regex(object expression)
        {
            return GetOperator(BinaryOpType.RegexLike, expression);
        }

        public IExpression Subtract(object expression)
        {
            return GetOperator(BinaryOpType.Subtract, expression);
        }

        #endregion
    }
}
