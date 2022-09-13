//
// C4Database_native.cs
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
        public static C4Database* c4db_openNamed(string name, C4DatabaseConfig2* config, C4Error* outError)
        {
            using(var name_ = new C4String(name)) {
                return NativeRaw.c4db_openNamed(name_.AsFLSlice(), config, outError);
            }
        }

        public static bool c4db_copyNamed(string sourcePath, string destinationName, C4DatabaseConfig2* config, C4Error* error)
        {
            using (var sourcePath_ = new C4String(sourcePath))
            using (var destinationName_ = new C4String(destinationName)) {
                return NativeRaw.c4db_copyNamed(sourcePath_.AsFLSlice(), destinationName_.AsFLSlice(), config, error);
            }
        }

        public static bool c4db_deleteNamed(string dbName, string inDirectory, C4Error* outError)
        {
            using (var dbName_ = new C4String(dbName))
            using (var inDirectory_ = new C4String(inDirectory)) {
                return NativeRaw.c4db_deleteNamed(dbName_.AsFLSlice(), inDirectory_.AsFLSlice(), outError);
            }
        }

        public static string c4db_getPath(C4Database* db)
        {
            using (var retVal = NativeRaw.c4db_getPath(db)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static C4RawDocument* c4raw_get(C4Database* database, string storeName, string docID, C4Error* outError)
        {
            using (var storeName_ = new C4String(storeName))
            using (var docID_ = new C4String(docID)) {
                return NativeRaw.c4raw_get(database, storeName_.AsFLSlice(), docID_.AsFLSlice(), outError);
            }
        }

        public static bool c4raw_put(C4Database* database, string storeName, string key, string meta, string body, C4Error* outError)
        {
            using (var storeName_ = new C4String(storeName))
            using (var key_ = new C4String(key))
            using (var meta_ = new C4String(meta))
            using (var body_ = new C4String(body)) {
                return NativeRaw.c4raw_put(database, storeName_.AsFLSlice(), key_.AsFLSlice(), meta_.AsFLSlice(), body_.AsFLSlice(), outError);
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_close(C4Database* database, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_delete(C4Database* database, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_rekey(C4Database* database, C4EncryptionKey* newKey, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4_shutdown(C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4DatabaseConfig2* c4db_getConfig2(C4Database* database);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_getUUIDs(C4Database* database, C4UUID* publicUUID, C4UUID* privateUUID, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4db_setExtraInfo(C4Database* database, C4ExtraInfo x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4ExtraInfo c4db_getExtraInfo(C4Database* database);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_maintenance(C4Database* database, C4MaintenanceType type, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_beginTransaction(C4Database* database, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_endTransaction(C4Database* database, [MarshalAs(UnmanagedType.U1)]bool commit, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_isInTransaction(C4Database* database);

    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Database* c4db_openNamed(FLSlice name, C4DatabaseConfig2* config, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_copyNamed(FLSlice sourcePath, FLSlice destinationName, C4DatabaseConfig2* config, C4Error* error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4db_deleteNamed(FLSlice dbName, FLSlice inDirectory, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4db_getPath(C4Database* db);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4RawDocument* c4raw_get(C4Database* database, FLSlice storeName, FLSlice docID, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4raw_put(C4Database* database, FLSlice storeName, FLSlice key, FLSlice meta, FLSlice body, C4Error* outError);


    }
}
