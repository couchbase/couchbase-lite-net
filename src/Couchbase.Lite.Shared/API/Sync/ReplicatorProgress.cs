// 
// ReplicationProgress.cs
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
    /// A struct describing the current progress of a <see cref="Replicator"/>
    /// </summary>
    public struct ReplicatorProgress
    {
        /// <summary>
        /// Gets the number of changes that have finished processing
        /// </summary>
        public ulong Completed { get; }

        /// <summary>
        /// Gets the current count of changes that have been received for
        /// processing
        /// </summary>
        public ulong Total { get; }

        internal ReplicatorProgress(ulong completed, ulong total)
        {
            Completed = completed;
            Total = total;
        }
    }

    /// <summary>
    /// A struct describing the current <see cref="Document"/> ended progress 
    /// of a <see cref="Replicator"/>
    /// </summary>
    public struct DocumentReplicatedStatus
    {
        public bool Completed { get; }
        public bool Pushing { get; }
        public string DocID { get; }

        internal DocumentReplicatedStatus(string docID, bool pushing, bool completed)
        {
            DocID = docID;
            Completed = completed;
            Pushing = pushing;
        }
    }
}
