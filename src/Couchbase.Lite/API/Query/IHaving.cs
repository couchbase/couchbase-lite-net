using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    public interface IHaving : IQuery, IOrderByRouter, ILimitRouter
    {
    }
}
