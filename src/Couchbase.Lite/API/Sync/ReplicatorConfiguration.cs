// 
// ReplicatorConfiguration.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// An enum representing the direction of a <see cref="Replicator"/>
    /// </summary>
    [Flags]
    public enum ReplicatorType
    {
        /// <summary>
        /// The replication will push data from local to remote
        /// </summary>
        Push = 1 << 0,

        /// <summary>
        /// The replication will pull data from remote to local
        /// </summary>
        Pull = 1 << 1,

        /// <summary>
        /// The replication will operate in both directions
        /// </summary>
        PushAndPull = Push | Pull
    }

    /// <summary>
    /// A class representing configuration options for a <see cref="Replicator"/>
    /// </summary>
    public sealed class ReplicatorConfiguration
    {
        /// <summary>
        /// Gets or sets the local database participating in the replication. 
        /// </summary>
        public Database Database { get; }

        /// <summary>
        /// Gets the target to replicate with (either <see cref="Database"/>
        /// or <see cref="Uri"/>
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// A value indicating the direction of the replication.  The default is
        /// <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
        /// </summary>
        public ReplicatorType ReplicatorType { get; set; } = ReplicatorType.PushAndPull;

        /// <summary>
        /// Gets or sets whether or not the <see cref="Replicator"/> should stay
        /// active indefinitely.  The default is <c>false</c>
        /// </summary>
        public bool Continuous { get; set; }

        /// <summary>
        /// Gets or sets the object to use when resolving incoming conflicts.  The default
        /// is <c>null</c> which will set up the default algorithm of the most active revision
        /// </summary>
        public IConflictResolver ConflictResolver { get; set; }

        /// <summary>
        /// Gets or sets extra options affecting replication.
        /// </summary>
        public ReplicatorOptionsDictionary Options { get; set; } = new ReplicatorOptionsDictionary();

        /// <summary>
        /// Gets or sets the class which will authenticate the replication
        /// </summary>
        public Authenticator Authenticator { get; set; }

        internal Database OtherDB { get; }

        internal Uri RemoteUrl { get; }

        /// <summary>
        /// Constructs a configuration between two databases
        /// </summary>
        /// <param name="localDatabase">The local database for replication</param>
        /// <param name="targetDatabase">The target database to use as the endpoint</param>
        public ReplicatorConfiguration(Database localDatabase, Database targetDatabase)
        {
            Database = localDatabase;
            Target = OtherDB = targetDatabase;
        }

        /// <summary>
        /// Constructs a configuration between a database and a remote URL
        /// </summary>
        /// <param name="localDatabase">The local database for replication</param>
        /// <param name="endpoint">The URL to replicate with</param>
        public ReplicatorConfiguration(Database localDatabase, Uri endpoint)
        {
            Database = localDatabase;
            Target = RemoteUrl = endpoint;
        }

        internal static ReplicatorConfiguration Clone(ReplicatorConfiguration source)
        {
            return (ReplicatorConfiguration) source.MemberwiseClone();
        }
    }
}
