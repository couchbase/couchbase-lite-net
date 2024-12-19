// 
//  Replicator.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

using Dispatch;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// An object that is responsible for the replication of data between two
    /// endpoints.  The replication can set up to be pull only, push only, or both
    /// (i.e. pusher and puller are no longer separate) between a database and a URL
    /// or a database and another database on the same filesystem.
    /// </summary>
    public sealed unsafe partial class Replicator : IDisposable, IStoppable, IChangeObservable<ReplicatorStatusChangedEventArgs>,
        IDocumentReplicatedObservable
    {
        #region Constants

        private const string Tag = nameof(Replicator);

        private static readonly C4ReplicatorMode[] Modes = {
            C4ReplicatorMode.Disabled, C4ReplicatorMode.Disabled, C4ReplicatorMode.OneShot, C4ReplicatorMode.Continuous
        };

        #endregion

        #region Variables

        private enum DisposalState
        {
            None,
            Disposing,
            Disposed
        }

        private enum ReplicatorState : int
        {

            Stopped,
            Stopping,
            Suspended,
            Suspending,
            Offline,
            Running,
            Starting
        }

        private readonly ThreadSafety _databaseThreadSafety;

        private readonly Event<DocumentReplicationEventArgs> _documentEndedUpdate =
            new Event<DocumentReplicationEventArgs>();

        private readonly Event<ReplicatorStatusChangedEventArgs> _statusChanged =
            new Event<ReplicatorStatusChangedEventArgs>();

        private string? _desc;
        private DisposalState _disposalState;

        private ReplicatorParameters? _nativeParams;
        private ReplicatorState _state;
        private C4ReplicatorStatus _rawStatus;
        private IReachability? _reachability;
        private C4ReplicatorWrapper? _repl;
        private ConcurrentDictionary<Task, int> _conflictTasks = new ConcurrentDictionary<Task, int>();
        private CancellationTokenSource _conflictCancelSource = new();
        private IImmutableSet<string>? _pendingDocIds;
        private ReplicatorConfiguration _config;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the configuration that was used to create this Replicator
        /// </summary>
        /// <exception cref="CouchbaseLiteException">Thrown if the replicator configuration doesn't contain any collection.</exception>
        public ReplicatorConfiguration Config => _config.Collections.Count > 0 ? _config 
            : throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter, "Cannot operate on the replicator configuration without any collection.");

        /// <summary>
        /// Gets the current status of the <see cref="Replicator"/>
        /// </summary>
        public ReplicatorStatus Status { get; set; }

        internal SerialQueue DispatchQueue { get; } = new SerialQueue();

        /// <summary>
        /// This property allows the developer to know what the current server certificate is when using TLS communication. 
        /// The developer could save the certificate and pin the certificate next time when setting up the replicator to 
        /// provide an SSH type of authentication.
        /// </summary>
        public X509Certificate2? ServerCertificate { get; private set; }
        
        internal int PendingConflictCount => _conflictTasks.Count;

        #endregion

        #region Constructors

        static Replicator()
        {
            WebSocketTransport.RegisterWithC4();
        }

        /// <summary>
        /// Constructs a replicator based on the given <see cref="ReplicatorConfiguration"/>
        /// </summary>
        /// <param name="config">The configuration to use to create the replicator</param>
        public Replicator(ReplicatorConfiguration config)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(config), config);
            if (config.Collections.Count <= 0)
                throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter, "Replicator Configuration must contain at least one collection.");

            _config = config.Freeze();
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~Replicator()
        {
            Dispose(true);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a change listener on this replication object (similar to a C# event)
        /// </summary>
        /// <param name="handler">The logic to run during the callback</param>
        /// <returns>A token to remove the handler later</returns>
        public ListenerToken AddChangeListener(EventHandler<ReplicatorStatusChangedEventArgs> handler)
        {
            return AddChangeListener(null, handler);
        }

        /// <summary>
        /// Adds a change listener on this replication object (similar to a C# event, but
        /// with the ability to specify a <see cref="TaskScheduler"/> to schedule the 
        /// handler to run on)
        /// </summary>
        /// <param name="scheduler">The <see cref="TaskScheduler"/> to run the <c>handler</c> on
        /// (<c>null</c> for default)</param>
        /// <param name="handler">The logic to run during the callback</param>
        /// <returns>A token to remove the handler later</returns>
        public ListenerToken AddChangeListener(TaskScheduler? scheduler,
            EventHandler<ReplicatorStatusChangedEventArgs> handler)
        {
            if (_disposalState != DisposalState.None) {
                throw new ObjectDisposedException(nameof(Replicator));
            }

            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(handler), handler);

            var cbHandler = new CouchbaseEventHandler<ReplicatorStatusChangedEventArgs>(handler, scheduler);
            _statusChanged.Add(cbHandler);
            return new ListenerToken(cbHandler, ListenerTokenType.Replicator, this);
        }

        /// <summary>
        /// Adds a documents ended listener on this replication object (similar to a C# event)
        /// </summary>
        /// <remarks>
        /// Make sure add documents ended listener on this replication object before starting the replicator.
        /// </remarks>
        /// <param name="handler">The logic to run during the callback</param>
        /// <returns>A token to remove the handler later</returns>
        public ListenerToken AddDocumentReplicationListener(EventHandler<DocumentReplicationEventArgs> handler)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(handler), handler);

            return AddDocumentReplicationListener(null, handler);
        }

        /// <summary>
        /// Adds a document ended listener on this replication object (similar to a C# event, but
        /// with the ability to specify a <see cref="TaskScheduler"/> to schedule the 
        /// handler to run on)
        /// </summary>
        /// <remarks>
        /// Make sure add documents ended listener on this replication object before starting the replicator.
        /// </remarks>
        /// <param name="scheduler">The <see cref="TaskScheduler"/> to run the <c>handler</c> on
        /// (<c>null</c> for default)</param>
        /// <param name="handler">The logic to run during the callback</param>
        /// <returns>A token to remove the handler later</returns>
        public ListenerToken AddDocumentReplicationListener(TaskScheduler? scheduler,
            EventHandler<DocumentReplicationEventArgs> handler)
        {
            if (_disposalState != DisposalState.None) {
                throw new ObjectDisposedException(nameof(Replicator));
            }

            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(handler), handler);
            var cbHandler = new CouchbaseEventHandler<DocumentReplicationEventArgs>(handler, scheduler);
            if (_documentEndedUpdate.Add(cbHandler) == 0) {
                SetProgressLevel(C4ReplicatorProgressLevel.ReplProgressPerDocument);
            }
            
            return new ListenerToken(cbHandler, ListenerTokenType.DocReplicated, this);
        }

        /// <summary>
        /// Removes a previously added change listener via its <see cref="ListenerToken"/> and/or
        /// Removes a previously added documents ended listener via its <see cref="ListenerToken"/>
        /// </summary>
        /// <param name="token">The token received from <see cref="AddChangeListener(TaskScheduler, EventHandler{ReplicatorStatusChangedEventArgs})"/>
        /// and/or The token received from <see cref="AddDocumentReplicationListener(TaskScheduler, EventHandler{DocumentReplicationEventArgs})"/></param>
        public void RemoveChangeListener(ListenerToken token)
        {
            if (_disposalState != DisposalState.None) {
                throw new ObjectDisposedException(nameof(Replicator));
            }

            if (token.Type == ListenerTokenType.Replicator) {
                _statusChanged.Remove(token);
            } else if (_documentEndedUpdate.Remove(token) == 0) {
                SetProgressLevel(C4ReplicatorProgressLevel.ReplProgressOverall);
            }
        }

        /// <summary>
        /// Starts the replication
        /// </summary>
        /// <remarks>
        /// > [!WARNING] 
        /// > Calling this function inside of <see cref="Database.InBatch(Action)"/> will result
        /// > in a deadlock.
        /// </remarks>
        public void Start()
        {
            Start(false);
        }

        /// <summary>
        /// Starts the replication with an option to reset the checkpoint.
        /// </summary>
        /// <param name="reset">Resets the local checkpoint of the replicator, meaning that it will read all changes since the beginning
        /// of time from the remote database.
        /// </param>
        /// <remarks>
        /// > [!WARNING] 
        /// > Calling this function inside of <see cref="Database.InBatch(Action)"/> will result
        /// > in a deadlock.
        /// </remarks>
        public void Start(bool reset)
        {
            var status = default(C4ReplicatorStatus);
            DispatchQueue.DispatchSync(() =>
            {
                if (_disposalState != DisposalState.None) {
                    throw new ObjectDisposedException(nameof(Replicator), CouchbaseLiteErrorMessage.ReplicatorDisposed);
                }

                if(_state != ReplicatorState.Stopped && _state != ReplicatorState.Suspended) {
                    WriteLog.To.Sync.W(Tag, $"Replicator has already been started (state = {_state}, status = {_rawStatus.level}); ignored.");
                    return;
                }

                var err = SetupC4Replicator();
                if (err.code > 0) {
                    WriteLog.To.Sync.E(Tag, $"Setup replicator {this} failed.");
                }

                if (_repl != null) {
                    _state = ReplicatorState.Starting;
                    status = NativeSafe.c4repl_getStatus(_repl);
                    if (status.level == C4ReplicatorActivityLevel.Stopped
                    || status.level == C4ReplicatorActivityLevel.Stopping
                    || status.level == C4ReplicatorActivityLevel.Offline) {
                        ServerCertificate = null;
                        WriteLog.To.Sync.I(Tag, $"{this}: Starting");
                        NativeSafe.c4repl_start(_repl, Config.Options.Reset || reset);
                        Config.Options.Reset = false;
                        Config.DatabaseInternal.AddActiveStoppable(this);
                        status = NativeSafe.c4repl_getStatus(_repl);
#if __IOS__ && !MACCATALYST
                        if(!Config.AllowReplicatingInBackground) {
                            if (ObjCRuntime.Runtime.Arch == ObjCRuntime.Arch.DEVICE) {
                                StartBackgroundingMonitor();
                            } else {
                                WriteLog.To.Sync.W(Tag, "AllowReplicatingInBackground has no effect in iOS simulator");
                            }
                        }
#endif
                    }
                } else {
                    status = new C4ReplicatorStatus {
                        error = err,
                        level = C4ReplicatorActivityLevel.Stopped,
                        progress = new C4Progress()
                    };
                }
            });

            UpdateStateProperties(status);
            DispatchQueue.DispatchSync(() => StatusChangedCallback(status));
        }


        /// <summary>
        /// Stops a running replicator.  This method returns immediately; when the replicator actually
        /// stops, the replicator will change its status's activity level to <see cref="ReplicatorActivityLevel.Stopped"/>
        /// and the replicator change notification will be notified accordingly.
        /// </summary>
        public void Stop()
        {
            DispatchQueue.DispatchSync(() =>
            {
                if (_disposalState == DisposalState.Disposed) {
                    // This is called by Dispose, in which the state is Disposing, so only consider
                    // the Disposed state when throwing this
                    throw new ObjectDisposedException(nameof(Replicator));
                }

                if (_state <= ReplicatorState.Stopping) {
                    WriteLog.To.Sync.W(Tag, $"Replicator has been stopped or is stopping (state = {_state}, status = {_rawStatus.level}); ignore stop.");
                    return;
                }

                WriteLog.To.Sync.I(Tag, "Stopping...");
                _state = ReplicatorState.Stopping;

                StopReachabilityObserver();
                if (_repl != null) {
                    if (_rawStatus.level == C4ReplicatorActivityLevel.Stopped
                        || _rawStatus.level == C4ReplicatorActivityLevel.Stopping) {
                        return;
                    }


                    NativeSafe.c4repl_stop(_repl);
                }
            });
        }

        /// <summary>
        /// [DEPRECATED] Gets a list of document IDs that are going to be pushed, but have not been pushed yet
        /// <item type="bullet">
        /// <description>API is a snapshot and results may change between the time the call was made and the time</description>
        /// </item>
        /// </summary>
        /// <returns>An immutable set of strings, each of which is a document ID</returns>
        /// <exception cref="CouchbaseLiteException">Thrown if no push replication</exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        [Obsolete("GetPendingDocumentIDs() is deprecated, please use GetPendingDocumentIDs(Collection collection)")]
        public IImmutableSet<string> GetPendingDocumentIDs()
        {
            return GetPendingDocumentIDs(Config.Database.GetDefaultCollection());
        }

        /// <summary>
        /// [DEPRECATED] Checks whether or not a document with the given ID has any pending revisions to push
        /// </summary>
        /// <param name="documentID">The document ID</param>
        /// <returns>A bool which represents whether or not the document with the corresponding ID has one or more pending revisions.  
        /// <c>true</c> means that one or more revisions have not been pushed to the remote yet, 
        /// and <c>false</c> means that all revisions on the document have been pushed</returns>
        /// <exception cref="CouchbaseLiteException">Thrown if no push replication</exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        [Obsolete("IsDocumentPending(string documentID) is deprecated, please use IsDocumentPending(string documentID, Collection collection)")]
        public bool IsDocumentPending(string documentID)
        {
            return IsDocumentPending(documentID, Config.Database.GetDefaultCollection());
        }

        /// <summary>
        /// Checks whether or not a document with the given ID in the given collection is pending to push or not. 
        /// If the given collection is not part of the replication, an Invalid Parameter Exception will be thrown.
        /// </summary>
        /// <param name="documentID">The document ID</param>
        /// <param name="collection">The collection contains the doc with the given document ID</param>
        /// <returns>A bool which represents whether or not the document with the corresponding ID has one or more pending revisions.  
        /// <c>true</c> means that one or more revisions have not been pushed to the remote yet, 
        /// and <c>false</c> means that all revisions on the document have been pushed</returns>
        /// <exception cref="CouchbaseLiteException">Thrown if no push replication</exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public bool IsDocumentPending(string documentID, Collection collection)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(documentID), documentID);
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(collection), collection);

            DispatchQueue.DispatchSync(() => {
                if(_disposalState != DisposalState.None) {
                    throw new ObjectDisposedException(nameof(Replicator));
                }

                var errSetupRepl = SetupC4Replicator();
                if (errSetupRepl.code > 0) {
                    CBDebug.LogAndThrow(WriteLog.To.Sync, CouchbaseException.Create(errSetupRepl), Tag, errSetupRepl.ToString()!, true);
                }

                if (!IsPushing(collection)) {
                    CBDebug.LogAndThrow(WriteLog.To.Sync,
                        new CouchbaseLiteException(C4ErrorCode.Unsupported, CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs),
                        Tag, CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs, true);
                }
            });

            using (var collName_ = new C4String(collection.Name))
            using (var scopeName_ = new C4String(collection.Scope.Name)) {
                var collectionSpec = new C4CollectionSpec()
                {
                    name = collName_.AsFLSlice(),
                    scope = scopeName_.AsFLSlice()
                };

                return LiteCoreBridge.Check(err =>
                {
                    if(_repl == null) {
                        return false;
                    }

                    return NativeSafe.c4repl_isDocumentPending(_repl, documentID, collectionSpec, err);
                });
            }
        }

        /// <summary>
        /// Gets a list of document IDs of docs in the given collection that are going to be pushed, but have not been pushed yet. 
        /// If the given collection is not part of the replication, an Invalid Parameter Exception will be thrown.
        /// <item type="bullet">
        /// <description>API is a snapshot and results may change between the time the call was made and the time</description>
        /// </item>
        /// </summary>
        /// <param name="collection">The collection contains the list of document IDs of docs</param>
        /// <returns>An immutable set of strings, each of which is a document ID</returns>
        /// <exception cref="CouchbaseLiteException">Thrown if no push replication</exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public IImmutableSet<string> GetPendingDocumentIDs(Collection collection)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(collection), collection);
            var result = new HashSet<string>();
            byte[]? pendingDocIds = null;

            DispatchQueue.DispatchSync(() => {
                if (_disposalState != DisposalState.None) {
                    throw new ObjectDisposedException(nameof(Replicator));
                }

                var errSetupRepl = SetupC4Replicator();
                if (errSetupRepl.code > 0) {
                    CBDebug.LogAndThrow(WriteLog.To.Sync, CouchbaseException.Create(errSetupRepl), Tag, errSetupRepl.ToString()!, true);
                }

                if (!IsPushing(collection)) {
                    CBDebug.LogAndThrow(WriteLog.To.Sync,
                        new CouchbaseLiteException(C4ErrorCode.Unsupported, CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs),
                        Tag, CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs, true);
                }
            });

            using (var collName_ = new C4String(collection.Name))
            using (var scopeName_ = new C4String(collection.Scope.Name)) {
                var collectionSpec = new C4CollectionSpec()
                {
                    name = collName_.AsFLSlice(),
                    scope = scopeName_.AsFLSlice()
                };

                pendingDocIds = LiteCoreBridge.Check(err =>
                {
                    if(_repl == null) {
                        return null;
                    }

                    return NativeSafe.c4repl_getPendingDocIDs(_repl, collectionSpec, err);
                });

                if (pendingDocIds != null) {
                    var flval = Native.FLValue_FromData(pendingDocIds, FLTrust.Trusted);
                    var flarr = Native.FLValue_AsArray(flval);
                    var cnt = (int)Native.FLArray_Count(flarr);
                    for (int i = 0; i < cnt; i++) {
                        var flv = Native.FLArray_Get(flarr, (uint)i);
                        var nextId = Native.FLValue_AsString(flv);
                        Debug.Assert(nextId != null);
                        result.Add(nextId!);
                    }

                    Array.Clear(pendingDocIds, 0, pendingDocIds.Length);
                    pendingDocIds = null;
                }
            }

            _pendingDocIds = result.ToImmutableHashSet<string>();
            return _pendingDocIds;
        }

        #endregion

        #region Internal Methods

        internal void WatchForCertificate(WebSocketWrapper wrapper)
        {
            wrapper.PeerCertificateReceived += OnTlsCertificate;
        }

        internal void CheckForCookiesToSet(WebSocketWrapper wrapper)
        {
            wrapper.CookiesToSetReceived += OnCookiesToSetReceived;
        }

        #endregion

        #region Private Methods - Filters

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4ReplicatorValidationFunction))]
        #endif
        private static bool PullValidateCallback(C4CollectionSpec collectionSpec, FLSlice docID, FLSlice revID, C4RevisionFlags revisionFlags, FLDict* dict, void* context)
        {
            var replicator = GCHandle.FromIntPtr((IntPtr)context).Target as Replicator;
            if (replicator == null) {
                WriteLog.To.Database.E(Tag, "Pull filter context pointing to invalid null replicator, aborting and returning true...");
                return true;
            }

            var docIDStr = docID.CreateString();
            if (docIDStr == null) {
                WriteLog.To.Database.E(Tag, "Null document ID received in pull filter, rejecting...");
                return false;
            }

            var collName = collectionSpec.name.CreateString()!;
            var scope = collectionSpec.scope.CreateString()!;
            var flags = revisionFlags.ToDocumentFlags();
            return replicator.PullValidateCallback(collName, scope, docIDStr, revID.CreateString()!, dict, flags);
        }

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4ReplicatorValidationFunction))]
        #endif
        private static bool PushFilterCallback(C4CollectionSpec collectionSpec, FLSlice docID, FLSlice revID, C4RevisionFlags revisionFlags, FLDict* dict, void* context)
        {
            var replicator = GCHandle.FromIntPtr((IntPtr)context).Target as Replicator;
            if (replicator == null) {
                WriteLog.To.Database.E(Tag, "Push filter context pointing to invalid null replicator, aborting and returning true...");
                return true;
            }

            var docIDStr = docID.CreateString();
            if (docIDStr == null) {
                WriteLog.To.Database.E(Tag, "Null document ID received in push filter, rejecting...");
                return false;
            }

            var collName = collectionSpec.name.CreateString()!;
            var scope = collectionSpec.scope.CreateString()!;
            var flags = revisionFlags.ToDocumentFlags();
            return replicator.PushFilterCallback(collName, scope, docIDStr, revID.CreateString()!, dict, flags);
        }

        private bool PullValidateCallback(string collName, string scope, string docID, string revID, FLDict* value, DocumentFlags flags)
        {
            var coll = Config.Collections.FirstOrDefault(x => x.Name == collName && x.Scope.Name == scope);
            if(coll == null) {
                WriteLog.To.Sync.E(Tag, "Collection doesn't exist inside PullValidateCallback, aborting and returning true...");
                return true;
            }

            var config = Config.GetCollectionConfig(coll);
            if(config?.PullFilter == null) {
                WriteLog.To.Sync.E(Tag, "Unable to find filter inside PullValidateCallback, aborting and returning true...");
                return true;
            }

            return config.PullFilter(new Document(coll, docID, revID, value), flags);
        }

        private bool PushFilterCallback(string collName, string scope, string docID, string revID, FLDict* value, DocumentFlags flags)
        {
            var coll = Config.Collections.FirstOrDefault(x => x.Name == collName && x.Scope.Name == scope);
            if (coll == null) {
                WriteLog.To.Sync.E(Tag, "Collection doesn't exist inside PushFilterCallback, aborting and returning true...");
                return true;
            }

            var config = Config.GetCollectionConfig(coll);
            if (config?.PushFilter == null) {
                WriteLog.To.Sync.E(Tag, "Unable to find filter inside PushFilterCallback, aborting and returning true...");
                return true;
            }

            return config.PushFilter(new Document(coll, docID, revID, value), flags);
        }

        #endregion

        #region Private Methods - Doc Ended

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4ReplicatorDocumentEndedCallback))]
        #endif
        private static void OnDocEnded(C4Replicator* repl, bool pushing, IntPtr numDocs, C4DocumentEnded** docs, void* context)
        {
            if (docs == null || numDocs == IntPtr.Zero) {
                return;
            }

            var replicatedDocumentsContainConflict = new List<ReplicatedDocument>();
            var documentReplications = new List<ReplicatedDocument>();
            for (int i = 0; i < (int)numDocs; i++) {
                var current = docs[i];
                if (!pushing && current->error.domain == C4ErrorDomain.LiteCoreDomain &&
                    current->error.code == (int)C4ErrorCode.Conflict) {
                    replicatedDocumentsContainConflict.Add(new ReplicatedDocument(current->docID.CreateString() ?? "", current->collectionSpec,
                        current->flags, current->error, current->errorIsTransient));
                } else {
                    documentReplications.Add(new ReplicatedDocument(current->docID.CreateString() ?? "", current->collectionSpec,
                        current->flags, current->error, current->errorIsTransient));
                }
            }

            var replicator = GCHandle.FromIntPtr((IntPtr)context).Target as Replicator;
            if (documentReplications.Count > 0) {
                replicator?.DispatchQueue.DispatchAsync(() =>
                {
                    replicator.OnDocEnded(documentReplications, pushing);
                });
            }

            if (replicatedDocumentsContainConflict.Count > 0) {
                replicator?.DispatchQueue.DispatchAsync(() =>
                {
                    replicator.OnDocEndedWithConflict(replicatedDocumentsContainConflict);
                });
            }
        }

        private void OnDocEndedWithConflict(List<ReplicatedDocument> replications)
        {
            if (_disposalState != DisposalState.None) {
                return;
            }

#if __IOS__ && !MACCATALYST
            if(ConflictResolutionSuspended) {
                return;
            }
#endif

            for (int i = 0; i < replications.Count; i++) {
                var replication = replications[i];
                // Conflict pulling a document -- the revision was added but app needs to resolve it:
                var safeDocID = new SecureLogString(replication.Id, LogMessageSensitivity.PotentiallyInsecure);
                WriteLog.To.Sync.I(Tag, $"{this} pulled conflicting version of '{safeDocID}'");
                var cancelToken = _conflictCancelSource.Token;
                Task t = Task.Run(() =>
                {
                    try {
                        var coll = Config.Collections.First(x => x.Name == replication.CollectionName && x.Scope.Name == replication.ScopeName);
                        var collectionConfig = Config.GetCollectionConfig(coll);
                        if (cancelToken.IsCancellationRequested) {
                            // Try to catch cancellation before it reaches the user
                            return;
                        }

                        coll.Database.ResolveConflict(replication.Id, collectionConfig!.ConflictResolver, coll);
                        replication = replication.ClearError();
                    } catch (CouchbaseException e) {
                        replication.Error = e;
                    } catch (Exception e) {
                        replication.Error = new CouchbaseLiteException(C4ErrorCode.UnexpectedError, e.Message, e);
                    }

                    if (replication.Error != null) {
                        WriteLog.To.Sync.W(Tag, $"Conflict resolution of '{replication.Id}' failed", replication.Error);
                    }

                    _documentEndedUpdate.Fire(this, new DocumentReplicationEventArgs(new[] { replication }, false));
                }, _conflictCancelSource.Token);
                    
                t.ContinueWith(task =>
                {
                    _conflictTasks.TryRemove(task, out var dummy);
                });

                _conflictTasks.TryAdd(t, 0);
            }
        }

        private void OnDocEnded(List<ReplicatedDocument> replications, bool pushing)
        {
            if (_disposalState != DisposalState.None) {
                return;
            }

            for (int i = 0; i < replications.Count; i++) {
                var replication = replications[i];
                var error = replication.NativeError;
                if (error.code > 0) {
                    var docID = replication.Id;
                    var transient = replication.IsTransient;
                    var logDocID = new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure);
                    var transientStr = transient ? "transient " : String.Empty;
                    var dirStr = pushing ? "pushing" : "pulling";
                    WriteLog.To.Sync.I(Tag,
                        $"{this}: {transientStr}error {dirStr} '{logDocID}' : {error.code} ({Native.c4error_getMessage(error)})");
                }
            }

            _documentEndedUpdate.Fire(this, new DocumentReplicationEventArgs(replications, pushing));
        }

        #endregion

        #region Private Methods - Status Change

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4ReplicatorStatusChangedCallback))]
        #endif
        private static void StatusChangedCallback(C4Replicator* repl, C4ReplicatorStatus status, void* context)
        {
            var replicator = GCHandle.FromIntPtr((IntPtr)context).Target as Replicator;
            if (replicator == null)
                return;

            replicator.WaitPendingConflictTasks(status);
            replicator.DispatchQueue.DispatchSync(() =>
            {
                replicator.StatusChangedCallback(status);
            });
        }

        // Must be called from within the ThreadSafety
        private void StatusChangedCallback(C4ReplicatorStatus status)
        {
            if(_disposalState == DisposalState.Disposed) {
                return;
            }

            if (_disposalState == DisposalState.Disposing && status.level != C4ReplicatorActivityLevel.Stopped) {
                return;
            }

            // idle or busy
            if ((status.level > C4ReplicatorActivityLevel.Connecting
                && status.level != C4ReplicatorActivityLevel.Stopping)
                && status.error.code == 0) {
                _state = ReplicatorState.Running;
                StopReachabilityObserver();
            }

            UpdateStateProperties(status);

            // offline
            if (status.level == C4ReplicatorActivityLevel.Offline) {
                if (_state == ReplicatorState.Suspending) {
                    _state = ReplicatorState.Suspended;
                } else if (_state > ReplicatorState.Stopping) {
                    _state = ReplicatorState.Offline;
                    StartReachabilityObserver();
                }
            }

            //  stopped
            if (status.level == C4ReplicatorActivityLevel.Stopped) {
                StopReachabilityObserver();
                Stopped();
            }

            try {
                _statusChanged.Fire(this, new ReplicatorStatusChangedEventArgs(Status));
            } catch (Exception e) {
                WriteLog.To.Sync.W(Tag, "Exception during StatusChanged callback", e);
            }
        }

        private void WaitPendingConflictTasks(C4ReplicatorStatus status)
        {
            if (status.level == C4ReplicatorActivityLevel.Stopped
                || status.level == C4ReplicatorActivityLevel.Idle) {
                var array = _conflictTasks?.Keys?.ToArray();
                if (array?.Length > 0) {
                    Task.WaitAll(array);
                }
            }
        }

        #endregion

        #region Private Methods - Reachability

        private void StartReachabilityObserver()
        {
            if (_reachability != null) {
                return;
            }

            var remoteUrl = (Config.Target as URLEndpoint)?.Url;
            if (remoteUrl == null) {
                return;
            }

            _reachability = Service.GetInstance<IReachability>() ?? new Reachability();
            _reachability.StatusChanged += ReachabilityChanged;
            _reachability.Url = remoteUrl;
            _reachability.Start();
        }

        private void StopReachabilityObserver()
        {
            if (_reachability != null) {
                _reachability.StatusChanged -= ReachabilityChanged;
                _reachability.Stop();
                _reachability = null;
            }
        }

        private void ReachabilityChanged(object? sender, NetworkReachabilityChangeEventArgs e)
        {
            DispatchQueue.DispatchAsync(() =>
            {
                if (_repl != null /* just to be safe */) {
                    NativeSafe.c4repl_setHostReachable(_repl, e.Status == NetworkReachabilityStatus.Reachable);
                }
            });
        }

        #endregion

        #region Private Methods

        private bool IsPushing(Collection collection)
        {
            var collConfig = Config.GetCollectionConfig(collection)
                ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Collection config not found inside IsPushing");
            return collConfig.ReplicatorType.HasFlag(ReplicatorType.Push);
        }

        private static C4ReplicatorMode Mkmode(bool active, bool continuous)
        {
            return Modes[2 * Convert.ToInt32(active) + Convert.ToInt32(continuous)];
        }

        private void OnTlsCertificate(object? sender, TlsCertificateReceivedEventArgs e)
        {
            (sender as WebSocketWrapper)!.PeerCertificateReceived -= OnTlsCertificate;
            ServerCertificate = e.PeerCertificate;
        }

        private void OnCookiesToSetReceived(object? sender, string e)
        {
            (sender as WebSocketWrapper)!.CookiesToSetReceived -= OnCookiesToSetReceived;

            var remoteUrl = (Config.Target as URLEndpoint)?.Url;
            if (remoteUrl == null) {
                return;
            }

            Config.DatabaseInternal.SaveCookie(e, remoteUrl, Config.AcceptParentDomainCookies);
        }

        private void Dispose(bool finalizing)
        {
            if(!finalizing) {
                DispatchQueue.DispatchSync(() =>
                {
                    if (_disposalState != DisposalState.None) {
                        return;
                    }

                    _disposalState = DisposalState.Disposing;
                    if (Status.Activity != ReplicatorActivityLevel.Stopped) {
                        // This will defer the freeing until after Stop
                        Stop();
                    } else {
                        // Already stopped, so go ahead and free
                        _disposalState = DisposalState.Disposed;
                        _nativeParams?.Dispose();
                        Config.Options.Dispose();
                        _repl?.Dispose();
                        _repl = null;
                    }
                });
            }
        }

        private C4Error SetupC4Replicator()
        {
            Config.DatabaseInternal.CheckOpenLocked();
            C4Error err = new C4Error();
            if (_repl != null) {
                var options = Config.Options.FLEncode();
                NativeSafe.c4repl_setOptions(_repl, (FLSlice)options);
                Native.FLSliceResult_Release(options);
                return err;
            }

            _desc = ToString(); // Cache this; it may be called a lot when logging

            // Target:
            var addr = new C4Address();
            Database? otherDB = null;
            var remoteUrl = Config.RemoteUrl;
            string? dbNameStr = remoteUrl?.Segments?.Last().TrimEnd('/');
            using (var dbNameStr_ = new C4String(dbNameStr))
            using (var remoteUrlStr_ = new C4String(remoteUrl?.AbsoluteUri)) {
                FLSlice dn = dbNameStr_.AsFLSlice();
                C4Address localAddr;

                // Note: Don't use Native.c4address_fromURL, remoteUrlStr_ MUST stay alive for a this entire method
                var addrFromUrl = NativeSafe.c4address_fromURL(remoteUrlStr_.AsFLSlice(), &localAddr, &dn);
                addr = localAddr;

                if (addrFromUrl) {
                    //get cookies from url and add to replicator options
                    var cookiestring = Config.DatabaseInternal.GetCookies(remoteUrl);
                    if (!String.IsNullOrEmpty(cookiestring)) {
                        var split = cookiestring!.Split(';') ?? Enumerable.Empty<string>();
                        foreach (var entry in split) {
                            var pieces = entry?.Split('=');
                            if (pieces?.Length != 2) {
                                WriteLog.To.Sync.W(Tag, "Garbage cookie value, ignoring");
                                continue;
                            }

                            Config.Options.Cookies.Add(new Cookie(pieces[0]?.Trim()!, pieces[1]?.Trim()));
                        }
                    }
                } else {
                    Config.OtherDB?.CheckOpenLocked();
                    otherDB = Config.OtherDB;
                }

                var options = Config.Options;

                Config.Authenticator?.Authenticate(options);
                Config.ProxyAuthenticator?.Authenticate(options);

                options.Build();
                var push = Config.ReplicatorType.HasFlag(ReplicatorType.Push);
                var pull = Config.ReplicatorType.HasFlag(ReplicatorType.Pull);
                var continuous = Config.Continuous;

                var socketFactory = Config.SocketFactory;
                socketFactory.context = GCHandle.ToIntPtr(GCHandle.Alloc(this)).ToPointer();
                _nativeParams = new ReplicatorParameters(options)
                {
                    Context = this,
                    OnDocumentEnded = OnDocEnded,
                    OnStatusChanged = StatusChangedCallback,
                    SocketFactory = &socketFactory
                };

                // Clear the reset flag, it is a one-time thing
                options.Reset = false;

                var collCnt = (long)Config.Collections.Count;
                var replicatorIdTag = (ulong)socketFactory.context;
                DispatchQueue.DispatchSync(() =>
                {
                    var replicationCollections = new ReplicationCollection[collCnt];
                    for (int i = 0; i < collCnt; i++) {
                        var collectionConfig = Config.CollectionConfigs.ElementAt(i);
                        var col = collectionConfig.Key;
                        var config = collectionConfig.Value;
                        var colConfigOptions = config.Options;

                        //TODO: in the future we can set different replicator type per collection
                        //var collPush = config.ReplicatorType.HasFlag(ReplicatorType.Push);
                        //var collPull = config.ReplicatorType.HasFlag(ReplicatorType.Pull);
                        //for now collecion config's ReplicatorType should be the same as ReplicatorType in replicator config
                        config.ReplicatorType = Config.ReplicatorType; 

                        colConfigOptions.Build();

                        var spec = new CollectionSpec(col.Scope.Name, col.Name);

                        var replicationCollection = new ReplicationCollection(colConfigOptions)
                        {
                            Push = Mkmode(push, continuous),
                            Pull = Mkmode(pull, continuous),
                            Context = this,
                            Spec = spec
                        };

                        if (config.PushFilter != null)
                            replicationCollection.PushFilter = PushFilterCallback;
                        if (config.PullFilter != null)
                            replicationCollection.PullFilter = PullValidateCallback;

                        replicationCollections[i] = replicationCollection;
                    }

                    C4Error localErr = new C4Error();
                    _nativeParams.ReplicationCollections = replicationCollections;
                    using (var replParamsPinned = _nativeParams.Pinned()) {
#if COUCHBASE_ENTERPRISE
                        // Both c4db are assured not null by CheckOpenLocked previously
                        if (otherDB != null) {
                            _repl = NativeSafe.c4repl_newLocal(Config.DatabaseInternal.c4db!, otherDB.c4db!.RawDatabase, _nativeParams.C4Params,
                                $"DNRepl@{replicatorIdTag:X2}", &localErr);
                        } else
#endif
                        {
                            _repl = NativeSafe.c4repl_new(Config.DatabaseInternal.c4db!, addr, dbNameStr, _nativeParams.C4Params, $"DNRepl@{replicatorIdTag:X2}", &localErr);
                        }
                    }

                    if (_documentEndedUpdate.Counter > 0) {
                        SetProgressLevel(C4ReplicatorProgressLevel.ReplProgressPerDocument);
                    }

                    err = localErr;
                });
            }

            return err;
        }

        private void SetProgressLevel(C4ReplicatorProgressLevel progressLevel)
        {
            if (_repl == null) {
                WriteLog.To.Sync.V(Tag, $"Progress level {progressLevel} is not yet set because C4Replicator is not created.");
                return;
            }

            C4Error err = new C4Error();
            var setResult = NativeSafe.c4repl_setProgressLevel(_repl, progressLevel, &err);
            if (!setResult || err.code > 0) {
                WriteLog.To.Sync.W(Tag, $"Failed set progress level to {progressLevel}", err);
            }
        }

        private void Stopped()
        {
            Debug.Assert(_rawStatus.level == C4ReplicatorActivityLevel.Stopped);
            _state = ReplicatorState.Stopped;
            Config.DatabaseInternal.RemoveActiveStoppable(this);
            
            #if __IOS__ && !MACCATALYST
            EndBackgroundingMonitor();
            #endif
            
            if(_disposalState == DisposalState.Disposing) {
                _disposalState = DisposalState.Disposed;
                _nativeParams?.Dispose();
                Config.Options.Dispose();
                _repl?.Dispose();
                _repl = null;
            }
        }

        private void UpdateStateProperties(C4ReplicatorStatus state)
        {
            Exception? error = null;
            if (state.error.code > 0) {
                error = CouchbaseException.Create(state.error);
            }

            _rawStatus = state;
            var level = (ReplicatorActivityLevel) state.level;
            var progress = new ReplicatorProgress(state.progress.unitsCompleted, state.progress.unitsTotal);
            Status = new ReplicatorStatus(level, progress, error);
            WriteLog.To.Sync.I(Tag, $"{this} is {state.level}, progress {state.progress.unitsCompleted}/{state.progress.unitsTotal}");
        }

#endregion

        #region Overrides

        /// <inheritdoc />
        public override string ToString()
        {
            if (_disposalState == DisposalState.Disposed) {
                // This is called after Dispose, in which the state is Disposing, so only consider
                // the Disposed state when throwing this
                throw new ObjectDisposedException(nameof(Replicator));
            }

            if (_desc != null) {
                return _desc;
            }

            var sb = new StringBuilder(3, 3);
            if (Config.ReplicatorType.HasFlag(ReplicatorType.Pull)) {
                sb.Append("<");
            }

            sb.Append(Config.Continuous ? '*' : 'o');

            if (Config.ReplicatorType.HasFlag(ReplicatorType.Push)) {
                sb.Append(">");
            }

            return $"{nameof(Replicator)}[{sb} {Config.Target}]";
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}