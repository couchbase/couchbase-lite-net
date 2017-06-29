using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    public interface IGroupByRouter
    {
        IGroupBy GroupBy(params IGroupBy[] groupBy);
    }
}
