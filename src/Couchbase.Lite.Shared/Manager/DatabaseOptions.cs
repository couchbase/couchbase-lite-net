//
// DatabaseOptions.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using Couchbase.Lite.Store;

namespace Couchbase.Lite
{
    /// <summary>
    /// Options for opening a database. All properties default to false or null.
    /// </summary>
    public sealed class DatabaseOptions
    {

        #region Constants

        /// <summary>
        /// The identifier for SQLite based storage
        /// </summary>
        public const string SQLITE_STORAGE = "SQLite";

        /// <summary>
        /// The identifier for ForestDB based storage
        /// </summary>
        public const string FORESTDB_STORAGE = "ForestDB";

        #endregion

        #region Properties

        /// <summary>
        /// Create database if it doesn't exist?
        /// </summary>
        public bool Create { get; set; }

        /// <summary>
        /// Open database read-only?
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// The underlying storage engine to use. Legal values are SQLITE_STORAGE, FORESTDB_STORAGE, 
        /// or null.
        /// * If the database is being created, the given storage engine will be used, or the default if
        ///   the value is null.
        /// * If the database exists, and the value is not null, the database will be upgraded to that
        ///   storage engine if possible. (SQLite-to-ForestDB upgrades are supported.)
        /// </summary>
        public string StorageType { get; set; }

        /// <summary>
        /// A key to encrypt the database with. If the database does not exist and is being created, it
        /// will use this key, and the same key must be given every time it's opened.  The default, null, 
        /// means the database is not encrypted.
        /// </summary>
        public ISymmetricKey EncryptionKey { get; set; }

        #endregion

    }
}

