//
// C4Document_native_ios.cs
//
// Copyright (c) 2019 Couchbase, Inc All rights reserved.
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
using System.Linq;
using System.Runtime.InteropServices;

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        public static C4Document* c4doc_get(C4Database* database, string docID, bool mustExist, C4Error* outError)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4doc_get(database, docID_.AsFLSlice(), mustExist, outError);
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4doc_getBySequence(C4Database* database, ulong sequence, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_save(C4Document* doc, uint maxRevTreeDepth, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4doc_free(C4Document* doc);

        public static bool c4doc_selectRevision(C4Document* doc, string revID, bool withBody, C4Error* outError)
        {
            using(var revID_ = new C4String(revID)) {
                return NativeRaw.c4doc_selectRevision(doc, revID_.AsFLSlice(), withBody, outError);
            }
        }

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_selectCurrentRevision(C4Document* doc);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_loadRevisionBody(C4Document* doc, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_hasRevisionBody(C4Document* doc);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_selectParentRevision(C4Document* doc);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_selectNextRevision(C4Document* doc);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_selectNextLeafRevision(C4Document* doc, [MarshalAs(UnmanagedType.U1)]bool includeDeleted, [MarshalAs(UnmanagedType.U1)]bool withBody, C4Error* outError);

        public static bool c4doc_selectFirstPossibleAncestorOf(C4Document* doc, string revID)
        {
            using(var revID_ = new C4String(revID)) {
                return NativeRaw.c4doc_selectFirstPossibleAncestorOf(doc, revID_.AsFLSlice());
            }
        }

        public static bool c4doc_selectNextPossibleAncestorOf(C4Document* doc, string revID)
        {
            using(var revID_ = new C4String(revID)) {
                return NativeRaw.c4doc_selectNextPossibleAncestorOf(doc, revID_.AsFLSlice());
            }
        }

        public static bool c4doc_selectCommonAncestorRevision(C4Document* doc, string rev1ID, string rev2ID)
        {
            using(var rev1ID_ = new C4String(rev1ID))
            using(var rev2ID_ = new C4String(rev2ID)) {
                return NativeRaw.c4doc_selectCommonAncestorRevision(doc, rev1ID_.AsFLSlice(), rev2ID_.AsFLSlice());
            }
        }

        public static uint c4rev_getGeneration(string revID)
        {
            using(var revID_ = new C4String(revID)) {
                return NativeRaw.c4rev_getGeneration(revID_.AsFLSlice());
            }
        }

        public static int c4doc_purgeRevision(C4Document* doc, string revID, C4Error* outError)
        {
            using(var revID_ = new C4String(revID)) {
                return NativeRaw.c4doc_purgeRevision(doc, revID_.AsFLSlice(), outError);
            }
        }

        public static bool c4doc_resolveConflict(C4Document* doc, string winningRevID, string losingRevID, byte[] mergedBody, C4RevisionFlags mergedFlags, C4Error* error)
        {
            using(var winningRevID_ = new C4String(winningRevID))
            using(var losingRevID_ = new C4String(losingRevID))
            fixed(byte *mergedBody_ = mergedBody) {
                return NativeRaw.c4doc_resolveConflict(doc, winningRevID_.AsFLSlice(), losingRevID_.AsFLSlice(), new FLSlice(mergedBody_, mergedBody == null ? 0 : (ulong)mergedBody.Length), mergedFlags, error);
            }
        }

        public static bool c4db_purgeDoc(C4Database* database, string docID, C4Error* outError)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4db_purgeDoc(database, docID_.AsFLSlice(), outError);
            }
        }

        public static bool c4doc_setExpiration(C4Database* db, string docId, long timestamp, C4Error* outError)
        {
            using(var docId_ = new C4String(docId)) {
                return NativeRaw.c4doc_setExpiration(db, docId_.AsFLSlice(), timestamp, outError);
            }
        }

        public static long c4doc_getExpiration(C4Database* db, string docId)
        {
            using(var docId_ = new C4String(docId)) {
                return NativeRaw.c4doc_getExpiration(db, docId_.AsFLSlice());
            }
        }

        public static C4Document* c4doc_put(C4Database *database, C4DocPutRequest *request, ulong* outCommonAncestorIndex, C4Error *outError)
        {
            var uintptr = new UIntPtr();
            var retVal = NativeRaw.c4doc_put(database, request, &uintptr, outError);
            if(outCommonAncestorIndex != null) {
                *outCommonAncestorIndex = uintptr.ToUInt64();
            }

            return retVal;
        }


        public static C4Document* c4doc_create(C4Database* db, string docID, byte[] body, C4RevisionFlags revisionFlags, C4Error* error)
        {
            using(var docID_ = new C4String(docID))
            fixed(byte *body_ = body) {
                return NativeRaw.c4doc_create(db, docID_.AsFLSlice(), new FLSlice(body_, body == null ? 0 : (ulong)body.Length), revisionFlags, error);
            }
        }

        public static C4Document* c4doc_update(C4Document* doc, byte[] revisionBody, C4RevisionFlags revisionFlags, C4Error* error)
        {
            fixed(byte *revisionBody_ = revisionBody) {
                return NativeRaw.c4doc_update(doc, new FLSlice(revisionBody_, revisionBody == null ? 0 : (ulong)revisionBody.Length), revisionFlags, error);
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4doc_get(C4Database* database, FLSlice docID, [MarshalAs(UnmanagedType.U1)]bool mustExist, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_selectRevision(C4Document* doc, FLSlice revID, [MarshalAs(UnmanagedType.U1)]bool withBody, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_selectFirstPossibleAncestorOf(C4Document* doc, FLSlice revID);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_selectNextPossibleAncestorOf(C4Document* doc, FLSlice revID);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_selectCommonAncestorRevision(C4Document* doc, FLSlice rev1ID, FLSlice rev2ID);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint c4rev_getGeneration(FLSlice revID);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern int c4doc_purgeRevision(C4Document* doc, FLSlice revID, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_resolveConflict(C4Document* doc, FLSlice winningRevID, FLSlice losingRevID, FLSlice mergedBody, C4RevisionFlags mergedFlags, C4Error* error);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_purgeDoc(C4Database* database, FLSlice docID, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_setExpiration(C4Database* db, FLSlice docId, long timestamp, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern long c4doc_getExpiration(C4Database* db, FLSlice docId);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4doc_put(C4Database* database, C4DocPutRequest* request, UIntPtr* outCommonAncestorIndex, C4Error* outError);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4doc_create(C4Database* db, FLSlice docID, FLSlice body, C4RevisionFlags revisionFlags, C4Error* error);

        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4doc_update(C4Document* doc, FLSlice revisionBody, C4RevisionFlags revisionFlags, C4Error* error);


    }
}
