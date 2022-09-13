//
// C4Collection_native.cs
//
// Copyright (c) 2022 Couchbase, Inc All rights reserved.
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
using System.Runtime.InteropServices;

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Collection* c4db_getDefaultCollection(C4Database* db, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_hasCollection(C4Database* db, C4CollectionSpec spec);

        public static bool c4db_hasScope(C4Database* db, string name)
        {
            using(var name_ = new C4String(name)) {
                return NativeRaw.c4db_hasScope(db, name_.AsFLSlice());
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Collection* c4db_getCollection(C4Database* db, C4CollectionSpec spec, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Collection* c4db_createCollection(C4Database* db, C4CollectionSpec spec, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool c4db_deleteCollection(C4Database* db, C4CollectionSpec spec, C4Error* outError);

        public static FLMutableArray* c4db_collectionNames(C4Database* db, string inScope, C4Error* outError)
        {
            using(var inScope_ = new C4String(inScope)) {
                return NativeRaw.c4db_collectionNames(db, inScope_.AsFLSlice(), outError);
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableArray* c4db_scopeNames(C4Database* db, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4coll_isValid(C4Collection* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4CollectionSpec c4coll_getSpec(C4Collection* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Database* c4coll_getDatabase(C4Collection* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong c4coll_getDocumentCount(C4Collection* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong c4coll_getLastSequence(C4Collection* x);

        public static C4Document* c4coll_getDoc(C4Collection* collection, string docID, bool mustExist, C4DocContentLevel content, C4Error* outError)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4coll_getDoc(collection, docID_.AsFLSlice(), mustExist, content, outError);
            }
        }

        public static C4Document* c4coll_putDoc(C4Collection* collection, C4DocPutRequest* request, UIntPtr* outCommonAncestorIndex, C4Error* outError)
        {
            return NativeRaw.c4coll_putDoc(collection, request, outCommonAncestorIndex, outError);
        }

        public static C4Document* c4coll_createDoc(C4Collection* collection, string docID, byte[] body, C4RevisionFlags revisionFlags, C4Error* error)
        {
            using(var docID_ = new C4String(docID))
            fixed(byte *body_ = body) {
                return NativeRaw.c4coll_createDoc(collection, docID_.AsFLSlice(), new FLSlice(body_, body == null ? 0 : (ulong)body.Length), revisionFlags, error);
            }
        }

        public static bool c4coll_moveDoc(C4Collection* collection, string docID, C4Collection* toCollection, string newDocID, C4Error* error)
        {
            using(var docID_ = new C4String(docID))
            using(var newDocID_ = new C4String(newDocID)) {
                return NativeRaw.c4coll_moveDoc(collection, docID_.AsFLSlice(), toCollection, newDocID_.AsFLSlice(), error);
            }
        }

        public static bool c4coll_purgeDoc(C4Collection* collection, string docID, C4Error* outError)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4coll_purgeDoc(collection, docID_.AsFLSlice(), outError);
            }
        }

        public static bool c4coll_setDocExpiration(C4Collection* collection, string docID, long timestamp, C4Error* outError)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4coll_setDocExpiration(collection, docID_.AsFLSlice(), timestamp, outError);
            }
        }

        public static long c4coll_getDocExpiration(C4Collection* collection, string docID, C4Error* outError)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4coll_getDocExpiration(collection, docID_.AsFLSlice(), outError);
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long c4coll_nextDocExpiration(C4Collection* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long c4coll_purgeExpiredDocs(C4Collection* x, C4Error* outError);

        public static bool c4coll_createIndex(C4Collection* collection, string name, string indexSpec, C4QueryLanguage queryLanguage, C4IndexType indexType, C4IndexOptions* indexOptions, C4Error* outError)
        {
            using(var name_ = new C4String(name))
            using(var indexSpec_ = new C4String(indexSpec)) {
                return NativeRaw.c4coll_createIndex(collection, name_.AsFLSlice(), indexSpec_.AsFLSlice(), queryLanguage, indexType, indexOptions, outError);
            }
        }

        public static bool c4coll_deleteIndex(C4Collection* collection, string name, C4Error* outError)
        {
            using(var name_ = new C4String(name)) {
                return NativeRaw.c4coll_deleteIndex(collection, name_.AsFLSlice(), outError);
            }
        }

        public static byte[] c4coll_getIndexesInfo(C4Collection* collection, C4Error* outError)
        {
            using(var retVal = NativeRaw.c4coll_getIndexesInfo(collection, outError)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_hasScope(C4Database* db, FLSlice name);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLMutableArray* c4db_collectionNames(C4Database* db, FLSlice inScope, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4coll_getDoc(C4Collection* collection, FLSlice docID, [MarshalAs(UnmanagedType.U1)]bool mustExist, C4DocContentLevel content, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4coll_putDoc(C4Collection* collection, C4DocPutRequest* request, UIntPtr* outCommonAncestorIndex, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4coll_createDoc(C4Collection* collection, FLSlice docID, FLSlice body, C4RevisionFlags revisionFlags, C4Error* error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4coll_moveDoc(C4Collection* collection, FLSlice docID, C4Collection* toCollection, FLSlice newDocID, C4Error* error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4coll_purgeDoc(C4Collection* collection, FLSlice docID, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4coll_setDocExpiration(C4Collection* collection, FLSlice docID, long timestamp, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long c4coll_getDocExpiration(C4Collection* collection, FLSlice docID, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4coll_createIndex(C4Collection* collection, FLSlice name, FLSlice indexSpec, C4QueryLanguage queryLanguage, C4IndexType indexType, C4IndexOptions* indexOptions, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4coll_deleteIndex(C4Collection* collection, FLSlice name, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4coll_getIndexesInfo(C4Collection* collection, C4Error* outError);


    }
}
