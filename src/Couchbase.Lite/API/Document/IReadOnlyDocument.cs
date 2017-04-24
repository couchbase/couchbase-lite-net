using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    public interface IReadOnlyDocument : IReadOnlyDictionary, IDisposable
    {
        string Id { get; }

        ulong Sequence { get; }

        bool IsDeleted { get; }
    }
}
