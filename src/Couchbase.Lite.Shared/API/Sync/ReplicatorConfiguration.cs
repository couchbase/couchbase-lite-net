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
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Lite.Info;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

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
    public sealed partial class ReplicatorConfiguration
    {
        #region Constants

        private const string Tag = nameof(ReplicatorConfiguration);

        #endregion

        #region Variables

        private readonly Freezer _freezer = new Freezer();
        private Authenticator? _authenticator;
        private bool _continuous = Constants.DefaultReplicatorContinuous;
        private Database? _otherDb;
        private Uri? _remoteUrl;
        private ReplicatorType _replicatorType = Constants.DefaultReplicatorType;
        private C4SocketFactory _socketFactory;
        private bool _isDefaultMaxAttemptSet = true;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether or not a cookie can be set on a parent domain
        /// of the host that issued it (i.e. foo.bar.com can set a cookie for all
        /// of bar.com).  This is only recommended if the host issuing the cookie
        /// is well trusted.
        /// </summary>
        public bool AcceptParentDomainCookies
        {
            get => Options.AcceptParentDomainCookies;
            set => _freezer.PerformAction(() => Options.AcceptParentDomainCookies = value);
        }

        /// <summary>
        /// Gets or sets the class which will authenticate the replication
        /// </summary>
        public Authenticator? Authenticator
        {
            get => _authenticator;
            set => _freezer.SetValue(ref _authenticator, value);
        }

        /// <summary>
        /// [DEPRECATED] A set of Sync Gateway channel names to pull from.  Ignored for push replicatoin.
        /// The default value is null, meaning that all accessible channels will be pulled.
        /// Note: channels that are not accessible to the user will be ignored by Sync Gateway.
        /// </summary>
        /// <remarks>
        /// Note: Channels property is only applicable in the replications with Sync Gateway.
        /// </remarks>
        [Obsolete("Channels is deprecated, please use CollectionConfiguration.Channels")]
        public IList<string>? Channels
        {
            get => DefaultCollectionConfig.Options.Channels;
            set {
                _freezer.PerformAction(() =>
                {
                    DefaultCollectionConfig.Options.Channels = value;
                    Options.Channels = value;
                });
            }
        }

        /// <summary>
        /// Gets or sets whether or not the <see cref="Replicator"/> should stay
        /// active indefinitely.  The default is <c>false</c>
        /// Default value is <see cref="Constants.DefaultReplicatorContinuous" />
        /// </summary>
        public bool Continuous
        {
            get => _continuous;
            set
            {
                _freezer.SetValue(ref _continuous, value);
                if (_isDefaultMaxAttemptSet)
                    MaxAttempts = 0;
            }
        }

        /// <summary>
        /// [DEPRECATED] Gets the local database participating in the replication. 
        /// </summary>
        /// <exception cref="CouchbaseLiteException">Thrown if Database doesn't exist in the replicator configuration.</exception>
        [Obsolete("Database is deprecated, please use Collections")]
        public Database Database => Collections.Count > 0 && Collections[0].Database != null ? Collections[0].Database
            : throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter, "Cannot operate on a missing Database in the Replication Configuration.");

        /// <summary>
        /// [DEPRECATED] A set of document IDs to filter by.  If not null, only documents with these IDs will be pushed
        /// and/or pulled
        /// </summary>
        [Obsolete("DocumentIDs is deprecated, please use CollectionConfiguration.DocumentIDs")]
        public IList<string>? DocumentIDs
        {
            get => DefaultCollectionConfig.Options.DocIDs;
            set {
                _freezer.PerformAction(() =>
                {
                    DefaultCollectionConfig.Options.DocIDs = value;
                    Options.DocIDs = value;
                });
            }
        }

        /// <summary>
        /// Extra HTTP headers to send in all requests to the remote target
        /// </summary>
        public IDictionary<string, string?> Headers
        {
            get => Options.Headers;
            set => _freezer.PerformAction(() => Options.Headers = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(Headers), value));
        }

        /// <summary>
        /// Gets or sets a certificate to trust.  All other certificates received
        /// by a <see cref="Replicator"/> with this configuration will be rejected.
        /// </summary>
        /// <remarks>
        /// A server will be authenticated if it presents a chain of certificates (possibly of length 1)
        /// in which any one of the certificates matches the one passed here.
        /// </remarks>
        public X509Certificate2? PinnedServerCertificate
        {
            get => Options.PinnedServerCertificate;
            set => _freezer.PerformAction(() => Options.PinnedServerCertificate = value);
        }

        /// <summary>
        /// The NetworkInterface will accept the networkInterface name such as en0, eth0, 
        /// or pdp_ip0. When the network interface is specified, the replicator will use 
        /// the specified network interface to connect with the remote server instead of 
        /// using the default network interface as specified in the OS’s routing table. 
        /// If the specified network interface is not valid, the Replicator will fail to 
        /// connect with a permanent error, and the error code could be platform dependent 
        /// depending on what is being used to communicate with the remote server.
        /// </summary>
        internal string? NetworkInterface
        {
            get => Options.NetworkInterface;
            set => _freezer.PerformAction(() => Options.NetworkInterface = value);
        }

        /// <summary>
        /// [DEPRECATED] Func delegate that takes Document input parameter and bool output parameter
        /// Document pull will be allowed if output is true, othewise, Document pull 
        /// will not be allowed
        /// </summary>
        [Obsolete("PullFilter is deprecated, please use CollectionConfiguration.PullFilter")]
        public Func<Document, DocumentFlags, bool>? PullFilter
        {
            get => DefaultCollectionConfig.PullFilter;
            set => _freezer.PerformAction(() => DefaultCollectionConfig.PullFilter = value);
        }

        /// <summary>
        /// [DEPRECATED] Func delegate that takes Document input parameter and bool output parameter
        /// Document push will be allowed if output is true, othewise, Document push 
        /// will not be allowed
        /// </summary>
        [Obsolete("PushFilter is deprecated, please use CollectionConfiguration.PushFilter")]
        public Func<Document, DocumentFlags, bool>? PushFilter
        {
            get => DefaultCollectionConfig.PushFilter;
            set => _freezer.PerformAction(() => DefaultCollectionConfig.PushFilter = value);
        }

        /// <summary>
        /// A value indicating the direction of the replication.  The default is
        /// <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
        /// Default value is <see cref="Constants.DefaultReplicatorType" />
        /// </summary>
        public ReplicatorType ReplicatorType
        {
            get => _replicatorType;
            set =>_freezer.SetValue(ref _replicatorType, value);
        }

        /// <summary>
        /// Gets or sets the value to enable/disable the auto-purge feature. 
        /// * The default value is <c>true</c> which means that the document will be automatically purged 
        /// by the pull replicator when the user loses access to the document from both removed 
        /// and revoked scenarios. 
        /// * If set the property to <c>false</c>, AutoPurge is disabled, the replicator will notify the registered 
        /// DocumentReplicationListener <see cref="Replicator.AddDocumentReplicationListener"/> with an "access removed" 
        /// event <see cref="DocumentFlags.AccessRemoved"/> when access to the document is revoked on the Sync Gateway. 
        /// On receiving the event, the application may decide to manually purge the document. However, for performance reasons,
        /// any DocumentReplicationListeners added <see cref="Replicator.AddDocumentReplicationListener"/> to the replicator 
        /// after the replicator is started will not receive the access removed events until the replicator is restarted or 
        /// reconnected with Sync Gateway.
        /// * auto-purge will not be performed when DocumentIDs filter <see cref="CollectionConfiguration.DocumentIDs"/> is used.
        /// Default value is <see cref="Constants.DefaultReplicatorEnableAutoPurge" />
        /// </summary>
        public bool EnableAutoPurge
        {
            get => Options.EnableAutoPurge;
            set => _freezer.PerformAction(() => Options.EnableAutoPurge = value);
        }

        /// <summary>
        /// Gets or sets the replicator heartbeat keep-alive interval. 
        /// Default value is <see cref="Constants.DefaultReplicatorHeartbeat" /> 
        /// (5 min interval). 
        /// </summary>
        /// <exception cref="ArgumentException"> 
        /// Throw if set the Heartbeat to less or equal to 0 full seconds.
        /// </exception>
        public TimeSpan? Heartbeat
        {
            get => Options.Heartbeat;
            set => _freezer.PerformAction(() => Options.Heartbeat = value);
        }

        /// <summary>
        /// Gets or sets the Max number of retry attempts. The retry attempts will reset
        /// after the replicator is connected to a remote peer. 
        /// * Setting the value to 1 means that the replicator will try connect once and 
        /// the replicator will stop if there is a transient error.
        /// * Default value is <see cref="Constants.DefaultReplicatorMaxAttemptsSingleShot" />
        /// (<c>10</c>) for a single shot replicator.
        /// * Default value is <see cref="Constants.DefaultReplicatorMaxAttemptsContinuous" />
        /// (<see cref="int.MaxValue" />) for a continuous replicator.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Throw if set the MaxAttempts to a negative value.
        /// </exception>
        public int MaxAttempts
        {
            get => Options.MaxAttempts;
            set 
            {
                if (value == 0) { // backward compatible when user set the value to 0
                    _freezer.PerformAction(() => Options.MaxAttempts = Continuous ? Constants.DefaultReplicatorMaxAttemptsContinuous : Constants.DefaultReplicatorMaxAttemptsSingleShot);
                    _isDefaultMaxAttemptSet = true;
                } else {
                    _freezer.PerformAction(() => Options.MaxAttempts = value);
                        _isDefaultMaxAttemptSet = false;
                }
            }
        }

        /// <summary>
        /// Gets or sets the Max delay between retries.
        /// The default is null (5 min interval is applied).
        /// * <c>5</c> min interval is applied when MaxAttemptsWaitTime is set to null.
        /// * null will be returned when default <c>5</c> min interval is applied.
        /// Default value is <see cref="Constants.DefaultReplicatorMaxAttemptsWaitTime" />
        /// </summary>
        /// <exception cref="ArgumentException"> 
        /// Throw if set the MaxRetryWaitTime to less than 0 full seconds.
        /// </exception>
        public TimeSpan? MaxAttemptsWaitTime
        {
            get => Options.MaxAttemptsWaitTime;
            set => _freezer.PerformAction(() => Options.MaxAttemptsWaitTime = value);
        }

        /// <summary>
        /// Gets the target to replicate with (either <see cref="Database"/>
        /// or <see cref="Uri"/>
        /// </summary>
        public IEndpoint Target { get; }

        /// <summary>
        /// [DEPRECATED] The implemented custom conflict resolver object can be registered to the replicator 
        /// at ConflictResolver property. The default value of the conflictResolver is null. 
        /// When the value is null, the default conflict resolution will be applied.
        /// </summary>
        [Obsolete("ConflictResolver is deprecated, please use CollectionConfiguration.ConflictResolver")]
        public IConflictResolver? ConflictResolver
        {
            get => DefaultCollectionConfig.ConflictResolver;
            set => _freezer.PerformAction(() => DefaultCollectionConfig.ConflictResolver = value);
        }

        /// <summary>
        /// The collections in the replication.
        /// </summary>
        public IReadOnlyList<Collection> Collections => CollectionConfigs.Keys.ToList();

        //Pre 3.1 Default Collection Config
        internal CollectionConfiguration DefaultCollectionConfig => CollectionConfigs.ContainsKey(Database.GetDefaultCollection()) ? CollectionConfigs[Database.GetDefaultCollection()] 
            : throw new InvalidOperationException("Cannot operate on a missing Default Collection Configuration. Please AddCollection(Database.DefaultCollection, CollectionConfiguration).");

        internal IDictionary<Collection, CollectionConfiguration> CollectionConfigs { get; set; } = new Dictionary<Collection, CollectionConfiguration>();

        internal TimeSpan CheckpointInterval
        {
            get => Options.CheckpointInterval;
            set => _freezer.PerformAction(() => Options.CheckpointInterval = value);
        }

        internal ReplicatorOptionsDictionary Options { get; set; } = new ReplicatorOptionsDictionary();

        internal Database? OtherDB
        {
            get => _otherDb;
            set => _freezer.SetValue(ref _otherDb, value);
        }


        internal Uri? RemoteUrl
        {
            get => _remoteUrl;
            set => _freezer.SetValue(ref _remoteUrl, value);
        }

        internal C4SocketFactory SocketFactory
        {
            get => _socketFactory.open != IntPtr.Zero ? _socketFactory : LiteCore.Interop.SocketFactory.InternalFactory;
            set => _freezer.SetValue(ref _socketFactory, value);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new builder object with the required properties
        /// </summary>
        /// <remarks>
        /// After the ReplicatorConfiguration created, use <see cref="AddCollection(Collection, CollectionConfiguration)"/> 
        /// or <see cref="AddCollections(IList<Collection>, CollectionConfiguration)"/> to
        /// configure the collections in the replication with the target. If there is no collection
        /// specified, the replicator will not start with a no collections specified error.
        /// </remarks>
        /// <param name="target">The endpoint to replicate to, either local or remote</param>
        /// <exception cref="ArgumentException">Thrown if an unsupported <see cref="IEndpoint"/> implementation
        /// is provided as <paramref name="target"/></exception>
        public ReplicatorConfiguration(IEndpoint target)
        {
            Target = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(target), target);

            var castTarget = Misc.TryCast<IEndpoint, IEndpointInternal>(target);
            castTarget.Visit(this);
        }

        /// <summary>
        /// [DEPRECATED] Constructs a new builder object with the required properties
        /// </summary>
        /// <param name="database">The database that will serve as the local side of the replication</param>
        /// <param name="target">The endpoint to replicate to, either local or remote</param>
        /// <exception cref="ArgumentException">Thrown if an unsupported <see cref="IEndpoint"/> implementation
        /// is provided as <paramref name="target"/></exception>
        [Obsolete("Constructor ReplicatorConfiguration(Database, IEndpoint) is deprecated, please use ReplicatorConfiguration(IEndpoint)")]
        public ReplicatorConfiguration(Database database, IEndpoint target)
            :this(target)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(database), database);
            AddCollection(database.GetDefaultCollection());
        }

        #endregion

        #region Public Methods - Collections' Replicator Configurations

        /// <summary>
        /// Add a list of collections in the replication with an optional shared configuration. 
        /// </summary>
        /// <param name="collections"> that the given config will apply to</param>
        /// <param name="config"> will apply to the given collections</param>
        public void AddCollections(IList<Collection> collections, CollectionConfiguration? config = null)
        {
            if (collections == null || collections.Count == 0)
                return;

            foreach(var col in collections) {
                AddCollection(col, config);
            }
        }

        /// <summary>
        /// Add a collection in the replication with an optional collection configuration. 
        /// </summary>
        /// <remarks>
        /// The given configuration will replace the existing configuration of the given collection in replication.
        /// Default configuration will apply in the replication of the given collection if the given configuration is null.
        /// Configuration will be read only once applied to the collection in the replication.
        /// </remarks>
        /// <param name="collection"> to be added in the collections' configuration list</param>
        /// <param name="config"> to be added in the collections' configuration list</param>
        /// <exception cref="CouchbaseLiteException">Thrown if database of the given collection doesn't match 
        /// with the database <see cref="Database"/> of the replicator configuration.</exception>
        /// <exception cref="ArgumentNullException">Thrown if collection is null.</exception>
        public void AddCollection(Collection collection, CollectionConfiguration? config = null)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(collection), collection);

            if (Collections.Count > 0 && collection.Database != Database)
                throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter, 
                    $"The given collection database {collection.Database} doesn't match the database {Database} participating in the replication. All collections in the replication configuration must operate on the same database.");

            config = config == null ? new CollectionConfiguration() : new CollectionConfiguration(config);

            if (CollectionConfigs.ContainsKey(collection))
                CollectionConfigs.Remove(collection);

            CollectionConfigs.Add(collection, config);
        }

        /// <summary>
        /// Remove the give collection and it's configuration from the replication.
        /// </summary>
        /// <param name="collection"> to be removed</param>
        public void RemoveCollection(Collection collection)
        {
            CollectionConfigs.Remove(collection);
        }

        /// <summary>
        /// Get a copy of the given collection’s config. 
        /// </summary>
        /// <remarks>
        /// Use <see cref="AddCollection(Collection, CollectionConfiguration)"/> to add or update collection configuration in a replication.
        /// </remarks>
        /// <param name="collection">The collection config belongs to</param>
        /// <returns>The collection config of the given collection</returns>
        public CollectionConfiguration? GetCollectionConfig(Collection collection)
        {
            if (!CollectionConfigs.TryGetValue(collection, out var config)) {
                WriteLog.To.Sync.W(Tag, $"Failed getting the collection {collection}'s config.");
                return null;
            }

            return config;
        }

        #endregion

        #region Internal Methods

        internal ReplicatorConfiguration Freeze()
        {
            var frozenConfigs = new Dictionary<Collection, CollectionConfiguration>();
            foreach (var cc in CollectionConfigs) {
                frozenConfigs[cc.Key] = cc.Value.Freeze();
            }

            var retVal = new ReplicatorConfiguration(Target)
            {
                Authenticator = Authenticator,
#if COUCHBASE_ENTERPRISE
                AcceptOnlySelfSignedServerCertificate = AcceptOnlySelfSignedServerCertificate,
#endif
                Continuous = Continuous,
                ReplicatorType = ReplicatorType,
                Options = Options,
                CollectionConfigs = frozenConfigs
            };

            retVal._freezer.Freeze("Cannot modify a ReplicatorConfiguration that is in use");
            return retVal;
        }

        #endregion
    }
}
