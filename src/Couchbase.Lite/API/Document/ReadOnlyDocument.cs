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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a document which cannot be altered
    /// </summary>
    public unsafe class ReadOnlyDocument : IReadOnlyDictionary, IDisposable
    {
        #region Variables

        internal readonly ThreadSafety _selfThreadSafety = new ThreadSafety();

        private readonly bool _owner;
        protected IReadOnlyDictionary _dict;
        private C4Document* _c4Doc;
        private Database _database;
        private MRoot _root;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of top level entries in this document
        /// </summary>
        public int Count => _dict?.Count ?? 0;

        /// <summary>
        /// Gets the database that this document belongs to, if any
        /// </summary>
        public Database Database
        {
            get => _database;
            set {
                _database = value;
                DatabaseThreadSafety = value?.ThreadSafety;
            }
        }

        /// <summary>
        /// Gets this document's unique ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets whether or not this document is deleted
        /// </summary>
        public bool IsDeleted => _selfThreadSafety.DoLocked(() => _c4Doc != null && _c4Doc->flags.HasFlag(C4DocumentFlags.DocDeleted));

        /// <summary>
        /// Accesses JSON paths in the document to get their values
        /// </summary>
        /// <param name="key">The key to create the fragment from</param>
        public ReadOnlyFragment this[string key] => _dict[key];

        /// <summary>
        /// Gets all the keys present in this document
        /// </summary>
        public ICollection<string> Keys => _dict.Keys;

        /// <summary>
        /// Gets the sequence of this document (a unique incrementing number
        /// identifying its status in a database)
        /// </summary>
        public ulong Sequence => _selfThreadSafety.DoLocked(() => _c4Doc != null ? _c4Doc->sequence : 0UL);

        internal C4Database* c4Db
        {
            get {
                Debug.Assert(Database != null && Database.c4db != null);
                return Database.c4db;
            }
        }

        internal C4Document* c4Doc
        {
            get => _c4Doc;
            set {
                _c4Doc = value;
                Data = null;

                if (value != null) {
                    var body = value->selectedRev.body;
                    if (body.size > 0) {
                        Data = Native.FLValue_AsDict(NativeRaw.FLValue_FromTrustedData(new FLSlice(body.buf, body.size)));
                    }
                }

                UpdateDictionary();
            }
        }

        internal FLDict* Data { get; private set; }

        internal ThreadSafety DatabaseThreadSafety { get; private set; }

        internal IConflictResolver EffectiveConflictResolver => Database?.Config.ConflictResolver ??
                                                                        new DefaultConflictResolver();

        internal bool Exists => _selfThreadSafety.DoLocked(() => _c4Doc != null && _c4Doc->flags.HasFlag(C4DocumentFlags.DocExists));

        internal virtual uint Generation => _selfThreadSafety.DoLocked(() => _c4Doc != null ? NativeRaw.c4rev_getGeneration(_c4Doc->revID) : 0U);

        internal virtual bool IsMutable => false;

        internal string RevID => _c4Doc != null ? _c4Doc->selectedRev.revID.CreateString() : null;

        #endregion

        #region Constructors

        internal ReadOnlyDocument(Database database, string documentID, C4Document* c4Doc, FLDict* data, ThreadSafety threadSafety,
            bool owner = true)
        {
            Database = database;
            Id = documentID ?? throw new ArgumentNullException(nameof(documentID));
            _c4Doc = c4Doc;
            Data = data;
            _owner = owner;
            Debug.Assert(database == null || threadSafety != null);
            DatabaseThreadSafety = threadSafety;
            if (!owner) {
                GC.SuppressFinalize(this);
            }

            UpdateDictionary();
        }

        internal ReadOnlyDocument(Database database, string documentID, bool mustExist, ThreadSafety threadSafety, bool owner = true)
            : this(database, documentID, null, null, threadSafety, owner)
        {
            var db = database ?? throw new ArgumentNullException(nameof(database));
            threadSafety.DoLocked(() =>
            {
                var doc = (C4Document*)NativeHandler.Create().AllowError(new C4Error(C4ErrorCode.NotFound)).Execute(
                    err => Native.c4doc_get(db.c4db, documentID, mustExist, err));
                c4Doc = doc;
            });
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
        protected virtual void Dispose(bool disposing)
        {
            if (disposing) {
                _root?.Dispose();
            }

            if (_owner) {
                Native.c4doc_free(_c4Doc);
            }

            c4Doc = null;
        }

        #endregion

        #region Internal Methods

        internal virtual FLSlice Encode()
        {
            return _c4Doc != null ? (FLSlice)_c4Doc->selectedRev.body : new FLSlice();
        }

        internal bool SelectCommonAncestor(ReadOnlyDocument doc1, ReadOnlyDocument doc2)
        {
            if (_c4Doc == null) {
                return false;
            }

            var success = false;
            _selfThreadSafety.DoLocked(() => success = NativeRaw.c4doc_selectCommonAncestorRevision(_c4Doc, doc1.c4Doc->selectedRev.revID,
                doc2.c4Doc->selectedRev.revID));
            if(!success) {
                return false;
            }

            c4Doc = _c4Doc;
            return true;
        }

        internal void SelectConflictingRevision()
        {
            if (_c4Doc == null) {
                throw new InvalidOperationException("No revision data on the document!");
            }

            _selfThreadSafety.DoLockedBridge(err => Native.c4doc_selectNextLeafRevision(_c4Doc, false, true, err));
            c4Doc = _c4Doc;
        }

        #endregion

        #region Private Methods

        private void UpdateDictionary()
        {
            if (Data != null) {
                Misc.SafeSwap(ref _root,
                    new MRoot(new DocContext(_database, _c4Doc), (FLValue*) Data, IsMutable));
                _dict = (ReadOnlyDictionary) _root.AsObject();
            } else {
                Misc.SafeSwap(ref _root, null);
                _dict = IsMutable ? (IReadOnlyDictionary)new InMemoryDictionary() : new ReadOnlyDictionary();
            }
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
            _selfThreadSafety.DoLocked(() => Dispose(true));
            GC.SuppressFinalize(this);
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _dict.GetEnumerator();

        #endregion

        #region IReadOnlyDictionary

        public bool Contains(string key) => _dict.Contains(key);

        public IReadOnlyArray GetArray(string key) => _dict.GetArray(key);

        public Blob GetBlob(string key) => _dict.GetBlob(key);

        public bool GetBoolean(string key) => _dict.GetBoolean(key);

        public DateTimeOffset GetDate(string key) => _dict.GetDate(key);

        public IReadOnlyDictionary GetDictionary(string key) => _dict.GetDictionary(key);

        public double GetDouble(string key) => _dict.GetDouble(key);

        public float GetFloat(string key) => _dict.GetFloat(key);

        public int GetInt(string key) => _dict.GetInt(key);

        public long GetLong(string key) => _dict.GetLong(key);

        public object GetObject(string key) => _dict.GetObject(key);

        public string GetString(string key) => _dict.GetString(key);

        public IDictionary<string, object> ToDictionary() => _dict.ToDictionary();

        #endregion
    }
}
