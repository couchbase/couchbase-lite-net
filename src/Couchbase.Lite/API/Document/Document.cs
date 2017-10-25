// 
// Document.cs
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an entry in a Couchbase Lite <see cref="Lite.Database"/>.  
    /// It consists of some metadata, and a collection of user-defined properties
    /// </summary>
    public sealed unsafe class Document : ReadOnlyDocument, IDictionaryObject
    {
        #region Constants

        //private const string Tag = nameof(Document);

        private static readonly Dictionary<Guid, Document> _NativeCacheMap = new Dictionary<Guid, Document>();

        #endregion

        #region Properties

        /// <inheritdoc />
        public new Fragment this[string key] => Dict[key];

        internal static IReadOnlyDictionary<Guid, Document> NativeCacheMap => _NativeCacheMap;

        internal override uint Generation => base.Generation + Convert.ToUInt32(Changed);
        
        internal override bool IsMutable => true;

        private bool Changed => (_dict as DictionaryObject)?.HasChanges ?? (_dict as InMemoryDictionary)?.HasChanges ?? false;

        private bool IsEmpty => Dict.Count == 0;

        private IDictionaryObject Dict => _dict as IDictionaryObject;

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public Document() : this(default(string))
        {

        }

        /// <summary>
        /// Creates a document given an ID
        /// </summary>
        /// <param name="documentID">The ID for the document</param>
        public Document(string documentID)
            : this(null, documentID ?? Misc.CreateGuid(), null, null, null)
        {
            
        }

        /// <summary>
        /// Creates a document with the given properties
        /// </summary>
        /// <param name="dictionary">The properties of the document</param>
        public Document(IDictionary<string, object> dictionary)
            : this()
        {
            Set(dictionary);
        }

        /// <summary>
        /// Creates a document with the given ID and properties
        /// </summary>
        /// <param name="documentID">The ID for the document</param>
        /// <param name="dictionary">The properties for the document</param>
        public Document(string documentID, IDictionary<string, object> dictionary)
            : this(documentID)
        {
            Set(dictionary);
        }

        internal Document(Database database, string documentID, bool mustExist, ThreadSafety threadSafety)
            : base(database, documentID, mustExist, threadSafety)
        {

        }

        private Document(Database database, string documentID, C4Document* c4Doc, FLDict* data, ThreadSafety threadSafety)
            : base(database, documentID, c4Doc, data, threadSafety)
        {
            
        }

        #endregion

        #region Internal Methods

        internal void Delete()
        {
            DatabaseThreadSafety.DoLocked(() => Save(EffectiveConflictResolver, true));
        }

        internal void Purge()
        {
            DatabaseThreadSafety.DoLocked(() =>
            {
                if (!Exists) {
                    throw new CouchbaseLiteException(StatusCode.NotFound);
                }

                Database.InBatch(() =>
                {
                    // InBatch has an implicit database thread safety lock, so just lock
                    // the document
                    _selfThreadSafety.DoLocked(() =>
                    {
                        LiteCoreBridge.Check(err => NativeRaw.c4doc_purgeRevision(c4Doc, C4Slice.Null, err));
                        LiteCoreBridge.Check(err => Native.c4doc_save(c4Doc, 0, err));
                    });
                });

                c4Doc = null;
            });
        }

        internal void Save()
        {
            DatabaseThreadSafety.DoLocked(() => Save(EffectiveConflictResolver, false));
        }

        #endregion

        #region Private Methods

        private void Merge(IConflictResolver resolver, bool deletion)
        {
            if (resolver == null) {
                throw new LiteCoreException(new C4Error(C4ErrorCode.Conflict));
            }

            var database = Database;
            using (var current = new ReadOnlyDocument(database, Id, true, DatabaseThreadSafety, false)) {
                var curC4doc = current.c4Doc;

                // Resolve conflict:
                ReadOnlyDocument resolved;
                if (deletion) {
                    // Deletion always loses a conflict:
                    resolved = current;
                } else {
                    // Call the conflict resolver:
                    using (var baseDoc = new ReadOnlyDocument(database, Id, c4Doc, Data, DatabaseThreadSafety, false)) {
                        var conflict = new Conflict(this, current, c4Doc != null ? baseDoc : null);
                        resolved = resolver.Resolve(conflict);
                        if (resolved == null) {
                            throw new LiteCoreException(new C4Error(C4ErrorCode.Conflict));
                        }
                    }
                }

                // Now update my state to the current C4Document and the merged/resolved properties
                if (!resolved.Equals(current)) {
                    var dict = resolved.ToDictionary();
                    c4Doc = curC4doc;
                    Set(dict);
                } else {
                    c4Doc = curC4doc;
                }
            }
        }

        private void Save(IConflictResolver resolver, bool deletion)
        {
            if (deletion && !Exists) {
                throw new CouchbaseLiteException(StatusCode.NotFound);
            }

            C4Document* newDoc = null;
            var success = true;
            Database.BeginTransaction();
            try {
                var tmp = default(C4Document*);
                SaveInto(&tmp, deletion);
                if (tmp == null) {
                    Merge(resolver, deletion);
                    if (!Changed) {
                        return;
                    }

                    SaveInto(&tmp, deletion);
                    if (tmp == null) {
                        throw new LiteCoreException(new C4Error(C4ErrorCode.Conflict));
                    }
                }

                newDoc = tmp;
            } catch (Exception) {
                success = false;
                throw;
            } finally {
                Database.EndTransaction(success);
            }

            var oldDoc = c4Doc;

            c4Doc = newDoc;

            if (oldDoc != null) {
                Native.c4doc_free(oldDoc);
            }
        }

        [SuppressMessage("ReSharper", "AccessToDisposedClosure", Justification = "The closure is executed synchronously")]
        private void SaveInto(C4Document** outDoc, bool deletion)
        {
            var revFlags = (C4RevisionFlags) 0;
            if (deletion) {
                revFlags = C4RevisionFlags.Deleted;
            }

            var body = new FLSliceResult();
            if (!deletion && !IsEmpty) {

                var encoded = Encode();
                body.buf = encoded.buf;
                body.size = encoded.size;
                var root = NativeRaw.FLValue_FromTrustedData((FLSlice)body);
                DatabaseThreadSafety.DoLocked(() =>
                {
                    if (Native.c4doc_dictContainsBlobs((FLDict *)root, Database.SharedStrings.SharedKeys)) {
                        revFlags |= C4RevisionFlags.HasAttachments;
                    }
                });
                
            } else if (IsEmpty) {
                var encoder = default(FLEncoder*);
                DatabaseThreadSafety.DoLocked(() => encoder = Native.c4db_createFleeceEncoder(c4Db));
                Native.FLEncoder_BeginDict(encoder, 0);
                Native.FLEncoder_EndDict(encoder);
                body = NativeRaw.FLEncoder_Finish(encoder, null);
                Native.FLEncoder_Free(encoder);
            }
            
            try {
                var rawDoc = c4Doc;
                if (rawDoc != null) {
                    _selfThreadSafety.DoLocked(() =>
                    {
                        DatabaseThreadSafety.DoLocked(() =>
                        {
                            *outDoc = (C4Document*)NativeHandler.Create()
                                .AllowError((int)C4ErrorCode.Conflict, C4ErrorDomain.LiteCoreDomain).Execute(
                                    err => NativeRaw.c4doc_update(rawDoc, (C4Slice)body, revFlags, err));
                        });
                    });
                } else {
                    DatabaseThreadSafety.DoLocked(() =>
                    {
                        using (var docID_ = new C4String(Id)) {
                            *outDoc = (C4Document*)NativeHandler.Create()
                                .AllowError((int)C4ErrorCode.Conflict, C4ErrorDomain.LiteCoreDomain).Execute(
                                    err => NativeRaw.c4doc_create(c4Db, docID_.AsC4Slice(), (C4Slice)body, revFlags, err));
                        }
                    });
                }
            } finally {
                Native.FLSliceResult_Free(body);
            }
        }

        #endregion

        #region Overrides

        internal override FLSlice Encode()
        {
            var encoder = default(FLEncoder*);
            DatabaseThreadSafety.DoLocked(() => encoder = Native.c4db_getSharedFleeceEncoder(c4Db));
            var guid = Guid.NewGuid();
            _NativeCacheMap[guid] = this;
            Native.FLEncoder_SetExtraInfo(encoder, &guid);

            try {
                _dict.FLEncode(encoder);
            } catch (Exception) {
                Native.FLEncoder_Reset(encoder);
                throw;
            } finally {
                _NativeCacheMap.Remove(guid);
            }

            FLError err;
            var body = NativeRaw.FLEncoder_Finish(encoder, &err);
            if (body.buf == null) {
                throw new LiteCoreException(new C4Error(err));
            }

            return (FLSlice)body;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var id = new SecureLogString(Id, LogMessageSensitivity.PotentiallyInsecure);
            return $"{GetType().Name}[{id}]";
        }

        #endregion

        #region IDictionaryObject

        /// <inheritdoc />
        public new IArray GetArray(string key)
        {
            return Dict.GetArray(key);
        }

        /// <inheritdoc />
        public new IDictionaryObject GetDictionary(string key)
        {
            return Dict.GetDictionary(key);
        }

        /// <inheritdoc />
        public IDictionaryObject Remove(string key)
        {
            Dict.Remove(key);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, object value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(IDictionary<string, object> dictionary)
        {
            Dict.Set(dictionary);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, string value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, int value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, long value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, float value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, double value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, bool value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, Blob value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, DateTimeOffset value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, ArrayObject value)
        {
            Dict.Set(key, value);
            return this;
        }

        /// <inheritdoc />
        public IDictionaryObject Set(string key, DictionaryObject value)
        {
            Dict.Set(key, value);
            return this;
        }

        #endregion
    }
}
