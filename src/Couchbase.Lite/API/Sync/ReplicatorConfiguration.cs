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
    public sealed partial class ReplicatorConfiguration
    {
        #region Constants

        private const string Tag = nameof(ReplicatorConfiguration);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the class which will authenticate the replication
        /// </summary>
        [CanBeNull]
        public Authenticator Authenticator { get; }

        /// <summary>
        /// A set of Sync Gateway channel names to pull from.  Ignored for push replicatoin.
        /// The default value is null, meaning that all accessible channels will be pulled.
        /// Note: channels that are not accessible to the user will be ignored by Sync Gateway.
        /// </summary>
        [CanBeNull]
        public IList<string> Channels => Options.Channels;

        internal TimeSpan CheckpointInterval => Options.CheckpointInterval;

        /// <summary>
        /// Gets the object to use when resolving incoming conflicts.
        /// </summary>
        [NotNull]
        public IConflictResolver ConflictResolver { get; }

        /// <summary>
        /// Gets whether or not the <see cref="Replicator"/> should stay
        /// active indefinitely.
        /// </summary>
        public bool Continuous { get; }

        /// <summary>
        /// Gets the local database participating in the replication. 
        /// </summary>
        [NotNull]
        public Database Database { get; }

        /// <summary>
        /// Gets the set of document IDs to filter by.  If not null, only documents with these IDs will be pushed
        /// and/or pulled
        /// </summary>
        [CanBeNull]
        public IList<string> DocumentIDs => Options.DocIDs;

        /// <summary>
        /// Extra HTTP headers to send in all requests to the remote target
        /// </summary>
        [NotNull]
        public IDictionary<string, string> Headers => Options.Headers;

        [NotNull]
        internal ReplicatorOptionsDictionary Options { get; }

        [CanBeNull]
        internal Database OtherDB { get; }

        /// <summary>
        /// Gets the certificate to trust.  All other certificates received
        /// by a <see cref="Replicator"/> with this configuration will be rejected.
        /// </summary>
        [CanBeNull]
        public X509Certificate2 PinnedServerCertificate => Options.PinnedServerCertificate;

        [CanBeNull]
        internal Uri RemoteUrl { get; }

        /// <summary>
        /// Gets the value indicating the direction of the replication. 
        /// </summary>
        public ReplicatorType ReplicatorType { get; }

        /// <summary>
        /// Gets the target to replicate with (either <see cref="Database"/>
        /// or <see cref="Uri"/>
        /// </summary>
        [NotNull]
        public IEndpoint Target { get; }

        #endregion

        #region Constructors

        internal ReplicatorConfiguration([NotNull]Builder builder)
        {
            Database = builder.Database;
            Target = builder.Target;
            if (Target is URLEndpoint ne) {
                RemoteUrl = ne.Url;
            } else if (Target is DatabaseEndpoint de) {
                OtherDB = de.Database;
            } else {
                throw new ArgumentException($"Invalid type for target ({Target.GetType().Name})");
            }

            Authenticator = builder.Authenticator;
            ConflictResolver = builder.ConflictResolver;
            Continuous = builder.Continuous;
            ReplicatorType = builder.ReplicatorType;
            Options = builder.Options;
        }

        #endregion
    }

    public sealed partial class ReplicatorConfiguration
    {
        #region Nested

        /// <summary>
        /// The class responsible for building <see cref="ReplicatorConfiguration"/>
        /// </summary>
        public sealed class Builder
        {
            #region Variables

            [NotNull]private IConflictResolver _conflictResolver = new DefaultConflictResolver();

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

            internal TimeSpan CheckpointInterval
            {
                get => Options.CheckpointInterval;
                set => Options.CheckpointInterval = value;
            }

            /// <summary>
            /// Gets or sets the object to use when resolving incoming conflicts.  The default
            /// is <c>null</c> which will set up the default algorithm of the most active revision
            /// </summary>
            [NotNull]
            public IConflictResolver ConflictResolver
            {
                get => _conflictResolver;
                set => _conflictResolver = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(ConflictResolver), value);
            }

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
                set => Options.Headers = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(Headers), value);
            }

            [NotNull]
            internal ReplicatorOptionsDictionary Options { get; set; } = new ReplicatorOptionsDictionary();

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
            public IEndpoint Target { get; }

            #endregion

            #region Constructors

            /// <summary>
            /// Constructs a new builder object with the required properties
            /// </summary>
            /// <param name="database">The database that will serve as the local side of the replication</param>
            /// <param name="target">The endpoint to replicate to, either local or remote</param>
            public Builder([NotNull] Database database, [NotNull] IEndpoint target)
            {
                Database = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(database), database);
                Target = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(target), target);
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Builds a configuration based on the current state of the builder
            /// </summary>
            /// <returns>A new configuration object</returns>
            [NotNull]
            public ReplicatorConfiguration Build() => new ReplicatorConfiguration(this);

            #endregion
        }

        #endregion
    }
}
