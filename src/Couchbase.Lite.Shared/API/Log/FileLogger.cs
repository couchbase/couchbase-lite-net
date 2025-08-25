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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using Constants = Couchbase.Lite.Info.Constants;

namespace Couchbase.Lite.Logging;

/// <summary>
/// A class that describes the file configuration for the <see cref="FileLogger"/>
/// class.  These options must be set atomically so they won't take effect unless
/// a new configuration object is set on the logger.  Attempting to modify an in-use
/// configuration object will result in an exception being thrown.
/// </summary>
public sealed class LogFileConfiguration
{
    private const string Tag = nameof(LogFileConfiguration);

    /// <summary>
    /// Gets the directory that the log files are stored in.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// Gets or sets the number of rotated logs that are saved (i.e.
    /// if the value is 1, then 2 logs will be present:  the 'current'
    /// and the 'rotated')
    /// Default value is <see cref="Constants.DefaultLogFileMaxRotateCount" />
    /// </summary>
    public uint MaxRotateCount { get; init; } = Constants.DefaultLogFileMaxRotateCount;

    /// <summary>
    /// Gets or sets the max size of the log files in bytes.  If a log file
    /// passes this size then a new log file will be started.  This
    /// number is a best-effort guess and the actual size may go over slightly.
    /// Default value is <see cref="Constants.DefaultLogFileMaxSize" />
    /// </summary>
    public long MaxSize { get; init; } = Constants.DefaultLogFileMaxSize;

    /// <summary>
    /// Gets or sets whether to log in plaintext.  The default is
    /// to log in a binary encoded format that is more CPU and I/O friendly
    /// and enabling plaintext is not recommended in production.
    /// Default value is <see cref="Constants.DefaultLogFileUsePlaintext" />
    /// </summary>
    public bool UsePlaintext { get; init; } =  Constants.DefaultLogFileUsePlaintext;

    /// <summary>
    /// Constructs a file configuration object with the given directory
    /// </summary>
    /// <param name="directory">The directory that logs will be written to</param>
    public LogFileConfiguration(string directory)
    {
        Directory = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(directory), directory);
    }

    /// <summary>
    /// Constructs a file configuration object based on another one so
    /// that it may be modified
    /// </summary>
    /// <param name="other">The other configuration to copy settings from</param>
    public LogFileConfiguration(LogFileConfiguration other)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(other), other);
        Directory = other.Directory;
        MaxRotateCount = other.MaxRotateCount;
        MaxSize = other.MaxSize;
        UsePlaintext = other.UsePlaintext;
    }

    /// <summary>
    /// Constructs a file configuration object based on another one but changing
    /// the directory
    /// </summary>
    /// <param name="directory">The directory that logs will be written to</param>
    /// <param name="other">The other configuration to copy the other settings from</param>
    public LogFileConfiguration(string directory, LogFileConfiguration? other)
        : this(directory)
    {
        if (other == null) {
            return;
        }
        
        MaxRotateCount = other.MaxRotateCount;
        MaxSize = other.MaxSize;
        UsePlaintext = other.UsePlaintext;
    }
}

/// <summary>
/// A class that controls the file logging facility of
/// Couchbase Lite
/// </summary>
public sealed class FileLogger : ILogger
{
    private readonly Dictionary<LogDomain,IntPtr> _domainObjects = new Dictionary<LogDomain, IntPtr>();

    private LogFileConfiguration? _config;

    /// <summary>
    /// Gets or sets the configuration currently in use on the file logger.
    /// Note that once it is set, it can no longer be modified and doing so
    /// will throw an exception.
    /// </summary>
    public LogFileConfiguration? Config
    {
        get => _config;
        set {
            if (value == null) {
                WriteLog.To.Database.W("Logging", "Database.Log.File.Config is now null, meaning file logging is disabled.  Log files required for product support are not being generated.");
            }

            _config = value;
            UpdateConfig();
        }
    }

    /// <summary>
    /// Gets or sets the max level to log.  The set level and all
    /// levels under it will be logged (i.e. <c>Error</c> will log
    /// only errors but <c>Warning</c> will log warnings and errors)
    /// </summary>
    public LogLevel Level
    {
        get => (LogLevel)Native.c4log_binaryFileLevel();
        set {
            if (Config == null) {
                throw new InvalidOperationException("Cannot set logging level without a configuration");
            }

            Native.c4log_setBinaryFileLevel((C4LogLevel) value);
        }
    }

    /// <summary>
    /// Default Constructor
    /// </summary>
    public FileLogger()
    {
        SetupDomainObjects();
        Native.c4log_setBinaryFileLevel(C4LogLevel.None);
    }

    private unsafe void SetupDomainObjects()
    {
        _domainObjects[LogDomain.Couchbase] = (IntPtr)Native.c4log_getDomain("Couchbase", true);
        _domainObjects[LogDomain.Database] = (IntPtr)Native.c4log_getDomain("DB", true);
        _domainObjects[LogDomain.Query] = (IntPtr)Native.c4log_getDomain("Query", true);
        _domainObjects[LogDomain.Replicator] = (IntPtr)Native.c4log_getDomain("Sync", true);

        foreach (var domain in _domainObjects) {
            Native.c4log_setLevel((C4LogDomain *)domain.Value.ToPointer(),
                C4LogLevel.Debug);
        }

        foreach (var domain in new[] { 
            WriteLog.LogDomainBLIP, 
            WriteLog.LogDomainSyncBusy, 
            WriteLog.LogDomainWebSocket
        }) {
            Native.c4log_setLevel(domain, C4LogLevel.Debug);
        }
    }

    private unsafe void UpdateConfig()
    {
        if (_config != null) {
            Directory.CreateDirectory(_config.Directory);
        }

        using var dir = new C4String(_config?.Directory);
        using var header = new C4String(HTTPLogic.UserAgent);
        var options = new C4LogFileOptions
        {
            base_path = dir.AsFLSlice(),
            log_level = Config?.Directory == null ? C4LogLevel.None : (C4LogLevel) Level,
            max_rotate_count = (int)(_config?.MaxRotateCount ?? 1U),
            max_size_bytes = _config?.MaxSize ?? 1024 * 500L,
            use_plaintext = _config?.UsePlaintext ?? false,
            header = header.AsFLSlice()
        };
        LiteCoreBridge.Check(err => Native.c4log_writeToBinaryFile(options, err));
    }

    /// <inheritdoc />
    public unsafe void Log(LogLevel level, LogDomain domain, string message)
    {
        if (level < Level || !_domainObjects.TryGetValue(domain, out var domainObject)) {
            return;
        }

        Native.c4slog((C4LogDomain*)domainObject, (C4LogLevel)level, message);
    }
}