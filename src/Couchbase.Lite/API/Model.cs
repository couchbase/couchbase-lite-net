//
//  Model.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite.Serialization;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Lite
{
    public sealed unsafe class ModeledDocument<T> : InteropObject
    {
        public T Item { get; set; }

        public string Type { get; set; }

        public string Id { get; }

        public Database Db { get; }

        public bool IsDeleted { get; private set; }

        private long p_document;
        private C4Document* _document
        {
            get {
                return (C4Document*)p_document;
            }
            set {
                p_document = (long)value;
            }
        }

        internal ModeledDocument(T item, Database db, C4Document* native)
        {
            Db = db;
            Item = item;
            Id = native->docID.CreateString();
            _document = native;
        }

        public bool Save()
        {
            return Save(null, false);
        }

        public bool Delete()
        {
            return Save(null, true);
        }

        private bool Save(IConflictResolver conflictResolver, bool deletion)
        {
            C4Document* newDoc = null;
            var success = Db.InBatch(() =>
            {
                var put = new C4DocPutRequest {
                    docID = _document->docID,
                    history = &_document->revID,
                    historyCount = 1,
                    save = true
                };

                if(deletion) {
                    put.revFlags = C4RevisionFlags.Deleted;
                }

                var body = new FLSliceResult();
                if(!deletion) {
                    using(var writer = new JsonFLValueWriter(Db.c4db)) {
                        ITraceWriter traceWriter = new MemoryTraceWriter();

                        var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings {
                            TraceWriter = traceWriter
                        });
                        serializer.Serialize(writer, Item);
                        Debug.WriteLine(traceWriter);
                        writer.Flush();
                        body = writer.Result;
                        put.body = body;
                    }
                }

                try {
                    using(var type = new C4String(Type)) {
                        newDoc = (C4Document*)LiteCoreBridge.Check(err =>
                        {
                            var localPut = put;
                            localPut.docType = type.AsC4Slice();
                            return Native.c4doc_put(Db.c4db, &localPut, null, err);
                        });
                    }
                } finally {
                    Native.FLSliceResult_Free(body);
                }

                return true;
            });

            if(!success) {
                Native.c4doc_free(newDoc);
                return success;
            }

            _document = newDoc;
            if(deletion) {
                IsDeleted = true;
            }

            return success;
            
        }

        protected override void Dispose(bool finalizing)
        {
            var doc = (C4Document *)Interlocked.Exchange(ref p_document, 0);
            Native.c4doc_free(doc);
        }
    }

    public interface IDocumentModel
    {
    }

    public interface ISubdocumentModel
    {
        Subdocument Subdocument { get; set; }
    }

    public interface IPropertyModel
    {
        Property Property { get; set; }
    }
}
