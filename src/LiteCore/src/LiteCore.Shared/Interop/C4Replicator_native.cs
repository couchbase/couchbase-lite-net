//
// C4Replicator_native.cs
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
using System.Linq;
using System.Runtime.InteropServices;

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Replicator* c4repl_new(C4Database* db, C4Address remoteAddress, FLSlice remoteDatabaseName, C4ReplicatorParameters @params, C4Error* outError);
        
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Replicator* c4repl_newLocal(C4Database* db, C4Database* otherLocalDB, C4ReplicatorParameters @params, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Replicator* c4repl_newWithSocket(C4Database* db, C4Socket* openSocket, C4ReplicatorParameters @params, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4repl_start(C4Replicator* repl, [MarshalAs(UnmanagedType.U1)]bool reset);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4repl_stop(C4Replicator* repl);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4repl_retry(C4Replicator* repl, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4repl_setHostReachable(C4Replicator* repl, [MarshalAs(UnmanagedType.U1)]bool reachable);

        public static void c4repl_setOptions(C4Replicator* repl, byte[] optionsDictFleece)
        {
            fixed(byte *optionsDictFleece_ = optionsDictFleece) {
                NativeRaw.c4repl_setOptions(repl, new FLSlice(optionsDictFleece_, optionsDictFleece == null ? 0 : (ulong)optionsDictFleece.Length));
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4ReplicatorStatus c4repl_getStatus(C4Replicator* repl);

        public static byte[] c4repl_getPendingDocIDs(C4Replicator* repl, C4CollectionSpec spec, C4Error* outErr)
        {
            using(var retVal = NativeRaw.c4repl_getPendingDocIDs(repl, spec, outErr)) {
                return ((FLSlice)retVal).ToArrayFast();
            }
        }

        public static bool c4repl_isDocumentPending(C4Replicator* repl, string docID, C4CollectionSpec spec, C4Error* outErr)
        {
            using(var docID_ = new C4String(docID)) {
                return NativeRaw.c4repl_isDocumentPending(repl, docID_.AsFLSlice(), spec, outErr);
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4repl_setProgressLevel(C4Replicator* repl, C4ReplicatorProgressLevel level, C4Error* outErr);

        public static bool c4db_setCookie(C4Database* db, string setCookieHeader, string fromHost, string fromPath, C4Error* outError)
        {
            using(var setCookieHeader_ = new C4String(setCookieHeader))
            using(var fromHost_ = new C4String(fromHost))
            using(var fromPath_ = new C4String(fromPath)) {
                return NativeRaw.c4db_setCookie(db, setCookieHeader_.AsFLSlice(), fromHost_.AsFLSlice(), fromPath_.AsFLSlice(), outError);
            }
        }

        public static string c4db_getCookies(C4Database* db, C4Address request, C4Error* error)
        {
            using(var retVal = NativeRaw.c4db_getCookies(db, request, error)) {
                return ((FLSlice)retVal).CreateString();
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4address_fromURL(FLSlice url, C4Address* address, FLSlice* dbName);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4repl_setOptions(C4Replicator* repl, FLSlice optionsDictFleece);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4repl_getPendingDocIDs(C4Replicator* repl, C4CollectionSpec spec, C4Error* outErr);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4repl_isDocumentPending(C4Replicator* repl, FLSlice docID, C4CollectionSpec spec, C4Error* outErr);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_setCookie(C4Database* db, FLSlice setCookieHeader, FLSlice fromHost, FLSlice fromPath, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4db_getCookies(C4Database* db, C4Address request, C4Error* error);


    }
}
