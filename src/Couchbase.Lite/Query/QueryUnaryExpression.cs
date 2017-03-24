using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    internal enum UnaryOpType
    {
        Missing,
        NotMissing,
        NotNull,
        Null
    }

    internal sealed class QueryUnaryExpression : QueryExpression
    {
        private readonly object _argument;
        private readonly UnaryOpType _type;

        internal QueryUnaryExpression(object argument, UnaryOpType type)
        {
            _argument = argument;
            _type = type;
        }


        protected override object ToJSON()
        {
            var obj = new List<object>();
            switch (_type) {
                case UnaryOpType.Missing:
                    obj.Add("IS MISSING");
                    break;
                case UnaryOpType.NotMissing:
                    obj.Add("IS NOT MISSING");
                    break;
                case UnaryOpType.NotNull:
                    obj.Add("IS NOT NULL");
                    break;
                case UnaryOpType.Null:
                    obj.Add("IS NULL");
                    break;
            }

            var operand = _argument as QueryExpression ?? new QueryTypeExpression {
                ConstantValue = _argument
            };

            if ((operand as QueryTypeExpression)?.ExpressionType == ExpressionType.Aggregate) {
                obj.AddRange(operand.ConvertToJSON() as IList<object>);
            } else {
                obj.Add(operand.ConvertToJSON());
            }
            return obj;
        }
    }
}
