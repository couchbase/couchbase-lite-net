using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite.Internal.Query;

namespace Couchbase.Lite.Query
{
    public static class JoinFactory
    {
        public static IJoinOn Join(IDataSource dataSource)
        {
            return InnerJoin(dataSource);
        }

        public static IJoinOn LeftJoin(IDataSource dataSource)
        {
            return new Join("LEFT OUTER", dataSource);
        }

        public static IJoinOn LeftOuterJoin(IDataSource dataSource)
        {
            return new Join("LEFT OUTER", dataSource);
        }

        public static IJoinOn InnerJoin(IDataSource dataSource)
        {
            return new Join(null, dataSource);
        }

        public static IJoinOn CrossJoin(IDataSource dataSource)
        {
            return new Join("CROSS", dataSource);
        }
    }
}
