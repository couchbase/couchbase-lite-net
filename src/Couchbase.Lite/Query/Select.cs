using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    internal sealed class Select : XQuery, ISelect
    {
        private readonly QueryExpression _select;

        public Select(string select, bool distinct)
        {
            if (select != null) {
                _select = new QueryTypeExpression(select);
            }

            SelectImpl = this;
            Distinct = distinct;
        }

        public IFrom From(IDataSource dataSource)
        {
            return new From(this, dataSource);
        }

        public object ToJSON()
        {
            return _select?.ConvertToJSON();
        }
    }
}
