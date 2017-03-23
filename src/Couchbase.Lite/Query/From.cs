using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    internal sealed class From : XQuery, IFrom
    {
        public From(XQuery query, IDataSource impl)
        {
            Copy(query);

            FromImpl = impl;
            Database = (impl as DatabaseSource)?.Database;
        }

        public IWhere Where(IExpression expression)
        {
            return new Where(this, expression);
        }

        public IOrderBy OrderBy(params IOrderBy[] orderBy)
        {
            return new OrderBy(this, orderBy);
        }
    }
}
