// 
// Replication.cs
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
using System.Linq;
using System.Text;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.Sync
{
    internal sealed unsafe class Replication : IReplication
    {
        #region Constants

        private static readonly C4ReplicatorMode[] Modes = {
            C4ReplicatorMode.Disabled, C4ReplicatorMode.Disabled, C4ReplicatorMode.OneShot, C4ReplicatorMode.Continuous
        };

        private const string Tag = nameof(Replication);

        #endregion

        #region Variables

        public event EventHandler<ReplicationStatusChangedEventArgs> StatusChanged;
        private C4Replicator* _repl;
        private ThreadSafety _threadSafety = new ThreadSafety();

        #endregion

        #region Properties

        public bool Continuous { get; set; }
        public Database Database { get; }
        public Database OtherDatabase { get; }
        public bool Pull { get; set; }
        public bool Push { get; set; }
        public Uri RemoteUrl { get; }

        public Exception LastError
        {
            get => _threadSafety.DoLocked(() => _lastError);
            set => _threadSafety.DoLocked(() => _lastError = value);
        }
        private Exception _lastError;


        public ReplicationStatus Status
        {
            get => _threadSafety.DoLocked(() => _status);
            private set => _threadSafety.DoLocked(() => _status = value);
        }
        private ReplicationStatus _status;

        #endregion

        #region Constructors

        static Replication()
        {
            WebSocketTransport.RegisterWithC4();
        }

        public Replication(Database db, Uri remoteUrl, Database otherDb)
        {
            Database = db;
            RemoteUrl = remoteUrl;
            OtherDatabase = otherDb;
            Push = Pull = true;
        }

        ~Replication()
        {
            Dispose(true);
        }

        #endregion

        #region Private Methods

        private static C4ReplicatorMode Mkmode(bool active, bool continuous)
        {
            return Modes[2 * Convert.ToInt32(active) + Convert.ToInt32(continuous)];
        }

        private static void StatusChangedCallback(C4ReplicatorStatus status, object context)
        {
            var repl = context as Replication;
            repl?.StatusChangedCallback(status);
        }

        private void Dispose(bool finalizing)
        {
            Native.c4repl_free(_repl);
        }

        private void SetC4Status(C4ReplicatorStatus state)
        {
            Exception error = null;
            if (state.error.code > 0) {
                error = new LiteCoreException(state.error);
            }

            if (LastError != error) {
                LastError = error;
            }

            //NOTE: ReplicationStatus values need to match C4ReplicatorActivityLevel!
            var activity = (ReplicationActivityLevel)state.level;
            var progress = new ReplicationProgress(state.progress.completed, state.progress.total);
            Status = new ReplicationStatus(activity, progress);
            Log.To.Sync.I(Tag, $"{this} is {state.level}, progress {state.progress.completed}/{state.progress.total}");
        }

        private void StatusChangedCallback(C4ReplicatorStatus status)
        {
            SetC4Status(status);

            StatusChanged?.Invoke(this, new ReplicationStatusChangedEventArgs(Status));
            if (status.level == C4ReplicatorActivityLevel.Stopped) {
                // Stopped:
                Native.c4repl_free(_repl);
                _repl = null;
                (Database as Database)?.ActiveReplications.Remove(this);
            }
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            var sb = new StringBuilder(3, 3);
            if (Pull) {
                sb.Append("<");
            }

            if (Continuous) {
                sb.Append("*");
            }

            if (Push) {
                sb.Append(">");
            }

            var other = RemoteUrl?.AbsoluteUri ?? OtherDatabase.Name;
            return $"{GetType().Name}[{sb} {other}]";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region IReplication

        public void Start()
        {
            C4ReplicatorStatus? statusHolder = null;
            _threadSafety.DoLocked(() =>
            {
                if (_repl != null) {
                    Log.To.Sync.W(Tag, $"{this} has already started");
                    return;
                }

                if (!Push && !Pull) {
                    throw new InvalidOperationException("Replication must be either push or pull, or both");
                }

                string pathStr = null;
                string dbNameStr = null;
                if (RemoteUrl != null) {
                    pathStr = String.Concat(RemoteUrl.Segments.Take(RemoteUrl.Segments.Length - 1));
                    dbNameStr = RemoteUrl.Segments.Last().TrimEnd('/');
                }

                var database = Database as Database;
                var otherDatabase = OtherDatabase as Database;
                if (database == null) {
                    throw new NotSupportedException("Custom IDatabase not supported in Replication");
                }

                C4Error err;
                using (var scheme = new C4String(RemoteUrl?.Scheme))
                using (var host = new C4String(RemoteUrl?.Host))
                using (var path = new C4String(pathStr)) {
                    ushort port = 0;
                    if (RemoteUrl != null) {
                        port = (ushort) RemoteUrl.Port;
                    }

                    var addr = new C4Address {
                        scheme = scheme.AsC4Slice(),
                        hostname = host.AsC4Slice(),
                        port = port,
                        path = path.AsC4Slice()
                    };

                    var callback = new ReplicatorStateChangedCallback(StatusChangedCallback, this);

                    var otherDb = otherDatabase == null ? null : otherDatabase.c4db;
                    _repl = Native.c4repl_new(database.c4db, addr, dbNameStr, otherDb, Mkmode(Push, Continuous),
                        Mkmode(Pull, Continuous), callback, &err);
                }

                C4ReplicatorStatus status;
                if (_repl != null) {
                    status = Native.c4repl_getStatus(_repl);
                    database.ActiveReplications.Add(this);
                } else {
                    status = new C4ReplicatorStatus {
                        level = C4ReplicatorActivityLevel.Stopped,
                        progress = new C4Progress(),
                        error = err
                    };
                }

                SetC4Status(status);
                statusHolder = status;
            });

            if (statusHolder.HasValue) {
                // Post an initial notification:
                StatusChangedCallback(statusHolder.Value, this);
            }
        }

        public void Stop()
        {
            _threadSafety.DoLocked(() =>
            {
                if (_repl != null) {
                    Native.c4repl_stop(_repl);
                }
            });
        }

        #endregion
    }
}
