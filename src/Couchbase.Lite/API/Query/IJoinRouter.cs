using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    public interface IJoinRouter
    {
        IJoin Join(params IJoin[] join);
    }
}
