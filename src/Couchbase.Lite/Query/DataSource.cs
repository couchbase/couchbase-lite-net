using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    internal abstract class DataSource : IDataSource
    {
        internal object Source { get; }

        protected DataSource(object source)
        {
            Source = source;            
        }
    }
}
