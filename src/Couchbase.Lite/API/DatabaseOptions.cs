//
//  DatabaseOptions.cs
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


namespace Couchbase.Lite
{
    /// <summary>
    /// A struct containing options for creating or opening database data
    /// </summary>
    public struct DatabaseOptions
    {
        /// <summary>
        /// The default set of options (useful to use as a starting point)
        /// </summary>
        public static readonly DatabaseOptions Default = new DatabaseOptions();

        /// <summary>
        /// Gets or sets the directory to use when creating or opening the data
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// Gets or sets the encryption key to use on the database
        /// </summary>
        public IEncryptionKey EncryptionKey { get; set; }

        /// <summary>
        /// Gets or sets whether or not this database is readonly.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Gets or sets whether or not to add extra checks for thread safety to track
        /// down concurrency issues
        /// </summary>
        public bool CheckThreadSafety { get; set; }
    }
}
