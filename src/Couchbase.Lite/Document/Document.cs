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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Couchbase.Lite.Internal.DB;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Serialization;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class Document : ReadOnlyDocument, IDocument
    {
        #region Constants

        private const string Tag = nameof(Document);

        #endregion

        #region Variablesp

        private readonly C4Database* _c4Db;
        private Database _database;
        private DictionaryObject _dict;

        #endregion

        #region Properties

        public IDatabase Database
        {
            get => _database;
            set => _database = value as Database;
        }

        public IFragment Get(string key)
        {
            throw new NotImplementedException();
        }

        public new IFragment this[string key] => _dict[key];

        internal override bool IsEmpty => _dict.IsEmpty;

        #endregion

        #region Constructors

        public Document() : this(Misc.CreateGuid())
        {

        }

        public Document(string documentID)
            : base(documentID, null, new FleeceDictionary())
        {
            _dict = new DictionaryObject(Data);
        }

        public Document(IDictionary<string, object> dictionary)
            : this()
        {
            Set(dictionary);
        }

        public Document(string documentID, IDictionary<string, object> dictionary)
            : this(documentID)
        {
            Set(dictionary);
        }

        internal Document(IDatabase database, string documentID, bool mustExist)
            : base(documentID, null, new FleeceDictionary())
        {
            Database = database;
            LoadDoc(mustExist);
        }

        ~Document()
        {
            Dispose(false);
        }

        #endregion

        public override IDictionary<string, object> ToDictionary()
        {
            return _dict.ToDictionary();
        }

        public override ICollection<string> AllKeys()
        {
            return _dict.AllKeys();
        }

        public override bool Contains(string key)
        {
            return _dict.Contains(key);
        }

        public override object GetObject(string key)
        {
            return _dict.GetObject(key);
        }

        public override IBlob GetBlob(string key)
        {
            return _dict.GetBlob(key);
        }

        public override bool GetBoolean(string key)
        {
            return _dict.GetBoolean(key);
        }

        public override DateTimeOffset GetDate(string key)
        {
            return _dict.GetDate(key);
        }

        public override double GetDouble(string key)
        {
            return _dict.GetDouble(key);
        }

        public override int GetInt(string key)
        {
            return _dict.GetInt(key);
        }

        public override long GetLong(string key)
        {
            return _dict.GetLong(key);
        }

        public override string GetString(string key)
        {
            return _dict.GetString(key);
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Only types that need to be disposed unconditionally are dealt with")]
        private void Dispose(bool disposing)
        {
            Native.c4doc_free(_c4Doc);
            _c4Doc = null;
        }

        private void LoadDoc(bool mustExist)
        {
            var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(_c4Db, Id, mustExist, err));
            SetC4Doc(doc);
        }

        private void Merge(IConflictResolver resolver, bool deletion)
        {
            var currentDoc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(_c4Db, Id, true, err));
            var currentData = currentDoc->selectedRev.body;
            var current = default(IDictionary<string, object>);
            if(currentData.size > 0) {
                var currentRoot = NativeRaw.FLValue_FromTrustedData((FLSlice)currentData);
                var currentKeys = new SharedStringCache(SharedKeys, (FLDict *)currentRoot);
                current = FLValueConverter.ToObject(currentRoot, currentKeys) as IDictionary<string, object>;
            }

            IDictionary<string, object> resolved;
            if(deletion) {
                resolved = current;
            } else if (resolver != null) {
                var empty = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
                resolved = resolver.Resolve(Properties != null ? new ReadOnlyDictionary<string, object>(Properties) : empty,
                    current != null ? new ReadOnlyDictionary<string, object>(current) : empty,
                    SavedProperties);
                if(resolved == null) {
                    Native.c4doc_free(currentDoc);
                    throw new LiteCoreException(new C4Error(LiteCoreError.Conflict));
                }
            } else {
                // Thank Jens Alfke for this variable name (lol)
                var myGgggeneration = Generation + 1;
                var theirGgggeneration = NativeRaw.c4rev_getGeneration(currentDoc->revID);
                resolved = myGgggeneration >= theirGgggeneration ? Properties : current;
            }

            SetC4Doc(currentDoc);
            Properties = resolved;
            if(resolved != null && resolved.Equals(current) || resolved == null && current == null) {
                HasChanges = false;
            }
        }

        private void Save(IConflictResolver resolver, bool deletion, IDocumentModel model = null)
        {
            if(_database == null || _c4Db == null) {
                throw new InvalidOperationException("Save attempted after database was closed");
            }

            if(!_dict.HasChanges && !deletion && Exists) {
                return;
            }

            C4Document* newDoc = null;
            var endedEarly = false;
            Database.InBatch(() =>
            {
                var tmp = default(C4Document*);
                SaveInto(&tmp, deletion, model);
                if (tmp == null) {
                    Merge(resolver, deletion);
                    if (!_dict.HasChanges) {
                        endedEarly = true;
                        return;
                    }

                    SaveInto(&tmp, deletion, model);
                    if (tmp == null) {
                        throw new CouchbaseLiteException("Conflict still occuring after resolution", StatusCode.DbError);
                    }
                }

                newDoc = tmp;
            });

            if (endedEarly) {
                return;
            }

            SetC4Doc(newDoc);
        }

        [SuppressMessage("ReSharper", "AccessToDisposedClosure", Justification = "The closure is executed synchronously")]
        private void SaveInto(C4Document** outDoc, bool deletion, IDocumentModel model = null)
        {
            var put = new C4DocPutRequest();
            using(var docId = new C4String(Id)) {
                put.docID = docId.AsC4Slice();
                if(c4Doc != null) {
                    put.history = &c4Doc->revID;
                    put.historyCount = 1;
                }

                put.save = true;

                if(deletion) {
                    put.revFlags = C4RevisionFlags.Deleted;
                }

                if(ContainsBlob(this)) {
                    put.revFlags |= C4RevisionFlags.HasAttachments;
                }

                if(!deletion && !IsEmpty) {
                    var body = new FLSliceResult();
                    if (model != null) {
                        body = _database.JsonSerializer.Serialize(model);
                        put.body = body;
                    } else { 
                        body = _database.JsonSerializer.Serialize(_dict);
                        put.body = body;
                    }

                    try {
                        using (var type = new C4String(GetString("type"))) {
                            *outDoc = (C4Document*)RetryHandler.RetryIfBusy()
                                .AllowError(new C4Error(LiteCoreError.Conflict))
                                .Execute(err =>
                                {
                                    var localPut = put;
                                    localPut.docType = type.AsC4Slice();
                                    return Native.c4doc_put(_c4Db, &localPut, null, err);
                                });
                        }
                    } finally {
                        Native.FLSliceResult_Free(body);
                    }
                }
            }
           
        }

        private void SetC4Doc(C4Document* doc)
        {
            FLDict* root = null;
            var body = c4Doc->selectedRev.body;
            if(body.size > 0) {
                root = Native.FLValue_AsDict(NativeRaw.FLValue_FromTrustedData(new FLSlice(body.buf, body.size)));
            }

            c4Doc = doc;
            Data = new FleeceDictionary(root, c4Doc, _database);
            _dict = new DictionaryObject(Data);
        }

        #region Overrides

        public override string ToString()
        {
            var id = new SecureLogString(Id, LogMessageSensitivity.PotentiallyInsecure);
            return $"{GetType().Name}[{id}]";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region IDocument

        public void Delete()
        {
            _threadSafety.DoLocked(() => Save(_database.ConflictResolver, true));
        }

        public bool Purge()
        {
            return _threadSafety.DoLocked(() =>
            {
                if(_database == null || _c4Db == null) {
                    throw new InvalidOperationException("Document's owning database has been closed");
                }

                if (!Exists) {
                    return false;
                }

                Database.InBatch(() =>
                {
                    LiteCoreBridge.Check(err => NativeRaw.c4doc_purgeRevision(c4Doc, C4Slice.Null, err));
                    LiteCoreBridge.Check(err => Native.c4doc_save(c4Doc, 0, err));
                });

                LoadDoc(false);
                return true;
            });
        }

        public void Save()
        {
            _threadSafety.DoLocked(() => Save(_database.ConflictResolver, false));
        }

        #endregion

        #region IModellable

        public IDictionaryObject Set(string key, object value)
        {
            _dict.Set(key, value);
            return this;
        }

        public IDictionaryObject Set(IDictionary<string, object> dictionary)
        {
            _dict.Set(dictionary);
            return this;
        }

        public IDictionaryObject Remove(string key)
        {
            _dict.Remove(key);
            return this;
        }

        IArray IDictionaryObject.GetArray(string key)
        {
            return _dict.GetArray(key);
        }

        ISubdocument IDictionaryObject.GetSubdocument(string key)
        {
            return _dict.GetSubdocument(key);
        }

        #endregion
    }
}
