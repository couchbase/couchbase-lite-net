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

using LiteCore.Interop;
using System;
using System.Diagnostics.CodeAnalysis;

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
        /// Gets the collection name of replicated document
        /// </summary>
        public string CollectionName { get; }

        /// <summary>
        /// Gets the scope name of replicated document
        /// </summary>
        public string ScopeName { get; }

        /// <summary>
        /// Gets the special flags, if any, for this replicated document
        /// </summary>
        public DocumentFlags Flags { get; }

        /// <summary>
        /// Gets the document ID of the document that was replicated
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the error that occurred during replication, if any.
        /// </summary>
        public CouchbaseException? Error { get; internal set; }

        internal bool IsTransient { get; }

        internal C4Error NativeError { get; }

        internal ReplicatedDocument(string docID, C4CollectionSpec collectionSpec, C4RevisionFlags flags, C4Error error,
            bool isTransient)
        {
            Id = docID;
            CollectionName = collectionSpec.name.CreateString()!;
            ScopeName = collectionSpec.scope.CreateString()!;
            Flags = flags.ToDocumentFlags();
            NativeError = error;
            Error = error.domain == 0 ? null : CouchbaseException.Create(error);
            IsTransient = isTransient;
        }

        private ReplicatedDocument(string docID, string collectionName, string scopeName, DocumentFlags flags, C4Error error,
            bool isTransient)
        {
            Id = docID;
            CollectionName = collectionName;
            ScopeName = scopeName;
            Flags = flags;
            NativeError = error;
            Error = error.domain == 0 ? null : CouchbaseException.Create(error);
            IsTransient = isTransient;
        }

        internal ReplicatedDocument ClearError()
        {
            return new ReplicatedDocument(Id, CollectionName, ScopeName, Flags, new C4Error(), IsTransient);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var retVal = $"ReplicatedDocument[ Doc ID: {Id}; " +
                   $"Flags: {Flags};" +
                   $"Collection Name: {CollectionName};" +
                   $"Scope Name: {ScopeName};";

            if (Error != null) {
                retVal += $"Error domain: {Error.Domain}; " +
                $"Error code: {Error.Error}; " +
                $"IsTransient: {IsTransient} ]";
            }

            return retVal;
        }
    }
}
