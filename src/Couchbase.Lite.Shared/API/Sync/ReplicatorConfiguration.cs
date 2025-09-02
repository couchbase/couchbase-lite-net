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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

using Couchbase.Lite.Info;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

using LiteCore.Interop;

namespace Couchbase.Lite.Sync;

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
public sealed partial record ReplicatorConfiguration
{
    private const string Tag = nameof(ReplicatorConfiguration);

    private readonly bool _continuous;
    private readonly C4SocketFactory _socketFactory;
    private readonly IEndpoint _target = null!; // Set via property
    private bool _isDefaultMaxAttemptSet = true;

#if __IOS__ && !MACCATALYST
#endif

    /// <summary>
    /// Gets or sets whether a cookie can be set on a parent domain
    /// of the host that issued it (i.e. foo.bar.com can set a cookie for all
    /// of bar.com).  This is only recommended if the host issuing the cookie
    /// is well trusted.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool AcceptParentDomainCookies
    {
        get => Options.AcceptParentDomainCookies;
        init => Options.AcceptParentDomainCookies = value;
    }

#if __IOS__ && !MACCATALYST
        /// <summary>
        /// Allows the replicator to continue replicating in the background. The default
        /// value is <c>false</c>, which means that the replicator will suspend itself when the
        /// replicator detects that the application is being backgrounded.
        ///
        /// If setting the value to <c>true</c>, please ensure that your application delegate
        /// requests background time from the OS until the replication finishes.
        /// </summary>
        /// <remarks>
        /// > [!WARNING]
        /// > There is a bug in earlier versions in which the functionality is reversed from
        /// > what the documentation says, so please upgrade to get the correct behavior
        /// </remarks>
        public bool AllowReplicatingInBackground { get; init; }
#endif

    /// <summary>
    /// Gets or sets the class which will authenticate the replication
    /// </summary>
    public Authenticator? Authenticator { get; init; }

    /// <summary>
    /// Gets or sets the class which will authenticate with the proxy, if needed.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public ProxyAuthenticator? ProxyAuthenticator { get; init; }

    /// <summary>
    /// Gets or sets whether the <see cref="Replicator"/> should stay
    /// active indefinitely.  The default is <c>false</c>
    /// Default value is <see cref="Constants.DefaultReplicatorContinuous" />
    /// </summary>
    public bool Continuous
    {
        get => _continuous;
        init {
            _continuous = value;
            if (_isDefaultMaxAttemptSet)
                SetMaxAttempts(0);
        }
    }

    /// <summary>
    /// Extra HTTP headers to send in all requests to the remote target
    /// </summary>
    public IImmutableDictionary<string, string?> Headers
    {
        get => Options.Headers;
        init => Options.Headers = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(Headers), value);
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
        init => Options.PinnedServerCertificate = value;
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
        init => Options.NetworkInterface = value;
    }

    /// <summary>
    /// A value indicating the direction of the replication.  The default is
    /// <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
    /// Default value is <see cref="Constants.DefaultReplicatorType" />
    /// </summary>
    public ReplicatorType ReplicatorType { get; init; }

    /// <summary>
    /// Gets or sets the value to enable/disable the auto-purge feature. 
    /// * The default value is <c>true</c> which means that the document will be automatically purged 
    /// by the pull replicator when the user loses access to the document from both removed 
    /// and revoked scenarios. 
    /// * If set the property to <c>false</c>, AutoPurge is disabled, the replicator will notify the registered 
    /// DocumentReplicationListener <see cref="Replicator.AddDocumentReplicationListener(EventHandler{DocumentReplicationEventArgs})"/> with an "access removed" 
    /// event <see cref="DocumentFlags.AccessRemoved"/> when access to the document is revoked on the Sync Gateway. 
    /// On receiving the event, the application may decide to manually purge the document. However, for performance reasons,
    /// any DocumentReplicationListeners added <see cref="Replicator.AddDocumentReplicationListener(EventHandler{DocumentReplicationEventArgs})"/> to the replicator 
    /// after the replicator is started will not receive the access removed events until the replicator is restarted or 
    /// reconnected with Sync Gateway.
    /// * auto-purge will not be performed when DocumentIDs filter <see cref="CollectionConfiguration.DocumentIDs"/> is used.
    /// Default value is <see cref="Constants.DefaultReplicatorEnableAutoPurge" />
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool EnableAutoPurge
    {
        get => Options.EnableAutoPurge;
        init => Options.EnableAutoPurge = value;
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
        init => Options.Heartbeat = value;
    }

    /// <summary>
    /// Gets or sets the Max number of retry attempts. The retry attempts will reset
    /// after the replicator is connected to a remote peer. 
    /// * Setting the value to 1 means that the replicator will try to connect once and 
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
        init => SetMaxAttempts(value);
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
        init => Options.MaxAttemptsWaitTime = value;
    }

    /// <summary>
    /// Gets the target to replicate with (either <see cref="Database"/>
    /// or <see cref="Uri"/>
    /// </summary>
    public required IEndpoint Target
    {
        get => _target;
        init {
            switch (value) {
                case URLEndpoint e:
                    RemoteUrl = e.Url;
                    break;
    #if COUCHBASE_ENTERPRISE
                case DatabaseEndpoint e:
                    OtherDB = e.Database;
                    break;
                case P2P.MessageEndpoint e:
                    SocketFactory = e.SocketFactory;
                    RemoteUrl = new("x-msg-endpoint://");
                    Options.RemoteDBUniqueID = e.Uid;
                    break;
    #endif
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid implementation of IEndpoint");
            }
            _target = value;
        }
    }

    /// <summary>
    /// The configuration for the collections in the replication.
    /// </summary>
    public required IImmutableList<CollectionConfiguration> Collections { get; init; }
    
    internal TimeSpan CheckpointInterval
    {
        get => Options.CheckpointInterval;
        init => Options.CheckpointInterval = value;
    }

    internal ReplicatorOptionsDictionary Options { get; }

    internal Database DatabaseInternal
    {
        get {
            Debug.Assert(Collections.Count != 0);
            return Collections[0].Collection.Database;
        }
    }

    internal Uri? RemoteUrl { get; init; }

    internal Database? OtherDB { get; init; }

    internal C4SocketFactory SocketFactory
    {
        get => _socketFactory.open != IntPtr.Zero ? _socketFactory : LiteCore.Interop.SocketFactory.InternalFactory;
        init => _socketFactory = value;
    }

    /// <summary>
    /// Constructs a ReplicatorConfiguration object with the provided collection configurations
    /// and endpoint target.
    /// </summary>
    /// <param name="collections">Configurations for each collection to be replicated</param>
    /// <param name="target">The endpoint to replicate with</param>
    /// <exception cref="CouchbaseLiteException">If the collection set is empty or invalid</exception>
    [SetsRequiredMembers]
    public ReplicatorConfiguration(IEnumerable<CollectionConfiguration> collections,
        IEndpoint target)
    {
        
        // Set this first, other properties reference it
        Options = new();
        
        Continuous = Constants.DefaultReplicatorContinuous;
        AcceptParentDomainCookies = Constants.DefaultReplicatorAcceptParentCookies;
        EnableAutoPurge = Constants.DefaultReplicatorEnableAutoPurge;
        Heartbeat = Constants.DefaultReplicatorHeartbeat;
        MaxAttemptsWaitTime = Constants.DefaultReplicatorMaxAttemptsWaitTime;
        ReplicatorType = Constants.DefaultReplicatorType;
        
        #if COUCHBASE_ENTERPRISE
        AcceptOnlySelfSignedServerCertificate = Constants.DefaultReplicatorSelfSignedCertificateOnly;
        #endif

        // ReSharper disable once UseCollectionExpression (not supported in .NET 4.6.2)
        Collections = collections.ToImmutableArray();
        if (Collections.Count == 0) {
            throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter, "No collections were specified for replication. At least one collection must be specified.");
        }

        foreach (var c in Collections.Where(c => !ReferenceEquals(c.Collection.Database, DatabaseInternal))) {
            throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter,
                $"The given collection database {c.Collection.Database} doesn't match the database {DatabaseInternal} "
                + $"participating in the replication. All collections in the replication configuration must operate on the same database.");
        }
        
        Target = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(target), target);
    }

    [SetsRequiredMembers]
    internal ReplicatorConfiguration(ReplicatorConfiguration other)
    {
        // Normally this would be generated by the compiler but we need to adjust
        // Options so that it's a copy, not a reference to the same object
        Options = new(other.Options);
        Collections = other.Collections;
        Target = other.Target;
        
        // The rest of the properties not below are set via Options or Target above
        Authenticator = other.Authenticator;
        ProxyAuthenticator = other.ProxyAuthenticator;
        Continuous = other.Continuous;
        ReplicatorType = other.ReplicatorType;
        MaxAttempts = other.MaxAttempts;
    }

    private void SetMaxAttempts(int newValue)
    {
        if (newValue == 0) { // backward compatible when user set the value to 0
            Options.MaxAttempts = Continuous ? Constants.DefaultReplicatorMaxAttemptsContinuous : Constants.DefaultReplicatorMaxAttemptsSingleShot;
            _isDefaultMaxAttemptSet = true;
        } else {
            Options.MaxAttempts = newValue;
            _isDefaultMaxAttemptSet = false;
        }
    }
}