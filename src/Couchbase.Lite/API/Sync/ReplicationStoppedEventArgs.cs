using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Sync
{
    internal sealed class ReplicationStoppedEventArgs : EventArgs
    {
        public Exception Error { get; }

        internal ReplicationStoppedEventArgs(Exception error)
        {
            Error = error;
        }
    }
}
