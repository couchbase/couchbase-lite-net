using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.Lite.Query
{
    internal sealed class QueryCompoundExpression : QueryExpression
    {
        private readonly string _operation;
        private readonly object[] _subpredicates;

        public QueryCompoundExpression(string op, params object[] subpredicates)
        {
            _operation = op;
            _subpredicates = subpredicates;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(ConvertToJSON());
        }

        protected override object ToJSON()
        {
            var obj = new List<object> { _operation };
            foreach (var subp in _subpredicates) {
                var queryExp = subp as QueryExpression;
                obj.Add(queryExp?.ConvertToJSON() ?? subp.ToString());
            }

            return obj;
        }
    }
}
