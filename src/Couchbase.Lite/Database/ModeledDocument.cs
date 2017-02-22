// 
//  ModeledDocument.cs
// 
//  Author:
//  Jim Borden  <jim.borden@couchbase.com>
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

using Couchbase.Lite.Support;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.DB
{
    internal sealed unsafe class ModeledDocument<T> : ThreadSafe, IModeledDocument<T> where T : class, new()
    {
        #region Variables

        private Database _db;

        private C4Document* _document;
        private bool _isDeleted;
        private T _item;
        private string _type;

        #endregion

        #region Properties

        public IDatabase Db
        {
            get {
                return _db;
            }
        }

        public string Id { get; private set; }

        public bool IsDeleted
        {
            get {
                AssertSafety();
                return _isDeleted;
            }
            private set {
                AssertSafety();
                _isDeleted = value;
            }
        }

        public T Item
        {
            get {
                AssertSafety();
                return _item;
            }
            set {
                AssertSafety();
                _item = value;
            }
        }

        public ulong Sequence { get; private set; }

        public string Type
        {
            get {
                AssertSafety();
                return _type;
            }
            set {
                AssertSafety();
                _type = value;
            }
        }

        #endregion

        #region Constructors

        internal ModeledDocument(Database db, C4Document* native)
            : this(Activator.CreateInstance<T>(), db, native)
        {
            
        }

        internal ModeledDocument(T item, Database db, C4Document* native)
        {
            _item = item;
            Reconstruct(db, native);
        }

        #endregion

        #region Internal Methods

        internal void Reconstruct(Database db, C4Document* native)
        {
            _db = db;
            Id = native->docID.CreateString();
            Sequence = native->sequence;
            _document = native;
        }

        #endregion

        #region Private Methods

        private void Save(IConflictResolver conflictResolver, bool deletion)
        {
            C4Document* newDoc = null;
            PerfTimer.StartEvent("Save_DispatchSync");
            var success = Db.ActionQueue.DispatchSync(() => {
                PerfTimer.StopEvent("Save_DispatchSync");
                PerfTimer.StartEvent("Save_BeginInBatch");
                return Db.InBatch(() => {
                    PerfTimer.StopEvent("Save_BeginInBatch");
                    var put = new C4DocPutRequest {
                        docID = _document->docID,
                        history = &_document->revID,
                        historyCount = 1,
                        save = true
                    };

                    if(deletion) {
                        put.revFlags = C4RevisionFlags.Deleted;
                    }

                    var body = default(FLSliceResult);
                    if(!deletion) {
                        PerfTimer.StartEvent("Save_Serialize");
                        body = _db.JsonSerializer.Serialize(Item);
                        PerfTimer.StopEvent("Save_Serialize");
                        put.body = body;
                    }

                    try {
                        using(var type = new C4String(Type)) {
                            PerfTimer.StartEvent("Save_c4doc_put");
                            newDoc = (C4Document*)LiteCoreBridge.Check(err => {
                                var localPut = put;
                                localPut.docType = type.AsC4Slice();
                                return Native.c4doc_put(_db.c4db, &localPut, null, err);
                            });
                            PerfTimer.StopEvent("Save_c4doc_put");
                        }
                    } finally {
                        body.Dispose();
                    }

                    return true;
                });
            });

            if(!success) {
                Native.c4doc_free(newDoc);
                return;
            }

            PerfTimer.StartEvent("Save_c4doc_free");
            Native.c4doc_free(_document);
            PerfTimer.StopEvent("Save_c4doc_free");
            _document = newDoc;
            if(deletion) {
                IsDeleted = true;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Native.c4doc_free(_document);
            _document = null;
        }

        #endregion

        #region IModeledDocument<T>

        public void Delete()
        {
            AssertSafety();
            Save(null, true);
        }

        public void Save()
        {
            AssertSafety();
            Save(null, false);
        }

        #endregion
    }
}
