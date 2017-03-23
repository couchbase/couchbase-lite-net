using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.Lite.Query
{
    internal enum ExpressionType
    {
        Constant,
        KeyPath,
        Aggregate
    }

    internal sealed class QueryTypeExpression : QueryExpression
    {
        internal ExpressionType ExpressionType { get; }

        internal string KeyPath { get; }

        private readonly IList _subpredicates;

        internal object ConstantValue { get; set; }

        public QueryTypeExpression()
        {
            ExpressionType = ExpressionType.Constant;
        }

        public QueryTypeExpression(string keyPath)
        {
            ExpressionType = ExpressionType.KeyPath;
            KeyPath = keyPath;
        }

        public QueryTypeExpression(IList subpredicates)
        {
            ExpressionType = ExpressionType.Aggregate;
            _subpredicates = subpredicates;
        }

        private object CalculateKeyPath()
        {
            if (KeyPath.StartsWith("rank(")) {
                return new object[] {"rank()", new[] {".", KeyPath.Substring(5, KeyPath.Length - 6)}};
            }

            return new[] { $".{KeyPath}" };
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(ConvertToJSON());
        }

        protected override object ToJSON()
        {
            switch (ExpressionType) {
                case ExpressionType.Constant:
                    return ConstantValue;
                case ExpressionType.KeyPath:
                    return CalculateKeyPath();
                case ExpressionType.Aggregate:
                {
                    var obj = new List<object>();
                    foreach (var entry in _subpredicates) {
                        var queryExp = entry as QueryExpression;
                        obj.Add(queryExp == null ? entry.ToString() : queryExp.ConvertToJSON());
                    }

                    return obj;
                }
            }

            return null;
        }
    }
}
