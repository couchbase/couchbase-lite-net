// 
//  Document.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
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
    public unsafe class Document : IDictionaryObject, IDisposable
    {
        #region Variables

        private C4DocumentWrapper _c4Doc;
        protected IDictionaryObject _dict;
        private MRoot _root;

        #endregion

        #region Properties

        internal C4Database* c4Db
        {
            get {
                Debug.Assert(Database != null && Database.c4db != null);
                return Database.c4db;
            }
        }

        internal C4DocumentWrapper c4Doc
        {
            get => _c4Doc;
            set {
                var newVal = value;
                Misc.SafeSwap(ref _c4Doc, newVal);
                Data = null;

                if (newVal?.HasValue == true) {
                    var body = newVal.RawDoc->selectedRev.body;
                    if (body.size > 0) {
                        Data = Native.FLValue_AsDict(NativeRaw.FLValue_FromTrustedData(new FLSlice(body.buf, body.size)));
                    }
                }

                UpdateDictionary();
            }
        }

        internal FLDict* Data { get; private set; }

        /// <summary>
        /// Gets the database that this document belongs to, if any
        /// </summary>
        public Database Database { get; internal set; }

        internal bool Exists => ThreadSafety.DoLocked(() => c4Doc?.HasValue == true && c4Doc.RawDoc->flags.HasFlag(C4DocumentFlags.DocExists));

        internal virtual uint Generation => ThreadSafety.DoLocked(() => c4Doc?.HasValue == true ? NativeRaw.c4rev_getGeneration(c4Doc.RawDoc->revID) : 0U);

        /// <summary>
        /// Gets this document's unique ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets whether or not this document is deleted
        /// </summary>
        public bool IsDeleted => ThreadSafety.DoLocked(() => c4Doc?.HasValue == true && c4Doc.RawDoc->flags.HasFlag(C4DocumentFlags.DocDeleted));

        internal bool IsEmpty => _dict.Count == 0;

        internal virtual bool IsMutable => false;

        internal string RevID => c4Doc?.HasValue == true ? c4Doc.RawDoc->selectedRev.revID.CreateString() : null;

        /// <summary>
        /// Gets the sequence of this document (a unique incrementing number
        /// identifying its status in a database)
        /// </summary>
        public ulong Sequence => ThreadSafety.DoLocked(() => c4Doc?.HasValue == true ? c4Doc.RawDoc->sequence : 0UL);

        internal ThreadSafety ThreadSafety { get; } = new ThreadSafety();

        /// <summary>
        /// Accesses JSON paths in the document to get their values
        /// </summary>
        /// <param name="key">The key to create the fragment from</param>
        public Fragment this[string key] => _dict[key];

        /// <summary>
        /// Gets all the keys present in this document
        /// </summary>
        public ICollection<string> Keys => _dict.Keys;

        /// <summary>
        /// Gets the number of top level entries in this document
        /// </summary>
        public int Count => _dict?.Count ?? 0;

        #endregion

        #region Constructors

        internal Document(Database database, string documentID, C4DocumentWrapper c4Doc)
        {
            Database = database;
            Id = documentID ?? throw new ArgumentNullException(nameof(documentID));
            this.c4Doc = c4Doc;
        }

        internal Document(Database database, string documentID, bool mustExist)
        {
            Database = database ?? throw new ArgumentNullException(nameof(database));
            Id = documentID ?? throw new ArgumentNullException(nameof(documentID));
            database.ThreadSafety.DoLocked(() =>
            {
                var doc = (C4Document*)NativeHandler.Create().AllowError(new C4Error(C4ErrorCode.NotFound)).Execute(
                    err => Native.c4doc_get(database.c4db, documentID, mustExist, err));
                c4Doc = new C4DocumentWrapper(doc);
            });
        }

        #endregion

        #region Public Methods

        public virtual MutableDocument ToMutable()
        {
            return new MutableDocument(this);
        }

        #endregion

        #region Private Methods

        private void UpdateDictionary()
        {
            if (Data != null) {
                var rawDoc = c4Doc?.HasValue == true ? c4Doc.RawDoc : null;
                Misc.SafeSwap(ref _root,
                    new MRoot(new DocContext(Database, rawDoc), (FLValue*) Data, IsMutable));
                _dict = (DictionaryObject) _root.AsObject();
            } else {
                Misc.SafeSwap(ref _root, null);
                _dict = IsMutable ? (IDictionaryObject)new InMemoryDictionary() : new DictionaryObject();
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

        #region IDictionaryObject

        public bool Contains(string key) => _dict.Contains(key);

        public IArray GetArray(string key) => _dict.GetArray(key);

        public Blob GetBlob(string key) => _dict.GetBlob(key);

        public bool GetBoolean(string key) => _dict.GetBoolean(key);

        public DateTimeOffset GetDate(string key) => _dict.GetDate(key);

        public IDictionaryObject GetDictionary(string key) => _dict.GetDictionary(key);

        public double GetDouble(string key) => _dict.GetDouble(key);

        public float GetFloat(string key) => _dict.GetFloat(key);

        public int GetInt(string key) => _dict.GetInt(key);

        public long GetLong(string key) => _dict.GetLong(key);

        public object GetObject(string key) => _dict.GetObject(key);

        public string GetString(string key) => _dict.GetString(key);

        public Dictionary<string, object> ToDictionary() => _dict.ToDictionary();

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            ThreadSafety.DoLocked(() =>
            {
                _root?.Dispose();
                Misc.SafeSwap(ref _c4Doc, null);
            });
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _dict.GetEnumerator();

        #endregion

        internal virtual FLSlice Encode()
        {
            return c4Doc?.HasValue == true ? (FLSlice)c4Doc.RawDoc->selectedRev.body : new FLSlice();
        }

        internal bool SelectCommonAncestor(Document doc1, Document doc2)
        {
            if (_c4Doc == null) {
                return false;
            }

            var revID1 = doc1?.c4Doc?.HasValue == true ? doc1.c4Doc.RawDoc->selectedRev.revID : C4Slice.Null;
            var revID2 = doc2?.c4Doc?.HasValue == true ? doc2.c4Doc.RawDoc->selectedRev.revID : C4Slice.Null;
            var success = false;
            ThreadSafety.DoLocked(() => success = NativeRaw.c4doc_selectCommonAncestorRevision(c4Doc.RawDoc, revID1, revID2));
            if(!success) {
                return false;
            }

            // HACK: Trigger side effect
            c4Doc = _c4Doc.Retain<C4DocumentWrapper>();
            return true;
        }

        internal void SelectConflictingRevision()
        {
            if (_c4Doc == null) {
                throw new InvalidOperationException("No revision data on the document!");
            }

            ThreadSafety.DoLockedBridge(err => Native.c4doc_selectNextLeafRevision(c4Doc.RawDoc, false, true, err));
            c4Doc = _c4Doc.Retain<C4DocumentWrapper>();
        }
    }
}
