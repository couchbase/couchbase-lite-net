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

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// An interface describing a replication.  A replication is a transfer of data
    /// between two database endpoints.  The two endpoints are a local database, and 
    /// either another local database or a remote URL.
    /// </summary>
    public interface IReplication : IThreadSafe, IDisposable
    {
        #region Variables

        /// <summary>
        /// An event that fires when the replication's status changes
        /// </summary>
        event EventHandler<ReplicationStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// An event that fires when the replication stops
        /// </summary>
        event EventHandler<ReplicationStoppedEventArgs> Stopped;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether or not this replication should be continuous.
        /// Non-continuous replications stop when they finish processing their
        /// initial set of changes
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        bool Continuous { get; set; }

        /// <summary>
        /// Gets the local <see cref="IDatabase"/> associated with this replication
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        IDatabase Database { get; }

        /// <summary>
        /// Gets the most recent error associated with this replication
        /// </summary>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        Exception LastError { get; }

        /// <summary>
        /// Gets the remote <see cref="IDatabase"/> being replicated to, if this is
        /// a local replication.
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        IDatabase OtherDatabase { get; }

        /// <summary>
        /// Gets or sets whether or not this replication should perform pull operations
        /// from the remote dataset
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        bool Pull { get; set; }

        /// <summary>
        /// Gets or sets whether or not this replication should perform push operations
        /// from the remote dataset
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        bool Push { get; set; }

        /// <summary>
        /// Gets the URL of the remote database being replicated to, if this is
        /// a network replication
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        Uri RemoteUrl { get; }

        /// <summary>
        /// Gets the current status of the <see cref="IReplication"/>
        /// </summary>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        ReplicationStatus Status { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the replication
        /// </summary>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        void Start();

        /// <summary>
        /// Stops the replication
        /// </summary>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        void Stop();

        #endregion
    }
}
