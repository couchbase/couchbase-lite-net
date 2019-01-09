// 
//  FileLogger.cs
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
using System.IO;
using System.Runtime.InteropServices;

using Couchbase.Lite.DI;
using Couchbase.Lite.Interop;
using Couchbase.Lite.Sync;

using JetBrains.Annotations;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// A class that controls the file logging facility of
    /// Couchbase Lite
    /// </summary>
    public sealed class FileLogger : ILogger
    {
        #region Variables

        [NotNull]
        private readonly Dictionary<LogDomain,IntPtr> _domainObjects = new Dictionary<LogDomain, IntPtr>();

        [NotNull]private string _directory;
        private bool _hasConfigChanges;
        private int _maxRotateCount;
        private long _maxSize;
        private bool _usePlaintext;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the directory that the log files are stored in
        /// </summary>
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

        /// <summary>
        /// Gets or sets the number of rotated logs that are saved (i.e.
        /// if the value is 1, then 2 logs will be present:  the 'current'
        /// and the 'rotated')
        /// </summary>
        public int MaxRotateCount
        {
            get => _maxRotateCount;
            set => SetAndReact(ref _maxRotateCount, value);
        }

        /// <summary>
        /// Gets or sets the max size of the log files in bytes.  If a log file
        /// passes this size then a new log file will be started.  This
        /// number is a best effort and the actual size may go over slightly.
        /// </summary>
        public long MaxSize
        {
            get => _maxSize;
            set => SetAndReact(ref _maxSize, value);
        }

        /// <summary>
        /// Gets or sets whether or not to log in plaintext.  The default is
        /// to log in a binary encoded format that is more CPU and I/O friendly
        /// and enabling plaintext is not recommended in production.
        /// </summary>
        public bool UsePlaintext
        {
            get => _usePlaintext;
            set => SetAndReact(ref _usePlaintext, value);
        }

        /// <summary>
        /// Gets or sets the max level to log.  The set level and all
        /// levels under it will be logged (i.e. <c>Error</c> will log
        /// only errors but <c>Warning</c> will log warnings and errors)
        /// </summary>
        public LogLevel Level
        {
            get => (LogLevel)Native.c4log_binaryFileLevel();
            set => Native.c4log_setBinaryFileLevel((C4LogLevel) value);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public FileLogger()
        {
            _maxRotateCount = 1;
            _maxSize = 1024 * 500;
            SetupDomainObjects();
            Level = LogLevel.Info;
            _directory = DefaultDirectory();
            System.IO.Directory.CreateDirectory(_directory);
            UpdateConfig();
        }

        #endregion

        #region Private Methods

        [NotNull]
        private static string DefaultDirectory()
        {
            return Path.Combine(Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory(),
                "Logs") + Path.DirectorySeparatorChar;
        }

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

        private unsafe void UpdateConfig()
        {
            using (var dir = new C4String(Directory))
            using (var header = new C4String(HTTPLogic.UserAgent)) {
                var options = new C4LogFileOptions
                {
                    base_path = dir.AsFLSlice(),
                    log_level = (C4LogLevel) Level,
                    max_rotate_count = MaxRotateCount,
                    max_size_bytes = MaxSize,
                    use_plaintext = UsePlaintext,
                    header = header.AsFLSlice()
                };
                LiteCoreBridge.Check(err => Native.c4log_writeToBinaryFile(options, err));
                _hasConfigChanges = false;
            }
        }

        #endregion

        #region ILogger

        /// <inheritdoc />
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
