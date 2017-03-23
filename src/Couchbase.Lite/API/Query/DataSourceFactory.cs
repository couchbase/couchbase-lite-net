using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite.DB;

namespace Couchbase.Lite.Query
{
    public static class DataSourceFactory
    {
        public static IDatabaseSource Database(IDatabase database)
        {
            var db = default(Database);
            if (database != null) {
                db = database as Database;
                if (db == null) {
                    throw new NotSupportedException("Custom IDatabase not supported");
                }
            }

            return new DatabaseSource(db);
        }
    }
}
