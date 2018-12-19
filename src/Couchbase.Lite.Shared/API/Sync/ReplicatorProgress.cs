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

using JetBrains.Annotations;

using LiteCore.Interop;

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
    public struct DocumentReplication
    {
        /// <summary>
        /// Gets whether or not the document's access has been lost
        /// via removal from all Sync Gateway channels that a user
        /// has access to
        /// </summary>
        public bool IsAccessRemoved { get; }

        /// <summary>
        /// Gets whether or not the document that was replicated
        /// was deleted
        /// </summary>
        public bool IsDeleted { get; }

        /// <summary>
        /// Gets whether or not the replicated document was in
        /// a push replication (<c>false</c> means pull)
        /// </summary>
        public bool IsPush { get; }

        /// <summary>
        /// Gets the document ID of the document that was replicated
        /// </summary>
        [NotNull]
        public string DocumentID { get; }

        /// <summary>
        /// Gets the error that occurred during replication, if any.
        /// </summary>
        [CanBeNull]
        public CouchbaseException Error { get; }

        internal bool IsTransient { get; }

        internal C4Error NativeError { get; }

        internal DocumentReplication([NotNull]string docID, bool pushing, C4RevisionFlags flags, C4Error error,
            bool isTransient)
        {
            DocumentID = docID;
            IsDeleted = flags.HasFlag(C4RevisionFlags.Deleted);
            IsAccessRemoved = flags.HasFlag(C4RevisionFlags.Purged);
            IsPush = pushing;
            NativeError = error;
            Error = error.domain == 0 ? null : CouchbaseException.Create(error);
            IsTransient = isTransient;
        }
    }
}
