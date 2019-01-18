//
// C4Document+Fleece_native.cs
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
        public static bool c4doc_isOldMetaProperty(string prop)
        {
            using(var prop_ = new C4String(prop)) {
                return NativeRaw.c4doc_isOldMetaProperty(prop_.AsFLSlice());
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_dictContainsBlobs(FLDict* dict);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLEncoder* c4db_getSharedFleeceEncoder(C4Database* db);

        public static byte[] c4db_encodeJSON(C4Database* db, string jsonData, C4Error* outError)
        {
            using(var jsonData_ = new C4String(jsonData)) {
                using(var retVal = NativeRaw.c4db_encodeJSON(db, jsonData_.AsFLSlice(), outError)) {
                    return ((FLSlice)retVal).ToArrayFast();
                }
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSharedKeys* c4db_getFLSharedKeys(C4Database* db);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_isOldMetaProperty(FLSlice prop);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4db_encodeJSON(C4Database* db, FLSlice jsonData, C4Error* outError);


    }
}
