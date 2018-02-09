// 
// ReplicationActivityLevel.cs
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

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// An enum describing states for a <see cref="Replicator"/>
    /// </summary>
    public enum ReplicatorActivityLevel
    {
        /// <summary>
        /// The replication is finished or hit a fatal error
        /// </summary>
        Stopped,
        /// <summary>
        /// The replicator has detected that there is no Internet connection available
        /// </summary>
        Offline,
        /// <summary>
        /// The replicator is connecting to the remote host
        /// </summary>
        Connecting,
        /// <summary>
        /// The replication is inactive; either waiting for changes or offline
        /// because the remote host is unreachable
        /// </summary>
        Idle,
        /// <summary>
        /// The replication is actively transferring data
        /// </summary>
        Busy
    }
}
