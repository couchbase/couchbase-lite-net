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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore.Interop;

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
    /// A set of flags describing the properties of a replicated
    /// document.
    /// </summary>
    [Flags]
    public enum DocumentFlags
    {
        /// <summary>
        /// The replication action represents a deletion of the
        /// document in question
        /// </summary>
        Deleted = 1 << 0,

        /// <summary>
        /// The replication action represents a loss of access from
        /// the server for the document in question (i.e. no more access
        /// granted from the sync function)
        /// </summary>
        AccessRemoved = 1 << 1
    }

    /// <summary>
    /// A class representing configuration options for a <see cref="Replicator"/>
    /// </summary>
    public struct ReplicatorConfiguration
    {
        #region Constants

        private const string Tag = nameof(ReplicatorConfiguration);

        internal const long DefaultHeartbeatInterval = 300;
        internal const long DefaultMaxRetryInterval = 300;
        internal const int MaxRetriesContinuous = int.MaxValue;
        internal const int MaxRetriesOneShot = 9;

        #endregion

        #region Variables

        private C4SocketFactory _socketFactory;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the local database participating in the replication. 
        /// </summary>
        [NotNull]
        public Database Database { get; }

        /// <summary>
        /// Gets the target to replicate with (either <see cref="Database"/>
        /// or <see cref="Uri"/>
        /// </summary>
        [NotNull]
        public IEndpoint Target { get; }

        /// <summary>
        /// Gets the class which will authenticate the replication
        /// </summary>
        [CanBeNull]
        public Authenticator Authenticator { get; }

        /// <summary>
        /// Gets whether or not the <see cref="Replicator"/> should stay
        /// active indefinitely.  The default is <c>false</c>
        /// </summary>
        public bool Continuous { get; }

        /// <summary>
        /// Func delegate that takes Document input parameter and bool output parameter
        /// Document pull will be allowed if output is true, othewise, Document pull 
        /// will not be allowed
        /// </summary>
        [CanBeNull]
        public Func<Document, DocumentFlags, bool> PullFilter { get; }

        /// <summary>
        /// Func delegate that takes Document input parameter and bool output parameter
        /// Document push will be allowed if output is true, othewise, Document push 
        /// will not be allowed
        /// </summary>
        [CanBeNull]
        public Func<Document, DocumentFlags, bool> PushFilter { get; }

        /// <summary>
        /// A value indicating the direction of the replication.  The default is
        /// <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
        /// </summary>
        public ReplicatorType ReplicatorType { get; }

        /// <summary>
        /// The implemented custom conflict resolver object can be registered to the replicator 
        /// at ConflictResolver property. The default value of the conflictResolver is null. 
        /// When the value is null, the default conflict resolution will be applied.
        /// </summary>
        [CanBeNull]
        public IConflictResolver ConflictResolver { get; }

        /// <summary>
        /// A set of Sync Gateway channel names to pull from.  Ignored for push replicatoin.
        /// The default value is null, meaning that all accessible channels will be pulled.
        /// Note: channels that are not accessible to the user will be ignored by Sync Gateway.
        /// </summary>
        [CanBeNull]
        public IList<string> Channels { get => Options.Channels; }

        /// <summary>
        /// A set of document IDs to filter by.  If not null, only documents with these IDs will be pushed
        /// and/or pulled
        /// </summary>
        [CanBeNull]
        public IList<string> DocumentIDs { get => Options.DocIDs; }

        /// <summary>
        /// Extra HTTP headers to send in all requests to the remote target
        /// </summary>
        [CanBeNull]
        public IDictionary<string, string> Headers { get => Options.Headers; }

        /// <summary>
        /// Gets a certificate to trust.  All other certificates received
        /// by a <see cref="Replicator"/> with this configuration will be rejected.
        /// </summary>
        [CanBeNull]
        public X509Certificate2 PinnedServerCertificate { get => Options.PinnedServerCertificate; }

        /// <summary>
        /// Gets the replicator heartbeat keep-alive interval. 
        /// The default is null (5 min interval is applied).
        /// </summary>
        /// <exception cref="ArgumentException"> 
        /// Throw if set the Heartbeat to less or equal to 0 full seconds.
        /// </exception>
        [CanBeNull]
        public TimeSpan? Heartbeat { get => TimeSpan.FromSeconds(Options.Heartbeat); }

        /// <summary>
        /// Max number of retry attempts. The retry attempts will reset
        /// after the replicator is connected to a remote peer. 
        /// The default is <c>0</c> (<c>10</c> for a single shot replicator or 
        /// <see cref="int.MaxValue" /> for a continuous replicator is applied.)
        /// Setting the value to 1 means that the replicator will perform an initial request 
        /// and if there is a transient error occurs, the replicator will stop without retrying.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Throw if set the MaxRetries to a negative value.
        /// </exception>
        public int MaxAttempts { get => Options.MaxAttempts; }

        /// <summary>
        /// Max delay between retries.
        /// The default is null (5 min interval is applied).
        /// </summary>
        /// <exception cref="ArgumentException"> 
        /// Throw if set the MaxRetryWaitTime to less than 0 full seconds.
        /// </exception>
        [CanBeNull]
        public TimeSpan? MaxAttemptWaitTime { get => TimeSpan.FromSeconds(Options.MaxRetryInterval); }

#if COUCHBASE_ENTERPRISE
        /// <summary>
        /// Get the way that the replicator will validate TLS certificates.  This
        /// property will be overriden if the <see cref="PinnedServerCertificate"/> property
        /// is set.
        /// </summary>
        public bool AcceptOnlySelfSignedServerCertificate { get => Options.AcceptOnlySelfSignedServerCertificate; }
#endif

        // Sets the interval between auto checkpoint saves
        internal TimeSpan CheckpointInterval
        {
            get => Options.CheckpointInterval;
            set => Options.CheckpointInterval = value;
        }

        [NotNull]
        internal ReplicatorOptionsDictionary Options { get; }

        [CanBeNull]
        internal Database OtherDB { get; set; }

        [CanBeNull]
        internal Uri RemoteUrl { get; set; }

        internal C4SocketFactory SocketFactory
        {
            get => _socketFactory.open != IntPtr.Zero ? _socketFactory : LiteCore.Interop.SocketFactory.InternalFactory;
            set => _socketFactory = value;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new builder object with the required properties
        /// </summary>
        /// <param name="database">The database that will serve as the local side of the replication</param>
        /// <param name="target">The endpoint to replicate to, either local or remote</param>
        /// <exception cref="ArgumentException">Thrown if an unsupported <see cref="IEndpoint"/> implementation
        /// is provided as <paramref name="target"/></exception>
        public ReplicatorConfiguration([NotNull] Database database, [NotNull] IEndpoint target, Authenticator authenticator = null, 
            bool continuous = false, ReplicatorType replicatorType = ReplicatorType.PushAndPull, IConflictResolver conflictResolver = null,
            Func<Document, DocumentFlags, bool> pullFilter = null, Func<Document, DocumentFlags, bool> pushFilter = null,
            IList<string> channels = null, IList<string> documentIDs = null, IDictionary<string, string> headers = null,
            X509Certificate2 pinnedServerCertificate = null, TimeSpan? heartbeat = null, int maxAttempts = 0, TimeSpan? maxAttemptsWaitTime = null
#if COUCHBASE_ENTERPRISE
            , bool acceptOnlySelfSignedServerCertificate = false
#endif
            ) : this()
        {
            Database = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(database), database);
            Target = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(target), target);

            Options = new ReplicatorOptionsDictionary();

            Authenticator = authenticator;
            Continuous = continuous;
            PullFilter = pullFilter;
            PushFilter = pushFilter;
            ReplicatorType = replicatorType;
            ConflictResolver = conflictResolver;

            // Replicator Options
            Options.Channels = channels;
            Options.DocIDs = documentIDs;
            Options.Headers = headers;
            Options.PinnedServerCertificate = pinnedServerCertificate;
            long sec = heartbeat == null ? DefaultHeartbeatInterval : heartbeat.Value.Ticks / TimeSpan.TicksPerSecond;
            Options.Heartbeat = sec > 0 ? sec : throw new ArgumentException(CouchbaseLiteErrorMessage.InvalidHeartbeatInterval);
            Options.MaxAttempts = maxAttempts < 0 ? throw new ArgumentException(CouchbaseLiteErrorMessage.InvalidMaxRetries)
                : maxAttempts == 0 ? continuous ? MaxRetriesContinuous : MaxRetriesOneShot : maxAttempts;
            sec = maxAttemptsWaitTime == null ? DefaultMaxRetryInterval : maxAttemptsWaitTime.Value.Ticks / TimeSpan.TicksPerSecond;
            Options.MaxRetryInterval = sec > 0 ? sec : throw new ArgumentException(CouchbaseLiteErrorMessage.InvalidMaxRetryInterval);
#if COUCHBASE_ENTERPRISE
            Options.AcceptOnlySelfSignedServerCertificate = acceptOnlySelfSignedServerCertificate;
#endif

            var castTarget = Misc.TryCast<IEndpoint, IEndpointInternal>(target);
            castTarget.Visit(ref this);
        }

        #endregion
    }
}
