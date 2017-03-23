using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.Lite.Query
{
    internal enum OperationType
    {
        LessThan,
        LessThanOrEqualTo,
        GreaterThan,
        GreaterThanOrEqualTo,
        EqualTo,
        NotEqualTo,
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
        private readonly OperationType _type;
        private readonly QueryExpression _lhs;
        private readonly QueryExpression _rhs;

        public QueryBinaryExpression(QueryExpression lhs, QueryExpression rhs, OperationType type)
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
                case OperationType.Add:
                    obj.Add("+");
                    break;
                case OperationType.Between:
                    obj.Add("BETWEEN");
                    break;
                case OperationType.Divide:
                    obj.Add("/");
                    break;
                case OperationType.EqualTo:
                    obj.Add("=");
                    break;
                case OperationType.GreaterThan:
                    obj.Add(">");
                    break;
                case OperationType.GreaterThanOrEqualTo:
                    obj.Add(">=");
                    break;
                case OperationType.In:
                    obj.Add("IN");
                    break;
                case OperationType.LessThan:
                    obj.Add("<");
                    break;
                case OperationType.LessThanOrEqualTo:
                    obj.Add("<=");
                    break;
                case OperationType.Like:
                    obj.Add("LIKE");
                    break;
                case OperationType.Matches:
                    obj.Add("MATCH");
                    break;
                case OperationType.Modulus:
                    obj.Add("%");
                    break;
                case OperationType.Multiply:
                    obj.Add("*");
                    break;
                case OperationType.NotEqualTo:
                    obj.Add("!=");
                    break;
                case OperationType.RegexLike:
                    obj.Add("regexp_like()");
                    break;
                case OperationType.Subtract:
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
