using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Query
{
    public interface IParameters
    {
        void Set(string name, object value);
        void Set(int index, object value);
    }
}
