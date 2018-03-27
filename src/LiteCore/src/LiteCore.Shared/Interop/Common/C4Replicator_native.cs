//
// C4Replicator_native.cs
//
// Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
        public static bool c4repl_isValidDatabaseName(string dbName)
        {
            using(var dbName_ = new C4String(dbName)) {
                return NativeRaw.c4repl_isValidDatabaseName(dbName_.AsC4Slice());
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Replicator* c4repl_new(C4Database* db, C4Address remoteAddress, C4Slice remoteDatabaseName, C4Database* otherLocalDB, C4ReplicatorParameters @params, C4Error* err);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4repl_free(C4Replicator* repl);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4repl_stop(C4Replicator* repl);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4ReplicatorStatus c4repl_getStatus(C4Replicator* repl);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Slice c4repl_getResponseHeaders(C4Replicator* repl);

        public static bool c4db_setCookie(C4Database* db, string setCookieHeader, string fromHost, string fromPath, C4Error* outError)
        {
            using(var setCookieHeader_ = new C4String(setCookieHeader))
            using(var fromHost_ = new C4String(fromHost))
            using(var fromPath_ = new C4String(fromPath)) {
                return NativeRaw.c4db_setCookie(db, setCookieHeader_.AsC4Slice(), fromHost_.AsC4Slice(), fromPath_.AsC4Slice(), outError);
            }
        }

        public static string c4db_getCookies(C4Database* db, C4Address request, C4Error* error)
        {
            using(var retVal = NativeRaw.c4db_getCookies(db, request, error)) {
                return ((C4Slice)retVal).CreateString();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4db_clearCookies(C4Database* db);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4repl_isValidDatabaseName(C4Slice dbName);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_setCookie(C4Database* db, C4Slice setCookieHeader, C4Slice fromHost, C4Slice fromPath, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4db_getCookies(C4Database* db, C4Address request, C4Error* error);


    }
}
