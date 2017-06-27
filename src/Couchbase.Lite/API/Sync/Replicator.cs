// 
// Replicator.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// An object that is responsible for the replication of data between two
    /// endpoints.  The replication can set up to be pull only, push only, or both
    /// (i.e. pusher and puller are no longer separate) between a database and a URL
    /// or a database and another database on the same filesystem.
    /// </summary>
    public sealed unsafe class Replicator
    {
        #region Constants

        private const int MaxOneShotRetryCount = 2;
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(10);

        private static readonly C4ReplicatorMode[] Modes = {
            C4ReplicatorMode.Disabled, C4ReplicatorMode.Disabled, C4ReplicatorMode.OneShot, C4ReplicatorMode.Continuous
        };

        private const string Tag = nameof(Replicator);

        #endregion

        #region Variables

        private readonly ReplicatorConfiguration _config;
        private readonly ThreadSafety _threadSafety = new ThreadSafety(true);

        /// <summary>
        /// An event that is fired when the replicator changes its status for reasons like
        /// processing more data or changing its condition
        /// </summary>
        public event EventHandler<ReplicationStatusChangedEventArgs> StatusChanged;

        private ReplicatorParameters _nativeParams;
        private string _desc;
        private Exception _lastError;
        private C4ReplicatorStatus _rawStatus;
        private C4Replicator* _repl;
        private IDictionary<string, object> _responseHeaders;
        private int _retryCount;
        private ReplicationStatus _status;
        private IReachability _reachability;
        private readonly SerialQueue _dispatchQueue = new SerialQueue();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the configuration that was used to create this Replicator
        /// </summary>
        public ReplicatorConfiguration Config => ReplicatorConfiguration.Clone(_config);

        /// <summary>
        /// Gets the most recent error associated with this replication
        /// </summary>
        public Exception LastError
        {
            get => _threadSafety.LockedForRead(() => _lastError);
            set => _threadSafety.LockedForWrite(() => _lastError = value);
        }


        /// <summary>
        /// Gets the current status of the <see cref="Replicator"/>
        /// </summary>
        public ReplicationStatus Status
        {
            get => _threadSafety.LockedForRead(() => _status);
            private set => _threadSafety.LockedForWrite(() => _status = value);
        }

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
            _config = ReplicatorConfiguration.Clone(config);
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

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts the replication
        /// </summary>
        public void Start()
        {
            _threadSafety.LockedForWrite(() =>
            {
                if (_repl != null) {
                    Log.To.Sync.W(Tag, $"{this} has already started");
                    return;
                }

                Log.To.Sync.I(Tag, $"{this}: Starting");
                _retryCount = 0;
                StartInternal();
            });
        }

        /// <summary>
        /// Stops the replication
        /// </summary>
        public void Stop()
        {
            _threadSafety.LockedForWrite(() =>
            {
                if (_repl != null) {
                    Native.c4repl_stop(_repl);
                }
            });
        }

        #endregion

        #region Private Methods

        private static C4ReplicatorMode Mkmode(bool active, bool continuous)
        {
            return Modes[2 * Convert.ToInt32(active) + Convert.ToInt32(continuous)];
        }

        private static TimeSpan RetryDelay(int retryCount)
        {
            var delaySecs = 1 << Math.Min(retryCount, 30);
            return TimeSpan.FromSeconds(Math.Min(delaySecs, MaxRetryDelay.TotalSeconds));
        }

        private static void ErrorCallback(bool pushing, string docID, C4Error error, bool transient, object context)
        {
            
        }

        private static void StatusChangedCallback(C4ReplicatorStatus status, object context)
        {
            var repl = context as Replicator;
            repl?._dispatchQueue.DispatchAsync(() =>
            {
                repl.StatusChangedCallback(status);
            });
        }

        private static void ValidateCallback(string docID, IntPtr body, object context)
        {
            
        }

        private void ClearRepl()
        {
            Native.c4repl_free(_repl);
            _repl = null;
            _desc = null;
        }

        private void Dispose(bool finalizing)
        {
            if (!finalizing) {
                _reachability.Stop();
            }

            Native.c4repl_free(_repl);
			_nativeParams?.Dispose();
        }

        private bool HandleError(C4Error error)
        {
            // If this is a transient error, or if I'm continuous and the error might go away with a change
            // in network (i.e. network down, hostname unknown), then go offline and retry later
            var transient = Native.c4error_mayBeTransient(error);
            if (!transient && !(_config.Continuous && Native.c4error_mayBeNetworkDependent(error))) {
                return false; // Nope, this is permanent
            }

            if (!_config.Continuous && _retryCount >= MaxOneShotRetryCount) {
                return false; //Too many retries
            }

            ClearRepl();
            if (transient) {
                // On transient error, retry periodically, with exponential backoff
                var delay = RetryDelay(++_retryCount);
                Log.To.Sync.I(Tag,
                    $"{this}: Transient error ({Native.c4error_getMessage(error)}); will retry in {delay}...");
                _dispatchQueue.DispatchAfter(Retry, delay);
            } else {
                Log.To.Sync.I(Tag,
                    $"{this}: Network error ({Native.c4error_getMessage(error)}); will retry when network changes...");
            }

            // Also retry when the network changes
            StartReachabilityObserver();
            return true;
        }

        private void StartReachabilityObserver()
        {
            if (_reachability != null) {
                return;   
            }

            _reachability = Service.Provider.GetService<IReachabilityFactory>()?.Create() ?? new Reachability();
            _reachability.StatusChanged += ReachabilityChanged;
            _reachability.Start(_dispatchQueue);
        }

        private void ReachabilityChanged(object sender, NetworkReachabilityChangeEventArgs e)
        {
            if (_repl == null && e.Status == NetworkReachabilityStatus.Reachable) {
                Log.To.Sync.I(Tag, $"{this}: Server may now be reachable; retrying...");
                _retryCount = 0;
                Retry();
            }
        }

        private void Retry()
        {
            if (_repl != null || _rawStatus.level != C4ReplicatorActivityLevel.Offline) {
                return;
            }

            Log.To.Sync.I(Tag, $"{this}: Retrying...");
            StartInternal();
        }

        private void StartInternal()
        {
            _desc = ToString(); // Cache this; it may be called a lot when logging

            // Target:
            var addr = new C4Address();
            var scheme = new C4String();
            var host = new C4String();
            var path = new C4String();
            Database otherDB = null;
            var remoteUrl = _config.RemoteUrl;
            string dbNameStr = null;
            if (remoteUrl != null) {
                string pathStr = String.Concat(remoteUrl.Segments.Take(remoteUrl.Segments.Length - 1));
                dbNameStr = remoteUrl.Segments.Last().TrimEnd('/');
                scheme = new C4String(remoteUrl.Scheme);
                host = new C4String(remoteUrl.Host);
                path = new C4String(pathStr);
                addr.scheme = scheme.AsC4Slice();
                addr.hostname = host.AsC4Slice();
                addr.port = (ushort) remoteUrl.Port;
                addr.path = path.AsC4Slice();
            } else {
                otherDB = _config.OtherDB;
            }

            var options = _config.Options ?? new ReplicatorOptionsDictionary();
            var userInfo = remoteUrl?.UserInfo?.Split(':');
            if (userInfo?.Length == 2 && options.Auth == null) {
                _config.Authenticator = new BasicAuthenticator(userInfo[0], userInfo[1]);
            }

            _config.Authenticator?.Authenticate(_config.Options);

            options.Freeze();
            var push = _config.ReplicatorType.HasFlag(ReplicatorType.Push);
            var pull = _config.ReplicatorType.HasFlag(ReplicatorType.Pull);
            var continuous = _config.Continuous;
            _nativeParams = new ReplicatorParameters(Mkmode(push, continuous), Mkmode(pull, continuous), options, ValidateCallback, 
                ErrorCallback, StatusChangedCallback, this);

            C4Error err;
            _repl = Native.c4repl_new(_config.Database.c4db, addr, dbNameStr, otherDB != null ? otherDB.c4db : null,
                _nativeParams.C4Params, &err);

            scheme.Dispose();
            path.Dispose();
            host.Dispose();


            C4ReplicatorStatus status;
            if (_repl != null) {
                status = Native.c4repl_getStatus(_repl);
                _config.Database.ActiveReplications.Add(this);
            } else {
                status = new C4ReplicatorStatus {
                    error = err,
                    level = C4ReplicatorActivityLevel.Stopped,
                    progress = new C4Progress()
                };
            }

            UpdateStateProperties(status);
            StatusChangedCallback(status, this);

        }

        private void StatusChangedCallback(C4ReplicatorStatus status)
        {
            if (_responseHeaders == null && _repl != null) {
                var h = Native.c4repl_getResponseHeaders(_repl);
                _responseHeaders =
                    FLSliceExtensions.ToObject(NativeRaw.FLValue_FromTrustedData((FLSlice) h)) as
                        IDictionary<string, object>;
            }

            if (status.level == C4ReplicatorActivityLevel.Stopped) {
                if (HandleError(status.error)) {
                    status.level = C4ReplicatorActivityLevel.Offline;
                }
            } else if (status.level > C4ReplicatorActivityLevel.Connecting) {
                _retryCount = 0;
            }

            UpdateStateProperties(status);
            StatusChanged?.Invoke(this, new ReplicationStatusChangedEventArgs(Status, LastError));

            if (status.level == C4ReplicatorActivityLevel.Stopped) {
                ClearRepl();
                _config.Database.ActiveReplications.Remove(this);
            }
        }

        private void UpdateStateProperties(C4ReplicatorStatus state)
        {
            Exception error = null;
            if (state.error.code > 0) {
                error = new LiteCoreException(state.error);
            }

            if (LastError != error) {
                LastError = error;
            }

            _rawStatus = state;

            ReplicatorActivityLevel level;
            switch (state.level) {
                case C4ReplicatorActivityLevel.Stopped:
                    level = ReplicatorActivityLevel.Stopped;
                    break;
                case C4ReplicatorActivityLevel.Idle:
                case C4ReplicatorActivityLevel.Offline:
                    level = ReplicatorActivityLevel.Idle;
                    break;
                default:
                    level = ReplicatorActivityLevel.Busy;
                    break;
            }
            
            var progress = new ReplicationProgress(state.progress.completed, state.progress.total);
            Status = new ReplicationStatus(level, progress);
            Log.To.Sync.I(Tag, $"{this} is {state.level}, progress {state.progress.completed}/{state.progress.total}");
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
            if (_config.ReplicatorType.HasFlag(ReplicatorType.Pull)) {
                sb.Append("<");
            }

            if (_config.Continuous) {
                sb.Append("*");
            }

            if (_config.ReplicatorType.HasFlag(ReplicatorType.Push)) {
                sb.Append(">");
            }

            return $"{GetType().Name}[{sb} {_config.Target}]";
        }

        #endregion
    }
}
