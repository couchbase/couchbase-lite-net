// 
//  FileLogSink.cs
// 
//  Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Sync;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Constants = Couchbase.Lite.Info.Constants;

namespace Couchbase.Lite.Logging;

/// <summary>
/// A log sink for writing log messages to files on disk.  This class must be set up
/// in order to receive support for the SDK.  Each log level is written into a separate
/// file.
/// </summary>
/// <param name="level">The log level to emit (see <see cref="BaseLogSink.Level"/>)</param>
/// <param name="directory">The directory to write files to</param>
public sealed class FileLogSink(LogLevel level, string directory) : BaseLogSink(level)
{
    /// <summary>
    /// Gets the directory to write files into
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public string Directory { get; init; } = directory;

    /// <summary>
    /// Gets the maximum number of files to save per log level
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public int MaxKeptFiles { get; init; } = Constants.DefaultFileLogSinkMaxKeptFiles;

    /// <summary>
    /// Gets the maximum size of each file.  Once exceeded, a new file is started and the previous
    /// one erased until only <see cref="MaxKeptFiles"/> remain.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public long MaxSize { get; init; } = Constants.DefaultFileLogSinkMaxSize;

    /// <summary>
    /// Gets whether to write the logs in plaintext.  This increases immediate readability,
    /// but requires far more disk space per log. 
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool UsePlaintext { get; init; } = Constants.DefaultFileLogSinkUsePlaintext;

    /// <summary>
    /// Copy constructor
    /// </summary>
    /// <param name="other">The object to copy</param>
    public FileLogSink(FileLogSink other) : this(other.Level, other.Directory)
    {
        MaxKeptFiles = other.MaxKeptFiles;
        MaxSize = other.MaxSize;
    }

    /// <inheritdoc />
    protected override unsafe void WriteLog(LogLevel level, LogDomain domain, string message)
    {
        var domainObject = LogSinks.GetDomainObject(domain);
        Debug.Assert(domainObject != null);
        Native.c4slog(domainObject, (C4LogLevel)level, message);
    }

    internal static unsafe void Apply(FileLogSink? prev, FileLogSink? next)
    {
        if(ReferenceEquals(prev, next)) {
            return;
        }

        if(prev?.OnlyLevelDifferent(next) == true) {
            // Shortcut: If only level is different, just set the level and finish
            Native.c4log_setBinaryFileLevel((C4LogLevel?)next?.Level ?? C4LogLevel.None);
            return;
        }

        var maxRotateCount = 1;
        if(next != null) {
            maxRotateCount = next.MaxKeptFiles - 1;
            System.IO.Directory.CreateDirectory(next.Directory);
        }

        using var dir = new C4String(next?.Directory);
        using var header = new C4String(HTTPLogic.UserAgent);
        var options = new C4LogFileOptions
        {
            base_path = dir.AsFLSlice(),
            log_level = (C4LogLevel?)next?.Level ?? C4LogLevel.None,
            max_rotate_count = maxRotateCount,
            max_size_bytes = next?.MaxSize ?? 1024 * 500L,
            use_plaintext = next?.UsePlaintext ?? false,
            header = header.AsFLSlice()
        };
        LiteCoreBridge.Check(err => Native.c4log_writeToBinaryFile(options, err));
    }

    internal bool OnlyLevelDifferent(FileLogSink? other)
    {
        if(other == null) {
            return false;
        }

        return Directory == other.Directory
            && MaxKeptFiles == other.MaxKeptFiles
            && MaxSize == other.MaxSize
            && UsePlaintext == other.UsePlaintext;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if(obj is not FileLogSink other) {
            return false;
        }

        return Directory == other.Directory
            && MaxKeptFiles == other.MaxKeptFiles
            && MaxSize == other.MaxSize
            && UsePlaintext == other.UsePlaintext
            && Level == other.Level;
    }

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            Directory,
            MaxKeptFiles,
            MaxSize,
            UsePlaintext,
            Level);
}