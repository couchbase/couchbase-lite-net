using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    public interface IJoin : IQuery, IWhereRouter, IOrderByRouter, ILimitRouter
    {
    }

    public interface IJoinOn : IJoin
    {
        IJoin On(IExpression expression);
    }
}
