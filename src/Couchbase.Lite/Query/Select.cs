using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    internal sealed class Select : XQuery, ISelect
    {
        private readonly object _select;

        public Select(object select, bool distinct)
        {
            _select = select;
            SelectImpl = this;
            Distinct = distinct;
        }

        public IFrom From(IDataSource dataSource)
        {
            return new From(this, dataSource);
        }

        public object ToJSON()
        {
            if (_select == null) {
                return null;
            }

            return new Dictionary<string, object> {
                ["WHAT"] = _select
            };
        }
    }
}
