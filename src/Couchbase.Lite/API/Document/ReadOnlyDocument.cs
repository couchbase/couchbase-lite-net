// 
// ReadOnlyDocument.cs
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
using System.Diagnostics.CodeAnalysis;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a document which cannot be altered
    /// </summary>
    public unsafe class ReadOnlyDocument : ReadOnlyDictionary, IDisposable
    {
        private readonly bool _owner;

        #region Properties

        /// <summary>
        /// Gets this document's unique ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets whether or not this document is deleted
        /// </summary>
        public bool IsDeleted => _threadSafety.DoLocked(() => c4Doc != null && c4Doc->flags.HasFlag(C4DocumentFlags.Deleted));

        /// <summary>
        /// Gets the sequence of this document (a unique incrementing number
        /// identifying its status in a database)
        /// </summary>
        public ulong Sequence => _threadSafety.DoLocked(() => c4Doc != null ? c4Doc->sequence : 0UL);

        internal C4Document* c4Doc { get; set; }

        internal bool Exists => _threadSafety.DoLocked(() => c4Doc != null && c4Doc->flags.HasFlag(C4DocumentFlags.Exists));

        internal uint Generation => _threadSafety.DoLocked(() => c4Doc != null ? NativeRaw.c4rev_getGeneration(c4Doc->revID) : 0U);

        #endregion

        #region Constructors

        internal ReadOnlyDocument(string documentID, C4Document* c4Doc, FleeceDictionary data, bool owner = true)
            : base(data)
        {
            Id = documentID ?? Misc.CreateGuid();
            this.c4Doc = c4Doc;
            _owner = owner;
            if (!owner) {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ReadOnlyDocument()
        {
            Dispose(false);
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Used for disposing this object
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, <c>false</c> if finalizing</param>
        [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Only types that need to be disposed unconditionally are dealt with")]
        protected virtual void Dispose(bool disposing)
        {
            if (_owner) {
                Native.c4doc_free(c4Doc);
            }

            c4Doc = null;
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            var id = new SecureLogString(Id, LogMessageSensitivity.PotentiallyInsecure);
            return $"{GetType().Name}[{id}]";
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _threadSafety.DoLocked(() => Dispose(true));
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
