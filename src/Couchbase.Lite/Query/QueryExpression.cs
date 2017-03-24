using System;
using System.Collections;
using System.Collections.Generic;

namespace Couchbase.Lite.Query
{
    internal abstract class QueryExpression : IExpression
    {
        private object _serialized;

        internal object ConvertToJSON()
        {
            return _serialized ?? (_serialized = ToJSON());
        }

        public static object EncodeToJSON(IList expressions)
        {
            return EncodeExpressions(expressions, false);
        }

        protected abstract object ToJSON();

        private static IList EncodeExpressions(IList expressions, bool aggregate)
        {
            var result = new List<object>();
            foreach (var r in expressions) {
                var jsonObj = default(object);
                var arr = r as IList;
                if (arr != null) {
                    jsonObj = arr;
                } else {
                    var expr = default(QueryExpression);
                    var str = r as string;
                    if (str != null) {
                        expr = new QueryTypeExpression(str);
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
            var rhs = expression as QueryTypeExpression;
            if (rhs == null) {
                if (expression is QueryExpression) {
                    throw new ArgumentException("Invalid expression type");
                }

                rhs = new QueryTypeExpression {
                    ConstantValue = expression
                };
            }

            return new QueryBinaryExpression(lhs, rhs, type);
        }

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

        public IExpression InExpressions(IList expressions)
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

        public IExpression IsNull()
        {
            return GetOperator(BinaryOpType.Is, null);
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
            return ExpressionFactory.Negated(Between(expression1, expression2));
        }

        public IExpression NotEqualTo(object expression)
        {
            return GetOperator(BinaryOpType.NotEqualTo, expression);
        }

        public IExpression NotInExpressions(IList expressions)
        {
            return ExpressionFactory.Negated(InExpressions(expressions));
        }

        public IExpression NotGreaterThan(object expression)
        {
            return LessThanOrEqualTo(expression);
        }

        public IExpression NotGreaterThanOrEqualTo(object expression)
        {
            return LessThan(expression);
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
            return ExpressionFactory.Negated(Like(expression));
        }

        public IExpression NotMatch(object expression)
        {
            return ExpressionFactory.Negated(Match(expression));
        }

        public IExpression NotNull()
        {
            return GetOperator(BinaryOpType.IsNot, null);
        }

        public IExpression NotRegex(object expression)
        {
            return ExpressionFactory.Negated(Regex(expression));
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
    }
}
