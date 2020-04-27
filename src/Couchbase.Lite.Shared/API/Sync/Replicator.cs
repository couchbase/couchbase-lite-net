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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

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

        private const int MaxOneShotRetryCount = 2;

        private const string Tag = nameof(Replicator);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(10);

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
        private int _retryCount;
        private bool _stopping;
        private ConcurrentDictionary<Task, int> _conflictTasks = new ConcurrentDictionary<Task, int>();

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
            if(_documentEndedUpdate.Remove(token)==0)
                Config.Options.ProgressLevel = ReplicatorProgressLevel.Overall;
        }

        /// <summary>
        /// Resets the local checkpoint of the replicator, meaning that it will read all changes since the beginning
        /// of time from the remote database.  This can only be called when the replicator is in a stopped state.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this method is called while the replicator is
        /// not in a stopped state</exception>
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
            DispatchQueue.DispatchSync(() =>
            {
                if (_disposed) {
                    throw new ObjectDisposedException(CouchbaseLiteErrorMessage.ReplicatorDisposed);
                }

                if (_repl != null) {
                    WriteLog.To.Sync.W(Tag, $"{this} has already started");
                    return;
                }

                WriteLog.To.Sync.I(Tag, $"{this}: Starting");
                _retryCount = 0;
                StartInternal();
            });
        }

        /// <summary>
        /// Stops the replication
        /// </summary>
        public void Stop()
        {
            DispatchQueue.DispatchSync(() =>
            {
                if (_stopping) {
                    return;
                }
                
                _stopping = true;
                _reachability?.Stop();
                _reachability = null;
                if (_repl != null) {
                    Native.c4repl_stop(_repl);
                } else if(_rawStatus.level == C4ReplicatorActivityLevel.Offline) {
                    StatusChangedCallback(new C4ReplicatorStatus
                    {
                        level = C4ReplicatorActivityLevel.Stopped
                    });
                }
            });
        }

        #endregion

        #region Private Methods

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
                    current->error.code == (int)C4ErrorCode.Conflict) {
                    replicatedDocumentsContainConflict.Add(new ReplicatedDocument(current->docID.CreateString() ?? "",
                        current->flags, current->error, current->errorIsTransient));
                } else {
                    documentReplications.Add(new ReplicatedDocument(current->docID.CreateString() ?? "",
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

        #if __IOS__
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4ReplicatorValidationFunction))]
        #endif
        private static bool PullValidateCallback(FLSlice docID, FLSlice revID, C4RevisionFlags revisionFlags, FLDict* dict, void* context)
        {
            var replicator = GCHandle.FromIntPtr((IntPtr)context).Target as Replicator;
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
            var replicator = GCHandle.FromIntPtr((IntPtr)context).Target as Replicator;
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

        private static TimeSpan RetryDelay(int retryCount)
        {
            var delaySecs = 1 << Math.Min(retryCount, 30);
            return TimeSpan.FromSeconds(Math.Min(delaySecs, MaxRetryDelay.TotalSeconds));
        }

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

        private void WaitPendingConflictTasks(C4ReplicatorStatus status)
        {
            if (status.error.code == 0 && status.error.domain == 0)
                return;

            bool transient;
            if (!IsPermanentError(status.error, out transient))
                return;

            if (status.level == C4ReplicatorActivityLevel.Stopped
                || status.level == C4ReplicatorActivityLevel.Idle) {
                var array = _conflictTasks?.Keys?.ToArray();
                if (array != null) {
                    Task.WaitAll(array);
                }
            }
        }

        private void ClearRepl()
        {
            DispatchQueue.DispatchSync(() =>
            {
                Native.c4repl_free(_repl);
                _repl = null;
                _desc = null;
            });
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

        private bool IsPermanentError(C4Error error, out bool transient)
        {
            // If this is a transient error, or if I'm continuous and the error might go away with a change
            // in network (i.e. network down, hostname unknown), then go offline and retry later
            transient = Native.c4error_mayBeTransient(error) ||
                            (error.domain == C4ErrorDomain.WebSocketDomain && error.code ==
                             (int)C4WebSocketCustomCloseCode.WebSocketCloseUserTransient);

            if (!transient && !(Config.Continuous && Native.c4error_mayBeNetworkDependent(error))) {
                return true; // Nope, this is permanent
            }

            return false;
        }

        private bool HandleError(C4Error error)
        {
            if (_stopping) {
                WriteLog.To.Sync.I(Tag, "Already stopping, ignoring error...");
                return false;
            }

            bool transient;
            if(IsPermanentError(error, out transient)) {
                if (error.code > 0) {
                    WriteLog.To.Sync.I(Tag, "Permanent error encountered ({0} / {1}), giving up...", error.domain, error.code);
                }

                return false;
            }

            if (!Config.Continuous && _retryCount >= MaxOneShotRetryCount) {
                WriteLog.To.Sync.I(Tag, "Exceeded one-shot retry count, giving up...");
                return false; //Too many retries
            }

            ClearRepl();
            if (transient) {
                // On transient error, retry periodically, with exponential backoff
                var delay = RetryDelay(++_retryCount);
                WriteLog.To.Sync.I(Tag,
                    $"{this}: Transient error ({Native.c4error_getMessage(error)}); will retry in {delay}...");
                DispatchQueue.DispatchAfter(Retry, delay);
            } else {
                WriteLog.To.Sync.I(Tag,
                    $"{this}: Network error ({Native.c4error_getMessage(error)}); will retry when network changes...");
            }

            // Also retry when the network changes
            StartReachabilityObserver();
            return true;
        }

        private void OnDocEndedWithConflict(List<ReplicatedDocument> replications)
        {
            if (_disposed) {
                return;
            }

            for(int i = 0; i < replications.Count; i++) {
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
                    Retry();
                }
            });
        }

        // Must be called from within the ThreadSafety
        private void Retry()
        {
            if (_repl != null || _rawStatus.level != C4ReplicatorActivityLevel.Offline || _stopping) {
                WriteLog.To.Sync.I(Tag,
                    $"{this}: Not in a state to retry, giving up (_repl != null {_repl != null}, level {_rawStatus.level}, _stopping {_stopping}");
                return;
            }

            WriteLog.To.Sync.I(Tag, $"{this}: Retrying...");
            StartInternal();
        }

        // Must be called from within the ThreadSafety
        private void StartInternal()
        {
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

            if(Config.PushFilter!=null)
                _nativeParams.PushFilter = PushFilterCallback;
            if (Config.PullFilter != null)
                _nativeParams.PullFilter = PullValidateCallback;

            var err = new C4Error();
            var status = default(C4ReplicatorStatus);
            _stopping = false;
            _databaseThreadSafety.DoLocked(() =>
            {
                C4Error localErr;
                if (otherDB != null) {
                    _repl = Native.c4repl_newLocal(Config.Database.c4db, otherDB.c4db, _nativeParams.C4Params,
                        &localErr);
                } else {
                    _repl = Native.c4repl_new(Config.Database.c4db, addr, dbNameStr, _nativeParams.C4Params, &localErr);
                }

                err = localErr;
                if (_repl != null) {
                    Native.c4repl_start(_repl);
                    status = Native.c4repl_getStatus(_repl);
                    Config.Database.AddActiveReplication(this);
                } else {
                    status = new C4ReplicatorStatus {
                        error = err,
                        level = C4ReplicatorActivityLevel.Stopped,
                        progress = new C4Progress()
                    };
                }
            });

            scheme.Dispose();
            path.Dispose();
            host.Dispose();

            UpdateStateProperties(status);
            DispatchQueue.DispatchSync(() => StatusChangedCallback(status));
        }

        private void StartReachabilityObserver()
        {
            if (_reachability != null) {
                return;   
            }

            var remoteUrl = (Config.Target as URLEndpoint)?.Url;
            if(remoteUrl == null) {
                return;
            }

            _reachability = Service.GetInstance<IReachability>() ?? new Reachability();
            _reachability.StatusChanged += ReachabilityChanged;
            _reachability.Url = remoteUrl;
            _reachability.Start();
        }

        // Must be called from within the ThreadSafety
        private void StatusChangedCallback(C4ReplicatorStatus status)
        {
            if (_disposed) {
                return;
            }

            if (status.level == C4ReplicatorActivityLevel.Stopped) {
                if (HandleError(status.error)) {
                    status.level = C4ReplicatorActivityLevel.Offline;
                }
            } else if (status.level > C4ReplicatorActivityLevel.Connecting && status.error.code == 0) {
                _retryCount = 0;
                _reachability?.Stop();
                _reachability = null;
            }

            UpdateStateProperties(status);
            if (status.level == C4ReplicatorActivityLevel.Stopped) {
                ClearRepl();
                Config.Database.RemoveActiveReplication(this);
            }

            try {
                _statusChanged.Fire(this, new ReplicatorStatusChangedEventArgs(Status));
            } catch (Exception e) {
                WriteLog.To.Sync.W(Tag, "Exception during StatusChanged callback", e);
            }
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