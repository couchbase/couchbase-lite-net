// 
//  ReplicatorConfiguration.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

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
        #region Constants

        private const string Tag = nameof(ReplicatorConfiguration);

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the class which will authenticate the replication
        /// </summary>
        [CanBeNull]
        public Authenticator Authenticator { get; set; }

        /// <summary>
        /// A set of Sync Gateway channel names to pull from.  Ignored for push replicatoin.
        /// The default value is null, meaning that all accessible channels will be pulled.
        /// Note: channels that are not accessible to the user will be ignored by Sync Gateway.
        /// </summary>
        [CanBeNull]
        public IList<string> Channels
        {
            get => Options.Channels;
            set => Options.Channels = value;
        }

        /// <summary>
        /// Gets or sets the object to use when resolving incoming conflicts.  The default
        /// is <c>null</c> which will set up the default algorithm of the most active revision
        /// </summary>
        [CanBeNull]
        public IConflictResolver ConflictResolver { get; set; }

        /// <summary>
        /// Gets or sets whether or not the <see cref="Replicator"/> should stay
        /// active indefinitely.  The default is <c>false</c>
        /// </summary>
        public bool Continuous { get; set; }

        /// <summary>
        /// Gets or sets the local database participating in the replication. 
        /// </summary>
        [NotNull]
        public Database Database { get; }

        /// <summary>
        /// A set of document IDs to filter by.  If not null, only documents with these IDs will be pushed
        /// and/or pulled
        /// </summary>
        [CanBeNull]
        public IList<string> DocumentIDs
        {
            get => Options.DocIDs;
            set => Options.DocIDs = value;
        }

        /// <summary>
        /// Extra HTTP headers to send in all requests to the remote target
        /// </summary>
        [NotNull]
        public IDictionary<string, string> Headers
        {
            get => Options.Headers;
            set => Options.Headers = value;
        }

        internal TimeSpan CheckpointInterval
        {
            get => Options.CheckpointInterval;
            set => Options.CheckpointInterval = value;
        }

        [NotNull]
        internal ReplicatorOptionsDictionary Options { get; } = new ReplicatorOptionsDictionary();

        [CanBeNull]
        internal Database OtherDB { get; }

        /// <summary>
        /// Gets or sets a certificate to trust.  All other certificates received
        /// by a <see cref="Replicator"/> with this configuration will be rejected.
        /// </summary>
        [CanBeNull]
        public X509Certificate2 PinnedServerCertificate
        {
            get => Options.PinnedServerCertificate;
            set => Options.PinnedServerCertificate = value;
        }

        [CanBeNull]
        internal Uri RemoteUrl { get; }

        /// <summary>
        /// A value indicating the direction of the replication.  The default is
        /// <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
        /// </summary>
        public ReplicatorType ReplicatorType { get; set; } = ReplicatorType.PushAndPull;

        /// <summary>
        /// Gets the target to replicate with (either <see cref="Database"/>
        /// or <see cref="Uri"/>
        /// </summary>
        [NotNull]
        public object Target { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a configuration between two databases
        /// </summary>
        /// <param name="localDatabase">The local database for replication</param>
        /// <param name="targetDatabase">The target database to use as the endpoint</param>
        public ReplicatorConfiguration([NotNull]Database localDatabase, [NotNull]Database targetDatabase)
        {
            Database = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(localDatabase), localDatabase);
            Target = OtherDB = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(targetDatabase), targetDatabase);
        }

        /// <summary>
        /// Constructs a configuration between a database and a remote URL
        /// </summary>
        /// <param name="localDatabase">The local database for replication</param>
        /// <param name="endpoint">The URL to replicate with</param>
        public ReplicatorConfiguration([NotNull]Database localDatabase, [NotNull]Uri endpoint)
        {
            Database = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(localDatabase), localDatabase);
            Target = RemoteUrl = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(endpoint), endpoint);
        }

        #endregion

        #region Internal Methods

        internal static ReplicatorConfiguration Clone(ReplicatorConfiguration source)
        {
            return (ReplicatorConfiguration) source?.MemberwiseClone();
        }

        #endregion
    }
}
