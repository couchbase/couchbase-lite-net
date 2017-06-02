// 
// ReplicatorTarget.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// A class describing a remote endpoint for an <see cref="IReplicator"/>
    /// </summary>
    public sealed class ReplicatorTarget
    {
        #region Properties

        /// <summary>
        /// Gets the local database being replicated to (if the target endpoint
        /// is a local database)
        /// </summary>
        public Database Database { get; }

        /// <summary>
        /// Gets the URL of the target endpoint (if the target endpoint is a URL)
        /// </summary>
        public Uri Url { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a target with a url endpoint
        /// </summary>
        /// <param name="url">The url to use</param>
        public ReplicatorTarget(Uri url)
        {
            Database = null;
            Url = url;
        }

        /// <summary>
        /// Constructs a target with a local database endpoint
        /// </summary>
        /// <param name="other">The database to replicate with</param>
        public ReplicatorTarget(Database other)
        {
            Database = other;
            Url = null;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return Url != null ? Url.AbsoluteUri : Database.Name;
        }

        #endregion
    }
}
