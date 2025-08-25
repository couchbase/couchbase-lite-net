// 
//  DatabaseConfiguration.cs
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

using Couchbase.Lite.DI;
using Couchbase.Lite.Info;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

#if NET8_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace Couchbase.Lite
{
    /// <summary>
    /// A record containing configuration for creating or opening database data
    /// </summary>
    public sealed record DatabaseConfiguration
    {
        private const string Tag = nameof(DatabaseConfiguration);

        private readonly string? _directory;

        /// <summary>
        /// Gets or sets the directory to use when creating or opening the data
        /// </summary>
        public string Directory
        {
            get => _directory ?? Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory();
            init => _directory = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, "Directory", value);
        }

        /// <summary>
        /// There is a very small (though non-zero) chance that a power 
        /// failure at just the wrong time could cause the most recently
        /// committed transaction's changes to be lost. This would cause the
        /// database to appear as it did immediately before that transaction.
        /// Setting this mode true ensures that an operating system crash or
        /// power failure will not cause the loss of any data.  FULL
        /// synchronous is very safe, but it is also dramatically slower.
        /// </summary>
        /// <returns>A boolean representing whether full sync is enabled</returns>
        public bool FullSync { get; init; } = Constants.DefaultDatabaseFullSync;

        /// <summary>
        /// Hint for enabling or disabling memory-mapped I/O. 
        /// Disabling it may affect database performance.
        /// Typically, there is no need to modify this setting.
        /// </summary>
        /// <remarks>
        /// [!NOTE]
        /// Memory-mapped I/O is always disabled to prevent database
        /// corruption on macOS. As a result, this configuration is not
        /// supported on the macOS platform.
        /// </remarks>
#if NET6_0_OR_GREATER
            [UnsupportedOSPlatform("osx")]
            [UnsupportedOSPlatform("maccatalyst")]
#endif
        public bool MmapEnabled { get; init; } = Constants.DefaultDatabaseMmapEnabled;

        #if COUCHBASE_ENTERPRISE
        /// <summary>
        /// Gets or sets the encryption key to use on the database
        /// </summary>
        public EncryptionKey? EncryptionKey { get; init; }
        #endif
    }
}

namespace System.Runtime.CompilerServices
{
    // https://developercommunity.visualstudio.com/t/error-cs0518-predefined-type-systemruntimecompiler/1244809
    internal static class IsExternalInit { }
}