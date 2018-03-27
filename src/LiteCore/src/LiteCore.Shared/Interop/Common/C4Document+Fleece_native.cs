//
// C4Document+Fleece_native.cs
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
        public static bool c4doc_isOldMetaProperty(string prop)
        {
            using(var prop_ = new C4String(prop)) {
                return NativeRaw.c4doc_isOldMetaProperty(prop_.AsC4Slice());
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_hasOldMetaProperties(FLDict* doc);

        public static byte[] c4doc_encodeStrippingOldMetaProperties(FLDict* doc)
        {
            using(var retVal = NativeRaw.c4doc_encodeStrippingOldMetaProperties(doc)) {
                return ((C4Slice)retVal).ToArrayFast();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_dictIsBlob(FLDict* dict, FLSharedKeys* sk, C4BlobKey* outKey);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_dictContainsBlobs(FLDict* dict, FLSharedKeys* sk);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_blobIsCompressible(FLDict* blobDict, FLSharedKeys* sk);

        public static string c4doc_bodyAsJSON(C4Document* doc, bool canonical, C4Error* outError)
        {
            using(var retVal = NativeRaw.c4doc_bodyAsJSON(doc, canonical, outError)) {
                return ((C4Slice)retVal).CreateString();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLEncoder* c4db_createFleeceEncoder(C4Database* db);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLEncoder* c4db_getSharedFleeceEncoder(C4Database* db);

        public static byte[] c4db_encodeJSON(C4Database* db, string jsonData, C4Error* outError)
        {
            using(var jsonData_ = new C4String(jsonData)) {
                using(var retVal = NativeRaw.c4db_encodeJSON(db, jsonData_.AsC4Slice(), outError)) {
                    return ((C4Slice)retVal).ToArrayFast();
                }
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSharedKeys* c4db_getFLSharedKeys(C4Database* db);

        // Note: Allocates unmanaged heap memory; should only be used with constants
        public static FLDictKey c4db_initFLDictKey(C4Database* db, string str)
        {
            return NativeRaw.c4db_initFLDictKey(db, C4Slice.Constant(str));
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4doc_isOldMetaProperty(C4Slice prop);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4doc_encodeStrippingOldMetaProperties(FLDict* doc);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4doc_bodyAsJSON(C4Document* doc, [MarshalAs(UnmanagedType.U1)]bool canonical, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4db_encodeJSON(C4Database* db, C4Slice jsonData, C4Error* outError);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDictKey c4db_initFLDictKey(C4Database* db, C4Slice @string);


    }
}
