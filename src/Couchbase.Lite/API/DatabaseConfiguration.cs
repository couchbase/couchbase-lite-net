using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    public sealed class DatabaseConfiguration
    {
        public string Directory { get; }

        public IEncryptionKey EncryptionKey { get; }

        public IConflictResolver ConflictResolver { get; }

        public DatabaseConfiguration() : this(new Builder())
        {
            
        }

        public DatabaseConfiguration(Builder builder)
        {
            Directory = builder.Directory;
            EncryptionKey = builder.EncryptionKey;
            ConflictResolver = builder.ConflictResolver ?? new MostActiveWinsConflictResolver();
        }

        public class Builder
        {
            public string Directory { get; set; }

            public IEncryptionKey EncryptionKey { get; set; }

            public IConflictResolver ConflictResolver { get; set; }
        }
    }
}
