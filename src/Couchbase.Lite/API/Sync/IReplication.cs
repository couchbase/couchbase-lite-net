using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Sync
{
    internal interface IReplication : IThreadSafe, IDisposable
    {
        event EventHandler<ReplicationStatusChangedEventArgs> StatusChanged;

        event EventHandler<ReplicationStoppedEventArgs> Stopped;

        IDatabase Database { get; }

        Uri RemoteUrl { get; }

        IDatabase OtherDatabase { get; }

        bool Push { get; set; }

        bool Pull { get; set; }

        bool Continuous { get; set; }

        ReplicationStatus Status { get; }

        Exception LastError { get; }

        void Start();

        void Stop();
    }
}
