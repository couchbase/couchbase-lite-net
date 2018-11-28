using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Couchbase.Lite.DI;
using Couchbase.Lite.Interop;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.Logging
{
    public sealed class FileLogger : ILogger
    {
        #region Variables

        private string _directory;
        private LogLevel _level;
        private int _maxRotateCount;
        private long _maxSize;
        private bool _usePlaintext;
        private readonly Dictionary<LogDomain,IntPtr> _domainObjects = new Dictionary<LogDomain, IntPtr>();

        #endregion

        #region Properties

        public string Directory
        {
            get => _directory;
            set => SetAndReact(ref _directory, value ?? DefaultDirectory());
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
            get => _level;
            set => SetAndReact(ref _level, value);
        }

        #endregion

        #region Constructors

        public FileLogger()
        {
            Directory = DefaultDirectory();
            MaxRotateCount = 1;
            MaxSize = 1024;
            Level = LogLevel.Info;
            SetupDomainObjects();
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
        }

        #endregion

        #region Private Methods

        private unsafe void SetAndReact<T>(ref T storage, T value)
        {
            if (storage.Equals(value)) {
                return;
            }

            storage = value;
            using (var dir = new C4String(Path.Combine(Directory, "cbl_"))) {
                var options = new C4LogFileOptions
                {
                    base_path = dir.AsFLSlice(),
                    log_level = (C4LogLevel) Level,
                    max_rotate_count = MaxRotateCount,
                    max_size_bytes = MaxSize,
                    use_plaintext = UsePlaintext
                };
                LiteCoreBridge.Check(err => Native.c4log_writeToBinaryFile(options, err));
            }
        }

        private static string DefaultDirectory()
        {
            return Path.Combine(Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory(),
                "Logs");
        }

        #endregion

        #region ILogger

        public unsafe void Log(LogLevel level, LogDomain domain, string message)
        {
            if (level > Level || !_domainObjects.ContainsKey(domain)) {
                return;
            }

            Native.c4slog((C4LogDomain*)_domainObjects[domain], (C4LogLevel)level, message);
        }

        #endregion
    }
}
