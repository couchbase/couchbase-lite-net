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

        [NotNull] private readonly Freezer _freezer = new Freezer();

        [NotNull] private string _directory =
            Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory();

        #if COUCHBASE_ENTERPRISE
        private EncryptionKey _encryptionKey;
        #endif

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the directory to use when creating or opening the data
        /// </summary>
        [NotNull]
        public string Directory
        {
            get => _directory;
            set => _freezer.SetValue(ref _directory, CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, "Directory", value));
            //init => _directory = string.IsNullOrWhiteSpace(value)
            //    ? CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, "Directory", value)
            //    : value;
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
        public EncryptionKey EncryptionKey
        {
            get => _encryptionKey;
            set => _freezer.SetValue(ref _encryptionKey, value);
        }
        #endif

        #endregion

        /// <summary>
        /// Default constructor
        /// </summary>
        public DatabaseConfiguration()
        {

        }


        internal DatabaseConfiguration(bool frozen)
        {
            if (frozen) {
                _freezer.Freeze("Cannot modify a DatabaseConfiguration that is currently in use");
            }
        }

        #region Internal Methods

        [NotNull]
        internal DatabaseConfiguration Freeze()
        {
            var retVal = new DatabaseConfiguration
            {
                Directory = Directory,
            };

            #if COUCHBASE_ENTERPRISE
            retVal.EncryptionKey = EncryptionKey;
            #endif

            retVal._freezer.Freeze("Cannot modify a DatabaseConfiguration that is currently in use");
            return retVal;
        }

        #endregion
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}