// 
//  DatabaseEndpoint.cs
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

using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// A local endpoint which is another database for use with the target of
    /// a <see cref="Replicator"/>
    /// </summary>
    public sealed class DatabaseEndpoint : IEndpoint
    {
        #region Constants

        private const string Tag = nameof(DatabaseEndpoint);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the database used for this endpoint
        /// </summary>
        [NotNull]
        public Database Database { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor creating an endpoint using the given database
        /// </summary>
        /// <param name="database">The database to use in the replication</param>
        public DatabaseEndpoint([NotNull]Database database)
        {
            Database = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(database), database);
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override string ToString() => Database.ToString();

        #endregion
    }
}