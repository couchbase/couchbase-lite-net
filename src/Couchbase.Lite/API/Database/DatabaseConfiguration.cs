// 
//  DatabaseConfiguration.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// A struct containing configuration for creating or opening database data
    /// </summary>
    public sealed partial class DatabaseConfiguration
    {
        #region Constants

        private const string Tag = nameof(DatabaseConfiguration);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the <see cref="IConflictResolver"/> used to handle conflicts by default
        /// in the database to be created
        /// </summary>
        [NotNull]
        public IConflictResolver ConflictResolver { get; }

        /// <summary>
        /// Gets the directory to use when creating or opening the data
        /// </summary>
        [NotNull]
        public string Directory { get; }

        /// <summary>
        /// Gets the encryption key to use on the database
        /// </summary>
        [CanBeNull]
        public EncryptionKey EncryptionKey { get; }

        #endregion

        #region Constructors

        internal DatabaseConfiguration([NotNull]Builder builder)
        {
            ConflictResolver = builder.ConflictResolver;
            Directory = builder.Directory;
            EncryptionKey = builder.EncryptionKey;
        }

        #endregion
    }

    public sealed partial class DatabaseConfiguration
    {
        #region Nested

        public sealed class Builder
        {
            #region Variables

            [NotNull]private IConflictResolver _conflictResolver = new DefaultConflictResolver();
            [NotNull]private string _directory =  Service.GetRequiredInstance<IDefaultDirectoryResolver>().DefaultDirectory();

            #endregion

            #region Properties

            /// <summary>
            /// Gets or sets the <see cref="IConflictResolver"/> used to handle conflicts by default
            /// in the database to be created
            /// </summary>
            [NotNull]
            public IConflictResolver ConflictResolver
            {
                get => _conflictResolver;
                set => _conflictResolver = CBDebug.MustNotBeNull(Log.To.Database, Tag, "ConflictResolver", value);
            }

            /// <summary>
            /// Gets or sets the directory to use when creating or opening the data
            /// </summary>
            [NotNull]
            public string Directory
            {
                get => _directory;
                set => _directory = CBDebug.MustNotBeNull(Log.To.Database, Tag, "Directory", value);
            }

            /// <summary>
            /// Gets or sets the encryption key to use on the database
            /// </summary>
            [CanBeNull]
            public EncryptionKey EncryptionKey { get; set; }

            #endregion

            #region Public Methods

            [NotNull]
            public DatabaseConfiguration Build() => new DatabaseConfiguration(this);

            #endregion
        }

        #endregion
    }
}