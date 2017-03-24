using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.Lite.Query
{
    internal enum BinaryOpType
    {
        LessThan,
        LessThanOrEqualTo,
        GreaterThan,
        GreaterThanOrEqualTo,
        EqualTo,
        NotEqualTo,
        Is,
        IsNot,
        Matches,
        Like,
        Between,
        RegexLike,
        In,
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulus
    }

    internal sealed class QueryBinaryExpression : QueryExpression
    {
        private readonly BinaryOpType _type;
        private readonly QueryExpression _lhs;
        private readonly QueryExpression _rhs;

        public QueryBinaryExpression(QueryExpression lhs, QueryExpression rhs, BinaryOpType type)
        {
            _lhs = lhs;
            _rhs = rhs;
            _type = type;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(ConvertToJSON());
        }

        protected override object ToJSON()
        {
            var obj = new List<object>();
            switch (_type) {
                case BinaryOpType.Add:
                    obj.Add("+");
                    break;
                case BinaryOpType.Between:
                    obj.Add("BETWEEN");
                    break;
                case BinaryOpType.Divide:
                    obj.Add("/");
                    break;
                case BinaryOpType.EqualTo:
                    obj.Add("=");
                    break;
                case BinaryOpType.GreaterThan:
                    obj.Add(">");
                    break;
                case BinaryOpType.GreaterThanOrEqualTo:
                    obj.Add(">=");
                    break;
                case BinaryOpType.In:
                    obj.Add("IN");
                    break;
                case BinaryOpType.Is:
                    obj.Add("IS");
                    break;
                case BinaryOpType.IsNot:
                    obj.Add("IS NOT");
                    break;
                case BinaryOpType.LessThan:
                    obj.Add("<");
                    break;
                case BinaryOpType.LessThanOrEqualTo:
                    obj.Add("<=");
                    break;
                case BinaryOpType.Like:
                    obj.Add("LIKE");
                    break;
                case BinaryOpType.Matches:
                    obj.Add("MATCH");
                    break;
                case BinaryOpType.Modulus:
                    obj.Add("%");
                    break;
                case BinaryOpType.Multiply:
                    obj.Add("*");
                    break;
                case BinaryOpType.NotEqualTo:
                    obj.Add("!=");
                    break;
                case BinaryOpType.RegexLike:
                    obj.Add("regexp_like()");
                    break;
                case BinaryOpType.Subtract:
                    obj.Add("-");
                    break;
            }

            obj.Add(_lhs.ConvertToJSON());
            if ((_rhs as QueryTypeExpression)?.ExpressionType == ExpressionType.Aggregate) {
                obj.AddRange(_rhs.ConvertToJSON() as IList<object>);
            } else {
                obj.Add(_rhs.ConvertToJSON());
            }
            return obj;
        }
    }
}
