// 
//  Document.cs
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
using System.Linq;

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a document which cannot be altered
    /// </summary>
    public unsafe class Document : IDictionaryObject, IJSON, IDisposable
    {
        private static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        #region Variables

        private string? _revId;

        private C4DocumentWrapper? _c4Doc;

        /// <summary>
        /// The backing dictionary for this document
        /// </summary>
        protected IDictionaryObject? _dict;

        private MRoot? _root;

        internal readonly DisposalWatchdog _disposalWatchdog;

        #endregion

        #region Properties

        internal C4DatabaseWrapper c4Db
        {
            get {
                Debug.Assert(Database != null && Database.c4db != null);
                return Database!.c4db!;
            }
        }

        internal C4CollectionWrapper c4Coll
        {
            get {
                Debug.Assert(Collection != null && Collection.c4coll != null);
                return Collection!.c4coll!;
            }
        }

        internal C4DocumentWrapper? c4Doc
        {
            get => _c4Doc;
            set {
                using var threadSafetyScope = ThreadSafety.BeginLockedScope();
                var newVal = value;
                Misc.SafeSwap(ref _c4Doc, newVal);

                Data = null;

                if (newVal?.HasValue == true) {
                    Data = NativeSafe.c4doc_getProperties(newVal);
                }

                UpdateDictionary();
            }
        }

        internal FLDict* Data { get; private set; }

        /// <summary>
        /// Gets the database that this document belongs to, if any
        /// </summary>
        internal Database? Database
        {
            get {
                return Collection?.Database;
            }
        }

        internal IReadOnlyList<string> RevisionIDs
        {
            get {
                var returned = ThreadSafety.DoLocked(() => c4Doc?.HasValue == true ? Native.c4doc_getRevisionHistory(c4Doc.RawDoc) : null);
                if (returned == null) {
                    return new List<string>();
                }

                return returned.Replace(" ", "").Split(',');
            }    
        }

        /// <summary>
        /// Gets the Collection that this document belongs to, if any
        /// </summary>
        public Collection? Collection { get; set; }

        internal bool Exists
        {
            get {
                using var threadSafetyScope = ThreadSafety.BeginLockedScope();
                return c4Doc?.HasValue == true && c4Doc.RawDoc->flags.HasFlag(C4DocumentFlags.DocExists);
            }
        }

        /// <summary>
        /// Gets this document's unique ID
        /// </summary>
        public string Id { get; }

        internal virtual bool IsDeleted
        {
            get {
                using var threadSafetyScope = ThreadSafety.BeginLockedScope();
                return c4Doc?.HasValue == true && c4Doc.RawDoc->selectedRev.flags.HasFlag(C4RevisionFlags.Deleted);
            }
        }

        internal virtual bool IsEmpty => _dict?.Count == 0;

        internal virtual bool IsMutable => false;

        /// <summary>
        /// The RevisionID in Document class is a constant, while the RevisionID in <see cref="MutableDocument" /> class is not.
        /// Newly created document will have a null RevisionID. The RevisionID in <see cref="MutableDocument" /> will be updated on save.
        /// The RevisionID format is opaque, which means it's format has no meaning and shouldn�t be parsed to get information.
        /// </summary>
        public string? RevisionID
        {
            get {
                using var threadSafetyScope = ThreadSafety.BeginLockedScope();
                return c4Doc?.HasValue == true ? c4Doc.RawDoc->selectedRev.revID.CreateString() : _revId;
            }
        }

        /// <summary>
        /// The hybrid logical timestamp that the revision was created.
        /// </summary>
        public DateTimeOffset? Timestamp
        {
            get {
                var rawVal = ThreadSafety.DoLocked(() => c4Doc?.HasValue == true ? NativeRaw.c4rev_getTimestamp(c4Doc.RawDoc->selectedRev.revID) : 0);
                if(rawVal == 0) {
                    return null;
                }

                // .NET ticks are in 100 nanosecond intervals
                rawVal /= 100;

                if(rawVal > Int64.MaxValue) {
                    throw new OverflowException("The returned value from LiteCore is too large to be represented by DateTimeOffset");
                }

                return UnixEpoch + TimeSpan.FromTicks((long)rawVal);
            }
        }

        /// <summary>
        /// Gets the sequence of this document (a unique incrementing number
        /// identifying its status in a database)
        /// </summary>
        public ulong Sequence
        {
            get {
                using var threadSafetyScope = ThreadSafety.BeginLockedScope();
                return c4Doc?.HasValue == true ? c4Doc.RawDoc->selectedRev.sequence : 0UL;
            }
        }

        internal ThreadSafety ThreadSafety { get; } = new ThreadSafety();

        /// <inheritdoc />
        public IFragment this[string key]
        {
            get {
                if(_dict == null) {
                    throw new InvalidOperationException("Null _dict when trying to access key value");
                }

                return _dict[key];
            }
        }

        /// <inheritdoc />
        public ICollection<string> Keys => _dict?.Keys ?? throw new InvalidOperationException("Null _dict when trying to access Keys");

        /// <inheritdoc />
        public int Count => _dict?.Count ?? 0;

        #endregion

        #region Constructors

        internal Document(Collection? collection, string id, C4DocumentWrapper? c4Doc)
        {
            Collection = collection;
            Id = id;
            this.c4Doc = c4Doc;
            _disposalWatchdog = new DisposalWatchdog(GetType().Name);
        }

        internal Document(Collection? collection, string id, C4DocContentLevel contentLevel = C4DocContentLevel.DocGetCurrentRev)
            : this(collection, id, default(C4DocumentWrapper))
        {
            c4Doc = NativeHandler.Create().AllowError(new C4Error(C4ErrorCode.NotFound)).Execute(
                err => NativeSafe.c4coll_getDoc(c4Coll, id, true, contentLevel, err));
        }

        internal Document(Collection? collection, string id, string revId, FLDict* body)
            : this(collection, id, default(C4DocumentWrapper))
        {
            Data = body;
            UpdateDictionary();
            _revId = revId;
        }

        internal Document(Document other)
            : this(other.Collection, other.Id, other.c4Doc?.Retain<C4DocumentWrapper>())
        {
            if(other._root != null) {
                _root = new MRoot(other._root);
            } else {
                _root = new MRoot();
            }

            Data = other.Data;
            Id = other.Id;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a mutable version of a document (i.e. one that
        /// can be edited)
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// InvalidOperationException thrown when trying edit Documents from a replication filter.
        /// </exception>
        /// <returns>A mutable version of the document</returns>
        public virtual MutableDocument ToMutable() {
            if (_revId != null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.NoDocEditInReplicationFilter);
            }

            return new MutableDocument(this);
        }

#if CBL_LINQ
        public T ToModel<T>() where T : class, Linq.IDocumentModel, new()
        {
            var serializer = Newtonsoft.Json.JsonSerializer.CreateDefault();
            var flValue = NativeRaw.FLValue_FromTrustedData((FLSlice) c4Doc.RawDoc->selectedRev.body);
            using (var reader = new Internal.Serialization.JsonFLValueReader(flValue, Database.SharedStrings)) {
                var retVal = serializer.Deserialize<T>(reader);
                retVal.Document = this;
                return retVal;
            }
        }
#endif

        #endregion

        #region Internal Methods

        internal virtual FLSliceResult Encode()
        {
            _disposalWatchdog.CheckDisposed();
            if (c4Doc?.HasValue == true) {
                var data = NativeSafe.c4doc_getRevisionBody(c4Doc);
                return Native.FLSlice_Copy(data);
            }

            return (FLSliceResult) FLSlice.Null;
        }

        internal void ReplaceC4Doc(C4DocumentWrapper newDoc)
        {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            Misc.SafeSwap(ref _c4Doc, newDoc);
        }

        internal bool SelectConflictingRevision()
        {
            if (_c4Doc == null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.NoDocumentRevision);
            }
            
            var foundConflict = false;
            var err = new C4Error();
            while (!foundConflict && NativeSafe.c4doc_selectNextLeafRevision(_c4Doc, true, true, &err)) {
                foundConflict = _c4Doc.RawDoc->selectedRev.flags.HasFlag(C4RevisionFlags.IsConflict);
            }

            if (err.code != 0) {
                throw CouchbaseException.Create(err);
            }

            if (foundConflict) {
                // HACK: Side effect of updating data
                c4Doc = _c4Doc.Retain<C4DocumentWrapper>();
            }
            
            return foundConflict;
        }
        #endregion

        #region Private Methods

        private void UpdateDictionary()
        {
            if (Data != null) {
                Debug.Assert(Database != null);
                Misc.SafeSwap(ref _root,
                    new MRoot(new DocContext(Database!, _c4Doc), (FLValue*) Data, IsMutable));

                // TODO: Is this needed?
                using var threadSafetyScope = Collection?.ThreadSafety?.BeginLockedScope();
                _dict = (DictionaryObject?) _root!.AsObject();
            } else {
                Misc.SafeSwap(ref _root, null);
                _dict = IsMutable ? new InMemoryDictionary() : new DictionaryObject();
            }
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override string ToString()
        {
            var id = new SecureLogString(Id, LogMessageSensitivity.PotentiallyInsecure);
            return $"{GetType().Name}[{id}]";
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var h = Hasher.Start;
            h.Add(Id);
            if (RevisionID != null) {
                h.Add(RevisionID);
            }

            return h;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (!(obj is Document d)) {
                return false;
            }

            // First check the collection and database match
            // This needs to be done in a less strict way than
            // normal comparison because we don't care as much that
            // the exact instances are the same, just that they refer
            // to the same on-disk entities
            if(Database?.Path != d.Database?.Path || Collection?.FullName != d.Collection?.FullName) {
                return false;
            }
            
            // Next check the ID
            if (Id != d.Id) {
                return false;
            }

            // Do a quick check that the actual keys are the same
            var commonCount = Keys.Intersect(d.Keys).Count();
            if (commonCount != Keys.Count || commonCount != d.Keys.Count) {
                return false; // The set of keys is different
            }

            // Final fallback, examine every key and value
            return !(from key in Keys 
                let left = GetValue(key) 
                let right = d.GetValue(key) 
                where !left.RecursiveEqual(right)
                select left).Any();
        }

        #endregion

        #region IDictionaryObject

        /// <inheritdoc />
        public bool Contains(string key) => _dict?.Contains(key) == true;

        /// <inheritdoc />
        public ArrayObject? GetArray(string key) => _dict?.GetArray(key);

        /// <inheritdoc />
        public Blob? GetBlob(string key) => _dict?.GetBlob(key);

        /// <inheritdoc />
        public bool GetBoolean(string key) => _dict?.GetBoolean(key) ?? false;

        /// <inheritdoc />
        public DateTimeOffset GetDate(string key) => _dict?.GetDate(key) ?? DateTimeOffset.MinValue;

        /// <inheritdoc />
        public DictionaryObject? GetDictionary(string key) => _dict?.GetDictionary(key);

        /// <inheritdoc />
        public double GetDouble(string key) => _dict?.GetDouble(key) ?? 0.0;

        /// <inheritdoc />
        public float GetFloat(string key) => _dict?.GetFloat(key) ?? 0.0f;

        /// <inheritdoc />
        public int GetInt(string key) => _dict?.GetInt(key) ?? 0;

        /// <inheritdoc />
        public long GetLong(string key) => _dict?.GetLong(key) ?? 0L;

        /// <inheritdoc />
        public object? GetValue(string key) => _dict?.GetValue(key);

        /// <inheritdoc />
        public string? GetString(string key) => _dict?.GetString(key);

        /// <inheritdoc />
        public Dictionary<string, object?> ToDictionary() => _dict?.ToDictionary() ?? new Dictionary<string, object?>();

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            _disposalWatchdog.Dispose();
            _root?.Dispose();
            Misc.SafeSwap(ref _c4Doc, null);
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _dict?.GetEnumerator() ?? new InMemoryDictionary().GetEnumerator();

        #endregion

        #region IJSON

        /// <inheritdoc />
        public string ToJSON()
        {
            if(IsMutable) {
                throw new NotSupportedException();
            }

            if(c4Doc == null) {
                WriteLog.To.Database.E("Document", "c4Doc null in ToJSON()");
                return "";
            }

            // This will throw if null, so ! is safe
            return LiteCoreBridge.Check(err =>
            {
                return NativeSafe.c4doc_bodyAsJSON(c4Doc, true, err);
            })!;
        }
        #endregion
    }
}
