// 
//  ReplicatorConfiguration.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
using Couchbase.Lite.Support;
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

        #region Variables

        [NotNull]private readonly Freezer _freezer = new Freezer();
        private Authenticator _authenticator;
        [NotNull]private IConflictResolver _conflictResolver = new DefaultConflictResolver();
        private bool _continuous;
        private ReplicatorType _replicatorType = ReplicatorType.PushAndPull;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the class which will authenticate the replication
        /// </summary>
        [CanBeNull]
        public Authenticator Authenticator
        {
            get => _authenticator;
            set => _freezer.SetValue(ref _authenticator, value);
        }

        /// <summary>
        /// A set of Sync Gateway channel names to pull from.  Ignored for push replicatoin.
        /// The default value is null, meaning that all accessible channels will be pulled.
        /// Note: channels that are not accessible to the user will be ignored by Sync Gateway.
        /// </summary>
        [CanBeNull]
        public IList<string> Channels
        {
            get => Options.Channels;
            set => _freezer.PerformAction(() => Options.Channels = value);
        }

        internal TimeSpan CheckpointInterval
        {
            get => Options.CheckpointInterval;
            set => _freezer.PerformAction(() => Options.CheckpointInterval = value);
        }

        /// <summary>
        /// Gets or sets the object to use when resolving incoming conflicts.  The default
        /// is <c>null</c> which will set up the default algorithm of the most active revision
        /// </summary>
        [NotNull]
        public IConflictResolver ConflictResolver
        {
            get => _conflictResolver;
            set => _freezer.SetValue(ref _conflictResolver,
                CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(ConflictResolver), value));
        }

        /// <summary>
        /// Gets or sets whether or not the <see cref="Replicator"/> should stay
        /// active indefinitely.  The default is <c>false</c>
        /// </summary>
        public bool Continuous
        {
            get => _continuous;
            set => _freezer.SetValue(ref _continuous, value);
        }

        /// <summary>
        /// Gets the local database participating in the replication. 
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
            set => _freezer.PerformAction(() =>  Options.DocIDs = value);
        }

        /// <summary>
        /// Extra HTTP headers to send in all requests to the remote target
        /// </summary>
        [NotNull]
        public IDictionary<string, string> Headers
        {
            get => Options.Headers;
            set => _freezer.PerformAction(() => Options.Headers = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(Headers), value));
        }

        [NotNull]
        internal ReplicatorOptionsDictionary Options { get; set; } = new ReplicatorOptionsDictionary();

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
            set => _freezer.PerformAction(() => Options.PinnedServerCertificate = value);
        }

        [CanBeNull]
        internal Uri RemoteUrl { get; }

        /// <summary>
        /// A value indicating the direction of the replication.  The default is
        /// <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
        /// </summary>
        public ReplicatorType ReplicatorType
        {
            get => _replicatorType;
            set => _freezer.SetValue(ref _replicatorType, value);
        }

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
        /// <exception cref="ArgumentException">Thrown if an unsupported <see cref="IEndpoint"/> implementation
        /// is provided as <paramref name="target"/></exception>
        public ReplicatorConfiguration([NotNull] Database database, [NotNull] IEndpoint target)
        {
            Database = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(database), database);
            Target = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(target), target);
            if (Target is URLEndpoint ne) {
                RemoteUrl = ne.Url;
            } else if (Target is DatabaseEndpoint de) {
                OtherDB = de.Database;
            } else {
                throw new ArgumentException($"Invalid type for target ({Target.GetType().Name})");
            }
        }

        #endregion

        #region Internal Methods

        [NotNull]
        internal ReplicatorConfiguration Freeze()
        {
            var retVal = new ReplicatorConfiguration(Database, Target)
            {
                Authenticator = Authenticator,
                ConflictResolver = ConflictResolver,
                Continuous = Continuous,
                ReplicatorType = ReplicatorType,
                Options = Options
            };

            retVal._freezer.Freeze("Cannot modify a ReplicatorConfiguration that is in use");
            return retVal;
        }

        #endregion
    }
}
