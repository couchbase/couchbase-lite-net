//
//  DatabaseConfiguration.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Lite
{
    /// <summary>
    /// A struct containing configuration for creating or opening database data
    /// </summary>
    public struct DatabaseConfiguration
    {
        private string _directory;

        /// <summary>
        /// Gets or sets the <see cref="IConflictResolver"/> used to handle conflicts by default
        /// in the database to be created
        /// </summary>
        public IConflictResolver ConflictResolver { get; set; }

        /// <summary>
        /// Gets or sets the directory to use when creating or opening the data
        /// </summary>
        public string Directory
        {
            get => _directory ?? Service.Provider.TryGetRequiredService<IDefaultDirectoryResolver>()
                       .DefaultDirectory();
            set => _directory = value;
        }

        /// <summary>
        /// Gets or sets the encryption key to use on the database
        /// </summary>
        public IEncryptionKey EncryptionKey { get; set; }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">The instance to copy from</param>
        public DatabaseConfiguration(DatabaseConfiguration other)
        {
            ConflictResolver = other.ConflictResolver;
            _directory = other._directory;
            EncryptionKey = other.EncryptionKey;
        }
    }
}