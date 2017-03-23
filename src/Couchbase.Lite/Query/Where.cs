using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    internal sealed class Where : XQuery, IWhere
    {
        public Where(XQuery query, IExpression expression)
        {
            Copy(query);
            WhereImpl = expression;
        }

        public IOrderBy OrderBy(params IOrderBy[] orderBy)
        {
            return new OrderBy(this, orderBy);
        }
    }
}
