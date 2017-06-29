using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    public interface IHavingRouter
    {
        IHaving Having(IExpression expression);
    }
}
