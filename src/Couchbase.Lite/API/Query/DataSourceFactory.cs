using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Lite.DB;
using Couchbase.Lite.Querying;

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

        internal static IQueryable<TElement> LinqDataSource<TElement>(IDatabase database, bool prefetch)
            where TElement : class, IDocumentModel, new()
        {
            if (database == null) {
                throw new ArgumentNullException(nameof(database));
            }

            var db = database as Database;
            if (db == null) {
                throw new NotSupportedException("Custom IDatabase not supported");
            }

            return new DatabaseQueryable<TElement>(db, prefetch);
        }
    }
}
