using System;
using System.Collections.Generic;
using System.Collections;

namespace Couchbase.Lite.Portable
{
    public interface IQueryEnumerator : IEnumerator<IQueryRow>, IEnumerable<IQueryRow>, IDisposable
    {
        bool Equals(object obj);

        int GetHashCode();
        
        global::Couchbase.Lite.Portable.IQueryRow GetRow(int index);
        
        long SequenceNumber { get; }

        bool Stale { get; }

        Int32 Count { get; }
    }
}
