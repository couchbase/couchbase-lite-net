using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Sync
{
    internal struct ReplicationProgress
    {
        public ulong Completed { get; }

        public ulong Total { get; }

        internal ReplicationProgress(ulong completed, ulong total)
        {
            Completed = completed;
            Total = total;
        }
    }
}
