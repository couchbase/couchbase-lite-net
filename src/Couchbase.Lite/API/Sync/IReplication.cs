// 
// IReplication.cs
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
using System.Collections.Generic;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// An interface describing a replication.  A replication is a transfer of data
    /// between two database endpoints.  The two endpoints are a local database, and 
    /// either another local database or a remote URL.  This API is not yet finalized.  
    /// It WILL change.
    /// </summary>
    /// <remarks>
    /// This API is not yet finalized.  It WILL change.
    /// </remarks>
    public interface IReplication : IDisposable
    {
        #region Variables

        /// <summary>
        /// An event that fires when the replication's status changes
        /// </summary>
        event EventHandler<ReplicationStatusChangedEventArgs> StatusChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether or not this replication should be continuous.
        /// Non-continuous replications stop when they finish processing their
        /// initial set of changes
        /// </summary>
        bool Continuous { get; set; }

        /// <summary>
        /// Gets the local <see cref="Database"/> associated with this replication
        /// </summary>
        Database Database { get; }

        /// <summary>
        /// Gets the most recent error associated with this replication
        /// </summary>
        Exception LastError { get; }

        ///// <summary>
        ///// Get or set options affecting replication (See <see cref="ReplicationOptionKeys" />
        ///// for a list of keys)
        ///// </summary>
        //IDictionary<string, object> Options { get; set; }

        /// <summary>
        /// Gets the remote <see cref="Database"/> being replicated to, if this is
        /// a local replication.
        /// </summary>
        Database OtherDatabase { get; }

        /// <summary>
        /// Gets or sets whether or not this replication should perform pull operations
        /// from the remote dataset
        /// </summary>
        bool Pull { get; set; }

        /// <summary>
        /// Gets or sets whether or not this replication should perform push operations
        /// from the remote dataset
        /// </summary>
        bool Push { get; set; }

        /// <summary>
        /// Gets the URL of the remote database being replicated to, if this is
        /// a network replication
        /// </summary>
        Uri RemoteUrl { get; }

        /// <summary>
        /// Gets the current status of the <see cref="IReplication"/>
        /// </summary>
        ReplicationStatus Status { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the replication
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the replication
        /// </summary>
        void Stop();

        #endregion
    }
}
