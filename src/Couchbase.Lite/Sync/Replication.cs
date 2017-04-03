using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Couchbase.Lite.DB;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.Sync
{
    internal sealed unsafe class Replication : ThreadSafe, IReplication
    {
        private const string Tag = nameof(Replication);
        private C4Replicator* _repl;

        private static readonly C4ReplicatorMode[] _Modes = {
            C4ReplicatorMode.Disabled, C4ReplicatorMode.Disabled, C4ReplicatorMode.OneShot, C4ReplicatorMode.Continuous
        };

        public event EventHandler<ReplicationStatusChangedEventArgs> StatusChanged;
        public event EventHandler<ReplicationStoppedEventArgs> Stopped;
        public IDatabase Database { get; }
        public Uri RemoteUrl { get; }
        public IDatabase OtherDatabase { get; }
        public bool Push { get; set; }
        public bool Pull { get; set; }
        public bool Continuous { get; set; }
        public ReplicationStatus Status { get; private set; }
        public Exception LastError { get; private set; }

        static Replication()
        {
            WebSocketTransport.RegisterWithC4();
        }

        public Replication(IDatabase db, Uri remoteUrl, IDatabase otherDb)
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

        private static C4ReplicatorMode Mkmode(bool active, bool continuous)
        {
            return _Modes[2 * Convert.ToInt32(active) + Convert.ToInt32(continuous)];
        }

        private static void StatusChangedCallback(C4ReplicatorStatus status, object context)
        {
            var repl = context as Replication;
            repl?.DoAsync(() =>
            {
                repl.StatusChangedCallback(status);
            });
        }

        private void StatusChangedCallback(C4ReplicatorStatus status)
        {
            SetC4Status(status);

            StatusChanged?.Invoke(this, new ReplicationStatusChangedEventArgs(Status));
            if (status.level == C4ReplicatorActivityLevel.Stopped) {
                // Stopped:
                Native.c4repl_free(_repl);
                _repl = null;
                Stopped?.Invoke(this, new ReplicationStoppedEventArgs(LastError));
                (Database as Database)?.ActiveReplications.Remove(this);
            }
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
            var activity = (ReplicationActivityLevel) state.level;
            var progress = new ReplicationProgress(state.progress.completed, state.progress.total);
            Status = new ReplicationStatus(activity, progress);
            Log.To.Sync.I(Tag, $"{this} is {Status}, progress {state.progress.completed}/{state.progress.total}");
        }

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

        public void Start()
        {
            AssertSafety();
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
                    port = (ushort)RemoteUrl.Port;
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

            // Post an initial notification:
            StatusChangedCallback(status, this);
        }

        public void Stop()
        {
            AssertSafety();
            if (_repl != null) {
                Native.c4repl_stop(_repl);
            }
        }

        private void Dispose(bool finalizing)
        {
            Native.c4repl_free(_repl);
        }

        public void Dispose()
        {
            DoSync(() =>
            {
                Dispose(false);
            });
            GC.SuppressFinalize(this);
        }
    }
}
