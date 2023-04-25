﻿// 
// ReplicatorStatus.cs
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
    /// A struct describing the current status of a <see cref="Replicator"/>
    /// </summary>
    public struct ReplicatorStatus
    {
        /// <summary>
        /// Gets the current state of the replication (i.e. whether or not it is
        /// actively processing changes)
        /// </summary>
        public ReplicatorActivityLevel Activity { get; }

        /// <summary>
        /// Gets the current progress of the replication
        /// </summary>
        public ReplicatorProgress Progress { get; }

        /// <summary>
        /// Gets the last error that occurred, if any
        /// </summary>
        public Exception? Error { get; }

        internal ReplicatorStatus(ReplicatorActivityLevel activity, ReplicatorProgress progress, Exception? error)
        {
            Activity = activity;
            Progress = progress;
            Error = error;
        }
    }
}
