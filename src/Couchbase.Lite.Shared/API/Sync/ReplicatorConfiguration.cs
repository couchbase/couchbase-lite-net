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
using Couchbase.Lite.Support;
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
    /// An enum representing level of opt in on progress of replication
    /// </summary>
    [Flags]
    internal enum ReplicatorProgressLevel : int
    {
        /// <summary>
        /// No additional replication progress callback
        /// </summary>
        Overall,

        /// <summary>
        /// Every document replication ended callback
        /// </summary>
        PerDocument, // >=1

        /// <summary>
        /// Every blob replication progress callback
        /// </summary>
        PerAttachment // >=2
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
        private bool _continuous;
        private Func<Document, DocumentFlags, bool> _pushFilter;
        private Func<Document, DocumentFlags, bool> _pullValidator;
        private Database _otherDb;
        private Uri _remoteUrl;
        private ReplicatorType _replicatorType = ReplicatorType.PushAndPull;
        private C4SocketFactory _socketFactory;
        private IConflictResolver _resolver;

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
            set => _freezer.PerformAction(() => Options.Headers = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(Headers), value));
        }

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

        /// <summary>
        /// Func delegate that takes Document input parameter and bool output parameter
        /// Document pull will be allowed if output is true, othewise, Document pull 
        /// will not be allowed
        /// </summary>
        public Func<Document, DocumentFlags, bool> PullFilter
        {
            get => _pullValidator;
            set => _freezer.PerformAction(() => _pullValidator = value);
        }

        /// <summary>
        /// Func delegate that takes Document input parameter and bool output parameter
        /// Document push will be allowed if output is true, othewise, Document push 
        /// will not be allowed
        /// </summary>
        public Func<Document, DocumentFlags, bool> PushFilter
        {
            get => _pushFilter;
            set => _freezer.PerformAction(() => _pushFilter = value);
        }

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

        public IConflictResolver ConflictResolver
        {
            get => _resolver;
            set => _freezer.PerformAction(() => _resolver = value);
        }

        internal TimeSpan CheckpointInterval
        {
            get => Options.CheckpointInterval;
            set => _freezer.PerformAction(() => Options.CheckpointInterval = value);
        }

        [NotNull]
        internal ReplicatorOptionsDictionary Options { get; set; } = new ReplicatorOptionsDictionary();

        [CanBeNull]
        internal Database OtherDB
        {
            get => _otherDb;
            set => _freezer.SetValue(ref _otherDb, value);
        }

        internal ReplicatorProgressLevel ProgressLevel
        {
            get => Options.ProgressLevel;
            set => _freezer.PerformAction(() => Options.ProgressLevel = value);
        }

        [CanBeNull]
        internal Uri RemoteUrl
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
        /// <param name="database">The database that will serve as the local side of the replication</param>
        /// <param name="target">The endpoint to replicate to, either local or remote</param>
        /// <exception cref="ArgumentException">Thrown if an unsupported <see cref="IEndpoint"/> implementation
        /// is provided as <paramref name="target"/></exception>
        public ReplicatorConfiguration([NotNull] Database database, [NotNull] IEndpoint target)
        {
            Database = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(database), database);
            Target = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(target), target);

            var castTarget = Misc.TryCast<IEndpoint, IEndpointInternal>(target);
            castTarget.Visit(this);
        }

        #endregion

        #region Internal Methods

        [NotNull]
        internal ReplicatorConfiguration Freeze()
        {
            var retVal = new ReplicatorConfiguration(Database, Target)
            {
                Authenticator = Authenticator,
                Continuous = Continuous,
                PushFilter = PushFilter,
                PullFilter = PullFilter,
                ReplicatorType = ReplicatorType,
                ProgressLevel = ProgressLevel,
                ConflictResolver = ConflictResolver,
                Options = Options
            };

            retVal._freezer.Freeze("Cannot modify a ReplicatorConfiguration that is in use");
            return retVal;
        }

        #endregion
    }
}
