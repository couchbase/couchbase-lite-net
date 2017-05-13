using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Sync
{
    [Flags]
    internal enum ReplicationType
    {
        Push = 1 << 0,
        Pull = 1 << 1,
        PushAndPull = Push | Pull
    }

    internal sealed class ReplicatorConfiguration
    {
        public object Source { get; }

        public object Target { get; }

        public ReplicationType ReplicationType { get; }

        public bool Continuous { get; }

        public IConflictResolver ConflictResolver { get; }

        public ReplicatorConfiguration() : this(new Builder())
        {
            
        }

        public ReplicatorConfiguration(Builder builder)
        {
            Source = builder.Source ?? throw new ArgumentNullException(nameof(builder.Source));
            Target = builder.Target ?? throw new ArgumentNullException(nameof(builder.Target));
            ReplicationType = builder.ReplicationType;
            Continuous = builder.Continuous;
            ConflictResolver = builder.ConflictResolver ?? new MostActiveWinsConflictResolver();
        }

        public sealed class Builder
        {
            public object Source { get; set; }

            public object Target { get; set; }

            public ReplicationType ReplicationType { get; set; } = ReplicationType.Push;

            public bool Continuous { get; set; }

            public IConflictResolver ConflictResolver { get; set; }
        }
    }
}
