//
//  Document.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using System.Collections.ObjectModel;
using Couchbase.Lite.Serialization;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    public sealed class DocumentChangedEventArgs : ComponentChangedEventArgs<IDocument>
    {
        
    }

    public interface IDocument : IPropertyContainer, IDisposable
    {
        event EventHandler<DocumentChangedEventArgs> Changed;

        event EventHandler<PropertyChangedEventArgs> PropertyChanged;

        IDatabase Database { get; }

        IConflictResolver ConflictResolver { get; set; }

        string Id { get; }

        bool IsDeleted { get; }

        bool Exists { get; }

        ulong Sequence { get; }

        new IDocument Set(string key, object value);

        void Save();

        void Delete();

        bool Purge();

        void Revert();
    }

    internal sealed unsafe class Document : PropertyContainer, IDocument //, IModellable
    {
        private C4Database* _c4db;
        private C4Document* _c4doc;
        private readonly Database _database;

        public event EventHandler<DocumentChangedEventArgs> Changed;

        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;

        public IDatabase Database
        {
            get {
                return _database;
            }
        }

        public IConflictResolver ConflictResolver { get; set; }

        public string Id { get; }

        public bool IsDeleted
        {
            get {
                return _c4doc->flags.HasFlag(C4DocumentFlags.Deleted);
            }
        }

        public bool Exists
        {
            get {
                return _c4doc->flags.HasFlag(C4DocumentFlags.Exists);
            }
        }

        public ulong Sequence { get; }

        private uint Generation
        {
            get {
                return NativeRaw.c4rev_getGeneration(_c4doc->revID);
            }
        }

        private IConflictResolver EffectiveConflictResolver
        {
            get {
                return ConflictResolver ?? Database.ConflictResolver;
            }
        }
        
        internal Document(Database db, string docID, bool mustExist)
        {
            _database = db;
            Id = docID;
            _c4db = db.c4db;
            LoadDoc(mustExist);
        }

        ~Document()
        {
            Dispose(false);
        }

        public new IDocument Set(string key, object value)
        {
            base.Set(key, value);
            return this;
        }

        public void Save()
        {
            Save(EffectiveConflictResolver, false);
        }

        public void Delete()
        {
            Save(EffectiveConflictResolver, true);
        }

        public bool Purge()
        {
            if(!Exists) {
                return false;
            }

            var success = Database.InBatch(() =>
            {
                LiteCoreBridge.Check(err => NativeRaw.c4doc_purgeRevision(_c4doc, C4Slice.Null, err));
                LiteCoreBridge.Check(err => Native.c4doc_save(_c4doc, 0, err));

                return true;
            });

            if(!success) {
                return false;
            }

            LoadDoc(false);
            ResetChanges();
            return true;
        }

        public void Revert()
        {
            ResetChanges();
        }

        protected internal override IBlob CreateBlob(IDictionary<string, object> properties)
        {
            return new Blob(_database, properties);
        }

        private void LoadDoc(bool mustExist)
        {
            var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(_c4db, Id, mustExist, err));
            SetC4Doc(doc);
        }

        private void SetC4Doc(C4Document* doc)
        {
            Native.c4doc_free(_c4doc);
            _c4doc = doc;
            SetRoot(null, null);
            if(doc != null) {
                var body = doc->selectedRev.body;
                if(body.size > 0) {
                    var root = Native.FLValue_AsDict(NativeRaw.FLValue_FromTrustedData(new FLSlice(body.buf, body.size)));
                    SetRoot(root, null);
                }
            }
        }

        private void Dispose(bool disposing)
        {
            Native.c4doc_free(_c4doc);
            _c4doc = null;
        }

        private void Save(IConflictResolver resolver, bool deletion)
        {
            if(!HasChanges && !deletion && Exists) {
                return;
            }

            C4Document* newDoc = null;
            var endedEarly = false;
            var success = Database.InBatch(() =>
            {
                var tmp = default(C4Document*);
                SaveInto(&tmp, deletion);
                if(tmp == null) {
                    Merge(resolver, deletion);
                    if(!HasChanges) {
                        endedEarly = true;
                        return false;
                    }

                    SaveInto(&tmp, deletion);
                    if(tmp == null) {
                        throw new CouchbaseLiteException("Conflict still occuring after resolution", StatusCode.DbError);
                    }
                }

                newDoc = tmp;
                return true;
            });

            if(endedEarly) {
                return;
            }

            if(!success) {
                Native.c4doc_free(newDoc);
                return;
            }

            SetC4Doc(newDoc);
            if(deletion) {
                ResetChanges();
            }
        }

        private void Merge(IConflictResolver resolver, bool deletion)
        {
            var currentDoc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(_c4db, Id, true, err));
            var currentData = currentDoc->selectedRev.body;
            var current = default(IDictionary<string, object>);
            if(currentData.size > 0) {
                var currentRoot = NativeRaw.FLValue_FromTrustedData((FLSlice)currentData);
                current = FLValueConverter.ToObject(currentRoot, this, GetSharedStrings()) as IDictionary<string, object>;
            }

            var resolved = default(IDictionary<string, object>);
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
                uint myGgggeneration = Generation + 1;
                uint theirGgggeneration = NativeRaw.c4rev_getGeneration(currentDoc->revID);
                if(myGgggeneration >= theirGgggeneration) { // hope I die before I get old
                    resolved = Properties;
                } else {
                    resolved = current;
                }
            }

            SetC4Doc(currentDoc);
            Properties = resolved;
            if(resolved.Equals(current)) {
                HasChanges = false;
            }
        }

        private void SaveInto(C4Document** outDoc, bool deletion)
        {
            //TODO: Need to be able to save a deletion that has properties on it
            var propertiesToSave = deletion ? null : Properties;
            var put = new C4DocPutRequest {
                docID = _c4doc->docID,
                history = &_c4doc->revID,
                historyCount = 1,
                save = true
            };

            if(deletion) {
                put.revFlags = C4RevisionFlags.Deleted;
            }

            if(ContainsBlob(propertiesToSave)) {
                put.revFlags |= C4RevisionFlags.HasAttachments;
            }

            var body = new FLSliceResult();
            if(propertiesToSave?.Count > 0) {
                body = _database.JsonSerializer.Serialize(propertiesToSave);
                put.body = body;
            }

            try {
                using(var type = new C4String(this["type"] as string)) {
                    *outDoc = (C4Document*)RetryHandler.RetryIfBusy()
                        .AllowError(new C4Error(LiteCoreError.Conflict))
                        .Execute(err =>
                    {
                        var localPut = put;
                        localPut.docType = type.AsC4Slice();
                        return Native.c4doc_put(_c4db, &localPut, null, err);
                    });
                }
            } finally {
                Native.FLSliceResult_Free(body);
            }
        }

        private static bool ContainsBlob(IDictionary<string, object> dict)
        {
            if(dict == null) {
                return false;
            }

            foreach(var obj in dict.Values) {
                if(ContainsBlob(obj)) {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsBlob(object obj)
        {
            if(obj == null) {
                return false;
            }

            var blob = obj as IBlob;
            if(blob != null) {
                return true;
            }

            var dict = obj as IDictionary<string, object>;
            if(dict != null) {
                return ContainsBlob(dict);
            }

            var arr = obj as IList;
            if(arr != null) {
                return ContainsBlob(arr);
            }

            return false;
        }

        private static bool ContainsBlob(IList list)
        {
            if(list == null) {
                return false;
            }

            foreach(var obj in list) {
                if(ContainsBlob(obj)) {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return $"{GetType().Name}[{Id}]";
        }

        internal override SharedStringCache GetSharedStrings()
        {
            return _database.SharedStrings;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
