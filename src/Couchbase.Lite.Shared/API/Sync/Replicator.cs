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
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// An object that is responsible for the replication of data between two
    /// endpoints.  The replication can set up to be pull only, push only, or both
    /// (i.e. pusher and puller are no longer separate) between a database and a URL
    /// or a database and another database on the same filesystem.
    /// </summary>
    public sealed unsafe class Replicator : IDisposable
    {
        #region Constants

        private const string Tag = nameof(Replicator);

        [NotNull]
        private static readonly C4ReplicatorMode[] Modes = {
            C4ReplicatorMode.Disabled, C4ReplicatorMode.Disabled, C4ReplicatorMode.OneShot, C4ReplicatorMode.Continuous
        };

        #endregion

        #region Variables

        [NotNull]private readonly ThreadSafety _databaseThreadSafety;

        [NotNull]private readonly Event<DocumentReplicationEventArgs> _documentEndedUpdate =
            new Event<DocumentReplicationEventArgs>();

        [NotNull]private readonly Event<ReplicatorStatusChangedEventArgs> _statusChanged =
            new Event<ReplicatorStatusChangedEventArgs>();

        private string _desc;
        private bool _disposed;

        private ReplicatorParameters _nativeParams;
        private C4ReplicatorStatus _rawStatus;
        private IReachability _reachability;
        private C4Replicator* _repl;
        private ConcurrentDictionary<Task, int> _conflictTasks = new ConcurrentDictionary<Task, int>();
        private IImmutableSet<string> _pendingDocIds;
        #if COUCHBASE_ENTERPRISE
        private X509Certificate2 _serverCertificate;
        #endif

        #endregion

        #region Properties

        /// <summary>
        /// Gets the configuration that was used to create this Replicator
        /// </summary>
        [NotNull]
        public ReplicatorConfiguration Config { get; }

        /// <summary>
        /// Gets the current status of the <see cref="Replicator"/>
        /// </summary>
        public ReplicatorStatus Status { get; set; }

        internal SerialQueue DispatchQueue { get; } = new SerialQueue();

        #if COUCHBASE_ENTERPRISE
        /// <summary>
        /// This property allows the developer to know what the current server certificate is when using TLS communication. 
        /// The developer could save the certificate and pin the certificate next time when setting up the replicator to 
        /// provide an SSH type of authentication.
        /// </summary>
        internal X509Certificate2 ServerCertificate { get; }
        #endif

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
        public Replicator([NotNull]ReplicatorConfiguration config)
        {
            Config = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(config), config).Freeze();
            _databaseThreadSafety = Config.Database.ThreadSafety;
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
        public ListenerToken AddChangeListener([NotNull]EventHandler<ReplicatorStatusChangedEventArgs> handler)
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
        public ListenerToken AddChangeListener([CanBeNull]TaskScheduler scheduler,
            [NotNull]EventHandler<ReplicatorStatusChangedEventArgs> handler)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(handler), handler);

            var cbHandler = new CouchbaseEventHandler<ReplicatorStatusChangedEventArgs>(handler, scheduler);
            _statusChanged.Add(cbHandler);
            return new ListenerToken(cbHandler, "repl");
        }

        /// <summary>
        /// Adds a documents ended listener on this replication object (similar to a C# event)
        /// </summary>
        /// <param name="handler">The logic to run during the callback</param>
        /// <returns>A token to remove the handler later</returns>
        public ListenerToken AddDocumentReplicationListener([NotNull]EventHandler<DocumentReplicationEventArgs> handler)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(handler), handler);

            return AddDocumentReplicationListener(null, handler);
        }

        /// <summary>
        /// Adds a document ended listener on this replication object (similar to a C# event, but
        /// with the ability to specify a <see cref="TaskScheduler"/> to schedule the 
        /// handler to run on)
        /// </summary>
        /// <param name="scheduler">The <see cref="TaskScheduler"/> to run the <c>handler</c> on
        /// (<c>null</c> for default)</param>
        /// <param name="handler">The logic to run during the callback</param>
        /// <returns>A token to remove the handler later</returns>
        public ListenerToken AddDocumentReplicationListener([CanBeNull]TaskScheduler scheduler,
            [NotNull]EventHandler<DocumentReplicationEventArgs> handler)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(handler), handler);
            Config.Options.ProgressLevel = ReplicatorProgressLevel.PerDocument;
            var cbHandler = new CouchbaseEventHandler<DocumentReplicationEventArgs>(handler, scheduler);
            _documentEndedUpdate.Add(cbHandler);
            return new ListenerToken(cbHandler, "repl");
        }

        /// <summary>
        /// Removes a previously added change listener via its <see cref="ListenerToken"/> and/or
        /// Removes a previously added documents ended listener via its <see cref="ListenerToken"/>
        /// </summary>
        /// <param name="token">The token received from <see cref="AddChangeListener(TaskScheduler, EventHandler{ReplicatorStatusChangedEventArgs})"/>
        /// and/or The token received from <see cref="AddDocumentReplicationListener(TaskScheduler, EventHandler{DocumentReplicationEventArgs})"/></param>
        public void RemoveChangeListener(ListenerToken token)
        {
            _statusChanged.Remove(token);
            if (_documentEndedUpdate.Remove(token) == 0) {
                Config.Options.ProgressLevel = ReplicatorProgressLevel.Overall;
            }
        }

        /// <summary>
        /// [DEPRECATED] Resets the local checkpoint of the replicator, meaning that it will read all changes since the beginning
        /// of time from the remote database.  This can only be called when the replicator is in a stopped state.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this method is called while the replicator is
        /// not in a stopped state</exception>
        [Obsolete("This method deprecated, please use Start(bool reset) to reset checkpoint when starting the replicator.")]
        public void ResetCheckpoint()
        {
            if (Status.Activity != ReplicatorActivityLevel.Stopped) {
                throw new InvalidOperationException(
                    "Replicator is not stopped.  Resetting checkpoint is only allowed when the replicator is in the stopped state.");
            }

            Config.Options.Reset = true;
        }

        /// <summary>
        /// Starts the replication
        /// </summary>
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
        public void Start(bool reset)
        {
            var status = default(C4ReplicatorStatus);
            DispatchQueue.DispatchSync(() =>
            {
                if (_disposed) {
                    throw new ObjectDisposedException(CouchbaseLiteErrorMessage.ReplicatorDisposed);
                }

                var err = SetupC4Replicator();
                if (err.code > 0) {
                    WriteLog.To.Sync.E(Tag, $"Setup replicator {this} failed.");
                }

                if (_repl != null) {
                    WriteLog.To.Sync.I(Tag, $"{this}: Starting");
                    Native.c4repl_start(_repl, Config.Options.Reset || reset);
                    Config.Options.Reset = false;
                    Config.Database.AddActiveReplication(this);
                    status = Native.c4repl_getStatus(_repl);
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
        /// Stops the replication
        /// </summary>
        public void Stop()
        {
            DispatchQueue.DispatchSync(() =>
            {
                StopReachabilityObserver();
                if (_repl != null) {
                    if (_rawStatus.level == C4ReplicatorActivityLevel.Stopped
                        || _rawStatus.level == C4ReplicatorActivityLevel.Stopping) {
                        return;
                    }

                    Native.c4repl_stop(_repl);
                }
            });
        }

        /// <summary>
        /// Gets a list of document IDs that are going to be pushed, but have not been pushed yet
        /// <item type="bullet">
        /// <description>API is a snapshot and results may change between the time the call was made and the time</description>
        /// </item>
        /// </summary>
        /// <returns>An immutable set of strings, each of which is a document ID</returns>
        /// <exception cref="CouchbaseLiteException">Thrown if no push replication</exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        [NotNull]
        public IImmutableSet<string> GetPendingDocumentIDs()
        {
            var result = new HashSet<string>();
            if (!IsPushing()) {
                CBDebug.LogAndThrow(WriteLog.To.Sync,
                    new CouchbaseLiteException(C4ErrorCode.Unsupported, CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs),
                    Tag, CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs, true);
            }

            DispatchQueue.DispatchSync(() => {
                var errSetupRepl = SetupC4Replicator();
                if (errSetupRepl.code > 0) {
                    CBDebug.LogAndThrow(WriteLog.To.Sync, CouchbaseException.Create(errSetupRepl), Tag, errSetupRepl.ToString(), true);
                }
            });

            byte[] pendingDocIds = LiteCoreBridge.Check(err =>
            {
                return Native.c4repl_getPendingDocIDs(_repl, err);
            });
            
            if (pendingDocIds != null) {
                _databaseThreadSafety.DoLocked(() => {
                    var flval = Native.FLValue_FromData(pendingDocIds, FLTrust.Trusted);
                    var flarr = Native.FLValue_AsArray(flval);
                    var cnt = (int) Native.FLArray_Count(flarr);
                    for (int i = 0; i < cnt; i++) {
                        var flv = Native.FLArray_Get(flarr, (uint) i);
                        result.Add(Native.FLValue_AsString(flv));
                    }

                    Array.Clear(pendingDocIds, 0, pendingDocIds.Length);
                    pendingDocIds = null;
                });
            }

            _pendingDocIds = result.ToImmutableHashSet<string>();
            return _pendingDocIds;
        }

        /// <summary>
        /// Checks whether or not a document with the given ID has any pending revisions to push
        /// </summary>
        /// <param name="documentID">The document ID</param>
        /// <returns>A bool which represents whether or not the document with the corresponding ID has one or more pending revisions.  
        /// <c>true</c> means that one or more revisions have not been pushed to the remote yet, 
        /// and <c>false</c> means that all revisions on the document have been pushed</returns>
        /// <exception cref="CouchbaseLiteException">Thrown if no push replication</exception>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public bool IsDocumentPending([NotNull]string documentID)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(documentID), documentID);
            bool isDocPending = false;

            if (!IsPushing()) {
                CBDebug.LogAndThrow(WriteLog.To.Sync,
                    new CouchbaseLiteException(C4ErrorCode.Unsupported, CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs),
                    Tag, CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs, true);
            }

            DispatchQueue.DispatchSync(() => {
                var errSetupRepl = SetupC4Replicator();
                if (errSetupRepl.code > 0) {
                    CBDebug.LogAndThrow(WriteLog.To.Sync, CouchbaseException.Create(errSetupRepl), Tag, errSetupRepl.ToString(), true);
                }
            });

            LiteCoreBridge.Check(err => 
            {
                isDocPending = Native.c4repl_isDocumentPending(_repl, documentID, err);
                return isDocPending;
            });

            return isDocPending;
        }

        #endregion

        #region Private Methods

        private bool IsPushing()
        {
            return Config.ReplicatorType.HasFlag(ReplicatorType.Push);
        }

        private static C4ReplicatorMode Mkmode(bool active, bool continuous)
        {
            return Modes[2 * Convert.ToInt32(active) + Convert.ToInt32(continuous)];
        }

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
            for (int i = 0; i < (int) numDocs; i++) {
                var current = docs[i];
                if (!pushing && current->error.domain == C4ErrorDomain.LiteCoreDomain &&
                    current->error.code == (int) C4ErrorCode.Conflict) {
                    replicatedDocumentsContainConflict.Add(new ReplicatedDocument(current->docID.CreateString() ?? "",
                        current->flags, current->error, current->errorIsTransient));
                } else {
                    documentReplications.Add(new ReplicatedDocument(current->docID.CreateString() ?? "",
                        current->flags, current->error, current->errorIsTransient));
                }
            }

            var replicator = GCHandle.FromIntPtr((IntPtr) context).Target as Replicator;

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

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4ReplicatorValidationFunction))]
        #endif
        private static bool PullValidateCallback(FLSlice docID, FLSlice revID, C4RevisionFlags revisionFlags, FLDict* dict, void* context)
        {
            var replicator = GCHandle.FromIntPtr((IntPtr) context).Target as Replicator;
            if (replicator == null) {
                WriteLog.To.Database.E(Tag, "Pull filter context pointing to invalid object {0}, aborting and returning true...",
                    replicator);
                return true;
            }

            var docIDStr = docID.CreateString();
            if (docIDStr == null) {
                WriteLog.To.Database.E(Tag, "Null document ID received in pull filter, rejecting...");
                return false;
            }

            var flags = revisionFlags.ToDocumentFlags();
            return replicator.PullValidateCallback(docIDStr, revID.CreateString(), dict, flags);
        }

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4ReplicatorValidationFunction))]
        #endif
        private static bool PushFilterCallback(FLSlice docID, FLSlice revID, C4RevisionFlags revisionFlags, FLDict* dict, void* context)
        {
            var replicator = GCHandle.FromIntPtr((IntPtr) context).Target as Replicator;
            if (replicator == null) {
                WriteLog.To.Database.E(Tag, "Push filter context pointing to invalid object {0}, aborting and returning true...",
                    replicator);
                return true;
            }

            var docIDStr = docID.CreateString();
            if (docIDStr == null) {
                WriteLog.To.Database.E(Tag, "Null document ID received in push filter, rejecting...");
                return false;
            }

            var flags = revisionFlags.ToDocumentFlags();
            return replicator.PushFilterCallback(docIDStr, revID.CreateString(), dict, flags);
        }

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4ReplicatorStatusChangedCallback))]
        #endif
        private static void StatusChangedCallback(C4Replicator* repl, C4ReplicatorStatus status, void* context)
        {
            var replicator = GCHandle.FromIntPtr((IntPtr) context).Target as Replicator;
            if (replicator == null)
                return;

            replicator.WaitPendingConflictTasks(status);
            replicator.DispatchQueue.DispatchSync(() =>
            {
                replicator.StatusChangedCallback(status);
            });
        }

        private void WaitPendingConflictTasks(C4ReplicatorStatus status)
        {
            if (status.error.code == 0 && status.error.domain == 0)
                return;

            if (status.level == C4ReplicatorActivityLevel.Stopped
                || status.level == C4ReplicatorActivityLevel.Idle) {
                var array = _conflictTasks?.Keys?.ToArray();
                if (array != null) {
                    Task.WaitAll(array);
                }
            }
        }

        private void Dispose(bool finalizing)
        {
            DispatchQueue.DispatchSync(() =>
            {
                if (_disposed) {
                    return;
                }

                if (!finalizing) {
                    _nativeParams?.Dispose();
                    if (Status.Activity != ReplicatorActivityLevel.Stopped) {
                        var newStatus = new ReplicatorStatus(ReplicatorActivityLevel.Stopped, Status.Progress, null);
                        _statusChanged.Fire(this, new ReplicatorStatusChangedEventArgs(newStatus));
                        Status = newStatus;
                    }
                }

                Stop();
                Native.c4repl_free(_repl);
                _repl = null;
                _disposed = true;
            });
        }

        private bool filterCallback(Func<Document, DocumentFlags, bool> filterFunction, string docID, string revID, FLDict* value, DocumentFlags flags)
        {
            var doc = new Document(Config.Database, docID, revID, value);
            return filterFunction(doc, flags);
        }

        private void OnDocEndedWithConflict(List<ReplicatedDocument> replications)
        {
            if (_disposed) {
                return;
            }

            for (int i = 0; i < replications.Count; i++) {
                var replication = replications[i];
                // Conflict pulling a document -- the revision was added but app needs to resolve it:
                var safeDocID = new SecureLogString(replication.Id, LogMessageSensitivity.PotentiallyInsecure);
                WriteLog.To.Sync.I(Tag, $"{this} pulled conflicting version of '{safeDocID}'");
                Task t = Task.Run(() =>
                {
                    try {
                        Config.Database.ResolveConflict(replication.Id, Config.ConflictResolver);
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
                });

                _conflictTasks.TryAdd(t.ContinueWith(task => _conflictTasks.TryRemove(t, out var dummy)), 0);
            }
        }

        private void OnDocEnded(List<ReplicatedDocument> replications, bool pushing)
        {
            if (_disposed) {
                return;
            }
            
            for (int i = 0; i < replications.Count; i++) {
                var replication = replications[i];
                var docID = replication.Id;
                var error = replication.NativeError;
                var transient = replication.IsTransient;
                var logDocID = new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure);
                var transientStr = transient ? "transient " : String.Empty;
                var dirStr = pushing ? "pushing" : "pulling";
                if (error.code > 0) {
                    WriteLog.To.Sync.I(Tag,
                        $"{this}: {transientStr}error {dirStr} '{logDocID}' : {error.code} ({Native.c4error_getMessage(error)})");
                }
            }
            _documentEndedUpdate.Fire(this, new DocumentReplicationEventArgs(replications, pushing));
        }

        private bool PullValidateCallback(string docID, string revID, FLDict* value, DocumentFlags flags)
        {
            return filterCallback(Config.PullFilter, docID, revID, value, flags);
        }

        private bool PushFilterCallback([NotNull]string docID, string revID, FLDict* value, DocumentFlags flags)
        {
            return Config.PushFilter(new Document(Config.Database, docID, revID, value), flags);
        }

        private void ReachabilityChanged(object sender, NetworkReachabilityChangeEventArgs e)
        {
            Debug.Assert(e != null);

            DispatchQueue.DispatchAsync(() =>
            {
                if (_repl == null && e.Status == NetworkReachabilityStatus.Reachable) {
                    WriteLog.To.Sync.I(Tag, $"{this}: Server may now be reachable; retrying...");
                }

                if (_repl != null && _reachability != null) {
                    Native.c4repl_setHostReachable(_repl, e.Status == NetworkReachabilityStatus.Reachable);
                }
            });
        }

        private C4Error SetupC4Replicator()
        {
            C4Error err = new C4Error();
            if (_repl != null) {
                Native.c4repl_setOptions(_repl, ((FLSlice) Config.Options.FLEncode()).ToArrayFast());
                return err;
            }

            _desc = ToString(); // Cache this; it may be called a lot when logging

            // Target:
            var addr = new C4Address();
            var scheme = new C4String();
            var host = new C4String();
            var path = new C4String();
            Database otherDB = null;
            var remoteUrl = Config.RemoteUrl;
            string dbNameStr = null;
            if (remoteUrl != null) {
                var pathStr = String.Concat(remoteUrl.Segments.Take(remoteUrl.Segments.Length - 1));
                dbNameStr = remoteUrl.Segments.Last().TrimEnd('/');
                scheme = new C4String(remoteUrl.Scheme);
                host = new C4String(remoteUrl.Host);
                path = new C4String(pathStr);
                addr.scheme = scheme.AsFLSlice();
                addr.hostname = host.AsFLSlice();
                addr.port = (ushort) remoteUrl.Port;
                addr.path = path.AsFLSlice();
            } else {
                otherDB = Config.OtherDB;
            }

            var options = Config.Options;

            Config.Authenticator?.Authenticate(options);

            options.Build();
            var push = Config.ReplicatorType.HasFlag(ReplicatorType.Push);
            var pull = Config.ReplicatorType.HasFlag(ReplicatorType.Pull);
            var continuous = Config.Continuous;
            
            var socketFactory = Config.SocketFactory;
            socketFactory.context = GCHandle.ToIntPtr(GCHandle.Alloc(this)).ToPointer();
            _nativeParams = new ReplicatorParameters(options)
            {
                Push = Mkmode(push, continuous),
                Pull = Mkmode(pull, continuous),
                Context = this,
                OnDocumentEnded = OnDocEnded,
                OnStatusChanged = StatusChangedCallback,
                SocketFactory = &socketFactory
            };

            // Clear the reset flag, it is a one-time thing
            options.Reset = false;

            if (Config.PushFilter != null)
                _nativeParams.PushFilter = PushFilterCallback;
            if (Config.PullFilter != null)
                _nativeParams.PullFilter = PullValidateCallback;

            DispatchQueue.DispatchSync(() =>
            {
                C4Error localErr = new C4Error();
            #if COUCHBASE_ENTERPRISE
                if (otherDB != null)
                    _repl = Native.c4repl_newLocal(Config.Database.c4db, otherDB.c4db, _nativeParams.C4Params,
                        &localErr);
                else
            #endif
                    _repl = Native.c4repl_new(Config.Database.c4db, addr, dbNameStr, _nativeParams.C4Params, &localErr);
                err = localErr;
            });

            scheme.Dispose();
            path.Dispose();
            host.Dispose();

            return err;
        }

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
            _reachability?.Stop();
            _reachability = null;
        }

        // Must be called from within the ThreadSafety
        private void StatusChangedCallback(C4ReplicatorStatus status)
        {
            if (_disposed) {
                return;
            }

            // idle or busy
            if ((status.level > C4ReplicatorActivityLevel.Connecting
                && status.level != C4ReplicatorActivityLevel.Stopping)
                && status.error.code == 0) {
                StopReachabilityObserver();
            }

            UpdateStateProperties(status);

            // offline
            if (status.level == C4ReplicatorActivityLevel.Offline) {
                StartReachabilityObserver();
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

        private void Stopped()
        {
            Debug.Assert(_rawStatus.level == C4ReplicatorActivityLevel.Stopped);
            Config.Database.RemoveActiveReplication(this);
        }

        private void UpdateStateProperties(C4ReplicatorStatus state)
        {
            Exception error = null;
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
            if (_desc != null) {
                return _desc;
            }

            var sb = new StringBuilder(3, 3);
            if (Config.ReplicatorType.HasFlag(ReplicatorType.Pull)) {
                sb.Append("<");
            }

            if (Config.Continuous) {
                sb.Append("*");
            }

            if (Config.ReplicatorType.HasFlag(ReplicatorType.Push)) {
                sb.Append(">");
            }

            return $"{GetType().Name}[{sb} {Config.Target}]";
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