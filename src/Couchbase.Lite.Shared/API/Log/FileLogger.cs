using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Couchbase.Lite.DI;
using Couchbase.Lite.Interop;

using JetBrains.Annotations;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.Logging
{
    public sealed class FileLogger : ILogger
    {
        #region Variables

        [NotNull]private string _directory;
        private int _maxRotateCount;
        private long _maxSize;
        private bool _usePlaintext;
        private bool _hasConfigChanges;

        [NotNull]
        private readonly Dictionary<LogDomain,IntPtr> _domainObjects = new Dictionary<LogDomain, IntPtr>();

        #endregion

        #region Properties

        public string Directory
        {
            get => _directory;
            set {
                SetAndReact(ref _directory, value ?? DefaultDirectory());
                System.IO.Directory.CreateDirectory(_directory);

                // This one should update immediately since it won't affect rotation
                UpdateConfig();
            } 
        }

        public int MaxRotateCount
        {
            get => _maxRotateCount;
            set => SetAndReact(ref _maxRotateCount, value);
        }

        public long MaxSize
        {
            get => _maxSize;
            set => SetAndReact(ref _maxSize, value);
        }

        public bool UsePlaintext
        {
            get => _usePlaintext;
            set => SetAndReact(ref _usePlaintext, value);
        }

        public LogLevel Level
        {
            get => (LogLevel)Native.c4log_binaryFileLevel();
            set => Native.c4log_setBinaryFileLevel((C4LogLevel) value);
        }

        #endregion

        #region Constructors

        public FileLogger()
        {
            _maxRotateCount = 1;
            _maxSize = 1024 * 500;
            SetupDomainObjects();
            Level = LogLevel.Info;
            _directory = DefaultDirectory();
            UpdateConfig();
        }

        private unsafe void SetupDomainObjects()
        {
            var bytes = (byte *)Marshal.StringToHGlobalAnsi("Couchbase");
            _domainObjects[LogDomain.Couchbase] = (IntPtr)Native.c4log_getDomain(bytes, true);

            bytes = (byte *)Marshal.StringToHGlobalAnsi("DB");
            _domainObjects[LogDomain.Database] = (IntPtr)Native.c4log_getDomain(bytes, true);

            bytes = (byte *)Marshal.StringToHGlobalAnsi("Query");
            _domainObjects[LogDomain.Query] = (IntPtr)Native.c4log_getDomain(bytes, true);

            bytes = (byte *)Marshal.StringToHGlobalAnsi("Sync");
            _domainObjects[LogDomain.Replicator] = (IntPtr)Native.c4log_getDomain(bytes, true);

            foreach (var domain in _domainObjects) {
                Native.c4log_setLevel((C4LogDomain *)domain.Value.ToPointer(),
                    C4LogLevel.Debug);
            }
        }

        #endregion

        #region Private Methods

        private void SetAndReact<T>(ref T storage, T value)
        {
            if (storage?.Equals(value) == true) {
                return;
            }

            storage = value;
            // Defer the actual config change to later to avoid excess
            // log rotation on essentially empty logs
            _hasConfigChanges = true;
        }

        private unsafe void UpdateConfig()
        {
            using (var dir = new C4String(Directory)) {
                var options = new C4LogFileOptions
                {
                    base_path = dir.AsFLSlice(),
                    log_level = (C4LogLevel) Level,
                    max_rotate_count = MaxRotateCount,
                    max_size_bytes = MaxSize,
                    use_plaintext = UsePlaintext
                };
                LiteCoreBridge.Check(err => Native.c4log_writeToBinaryFile(options, err));
                _hasConfigChanges = false;
            }
        }

        [NotNull]
        private static string DefaultDirectory()
        {
            return Path.Combine(Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory(),
                "Logs") + Path.DirectorySeparatorChar;
        }

        #endregion

        #region ILogger

        public unsafe void Log(LogLevel level, LogDomain domain, string message)
        {
            if (_hasConfigChanges) {
                // Log is only called in one place in the codebase, and it will
                // handle the potential exception here
                UpdateConfig();
            }

            if (level < Level || !_domainObjects.ContainsKey(domain)) {
                return;
            }

            Native.c4slog((C4LogDomain*)_domainObjects[domain], (C4LogLevel)level, message);
        }

        #endregion
    }
}
