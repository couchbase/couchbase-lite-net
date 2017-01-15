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
using System.Threading;

using LiteCore;
using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    public sealed class DocumentChangedEventArgs : ComponentChangedEventArgs<Document>
    {
        
    }

    public sealed unsafe class Document : PropertyContainer, IDisposable // ,IPropertyContainer, IModellable
    {
        public event EventHandler<DocumentChangedEventArgs> Changed;

        public event EventHandler<PropertyChangedEventArgs> PropertyChanged;

        public Database Database { get; }

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

        private long p_c4db;
        private C4Database* _c4db
        {
            get {
                return (C4Database*)p_c4db;
            }
            set {
                p_c4db = (long)value;
            }
        }

        private long p_c4doc;
        private C4Document* _c4doc
        {
            get {
                return (C4Document*)p_c4doc;
            }
            set {
                p_c4doc = (long)value;
            }
        }

        internal Document(Database db, string docID, bool mustExist)
        {
            Database = db;
            Id = docID;
            _c4db = db.c4db;
            LoadDoc(mustExist);
        }

        ~Document()
        {
            Dispose(false);
        }

        public bool Save()
        {
            return Save(null, false);
        }

        public bool Delete()
        {
            return Save(null, true);
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

        public void Reset()
        {
            ResetChanges();
        }

        private void LoadDoc(bool mustExist)
        {
            var doc = (C4Document *)LiteCoreBridge.Check(err => Native.c4doc_get(_c4db, Id, mustExist, err));
            SetC4Doc(doc);
        }

        private void SetC4Doc(C4Document* doc)
        {
            var oldDoc = Interlocked.Exchange(ref p_c4doc, (long)doc);
            Native.c4doc_free((C4Document *)oldDoc);
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
            var oldDoc = Interlocked.Exchange(ref p_c4doc, 0);
            Native.c4doc_free((C4Document *)oldDoc);
        }

        private bool Save(object resolver, bool deletion)
        {
            if(!HasChanges && !deletion && Exists) {
                return false;
            }

            C4Document* newDoc = null;
            var success = Database.InBatch(() =>
            {
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

                var body = new FLSliceResult();
                if(propertiesToSave?.Count > 0) {
                    using(var writer = new JsonFLValueWriter(_c4db)) {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(writer, propertiesToSave);
                        writer.Flush();
                        body = writer.Result;
                        put.body = body;
                    }
                }

                try {
                    newDoc = (C4Document*)LiteCoreBridge.Check(err =>
                    {
                        var localPut = put;
                        return Native.c4doc_put(_c4db, &localPut, null, err);
                    });
                } finally {
                    Native.FLSliceResult_Free(body);
                }

                return true;
            });

            if(!success) {
                Native.c4doc_free(newDoc);
                return success;
            }

            SetC4Doc(newDoc);
            if(deletion) {
                ResetChanges();
            }

            return success;
        }

        public override string ToString()
        {
            return $"{GetType().Name}[{Id}]";
        }

        protected override FLSharedKeys* GetSharedKeys()
        {
            return Native.c4db_getFLSharedKeys(_c4db);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
