using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite.DB;

namespace Couchbase.Lite.Query
{
    internal sealed class DatabaseSource : DataSource, IDatabaseSource
    {
        internal Database Database
        {
            get { return Source as Database; }
        }

        public DatabaseSource(Database database) : base(database)
        {
            
        }

        public IDataSource As(string alias)
        {
            return this;
        }
    }
}
