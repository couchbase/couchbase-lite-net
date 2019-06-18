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

using Couchbase.Lite.Util;

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
    public struct ReplicatedDocument
    {
        /// <summary>
        /// Gets the special flags, if any, for this replicated document
        /// </summary>
        public DocumentFlags Flags { get; }

        /// <summary>
        /// Gets the document ID of the document that was replicated
        /// </summary>
        [NotNull]
        public string Id { get; }

        /// <summary>
        /// Gets the error that occurred during replication, if any.
        /// </summary>
        [CanBeNull]
        public CouchbaseException Error { get; }

        internal bool IsTransient { get; }

        internal C4Error NativeError { get; }

        internal ReplicatedDocument([NotNull]string docID, C4RevisionFlags flags, C4Error error,
            bool isTransient)
        {
            Id = docID;
            Flags = flags.ToDocumentFlags();
            NativeError = error;
            Error = error.domain == 0 ? null : CouchbaseException.Create(error);
            IsTransient = isTransient;
        }

        private ReplicatedDocument([NotNull] string docID, DocumentFlags flags, C4Error error,
            bool isTransient)
        {
            Id = docID;
            Flags = flags;
            NativeError = error;
            Error = error.domain == 0 ? null : CouchbaseException.Create(error);
            IsTransient = isTransient;
        }

        internal ReplicatedDocument AssignError(C4Error error)
        {
            return new ReplicatedDocument(Id, Flags, error, IsTransient);
        }

        internal ReplicatedDocument ClearError()
        {
            return new ReplicatedDocument(Id, Flags, new C4Error(), IsTransient);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"ReplicatedDocument[ Doc ID: {Id}; " +
                   $"Flags: { Flags };" + 
                   $"Error domain: { Error.Domain }; " +
                   $"Error code: { Error.Error }; " +
                   $"IsTransient: { IsTransient } ]";
        }
    }
}
