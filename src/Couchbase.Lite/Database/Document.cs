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
using System.Threading.Tasks;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Serialization;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.DB
{
    internal sealed unsafe class Document : PropertyContainer, IDocument
    {
        #region Constants

        private const string Tag = nameof(Document);

        #endregion

        #region Variables

        private readonly C4Database* _c4Db;
        private readonly Database _database;
        public event EventHandler Changed;

        public event EventHandler<DocumentSavedEventArgs> Saved;
        private C4Document* _c4Doc;
        private IConflictResolver _conflictResolver;

        #endregion

        #region Properties

        public IConflictResolver ConflictResolver
        {
            get {
                AssertSafety();
                return _conflictResolver;
            }
            set {
                AssertSafety();
                _conflictResolver = value;
            }
        }

        public IDatabase Database => _database;

        public bool Exists
        {
            get {
                AssertSafety();
                return _c4Doc->flags.HasFlag(C4DocumentFlags.Exists);
            }
        }

        public string Id { get; }

        public bool IsDeleted
        {
            get {
                AssertSafety();
                return _c4Doc->flags.HasFlag(C4DocumentFlags.Deleted);
            }
        }

        public ulong Sequence => _c4Doc->sequence;

        internal override bool HasChanges
        {
            get => base.HasChanges;
            set {
                base.HasChanges = value;
                _database.SetHasUnsavedChanges(this, value);
            }
        }

        private IConflictResolver EffectiveConflictResolver => ConflictResolver ?? Database.ConflictResolver;

        private uint Generation => NativeRaw.c4rev_getGeneration(_c4Doc->revID);

        #endregion

        #region Constructors

        internal Document(Database db, string docID, bool mustExist)
            : base(db.SharedStrings)
        {
            _database = db;
            Id = docID;
            _c4Db = db.c4db;
            LoadDoc(mustExist);
        }

        internal Document(Database db, C4Document* doc)
            : base(db.SharedStrings)
        {
            _database = db;
            Id = doc->docID.CreateString();
            _c4Db = db.c4db;
            SetC4Doc(doc);
        }

        ~Document()
        {
            Dispose(false);
        }

        #endregion

        #region Internal Methods

        internal void ChangedExternally()
        {
            // The current API design decision is that when a document has unsaved changes, it should
            // not update with external changes and should not post notifications.  Instead the conflict
            // resolution will happen when the app saves the document
            AssertSafety();
            if(!HasChanges) {
                try {
                    LoadDoc(true);
                } catch(Exception e) {
                    Log.To.Database.W(Tag, $"{this} failed to load external changes", e);
                }

                PostChangedNotifications(true);
            }
        }

        internal void PostChangedNotifications(bool external)
        {
            Saved?.Invoke(this, new DocumentSavedEventArgs(external));
        }

        #endregion

        #region Private Methods

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
            HasChanges = false;
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
            if(!HasChanges && !deletion && Exists) {
                return;
            }

            C4Document* newDoc = null;
            var endedEarly = false;
            var success = Database.InBatch(() =>
            {
                var tmp = default(C4Document*);
                SaveInto(&tmp, deletion, model);
                if (tmp == null) {
                    Merge(resolver, deletion);
                    if (!HasChanges) {
                        endedEarly = true;
                        return false;
                    }

                    SaveInto(&tmp, deletion, model);
                    if (tmp == null) {
                        throw new CouchbaseLiteException("Conflict still occuring after resolution", StatusCode.DbError);
                    }
                }

                newDoc = tmp;
                return true;
            });

            if (endedEarly) {
                return;
            }

            if(!success) {
                Native.c4doc_free(newDoc);
                return;
            }

            SetC4Doc(newDoc);
            if(deletion) {
                Properties = null;
            }

            ResetChangesKeys();
            PostChangedNotifications(false);
        }

        [SuppressMessage("ReSharper", "AccessToDisposedClosure", Justification = "The closure is executed synchronously")]
        private void SaveInto(C4Document** outDoc, bool deletion, IDocumentModel model = null)
        {
            //TODO: Need to be able to save a deletion that has properties on it
            var propertiesToSave = deletion ? null : _properties;
            var put = new C4DocPutRequest {
                docID = _c4Doc->docID,
                history = &_c4Doc->revID,
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
            if (model != null) {
                body = _database.JsonSerializer.Serialize(model);
                put.body = body;
            } else if (propertiesToSave?.Count > 0) {
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
                        return Native.c4doc_put(_c4Db, &localPut, null, err);
                    });
                }
            } finally {
                Native.FLSliceResult_Free(body);
            }
        }

        private void SetC4Doc(C4Document* doc)
        {
            Native.c4doc_free(_c4Doc);
            _c4Doc = doc;
            SetRootDict(null);
            if(doc != null) {
                var body = doc->selectedRev.body;
                if(body.size > 0) {
                    var root = Native.FLValue_AsDict(NativeRaw.FLValue_FromTrustedData(new FLSlice(body.buf, body.size)));
                    SetRootDict(root);
                }
            }

            UseNewRoot();
        }

        #endregion

        #region Overrides

        protected internal override IBlob CreateBlob(IDictionary<string, object> properties)
        {
            AssertSafety();
            return new Blob(_database, properties) {
                CheckThreadSafety = CheckThreadSafety
            };
        }

        internal override void MarkChangedKey(string key)
        {
            base.MarkChangedKey(key);
            Changed?.Invoke(this, null);
        }

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
            AssertSafety();
            Save(EffectiveConflictResolver, true);
        }

        public bool Purge()
        {
            AssertSafety();
            if(!Exists) {
                return false;
            }

            Database.InBatch(() =>
            {
                LiteCoreBridge.Check(err => NativeRaw.c4doc_purgeRevision(_c4Doc, C4Slice.Null, err));
                LiteCoreBridge.Check(err => Native.c4doc_save(_c4Doc, 0, err));

                return true;
            });

            LoadDoc(false);
            ResetChangesKeys();
            return true;
        }

        public void Save()
        {
            AssertSafety();
            Save(EffectiveConflictResolver, false);
        }

        public new IDocument Set(string key, object value)
        {
            base.Set(key, value);
            return this;
        }

        #endregion

        #region IModellable

        public T AsModel<T>() where T : IDocumentModel, new()
        {
            FLValue* value = NativeRaw.FLValue_FromTrustedData((FLSlice)_c4Doc->selectedRev.body);
            var retVal = _database.JsonSerializer.Deserialize<T>(value);
            retVal.Document = this;
            return retVal;
        }

        public void Set(IDocumentModel model)
        {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }

            HasChanges = true;
            Save(EffectiveConflictResolver, false, model);
        }

        #endregion
    }
}
