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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a document which cannot be altered
    /// </summary>
    public unsafe class ReadOnlyDocument : ReadOnlyDictionary, IDisposable
    {
        #region Variables

        private readonly bool _owner;
        private C4Document* _c4Doc;
        internal readonly ThreadSafety _threadSafety = new ThreadSafety();

        #endregion

        #region Properties

        /// <summary>
        /// Gets this document's unique ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets whether or not this document is deleted
        /// </summary>
        public bool IsDeleted => _threadSafety.DoLocked(() => _c4Doc != null && _c4Doc->flags.HasFlag(C4DocumentFlags.Deleted));

        /// <summary>
        /// Gets the sequence of this document (a unique incrementing number
        /// identifying its status in a database)
        /// </summary>
        public ulong Sequence => _threadSafety.DoLocked(() => _c4Doc != null ? _c4Doc->sequence : 0UL);

        public Database Database { get; internal set; }

        internal C4Database* c4Db
        {
            get {
                Debug.Assert(Database != null && Database.c4db != null);
                return Database.c4db;
            }
        }

        internal virtual C4Document* c4Doc
        {
            get => _c4Doc;
            set {
                _c4Doc = value;
                if (value != null) {
                    FLDict* root = null;
                    var body = value->selectedRev.body;
                    if (body.size > 0) {
                        root = Native.FLValue_AsDict(NativeRaw.FLValue_FromTrustedData(new FLSlice(body.buf, body.size)));
                    }

                    Data = new FleeceDictionary(root, value, Database);
                }
                else {
                    Data = null;
                }
            }
        }

        internal bool Exists => _threadSafety.DoLocked(() => _c4Doc != null && _c4Doc->flags.HasFlag(C4DocumentFlags.Exists));

        internal virtual uint Generation => _threadSafety.DoLocked(() => _c4Doc != null ? NativeRaw.c4rev_getGeneration(_c4Doc->revID) : 0U);

        internal string RevID => _c4Doc != null ? _c4Doc->selectedRev.revID.CreateString() : null;

        internal IConflictResolver EffectiveConflictResolver => Database?.Config.ConflictResolver ??
                                                                        new DefaultConflictResolver();

        #endregion

        #region Constructors

        internal ReadOnlyDocument(Database database, string documentID, C4Document* c4Doc, FleeceDictionary data, bool owner = true)
            : base(data)
        {
            Database = database;
            Id = documentID ?? throw new ArgumentNullException(nameof(documentID));
            _c4Doc = c4Doc;
            _owner = owner;
            if (!owner) {
                GC.SuppressFinalize(this);
            }
        }

        internal ReadOnlyDocument(Database database, string documentID, bool mustExist, bool owner = true)
            : this(database, documentID, null, null, owner)
        {
            var db = database ?? throw new ArgumentNullException(nameof(database));
            var doc = (C4Document*) NativeHandler.Create().AllowError(new C4Error(C4ErrorCode.NotFound)).Execute(
                err => Native.c4doc_get(db.c4db, documentID, mustExist, err));
            c4Doc = doc;
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
                Native.c4doc_free(_c4Doc);
            }

            c4Doc = null;
        }

        #endregion

        internal virtual byte[] Encode()
        {
            return _c4Doc != null ? _c4Doc->selectedRev.body.ToArrayFast() : new byte[0];
        }

        internal void SelectConflictingRevision()
        {
            if (_c4Doc == null) {
                throw new InvalidOperationException("No revision data on the document!");
            }

            LiteCoreBridge.Check(err => Native.c4doc_selectNextLeafRevision(_c4Doc, false, true, err));
            c4Doc = _c4Doc;
        }

        internal bool SelectCommonAncestor(ReadOnlyDocument doc1, ReadOnlyDocument doc2)
        {
            if (_c4Doc == null ||
                !NativeRaw.c4doc_selectCommonAncestorRevision(_c4Doc, doc1.c4Doc->selectedRev.revID,
                    doc2.c4Doc->selectedRev.revID)) {
                return false;
            }

            c4Doc = _c4Doc;
            return true;
        }

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
