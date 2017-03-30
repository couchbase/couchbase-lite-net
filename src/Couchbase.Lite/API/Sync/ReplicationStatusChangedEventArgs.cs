using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Sync
{
    internal sealed class ReplicationStatusChangedEventArgs : EventArgs
    {
        public ReplicationStatus Status { get; }

        internal ReplicationStatusChangedEventArgs(ReplicationStatus status)
        {
            Status = status;
        }
    }
}
