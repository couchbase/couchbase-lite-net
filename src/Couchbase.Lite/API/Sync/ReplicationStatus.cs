using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Sync
{
    internal struct ReplicationStatus
    {
        public ReplicationActivityLevel Activity { get; }

        public ReplicationProgress Progress { get; }

        internal ReplicationStatus(ReplicationActivityLevel activity, ReplicationProgress progress)
        {
            Activity = activity;
            Progress = progress;
        }
    }
}
