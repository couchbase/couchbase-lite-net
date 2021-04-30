﻿// 
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
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// A struct containing configuration for creating or opening database data
    /// </summary>
    public sealed class DatabaseConfiguration
    {
        #region Constants

        private const string Tag = nameof(DatabaseConfiguration);

        #endregion

        #region Variables

        [NotNull] private string _directory =
            Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory();
      
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the directory to use when creating or opening the data
        /// </summary>
        [NotNull]
        public string Directory
        {
            get => _directory;
            init => _directory = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, "Directory", value);
        }

        /// <summary>
        /// Experiment API. Enable version vector.
        /// </summary>
        /// <remarks>
        /// If the enableVersionVector is set to true, the database will use version vector instead of
        /// using revision tree.When enabling version vector on an existing database, the database
        /// will be upgraded to use the revision tree while the database is opened.
        /// NOTE:
        /// 1. The database that uses version vector cannot be downgraded back to use revision tree.
        /// 2. The current version of Sync Gateway doesn't support version vector so the syncronization
        /// with Sync Gateway will not be working.
        /// </remarks>
        internal bool EnableVersionVector => false;

        #if COUCHBASE_ENTERPRISE
        /// <summary>
        /// Gets or sets the encryption key to use on the database
        /// </summary>
        [CanBeNull]
        public EncryptionKey EncryptionKey { get; init; }
        #endif

        #endregion

        /// <summary>
        /// Default constructor
        /// </summary>
        public DatabaseConfiguration()
        {

        }
    }
}

namespace System.Runtime.CompilerServices
{
    // This is a hack to get C# 9 to work with none .Net 5+
    // Should be able to remove once the projec is updated to .Net 5+
    internal static class IsExternalInit { }
}